// #Conformance #LexFilter #Exceptions 
#light

[<AutoOpen>]
type IProvideTestFunctionX =
    static member x (y: obj, ?z: obj) = ()
    static member x'<'T> (y: 'T, ?z: obj) = y

module BeginEnd =
    let a = begin
        2
    end
    let b = id begin
        2
    end
    let c =
        if true then ignore begin
            2
        end
    let d =
        if true then begin 1 end else begin
            2
        end
    let e =
        if true then begin
            1
        end else begin
            2
        end
    let (
        _
     ) as f = begin
        2
    end
    let (
        _
    ) = begin
        3
    end
    let h =
        if begin
            true
        end
        then begin
         end
    let i =
        if x' begin
            false
        end
        then begin
         end
    let a0 = begin 2 =
        2
    end
    let b0 = x begin y =
        2
    end
    let c0 =
        if true then x begin y =
            2
        end
    let d0 =
        if true then x begin y = 1 end else x begin y =
            2
        end
    let e0 =
        if true then x begin y =
            1
        end else x begin y =
            2
        end
    let (
        _
     ) as f0 = x begin y =
        2
    end
    let (
        _
    ) = begin 2 =
        3
    end
    let h0 =
        if begin 2 =
            2
        end
        then begin
         end
    let i0 =
        if x' begin y =
            true
        end
        then begin
         end
    let a1 = begin 2 = 2,
        3 = 2
    end
    let b1 = x begin y = 2,
        z = 2
    end
    let c1 =
        if true then x begin y = 2,
            z = 3
        end
    let d1 =
        if true then x begin y = 1 end else x begin y = 2,
            z = 3
        end
    let e1 =    
        if true then x begin y = 1,
            z = 3
        end else x begin y = 2,
            z = 3
        end
    let (
        _
     ) as f1 = x begin y = 2,
        z = 3
    end
    let (
        _
    ) = begin 2 = 3,
        3 = 3
    end
    let h1 =
        if begin 2 = 2 |> ignore;
            3 = 3
        end
        then begin
         end
    let i1 =
        if x' begin y = true,
            z = false
        end
        then begin
         end
type Paren = (
    int
)
module Parens =
    let a = (
        2
    )
    let b = id (
        2
    )
    let c =
        if true then ignore (
            2
        )
    let d =
        if true then ( 1 ) else (
            2
        )
    let e =
        if true then (
            1
        ) else (
            2
        )
    let (
        _
     ) as f = (
        2
    )
    let (
        _
    ) = (
        3
    )
    let a0 = (2 =
        2
    )
    let b0 = x (y =
        2
    )
    let c0 =
        if true then x (y =
            2
        )
    let d0 =
        if true then x ( y = 1 ) else x (y =
            2
        )
    let e0 =
        if true then x (y =
            1
        ) else x (y =
            2
        )
    let (
        _
     ) as f0 = x (y =
        2
    )
    let (
        _
    ) = (2 =
        3
    )
    let a1 = (2 = 2,
        3 = 3
    )
    let b1 = x (y = 2,
        z = 3
    )
    let c1 =
        if true then x (y = 2,
            z = 3
        )
    let d1 =
        if true then x ( y = 1 ) else x (y = 2,
            z = 3
        )
    let e1 =
        if true then x (y = 1,
            z = 3
        ) else x (y = 2,
            z = 3
        )
    let (
        _
     ) as f1 = x (y = 2,
        z = 3
    )
    let (
        _
    ) = (2 = 3,
        3 = 3
    )
// These are lexed differently but it's visually appealing to still include
module ActivePatterns =
    let (|
        A
    |) = id
    let (|
        B
    |) _ = (|
        A
    |)
    let (|C|) =
        if true then ignore (|
            A
        |)
    let d<'a, 'b> =
        if true then (| A |) else (|
            A
        |)
    let e<'a, 'b> =
        if true then (|
            A
        |) else (|
            A
        |)
    let (|
        F
        |
        _
    |) as f = Some (|
        C
    |)
    let (|
        G
        |
        _
    |) = (|
        F
        |
        _
    |)
module Lists =
    let a = [
        2
    ]
    let b = id [
        2
    ]
    let c =
        if true then ignore [
            2
        ]
    let d =
        if true then [ 1 ] else [
            2
        ]
    let e =
        if true then [
            1
        ] else [
            2
        ]
    let [
        _
     ] as f = [
        2
    ]
    let [
        _
    ] = [
        3
    ]
    let a0 = [ 2 =
        2
    ]
    let b0 = x [ 2 =
        2
    ]
    let c0 =
        if true then x [ 2 =
            2
        ]
    let d0 =
        if true then x [ 2 = 1 ] else x [ 2 =
            2
        ]
    let e0 =
        if true then x [ 2 =
            1
        ] else x [ 2 =
            2
        ]
    let [
        _
     ] as f0 = x' [ 2 =
        2
    ]
    let [
        _
    ] = [ 2 =
        3
    ]
    let a1 = [ 2 = 2,
        3 = 3
    ]
    let b1 = x [ 2 = 2,
        3 = 3
    ]
    let c1 =
        if true then x [ 2 = 2,
            3 = 3
        ]
    let d1 =
        if true then x [ 2 = 1 ] else x [ 2 = 2,
            3 = 3
        ]
    let e1 =
        if true then x [ 2 = 1,
            3 = 3
        ] else x [ 2 = 2,
            3 = 3
        ]
    let [
        _
     ] as f1 = x' [ 2 = 2,
        3 = 3
    ]
    let [
        _
    ] = [ 2 = 3,
        3 = 3
    ]
module Arrays =
    let a = [|
        2
    |]
    let b = id [|
        2
    |]
    let c =
        if true then ignore [|
            2
        |]
    let d =
        if true then [| 1 |] else [|
            2
        |]
    let e =
        if true then [|
            1
        |] else [|
            2
        |]
    let [|
        _
     |] as f = [|
        2
    |]
    let [|
        _
    |] = [|
        3
    |]
    let a0 = [| 2 =
        2
    |]
    let b0 = x [| 2 =
        2
    |]
    let c0 =
        if true then x [| 2 =
            2
        |]
    let d0 =
        if true then x [| 2 = 1 |] else x [| 2 =
            2
        |]
    let e0 =
        if true then x [| 2 =
            1
        |] else x [| 2 =
            2
        |]
    let [|
        _
     |] as f0 = x' [| 2 =
        2
    |]
    let [|
        _
    |] = [| 2 =
        3
    |]
    let a1 = [| 2 = 2,
        3 = 3
    |]
    let b1 = x [| 2 = 2,
        3 = 3
    |]
    let c1 =
        if true then x [| 2 = 2,
            3 = 3
        |]
    let d1 =
        if true then x [| 2 = 1 |] else x [| 2 = 2,
            3 = 3
        |]
    let e1 =
        if true then x [| 2 = 1,
            3 = 3
        |] else x [| 2 = 2,
            3 = 3
        |]
    let [|
        _
     |] as f1 = x' [| 2 = 2,
        3 = 3
    |]
    let [|
        _
    |] = [| 2 = 3,
        3 = 3
    |]
type Record = {
    y : int
}
type Record2 = {
    x : int; z : Record
}
module Records =
    let a = {
        y = 2
    }
    let b = id {
        y = 2
    }
    let c =
        if true then ignore {
            y = 2
        }
    let d =
        if true then { y = 1 } else {
            y = 2
        }
    let e =
        if true then {
            y = 1
        } else {
            y = 2
        }
    let {
        y = _
     } as f = {
        y = 2
    }
    let {
        y = _
    } = {
        f with y = 3
    }
    let a0 = { y =
        2
    }
    let b0 = x { y =
        2
    }
    let c0 =
        if true then x { y =
            2
        }
    let d0 =
        if true then x { y = 1 } else x { y =
            2
        }
    let e0 =
        if true then { y =
            1
        } else { y =
            2
        }
    let { z = {
        y = _
     }} as f0 = { z = {
        y = 2
    }; x = 1}
    let { z = {
        y = _
    }} = { f0 with z = {
               y = 3
           }
    }
    let a1 = { x = 2;
        z = { y = 3 }
    }
    let b1 = x { x = 2;
        z = { y = 3 }
    }
    let c1 =
        if true then x { x = 2;
            z = { y = 3 }
        }
    let d1 =
        if true then x { y = 1 } else x { x = 2;
            z = { y = 3 }
        }
    let e1 =
        if true then { x = 1;
            z = { y = 3 }
        } else { x = 2;
            z = { y = 3 }
        }
    let { z = {
        y = _
     }} as f1 = { x = 1;
     z = {
        y = 2
    } }
    let { x = _; z = {
        y = _
    }} = { f1 with x = 2; z = {
               y = 3
           }
    }
