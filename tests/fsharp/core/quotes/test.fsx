// #Conformance #Quotations #Interop #Classes #ObjectConstructors #Attributes #Reflection #ComputationExpression
#if TESTS_AS_APP
module Core_quotes
#endif
#light

#if !TESTS_AS_APP && !NETCOREAPP
#r "cslib.dll"
#endif


#nowarn "57"
open System
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns


let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

let test s b = stderr.Write(s:string);  if b then stderr.WriteLine " OK" else report_failure s

let check s v1 v2 = 
   stderr.Write(s:string);  
   if (v1 = v2) then 
       stderr.WriteLine " OK" 
   else
       eprintf " FAILED: got %A, expected %A" v1 v2 
       report_failure s

let rec removeDoubleSpaces (s: string) =
   let s2 = s.Replace("  ", " ")
   if s = s2 then s else removeDoubleSpaces s2

let normalizeActivePatternResults (s: string) =
   System.Text.RegularExpressions.Regex(@"activePatternResult\d+").Replace(s, "activePatternResult")

let checkStrings s (v1: string) (v2: string) = 
    check s 
       (v1.Replace("\r","").Replace("\n","") |> removeDoubleSpaces |> normalizeActivePatternResults)
       (v2.Replace("\r","").Replace("\n","") |> removeDoubleSpaces |> normalizeActivePatternResults)

let checkQuoteString s expected (q: Expr)  = checkStrings s (sprintf "%A" q) expected

