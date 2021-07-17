module Neg133

// This code should fail to compile regardless RFC-1043

let inline CallReturn< ^M, ^R, 'T when (^M or ^R) : (static member Return : unit -> ('T -> ^R))> () = 
    ((^M or ^R) : (static member Return : unit -> ('T -> ^R)) ())

let inline CallApply< ^M, ^I1, ^I2, ^R when (^M or ^I1 or ^I2) : (static member Apply : ^I1 * ^I2 -> ^R)> (input1: ^I1, input2: ^I2) =
    ((^M or ^I1 or ^I2) : (static member Apply : ^I1 * ^I2 -> ^R) input1, input2)

let inline CallMap< ^M, ^F, ^I, ^R when (^M or ^I or ^R) : (static member Map : ^F * ^I -> ^R)>  (mapping: ^F, source: ^I) : ^R =
    ((^M or ^I or ^R) : (static member Map : ^F * ^I -> ^R) mapping, source)

let inline CallSequence< ^M, ^I, ^R when (^M or ^I) : (static member Sequence : ^I -> ^R)> (b: ^I) : ^R = 
    ((^M or ^I) : (static member Sequence : ^I -> ^R) b)

type Return = class end

type Apply = class end

type Map = class end

type Sequence = class end

let inline InvokeReturn (x: 'T) : ^R =
    CallReturn< Return , ^R , 'T> () x

let inline InvokeApply (f: ^I1) (x: ^I2) : ^R =
    CallApply<Apply, ^I1, ^I2, ^R>(f, x)

let inline InvokeMap (mapping: ^F) (source: ^I) : ^R = 
    CallMap<Map, ^F, ^I, ^R> (mapping, source)

type Sequence with
    static member inline Sequence (t: list<option<'t>>) : ^R =
        List.foldBack (fun (x: 't option) (ys: ^R) -> InvokeApply (InvokeMap (fun x y -> x :: y) x) ys) t (InvokeReturn [])

type Map with
    static member Map (f: 'T->'U, x: option<_>) = Option.map  f x

type Apply with
    static member Apply (f: option<_>, x: option<'T>) : option<'U> =  failwith ""

type Return with    
    static member Return () = fun x -> Some x : option<'a>
let res18() = 
    CallSequence<Sequence, _, _> [Some 3; Some 2; Some 1]