type AnonymousRecord = {|
    y : int
|}
module AnonymousRecords =
    let a = {|
        y = 2
    |}
    let b = id {|
        y = 2
    |}
    let c =
        if true then ignore {|
            y = 2
        |}
    let d =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    let e =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    let f : {|
        y : int
     |} = {|
        y = 2
    |}
    let g : {|
        y : int
    |} = {|
        f with y = 3
    |}
    let a0 = {| y =
        2
    |}
    let b0 = x {| y =
        2
    |}
    let c0 =
        if true then x {| y =
            2
        |}
    let d0 =
        if true then x {| y = 1 |} else x {| y =
            2
        |}
    let e0 =
        if true then x {| y =
            1
        |} else x {| y =
            2
        |}
    let f0 : {| y :
        int
     |} = x' {| y =
        2
    |}
    let g0 : {| x : int;
        y : int; z : int
    |} = {| f0 with x = 1
                    z = 3
    |}
    let a1 = {| y = 2;
        z = 3
    |}
    let b1 = x {| y = 2;
        z = 3
    |}
    let c1 =
        if true then x {| y = 2;
            z = 3
        |}
    let d1 =
        if true then x {| y = 1 |} else x {| y = 2;
            z = 3
        |}
    let e1 =
        if true then x {| y = 1;
            z = 3
        |} else x {| y = 2;
            z = 3
        |}
    let f1 : {| y : int;
        z : int
     |} = x' {| y = 2;
        z = 3
    |}
    let g1 : {| x : int;
        y : int; z : int
    |} = {| f1 with x = 1;
                z = 3
    |}
type StructAnonymousRecord = (struct {| // Parentheses required to disambiguate from struct ... end
    y : int
|})
module StructAnonymousRecords =
    let a = struct {|
        y = 2
    |}
    let b = id struct {|
        y = 2
    |}
    let c =
        if true then ignore struct {|
            y = 2
        |}
    let d =
        if true then struct {| y = 1 |} else struct {|
            y = 2
        |}
    let e =
        if true then struct {|
            y = 1
        |} else struct {|
            y = 2
        |}
    let f : struct {|
        y : int
     |} = struct {|
        y = 2
    |}
    let g : struct {|
        y : int
    |} = struct {|
        f with y = 3
    |}
    let a0 = struct {| y =
        2
    |}
    let b0 = id struct {| y =
        2
    |}
    let c0 =
        if true then ignore struct {| y =
            2
        |}
    let d0 =
        if true then struct {| y = 1 |} else struct {| y =
            2
        |}
    let e0 =
        if true then struct {| y =
            1
        |} else struct {| y =
            2
        |}
    let f0 : struct {| y :
        int
     |} = x' struct {| y =
        2
    |}
    let g0 : struct {| x : int;
        y : int; z : int
    |} = struct {| f with x = 1
                          z = 3
    |}
    let a1 = struct {| y = 2;
        z = 3
    |}
    let b1 = x struct {| y = 2;
        z = 3
    |}
    let c1 =
        if true then x struct {| y = 2;
            z = 3
        |}
    let d1 =
        if true then x struct {| y = 1 |} else x struct {| y = 2;
            z = 3
        |}
    let e1 =
        if true then x struct {| y = 1;
            z = 3
        |} else x struct {| y = 2;
            z = 3
        |}
    let f1 : struct {| y : int;
        z : int
     |} = x' struct {| y = 2;
        z = 3
    |}
    let g1 : struct {| x : int;
        y : int; z : int
    |} = struct {| f1 with x = 1;
                       z = 3
    |}
module TypedQuotations =
    let a = <@
        2
    @>
    let b = id <@
        2
    @>
    let c =
        if true then ignore <@
            2
        @>
    let d =
        if true then <@ 1 @> else <@
            2
        @>
    let e =
        if true then <@
            1
        @> else <@
            2
        @>
    let (ActivePatterns.B <@ 2 =
        2
     @> f) = <@
        2
    @>
    let (ActivePatterns.B <@ 2 =
        2
    @> _) = <@
        2
    @>
    let a0 = <@ 2 =
        2
    @>
    let b0 = x <@ 2 =
        2
    @>
    let c0 =
        if true then x <@ 2 =
            2
        @>
    let d0 =
        if true then x <@ 2 = 1 @> else x <@ 2 =
            2
        @>
    let e0 =
        if true then x <@ 2 =
            1
        @> else x <@ 2 =
            2
        @>
    let (ActivePatterns.B <@ 2 =
        2
     @> f0) = x' <@ 2 =
        2
    @>
    let (ActivePatterns.B <@ 2 =
        2
    @> _) = <@ 2 =
        2
    @>
    let a1 = <@ 2 = 2,
        3 = 3
    @>
    let b1 = x <@ 2 = 2,
        3 = 3
    @>
    let c1 =
        if true then x <@ 2 = 2,
            3 = 3
        @>
    let d1 =
        if true then x <@ 2 = 1 @> else x <@ 2 = 2,
            3 = 3
        @>
    let e1 =
        if true then x <@ 2 = 1,
            3 = 3
        @> else x <@ 2 = 2,
            3 = 3
        @>
    let (ActivePatterns.B <@ 2 = 2,
        3 = 3
     @> f1) = x' <@ 2 = 2,
        3 = 3
    @>
    let (ActivePatterns.B <@ 2 = 2,
        3 = 3
    @> _) = <@ 2 = 2,
        3 = 3
    @>
module UntypedQuotations =
    let a = <@@
        2
    @@>
    let b = id <@@
        2
    @@>
    let c =
        if true then ignore <@@
            2
        @@>
    let d =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    let e =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    let (ActivePatterns.B <@@ 2 =
        2
     @@> f) = <@@
        2
    @@>
    let (ActivePatterns.B <@@ 2 =
        2
    @@> _) = <@@
        2
    @@>
    let a0 = <@@ 2 =
        2
    @@>
    let b0 = x <@@ 2 =
        2
    @@>
    let c0 =
        if true then x <@@ 2 =
            2
        @@>
    let d0 =
        if true then x <@@ 2 = 1 @@> else x <@@ 2 =
            2
        @@>
    let e0 =
        if true then x <@@ 2 =
            1
        @@> else x <@@ 2 =
            2
        @@>
    let (ActivePatterns.B <@@ 2 =
        2
     @@> f0) = x' <@@ 2 =
        2
    @@>
    let (ActivePatterns.B <@@ 2 =
        2
    @@> _) = <@@ 2 =
        2
    @@>
    let a1 = <@@ 2 = 2,
        3 = 3
    @@>
    let b1 = x <@@ 2 = 2,
        3 = 3
    @@>
    let c1 =
        if true then x <@@ 2 = 2,
            3 = 3
        @@>
    let d1 =
        if true then x <@@ 2 = 1 @@> else x <@@ 2 = 2,
            3 = 3
        @@>
    let e1 =
        if true then x <@@ 2 = 1,
            3 = 3
        @@> else x <@@ 2 = 2,
            3 = 3
        @@>
    let (ActivePatterns.B <@@ 2 = 2,
        3 = 3
     @@> f1) = x' <@@ 2 = 2,
        3 = 3
    @@>
    let (ActivePatterns.B <@@ 2 = 2,
        3 = 3
    @@> _) = <@@ 2 = 2,
        3 = 3
    @@>