let (|TypedValue|_|) (v : 'T) value = 
    match value with 
    | Patterns.Value(:? 'T as v1, ty) when ty = typeof<'T> && v = v1-> Some ()
    | _ -> None

let (|ObjTy|_|) ty = if ty = typeof<obj> then Some() else None
let (|IntTy|_|) ty = if ty = typeof<int> then Some() else None
let (|StringTy|_|) ty = if ty = typeof<string> then Some() else None

let (|TupleTy|_|) ty = 
    if Microsoft.FSharp.Reflection.FSharpType.IsTuple ty then
        let [| t1; t2 |] = Microsoft.FSharp.Reflection.FSharpType.GetTupleElements ty
        Some (t1, t2)
    else None

[<Struct>]
type S = 
    val mutable x : int


module TypedTest = begin 

    // Checks the shape of the quotation to match that of
    // foreach implemented in terms of GetEnumerator ()
    let (|ForEachShape|_|) = function
        | Let (
                inputSequence,
                inputSequenceBinding,
                Let (
                        enumerator,
                        enumeratorBinding,
                        TryFinally (
                            WhileLoop (
                                guard,
                                Let (i, currentExpr, body)),
                            cleanup)
                        )
                ) -> Some inputSequence
        | _ -> None

    let x = <@ 1 @>

    test "check SByte"    ((<@  1y   @> |> (function SByte 1y ->   true | _ -> false))) 
    test "check Int16"    ((<@  1s   @> |> (function Int16 1s ->   true | _ -> false))) 
    test "check Int32"    ((<@  1    @> |> (function Int32 1 ->    true | _ -> false))) 
    test "check Int64"    ((<@  1L   @> |> (function Int64 1L ->   true | _ -> false))) 
    test "check Byte"     ((<@  1uy  @> |> (function Byte 1uy ->   true | _ -> false))) 
    test "check UInt16"   ((<@  1us  @> |> (function UInt16 1us -> true | _ -> false))) 
    test "check UInt32"   ((<@  1u   @> |> (function UInt32 1u ->  true | _ -> false))) 
    test "check UInt64"   ((<@  1UL  @> |> (function UInt64 1UL -> true | _ -> false))) 
    test "check String"   ((<@  "1"  @> |> (function String "1" -> true | _ -> false))) 

    test "check ~SByte"   ((<@  "1"  @> |> (function SByte _ ->    false | _ -> true))) 
    test "check ~Int16"   ((<@  "1"  @> |> (function Int16 _ ->    false | _ -> true))) 
    test "check ~Int32"   ((<@  "1"  @> |> (function Int32 _ ->    false | _ -> true))) 
    test "check ~Int64"   ((<@  "1"  @> |> (function Int64 _ ->    false | _ -> true))) 
    test "check ~Byte"    ((<@  "1"  @> |> (function Byte _ ->     false | _ -> true))) 
    test "check ~UInt16"  ((<@  "1"  @> |> (function UInt16 _ ->   false | _ -> true))) 
    test "check ~UInt32"  ((<@  "1"  @> |> (function UInt32 _ ->   false | _ -> true))) 
    test "check ~UInt64"  ((<@  "1"  @> |> (function UInt64 _ ->   false | _ -> true))) 
    test "check ~String"  ((<@  1    @> |> (function String "1" -> false | _ -> true))) 

#if !FSHARP_CORE_31
    test "check Decimal"  ((<@  1M   @> |> (function Decimal 1M -> true | _ -> false))) 
    test "check ~Decimal" ((<@  "1"  @> |> (function Decimal _ ->  false | _ -> true))) 
    test "check ~Decimal neither" ((<@ 1M + 1M @> |> (function Decimal _ ->  false | _ -> true))) 
#endif

    test "check AndAlso" ((<@ true && true  @> |> (function AndAlso(Bool(true),Bool(true)) -> true | _ -> false))) 
    test "check OrElse"  ((<@ true || true  @> |> (function OrElse(Bool(true),Bool(true)) -> true | _ -> false))) 
    test "check AndAlso" ((<@ true && true  @> |> (function AndAlso(Bool(true),Bool(true)) -> true | _ -> false))) 
    test "check OrElse"  ((<@ true || true  @> |> (function OrElse(Bool(true),Bool(true)) -> true | _ -> false))) 
    test "check AndAlso" ((<@ false && false @> |> (function AndAlso(Bool(false),Bool(false)) -> true | _ -> false))) 
    test "check OrElse"  ((<@ false || false @> |> (function OrElse(Bool(false),Bool(false)) -> true | _ -> false))) 
    test "check AndAlso - encoded" ((<@ true && false @> |> (function IfThenElse(Bool(true),Bool(false),Bool(false)) -> true | _ -> false))) 
    test "check OrElse - encoded" ((<@ true || false @> |> (function IfThenElse(Bool(true),Bool(true),Bool(false)) -> true | _ -> false))) 


    test "check ForIntegerRangeLoop"   (<@ for i = 1 to 10 do printf "hello" @> |> (function ForIntegerRangeLoop(v,Int32(1),Int32(10),b) -> true | _ -> false))
    test "check ForIntegerRangeLoop"   (<@ for i in 1 .. 10 do printf "hello" @> |> (function ForIntegerRangeLoop(v,Int32(1),Int32(10),b) -> true | _ -> false))
    // In this example, the types of the start and end points are not known at the point the loop
    // is typechecked. There was a bug (6064) where the transformation to a ForIntegerRangeLoop was only happening
    // when types were known
    test "check ForIntegerRangeLoop"    (<@ for i in failwith "" .. failwith "" do printf "hello" @> |> (function ForIntegerRangeLoop(v,_,_,b) -> true | _ -> false))
    // Checks that foreach over non-integer ranges should have the shape of foreach implemented in terms of GetEnumerator
    test "check ForEachInSeq"           (<@ for i in seq {for x in 0..10 -> x} do printf "hello" @> |> (function ForEachShape(_) -> true | _ -> false))
    test "check ForEachInList"          (<@ for i in "123" do printf "hello" @> |> (function ForEachShape(_) -> true | _ -> false))
    test "check ForEachInString"        (<@ for i in [1;2;3] do printf "hello" @> |> (function ForEachShape(_) -> true | _ -> false))
    // A slight non orthogonality is that all other 'for' loops go to (quite complex) the desugared form
    test "check Other Loop"   (<@ for i in 1 .. 2 .. 10 do printf "hello" @> |> (function Let(v,_,b) -> true | _ -> false))
    test "check Other Loop"   (<@ for i in 1L .. 10L do printf "hello" @> |> (function Let(v,_,b) -> true | _ -> false))

    let mutable mutableX = 1
    test "check mutableX top level set"   ((<@  mutableX  <- 10 @> |> (function PropertySet(None,pinfo,[],Int32 10) when pinfo.Name = "mutableX" -> true | _ -> false))) 
    test "check mutableX top level get"   ((<@  mutableX   @> |> (function PropertyGet(None,pinfo,[]) when pinfo.Name = "mutableX" -> true | _ -> false))) 


    let structFieldSetFromArray () = 
        <@ let mutable arr = [| S() |]
           arr.[0].x <- 3 @>

    let structFieldGetFromArray () = 
        <@ let mutable arr = [| S() |]
           arr.[0].x  @>

    test "check struct field set from array"   
     ((structFieldSetFromArray() |> 
        (function 
          | Let (varr, NewArray (_, [ DefaultValue _ ]),FieldSet (Some (Call (None, getter, [arr; Int32 0])), field, Int32 3)) -> true 
          | _ -> false))) 

    test "check struct field get from array"   
     ((structFieldGetFromArray() |> 
        (function 
          | Let (varr, NewArray (_, [ DefaultValue _ ]),FieldGet (Some (Call (None, getter, [arr; Int32 0])), field)) -> true 
          | _ -> false))) 


    test "checkIsMutable1" 
        (let e = <@@ let mutable x = 1 in if x = 1 then x <- 2 @@>

         match e with
                  |Let(v,e1,e2) -> v.IsMutable
                  |_ -> failwith "unexpected shape") 

    test "checkIsMutable2" 
        (let e = <@@ let x = 1 in if x = 1 then 2 else 3 @@>

         match e with
                  |Let(v,e1,e2) -> not v.IsMutable
                  |_ -> failwith "unexpected shape") 

    test "checkIsMutable3" 
        (let e = <@@ let f (x:int) = 1 in f 3 @@>

         match e with
                  |Let(v,e1,e2) -> not v.IsMutable
                  |_ -> failwith "unexpected shape") 

    test "checkType" 
        (let e = <@@ let mutable x = 1 in if x = 1 then x <- 2 @@>

         match e with
                  |Let(v,e1,e2) -> v.Type = typeof<int>
                  |_ -> failwith "unexpected shape") 


    type MyEnum = Foo = 0 | Bar = 1
    test "klnwce-0" 
        (match <@@ MyEnum.Foo @@> with  | Value(x,ty) when ty = typeof<MyEnum> && (x:?>MyEnum)=MyEnum.Foo -> true | _ -> false)
    test "klnwce-1" 
        (match <@@ MyEnum.Bar @@> with  | Value(x,ty) when ty = typeof<MyEnum> && (x:?>MyEnum)=MyEnum.Bar  -> true | _ -> false)
    test "klnwce-2" 
        (match <@@ System.DayOfWeek.Monday @@> with  | Value(x,ty) when ty = typeof<System.DayOfWeek> && (x:?>System.DayOfWeek)=System.DayOfWeek.Monday -> true | _ -> false)
    test "klnwce-3" 
        (<@@ System.DayOfWeek.Monday @@>.Type = typeof<System.DayOfWeek >)
    test "klnwce-4" 
        (match <@@ (fun () -> MyEnum.Bar) @@> with  | Lambda(_,Value(x,ty)) when ty = typeof<MyEnum> && (x:?>MyEnum)=MyEnum.Bar -> true | _ -> false)
        
    test "check NewArray"   (<@ [| |] :int[] @> |> (function NewArray(typ,[]) when typ = typeof<int32> -> true | _ -> false))
    test "check NewArray"   (<@ [| 1;2;3 |] @> |> (function NewArray(typ,[Int32(1);Int32(2);Int32(3)]) when typ = typeof<int32> -> true | _ -> false))
    test "check NewRecord"   (<@ { contents = 3 } @> |> (function NewRecord(typ,args) -> true | _ -> false))
    test "check NewUnion"   (<@ [] @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))
    test "check NewUnion"   (<@ [1] @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))
    test "check NewUnion"   (<@ None @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))
    test "check NewUnion"   (<@ Some(1) @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))

    test "check NewDelegate"   (<@ new System.EventHandler<System.EventArgs>(fun sender evArgs -> ()) @> |> (function NewDelegate(ty,[v1;v2],_) when v1.Name = "sender" && v2.Name = "evArgs" -> true | _ -> false))

    test "check NewTuple (2)"   (<@ (1,2) @>           |> (function NewTuple([Int32(1);Int32(2)]) -> true | _ -> false))
    test "check NewTuple (3)"   (<@ (1,2,3) @>         |> (function NewTuple([Int32(1);Int32(2);Int32(3)]) -> true | _ -> false))
    test "check NewTuple (4)"   (<@ (1,2,3,4) @>       |> (function NewTuple([Int32(1);Int32(2);Int32(3);Int32(4)]) -> true | _ -> false))
    test "check NewTuple (5)"   (<@ (1,2,3,4,5) @>     |> (function NewTuple([Int32(1);Int32(2);Int32(3);Int32(4);Int32(5)]) -> true | _ -> false))
    test "check NewTuple (6)"   (<@ (1,2,3,4,5,6) @>   |> (function NewTuple([Int32(1);Int32(2);Int32(3);Int32(4);Int32(5);Int32(6)]) -> true | _ -> false))
    test "check NewTuple (6)"   (<@ (1,2,3,4,5,6,7) @> |> (function NewTuple([Int32(1);Int32(2);Int32(3);Int32(4);Int32(5);Int32(6);Int32(7)]) -> true | _ -> false))

    test "check  Lambda"  ((<@ (fun (x:int) -> x) @>               |> (function Lambda(v,_) -> true | _ -> false))) 
    test "check  Lambda"  ((<@ (fun (x:int,y:int) -> x) @>         |> (function Lambda(v,_) -> true | _ -> false))) 
    test "check  Lambda"  ((<@ (fun (p:int*int) -> p) @>           |> (function Lambda(v,_) -> true | _ -> false))) 
    test "check  Lambda"  ((<@ (fun () -> 1) @>           |> (function Lambda(v,_) -> true | _ -> false))) 

    test "check  Lambdas" ((<@ (fun (x:int) -> x) @>               |> (function Lambdas([[v]],_) -> true | _ -> false))) 
    test "check  Lambdas" ((<@ (fun (x:int,y:int) -> x) @>         |> (function Lambdas([[v1;v2]],_) -> true | _ -> false))) 
    test "check ~Lambdas" ((<@ (fun (x:int) (y:int) -> x) @>       |> (function Lambdas([[v1;v2]],_) -> false | _ -> true))) 
    test "check  Lambdas" ((<@ (fun (x:int,y:int) (z:int) -> z) @> |> (function Lambdas([[v1;v2];[v3]],_) -> true | _ -> false))) 
    test "check  Lambdas" ((<@ (fun ((x:int,y:int),(z:int)) -> z) @> |> (function Lambdas([[v1;v2]],_) -> true | _ -> false))) 
    test "check  Lambdas" ((<@ (fun ((x:int),(y:int,z:int)) -> z) @> |> (function Lambdas([[v1;v2]],_) -> true | _ -> false))) 
    //
    //test "check  Lambdas" ((<@ (fun [(x:int)] -> x) @> |> (function Lambdas([[v1]],_) -> true | _ -> false))) 
    test "check  Lambdas" ((<@ (fun () -> 1) @> |> (function Lambdas([[v1]],_) -> true | _ -> false))) 

    test "check  Let" ((<@ let x = 1 in x @> |> (function Let(v,Int32(1),Var(v2)) when v = v2 -> true | _ -> false))) 
    test "check  Let" ((<@ let x = 1 
                           let y = 2 
                           x,y @> |> (function Let(vx,Int32(1),Let(vy,Int32(2),NewTuple([Var(vx2);Var(vy2)]))) when vx.Name = "x" && vx = vx2 && vy = vy2 -> true | _ -> false))) 

    test "check  Let" ((<@ let x = 1 
                           let x = 2 
                           x,x @> |> (function Let(vx,Int32(1),Let(vy,Int32(2),NewTuple([Var(vx2);Var(vy2)]))) when vx.Name = "x" && vy.Name = "x" && vy = vx2 && vy = vy2 -> true | _ -> false))) 

    test "check  Let" ((<@ let f () = 1 in f @> |> (function Let(v,Lambda(_,Int32(1)),Var(v2)) when v = v2 -> true | _ -> false))) 

    test "check  LetRecursive" ((<@ let rec f (x:int) : int = 1 in f @> |> (function LetRecursive([vf,Lambda(vx,Int32(1))],Var(vf2)) when vf = vf2 -> true | _ -> false))) 

    test "check  LetRecursive" ((<@ let rec f (x:int) : int = 1 
                                    and     g (x:int) = 2 
                                    (f,g) @> |> (function LetRecursive([(vf,Lambda(vx,Int32(1)));(vg,Lambda(vx2,Int32(2)))],NewTuple[Var(vf2);Var(vg2)]) when (vf = vf2 && vg = vg2)-> true | _ -> false))) 

    test "check  Application" ((<@ let f () = 1 in f () @> |> (function Let(fv1,Lambda(_,Int32(1)),Application(Var(fv2),Unit)) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Application" ((<@ let f (x:int) = 1 in f 1 @> |> (function Let(fv1,Lambda(_,Int32(1)),Application(Var(fv2),Int32(1))) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Application" ((<@ let f (x:int) (y:int) = 1 in f 1 2 @> |> (function Let(fv1,Lambda(_,Lambda(_,Int32(1))),Application(Application(Var(fv2),Int32(1)),Int32(2))) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Application" ((<@ let f (x:int,y:int) = 1 in f (1,2) @> |> (function Let(fv1,Lambdas(_,Int32(1)),Application(Var(fv2),NewTuple[Int32(1);Int32(2)])) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Applications" ((<@ let f (x:int) (y:int) = 1 in f 1 2 @> |> (function Let(fv1,Lambdas(_,Int32(1)),Applications(Var(fv2),[[Int32(1)];[Int32(2)]])) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Applications" ((<@ let f (x:int,y:int) = 1 in f (1,2) @> |> (function Let(fv1,Lambdas(_,Int32(1)),Applications(Var(fv2),[[Int32(1);Int32(2)]])) when fv1 = fv2 -> true | _ -> false))) 
    test "check  Applications" ((<@ let f () = 1 in f () @> |> (function Let(fv1,Lambdas(_,Int32(1)),Applications(Var(fv2),[[]])) when fv1 = fv2 -> true | _ -> false))) 

    test "check  pattern matching 1" 
        ((<@ function (x:int) -> x  @> 
             |> (function Lambda(argv1,Let(xv1,Var(argv2),Var(xv2))) when xv1 = xv2 && argv1 = argv2 -> true | _ -> false))) 

    test "check  incomplete pattern matching 1" 
        ((<@ function (None : int option) -> 1  @> 
             // Pipe the quotation into a matcher that checks its form
             |> (function Lambda(argv1,IfThenElse(UnionCaseTest(Var(argv2),ucase1),Int32(1),Call(None,minfo,[_])))  when argv1 = argv2 && minfo.Name = "Raise" && ucase1.Name = "None" -> true 
                        | _ -> false))) 
             
    test "check  pattern matching 2" 
        ((<@ function { contents = (x:int) } -> x  @> 
             // Pipe the quotation into a matcher that checks its form
             |> (function Lambda(argv1,Let(xv1,PropertyGet(Some(Var(argv2)),finfo,[]),Var(xv2))) when xv1 = xv2 && argv1 = argv2 -> true 
                        | _ -> false))) 

    test "check  pattern matching 3" 
        ((<@ function ([]:int list) -> 1 | _ -> 2  @> 
             // Pipe the quotation into a matcher that checks its form
             |> (function Lambda(argv1,IfThenElse(UnionCaseTest(Var(argv2),ucase),Int32(1),Int32(2))) when argv1 = argv2 -> true | _ -> false))) 

    test "check  pattern matching 4" 
        ((<@ function ([]:int list) -> 1 | h::t -> 2  @> 
             // Pipe the quotation into a matcher that checks its form
             |> (function Lambda(argv1,IfThenElse(UnionCaseTest(Var(argv2),ucaseCons),
                                                  Let(tv1,PropertyGet(Some(Var(argv3)),pinfoTail,[]),
                                                    Let(hv1,PropertyGet(Some(Var(argv4)),pinfoHead,[]),
                                                         Int32(2))),
                                                  Int32(1))) when (argv1 = argv2 && 
                                                                   argv1 = argv3 && 
                                                                   argv1 = argv4 && 
                                                                   ucaseCons.Name = "Cons" && 
                                                                   pinfoTail.Name = "Tail" && 
                                                                   pinfoTail.Name = "Tail") -> true 
                        | _ -> false))) 

    test "check  pattern matching 5" 
        ((<@ function h::t -> 2  | ([]:int list) -> 1 @> 
             |> (function Lambda(argv1,IfThenElse(UnionCaseTest(Var(argv2),ucaseEmpty),
                                                  Int32(1),
                                                  Let(tv1,PropertyGet(Some(Var(argv3)),pinfoTail,[]),
                                                    Let(hv1,PropertyGet(Some(Var(argv4)),pinfoHead,[]),
                                                         Int32(2))))) when (argv1 = argv2 && 
                                                                            argv1 = argv3 && 
                                                                            argv1 = argv4 && 
                                                                            ucaseEmpty.Name = "Empty" && 
                                                                            pinfoTail.Name = "Tail" && 
                                                                            pinfoTail.Name = "Tail") -> true 
                        | _ -> false))) 

    test "check  pattern matching 6" 
        ((<@ function [h1;(h2:int)] -> 2 | _ -> 0 @> 
             |> (function Lambda(argv1,
                                 IfThenElse(UnionCaseTest(Var(argv2),ucaseCons),
                                            IfThenElse(UnionCaseTest(PropertyGet(Some(Var(argv3)),pinfoTail,[]),ucaseCons2),
                                                       IfThenElse(UnionCaseTest(PropertyGet(Some(PropertyGet(Some(Var(argv4)),pinfoTail2,[])),pinfoTail3,[]),ucaseEmpty),
                                                                  Let(h1v1,PropertyGet(Some(Var(argv5)),pinfoHead,[]),
                                                                    Let(h2v1,PropertyGet(Some(PropertyGet(Some(Var(argv6)),pinfoTail4,[])),pinfoHead2,[]),
                                                                         Int32(2))),
                                                                  Int32(0)),
                                                       Int32(0)),
                                            Int32(0))) 
                                when (argv1 = argv2 && 
                                      argv1 = argv3 && 
                                      argv1 = argv4 && 
                                      argv1 = argv5 && 
                                      argv1 = argv6 && 
                                      h1v1.Name = "h1" && 
                                      h2v1.Name = "h2" && 
                                      ucaseEmpty.Name = "Empty" && 
                                      pinfoTail.Name = "Tail" && 
                                      pinfoTail2.Name = "Tail" && 
                                      pinfoTail3.Name = "Tail" && 
                                      pinfoTail4.Name = "Tail" && 
                                      pinfoHead.Name = "Head" && 
                                      pinfoHead2.Name = "Head") -> true
                        | _ -> false))) 

    // Check the elaborated form of a pattern match that uses an active pattern 
    let (|RefCell|) (x : int ref) = x.Value
    test "check  pattern matching 7" 
        ((<@ function RefCell(x) -> x @> 
             |> (function Lambda(argv1,
                                 Let(apv1, Call(None,minfo,[Var(argv2)]),
                                     Let(xv1, Var(apv2),
                                         Var(xv2))))
                                when (argv1 = argv2 && 
                                      xv1 = xv2  && 
                                      apv1 = apv2  && 
                                      minfo.Name = "|RefCell|") -> true
                        | _ -> false))) 

    // Check calling .NET things
    test "check  NewObject" ((<@ new System.Object() @> |> (function NewObject(_,[]) -> true | _ -> false))) 
    test "check  NewObject" ((<@ new System.String('c',3) @> |> (function NewObject(_,[Char('c');Int32(3)]) -> true | _ -> false))) 
    
    test "check  Call (static)" ((<@ System.Object.Equals("1","2") @> |> (function Call(None,_,[Coerce(String("1"),_);Coerce(String("2"),_)]) -> true | _ -> false))) 
    test "check  Call (instance)" ((<@ ("1").Equals("2") @> |> (function Call(Some(String("1")),_,[String("2")]) -> true | _ -> false))) 
    test "check  Call (instance)" ((<@ ("1").GetHashCode() @> |> (function Call(Some(String("1")),_,[]) -> true | _ -> false))) 
    test "check  PropertyGet (static)" ((<@ System.DateTime.Now @> |> (function PropertyGet(None,_,[]) -> true | _ -> false))) 
    test "check  PropertyGet (instance)" ((<@ ("1").Length @> |> (function PropertyGet(Some(String("1")),_,[]) -> true | _ -> false))) 

#if !NETCOREAPP
    test "check  PropertySet (static)" ((<@ System.Environment.ExitCode <- 1 @> |> (function PropertySet(None,_,[],Int32(1)) -> true | _ -> false))) 
#endif
    test "check  PropertySet (instance)" ((<@ ("1").Length @> |> (function PropertyGet(Some(String("1")),_,[]) -> true | _ -> false))) 

    test "check null (string)"   (<@ (null:string) @> |> (function Value(null,ty) when ty = typeof<string> -> true | _ -> false))

    let v = Expr.GlobalVar<int>("IntVar")
    test "check var (GlobalVar)"   (v |> (function Var(v2) when v2.Name = "IntVar" -> true | _ -> false))

    test "check Var"   (<@ %v @> |> (function Var(v2) when v2.Name = "IntVar"  -> true | _ -> false))
    test "check Coerce"   (<@ 3 :> obj @> |> (function Coerce(x,ty) when ty = typeof<obj> -> true | _ -> false))
    test "check Sequential"   (<@ (); () @> |> (function Sequential(Unit,Unit) -> true | _ -> false))
    test "check Sequential"   (<@ ""; () @> |> (function Sequential(Sequential(String(""),Unit),Unit) -> true | _ -> false)) (* changed for bug 3628 fix *)
    test "check Sequential"   (<@ (); "" @> |> (function Sequential(Unit,String("")) -> true | _ -> false))
    test "check Sequential"   (<@ (); (); () @> |> (function Sequential(Unit,Sequential(Unit,Unit)) -> true | _ -> false))
    test "check WhileLoop"   (<@ while true do () done @> |> (function WhileLoop(Bool(true),Unit) -> true | _ -> false))
    test "check TryFinally"   (<@ try 1 finally () @> |> (function TryFinally(Int32(1),Unit) -> true | _ -> false))

    <@ new obj() :?> int @>

    [<ReflectedDefinition>]
    let f (x:int) = 1

    [<ReflectedDefinition>]
    module M = 
        let f (x:int) = 1

    test "clewlkjncew" 
        ((<@ f 1 @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 



    test "clewlkjncewb" 
        ((<@ M.f 1 @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 

    // check failure of TryGetReflectedDefinition on non-ReflectedDefinition for locally-defined f3

    //[<ReflectedDefinition>]
    let f3 (x:int) = 1
    test "ejnwe98" 
        ((<@ f3 1 @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsNone | _ -> false)))

    [<ReflectedDefinition>]
    let rec f2 (x:int) = not (f2 x)

    // check success of TryGetReflectedDefinition on local recursive f2
    test "cwuic9en" 
              ((<@ f2 1 @> 
               
               |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 


    // test GetFreeVars
    
    test "check lambda closed"       (Seq.length ((<@ (fun (x:int)  -> 1) @>).GetFreeVars()   ) = 0)
    test "check for loop closed"         (Seq.length ((<@ for i = 1 to 10 do () done @>).GetFreeVars()) = 0)
    test "check while loop closed"       (Seq.length ((<@ while true do () done @>).GetFreeVars()) = 0)
    test "check let rec closed"          (Seq.length ((<@ let rec f (x:int) = f (f x) in f @>).GetFreeVars()) = 0)

    module AddressOfTests = 
        [<Struct>]
        type S(z : int) =
            [<DefaultValue>] val mutable x : int

        [<Struct>]
        type S2(z : int) =
            [<DefaultValue>] val mutable s : S

        test "check Struct 1"   (<@ S(1).x  @> |> (function Let(_,NewObject _, FieldGet (Some (Var _), _)) -> true | _ -> false))
        test "check Struct 2a"  (<@ (fun (s2: S2) -> s2.s.x)  @> |> (function Lambda(_,FieldGet(Some(FieldGet(Some(Var _),_)),_)) -> true | _ -> false))
        test "check Struct 2"   (<@ (fun (arr: S[]) -> arr.[0])  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "GetArray" -> true | _ -> false))
        test "check Struct 3"   (<@ (fun (arr: S[,]) -> arr.[0,0])  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "GetArray2D" -> true | _ -> false))
        test "check Struct 4"   (<@ (fun (arr: S[,,]) -> arr.[0,0,0])  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "GetArray3D" -> true | _ -> false))
        test "check Struct 5"   (<@ (fun (arr: S[,,,]) -> arr.[0,0,0,0])  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "GetArray4D" -> true | _ -> false))
        test "check Struct 2 arr"   (<@ (fun (arr: S[]) -> arr.[0].x)  @> |> (function Lambda(_,FieldGet(Some(Call(None, minfo, _)),_)) when minfo.Name = "GetArray" -> true | _ -> false))
        test "check Struct 3 arr"   (<@ (fun (arr: S[,]) -> arr.[0,0].x)  @> |> (function Lambda(_,FieldGet(Some(Call(None, minfo, _)),_)) when minfo.Name = "GetArray2D" -> true | _ -> false))
        test "check Struct 4 arr"   (<@ (fun (arr: S[,,]) -> arr.[0,0,0].x)  @> |> (function Lambda(_,FieldGet(Some(Call(None, minfo, _)),_)) when minfo.Name = "GetArray3D" -> true | _ -> false))
        test "check Struct 5 arr"   (<@ (fun (arr: S[,,,]) -> arr.[0,0,0,0].x)  @> |> (function Lambda(_,FieldGet(Some(Call(None, minfo, _)),_)) when minfo.Name = "GetArray4D" -> true | _ -> false))
        test "check Struct 6"   (<@ (fun (arr: int[]) -> arr.[0] <- 0)  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "SetArray" -> true | _ -> false))
        test "check Struct 7"   (<@ (fun (arr: int[,]) -> arr.[0,0] <- 0)  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "SetArray2D" -> true | _ -> false))
        test "check Struct 8"   (<@ (fun (arr: int[,,]) -> arr.[0,0,0] <- 0)  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "SetArray3D" -> true | _ -> false))
        test "check Struct 9"   (<@ (fun (arr: int[,,,]) -> arr.[0,0,0,0] <- 0)  @> |> (function Lambda(_,Call(None, minfo, _)) when minfo.Name = "SetArray4D" -> true | _ -> false))
        test "check Struct C"   (<@ S()  @> |> (function DefaultValue _ -> true | _ -> false))
        test "check Mutate 1"   (<@ let mutable x = 0 in x <- 1 @> |> (function Let(v,Int32 0, VarSet(v2,Int32 1)) when v = v2 -> true | _ -> false))

        let q = <@ let mutable x = 0 in x <- 1 @>
        
        let rec getMethod (e : Expr) =
            match e with
            | Call(None, mi, _) -> mi
            | Let(_,_,m) -> getMethod m
            | Lambdas(_, e) -> getMethod e
            | _ -> failwithf "not a lambda: %A" e

        let increment (r : byref<int>) = r <- r + 1
        let incrementMeth = getMethod <@ let mutable a = 10 in increment(&a) @>

        let rec rebuild (e : Expr) =
            match e with
            | ExprShape.ShapeLambda(v,b) -> Expr.Lambda(v, rebuild b)
            | ExprShape.ShapeVar(v) -> Expr.Var v
            | ExprShape.ShapeCombination(o, args) -> ExprShape.RebuildShapeCombination(o, args |> List.map rebuild)

        test "check AddressOf in call"      (try let v = Var("a", typeof<int>, true) in Expr.Let(v, Expr.Value 10, Expr.Call(incrementMeth, [Expr.AddressOf(Expr.Var v)])) |> ignore; true with _ -> false)
        test "check AddressOf rebuild"      (try rebuild <@ let mutable a = 10 in increment(&a) @> |> ignore; true with _ -> false)
        test "check AddressOf argument"     (<@ let mutable a = 10 in increment(&a) @> |> function Let(_, _, Call(None, _, [AddressOf(_)])) -> true | _ -> false)
        test "check AddressOf type"         (<@ let mutable a = 10 in increment(&a) @> |> function Let(_, _, Call(None, _, [AddressOf(_) as e])) -> (try e.Type = typeof<int>.MakeByRefType() with _ -> false) | _ -> false)


    // Test basic expression splicing
    let f8383 (x:int) (y:string) = 0
    let test2 =   
        let v = 1 in 
        let s = "2" in   
        <@ f8383 v s @>

    let f8384 (x:'a) (y:string) = 0
    let test3a =   
        let v = 1 in 
        let s = "2" in   
        <@ f8384 v s @>

    let test3b() =   
        let v = 1 in 
        let s = "2" in   
        <@ f8384 v s @>
    
    check "value splice 1" test2 <@ f8383 1 "2" @>
    check "value splice 2" test3a <@ f8384 1 "2" @>
    check "value splice 3" (test3b()) <@ f8384 1 "2" @>

    test "value splice 4"  (let v1 = 3 in let v2 = 1+2 in  <@ 1 + v1 @> = <@ 1 + v2 @>)
    test "expr splice 1"  (<@ %(<@ 1 @>) @> = <@ 1 @>)
    
    // Test basic type splicing

    let f8385 (x:'a) (y:string) = <@ (x,y) @>
    check "type splice 1" (f8385 1 "a") <@ (1,"a") @>
    check "type splice 2" (f8385 "b" "a") <@ ("b","a") @>

    test "check TryGetReflectedDefinition (local f)" ((<@ f 1 @> |> (function  Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false)))

    test "check TryGetReflectedDefinition (local recursive f2)" 
        ((<@ f2 1 @> |> (function Call(None,minfo,args)  -> Quotations.Expr.TryGetReflectedDefinition(minfo) <> None | _ -> false))) 


    test "check lambda closed"       (Seq.length ((<@ (fun (x:int)  -> 1) @>).GetFreeVars()) = 0)
    test "check for loop closed"         (Seq.length ((<@ for i = 1 to 10 do () done @>).GetFreeVars()) = 0)
    test "check while loop closed"       (Seq.length ((<@ while true do () done @>).GetFreeVars()) = 0)
    test "check let rec closed"          (Seq.length ((<@ let rec f (x:int) = f (f x) in f @>).GetFreeVars()) = 0)

    // Check we can use ReflectedDefinition on a floating point pattern match
    type T = | A of float

    
    test "check NewUnionCase"   (<@ A(1.0) @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))
    

    [<ReflectedDefinition>]
    let foo v = match v with  | A(1.0) -> 0 | _ -> 1
      
    test "check TryGetReflectedDefinition (local f)" 
        ((<@ foo (A(1.0)) @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 

    [<ReflectedDefinition>]
    let test3297327 v = match v with  | A(1.0) -> 0 | _ -> 1
      
    test "check TryGetReflectedDefinition (local f)" 
        ((<@ foo (A(1.0)) @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 

    type Foo() =
       let source = [1;2;3]
       [<ReflectedDefinition>]
       let foo() = source
       let bar() =
            let b = <@ source @>
            b
       member __.Bar = bar()
       [<ReflectedDefinition>]
       member x.Z() = source


    test "check accesses to implicit fields in ReflectedDefinitions" 
        begin
            let foo = Foo()
            match foo.Bar with
            |   FieldGet(Some (Value (v,t)), _) -> Object.ReferenceEquals(v, foo)
            |   _ -> false
        end

#if !FSHARP_CORE_31 && !TESTS_AS_APP && !NETCOREAPP
    test "check accesses to readonly fields in ReflectedDefinitions" 
        begin
            let c1 = Class1("a")
            match <@ c1.myReadonlyField @> with
            |   FieldGet(Some (ValueWithName (_, v, "c1")), field) -> (v.Name = "Class1") && (field.Name = "myReadonlyField")
            |   _ -> false
        end
#endif

end

(*
module SubstiutionTest = begin
  let tm = (<@ (fun x y -> x + y + y) @>)
  // TEST INVALID - this match fails because a variable is escaping.
  let Some(x,y,y') = Template <@. (fun x y -> _ + _ + _) .@> tm
  let Some(xyy) = Template <@. (fun (x:int) (y:int) -> _) .@> tm
  test "check free vars (tm)" (List.length (freeInExpr tm.Raw) = 0)
  test "check free vars (x)" (List.length (freeInExpr x.Raw) = 1)
  test "check free vars (y)" (List.length (freeInExpr y.Raw) = 1)
  test "check free vars (xyy)" (List.length (freeInExpr xyy.Raw) = 2)
  

  let Some xv = Var.Query(x.Raw)
  let Some body = Template <@. (fun x -> _) .@> tm
  test "check free vars (body)" (List.length (freeInExpr body.Raw) = 1)
  let body2 = substitute (fun _ -> None) (fun v -> if v = xv then (printf "Yes!\n"; Some((<@ 1 @>).Raw)) else None) body
  test "check free vars (body2)" (List.length (freeInExpr body2.Raw) = 0)
  let body3 = substitute (fun _ -> None) (fun v -> if v = xv then Some y.Raw else None) body
  test "check free vars (body3)" (List.length (freeInExpr body3.Raw) = 1)

end
*)

(*

module TomasP = begin
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns

    let ex1 = <@ 1 + 2 @>
    let ex2 = <@ 1 + 10/5 @>

    type simple_expr =
      | Int of int
      | Add of simple_expr * simple_expr
      | Sub of simple_expr * simple_expr
      | Mul of simple_expr * simple_expr
      | Div of simple_expr * simple_expr


    let what_is x =
      match x with  
        | Int32 (_) -> "number";
        | _ -> 
        match x with  
          | Application(_) -> "application";
          | _ -> 
            "something else..."
        
    // Prints "number"      
    do print_string (what_is <@ 1 @>)

    // Prints "application"         
    do print_string (what_is <@ 1 + 2 @>)

    let rec parse x =
      match x with  
        // x contains the number so we can simply return Int(x)
        | Int32 (x) -> Int(x); 
        | Applications (GenericTopDnUse <@ (+) @> tyargs,[a1;a2]) -> Add(parse a1, parse a2)
        | Applications (GenericTopDnUse <@ (-) @> tyargs,[a1;a2]) -> Sub(parse a1, parse a2)
        | Applications (GenericTopDnUse <@ ( * ) @> tyargs,[a1;a2]) -> Mul(parse a1, parse a2)
        | Applications (GenericTopDnUse <@ ( / ) @> tyargs,[a1;a2]) -> Div(parse a1, parse a2)
        | _ -> failwith "parse"

    let a = 1
    let q = <@ if (a = 0) then 1 else 2 @>
    let ex4 = 
     match q with
      | IfThenElse (cond,trueBranch,falseBranch) ->
          // cond        - 'expr' that represents condition
          // trueBranch  - 'expr' that represents the true branch
          // falseBranch - 'expr' that represents the false branch
          print_string "If-then-else statement"
          
      | _ -> 
          print_string "Something else"

    type a = | B of string
    [<ReflectedDefinition>]
    let processStuff sequence = Seq.iter (function B packet -> ()) sequence

end

module ErrorEstimateTest = 
    open Quotations
    open Quotations.Expr

    //let f x = x + 2.0*x*x
    //let t = <@ fun x -> x + 2*x*x @>

    type Error = Err of float

    let rec errorEstimateAux t (env : Map<_,_>) = 
        match t with 
        | GenericTopDnApp <@ (+) @> (tyargs,[xt;yt]) -> 
            let x,Err(xerr) = errorEstimateAux xt env
            let y,Err(yerr) = errorEstimateAux yt env
            (x+y,Err(xerr+yerr))
        | GenericTopDnApp <@ (-) @> (tyargs,[xt;yt]) -> 
            let x,Err(xerr) = errorEstimateAux xt env
            let y,Err(yerr) = errorEstimateAux yt env
            (x-y,Err(xerr+yerr))
        | GenericTopDnApp <@ ( * ) @> (tyargs,[xt;yt]) -> 
            let x,Err(xerr) = errorEstimateAux xt env
            let y,Err(yerr) = errorEstimateAux yt env
            
            (x*y,Err(xerr*abs(y)+yerr*abs(x)+xerr*yerr))

        // TBD...        
        | GenericTopDnApp <@ ( / ) @> (tyargs,[xt;yt]) ->
            let x,Err(xerr) = errorEstimateAux xt env
            let y,Err(yerr) = errorEstimateAux yt env
            // check:
            (x/y,Err(abs((y*xerr - yerr*x)/(y+yerr))))
            
        | GenericTopDnApp <@ abs @> (tyargs,[xt]) -> 
            let x,Err(xerr) = errorEstimateAux xt env
            (abs(x),Err(xerr))
        | Let((var,vet), bodyt) -> 
            let varv,verr = errorEstimateAux vet env
            errorEstimateAux bodyt (env.Add(var.Name,(varv,verr)))

        | App(ResolvedTopDnUse(info,Lambda(v,body)),arg) -> 
            errorEstimateAux  (MkLet((v,arg),body)) env
        | Var(x) -> env.[x]
        | Double(n) -> (n,Err(0.0))       
        | _ -> failwithf "unrecognized term: %A" t


    let rec errorEstimateFun (t : Expr) = 
        match t with 
        | Lambda(x,t) ->
            (fun xv -> errorEstimateAux t (Map.ofSeq [(x.Name,xv)]))
        | ResolvedTopDnUse(info,body) -> 
            errorEstimateFun body 
        | _ -> failwithf "unrecognized term: %A - expected a lambda" t



    let errorEstimate (t : Expr<float -> float>) = errorEstimateFun t.Raw 

    let rec errorEstimate2 (t : Expr<float -> float -> float>) = 
        match t.Raw with 
        | Lambdas([x;y],t) ->
            (fun xv yv -> errorEstimateAux t (Map.ofSeq [(x.Name,xv); (y.Name,yv)]))
        | _ -> failwithf "unrecognized term: %A - expected a lambda of two args" t

    let (±) x = Err(x)
    //fsi.AddPrinter (fun (x,Err(v)) -> sprintf "%g±%g" x v)

    errorEstimate <@ fun x -> x @> (1.0,±0.1)
    errorEstimate <@ fun x -> 2.0*x @> (1.0,±0.1)
    errorEstimate <@ fun x -> x*x @> (1.0,±0.1)
    errorEstimate <@ fun x -> 1.0/x @> (0.5,±0.1)

    errorEstimate <@ fun x -> let y = x + x 
                              y*y + 2.0 @> (1.0,±0.1)

    errorEstimate <@ fun x -> x+2.0*x+3.0*x*x @> (1.0,±0.1)

    errorEstimate <@ fun x -> x+2.0*x+3.0/(x*x) @> (0.3,±0.1)

    [<ReflectedDefinition>]
    let poly x = x+2.0*x+3.0/(x*x)

    errorEstimate <@ poly @> (0.3,±0.1)
    errorEstimate <@ poly @> (30271.3,±0.0001)
*)
module Test72594 =
    let effect (i:int) = ()
    let foo () = ()
    let foo1 () =         
        let i = 1 // prevent uncurring of foo1
        fun () -> ()    
    let foo2 () () = ()

    type C() =
        member this.CFoo() () = ()
    let c = C()

    test "test72594-effect"
        (<@ foo (effect 1) @> 
            |> function (Sequential
                            ((Call(None,mi1,[Value(v, t)])), 
                             (Call(None,mi2,[])))) when mi1.Name = "effect" && t = typeof<int> && v = box 1 && mi2.Name = "foo" -> true 
                      | _ -> false)

    test "test72594-no-effect"
        (<@ foo () @> |> function (Call(None,mi2,[])) when mi2.Name = "foo"-> true | _ -> false)

    test "test72594-curried"
       (<@ foo1 (effect 1) () @> 
        |> function Application(Sequential( 
                                    (Call (None, effect, [Value(v1,tInt)])),
                                    (Call (None, foo1,[]))), Value (vUnit,tUnit)) 
                                        when effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && vUnit = box () && tUnit = typeof<unit> && foo1.Name = "foo1" -> true
                    | _ -> false)


    test "test72594-curried-2nd-arg"
       (<@ foo1 () (effect 1) @> 
        |> function Application(Call(None, foo1,[]),Call (None, effect, [Value(v1,tInt)]))

                                        when effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && foo1.Name = "foo1" -> true
                    | x -> 
                        printfn "%A" x
                        false)


    test "test72594-uncurried" 
       (<@ foo2 (effect 1) () @> 
        |> function (Call(None, foo2, [Call(None,effect,[Value(v1,tInt)]);Value(vUnit,tUnit)])) 
                        when effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && vUnit = box () && tUnit = typeof<unit> && foo2.Name = "foo2" -> true
                    | _ -> false)

    test "test72594-uncurried-2nd-arg" 
       (<@ foo2 () (effect 1) @> 
        |> function (Call(None, foo2, [Value(vUnit,tUnit);Call(None,effect,[Value(v1,tInt)])])) 
                        when effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && vUnit = box () && tUnit = typeof<unit> && foo2.Name = "foo2" -> true
                    | _ -> false)

    test "test72594-member-curried"
       (<@ c.CFoo (effect 1) () @> 
        |> function Application (Application (Lambda (_,
                                                Lambda (_,
                                                    Call (Some _, cFoo, [_; _]))),
                                                Call (None, effect, [Value (v1,tInt)])), Value (vUnit,tUnit)) 
                        when cFoo.Name="CFoo" && effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && vUnit = box () && tUnit = typeof<unit> -> true
                    | _ -> false)
    test "test72594-member-curried-2nd-arg"
       (<@ c.CFoo () (effect 1) @> 
        |> function Application (Application (Lambda (_,
                                                Lambda (_,
                                                    Call (Some _, cFoo, [_; _]))),
                                                    Value (vUnit,tUnit)),
                                                Call (None, effect, [Value (v1,tInt)]))
                        when cFoo.Name="CFoo" && effect.Name = "effect" && v1 = box 1 && tInt = typeof<int> && vUnit = box () && tUnit = typeof<unit> -> true
                    | _ -> false)

module Test414894 = 

    let effect(a : int) = ()
    
    let foo() = ()

    let f () () = ()
    let f1 (x:int) () =  ()
    let f2 () (x:int) =  ()


    type X() =
        static member f1 () = ()
        static member f2 () () = () 
        static member f3 (x:int) () =  ()
        static member f4 () (x:int) = ()
        member x.f5 () = ()
        member x.f6 () () = ()
        member x.f7 (a:int) () = ()
        member x.f8 () (a:int) = ()
    
    let x = X()

    let (|IntVal|_|) expected = 
        function
        | Value(v, ty) when ty = typeof<int> && expected = unbox<int> v  -> Some() 
        | _ -> None

    let (|UnitVal|_|) = 
        function
        | Value(v, ty) when v = box () && ty = typeof<unit> -> Some() 
        | _ -> None

    let (|EffectCall|_|) = 
        function
        | Call(_, m, _) when m.Name = "effect" -> Some()
        | _ -> None

    test "test414894"
        (
            <@ foo @>
            |> function 
                | Lambda(_, Call(None, mFoo, [])) when mFoo.Name = "foo" -> true
                | _ -> false
        )
    test "Test414894-2curried-unit-args-1-1"
        (
            <@ f @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f" -> true 
                | _ -> false
        )
    test "Test414894-2curried-args-1-2"
        (
            <@ f () @>
            |> function 
                | Let(_, UnitVal, Lambda(_, Call(_, mi, args))) when mi.Name = "f" -> true
                | _ -> false
        )
    test "Test414894-2curried-args-1-3"
        (
            <@ f (effect 1) @>
            |> function 
                | Let(_, EffectCall, Lambda(_, Call(_, mi, args))) when mi.Name = "f" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-2-1"
        (
            <@ f1 @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f1" -> true 
                | _ -> false
        )
    test "Test414894-2curried-args-2-2"
        (
            <@ f1 1 @>
            |> function 
                | Let(_, IntVal 1, Lambda(_, Call(_, mi, args))) when mi.Name = "f1" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-3-1"
        (
            <@ f2 @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f2" -> true 
                | _ -> false
        )
    test "Test414894-2curried-args-3-2"
        (
            <@ f2 () @>
            |> function 
                | Let(_, UnitVal, Lambda(_, Call(_, mi, args))) when mi.Name = "f2" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-3-3"
        (
            <@ f2 (effect 1) @>
            |> function 
                | Let(_, EffectCall, Lambda(_, Call(_, mi, args))) when mi.Name = "f2" -> true
                | _ -> false
        )
    
    test "Test414894-2curried-args-static-member-1-1"
        (
            <@ X.f1 @>
            |> function 
                | Lambda(_, Call(None, mFoo, [])) when mFoo.Name = "f1" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-2-1"
        (
            <@ X.f2  @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f2" -> true 
                | _ -> false
        )
    test "Test414894-2curried-args-static-member-2-2"
        (
            <@ X.f2 ()  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(None, mi, _))), UnitVal) when mi.Name = "f2" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-2-3"
        (
            <@ X.f2 (effect 1)  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(None, mi, _))), EffectCall) when mi.Name = "f2" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-3-1"
        (
            <@ X.f3  @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f3" -> true 
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-3-2"
        (
            <@ X.f3 5  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(None, mi, _))), IntVal 5) when mi.Name = "f3" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-4-1"
        (
            <@ X.f4  @>
            |> function 
                | Lambda(_, Lambda(_, Call(_, mi, args))) when mi.Name = "f4" -> true 
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-4-2"
        (
            <@ X.f4 ()  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(None, mi, _))), UnitVal) when mi.Name = "f4" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-static-member-4-3"
        (
            <@ X.f4 (effect 1)  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(None, mi, _))), EffectCall) when mi.Name = "f4" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-5-1"
        (
            <@ x.f5 @>
            |> function 
                | Lambda(_, Call(Some _, mFoo, [])) when mFoo.Name = "f5" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-6-1"
        (
            <@ x.f6  @>
            |> function 
                | Lambda(_, Lambda(_, Call(Some _, mi, args))) when mi.Name = "f6" -> true 
                | _ -> false
        )
    test "Test414894-2curried-args-instance-member-6-2"
        (
            <@ x.f6 ()  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(Some _, mi, _))), UnitVal) when mi.Name = "f6" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-6-3"
        (
            <@ x.f6 (effect 1)  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(Some _, mi, _))), EffectCall) when mi.Name = "f6" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-7-1"
        (
            <@ x.f7  @>
            |> function 
                | Lambda(_, Lambda(_, Call(Some _, mi, args))) when mi.Name = "f7" -> true 
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-7-2"
        (
            <@ x.f7 5  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(Some _, mi, _))), IntVal 5) when mi.Name = "f7" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-8-1"
        (
            <@ x.f8  @>
            |> function 
                | Lambda(_, Lambda(_, Call(Some _, mi, args))) when mi.Name = "f8" -> true 
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-8-2"
        (
            <@ x.f8 ()  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(Some _, mi, _))), UnitVal) when mi.Name = "f8" -> true
                | _ -> false
        )

    test "Test414894-2curried-args-instance-member-8-3"
        (
            <@ x.f8 (effect 1)  @>
            |> function 
                | Application(Lambda(_, Lambda(_, Call(Some _, mi, _))), EffectCall) when mi.Name = "f8" -> true
                | _ -> false
        )

module MoreTests = 


    open Microsoft.FSharp.Quotations

    module OneModule =
        let ModuleFunctionNoArgs() = 1
        let ModuleFunctionOneArg(x:int) = 1
        let ModuleFunctionOneUnitArg(x:unit) = 1
        let ModuleFunctionOneTupledArg(x:int*int) = 1
        let ModuleFunctionTwoArgs(x:int,y:int) = 1
    
    
    type ClassOneArg(a:int) = 
        static member TestStaticMethodOneTupledArg(x:int*int) = 1
        static member TestStaticMethodOneArg(x:int) = x
        static member TestStaticMethodNoArgs() = 1
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        static member TestStaticProp = 3
        member c.TestInstanceProp = 3
        member c.TestInstanceIndexProp with get(idx:int) = 3
        member c.TestInstanceSettableIndexProp with set (idx:int) (v:int) = ()
        member c.TestInstanceSettableIndexProp2 with set (idx1:int, idx2:int) (v:int) = ()
        member c.TestInstanceMethodNoArgs() = 1
        member c.TestInstanceMethodOneArg(x:int) = x
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y

        member this.GetterIndexer
            with get (x:int) = 1

        member this.TupleGetterIndexer
            with get (x:int*int) = 1

        member this.Item
            with get (x:int) = 1
                
        member this.TupleSetterIndexer
            with get (x : int*int) = 1

        member this.SetterIndexer
            with set (x:int) (y:int) = ()

        member this.Item
            with set (x:int) (y:int) = ()
            
        member this.Setter
            with set (x : int) = ()
                
        member this.TupleSetter
            with set (x : int*int) = ()


    type ClassNoArg() = 
        static member TestStaticMethodOneTupledArg(x:int*int) = x
        static member TestStaticMethodOneArg(x:int) = x
        static member TestStaticMethodNoArgs() = 1
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        static member TestStaticProp = 3
        static member TestStaticSettableProp with set (v:int) = ()
        static member TestStaticSettableIndexProp with set (idx:int) (v:int) = ()
        member c.TestInstanceProp = 3
        member c.TestInstanceIndexProp with get(idx:int) = 3
        member c.TestInstanceSettableIndexProp with set (idx:int) (v:int) = ()
        member c.TestInstanceMethodNoArgs() = 1
        member c.TestInstanceMethodOneArg(x:int) = x
        member c.TestInstanceMethodOneTupledArg(x:int*int) = 1
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y

    type GenericClassNoArg<'a>() = 
        static member TestStaticMethodOneTupledArg(x:int*int) = x
        static member TestStaticMethodOneArg(x:int) = x
        static member TestStaticMethodNoArgs() = 1
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        static member TestStaticProp = 3
        member c.TestInstanceProp = 3
        member c.TestInstanceIndexProp with get(idx:int) = 3
        member c.TestInstanceSettableIndexProp with set (idx:int) (v:int) = ()
        member c.TestInstanceMethodNoArgs() = 1
        member c.TestInstanceMethodOneArg(x:int) = x
        member c.TestInstanceMethodOneTupledArg(x:int*int) = 1
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y


    let isMeth (inp : Expr<_>) = match inp with Call _ -> true | _ -> false
    let isPropGet (inp : Expr<_>) = match inp with PropertyGet _ -> true | _ -> false
    let isPropSet (inp : Expr<_>) = match inp with PropertySet _ -> true | _ -> false

    //printfn "res = %b" (isPropGet <@ ClassOneArg.TestStaticProp @>)
    test "test3931a" (isMeth <@ OneModule.ModuleFunctionNoArgs() @>)
    test "test3931a" (isMeth <@ OneModule.ModuleFunctionOneArg(1) @>)
    test "test3931a" (isMeth <@ OneModule.ModuleFunctionOneUnitArg() @>)
    test "test3931a" (isMeth <@ OneModule.ModuleFunctionTwoArgs(1,2) @>)
    test "test3931a" (isMeth <@ OneModule.ModuleFunctionOneTupledArg(1,2) @>)
    let p = (1,2)
    // This case doesn't match because F# performs type-base arity analysis for module 'let' bindings
    // and we see this untupling here.
    // Thus this is elaborated into 'let v = p in let v1 = p#1 in let v2 = p#2 in f(v1,v2)'
    // test "test3931a" (isMeth <@ OneModule.ModuleFunctionOneTupledArg(p) @>)
         
    //printfn "res = %b" (isPropGet <@ ClassOneArg.TestStaticProp @>)
    test "test3932a" (isMeth <@ ClassOneArg.TestStaticMethodOneArg(3) @>)
    test "test3932f" (isMeth <@ ClassOneArg.TestStaticMethodNoArgs() @>)
    test "test3932g" (isMeth <@ ClassOneArg.TestStaticMethodTwoArgs(3,4) @>)

    test "test3932qA" (isPropGet <@ ClassOneArg(3).TestInstanceProp @>)
    test "test3932qB" (isPropGet <@ ClassOneArg(3).TestInstanceIndexProp(4) @>)
    test "test3932qC" (isPropSet <@ ClassOneArg(3).TestInstanceSettableIndexProp(4) <- 5 @>)
    test "test3932qD" (isPropSet <@ ClassOneArg(3).TestInstanceSettableIndexProp2(4,5) <- 6 @>)
    test "test3932q77" (match <@ ClassOneArg(3).TestInstanceSettableIndexProp2(4,5) <- 6 @> with 
                        | PropertySet(Some _, _, [Int32(4); Int32(5)], Int32(6)) -> true 
                        | _ -> false)

    test "test3932wA" (isMeth <@ ClassOneArg(3).TestInstanceMethodNoArgs() @>)
    test "test3932wB" (isMeth <@ ClassOneArg(3).TestInstanceMethodOneArg(3) @>)
    test "test3932e" (isMeth <@ ClassOneArg(3).TestInstanceMethodTwoArgs(3,4) @>)

    test "test3932q1" (isPropSet <@ ClassOneArg(3).Setter <- 3 @>)
    test "test3932q2" (isPropGet <@ ClassOneArg(3).GetterIndexer(3) @>)
    test "test3932q3" (isPropGet <@ ClassOneArg(3).[3] @>)
    test "test3932q4" (isPropGet <@ ClassOneArg(3).TupleGetterIndexer((3,4)) @>)
    test "test3932q5" (isPropSet <@ ClassOneArg(3).SetterIndexer(3) <- 3 @>)
    test "test3932q61" (isPropSet <@ ClassOneArg(3).[3] <- 3 @>)
    test "test3932q62" (match <@ ClassOneArg(3).[4] <- 5 @> with PropertySet(Some _,_, [Int32(4)], Int32(5)) -> true | _ -> false)
    test "test3932q7" (isPropSet <@ ClassOneArg(3).TupleSetter <- (3,4) @>)


    test "test3932" (isPropGet <@ ClassNoArg.TestStaticProp @>)
    test "test3932" (isPropSet <@ ClassNoArg.TestStaticSettableProp <- 3 @>)
    
    printfn "res = %A" <@ ClassNoArg.TestStaticSettableProp <- 5 @> 
    test "test3932q63" (match <@ ClassNoArg.TestStaticSettableProp <- 5 @> with PropertySet(None, _, [], Int32(5)) -> true | _ -> false)
    test "test3932q64" (match <@ ClassNoArg.TestStaticSettableIndexProp(4) <- 5 @> with PropertySet(None, _, [Int32(4)], Int32(5)) -> true | _ -> false)
    test "test3932r" (isMeth <@ ClassNoArg.TestStaticMethodOneArg(3) @>)
    test "test3932r" (isMeth <@ ClassNoArg.TestStaticMethodOneTupledArg((3,2)) @>)
    test "test3932r" (isMeth <@ ClassNoArg.TestStaticMethodOneTupledArg(p) @>)
    test "test3932t" (isMeth <@ ClassNoArg.TestStaticMethodNoArgs() @>)
    test "test3932y" (isMeth <@ ClassNoArg.TestStaticMethodTwoArgs(3,4) @>)

    test "test3932u" (isPropGet <@ ClassNoArg().TestInstanceProp @>)
    test "test3932u" (isPropGet <@ ClassNoArg().TestInstanceIndexProp(4) @>)
    test "test3932q65" (match <@ ClassNoArg().TestInstanceIndexProp(4) @> with PropertyGet(Some _, _, [(Int32(4))]) -> true | _ -> false)
    test "test3932u" (isPropSet <@ ClassNoArg().TestInstanceSettableIndexProp(4) <- 5 @>)
    test "test3932q66" (match <@ ClassNoArg().TestInstanceSettableIndexProp(4) <- 5 @> with PropertySet(Some _, _, [(Int32(4))], Int32(5)) -> true | _ -> false)
    test "test3932i" (isMeth <@ ClassNoArg().TestInstanceMethodNoArgs() @>)
    test "test3932i" (isMeth <@ ClassNoArg().TestInstanceMethodOneArg(3) @>)
    test "test3932i" (isMeth <@ ClassNoArg().TestInstanceMethodOneTupledArg((3,4)) @>)
    test "test3932i" (isMeth <@ ClassNoArg().TestInstanceMethodOneTupledArg(p) @>)
    test "test3932o" (isMeth <@ ClassNoArg().TestInstanceMethodTwoArgs(3,4) @>)

    test "test3932" (isPropGet <@ ClassNoArg.TestStaticProp @>)
    test "test3932rg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodOneArg(3) @>)
    test "test3932rg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodOneTupledArg((3,4)) @>)
    test "test3932rg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodOneTupledArg(p) @>)
    test "test3932tg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodNoArgs() @>)
    test "test3932yg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodTwoArgs(3,4) @>)

    test "test3932ug" (isPropGet <@ (GenericClassNoArg<int>()).TestInstanceProp @>)
    test "test3932ug" (isPropGet <@ (GenericClassNoArg<int>()).TestInstanceIndexProp(4) @>)
    test "test3932ug" (match <@ (GenericClassNoArg<int>()).TestInstanceIndexProp(4) @> with PropertyGet(Some _, _, [Int32(4)]) -> true | _ -> false)

    test "test3932ig" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodNoArgs() @>)
    test "test3932ig" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodOneArg(3) @>)
    test "test3932ig" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodOneTupledArg((3,4)) @>)
    test "test3932ig" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodOneTupledArg(p) @>)
    test "test3932og" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodTwoArgs(3,4) @>)

