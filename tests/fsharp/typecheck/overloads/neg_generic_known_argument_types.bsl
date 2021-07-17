
neg_generic_known_argument_types.fsx(9,16,9,49): typecheck error FS0041: A unique overload for method 'Foo' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known types of arguments:  ^fa * 'fb * 'a * argD:'c when  ^fa : (member X :  ^b ->  ^b) and  ^b : (member BBBB : unit -> unit)

Candidates:
 - static member A.Foo : argA1:'a * argB1:('a -> 'b) * argC1:('a -> 'b) * argD:('a -> 'b) * argZ1:'zzz -> 'b
 - static member A.Foo : argA2:'a * argB2:('a -> 'b) * argC2:('b -> 'c) * argD:('c -> 'd) * argZ2:'zzz -> 'd