type Id<'T> = 'T -> 'T
type Id2<'T, 'U> = 'T * 'U -> 'T * 'U
let [<GeneralizableValue>] id2<'T, 'U> = id<'T * 'U>
module Generics =
    // Unlike 'end' / ')' / '|)' / ']' / '|]' / '}' / '|}',
    // '>' terminates the declaration automatically,
    // so you must indent non-terminating '>'s by at least one space
    let a : Id<
        'T
    > = id<
        'T
    >
    let b = id<
        int
    >
    let c =
        if true then ignore id<
            int
        >
    let d =
        if true then id<int> else id<
            int
        >
    let e =
        if true then id<
            int
        > else id<
            int
        >
    let f<
        'T
    > = id<
        'T
    >
    let a0 : Id<'T * (
        'T
    )> = id<'T *
        'T
    >
    let b0 = x id<int *
        int
    >
    let c0 =
        if true then x id<int *
            int
        >
    let d0 =
        if true then x id<int * int> else x id<int *
            int
        >
    let e0 =
        if true then x id<int *
            int
        > else x id<int *
            int
        >
    let f0 : Id<
        int
    > = id |> x'<Id<
        int
    >>
    let a1 : Id2<'T,
        'T
    > = id2<'T,
        'T
    >
    let b1 = x id2<int,
        int
    >
    let c1 =
        if true then x id2<int,
            int
        >
    let d1 =
        if true then x id2<int, int> else x id2<int,
            int
        >
    let e1 =
        if true then x id2<int,
            int
        > else x id2<int,
            int
        >
    let f1 : Id2<int,
        int
    > = id2 |> x'<Id2<int,
        int
    >>
type BeginEnd(
    __
) =
    let a = begin
        2
    end
    let b = id begin
        2
    end
    let c =
        if true then ignore begin
            2
        end
    let d =
        if true then begin 1 end else begin
            2
        end
    let e =
        if true then begin
            1
        end else begin
            2
        end
    let (
        _
     ) as f = begin
        2
    end
    let (
        _
    ) = begin
        3
    end
    static let a' = begin
        2
    end
    static let b' = id begin
        2
    end
    static let c' =
        if true then ignore begin
            2
        end
    static let d' =
        if true then begin 1 end else begin
            2
        end
    static let e' =
        if true then begin
            1
        end else begin
            2
        end
    static let (
        _
     ) as f' = begin
        2
    end
    static let (
        _
    ) = begin
        3
    end
    static member A = begin
        2
    end
    static member B = id begin
        2
    end
    static member C =
        if true then ignore begin
            2
        end
    static member D =
        if true then begin 1 end else begin
            2
        end
    static member E =
        if true then begin
            1
        end else begin
            2
        end
    static member F (
        _
     ) = begin
        2
    end
    static member G (
        _
    ) = begin
        3
    end
    member _.A' = begin
        2
    end
    member _.B' = id begin
        2
    end
    member _.C' =
        if true then ignore begin
            2
        end
    member _.D' =
        if true then begin 1 end else begin
            2
        end
    member _.E' =
        if true then begin
            1
        end else begin
            2
        end
    member _.F' (
        _
     ) = begin
        2
    end
    member _.G' (
        _
    ) = begin
        3
    end
type Parens(__:(
    obj
)) =
    let a = (
        2
    )
    let b = id (
        2
    )
    let c =
        if true then ignore (
            2
        )
    let d =
        if true then ( 1 ) else (
            2
        )
    let e =
        if true then (
            1
        ) else (
            2
        )
    let (
        _
     ) as f = (
        2
    )
    let (
        _
    ) = (
        3
    )
    static let a' = (
        2
    )
    static let b' = id (
        2
    )
    static let c' =
        if true then ignore (
            2
        )
    static let d' =
        if true then ( 1 ) else (
            2
        )
    static let e' =
        if true then (
            1
        ) else (
            2
        )
    static let (
        _
     ) as f' = (
        2
    )
    static let (
        _
    ) = (
        3
    )
    static member A = (
        2
    )
    static member B = id (
        2
    )
    static member C =
        if true then ignore (
            2
        )
    static member D =
        if true then ( 1 ) else (
            2
        )
    static member E =
        if true then (
            1
        ) else (
            2
        )
    static member F(
        _
     ) = (
        2
    )
    static member G(
        _
    ) = (
        3
    )
    member _.A' = (
        2
    )
    member _.B' = id (
        2
    )
    member _.C' =
        if true then ignore (
            2
        )
    member _.D' =
        if true then ( 1 ) else (
            2
        )
    member _.E' =
        if true then (
            1
        ) else (
            2
        )
    member _.F'(
        _
     ) = (
        2
    )
    member _.G'(
        _
    ) = (
        3
    )
// These are lexed differently but it's visually appealing to still include
type ActivePatterns() =
    let (|
        A
    |) = (|
        Lazy
    |)
    let (|
        B
    |) = (|
        A
    |)
    let (|C|) =
        if true then ignore (|
            A
        |)
    let d =
        if true then (| A |) else (|
            B
        |)
    let e =
        if true then (|
            A
        |) else (|
            B
        |)
    let (|
        F
        |
        _
    |) as f = Some (|
        C
    |)
    let (|
        G
        |
        _
    |) = (|
        F
        |
        _
    |)
    static let (|
        A_
    |) = (|
        Lazy
    |)
    static let (|
        B_
    |) = (|
        A_
    |)
    static let (|C_|) =
        if true then ignore (|
            A_
        |)
    static let d_ =
        if true then (| A_ |) else (|
            B_
        |)
    static let e_ =
        if true then (|
            A_
        |) else (|
            B_
        |)
    static let (|
        F_
        |
        _
    |) as f_ = Some (|
        C_
    |)
    static let (|
        G_
        |
        _
    |) = (|
        F_
        |
        _
    |)
    static member (|
        AA
    |) = (|
        Lazy
    |)
    static member (|
        BB
    |) = ActivePatterns.(|
        AA
    |)
    static member (|CC|) =
        if true then ignore ActivePatterns.(|
            AA
        |)
    static member D =
        if true then ActivePatterns.(| AA |) else ActivePatterns.(|
            BB
        |)
    static member E =
        if true then ActivePatterns.(|
            AA
        |) else ActivePatterns.(|
            BB
        |)
    static member (|
        FF
        |
        _
    |) = Some ActivePatterns.(|
        CC
    |)
    static member (|
        GG
        |
        _
    |) = ActivePatterns.(|
        FF
        |
        _
    |)
    member _.(|
        A'
    |) = (|
        Lazy
    |)
    member this.(|
        B'
    |) = this.(|
        A'
    |)
    member this.(|C'|) =
        if true then ignore this.(|
            A'
        |)
    member this.D' =
        if true then this.(| A' |) else this.(|
            B'
        |)
    member this.E' =
        if true then this.(|
            A'
        |) else this.(|
            B'
        |)
    member this.(|
        F'
        |
        _
    |) = Some this.(|
        C'
    |)
    member this.(|
        G'
        |
        _
    |) = this.(|
        F'
        |
        _
    |)
type Lists() =
    let a = [
        2
    ]
    let b = id [
        2
    ]
    let c =
        if true then ignore [
            2
        ]
    let d =
        if true then [ 1 ] else [
            2
        ]
    let e =
        if true then [
            1
        ] else [
            2
        ]
    let [
        _
     ] as f = [
        2
    ]
    let [
        _
    ] = [
        3
    ]
    static let a' = [
        2
    ]
    static let b' = id [
        2
    ]
    static let c' =
        if true then ignore [
            2
        ]
    static let d' =
        if true then [ 1 ] else [
            2
        ]
    static let e' =
        if true then [
            1
        ] else [
            2
        ]
    static let [
        _
     ] as f' = [
        2
    ]
    static let [
        _
    ] = [
        3
    ]
    static member A = [
        2
    ]
    static member B = id [
        2
    ]
    static member C =
        if true then ignore [
            2
        ]
    static member D =
        if true then [ 1 ] else [
            2
        ]
    static member E =
        if true then [
            1
        ] else [
            2
        ]
    static member F[
        _
     ] = [
        2
    ]
    static member G[
        _
    ] = [
        3
    ]
    member _.A' = [
        2
    ]
    member _.B' = id [
        2
    ]
    member _.C' =
        if true then ignore [
            2
        ]
    member _.D' =
        if true then [ 1 ] else [
            2
        ]
    member _.E' =
        if true then [
            1
        ] else [
            2
        ]
    member _.F'[
        _
     ] = [
        2
    ]
    member _.G'[
        _
    ] = [
        3
    ]