// Checks we can use ResolveMethodDn on methods marked with ReflectedDefinition attribute
module CheckRlectedMembers = 
    


    open Microsoft.FSharp.Quotations

    type ClassOneArg(a:int) = 
        [<ReflectedDefinition>]
        new () = ClassOneArg(3)
        [<ReflectedDefinition>]
        static member TestStaticMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        static member TestStaticMethodNoArgs() = 1
        [<ReflectedDefinition>]
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        [<ReflectedDefinition>]
        static member TestStaticProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y


        [<ReflectedDefinition>]
        member this.GetterIndexer
            with get (x:int) = 1

        [<ReflectedDefinition>]
        member this.TupleGetterIndexer
            with get (x:int*int) = 1

        [<ReflectedDefinition>]
        member this.Item
            with get (x:int) = 1
                
        [<ReflectedDefinition>]
        member this.TupleSetterIndexer
            with get (x : int*int) = 1

        [<ReflectedDefinition>]
        member this.SetterIndexer
            with set (x:int) (y:int) = ()

        [<ReflectedDefinition>]
        member this.Item
            with set (x:int) (y:int) = ()
            
        [<ReflectedDefinition>]
        member this.Setter
            with set (x : int) = ()
                
        [<ReflectedDefinition>]
        member this.TupleSetter
            with set (x : int*int) = ()


    [<ReflectedDefinition>]
    type ClassOneArgOuterAttribute(a:int) = 
        new () = ClassOneArgOuterAttribute(3)
        static member TestStaticMethodOneArg(x:int) = x
        static member TestStaticMethodNoArgs() = 1
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        static member TestStaticProp = 3
        member c.TestInstanceProp = 3
        member c.TestInstanceMethodOneArg(x:int) = x
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y


        member this.GetterIndexer
            with get (x:int) = 1

        member this.TupleGetterIndexer
            with get (x:int*int) = 1

        member this.Item
            with get (x:int) = 1
                
        member this.TupleSetterIndexer
            with get (x : int*int) = 1

        member this.SetterIndexer
            with set (x:int) (y:int) = ()

        member this.Item
            with set (x:int) (y:int) = ()
            
        member this.Setter
            with set (x : int) = ()
                
        member this.TupleSetter
            with set (x : int*int) = ()

    type ClassNoArg() = 
        [<ReflectedDefinition>]
        static member TestStaticMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        static member TestStaticMethodNoArgs() = 1
        [<ReflectedDefinition>]
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        [<ReflectedDefinition>]
        static member TestStaticProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y

    type GenericClassNoArg<'a>() = 
        [<ReflectedDefinition>]
        new (x:'a) = GenericClassNoArg<_>()
        [<ReflectedDefinition>]
        static member TestStaticMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        static member TestStaticMethodNoArgs() = 1
        [<ReflectedDefinition>]
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        [<ReflectedDefinition>]
        static member TestStaticProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceProp = 3
        [<ReflectedDefinition>]
        member c.TestInstanceMethodOneArg(x:int) = x
        [<ReflectedDefinition>]
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y

    type ClassOneArgWithOverrideID(a:int) = 
        [<ReflectedDefinition; >]
        static member TestStaticMethodOneArg(x:int) = x
        [<ReflectedDefinition; >]
        static member TestStaticMethodNoArgs() = 1
        [<ReflectedDefinition; >]
        static member TestStaticMethodTwoArgs(x:int,y:int) = x+y
        [<ReflectedDefinition; >]
        static member TestStaticProp = 3
        [<ReflectedDefinition; >]
        member c.TestInstanceProp = 3
        [<ReflectedDefinition; >]
        member c.TestInstanceMethodOneArg(x:int) = x
        [<ReflectedDefinition; >]
        member c.TestInstanceMethodTwoArgs(x:int,y:int) = x + y


    let isNewObj (inp : Expr<_>) = match inp with NewObject (ci,_) -> Expr.TryGetReflectedDefinition(ci).IsSome | _ -> false
    let isMeth (inp : Expr<_>) = match inp with Call (_,mi,_) -> Expr.TryGetReflectedDefinition(mi).IsSome | _ -> false
    let isPropGet (inp : Expr<_>) = match inp with PropertyGet (_,mi,_) -> Expr.TryGetReflectedDefinition(mi.GetGetMethod(true)).IsSome | _ -> false
    let isPropSet (inp : Expr<_>) = match inp with PropertySet (_,mi,_,_) -> Expr.TryGetReflectedDefinition(mi.GetSetMethod(true)).IsSome | _ -> false
         
    //printfn "res = %b" (isPropGet <@ ClassOneArg.TestStaticProp @>)
    // Note: there is a ReflectedDefinition on this constructor
    test "testReflect39320a" (isNewObj <@ new ClassOneArg() @>)
    test "testReflect39320ax" (isNewObj <@ new ClassOneArgOuterAttribute() @>)
    // Note: no ReflectedDefinition on this constructor
    test "testReflect39320b" (not (isNewObj <@ new ClassOneArg(3) @>))
    // Note: no ReflectedDefinition on this constructor
    test "testReflect39320c" (not (isNewObj <@ new GenericClassNoArg<int>() @>))
    // Note: there is a ReflectedDefinition on this constructor
    test "testReflect39320d" (isNewObj <@ new GenericClassNoArg<_>(3) @>)
    test "testReflect3932a" (isMeth <@ ClassOneArg.TestStaticMethodOneArg(3) @>)
    test "testReflect3932f" (isMeth <@ ClassOneArg.TestStaticMethodNoArgs() @>)
    test "testReflect3932g" (isMeth <@ ClassOneArg.TestStaticMethodTwoArgs(3,4) @>)

    test "testReflect3932q" (isPropGet <@ ClassOneArg(3).TestInstanceProp @>)
    test "testReflect3932w" (isMeth <@ ClassOneArg(3).TestInstanceMethodOneArg(3) @>)
    test "testReflect3932e" (isMeth <@ ClassOneArg(3).TestInstanceMethodTwoArgs(3,4) @>)

    test "testReflect3932ax" (isMeth <@ ClassOneArgOuterAttribute.TestStaticMethodOneArg(3) @>)
    test "testReflect3932fx" (isMeth <@ ClassOneArgOuterAttribute.TestStaticMethodNoArgs() @>)
    test "testReflect3932gx" (isMeth <@ ClassOneArgOuterAttribute.TestStaticMethodTwoArgs(3,4) @>)
    test "testReflect3932qx" (isPropGet <@ ClassOneArgOuterAttribute(3).TestInstanceProp @>)
    test "testReflect3932wx" (isMeth <@ ClassOneArgOuterAttribute(3).TestInstanceMethodOneArg(3) @>)
    test "testReflect3932ex" (isMeth <@ ClassOneArgOuterAttribute(3).TestInstanceMethodTwoArgs(3,4) @>)

    test "testReflect3932" (isPropGet <@ ClassNoArg.TestStaticProp @>)
    test "testReflect3932r" (isMeth <@ ClassNoArg.TestStaticMethodOneArg(3) @>)
    test "testReflect3932t" (isMeth <@ ClassNoArg.TestStaticMethodNoArgs() @>)
    test "testReflect3932y" (isMeth <@ ClassNoArg.TestStaticMethodTwoArgs(3,4) @>)

    test "testReflect3932u" (isPropGet <@ ClassNoArg().TestInstanceProp @>)
    test "testReflect3932i" (isMeth <@ ClassNoArg().TestInstanceMethodOneArg(3) @>)
    test "testReflect3932o" (isMeth <@ ClassNoArg().TestInstanceMethodTwoArgs(3,4) @>)

    test "testReflect3932q1" (isPropSet <@ ClassOneArg(3).Setter <- 3 @>)
    test "testReflect3932q2" (isPropGet <@ ClassOneArg(3).GetterIndexer(3) @>)
    test "testReflect3932q3" (isPropGet <@ ClassOneArg(3).[3] @>)
    test "testReflect3932q4" (isPropGet <@ ClassOneArg(3).TupleGetterIndexer((3,4)) @>)
    test "testReflect3932q5" (isPropSet <@ ClassOneArg(3).SetterIndexer(3) <- 3 @>)
    test "testReflect3932q6" (isPropSet <@ ClassOneArg(3).[3] <- 3 @>)
    test "testReflect3932q7" (isPropSet <@ ClassOneArg(3).TupleSetter <- (3,4) @>)

    test "testReflect3932q1x" (isPropSet <@ ClassOneArgOuterAttribute(3).Setter <- 3 @>)
    test "testReflect3932q2x" (isPropGet <@ ClassOneArgOuterAttribute(3).GetterIndexer(3) @>)
    test "testReflect3932q3x" (isPropGet <@ ClassOneArgOuterAttribute(3).[3] @>)
    test "testReflect3932q4x" (isPropGet <@ ClassOneArgOuterAttribute(3).TupleGetterIndexer((3,4)) @>)
    test "testReflect3932q5x" (isPropSet <@ ClassOneArgOuterAttribute(3).SetterIndexer(3) <- 3 @>)
    test "testReflect3932q6x" (isPropSet <@ ClassOneArgOuterAttribute(3).[3] <- 3 @>)
    test "testReflect3932q7x" (isPropSet <@ ClassOneArgOuterAttribute(3).TupleSetter <- (3,4) @>)

    test "testReflect3932rg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodOneArg(3) @>)
    test "testReflect3932tg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodNoArgs() @>)
    test "testReflect3932yg" (isMeth <@ GenericClassNoArg<int>.TestStaticMethodTwoArgs(3,4) @>)

    test "testReflect3932ug" (isPropGet <@ (GenericClassNoArg<int>()).TestInstanceProp @>)
    test "testReflect3932ig" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodOneArg(3) @>)
    test "testReflect3932og" (isMeth <@ (GenericClassNoArg<int>()).TestInstanceMethodTwoArgs(3,4) @>)

    test "testReflect3932a" (isMeth <@ ClassOneArgWithOverrideID.TestStaticMethodOneArg(3) @>)
    test "testReflect3932f" (isMeth <@ ClassOneArgWithOverrideID.TestStaticMethodNoArgs() @>)
    test "testReflect3932g" (isMeth <@ ClassOneArgWithOverrideID.TestStaticMethodTwoArgs(3,4) @>)

    test "testReflect3932q" (isPropGet <@ ClassOneArgWithOverrideID(3).TestInstanceProp @>)
    test "testReflect3932w" (isMeth <@ ClassOneArgWithOverrideID(3).TestInstanceMethodOneArg(3) @>)
    test "testReflect3932e" (isMeth <@ ClassOneArgWithOverrideID(3).TestInstanceMethodTwoArgs(3,4) @>)


module Bug959_Regression = begin
    open Microsoft.FSharp
    open Microsoft.FSharp

    //let f x  = <@ _ @> (lift x)

    <@
      match 1.0,"b" with
      | 1.0, "a" ->
        ""
      | 2.0, "b" ->
        ""
      | _ -> "nada" @>
end

