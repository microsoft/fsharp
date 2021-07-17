// #Conformance #TypeInference #TypeConstraints #UnitsOfMeasure #Regression #Operators #Mutable 
#if TESTS_AS_APP
module Core_subtype
#endif

#light

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

(* TEST SUITE FOR SUBTYPE CONSTRAINTS *)


open System
open System.IO
open System.Reflection
open System.Collections.Generic

(* 'a[] :> ICollection<'a> *)
let f1 (x: 'a[]) = (x :> ICollection<'a>) 
do let x = f1 [| 3;4; |] in test "test239809" (x.Contains(3))

#if !NETCOREAPP
(* 'a[] :> IReadOnlyCollection<'a> *)
let f1ReadOnly (x: 'a[]) = (x :> IReadOnlyCollection<'a>) 
do let x = f1ReadOnly [| 3;4; |] in test "test239809ReadOnly" (x.Count = 2)
#endif

(* 'a[] :> IList<'a> *)
let f2 (x: 'a[]) = (x :> IList<'a>) 
do let x = f2 [| 3;4; |] in test "test239810" (x.Item(1) = 4)

#if !NETCOREAPP
(* 'a[] :> IReadOnlyList<'a> *)
let f2ReadOnly (x: 'a[]) = (x :> IReadOnlyList<'a>) 
do let x = f2ReadOnly [| 3;4; |] in test "test239810ReadOnly" (x.Item(1) = 4)
#endif

(* 'a[] :> IEnumerable<'a> *)
let f3 (x: 'a[]) = (x :> IEnumerable<'a>) 
do let x = f3 [| 3;4; |] in for x in x do (Printf.printf "val %d\n" x) done

(* Call 'foreachG' using an IList<int> (solved to IEnumerable<int>) *)
let f4 (x: 'a[]) = (x :> IList<'a>) 
do let x = f4 [| 31;42; |] in for x in x do (Printf.printf "val %d\n" x) done

#if !NETCOREAPP
(* Call 'foreachG' using an IReadOnlyList<int> (solved to IEnumerable<int>) *)
let f4ReadOnly (x: 'a[]) = (x :> IReadOnlyList<'a>) 
do let x = f4ReadOnly [| 31;42; |] in for x in x do (Printf.printf "val %d\n" x) done
#endif

(* Call 'foreachG' using an ICollection<int> (solved to IEnumerable<int>) *)
let f5 (x: 'a[]) = (x :> ICollection<'a>) 
do let x = f5 [| 31;42; |] in for x in x do (Printf.printf "val %d\n" x) done

#if !NETCOREAPP
(* Call 'foreachG' using an IReadOnlyCollection<int> (solved to IEnumerable<int>) *)
let f5ReadOnly (x: 'a[]) = (x :> IReadOnlyCollection<'a>) 
do let x = f5ReadOnly [| 31;42; |] in for x in x do (Printf.printf "val %d\n" x) done
#endif

[<Measure>] type kg