type Arrays() =
    let a = [|
        2
    |]
    let b = id [|
        2
    |]
    let c =
        if true then ignore [|
            2
        |]
    let d =
        if true then [| 1 |] else [|
            2
        |]
    let e =
        if true then [|
            1
        |] else [|
            2
        |]
    let [|
        _
     |] as f = [|
        2
    |]
    let [|
        _
    |] = [|
        3
    |]
    static let a' = [|
        2
    |]
    static let b' = id [|
        2
    |]
    static let c' =
        if true then ignore [|
            2
        |]
    static let d' =
        if true then [| 1 |] else [|
            2
        |]
    static let e' =
        if true then [|
            1
        |] else [|
            2
        |]
    static let [|
        _
     |] as f' = [|
        2
    |]
    static let [|
        _
    |] = [|
        3
    |]
    static member A = [|
        2
    |]
    static member B = id [|
        2
    |]
    static member C =
        if true then ignore [|
            2
        |]
    static member D =
        if true then [| 1 |] else [|
            2
        |]
    static member E =
        if true then [|
            1
        |] else [|
            2
        |]
    static member F[|
        _
     |] = [|
        2
    |]
    static member G[|
        _
    |] = [|
        3
    |]
    member _.A' = [|
        2
    |]
    member _.B' = id [|
        2
    |]
    member _.C' =
        if true then ignore [|
            2
        |]
    member _.D' =
        if true then [| 1 |] else [|
            2
        |]
    member _.E' =
        if true then [|
            1
        |] else [|
            2
        |]
    member _.F'[|
        _
     |] = [|
        2
    |]
    member _.G'[|
        _
    |] = [|
        3
    |]
type Records(__:struct {|
    x : unit
|}) =
    let a = {
        y = 2
    }
    let b = id {
        y = 2
    }
    let c =
        if true then ignore {
            y = 2
        }
    let d =
        if true then { y = 1 } else {
            y = 2
        }
    let e =
        if true then {
            y = 1
        } else {
            y = 2
        }
    let {
        y = _
     } as f = {
        y = 2
    }
    let {
        y = _
    } = {
        f with y = 3
    }
    static let a' = {
        y = 2
    }
    static let b' = id {
        y = 2
    }
    static let c' =
        if true then ignore {
            y = 2
        }
    static let d' =
        if true then { y = 1 } else {
            y = 2
        }
    static let e' =
        if true then {
            y = 1
        } else {
            y = 2
        }
    static let {
        y = _
     } as f' = {
        y = 2
    }
    static let {
        y = _
    } = {
        f' with y = 3
    }
    static member A = {
        y = 2
    }
    static member B = id {
        y = 2
    }
    static member C =
        if true then ignore {
            y = 2
        }
    static member D =
        if true then { y = 1 } else {
            y = 2
        }
    static member E =
        if true then {
            y = 1
        } else {
            y = 2
        }
    static member F{
        y = _
     } = {
        y = 2
    }
    static member G{
        y = _
    } = {
        Records.F { y = 1 } with y = 3
    }
    member _.A' = {
        y = 2
    }
    member _.B' = id {
        y = 2
    }
    member _.C' =
        if true then ignore {
            y = 2
        }
    member _.D' =
        if true then { y = 1 } else {
            y = 2
        }
    member _.E' =
        if true then {
            y = 1
        } else {
            y = 2
        }
    member _.F'{
        y = _
     } = {
        y = 2
    }
    member this.G'{
        y = _
    } = {
        this.F' { y = 1 } with y = 3
    }
type AnonymousRecords(x:{|
    x : bool
|}) =
    let a = {|
        y = 2
    |}
    let b = id {|
        y = 2
    |}
    let c =
        if true then ignore {|
            y = 2
        |}
    let d =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    let e =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    let f : {|
        y : int
     |} = {|
        y = 2
    |}
    let g : {|
        y : int
    |} = {|
        f with y = 3
    |}
    static let a' = {|
        y = 2
    |}
    static let b' = id {|
        y = 2
    |}
    static let c' =
        if true then ignore {|
            y = 2
        |}
    static let d' =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    static let e' =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    static let f' : {|
        y : int
     |} = {|
        y = 2
    |}
    static let g' : {|
        y : int
    |} = {|
        f' with y = 3
    |}
    static member A = {|
        y = 2
    |}
    static member B = id {|
        y = 2
    |}
    static member C =
        if true then ignore {|
            y = 2
        |}
    static member D =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    static member E =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    static member F : {|
        y : int
     |} = {|
        y = 2
    |}
    static member G : {|
        y : int
    |} = {|
        AnonymousRecords.F with y = 3
    |}
    member _.A' = {|
        y = 2
    |}
    member _.B' = id {|
        y = 2
    |}
    member _.C' =
        if true then ignore {|
            y = 2
        |}
    member _.D' =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    member _.E' =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    member _.F' : {|
        y : int
     |} = {|
        y = 2
    |}
    member this.G' : {|
        y : int
    |} = {|
        this.F' with y = 3
    |}
type StructAnonymousRecords(x:struct{|
    x : bool
|}) =
    let a = struct{|
        y = 2
    |}
    let b = id struct{|
        y = 2
    |}
    let c =
        if true then ignore struct{|
            y = 2
        |}
    let d =
        if true then struct{| y = 1 |} else struct{|
            y = 2
        |}
    let e =
        if true then struct{|
            y = 1
        |} else struct{|
            y = 2
        |}
    let f : struct{|
        y : int
     |} = struct{|
        y = 2
    |}
    let g : struct{|
        y : int
    |} = struct{|
        f with y = 3
    |}
    static let a' = struct{|
        y = 2
    |}
    static let b' = id struct{|
        y = 2
    |}
    static let c' =
        if true then ignore struct{|
            y = 2
        |}
    static let d' =
        if true then struct{| y = 1 |} else struct{|
            y = 2
        |}
    static let e' =
        if true then struct{|
            y = 1
        |} else struct{|
            y = 2
        |}
    static let f' : struct{|
        y : int
     |} = struct{|
        y = 2
    |}
    static let g' : struct{|
        y : int
    |} = struct{|
        f' with y = 3
    |}
    static member A = struct{|
        y = 2
    |}
    static member B = id struct{|
        y = 2
    |}
    static member C =
        if true then ignore struct{|
            y = 2
        |}
    static member D =
        if true then struct{| y = 1 |} else struct{|
            y = 2
        |}
    static member E =
        if true then struct{|
            y = 1
        |} else struct{|
            y = 2
        |}
    static member F : struct{|
        y : int
     |} = struct{|
        y = 2
    |}
    static member G : struct{|
        y : int
    |} = struct{|
        AnonymousRecords.F with y = 3
    |}
    member _.A' = struct{|
        y = 2
    |}
    member _.B' = id struct{|
        y = 2
    |}
    member _.C' =
        if true then ignore struct{|
            y = 2
        |}
    member _.D' =
        if true then struct{| y = 1 |} else struct{|
            y = 2
        |}
    member _.E' =
        if true then struct{|
            y = 1
        |} else struct{|
            y = 2
        |}
    member _.F' : struct{|
        y : int
     |} = struct{|
        y = 2
    |}
    member this.G' : struct{|
        y : int
    |} = struct{|
        this.F' with y = 3
    |}
type TypedQuotations(x:
int Quotations.Expr) =
    let a = <@
        2
    @>
    let b = id <@
        2
    @>
    let c =
        if true then ignore <@
            2
        @>
    let d =
        if true then <@ 1 @> else <@
            2
        @>
    let e =
        if true then <@
            1
        @> else <@
            2
        @>
    let (ActivePatterns.B <@
        2
     @> _) as f = <@
        2
    @>
    let (ActivePatterns.B <@
        2
    @> _) = <@
        3
    @>
    static let a' = <@
        2
    @>
    static let b' = id <@
        2
    @>
    static let c' =
        if true then ignore <@
            2
        @>
    static let d' =
        if true then <@ 1 @> else <@
            2
        @>
    static let e' =
        if true then <@
            1
        @> else <@
            2
        @>
    static let (ActivePatterns.B <@
        2
     @> _) as f' = <@
        2
    @>
    static let (ActivePatterns.B <@
        2
    @> _) = <@
        3
    @>
    static member A = <@
        2
    @>
    static member B = id <@
        2
    @>
    static member C =
        if true then ignore <@
            2
        @>
    static member D =
        if true then <@ 1 @> else <@
            2
        @>
    static member E =
        if true then <@
            1
        @> else <@
            2
        @>
    static member F(ActivePatterns.B <@
        2
     @> _) = <@
        2
    @>
    static member G(ActivePatterns.B <@
        2
    @> _) = <@
        3
    @>
    member _.A' = <@
        2
    @>
    member _.B' = id <@
        2
    @>
    member _.C' =
        if true then ignore <@
            2
        @>
    member _.D' =
        if true then <@ 1 @> else <@
            2
        @>
    member _.E' =
        if true then <@
            1
        @> else <@
            2
        @>
    member _.F'(ActivePatterns.B <@
        2
     @> _) = <@
        2
    @>
    member _.G'(ActivePatterns.B <@
        2
    @> _) = <@
        3
    @>