module MoreQuotationsTests = 

    let t1 = <@@ try 1 with e when true -> 2 | e -> 3 @@>
    checkStrings "vwjnkwve0-vwnio" 
        (sprintf "%A" t1)
        """TryWith (Value (1), matchValue,
          IfThenElse (Let (e, matchValue, Value (true)),
                      Let (e, matchValue, Value (1)),
                      Let (e, matchValue, Value (1))), matchValue,
          IfThenElse (Let (e, matchValue, Value (true)),
                      Let (e, matchValue, Value (2)),
                      Let (e, matchValue, Value (3))))"""

    [<ReflectedDefinition>]
    let k (x:int) =
       try 1 with _ when true -> 2 | e -> 3

    let t2 = <@@ Map.empty.[0] @@>
    checkStrings "vwjnkwve0-vwnio1" 
       (sprintf "%A" t2) 
       "PropertyGet (Some (Call (None, Empty, [])), Item, [Value (0)])"


    let t4 = <@@ use a = new System.IO.StreamWriter(System.IO.Stream.Null) in a @@>
    checkStrings "vwjnkwve0-vwnio3" 
        (sprintf "%A" t4) 
        "Let (a, NewObject (StreamWriter, FieldGet (None, Null)),
        TryFinally (a,
                    IfThenElse (TypeTest (IDisposable, Coerce (a, Object)),
                                Call (Some (Call (None, UnboxGeneric,
                                                [Coerce (a, Object)])), Dispose,
                                    []), Value (<null>))))"

    checkStrings "vwjnkwve0-vwnio3fuull" 
        (t4.ToString(true))
        "Let (a,
        NewObject (Void .ctor(System.IO.Stream),
                FieldGet (None, System.IO.Stream Null)),
        TryFinally (a,
                    IfThenElse (TypeTest (System.IDisposable,
                                        Coerce (a, System.Object)),
                                Call (Some (Call (None,
                                                System.IDisposable UnboxGeneric[IDisposable](System.Object),
                                                [Coerce (a, System.Object)])),
                                    Void Dispose(), []), Value (<null>))))"


    let t5 = <@@ try failwith "test" with _ when true -> 0 @@>
    checkStrings "vwekwvel5" (sprintf "%A" t5) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
         IfThenElse (Value (true), Value (1), Value (0)), matchValue,
         IfThenElse (Value (true), Value (0), Call (None, Reraise, [])))"""
    
    let t6 = <@@ let mutable a = 0 in a <- 2 @@>

    checkStrings "vwewvwewe6" (sprintf "%A" t6) 
        """Let (a, Value (0), VarSet (a, Value (2)))"""

    let f (x: _ byref) = x

    let t7 = <@@ let mutable a = 0 in f (&a) @@>
    checkStrings "vwewvwewe7" (sprintf "%A" t7) 
        """Let (a, Value (0), Call (None, f, [AddressOf (a)]))"""

    let t8 = <@@ for i in 1s .. 10s do printfn "%A" i @@>
    checkStrings "vwewvwewe8" (sprintf "%A" t8) 
        """Let (inputSequence, Call (None, op_Range, [Value (1s), Value (10s)]),
        Let (enumerator, Call (Some (inputSequence), GetEnumerator, []),
             TryFinally (WhileLoop (Call (Some (enumerator), MoveNext, []),
                                    Let (i,
                                         PropertyGet (Some (enumerator), Current,
                                                      []),
                                         Application (Let (clo1,
                                                           Call (None,
                                                                 PrintFormatLine,
                                                                 [Coerce (NewObject (PrintfFormat`5,
                                                                                     Value ("%A")),
                                                                          PrintfFormat`4)]),
                                                           Lambda (arg10,
                                                                   Application (clo1,
                                                                                arg10))),
                                                      i))),
                         IfThenElse (TypeTest (IDisposable,
                                               Coerce (enumerator, Object)),
                                     Call (Some (Call (None, UnboxGeneric,
                                                       [Coerce (enumerator, Object)])),
                                           Dispose, []), Value (<null>)))))"""

    let t9() = <@@ try failwith "test" with Failure _ -> 0  @@>
    checkStrings "vwewvwewe9" (sprintf "%A" (t9())) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        Let (activePatternResult1557, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1557, Some),
                         Value (1), Value (0))), matchValue,
        Let (activePatternResult1558, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1558, Some),
                         Value (0), Call (None, Reraise, []))))"""

    let t9b = <@@ Failure "fil" @@>
    checkStrings "vwewvwewe9b" (sprintf "%A" t9b) 
        """Call (None, Failure, [Value ("fil")])"""

    let t9c = <@@ match Failure "fil" with Failure msg -> msg |  _ -> "no" @@>
    checkStrings "vwewvwewe9c" (sprintf "%A" t9c) 
        """Let (matchValue, Call (None, Failure, [Value ("fil")]),
        Let (activePatternResult1564, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1564, Some),
                         Let (msg,
                              PropertyGet (Some (activePatternResult1564), Value,
                                           []), msg), Value ("no"))))"""

    let t10 = <@@ try failwith "test" with Failure _ -> 0 |  _ -> 1 @@>
    checkStrings "vwewvwewe10" (sprintf "%A" t10) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        Let (activePatternResult1565, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1565, Some),
                         Value (1), Value (1))), matchValue,
        Let (activePatternResult1566, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1566, Some),
                         Value (0), Value (1))))"""

    let t11 = <@@ try failwith "test" with :? System.NullReferenceException -> 0 @@>
    checkStrings "vwewvwewe11" (sprintf "%A" t11) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        IfThenElse (TypeTest (NullReferenceException, matchValue), Value (1),
                    Value (0)), matchValue,
        IfThenElse (TypeTest (NullReferenceException, matchValue), Value (0),
                    Call (None, Reraise, [])))"""

    let t12 = <@@ try failwith "test" with :? System.NullReferenceException as n -> 0 @@>
    checkStrings "vwewvwewe12" (sprintf "%A" t12) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        IfThenElse (TypeTest (NullReferenceException, matchValue),
                    Let (n, Call (None, UnboxGeneric, [matchValue]), Value (1)),
                    Value (0)), matchValue,
        IfThenElse (TypeTest (NullReferenceException, matchValue),
                    Let (n, Call (None, UnboxGeneric, [matchValue]), Value (0)),
                    Call (None, Reraise, [])))"""

    let t13 = <@@ try failwith "test" with Failure _ -> 1 | :? System.NullReferenceException as n -> 0 @@>
    checkStrings "vwewvwewe13" (sprintf "%A" t13) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        Let (activePatternResult1576, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1576, Some),
                         Value (1),
                         IfThenElse (TypeTest (NullReferenceException,
                                               matchValue),
                                     Let (n,
                                          Call (None, UnboxGeneric,
                                                [matchValue]), Value (1)),
                                     Value (0)))), matchValue,
        Let (activePatternResult1577, Call (None, FailurePattern, [matchValue]),
             IfThenElse (UnionCaseTest (activePatternResult1577, Some),
                         Value (1),
                         IfThenElse (TypeTest (NullReferenceException,
                                               matchValue),
                                     Let (n,
                                          Call (None, UnboxGeneric,
                                                [matchValue]), Value (0)),
                                     Call (None, Reraise, [])))))"""

    let t14 = <@@ try failwith "test" with _ when true -> 0 @@>
    checkStrings "vwewvwewe13" (sprintf "%A" t14) 
        """TryWith (Call (None, FailWith, [Value ("test")]), matchValue,
        IfThenElse (Value (true), Value (1), Value (0)), matchValue,
        IfThenElse (Value (true), Value (0), Call (None, Reraise, [])))"""

    let _ = <@@ let x : int option = None in x.IsSome @@> |> checkQuoteString "fqekhec1" """Let (x, NewUnionCase (None), Call (None, get_IsSome, [x]))"""
    let _ = <@@ let x : int option = None in x.IsNone @@> |> checkQuoteString "fqekhec2" """Let (x, NewUnionCase (None), Call (None, get_IsNone, [x]))"""
    let _ = <@@ let x : int option = None in x.Value @@> |> checkQuoteString "fqekhec3" """Let (x, NewUnionCase (None), PropertyGet (Some (x), Value, []))"""
    let _ = <@@ let x : int option = None in x.ToString() @@> |> checkQuoteString "fqekhec4" """Let (x, NewUnionCase (None), Call (Some (x), ToString, []))"""

    module Extensions = 
        type System.Object with 
            member x.ExtensionMethod0()  = 3
            member x.ExtensionMethod1()  = ()
            member x.ExtensionMethod2(y:int)  = y
            member x.ExtensionMethod3(y:int)  = ()
            member x.ExtensionMethod4(y:int,z:int)  = y + z
            member x.ExtensionMethod5(y:(int*int))  = y 
            member x.ExtensionProperty1 = 3
            member x.ExtensionProperty2 with get() = 3
            member x.ExtensionProperty3 with set(v:int) = ()
            member x.ExtensionIndexer1 with get(idx:int) = idx
            member x.ExtensionIndexer2 with set(idx:int) (v:int) = ()

        type System.Int32 with 
            member x.Int32ExtensionMethod0()  = 3
            member x.Int32ExtensionMethod1()  = ()
            member x.Int32ExtensionMethod2(y:int)  = y
            member x.Int32ExtensionMethod3(y:int)  = ()
            member x.Int32ExtensionMethod4(y:int,z:int)  = y + z
            member x.Int32ExtensionMethod5(y:(int*int))  = y 
            member x.Int32ExtensionProperty1 = 3
            member x.Int32ExtensionProperty2 with get() = 3
            member x.Int32ExtensionProperty3 with set(v:int) = ()
            member x.Int32ExtensionIndexer1 with get(idx:int) = idx
            member x.Int32ExtensionIndexer2 with set(idx:int) (v:int) = ()
 
        let v = new obj()
        let _ = <@@ v.ExtensionMethod0() @@>  |> checkQuoteString "fqekhec5" """Call (None, Object.ExtensionMethod0, [PropertyGet (None, v, [])])"""
        let _ = <@@ v.ExtensionMethod1() @@> |> checkQuoteString "fqekhec6" """Call (None, Object.ExtensionMethod1, [PropertyGet (None, v, [])])"""
        let _ = <@@ v.ExtensionMethod2(3) @@> |> checkQuoteString "fqekhec7" """Call (None, Object.ExtensionMethod2, [PropertyGet (None, v, []), Value (3)])"""
        let _ = <@@ v.ExtensionMethod3(3) @@> |> checkQuoteString "fqekhec8" """Call (None, Object.ExtensionMethod3, [PropertyGet (None, v, []), Value (3)])"""
        let _ = <@@ v.ExtensionMethod4(3,4) @@> |> checkQuoteString "fqekhec9" """Call (None, Object.ExtensionMethod4, [PropertyGet (None, v, []), Value (3), Value (4)])"""
        let _ = <@@ v.ExtensionMethod5(3,4) @@> |> checkQuoteString "fqekhec10" """Call (None, Object.ExtensionMethod5, [PropertyGet (None, v, []), NewTuple (Value (3), Value (4))])"""
        let _ = <@@ v.ExtensionProperty1 @@> |> checkQuoteString "fqekhec11" """Call (None, Object.get_ExtensionProperty1, [PropertyGet (None, v, [])])"""
        let _ = <@@ v.ExtensionProperty2 @@> |> checkQuoteString "fqekhec12" """Call (None, Object.get_ExtensionProperty2, [PropertyGet (None, v, [])])"""
        let _ = <@@ v.ExtensionProperty3 <- 4 @@> |> checkQuoteString "fqekhec13" """Call (None, Object.set_ExtensionProperty3, [PropertyGet (None, v, []), Value (4)])"""
        let _ = <@@ v.ExtensionIndexer1(3) @@> |> checkQuoteString "fqekhec14" """Call (None, Object.get_ExtensionIndexer1, [PropertyGet (None, v, []), Value (3)])"""
        let _ = <@@ v.ExtensionIndexer2(3) <- 4 @@> |> checkQuoteString "fqekhec15" """Call (None, Object.set_ExtensionIndexer2, [PropertyGet (None, v, []), Value (3), Value (4)])"""

        let _ = <@@ v.ExtensionMethod0 @@> |> checkQuoteString "fqekhec16" """Lambda (unitVar, Call (None, Object.ExtensionMethod0, [PropertyGet (None, v, [])]))"""
        let _ = <@@ v.ExtensionMethod1 @@> |> checkQuoteString "fqekhec17" """Lambda (unitVar, Call (None, Object.ExtensionMethod1, [PropertyGet (None, v, [])]))"""
        let _ = <@@ v.ExtensionMethod2 @@> |> checkQuoteString "fqekhec18" """Lambda (arg00, Call (None, Object.ExtensionMethod2, [PropertyGet (None, v, []), arg00]))"""
        let _ = <@@ v.ExtensionMethod3 @@> |> checkQuoteString "fqekhec19" """Lambda (arg00, Call (None, Object.ExtensionMethod3, [PropertyGet (None, v, []), arg00]))"""
        let _ = <@@ v.ExtensionMethod4 @@> |> checkQuoteString "fqekhec20" """Lambda (tupledArg, Let (arg00, TupleGet (tupledArg, 0), Let (arg01, TupleGet (tupledArg, 1), Call (None, Object.ExtensionMethod4, [PropertyGet (None, v, []), arg00, arg01]))))"""
        let _ = <@@ v.ExtensionMethod5 @@> |> checkQuoteString "fqekhec21" """Lambda (arg00, Call (None, Object.ExtensionMethod5, [PropertyGet (None, v, []), arg00]))"""

        let v2 = 3
        let _ = <@@ v2.ExtensionMethod0() @@> |> checkQuoteString "fqekhec22" """Call (None, Object.ExtensionMethod0, [Coerce (PropertyGet (None, v2, []), Object)])"""
        let _ = <@@ v2.ExtensionMethod1() @@> |> checkQuoteString "fqekhec23" """Call (None, Object.ExtensionMethod1, [Coerce (PropertyGet (None, v2, []), Object)])"""
        let _ = <@@ v2.ExtensionMethod2(3) @@> |> checkQuoteString "fqekhec24" """Call (None, Object.ExtensionMethod2, [Coerce (PropertyGet (None, v2, []), Object), Value (3)])"""
        let _ = <@@ v2.ExtensionMethod3(3) @@> |> checkQuoteString "fqekhec25" """Call (None, Object.ExtensionMethod3, [Coerce (PropertyGet (None, v2, []), Object), Value (3)])"""
        let _ = <@@ v2.ExtensionMethod4(3,4) @@> |> checkQuoteString "fqekhec26" """Call (None, Object.ExtensionMethod4, [Coerce (PropertyGet (None, v2, []), Object), Value (3), Value (4)])"""
        let _ = <@@ v2.ExtensionMethod5(3,4) @@> |> checkQuoteString "fqekhec27" """Call (None, Object.ExtensionMethod5, [Coerce (PropertyGet (None, v2, []), Object), NewTuple (Value (3), Value (4))])"""
        let _ = <@@ v2.ExtensionProperty1 @@> |> checkQuoteString "fqekhec28" """Call (None, Object.get_ExtensionProperty1, [Coerce (PropertyGet (None, v2, []), Object)])"""
        let _ = <@@ v2.ExtensionProperty2 @@> |> checkQuoteString "fqekhec29" """Call (None, Object.get_ExtensionProperty2, [Coerce (PropertyGet (None, v2, []), Object)])"""
        let _ = <@@ v2.ExtensionProperty3 <- 4 @@> |> checkQuoteString "fqekhec30" """Call (None, Object.set_ExtensionProperty3, [Coerce (PropertyGet (None, v2, []), Object), Value (4)])"""
        let _ = <@@ v2.ExtensionIndexer1(3) @@> |> checkQuoteString "fqekhec31" """Call (None, Object.get_ExtensionIndexer1, [Coerce (PropertyGet (None, v2, []), Object), Value (3)])"""
        let _ = <@@ v2.ExtensionIndexer2(3) <- 4 @@> |> checkQuoteString "fqekhec32" """Call (None, Object.set_ExtensionIndexer2, [Coerce (PropertyGet (None, v2, []), Object), Value (3), Value (4)])"""

        let _ = <@@ v2.ExtensionMethod0 @@> |> checkQuoteString "fqekhec33" """Lambda (unitVar, Call (None, Object.ExtensionMethod0, [Coerce (PropertyGet (None, v2, []), Object)]))"""
        let _ = <@@ v2.ExtensionMethod1 @@> |> checkQuoteString "fqekhec34" """Lambda (unitVar, Call (None, Object.ExtensionMethod1, [Coerce (PropertyGet (None, v2, []), Object)]))"""
        let _ = <@@ v2.ExtensionMethod2 @@> |> checkQuoteString "fqekhec35" """Lambda (arg00, Call (None, Object.ExtensionMethod2, [Coerce (PropertyGet (None, v2, []), Object), arg00]))"""
        let _ = <@@ v2.ExtensionMethod3 @@> |> checkQuoteString "fqekhec36" """Lambda (arg00, Call (None, Object.ExtensionMethod3, [Coerce (PropertyGet (None, v2, []), Object), arg00]))"""
        let _ = <@@ v2.ExtensionMethod4 @@> |> checkQuoteString "fqekhec37" """Lambda (tupledArg, Let (arg00, TupleGet (tupledArg, 0), Let (arg01, TupleGet (tupledArg, 1), Call (None, Object.ExtensionMethod4, [Coerce (PropertyGet (None, v2, []), Object), arg00, arg01]))))"""
        let _ = <@@ v2.ExtensionMethod5 @@> |> checkQuoteString "fqekhec38" """Lambda (arg00, Call (None, Object.ExtensionMethod5, [Coerce (PropertyGet (None, v2, []), Object), arg00]))"""

        let _ = <@@ v2.Int32ExtensionMethod0() @@> |> checkQuoteString "fqekhec39" """Call (None, Int32.Int32ExtensionMethod0, [PropertyGet (None, v2, [])])"""
        let _ = <@@ v2.Int32ExtensionMethod1() @@> |> checkQuoteString "fqekhec40" """Call (None, Int32.Int32ExtensionMethod1, [PropertyGet (None, v2, [])])"""
        let _ = <@@ v2.Int32ExtensionMethod2(3) @@> |> checkQuoteString "fqekhec41" """Call (None, Int32.Int32ExtensionMethod2, [PropertyGet (None, v2, []), Value (3)])"""
        let _ = <@@ v2.Int32ExtensionMethod3(3) @@> |> checkQuoteString "fqekhec42" """Call (None, Int32.Int32ExtensionMethod3, [PropertyGet (None, v2, []), Value (3)])"""
        let _ = <@@ v2.Int32ExtensionMethod4(3,4) @@> |> checkQuoteString "fqekhec43" """Call (None, Int32.Int32ExtensionMethod4, [PropertyGet (None, v2, []), Value (3), Value (4)])"""
        let _ = <@@ v2.Int32ExtensionMethod5(3,4) @@> |> checkQuoteString "fqekhec44" """Call (None, Int32.Int32ExtensionMethod5, [PropertyGet (None, v2, []), NewTuple (Value (3), Value (4))])"""
        let _ = <@@ v2.Int32ExtensionProperty1 @@> |> checkQuoteString "fqekhec45" """Call (None, Int32.get_Int32ExtensionProperty1, [PropertyGet (None, v2, [])])"""
        let _ = <@@ v2.Int32ExtensionProperty2 @@> |> checkQuoteString "fqekhec46" """Call (None, Int32.get_Int32ExtensionProperty2, [PropertyGet (None, v2, [])])"""
        let _ = <@@ v2.Int32ExtensionProperty3 <- 4 @@> |> checkQuoteString "fqekhec47" """Call (None, Int32.set_Int32ExtensionProperty3, [PropertyGet (None, v2, []), Value (4)])"""
        let _ = <@@ v2.Int32ExtensionIndexer1(3) @@> |> checkQuoteString "fqekhec48" """Call (None, Int32.get_Int32ExtensionIndexer1, [PropertyGet (None, v2, []), Value (3)])"""
        let _ = <@@ v2.Int32ExtensionIndexer2(3) <- 4 @@> |> checkQuoteString "fqekhec49" """Call (None, Int32.set_Int32ExtensionIndexer2, [PropertyGet (None, v2, []), Value (3), Value (4)])"""

        let _ = <@@ v2.Int32ExtensionMethod0 @@> |> checkQuoteString "fqekhec50" """Lambda (unitVar, Call (None, Int32.Int32ExtensionMethod0, [PropertyGet (None, v2, [])]))"""
        let _ = <@@ v2.Int32ExtensionMethod1 @@> |> checkQuoteString "fqekhec51" """Lambda (unitVar, Call (None, Int32.Int32ExtensionMethod1, [PropertyGet (None, v2, [])]))"""
        let _ = <@@ v2.Int32ExtensionMethod2 @@> |> checkQuoteString "fqekhec52" """Lambda (arg00, Call (None, Int32.Int32ExtensionMethod2, [PropertyGet (None, v2, []), arg00]))"""
        let _ = <@@ v2.Int32ExtensionMethod3 @@> |> checkQuoteString "fqekhec53" """Lambda (arg00, Call (None, Int32.Int32ExtensionMethod3, [PropertyGet (None, v2, []), arg00]))"""
        let _ = <@@ v2.Int32ExtensionMethod4 @@> |> checkQuoteString "fqekhec54" """Lambda (tupledArg, Let (arg00, TupleGet (tupledArg, 0), Let (arg01, TupleGet (tupledArg, 1), Call (None, Int32.Int32ExtensionMethod4, [PropertyGet (None, v2, []), arg00, arg01]))))"""
        let _ = <@@ v2.Int32ExtensionMethod5 @@> |> checkQuoteString "fqekhec55" """Lambda (arg00, Call (None, Int32.Int32ExtensionMethod5, [PropertyGet (None, v2, []), arg00]))"""


module QuotationConstructionTests = 
    let arr = [| 1;2;3;4;5 |]
    let f : int -> int = printfn "hello"; (fun x -> x)
    let f2 : int * int -> int -> int = printfn "hello"; (fun (x,y) z -> x + y + z)
    let F (x:int) = x
    let F2 (x:int,y:int) (z:int) = x + y + z

    type Foo () =
        member t.Item with get (index:int) = 1
                      and set (index:int) (value:int) = ()

    let ctorof q = match q with Patterns.NewObject(cinfo,_) -> cinfo | _ -> failwith "ctorof"
    let methodof q = match q with DerivedPatterns.Lambdas(_,Patterns.Call(_,minfo,_)) -> minfo | _ -> failwith "methodof"
    let fieldof q = match q with Patterns.FieldGet(_,finfo) -> finfo | _ -> failwith "fieldof"
    let ucaseof q = match q with Patterns.NewUnionCase(ucinfo,_) -> ucinfo | _ -> failwith "ucaseof"
    let getof q = match q with Patterns.PropertyGet(_,pinfo,_) -> pinfo | _ -> failwith "getof"
    let setof q = match q with Patterns.PropertySet(_,pinfo,_,_) -> pinfo | _ -> failwith "setof"
    check "vcknwwe01" (match Expr.AddressOf <@@ arr.[3] @@> with AddressOf(expr) -> expr = <@@ arr.[3] @@> | _ -> false) true
    check "vcknwwe02" (match Expr.AddressSet (Expr.AddressOf <@@ arr.[3] @@>, <@@ 4 @@>) with AddressSet(AddressOf(expr),v) -> expr = <@@ arr.[3] @@> && v = <@@ 4 @@> | _ -> false) true
    check "vcknwwe03" (match Expr.Application(<@@ f @@>,<@@ 5 @@>) with Application(f1,x) -> f1 = <@@ f @@> && x = <@@ 5 @@> | _ -> false) true
    check "vcknwwe04" (match Expr.Applications(<@@ f @@>,[[ <@@ 5 @@> ]]) with Applications(f1,[[x]]) -> f1 = <@@ f @@> && x = <@@ 5 @@> | _ -> false) true
    check "vcknwwe05" (match Expr.Applications(<@@ f2 @@>,[[ <@@ 5 @@>;<@@ 6 @@> ]; [ <@@ 7 @@> ]]) with Applications(f1,[[x;y];[z]]) -> f1 = <@@ f2 @@> && x = <@@ 5 @@> && y = <@@ 6 @@> && z = <@@ 7 @@>  | _ -> false) true
    check "vcknwwe06" (match Expr.Call(methodof <@@ F2 @@>,[ <@@ 5 @@>;<@@ 6 @@>; <@@ 7 @@> ]) with Call(None,minfo,[x;y;z]) -> minfo = methodof <@@ F2 @@> && x = <@@ 5 @@> && y = <@@ 6 @@> && z = <@@ 7 @@>  | _ -> false) true
    check "vcknwwe07" (Expr.Cast(<@@ 5 @@>) : Expr<int>) (<@ 5 @>)
    check "vcknwwe08" (try let _ = Expr.Cast(<@@ 5 @@>) : Expr<string> in false with :? System.ArgumentException -> true) true
    check "vcknwwe09" (match Expr.Coerce(<@@ 5 @@>, typeof<obj>) with Coerce(q,ty) -> ty = typeof<obj> && q = <@@ 5 @@> | _ -> false) true
    check "vcknwwe0q" (match Expr.DefaultValue(typeof<obj>) with DefaultValue(ty) -> ty = typeof<obj> | _ -> false) true
    check "vcknwwe0w" (match Expr.FieldGet(typeof<int>.GetField("MaxValue")) with FieldGet(None,finfo) -> finfo = typeof<int>.GetField("MaxValue") | _ -> false) true
    check "vcknwwe0e" (match Expr.FieldSet(typeof<int>.GetField("MaxValue"),<@@ 1 @@>) with FieldSet(None,finfo,v) -> finfo = typeof<int>.GetField("MaxValue") && v = <@@ 1 @@> | _ -> false) true
    check "vcknwwe0r" (match Expr.ForIntegerRangeLoop(Var.Global("i",typeof<int>),<@@ 1 @@>,<@@ 10 @@>,<@@ () @@>) with ForIntegerRangeLoop(v,start,finish,body) -> v = Var.Global("i",typeof<int>) && start = <@@ 1 @@> && finish = <@@ 10 @@> && body = <@@ () @@>  | _ -> false) true
    check "vcknwwe0t" (match Expr.GlobalVar("i") : Expr<int> with Var(v) -> v = Var.Global("i",typeof<int>)   | _ -> false) true
    check "vcknwwe0y" (match Expr.IfThenElse(<@@ true @@>,<@@ 1 @@>,<@@ 2 @@>) with IfThenElse(gd,t,e) -> gd = <@@ true @@> && t = <@@ 1 @@> && e = <@@ 2 @@>   | _ -> false) true
    check "vcknwwe0u" (match Expr.Lambda(Var.Global("i",typeof<int>), <@@ 2 @@>) with Lambda(v,b) -> v = Var.Global("i",typeof<int>) && b = <@@ 2 @@>   | _ -> false) true
    check "vcknwwe0i" (match Expr.Let(Var.Global("i",typeof<int>), <@@ 2 @@>, <@@ 3 @@>) with Let(v,e,b) -> v = Var.Global("i",typeof<int>) && e = <@@ 2 @@> && b = <@@ 3 @@>   | _ -> false) true
    check "vcknwwe0o" (match Expr.LetRecursive([(Var.Global("i",typeof<int>), <@@ 2 @@>)], <@@ 3 @@>) with LetRecursive([(v,e)],b) -> v = Var.Global("i",typeof<int>) && e = <@@ 2 @@> && b = <@@ 3 @@>   | _ -> false) true
    check "vcknwwe0p" (match Expr.LetRecursive([(Var.Global("i",typeof<int>), <@@ 2 @@>);(Var.Global("j",typeof<int>), <@@ 3 @@>)], <@@ 3 @@>) with LetRecursive([(v1,e1);(v2,e2)],b) -> v1 = Var.Global("i",typeof<int>) && v2 = Var.Global("j",typeof<int>) && e1 = <@@ 2 @@> && e2 = <@@ 3 @@> && b = <@@ 3 @@>   | _ -> false) true
    check "vcknwwe0a" (Expr.NewArray(typeof<int>,[ <@@ 1 @@>; <@@ 2 @@> ])) <@@ [| 1;2 |] @@>
    check "vcknwwe0s" (match Expr.NewDelegate(typeof<Action<int>>,[ Var.Global("i",typeof<int>) ], <@@ () @@>) with NewDelegate(ty,[v],e) -> ty = typeof<Action<int>> && v = Var.Global("i",typeof<int>) && e = <@@ () @@> | _ -> false) true
    check "vcknwwe0d" (match Expr.NewObject(ctorof <@@ new obj() @@> ,[ ]) with NewObject(ty,[]) -> ty = ctorof <@@ new obj() @@> | _ -> false) true
    check "vcknwwe0f" (match Expr.NewObject(ctorof <@@ new System.String('a',3) @@> ,[ <@@ 'b' @@>; <@@ 4 @@>]) with NewObject(ty,[x;y]) -> ty = ctorof <@@ new string('a',3) @@> && x = <@@ 'b' @@> && y = <@@ 4 @@> | _ -> false) true
    check "vcknwwe0g" (Expr.NewRecord(typeof<int ref> ,[ <@@ 4 @@> ])) <@@ { contents = 4 } @@>
    check "vcknwwe0h" (try let _ = Expr.NewTuple([]) in false with :? System.ArgumentException -> true) true
    check "vcknwwe0j" (try let _ = Expr.NewTuple([ <@@ 1 @@> ]) in true with :? System.ArgumentException -> false) true
    check "vcknwwe0k" (match Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>]) with NewTuple([x;y]) -> x = <@@ 'b' @@> && y = <@@ 4 @@> | _ -> false) true
    check "vcknwwe0l" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>])) <@@ ('b',4) @@>
    check "vcknwwe0z" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>])) <@@ ('b',4,5) @@>
    check "vcknwwe0x" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>])) <@@ ('b',4,5,6) @@>
    check "vcknwwe0c" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>; <@@ 7 @@>])) <@@ ('b',4,5,6,7) @@>
    check "vcknwwe0v" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>; <@@ 7 @@>; <@@ 8 @@>])) <@@ ('b',4,5,6,7,8) @@>
    check "vcknwwe0b" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>; <@@ 7 @@>; <@@ 8 @@>; <@@ 9 @@>])) <@@ ('b',4,5,6,7,8,9) @@>
    check "vcknwwe0n" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>; <@@ 7 @@>; <@@ 8 @@>; <@@ 9 @@>; <@@ 10 @@>])) <@@ ('b',4,5,6,7,8,9,10) @@>
    check "vcknwwe0m" (Expr.NewTuple([ <@@ 'b' @@>; <@@ 4 @@>; <@@ 5 @@>; <@@ 6 @@>; <@@ 7 @@>; <@@ 8 @@>; <@@ 9 @@>; <@@ 10 @@>])) <@@ ('b',4,5,6,7,8,9,10) @@>
    check "vcknwwe011" (Expr.NewUnionCase(ucaseof <@@ Some(3) @@>,[ <@@ 4 @@> ])) <@@ Some(4) @@>
    check "vcknwwe022" (Expr.NewUnionCase(ucaseof <@@ None @@>,[  ])) <@@ None @@>
    check "vcknwwe033" (try let _ = Expr.NewUnionCase(ucaseof <@@ Some(3) @@>,[  ]) in false with :? ArgumentException -> true) true
    check "vcknwwe044" (try let _ = Expr.NewUnionCase(ucaseof <@@ None @@>,[ <@@ 1 @@> ]) in false with :? ArgumentException -> true) true
    check "vcknwwe055" (Expr.PropertyGet(getof <@@ System.DateTime.Now @@>,[  ])) <@@ System.DateTime.Now @@>
    check "vcknwwe066" (try let _ = Expr.PropertyGet(getof <@@ System.DateTime.Now @@>,[ <@@ 1 @@> ]) in false with :? ArgumentException -> true) true
    check "vcknwwe077" (Expr.PropertyGet(<@@ "3" @@>, getof <@@ "1".Length @@>)) <@@ "3".Length @@>
    check "vcknwwe088" (Expr.PropertyGet(<@@ "3" @@>, getof <@@ "1".Length @@>,[  ])) <@@ "3".Length @@>
#if !TESTS_AS_APP && !NETCOREAPP
    check "vcknwwe099" (Expr.PropertySet(<@@ (new System.Windows.Forms.Form()) @@>, setof <@@ (new System.Windows.Forms.Form()).Text <- "2" @@>, <@@ "3" @@> )) <@@ (new System.Windows.Forms.Form()).Text <- "3" @@>
#endif
    check "vcknwwe099" (Expr.PropertySet(<@@ (new Foo()) @@>, setof <@@ (new Foo()).[3] <- 1 @@>, <@@ 2 @@> , [ <@@ 3 @@> ] )) <@@ (new Foo()).[3] <- 2 @@>
#if FSHARP_CORE_31
#else
    check "vcknwwe0qq1" (Expr.QuoteRaw(<@ "1" @>)) <@@ <@@ "1" @@> @@>
    check "vcknwwe0qq2" (Expr.QuoteRaw(<@@ "1" @@>)) <@@ <@@ "1" @@> @@>
    check "vcknwwe0qq3" (Expr.QuoteTyped(<@ "1" @>)) <@@ <@ "1" @> @@>
    check "vcknwwe0qq4" (Expr.QuoteTyped(<@@ "1" @@>)) <@@ <@ "1" @> @@>
#endif
    check "vcknwwe0ww" (Expr.Sequential(<@@ () @@>, <@@ 1 @@>)) <@@ (); 1 @@>
    check "vcknwwe0ee" (Expr.TryFinally(<@@ 1 @@>, <@@ () @@>)) <@@ try 1 finally () @@>
    check "vcknwwe0rr" (match Expr.TryWith(<@@ 1 @@>, Var.Global("e1",typeof<exn>), <@@ 1 @@>, Var.Global("e2",typeof<exn>), <@@ 2 @@>) with TryWith(b,v1,ef,v2,eh) -> b = <@@ 1 @@> && eh = <@@ 2 @@> && ef = <@@ 1 @@> && v1 = Var.Global("e1",typeof<exn>) && v2 = Var.Global("e2",typeof<exn>)| _ -> false) true 
    check "vcknwwe0tt" (match Expr.TupleGet(<@@ (1,2) @@>, 0) with TupleGet(b,n) -> b = <@@ (1,2) @@> && n = 0 | _ -> false) true 
    check "vcknwwe0yy" (match Expr.TupleGet(<@@ (1,2) @@>, 1) with TupleGet(b,n) -> b = <@@ (1,2) @@> && n = 1 | _ -> false) true 
    check "vcknwwe0uu" (try let _ = Expr.TupleGet(<@@ (1,2) @@>, 2) in false with :? ArgumentException -> true) true
    check "vcknwwe0ii" (try let _ = Expr.TupleGet(<@@ (1,2) @@>, -1) in false with :? ArgumentException -> true) true
    for i = 0 to 7 do 
        check "vcknwwe0oo" (match Expr.TupleGet(<@@ (1,2,3,4,5,6,7,8) @@>, i) with TupleGet(b,n) -> b = <@@ (1,2,3,4,5,6,7,8) @@> && n = i | _ -> false) true 

    check "vcknwwe0pp" (match Expr.TypeTest(<@@ new obj() @@>, typeof<string>) with TypeTest(e,ty) -> e = <@@ new obj() @@> && ty = typeof<string> | _ -> false) true
    check "vcknwwe0aa" (match Expr.UnionCaseTest(<@@ [] : int list @@>, ucaseof <@@ [] : int list @@> ) with UnionCaseTest(e,uc) -> e = <@@ [] : int list @@> && uc = ucaseof <@@ [] : int list @@>  | _ -> false) true
    check "vcknwwe0ss" (Expr.Value(3)) <@@ 3 @@>
    check "vcknwwe0dd" (match Expr.Var(Var.Global("i",typeof<int>)) with Var(v) -> v = Var.Global("i",typeof<int>) | _ -> false) true
    check "vcknwwe0ff" (match Expr.VarSet(Var.Global("i",typeof<int>), <@@ 4 @@>) with VarSet(v,q) -> v = Var.Global("i",typeof<int>) && q = <@@ 4 @@>  | _ -> false) true
    check "vcknwwe0gg" (match Expr.WhileLoop(<@@ true @@>, <@@ () @@>) with WhileLoop(g,b) -> g = <@@ true @@> && b = <@@ () @@>  | _ -> false) true