let testUpcastToArray1 (x: 'a[]) = (x :> System.Array) 
let testUpcastToArray2 (x: 'a[,]) = (x :> System.Array) 
let testUpcastToArray3 (x: 'a array) = (x :> System.Array) 
let testUpcastToArray4 (x: 'a array  array) = (x :> System.Array) 

let testUpcastToIEnumerable1 (x: 'a[]) = (x :> System.Collections.IEnumerable) 
let testUpcastToIEnumerable2 (x: 'a[,]) = (x :> System.Collections.IEnumerable) 
let testUpcastToIEnumerable3 (x: 'a array) = (x :> System.Collections.IEnumerable) 
let testUpcastToIEnumerable4 (x: 'a array  array) = (x :> System.Collections.IEnumerable) 

let testUpcastToICollection1 (x: 'a[]) = (x :> System.Collections.ICollection) 
let testUpcastToICollection2 (x: 'a[,]) = (x :> System.Collections.ICollection) 
let testUpcastToICollection3 (x: 'a array) = (x :> System.Collections.ICollection) 
let testUpcastToICollection4 (x: 'a array  array) = (x :> System.Collections.ICollection) 


let testUpcastToIList1 (x: 'a[]) = (x :> System.Collections.IList) 
let testUpcastToIList2 (x: 'a[,]) = (x :> System.Collections.IList) 
let testUpcastToIList3 (x: 'a array) = (x :> System.Collections.IList) 
let testUpcastToIList4 (x: 'a array  array) = (x :> System.Collections.IList) 

let testUpcastToValueType1 (x: int) = (x :> System.ValueType) 
let testUpcastToValueType2 (x: bool) = (x :> System.ValueType) 
let testUpcastToValueType3 (x: char) = (x :> System.ValueType) 
let testUpcastToValueType4 (x: uint32) = (x :> System.ValueType) 
let testUpcastToValueType5 (x: System.DateTime) = (x :> System.ValueType) 
let testUpcastToValueType6 (x: System.ValueType) = (x :> System.ValueType) 
let testUpcastToValueType7 (x: float<kg>) = (x :> System.ValueType)

let testUpcastToEnum1 (x: System.AttributeTargets) = (x :> System.Enum) 
let testUpcastToEnum6 (x: System.Enum) = (x :> System.Enum) 

// these delegates don't exist in portable
#if !UNIX && !NETCOREAPP
let testUpcastToDelegate1 (x: System.Threading.ThreadStart) = (x :> System.Delegate) 

let testUpcastToMulticastDelegate1 (x: System.Threading.ThreadStart) = (x :> System.MulticastDelegate) 

do for name in Directory.GetFiles("c:\\") do stdout.WriteLine name done
#endif

let f (x : #System.IComparable<'a>) = 1


module RandomGregChapmanTest = begin
    open System.Collections.Generic
    open Microsoft.FSharp.Collections

    type Range = {Dummy: int}
    type Worksheet = {Name: string; UsedRange: Range}
    type Track = {Album: string; Track: string; Artist: string}

    type 
        Workbook  = class
            new() = {}
        
            member x.Sheets: System.Collections.IEnumerable =
                upcast Seq.unfold (
                    fun index -> 
                        if index >= 4 then None
                        else Some({Name="Sheet"+(index.ToString()); UsedRange={Dummy=index}}, index+1)
                ) 1
        end
        
    let rangeContains _ _ = true

    let rowstr s (i1:int) (i2:int) = s + ( i1.ToString()) + ( i2.ToString())

    let rangeRowIter range =
        Seq.unfold (
            fun index ->
                if index >= 4 then None
                else begin
                    let row = 
                       Map.ofList [
                        "ALBUM", (rowstr "Album" range.Dummy index);
                        "TRACK", (rowstr "Track" range.Dummy index);
                        "LEADER", (rowstr "Leader" range.Dummy index) ]
                    in
                    Some(row, index+1)
                end
        ) 1

    let Run (matchtext: string) =
        let GetMatchingRanges() =
            let workbook = new Workbook() in
            Seq.map (fun (sheet: Worksheet) -> (sheet.Name, sheet.UsedRange)) (
                Seq.filter (fun sheet -> rangeContains sheet.UsedRange matchtext) 
                  (Seq.cast workbook.Sheets : IEnumerable<Worksheet>)
            )
        in
        let ParseRanges ranges =
            ranges |> Seq.map (
                fun (name, range) ->
                    let items = List.ofSeq ((rangeRowIter range) |> Seq.map (fun row -> {Album=row.["ALBUM"]; Track=row.["TRACK"]; Artist=row.["LEADER"]})) in
                    (name, items)
            )
        in
        GetMatchingRanges() |> ParseRanges
      
end


let iobj = (3 :> obj)
let iobj2 = (iobj :?> int)
let iobj3 = (iobj :? int)
let iobj4 = 
    match iobj with 
    | :? int as i -> i 
    | :? int64 as i -> int i
    | _ -> 0



(* BUG 858: function types in constraints of the genparams of either a method or a type *)
let seq_to_array  (ss : #seq<'a>) = Seq.toArray ss
let seq_to_array2 (ss : #seq<'a>) = seq_to_array ss
let badCode (actions : 'b when 'b :> (unit -> unit) seq) = seq_to_array actions

(* BUG 858: function types in constraints of the genparams of either a method or a type *)
type 'a MyClass when 'a :> seq<(unit -> unit)> =
 class
   static member MyMeth(x:'a) = (x :> #(unit -> unit) seq)
 end


(*FAILING: logged as bug 872 *)
(*fsi says error: buildGenParam: multiple base types*)
#if COMPILED
module PositiveTestsForConstraintNormalization = begin

    type I1<'a> = interface end
    type I2<'a> = interface inherit I1<'a> end
    type I3<'a> = interface inherit I1<'a list> end
    type I4<'a> = interface inherit I2<'a list> inherit I3<'a> end
    type I5<'a> = interface inherit I2<'a list> inherit I3<'a> end
    type C1<'a when 'a : comparison> = class interface I4<'a Set> end
    type C2<'a> = class interface I5<'a array> end
    type C3<'a> = class interface I4<'a array> interface I5<'a array> end
    let f1 (x : #I4<'a>) = ()
    let f2 (x : #I5<'a>) = ()
    let f3 x = f1 x; f2 x
    let f4 x = f1 x; f2 x; f3 x
    let f5 (x : C3<int>) = f1 x; f2 x; f3 x
    
    

end
#endif
    
module SomeRandomOperatorConstraints = begin
    open System
    open System.Numerics
    
    let f x = abs(x*x)


    let f2 x : float = x * x 
    let f3 x (y:float) = x * y

    //let neg4 x (y:System.DateTime) = x + y

    // This example resolves the type of "y" to "TimeSpam". It checks that a single "+" overload between 
    // two different types DateTime and TimeSpan get resolved via 
    // via weak SRTP resolution using a DateTime constraint alone.
    let f5 (x:DateTime) y = x + y

    // This example checks a use of TimeSpan/DateTime overloads
    let f5b (x:DateTime) (y:DateTime) = (x - y) 


    // This example checks a use of TimeSpan/DateTime overloads
    let f5b2 (x:DateTime) (y:TimeSpan) = (x - y) 

    // This example coincidentally checks that the return type is not taken into account before the list of method overloads
    // is prepared in SRTP resolution.  That is the type of (a - b) is immediately known (and we can use it for
    // dot-notation name resolution of .TotalSeconds) _immediately_ that the types of a and b are
    // known and _prior_ to generalization.
    let f5c (x: DateTime) (y:DateTime) = 
        (x  - y).TotalSeconds |> int

    let f5c2 (x: DateTime) (y:TimeSpan) = 
        (x  - y).Second |> int

    let f6 (x:int64) y = x + y
    let f7 x y : int64 = x + y
    let f8 x = Seq.reduce (+) x
    let sum seq : float = Seq.reduce (+) seq
    let inline sumg seq = Seq.reduce (+) seq
    let sumgi seq : int = sumg seq 
    let sumgf seq : float32 = sumg seq 

    let sum64 seq : int64 = Seq.reduce (+) seq
    let sum32 seq : int64 = Seq.reduce (+) seq
#if !NETCOREAPP
    let sumBigInt seq : BigInteger = Seq.reduce (+) seq
#endif
    let sumDateTime (dt : DateTime) (seq : #seq<TimeSpan>) : DateTime = Seq.fold (+) dt seq
end

(* This test is funky because the type constraint on the variable associated with parameter 'x' *)
(* invloves the type variable from the enclosing class.  This exposed a bug with fixing up type *)
(* constraints correctly. *)
module NestedGenericMethodWithSubtypeConstraint = begin
    type I<'a> =
        interface
            abstract Post : unit -> unit
        end

    type A<'b>  = 
        class 
            member this.M(x : #I<'b> ) = ()
            new() = {}
        end

    let B() = 
        { new I<string> with  member this.Post() = () end }

    let main() =
          let d = new A<string>() in
          let a = B() in
          d.M(a)

    do main()
  
    do System.Array.FindIndex<int>([| 1;2;3 |], (fun x -> x % 2 = 0)) |> ignore

end

(* Test that generic inheritance through units-of-measure parameters works correctly *)
(* See bug 6167 *)
module SubsumptionAndUnitsOfMeasure = begin

  [<Measure>] type kg
  [<Measure>] type s

  type SC< [<Measure>] 'u>(arg : float<'u>) = 
    member this.Value = arg

  type TC< [<Measure>] 'u>(arg) =
    inherit SC<'u>(arg)

  type WC< [<Measure>] 'v>(arg : float<'v^2>) =
    inherit SC<'v^2>(arg)

  type GC(arg : float<kg>) =
    inherit SC<kg>(arg)

  type GC2(arg : float<kg^2>) =
    inherit SC<kg^2>(arg)

  let ff (x:SC<'u>) = x.Value
  let gg (x:TC<'v>, f:float<_>) = ff x + f
  let hh (x:WC<'w>, f:float<'w^2>) = ff x + f
  let hh2 (x:WC<kg>, f:float<kg^2>) = ff x + f
  let hh3 (x:WC<kg>, f:float<kg^2>) = hh (x,f)

  let ii (x:GC) = ff x + 3.0<kg>
  let jj (x:GC2) = ff x + 3.0<kg^2>
end

module CheckExceptionsCanBeUsedAsTypes = begin
    exception MyEException of string with member x.M() = 4  end

    let e = (MyEException("test") :?> MyEException)

    let x = e.M()
end



module SimpleDatatypeInModuleThatFailedToLoad = begin
    module FooMod = begin
        type 'a foo = Foo of 'a 
        let makeFoo v = Foo v
    end

    module FooMod2 = begin
        let makeFoo2 = FooMod.makeFoo 
    end
end
    
    
module AttrbuteArgTest = 
    [<System.AttributeUsage (System.AttributeTargets.Class,AllowMultiple=false)>]  
    [<Sealed>]
    type RowAttribute(values: obj[]) = 
        inherit System.Attribute()
        member x.Values = values

    [<Row([| |])>]
    type fixture = { x:int }

    [<Row([| (1 :> obj) |])>]
    type fixture2 = { x:int }


    [<Row([| ("1" :> obj) |])>]
    type fixture3 = { x:int }

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


module FunctionsTakingSubsumableArgsTestValueTypes = 
    let TakesOneSubsumableArg (x:System.ValueType) = ()
    let TakesTwoCurriedSubsumableArgs (x:System.ValueType) (y:System.ValueType) = ()
    let TakesTwoTupledSubsumableArgs (x:System.ValueType, y:System.ValueType) = ()
    let TakesTwoGroupsOfTupledSubsumableArgs (x:System.ValueType, y:System.ValueType) (x2:System.ValueType, y2:System.ValueType) = ()
    let TakesTwoCurriedObjArgs (x:obj) (x2:obj) = ()

    let a1 = (1 :> System.ValueType) //(new B())
    let a2 = (2 :> System.ValueType) //(new B())
    let b1 = 1 //(new B())
    let b2 = 2 //(new B())
    let pAA = (a1,a1) //(new B())
    let pAB = (a1,b1) //(new B())
    let pBA = (b1,a1) //(new B())
    let pBB = (b1,b1) //(new B())


    let v1 =  <@ TakesOneSubsumableArg a1 @>
    let v2 =  <@ TakesOneSubsumableArg b1 @>
    let _ =  <@ TakesOneSubsumableArg @>

    let v4 =  <@ TakesTwoCurriedSubsumableArgs a1 a2 @>
    let v5 =  <@ TakesTwoCurriedSubsumableArgs a1 b2 @>
    let v6 =  <@ TakesTwoCurriedSubsumableArgs b1 a2 @>
    let v7 =  <@ TakesTwoCurriedSubsumableArgs b1 b2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs @>

    let vw =  <@ TakesTwoTupledSubsumableArgs (a1,a2) @>
    let ve =  <@ TakesTwoTupledSubsumableArgs (a1,b2) @>
    let vr =  <@ TakesTwoTupledSubsumableArgs (b1,a2) @>
    let vt =  <@ TakesTwoTupledSubsumableArgs (b1,b2) @>
    let vW =  <@ TakesTwoTupledSubsumableArgs pAA @>
    let vX =  <@ TakesTwoTupledSubsumableArgs pAB @>
    let vY =  <@ TakesTwoTupledSubsumableArgs pBA @>
    let vZ =  <@ TakesTwoTupledSubsumableArgs pBB @>
    let _ =  <@ TakesTwoTupledSubsumableArgs @>

    let Qvw =  <@ TakesTwoGroupsOfTupledSubsumableArgs (a1,a2) (a1,a2) @>
    let Qve =  <@ TakesTwoGroupsOfTupledSubsumableArgs (a1,b2) (a1,a2) @>
    let Qvr =  <@ TakesTwoGroupsOfTupledSubsumableArgs (b1,a2) (a1,a2) @>
    let Qvt =  <@ TakesTwoGroupsOfTupledSubsumableArgs (b1,b2) (a1,a2) @>
    let QvW =  <@ TakesTwoGroupsOfTupledSubsumableArgs pAA     (a1,a2) @>
    let QvX =  <@ TakesTwoGroupsOfTupledSubsumableArgs pAB     (a1,a2) @>
    let QvY =  <@ TakesTwoGroupsOfTupledSubsumableArgs pBA     (a1,a2) @>
    let QvZ =  <@ TakesTwoGroupsOfTupledSubsumableArgs pBB     (a1,a2) @>

    let Wvw =  <@ TakesTwoGroupsOfTupledSubsumableArgs (a1,a2) (a1,b2) @>
    let Wve =  <@ TakesTwoGroupsOfTupledSubsumableArgs (a1,b2) (a1,b2) @>
    let Wvr =  <@ TakesTwoGroupsOfTupledSubsumableArgs (b1,a2) (a1,b2) @>
    let Wvt =  <@ TakesTwoGroupsOfTupledSubsumableArgs (b1,b2) (a1,b2) @>
    let WvW =  <@ TakesTwoGroupsOfTupledSubsumableArgs pAA     (a1,b2) @>
    let WvX =  <@ TakesTwoGroupsOfTupledSubsumableArgs pAB     (a1,b2) @>
    let WvY =  <@ TakesTwoGroupsOfTupledSubsumableArgs pBA     (a1,b2) @>
    let WvZ =  <@ TakesTwoGroupsOfTupledSubsumableArgs pBB     (a1,b2) @>

    let _ =  <@ TakesTwoGroupsOfTupledSubsumableArgs @>

    let fq() = TakesOneSubsumableArg a1
    let fw() = TakesOneSubsumableArg b1
    let fe() = TakesOneSubsumableArg
    let fr() = TakesTwoCurriedSubsumableArgs a1 a2
    let ft() = TakesTwoCurriedSubsumableArgs a1 b2
    let fy() = TakesTwoCurriedSubsumableArgs b1 a2
    let fu() = TakesTwoCurriedSubsumableArgs b1 b2
    let fi() = TakesTwoCurriedSubsumableArgs a1
    let fo() = TakesTwoCurriedSubsumableArgs b1
    let fp() = TakesTwoCurriedSubsumableArgs
    let fa() = TakesTwoTupledSubsumableArgs (a1,a2)
    let fs() = TakesTwoTupledSubsumableArgs (a1,b2)
    let fd() = TakesTwoTupledSubsumableArgs (b1,a2)
    let ff() = TakesTwoTupledSubsumableArgs (b1,b2)
    let fQ() = TakesTwoTupledSubsumableArgs pAA
    let fW() = TakesTwoTupledSubsumableArgs pAB
    let fX() = TakesTwoTupledSubsumableArgs pBA
    let fY() = TakesTwoTupledSubsumableArgs pBB


    let Dfa() = TakesTwoGroupsOfTupledSubsumableArgs (a1,a2)
    let Dfs() = TakesTwoGroupsOfTupledSubsumableArgs (a1,b2)
    let Dfd() = TakesTwoGroupsOfTupledSubsumableArgs (b1,a2)
    let Dff() = TakesTwoGroupsOfTupledSubsumableArgs (b1,b2)
    let DfQ() = TakesTwoGroupsOfTupledSubsumableArgs pAA
    let DfW() = TakesTwoGroupsOfTupledSubsumableArgs pAB
    let DfX() = TakesTwoGroupsOfTupledSubsumableArgs pBA
    let DfY() = TakesTwoGroupsOfTupledSubsumableArgs pBB

    let Cfa() = TakesTwoGroupsOfTupledSubsumableArgs : System.ValueType * System.ValueType -> System.ValueType * System.ValueType -> unit
    let Cfb() = TakesTwoGroupsOfTupledSubsumableArgs : System.ValueType * int -> System.ValueType * System.ValueType -> unit
    let Cfc() = TakesTwoGroupsOfTupledSubsumableArgs : int * System.ValueType -> System.ValueType * System.ValueType -> unit
    let Cfd() = TakesTwoGroupsOfTupledSubsumableArgs : int * int -> System.ValueType * System.ValueType -> unit
    let Cfe() = TakesTwoGroupsOfTupledSubsumableArgs : System.ValueType * System.ValueType -> int * System.ValueType -> unit
    let Cfr() = TakesTwoGroupsOfTupledSubsumableArgs : System.ValueType * System.ValueType -> System.ValueType * int -> unit


    let Xfa() = TakesTwoCurriedObjArgs : System.ValueType * System.ValueType -> System.ValueType * System.ValueType -> unit
    let Xfb() = TakesTwoCurriedObjArgs : System.ValueType * int -> System.ValueType * System.ValueType -> unit
    let Xfc() = TakesTwoCurriedObjArgs : int * System.ValueType -> System.ValueType * System.ValueType -> unit
    let Xfd() = TakesTwoCurriedObjArgs : int * int -> System.ValueType * System.ValueType -> unit
    let Xfe() = TakesTwoCurriedObjArgs : System.ValueType * System.ValueType -> int * System.ValueType -> unit
    let Xfr() = TakesTwoCurriedObjArgs : System.ValueType * System.ValueType -> System.ValueType * int -> unit

    let Rfa() = TakesTwoGroupsOfTupledSubsumableArgs (a1,a2) pAA
    let Rfs() = TakesTwoGroupsOfTupledSubsumableArgs (a1,b2) pAB
    let Rfd() = TakesTwoGroupsOfTupledSubsumableArgs (b1,a2) pBA
    let Rff() = TakesTwoGroupsOfTupledSubsumableArgs (b1,b2) pBB
    let RfQ() = TakesTwoGroupsOfTupledSubsumableArgs pAA pAA
    let RfW() = TakesTwoGroupsOfTupledSubsumableArgs pAB pAB
    let RfX() = TakesTwoGroupsOfTupledSubsumableArgs pBA pBA
    let RfY() = TakesTwoGroupsOfTupledSubsumableArgs pBB pBB

    let fg() = TakesTwoGroupsOfTupledSubsumableArgs
    let fRR() = TakesTwoCurriedSubsumableArgs


module FunctionsTakingSubsumableArgsTestReferenceTypes = 
    let TakesOneSubsumableArg (x:A) = ()
    let TakesTwoCurriedSubsumableArgs (x:A) (y:A) = ()
    let TakesTwoTupledSubsumableArgs (x:A, y:A) = ()

    let a1 = (new B() :> A) //(new B())
    let a2 = (new B() :> A) //(new B())
    let b1 = new B() //(new B())
    let b2 = new B() //(new B())


    let v1 =  <@ TakesOneSubsumableArg a1 @>
    let v2 =  <@ TakesOneSubsumableArg b1 @>
    let _ =  <@ TakesOneSubsumableArg @>
    let v4 =  <@ TakesTwoCurriedSubsumableArgs a1 a2 @>
    let v5 =  <@ TakesTwoCurriedSubsumableArgs a1 b2 @>
    let v6 =  <@ TakesTwoCurriedSubsumableArgs b1 a2 @>
    let v7 =  <@ TakesTwoCurriedSubsumableArgs b1 b2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs @>
    let vw =  <@ TakesTwoTupledSubsumableArgs (a1,a2) @>
    let ve =  <@ TakesTwoTupledSubsumableArgs (a1,b2) @>
    let vr =  <@ TakesTwoTupledSubsumableArgs (b1,a2) @>
    let vt =  <@ TakesTwoTupledSubsumableArgs (b1,b2) @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs @>

    let fq() = TakesOneSubsumableArg a1
    let fw() = TakesOneSubsumableArg b1
    let fe() = TakesOneSubsumableArg
    let fr() = TakesTwoCurriedSubsumableArgs a1 a2
    let ft() = TakesTwoCurriedSubsumableArgs a1 b2
    let fy() = TakesTwoCurriedSubsumableArgs b1 a2
    let fu() = TakesTwoCurriedSubsumableArgs b1 b2
    let fi() = TakesTwoCurriedSubsumableArgs a1
    let fo() = TakesTwoCurriedSubsumableArgs b1
    let fp() = TakesTwoCurriedSubsumableArgs
    let fa() = TakesTwoTupledSubsumableArgs (a1,a2)
    let fs() = TakesTwoTupledSubsumableArgs (a1,b2)
    let fd() = TakesTwoTupledSubsumableArgs (b1,a2)
    let ff() = TakesTwoTupledSubsumableArgs (b1,b2)
    let fg() = TakesTwoCurriedSubsumableArgs


module FunctionsTakingSubsumableArgsTestWithExtraApplication = 
    let TakesOneSubsumableArg (x:A) = failwith ""
    let TakesTwoCurriedSubsumableArgs (x:A) (y:A) = failwith ""
    let TakesTwoTupledSubsumableArgs (x:A, y:A) = failwith ""

    let a1 = (new B() :> A) //(new B())
    let a2 = (new B() :> A) //(new B())
    let b1 = new B() //(new B())
    let b2 = new B() //(new B())


    let _ =  <@ TakesOneSubsumableArg a1 @>
    let _ =  <@ TakesOneSubsumableArg b1 @>
    let _ =  <@ TakesOneSubsumableArg @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 a2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 b2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 a2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 b2 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,a2) @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,b2) @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,a2) @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,b2) @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs @>


    let _ =  <@ TakesOneSubsumableArg a1 () @>
    let _ =  <@ TakesOneSubsumableArg b1 ()  @>

    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 a2 () @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 b2 () @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 a2 () @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 b2 () @>

    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,a2) () @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,b2) () @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,a2) () @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,b2) () @>


    let _ =  <@ TakesOneSubsumableArg a1 b1 @>
    let _ =  <@ TakesOneSubsumableArg b1 b1  @>

    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 a2 b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs a1 b2 b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 a2 b1 @>
    let _ =  <@ TakesTwoCurriedSubsumableArgs b1 b2 b1 @>

    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,a2) b1 @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (a1,b2) b1 @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,a2) b1 @>
    let _ =  <@ TakesTwoTupledSubsumableArgs (b1,b2) b1 @>

    let fq() = TakesOneSubsumableArg a1 b1
    let fw() = TakesOneSubsumableArg b1 b1
    let fe() = TakesOneSubsumableArg b1
    let fr() = TakesTwoCurriedSubsumableArgs a1 a2 b1
    let ft() = TakesTwoCurriedSubsumableArgs a1 b2 b1
    let fy() = TakesTwoCurriedSubsumableArgs b1 a2 b1
    let fu() = TakesTwoCurriedSubsumableArgs b1 b2 b1
    let fi() = TakesTwoCurriedSubsumableArgs a1 b1
    let fo() = TakesTwoCurriedSubsumableArgs b1 b1
    let fp() = TakesTwoCurriedSubsumableArgs b1
    let fa() = TakesTwoTupledSubsumableArgs (a1,a2) b1
    let fs() = TakesTwoTupledSubsumableArgs (a1,b2) b1
    let fd() = TakesTwoTupledSubsumableArgs (b1,a2) b1
    let ff() = TakesTwoTupledSubsumableArgs (b1,b2) b1
    let fg() = TakesTwoCurriedSubsumableArgs b1

module BiGenericStaticMemberTests =
    type StaticClass1() = 
        static member M<'b>(c:'b, d:'b) = 1

    let obj = new obj()
    let str = ""

    StaticClass1.M<obj>(obj,str)  
    StaticClass1.M<obj>(str,obj)
    StaticClass1.M<obj>(obj,obj)
    StaticClass1.M<obj>(str,str)

    StaticClass1.M<string>(str,str)

    StaticClass1.M(obj,obj)
    StaticClass1.M(str,str)

module BiGenericFunctionTests =
    let M<'b>(c:'b, d:'b) = 1

    let obj = new obj()
    let str = ""

    M<obj>(obj,str)  
    M<obj>(str,obj)
    M<obj>(obj,obj)
    M<obj>(str,str)

    M<string>(str,str)

    M(obj,obj)
    M(str,str)

//-----------------------------------------------

module MoreSubsumptionPositiveTests = 
    let ex1a (x: A) = ()  
    let ex1b (x: #A) = ()  
    let ex1c (x: 'a when 'a :> A) = ()  

    let ex2a x = ex1a x
    let ex3a () x = ex1a x
    let ex3b <'a>  (x:A) = ex1a x

    ex1a (new B())  // 1.9.4 gives error 

    type StaticClass() = 
        static member Ex1A(x: A) = ()

    StaticClass.Ex1A(new B()) // 1.9.4 does not give error, "subsumption on member calls"
    new B() |> StaticClass.Ex1A // 1.9.4 does not give error, "subsumption on member calls"
    [new B()] |> List.map StaticClass.Ex1A // 1.9.4 does not give error, "subsumption on member calls"

    ex1a(new B()) 
    new B() |> ex1a 
    [new B()] |> List.map ex1a 

    ex2a(new B()) 
    new B() |> ex2a 
    [new B()] |> List.map ex2a 

    ex3a () (new B()) 
    new B() |> ex3a () 
    [new B()] |> List.map (ex3a ()) 

    ex3b<int> (new B()) 
    new B() |> ex3b<int> 
    [new B()] |> List.map (ex3b<int>) 

    ex3b<int> (new B2()) 
    new B2() |> ex3b<int> 
    [new B2()] |> List.map (ex3b<int>) 

    type ClassWithConstructorThatIsTooGenericWithoutCondensation =
        class
            val x : A
            new (x) = { x= x  }
        end

    type ClassWithSetterPropertyThatIsTooGenericWithoutCondensation() = 
        let mutable p = new A()
        member x.P 
            with get() = p 
            and set(v) = p <- v




module StillMoreSubsumptiontests = 
    type StaticClass2() = 
        static member DisplayControls(controls: A list) = ()
        
    let v = [ new A() ]
    //let v2 = [ new A(); new B() ]
    //let v2b = [ new B(); new C() ]
    //let v2c : A list = [ (new B() :> A); new C() ]


    //StaticClass2.DisplayControls [ (new B() :> A); new C() ]
    //StaticClass2.DisplayControls [ (new B1() :> A); new B2() ]

    //-----

    let f100 (x:A) = 1

    let g100 x = f100 x

    let g101 = f100  // Note: if a value restriction triggers on this test then the is-generalizeable-value check is not succeeding on the encoded form of the subsumable "f100"

    let p = (new B(), new B())

    let q = p

    let h100 x = 1


    let f71 (x : _) = x


    let f72 (x : list<_>) = x.Length

    let f73 (x:A) = x.P
    let f73b (x:_) = f73 x

    let f74 (x:list<#A>) = ()
    let f75 (x:list<'a> when 'a :> A) = ()

    //let v3 : A list = [ (new B() :> A); new B(); new B() ]
    //let v4 : A array = [| (new B() :> A); new B(); new B() |]

    let f1 (x:A) = ()    
    let f2 (x:A) (y:A) = ()    
    let f2b (x:A, y:A) = ()    
    let f2c (p : A*A) = 1+1

    f1 (new A())
    f1 (new B())

    f2 (new A()) (new A())
    f2 (new B()) (new B())
    f2 (new A()) (new B())
    f2 (new B()) (new A())

    f2b (new A(),new A())
    f2b (new B(),new B())
    f2b (new A(),new B())
    f2b (new B(),new A())

    f2c (new A(),new A())
    f2c (new B(),new B())
    f2c (new A(),new B())
    f2c (new B(),new A())

    let g x = f1 x

    let rec f3 (x:A)  = if false then () else f3 (new B())

    type r = { mutable f1 : A } 

    let r1 = { f1 = new A() } 
    r1.f1 <- new B()
    let r2 = { f1 = new B() } 

    type Data = Data of A * A

    Data (new A(),new A())
    Data (new B(),new B())
    Data (new A(),new B())
    Data (new B(),new A())

    let pAA = (new A(),new A()) 
    let pBB = (new B(),new B())
    let pAB = (new A(),new B())
    let pBA = (new B(),new A())
    pAA |> Data 

module BiGenericMethodsInGenericClassTests = 
    type C<'a>() =
        static member M(x:'a) = 1
        static member M2<'b>(x:'b) = 1
        static member M3<'b>(x:'b,y:'b) = 1
        static member OM3<'b>(x:'b,y:'b) = 1
        static member OM3<'b>(x:'b,y:int) = 1

    let obj = new obj()
    let str = ""


    C<obj>.M("a")
    C<obj>.M2<obj>("a")
    C<obj>.M2("a")
    C<obj>.M3<obj>(obj,obj)
    C<obj>.M3<obj>("a",obj)
    C<obj>.M3<obj>(obj,"a")
    C<obj>.M3<string>("b","a")

    C<obj>.M3(obj,obj)  
    C<obj>.M3("a","a")  

    C<obj>.OM3(obj,obj)  
    C<obj>.OM3<obj>(obj,obj)  
    C<obj>.OM3("a","a")  
    C<obj>.OM3<string>("a","a")  

    C<obj>.OM3(obj,"a")  



module TestSideEffectOrderForLambdasIntroducedBySubsumption1 = 
    type A() = 
       member x.P = 1

    type B() = 
       inherit A()
       member x.P = 1

    let vA = A()
    let vB = B()

    let mutable x = 0

    let f (a:A) = 
        x <- 1
        fun (b:A,c:A) ->
            x <- 2
            fun (c:int) ->
                x <- 3
                99

    module Test1 = 

        check "nckew9" x 0
        let f1  :      A * A -> int -> int = f   vA
        check "nckew9" x 1
        let f1b :               int -> int = f1  (vA,vA)  // no precomputation here when T is class type
        check "nckew9" x 2
        let f1c :                      int = f1b 3       // no precomputation here when T is class type
        check "nckew9" x 3


    module Test2 = 
        x <- 0
        check "nckew9" x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" x 1
        let f2b :               int -> int = f2 (vB,vB)  // no precomputation here when T is class type
        check "nckew9" x 2
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" x 3

    module Test3 = 
        x <- 0
        check "nckew9" x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" x 1
        let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
        check "nckew9" x 2
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" x 3


    module Test4 = 
        let TEST (f:A -> A * A -> int -> int) = 
            x <- 0
            check "nckew9" x 0
            let f2  :      A * A -> int -> int = f  vB
            check "nckew9" x 1
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" x 2
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" x 3

        TEST f
        

    module Test5 = 
        let TEST (f:A -> A * B -> int -> int) = 
            x <- 0
            check "nckew9" x 0
            let f2  :      A * B -> int -> int = f  vB
            check "nckew9" x 1
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" x 2
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" x 3

        TEST f    


    
module TestSideEffectOrderForLambdasIntroducedBySubsumption2 = 
    type A() = 
       member x.P = 1

    type B() = 
       inherit A()
       member x.P = 1

    let vA = A()
    let vB = B()

    let x = ref 0

    let f (a:A) (b:A,c:A) (d:int) =
        x := 3
        99

    let check s x1 x2 = if x1 = x2 then printfn "%s: ok" s else printfn "%s FAILED, expected %A, got %A  <<-----" s x2 x1

    module Test1 = 

        check "nckew9" !x 0
        let f1  :      A * A -> int -> int = f   vA
        check "nckew9" !x 0
        let f1b :               int -> int = f1  (vA,vA)  // no precomputation here when T is class type
        check "nckew9" !x 0
        let f1c :                      int = f1b 3       // no precomputation here when T is class type
        check "nckew9" !x 3


    module Test2 = 
        x := 0
        check "nckew9" !x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" !x 0
        let f2b :               int -> int = f2 (vB,vB)  // no precomputation here when T is class type
        check "nckew9" !x 0
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" !x 3

    module Test3 = 
        x := 0
        check "nckew9" !x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" !x 0
        let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
        check "nckew9" !x 0
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" !x 3


    module Test4 = 
        let TEST (f:A -> A * A -> int -> int) = 
            x := 0
            check "nckew9" !x 0
            let f2  :      A * A -> int -> int = f  vB
            check "nckew9" !x 0
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" !x 0
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" !x 3

        TEST f
        

    module Test5 = 
        let TEST (f:A -> A * B -> int -> int) = 
            x := 0
            check "nckew9" !x 0
            let f2  :      A * B -> int -> int = f  vB
            check "nckew9" !x 0
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" !x 0
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" !x 3

        TEST f    


// This variation has an argument pair "p:A*A"
module TestSideEffectOrderForLambdasIntroducedBySubsumption3 = 
    type A() = 
       member x.P = 1

    type B() = 
       inherit A()
       member x.P = 1

    let vA = A()
    let vB = B()

    let x = ref 0

    let f (a:A) = 
        x := 1
        fun (p: A*A) ->
            let (b:A,c:A) = p
            x := 2
            fun (c:int) ->
                x := 3
                99

    let check s x1 x2 = if x1 = x2 then printfn "%s: ok" s else printfn "%s FAILED, expected %A, got %A  <<-----" s x2 x1

    module Test1 = 

        check "nckew9" !x 0
        let f1  :      A * A -> int -> int = f   vA
        check "nckew9" !x 1
        let f1b :               int -> int = f1  (vA,vA)  // no precomputation here when T is class type
        check "nckew9" !x 2
        let f1c :                      int = f1b 3       // no precomputation here when T is class type
        check "nckew9" !x 3


    module Test2 = 
        x := 0
        check "nckew9" !x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" !x 1
        let f2b :               int -> int = f2 (vB,vB)  // no precomputation here when T is class type
        check "nckew9" !x 2
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" !x 3

    module Test3 = 
        x := 0
        check "nckew9" !x 0
        let f2  :      A * A -> int -> int = f  vB
        check "nckew9" !x 1
        let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
        check "nckew9" !x 2
        let f1c :                      int = f2b 3       // no precomputation here when T is class type
        check "nckew9" !x 3


    module Test4 = 
        let TEST (f:A -> A * A -> int -> int) = 
            x := 0
            check "nckew9" !x 0
            let f2  :      A * A -> int -> int = f  vB
            check "nckew9" !x 1
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" !x 2
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" !x 3

        TEST f
        

    module Test5 = 
        let TEST (f:A -> A * B -> int -> int) = 
            x := 0
            check "nckew9" !x 0
            let f2  :      A * B -> int -> int = f  vB
            check "nckew9" !x 1
            let f2b :               int -> int = f2 (vA,vB)  // no precomputation here when T is class type
            check "nckew9" !x 2
            let f1c :                      int = f2b 3       // no precomputation here when T is class type
            check "nckew9" !x 3

        TEST f    



/// Test the code generation for local type functions
module InnerConstrainedClosureTests = 
    let Example1 (y:'a,z:'b) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(x: #System.IComparable,y:'a) = 
            printfn "hello, %A" z
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h() = g(3,y)
        /// This just returnes the closure to make sure we don't optimize it all away
        h



    let Example2 (y:'a,z:'b) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(x: #System.IComparable) = 
            printfn "hello, %A" z
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h() = g(3)
        /// This just returnes the closure to make sure we don't optimize it all away
        h
            
    let Example3 (y:'b,z:'a) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(x: #System.IComparable,y:'b) = 
            printfn "hello, %A" z
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h() = g(3,y)
        /// This just returnes the closure to make sure we don't optimize it all away
        h

    let Example4 (y:'b,z:'a) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(x1: #System.IComparable,x2: #System.IComparable,y:'b) = 
            printfn "hello, %A" z
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h1() = g(3,4,y)
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h2() = g("3","4",y)
        /// This just returnes the closure to make sure we don't optimize it all away
        h1,h2


    let Example5 (y:'b,z:'a) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(x1: #System.IComparable,x2: #System.IComparable,y:'b) = 
            printfn "hello, %A" y
            printfn "hello, %A" z
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h1() = g(3,4,y)
        /// This uses the local type function in another closure that also captures one of the outer arguments
        let h2() = g("3","4",y)
        /// This just returnes the closure to make sure we don't optimize it all away
        h1,h2

    let Example6 (y:'b,z:'a) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g() = 
            printfn "hello, %A" y
            printfn "hello, %A" z
        g


    let Example7 (y:'b,z:'a) = 
        /// This gets compiled as a local type function because it is both generic and constrained
        let g(onp:#System.IComparable) = 
            printfn "hello, %A" y
            printfn "hello, %A" z
        g 3
        g "3"


module HashEqualityCOmpareOnNull =
/// As of Dev11, we now emit callvirt rather than call in these situations, so the tests below
/// will throw a System.NullReferenceException

    type R1 = { x : int }
    type R2 = { x1 : int; x2 : int  }

    type R4 = { x1 : int ; x2 : int ; x3 : int ; x4 : int }

    type U1 = | A
    type U2 = | A | B
    type U4 = | A | B | C | D 

        

    let hash_u1() = 
        (try hash (Unchecked.defaultof<U1>) with :? System.NullReferenceException -> 99) |> check "erw90re1" 99

    let hash_u2() = 
        (try hash (Unchecked.defaultof<U2>) with :? System.NullReferenceException -> 99) |> check "erw90re2" 99

    let hash_u4() = 
        (try hash (Unchecked.defaultof<U4>) with :? System.NullReferenceException -> 99) |> check "erw90re3" 99

    let hash_r1() = 
        (try hash (Unchecked.defaultof<R1>) with :? System.NullReferenceException -> 99) |> check "erw90re4" 99

    let hash_r2() = 
        (try hash (Unchecked.defaultof<R2>) with :? System.NullReferenceException -> 99) |> check "erw90re5" 99

    let hash_r4() = 
        (try hash (Unchecked.defaultof<R4>) with :? System.NullReferenceException -> 99) |> check "erw90re6" 99

    let equal_u1() = 
        (try ignore(Unchecked.defaultof<U1> = Unchecked.defaultof<U1>) ; false with :? System.NullReferenceException -> true) |> check "erw90re7" true
        (U1.A = Unchecked.defaultof<U1>) |> check "erw90re7" false
        (try ignore(Unchecked.defaultof<U1> = U1.A) ; false with :? System.NullReferenceException -> true)  |> check "erw90re7" true

    let equal_u2() = 
        (try ignore(Unchecked.defaultof<U2> = Unchecked.defaultof<U2>) ; false with :? System.NullReferenceException -> true)  |> check "erw90re8" true
        (U2.A = Unchecked.defaultof<U2>) |> check "erw90re7" false
        (try ignore(Unchecked.defaultof<U2> = U2.A) ; false with :? System.NullReferenceException -> true) |> check "erw90re7" true
        (U2.B = Unchecked.defaultof<U2>) |> check "erw90re7" false
        (try ignore(Unchecked.defaultof<U2> = U2.B) ; false with :? System.NullReferenceException -> true) |> check "erw90re7" true

    let equal_u4() = 
        (try ignore(Unchecked.defaultof<U4> = Unchecked.defaultof<U4>) ; false with :? System.NullReferenceException -> true)  |> check "erw90re9" true
        (U4.A = Unchecked.defaultof<U4>) |> check "erw90re7" false
        (try ignore(Unchecked.defaultof<U4> = U4.A) ; false with :? System.NullReferenceException -> true)  |> check "erw90re7" true
        (U4.B = Unchecked.defaultof<U4>) |> check "erw90re7" false
        (try ignore(Unchecked.defaultof<U4> = U4.B) ; false with :? System.NullReferenceException -> true)  |> check "erw90re7" true

    let equal_r1() = 
        (try ignore(Unchecked.defaultof<R1> = Unchecked.defaultof<R1>) ; false with :? System.NullReferenceException -> true)  |> check "erw90reQ" true
        ( { R1.x = 1 } = Unchecked.defaultof<R1>) |> check "erw90re7" false
        (try ignore( Unchecked.defaultof<R1> = { R1.x = 1 }) ; false with :? System.NullReferenceException -> true) |> check "erw90re7" true

    let equal_r2() = 
        (try ignore(Unchecked.defaultof<R2> = Unchecked.defaultof<R2>) ; false with :? System.NullReferenceException -> true)  |> check "erw90reW" true
        ( { R2.x1 = 1; R2.x2 = 3 } = Unchecked.defaultof<R2>) |> check "erw90re7" false
        (try ignore( Unchecked.defaultof<R2> = { R2.x1 = 1; R2.x2 = 3 } ) ; false with :? System.NullReferenceException -> true)  |> check "erw90re7" true

    let equal_r4() = 
        (try ignore(Unchecked.defaultof<R4> = Unchecked.defaultof<R4>) ; false with :? System.NullReferenceException -> true)  |> check "erw90reE" true

    let compare_u1() = 
        (try ignore(compare Unchecked.defaultof<U1> Unchecked.defaultof<U1>) ; 99 with :? System.NullReferenceException -> 99)  |> check "erw90reR" 99

    let compare_u2() = 
        (try ignore(compare Unchecked.defaultof<U2> Unchecked.defaultof<U2>) ; 99 with :? System.NullReferenceException -> 99)   |> check "erw90reT" 99

    let compare_u4() = 
        (try ignore(compare Unchecked.defaultof<U4> Unchecked.defaultof<U4>) ; 99 with :? System.NullReferenceException -> 99)   |> check "erw90reY" 99

    let compare_r1() = 
        (try ignore(compare Unchecked.defaultof<R1> Unchecked.defaultof<R1>) ; 99 with :? System.NullReferenceException -> 99)   |> check "erw90reU" 99

    let compare_r2() = 
        (try ignore(compare Unchecked.defaultof<R2> Unchecked.defaultof<R2>) ; 99 with :? System.NullReferenceException -> 99)   |> check "erw90reI" 99

    let compare_r4() = 
        (try ignore(compare Unchecked.defaultof<R4> Unchecked.defaultof<R4>) ; 99 with :? System.NullReferenceException -> 99)   |> check "erw90reO" 99



    hash_u1()
    hash_u2()
    hash_u4()
    hash_r1()
    hash_r2()
    hash_r4()
    equal_u1()
    equal_u2()
    equal_u4()
    equal_r1()
    equal_r2()
    equal_r4()
    compare_u1()
    compare_u2()
    compare_u4()
    compare_r1()
    compare_r2()
    compare_r4()

    let hash_g<'T when 'T : equality>() = 
        // random code to prevent inlining
        let v1 = ref 0 
        let v2 = ref 0 
        let v3 = ref 0 
        let nm = (sprintf "erw90re1h, %A" (typeof<'T>).Name)
        (try ignore(hash (Unchecked.defaultof<'T>)) ; 99 with :? System.NullReferenceException -> 99)   |> check nm 99
        v1 := 0
        v2 := 0
        v3 := 0

    let compare_g<'T when 'T : comparison>(v) = 
        // random code to prevent inlining
        let v1 = ref 0 
        let v2 = ref 0 
        let v3 = ref 0 
        let nm = (sprintf "erw90re1c, %A" (typeof<'T>).Name)
        (try ignore(compare Unchecked.defaultof<'T> Unchecked.defaultof<'T>) ; 99 with :? System.NullReferenceException -> 99)   |> check nm 99
        (try ignore(compare Unchecked.defaultof<'T> v) ; 99 with :? System.NullReferenceException -> 99 )  |> check nm 99
        compare v Unchecked.defaultof<'T> |> check nm 1
        v1 := 0
        v2 := 0
        v3 := 0

    let equals_g<'T when 'T : equality>(v) = 
        // random code to prevent inlining
        let v1 = ref 0 
        let v2 = ref 0 
        let v3 = ref 0 
        let nm = (sprintf "erw90re1e, %A" (typeof<'T>).Name)
        (Unchecked.defaultof<'T> = Unchecked.defaultof<'T>) |> check nm true
        (Unchecked.defaultof<'T> = v) |> check nm false
        //(try ignore(Unchecked.defaultof<'T> = Unchecked.defaultof<'T>) ; false with :? System.NullReferenceException -> true)   |> check nm true
        //(try ignore(Unchecked.defaultof<'T> = v) ; false with :? System.NullReferenceException -> true)   |> check nm true
        (v = Unchecked.defaultof<'T>) |> check nm false
        v1 := 0
        v2 := 0
        v3 := 0

    hash_g<U1>() 
    hash_g<U2>() 
    hash_g<U4>() 
    hash_g<R1>() 
    hash_g<R2>() 
    hash_g<R4>() 

    compare_g<U1>(U1.A) 
    compare_g<U2>(U2.A) 
    compare_g<U4>(U4.A) 
    compare_g<R1>( { R1.x = 1 } ) 
    compare_g<R2>( { R2.x1 = 1; R2.x2 = 2 } ) 
    compare_g<R4>( { x1 = 1; x2 = 2; x3 = 2; x4 = 2 }  ) 

    equals_g<U1>(U1.A) 
    equals_g<U2>(U2.A) 
    equals_g<U4>(U4.A) 
    equals_g<R1>( { R1.x = 1 } ) 
    equals_g<R2>( { R2.x1 = 1; R2.x2 = 2 } ) 
    equals_g<R4>( { x1 = 1; x2 = 2; x3 = 2; x4 = 2 }  ) 


module CoercivePipingTest = 
    let f1 (x:obj) = 
        x |> id :?> int  |> id :> obj |> id

    let f2 (x:obj) = 
        x |> id 
          :?> int  |> id :> obj |> id

    let f3 (x:obj) = 
        x |> id 
          :?> int  
          |> id :> obj |> id
               
    let f4 (x:obj) = 
        x |> id 
          :?> int  
          |> id 
          :> obj |> id

    let f5 (x:obj) = 
        x |> id 
          :?> int  
          |> id 
          :> obj 
          |> id
               
    let f6 (x:obj) = 
        x 
        |> id 
          :?> int  
          |> id 
          :> obj 
          |> id
               
    let f7 (x:obj) = 
        x 
        |> id 
        :?> int  
          |> id 
          :> obj 
          |> id
               

    let f8 (x:obj) = 
        x 
        |> id 
        :?> int  
        |> id 
        :> obj 
          |> id

    let f9 (x:obj) = 
        x 
        |> id 
        :?> int  
        |> id 
        :> obj 
          |> id

    check "clwcweki" (f1 3) (box 3)
    check "clwcweki" (f2 3) (box 3)
    check "clwcweki" (f3 3) (box 3)
    check "clwcweki" (f4 3) (box 3)
    check "clwcweki" (f5 3) (box 3)
    check "clwcweki" (f6 3) (box 3)
    check "clwcweki" (f7 3) (box 3)
    check "clwcweki" (f8 3) (box 3)
    check "clwcweki" (f9 3) (box 3)

    // this was the actual repro
    let f (info: System.Reflection.MethodInfo) = 
      System.Attribute.GetCustomAttribute(info, typeof<ReflectedDefinitionAttribute>)
      :?> ReflectedDefinitionAttribute

module Test_Dev10_Bug_917383 = 

    // This code did not verify
    let testCode inp = 
       let lf s v = List.ofSeq s |> List.findIndex (fun y -> y = v)
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
       match inp with 
       | 0 -> 1
       | _ -> 
           lf [1;inp] inp
    check "elcvewerv" (testCode 5) 1

/// This gave  a peverify error due to bad codegen
module DevDiv_Bug_918202 = 
    type sideffect = (unit -> unit) 
    type 'a refopt = 'a ref option 
    type FwdRev<'x,'xb>(rev) = 
         let rev : 'xb refopt       = rev 
         member this.Rev with get() = rev 
    type FwdRevArray<'x,'xb> = FwdRev<'x[], 'xb ref[]> 
    let blit_b  (arr1 : FwdRevArray<'x,'xb>) start1 
                (arr2 : FwdRevArray<'x,'xb>) start2 len = 
        let reset =                                                                        
            let arr2b = !arr2.Rev.Value 
            let zeros = arr2b.[start2 .. start2+len-1] 
            let geti  = 
                let temp = !arr1.Rev.Value 
                fun i -> temp.[i] 
            for j in start2 .. start2+len-1 do                    
                arr2b.[j] <- geti (j-start2+start1)       
            fun () -> for j in start2 .. start2+len-1 do 
                            arr2b.[j] <- zeros.[j-start2] 
        (), reset 


module TestTwoConversionsOK = 

    let test1 i =     
       let v = uint32 i 
       int64 i  // previously gave: error: type 'int64' does not match 'uint32'

// See FSharp 1.0 6477
//The problem is related to the rule that  type variable sets may not have two 
// trait constraints with the same name (similarly, may not be constrained by 
// different instantiations of the same interface type), and if two constraints 
// with the same name are given then the argument types and return types are 
// asserted to be equal.
//
//This rule is a deliberate artificial limitation to reduce the complexity 
// of type inference in the common case, at the cost of making inline code 
// less generic. However, the rule should not apply to op_Explicit and op_Implicit constraints. These are special constraint names, known to the language, and we already have special rules around these operators to ensure that the return type 
// is effectively considered to be part of the name of the constraint
//  (i.t. op_Explicit -->  int64 is effectively a different constraint to op_Explicit --> int32). 
//
//So the solution is thus to not apply the rule for these constraints. 
module TestTwoConversionsOK_FSharp_6477 = 


    let test() =
         let test1 i =
             uint32 i |> ignore

         let test2 i =
             test1 i
             int64 (min i 10) // previously gave: error: type 'int64' does not match 'uint32'

         test2 10


// See bug dev11 129910.
// The common thing here is that the type definitions include members or fields
// whose type is a type variable ?X constrained to a type IFoo<'T> involving the 
// enclosing class type variable T. ?X should be solved at the point where we generalize
// T. In F# 2.0 some of these gave invalid code generation ("undefined type variable" errors
// during code generation). In F# 3.0 they compile OK.
//
// Some mutually recursive cases have been included for completeness, though there's nothing 
// particularly specific being tested in those cases.
module TestPartiallyConstrainedInferenceProblems = 

    type C1<'T>(v) = 
        member x.M() = (v :> IEvent<'T> |> ignore)

    type C2<'T> = 
        member x.P with get v = (v :> IEvent<'T> |> ignore)

    type C3<'T>() = 
        member x.P with get v = (v :> IEvent<'T> |> ignore)

    type C4<'T> = 
        new () = { } 
        new v = (v :> IEvent<'T> |> ignore); C4<'T>()

    type C5<'T>() = 
        member x.P with get v = (v + v |> ignore)

    type C6<'T>() = 
        member x.P with get v = (match v with null -> ()  | _ -> ())

    type C7<'T>(v) = 
        member x.M() = (match v with null -> ()  | _ -> ())

    type C8<'T>(v) = 
        member x.M() = (sprintf "%d" v |> ignore)

    type C9<'T>(v) = 
        member x.M() = (sprintf "%f" v |> ignore)





    type MutualC1a<'T>(v) = 
        member x.M() = (v :> IEvent<'T> |> ignore)
    and MutualC1b<'T>(v) = 
        member x.M() = (v :> System.IComparable<'T> |> ignore)

    type MutualC2a<'T> = 
        member x.P with get v = (v :> IEvent<'T> |> ignore)
    and MutualC2b<'T> = 
        member x.P with get v = (v :> System.IComparable<'T> |> ignore)

    type MutualC3a<'T>() = 
        member x.P with get v = (v :> IEvent<'T> |> ignore)
    and MutualC3b<'T>() = 
        member x.P with get v = (v :> System.IComparable<'T> |> ignore)

    type MutualC4a<'T> = 
        new () = { } 
        new v = (v :> IEvent<'T> |> ignore); MutualC4a<'T>()

    and MutualC4b<'T> = 
        new () = { } 
        new v = (v :> System.IComparable<'T> |> ignore); MutualC4b<'T>()

module RecordPropertyConstraints = 

    type A1 = { Age: int;    Name: string; LifeTime: System.TimeSpan }
    type A2 = { mutable Age: int;    mutable Name: string ; mutable LifeTime: System.TimeSpan }
    type B = { name: string } with member this.Name = this.name

    [<Struct>]
    type ImmutableStructImplicit(age:int,name:string, lifeTime:System.TimeSpan) = 
        member x.Age = age
        member x.Name = name
        member x.LifeTime = lifeTime

    type ImmutableClassImplicit(age:int,name:string, lifeTime:System.TimeSpan) = 
        member val Age = age with get
        member val Name = name with get
        member val LifeTime = lifeTime with get

    type MutableClassImplicit(age:int,name:string, lifeTime:System.TimeSpan) = 
        member val Age = age with get,set
        member val Name = name with get,set
        member val LifeTime = lifeTime with get,set

    // This form of struct definition does not satisfy property constraints
    [<Struct>]
    type ImmutableStructExplicit = 
        val Age: int
        val Name: string
        val LifeTime: System.TimeSpan

    [<Struct>]
    type MutableStructExplicit =
        val mutable Age: int
        val mutable Name: string 
        val mutable LifeTime: System.TimeSpan 

    type ImmutableClassExplicit = 
        val Age: int
        val Name: string
        val LifeTime: System.TimeSpan
        new(age,name,lifeTime) = { Age=age; Name=name; LifeTime = lifeTime }

    type MutableClassExplicit =
        val mutable Age: int
        val mutable Name: string 
        val mutable LifeTime: System.TimeSpan 
        new(age,name,lifeTime) = { Age=age; Name=name; LifeTime = lifeTime }

    let inline name (x:^T) = (^T : (member Name : string) x)
    let inline setName (x:^T) (y:string) = (^T : (member set_Name : string -> unit) (x,y))
    let inline setName2 (x:^T) (y:string) = (^T : (member Name : string with set) (x,y))
    let inline age (x:^T) = (^T : (member Age : int) x)
    let inline setAge (x:^T) (y:int) = (^T : (member set_Age : int -> unit) (x,y))
    let inline lifetime (x:^T) = (^T : (member LifeTime : System.TimeSpan) x)
    let inline setLifetime (x:^T) (y:System.TimeSpan) = (^T : (member set_LifeTime : System.TimeSpan -> unit) (x,y))

    let a1 : A1 = { Age = 29; Name = "Harry" ; LifeTime=System.TimeSpan.Zero}
    let a2 : A2 = { Age = 29; Name = "Sally" ; LifeTime=System.TimeSpan.Zero}
    let b = { name = "Gary" }
    let s1 : ImmutableStructImplicit = ImmutableStructImplicit()
    let s1e  = ImmutableStructExplicit()
    let s2e  = MutableStructExplicit()
    let c1 : ImmutableClassImplicit = ImmutableClassImplicit(29,"HarryJ",System.TimeSpan.Zero)
    let c2 : MutableClassImplicit = MutableClassImplicit(29,"HarryK",System.TimeSpan.Zero)
    let c1e : ImmutableClassExplicit = ImmutableClassExplicit(29,"HarryL",System.TimeSpan.Zero)
    let c2e : MutableClassExplicit = MutableClassExplicit(29,"HarryM",System.TimeSpan.Zero)

    let f1() = name a1 // now works
    let f1s1() = name s1 // always worked
    let f1s1e() = name s1e // always worked
    let f1s2e() = name s2e // always worked
    let f1c1() = name c1 // always worked
    let f1c2() = name c2 // always worked
    let f1c1e() = name c1e // always worked
    let f1c2e() = name c2e // always worked
    let f1a2() = name a2 // now works
    let f3a2() = setName a2 "HarrySally" // now works
    let f3c2() = setName c2 "HarrySallyDally" // now works
    let f3c2e() = setName c2e "HarrySallyDallyMally" // now works
    let f3a2x() = setName2 a2 "HarrySallyX" // now works
    let f3c2x() = setName2 c2 "HarrySallyDallyX" // now works
    let f3c2ex() = setName2 c2e "HarrySallyDallyMallyX" // now works
    let f4() = age a1 // now works
    let f5 () = age a2 // now works
    let f6() = setAge a2 3 // now works
    let f7() = lifetime a1 // now works
    let f8 () = lifetime a2 // now works
    let f9() = setLifetime a2 (System.TimeSpan.FromSeconds 2.0) // now works
    let f10() = name b // always worked

module RecordPropertyConstraintTests =  
   
    open RecordPropertyConstraints 

    check "ckjwnewk" (f1()) "Harry"
    check "ckjwnewk" (f1s1()) null
    check "ckjwnewk" (f1s1e()) null
    check "ckjwnewk" (f1s2e()) null
    check "ckjwnewk" (f1c1()) "HarryJ"
    check "ckjwnewk" (f1c2()) "HarryK"
    check "ckjwnewk" (f1c1e()) "HarryL"
    check "ckjwnewk" (f1c2e()) "HarryM"
    
    check "ckjwnewk" (f1a2()) "Sally"
    check "ckjwnewk" (f3a2()) ()
    check "ckjwnewk" (f1a2()) "HarrySally"  // after mutation
    check "ckjwnewk" (f3a2x()) ()
    check "ckjwnewk" (f1a2()) "HarrySallyX"  // after mutation
    check "ckjwnewk" (f3c2()) ()
    check "ckjwnewk" (f1c2()) "HarrySallyDally"  // after mutation
    check "ckjwnewk" (f3c2x()) ()
    check "ckjwnewk" (f1c2()) "HarrySallyDallyX"  // after mutation
    check "ckjwnewk" (f3c2e()) ()
    check "ckjwnewk" (f1c2e()) "HarrySallyDallyMally"  // after mutation
    check "ckjwnewk" (f3c2ex()) ()
    check "ckjwnewk" (f1c2e()) "HarrySallyDallyMallyX"  // after mutation

    check "ckjwnewk" (f4()) 29
    check "ckjwnewk" (f5()) 29
    check "ckjwnewk" (f6()) ()
    check "ckjwnewk" (f5()) 3  // after mutation
    check "ckjwnewk" (f7()) System.TimeSpan.Zero
    check "ckjwnewk" (f8()) System.TimeSpan.Zero
    check "ckjwnewk" (f9()) ()
    check "ckjwnewk" (f8()) (System.TimeSpan.FromSeconds 2.0) // after mutation
    check "ckjwnewk" (f10()) "Gary"

// See https://github.com/Microsoft/visualfsharp/issues/740 - inlining on subtypes was not allowed
module InliningOnSubTypes1 = 
    type A() =
        static member inline dosomething() = ()

    type B() =
        inherit A()
        member inline this.SomethingElse a = a + 10
        member inline this.SomethingElse2 a b = a + b + 10

    let f () = 
        let b = B() 
        let x1 = b.SomethingElse 3
        let x2 = b.SomethingElse2 3 4
        (x1, x2)
    do check "clkewlijwlkw" (f()) (13, 17) 


module StructUnionSingleCase = 
    [<Struct>]
    type S = S

    do check "wekew0ewek1" (typeof<S>.IsValueType) true
    do check "wekew0ewek1b" (typeof<S>.BaseType) typeof<System.ValueType>

    type SAbbrev = S

    do check "wekew0ewek2" (typeof<SAbbrev>.IsValueType) true
    do check "wekew0ewek2b" (typeof<SAbbrev>.BaseType) typeof<System.ValueType>

    type S0 = S0
    do check "wekew0ewek3" (typeof<S0>.IsValueType) false
    do check "wekew0ewek3b" (typeof<S0>.BaseType) typeof<obj>

    [<Struct>]
    type S2 = | S2

    do check "wekew0ewek4" (typeof<S2>.IsValueType) true
    do check "wekew0ewek4b" (typeof<S2>.BaseType) typeof<System.ValueType>

    [<Struct>]
    type S3 = | S2a | S3a

    do check "wekew0ewek5" (typeof<S3>.IsValueType) true
    do check "wekew0ewek5b" (typeof<S3>.BaseType) typeof<System.ValueType>

// See https://github.com/Microsoft/visualfsharp/issues/238
module GenericPropertyConstraintSolvedByRecord = 

    type hober<'a> = { foo : 'a }

    let inline print_foo_memb x = box (^a : (member foo : 'b) x)

    let v = print_foo_memb { foo=1 } 


module SRTPFix = 

    open System

    let inline konst x _ = x

    type CFunctor() = 
      static member inline fmap (f : ^a -> ^b, a : ^a list) = List.map f a
      static member inline fmap (f : ^a -> ^b, a : ^a option) =
        match a with
        | None -> None
        | Some x -> Some (f x)

      // default implementation of replace
      static member inline replace< ^a, ^b, ^c, ^d, ^e when ^a :> CFunctor and (^a or ^d) : (static member fmap : (^b -> ^c) * ^d -> ^e) > (a, f) =
        ((^a or ^d) : (static member fmap : (^b -> ^c) * ^d -> ^e) (konst a, f))

      // call overridden replace if present
      static member inline replace< ^a, ^b, ^c when ^b : (static member replace : ^a * ^b -> ^c)>(a : ^a, f : ^b) =
        (^b : (static member replace : ^a * ^b -> ^c) (a, f))

    let inline replace_instance< ^a, ^b, ^c, ^d when (^a or ^c) : (static member replace : ^b * ^c -> ^d)> (a : ^b, f : ^c) =
      ((^a or ^c) : (static member replace : ^b * ^c -> ^d) (a, f))

    let inline fmap_instance< ^a, ^b, ^c, ^d, ^e when (^a or ^d) : (static member fmap : (^b -> ^c) * ^d -> ^e)>(f : ^b -> ^c, a : ^d) =
      ((^a or ^d) : (static member fmap : (^b -> ^c) * ^d -> ^e) (f, a))

    let inline fmap (f : ^a -> ^b) (a : ^c) =
      fmap_instance<CFunctor, _, _, _, _> (f, a)

    let inline replace (a : ^a) (f : ^b) : ^a0 when (CFunctor or  ^b) : (static member replace :  ^a *  ^b ->  ^a0) =
      replace_instance<CFunctor, _, _, _> (a, f)

    (*
    type test(arg : string) = class
      member __.data = arg
      static member inline fmap (f : char -> char, a : test) = String.map f a.data
      static member inline replace (a : char, f : test) = test.fmap (konst a, f)
    end

    let _ =
      printfn "%A" <| fmap id [1;2;3];
      printfn "%A" <| replace 5 [1;2;3];
      printfn "%A" <| fmap ((+) 1) (Some 2);
      printfn "%A" <| replace 'q' (test("HI"))
     *)


module SRTPFixAmbiguity =
    // Mini Repro from FSharpPlus https://github.com/gusty/FSharpPlus
    type Id<'t>(v:'t) = member __.getValue = v
    type Interface<'t> = abstract member getValue : 't

    type Monad =
        static member inline InvokeReturn (x:'T) : '``Monad<'T>`` =
            let inline call (mthd : ^M, output : ^R) = ((^M or ^R) : (static member Return: _ -> _) output)
            call (Unchecked.defaultof<Monad>, Unchecked.defaultof<'``Monad<'T>``>) x
        static member Return (_:Interface<'a>) = fun (_:'a) -> Unchecked.defaultof<Interface<'a>> : Interface<'a>
        static member Return (_:seq<'a>      ) = fun x -> Seq.singleton x                         : seq<'a>
        static member Return (_:option<'a>   ) = fun x -> Some x                                  : option<'a>
        static member Return (_:Id<'a>       ) = fun x -> Id x                                    : Id<'a>

        static member inline InvokeBind (source : '``Monad<'T>``) (binder : 'T -> '``Monad<'U>``) : '``Monad<'U>`` =
            let inline call (mthd : 'M, input : 'I, _output : 'R, f) = ((^M or ^I or ^R) : (static member Bind: _*_ -> _) input, f)
            call (Unchecked.defaultof<Monad>, source, Unchecked.defaultof<'``Monad<'U>``>, binder)
        static member Bind (source : Interface<'T>, f : 'T -> Interface<'U>) = f source.getValue    : Interface<'U>
        static member Bind (source : seq<'T>      , f : 'T -> seq<'U>      ) = Seq.collect f source : seq<'U>
        static member Bind (source : Id<'T>       , f : 'T -> Id<'U>       ) = f source.getValue    : Id<'U>
        static member Bind (source :option<'T>    , f : 'T -> _            ) = Option.bind f source : option<'U>

    let inline result (x:'T)                                   = Monad.InvokeReturn x :'``Monad<'T>``
    let inline (>>=) (x:'``Monad<'T>``) (f:'T->'``Monad<'U>``) = Monad.InvokeBind x f :'``Monad<'U>``

    type ReaderT<'R,'``monad<'T>``> = ReaderT of ('R -> '``monad<'T>``)
    let runReaderT (ReaderT x) = x : 'R -> '``Monad<'T>``
    type ReaderT<'R,'``monad<'T>``> with
        static member inline Return _ = fun (x : 'T) -> ReaderT (fun _ -> result x)                                                   : ReaderT<'R, '``Monad<'T>``> 
        static member inline Bind (ReaderT (m:_->'``Monad<'T>``), f:'T->_) = ReaderT (fun r -> m r >>= (fun a -> runReaderT (f a) r)) : ReaderT<'R, '``Monad<'U>``>


    let test1 : ReaderT<string, option<_>> = ReaderT result >>= result
    let test2 : ReaderT<string, Id<_>>     = ReaderT result >>= result
    let test3 : ReaderT<string, seq<_>>    = ReaderT result >>= result


// See https://github.com/Microsoft/visualfsharp/issues/4040
module InferenceRegression4040 = 
    type Foo() =
        static let test (t : 'T) : 'T list = 
            let b : Bar<'T> = new Bar<'T>(t)
            [b.Value]

        static member Test(t : int) = test t

    and Bar<'U>(value : 'U) =
        member __.Value = value

    printfn "%A" (Foo.Test 42)


// See https://github.com/Microsoft/visualfsharp/issues/4040
module InferenceRegression4040b = 
    type Foo() =
        static let test (t : 'T) : 'T list = 
            let b : Bar<'T> = new Bar<'T>(t)
            [b.Value]

        static member Test(t : int) = test t

    and Bar<'T>(value : 'T) =
        member __.Value = value

    printfn "%A" (Foo.Test 42)

// See https://github.com/Microsoft/visualfsharp/issues/4040
module InferenceRegression4040C = 
    type Foo() =
        static let test (t : 'T) : 'T list = 
            let b : Bar<'T> = new Bar<'T>(t)
            [b.Value]

        static member Test(t : int) = test t

    and Bar<'U>(value : 'U) =
        member __.Value : 'U = value

    printfn "%A" (Foo.Test 42)


module TestInheritFunc = 
    type Foo() =
        inherit FSharpFunc<int,int>()
        override __.Invoke(a:int) = a + 1

    check "cnwcki1" ((Foo() |> box |> unbox<int -> int> ) 5) 6

module TestInheritFuncGeneric = 
    type Foo<'T,'U>() =
        inherit FSharpFunc<'T,'T>()
        override __.Invoke(a:'T) = a

    check "cnwcki2" ((Foo<int,int>() |> box |> unbox<int -> int> ) 5) 5


module TestInheritFunc2 = 
    type Foo() =
        inherit OptimizedClosures.FSharpFunc<int,int,int>()
        override f.Invoke(a:int) = (fun u -> f.Invoke(a,u))
        override __.Invoke(a:int,b:int) = a + b + 1

    check "cnwcki3" ((Foo() |> box |> unbox<int -> int -> int> ) 5 6) 12

module TestInheritFunc3 = 
    type Foo() =
        inherit OptimizedClosures.FSharpFunc<int,int,int,int>()
        override f.Invoke(t) = (fun u v -> f.Invoke(t,u,v))
        override __.Invoke(a:int,b:int,c:int) = a + b + c + 1

    check "cnwcki4" ((Foo() |> box |> unbox<int -> int -> int -> int> ) 5 6 7) 19

#if !NETCOREAPP
module TestConverter =
    open System

    let fromConverter (f: Converter<'T1,'X>) = FSharp.Core.FSharpFunc.FromConverter f
    let implicitConv (f: Converter<'T1,'X>) = FSharp.Core.FSharpFunc.op_Implicit f
    let toConverter (f: 'T1 -> 'X) = FSharp.Core.FSharpFunc.ToConverter f
    let toConverter2 (f: FSharpFunc<'T1, 'X>) = FSharp.Core.FSharpFunc.ToConverter f

    test "cenwceoiwe1" ((id |> toConverter |> fromConverter) 6 = 6)
    test "cenwceoiwe2" ((id |> toConverter |> fromConverter |> toConverter2 |> implicitConv) 6 = 6)
#endif

// See https://github.com/dotnet/fsharp/issues/3814#issuecomment-441048460
module TestAnotherCaseOfSRTP = 

    type X =
        static member Method (a: int) = 2
        static member Method (a: int64) = 3


    let inline Test< ^t, ^a when (^t or ^a): (static member Method: ^a -> int)> (value: ^a) =
        ( (^t or ^a): (static member Method: ^a -> int)(value))
        
    let inline Test2< ^t when (X or  ^t) : (static member Method :  ^t -> int)> a = Test<X, ^t> a

    check "fweew-0wve" (Test2<int> 0) 2

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

