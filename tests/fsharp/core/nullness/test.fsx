#if TESTS_AS_APP
module Core_nullness
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

open System
open System.Runtime.CompilerServices

//let f<'T when 'T : not struct> (x: 'T?) = 1

module Basics = 
    let x1 : string = null // ** Expected to give a Nullness warning
    check "ekjnceoiwe1" x1 null
    let x2 : string? = null // Should not give a Nullness warning
    check "ekjnceoiwe2" x2 null
    let x3 : string? = "a" // Should not give a Nullness warning
    check "ekjnceoiwe3" x3 "a"
    let x4 : string = "" // Should not give a Nullness warning
    check "ekjnceoiwe4" x4 ""

    let x5 = nonNull<string> "" // Should not give a Nullness warning
    check "ekjnceoiwe5" x5 ""
    let x6 = nonNull<string?> "" // **Expected to give a Nullness warning, expected also to give a warning with nullness checking off
    check "ekjnceoiwe6" x6 ""
    let x7 = nonNull ""
    check "ekjnceoiwe7" x7 ""
    let _x7 : string = x7
    let x8 = nonNull<string[]> Array.empty
    check "ekjnceoiwe8" x8 [| |]
    let x9 = nonNull [| "" |]
    check "ekjnceoiwe9" x9 [| "" |]
    let x10 = nonNullV (Nullable(3))
    check "ekjnceoiwe10" x10 3
    let x11 = try nonNullV (Nullable<int>()) with :? System.NullReferenceException -> 10
    check "ekjnceoiwe11" x11 10
    let x12 = nullV<int>
    check "ekjnceoiwe12" x12 (Nullable())
    let x13 = nullV<int64>
    check "ekjnceoiwe13" x13 (Nullable())
    let x14 = withNullV 6L
    check "ekjnceoiwe14" x14 (Nullable(6L))
    let x15 : string? = withNull x4
    check "ekjnceoiwe15" x15 ""
    let x15a : string? = withNull ""
    check "ekjnceoiwe15a" x15a ""
    let x15b : string? = withNull<string> x4
    check "ekjnceoiwe15b" x15b ""
    let x15c : string? = withNull<string?> x4 // **Expected to give a Nullness warning
    check "ekjnceoiwe15c" x15c ""
    let x16 : Nullable<int> = withNullV 3
    check "ekjnceoiwe16" x16 (Nullable(3))
    
    let y0 = isNull null // Should not give a Nullness warning (obj)
    check "ekjnceoiwey0" y0 true
    let y1 = isNull (null: obj?) // Should not give a Nullness warning
    check "ekjnceoiwey1" y1 true
    let y1b = isNull (null: string?) // Should not give a Nullness warning
    check "ekjnceoiwey1b" y1b true
    let y2 = isNull "" // **Expected to give a Nullness warning - type instantiation of a nullable type is non-nullable string
    check "ekjnceoiwey2" y2 false
    let y9 = isNull<string> "" // **Expected to give a Nullness warning - type instantiation of a nullable type is non-nullable string
    check "ekjnceoiwey9" y9 false
    let y10 = isNull<string?> "" // Should not give a Nullness warning.
    check "ekjnceoiwey10" y10 false

module NotNullConstraint =
    let f3 (x: 'T when 'T : not null) = 1
    let v1 = f3 1 // Should not give an error
    check "ekjnceoiwev1" v1 1
    let v2 = f3 "a" // Should not give an error
    check "ekjnceoiwev2" v2 1
    let v3 = f3 (null: obj?) // Expect to give a warning
    check "ekjnceoiwev3" v3 1
    let v4 = f3 (null: string?) // Expect to give a warning
    check "ekjnceoiwev4" v4 1
#if NEGATIVE
    f3 (Some 1) // Expect to give an error