type UntypedQuotations(x:
Quotations.Expr) =
    let a = <@@
        2
    @@>
    let b = id <@@
        2
    @@>
    let c =
        if true then ignore <@@
            2
        @@>
    let d =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    let e =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    let (ActivePatterns.B <@@
        2
     @@> _) as f = <@@
        2
    @@>
    let (ActivePatterns.B <@@
        2
    @@> _) = <@@
        3
    @@>
    static let a' = <@@
        2
    @@>
    static let b' = id <@@
        2
    @@>
    static let c' =
        if true then ignore <@@
            2
        @@>
    static let d' =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    static let e' =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    static let (ActivePatterns.B <@@
        2
     @@> _) as f' = <@@
        2
    @@>
    static let (ActivePatterns.B <@@
        2
    @@> _) = <@@
        3
    @@>
    static member A = <@@
        2
    @@>
    static member B = id <@@
        2
    @@>
    static member C =
        if true then ignore <@@
            2
        @@>
    static member D =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    static member E =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    static member F(ActivePatterns.B <@@
        2
     @@> _) = <@@
        2
    @@>
    static member G(ActivePatterns.B <@@
        2
    @@> _) = <@@
        3
    @@>
    member _.A' = <@@
        2
    @@>
    member _.B' = id <@@
        2
    @@>
    member _.C' =
        if true then ignore <@@
            2
        @@>
    member _.D' =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    member _.E' =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    member _.F'(ActivePatterns.B <@@
        2
     @@> _) = <@@
        2
    @@>
    member _.G'(ActivePatterns.B <@@
        2
    @@> _) = <@@
        3
    @@>
type Generics(__:Id<
    obj
>) =
    let a : Id<
        'T
    > = id<
        'T
    >
    let b = id<
        int
    >
    let c =
        if true then ignore id<
            int
        >
    let d =
        if true then id<int> else id<
            int
        >
    let e =
        if true then id<
            int
        > else id<
            int
        >
    static let a' : Id<
        'T
    > = id<
        'T
    >
    static let b' = id<
        int
    >
    static let c' =
        if true then ignore id<
            int
        >
    static let d' =
        if true then id<int> else id<
            int
        >
    static let e' =
        if true then id<
            int
        > else id<
            int
        >
    static member A<
        'T
    >() = id<
        'T
    >
    static member B = id<
        int
    >
    static member C =
        if true then ignore id<
            int
        >
    static member D =
        if true then id<int> else id<
            int
        >
    static member E =
        if true then id<
            int
        > else id<
            int
        >
    member _.A'<
        'T
    >() = id<
        'T
    >
    member _.B' = id<
        int
    >
    member _.C' =
        if true then ignore id<
            int
        >
    member _.D' =
        if true then id<int> else id<
            int
        >
    member _.E' =
        if true then id<
            int
        > else id<
            int
        >
let BeginEnd =
    let a = begin
        2
    end
    let b = id begin
        2
    end
    let c =
        if true then ignore begin
            2
        end
    let d =
        if true then begin 1 end else begin
            2
        end
    let e =
        if true then begin
            1
        end else begin
            2
        end
    let (
        _
     ) as f = begin
        2
    end
    let (
        _
    ) = begin
        3
    end
    let h =
        if begin
            true
        end
        then begin
         end
    let i =
        if x' begin
            false
        end
        then begin
         end
    let a0 = begin 2 =
        2
    end
    let b0 = x begin y =
        2
    end
    let c0 =
        if true then x begin y =
            2
        end
    let d0 =
        if true then x begin y = 1 end else x begin y =
            2
        end
    let e0 =
        if true then x begin y =
            1
        end else x begin y =
            2
        end
    let (
        _
     ) as f0 = x begin y =
        2
    end
    let (
        _
    ) = begin 2 =
        3
    end
    let h0 =
        if begin 2 =
            2
        end
        then begin
         end
    let i0 =
        if x' begin y =
            true
        end
        then begin
         end
    let a1 = begin 2 = 2,
        3 = 2
    end
    let b1 = x begin y = 2,
        z = 2
    end
    let c1 =
        if true then x begin y = 2,
            z = 3
        end
    let d1 =
        if true then x begin y = 1 end else x begin y = 2,
            z = 3
        end
    let e1 =    
        if true then x begin y = 1,
            z = 3
        end else x begin y = 2,
            z = 3
        end
    let (
        _
     ) as f1 = x begin y = 2,
        z = 3
    end
    let (
        _
    ) = begin 2 = 3,
        3 = 3
    end
    let h1 =
        if begin 2 = 2 |> ignore;
            3 = 3
        end
        then begin
         end
    let i1 =
        if x' begin y = true,
            z = false
        end
        then begin
         end
    begin 1
    end
let Parens =
    let a = (
        2
    )
    let b = id (
        2
    )
    let c =
        if true then ignore (
            2
        )
    let d =
        if true then ( 1 ) else (
            2
        )
    let e =
        if true then (
            1
        ) else (
            2
        )
    let (
        _
     ) as f = (
        2
    )
    let (
        _
    ) = (
        3
    )
    let a0 = (2 =
        2
    )
    let b0 = x (y =
        2
    )
    let c0 =
        if true then x (y =
            2
        )
    let d0 =
        if true then x ( y = 1 ) else x (y =
            2
        )
    let e0 =
        if true then x (y =
            1
        ) else x (y =
            2
        )
    let (
        _
     ) as f0 = x (y =
        2
    )
    let (
        _
    ) = (2 =
        3
    )
    let a1 = (2 = 2,
        3 = 3
    )
    let b1 = x (y = 2,
        z = 3
    )
    let c1 =
        if true then x (y = 2,
            z = 3
        )
    let d1 =
        if true then x ( y = 1 ) else x (y = 2,
            z = 3
        )
    let e1 =
        if true then x (y = 1,
            z = 3
        ) else x (y = 2,
            z = 3
        )
    let (
        _
     ) as f1 = x (y = 2,
        z = 3
    )
    let (
        _
    ) = (2 = 3,
        3 = 3
    )
    ( 1
    )
// These are lexed differently but it's visually appealing to still include
let ActivePatterns<'a> =
    let (|
        A
    |) = id
    let (|
        B
    |) _ = (|
        A
    |)
    let (|C|) =
        if true then ignore (|
            A
        |)
    let d =
        if true then (| A |) else (|
            A
        |)
    let e =
        if true then (|
            A
        |) else (|
            A
        |)
    let (|
        F
        |
        _
    |) as f = Some (|
        C
    |)
    let (|
        G
        |
        _
    |) = (|
        F
        |
        _
    |)
    (| A
    |)
let Lists =
    let a = [
        2
    ]
    let b = id [
        2
    ]
    let c =
        if true then ignore [
            2
        ]
    let d =
        if true then [ 1 ] else [
            2
        ]
    let e =
        if true then [
            1
        ] else [
            2
        ]
    let [
        _
     ] as f = [
        2
    ]
    let [
        _
    ] = [
        3
    ]
    let a0 = [ 2 =
        2
    ]
    let b0 = x [ 2 =
        2
    ]
    let c0 =
        if true then x [ 2 =
            2
        ]
    let d0 =
        if true then x [ 2 = 1 ] else x [ 2 =
            2
        ]
    let e0 =
        if true then x [ 2 =
            1
        ] else x [ 2 =
            2
        ]
    let [
        _
     ] as f0 = x' [ 2 =
        2
    ]
    let [
        _
    ] = [ 2 =
        3
    ]
    let a1 = [ 2 = 2,
        3 = 3
    ]
    let b1 = x [ 2 = 2,
        3 = 3
    ]
    let c1 =
        if true then x [ 2 = 2,
            3 = 3
        ]
    let d1 =
        if true then x [ 2 = 1 ] else x [ 2 = 2,
            3 = 3
        ]
    let e1 =
        if true then x [ 2 = 1,
            3 = 3
        ] else x [ 2 = 2,
            3 = 3
        ]
    let [
        _
     ] as f1 = x' [ 2 = 2,
        3 = 3
    ]
    let [
        _
    ] = [ 2 = 3,
        3 = 3
    ]
    [ 1
    ]
