// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Core.UnitTests

open Xunit

// Various tests for the:
// Microsoft.FSharp.Core.Option module

(*
[Test Strategy]
Make sure each method works on:
* Integer option (value type)
* String option  (reference type)
* None   (0 elements)
*)

type OptionModule() =

    let assertWasNotCalledThunk () = raise (exn "Thunk should not have been called.")

    [<Fact>]
    member this.Flatten () =
        Assert.AreEqual( Option.flatten None, None)
        Assert.AreEqual( Option.flatten (Some None), None)
        Assert.AreEqual( Option.flatten (Some <| Some 1), Some 1)
        Assert.AreEqual( Option.flatten (Some <| Some ""), Some "") 

    [<Fact>]
    member this.FilterSomeIntegerWhenPredicateReturnsTrue () =
        let test x =
            let actual = x |> Some |> Option.filter (fun _ -> true)

            let expected = x |> Some
            Assert.AreEqual(expected, actual)            
        [0;1;-1;42] |> List.iter test

    [<Fact>]
    member this.FilterSomeStringWhenPredicateReturnsTrue () =
        let test x =
            let actual = x |> Some |> Option.filter (fun _ -> true)

            let expected = x |> Some
            Assert.AreEqual(expected, actual)
        [""; " "; "Foo"; "Bar"] |> List.iter test

    [<Fact>]
    member this.FilterSomeIntegerWhenPredicateReturnsFalse () =
        let test x =
            let actual = x |> Some |> Option.filter (fun _ -> false)

            let expected = None
            Assert.AreEqual(expected, actual)
        [0; 1; -1; 1337] |> List.iter test

    [<Fact>]
    member this.FilterSomeStringWhenPredicateReturnsFalse () =
        let test x =
            let actual = x |> Some |> Option.filter (fun _ -> false)

            let expected = None
            Assert.AreEqual(expected, actual)
        [""; "  "; "Ploeh"; "Fnaah"] |> List.iter test

    [<Fact>]
    member this.FilterNoneReturnsCorrectResult () =
        let test x =
            let actual = None |> Option.filter (fun _ -> x)

            let expected = None
            Assert.AreEqual(expected, actual)
        [false; true] |> List.iter test

    [<Fact>]
    member this.FilterSomeIntegerWhenPredicateEqualsInput () =
        let test x =
            let actual = x |> Some |> Option.filter ((=) x)

            let expected = x |> Some
            Assert.AreEqual(expected, actual)
        [0; 1; -1; -2001] |> List.iter test

    [<Fact>]
    member this.FilterSomeStringWhenPredicateEqualsInput () =
        let test x =
            let actual = x |> Some |> Option.filter ((=) x)

            let expected = x |> Some
            Assert.AreEqual(expected, actual)
        [""; "     "; "Xyzz"; "Sgryt"] |> List.iter test

    [<Fact>]
    member this.FilterSomeIntegerWhenPredicateDoesNotEqualsInput () =
        let test x =
            let actual = x |> Some |> Option.filter ((<>) x)

            let expected = None
            Assert.AreEqual(expected, actual)
        [0; 1; -1; 927] |> List.iter test

    [<Fact>]
    member this.FilterSomeStringWhenPredicateDoesNotEqualsInput () =
        let test x =
            let actual = x |> Some |> Option.filter ((<>) x)

            let expected = None
            Assert.AreEqual(expected, actual)
        [""; "     "; "Baz Quux"; "Corge grault"] |> List.iter test

    [<Fact>]
    member this.Contains() =
        Assert.False( Option.contains 1 None)
        Assert.True( Option.contains 1 (Some 1))

        Assert.False( Option.contains "" None)
        Assert.True( Option.contains "" (Some ""))

        Assert.False( Option.contains None None)
        Assert.True( Option.contains None (Some None))
    [<Fact>]
    member this.OfToNullable() =
        Assert.True( Option.ofNullable (System.Nullable<int>()) = None)
        Assert.True( Option.ofNullable (System.Nullable<int>(3)) = Some 3)

        Assert.True( Option.toNullable (None : int option) = System.Nullable<int>())
        Assert.True( Option.toNullable (None : System.DateTime option) = System.Nullable())
        Assert.True( Option.toNullable (Some 3) = System.Nullable(3))

    [<Fact>]
    member this.OfToObj() =
        Assert.True( Option.toObj (Some "3") = "3")
        Assert.True( Option.toObj (Some "") = "")
        Assert.True( Option.toObj<obj> (Some null) = null) // TODO NULLNESS: this type annotation should not be needed 
        Assert.True( Option.toObj None = null)     
     
        Assert.True( Option.ofObj "3" = Some "3")
        Assert.True( Option.ofObj "" = Some "")
        Assert.True( Option.ofObj [| "" |] = Some [| "" |])
        Assert.True( Option.ofObj (null : string array) = None)
        Assert.True( Option.ofObj<string> null = None)
        Assert.True( Option.ofObj<string[]> null = None)
        Assert.True( Option.ofObj<int[]> null = None)

    [<Fact>]
    member this.DefaultValue() =
        Assert.AreEqual( Option.defaultValue 3 None, 3)
        Assert.AreEqual( Option.defaultValue 3 (Some 42), 42)
        Assert.AreEqual( Option.defaultValue "" None, "")
        Assert.AreEqual( Option.defaultValue "" (Some "x"), "x")

    [<Fact>]
    member this.DefaultWith() =
        Assert.AreEqual( Option.defaultWith (fun () -> 3) None, 3)
        Assert.AreEqual( Option.defaultWith (fun () -> "") None, "")

        Assert.AreEqual( Option.defaultWith assertWasNotCalledThunk (Some 42), 42)
        Assert.AreEqual( Option.defaultWith assertWasNotCalledThunk (Some ""), "")

    [<Fact>]
    member this.OrElse() =
        Assert.AreEqual( Option.orElse None None, None)
        Assert.AreEqual( Option.orElse (Some 3) None, Some 3)
        Assert.AreEqual( Option.orElse None (Some 42), Some 42)
        Assert.AreEqual( Option.orElse (Some 3) (Some 42), Some 42)

        Assert.AreEqual( Option.orElse (Some "") None, Some "")
        Assert.AreEqual( Option.orElse None (Some "x"), Some "x")
        Assert.AreEqual( Option.orElse (Some "") (Some "x"), Some "x")

    [<Fact>]
    member this.OrElseWith() =
        Assert.AreEqual( Option.orElseWith (fun () -> None) None, None)
        Assert.AreEqual( Option.orElseWith (fun () -> Some 3) None, Some 3)
        Assert.AreEqual( Option.orElseWith (fun () -> Some "") None, Some "")

        Assert.AreEqual( Option.orElseWith assertWasNotCalledThunk (Some 42), Some 42)
        Assert.AreEqual( Option.orElseWith assertWasNotCalledThunk (Some ""), Some "")

    [<Fact>]
    member this.Map2() =
        Assert.AreEqual( Option.map2 (-) None None, None)
        Assert.AreEqual( Option.map2 (-) (Some 1) None, None)
        Assert.AreEqual( Option.map2 (-) None (Some 2), None)
        Assert.AreEqual( Option.map2 (-) (Some 1) (Some 2), Some -1)

        Assert.AreEqual( Option.map2 (+) None None, None)
        Assert.AreEqual( Option.map2 (+) (Some "x") None, None)
        Assert.AreEqual( Option.map2 (+) None (Some "y"), None)
        Assert.AreEqual( Option.map2 (+) (Some "x") (Some "y"), Some "xy")

    [<Fact>]
    member this.Map3() =
        let add3 x y z = string x + string y + string z
        Assert.AreEqual( Option.map3 add3 None None None, None)
        Assert.AreEqual( Option.map3 add3 (Some 1) None None, None)
        Assert.AreEqual( Option.map3 add3 None (Some 2) None, None)
        Assert.AreEqual( Option.map3 add3 (Some 1) (Some 2) None, None)
        Assert.AreEqual( Option.map3 add3 None None (Some 3), None)
        Assert.AreEqual( Option.map3 add3 (Some 1) None (Some 3), None)
        Assert.AreEqual( Option.map3 add3 None (Some 2) (Some 3), None)
        Assert.AreEqual( Option.map3 add3 (Some 1) (Some 2) (Some 3), Some "123")

        let concat3 x y z = x + y + z
        Assert.AreEqual( Option.map3 concat3 None None None, None)
        Assert.AreEqual( Option.map3 concat3 (Some "x") None None, None)
        Assert.AreEqual( Option.map3 concat3 None (Some "y") None, None)
        Assert.AreEqual( Option.map3 concat3 (Some "x") (Some "y") None, None)
        Assert.AreEqual( Option.map3 concat3 None None (Some "z"), None)
        Assert.AreEqual( Option.map3 concat3 (Some "x") None (Some "z"), None)
        Assert.AreEqual( Option.map3 concat3 None (Some "y") (Some "z"), None)
        Assert.AreEqual( Option.map3 concat3 (Some "x") (Some "y") (Some "z"), Some "xyz")

    [<Fact>]
    member this.MapBindEquivalenceProperties () =
        let fn x = x + 3
        Assert.AreEqual(Option.map fn None, Option.bind (fn >> Some) None)
        Assert.AreEqual(Option.map fn (Some 5), Option.bind (fn >> Some) (Some 5))

