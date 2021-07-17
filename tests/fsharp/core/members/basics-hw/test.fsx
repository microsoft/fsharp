// #Regression #Conformance #SignatureFiles #Classes #ObjectConstructors #ObjectOrientedTypes #Fields #MemberDefinitions #MethodsAndProperties #Unions #InterfacesAndImplementations #Events #Overloading #Recursion #Regression 

#light

#if TESTS_AS_APP
module Core_members_basics
#else
module Tests
#endif


let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

let test (s : string) b = 
    stderr.Write(s)
    if b then stderr.WriteLine " OK"
    else report_failure (s)

let check s v1 v2 = test s (v1 = v2)

//--------------------------------------------------------------
// Test defining a record using object-expression syntax

type RecordType = { a: int; mutable b: int }

let rval = { new RecordType with a = 1 and b = 2 }
let rvalaaa2 = { new RecordType 
                        with a = 1 
                        and b = 2 }

test "fweoew091" (rval.a = 1)
test "fweoew092" (rval.b = 2)
rval.b <- 3
test "fweoew093" (rval.b = 3)

type RecordType2<'a,'b> = { a: 'a; mutable b: 'b }

let rval2 = { new RecordType2<int,int> with a = 1 and b = 2 }

test "fweoew091" (rval2.a = 1)
test "fweoew092" (rval2.b = 2)
rval2.b <- 3
test "fweoew093" (rval2.b = 3)