let Arrays =
    let a = [|
        2
    |]
    let b = id [|
        2
    |]
    let c =
        if true then ignore [|
            2
        |]
    let d =
        if true then [| 1 |] else [|
            2
        |]
    let e =
        if true then [|
            1
        |] else [|
            2
        |]
    let [|
        _
     |] as f = [|
        2
    |]
    let [|
        _
    |] = [|
        3
    |]
    let a0 = [| 2 =
        2
    |]
    let b0 = x [| 2 =
        2
    |]
    let c0 =
        if true then x [| 2 =
            2
        |]
    let d0 =
        if true then x [| 2 = 1 |] else x [| 2 =
            2
        |]
    let e0 =
        if true then x [| 2 =
            1
        |] else x [| 2 =
            2
        |]
    let [|
        _
     |] as f0 = x' [| 2 =
        2
    |]
    let [|
        _
    |] = [| 2 =
        3
    |]
    let a1 = [| 2 = 2,
        3 = 3
    |]
    let b1 = x [| 2 = 2,
        3 = 3
    |]
    let c1 =
        if true then x [| 2 = 2,
            3 = 3
        |]
    let d1 =
        if true then x [| 2 = 1 |] else x [| 2 = 2,
            3 = 3
        |]
    let e1 =
        if true then x [| 2 = 1,
            3 = 3
        |] else x [| 2 = 2,
            3 = 3
        |]
    let [|
        _
     |] as f1 = x' [| 2 = 2,
        3 = 3
    |]
    let [|
        _
    |] = [| 2 = 3,
        3 = 3
    |]
    [| 1
    |]
let Records =
    let a = {
        y = 2
    }
    let b = id {
        y = 2
    }
    let c =
        if true then ignore {
            y = 2
        }
    let d =
        if true then { y = 1 } else {
            y = 2
        }
    let e =
        if true then {
            y = 1
        } else {
            y = 2
        }
    let {
        y = _
     } as f = {
        y = 2
    }
    let {
        y = _
    } = {
        f with y = 3
    }
    let a0 = { y =
        2
    }
    let b0 = x { y =
        2
    }
    let c0 =
        if true then x { y =
            2
        }
    let d0 =
        if true then x { y = 1 } else x { y =
            2
        }
    let e0 =
        if true then { y =
            1
        } else { y =
            2
        }
    let { z = {
        y = _
     }} as f0 = { z = {
        y = 2
    }; x = 1}
    let { z = {
        y = _
    }} = { f0 with z = {
               y = 3
           }
    }
    let a1 = { x = 2;
        z = { y = 3 }
    }
    let b1 = x { x = 2;
        z = { y = 3 }
    }
    let c1 =
        if true then x { x = 2;
            z = { y = 3 }
        }
    let d1 =
        if true then x { y = 1 } else x { x = 2;
            z = { y = 3 }
        }
    let e1 =
        if true then { x = 1;
            z = { y = 3 }
        } else { x = 2;
            z = { y = 3 }
        }
    let { z = {
        y = _
     }} as f1 = { x = 1;
     z = {
        y = 2
    } }
    let { x = _; z = {
        y = _
    }} = { f1 with x = 2; z = {
               y = 3
           }
    }
    { y = 1
    }
let AnonymousRecords =
    let a = {|
        y = 2
    |}
    let b = id {|
        y = 2
    |}
    let c =
        if true then ignore {|
            y = 2
        |}
    let d =
        if true then {| y = 1 |} else {|
            y = 2
        |}
    let e =
        if true then {|
            y = 1
        |} else {|
            y = 2
        |}
    let f : {|
        y : int
     |} = {|
        y = 2
    |}
    let g : {|
        y : int
    |} = {|
        f with y = 3
    |}
    let a0 = {| y =
        2
    |}
    let b0 = x {| y =
        2
    |}
    let c0 =
        if true then x {| y =
            2
        |}
    let d0 =
        if true then x {| y = 1 |} else x {| y =
            2
        |}
    let e0 =
        if true then x {| y =
            1
        |} else x {| y =
            2
        |}
    let f0 : {| y :
        int
     |} = x' {| y =
        2
    |}
    let g0 : {| x : int;
        y : int; z : int
    |} = {| f0 with x = 1
                    z = 3
    |}
    let a1 = {| y = 2;
        z = 3
    |}
    let b1 = x {| y = 2;
        z = 3
    |}
    let c1 =
        if true then x {| y = 2;
            z = 3
        |}
    let d1 =
        if true then x {| y = 1 |} else x {| y = 2;
            z = 3
        |}
    let e1 =
        if true then x {| y = 1;
            z = 3
        |} else x {| y = 2;
            z = 3
        |}
    let f1 : {| y : int;
        z : int
     |} = x' {| y = 2;
        z = 3
    |}
    let g1 : {| x : int;
        y : int; z : int
    |} = {| f1 with x = 1;
                z = 3
    |}
    {| y = 1
    |}
let StructAnonymousRecords =
    let a = struct {|
        y = 2
    |}
    let b = id struct {|
        y = 2
    |}
    let c =
        if true then ignore struct {|
            y = 2
        |}
    let d =
        if true then struct {| y = 1 |} else struct {|
            y = 2
        |}
    let e =
        if true then struct {|
            y = 1
        |} else struct {|
            y = 2
        |}
    let f : struct {|
        y : int
     |} = struct {|
        y = 2
    |}
    let g : struct {|
        y : int
    |} = struct {|
        f with y = 3
    |}
    let a0 = struct {| y =
        2
    |}
    let b0 = id struct {| y =
        2
    |}
    let c0 =
        if true then ignore struct {| y =
            2
        |}
    let d0 =
        if true then struct {| y = 1 |} else struct {| y =
            2
        |}
    let e0 =
        if true then struct {| y =
            1
        |} else struct {| y =
            2
        |}
    let f0 : struct {| y :
        int
     |} = x' struct {| y =
        2
    |}
    let g0 : struct {| x : int;
        y : int; z : int
    |} = struct {| f with x = 1
                          z = 3
    |}
    let a1 = struct {| y = 2;
        z = 3
    |}
    let b1 = x struct {| y = 2;
        z = 3
    |}
    let c1 =
        if true then x struct {| y = 2;
            z = 3
        |}
    let d1 =
        if true then x struct {| y = 1 |} else x struct {| y = 2;
            z = 3
        |}
    let e1 =
        if true then x struct {| y = 1;
            z = 3
        |} else x struct {| y = 2;
            z = 3
        |}
    let f1 : struct {| y : int;
        z : int
     |} = x' struct {| y = 2;
        z = 3
    |}
    let g1 : struct {| x : int;
        y : int; z : int
    |} = struct {| f1 with x = 1;
                       z = 3
    |}
    struct {| y = 1
    |}
let TypedQuotations =
    let a = <@
        2
    @>
    let b = id <@
        2
    @>
    let c =
        if true then ignore <@
            2
        @>
    let d =
        if true then <@ 1 @> else <@
            2
        @>
    let e =
        if true then <@
            1
        @> else <@
            2
        @>
    let (ActivePatterns.B <@ 2 =
        2
     @> f) = <@
        2
    @>
    let (ActivePatterns.B <@ 2 =
        2
    @> _) = <@
        2
    @>
    let a0 = <@ 2 =
        2
    @>
    let b0 = x <@ 2 =
        2
    @>
    let c0 =
        if true then x <@ 2 =
            2
        @>
    let d0 =
        if true then x <@ 2 = 1 @> else x <@ 2 =
            2
        @>
    let e0 =
        if true then x <@ 2 =
            1
        @> else x <@ 2 =
            2
        @>
    let (ActivePatterns.B <@ 2 =
        2
     @> f0) = x' <@ 2 =
        2
    @>
    let (ActivePatterns.B <@ 2 =
        2
    @> _) = <@ 2 =
        2
    @>
    let a1 = <@ 2 = 2,
        3 = 3
    @>
    let b1 = x <@ 2 = 2,
        3 = 3
    @>
    let c1 =
        if true then x <@ 2 = 2,
            3 = 3
        @>
    let d1 =
        if true then x <@ 2 = 1 @> else x <@ 2 = 2,
            3 = 3
        @>
    let e1 =
        if true then x <@ 2 = 1,
            3 = 3
        @> else x <@ 2 = 2,
            3 = 3
        @>
    let (ActivePatterns.B <@ 2 = 2,
        3 = 3
     @> f1) = x' <@ 2 = 2,
        3 = 3
    @>
    let (ActivePatterns.B <@ 2 = 2,
        3 = 3
    @> _) = <@ 2 = 2,
        3 = 3
    @>
    <@ 1
    @>