type ValueOptionTests() =

    let assertWasNotCalledThunk () = raise (exn "Thunk should not have been called.")

    [<Fact>]
    member _.``ValueNone gives "ValueNone" when calling ToString`` () =
        Assert.AreEqual("ValueNone", ValueNone.ToString())
        Assert.AreEqual("ValueNone", string ValueNone)
    
    [<Fact>]
    member _.``ValueNone with sprintf`` () =
        Assert.AreEqual("ValueNone", sprintf "%O" (ValueNone.ToString()))
        Assert.AreEqual("ValueNone", sprintf "%A" ValueNone)

    [<Fact>]
    member this.ValueOptionBasics () =
        Assert.AreEqual((ValueNone: int voption), (ValueNone: int voption))
        Assert.True((ValueNone: int voption) <= (ValueNone: int voption))
        Assert.True((ValueNone: int voption) >= (ValueNone: int voption))
        Assert.True((ValueNone: int voption) < (ValueSome 1: int voption))
        Assert.True((ValueSome 0: int voption) < (ValueSome 1: int voption))
        Assert.True((ValueSome 1: int voption) > (ValueSome 0: int voption))
        Assert.False((ValueSome 1: int voption) < (ValueNone : int voption))
        Assert.True((ValueSome 1: int voption) <= (ValueSome 1: int voption))
        Assert.AreEqual(compare (ValueSome 1) (ValueSome 1), 0)
        Assert.True(compare (ValueSome 0) (ValueSome 1) < 0)
        Assert.True(compare (ValueNone: int voption) (ValueSome 1) < 0)
        Assert.True(compare (ValueSome 1) (ValueNone : int voption) > 0)
        Assert.AreEqual(ValueSome 1, ValueSome 1)
        Assert.AreNotEqual(ValueSome 2, ValueSome 1)
        Assert.AreEqual(ValueSome 2, ValueSome 2)
        Assert.AreEqual(ValueSome (ValueSome 2), ValueSome (ValueSome 2))
        Assert.AreNotEqual(ValueSome (ValueSome 2), ValueSome (ValueSome 1))
        Assert.AreNotEqual(ValueSome (ValueSome 0), ValueSome ValueNone)
        Assert.AreEqual(ValueSome (ValueNone: int voption), ValueSome (ValueNone: int voption))
        Assert.AreEqual((ValueSome (ValueNone: int voption)).Value, (ValueNone: int voption))
        Assert.AreEqual((ValueSome 1).Value, 1)
        Assert.AreEqual((ValueSome (1,2)).Value, (1,2))
        Assert.AreEqual(defaultValueArg ValueNone 1, 1)
        Assert.AreEqual(defaultValueArg (ValueSome 3) 1, 3)
    
    [<Fact>]
    member this.Flatten () =
        Assert.AreEqual(ValueOption.flatten ValueNone, ValueNone)
        Assert.AreEqual(ValueOption.flatten (ValueSome ValueNone), ValueNone)
        Assert.AreEqual(ValueOption.flatten (ValueSome <| ValueSome 1), ValueSome 1)
        Assert.AreEqual(ValueOption.flatten (ValueSome <| ValueSome ""), ValueSome "") 

    [<Fact>]
    member this.FilterValueSomeIntegerWhenPredicateReturnsTrue () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter (fun _ -> true)

            actual = ValueSome x
            |> Assert.True
        [0;1;-1;42] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeStringWhenPredicateReturnsTrue () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter (fun _ -> true)

            actual = ValueSome x
            |> Assert.True
        [""; " "; "Foo"; "Bar"] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeIntegerWhenPredicateReturnsFalse () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter (fun _ -> false)

            actual = ValueNone
            |> Assert.True
        [0; 1; -1; 1337] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeStringWhenPredicateReturnsFalse () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter (fun _ -> false)

            actual= ValueNone
            |> Assert.True
        [""; "  "; "Ploeh"; "Fnaah"] |> List.iter test

    [<Fact>]
    member this.FilterValueNoneReturnsCorrectResult () =
        let test x =
            let actual = ValueNone |> ValueOption.filter (fun _ -> x)

            actual = ValueNone
            |> Assert.True
        [false; true] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeIntegerWhenPredicateEqualsInput () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter ((=) x)

            actual = ValueSome x
            |> Assert.True
        [0; 1; -1; -2001] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeStringWhenPredicateEqualsInput () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter ((=) x)

            actual = ValueSome x
            |> Assert.True
        [""; "     "; "Xyzz"; "Sgryt"] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeIntegerWhenPredicateDoesNotEqualsInput () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter ((<>) x)

            actual = ValueNone
            |> Assert.True
        [0; 1; -1; 927] |> List.iter test

    [<Fact>]
    member this.FilterValueSomeStringWhenPredicateDoesNotEqualsInput () =
        let test x =
            let actual = x |> ValueSome |> ValueOption.filter ((<>) x)

            actual = ValueNone
            |> Assert.True
        [""; "     "; "Baz Quux"; "Corge grault"] |> List.iter test

    [<Fact>]
    member this.Contains() =
        Assert.False(ValueOption.contains 1 ValueNone)
        Assert.True(ValueOption.contains 1 (ValueSome 1))

        Assert.False(ValueOption.contains "" ValueNone)
        Assert.True(ValueOption.contains "" (ValueSome ""))

        Assert.False(ValueOption.contains ValueNone ValueNone)
        Assert.True(ValueOption.contains ValueNone (ValueSome ValueNone))
    [<Fact>]
    member this.OfToNullable() =
        Assert.True(ValueOption.ofNullable (System.Nullable<int>()) = ValueNone)
        Assert.True(ValueOption.ofNullable (System.Nullable<int>(3)) = ValueSome 3)

        Assert.True(ValueOption.toNullable (ValueNone : int voption) = System.Nullable<int>())
        Assert.True(ValueOption.toNullable (ValueNone : System.DateTime voption) = System.Nullable())
        Assert.True(ValueOption.toNullable (ValueSome 3) = System.Nullable(3))

    [<Fact>]
    member this.OfToObj() =
        Assert.True(ValueOption.toObj (ValueSome "3") = "3")
        Assert.True(ValueOption.toObj (ValueSome "") = "")
        Assert.True(ValueOption.toObj<obj> (ValueSome null) = null)  // TODO NULLNESS: this type annotation should not be needed
        Assert.True(ValueOption.toObj ValueNone = null)     
     
        Assert.True(ValueOption.ofObj "3" = ValueSome "3")
        Assert.True(ValueOption.ofObj "" = ValueSome "")
        Assert.True(ValueOption.ofObj [| "" |] = ValueSome [| "" |])
        Assert.True(ValueOption.ofObj (null : string array) = ValueNone)
        Assert.True(ValueOption.ofObj<string> null = ValueNone)
        Assert.True(ValueOption.ofObj<string[]> null = ValueNone)
        Assert.True(ValueOption.ofObj<int[]> null = ValueNone)

    [<Fact>]
    member this.DefaultValue() =
        Assert.AreEqual(ValueOption.defaultValue 3 ValueNone, 3)
        Assert.AreEqual(ValueOption.defaultValue 3 (ValueSome 42), 42)
        Assert.AreEqual(ValueOption.defaultValue "" ValueNone, "")
        Assert.AreEqual(ValueOption.defaultValue "" (ValueSome "x"), "x")

    [<Fact>]
    member this.DefaultWith() =
        Assert.AreEqual(ValueOption.defaultWith (fun () -> 3) ValueNone, 3)
        Assert.AreEqual(ValueOption.defaultWith (fun () -> "") ValueNone, "")

        Assert.AreEqual(ValueOption.defaultWith assertWasNotCalledThunk (ValueSome 42), 42)
        Assert.AreEqual(ValueOption.defaultWith assertWasNotCalledThunk (ValueSome ""), "")

    [<Fact>]
    member this.OrElse() =
        Assert.AreEqual(ValueOption.orElse ValueNone ValueNone, ValueNone)
        Assert.AreEqual(ValueOption.orElse (ValueSome 3) ValueNone, ValueSome 3)
        Assert.AreEqual(ValueOption.orElse ValueNone (ValueSome 42), ValueSome 42)
        Assert.AreEqual(ValueOption.orElse (ValueSome 3) (ValueSome 42), ValueSome 42)

        Assert.AreEqual(ValueOption.orElse (ValueSome "") ValueNone, ValueSome "")
        Assert.AreEqual(ValueOption.orElse ValueNone (ValueSome "x"), ValueSome "x")
        Assert.AreEqual(ValueOption.orElse (ValueSome "") (ValueSome "x"), ValueSome "x")

    [<Fact>]
    member this.OrElseWith() =
        Assert.AreEqual(ValueOption.orElseWith (fun () -> ValueNone) ValueNone, ValueNone)
        Assert.AreEqual(ValueOption.orElseWith (fun () -> ValueSome 3) ValueNone, ValueSome 3)
        Assert.AreEqual(ValueOption.orElseWith (fun () -> ValueSome "") ValueNone, ValueSome "")

        Assert.AreEqual(ValueOption.orElseWith assertWasNotCalledThunk (ValueSome 42), ValueSome 42)
        Assert.AreEqual(ValueOption.orElseWith assertWasNotCalledThunk (ValueSome ""), ValueSome "")

    [<Fact>]
    member this.Map2() =
        Assert.True(ValueOption.map2 (-) ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map2 (-) (ValueSome 1) ValueNone = ValueNone)
        Assert.True(ValueOption.map2 (-) ValueNone (ValueSome 2) = ValueNone)
        Assert.True(ValueOption.map2 (-) (ValueSome 1) (ValueSome 2) = ValueSome -1)

        Assert.True(ValueOption.map2 (+) ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map2 (+) (ValueSome "x") ValueNone = ValueNone)
        Assert.True(ValueOption.map2 (+) (ValueSome "x") (ValueSome "y") = ValueSome "xy")
        Assert.True(ValueOption.map2 (+) ValueNone (ValueSome "y") = ValueNone)

    [<Fact>]
    member this.Map3() =
        let add3 x y z = string x + string y + string z
        Assert.True(ValueOption.map3 add3 ValueNone ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map3 add3 (ValueSome 1) ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map3 add3 ValueNone (ValueSome 2) ValueNone = ValueNone)
        Assert.True(ValueOption.map3 add3 (ValueSome 1) (ValueSome 2) ValueNone = ValueNone)
        Assert.True(ValueOption.map3 add3 ValueNone ValueNone (ValueSome 3) = ValueNone)
        Assert.True(ValueOption.map3 add3 (ValueSome 1) ValueNone (ValueSome 3) = ValueNone)
        Assert.True(ValueOption.map3 add3 ValueNone (ValueSome 2) (ValueSome 3) = ValueNone)
        Assert.True(ValueOption.map3 add3 (ValueSome 1) (ValueSome 2) (ValueSome 3) = ValueSome "123")

        let concat3 x y z = x + y + z
        Assert.True(ValueOption.map3 concat3 ValueNone ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map3 concat3 (ValueSome "x") ValueNone ValueNone = ValueNone)
        Assert.True(ValueOption.map3 concat3 ValueNone (ValueSome "y") ValueNone = ValueNone)
        Assert.True(ValueOption.map3 concat3 (ValueSome "x") (ValueSome "y") ValueNone = ValueNone)
        Assert.True(ValueOption.map3 concat3 ValueNone ValueNone (ValueSome "z") = ValueNone)
        Assert.True(ValueOption.map3 concat3 (ValueSome "x") ValueNone (ValueSome "z") = ValueNone)
        Assert.True(ValueOption.map3 concat3 ValueNone (ValueSome "y") (ValueSome "z") = ValueNone)
        Assert.True(ValueOption.map3 concat3 (ValueSome "x") (ValueSome "y") (ValueSome "z") = ValueSome "xyz")

    [<Fact>]
    member this.MapBindEquivalenceProperties () =
        let fn x = x + 3
        Assert.AreEqual(ValueOption.map fn ValueNone, ValueOption.bind (fn >> ValueSome) ValueNone)
        Assert.AreEqual(ValueOption.map fn (ValueSome 5), ValueOption.bind (fn >> ValueSome) (ValueSome 5))