let f(x) = 
  { new RecordType2<'a,int> with a = x and b = 2 }

test "fweoew091" ((f(1)).a = 1)
test "fweoew092" ((f(1)).b = 2)
(f(1)).b <- 3
test "fweoew093" ((f(1)).b = 2)


open System
open System.Collections
#if !NETCOREAPP
open System.Windows.Forms
#endif

//-----------------------------------------
// Some simple object-expression tests

let x0 = { new System.Object() with member __.GetHashCode() = 3 }
#if !NETCOREAPP
let x1 = { new System.Windows.Forms.Form() with member __.GetHashCode() = 3 }
#endif

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
     new(s1,s2) =  
       { inherit System.Object(); someField = "constructor2" + s1 + s2 }
  end


open System.Collections
type ClassType1 
  with 
    member x.GetEnumerator() = failwith "no implementation" 
    interface IEnumerable 
      with 
        member x.GetEnumerator() = failwith "no implementation" 
      end 
    end
let x = 1

let x2 = { new ClassType1("a") with member __.GetHashCode() = 3 }
let x3 = { new ClassType1("a") with member __.VirtualMethod1(s) = 4 }
let x4 = { new ClassType1("a") with 
                 member __.VirtualMethod1(s) = 5 
                 member __.VirtualMethod2(s1,s2) = s1.Length + s2.Length }



test "e09wckj2d" (try ignore((x2 :> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)

test "e09wckj2ddwdw" (try ignore(((x2 :> obj) :?> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)
test "e09wckj2defwe" (x2.VirtualMethod1("abc") = 3)
test "e09wckd2jfew3" (x3.VirtualMethod1("abc") = 4)
test "e09wckf3q2j" (x4.VirtualMethod1("abc") = 5)
test "e09wckj321" (x4.VirtualMethod2("abc","d") = 4)


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

test "e09wckj2ddwdw" (ignore(((x22 :> obj) :?> ClassType1)); true)
test "e09wckj2ddwdw" (ignore((x22 :> ClassType1)); true)

test "e09wckjd3" (x22.VirtualMethod1("abc") = 2001)
test "e09wckjd3" (x32.VirtualMethod1("abc") = 4002)
test "e09wckjfew" (x42.VirtualMethod1("abc") = 5004)
test "e09wckjd3" (x22.VirtualMethod2("abcd","dqw") = 8)
test "e09wckjd3" (x32.VirtualMethod2("abcd","dqw") = 10)



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

    test "e09wckj2ddwdw" (try ignore(((x2 :> obj) :?> IEnumerable).GetEnumerator()); false with Failure "no implementation" -> true)
    test "e09wckf3q2j" (x4.AbstractMethod1("abc") = 5)
    test "e09wckj321" (x4.AbstractMethod2("abc","d") = 4)


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

    test "e09wckjd3" (x22.AbstractMethod1("abc") = 2001)
    test "e09wckjd3" (x32.AbstractMethod1("abc") = 4002)
    test "e09wckjfew" (x42.AbstractMethod1("abc") = 5004)
    test "e09wckjd3" (x22.AbstractMethod2("abcd","dqw") = 8)
    test "e09wckjd3" (x32.AbstractMethod2("abcd","dqw") = 10)

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
        } 
      self

    type AbstractType 
     with 
      // properties
      member x.ToString() = x.instanceField
      member x.InstanceProperty = x.instanceField+".InstanceProperty"
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
      member x.PrivateInstanceProperty = x.instanceField+".InstanceProperty"
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
      member x.ToString() = match x with A _ -> "A" | B(s) -> "B"
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
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
    do System.Console.WriteLine("abc.InstanceProperty = {0}", xval.RecursiveInstance.InstanceProperty )
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
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.v (y :?> MyStringClass).v 
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.v) 
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = (compare x.v (y :?> MyStringClass).v ) = 0
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
    test "cepoiwelk1" ((s1 = s2) = (s1.v = s2.v))
    test "cepoiwelk2" ((s1 < s2) = (s1.v < s2.v))
    test "cepoiwelk3" ((s1 > s2) = (s1.v > s2.v))
    test "cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v))
    test "cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v))
    test "cepoiwelk6" ((s1 <> s2) = (s1.v <> s2.v))
    Printf.printf "hash s1 = %d\n" (hash(s1))
    Printf.printf "hash s1.v = %d\n" (hash(s1.v))
    test "cepoiwelk7" (hash(s1) = hash(s1.v))
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
  type MyStringClass = { cache: int; v: string  }
    with 
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.v (y :?> MyStringClass).v 
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.v) 
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = (compare x.v (y :?> MyStringClass).v ) = 0
      end 
      member x.Length = x.cache
      static member Create(s:string) = { cache=s.Length; v=s }
    end


  let s1 = MyStringClass.Create("abc")
  let s2 = MyStringClass.Create("def")
  let s3 = MyStringClass.Create("abc")
  let s4 = MyStringClass.Create("abcd")
  do test "recd-cepoiwelk" (s1.Length = 3)
  do test "recd-cepoiwelk" (s2.Length = 3)
  let testc s1 s2 = 
    test "recd-cepoiwelk1" ((s1 = s2) = (s1.v = s2.v))
    test "recd-cepoiwelk2" ((s1 < s2) = (s1.v < s2.v))
    test "recd-cepoiwelk3" ((s1 > s2) = (s1.v > s2.v))
    test "recd-cepoiwelk4" ((s1 <= s2) = (s1.v <= s2.v))
    test "recd-cepoiwelk5" ((s1 >= s2) = (s1.v >= s2.v))
    test "recd-cepoiwelk6" ((s1 <> s2) = (s1.v <> s2.v))
    Printf.printf "hash s1 = %d\n" (hash(s1))
    Printf.printf "hash s1.v = %d\n" (hash(s1.v))
    test "recd-cepoiwelk7" (hash(s1) = hash(s1.v))
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
  type MyStringClass = A of int * string | B of int * string
    with 
      member x.Value = match x with A(_,s) | B(_,s) -> s 
      interface IStructuralComparable with 
         member x.CompareTo(y:obj,comp:System.Collections.IComparer) = compare x.Value (y :?> MyStringClass).Value
      end 
      interface IStructuralEquatable with 
         member x.GetHashCode(comp:System.Collections.IEqualityComparer) = hash(x.Value)
         member x.Equals(y:obj,comp:System.Collections.IEqualityComparer) = x.Value = (y :?> MyStringClass).Value
      end 
      member x.Length = match x with A(n,_) | B(n,_) -> n
      static member Create(s:string) = A(s.Length,s)
    end


  let s1 = MyStringClass.Create("abc")
  let s2 = MyStringClass.Create("def")
  let s3 = MyStringClass.Create("abc")
  let s4 = MyStringClass.Create("abcd")
  do test "union-cepoiwelk" (s1.Length = 3)
  do test "union-cepoiwelk" (s2.Length = 3)
  let testc (s1:MyStringClass) (s2:MyStringClass) = 
    test "union-cepoiwelk1" ((s1 = s2) = (s1.Value = s2.Value))
    test "union-cepoiwelk2" ((s1 < s2) = (s1.Value < s2.Value))
    test "union-cepoiwelk3" ((s1 > s2) = (s1.Value > s2.Value))
    test "union-cepoiwelk4" ((s1 <= s2) = (s1.Value <= s2.Value))
    test "union-cepoiwelk5" ((s1 >= s2) = (s1.Value >= s2.Value))
    test "union-cepoiwelk6" ((s1 <> s2) = (s1.Value <> s2.Value))
    Printf.printf "hash s1 = %d\n" (hash(s1))
    Printf.printf "hash s1.Value = %d\n" (hash(s1.Value))
    test "union-cepoiwelk7" (hash(s1) = hash(s1.Value))
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
let [<DontPressThisButton("Please don't press this again")>] button () = 1


//---------------------------------------------------------------------
// Test we can use base calls


#if !NETCOREAPP
open System.Windows.Forms

type MyCanvas2 = 
  class 
    inherit Form 
    override x.OnPaint(args) =  Printf.printf "OnPaint\n"; base.OnPaint(args)

    new() = { inherit Form(); }
  end

let form2 = new MyCanvas2()
#endif


//---------------------------------------------------------------------
// Test we can inherit from the Event<'a> type to define our listeners

let (|>) x f = f x

(*
type MyEventListeners<'a>  =
  class
    inherit Event<'a> 

    val mutable listeners2: (Handler<'a>) list 

    member l.Fire(x : 'a) = 
      let arg = new SimpleEventArgs<_>(x) 
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
  let l = MyEventListeners<PaintEventArgs>() 
  object
    inherit Form()
    member x.Redraw = l
    override x.OnPaint(args) = l.Fire(args)
  end

class MyCanvas2 =
  let l = MyEventListeners<PaintEventArgs>() 
  object
    inherit Form
    member x.Redraw = l
    override x.OnPaint(args) = l.Fire(args)
  end
*)

(*
let form = new MyCanvas2()
// form.Paint.Add(...)
// form.add_Paint(...)
form.Redraw.AddHandler(new Handler(fun _ args -> Printf.printf "OnRedraw\n"))
form.Redraw.Add(fun args -> Printf.printf "OnRedraw\n")


form.Activate()
Application.Run(form)
*)

//x.add_Redraw

//---------------------------------------------------------------------
// Test we can define an exception

type MyException =
  class 
    inherit System.Exception
    val v: string 
    member x.Message = x.v
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
        member this.M1(y) = 1::y 
        member this.M2(x,y) = x::y
      end
      new() = { inherit Object(); }
   end
  type C2 = 
    class 
      interface InterfaceA3 with 
        member this.M1(y) = "a" :: y
        member this.M2(x,y) = x :: y
        member this.M3(y) = "a" :: "b" :: "c" :: y
      end
      new() = { inherit Object(); }
   end
  type C3 = 
    class 
      interface InterfaceA2<string> with 
        member this.M1(y) = "a" :: y
        member this.M2(x,y) = x :: y
      end
      interface InterfaceA3 with 
        member x.M3(y) = "a" :: "b" :: "c" :: y
      end
      new() = { inherit Object(); }
   end

  test "fewopvrej1" (((new C1()) :> InterfaceA1<int list>).M1([1]) = [1;1])
  test "fewopvrej2" (((new C1()) :> InterfaceA2<int>).M2(3,[1]) = [3;1])
   
  test "fewopvrej3" (((new C2()) :> InterfaceA1<string list>).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej4" (((new C2()) :> InterfaceA2<string>).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej4" (((new C2()) :> InterfaceA2<string>).M2("a",["hi"]) = ["a";"hi"])
  test "fewopvrej5" (((new C2()) :> InterfaceA3).M3(["hi"]) = ["a";"b";"c";"hi"])
  test "fewopvrej6" (((new C2()) :> InterfaceA3).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej7" (((new C2()) :> InterfaceA3).M2("a",["hi"]) = ["a";"hi"])
   
  test "fewopvrej8" (((new C3()) :> InterfaceA1<string list>).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej8" (((new C3()) :> InterfaceA2<string>).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej9" (((new C3()) :> InterfaceA2<string>).M2("a",["hi"]) = ["a";"hi"])
  test "fewopvrej10" (((new C3()) :> InterfaceA3).M3(["hi"]) = ["a";"b";"c";"hi"])
  test "fewopvrej11" (((new C3()) :> InterfaceA3).M1(["hi"]) = ["a";"hi"])
  test "fewopvrej12" (((new C3()) :> InterfaceA3).M2("a",["hi"]) = ["a";"hi"])
   
end


module PointTest =  begin


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

  
  let p = (new Point_with_no_inherits_clause(3)) 
  let f = p.Move 4 
  do test "wdfjcdwkj1" (p.X = 3)
  do f 4
  do test "wdfjcdwkj2" (p.X = 11)
  do f 1
  do test "wdfjcdwkj3" (p.X = 16)
  do test "wdfjcdwkj4" (Point.TwoArgs 1 2 = 3)
  do test "wdfjcdwkj5" (Point.TwoPatternArgs [1] [2] = 3)
  do test "wdfjcdwkj6" (Point.ThreeArgs 1 2 3 = 6)
  do test "wdfjcdwkj7" (Point.ThreePatternArgs [1] [2] [3] = 6)
  let p2 = (new Point(16)) 
  do test "wdfjcdwkj4" (p2.InstanceTwoArgs 1 2 = 16 + 3)
  do test "wdfjcdwkj5" (p2.InstanceTwoPatternArgs [1] [2] = 16 + 3)
  do test "wdfjcdwkj6" (p2.InstanceThreeArgs 1 2 3 = 16 + 6)
  do test "wdfjcdwkj7" (p2.InstanceThreePatternArgs [1] [2] [3] = 16 + 6)

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
        let rec length x acc = match x with MyNil -> acc | MyCons(a,b) -> length b (acc+1) 
        let len = length x.v 0 
        let items = Array.zeroCreate len 
        let rec go n l = match l with MyNil -> () | MyCons(a,b) -> items.[n] <- a; go (n+1) b 
        go 0 x.v;
        items
   end


//---------------------------------------------------------------------
// Pattern matching on objects

module PatternMatchTests = begin
    type P = 
      class 
        val x1: int; 
        val x2: string; 
        new(a,b) = {x1=a; x2=b } 
      end
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
       member this.Contains i = try (List.assoc i (List.map (fun itd -> (itd.ItemIndex, true)) !this.inv)) with :? System.Collections.Generic.KeyNotFoundException -> false
       member this.Remove i = this.inv := List.map snd (List.remove_assoc i (List.map (fun itd -> (itd.ItemIndex, itd)) !this.inv))
       member this.GetDetails i = List.assoc i (List.map (fun itd -> (itd.ItemIndex, itd)) !this.inv)
       member this.Add itd = if ((this :> IInventory).Contains (itd.ItemIndex) = false) then this.inv := itd :: !this.inv
       member this.GetTuple() =
          List.map (fun itd -> (itd.ItemIndex,itd.InventoryImage,itd.Name)) !this.inv
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
      
(*
    let NewAverage(toFloat) = 
      let count = ref 0
      let total = ref 0.0 
      { new Sampler<_,float>
          member self.Sample(x) = incr count; total := !total + toFloat x
          member self.GetStatistic() = !total / float(!count) }

*)
    let NewAverage(toFloat) = 
      let count = ref 0
      let total = ref 0.0 
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
    // // Return type is not sufficient:
    // let f2 (x:DateTime) y : DateTime = x - y
    // let g2 (x:DateTime) y : TimeSpan = x - y
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
      let SCGL = new System.Collections.Generic.List<'a>() 
      let _ = SCGL.Add(x) 
      let _ = SCGL.[0] 
      let _ = SCGL.[0] <- x 
      ()

    // check we have generalized 
    do f 1
    do f "3"

    let SCGD = new System.Collections.Generic.Dictionary<string,float>()
    let _ = SCGD.Add("hello",3.0)
    let _ = SCGD.["hello"]

    let g (k: 'a) (v:'b)= 
      let SCGD = new System.Collections.Generic.Dictionary<'a,'b>() 
      let _ = SCGD.Add(k,v)
      let _ = SCGD.[k]
      let _ = SCGD.[k] <- v
      ()



    // check we have generalized 
    do g 1 "3"
    do g "3" 1
    do g "3" "1"
    do g 1 1

    let h (v:'b)= 
      let arr = [| v;v;v |] 
      let elem = arr.[0] 
      let _ = arr.[0] <- v 
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
#if !NETCOREAPP
  let x4 = (f1() : System.Windows.Forms.Form)
#endif
  let f2 () = f1()
  let y1 = (f2() : obj)
  let y2 = (f2() : int)
  let y3 = (f2() : DateTime)
#if !NETCOREAPP
  let y4 = (f2() : System.Windows.Forms.Form)
#endif
  
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
                                                      let cmd = new SqlCommand()
                                                      cmd.CommandText <- "SELECT COUNT(*) FROM Topic WHERE Topic.Name = @name";
                                                      (cmd.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 255)).Value <- topic;
                                                      unbox(cmd.ExecuteScalar()) > 0
                                     
                                     override x.FileExists((virtualPath: string)) =
                                                      let relPath = VirtualPathUtility.ToAppRelative(virtualPath)
                                                      if relPath.StartsWith("~/topic") then
                                                                       x.TopicExists (relPath.Substring(7))
                                                      else
                                                                       x.Previous.FileExists(virtualPath)
                                                                       
                                     override x.DirectoryExists((virtualDir: string)) =
                                                      let relPath = VirtualPathUtility.ToAppRelative(virtualDir)
                                                      relPath.StartsWith("~/topic") || x.DirectoryExists(virtualDir)
                     end

    let AppInitialize()  = 
                     let provider = new TopicPathProvider()
                     HostingEnvironment.RegisterVirtualPathProvider(provider)

end



module TestConstrainedItemProperty = begin
    type Foo = 
        interface 
          abstract Item : int -> string with get
        end

    let f1 (x : #Foo) = x.[1]

    let f2 (x : #Foo) = x.[1]
end


module ExplicitSyntacCtor = begin

    type C =
       class
           val xx : int
           new(x,y) = 
               if y then 
                  { xx = x}
               else
                  { xx = x+x}
           new(x) = 
               let six = 3 + 3
               { xx = x}
           static member Create() = 
               let six = 3 + 3
               new C(3+3)
           new() = 
               let six = 3 + 3
               new C(3+3)
           new(a,b,c) = new C(a+b+c)
           new(a,b,c,d) = 
               new C(a+b+c+d)
               then
                 printf "hello"
       end



    type C1(x) =
       class
           let xx = x + x
           let f x = x + x
           let mutable state = x + x + xx

           do printf "hello\n"
           
           static member Create() = 
               let six = 3 + 3
               new C(3+3)

           new(x,y) = new C1(x+y)
                      
           member self.Increment = state <- state + 1
           member self.Once= xx 
           member self.Twice = xx + xx
       end

    let createRemoteAppDomain<'T> (d: AppDomain) = 
        unbox<'T>(d.CreateInstanceAndUnwrap(typeof<'T>.Assembly.FullName,typeof<'T>.FullName))


    // This is the simple version
    let rec maxList x =
      match x with 
      | [] -> failwith "no elements"
      | [h] -> h
      | h::t -> max h (maxList t)

    // This is how you use an accumulating parameter

    let rec maxListAcc acc x =
      match x with 
      | [] -> acc
      | [h] -> max acc h
      | h::t -> maxListAcc (max acc h) t

    let maxList2 x =
      match x with 
      | [] -> failwith "no elements"
      | h::t -> maxListAcc h t

    type ICool =
        interface
            abstract tst : unit->bool 
        end    
        
    [<AbstractClass>]
    type Cool() = 
        class
            abstract tst : unit -> bool
            interface ICool with
                member this.tst () = true
            end  
        end
        
    type beCool() =
        class 
            inherit Cool()
            override this.tst() = false
            interface ICool with
                member this.tst () = (this : beCool).tst()
            end  
        end

end

module Ex11 = 
    type MutableVector2D(dx:float,dy:float) =
        let mutable currDX = dx
        let mutable currDY = dy

        member this.DX with get() = currDX and set(v) = currDX <- v
        member this.DY with get() = currDY and set(v) = currDY <- v

        member v.Length 
             with get () = sqrt(currDX*currDX+currDY*currDY)
             and  set len = 
                 let theta = v.Angle
                 currDX <- cos(theta)*len;
                 currDY <- sin(theta)*len

        member c.Angle 
             with get () = atan2 currDY currDX
             and  set theta = 
                 let len = c.Length
                 currDX <- cos(theta)*len;
                 currDY <- sin(theta)*len

    let v1 = MutableVector2D(3.0,4.0)
    v1.Length
    v1.Angle
    v1.Angle <- System.Math.PI / 6.0 // = 30 degrees
    v1.Length
    v1.DX, v1.DY

    v1.DY / v1.Length

    type Vector2D(dx: float, dy: float) =
        let length = sqrt(dx * dx + dy * dy)
        member v.DX = dx
        member v.DY = dy
        member v.Length = length
        member v.Scale(k)    = Vector2D(k*dx,   k*dy)
        member v.ShiftX(dx') = Vector2D(dx=dx+dx', dy=dy)
        member v.ShiftY(dy') = Vector2D(dx=dx,     dy=dy+dy')
        static member Zero = Vector2D(dx=0.0, dy=0.0)
        static member OneX = Vector2D(dx=1.0, dy=0.0)
        static member OneY = Vector2D(dx=0.0, dy=1.0)

    type SparseVector  (items: seq<int * float>)= 
        let elems = new System.Collections.Generic.SortedDictionary<_,_>() 
        do items |> Seq.iter (fun (k,v) -> elems.Add(k,v))
        member t.Item 
            with get i = 
                if elems.ContainsKey(i) then elems.[i]
                else 0.0

    type UnitVector2D(dx,dy) = 
          let tolerance = 0.000001
          let len = sqrt(dx * dx + dy * dy)
          do if abs(len - 1.0) >= tolerance then failwith "not a unit vector";
          member v.DX = dx
          member v.DY = dy

module Ex11b = 
    type Vector2D (dx:float,dy:float) =
        member x.DX = dx
        member x.DY = dy
        static member (+) ((v1:Vector2D),(v2:Vector2D)) = 
            Vector2D(v1.DX + v2.DX, v1.DY + v2.DY)
        static member (-) ((v1:Vector2D),(v2:Vector2D)) =
            Vector2D(v1.DX - v2.DX, v1.DY - v2.DY)

    let v1 = Vector2D(3.0,4.0)
    v1
    v1 + v1
    v1 - v1


module Ex11c = 
    type Vector2D (dx:float,dy:float) =
        member x.DX = dx
        member x.DY = dy
        static member Create(? dx: float, ?dy:float) = 
            let dx = match dx with None -> 0.0 | Some v -> v
            let dy = match dy with None -> 0.0 | Some v -> v
            Vector2D(dx,dy)

module T1 = 
    type Vector2D(dx: float, dy: float) =
        let len = sqrt(dx * dx + dy * dy)
        member v.DX = dx
        member v.DY = dy
        member v.Length = len

    Vector2D(1.0,1.0) = Vector2D(1.0,1.0)

#if !NETCOREAPP
module Ex5 = 
    open System.Drawing
    type Label(?text,?font) =
        let text = match text with None -> "" | Some v -> v
        let font = match font with None -> new Font(FontFamily.GenericSansSerif,12.0f) | Some v -> v
        member x.Text = text
        member x.Font = font

    Label(text="Hello World")
    Label(font=new Font(FontFamily.GenericMonospace,36.0f),
          text="Hello World")
#endif

module Ex6 = 
    type IShape =
        abstract Contains : Point -> bool
        abstract BoundingBox : Rectangle

    and Vector = 
        { DX:float; DY:float }
        member v.Length = sqrt(v.DX*v.DX+v.DY*v.DY)
        static member (+) (v1:Vector,v2:Vector) = { DX=v1.DX+v2.DX; DY=v1.DY+v2.DY }
        static member (-) (v1:Vector,v2:Vector) = { DX=v1.DX-v2.DX; DY=v1.DY-v2.DY }
          
    and Point = 
        { X:float; Y:float }

        static member (-) (p1:Point,p2:Point) = { DX=p1.X-p2.X; DY=p1.Y-p2.Y }

        static member (-) (p:Point,v:Vector) = { X=p.X-v.DX; Y=p.Y-v.DY }
          
        static member (+) (p:Point,v:Vector) = { X=p.X+v.DX; Y=p.Y+v.DY }
          
    and Rectangle = 
        {X1:float; Y1:float; X2:float; Y2:float}
        static member Create(p1:Point,p2:Point) =
            {X1=p1.X; Y1=p1.Y; X2=p2.X; Y2=p2.Y }


    let circle(center:Point,radius:float) = 
        { new IShape with
              member x.Contains(p:Point) = (p - center).Length < radius
              member x.BoundingBox =
                  let diag = {DX=radius;DY=radius}
                  Rectangle.Create(center-diag,center+diag) }
    

    let square(center:Point,side:float) = 
        { new IShape with
              member x.Contains(p:Point) = 
                  let v = (p - center) 
                  abs(v.DX) < side/2.0 && abs(v.DY) < side/2.0
              member x.BoundingBox =
                  let diag = {DX=side/2.0;DY=side/2.0}
                  Rectangle.Create(center-diag,center+diag) }

    type MovingSquare() = 
        let mutable center = {X=0.0;Y=0.0}
        let mutable side = 1.0
        member sq.Center with get() = center and set(v) = center <- v
        member sq.SideLength with get() = side and set(v) = side <- v
        interface IShape with
              member x.Contains(p:Point) = 
                  let v = (p - center) 
                  abs(v.DX) < side/2.0 && abs(v.DY) < side/2.0
              member x.BoundingBox =
                  let diag = {DX=side/2.0;DY=side/2.0}
                  Rectangle.Create(center-diag,center+diag) 
    
module Ex7 = 
    open System.Drawing
    type IShape =
        abstract Contains : Point -> bool
        abstract BoundingBox : Rectangle

    let circle(center:Point,radius:int) = 
        { new IShape with
              member x.Contains(p:Point) = 
                  let dx = float32 (p.X - center.X)
                  let dy = float32 (p.Y - center.Y)
                  sqrt(dx*dx+dy*dy) < float32 radius
              member x.BoundingBox =
                  Rectangle(center.X-radius,center.Y-radius,2*radius,2*radius) }
    

    let bigCircle = circle(Point(0,0), 100)

    bigCircle.BoundingBox    
    bigCircle.Contains(Point(70,70))
    bigCircle.Contains(Point(71,71))
        
    let square(center:Point,side:int) = 
        { new IShape with
              member x.Contains(p:Point) = 
                  let dx = p.X - center.X
                  let dy = p.Y - center.Y
                  abs(dx) < side/2 && abs(dy) < side/2
              member x.BoundingBox =
                  Rectangle(center.X-side,center.Y-side,side*2,side*2) }

    type MovingSquare() = 
        let mutable center = Point(x=0,y=0)
        let mutable side = 10
        member sq.Center with get() = center and set(v) = center <- v
        member sq.SideLength with get() = side and set(v) = side <- v
        interface IShape with
              member x.Contains(p:Point) = 
                  let dx = p.X - center.X
                  let dy = p.Y - center.Y
                  abs(dx) < side/2 && abs(dy) < side/2
              member x.BoundingBox =
                  Rectangle(center.X-side,center.Y-side,side*2,side*2) 


module MoreOptionalArgTests = 
    open System
    open System.Text
    open System.Collections.Generic


    let defaultArg x y = match x with None -> y | Some v -> v

    type T() = 
        static member OneNormalTwoOptional (arg1, ?arg2, ?arg3) = 
            let arg2 = defaultArg arg2 3
            let arg3 = defaultArg arg3 10
            arg1 + arg2 + arg3

        static member TwoOptional (?arg1, ?arg2) = 
            let arg1 = defaultArg arg1 3
            let arg2 = defaultArg arg2 10
            arg1 + arg2 


    test "optional arg test" (16 = T.OneNormalTwoOptional(3))
    test "optional arg test" (15 = T.OneNormalTwoOptional(3,2))
    test "optional arg test" (16 = T.OneNormalTwoOptional(arg1=3))
    test "optional arg test" (14 = T.OneNormalTwoOptional(arg1=3,arg2=1))
    test "optional arg test" (13 = T.OneNormalTwoOptional(arg2=3,arg1=0))
    test "optional arg test" (14 = T.OneNormalTwoOptional(arg2=3,arg1=0,arg3=11))
    test "optional arg test" (14 = T.OneNormalTwoOptional(0,3,11))
    test "optional arg test" (14 = T.OneNormalTwoOptional(0,3,arg3=11))

    test "optional arg test" (16 = T.OneNormalTwoOptional(arg1=3))
    test "optional arg test" (14 = T.OneNormalTwoOptional(arg1=3,?arg2=Some(1)))
    test "optional arg test" (14 = T.OneNormalTwoOptional(arg2=3,arg1=0,arg3=11))
    test "optional arg test" (14 = T.OneNormalTwoOptional(?arg2=Some(3),arg1=0,arg3=11))
    test "optional arg test" (14 = T.OneNormalTwoOptional(0,3,?arg3=Some(11)))


    test "optional arg test" (13 = T.TwoOptional())
    test "optional arg test" (12 = T.TwoOptional(2))
    test "optional arg test" (11 = T.TwoOptional(arg1=1))
    test "optional arg test" (13 = T.TwoOptional(arg1=3))
    test "optional arg test" (14 = T.TwoOptional(arg1=3,arg2=11))
    test "optional arg test" (14 = T.TwoOptional(3,11))
    test "optional arg test" (14 = T.TwoOptional(3,arg2=11))
    do printfn "Done MoreOptionalArgTests"


module MoreRandomTests = 
    do printfn "MoreRandomTests"

    let memoize f = 
        let cache = ref Map.empty
        fun x -> 
            match (!cache).TryFind(x) with
            | Some res -> res
            | None -> 
                 let res = f x
                 cache := (!cache).Add(x,res)
                 res



    // Save computed results by using an internal dictionary.
    // Note that memoize is inferred to have type
    //   ('a -> 'b) -> ('a -> 'b)
    let memoize2 f = 
        let cache = System.Collections.Generic.Dictionary<_, _>()
        fun x -> 
            let ok,res = cache.TryGetValue(x)
            if ok then res 
            else let res = f x
                 cache.[x] <- res
                 res


module MemberTakingOptionalArgumentUsedAsFirstClassFunctionValue = 
    type C() = 
        static member M(?a:int) = a

    let pf = (C.M, C.M)

    fst pf ()
    snd pf ()

module StillMoreRandomTests = 
    do printfn "StillMoreRandomTests"

    type Var = string

    type ArithExpr = 
      | Sum of ArithExpr * ArithExpr
      | Mul of ArithExpr * ArithExpr
      | Neg of ArithExpr
      | Var of Var
      | Let of Var * ArithExpr * ArithExpr


    type Circuit = 
      | And of Circuit * Circuit
      | Not of Circuit
      | True
      | Var of Var
      | Exists of Var * Circuit

    let False = Not(True)
    let Forall(v,p) = Not(Exists(v,Not(p)))
    let Or (p1,p2) = Not(And(Not(p1),Not(p2)))

    // nice infix notation, also deerived equality and implication
    let (&&&) p1 p2 = And(p1,p2)
    let (|||) p1 p2 = Or(p1,p2)
    let (===) p1 p2 = (p1 &&& p2) ||| (Not(p1) &&& Not(p2))
    let (==>) p1 p2 = (Not(p1) ||| p2) 
    let Cond p1 p2 p3 = (p1 ==> p2) &&& (Not(p1) ==> p3)


    type Env = Map<Var,bool>

    let rec eval (env:Env) p = 
        match p with
        | And(p1,p2) -> eval env p1 && eval env p2
        | Not(p1) -> not (eval env p1)
        | Var(v) ->  env.[v]
        | True ->  true
        | Exists(v,p) ->  
            // Evaluate p with the variable 'true'
            eval (env.Add(v,true))  p ||
            // Evaluate p with the variable 'false'
            eval (env.Add(v,false)) p


    eval Map.empty True

    let varCount = ref 0 
    let freshVariable() = incr varCount; (!varCount).ToString()

    let hide1 g = 
        let stateVar = freshVariable()
        let state = Var(stateVar)
        Exists(stateVar, g(state))

    //let circuit inp out = hide1 (fun state -> state === (state &&& inp) &&& out === state)
    //let circuit2 inp out = hide1 (fun inner -> circuit inp inner &&& circuit inner out)

    /// Helper to generate a variable and generate a circuit that
    /// uses the variable.
    let forall1 g = 
        let v1 = freshVariable()
        Forall(v1,g(Var(v1)))

    /// Same for three variables
    let forall3 g = forall1 (fun v1 -> forall1 (fun v2 -> forall1 (fun v3 -> g v1 v2 v3)))

    // This is the circuit: it chooses the output based on the input
    // The circuit looks at input 0, and chooses the ouput to be input 1 or input 2
    let circuit (inp : Circuit[]) out = 
        Cond inp.[0] (out === inp.[1]) (out === inp.[2])

    /// This is a specification of one property of the circuit. It says 
    /// that if input 1 is the same as input 2 then the result is
    /// this input, regardless of the values of the inputs 
    let check1 = forall3 (fun inp0 inp1 out -> circuit [| inp0;inp1;inp1 |] out ==> (out === inp1))

    eval Map.empty check1 // 'true' - the property holds

    eval Map.empty (Cond True False False) = false
    eval Map.empty (Cond True False True) = false
    eval Map.empty (Cond True True False) = true
    eval Map.empty (Cond False True False) = false
    eval Map.empty (Cond False False True) = true
    eval Map.empty (False === True) = false
    eval Map.empty (False === False) = true
    eval Map.empty (True === True) = true
    eval Map.empty (True === False) = false
    eval Map.empty (Forall("a",Var("a") === Var("a"))) = true
    eval Map.empty (Forall("a",Var("a") ==> Var("a"))) = true
    eval Map.empty (Forall("a",Not(Var("a") === Var("a")))) = false
    eval Map.empty (Forall("a",Forall("b",Var("a") === Var("b")))) = true

module MemoizeSample = 
    do printfn "MemoizeSample"

    type Function<'a,'b> = interface
        abstract Item : 'a -> 'b with get
        abstract Clear : unit -> unit
    end
    
    let memoize f = 
        let lookasideTable = new System.Collections.Generic.Dictionary<_,_>()
        { new Function<_,_> with 
              member t.Item
                 with get(n) = 
                     if lookasideTable.ContainsKey(n) then lookasideTable.[n]
                     else let res = f n
                          lookasideTable.Add(n,res)
                          res 
              member t.Clear() = 
                  lookasideTable.Clear() }
                  

    do printfn "MemoizeSample - #0"
    let rec fibFast = memoize (fun n -> if n <= 2 then 1 else fibFast.[n-1] + fibFast.[n-2])

    do printfn "Memoize #1"
    fibFast.[3]
    do printfn "Memoize #2"
    fibFast.[30]
    do printfn "Memoize #3"
    fibFast.Clear()
    do printfn "Memoize #4"
    
(*
module NameLookupServiceExample = 
    do printfn "NameLookupServiceExample"
    type NameLookupService = 
        abstract Contains : string -> bool
        abstract ClosestPrefixMatch : string -> string 

    let simpleNameLookup (words: string list) = 
        let wordTable = Set.ofSeq(words)
        let score (w1:string) (w2:string) = 
            let lim = (min w1.Length w2.Length) 
            let rec loop i acc =  
                if i >= lim then acc 
                else loop (i+1) (Char.code w1.[i] - Char.code w2.[i] + acc)
            loop 0 0
            
        { new NameLookupService with
            member t.Contains(w) = wordTable.Contains(w)
            member t.ClosestPrefixMatch(w) = 
                if wordTable.Contains(w) then w
                else
                    printfn "w = %s" w
                    let above = 
                        match wordTable.GetNextElement(w) with
                        | Some w2 when w2.StartsWith(w) -> Some w2
                        | _ -> None
                    let below = 
                        match wordTable.GetPreviousElement(w) with
                        | Some w2 when w2.StartsWith(w) -> Some w2
                        | _ -> None
                    printfn "above = %A, below = %A" above below
                    match above, below with
                    | Some w1,Some w2 -> if score w w1 > score w w2 then w2 else w1
                    | Some res,None 
                    | None,Some res -> res
                    | None,None -> failwith "no match!" }


    let capitalLookup = simpleNameLookup ["London";"Paris";"Warsaw";"Tokyo"]

    capitalLookup.Contains "Paris"
    capitalLookup.ClosestPrefixMatch "Wars"

*)


module ConstraintsInMembers = begin

    do printfn "ConstraintsInMembers"
    type IDuplex = 
      interface 
      end

    type IServer = 
      interface 
      end
      
    let bind (a:#IServer) = "2"

    let Bind1(v:#IDuplex) : string = bind v 

    type C() = 
        member x.Bind1(v:#IDuplex) : string = bind v 
        member x.Bind2(v:#IDuplex) : string = bind v 
end

module DelegateByrefCreation = begin
    type D = delegate of int byref -> int
    type D2 = delegate of int byref * int byref -> int

    let createImmediateDelegate = new D(fun b -> b)
    let createImmediateDelegate2 = new D2(fun b1 b2 -> b1  + b2)
end

module InterfaceCastingTests = begin

    do printfn "InterfaceCastingTests"
    type IBar = 
        interface
        end

    type IFoo = 
        interface
        end

    type C() = 
        class
        end

    type D() = 
        class
           interface IFoo 
        end
        
    type Struct = 
        struct
           val x : int
        end
        
    type R = 
        { c : int }
        
    type U = 
        A | B
        
        
    let checkPatternTestInterfaceToInterface(l:IBar list) =
        match l with
        | [:? IFoo] -> None
        | _ -> None
        
    let checkPatternTestInterfaceToUnsealedClassImplicit(l:IBar list) =
        match l with
        | [:? C] -> None
        | _ -> None
        
    let checkPatternTestInterfaceToUnsealedClassExplicit(l:IBar list) =
        match l with
        | [:? D] -> None
        | _ -> None


        
    let checkTypeTestInterfaceToInterface(l:IBar ) =
        (l :? IFoo)
        
    let checkTypeTestInterfaceToUnsealedClassImplicit(l:IBar) =
        (l :? C)
        
    let checkTypeTestInterfaceToUnsealedClassExplicit(l:IBar) =
        (l :? D)

    let checkCoercionInterfaceToInterface(l:IBar ) =
        (l :?> IFoo)
        
    let checkCoercionInterfaceToUnsealedClassImplicit(l:IBar) =
        (l :?> C)
        
    let checkCoercionInterfaceToUnsealedClassExplicit(l:IBar) =
        (l :?> D)

    let checkDowncastInterfaceToInterface(l:IBar ) =
        (downcast l : IFoo)
        
    let checkDowncastInterfaceToUnsealedClassImplicit(l:IBar) =
        (downcast l : C)
        
    let checkDowncastInterfaceToUnsealedClassExplicit(l:IBar) =
        (downcast l : D)

end


module MiscGenericOverrideTest = 
   do printfn "MiscGenericOverrideTest"
   type 'a Class2 = 
      class
        inherit obj 
        new () = { inherit obj(); }
        override this.ToString() = base.ToString() 
      end

module GlobalTickTock = 
   //let x = 1
   do printfn "GlobalTickTock"
   type TickTock = Tick | Tock
   type time = float
   let private x = ref Tick
   
  // public module M =
//      let x = 1
      
   //let (private ticked,public TickEvent) = Event.create<TickTock>()
   //let internal oneTick() = 
   //    (x := match !x with Tick -> Tock | Tock -> Tick); 
    //   ticked(!x)


module RecursiveInheritanceExampleFromTomas = 
    type ISome = 
      interface
        abstract InterfaceMethod : int -> int
      end
    and Test = 
      class
        interface ISome with
          member this.InterfaceMethod  (a) =
            a + 5
        end
      end



module DefaultImplementationsOfAbstractProperties = 
    type A() = class
      abstract Prop : int with set
      abstract Prop : int with get
      default x.Prop 
        with get()  = printf "A::Prop.get"; 6
        and  set(v) = printf "A::Prop.set(%d)" v
    end    

module StructDefinition = 
    type ('h,'t) BUN =
        struct
            val head : 'h
            val tail : 't
            new(h,t) = {head = h; tail = t}
        end
     
    let heap = Array.init 100 (fun i -> BUN(i,i))
     
    let _ = heap.[3]


module StructKeywordAsConstraintTest = 

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
        
    type Class1b<'a when 'a : struct>(x:int) =
        member self.X = x
     
    type Class2b<'a when 'a : not struct>(x:int) =
        member self.X = x
     
     
module StaticInitializerTest1 =

    type C<'a>() = 
        static let mutable v = 2 + 2
        static do v <- 3
        
        member x.P = v
        static member P2 = v+v

    test "lwnohivw0" ((new C<int>()).P = 3)
    test "lwnohivw1" ((new C<int>()).P = 3)
    test "lwnohivw2" ((new C<string>()).P = 3)
    test "lwnohivw3" ((C<int>.P2) = 6)
    test "lwnohivw4" ((C<string>.P2) = 6)

module StaticInitializerTest2 =

    type C() = 
        static let mutable v = 2 + 2
        static do v <- 3
        
        member x.P = v
        static member P2 = v+v

    check "lwnohivq01" (new C()).P 3
    check "lwnohivq12" (new C()).P 3
    check "lwnohivq33" C.P2 6
    check "lwnohivq44" C.P2 6
        

module StaticInitializerTest3 =

    let mutable x = 2
    do x <- 3
    
    type C() = 
        static let mutable v = x + 1
        static do v <- 3
        
        member x.P = v
        static member P2 = v+x

    check "lwnohivq05" (new C()).P 3
    check "lwnohivq16" (new C()).P 3
    check "lwnohivq37" C.P2 6
    check "lwnohivq48" C.P2 6
        

module OkStructTest3 = begin
    type OkStruct1 = 
        struct
            val x : list<OkStruct1>
        end    
end

module FSharp_1_0_bug_1748_Problem_Calling_A_Base_Member_A = begin

    type C<'a>(value) =
        member x.BaseM() = printf "out %d" value

    type D<'b>(value:int) =
        inherit C<'b>(value) 

        member x.M() = base.BaseM()

end

module FSharp_1_0_bug_1748_Problem_Calling_A_Base_Member_B = begin

    type Exp<'c when 'c :> Exp<'c>> = abstract Print : unit -> unit

    type PrintLit<'c when 'c :> Exp<'c>>(value) =
        member x.Value = value
        member x.BasePrint() = printf "out %d" x.Value
        interface Exp<'c> with
            member x.Print() = x.BasePrint()

    type PrintAdd<'c when 'c :> Exp<'c>>(left:'c, right:'c) =
        member x.Left = left
        member x.Right = right
        member x.BasePrint() = x.Left.Print(); printf "+"; x.Right.Print()
        interface Exp<'c> with
            member x.Print() = x.BasePrint()

    type EvalExp<'c when 'c :> EvalExp<'c>> =
        inherit Exp<'c>
        abstract Eval : unit -> int

    type EvalLit<'c when 'c :> EvalExp<'c>>(value:int) =
        inherit PrintLit<'c>(value) 
        member x.BaseEval() = x.Value
        interface EvalExp<'c> with
            //the base is not strictly necessary here, but used for clarity
            member x.Print() = base.BasePrint()
            member x.Eval() = x.BaseEval()

    type EvalAdd<'c when 'c :> EvalExp<'c>>(left:'c, right:'c) =
        inherit PrintAdd<'c>(left, right) 
        member x.BaseEval() = x.Left.Eval() + x.Right.Eval()
        interface EvalExp<'c> with
            member x.Print() = base.BasePrint()
            member x.Eval() = x.BaseEval()

    type EvalExpFix = inherit EvalExp<EvalExpFix>

    type EvalLitFix(value) =
        inherit EvalLit<EvalExpFix>(value) 
        interface EvalExpFix with
            member x.Print() = base.BasePrint()
            member x.Eval() = base.BaseEval()

    type EvalAddFix(left:EvalExpFix, right:EvalExpFix) =
        inherit EvalAdd<EvalExpFix>(left, right) 
        interface EvalExpFix with
            member x.Print() = base.BasePrint()
            member x.Eval() = base.BaseEval()

    let e1 = new EvalLitFix(2)
    let e2 = new EvalLitFix(3)
    let e3 = new EvalAddFix(e1, e2) :> EvalExpFix
    do  e3.Print()
        System.Console.Write(" = " + e3.Eval().ToString())
end


open System.Collections.Generic

module Test1 = begin
    type C() = class end

    type ITest = interface
                       abstract member Read : #C -> unit
                     end

    type Impl() = 
        interface ITest  with
            override this.Read (orgList : #C) = printfn "done"
        end

    let impl() = 
        { new ITest  with
            override this.Read (orgList : #C) = printfn "done"
        }
end

module Test2 = begin

    type C<'a>() = class end

    type ITest<'d> = interface
                       abstract member Read : #C<'a> -> unit
                     end

    type Impl() = 
        interface ITest <int> with
            override this.Read (orgList : #C<'b>) = printfn "done"
        end

    let impl() = 
        { new ITest <int> with
            override this.Read (orgList : #C<'b>) = printfn "done"
        }

end


module Test3 = begin

    type C<'a>() = class end

    type ITest<'d> = interface
                       abstract member Read<'b when 'b :> C<'d> > : 'd  -> unit 
                     end

    type Impl() = 
        interface ITest <int> with
            override this.Read<'c when 'c :> C<int> >  _ = printfn "done"
        end

    let impl() = 
        { new ITest <int> with
            override this.Read<'c when 'c :> C<int> >  _ = printfn "done"
        }

end
module Test4 = begin

    type C<'a>() = class end

    type ITest<'d> = interface
                       abstract member Read<'b > : 'b  -> unit 
                     end

    type Impl() = 
        interface ITest <int> with
            override this.Read _ = printfn "done"
        end

    let impl() = 
        { new ITest <int> with
            override this.Read _ = printfn "done"
        }

end

module Test5 = begin

    type ITest = interface
                       abstract member Read<'b > : int  -> unit 
                 end

    type Impl() = 
        interface ITest with
            override this.Read _ = printfn "done"
        end

    let impl() = 
        { new ITest with
            override this.Read _ = printfn "done"
        }

end

module Test6 = begin


    type ITest<'d> = interface
                       abstract member Read : int -> unit
                     end

    type Impl<'e>() = 
        interface ITest <'e> with
            override this.Read (orgList : int) = printfn "done"
        end

    let impl<'e>() = 
        { new ITest <'e> with
            override this.Read (orgList : int) = printfn "done"
        }

end


module Test7 = begin

    type ITest<'d> = interface
                       abstract member Read : 'd -> unit
                     end

    type Impl<'e>() = 
        interface ITest <'e> with
            override this.Read (orgList : 'e) = printfn "done"
        end

    let impl() = 
        { new ITest <'e> with
            override this.Read (orgList : 'e) = printfn "done"
        }

end



module Test8 = begin

    type C<'a>() = class end

    type ITest<'q> = interface
                       abstract member Read : #C<'q> -> unit
                     end

    type Impl<'f>() = 
        interface ITest <'f> with
            override this.Read (orgList : #C<'f>) = printfn "done"
        end

    let impl() = 
        { new ITest <'f> with
            override this.Read (orgList : #C<'f>) = printfn "done"
        }

end


// FSB 1112, Bug in definition of generic interface

module Test9 = begin
    open System.Collections.Generic

    type ITest<'T> = interface
                       abstract member Read : #IList<'T> -> unit
                     end

    type Impl<'t>() = 
        interface ITest<'t>  with
            override this.Read (orgList : #IList<'t>) = printfn "done"


    let impl = {new ITest<'t> with
                     override this.Read orgList = printfn "done"};
                 
end


module InterfaceEndTokenTests_bugs_FSharp_1_0_1148_and_1431 = 
    type IFoo = interface
        abstract Meth3 : unit -> unit
    end

    type IBar = interface
        abstract Meth3 : unit -> unit
    end


    type Foo() = class
        interface IFoo with
            member this.Meth3 () = ()
    end

    type Foo1x() = class
        interface IFoo with
            member this.Meth3 () = ()
        end
    end

    type Foo1b() = 
        class
            interface IFoo with
                member this.Meth3 () = ()
        end

    type Foo1c() = 
        class
            interface IFoo with
                member this.Meth3 () = ()
          end

    type Foo1d() = 
        class
            interface IFoo with
                member this.Meth3 () = ()
                
         // note the 'end' token doesn't have to align with 'class'
           end

    type Foo1e() = 
        class
            interface IFoo with
                member this.Meth3 () = ()
                
         // note the 'end' token doesn't have to align with 'class'
      end

    type Foo2() = class
        interface IFoo with
            member this.Meth3 () = ()
        end
    end


    type Foo3() = 
        interface IFoo with
            member this.Meth3 () = ()
        end

    type Foo4() = 
        interface IFoo with
            member this.Meth3 () = ()
        end
            

    type Foo5() = 
        interface IFoo with
            member this.Meth3 () = ()
            
    type Foo6() = 
        interface IFoo with
            member this.Meth3 () = ()
        interface IBar with
            member this.Meth3 () = ()
            
            
    type Foo7() = 
        interface IFoo with
            member this.Meth3 () = ()
        interface IBar with
            member this.Meth3 () = ()
        end
        
    type Foo7b() = 
        interface IFoo with
            member this.Meth3 () = ()
        end
        interface IBar with
            member this.Meth3 () = ()
        end
            
    type Foo8() = 
        interface IFoo with
            member this.Meth3 () = ()
        end
        interface IBar with
            member this.Meth3 () = ()

    type Foo9() = 
        interface IFoo with
            member this.Meth3 () = ()
          end
        interface IBar with
            member this.Meth3 () = ()

    type Foo10() = 
        interface IFoo with
                   member this.Meth3 () = ()
              end
          // Spec issue: interfaces and members can progressively indent if 'end' tokens included
          interface IBar with
                 member this.Meth3 () = ()
       


type node =
 interface
   abstract wombat :string
 end
type nodeB =
 //member it.dummy = "dummy"
 interface node with
   member it.wombat = "B.wombat"


module Check_Static_Let_Bindings_In_Structs = 
    let r = ref 0 
    
    [<Struct>]
    type S(x:int) = 
        static let mutable v = 3
        static do printfn "Initialize S"
        static do v <- v + 1
        //do printfn "hello"
        //do incr r
        member y.P = x
        static member V = v
        
    do test "vr09jrweokm" (S.V = 3 || S.V = 4)
    //do test "vr09jrweokm" (!r = 0)
    let s3 = S(3)
    let s4 = S(4)
    do test "vr09jrweokm" (S.V = 4)
    //do test "vr09jrweokm" (!r = 2)

module UnitArgs = 
    let f () = 
       printfn "hello from f"
       printfn "hello from f"
       printfn "hello from f"
       printfn "hello from f"
       printfn "hello from f"
       printfn "hello from f"
       1+2

    let f2 (c:unit) = 
        printfn "hello  from f2";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        1+2

    let f3 = fun () -> 
        printfn "hello  from f3";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        1+2

    let f4 = function () -> 
                        printfn "hello  from f4";
                        printfn "hello";
                        printfn "hello";
                        printfn "hello";
                        printfn "hello";
                        printfn "hello";
                        printfn "hello";
                        1+2

    let f5 = (fun () -> 
        printfn "hello  from f5";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        printfn "hello";
        1+2)


    type C(x:int) = 
        static member M() = 
           printfn "hello from C.M"
           printfn "hello from C.M"
           printfn "hello from C.M"
           printfn "hello from C.M"
           printfn "hello from C.M"
           printfn "hello from C.M"
           1+2

        member x.M2() = 
           printfn "hello from C.M2"
           printfn "hello from C.M2"
           printfn "hello from C.M2"
           printfn "hello from C.M2"
           printfn "hello from C.M2"
           printfn "hello from C.M2"
           1+2
        member x.M3 = fun () ->
           printfn "hello from C.M3"
           printfn "hello from C.M3"
           printfn "hello from C.M3"
           printfn "hello from C.M3"
           printfn "hello from C.M3"
           printfn "hello from C.M3"
           1+2
        member x.M4 () ()  = 
           printfn "hello from C.M4"
           printfn "hello from C.M4"
           printfn "hello from C.M4"
           printfn "hello from C.M4"
           printfn "hello from C.M4"
           printfn "hello from C.M4"
           1+2
    f()
    List.map f [();();()]
    f2()
    List.map f2 [();();()]
    f2()
    List.map f3 [();();()]
    f3()
    List.map f4 [();();()]
    f4()
    List.map f5 [();();()]
    f5()
    List.map C.M [();();()]
    C.M()
    let c = C(3)
    c.M2()
    List.map c.M2 [();();()]
    c.M3()
    List.map c.M3 [();();()]
    c.M4() () 
    List.map (c.M4 ()) [();();()]
    
//---------------------------------------------------------------------
// Finish up


#if MONO // bug repro1
#else

module SingleArgumentFunctions =
    type C() = 
        let f0 () = 1
        let f1 (x:int) = x
        let f2 (x:int) = f1 x + f0()
        let rec f3 (x:int) = 2
        let f4 (x:int) = f3 x + f0()
        let v = f4 5
        let f5 (x:int) = f4 v + 6
        let rec f6 (x:int) = if x = 0 then 6 else f6 (x-1)
        let v2 = f6 5
        let v3 = List.map f0 [(); ()]
        let v4 = List.map f1 [1;2]
        let v5 = List.map f3 [1;2]
        member x.Result =     
            List.sum v3 + List.sum v4 + List.sum v5  + v2 + v

    let c = C()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09j" (c.Result = 18)
#endif

module MultiArgumentFunctions =
    type C() = 
        let f1 (x1:int) x2 = x1 + x2
        let v5 = f1 2 3
        member x.Result =     
            f1 3 4 + v5

    let c = C()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09h" (c.Result = 12)



module TupledMultiArgumentFunctions =
    type C() = 
        let f1 (x1:int,x2:int) (x3:int,x4:int) = x1 + x2 + x3 + x4
        let v5 = f1 (2,3) (4,5)
        member x.Result =     
            f1 (6,7) (8,9) + v5

    let c = C()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09g" (c.Result = 44)

module FunctionInGenericClass =
    type C<'a>() = 
        let f1 (x1:int) = x1 + 3
        let v5 = f1 3
        member x.Result =     
            f1 6  + v5

    let c = C<int>()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09f" (c.Result = 15)

module GenericFunctions =
    type C() = 
        let f1 x1 = x1
        let f2 (x : 'a) : 'a when 'a :> System.IComparable = x
        let v5 = f1 3
        let v6 = f1 "hello"
        let v7 = f2 1
        let v8 = f2 "hello"
        member x.Result =     
            f1 6  + v5 + v6.Length

    let c = C()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09d" (c.Result = 14)

#if MONO // bug repro1 (uncomfirmed)
#else
module GenericFunctionInGenericClass =
    type C<'a>() = 
        let f1 x1 = x1 
        let rec f2 n x1 = if n = 0 then x1  else f2 (n-1) x1
        let rec f3 n (x1:int) = if n = 0 then x1  else f2 (n-1) x1
        let v5 = f1 3 + sizeof<'a>
        let v6 = f1 "hello"
        let v7 = f2 8 3 + sizeof<'a>
        let v8 = f2 10 "hello"
        let v9 = f3 8 3 + sizeof<'a>
        member x.Result =     
            f1 6  + v5 + v6.Length + v7 + v8.Length + f2 8 6 + f3 8 6
        member x.Functions =     
            (f1 : int -> int), (f1 : string -> string),
            (f2 : int -> int -> int), (f2 : int -> string -> string),
            // Non generic function in generic class used as a first class value
            (f3 : int -> int -> int)

    let c = C<int>()
    printfn "c.Result = %d" c.Result
    let c2 = C<System.DateTime>()
    printfn "c2.Result = %d" c2.Result
    test "vrewiorvw09s" (c.Result = 42)
    test "vrewiorvw09a" (c2.Result = 50)
#endif

module TailCallLoop =
    type C<'a>() = 
        let rec f x = if x = 0 then 1 else f (x  - 1)
        let v5 = f 10000000
        member x.Result =     
            f 10000000 + v5

    let c = C<int>()
    printfn "c.Result = %d" c.Result
    let c2 = C<System.DateTime>()
    printfn "c2.Result = %d" c2.Result
    test "vrewiorvw09o" (c.Result = 2)
    test "vrewiorvw09i" (c2.Result = 2)

module FunctionsAsLambdas =
    type C() = 
        let f0 = fun () -> 1
        let f1 = fun (x:int) -> x
        let f2 = fun (x:int) -> f1 x + f0()
        let rec f3 = fun (x:int) -> 2
        let f4 = fun (x:int) -> f3 x + f0()
        let v = f4 5
        let f5 = fun (x:int) -> f4 v + 6
        let rec f6 = fun (x:int) -> if x = 0 then 6 else f6 (x-1)
        let v2 = f6 5
        let v3 = List.map f0 [(); ()]
        let v4 = List.map f1 [1;2]
        let v5 = List.map f3 [1;2]
        let f6 = fun (x1:int) x2 -> x1 + x2
        let v6 = f6 2 3
        let f7 = fun (x1:int,x2:int) (x3:int,x4:int) -> x1 + x2 + x3 + x4
        let v7 = f7 (2,3) (4,5)
        let f8 = fun (x1:int,x2:int) () -> x1 + x2 
        let v8 = f8 (2,3) ()
        let f9 = fun () -> fun (x1:int,x2:int) -> x1 + x2 
        let v9 = f9 () (2,3) 
        let f10 = fun  x1 -> x1
        let v10a = f10 3
        let v10b = f10 "hello"
        member x.Result =     
            List.sum v3 + List.sum v4 + List.sum v5 + v2 + v + v6 + v7 + v8 + v9 + v10a + v10b.Length

    let c = C()
    printfn "c.Result = %d" c.Result
    test "vrewiorvw09u" (c.Result = 55)



module Pathological1 =
    type C<'a>() = 
        let mutable f1  = fun x1 -> x1 
        let v5 = f1 3 + sizeof<'a>
        member x.Result =     
            f1 6  + v5 

    let c = C<int>()
    printfn "c.Result = %d" c.Result
    let c2 = C<System.DateTime>()
    printfn "c2.Result = %d" c2.Result
    test "vrewiorvw09y" (c.Result = 13)
    test "vrewiorvw09t" (c2.Result = 17)
    


module StaticSingleArgumentFunctions =
    type C() = 
        static let f0 () = 1
        static let f1 (x:int) = x
        static let f2 (x:int) = f1 x + f0()
        static let rec f3 (x:int) = 2
        static let f4 (x:int) = f3 x + f0()
        static let v = f4 5
        static let f5 (x:int) = f4 v + 6
        static let rec f6 (x:int) = if x = 0 then 6 else f6 (x-1)
        static let v2 = f6 5
        static let v3 = List.map f0 [(); ()]
        static let v4 = List.map f1 [1;2]
        static let v5 = List.map f3 [1;2]
        static member Result =     
            List.sum v3 + List.sum v4 + List.sum v5  + v2 + v

    printfn "c.Result = %d" C.Result
    test "vrewiorvw09r" (C.Result = 18)

module StaticMultiArgumentFunctions =
    type C() = 
        static let f1 (x1:int) x2 = x1 + x2
        static let v5 = f1 2 3
        static member Result =     
            f1 3 4 + v5

    printfn "c.Result = %d" C.Result
    test "vrewiorvw09e" (C.Result = 12)

module StaticTupledMultiArgumentFunctions =
    type C() = 
        static let f1 (x1:int,x2:int) (x3:int,x4:int) = x1 + x2 + x3 + x4
        static let v5 = f1 (2,3) (4,5)
        static member Result =     
            f1 (6,7) (8,9) + v5

    printfn "c.Result = %d" C.Result
    test "vrewiorvw09w" (C.Result = 44)

module StaticFunctionInGenericClass =
    type C<'a>() = 
        static let f1 (x1:int) = x1 + 3 + sizeof<'a>
        static let v5 = f1 3
        static member Result =     
            f1 6  + v5

    printfn "C<int>.Result = %d" (C<int>.Result)
    printfn "C<System.DateTime>.Result = %d" (C<System.DateTime>.Result)
    test "vrewiorvw09q" (C<int>.Result = 23)
    test "vrewiorvw099" (C<System.DateTime>.Result = 31)

module StaticGenericFunctions =
    type C() = 
        static let f1 x1 = x1
        static let f2 (x : 'a) : 'a when 'a :> System.IComparable = x
        static let v5 = f1 3
        static let v6 = f1 "hello"
        static let v7 = f2 1
        static let v8 = f2 "hello"
        static member Result =     
            f1 6  + v5 + v6.Length

    printfn "c.Result = %d" C.Result
    test "vrewiorvw098" (C.Result = 14)

module StaticGenericFunctionInGenericClass =
    type C<'a>() = 
        static let f1 x1 = x1 
        static let rec f2 n x1 = if n = 0 then x1  else f2 (n-1) x1
        static let rec f3 n (x1:int) = if n = 0 then x1  else f2 (n-1) x1
        static let v5 = f1 3 + sizeof<'a>
        static let v6 = f1 "hello"
        static let v7 = f2 8 3 + sizeof<'a>
        static let v8 = f2 10 "hello"
        static let v9 = f3 8 3 + sizeof<'a>
        static member Result =     
            f1 6  + v5 + v6.Length + v7 + v8.Length + f2 8 6 + f3 8 6
        static member Functions =     
            (f1 : int -> int), (f1 : string -> string),
            (f2 : int -> int -> int), (f2 : int -> string -> string),
            // Non generic function in generic class used as a first class value
            (f3 : int -> int -> int)

    printfn "C<int>.Result = %d" (C<int>.Result)
    printfn "C<System.DateTime>.Result = %d" (C<System.DateTime>.Result)
    test "vrewiorvw097" (C<int>.Result = 42)
    test "vrewiorvw096" (C<System.DateTime>.Result = 50)

module StaticTailCallLoop =
    type C<'a>() = 
        static let rec f x = if x = 0 then sizeof<'a> else f (x  - 1)
        static let v5 = f 10000000
        static member Result =     
            f 10000000 + v5

    printfn "C<int>.Result = %d" (C<int>.Result)
    printfn "C<System.DateTime>.Result = %d" (C<System.DateTime>.Result)
    test "vrewiorvw095" (C<int>.Result = 8)
    test "vrewiorvw094" (C<System.DateTime>.Result = 16)

module StaticFunctionsAsLambdas =
    type C() = 
        static let f0 = fun () -> 1
        static let f1 = fun (x:int) -> x
        static let f2 = fun (x:int) -> f1 x + f0()
        static let rec f3 = fun (x:int) -> 2
        static let f4 = fun (x:int) -> f3 x + f0()
        static let v = f4 5
        static let f5 = fun (x:int) -> f4 v + 6
        static let rec f6 = fun (x:int) -> if x = 0 then 6 else f6 (x-1)
        static let v2 = f6 5
        static let v3 = List.map f0 [(); ()]
        static let v4 = List.map f1 [1;2]
        static let v5 = List.map f3 [1;2]
        static let f6 = fun (x1:int) x2 -> x1 + x2
        static let v6 = f6 2 3
        static let f7 = fun (x1:int,x2:int) (x3:int,x4:int) -> x1 + x2 + x3 + x4
        static let v7 = f7 (2,3) (4,5)
        static let f8 = fun (x1:int,x2:int) () -> x1 + x2 
        static let v8 = f8 (2,3) ()
        static let f9 = fun () -> fun (x1:int,x2:int) -> x1 + x2 
        static let v9 = f9 () (2,3) 
        static let f10 = fun  x1 -> x1
        static let v10a = f10 3
        static let v10b = f10 "hello"
        static member Result =     
            List.sum v3 + List.sum v4 + List.sum v5 + v2 + v + v6 + v7 + v8 + v9 + v10a + v10b.Length

    printfn "c.Result = %d" C.Result
    test "vrewiorvw093" (C.Result = 55)



module StaticPathological1 =
    type C<'a>() = 
        static let mutable f1  = fun x1 -> x1 
        static let v5 = f1 3 + sizeof<'a>
        static member Result =     
            f1 6  + v5 

    printfn "C<int>.Result = %d" (C<int>.Result)
    printfn "C<System.DateTime>.Result = %d" (C<System.DateTime>.Result)
    test "vrewiorvw092" (C<int>.Result = 13)
    test "vrewiorvw091" (C<System.DateTime>.Result = 17)

module ImplicitOperatorTests =
    type Foo(x : int) = 
        member this.Val = x
        
        static member (-->) ((src : Foo), (target : Foo)) = new Foo(src.Val + target.Val)
        static member (-->) ((src : Foo), (target : int)) = new Foo(src.Val + target)

        static member (!!) (src : Foo) = new Foo(-src.Val)
            
        static member (+) ((src : Foo), (target : Foo)) = new Foo(src.Val + target.Val)
        static member (+) ((src : Foo), (target : int)) = new Foo(src.Val + target)
        
    let x = Foo(3) --> 4  
    let y = Foo(3) --> Foo(5)  
    let z = (-->) (Foo(3)) (Foo(5))
    let f1 : Foo -> Foo = (-->) (Foo(3)) 
    let _  = f1  (Foo(5)) 
    let f2 : int -> Foo = (-->) (Foo(3)) 
    let _  = f2  3
    let x2 = Foo(3) + 4  
    let y2 = Foo(3) + Foo(4)  
    let z2 = !!Foo(3) 

module SlicingTests =
    let s1 = "abcdef"

    check "ce0cew9j1" s1.[2..] "cdef"
    check "ce0cew9j2" s1.[2..2] "c"
    check "ce0cew9j3" s1.[5..5] "f"
    check "ce0cew9j4" s1.[0..0] "a"
    check "ce0cew9j5" s1.[2..1] ""
    check "ce0cew9j6" s1.[0.. -1] ""
    check "ce0cew9j7" s1.[2..3] "cd"
    check "ce0cew9j8" s1.[2..4] "cde"
    check "ce0cew9j9" s1.[2..5] "cdef"


    let a1 = [| for c in "abcdef" -> c |]

    check "ce0cew9jq" a1.[2..] [| for c in "cdef" -> c |]

    a1.[3..] <- [| for c in "DEF" -> c |]

    check "ce0cew9jw" a1.[2..] [| for c in "cDEF" -> c |]

    let m1 = Array2D.init 6 7 (fun i j -> i + 2*j)

    check "ce0cew9je" m1 (Array2D.init 6 7 (fun i j -> i + 2*j))
    for i = 1 to 5 do 
        for j = 1 to 6 do 
            check "ce0cew9jr" m1.[1..i,1..j] (Array2D.init i j (fun i j -> (i+1) + 2*(j+1)))


    m1.[1..3,1..4] <- Array2D.zeroCreate 3 4

    for i = 1 to 5 do 
        for j = 1 to 6 do 
            check "ce0cew9js" m1.[1..i,1..j] (Array2D.init i j (fun i j -> if (i+1) >= 1 && (i+1) <= 3 && (j+1) >= 1 && (j+1) <= 4 then 0 else (i+1) + 2*(j+1)))



    type System.Int32 with 
        member x.GetSlice(idx1,idx2) = 
            let idx1 = defaultArg idx1 0
            let idx2 = defaultArg idx2 17
            x % (17 * idx1 + idx2)
        member x.GetSlice(idx1,idx2,idx3,idx4) = 
            let idx1 = defaultArg idx1 0
            let idx2 = defaultArg idx2 17
            let idx3 = defaultArg idx3 17
            let idx4 = defaultArg idx4 17
            x % (17 * idx1 + idx2 + idx3 + idx4)

    (3).[3..4]
    (3).[3..]
    (3).[..4]

    (3).[3..4,4..5]
    (3).[3..,6..7]
    (3).[..4,8..9]
    (3).[..4,*]
    (3).[*,*]

    type Foo() =
        member this.GetSlice(lb1:int option, ub1:int option) = [1]
        member this.SetSlice(lb1:int option, ub1:int option, v2D: int list) = ()
 
    let f = new Foo()
 
    let vs = f.[1 .. 3]
    f.[1..3] <- [3]
    f.[..3] <- [3]
    f.[1..] <- [3]
    f.[*] <- [3]
 
 module Bug_FSharp_1_0_3246 =
    type r1 =  
        { x : int }
        static member Empty = { x = 3 } 
       
    and r2 = 
        { x : int }
        static member Empty = { x = 3 }

    let r1 : r1 = r1.Empty
    let r2 : r2 = r2.Empty

module First_class_use_of_non_overloaded_method_should_detuple_1 =

    let count = ref 0
    type A() = 
        let f (x,y) = incr count; printfn "hello"; incr count
        member this.M0 () = f (1,2)
        member this.M1 (x:int) = f (x,x)
        member this.M2 (x:int,y:float) = f (x,y)

        static member SM0 () = incr count; printfn "hello"; incr count
        static member SM1 (x:int) = incr count; printfn "hello"; incr count
        static member SM2 (x:int,y:float) = incr count; printfn "hello"; incr count


    [| (1,3.0) |] |> Array.iter (fun (x,y) -> A.SM2 (x,y))
    [| (1,3.0) |] |> Array.iter (fun d -> A.SM2 d)
    [| (1,3.0) |] |> Array.iter A.SM2 

    [| 1;2;3 |] |> Array.iter (fun d -> A.SM1 d)
    [| 1;2;3 |] |> Array.iter A.SM1 

    [| ();();() |] |> Array.iter (fun d -> A.SM0())
    [| ();();() |] |> Array.iter A.SM0 

    let a = A()
    [| (1,3.0) |] |> Array.iter (fun (x,y) -> a.M2 (x,y))
    [| (1,3.0) |] |> Array.iter (fun d -> a.M2 d)
    [| (1,3.0) |] |> Array.iter a.M2 

    [| 1;2;3 |] |> Array.iter (fun d -> a.M1 d)
    [| 1;2;3 |] |> Array.iter a.M1 

    [| ();();() |] |> Array.iter (fun d -> a.M0())
    [| ();();() |] |> Array.iter a.M0 

module First_class_use_of_non_overloaded_method_should_detuple_2 =

    let count = ref 0
    type A<'a>() = 
        let f (x,y) = incr count; printfn "hello"; incr count
        member this.M0 () = f (1,2)
        member this.M1 (x:'a) = f (x,x)
        member this.M2 (x:'a,y:'a) = f (x,y)

        static member SM0 () = incr count; printfn "hello"; incr count
        static member SM1 (x:'a) = incr count; printfn "hello"; incr count
        static member SM2 (x:'a,y:'a) = incr count; printfn "hello"; incr count


    [| (1,3) |] |> Array.iter (fun (x,y) -> A.SM2 (x,y))
    [| (1,3) |] |> Array.iter (fun d -> A.SM2 d)
    [| (1,3) |] |> Array.iter A.SM2 

    [| 1;2;3 |] |> Array.iter (fun d -> A.SM1 d)
    [| 1;2;3 |] |> Array.iter A.SM1 

    [| ();();() |] |> Array.iter (fun d -> A.SM1 d)
    [| ();();() |] |> Array.iter A.SM1 

    [| ();();() |] |> Array.iter (fun d -> A<int>.SM0())
    [| ();();() |] |> Array.iter A<int>.SM0 

    let a = A<int>()
    [| (1,3) |] |> Array.iter (fun (x,y) -> a.M2 (x,y))
    [| (1,3) |] |> Array.iter (fun d -> a.M2 d)
    [| (1,3) |] |> Array.iter a.M2 

    let au = A<unit>()
    [| ();();() |] |> Array.iter (fun d -> au.M1 d)
    [| ();();() |] |> Array.iter au.M1 

    [| 1;2;3 |] |> Array.iter (fun d -> a.M1 d)
    [| 1;2;3 |] |> Array.iter a.M1 

    [| ();();() |] |> Array.iter (fun d -> a.M0())
    [| ();();() |] |> Array.iter a.M0 


module First_class_use_of_non_overloaded_method_should_detuple_3 =

    let count = ref 0
    type A() = 
        let f (x,y) = ()
        member this.M0 () = f (1,2)
        member this.M1 (x:'a) = f (x,x)
        member this.M2 (x:'a,y:'a) = f (x,y)

        static member SM0 () = ()
        static member SM1 (x:'a) = ()
        static member SM2 (x:'a,y:'a) = ()


    [| (1,3) |] |> Array.iter (fun (x,y) -> A.SM2 (x,y))
    [| (1,3) |] |> Array.iter (fun d -> A.SM2 d)
    [| (1,3) |] |> Array.iter A.SM2 

    [| 1;2;3 |] |> Array.iter (fun d -> A.SM1 d)
    [| 1;2;3 |] |> Array.iter A.SM1 

    [| ();();() |] |> Array.iter (fun d -> A.SM1 d)
    [| ();();() |] |> Array.iter A.SM1 

    [| ();();() |] |> Array.iter (fun d -> A.SM0())
    [| ();();() |] |> Array.iter A.SM0 

    let a = A()
    [| (1,3) |] |> Array.iter (fun (x,y) -> a.M2 (x,y))
    [| (1,3) |] |> Array.iter (fun d -> a.M2 d)
    [| (1,3) |] |> Array.iter a.M2 

    [| ();();() |] |> Array.iter (fun d -> a.M1 d)
    [| ();();() |] |> Array.iter a.M1 

    [| 1;2;3 |] |> Array.iter (fun d -> a.M1 d)
    [| 1;2;3 |] |> Array.iter a.M1 

    [| ();();() |] |> Array.iter (fun d -> a.M0())
    [| ();();() |] |> Array.iter a.M0 

module Bug_1438 = 

    type Tuples =
        static member Sel1 t = match t with | (a,_) -> a

        static member Sel2 t = match t with | (_,b) -> b

    let unzip (xs : seq<'a * 'b>) : seq<'a> * seq<'b> =
        ( xs |> Seq.map (fun x -> Tuples.Sel1 x),
          xs |> Seq.map (fun x -> Tuples.Sel2 x) )

    let unzip2 (xs : seq<'a * 'b>) : seq<'a> * seq<'b> =
        ( xs |> Seq.map Tuples.Sel1,
          xs |> Seq.map Tuples.Sel2 )

module InheritingFromPartiallyImplementedTypes = 
    module PositiveTests = 



        module Test2 = 
            type ITest =
                abstract member Meth1: string -> string

            type ITestSub =
                inherit ITest
                abstract member Meth2: int -> int

            type OkComplete () =
                interface ITest with
                    override this.Meth1 x = x + "1"
                interface ITestSub with
                    override this.Meth2 x = x + 1

            module Throwaway =
                let foo = OkComplete()


        module Test4 = 
            [<AbstractClass>]
            type wire<'a>() =
                abstract Send   : 'a -> unit

            let createWire() =
              {new wire<'a>() with 
                  member obj.Send(x:'a)   = ()
              }

            type CreateWire<'a>() =
              inherit wire<'a>() 
              override obj.Send(x)   = ()


        module Test5 = 
            type ITest =
                abstract member Meth1: int -> int

            type ITestSub =
                inherit ITest
                abstract member Meth1: int -> int

            [<AbstractClass>]
            type Partial() =
                abstract Meth1 : int -> int
                interface ITest with
                    override this.Meth1 x = this.Meth1 x
                interface ITestSub with
                    override this.Meth1 x = this.Meth1 x

            type OkComplete () =
                inherit Partial()
                override this.Meth1 x = x

            module Throwaway =
                let foo = OkComplete()

    module MorePositiveTests =


          type IEnumerator<'T> = 
              abstract Current : 'T
          
          [<AbstractClass>]
          type MapEnumerator<'T> (x:'T) =
              interface IEnumerator<'T> with
                  member this.Current = x
              
          type MapE<'U>() =
              inherit MapEnumerator<'U>(failwith "")


    module MorePositiveTEsts2 = 
        open System.Reflection

        type IDelegateEvent<'del when 'del :> System.Delegate > =
            abstract AddHandler: handler:'del -> unit

        type IEvent<'del,'args when 'del :> System.Delegate > =
            inherit IDelegateEvent<'del>

        let f<'Delegate,'Args when 'Delegate :> System.Delegate >() = 
                { new IEvent<'Delegate,'Args> with 
                    member x.AddHandler(d) = () }


    module MorePositiveTEsts3 = 

        type IEvent<'del,'a> = 
            abstract Add : ('a -> unit) -> unit
            
        [<AbstractClass>]
        type wire<'a>() =
            abstract Send   : 'a -> unit
            abstract Listen : ('a -> unit) -> unit
            interface IEvent<Handler<'a>,'a> with
                member x.Add(handler) = x.Listen(handler)

        let createWire() =
            let listeners = ref [] in
            {new wire<'a>() with   // Expect an error here - Add2 no implemented
              member obj.Send(x)   = List.iter (fun f -> f x) !listeners   
              member obj.Listen(f) = listeners := f :: !listeners
            }

module MoreConstraintTEsts = 
    type C1<'T when 'T :> System.Array>() =
        member x.P = 1

    type C2<'T when 'T :> System.Delegate>() =
        member x.P = 1

    type C3<'T when 'T :> System.Enum>() =
        member x.P = 1


    type C4<'T when 'T :> System.ValueType>() =
        member x.P = 1

    type C5<'T when 'T :> System.IComparable and 'T :> System.IComparable>() =
        member x.P = 1


    type C6<'T when 'T :> System.Array and 'T :> System.Array>() =
        member x.P = 1

    type C7<'T when 'T :> System.Array and 'T :> System.IComparable>() =
        member x.P = 1
// Can't constraint to multiple class types anymore
// It is ok to constrain to multiple interfaces though.
    type C8<'T when 'T :> System.Array and 'T :> System.IComparable and 'T :> System.ICloneable>() =
        member x.P = 1

    let f x = new C8<_>()

module FinalizerTEst = 
    type S<'a>(x:'a) =   class [<DefaultValue(false)>] val mutable x : 'a override x.Finalize() = printfn "hello" end

    let x = S(3) |> box

module CheckBoxingSemantics = 
    module StructTest1 = 

        type IX = abstract ToStringMember : unit -> string


        [<Struct>]
        type Counter = 
          interface IX with 
            member x.ToStringMember() = x.value <- x.value + 1; x.value.ToString()
           
          [<DefaultValue>]
          val mutable value : int 

        let mutable x = new Counter();

        let f (x:'a when 'a :> IX) = 
            [ x.ToStringMember(); x.ToStringMember(); x.ToStringMember() ]

        check "vklnvwer0" (f x) ["1"; "1"; "1"]

    module StructTest2 = 


        [<Struct>]
        type Counter = 
          [<DefaultValue>]
          val mutable value : int 
          member x.ToStringMember() = x.value <- x.value + 1; x.value.ToString()

        let mutable x = new Counter();

        check "vklnvwer0" [ x.ToStringMember(); x.ToStringMember(); x.ToStringMember() ] ["1"; "2"; "3"]

    module StructTest3 = 

        [<Struct>]
        type Counter = 
          [<DefaultValue>]
          val mutable value : int 
          member x.ToStringMember() = x.value <- x.value + 1; x.value.ToString()

        let mutable x = Array.init 3 (fun i -> new Counter());
        check "vklnvwer0" [ x.[0].ToStringMember(); x.[0].ToStringMember(); x.[0].ToStringMember() ] ["1"; "2"; "3"]

    module StructTest4 = 

        [<Struct>]
        type Counter = 
          [<DefaultValue>]
          static val mutable private value : int 
          member x.ToStringMember() = Counter.value <- Counter.value + 1; Counter.value.ToString()

        let mutable x = Array.init 3 (fun i -> new Counter());
        check "vklnvwer0" [ x.[0].ToStringMember(); x.[0].ToStringMember(); x.[0].ToStringMember() ] ["1"; "2"; "3"]




module InterfaceExtendsSystemIComparable_Bug4919 = 
      open System.Collections
      open System.Collections.Generic

      exception EmptyQueue

      type Queue<'a> =
        inherit IEnumerable<'a>
        inherit IEnumerable
        inherit System.IEquatable<Queue<'a>>
        inherit System.IComparable
        abstract IsEmpty : bool
        abstract PushBack : 'a -> Queue<'a>
        abstract PopFront : unit -> 'a * Queue<'a>

      let private queueEnumerator (q: Queue<_>) =
        let decap (q: Queue<_>) = if q.IsEmpty then None else Some(q.PopFront())
        (Seq.unfold decap q).GetEnumerator()

      module BatchedQueue =
        let rec private mk(f, r) =
          let mk = function
            | [], r -> mk(List.rev r, [])
            | q -> mk q
          { new Queue<'a> with
              override q.IsEmpty =
                match f with
                | [] -> true
                | _ -> false

              override q.PushBack x =
                mk(f, x::r)

              override q.PopFront() =
                match f with
                | x::f -> x, mk(f, r)
                | [] -> raise EmptyQueue

              override q.GetEnumerator() =
                ((q: Queue<'a>).GetEnumerator() :> IEnumerator)

              override q.GetEnumerator() : IEnumerator<'a> =
                queueEnumerator q

              override q1.Equals(q2: Queue<'a>) =
                List.ofSeq q1 = List.ofSeq q2

              override q1.CompareTo(q2: obj) =
                let q2 = unbox<Queue<'a>> q2
                Seq.compareWith compare q1 q2
         }

        let empty<'a when 'a : equality and 'a : comparison> = mk<'a>([], [])

        let add (q: Queue<_>) x = q.PushBack x

        let take (q: Queue<_>) = q.PopFront()

      let time f x =
        let t = System.Diagnostics.Stopwatch.StartNew()
        try f x finally
        printf "Took %dms\n" t.ElapsedMilliseconds

      let () =
        time (Seq.length << Seq.fold BatchedQueue.add BatchedQueue.empty) (seq {1 .. 100})
        |> printf "Queue length %d\n"

module AllowExtensionsWithinARecursiveGroup = begin
    type System.Object with 
       member x.Name = "a"

    and System.String with 
       member x.Freddy = "b"

    and C with  
       member x.FreddyAndName = let a,b = x.NameAndFreddy in b,a
       
    and C(s:string) = 
       member x.NameAndFreddy = s.Name, s.Freddy

end



module PreferOverloadsUsingSystemFunc = begin

    type Foo() = 
        static member Method(x:System.Action) = 1
        static member Method(x:System.Func<'T>) = 2

    Foo.Method(fun _ -> ()) // expect return result 2
end


module AllowNullLiteralTest = begin

    [<AllowNullLiteral>]
    type I = 
        interface
          abstract P : int
        end

    let i = (null : I)

    let i2 = ((box null) :?>  I)


    [<AllowNullLiteral>]
    type C() = 
        member x.P = 1

    let c = (null : C)

    let c2 = ((box null) :?>  C)


    [<AllowNullLiteral>]
    type D() = 
        inherit C()
        interface I with 
            member x.P = 2
        member x.P = 1

    let d = (null : D)

    let d2 = ((box null) :?>  D)

end

module UnionConstructorsAsFirstClassValues = begin
    type A =
        | A of int * int
        
    let x = (1, 1)
    let y = (A) x // OK
    let z = A x // expect this to be ok
end


module ExtensionMemberTests = 
    module Set1 = 
        type System.DateTime with 
           static member Prop1 = (1,1)

        type System.DateTime with 
           static member Meth1(s:string) = (1,2)

        type System.DateTime with 
           static member Meth2(s:string) = (1,2)

        type System.DateTime with 
           member x.InstanceProp1 = (1,1)

        type System.DateTime with 
           member x.InstanceMeth1(s:string) = (1,2)

        type System.DateTime with 
           member x.InstanceMeth2(s:string) = (1,2)


    module Set2 = 
        type System.DateTime with 
           static member Prop1 = (2,2)

        type System.DateTime with 
           static member Meth1(s:string) = (2,1)

        type System.DateTime with 
           static member Meth2(s:obj) = (2,1)

        type System.DateTime with 
           member x.InstanceProp1 = (2,2)

        type System.DateTime with 
           member x.InstanceMeth1(s:string) = (2,1)

        type System.DateTime with 
           member x.InstanceMeth2(s:obj) = (2,1)
    
    // overloaded indexed extension properties
    module Set3A =
        type A() = 
            member m.Item with get(a: string) = "Item(string)"
            member m.Item with set(a: string) (value:string) = failwith "Setting Item(string) string"
            member m.Item with set(a: bool) (value:string) = failwith "Setting Item(bool) string"
                
            member m.Prop with get(a: string, b:string) = "Prop(string, string)"
            member m.Prop with set(a: string, b: string) (value:string) = failwith "Setting Prop(string, string) string"
            member m.Prop with set(a: string, b: bool) (value:string) = failwith "Setting Prop(string, bool) string"
            
            member m.Plain with get() = "Intrinsic plain"
            member m.Plain with set(a:string) = failwith "Setting intrinsic plain"
            
            member m.NonIndexed with get() = "Intrinsic nonindexed"
            
            member m.Indexed with get(a: string) = "Intrinsic indexed"


    module Set3B =
        type Set3A.A with
            member m.Item with get(a: bool) = "Item(bool)"
            member m.Item with set(a: bool) (value: bool) = failwith "Setting Item(bool) bool"
            member m.Item with set(a: string) (value: bool) = failwith "Setting Item(string) bool"
                
            member m.Prop with get(a: string, b:bool) = "Prop(string, bool)"
            member m.Prop with set(a: string, b:bool) (value: bool) = failwith "Setting Prop(string, bool) bool"
            member m.Prop with set(a: string, b:string) (value: bool) = failwith "Setting Prop(string, string) bool"
            
            member m.Plain with get() = "Extension plain"
            member m.Plain with set(a:string) = failwith "Setting extension plain"
            
            member m.NonIndexed with get(a: bool) = "Extension nonindexed"
            
            member m.Indexed with get() = "Extension indexed"
            
            member m.ExtensionOnly with get(a: bool) = "ExtensionOnly(bool)"
            
        type System.Net.WebHeaderCollection with
            member m.Item with get(a : bool) = "ExtensionGet"

    module Tests1 = 
        open Set1
        open Set2
        check "fewnr-0vrwep0" System.DateTime.Prop1 (2,2)
        check "fewnr-0vrwep1" (System.DateTime.Meth1("a")) (2,1)
        check "fewnr-0vrwep2" (System.DateTime.Meth2("a")) (1,2) // Set1 always preferred due to more precise type

        check "fewnr-0vrwep0" System.DateTime.Now.InstanceProp1 (2,2)
        check "fewnr-0vrwep1" (System.DateTime.Now.InstanceMeth1("a")) (2,1)
        check "fewnr-0vrwep2" (System.DateTime.Now.InstanceMeth2("a")) (1,2) // Set1 always preferred due to more precise type

    module Tests2 = 
        open Set2
        open Set1
        check "fewnr-0vrwep3" System.DateTime.Now.InstanceProp1 (1,1)
        check "fewnr-0vrwep4" (System.DateTime.Now.InstanceMeth1("a")) (1,2)
        check "fewnr-0vrwep5" (System.DateTime.Now.InstanceMeth2("a")) (1,2) // Set1 always preferred due to more precise type

    module Tests3 = 
        open Set3A
        open Set3B
        
        let checkSet testName (f : unit -> unit) expected =
            let result = 
                let mutable res = ""
                try f() with | e -> res <- e.Message
                res
                
            check testName result expected
        
        let foo = A()
        
        // getters
        check "ExtensionProps 0" (foo.[""]) "Item(string)"
        check "ExtensionProps 1" (foo.[true]) "Item(bool)"

        check "ExtensionProps 2" (foo.Prop("", "")) "Prop(string, string)"
        check "ExtensionProps 3" (foo.Prop("", true)) "Prop(string, bool)"
        
        // in case of exact duplicate, per spec intrinsic should be preferred
        check "ExtensionProps 4" (foo.Plain) "Intrinsic plain"
        
        check "ExtensionProps 5" (foo.NonIndexed) "Intrinsic nonindexed"
        // not expected to work:  check "ExtensionProps 6" (foo.NonIndexed(true)) "Extension nonindexed"
        
        check "ExtensionProps 7" (foo.Indexed("")) "Intrinsic indexed"
        // not expected to work:  check "ExtensionProps 8" (foo.Indexed) "Extension indexed"
        
        check "ExtensionProps 9" (System.Net.WebHeaderCollection().[true]) "ExtensionGet"
        
        // setters
        checkSet "ExtensionProps 10" (fun () -> foo.[""] <- "") "Setting Item(string) string"
        checkSet "ExtensionProps 11" (fun () -> foo.[true] <- "") "Setting Item(bool) string"
        checkSet "ExtensionProps 13" (fun () -> foo.[true] <- true) "Setting Item(bool) bool"
        checkSet "ExtensionProps 14" (fun () -> foo.[""] <- true) "Setting Item(string) bool"
        
        checkSet "ExtensionProps 16" (fun () -> foo.Prop("", "") <- "") "Setting Prop(string, string) string"
        checkSet "ExtensionProps 17" (fun () -> foo.Prop("", true) <- "") "Setting Prop(string, bool) string"
        checkSet "ExtensionProps 19" (fun () -> foo.Prop("", true) <- true) "Setting Prop(string, bool) bool"
        checkSet "ExtensionProps 20" (fun () -> foo.Prop("", "") <- true) "Setting Prop(string, string) bool"
        
        checkSet "ExtensionProps 22" (fun () -> foo.Plain <- "") "Setting intrinsic plain"

module AccessThisAsPartOfSUperClassConstruction1 = 
    open System
    type Z(x : obj) = class end

    type X() as Y =
      class
       inherit Z(Y.x)
       do
         ()
       [<DefaultValue>]
       val mutable x : int
      end


module AccessThisAsPartOfSUperClassConstruction2 = 

    module Test1 = 
       open System
       type Z(x : obj) = class end

       type X() as Y =
         class
          inherit Z(Y.x)
          do
            ()
          [<DefaultValue>]
          val mutable x : int
         end



    module Test2 = 
       open System
       type Z(x : obj) = class end

       type X() as Y =
         class
          inherit Z(Y.x)
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = Y
         end

    module Test3 = 
       open System
       type Z(x : obj) = class end

       type X() as Y =
         class
          inherit Z(Y.x)
          let getThis () = Y
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = getThis()
         end

    module Test4 = 
       open System
       type Z(x : obj) = class end

       type X() as Y =
         class
          inherit Z(Y.x)
          let ths = Y
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = ths
         end




    module GenericTest1 = 
       open System
       type Z(x : obj) = class end

       type X<'T>() as Y =
         class
          inherit Z(Y.x)
          do
            ()
          [<DefaultValue>]
          val mutable x : int
         end



    module GenericTest2 = 
       open System
       type Z(x : obj) = class end

       type X<'T>() as Y =
         class
          inherit Z(Y.x)
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = Y
         end

    module GenericTest3 = 
       open System
       type Z(x : obj) = class end

       type X<'T>() as Y =
         class
          inherit Z(Y.x)
          let getThis () = Y
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = getThis()
         end

    module GenericTest4 = 
       open System
       type Z(x : obj) = class end

       type X<'T>() as Y =
         class
          inherit Z(Y.x)
          let ths = Y
          do
            ()
          [<DefaultValue>]
          val mutable x : int

          member self.ThisPtr = ths
         end


module AccessThisAsPartOfSUperClassConstruction3 = 

    type B(obj:obj) = 
        member x.P = 1

    type C1() as a = 
        inherit B((fun () -> a)) // captures 'this' as part of call to super class constructor
        member x.P = 1
        
    type C2() as a = 
        inherit B((fun () -> a)) // captures 'this' as part of call to super class constructor
        member x.P = a // and captures it in a method


module EqualityComparisonPositiveTests = 


    module StructDefaultConstructorCantBeUsed =

        [<Struct>]
        type U1(v:int) = 
            member x.P = v

        let v1 = U1()  // can be used - expect no error


    module BasicExample1 =
        let f1 (x : list<int>) = (x = x) // expect ok
        let f2 (x : option<int>) = (x = x) // expect ok
        let f3 (x : Choice<int,int>) = (x = x) // expect ok
        let f4 (x : Choice<int,int,int>) = (x = x) // expect ok
        let f5 (x : Choice<int,int,int,int>) = (x = x) // expect ok
        let f6 (x : Choice<int,int,int,int,int>) = (x = x) // expect ok
        let f7 (x : Choice<int,int,int,int,int,int>) = (x = x) // expect ok
        let f8 (x : Choice<int,int,int,int,int,int,int>) = (x = x) // expect ok
        let f9 (x : ref<int>) = (x = x) // expect ok
        let fq (x : Set<int>) = (x = x) // expect ok
        let fw (x : Map<int,int>) = (x = x) // expect ok
        
        let fe (x : list<System.Type>) = (x = x) // expect ok
        let fr (x : option<System.Type>) = (x = x) // expect ok
        let ft (x : Choice<System.Type,int>) = (x = x) // expect ok
        let fy (x : Choice<System.Type,int,int>) = (x = x) // expect ok
        let fu (x : Choice<System.Type,int,int,int>) = (x = x) // expect ok
        let fi (x : Choice<System.Type,int,int,int,int>) = (x = x) // expect ok
        let fo (x : Choice<System.Type,int,int,int,int,int>) = (x = x) // expect ok
        let fp (x : Choice<System.Type,int,int,int,int,int,int>) = (x = x) // expect ok
        let fa (x : ref<System.Type>) = (x = x) // expect ok


        let fn (x : Set<list<int>>) = () // expect ok
        let fm (x : Set<option<int>>) = () // expect ok
        let fQ (x : Set<ref<int>>) = () // expect ok
        let fW (x : Set<Set<int>>) = () // expect ok
        let fE (x : Set<Map<int,int>>) = () // expect ok
        let fO (x : Set<int * int>) = () // expect ok
        let fP (x : Set<int * int * int>) = () // expect ok
        let fA (x : Set<int * int * int * int>) = () // expect ok
        let fS (x : Set<int * int * int * int * int>) = () // expect ok
        let fD (x : Set<int * int * int * int * int * int>) = () // expect ok
        let fF (x : Set<int * int * int * int * int * int * int>) = () // expect ok
        let fG (x : Set<int * int * int * int * int * int * int * int>) = () // expect ok
        let fH (x : Set<int * int * int * int * int * int * int * int * int >) = () // expect ok
        let fJ (x : Set<int * int * int * int * int * int * int * int * int * int>) = () // expect ok
        let fK (x : Set<int * int * int * int * int * int * int * int * int * int * int>) = () // expect ok

        type R<'T> = R of 'T * R<'T>
        let r1 (x : Set<R<int>>) = () // expect ok
        let r3 (x : R<int>) = (x = x) // expect ok
        let r4 (x : R<System.Type>) = (x = x) // expect ok

        //type R2<'T> = | R2 : 'T * R2<'T> -> R2<'T>
        //let q1 (x : Set<R2<int>>) = () // expect ok
        //let q3 (x : R2<int>) = (x = x) // expect ok
        //let q4 (x : R2<System.Type>) = (x = x) // expect ok
        
    module Example1 =
        type X<'T> = X of 'T

        let f0 (x : Set<X<int>>) = ()  // expect ok
        let f1 (x : Set<X<'T>>) = ()  // expect ok
        let f3 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example1_Record =
        type X<'T> = { r : 'T }

        let f0 (x : Set<X<int>>) = ()  // expect ok
        let f1 (x : Set<X<'T>>) = ()  // expect ok
        let f3 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example1_Struct =
        type X<'T> = struct val r : 'T  end

        let f0 (x : Set<X<int>>) = ()  // expect ok
        let f1 (x : Set<X<'T>>) = ()  // expect ok
        let f3 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example1_StructImplicit =
        [<Struct; StructuralComparison; StructuralEquality>]
        type X<[<EqualityConditionalOn;ComparisonConditionalOn>] 'T>(r:'T) = struct member x.R = r  end

        let f0 (x : Set<X<int>>) = ()  // expect ok
        let f1 (x : Set<X<'T>>) = ()  // expect ok
        let f3 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example1_StructImplicit2 =
        [<Struct>]
        type X<[<EqualityConditionalOn;ComparisonConditionalOn>] 'T>(r:'T) = struct member x.R = r  end

        let f0 (x : Set<X<int>>) = ()  // expect ok
        let f1 (x : Set<X<'T>>) = ()  // expect ok
        let f3 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example2 = 
        type X<'T> = X of list<'T>

        let f0 (x : Set<X<int>>) = ()   // expect ok
        let f1 (x : Set<X<'T>>) = ()   // expect ok

        let f4 (x : X<list<int>>) = (x = x) // expect ok
        let f6 (x : X<list<System.Type>>) = (x = x) // expect ok


    module Example3 = 
        type X<'T> = X of Y<'T>
        and Y<'T> = Y of 'T

        let f0 (x : Set<X<int>>) = ()   // expect ok
        let f1 (x : Set<X<'T>>) = ()   // expect ok

        let f4 (x : X<list<int>>) = (x = x) // expect ok
        let f6 (x : X<list<System.Type>>) = (x = x) // expect ok

    module Example4 = 
        type X<'T> = X of Y<'T>
        and Y<'T> = Y of 'T * X<'T>

        let f0 (x : Set<X<int>>) = ()   // expect ok
        let f1 (x : Set<X<'T>>) = ()   // expect ok

        let f4 (x : X<list<int>>) = (x = x) // expect ok
        let f6 (x : X<list<System.Type>>) = (x = x) // expect ok

        let g0 (x : Set<Y<int>>) = ()   // expect ok
        let g1 (x : Set<Y<'T>>) = ()   // expect ok

        let g4 (x : Y<list<int>>) = (x = x) // expect ok
        let g6 (x : Y<list<System.Type>>) = (x = x) // expect ok

    module Example5 = 
        type X<'T> = X of Y<'T>
        and Y<'T> = Y of int

        let f0 (x : Set<X<int>>) = ()   // expect ok
        let f1 (x : Set<X<'T>>) = ()   // expect ok
        let f2 (x : Set<X<System.Type>>) = () // expect ok
        let f3 (x : Set<X<list<System.Type>>>) = () // expect ok

        let f4 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<int -> int>) = (x = x) // expect ok
        let f6 (x : X<list<System.Type>>) = (x = x) // expect ok
        let f7 (x : X<list<int -> int>>) = (x = x) // expect ok

        let g0 (x : Set<Y<int>>) = ()   // expect ok
        let g1 (x : Set<Y<'T>>) = ()   // expect ok
        let g2 (x : Set<Y<System.Type>>) = () // expect ok
        let g3 (x : Set<Y<list<System.Type>>>) = () // expect ok

        let g4 (x : Y<list<int>>) = (x = x) // expect ok
        let g5 (x : Y<int -> int>) = (x = x) // expect ok
        let g6 (x : Y<list<System.Type>>) = (x = x) // expect ok
        let g7 (x : Y<list<int -> int>>) = (x = x) // expect ok

    module Example6 = 
        type X<'T> = X of Y<int,'T>
        and Y<'T,'U> = Y of 'T * X<'T>

        let f0 (x : Set<X<int>>) = ()   // expect ok
        let f1 (x : Set<X<'T>>) = ()   // expect ok
        let f2 (x : Set<X<System.Type>>) = () // expect ok
        let f3 (x : Set<X<list<System.Type>>>) = () // expect ok

        let f4 (x : X<list<int>>) = (x = x) // expect ok
        let f5 (x : X<int -> int>) = (x = x) // expect ok
        let f6 (x : X<list<System.Type>>) = (x = x) // expect ok
        let f7 (x : X<list<int -> int>>) = (x = x) // expect ok

        let g0 (x : Set<Y<int,int>>) = ()   // expect ok
        let g1 (x : Set<Y<'T,'T>>) = ()   // expect ok

        let g4 (x : Y<list<int>,int>) = (x = x) // expect ok
        let g6 (x : Y<list<System.Type>, int>) = (x = x) // expect ok


        let g8 (x : Y<int,list<int>>) = (x = x) // expect ok
        let g9 (x : Y<int,(int -> int)>) = (x = x) // expect ok
        let g10 (x : Y<int,list<System.Type>>) = (x = x) // expect ok
        let g11 (x : Y<int,list<(int -> int)>>) = (x = x) // expect ok

    module Example7 = 
        // a type inferred to be without equality or comparison
        type X = X of (int -> int)
        // a type transitively inferred to be without equality or comparison
        type  Y = Y of X


    module Example8 = 
        // a type inferred to be without comparison
        type X = X of System.Type
        // a type transitively inferred to be without comparison
        type  Y = Y of X

        let f2 (x : X) = (x = x) // expect ok
        let f3 (x : Y) = (x = x) // expect ok


module RuntimeCheckForSelfCallThroughProperty1 =  begin
    [<AbstractClass>]
    type Base(callIt:bool) as self = 
       do if callIt then printfn "P = %d" self.P // expect an exception here
       abstract P: int

    type C(callIt:bool) = 
       inherit Base(callIt)
       let x = 1
       override __.P = x

    check "cewlkcnc332a" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "cewlkcnc332b" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end

module RuntimeCheckForSelfCallThroughSetterProperty1 =  begin
    [<AbstractClass>]
    type Base(callIt:bool) as self = 
       do if callIt then self.P <- 1  // expect an exception here
       abstract P: int with set

    type C(callIt:bool) = 
       inherit Base(callIt)
       let x = 1
       override __.P with set v = ()

    check "cewlkcnc332c" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "cewlkcnc332d" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end

module RuntimeCheckForSelfCallThroughInterfaceSetterProperty1 =  begin
    
    type I = 
       abstract P: int with set
        
    type Base(callIt:bool) as self = 
       do if callIt then (box self :?> I).P <- 1  // expect an exception here

    type C(callIt:bool) = 
       inherit Base(callIt)
       let x = 1
       interface I with 
           member __.P with set v = ()

    check "cewlkcnc332y" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "cewlkcnc332t" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end


module RuntimeCheckForSelfCallThroughProperty1ExplicitClass =  begin
    [<AbstractClass>]
    type Base = 
       new (callIt: bool) as self = 
           { } 
           then 
               if callIt then printfn "P = %d" self.P // expect an exception here

       abstract P: int

    type C = 
       inherit Base
       val x : int
       new (callIt: bool) as self = { inherit Base(callIt); x = 1 }  
       override __.P = x

    check "explicit-cewlkcnc332a" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "explicit-cewlkcnc332b" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end

module RuntimeCheckForSelfCallThroughSetterProperty1ExplicitClass =  begin
    [<AbstractClass>]
    type Base = 
       new (callIt: bool) as self = 
           { } 
           then 
               if callIt then self.P <- 1 // expect an exception here
       abstract P: int with set

    type C = 
       inherit Base
       val x : int
       new (callIt: bool) as self = { inherit Base(callIt); x = 1 }  
       override __.P with set v = ()

    check "explicit-cewlkcnc332c" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "explicit-cewlkcnc332d" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end

module RuntimeCheckForSelfCallThroughInterfaceSetterProperty1ExplicitClass =  begin
    
    type I = 
       abstract P: int with set
        
    type Base = 
       new (callIt: bool) as self = 
           { } 
           then 
               if callIt then (box self :?> I).P <- 1 // expect an exception here


    type C = 
       inherit Base
       val x : int
       new (callIt: bool) as self = { inherit Base(callIt); x = 1 }  
       interface I with 
           member __.P with set v = ()

    check "explicit-cewlkcnc332y" (try C(true) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "fail"
    check "explicit-cewlkcnc332t" (try C(false) |> ignore; "ok" with :? System.InvalidOperationException -> "fail") "ok"
end

module PartiallyReimplementInheritedInterfaces = begin
    //---------------------------------------------------
    // Basic test 1

    module BasicTest1 = 
        type public I1 = 
            abstract V1 : string 
            
        type public I2 = 
            inherit I1 
            abstract V2 : string 
            
        type public C1() = 
            interface I1 with 
                member this.V1 = "C1" 
                
        type public C2() = 
            inherit C1() 
            interface I2 with 
                member this.V2 = "C2"         

        type public C3() = 
            inherit C1() 
            interface I2 with 
                member this.V1 = "C1b"         
                member this.V2 = "C2b"         

        check "8kvnvwe0-we1"  (C2() :> I2).V2 "C2"
        check "8kvnvwe0-we2"  (C2() :> I1).V1 "C1"
        check "8kvnvwe0-we3"  (C3() :> I1).V1 "C1b"

    //---------------------------------------------------
    // Basic test 3 - IEnumerable --> IEnumerable<int>


    module InheritIEnumerableTest1 = 
        open System.Collections
        open System.Collections.Generic
        type BaseCollection() = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box 1
                member __.MoveNext() = true
                
        type DerivedCollection() = 
            inherit BaseCollection()
            interface System.Collections.Generic.IEnumerator<int> with 
                member __.Reset() = ()
                member __.Current = 2
                member __.Current = box 2
                member __.Dispose() = ()
                member __.MoveNext() = false


        type ReDerivedCollection1() = 
            inherit DerivedCollection()
            interface System.Collections.Generic.IEnumerator<int> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection()
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection()
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4() = 
            inherit DerivedCollection()
            interface System.IDisposable


        check "8kvnvwe0-we4"  (new DerivedCollection() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-we5"  ((new DerivedCollection() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-we6"  (new DerivedCollection() :> IEnumerator).Current (box 2)
        check "8kvnvwe0-we7"  ((new DerivedCollection() :> IEnumerator).MoveNext()) false
        check "8kvnvwe0-we8"  (new ReDerivedCollection1() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-we9"  ((new ReDerivedCollection1() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-weq"  (new ReDerivedCollection2() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wew"  ((new ReDerivedCollection2() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wee"  (new ReDerivedCollection3() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wer"  ((new ReDerivedCollection3() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wet"  (new BaseCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-wey"  ((new BaseCollection() :> IEnumerator).MoveNext()) true

    module InheritIEnumerableTest2 = 
        open System.Collections
        open System.Collections.Generic
        type BaseCollection() = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box 1
                member __.MoveNext() = true
                
        type DerivedCollection() = 
            inherit BaseCollection()
            interface System.Collections.Generic.IEnumerator<int> with 
                // Emit one or more members to inherit implementations of some of the interfaces
                //member __.Reset() = ()
                member __.Current = 2
                member __.Current = box 2
                member __.Dispose() = ()
                member __.MoveNext() = false

        type ReDerivedCollection1() = 
            inherit DerivedCollection()
            interface System.Collections.Generic.IEnumerator<int> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection()
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection()
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4() = 
            inherit DerivedCollection()
            interface System.IDisposable

        check "8kvnvwe0-weu"  (new DerivedCollection() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wei"  ((new DerivedCollection() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-weo"  (new DerivedCollection() :> IEnumerator).Current (box 2)
        check "8kvnvwe0-wep"  ((new DerivedCollection() :> IEnumerator).MoveNext()) false
        check "8kvnvwe0-wea"  (new ReDerivedCollection1() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wes"  ((new ReDerivedCollection1() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wed"  (new ReDerivedCollection2() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wef"  ((new ReDerivedCollection2() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-weg"  (new ReDerivedCollection3() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weh"  ((new ReDerivedCollection3() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wej"  (new BaseCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-wek"  ((new BaseCollection() :> IEnumerator).MoveNext()) true

    module InheritIEnumerableTest3 = 
        open System.Collections
        open System.Collections.Generic
        type BaseCollection() = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box 1
                member __.MoveNext() = true
                
        type DerivedCollection() = 
            inherit BaseCollection()
            interface System.Collections.Generic.IEnumerator<int> with 
                // Emit one or more members to inherit implementations of some of the interfaces
                //member __.Reset() = ()
                member __.Current = 2
                //member __.Current = box 1
                member __.Dispose() = ()
                member __.MoveNext() = false

        type ReDerivedCollection1() = 
            inherit DerivedCollection()
            interface System.Collections.Generic.IEnumerator<int> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection()
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection()
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4() = 
            inherit DerivedCollection()
            interface System.IDisposable

        check "8kvnvwe0-wel"  (new DerivedCollection() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wez"  ((new DerivedCollection() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wex"  (new DerivedCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-wec"  ((new DerivedCollection() :> IEnumerator).MoveNext()) false
        check "8kvnvwe0-wev"  (new ReDerivedCollection1() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-web"  ((new ReDerivedCollection1() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-wen"  (new ReDerivedCollection2() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-wem"  ((new ReDerivedCollection2() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-weQ"  (new ReDerivedCollection3() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weW"  ((new ReDerivedCollection3() :> IEnumerator<int>).MoveNext()) false
        check "8kvnvwe0-weE"  (new BaseCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-weR"  ((new BaseCollection() :> IEnumerator).MoveNext()) true

    module InheritIEnumerableTest4 = 
        open System.Collections
        open System.Collections.Generic
        type BaseCollection() = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box 1
                member __.MoveNext() = true
                
        type DerivedCollection() = 
            inherit BaseCollection()
            interface System.Collections.Generic.IEnumerator<int> with 
                // Emit one or more members to inherit implementations of some of the interfaces
                //member __.Reset() = ()
                member __.Current = 2
                //member __.Current = box 1
                member __.Dispose() = ()
                //member __.MoveNext() = true

        type ReDerivedCollection1() = 
            inherit DerivedCollection()
            interface System.Collections.Generic.IEnumerator<int> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection()
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection()
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4() = 
            inherit DerivedCollection()
            interface System.IDisposable


        check "8kvnvwe0-weT"  (new DerivedCollection() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weY"  ((new DerivedCollection() :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weU"  (new DerivedCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-weI"  ((new DerivedCollection() :> IEnumerator).MoveNext()) true
        check "8kvnvwe0-weO"  (new ReDerivedCollection1() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weP"  ((new ReDerivedCollection1() :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weA"  (new ReDerivedCollection2() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weS"  ((new ReDerivedCollection2() :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weD"  (new ReDerivedCollection3() :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weF"  ((new ReDerivedCollection3() :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weG"  (new BaseCollection() :> IEnumerator).Current (box 1)
        check "8kvnvwe0-weH"  ((new BaseCollection() :> IEnumerator).MoveNext()) true

    // Add some generics
    module InheritIEnumerableTest5 = 
        open System.Collections
        open System.Collections.Generic
        type BaseCollection<'T>(x:'T) = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box x
                member __.MoveNext() = true
                
        type DerivedCollection<'U>(x:'U) = 
            inherit BaseCollection<'U>(x)
            interface System.Collections.Generic.IEnumerator<'U> with 
                // Emit one or more members to inherit implementations of some of the interfaces
                //member __.Reset() = ()
                member __.Current = x
                //member __.Current = box 1
                member __.Dispose() = ()
                //member __.MoveNext() = true

        type ReDerivedCollection1() = 
            inherit DerivedCollection<int>(3)
            interface System.Collections.Generic.IEnumerator<int> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection<string>("4")
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection<uint32>(3u)
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4<'T>(x:'T) = 
            inherit DerivedCollection<'T>(x)
            interface System.IDisposable


        check "8kvnvwe0-weJ"  (new DerivedCollection<int>(2) :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weK"  ((new DerivedCollection<int>(2) :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weL"  (new DerivedCollection<int>(1) :> IEnumerator).Current (box 1)
        check "8kvnvwe0-weZ"  ((new DerivedCollection<int>(1) :> IEnumerator).MoveNext()) true
        check "8kvnvwe0-weX"  (new ReDerivedCollection1() :> IEnumerator<int>).Current 3
        check "8kvnvwe0-weC"  ((new ReDerivedCollection1() :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weV"  (new ReDerivedCollection2() :> IEnumerator<string>).Current "4"
        check "8kvnvwe0-weB"  ((new ReDerivedCollection2() :> IEnumerator<string>).MoveNext()) true
        check "8kvnvwe0-weN"  (new ReDerivedCollection3() :> IEnumerator<uint32>).Current 3u
        check "8kvnvwe0-weM"  ((new ReDerivedCollection3() :> IEnumerator<uint32>).MoveNext()) true
        check "8kvnvwe1-weq"  (new BaseCollection<int>(1) :> IEnumerator).Current (box 1)
        check "8kvnvwe1-wWw"  ((new BaseCollection<int>(1) :> IEnumerator).MoveNext()) true

    // Add some units of measure
    module InheritIEnumerableTest6 = 
        [<Measure>] type kg
        
        open System.Collections
        open System.Collections.Generic
        type BaseCollection<'T>(x:'T) = 
            override __.GetHashCode() = 0
            override __.Equals(yobj) = true
            interface System.Collections.IEnumerator with 
                member __.Reset() = ()
                member __.Current = box x
                member __.MoveNext() = true
                
        type DerivedCollection<'U>(x:'U) = 
            inherit BaseCollection<'U>(x)
            interface System.Collections.Generic.IEnumerator<'U> with 
                // Emit one or more members to inherit implementations of some of the interfaces
                //member __.Reset() = ()
                member __.Current = x
                //member __.Current = box 1
                member __.Dispose() = ()
                //member __.MoveNext() = true

        type ReDerivedCollection1() = 
            inherit DerivedCollection<int<kg>>(3<kg>)
            interface System.Collections.Generic.IEnumerator<int<kg>> 

        type ReDerivedCollection2() = 
            inherit DerivedCollection<float<kg>>(4.0<kg>)
            interface System.Collections.IEnumerator

        type ReDerivedCollection3() = 
            inherit DerivedCollection<int64<kg>>(3L<kg>)
            interface System.IDisposable
            interface System.Collections.IEnumerator

        type ReDerivedCollection4<'T>(x:'T) = 
            inherit DerivedCollection<'T>(x)
            interface System.IDisposable

        type ReDerivedCollection5<'T>(x:'T) = 
            inherit ReDerivedCollection3()
            interface System.IDisposable
            interface System.Collections.Generic.IEnumerator<int64<kg>> 


        check "8kvnvwe0-weJ"  (new DerivedCollection<int>(2) :> IEnumerator<int>).Current 2
        check "8kvnvwe0-weK"  ((new DerivedCollection<int>(2) :> IEnumerator<int>).MoveNext()) true
        check "8kvnvwe0-weL"  (new DerivedCollection<int>(1) :> IEnumerator).Current (box 1)
        check "8kvnvwe0-weZ"  ((new DerivedCollection<int>(1) :> IEnumerator).MoveNext()) true
        check "8kvnvwe0-weX"  (new ReDerivedCollection1() :> IEnumerator<int<kg>>).Current 3<kg>
        check "8kvnvwe0-weC"  ((new ReDerivedCollection1() :> IEnumerator<int<kg>>).MoveNext()) true
        check "8kvnvwe0-weV"  (new ReDerivedCollection2() :> IEnumerator<float<kg>>).Current 4.0<kg>
        check "8kvnvwe0-weB"  ((new ReDerivedCollection2() :> IEnumerator<float<kg>>).MoveNext()) true
        check "8kvnvwe0-weN"  (new ReDerivedCollection3() :> IEnumerator<int64<kg>>).Current 3L<kg>
        check "8kvnvwe0-weM"  ((new ReDerivedCollection3() :> IEnumerator<int64<kg>>).MoveNext()) true
        check "8kvnvwe1-weq"  (new BaseCollection<int>(1) :> IEnumerator).Current (box 1)
        check "8kvnvwe1-wWw"  ((new BaseCollection<int>(1) :> IEnumerator).MoveNext()) true

end

module ClassWithInheritAndImmediateReferenceToThisInLet = 
    type B() = 
        member __.P = 1

    let id x = x

    type C() as x = 
        inherit B()
        let y = id x // it is ok to access the this pointer and pass it to external code as long as it doesn't call any members
        member __.ThisPointer1 = x
        member __.ThisPointer2 = y


    let checkA() = 
        let c = C()
        check "cwknecw021" c.ThisPointer1 c.ThisPointer2
   
module ClassWithNoInheritAndImmediateReferenceToThisInLet = 
    type C() as x = 
        let y = id x // it is ok to access the this pointer and pass it to external code as long as it doesn't call any members
        member __.ThisPointer1 = x
        member __.ThisPointer2 = y


    let checkB() = 
        let c = C()
        check "cwknecw021b" c.ThisPointer1 c.ThisPointer2


module CheckResolutionOrderForMembers1 = 
    type System.String with 
        static member M1 () = 1
        static member M1 (?s : string) = 2
    let a = System.String.M1()
    check "vew98vrknj1" a 1
    
module CheckResolutionOrderForMembers2 = 
    type System.String with 
        static member M1 (?s : string) = 2
        static member M1 () = 1

    let b = System.String.M1()
    
    check "vew98vrknj2" b 1

module CheckResolutionOrderForMembers3 = 
    type C = 
        static member M1 () = 1
        static member M1 (?s : string) = 2

    let a1 = C.M1()
    let a2 = C.M1("2")
    check "vew98vrknj3" a1 1
    check "vew98vrknj3" a2 2


module CheckUsesOfNonOverloadMembersAssertReturnTypeBeforeCheckingArgs = 

    type C() = 
        static member M(x:int)  = 0
        static member M(x:string) = 1

        member __.N(x:int)  = 0
        member __.N(x:string) = 1

    let test (x:int) = 1
    type D() = 
        static member Test (x:int) = 1


    // In this case, the overload C.M resolves, because the return type of 
    // loopX is known, from the return type of test.

    let rec loopX() = test (C.M(loopX()))  

    // In this case, the overload C.M resolves, because the return type of 
    // loopY is known, from the return type of test.

    let rec loopY() = D.Test (C.M(loopY()))  

    // Instance member versions of the same
    let c = C()
    let rec loopX1() = test (c.N(loopX1()))  
    let rec loopY1() = D.Test (c.N(loopY1()))  


// Believe it or not, this caused the type checker to loop (bug 5803)
module BasicTypeCHeckingLoop = 


    type Vector() =
        static member (+)(v1:Vector,v2) = 0

    let foo (v1:Vector) v2 : int = v1 + v2

module CheckGeneralizationOfMembersInRecursiveGroupsWhichIncludeImplicitConstructors = 

    open System.Collections
    open System.Collections.Generic

    type IA<'T> = interface abstract M2 : int -> int end

    type X<'T>() =
        let mutable redirectTo : X<'T> = Unchecked.defaultof<_>

        member x.M1() = if true then redirectTo else x
        member x.M2() = if true then redirectTo else x
        interface IA<int> with
            member x.M2 y = y


    [<Struct>]
    type S<'T>(redirectTo : list<S<'T>>) =

        member x.M() = if true then redirectTo.[0] else x


    module Issue3Minimal = 
        type MyDiscrUnion<'t> =
            | MyCase of MyConstructedClass<'t>  

            member this.M() =
                match this with
                | MyCase r -> r.Record
        and MyConstructedClass<'t>( record : MyDiscrUnion<'t> ) = 
           member x.Record = record

    module Issue4 = 
        type MyDiscrUnion<'t> =
            | MyCase       of MyConstructedClass<'t>  
            member x.Value (t : 't) =
                match x with
                | MyCase       r -> r.Apply t
        and MyConstructedClass<'t>( foo : 't ) = 
            member x.Apply ( t: 't ) = t

    type ClassA<'t>(b: ClassB<'t>, c:'t) =
        member x.A() = b.A() 
    and ClassB<'t>( a : ClassA<'t> ) = 
       member x.A()  = a

module CheckGeneralizationOfMembersInRecursiveGroupsWhichIncludeImplicitConstructorsPart2 = 

    // This test raised a code generation assert
    module DontGeneralizeTypeVariablesBoundByInnerPositions_1 = 
        type C() =  
            let someFuncValue = C.Meth2() // someFuncValue initially has variable type. This type is not generalized.
            static member Meth2() = C.Meth2() 
        and C2() =
            static member Meth1() = C.Meth2()

    // This test raised a code generation assert
    module DontGeneralizeTypeVariablesBoundByInnerPositions_2 = 
        type C<'T>() =  
            let someFuncValue = C<'T>.Meth2() // someFuncValue initially has variable type. This type is not generalized.
            static member Meth2() = C<'T>.Meth2() 
        and C2<'T>() =
            static member Meth1() = C<'T>.Meth2()

    // This test is a trimmed down version of a regression triggered by this fix
    module M0 = 
        type C<'T>(orig : 'T) =  
            [<DefaultValue(false)>] 
            val mutable parent : C<'T> 
            let MyFind(x : C<'T>) = x.parent 
            member this.Find() = MyFind(this) 

    // This test is a trimmed down version of a regression triggered by this fix
    module M1 = 
        type C<'T>(orig : 'T) as this =  
            [<DefaultValue(false)>] 
            val mutable parent : C<'T> 
            let MyFind(x : C<'T>) = x.parent 
            member this.Find() = MyFind(this) 

    // This test is an adapted version of the above trim-down
    module M2 = 
        type C<'T>(orig : 'T) as this =  
            [<DefaultValue(false)>] 
            val mutable parent : C2<'T> 
            let MyFind(x : C<'T>) = (x.parent , x.parent.parent)
            member this.Find() = MyFind(this) 
        and C2<'T>(orig : 'T) as this =  
            [<DefaultValue(false)>] 
            val mutable parent : C<'T> 
            let MyFind(x : C2<'T>) = (x.parent , x.parent.parent)
            member this.Find() = MyFind(this) 

    // This test is an adapted version of the above trim-down
    module M3 = 
        type C<'T>(orig : 'T) =  
            [<DefaultValue(false)>] 
            val mutable parent : C2<'T> 
            let MyFind(x : C<'T>) = (x.parent , x.parent.parent)
            member this.Find() = MyFind(this) 
        and C2<'T>(orig : 'T) =  
            [<DefaultValue(false)>] 
            val mutable parent : C<'T> 
            let MyFind(x : C2<'T>) = (x.parent , x.parent.parent)
            member this.Find() = MyFind(this) 

    // These are variations on tests where a cycle of methods and "let" bindings across one or more test is an adapted version of the above trim-down
    module RoundInCircles = 
        type C<'T>() as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))

    // Mutual recursion between two generic classes
    module RoundInCircles2 = 
        type C<'T>() as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
        and C2<'T>(x:C<'T>) as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(x))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(x) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(x))


    // Mutual recursion between generic class and generic record type
    module RoundInCircles3 = 
        type C<'T>() as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
        and C2<'T> =
            { x:C<'T> } 
            member this.Meth1() = 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.x)
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.x))

    // Mutual recursion between generic class and generic union type
    module RoundInCircles4 = 
        type C<'T>() as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
        and C2<'T> =
            | Parition2 of C<'T> 
            member this.Value =  match this with Parition2 x -> x
            member this.Meth1() = 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.Value)
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.Value))

    // Mutual recursion between generic class and generic union type, slight variation passing 'this' explicitly
    module RoundInCircles5 = 
        type C<'T>() =  
            let someFuncValue = (); (fun (this:C<'T>)  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1 this = 
                 someFuncValue this |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2 this = 
                 someFunc2 this |> ignore;
                 someFunc1 this
            member this.Meth1() = someFunc2 this
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
        and C2<'T> =
            | Parition2 of C<'T> 
            member this.Value =  match this with Parition2 x -> x
            member this.Meth1() = 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.Value)
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this.Value))

    // Mutual recursion between generic class and generic struct
    module RoundInCircles6 = 
        type C<'T>() as this =  
            let someFuncValue = (); (fun ()  -> 
                 this.Prop1() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
            let someFunc1() = 
                 someFuncValue() |> ignore
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this) 
            let rec someFunc2() = 
                 someFunc2() |> ignore;
                 someFunc1() 
            member this.Meth1() = someFunc2()
            static member Meth2(this:C<'T>) = this.Meth1()
            member this.Prop1 = (); (fun ()  -> 
                 this.Meth1() |> ignore
                 C<'T>.Meth2(this))
        and C2<'T>(x:C<'T>) =  
            struct
                member this.Value = x
                member this.Meth1() = x.Meth1()
                static member Meth2(this:C<'T>) = this.Meth1()
                member this.Prop1 = let v = this in (); (fun ()  -> 
                     v.Meth1() |> ignore
                     C<'T>.Meth2(v.Value))
            end

module CheckGeneralizationOfMembersInRecursiveGroupsWhichIncludeImplicitConstructorsAndStaticMembers = 

    open System.Collections
    open System.Collections.Generic

    type IA<'T> = interface abstract M2 : int -> int end

    type X<'T>() =
        let mutable redirectTo : X<'T> = Unchecked.defaultof<_>
        static let instance = X<'T>()
        static member Instance = instance
        member x.M1() = if true then redirectTo else x
        member x.M2() = if true then redirectTo else x
        interface IA<int> with
            member x.M2 y = y


    [<Struct>]
    type S<'T>(redirectTo : list<S<'T>>) =
        static let instance = S<'T>(List.empty<S<'T>>)
        static member Instance = instance
        member x.M() = if true then redirectTo.[0] else x


    // This test raised a code generation assert
    module DontGeneralizeTypeVariablesBoundByInnerPositions_2 = 
        type C<'T>() =  
            static let someStaticFuncValue = C<'T>.Meth2()
            let someFuncValue = C<'T>.Meth2() // someFuncValue initially has variable type. This type is not generalized.
            static member SomeStaticFuncValue = someStaticFuncValue            
            static member Meth2() = C<'T>.Meth2() 
        and C2<'T>() =
            static member Meth1() = C<'T>.Meth2()

    module Misc = 
    
        type Foo<'a>() =
          static let theInstance = new Foo<'a>()
          static member Instance = theInstance
          
        type Bar<'a,'b>() =
          static let theInstance = new Bar<'a,'b>()
          static member Instance = theInstance
          
        // Equi-recursive type defs
        type Rec1<'a>() = 
          static let rec2Instance = new Rec2<'a>()
          static let rec1Instance = new Rec1<'a>()  
          static member Rec2Instance = rec2Instance
          static member Rec1Instance = rec1Instance
        
        and Rec2<'a>() =
          static let rec1Instance = new Rec1<'a>()
          static let rec2Instance = new Rec2<'a>()
          static member Rec1Instance = rec1Instance
          static member Rec2Instance = rec2Instance   
          
        // recursive type defs - multi tyargs
        type Rec1AB<'a,'b>() = 
          static let rec2Instance = new Rec2B<'a>()
          static let rec1Instance = new Rec1AB<'a,'b>()  
          static member Rec2Instance = rec2Instance
          static member Rec1Instance = rec1Instance
        
        and Rec2B<'a>() =
          static let rec2Instance = new Rec2B<'a>()
          static member Rec2Instance = rec2Instance     
          
        // functions
        type FooFunc<'a>() =
            static let fact() = new FooFunc<'a>()
            static member factory() = fact()
        
        // test methods    
        type FooFunc2<'a>() =
            static let fact = new FooFunc2<'a>()
            static member factory() = fact
                        
        type BarFunc<'a,'b>() =
          static let fact() = new BarFunc<'a,'b>()
          static member factory() = fact()
          
        // Equi-recursive type defs
        type Rec1Func<'a>() = 
          static let rec2Instance() = new Rec2Func<'a>()
          static let rec1Instance() = new Rec1Func<'a>()  
          static member Rec2Instance() = rec2Instance()
          static member Rec1Instance() = rec1Instance()        
        and Rec2Func<'a>() =
          static let rec1Instance() = new Rec1Func<'a>()
          static let rec2Instance() = new Rec2Func<'a>()
          static member Rec1Instance() = rec1Instance()
          static member Rec2Instance() = rec2Instance()
          
        // recursive type defs - multi tyargs
        type Rec1ABFunc<'a,'b>() = 
          static let rec2Instance() = new Rec2BFunc<'a>()
          static let rec1Instance() = new Rec1ABFunc<'a,'b>()  
          static member Rec2Instance() = rec2Instance()
          static member Rec1Instance() = rec1Instance()        
        and Rec2BFunc<'a>() =
          static let rec2Instance() = new Rec2BFunc<'a>()
          static member Rec2Instance() = rec2Instance()

module Devdiv2_Bug_41009 = 
    type Rope<'T> = 
        { x : int }
        member x.Empty  : Rope<_> = failwith "" // the type variable for "_" is ungeneralized because this is a property
        member x.Create(lhs:Rope<'T>) : Rope<'T> =
            x.Empty  // here the type variable for "_" gets instantiated to 'T, but this gets generalized. This must not be considered an escaping variable


module Devdiv2_Bug_10649 = 

    // This should compile, because it compiles with F# 2.0. A has been generalized too early, 
    // but all recursive uses of the member end up consistent with the final inferred type
    module InstanceMembersEarlyGeneralizationPotentiallyInvalidButUltimatelyConsistent = 
        type C<'T>() = 
            let mutable x = Unchecked.defaultof<_> // this inference variable ultimately becomes 'obj'
            member this.A() = x
            member this.B1(c:C<string>) = c.A() 
            member this.B2(c:C<int>) = c.A() 

    // This should compile, because it compiles with F# 2.0. A has been generalized too early, 
    // but all recursive uses of the member end up consistent with the final inferred type
    module StaticMembersEarlyGeneralizationPotentiallyInvalidButUltimatelyConsistent = 
        type C<'T>() = 
            static let mutable x = Unchecked.defaultof<_> // this inference variable ultimately becomes 'obj'
            static member A() = x
            static member B1() = C<string>.A()
            static member B2() = C<int>.A()

    // This should compile, because it early generalization is valid for A
    module InstanceMembersEarlyGeneralizationValid = 
        type C<'T>() = 
            member this.A() = 1
            member this.B1(c:C<string>) = c.A() 
            member this.B2(c:C<int>) = c.A() 

    // This should compile, because it early generalization is valid for A
    module StaticMembersEarlyGeneralizationValid = 
        type C<'T>() = 
            static member A() = 1
            static member B1() = C<string>.A()
            static member B2() = C<int>.A()


module Devdiv2_Bug_5385 =
    let memoize (f: 'a -> 'b) = 
        let t = new System.Collections.Generic.Dictionary<'a, 'b>()
        fun n ->
            if t.ContainsKey(n) then 
                t.[n]
            else
                let res = f n
                t.[n] <- res
                res

    // In this variation, 
    //     -- 'f' is generic even though it is in the same mutually recursive group as the computed function 'g', because although 'f' calls 'g', its type doesn't involve any type from 'g'
    //     -- 'g' is not generic since 'g' is computed
    let test3e () =
        let count = ref 0
        let rec f (x: 'T) =  
                incr count; 
                if !count > 4 then 
                    1 
                else 
                    g "1" |> ignore; // note, use of non-generic 'g' within a generic, generalized memoized function
                    2

        and g : string -> int = memoize f // note, computed function value using generic �f� at an instance
        g "1"                                        

    let res = test3e()

    check "fe09ekn" res 2

    printfn "test passed ok without NullReferenceException"

module Devdiv2_5385_repro2 = 

    open System
    type Dispatch<'t, 'a> = 't -> ('t -> Lazy<'a>) -> 'a 

    let internal driver (mkString : 't -> string) (dispatch : Dispatch<'t, 'a>) (t : 't) : 'a = 
            let rec driver2 (seen : Map<string,Lazy<'a>>) (t : 't) : Lazy<'a> = 
                let tKey = mkString t 
                let find = seen.TryFind tKey 
                match find with 
                    | Some x -> x 
                    | None -> 
                        let rec seen2 = seen.Add(tKey,res) 
                            and res = lazy dispatch t (driver2 seen2) 
                        res 
            (driver2 (Map.empty) t).Value 



    let res : int = driver (fun t -> t.ToString()) (fun t f -> if t > 50 then (f (t/2)).Value else t) 100 

    check "kjnecwwe9" res 50

    printfn "test passed ok without NullReferenceException"



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