module QuotationStructUnionTests = 

    [<Struct>]
    type T = | A of int
        
    test "check NewUnionCase"   (<@ A(1) @> |> (function NewUnionCase(unionCase,args) -> true | _ -> false))
    
    [<ReflectedDefinition>]
    let foo v = match v with  | A(1) -> 0 | _ -> 1
      
    test "check TryGetReflectedDefinition (local f)" 
        ((<@ foo (A(1)) @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 

    [<ReflectedDefinition>]
    let test3297327 v = match v with  | A(1) -> 0 | _ -> 1
      
    test "check TryGetReflectedDefinition (local f)" 
        ((<@ foo (A(1)) @> |> (function Call(None,minfo,args) -> Quotations.Expr.TryGetReflectedDefinition(minfo).IsSome | _ -> false))) 


    [<Struct>]
    type T2 = 
        | A1 of int * int

    test "check NewUnionCase"   (<@ A1(1,2) @> |> (function NewUnionCase(unionCase,[ Int32 1; Int32 2 ]) -> true | _ -> false))

    //[<DefaultAugmentation(false); Struct>]
    //type T3 = 
    //    | A1 of int * int
    //
    //test "check NewUnionCase"   (<@ A1(1,2) @> |> (function NewUnionCase(unionCase,[ Int32 1; Int32 2 ]) -> true | _ -> false))


module EqualityOnExprDoesntFail = 
    let q = <@ 1 @>
    check "we09ceo" (q.Equals(1)) false
    check "we09ceo" (q.Equals(q)) true
    check "we09ceo" (q.Equals(<@ 1 @>)) true
    check "we09ceo" (q.Equals(<@ 2 @>)) false
    check "we09ceo" (q.Equals(null)) false
    
module EqualityOnVarDoesntFail = 
    let v = Var.Global("c",typeof<int>)
    let v2 = Var.Global("c",typeof<int>)
    let v3 = Var.Global("d",typeof<int>)
    check "we09ceo2" (v.Equals(1)) false
    check "we09ceo2" (v.Equals(v)) true
    check "we09ceo2" (v.Equals(v2)) true
    check "we09ceo2" (v.Equals(v3)) false
    check "we09ceo2" (v.Equals(null)) false
    
module RelatedChange3628 =
    // Fix for 3628 translates "do x" into "do (x;())" when x is not unit typed.
    // This regression checks the quotated form.

    open System
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.DerivedPatterns

    [<ReflectedDefinition>] 
    let f (x:int) = do x
    let (Call(None,minfo,args)) = <@ f 1 @>
    let (Some lamexp) = Quotations.Expr.TryGetReflectedDefinition(minfo)
    let (Lambda(v,body)) = lamexp
    let (Sequential (a,b)) = body
    let (Var v2) = a
    check "RelatedChange3628.a" v v2
    check "RelatedChange3628.b" b <@@ () @@>

module Check3628 =
  let inline fA (x:int) = (do x) 
  let        fB (x:int) = (do x)
  let resA = fA 12
  let resB = fB 13
  let mutable (z:unit) = ()
  z <- fA 14
  z <- fB 15

module ReflectedDefinitionForPatternInputTest = 

   [<ReflectedDefinition>] 
   let [x] = [1];;

module Test920236 =
    open System.Collections
    type Arr(a : int[]) =
      interface IEnumerable with
          member this.GetEnumerator() = 
              let i = ref -1
              { new IEnumerator with
                  member this.Reset() = failwith "not supported"
                  member this.MoveNext() = incr i; !i < a.Length
                  member this.Current = box (a.[!i]) 
              }
    let arr = Arr([||])

    let q = 
      let a = arr
      <@ for i in a do ignore i @>

    test "Test920236"
        (match q with
         |    Let(e,Call(Some (Coerce(Value a, typ)), mi, []), 
                  (TryFinally 
                    (WhileLoop((Call (Some e1, moveNext, [])),
                         Let(i,
                             PropertyGet ((Some e2),piCurrent,[]),
                             Call(None,_,[_]) // ignore
                         )
                    ),
                    IfThenElse(TypeTest(Coerce(_,typObj), typDisposable),
                               Call(Some(Call(None, unboxGeneric, [Coerce(e3,typObj2)])),disposeMI, []), Value _)
                  ))
                 )               
                 when typ.FullName = "System.Collections.IEnumerable" &&
                      mi.Name = "GetEnumerator" &&
                      moveNext.Name = "MoveNext" && 
                      piCurrent.Name = "Current" &&
                      typDisposable.FullName = "System.IDisposable" &&
                      unboxGeneric.Name = "UnboxGeneric"
                 -> true
         |    _ -> false)
        

module TestQuotationOfCOnstructors = 

    type MyClassWithNoFields [<ReflectedDefinition>] () = 
        member this.Bar z = ()

    [<ReflectedDefinition>] 
    type MyClassWithNoFieldsOuter () = 
        member this.Bar z = ()

    [<ReflectedDefinition>] 
    module M = 
        type MyClassWithNoFieldsNestedInModule () = 
            member this.Bar z = ()

        module Inner = 
            type MyClassWithNoFieldsNestedInInnerModule () = 
                member this.Bar z = ()

            module Inner = 
                type MyClassWithNoFieldsNestedInInnerModule () = 
                    member this.Bar z = ()


    type MyClassWithFields [<ReflectedDefinition>]() = 
        let x = 12
        let y = x
        let w = x // note this variable is not used in any method and becomes local to the constructor

        [<ReflectedDefinition>]
        member this.Bar z = x + z + y

    type MyGenericClassWithArgs<'T> [<ReflectedDefinition>](inp:'T) = 
        let x = inp
        let y = x
        let w = x // note this variable is not used in any method and becomes local to the constructor

        [<ReflectedDefinition>]
        member this.Bar z = (x,y,z)

    type MyGenericClassWithTwoArgs<'T> [<ReflectedDefinition>](inpA:'T, inpB:'T) = // note, inpB is captured 
        let x = inpA
        let y = x
        let w = x // note this variable is not used in any method and becomes local to the constructor

        [<ReflectedDefinition>]
        member this.Bar z = (x,y,z,inpB)

    type MyClassWithAsLetMethod () = 
        [<ReflectedDefinition>]
        let f() = 1

        [<ReflectedDefinition>]
        member this.Bar z = f()


    [<ReflectedDefinition>]
    type MyClassWithAsLetMethodOuter () = 
        let f() = 1

        member this.Bar z = f()



    Expr.TryGetReflectedDefinition (typeof<MyClassWithNoFields>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<MyClassWithNoFieldsOuter>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<M.MyClassWithNoFieldsNestedInModule>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<M.Inner.MyClassWithNoFieldsNestedInInnerModule>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<M.Inner.Inner.MyClassWithNoFieldsNestedInInnerModule>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<MyClassWithFields>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<MyGenericClassWithArgs<int>>.GetConstructors().[0]) |> printfn "%A"
    Expr.TryGetReflectedDefinition (typeof<MyGenericClassWithTwoArgs<int>>.GetConstructors().[0]) |> printfn "%A"



    test "vkjnkvrw2"
       (match Expr.TryGetReflectedDefinition (typeof<MyClassWithFields>.GetConstructors().[0]) with 
        | Some 
           (Lambda 
              (_,Sequential 
                   (NewObject objCtor,
                    Sequential 
                       (FieldSet (Some (Var thisVar0), xField1, Int32 12),
                        Sequential 
                           (FieldSet (Some (Var thisVar1), yField,FieldGet (Some (Var thisVar2),xField2)),
                            Let (wVar,FieldGet (Some (Var thisVar3), xField3), Unit))))))
            -> 
                thisVar0 = thisVar1 && 
                thisVar1 = thisVar2 && 
                thisVar2 = thisVar3 && 
                thisVar1.Name = "this" && 
                thisVar1.Type = typeof<MyClassWithFields> && 
                thisVar1 = Var.Global("this", typeof<MyClassWithFields>) && 
                xField1.Name = "x" &&
                xField2.Name = "x" &&
                xField3.Name = "x" &&
                yField.Name = "y" &&
                wVar.Name = "w" &&
                wVar.Type = typeof<int>  
                
        | _ -> false)



    test "vkjnkvrw3"
       (match Expr.TryGetReflectedDefinition (typeof<MyGenericClassWithArgs<int>>.GetConstructors().[0]) with 
        | Some 
           (Lambda
              (inpVar,Sequential 
                   (NewObject objCtor,
                    Sequential 
                       (FieldSet (Some (Var thisVar0), xField1, Var inpVar1),
                        Sequential 
                           (FieldSet (Some (Var thisVar1), yField,FieldGet (Some (Var thisVar2),xField2)),
                            Let (wVar,FieldGet (Some (Var thisVar3), xField3), Unit))))))
            -> 
                inpVar.Name = "inp" &&
                inpVar.Type = typeof<int> &&
                thisVar0 = thisVar1 && 
                thisVar1 = thisVar2 && 
                thisVar2 = thisVar3 && 
                thisVar1.Name = "this" && 
                thisVar1.Type = typeof<MyGenericClassWithArgs<int>> && 
                thisVar1 = Var.Global("this", typeof<MyGenericClassWithArgs<int>>) && 
                xField1.Name = "x" &&
                xField2.Name = "x" &&
                xField3.Name = "x" &&
                yField.Name = "y" &&
                wVar.Name = "w" &&
                wVar.Type = typeof<int>  
                
        | _ -> false)


    test "vkjnkvrw4"
       (match Expr.TryGetReflectedDefinition (typeof<MyGenericClassWithTwoArgs<int>>.GetConstructors().[0]) with 
        | Some 
           (Lambdas
              ([[inpAVar1; inpBVar1]],
               Sequential 
                   (NewObject objCtor,
                    Sequential 
                       (FieldSet (Some (Var thisVar0), inpBField, Var inpBVar2),
                        Sequential 
                           (FieldSet (Some (Var thisVar1), xField1, Var inpAVar2),
                            Sequential 
                               (FieldSet (Some (Var thisVar2), yField,FieldGet (Some (Var thisVar3),xField2)),
                                Let (wVar,FieldGet (Some (Var thisVar4), xField3), Unit)))))))
            -> true ||
                inpAVar1 = inpAVar2 &&
                inpAVar1.Name = "inpA" &&
                inpAVar2.Type = typeof<int> &&
                inpBVar1 = inpBVar2 &&
                inpBVar1.Name = "inpB" &&
                inpBVar1.Type = typeof<int> &&
                thisVar0 = thisVar1 && 
                thisVar1 = thisVar2 && 
                thisVar2 = thisVar3 && 
                thisVar3 = thisVar4 && 
                thisVar1.Name = "this" && 
                thisVar1 = Var.Global("this", typeof<MyGenericClassWithTwoArgs<int>>) && 
                thisVar1.Type = typeof<MyGenericClassWithTwoArgs<int>> && 
                inpBField.Name = "inpB" &&
                xField1.Name = "x" &&
                xField2.Name = "x" &&
                xField3.Name = "x" &&
                yField.Name = "y" &&
                wVar.Name = "w" &&
                wVar.Type = typeof<int>  
                
        | _ -> false)

    // Also test getting the reflected definition for private members implied by "let f() = ..." bindings
    let fMethod = (typeof<MyClassWithAsLetMethod>.GetMethod("f", Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.Public ||| Reflection.BindingFlags.NonPublic))

    // Also test getting the reflected definition for private members implied by "let f() = ..." bindings
    let fMethodOuter = (typeof<MyClassWithAsLetMethodOuter>.GetMethod("f", Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.Public ||| Reflection.BindingFlags.NonPublic))

    test "vkjnkvrw1"
       (match Expr.TryGetReflectedDefinition fMethod with 
        | Some (Lambdas ([[thisVar];[unitVar]], Int32 1))
            -> unitVar.Type = typeof<unit>
        | _ -> false)

    Expr.TryGetReflectedDefinition fMethod |> printfn "%A"

    test "vkjnkvrw0"
       (match Expr.TryGetReflectedDefinition (typeof<MyClassWithNoFields>.GetConstructors().[0]) with 
        | Some (Lambda (unitVar,Sequential (NewObject objCtor,Unit)))
            -> unitVar.Type = typeof<unit>
        | _ -> false)

    test "vkjnkvrw0b"
       (match Expr.TryGetReflectedDefinition (typeof<MyClassWithNoFieldsOuter>.GetConstructors().[0]) with 
        | Some (Lambda (unitVar,Sequential (NewObject objCtor,Unit)))
            -> unitVar.Type = typeof<unit>
        | _ -> false)

    test "vkjnkvrw0c"
       (match Expr.TryGetReflectedDefinition (typeof<M.MyClassWithNoFieldsNestedInModule>.GetConstructors().[0]) with 
        | Some (Lambda (unitVar,Sequential (NewObject objCtor,Unit)))
            -> unitVar.Type = typeof<unit>
        | _ -> false)


module IndexedPropertySetTest = 
    open System
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns

    // Having int[] will allow us to swap es and l in PropertySet builder for testing.
    type Foo (array:int[]) =
        member t.Item with get (index:int) = array.[index]
                      and set (index:int) (value:int) = do array.[index] <- value

                      
    let testExprPropertySet () =
        let foo = new Foo([|0..4|])
        let expr = <@do foo.[2] <- 0@>

        //printfn "%A" expr

        // let's rebuild expr ourself and bind it to bexpr.
        let bexpr =
            match expr with
            | PropertySet (inst, pi, l, es) ->
                match inst with
                | Some(e) ->
                    Expr.PropertySet(e, pi, es, l) // swaping params 2 and 3 e.g. (e, pi, l.[0], [es]) yield to OK
                | _ -> failwith ""
            | _ -> failwith ""   

        //printfn "%A" bexpr

        let result = bexpr.Equals(expr)
        if result then printfn "Test OK."
        else printfn "Test KO."

    do testExprPropertySet ()



module QuotationsOfLocalFunctions_FSharp_1_0_6403 =

    type C() = 
        let f1 (x:int) = 1
        let f2 (x:'T) = 1
        let f3 (x:byref<int>) = 1

        [<ReflectedDefinition>]
        let rd1 x = f1 x 

        [<ReflectedDefinition>]
        let rd2 x = f2 x 

        // not allowed - byrefs in quotations: 
        // [<ReflectedDefinition>]
        //let rd3 (x:byref<int>) = f3 (&x) 

        let q1 x = <@ f1 x  @>
        let q2 x = <@ f2 x  @>
        // not allowed - byrefs in quotations: 
        // let q3 (x:byref<int>) = <@ f3 (&x)  @>

        static let sf1 (x:int) = 1
        static let sf2 (x:'T) = 1
        static let sf3 (x:byref<int>) = 1

        [<ReflectedDefinition>]
        static let srd1 x = sf1 x 

        [<ReflectedDefinition>]
        static let srd2 x = sf2 x 

        // not allowed - byrefs in quotations: 
        // [<ReflectedDefinition>]
        //let rd3 (x:byref<int>) = f3 (&x) 

        static let sq1 x = <@ sf1 x  @>
        static let sq2 x = <@ sf2 x  @>

        static let mutable sfield1 = 1

        static member SQ1 = sq1 1
        static member SQ2 = sq2 1

        static member SQ3 v = <@ sf1 v @>
        static member SQ4 v = <@ sf2 v @>

        static member SQ5 = <@ sf1 1 @>
        static member SQ6 = <@ sf2 2 @>

        static member SQ7 v = <@ srd1 v @>
        static member SQ8 v = <@ srd2 v @>

        // not allowed - byrefs in quotations: 
        //static member SQ3 = q3 (&field1)

        static member SRD1 = Quotations.Expr.TryGetReflectedDefinition (match C.SQ7 3 with Quotations.Patterns.Call(_,mi,args) -> mi | _ -> failwith "method info not found")
        static member SRD2 = Quotations.Expr.TryGetReflectedDefinition (match C.SQ8 4 with Quotations.Patterns.Call(_,mi,args) -> mi | _ -> failwith "method info not found")

        member self.Q1 = q1 1
        member self.Q2 = q2 1

        member self.Q3 v = <@ f1 v @>
        member self.Q4 v = <@ f2 v @>

        member self.Q5 = <@ f1 1 @>
        member self.Q6 = <@ f2 2 @>

        member self.Q7 v = <@ rd1 v @>
        member self.Q8 v = <@ rd2 v @>

        // not allowed - byrefs in quotations: 
        //member self.Q3 = q3 (&field1)

        member self.RD1 = Quotations.Expr.TryGetReflectedDefinition (match self.Q7 3 with Quotations.Patterns.Call(_,mi,args) -> mi | _ -> failwith "method info not found")
        member self.RD2 = Quotations.Expr.TryGetReflectedDefinition (match self.Q8 4 with Quotations.Patterns.Call(_,mi,args) -> mi | _ -> failwith "method info not found")

    let test() =
        let c = C()
        printfn "c.Q1 = %A" c.Q1
        printfn "c.Q2 = %A" c.Q2
        printfn "c.Q3 = %A" (c.Q3 3)
        printfn "c.Q4 = %A" (c.Q4 4)
        printfn "c.Q5 = %A" c.Q5
        printfn "c.Q6 = %A" c.Q6
        printfn "c.Q7 = %A" (c.Q7 7)
        printfn "c.Q8 = %A" (c.Q8 8)
        printfn "c.RD1 = %A" c.RD1
        printfn "c.RD2 = %A" c.RD2

        printfn "C.SQ1 = %A" C.SQ1
        printfn "C.SQ2 = %A" C.SQ2
        printfn "C.SQ3 = %A" (C.SQ3 3)
        printfn "C.SQ4 = %A" (C.SQ4 4)
        printfn "C.SQ5 = %A" C.SQ5
        printfn "C.SQ6 = %A" C.SQ6
        printfn "C.SQ7 = %A" (C.SQ7 7)
        printfn "C.SQ8 = %A" (C.SQ8 8)
        printfn "C.SRD1 = %A" C.SRD1
        printfn "C.SRD2 = %A" C.SRD2

        test "cejnewoui1" (match c.Q1 with Call (Some (Value _),_, [(Int32 1)]) -> true | _ -> false)
        test "cejnewoui2" (match c.Q2 with Call (Some (Value _),_, [(Int32 1)]) -> true | _ -> false)
        test "cejnewoui3" (match (c.Q3 3) with Call (Some (Value _),_, [(Int32 3)]) -> true | _ -> false)
        test "cejnewoui4" (match (c.Q4 4) with Call (Some (Value _),_, [(Int32 4)]) -> true | _ -> false)

        // Note for the cases below: We still get temporaries introduced in some quotations, 
        // e.g. Q5 and Q6. The introduction of temporaries is OK according to our V2.0 specification, 
        // where compilation of some calls, pattern matching etc. may introduce temporaries. It’s not 
        // totally ideal: we would prefer if Q5 and Q6 reported “call”  quotations, and would be willing 
        // to make that breaking change at a later date.
        test "cejnewoui5" (match c.Q5 with Let(_, (Int32 1), Call (Some (Value _),_, [_])) -> true | _ -> false)
        test "cejnewoui6" (match c.Q6 with Let(_, (Int32 2), Call (Some (Value _),_, [_])) -> true | _ -> false)

        test "cejnewoui7" (match c.Q7 7 with Call (Some (Value _),_, [(Int32 7)]) -> true | _ -> false)
        test "cejnewoui8" (match c.Q8 8 with Call (Some (Value _),_, [(Int32 8)]) -> true | _ -> false)
        test "cejnewouiRD1" (match c.RD1 with Some(Lambda(_, Lambda(_, Call (Some _,_, [_])))) -> true | _ -> false)
        test "cejnewouiRD2" (match c.RD2 with Some(Lambda(_, Lambda(_, Call (Some _,_, [_])))) -> true | _ -> false)

        test "scejnewoui1" (match C.SQ1 with Call (None,_, [(Int32 1)]) -> true | _ -> false)
        test "scejnewoui2" (match C.SQ2 with Call (None,_, [(Int32 1)]) -> true | _ -> false)
        test "scejnewoui3" (match C.SQ3 3 with Call (None,_, [(Int32 3)]) -> true | _ -> false)
        test "scejnewoui4" (match C.SQ4 4 with Call (None,_, [(Int32 4)]) -> true | _ -> false)

        // Note for the cases below: We still get temporaries introduced in some quotations, 
        // e.g. Q5 and Q6. The introduction of temporaries is OK according to our V2.0 specification, 
        // where compilation of some calls, pattern matching etc. may introduce temporaries. It’s not 
        // totally ideal: we would prefer if Q5 and Q6 reported “call”  quotations, and would be willing 
        // to make that breaking change at a later date.
        test "scejnewoui5" (match C.SQ5 with Let(_, (Int32 1), Call (None,_, [_])) -> true | _ -> false)
        test "scejnewoui6" (match C.SQ6 with Let(_, (Int32 2), Call (None,_, [_])) -> true | _ -> false)

        test "scejnewoui7" (match C.SQ7 7 with Call (None,_, [(Int32 7)]) -> true | _ -> false)
        test "scejnewoui8" (match C.SQ8 8 with Call (None,_, [(Int32 8)]) -> true | _ -> false)
        test "scejnewouiRD1" (match C.SRD1 with Some(Lambda(_, Call (None,_, [_]))) -> true | _ -> false)
        test "scejnewouiRD2" (match C.SRD2 with Some(Lambda(_, Call (None,_, [_]))) -> true | _ -> false)
    test()

    (*
        printfn "c.Q1 = %A" c.Q1
        printfn "c.Q2 = %A" c.Q2
        printfn "c.Q3 = %A" (c.Q3 3)
        printfn "c.Q4 = %A" (c.Q4 4)
        printfn "c.Q5 = %A" c.Q5
        printfn "c.Q6 = %A" c.Q6
        printfn "c.Q7 = %A" c.Q7
        printfn "c.Q8 = %A" c.Q8
        printfn "c.RD1 = %A" c.RD1
        printfn "c.RD2 = %A" c.RD2

        printfn "C.SQ1 = %A" C.SQ1
        printfn "C.SQ2 = %A" C.SQ2
        printfn "C.SQ3 = %A" (C.SQ3 3)
        printfn "C.SQ4 = %A" (C.SQ4 4)
        printfn "C.SQ5 = %A" C.SQ5
        printfn "C.SQ6 = %A" C.SQ6
        printfn "C.SQ7 = %A" C.SQ7
        printfn "C.SQ8 = %A" C.SQ8
        printfn "C.SRD1 = %A" C.SRD1
        printfn "C.SRD2 = %A" C.SRD2
    *)

module OverloadsInTypeExtensions =
    module A = 
        type X = X

    module P = Microsoft.FSharp.Quotations.Patterns

    let test caption (q : Microsoft.FSharp.Quotations.Expr<'a>) (expected : 'a) =
        let (P.Call(None, mi, args)) = q
        let args = 
            [| 
                for arg in args do
                    match arg with
                    | P.Value(v, _) -> yield v
                    | P.Coerce(P.Value(v, _), toTy) -> yield System.Convert.ChangeType(v, toTy, null)
            |]
        let actual = mi.Invoke(null, args) :?> 'a
        check caption actual expected

    module Overloads =
        type A.X with
            member private this.F(_ : 'a) = 1 
            member private this.F(_ : string) = 2

            member this.F2(_ : 'a, _ : bool) = 3
            member this.F2(_ : obj, _ : string) = 4

            member this.F3(_ : string, _ : int) = 5
            member this.F3(_ : int, _ : obj) = 6 

            member this.TestOverloads() = 
                test "Overloads_1" <@ this.F(5)@> 1
                test "Overloads_2" <@ this.F("")@> 2
                test "Overloads_3" <@ this.F2(2, true) @> 3
                test "Overloads_4" <@ this.F2(2, "2") @> 4
                test "Overloads_5" <@ this.F3(2, true) @> 6
                test "Overloads_6" <@ this.F3(2, "2") @> 6
                test "Overloads_7" <@ this.F3("2", 2) @> 5
                true
    
    open Overloads

    check "OverloadsInTypeExtensions" (try A.X.TestOverloads() with _ -> false) true

module ArrayQuoteTests = 
    check "cenwkjen" (match <@ [| 2.0;3.0;4.0 |] @> with NewArray (ty, [Double 2.0; Double 3.0; Double 4.0]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2;3;4 |] @> with NewArray (ty, [Int32 2; Int32 3; Int32 4]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2u;3u;4u |] @> with NewArray (ty, [UInt32 2u; UInt32 3u; UInt32 4u]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2s;3s;4s |] @> with NewArray (ty, [Int16 2s; Int16 3s; Int16 4s]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2UL;3UL;4UL |] @> with NewArray (ty, [UInt64 2UL; UInt64 3UL; UInt64 4UL]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2L;3L;4L |] @> with NewArray (ty, [Int64 2L; Int64 3L; Int64 4L]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2us;3us;4us |] @> with NewArray (ty, [UInt16 2us; UInt16 3us; UInt16 4us]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2y;3y;4y |] @> with NewArray (ty, [SByte 2y; SByte 3y; SByte 4y]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ [| 2uy;3uy;4uy |] @> with NewArray (ty, [Byte 2uy; Byte 3uy; Byte 4uy]) -> true  | _ -> false) true
    check "cenwkjen" (match <@ "abc"B @> with NewArray (ty, [Byte 97uy; Byte 98uy; Byte 99uy]) -> true  | _ -> false) true

module ReflectedDefinitionOnTypesWithImplicitCodeGen = 
    
   [<ReflectedDefinition>]
   module M = 
      // This type has an implicit IComparable implementation, it is not accessible as a reflected definition
      type R = { x:int; y:string; z:System.DateTime }
#if NETCOREAPP
      for m in typeof<R>.GetMethods() do 
#else
      for m in typeof<R>.GetMethods(System.Reflection.BindingFlags.DeclaredOnly) do 
#endif
          check "celnwer32" (Quotations.Expr.TryGetReflectedDefinition(m).IsNone) true

      // This type has an implicit IComparable implementation, it is not accessible as a reflected definition
      type U = A of int | B of string | C of System.DateTime 
      for m in typeof<U>.GetMethods(System.Reflection.BindingFlags.DeclaredOnly) do 
          check "celnwer33" (Quotations.Expr.TryGetReflectedDefinition(m).IsNone) true

      // This type has some implicit codegen
      exception X of string * int
      for m in typeof<X>.GetMethods(System.Reflection.BindingFlags.DeclaredOnly) do 
          check "celnwer34" (Quotations.Expr.TryGetReflectedDefinition(m).IsNone) true

      // This type has an implicit IComparable implementation, it is not accessible as a reflected definition
      [<Struct>] type SR = { x:int; y:string; z:System.DateTime }
      for m in typeof<SR>.GetMethods(System.Reflection.BindingFlags.DeclaredOnly) do 
          check "celnwer35" (Quotations.Expr.TryGetReflectedDefinition(m).IsNone) true

#if !NETCOREAPP
module BasicUsingTEsts = 
    let q1() = 
      let a = ResizeArray<_>()
      for i in a do ignore i 
    let q2() = 
      use a = new System.Drawing.Bitmap(10,10) in 10 
    let q3() = 
      use a:  System.Drawing.Bitmap = null in 10
    let q4(x : #System.IDisposable) = 
      use a = x in 10
    let q5(x : ('T :> System.IDisposable)) = 
      use a : 'T = null in 10
 
    q1()
    q2()
    q3()
    q4 (new System.Drawing.Bitmap(10,10))
    q4 (ResizeArray<_>().GetEnumerator())
    q4 (null)

    q5 (new System.Drawing.Bitmap(10,10))
    q5 (null)

module QuotationOfBitmapDIsposal = 
    // Quotation of something which does a "use" on a sealed type
    let q = 
      <@ use a = new System.Drawing.Bitmap(10,10) in 10 @>

    test "Test920236a"
        (match q with
         |    Let(e,NewObject _, 
                  (TryFinally 
                    (Value _,
                     IfThenElse(TypeTest(Coerce(_,typObj), typDisposable),
                                Call(Some(Call(None, unboxGeneric, [Coerce(e3,typObj2)])),disposeMI, []), Value _)
                  ))
                 )               
                 when typDisposable.FullName = "System.IDisposable" &&
                      unboxGeneric.Name = "UnboxGeneric"
                 -> true
         |    _ -> false)
#endif

module ReflectedDefinitionAndSelfIdentifier = 
    [<ReflectedDefinition>]
    type T() as _selfReference = 
        member this.Property = 1

    test "ReflectedDefinitionAndSelfIdentifier"
        (
            let m = typeof<T>.GetMethod("get_Property")
            match Expr.TryGetReflectedDefinition m with
            | None -> false
            | Some e -> 
                match e with
                | Patterns.Lambda
                    (
                        thisVar,
                        Patterns.Lambda
                            (
                                unitVar,
                                TypedValue 1
                            )
                    )  -> thisVar.Type = typeof<T> && unitVar.Type = typeof<unit>
                | _ -> false
        )

module LoopsOverArraysInQuotations =

    test "LoopsOverArraysInQuotations1"
        (
            <@ for x in [|1;2|] do ignore () @>
            |> 
            function
            | Patterns.Let
              (
                    arr, 
                    Patterns.NewArray(IntTy, [TypedValue 1; TypedValue 2]), 
                    Patterns.ForIntegerRangeLoop(
                        idx, 
                        TypedValue 0, 
                        SpecificCall <@ (-) @> (None, [IntTy; IntTy; IntTy], [ SpecificCall <@ Array.length @>(None, [IntTy], _); TypedValue 1]), 
                        Patterns.Let(
                            forLoopVar,
                            SpecificCall <@ LanguagePrimitives.IntrinsicFunctions.GetArray @>(None, _, _),
                            SpecificCall <@ ignore @>(None, _, _)
                        )
                    )
                ) -> true
            | _ -> false
        )
    test "LoopsOverArraysInQuotations2"
        (
            <@ for (x,y) in [|1, ""|] do ignore x @>
            |> 
            function
            | Patterns.Let
                (
                    arr, 
                    Patterns.NewArray(TupleTy(IntTy, StringTy), [Patterns.NewTuple([TypedValue 1; TypedValue ""])]), 
                    Patterns.ForIntegerRangeLoop(
                        idx1, 
                        TypedValue 0,
                        SpecificCall <@ (-) @> (None, [IntTy; IntTy; IntTy], [ SpecificCall <@ Array.length @>(None, [TupleTy(IntTy, StringTy)], _); TypedValue 1]), 
                        Patterns.Let(
                            forLoopVar,
                            SpecificCall <@ LanguagePrimitives.IntrinsicFunctions.GetArray @>(None, _, [Patterns.Var(arr2); Patterns.Var idx2]),
                            Patterns.Let(
                                y,
                                Patterns.TupleGet(_, 1),
                                Patterns.Let(
                                    x,
                                    Patterns.TupleGet(_, 0),
                                    SpecificCall <@ ignore : 'T -> unit @>(None, _, [Patterns.Var(x2)])
                                )
                            )
                        )
                    )
                ) -> arr = arr2 && idx1 = idx2 && x = x2
            | _ -> false        
        )
    test "LoopsOverArraysInQuotations3"
        (
            <@ for (x,y) in [||] do () @>
            |> 
            function
            | Patterns.Let
                (
                    arr, 
                    Patterns.NewArray(TupleTy(ObjTy, ObjTy), []), 
                    Patterns.ForIntegerRangeLoop(
                        idx1, 
                        TypedValue 0,
                        SpecificCall <@ (-) @> (None, [IntTy; IntTy; IntTy], [ SpecificCall <@ Array.length @>(None, [TupleTy(ObjTy, ObjTy)], _); TypedValue 1]), 
                        Patterns.Let(
                            forLoopVar,
                            SpecificCall <@ LanguagePrimitives.IntrinsicFunctions.GetArray @>(None, _, [Patterns.Var(arr2); Patterns.Var idx2]),
                            _
                        )
                    )
                ) -> arr = arr2 && idx1 = idx2
            | _ -> false        
        )

module QuotationOfResizeArrayIteration = 
    // Quotation of an iteration which implictly does a "use" on a value of struct type
    let q = 
      let a = ResizeArray<_>()
      <@ for i in a do ignore i @>

    test "Test920236b"
        (match q with
         |    Let(e,Call(Some (Value a), mi, []), 
                  (TryFinally 
                    (WhileLoop((Call (Some e1, moveNext, [])),
                         Let(i,
                             PropertyGet ((Some e2),piCurrent,[]),
                             Call(None,_,[_]) // ignore
                         )
                    ),
                    Call(Some(e3),disposeMI, []))
                  ))
                 when mi.Name = "GetEnumerator" &&
                      moveNext.Name = "MoveNext" && 
                      piCurrent.Name = "Current" &&
                      disposeMI.Name = "Dispose"
                 -> true
         |    _ -> false)
        


#if !FSHARP_CORE_31
module TestAutoQuoteAtStaticMethodCalls = 
    open Microsoft.FSharp.Quotations

    type C() = 
        static let cleanup (s:string) = s.Replace(" ","").Replace("\n","").Replace("\r","")
        static member Plot ([<ReflectedDefinition>] x: Expr<'T>) = 
            sprintf "%A" x |> cleanup

        static member PlotTwoArg ([<ReflectedDefinition>] x: Expr<'T>, y : int) = 
            sprintf "%A" (x,y) |> cleanup

        static member PlotThreeArg (w:int, [<ReflectedDefinition>] x: Expr<'T>, y : int) = 
            sprintf "%A" (w,x,y) |> cleanup

        static member PlotParams ([<ReflectedDefinition; System.ParamArray>] x: Expr<int>[]) = 
            sprintf "%A" x |> cleanup

        static member PlotEval ([<ReflectedDefinition(true)>] x: Expr<'T>) = 
            sprintf "%A" x |> cleanup


    let shouldEqual  id x y = check id y x
    let x = 1
    let y = 1
    let xb = true
    let yb = true
    let testItAll() = 
        let z = 1
        let zb = true

        C.Plot (xb && yb || zb)  |> shouldEqual "testd109700" "IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb))"

        C.Plot (x + y * z) |> shouldEqual "testd109701" "Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])])"

        C.PlotTwoArg (x + y * z, 108) |> shouldEqual "testd109703" "(Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])]),108)"

        C.PlotThreeArg (107, x + y * z, 108)|> shouldEqual "testd109704" "(107,Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])]),108)"

        C.PlotParams (1, 2) |> shouldEqual "testd109708" "[|Value(1);Value(2)|]"

        C.PlotParams (x + y) |> shouldEqual "testd109709" "[|Call(None,op_Addition,[PropertyGet(None,x,[]),PropertyGet(None,y,[])])|]"

        C.Plot (fun (x,y,z) -> xb && yb || zb) |> shouldEqual "testd10970F" "Lambda(tupledArg,Let(x,TupleGet(tupledArg,0),Let(y,TupleGet(tupledArg,1),Let(z,TupleGet(tupledArg,2),IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb))))))"

        C.Plot (fun x -> x) |> shouldEqual "testd109710" "Lambda(x,x)"

        C.Plot (fun x -> x, x+1)  |> shouldEqual "testd109711" "Lambda(x,NewTuple(x,Call(None,op_Addition,[x,Value(1)])))"

        C.PlotEval (xb && yb || zb) |> shouldEqual "testd109712" "WithValue(true,IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb)))"

    testItAll()

module TestAutoQuoteAtInstanceMethodCalls = 
    open Microsoft.FSharp.Quotations
    open System.Runtime.CompilerServices


    type C() = 
        let cleanup (s:string) = s.Replace(" ","").Replace("\n","").Replace("\r","")
        member __.Plot ([<ReflectedDefinition>] x: Expr<'T>) = 
            sprintf "%A" x |> cleanup

        member __.PlotTwoArg ([<ReflectedDefinition>] x: Expr<'T>, y : int) = 
            sprintf "%A" (x,y) |> cleanup

        member __.PlotThreeArg (w:int, [<ReflectedDefinition>] x: Expr<'T>, y : int) = 
            sprintf "%A" (w,x,y) |> cleanup

        member __.PlotParams ([<ReflectedDefinition; System.ParamArray>] x: Expr<int>[]) = 
            sprintf "%A" x |> cleanup

        member __.Item ([<ReflectedDefinition>] x: Expr<'T>) = 
            sprintf "%A" x |> cleanup

        member __.PlotEval ([<ReflectedDefinition(true)>] x: Expr<'T>) = 
            sprintf "%A" x |> cleanup

        override __.ToString() = "C"

    [<Extension>]
    module CSharpStyleExtensionMember = 
        let cleanup (s:string) = s.Replace(" ","").Replace("\n","").Replace("\r","")
        [<Extension>]
        type CExtMem() = 
            [<Extension>]
            static member PlotCSharpStyleExtMem (this: C, [<ReflectedDefinition>] x: Expr<'T>) = 
                sprintf "%A" x |> cleanup

            // Adding 'ReflectedDefinition' to an argument that doesn't have type Expr<'T> is ignored (no error or warning is given at declaration or use)
            [<Extension>]
            static member PlotCSharpStyleExtMemNoExpr (this: C, [<ReflectedDefinition>] x: 'T) = 
                sprintf "%A" x |> cleanup

            // Adding 'ReflectedDefinition' to the 'this' argument of a C#-style extension member is ignored. 
            //
            //[<Extension>]
            //static member PlotCSharpStyleExtMemWithReflectedThis ([<ReflectedDefinition>] this: Expr<C>, [<ReflectedDefinition>] x: Expr<'T>) = 
            //    sprintf "%A" (this, x) |> cleanup
            [<Extension>]
            static member PlotCSharpStyleExtMemWithIgnoredReflectedThis ([<ReflectedDefinition>] this: C, [<ReflectedDefinition>] x: Expr<'T>) = 
                sprintf "%A" (this, x) |> cleanup

    open CSharpStyleExtensionMember
    let shouldEqual  id x y = check id y x
    let x = 1
    let y = 1
    let xb = true
    let yb = true
    let testItAll() = 
        let z = 1
        let zb = true
        let c = C()

        c.Plot (xb && yb || zb)  |> shouldEqual "testd109700" "IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb))"

        c.Plot (x + y * z) |> shouldEqual "testd109701" "Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])])"

        c.[x + y * z] |> shouldEqual "testd109701" "Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])])"

        c.PlotTwoArg (x + y * z, 108) |> shouldEqual "testd109703" "(Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])]),108)"

        c.PlotThreeArg (107, x + y * z, 108)|> shouldEqual "testd109704" "(107,Call(None,op_Addition,[PropertyGet(None,x,[]),Call(None,op_Multiply,[PropertyGet(None,y,[]),ValueWithName(1,z)])]),108)"

        c.PlotParams (1, 2) |> shouldEqual "testd109708" "[|Value(1);Value(2)|]"

        c.PlotParams (x + y) |> shouldEqual "testd109709" "[|Call(None,op_Addition,[PropertyGet(None,x,[]),PropertyGet(None,y,[])])|]"

        c.Plot (fun (x,y,z) -> xb && yb || zb) |> shouldEqual "testd10970F" "Lambda(tupledArg,Let(x,TupleGet(tupledArg,0),Let(y,TupleGet(tupledArg,1),Let(z,TupleGet(tupledArg,2),IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb))))))"

        c.Plot (fun x -> x) |> shouldEqual "testd109710" "Lambda(x,x)"

        c.Plot (fun x -> x, x+1)  |> shouldEqual "testd109711" "Lambda(x,NewTuple(x,Call(None,op_Addition,[x,Value(1)])))"

        c.PlotEval (xb && yb || zb) |> shouldEqual "testd109712" "WithValue(true,IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb)))"

        c.PlotCSharpStyleExtMem (xb && yb || zb)  |> shouldEqual "testd109713" "IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb))"

        c.PlotCSharpStyleExtMemNoExpr (xb && yb || zb)  |> shouldEqual "testdoqhwm" "true"

        c.PlotCSharpStyleExtMemWithIgnoredReflectedThis (xb && yb || zb)  |> shouldEqual "testd109714" "(C,IfThenElse(IfThenElse(PropertyGet(None,xb,[]),PropertyGet(None,yb,[]),Value(false)),Value(true),ValueWithName(true,zb)))"

    testItAll()

module TestsForUsingReflectedDefinitionArgumentsAsFirstClassValues = 
    open Microsoft.FSharp.Quotations
    open System.Linq.Expressions
    open System

    type FirstClassTests() = 
        static member PlotExpr ([<ReflectedDefinition>] x: Expr<'T>) = x.ToString()
        static member PlotExprOverloadedByType ([<ReflectedDefinition>] x: Expr<int>) = x.ToString()
        static member PlotExprOverloadedByType ([<ReflectedDefinition>] x: Expr<string>) = x.ToString()
        static member PlotExprOverloadedByShape (x:int) = x.ToString()
        static member PlotExprOverloadedByShape ([<ReflectedDefinition>] x: Expr<int>) = x.ToString()
        static member PlotLinq (x: Expression<Func<int,'T>>) =  x.ToString()
        static member PlotLinqOverloadedByType (x: Expression<Func<int,'T>>) =  x.ToString()
        static member PlotLinqOverloadedByType (x: Expression<Func<string,'T>>) =  x.ToString()
        static member PlotLinqOverloadedByShape (x: Func<int,'T>) =  x.ToString()
        static member PlotLinqOverloadedByShape (x: Expression<Func<int,'T>>) =  x.ToString()

    // Most of the following tests are just checking that overloads are resolved correctly
    let runAll() = 

        // Check we can define a function that calls the overloads
        let callLinqWithoutAutoConv (ef: Expression<Func<int,int>>) = FirstClassTests.PlotLinq ef     
        let callLinqWithAutoConv (f: int -> int) = FirstClassTests.PlotLinq (fun x -> f x)     // needs eta-expansion
        let callLinqOverloadedByTypeWithoutAutoConvInt (ef: Expression<Func<int,int>>) = FirstClassTests.PlotLinqOverloadedByType ef     
        let callLinqOverloadedByTypeWithoutAutoConvString (ef: Expression<Func<string,int>>) = FirstClassTests.PlotLinqOverloadedByType ef     
        let callLinqOverloadedByTypeWithAutoConvInt (f: int -> int) = FirstClassTests.PlotLinqOverloadedByType (fun x -> f x)   
        let callLinqOverloadedByTypeWithAutoConvString (f: string -> int) = FirstClassTests.PlotLinqOverloadedByType (fun x -> f x)   
        let callLinqOverloadedByShapeWithoutAutoConv (ef: Expression<Func<int,int>>) = FirstClassTests.PlotLinqOverloadedByShape ef     
        let callExprWithoutAutoConv (ef: Expr<int>) = FirstClassTests.PlotExpr <@ %ef @>
        let callExprWithAutoConv (ef: int) = FirstClassTests.PlotExpr ef     
        let callExprOverloadedWithoutAutoConvA (ef: Expr<int>) = FirstClassTests.PlotExprOverloadedByType <@ %ef @>
        let callExprOverloadedWithoutAutoConvB (ef: Expr<int>) = FirstClassTests.PlotExprOverloadedByType ef
        let callExprOverloadedWithAutoConv (ef: int) = FirstClassTests.PlotExprOverloadedByType ef     
        let callExprOverloadedByShapeWithoutAutoConvA (ef: Expr<int>) = FirstClassTests.PlotExprOverloadedByShape <@ %ef @>
        let callExprOverloadedByShapeWithoutAutoConvB (ef: Expr<int>) = FirstClassTests.PlotExprOverloadedByShape ef
        // EXPECTED OVERLOAD RESOLUTION FAILURE: let callLinqOverloadedByShapeWithAutoConv (f: int -> int) = C.PlotLinqOverloadedByShape (fun x -> f x)    // overload not resolved
        // EXPECTED OVERLOAD RESOLUTION FAILURE: let callExprOverloadedByShapeWithAutoConv (ef: int) = C.PlotExprOverloadedByShape ef      // overload not resolved

        // Check type-checking for type-annotated first-class function values
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinq : (int -> int) -> string)     // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinq : Expression<Func<int,int>> -> string)     
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinqOverloadedByType : (int -> int) -> string)     // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinqOverloadedByType : (int -> string) -> string)     // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinqOverloadedByType : Expression<Func<int,int>> -> string)     
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinqOverloadedByShape : Expression<Func<int,int>> -> string)    
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotLinqOverloadedByShape : Func<int,int> -> string)     // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExpr : Expr<int> -> string)     
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExpr : int -> string)           // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExprOverloadedByType : Expr<int> -> string)     
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExprOverloadedByType : int -> string)           // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExprOverloadedByType : string -> string)           // auto-quotes implicit var - though not very useful
        let _unusedFirstClassFunctionValue = (FirstClassTests.PlotExprOverloadedByShape : Expr<int> -> string)     
        // EXPECTED OVERLOAD RESOLUTION FAILURE: (C.PlotLinqOverloadedByShape : (int -> int) -> string)     // overload not resolved
        // EXPECTED OVERLOAD RESOLUTION FAILURE: (C.PlotExprOverloadedByShape : int -> string)           // overload not resolved


        // Check type-checking for applications
        let _unusedResultValue = FirstClassTests.PlotExpr 1
        let _unusedResultValue = FirstClassTests.PlotExpr <@ 1 @>
        let _unusedResultValue = FirstClassTests.PlotExprOverloadedByType 1
        let _unusedResultValue = FirstClassTests.PlotExprOverloadedByType <@ 1 @>
        let _unusedResultValue = FirstClassTests.PlotExprOverloadedByType "a"
        let _unusedResultValue = FirstClassTests.PlotExprOverloadedByType <@ "a" @>
        let _unusedResultValue = FirstClassTests.PlotExprOverloadedByShape <@ 1 @>
        // EXPECTED OVERLOAD RESOLUTION FAILURE: let _unusedResultValue = FirstClassTests.PlotLinqOverloadedByShape (fun x -> x)
        // EXPECTED OVERLOAD RESOLUTION FAILURE: let _unusedResultValue = FirstClassTests.PlotExprOverloadedByShape 1  // overload not resolved


        // Check type-checking for pipelining 
        let _unusedResultValue = 1 |> FirstClassTests.PlotExpr
        let _unusedResultValue = <@ 1 @> |> FirstClassTests.PlotExpr
        let _unusedResultValue = 1 |> FirstClassTests.PlotExprOverloadedByType
        let _unusedResultValue = <@ 1 @> |> FirstClassTests.PlotExprOverloadedByType
        let _unusedResultValue = "a" |> FirstClassTests.PlotExprOverloadedByType
        let _unusedResultValue = <@ "a" @> |> FirstClassTests.PlotExprOverloadedByType
        let _unusedResultValue = <@ 1 @> |> FirstClassTests.PlotExprOverloadedByShape
        // EXPECTED OVERLOAD RESOLUTION FAILURE: 1 |> FirstClassTests.PlotExprOverloadedByShape // overload not resolved

        ()

    runAll()


module NestedQuotations = 
    open Microsoft.FSharp.Quotations
    open System.Linq.Expressions
    open System

    let unnested1 = <@ 100 @> 
    let unnested2 = <@@ 100 @@> 
    let nested1 = <@@ <@ 100 @> @@>   
    let nested2 = <@@ <@@ 100 @@> @@> 
    let nested3 = <@ <@ 100 @> @>     
    let nested4 = <@ <@@ 100 @@> @>   

    let runAll() = 
        test "lfhwwlefkhelw-1a" (match nested1 with  Quote _ -> true | _ -> false)
        test "lfhwwlefkhelw-1b" (match nested1 with  QuoteTyped _ -> true | _ -> false)
        test "lfhwwlefkhelw-1c" (match nested1 with  QuoteRaw _ -> false | _ -> true)
        test "lfhwwlefkhelw-2a" (match nested2 with  Quote _ -> true | _ -> false)
        test "lfhwwlefkhelw-2b" (match nested2 with  QuoteTyped _ -> false | _ -> true)
        test "lfhwwlefkhelw-2c" (match nested2 with  QuoteRaw _ -> true | _ -> false)
        test "lfhwwlefkhelw-3a" (match nested3 with  Quote _ -> true | _ -> false)
        test "lfhwwlefkhelw-3b" (match nested3 with  QuoteTyped _ -> true | _ -> true)
        test "lfhwwlefkhelw-3c" (match nested3 with  QuoteRaw _ -> false | _ -> true)
        test "lfhwwlefkhelw-4a" (match nested4 with  Quote _ -> true | _ -> false)
        test "lfhwwlefkhelw-4b" (match nested4 with  QuoteRaw _ -> true | _ -> false)
        test "lfhwwlefkhelw-4c" (match nested4 with  QuoteTyped _ -> false | _ -> true)
        test "clenewjclkw-1" (match Expr.Quote unnested1 with  QuoteTyped _ -> true | _ -> false)
        test "clenewjclkw-2" (match Expr.QuoteRaw unnested1 with  QuoteRaw _ -> true | _ -> false)
        test "clenewjclkw-3" (match Expr.Quote unnested2 with  QuoteTyped _ -> true | _ -> false)
        test "clenewjclkw-4" (match Expr.QuoteRaw unnested2 with  QuoteRaw _ -> true | _ -> false)
        test "clenewjclkw-5" (unnested1.Type = typeof<int>)
        test "clenewjclkw-6" (unnested2.Type = typeof<int>)
        test "clenewjclkw-7" (Expr.Quote(unnested1).Type = typeof<Expr<int>>)
        test "clenewjclkw-8" (Expr.Quote(unnested2).Type = typeof<Expr<int>>)
        test "clenewjclkw-9" (Expr.QuoteTyped(unnested1).Type = typeof<Expr<int>>)
        test "clenewjclkw-10" (Expr.QuoteTyped(unnested2).Type = typeof<Expr<int>>)
        test "clenewjclkw-11" (Expr.QuoteRaw(unnested1).Type = typeof<Expr>)
        test "clenewjclkw-12" (Expr.QuoteRaw(unnested2).Type = typeof<Expr>)

    runAll()

module ExtensionMembersWithSameName = 

    type System.Object with
        [<ReflectedDefinition>]
        member this.Add(x) = x
        [<ReflectedDefinition>]
        member this.Add(x, y) = x + y
        [<ReflectedDefinition>]
        static member SAdd(x) = x
        [<ReflectedDefinition>]
        static member SAdd(x, y) = x + y

    let runAll () =
        match  <@ obj().Add(2) @> with
        | (Patterns.Call(_, m, _)) -> 
            let text = m |> Expr.TryGetReflectedDefinition |> sprintf "%A"
            check "clewwenf094" text "Some Lambda (this, Lambda (x, x))"
        | _ -> failwith "unexpected shape"

        match  <@ obj().Add(2,3) @> with
        | (Patterns.Call(_, m, _)) -> 
            let text = m |> Expr.TryGetReflectedDefinition |> sprintf "%A"
            check "clewwenf095" (m.GetParameters().Length) 3
        | _ -> failwith "unexpected shape"

        match  <@ obj.SAdd(2) @> with
        | (Patterns.Call(_, m, _)) -> 
            let text = m |> Expr.TryGetReflectedDefinition |> sprintf "%A"
            check "clewwenf096" text "Some Lambda (x, x)"
        | _ -> failwith "unexpected shape"

        match  <@ obj.SAdd(2,3) @> with
        | (Patterns.Call(_, m, _)) -> 
            let text = m |> Expr.TryGetReflectedDefinition |> sprintf "%A"
            check "clewwenf097" (m.GetParameters().Length) 2
        | _ -> failwith "unexpected shape"

    runAll()
#endif

module PartialApplicationLeadToInvalidCodeWhenOptimized = 
    let f () = 
        let x = 1
        let g (y:int) (z:int) = <@ x @>
        let _ = g 3 // the closure generated by this code was invalid
        ()

    f ()


/// TEST F# REFLECTION OVER THE IMPLEMENTATION OF SYMBOL TYPES FROM THE F# TYPE PROVIDER STARTER PACK
///
module ReflectionOverTypeInstantiations = 

    open System.Collections.Generic

    let notRequired opname item = 
        let msg = sprintf "The operation '%s' on item '%s' should not be called on provided type, member or parameter" opname item
        //System.Diagnostics.Debug.Assert (false, msg)
        raise (System.NotSupportedException msg)

    /// DO NOT ADJUST THIS TYPE - it is the implementation of symbol types from the F# type provider starer pack. 
    /// This code gets included in all F# type provider implementations. We expect F# reflection to be in a good, 
    /// known state over these types.
    ///
    ///
    /// Represents the type constructor in a provided symbol type.
    [<NoComparison>]
    type ProvidedSymbolKind = 
        | SDArray 
        | Array of int 
        | Pointer 
        | ByRef 
        | Generic of System.Type 
        | FSharpTypeAbbreviation of (System.Reflection.Assembly * string * string[])


    /// DO NOT ADJUST THIS TYPE - it is the implementation of symbol types from the F# type provider starer pack. 
    /// This code gets included in all F# type provider implementations. We expect F# reflection to be in a good, 
    /// known state over these types.
    ///
    /// Represents an array or other symbolic type involving a provided type as the argument.
    /// See the type provider spec for the methods that must be implemented.
    /// Note that the type provider specification does not require us to implement pointer-equality for provided types.
    type ProvidedSymbolType(kind: ProvidedSymbolKind, args: Type list, convToTgt: Type -> Type) =
        inherit Type()

        let rec isEquivalentTo (thisTy: Type) (otherTy: Type) =
            match thisTy, otherTy with
            | (:? ProvidedSymbolType as thisTy), (:? ProvidedSymbolType as thatTy) -> (thisTy.Kind,thisTy.Args) = (thatTy.Kind, thatTy.Args)
            | (:? ProvidedSymbolType as thisTy), otherTy | otherTy, (:? ProvidedSymbolType as thisTy) ->
                match thisTy.Kind, thisTy.Args with
                | ProvidedSymbolKind.SDArray, [ty] | ProvidedSymbolKind.Array _, [ty] when otherTy.IsArray-> ty.Equals(otherTy.GetElementType())
                | ProvidedSymbolKind.ByRef, [ty] when otherTy.IsByRef -> ty.Equals(otherTy.GetElementType())
                | ProvidedSymbolKind.Pointer, [ty] when otherTy.IsPointer -> ty.Equals(otherTy.GetElementType())
                | ProvidedSymbolKind.Generic baseTy, args -> otherTy.IsGenericType && isEquivalentTo baseTy (otherTy.GetGenericTypeDefinition()) && Seq.forall2 isEquivalentTo args (otherTy.GetGenericArguments())
                | _ -> false
            | a, b -> a.Equals b

        let nameText() = 
            match kind,args with 
            | ProvidedSymbolKind.SDArray,[arg] -> arg.Name + "[]" 
            | ProvidedSymbolKind.Array _,[arg] -> arg.Name + "[*]" 
            | ProvidedSymbolKind.Pointer,[arg] -> arg.Name + "*" 
            | ProvidedSymbolKind.ByRef,[arg] -> arg.Name + "&"
            | ProvidedSymbolKind.Generic gty, args -> gty.Name + (sprintf "%A" args)
            | ProvidedSymbolKind.FSharpTypeAbbreviation (_,_,path),_ -> path.[path.Length-1]
            | _ -> failwith "unreachable"

        /// Substitute types for type variables.
        static member convType (parameters: Type list) (ty:Type) = 
            if ty = null then null
            elif ty.IsGenericType then
                let args = Array.map (ProvidedSymbolType.convType parameters) (ty.GetGenericArguments())
                ty.GetGenericTypeDefinition().MakeGenericType(args)  
            elif ty.HasElementType then 
                let ety = ProvidedSymbolType.convType parameters (ty.GetElementType()) 
                if ty.IsArray then 
                    let rank = ty.GetArrayRank()
                    if rank = 1 then ety.MakeArrayType()
                    else ety.MakeArrayType(rank)
                elif ty.IsPointer then ety.MakePointerType()
                elif ty.IsByRef then ety.MakeByRefType()
                else ty
            elif ty.IsGenericParameter then 
                if ty.GenericParameterPosition <= parameters.Length - 1 then 
                    parameters.[ty.GenericParameterPosition]
                else
                    ty
            else ty

        override __.FullName =   
            match kind,args with 
            | ProvidedSymbolKind.SDArray,[arg] -> arg.FullName + "[]" 
            | ProvidedSymbolKind.Array _,[arg] -> arg.FullName + "[*]" 
            | ProvidedSymbolKind.Pointer,[arg] -> arg.FullName + "*" 
            | ProvidedSymbolKind.ByRef,[arg] -> arg.FullName + "&"
            | ProvidedSymbolKind.Generic gty, args -> gty.FullName + "[" + (args |> List.map (fun arg -> arg.ToString()) |> String.concat ",") + "]"
            | ProvidedSymbolKind.FSharpTypeAbbreviation (_,nsp,path),args -> String.concat "." (Array.append [| nsp |] path) + (match args with [] -> "" | _ -> args.ToString())
            | _ -> failwith "unreachable"
   
        /// Although not strictly required by the type provider specification, this is required when doing basic operations like FullName on
        /// .NET symbolic types made from this type, e.g. when building Nullable<SomeProvidedType[]>.FullName
        override __.DeclaringType =                                                                 
            match kind,args with 
            | ProvidedSymbolKind.SDArray,[arg] -> arg
            | ProvidedSymbolKind.Array _,[arg] -> arg
            | ProvidedSymbolKind.Pointer,[arg] -> arg
            | ProvidedSymbolKind.ByRef,[arg] -> arg
            | ProvidedSymbolKind.Generic gty,_ -> gty
            | ProvidedSymbolKind.FSharpTypeAbbreviation _,_ -> null
            | _ -> failwith "unreachable"

        override __.IsAssignableFrom(otherTy) = 
            match kind with
            | Generic gtd ->
                if otherTy.IsGenericType then
                    let otherGtd = otherTy.GetGenericTypeDefinition()
                    let otherArgs = otherTy.GetGenericArguments()
                    let yes = gtd.Equals(otherGtd) && Seq.forall2 isEquivalentTo args otherArgs
                    yes
                    else
                        base.IsAssignableFrom(otherTy)
            | _ -> base.IsAssignableFrom(otherTy)

        override __.Name = nameText()

        override __.BaseType =
            match kind with 
            | ProvidedSymbolKind.SDArray -> convToTgt typeof<System.Array> 
            | ProvidedSymbolKind.Array _ -> convToTgt typeof<System.Array> 
            | ProvidedSymbolKind.Pointer -> convToTgt typeof<System.ValueType> 
            | ProvidedSymbolKind.ByRef -> convToTgt typeof<System.ValueType> 
            | ProvidedSymbolKind.Generic gty  ->
                if gty.BaseType = null then null else
                ProvidedSymbolType.convType args gty.BaseType
            | ProvidedSymbolKind.FSharpTypeAbbreviation _ -> convToTgt typeof<obj>  

        override __.GetArrayRank() = (match kind with ProvidedSymbolKind.Array n -> n | ProvidedSymbolKind.SDArray -> 1 | _ -> invalidOp "non-array type")
        override __.IsValueTypeImpl() = (match kind with ProvidedSymbolKind.Generic gtd -> gtd.IsValueType | _ -> false)
        override __.IsArrayImpl() = (match kind with ProvidedSymbolKind.Array _ | ProvidedSymbolKind.SDArray -> true | _ -> false)
        override __.IsByRefImpl() = (match kind with ProvidedSymbolKind.ByRef _ -> true | _ -> false)
        override __.IsPointerImpl() = (match kind with ProvidedSymbolKind.Pointer _ -> true | _ -> false)
        override __.IsPrimitiveImpl() = false
        override __.IsGenericType = (match kind with ProvidedSymbolKind.Generic _ -> true | _ -> false)
        override __.GetGenericArguments() = (match kind with ProvidedSymbolKind.Generic _ -> args |> List.toArray | _ -> invalidOp "non-generic type")
        override __.GetGenericTypeDefinition() = (match kind with ProvidedSymbolKind.Generic e -> e | _ -> invalidOp "non-generic type")
        override __.IsCOMObjectImpl() = false
        override __.HasElementTypeImpl() = (match kind with ProvidedSymbolKind.Generic _ -> false | _ -> true)
        override __.GetElementType() = (match kind,args with (ProvidedSymbolKind.Array _  | ProvidedSymbolKind.SDArray | ProvidedSymbolKind.ByRef | ProvidedSymbolKind.Pointer),[e] -> e | _ -> invalidOp "not an array, pointer or byref type")
        override this.ToString() = this.FullName

        override __.Assembly = 
            match kind with 
            | ProvidedSymbolKind.FSharpTypeAbbreviation (assembly,_nsp,_path) -> assembly
            | ProvidedSymbolKind.Generic gty -> gty.Assembly
            | _ -> notRequired "Assembly" (nameText())

        override __.Namespace = 
            match kind with 
            | ProvidedSymbolKind.FSharpTypeAbbreviation (_assembly,nsp,_path) -> nsp
            | _ -> notRequired "Namespace" (nameText())

        override __.GetHashCode()                                                                    = 
            match kind,args with 
            | ProvidedSymbolKind.SDArray,[arg] -> 10 + hash arg
            | ProvidedSymbolKind.Array _,[arg] -> 163 + hash arg
            | ProvidedSymbolKind.Pointer,[arg] -> 283 + hash arg
            | ProvidedSymbolKind.ByRef,[arg] -> 43904 + hash arg
            | ProvidedSymbolKind.Generic gty,_ -> 9797 + hash gty + List.sumBy hash args
            | ProvidedSymbolKind.FSharpTypeAbbreviation _,_ -> 3092
            | _ -> failwith "unreachable"
    
        override __.Equals(other: obj) =
            match other with
            | :? ProvidedSymbolType as otherTy -> (kind, args) = (otherTy.Kind, otherTy.Args)
            | _ -> false

        member __.Kind = kind
        member __.Args = args
    
        member __.IsFSharpTypeAbbreviation  = match kind with FSharpTypeAbbreviation _ -> true | _ -> false
        // For example, int<kg>
        member __.IsFSharpUnitAnnotated = match kind with ProvidedSymbolKind.Generic gtd -> not gtd.IsGenericTypeDefinition | _ -> false

        override __.Module : Module                                                                   = notRequired "Module" (nameText())
        override __.GetConstructors _bindingAttr                                                      = notRequired "GetConstructors" (nameText())
        override __.GetMethodImpl(_name, _bindingAttr, _binderBinder, _callConvention, _types, _modifiers) = 
            match kind with
            | Generic gtd -> 
                let ty = gtd.GetGenericTypeDefinition().MakeGenericType(Array.ofList args)
                ty.GetMethod(_name, _bindingAttr)
            | _ -> notRequired "GetMethodImpl" (nameText())
        override __.GetMembers _bindingAttr                                                           = notRequired "GetMembers" (nameText())
        override __.GetMethods _bindingAttr                                                           = notRequired "GetMethods" (nameText())
        override __.GetField(_name, _bindingAttr)                                                     = notRequired "GetField" (nameText())
        override __.GetFields _bindingAttr                                                            = notRequired "GetFields" (nameText())
        override __.GetInterface(_name, _ignoreCase)                                                  = notRequired "GetInterface" (nameText())
        override __.GetInterfaces()                                                                   = notRequired "GetInterfaces" (nameText())
        override __.GetEvent(_name, _bindingAttr)                                                     = notRequired "GetEvent" (nameText())
        override __.GetEvents _bindingAttr                                                            = notRequired "GetEvents" (nameText())
        override __.GetProperties _bindingAttr                                                        = notRequired "GetProperties" (nameText())
        override __.GetPropertyImpl(_name, _bindingAttr, _binder, _returnType, _types, _modifiers)    = notRequired "GetPropertyImpl" (nameText())
        override __.GetNestedTypes _bindingAttr                                                       = notRequired "GetNestedTypes" (nameText())
        override __.GetNestedType(_name, _bindingAttr)                                                = notRequired "GetNestedType" (nameText())
        override __.GetAttributeFlagsImpl()                                                           = notRequired "GetAttributeFlagsImpl" (nameText())
        override this.UnderlyingSystemType = 
            match kind with 
            | ProvidedSymbolKind.SDArray
            | ProvidedSymbolKind.Array _
            | ProvidedSymbolKind.Pointer
            | ProvidedSymbolKind.FSharpTypeAbbreviation _
            | ProvidedSymbolKind.ByRef -> upcast this
            | ProvidedSymbolKind.Generic gty -> gty.UnderlyingSystemType  
    #if FX_NO_CUSTOMATTRIBUTEDATA
    #else
        override __.GetCustomAttributesData()                                                        =  ([| |] :> IList<_>)
    #endif
        override __.MemberType                                                                       = notRequired "MemberType" (nameText())
        override __.GetMember(_name,_mt,_bindingAttr)                                                = notRequired "GetMember" (nameText())
        override __.GUID                                                                             = notRequired "GUID" (nameText())
        override __.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired "InvokeMember" (nameText())
        override __.AssemblyQualifiedName                                                            = notRequired "AssemblyQualifiedName" (nameText())
        override __.GetConstructorImpl(_bindingAttr, _binder, _callConvention, _types, _modifiers)   = notRequired "GetConstructorImpl" (nameText())
        override __.GetCustomAttributes(_inherit)                                                    = [| |]
        override __.GetCustomAttributes(_attributeType, _inherit)                                    = [| |]
        override __.IsDefined(_attributeType, _inherit)                                              = false
        // FSharp.Data addition: this was added to support arrays of arrays
        override this.MakeArrayType() = ProvidedSymbolType(ProvidedSymbolKind.SDArray, [this], convToTgt) :> Type
        override this.MakeArrayType arg = ProvidedSymbolType(ProvidedSymbolKind.Array arg, [this], convToTgt) :> Type



    /// DO NOT ADJUST THIS TYPE - it is the implementation of symbol types from the F# type provider starer pack. 
    /// This code gets included in all F# type provider implementations. We expect F# reflection to be in a good, 
    /// known state over these types.
    type ProvidedTypeBuilder() =
        static member MakeGenericType(genericTypeDefinition, genericArguments) = ProvidedSymbolType(Generic genericTypeDefinition, genericArguments, id) :> Type
    

    /// TEST BEGINS HERE
    //
    let checkType nm (ty:System.Type) isTup = 
        // Calls to basic properties are in a known state
        check (nm + "-falihksec0 - expect IsArray to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsArray |> Some with e -> None) (Some false)
        check (nm + "-falihksec1 - expect IsPointer to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsPointer |> Some with e -> None) (Some false)
        check (nm + "-falihksec2 - expect IsAbstract to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsAbstract |> Some with e -> None) (Some false)
        check (nm + "-falihksec3 - expect IsClass to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsClass |> Some with e -> None) (Some true)
        check (nm + "-falihksec4 - expect IsValueType to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsValueType |> Some with e -> None) (Some false)
        check (nm + "-falihksec5 - expect IsTuple to give accurate results on typical F# type provider implementation of TypeBuilderInstantiation") (try Reflection.FSharpType.IsTuple(ty) |> Some with _ -> None) (Some isTup)

#if !MONO
        check (nm + "-falihksec3a - currently expect IsEnum to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.IsEnum |> ignore; 100 with e -> 200) 200
        check (nm + "-falihksec4a - currently expect FullName to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try ty.FullName |> ignore; 100 with e -> 200) 200
        check (nm + "-falihksec5a - currently expect IsFunction to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try Reflection.FSharpType.IsFunction(ty) |> ignore; 100 with _ -> 200) 200
        check (nm + "-falihksec6a - currently expect IsUnion to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try Reflection.FSharpType.IsUnion(ty) |> ignore; 100 with :? System.NotSupportedException -> 200) 200
        check (nm + "-falihksec7a - currently expect IsRecord to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try Reflection.FSharpType.IsRecord (ty) |> ignore; 100 with :? System.NotSupportedException -> 200) 200
        check (nm + "-falihksec8a - currently expect IsModule to throw on typical F# type provider implementation of TypeBuilderInstantiation") (try Reflection.FSharpType.IsModule (ty) |> ignore; 100 with :? System.NotSupportedException -> 200) 200
#endif

    // This makes a TypeBuilderInstantiation type, because a real type has been instantiated with a non-real type
    let t0 = ProvidedTypeBuilder.MakeGenericType(typedefof<list<_>>, [ typeof<int> ])
    let t1 = typedefof<list<_>>.MakeGenericType(t0)
    let t2 = typedefof<int * int>.MakeGenericType(t0, t0)

    checkType "test cvweler8" t1 false
    checkType "test cvweler9" t2 true

module QuotationStructTupleTests = 
    let actual = struct (0,0)
    let code = 
        <@ match actual with
           | struct (0,0) -> true
           | _ -> false @>

    printfn "code = %A" code
    check "wcelwec" (match code with 
                     | IfThenElse (Call (None, _, [TupleGet (PropertyGet (None, _, []), 0); _]), IfThenElse (Call (None, _, [TupleGet (PropertyGet (None, _, []), 1); _]), Value _, Value _), Value _) -> true
                     | _ -> false)
         true

    for i = 0 to 7 do 
        check "vcknwwe0oo" (match Expr.TupleGet(<@@ struct (1,2,3,4,5,6,7,8) @@>, i) with TupleGet(b,n) -> b = <@@ struct (1,2,3,4,5,6,7,8) @@> && n = i | _ -> false) true 

    let actual2 : Result<string, string> = Ok "foo"
    let code2 = 
        <@ match actual2 with
           | Ok _ -> true
           | Error _ -> false @>

    printfn "code2 = %A" code2
    check "cewcewwer" 
         (match code2 with 
            | IfThenElse (UnionCaseTest (PropertyGet (None, actual2, []), _), Value _, Value _) -> true
            | _ -> false)
         true


module TestStaticCtor = 
    [<ReflectedDefinition>]
    type T() =
        static do printfn "Hello" // removing this makes the RD lookup work
        static member Ident (x:int) = x

    let testStaticCtor() = 
        // bug: threw error with message "Could not bind to method" 
        check "cvwenklwevpo1" (Expr.TryGetReflectedDefinition(typeof<T>.GetMethod("Ident"))).IsSome true
        check "cvwenklwevpo2" (Expr.TryGetReflectedDefinition(typeof<T>.GetConstructors(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic).[0])).IsSome true

    testStaticCtor()


module TestFuncNoArgs = 
    type SomeType() = class end
    type Test =
        static member ParseThis (f : System.Linq.Expressions.Expression<System.Func<SomeType>>) = f

    
    type D = delegate of unit -> int
    type D2<'T> = delegate of unit -> 'T

    let testFunc() = 
        check "cvwenklwevpo1" (match <@ new System.Func<int>(fun () -> 3) @> with Quotations.Patterns.NewDelegate(_,[],Value _) -> true | _ -> false) true
        check "cvwenklwevpo2" (match <@ new System.Func<int,int>(fun n -> 3) @> with Quotations.Patterns.NewDelegate(_,[_],Value _) -> true | _ -> false) true
        check "cvwenklwevpo1d" (match <@ new D(fun () -> 3) @> with Quotations.Patterns.NewDelegate(_,[],Value _) -> true | _ -> false) true
        check "cvwenklwevpo2d" (match <@ new D2<int>(fun () -> 3) @> with Quotations.Patterns.NewDelegate(_,[],Value _) -> true | _ -> false) true

    testFunc()


    let testFunc2() = 
        // was raising exception
        let foo = Test.ParseThis (fun () -> SomeType())
        check "clew0mmlvew" (foo.ToString()) "() => new SomeType()"

    testFunc2()

module TestMatchBang =
    let myAsync = async {
        do! Async.Sleep 1
        return Some 42 }

    /// Unpacks code quotations containing computation expressions (CE)
    let (|CEDelay|CEBind|Expr|) expr =
        match expr with
        | Application (Lambda (_, Call (_, mDelay, [Lambda (_, innerExpr)])), _) when mDelay.Name = "Delay" -> CEDelay innerExpr
        | Call (_, mBind, [_; Lambda (_, innerExpr)]) when mBind.Name = "Bind" -> CEBind innerExpr
        | _ -> Expr expr

    let testSimpleMatchBang() =
        let quot1 = <@ async { match! myAsync with | Some (x: int) -> () | None -> () } @>
        check "matchbangquot1"
            (match quot1 with
            | CEDelay(CEBind(IfThenElse expr)) -> Ok ()
            | CEDelay(CEBind(expr)) -> Error "if statement (representing `match`) is missing"
            | CEDelay(expr) -> Error "Bind is incorrect"
            | expr -> Error "Delay is incorrect")
            (Ok ())

    testSimpleMatchBang()
    
#if LANGVERSION_PREVIEW
module WitnessTests = 
    open FSharp.Data.UnitSystems.SI.UnitSymbols

    test "check CallWithWitness"      
        (<@ 1 + 1  @> 
         |> function 
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                minfo1.Name = "op_Addition" && 
                minfo1.GetParameters().Length = 2 && 
                minfo2.Name = "op_Addition$W" &&
                minfo2.GetParameters().Length = 3 && 
                (printfn "checking witnessArgs.Length = %d... args.Length"; true) &&
                witnessArgs.Length = 1 &&
                (printfn "checking args.Length = %d... args.Length"; true) &&
                args.Length = 2 &&
                (printfn "checking witnessArgs is a Lambda..."; true) &&
                (match witnessArgs with [ Lambda _ ] -> true | _ -> false) &&
                (printfn "checking witnessArg is the expected call..."; true) &&
                (match witnessArgs with [ Lambda (v1A, Lambda (v2A, Call(None, m, [ Patterns.Var v1B; Patterns.Var v2B]))) ] when m.Name = "op_Addition" && v1A = v1B && v2A = v2B -> true | _ -> false)
                (printfn "checking witnessArg is not a CalWithWitnesses..."; true) &&
                (match witnessArgs with [ Lambda (v1A, Lambda (v2A, CallWithWitnesses _)) ] -> false | _ -> true) &&
                (printfn "checking args..."; true) &&
                (match args with [ Int32 _; Int32 _ ] -> true | _ -> false)
            | _ -> false)

    test "check CallWithWitness (DateTime + TimeSpan)"      
        (<@ System.DateTime.Now + System.TimeSpan.Zero  @> 
         |> function 
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                minfo1.Name = "op_Addition" && 
                (printfn "checking minfo1.GetParameters().Length..."; true) &&
                minfo1.GetParameters().Length = 2 && 
                minfo2.Name = "op_Addition$W" &&
                (printfn "checking minfo2.GetParameters().Length..."; true) &&
                minfo2.GetParameters().Length = 3 && 
                (printfn "checking witnessArgs.Length..."; true) &&
                witnessArgs.Length = 1 &&
                (printfn "checking witnessArg is the expected call, witnessArgs = %A" witnessArgs; true) &&
                (match witnessArgs with 
                  | [ Lambda (v1A, Lambda (v2A, Call(None, m, [Patterns.Var v1B; Patterns.Var v2B]))) ]  
                       when m.Name = "op_Addition" 
                            && m.GetParameters().[0].ParameterType.Name = "DateTime" 
                            && m.GetParameters().[1].ParameterType.Name = "TimeSpan" 
                            && v1A = v1B 
                            && v2A = v2B -> true 
                  | _ -> false)
                (printfn "checking witnessArg is not a CallWithWitnesses, witnessArgs = %A" witnessArgs; true) &&
                (match witnessArgs with [ Lambda (v1A, Lambda (v2A, CallWithWitnesses args)) ] -> printfn "unexpected! %A" args; false | _ -> true) &&
                args.Length = 2 &&
                (printfn "checking args..."; true) &&
                (match args with [ _; _ ] -> true | _ -> false) &&
                (match witnessArgs with [ Lambda _ ] -> true | _ -> false)
            | CallWithWitnesses _ -> 
                printfn "no object"
                false
            | _ -> 
                printfn "incorrect node"
                false)

    test "check Call (DateTime + TimeSpan)"      
        (<@ System.DateTime.Now + System.TimeSpan.Zero  @> 
         |> function 
            | Call(None, minfo1, args) -> 
                minfo1.Name = "op_Addition" && 
                (printfn "checking minfo1.GetParameters().Length..."; true) &&
                minfo1.GetParameters().Length = 2 && 
                //minfo2.GetParameters().[0].Name = "op_Addition" && 
                args.Length = 2 &&
                (match args with [ _; _ ] -> true | _ -> false)
            | _ -> false)

    type C() = 
        static member inline StaticAdd (x, y) = x + y
        member inline __.InstanceAdd (x, y) = x + y

    test "check CallWithWitness (DateTime + TimeSpan) using static member"      
        (<@ C.StaticAdd(System.DateTime.Now, System.TimeSpan.Zero)  @> 
         |> function 
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                minfo1.IsStatic && 
                minfo1.Name = "StaticAdd" && 
                (printfn "checking minfo1.GetParameters().Length..."; true) &&
                minfo1.GetParameters().Length = 2 && 
                minfo2.IsStatic && 
                minfo2.Name = "StaticAdd$W" &&
                (printfn "checking minfo2.GetParameters().Length = %d..." (minfo2.GetParameters().Length); true) &&
                minfo2.GetParameters().Length = 3 && 
                (printfn "checking witnessArgs.Length..."; true) &&
                witnessArgs.Length = 1 &&
                (printfn "checking args.Length..."; true) &&
                args.Length = 2 &&
                (printfn "witnessArgs..."; true) &&
                (match witnessArgs with [ Lambda _ ] -> true | _ -> false) &&
                (printfn "args..."; true) &&
                (match args with [ _; _ ] -> true | _ -> false)
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                printfn "no object..."
                false
            | _ -> false)

    test "check CallWithWitness (DateTime + TimeSpan) using instance member"      
        (<@ C().InstanceAdd(System.DateTime.Now, System.TimeSpan.Zero)  @> 
         |> function 
            | CallWithWitnesses(Some _obj, minfo1, minfo2, witnessArgs, args) -> 
                not minfo1.IsStatic && 
                minfo1.Name = "InstanceAdd" && 
                (printfn "checking minfo1.GetParameters().Length..."; true) &&
                minfo1.GetParameters().Length = 2 && 
                not minfo2.IsStatic && 
                minfo2.Name = "InstanceAdd$W" &&
                (printfn "checking minfo2.GetParameters().Length = %d..." (minfo2.GetParameters().Length); true) &&
                minfo2.GetParameters().Length = 3 && 
                (printfn "checking witnessArgs.Length..."; true) &&
                witnessArgs.Length = 1 &&
                (printfn "checking args.Length..."; true) &&
                args.Length = 2 &&
                (printfn "witnessArgs..."; true) &&
                (match witnessArgs with [ Lambda _ ] -> true | _ -> false) &&
                (printfn "args..."; true) &&
                (match args with [ _; _ ] -> true | _ -> false)
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                printfn "no object..."
                false
            | _ -> false)

    test "check CallWithWitnesses all operators)"      
      (let tests = 
            [ <@@ sin 1.0  @@>, true
              <@@ sin 1.0f  @@>, true
              <@@ sign 1.0f  @@>, true
              <@@ sqrt 1.0f<m>  @@>, true
              <@@ 2.0f ** 2.0f  @@>, true
              <@@ atan2 3.0 4.0  @@>, true
              <@@ 1.0f + 4.0f  @@>, true
              <@@ 1.0f - 4.0f  @@>, true
              <@@ 1.0f * 4.0f  @@>, true
              <@@ 1.0M * 4.0M  @@>, true
              <@@ 1.0f / 4.0f  @@>, true
              <@@ 1 % 4  @@>, true
              <@@ -(4.0M)  @@>, true

              <@@ 1y <<< 3  @@>, true
              <@@ 1uy <<< 3  @@>, true
              <@@ 1s <<< 3  @@>, true
              <@@ 1us <<< 3  @@>, true
              <@@ 1 <<< 3  @@>, true
              <@@ 1u <<< 3  @@>, true
              <@@ 1L <<< 3  @@>, true
              <@@ 1UL <<< 3  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint> <<< 3  @@>, false
              <@@ LanguagePrimitives.GenericOne<unativeint> <<< 3  @@>, false

              <@@ 1y >>> 3  @@>, true
              <@@ 1uy >>> 3  @@>, true
              <@@ 1s >>> 3  @@>, true
              <@@ 1us >>> 3  @@>, true
              <@@ 1 >>> 3  @@>, true
              <@@ 1u >>> 3  @@>, true
              <@@ 1L >>> 3  @@>, true
              <@@ 1UL >>> 3  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint> >>> 3  @@>, false
              <@@ LanguagePrimitives.GenericOne<unativeint> >>> 3  @@>, false
              
              <@@ 1y &&& 3y  @@>, true
              <@@ 1uy &&& 3uy  @@>, true
              <@@ 1s &&& 3s  @@>, true
              <@@ 1us &&& 3us  @@>, true
              <@@ 1 &&& 3  @@>, true
              <@@ 1u &&& 3u  @@>, true
              <@@ 1L &&& 3L  @@>, true
              <@@ 1UL &&& 3UL  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint> &&& LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ LanguagePrimitives.GenericOne<unativeint> &&& LanguagePrimitives.GenericOne<unativeint>  @@>, false

              <@@ 1y ||| 3y  @@>, true
              <@@ 1uy ||| 3uy  @@>, true
              <@@ 1s ||| 3s  @@>, true
              <@@ 1us ||| 3us  @@>, true
              <@@ 1 ||| 3  @@>, true
              <@@ 1u ||| 3u  @@>, true
              <@@ 1L ||| 3L  @@>, true
              <@@ 1UL ||| 3UL  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint> ||| LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ LanguagePrimitives.GenericOne<unativeint> ||| LanguagePrimitives.GenericOne<unativeint>  @@>, false

              <@@ 1y ^^^ 3y  @@>, true
              <@@ 1uy ^^^ 3uy  @@>, true
              <@@ 1s ^^^ 3s  @@>, true
              <@@ 1us ^^^ 3us  @@>, true
              <@@ 1 ^^^ 3  @@>, true
              <@@ 1u ^^^ 3u  @@>, true
              <@@ 1L ^^^ 3L  @@>, true
              <@@ 1UL ^^^ 3UL  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint> ^^^ LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ LanguagePrimitives.GenericOne<unativeint> ^^^ LanguagePrimitives.GenericOne<unativeint>  @@>, false

              <@@ ~~~3y  @@>, true
              <@@ ~~~3uy  @@>, true
              <@@ ~~~3s  @@>, true
              <@@ ~~~3us  @@>, true
              <@@ ~~~3  @@>, true
              <@@ ~~~3u  @@>, true
              <@@ ~~~3L  @@>, true
              <@@ ~~~3UL  @@>, true
              <@@ ~~~LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ ~~~LanguagePrimitives.GenericOne<unativeint>  @@>, false

              <@@ byte 3uy  @@>, true
              <@@ byte 3y  @@>, true
              <@@ byte 3s  @@>, true
              <@@ byte 3us  @@>, true
              <@@ byte 3  @@>, true
              <@@ byte 3u  @@>, true
              <@@ byte 3L  @@>, true
              <@@ byte 3UL  @@>, true
              <@@ byte 3.0f  @@>, true
              <@@ byte 3.0  @@>, true
              <@@ byte LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ byte LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ byte 3.0M  @@>, true
              <@@ byte "3"  @@>, false

              <@@ sbyte 3uy  @@>, true
              <@@ sbyte 3y  @@>, true
              <@@ sbyte 3s  @@>, true
              <@@ sbyte 3us  @@>, true
              <@@ sbyte 3  @@>, true
              <@@ sbyte 3u  @@>, true
              <@@ sbyte 3L  @@>, true
              <@@ sbyte 3UL  @@>, true
              <@@ sbyte 3.0f  @@>, true
              <@@ sbyte 3.0  @@>, true
              <@@ sbyte LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ sbyte LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ sbyte 3.0M  @@>, true
              <@@ sbyte "3"  @@>, false

              <@@ int16 3uy  @@>, true
              <@@ int16 3y  @@>, true
              <@@ int16 3s  @@>, true
              <@@ int16 3us  @@>, true
              <@@ int16 3  @@>, true
              <@@ int16 3u  @@>, true
              <@@ int16 3L  @@>, true
              <@@ int16 3UL  @@>, true
              <@@ int16 3.0f  @@>, true
              <@@ int16 3.0  @@>, true
              <@@ int16 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ int16 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ int16 3.0M  @@>, true
              <@@ int16 "3"  @@>, false

              <@@ uint16 3uy  @@>, true
              <@@ uint16 3y  @@>, true
              <@@ uint16 3s  @@>, true
              <@@ uint16 3us  @@>, true
              <@@ uint16 3  @@>, true
              <@@ uint16 3u  @@>, true
              <@@ uint16 3L  @@>, true
              <@@ uint16 3UL  @@>, true
              <@@ uint16 3.0f  @@>, true
              <@@ uint16 3.0  @@>, true
              <@@ uint16 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ uint16 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ uint16 3.0M  @@>, true
              <@@ uint16 "3"  @@>, false

              <@@ int32 3uy  @@>, true
              <@@ int32 3y  @@>, true
              <@@ int32 3s  @@>, true
              <@@ int32 3us  @@>, true
              <@@ int32 3  @@>, true
              <@@ int32 3u  @@>, true
              <@@ int32 3L  @@>, true
              <@@ int32 3UL  @@>, true
              <@@ int32 3.0f  @@>, true
              <@@ int32 3.0  @@>, true
              <@@ int32 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ int32 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ int32 3.0M  @@>, true
              <@@ int32 "3"  @@>, false

              <@@ uint32 3uy  @@>, true
              <@@ uint32 3y  @@>, true
              <@@ uint32 3s  @@>, true
              <@@ uint32 3us  @@>, true
              <@@ uint32 3  @@>, true
              <@@ uint32 3u  @@>, true
              <@@ uint32 3L  @@>, true
              <@@ uint32 3UL  @@>, true
              <@@ uint32 3.0f  @@>, true
              <@@ uint32 3.0  @@>, true
              <@@ uint32 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ uint32 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ uint32 3.0M  @@>, true
              <@@ uint32 "3"  @@>, false

              <@@ int64 3uy  @@>, true
              <@@ int64 3y  @@>, true
              <@@ int64 3s  @@>, true
              <@@ int64 3us  @@>, true
              <@@ int64 3  @@>, true
              <@@ int64 3u  @@>, true
              <@@ int64 3L  @@>, true
              <@@ int64 3UL  @@>, true
              <@@ int64 3.0f  @@>, true
              <@@ int64 3.0  @@>, true
              <@@ int64 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ int64 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ int64 3.0M  @@>, true
              <@@ int64 "3"  @@>, false
              
              <@@ uint64 3uy  @@>, true
              <@@ uint64 3y  @@>, true
              <@@ uint64 3s  @@>, true
              <@@ uint64 3us  @@>, true
              <@@ uint64 3  @@>, true
              <@@ uint64 3u  @@>, true
              <@@ uint64 3L  @@>, true
              <@@ uint64 3UL  @@>, true
              <@@ uint64 3.0f  @@>, true
              <@@ uint64 3.0  @@>, true
              <@@ uint64 LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ uint64 LanguagePrimitives.GenericOne<unativeint>  @@>, false
              <@@ uint64 3.0M  @@>, true
              <@@ uint64 "3"  @@>, false

              <@@ nativeint 3uy  @@>, true
              <@@ nativeint 3y  @@>, true
              <@@ nativeint 3s  @@>, true
              <@@ nativeint 3us  @@>, true
              <@@ nativeint 3  @@>, true
              <@@ nativeint 3u  @@>, true
              <@@ nativeint 3L  @@>, true
              <@@ nativeint 3UL  @@>, true
              <@@ nativeint 3.0f  @@>, true
              <@@ nativeint 3.0  @@>, true
              <@@ nativeint LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ nativeint LanguagePrimitives.GenericOne<unativeint>  @@>, false
              //<@@ nativeint 3.0M  @@>, false
              //<@@ nativeint "3"  @@>, false

              <@@ unativeint 3uy  @@>, true
              <@@ unativeint 3y  @@>, true
              <@@ unativeint 3s  @@>, true
              <@@ unativeint 3us  @@>, true
              <@@ unativeint 3  @@>, true
              <@@ unativeint 3u  @@>, true
              <@@ unativeint 3L  @@>, true
              <@@ unativeint 3UL  @@>, true
              <@@ unativeint 3.0f  @@>, true
              <@@ unativeint 3.0  @@>, true
              <@@ unativeint LanguagePrimitives.GenericOne<nativeint>  @@>, false
              <@@ unativeint LanguagePrimitives.GenericOne<unativeint>  @@>, false
              //<@@ unativeint 3.0M  @@>, true
              //<@@ unativeint "3"  @@>, true

              <@@ LanguagePrimitives.GenericZero<float>  @@>, true
              <@@ LanguagePrimitives.GenericZero<float32>  @@>, true
              <@@ LanguagePrimitives.GenericZero<int>  @@>, true
              <@@ LanguagePrimitives.GenericZero<int64>  @@>, true
              <@@ LanguagePrimitives.GenericZero<uint64>  @@>, true
              <@@ LanguagePrimitives.GenericZero<nativeint>  @@>, true
              <@@ LanguagePrimitives.GenericOne<float>  @@>, true
              <@@ LanguagePrimitives.GenericOne<float32>  @@>, true
              <@@ LanguagePrimitives.GenericOne<int>  @@>, true
              <@@ LanguagePrimitives.GenericOne<int64>  @@>, true
              <@@ LanguagePrimitives.GenericOne<uint64>  @@>, true
              <@@ LanguagePrimitives.GenericOne<nativeint>  @@>, true
              <@@ List.sum [ 1; 2 ]  @@>, true
              <@@ List.sum [ 1.0f; 2.0f ]  @@>, true
              <@@ List.sum [ 1.0; 2.0 ]  @@>, true
              <@@ List.sum [ 1.0M; 2.0M ]  @@>, true
              <@@ List.average [ 1.0; 2.0 ]  @@>, true
              <@@ List.average [ 1.0f; 2.0f ]  @@>, true
              <@@ List.average [ 1.0M; 2.0M ]  @@>, true 
            ]

       tests |> List.forall (fun (test, canEval) -> 
           if canEval then 
               printfn "--> checking we can evaluate %A" test
               FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation test |> ignore
               printfn "<-- evaluated!"
           else
               printfn "skipping evaluation of %A because LinqExpressionConverter can't handle it" test
           printfn "checking %A" test
           match test with
            | CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
                minfo1.IsStatic && 
                minfo2.IsStatic && 
                minfo2.Name = minfo1.Name + "$W" &&
    (*
                (printfn "checking minfo2.GetParameters().Length = %d..." (minfo2.GetParameters().Length); true) &&
                minfo2.GetParameters().Length = 3 && 
                (printfn "checking witnessArgs.Length..."; true) &&
                witnessArgs.Length = 1 &&
                (printfn "checking args.Length..."; true) &&
                args.Length = 2 &&
                (printfn "witnessArgs..."; true) &&
                (match witnessArgs with [ Lambda _ ] -> true | _ -> false) &&
                (printfn "args..."; true) &&
                (match args with [ _; _ ] -> true | _ -> false)
                *)
                true
            | _ -> false))

module MoreWitnessTests =

    open System.Runtime.CompilerServices
    open System.IO

    [<ReflectedDefinition>]
    module Tests = 
        let inline f0 (x: 'T) : (unit -> 'T) list = 
           [] 

        let inline f (x: 'T) : (unit -> 'T) list = 
           [(fun () -> x + x)] 

        type C() =
            member inline __.F(x: 'T) = x + x

        [<AutoOpen>]
        module M = 

            type C with 
                member inline __.F2(x: 'T) = x + x
                static member inline F2Static(x: 'T) = x + x

            [<Extension>]
            type FileExt =
               [<Extension>]
               static member CreateDirectory(fileInfo: FileInfo) =
                   Directory.CreateDirectory fileInfo.Directory.FullName

               [<Extension>]
               static member inline F3(s: string, x: 'T) =
                   x + x

               [<Extension>]
               static member inline F4(s: string, x1: 'T, x2: 'T) =
                   x1 + x2


        [<ReflectedDefinition>]
        module Usage  = 
            let q0 = <@ f0 3 @>
            let q1 = <@ f 3 @>
            let q2 = <@ C().F(3) @>
            let q3 = <@ C().F2(3) @>
            let q4 = <@ C.F2Static(3) @>
            let q5 = <@ "".F3(3) @>
            let q6 = <@ "".F4(3, 4) @>

            check "wekncjeck1" (q0.ToString()) "Call (None, f0, [Value (3)])"
            check "wekncjeck2" (q1.ToString()) "Call (None, f, [Value (3)])"
            check "wekncjeck3" (q2.ToString()) "Call (Some (NewObject (C)), F, [Value (3)])"
            check "wekncjeck4" (q3.ToString()) "Call (None, C.F2, [NewObject (C), Value (3)])"
            check "wekncjeck5" (q4.ToString()) "Call (None, C.F2Static.Static, [Value (3)])"
            check "wekncjeck6" (q5.ToString()) "Call (None, F3, [Value (\"\"), Value (3)])"
            check "wekncjeck7" (q6.ToString()) "Call (None, F4, [Value (\"\"), Value (3), Value (4)])"

            check "ewlknweknl1" (FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q0) (box ([] : (unit -> int) list))
            check "ewlknweknl2" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q1 with :? ((unit -> int) list) as x -> x.[0] ()) 6
            check "ewlknweknl3" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q2 with :? int as x -> x) 6
            check "ewlknweknl4" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q3 with :? int as x -> x) 6
            check "ewlknweknl5" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q4 with :? int as x -> x) 6
            check "ewlknweknl6" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q5 with :? int as x -> x) 6
            check "ewlknweknl7" (match FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation q6 with :? int as x -> x) 7

// Check we can take ReflectedDefinition of things involving witness and trait calls
module QuotationsOfGenericCodeWithWitnesses =
    [<ReflectedDefinition>]
    let inline f1 (x: ^T) = x + x // ( ^T : (static member Foo: int -> int) (3))

    match <@ f1 1 @> with
    | Quotations.Patterns.Call(_, mi, _) -> 
        let mi1 = mi.GetGenericMethodDefinition() 
        let q1 = Quotations.Expr.TryGetReflectedDefinition(mi1)
        check "vwehwevrwv" q1 None
    | q -> report_failure (sprintf "gfwhoewvioh - unexpected %A" q)


    match <@ f1 1 @> with
    | Quotations.Patterns.CallWithWitnesses(_, mi, minfoWithWitnesses, _, _) -> 
        let q2 = Quotations.Expr.TryGetReflectedDefinition(minfoWithWitnesses)

        match q2 with 
        | Some (Lambda (witnessArgVar, Lambda(v, CallWithWitnesses(None, mi, minfoWithWitnesses, [Var witnessArgVar2], [a2;b2])))) -> 
        
            check "cewlkjwvw0a" witnessArgVar.Name "op_Addition"
            check "cewlkjwvw0b" witnessArgVar.Type (typeof<int -> int -> int>)
            check "cewlkjwvw1a" witnessArgVar2.Name "op_Addition"
            check "cewlkjwvw1b" witnessArgVar2.Type (typeof<int -> int -> int>)
            check "cewlkjwvw2" minfoWithWitnesses.Name "op_Addition$W"
            check "vjnvwiowve" a2 b2 
            check "cewlkjwvw0" witnessArgVar witnessArgVar2

        | q -> report_failure (sprintf "gfwhoewvioh32 - unexpected %A" q)
            
    | q -> report_failure (sprintf "gfwhoewvioh37 - unexpected %A" q)
    
    
    type C() = 
        static member Foo (x:int) = x

    [<ReflectedDefinition>]
    let inline f3 (x: ^T) =
        ( ^T : (static member Foo: int -> int) (3))

    match <@ f3 (C()) @> with
    | Quotations.Patterns.Call(_, mi, _) -> 
        let mi3 = mi.GetGenericMethodDefinition()
        let q3 = Quotations.Expr.TryGetReflectedDefinition(mi3)
        check "fekjevwlw" q3 None
    | q -> report_failure (sprintf "3kjhhjkkjhe9 - %A unexpected"  q)

    match <@ f3 (C()) @> with 
    | Quotations.Patterns.CallWithWitnesses(_, mi, miw, [w4], _) -> 
        let q4 = Quotations.Expr.TryGetReflectedDefinition(miw)

        check "vwroirvjkn" miw.Name "f3$W"

        match q4 with 
        | Some (Lambda(witnessArgVar, Lambda(v, Application(Var witnessArgVar2, Int32 3)))) -> 
            check "vwehjrwlkj0" witnessArgVar.Name "Foo"
            check "vwehjrwlkj1" witnessArgVar.Type (typeof<int -> int>)
            check "vwehjrwlkj2" witnessArgVar2.Name "Foo"
            check "vwehjrwlkj3" witnessArgVar2 witnessArgVar
        | _ -> report_failure (sprintf "3kjhhjkkjhe1 - %A unexpected"  q4)

        match w4 with 
        | Lambda(v, Call(None, miFoo, [Var v2])) -> 
           check "vewhjwveoi1" miFoo.Name "Foo"
           check "vewhjwveoi2" v v2
        | _ -> report_failure (sprintf "3kjhhjkkjhe2 - %A unexpected"  w4)

    | q -> report_failure (sprintf "3kjhhjkkjhe0 - %A unexpected"  q)

/// Check we can take quotations of implicit operator trait calls

module QuotationOfConcreteTraitCalls =
    
    type Foo(s: string) =
         member _.S = s
         static member (?) (foo : Foo, name : string) = foo.S + name
         static member (++) (foo : Foo, name : string) = foo.S + name
         static member (?<-) (foo : Foo, name : string, v : string) = ()

    let foo = Foo("hello, ")

    // Desugared form is ok, but ? desugars to a method with constraints which aren't allowed in quotes
    let q1 = <@ Foo.op_Dynamic(foo, "uhh") @>
    let q2 = <@ foo ? uhh @>

    let q3 = <@ Foo.op_DynamicAssignment(foo, "uhh", "hm") @>
    let q4 = <@ foo ? uhh <- "hm" @>
    let q5 = <@ foo ++ "uhh" @>

    let cleanup (s:string) = s.Replace(" ","").Replace("\n","").Replace("\r","")
    check "wekncjeck112a" (cleanup (sprintf "%0A" q1)) "Call(None,op_Dynamic,[PropertyGet(None,foo,[]),Value(\"uhh\")])"
    check "wekncjeck112b" (cleanup (sprintf "%0A" q2)) "Application(Application(Lambda(arg0,Lambda(arg1,Call(None,op_Dynamic,[arg0,arg1]))),PropertyGet(None,foo,[])),Value(\"uhh\"))"
    check "wekncjeck112c" (cleanup (sprintf "%0A" q3)) "Call(None,op_DynamicAssignment,[PropertyGet(None,foo,[]),Value(\"uhh\"),Value(\"hm\")])"
    check "wekncjeck112d" (cleanup (sprintf "%0A" q4)) "Application(Application(Application(Lambda(arg0,Lambda(arg1,Lambda(arg2,Call(None,op_DynamicAssignment,[arg0,arg1,arg2])))),PropertyGet(None,foo,[])),Value(\"uhh\")),Value(\"hm\"))"
    check "wekncjeck112e" (cleanup (sprintf "%0A" q5)) "Application(Application(Lambda(arg0,Lambda(arg1,Call(None,op_PlusPlus,[arg0,arg1]))),PropertyGet(None,foo,[])),Value(\"uhh\"))"

    // Let bound functions handle this ok
    let (?) o s =
        printfn "%s" s

    // No error here because it binds to the let bound version
    let q8 = <@ foo ? uhh @>

// Check we can take ReflectedDefinition of things involving multiple implicit witnesses and trait calls
module QuotationsOfGenericCodeWithMultipleWitnesses =

    // This has three type paramters and two witnesses, one for + and one for -
    [<ReflectedDefinition>]
    let inline f1 x y z = (x + y) - z

    match <@ f1 1 2 3 @> with
    | Quotations.Patterns.Call(_, mi, _) -> 
        let q1 = Quotations.Expr.TryGetReflectedDefinition(mi)
        check "vwehwevrwv" q1 None
    | q -> report_failure (sprintf "gfwhoewvioh - unexpected %A" q)

    match <@ f1 1 2 3 @> with
    | Quotations.Patterns.CallWithWitnesses(_, mi, minfoWithWitnesses, _, _) -> 
        let q2 = Quotations.Expr.TryGetReflectedDefinition(minfoWithWitnesses)

        match q2 with 
        | Some (Lambda (witnessArgVarAdd, 
                 Lambda (witnessArgVarSub, 
                    Lambda(xVar, 
                      Lambda(yVar, 
                        Lambda(zVar, 
                           CallWithWitnesses(None, mi1, minfoWithWitnesses1, [Var witnessArgVarSub2], 
                            [CallWithWitnesses(None, mi2, minfoWithWitnesses2, [Var witnessArgVarAdd2], 
                               [Var xVar2; Var yVar2]);
                             Var zVar2]))))))) -> 
        
            check "cewlkjwv54" witnessArgVarAdd.Name "op_Addition"
            check "cewlkjwv55" witnessArgVarSub.Name "op_Subtraction"
            check "cewlkjwv56" witnessArgVarAdd.Type (typeof<int -> int -> int>)
            check "cewlkjwv57" witnessArgVarSub.Type (typeof<int -> int -> int>)
            check "cewlkjwv58" witnessArgVarAdd witnessArgVarAdd2
            check "cewlkjwv59" witnessArgVarSub witnessArgVarSub2
            check "cewlkjwv60" xVar xVar2
            check "cewlkjwv61" yVar yVar2
            check "cewlkjwv62" zVar zVar2

        | q -> report_failure (sprintf "gfwhoewvioh32 - unexpected %A" q)
            
    | q -> report_failure (sprintf "gfwhoewvioh37 - unexpected %A" q)
    
// Like QuotationsOfGenericCodeWithMultipleWitnesses but with implementation code the other way around
module QuotationsOfGenericCodeWithMultipleWitnesses2 =

    [<ReflectedDefinition>]
    let inline f1 x y z = (x - y) + z

    match <@ f1 1 2 3 @> with
    | Quotations.Patterns.Call(_, mi, _) -> 
        let q1 = Quotations.Expr.TryGetReflectedDefinition(mi)
        check "xvwehwevrwv" q1 None
    | q -> report_failure (sprintf "xgfwhoewvioh - unexpected %A" q)

    match <@ f1 1 2 3 @> with
    | Quotations.Patterns.CallWithWitnesses(_, mi, minfoWithWitnesses, _, _) -> 
        let q2 = Quotations.Expr.TryGetReflectedDefinition(minfoWithWitnesses)

        match q2 with 
        | Some (Lambda (witnessArgVarAdd, 
                 Lambda (witnessArgVarSub, 
                    Lambda(xVar, 
                      Lambda(yVar, 
                        Lambda(zVar, 
                           CallWithWitnesses(None, mi1, minfoWithWitnesses1, [Var witnessArgVarAdd2], 
                            [CallWithWitnesses(None, mi2, minfoWithWitnesses2, [Var witnessArgVarSub2], 
                               [Var xVar2; Var yVar2]);
                             Var zVar2]))))))) -> 
        
            check "xcewlkjwv54" witnessArgVarAdd.Name "op_Addition"
            check "xcewlkjwv55" witnessArgVarSub.Name "op_Subtraction"
            check "xcewlkjwv56" witnessArgVarAdd.Type (typeof<int -> int -> int>)
            check "xcewlkjwv57" witnessArgVarSub.Type (typeof<int -> int -> int>)
            check "xcewlkjwv58" witnessArgVarAdd witnessArgVarAdd2
            check "xcewlkjwv59" witnessArgVarSub witnessArgVarSub2
            check "xcewlkjwv60" xVar xVar2
            check "xcewlkjwv61" yVar yVar2
            check "xcewlkjwv62" zVar zVar2

        | q -> report_failure (sprintf "xgfwhoewvioh32 - unexpected %A" q)
            
    | q -> report_failure (sprintf "xgfwhoewvioh37 - unexpected %A" q)
    
    
module TestOuterConstrainedClass =
    // This example where there is an outer constrained class caused numerous failures
    // because it was trying to pass witnesses for the constraint in the type 
    //
    // No witnesses are passed for these
    type hoop< ^a when ^a : (static member (+) : ^a * ^a -> ^a) > =
        { Group1 : ^a
          Group2 : ^a } 
        static member inline (+) (x, y) = x.Group1 + y.Group2
        //member inline this.Sum = this.Group1 + this.Group2 

    let z = { Group1 = 1; Group2 = 2 } + { Group1 = 2; Group2 = 3 } // ok

module TestInlineQuotationOfAbsOperator  =

    let inline f x = <@ abs x @>

    type C(n:int) = 
        static member Abs(c: C) = C(-c.P)
        member x.P = n

    let v1 = f 3
    let v2 = f 3.4
    let v3 = f (C(4))
    
    test "check abs1" 
       (match v1 with 
         | CallWithWitnesses(None, minfo1, minfo2, [Value(f,_)], [Int32 3]) ->
             minfo1.Name = "Abs" && minfo2.Name = "Abs$W" && ((f :?> (int -> int)) -3 = 3)
         | _ -> false)

    test "check abs2" 
       (match v2 with 
         | CallWithWitnesses(None, minfo1, minfo2, [Value(f,_)], [Double 3.4]) ->
             minfo1.Name = "Abs" && minfo2.Name = "Abs$W"  && ((f :?> (double -> double)) -3.0 = 3.0)
         | _ -> false)

    test "check abs3" 
       (match v3 with 
         | CallWithWitnesses(None, minfo1, minfo2, [Value(f,_)], [Value (v,_)]) ->
             minfo1.Name = "Abs" && minfo2.Name = "Abs$W"  && ((v :?> C).P = 4) && (((f :?> (C -> C)) (C(-7))).P = 7)
         | _ -> false)

    
module TestQuotationOfListSum =
    type Point =
        { x: int; y: int }
        static member Zero = { x=0; y=0 }
        static member (+) (p1, p2) = { x= p1.x + p2.x; y = p1.y + p2.y }
    let points = [{x=1; y=10}]

    let q = <@ List.sum points @>

    match q with 
     | CallWithWitnesses(None, minfo1, minfo2, [w1; w2], [_]) ->
         test "check List.sum 111" (minfo1.Name = "Sum")
         test "check List.sum 112" (minfo2.Name = "Sum$W")
         printfn "w1 = %A" w1
         match w1 with 
         | Lambda(v, PropertyGet(None, miFoo, [])) -> 
           test  "check List.sum 113" (miFoo.Name = "Zero")
         | _ -> 
           test  "check List.sum 114" false
         match w2 with 
         | Lambda(v, Lambda(v2, Call(None, miFoo, [_;_]))) -> 
           test  "check List.sum 115" (miFoo.Name = "op_Addition")
         | _ -> 
           test  "check List.sum 116" false
     | _ -> 
         test "check List.sum 117" false


module TestQuotationOfListSum2 =
    type Point =
        { x: int; y: int }
        static member Zero = { x=0; y=0 }
        static member (+) (p1, p2) = { x= p1.x + p2.x; y = p1.y + p2.y }
    let points = [{x=1; y=10}]

    let inline quoteListSum points = <@ List.sum points @>

    match quoteListSum points with 
     | CallWithWitnesses(None, minfo1, minfo2, [Value (f1, _); Value (f2, _)], [Value (v,_)]) ->
         test "check List.sum 211" (minfo1.Name = "Sum")
         test "check List.sum 212" (minfo2.Name = "Sum$W")
         test "check List.sum 213" (((v :?> Point list) = points))
         test "check List.sum 214" ((((f1 :?> (unit -> Point)) ()) = Point.Zero))
         test "check List.sum 215" ((((f2 :?> (Point -> Point -> Point)) {x=1;y=1} {x=1;y=2}) = {x=2;y=3}))
     | _ -> 
         test "check List.sum 216" false


module ComputationExpressionWithOptionalsAndParamArray =
    open System
    type InputKind =
        | Text of placeholder:string option
        | Password of placeholder: string option
    type InputOptions =
      { Label: string option
        Kind : InputKind
        Validators : (string -> bool) array }
    type InputBuilder() =
        member t.Yield(_) =
          { Label = None
            Kind = Text None
            Validators = [||] }
        [<CustomOperation("text")>]
        member this.Text(io, ?placeholder) =
            { io with Kind = Text placeholder }
        [<CustomOperation("password")>]
        member this.Password(io, ?placeholder) =
            { io with Kind = Password placeholder }
        [<CustomOperation("label")>]
        member this.Label(io, label) =
            { io with Label = Some label }
        [<CustomOperation("with_validators")>]
        member this.Validators(io, [<ParamArray>] validators) =
            { io with Validators = validators }

    let input = InputBuilder()
    let name =
        input {
            label "Name"
            text
            with_validators
                (String.IsNullOrWhiteSpace >> not)
        }
    let email =
        input {
            label "Email"
            text "Your email"
            with_validators
                (String.IsNullOrWhiteSpace >> not)
                (fun s -> s.Contains "@")
        }
    let password =
        input {
            label "Password"
            password "Must contains at least 6 characters, one number and one uppercase"
            with_validators
                (String.exists Char.IsUpper)
                (String.exists Char.IsDigit)
                (fun s -> s.Length >= 6)
        }
    check "vewhkvh1" name.Kind (Text None)
    check "vewhkvh2" email.Kind (Text (Some "Your email"))
    check "vewhkvh3" email.Label (Some "Email")
    check "vewhkvh4" email.Validators.Length 2
    check "vewhkvh5" password.Label (Some "Password")
    check "vewhkvh6" password.Validators.Length 3

#endif

module QuotationOfComputationExpressionZipOperation =

    type Builder() =
      member __.Bind (x, f) = f x
      member __.Return x = x
      member __.For (x, f) = f x
      member __.Yield x = x
      [<CustomOperation("var", MaintainsVariableSpaceUsingBind = true, IsLikeZip=true)>]
      member __.Var (x, y, f) = f x y

    let builder = Builder()

    let q = 
        <@  builder {
                let! x = 1
                var y in 2
                return x + y
            } @> 

    let actual = (q.ToString())
    checkStrings "brewbreebr" actual 
       """Application (Lambda (builder@,
                     Call (Some (builder@), For,
                           [Call (Some (builder@), Var,
                                  [Call (Some (builder@), Bind,
                                         [Value (1),
                                          Lambda (_arg1,
                                                  Let (x, _arg1,
                                                       Call (Some (builder@),
                                                             Yield, [x])))]),
                                   Value (2),
                                   Lambda (x, Lambda (y, NewTuple (x, y)))]),
                            Lambda (_arg2,
                                    Let (y, TupleGet (_arg2, 1),
                                         Let (x, TupleGet (_arg2, 0),
                                              Call (Some (builder@), Return,
                                                    [Call (None, op_Addition,
                                                           [x, y])]))))])),
             PropertyGet (None, builder, []))"""
        
module CheckEliminatedConstructs = 
    let isNullQuoted (ts : 't[]) =
        <@
            match ts with
            | null -> true
            | _ -> false
        @>

    let actual1 = ((isNullQuoted [| |]).ToString())
    checkStrings "brewbreebrvwe1" actual1
       """IfThenElse (Call (None, op_Equality, [ValueWithName ([||], ts), Value (<null>)]),
            Value (true), Value (false))"""
        
module Interpolation =
    let interpolatedNoHoleQuoted = <@ $"abc" @>
    let actual1 = interpolatedNoHoleQuoted.ToString()
    checkStrings "brewbreebrwhat1" actual1 """Value ("abc")"""

    let interpolatedWithLiteralQuoted = <@ $"abc {1} def" @>
    let actual2 = interpolatedWithLiteralQuoted.ToString()
    checkStrings "brewbreebrwhat2" actual2
        """Call (None, PrintFormatToString,
                 [NewObject (PrintfFormat`5, Value ("abc %P() def"),
                             NewArray (Object, Call (None, Box, [Value (1)])),
                             Value (<null>))])"""

module TestAssemblyAttributes = 
    let attributes = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(false)

#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | errs -> 
      printfn "Test Failed, errors = %A" errs
      exit 1
#endif