let UntypedQuotations =
    let a = <@@
        2
    @@>
    let b = id <@@
        2
    @@>
    let c =
        if true then ignore <@@
            2
        @@>
    let d =
        if true then <@@ 1 @@> else <@@
            2
        @@>
    let e =
        if true then <@@
            1
        @@> else <@@
            2
        @@>
    let (ActivePatterns.B <@@ 2 =
        2
     @@> f) = <@@
        2
    @@>
    let (ActivePatterns.B <@@ 2 =
        2
    @@> _) = <@@
        2
    @@>
    let a0 = <@@ 2 =
        2
    @@>
    let b0 = x <@@ 2 =
        2
    @@>
    let c0 =
        if true then x <@@ 2 =
            2
        @@>
    let d0 =
        if true then x <@@ 2 = 1 @@> else x <@@ 2 =
            2
        @@>
    let e0 =
        if true then x <@@ 2 =
            1
        @@> else x <@@ 2 =
            2
        @@>
    let (ActivePatterns.B <@@ 2 =
        2
     @@> f0) = x' <@@ 2 =
        2
    @@>
    let (ActivePatterns.B <@@ 2 =
        2
    @@> _) = <@@ 2 =
        2
    @@>
    let a1 = <@@ 2 = 2,
        3 = 3
    @@>
    let b1 = x <@@ 2 = 2,
        3 = 3
    @@>
    let c1 =
        if true then x <@@ 2 = 2,
            3 = 3
        @@>
    let d1 =
        if true then x <@@ 2 = 1 @@> else x <@@ 2 = 2,
            3 = 3
        @@>
    let e1 =
        if true then x <@@ 2 = 1,
            3 = 3
        @@> else x <@@ 2 = 2,
            3 = 3
        @@>
    let (ActivePatterns.B <@@ 2 = 2,
        3 = 3
     @@> f1) = x' <@@ 2 = 2,
        3 = 3
    @@>
    let (ActivePatterns.B <@@ 2 = 2,
        3 = 3
    @@> _) = <@@ 2 = 2,
        3 = 3
    @@>
    <@@ 1
    @@>
let Generics =
    // Unlike 'end' / ')' / '|)' / ']' / '|]' / '}' / '|}',
    // '>' terminates the declaration automatically,
    // so you must indent non-terminating '>'s by at least one space
    let a : Id<
        'T
    > = id<
        'T
    >
    let b = id<
        int
    >
    let c =
        if true then ignore id<
            int
        >
    let d =
        if true then id<int> else id<
            int
        >
    let e =
        if true then id<
            int
        > else id<
            int
        >
    let f : Id<'T
    > = id<
        'T
    >
    let a0 : Id<'T * (
        'T
    )> = id<'T *
        'T
    >
    let b0 = x id<int *
        int
    >
    let c0 =
        if true then x id<int *
            int
        >
    let d0 =
        if true then x id<int * int> else x id<int *
            int
        >
    let e0 =
        if true then x id<int *
            int
        > else x id<int *
            int
        >
    let f0 : Id<
        int
    > = id |> x'<Id<
        int
    >>
    let a1 : Id2<'T,
        'T
    > = id2<'T,
        'T
    >
    let b1 = x id2<int,
        int
    >
    let c1 =
        if true then x id2<int,
            int
        >
    let d1 =
        if true then x id2<int, int> else x id2<int,
            int
        >
    let e1 =
        if true then x id2<int,
            int
        > else x id2<int,
            int
        >
    let f1 : Id2<int,
        int
    > = id2 |> x'<Id2<int,
        int
    >>
    id<int
    >

type Foo (
    y: int,
    x: int
) =
    // https://github.com/fsharp/fslang-suggestions/issues/931
    let g = System.DateTime(
        year = 2020,
        month = 12,
        day = 1
    )
    member _.Bar(
        a: int,
        b: int
    ) =
        let g = System.DateTime(
            year = 2020,
            month = 12,
            day = 1
        )

        // https://github.com/fsharp/fslang-suggestions/issues/833
        match
            2
        with 2 -> ()
        match
            2
        with | 2 -> ()
        match
            2
        with
        | 2 -> ()
        match
            match
                match
                    1
                with _ -> 2
            with
            | _ -> 3
        with
        | 4 -> ()
        match
            try
                match
                    1
                with | _ -> 2
            with
             _ -> 3
        with
        4 -> g
    static member Baz(
        _
    ) = ()

// https://github.com/fsharp/fslang-suggestions/issues/786
let f' x = x
let a = f' [
    2 // List: OK
]
let b = [|
    2 // Array: OK
|]
type X = { X : int }
let c = f' {
    X = 2 // Record: OK
}
let d = f' {|
    X = 2 (* FS0058	Possible incorrect indentation:
this token is offside of context started at position (12:11).
Try indenting this token further or using standard formatting conventions. *)
|}
let e = f' {|
            X = 2 // Indenting further is needed
        |}

open System
// https://github.com/fsharp/fslang-suggestions/issues/724
type SixAccessViolations(a:AccessViolationException,
    b:AccessViolationException, c:AccessViolationException,
    d:AccessViolationException, e:AccessViolationException,
    f:AccessViolationException) =
    class end
type SixAccessViolations2(
    a:AccessViolationException,
    b:AccessViolationException, c:AccessViolationException,
    d:AccessViolationException, e:AccessViolationException,
    f:AccessViolationException) =
    class end
type SixAccessViolations3(
    a:AccessViolationException,
    b:AccessViolationException, c:AccessViolationException,
    d:AccessViolationException, e:AccessViolationException,
    f:AccessViolationException
) = class end

// https://github.com/dotnet/fsharp/issues/3326
// https://github.com/fsharp/fslang-suggestions/issues/868
open System.IO

type message =
| HeatUp
| CoolDown
 
let climateControl1 = MailboxProcessor.Start( fun inbox ->
    // NOTE compiles
    let rec heating() = async {
        printfn "Heating"
        let! msg = inbox.Receive()
        match msg with                                                                                       
        | CoolDown -> return! cooling()
        | _ -> return! heating()
    } // NOTE placement of }
    and cooling() = async {
        printfn "Cooling"
        let! msg = inbox.Receive()
        match msg with
        | HeatUp -> return! heating()
        | _ -> return! cooling()
    } // NOTE placement of }
 
    heating()
)

let climateControl2 = MailboxProcessor.Start(fun inbox ->
    // NOTE compiles
    let rec heating() = async {
        printfn "Heating"
        let! msg = inbox.Receive()
        match msg with
        | CoolDown -> return ()
        | _ -> return! heating()
    } // NOTE placement of }
    heating()
)

let climateControl3 = MailboxProcessor.Start(fun inbox ->
    // NOTE does not compile
    let rec heating() = async {
        printfn "Heating"
        let! msg = inbox.Receive()
        match msg with
        | CoolDown -> return! cooling()
        | _ -> return! heating()
    } // NOTE placement of }
    and cooling() = async {
        printfn "Cooling"
        let! msg = inbox.Receive()
        match msg with
        | HeatUp -> return! heating()
        | _ -> return! cooling()
    } // NOTE placement of }
 
    heating()
)

// https://github.com/dotnet/fsharp/issues/10852
let f _ b = b 
let g _ b = b

let aaaaa<'t> = f >> g

let bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 42
let cc = 43

aaaaa (
    bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
    cc
) ()

(f >> g) (
    bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
    cc
) begin end

// https://github.com/dotnet/fsharp/issues/10929

let a' = System.Text.RegularExpressions.Regex.IsMatch(
    "myInput",
    """^[a-zA-Z][a-zA-Z0-9']+$"""
)

if System.Text.RegularExpressions.Regex.IsMatch(
    "myInput",
    """^[a-zA-Z][a-zA-Z0-9']+$"""
   ) then
  ()
  
// https://github.com/fsharp/fslang-suggestions/issues/504
module Q =
    module Z =
        type Alpha< ^b, ^a 
    when ^     a    :  (member Name:string)
and    ^a:        (member Zip
   : ^b when
^b : struct   )
and             ^a
:                 (static member(+)
    :    'a * 'a 
-> 'a 
) 
         > () = 
            member inline __.X = ()
        with
        static member inline Y = ()
        
