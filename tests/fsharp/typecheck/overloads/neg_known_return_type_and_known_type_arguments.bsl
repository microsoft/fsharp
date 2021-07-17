
neg_known_return_type_and_known_type_arguments.fsx(90,24,90,28): typecheck error FS0001: No overloads match for method 'Zero'.

Known return type: MonoidSample

Known type parameters: < MonoidSample , Zero >

Available overloads:
 - static member Zero.Zero :  ^t * Default1 ->  ^t when  ^t : (static member get_Zero :  ^t) // Argument at index 1 doesn't match
 - static member Zero.Zero :  ^t * Default1 -> ('a1 -> 'a1) when  ^t : null and  ^t : struct // Argument at index 1 doesn't match
 - static member Zero.Zero :  ^t * Default2 ->  ^t when (FromInt32 or  ^t) : (static member FromInt32 :  ^t * FromInt32 -> (int32 ->  ^t)) // Argument at index 1 doesn't match
 - static member Zero.Zero :  ^t * Default2 -> ('a1 -> 'a1) when  ^t : null and  ^t : struct // Argument at index 1 doesn't match
 - static member Zero.Zero :  ^t * Default3 ->  ^t when  ^t : (static member get_Empty :  ^t) // Argument at index 1 doesn't match
 - static member Zero.Zero : 'a array * Zero -> 'a array // Argument at index 1 doesn't match
 - static member Zero.Zero : 'a list * Zero -> 'a list // Argument at index 1 doesn't match
 - static member Zero.Zero : 'a option * Zero -> 'a option // Argument at index 1 doesn't match
 - static member Zero.Zero : ('T ->  ^Monoid) * Zero -> ('T ->  ^Monoid) when (Zero or  ^Monoid) : (static member Zero :  ^Monoid * Zero ->  ^Monoid) // Argument at index 1 doesn't match
 - static member Zero.Zero : Async< ^a> * Zero -> Async< ^a> when (Zero or  ^a) : (static member Zero :  ^a * Zero ->  ^a) // Argument at index 1 doesn't match
 - static member Zero.Zero : Lazy< ^a> * Zero -> Lazy< ^a> when (Zero or  ^a) : (static member Zero :  ^a * Zero ->  ^a) // Argument at index 1 doesn't match
 - static member Zero.Zero : Map<'a,'b> * Zero -> Map<'a,'b> when 'a : comparison // Argument at index 1 doesn't match
 - static member Zero.Zero : ResizeArray<'a> * Zero -> ResizeArray<'a> // Argument at index 1 doesn't match
 - static member Zero.Zero : Set<'a> * Zero -> Set<'a> when 'a : comparison // Argument at index 1 doesn't match
 - static member Zero.Zero : System.TimeSpan * Zero -> System.TimeSpan // Argument at index 1 doesn't match
 - static member Zero.Zero : seq<'a> * Zero -> seq<'a> // Argument at index 1 doesn't match
 - static member Zero.Zero : string * Zero -> string // Argument at index 1 doesn't match
 - static member Zero.Zero : unit * Zero -> unit // Argument at index 1 doesn't match