#endif

    let w1 = 1 |> f3 // Should not give an error
    check "ekjnceoiwew1" w1 1
    let w2 = "a" |> f3 // Should not give an error
    check "ekjnceoiwew2" w2 1
    let w3 = (null: obj?) |> f3 // Expect to give a warning
    check "ekjnceoiwew3" w3 1
    let w4 = (null: string?) |> f3 // Expect to give a warning
    check "ekjnceoiwew4" w4 1

module MemberBasics = 
    type C() = 
        member x.P = 1
        member x.M() = 2

    let c : C? = C()
    let v1 = c.P  // Expected to give a warning
    check "ekjnccwwecv1" v1 1
    let v2 = c.M()  // Expected to give a warning
    check "ekjnccwwecv2" v2 2
    let f1 = c.M  // Expected to give a warning
    check "ekjnccwwecv3" (f1()) 2

module Basics2 = 
    let f1 () = null
    // val f : unit -> 'a when 'a : null

    let f2 () : string? = null
    // val f : unit -> string?

    let f3 () : 'T? = null

    let f4 () : 'T? when 'T : not struct = null

    let f5 () : 'T when 'T : not struct and 'T : null = null

#if NEGATIVE
    let f6 () : 'T? when 'T : not struct and 'T : null = null // Expected to give an error about inconistent constraints
#endif

    // Note yet allowed 
    //let f7 () : 'T? when 'T : struct = null
    let f7b () : Nullable<'T> = nullV // BUG: Incorrectly gives a warning about System.ValueType with /test:AssumeNullOnImport

    let f8 () : string = null // Expected to give a Nullness warning

type C(s: string) = 
    member __.Value = s

module InteropBasics =
    let s0 = String.Concat("a","b") // Expected to infer string? with /test:AssumeNullOnImport
    check "ekjnccberpos0" s0 "ab"
    let s1 : string = String.Concat("a","c") // Expected to gives a warning with /test:AssumeNullOnImport
    check "ekjnccberpos0" s1 "ac"
    let test1()  = String.Concat("a","d")
    check "ekjnccberpos0" (test1()) "ad"
    let test2(s1:string, s2: string)  = String.Concat(s1,s2)
    let test3()  = String( [| 'a' |] )
    let test4()  = System.AppDomain.CurrentDomain
    let test5 : System.AppDomain  = System.AppDomain.CurrentDomain // Expected to gives a warning with /test:AssumeNullOnImport

type KonsoleWithNulls = 
    static member WriteLine(s: string?) = Console.WriteLine(s)
    static member WriteLine(fmt: string?, arg1: string?) = Console.WriteLine(fmt, arg1)
    static member WriteLine(fmt: string?, [<ParamArray>] args: obj?[]?) = Console.WriteLine(fmt, args)
    static member WriteLineC(s: C?) = Console.WriteLine(s.Value)
    static member WriteLineC(fmt: C?, arg1: C?) = Console.WriteLine(fmt.Value, arg1.Value)

module KonsoleWithNullsModule = 
    let WriteLine(s: string?) = Console.WriteLine(s)
    let WriteLine2(fmt: string?, arg1: string?) = Console.WriteLine(fmt, arg1)
    let WriteLineC(s: C?) = Console.WriteLine(s.Value)
    let WriteLineC2(fmt: C?, arg1: C?) = Console.WriteLine(fmt.Value, arg1.Value)

module KonsoleWithNullsModule2 = 
    let WriteLine x = KonsoleWithNullsModule.WriteLine x
    let WriteLine2 (fmt, arg1) = KonsoleWithNullsModule.WriteLine2(fmt, arg1)
    let WriteLineC(s) = KonsoleWithNullsModule.WriteLineC(s)
    let WriteLineC2(fmt, arg1) = KonsoleWithNullsModule.WriteLineC2(fmt, arg1)