type TypeWithALongName< ^a
    when ^a:(static member(+):'a * 'a -> 'a )
    and  ^a:(static member(-):'a * 'a -> 'a )
    and  ^a:(static member(*):'a * 'a -> 'a )
    and  ^a:(static member(/):'a * 'a -> 'a )
> =
    static member inline X = 2
type TypeWithALongName2< ^a 
              when ^a:(static member(+):'a * 'a -> 'a )
              and  ^a:(static member(-):'a * 'a -> 'a )            
              and  ^a:(static member(*):'a * 'a -> 'a )            
              and  ^a:(static member(/):'a * 'a -> 'a )            
    > = static member inline X = ()
type TypeWithALongName3< 'a 
 when 'a: not struct
 and  ^a:(static member(-):'a * 'a -> 'a )            
 and  'a:> System.IDisposable      
 and  ^a:> System.Object         
> = static member inline X = ()
// https://github.com/fsharp/fslang-suggestions/issues/470

type ColorAxis(values : int[],
    colors: string[]
) = struct
    member _.Values = values
    member _.Colors = colors
end
type Options(colorAxis: ColorAxis) = class
    member _.ColorAxis = colorAxis
end
type Geo = Geo
module Chart =
    let Geo _ = Geo
    let WithOptions (
        _: Options
    ) Geo = ()
let (
 =>
 ) _ = id
let wb = {|
    Countries = [|
        {|
            Name = ""
            Indicators = {|
                ``CO2 emissions (kt)`` = dict [
                    2010, ()
                ]
            |}
        |}
    |]
|}
let (
    series
) = ignore
let (
    growth
) = ignore

(*
Currently, the general rules for indentation in F# is that the code on 
the next line should be indented further than the thing that determines 
its starting point on the previous line.

There are a number of cases where this quite annoyingly means that you 
have to indent things very far (or, to avoid that, add lots of 
unnecessary line breaks). One example is when you have nesting in a 
method call. For example:
*)
Chart.Geo(growth)
|> Chart.WithOptions(Options(colorAxis=ColorAxis(values=[| -100;0;100;200;1000 |], colors=[| "#77D53D";"#D1C855";"#E8A958";"#EA4C41";"#930700" |])))
(*
Now, there is almost no way to make this code snippet look decent. I would
want to write something like this:
*)
Chart.Geo(growth)
|> Chart.WithOptions(Options(colorAxis=ColorAxis(values=[| -100;0;100;200;1000 |], 
    colors=[| "#77D53D";"#D1C855";"#E8A958";"#EA4C41";"#930700" |])))
(*
But this is not allowed, because "colors" should start after the opening
parenthesis of ColorAxis, so I would need 50 spaces! To make the number of
spaces smaller, you can add additional newline (to get the "ColorAxis" more to
the left), but this looks pretty bad:
*)
Chart.Geo(growth)
|> Chart.WithOptions
    (Options
      (colorAxis =
        ColorAxis
          (values=[| -100;0;100;200;1000 |], 
           colors=[| "#77D53D";"#D1C855";"#E8A958";"#EA4C41";"#930700" |])))
(*
Another example is very similar, but with list expressions. 
I want to write:
*)
// let pop2010 = series [ for c in wb.Countries -> 
//   c.Name => c.Indicators.``CO2 emissions (kt)``.[2010]]
// NOTE: That is probably is too much of an undentation. Try this instead:
let pop2010 = series [
    for c in wb.Countries -> 
        c.Name => c.Indicators.``CO2 emissions (kt)``.[2010]]

(*
This actually works, but it gives warning. Again, it wants me to indent the
second line so that it is after "for", but then I'm not saving pretty much
anything by the newline. Or, I can introduce lots of additional newlines 
and write:
*)
let pop2011 =
  series
    [ for c in wb.Countries -> 
        c.Name => c.Indicators.``CO2 emissions (kt)``.[2010]]
(*
I think that in situations like these, the rules should be relaxed. 
In particular, we should not require new line to be intended further 
than the "starting thing" on the previous line. Just further than the 
previous line.
*)

let foo = ([|
  1
  2
|])

let bar = [|
  1
  2
|]

// Some random cases

for i in <@
    1
@> |> box |> unbox<int seq> do ()
while <@@
    1
@@> |> box |> unbox<bool> do ()
do ignore <| <@@
    1
@@>

function
| {
    y = 6
  } -> ()
<| {
    y = 7
}
function
| {
    y = 6
  } when {
    y = 2
  } = {
    y = 3
  } -> ()
<| {
    y = 7
}
function {
         y = 6
        } when {
         y = 2
        } = {
         y = 3
        } -> ()
<| {
    y = 7
}

exception J of {|
    r : int
|}

// Individual testing of special-cased contexts

do (
    ()
)
do ignore (
    1
)
exception J'' of {|
    r : int
|}
exception J' of r : (
    int
) with
    member _.A(
        _
    ) = ()
    member _.A'(_:
        _
    ) = ()
type System.Int32 with
    member _.A(
        _
    ) = ()
    member _.A'(_:
        _
    ) = ()
for i in <@
    1
 @> |> box |> unbox<int seq> do ()
for i in seq {
    1
} |> box |> unbox<int seq> do ()
while <@
    1
@> |> box |> unbox<bool> do ()
while seq {
    1
 } |> box |> unbox<bool> do ()
try seq {
    1
} with _ -> seq { 2 }
|> ignore<int seq>
try <@@
    1
@@> with _ -> <@@ 2 @@>
|> ignore<Quotations.Expr>
if <@
    1
@> |> box<int Quotations.Expr> |> unbox<bool> then ()
if seq {
    1
 } |> box<int seq> |> unbox<bool> then ()
if true then Seq.head <| seq {
    ()
} |> id<unit>
if true then (box<unit Quotations.Expr> >> unbox<unit>) <@
    ()
@> |> id<unit>
if true then () else (box<int seq> >> unbox<unit>) <| seq {
    1
} |> id<unit>
if true then () else (box<int Quotations.Expr> >> unbox<unit>) <@
    1
@> |> id<unit>
fun () -> Seq.head <| seq {
    ()
} |> ignore<unit -> unit>
function () -> Seq.head <| seq {
              ()
} |> ignore<unit -> unit>

// Examples in the RFC

type R = { a : int }
type [<AutoOpen>] H = static member h a = ()
module rec M1 =
    let messageLoop (state:int) = async {
        return! someOther(state)
    } // Sure
    let someOther(state:int) = async {
        return! messageLoop(state)
    }
module M2 =
    let rec messageLoop (state:int) = async {
        return! someOther(state)
    }
    // error FS0010: Unexpected keyword 'and' in definition. Expected incomplete structured construct at or before this point or other token.
    and someOther(state:int) = async {
        return! messageLoop(state)
    }
let RFC =
    
    let a = id [
        1 // No longer produces warning FS0058 after [RFC FS-1054] less strict indentation on common DSL pattern
    ]
    let b = id (
        1 // warning FS0058: Possible incorrect indentation
    )
    let c = (
        1 // But this is ok
    )
    try
        1
    with _ -> 2 // Totally fine
    |> ignore
    
    match
        1
    with _ -> 2 // error FS0010: Unexpected start of structured construct in expression
    |> ignore
    if true then [ 0 ] else [
        1 // This is fine
    ]
    |> ignore
    if true then <@ 0 @> else <@
        1 // warning FS0058: Possible incorrect indentation
    @>
    |> ignore
    let d =
        if [
            2 // Fine
           ] = [3] then ()
    let e =
        if [3] = [
            2 // warning FS0058: Possible incorrect indentation
           ] then ()
    
    let f = {
        a = 1 // Ok
    }
    let {
        a = g
    } = f // error FS0010: Incomplete structured construct at or before this point in binding. Expected '=' or other token.

    let _ = h { a =
        2 // Ok
    }
    let _ = h ( a =
        2 // Also ok
    )
    let _ = h {| a =
        2 // warning FS0058: Possible incorrect indentation
    |}

    let i =
        if true then ignore [
            1 // Ok
        ] else ignore [ 2 ]
    let j =
        if true then ignore [ 1 ] else ignore [
            2 // Ok
        ]
    let k =
        let tru _ = true
        if tru [
            1 // warning FS0058: Possible incorrect indentation
        ] then ()
    ()