type KonsoleNoNulls = 
    static member WriteLine(s: string) = Console.WriteLine(s)
    static member WriteLine(fmt: string, arg1: string?) = Console.WriteLine(fmt, arg1)
    static member WriteLine(fmt: string, [<ParamArray>] args: obj[]) = Console.WriteLine(fmt, args)
    static member WriteLineC(s: C) = Console.WriteLine(s.Value)
    static member WriteLineC(fmt: C, arg1: C) = Console.WriteLine(fmt.Value, arg1.Value)

module KonsoleNoNullsModule = 
    let WriteLine(s: string) = Console.WriteLine(s)
    let WriteLine2(fmt: string, arg1: string) = Console.WriteLine(fmt, arg1)
    let WriteLineC(s: C) = Console.WriteLine(s.Value)
    let WriteLineC2(fmt: C, arg1: C) = Console.WriteLine(fmt.Value, arg1.Value)

module KonsoleNoNullsModule2 = 
    let WriteLine x = KonsoleNoNullsModule.WriteLine x
    let WriteLine2 (fmt, arg1) = KonsoleNoNullsModule.WriteLine2(fmt, arg1)
    let WriteLineC(s) = KonsoleNoNullsModule.WriteLineC(s)
    let WriteLineC2(fmt, arg1) = KonsoleNoNullsModule.WriteLineC2(fmt, arg1)

System.Console.WriteLine("a")
System.Console.WriteLine("a", (null: obj[])) // Expected to give a Nullness warning

KonsoleWithNulls.WriteLine("Hello world")
KonsoleWithNulls.WriteLine(null) // WRONG: gives an incorrect Nullness warning for string? and string?
KonsoleWithNulls.WriteLine("Hello","world")
KonsoleWithNulls.WriteLine("Hello","world","there") // // WRONG: gives an incorrect Nullness warning for string? and string?

KonsoleNoNulls.WriteLine("Hello world")
try 
   KonsoleNoNulls.WriteLine(null)  // Expected to give a Nullness warning
with :? System.ArgumentNullException -> ()
KonsoleNoNulls.WriteLine("Hello","world")
//KonsoleNoNulls.WriteLine("Hello",null) // CHECK ME 
try 
    KonsoleNoNulls.WriteLine(null, "World")   // Expected to give a Nullness warning
with :? System.ArgumentNullException -> ()

KonsoleWithNullsModule.WriteLine("Hello world")
try 
    KonsoleWithNullsModule.WriteLine(null) 
with :? System.ArgumentNullException -> ()
KonsoleWithNullsModule.WriteLine2("Hello","world") 
KonsoleWithNullsModule.WriteLine2("Hello",null)
try
    KonsoleWithNullsModule.WriteLine2(null,"world")
with :? System.ArgumentNullException -> ()

KonsoleWithNullsModule2.WriteLine("Hello world")
try 
    KonsoleWithNullsModule2.WriteLine(null) 
with :? System.ArgumentNullException -> ()
KonsoleWithNullsModule2.WriteLine2("Hello","world")
KonsoleWithNullsModule2.WriteLine2("Hello",null)
try 
    KonsoleWithNullsModule2.WriteLine2(null,"world")
with :? System.ArgumentNullException -> ()

KonsoleNoNullsModule.WriteLine("Hello world")
try 
    KonsoleNoNullsModule.WriteLine(null)  // Expected to give a Nullness warning
with :? System.ArgumentNullException -> ()
KonsoleNoNullsModule.WriteLine2("Hello","world")
KonsoleNoNullsModule.WriteLine2("Hello",null) // Expected to give a Nullness warning
try 
    KonsoleNoNullsModule.WriteLine2(null,"world") // Expected to give a Nullness warning
with :? System.ArgumentNullException -> ()

//-------------------------------------

// Param array cases

KonsoleNoNulls.WriteLine("Hello","world","there")
KonsoleWithNulls.WriteLine("Hello","world",null)  // Expected to give a Nullness warning 
KonsoleNoNulls.WriteLine("Hello","world",null)  // Expected to give a Nullness warning
System.Console.WriteLine("a", (null: obj[]?)) 
System.Console.WriteLine("a", (null: obj?[]?))

//-------
// random stuff

let f0 line = 
    let add (s:string) = ()
    match line with 
    | null | "" -> ()
    | _ -> add line // Exected to give a nullness warning

let f0b line = 
    let add (s:string) = ()
    match line with 
    | null  -> ()
    | _ -> add (nonNull<string> line) // Exected to give a nullness warning

let add (s:string) = ()
let f0c line = 
    add (nonNull<string> "") // WRONG: should not give a nullness warning

//let f2 (x: string or null) = x;;
let f3 (x: string?) = x
let f5 x = (x: int)
//let f4 x = (x: string nullable)

//let f6<'T when 'T : not null> (x: 'T) = x
//let f6<'T when 'T : not null> (x: 'T) = x

//let f2 (x: string | null) = x;;

//let f2 (_x : string | null) = x;;

//let f2 (x: (string | null)) = x;;

//let f3 x = (x: (string | null))

module NullConstraintTests =
    type C<'T when 'T : null>() = class end

#if NEGATIVE
    let f1 (y : C< (int * int) >) = y // This gave an error in F# 4.5 and we expect it to continue to give an error

#endif

#if !NO_CHECKNULLS
     // This gave an error in F# 4.5.  It now only gives a warning when --checknulls is on which is sort of ok
     // since we are treating .NET and F# types more symmetrically.
     //
     // TODO: However it gives no error or warning at all with --checknulls off in F# 5.0...  That seems bad.
    let f2 (y : C<int list>) = y

    let f3 (y : C<string>) = y // Expect a Nullness warning

    let f4 (y : C<string?>) = y // No warning expected 

    let f5 (y : C<int list?>) = y // No warning expected

    let f6 (y : C<list<string>?>) = y // No warning expected, lexing/parsing should succeed 

    let f7 (y : C<list<string>>?) = y // No warning expected, lexing/parsing should succeed
#endif


module DefaultValueTests =


    module StructExamples = 
        [<Struct>]
        type C1 =
            [<DefaultValue>]
            val mutable Whoops : string // expect a warning

        [<Struct>]
        type C2 =
            [<DefaultValue(false)>]
            val mutable Whoops : string // expect no warning

        [<Struct>]
        type C3 =
            [<DefaultValue>]
            val mutable Whoops : string? // expect no warning

#if NEGATIVE
        [<Struct>]
        type C4a =
            [<DefaultValue>]
            val mutable Whoops : int list // expect a hard error like in F# 4.5
#endif

        [<Struct>]
        type C4b =
            [<DefaultValue>]
            val mutable Whoops : int list? // expect no warning

#if NEGATIVE
        [<Struct>]
        type C5 =
            [<DefaultValue>]
            val mutable Whoops : int * int // expect an error like F# 4.5

        [<Struct>]
        type C6 =
            [<DefaultValue>]
            val mutable Whoops : int -> int // expect an error like F# 4.5
#endif

        [<Struct>]
        type C7 =
            [<DefaultValue>]
            val mutable Whoops : (int -> int)? // expect no warning

    module ClassExamples = 
        type C1 =
            [<DefaultValue>]
            val mutable Whoops : string // expect a warning

        type C2 =
            [<DefaultValue(false)>]
            val mutable Whoops : string // expect no warning

        type C3 =
            [<DefaultValue>]
            val mutable Whoops : string? // expect no warning

        type C4a =
            [<DefaultValue>]
            val mutable Whoops : int list // ** expect a warning

        type C4b =
            [<DefaultValue>]
            val mutable Whoops : int list? // expect no warning

    #if NEGATIVE
        type C5 =
            [<DefaultValue>]
            val mutable Whoops : int * int // expect an error like F# 4.5

        type C6 =
            [<DefaultValue>]
            val mutable Whoops : int -> int // expect an error like F# 4.5
    #endif

        type C7 =
            [<DefaultValue>]
            val mutable Whoops : (int -> int)? // expect no warning

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

                    