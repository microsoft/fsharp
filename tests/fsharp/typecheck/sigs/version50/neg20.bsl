
neg20.fs(30,28,30,31): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(31,32,31,35): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(32,28,32,31): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(32,32,32,35): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(34,24,34,27): typecheck error FS0001: This expression was expected to have type
    'obj'    
but here has type
    'string'    

neg20.fs(35,24,35,27): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(43,15,43,18): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(44,19,44,22): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(45,15,45,18): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(45,19,45,22): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(47,11,47,14): typecheck error FS0001: This expression was expected to have type
    'obj'    
but here has type
    'string'    

neg20.fs(48,11,48,14): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(52,24,52,31): typecheck error FS0001: This expression was expected to have type
    'A'    
but here has type
    'B'    

neg20.fs(53,38,53,39): typecheck error FS0001: This expression was expected to have type
    'System.ValueType'    
but here has type
    'int'    

neg20.fs(60,26,60,33): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'B'. This element has type 'A'.

neg20.fs(61,27,61,35): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'B1'. This element has type 'B2'.

neg20.fs(62,26,62,33): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'C'. This element has type 'B'.

neg20.fs(66,25,66,32): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'A'. This element has type 'B'.

neg20.fs(67,27,67,34): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'B'. This element has type 'C'.

neg20.fs(70,31,70,38): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'B'. This element has type 'C'.

neg20.fs(71,34,71,42): typecheck error FS0001: Type mismatch. Expecting a
    'A list'    
but given a
    'B list'    
The type 'A' does not match the type 'B'

neg20.fs(75,30,75,37): typecheck error FS0001: This expression was expected to have type
    'B'    
but here has type
    'C'    

neg20.fs(76,34,76,43): typecheck error FS0001: Type mismatch. Expecting a
    'A list'    
but given a
    'B list'    
The type 'A' does not match the type 'B'

neg20.fs(80,23,80,39): typecheck error FS0193: Type constraint mismatch. The type 
    'C list'    
is not compatible with type
    'seq<B>'    


neg20.fs(81,34,81,43): typecheck error FS0001: Type mismatch. Expecting a
    'A list'    
but given a
    'B list'    
The type 'A' does not match the type 'B'

neg20.fs(83,47,83,54): typecheck error FS0001: All branches of an 'if' expression must return values implicitly convertible to the type of the first branch, which here is 'B'. This branch returns a value of type 'C'.

neg20.fs(87,54,87,61): typecheck error FS0001: All branches of a pattern match expression must return values implicitly convertible to the type of the first branch, which here is 'B'. This branch returns a value of type 'C'.

neg20.fs(92,19,92,26): typecheck error FS0001: This expression was expected to have type
    'A'    
but here has type
    'B'    

neg20.fs(96,26,96,33): typecheck error FS0001: This expression was expected to have type
    'B'    
but here has type
    'A'    

neg20.fs(97,26,97,33): typecheck error FS0001: This expression was expected to have type
    'A'    
but here has type
    'B'    

neg20.fs(99,26,99,33): typecheck error FS0001: All elements of a list must be implicitly convertible to the type of the first element, which here is 'B'. This element has type 'A'.

neg20.fs(108,12,108,16): typecheck error FS0001: Type mismatch. Expecting a
    'B * B -> 'a'    
but given a
    'A * A -> Data'    
The type 'B' does not match the type 'A'

neg20.fs(109,12,109,16): typecheck error FS0001: Type mismatch. Expecting a
    'A * B -> 'a'    
but given a
    'A * A -> Data'    
The type 'B' does not match the type 'A'

neg20.fs(110,12,110,16): typecheck error FS0001: Type mismatch. Expecting a
    'B * A -> 'a'    
but given a
    'A * A -> Data'    
The type 'B' does not match the type 'A'

neg20.fs(128,19,128,22): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(129,19,129,22): typecheck error FS0001: This expression was expected to have type
    'obj'    
but here has type
    'string'    

neg20.fs(131,5,131,24): typecheck error FS0041: No overloads match for method 'OM3'.

Known types of arguments: string * obj

Available overloads:
 - static member C.OM3 : x:'b * y:'b -> int // Argument 'y' doesn't match
 - static member C.OM3 : x:'b * y:int -> int // Argument 'y' doesn't match

neg20.fs(152,13,152,23): typecheck error FS0033: The type 'Test.BadNumberOfGenericParameters.C<_>' expects 1 type argument(s) but is given 2

neg20.fs(153,13,153,23): typecheck error FS0033: The type 'Test.BadNumberOfGenericParameters.C<_>' expects 1 type argument(s) but is given 2

neg20.fs(154,13,154,25): typecheck error FS0502: The member or object constructor 'SM1' takes 0 type argument(s) but is here given 1. The required signature is 'static member C.SM1 : unit -> int'.

neg20.fs(155,13,155,26): typecheck error FS0502: The member or object constructor 'SM2' takes 0 type argument(s) but is here given 1. The required signature is 'static member C.SM2 : y:int -> int'.

neg20.fs(156,13,156,28): typecheck error FS0502: The member or object constructor 'SM3' takes 0 type argument(s) but is here given 1. The required signature is 'static member C.SM3 : y:int * z:int -> int'.

neg20.fs(157,28,157,29): typecheck error FS0495: The member or object constructor 'SM3' has no argument or settable return property 'x'. The required signature is static member C.SM3 : y:int * z:int -> int.

neg20.fs(158,13,158,36): typecheck error FS0502: The member or object constructor 'SM4' takes 1 type argument(s) but is here given 2. The required signature is 'static member C.SM4 : y:'a * z:'b -> int'.

neg20.fs(159,13,159,32): typecheck error FS0502: The member or object constructor 'SM5' takes 2 type argument(s) but is here given 1. The required signature is 'static member C.SM5 : y:'a * z:'b -> int'.

neg20.fs(162,13,162,24): typecheck error FS0502: The member or object constructor 'M1' takes 0 type argument(s) but is here given 1. The required signature is 'member C.M1 : unit -> int'.

neg20.fs(163,13,163,25): typecheck error FS0502: The member or object constructor 'M2' takes 0 type argument(s) but is here given 1. The required signature is 'member C.M2 : y:int -> int'.

neg20.fs(164,13,164,27): typecheck error FS0502: The member or object constructor 'M3' takes 0 type argument(s) but is here given 1. The required signature is 'member C.M3 : y:int * z:int -> int'.

neg20.fs(165,27,165,28): typecheck error FS0495: The member or object constructor 'M3' has no argument or settable return property 'x'. The required signature is member C.M3 : y:int * z:int -> int.

neg20.fs(166,13,166,35): typecheck error FS0502: The member or object constructor 'M4' takes 1 type argument(s) but is here given 2. The required signature is 'member C.M4 : y:'a * z:'b -> int'.

neg20.fs(167,13,167,31): typecheck error FS0502: The member or object constructor 'M5' takes 2 type argument(s) but is here given 1. The required signature is 'member C.M5 : y:'a * z:'b -> int'.

neg20.fs(182,14,182,31): typecheck error FS0041: No overloads match for method 'M'.

Known types of arguments: string * obj

Available overloads:
 - static member C2.M : fmt:string * [<System.ParamArray>] args:int [] -> string // Argument 'args' doesn't match
 - static member C2.M : fmt:string * [<System.ParamArray>] args:int [] -> string // Argument at index 1 doesn't match

neg20.fs(183,29,183,34): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(183,29,183,34): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(183,35,183,40): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(183,35,183,40): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(183,14,183,41): typecheck error FS0001: This expression was expected to have type
    'unit'    
but here has type
    'string'    

neg20.fs(184,28,184,33): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(184,28,184,33): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(184,34,184,39): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(184,34,184,39): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    'obj'    

neg20.fs(188,14,188,31): typecheck error FS0041: No overloads match for method 'M'.

Known types of arguments: string * obj

Available overloads:
 - static member C3.M : fmt:string * [<System.ParamArray>] args:string [] -> string // Argument 'args' doesn't match
 - static member C3.M : fmt:string * [<System.ParamArray>] args:string [] -> string // Argument at index 1 doesn't match

neg20.fs(189,29,189,34): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(189,29,189,34): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(189,35,189,40): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(189,35,189,40): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(189,14,189,41): typecheck error FS0001: This expression was expected to have type
    'unit'    
but here has type
    'string'    

neg20.fs(190,28,190,33): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(190,28,190,33): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(190,34,190,39): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(190,34,190,39): typecheck error FS0001: This expression was expected to have type
    'string'    
but here has type
    'obj'    

neg20.fs(195,5,195,10): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(198,5,198,11): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(201,3,202,9): typecheck error FS0825: The 'DefaultValue' attribute may only be used on 'val' declarations

neg20.fs(204,5,204,14): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(207,5,207,11): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(210,5,210,12): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(213,5,213,33): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(216,5,216,12): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(219,5,219,15): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(222,5,222,24): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(225,5,225,22): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(228,5,228,23): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(231,5,231,21): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(234,5,234,34): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(237,5,237,34): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(240,5,240,23): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(243,5,243,23): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(249,9,249,27): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(255,5,255,21): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(258,5,258,31): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(261,5,261,17): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(265,5,265,24): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(268,5,268,27): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(271,5,271,13): typecheck error FS0842: This attribute is not valid for use on this language element

neg20.fs(278,14,278,95): typecheck error FS0507: No accessible member or object constructor named 'ProcessStartInfo' takes 0 arguments. Note the call to this member also provides 2 named arguments.

neg20.fs(280,14,280,94): typecheck error FS0508: No accessible member or object constructor named 'ProcessStartInfo' takes 0 arguments. The named argument 'Argument' doesn't correspond to any argument or settable return property for any overload.

neg20.fs(285,12,285,13): typecheck error FS0038: 'x' is bound twice in this pattern

neg20.fs(286,14,286,15): typecheck error FS0038: 'x' is bound twice in this pattern

neg20.fs(288,17,288,18): typecheck error FS0038: 'x' is bound twice in this pattern

neg20.fs(294,5,294,36): typecheck error FS0840: Unrecognized attribute target. Valid attribute targets are 'assembly', 'module', 'type', 'method', 'property', 'return', 'param', 'field', 'event', 'constructor'.

neg20.fs(301,8,301,16): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(304,8,304,16): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(307,8,307,20): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(310,8,310,17): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(313,8,313,15): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(316,8,316,19): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(319,8,319,17): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(322,8,322,18): typecheck error FS3132: This type definition may not have the 'CLIMutable' attribute. Only record types may have this attribute.

neg20.fs(335,11,335,24): typecheck error FS0041: A unique overload for method 'String' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: 'a0

Candidates:
 - System.String(value: char []) : System.String
 - System.String(value: nativeptr<char>) : System.String
 - System.String(value: nativeptr<sbyte>) : System.String

neg20.fs(336,11,336,22): typecheck error FS0041: A unique overload for method 'Guid' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: 'a0

Candidates:
 - System.Guid(b: byte []) : System.Guid
 - System.Guid(g: string) : System.Guid

neg20.fs(355,19,355,38): typecheck error FS1124: Multiple types exist called 'OverloadedClassName', taking different numbers of generic parameters. Provide a type instantiation to disambiguate the type resolution, e.g. 'OverloadedClassName<_>'.

neg20.fs(356,22,356,41): typecheck error FS1124: Multiple types exist called 'OverloadedClassName', taking different numbers of generic parameters. Provide a type instantiation to disambiguate the type resolution, e.g. 'OverloadedClassName<_>'.

neg20.fs(370,19,370,38): typecheck error FS1133: No constructors are available for the type 'OverloadedClassName<'a,'b>'

neg20.fs(371,19,371,38): typecheck error FS1124: Multiple types exist called 'OverloadedClassName', taking different numbers of generic parameters. Provide a type instantiation to disambiguate the type resolution, e.g. 'OverloadedClassName<_>'.

neg20.fs(372,22,372,41): typecheck error FS1133: No constructors are available for the type 'OverloadedClassName<'a,'b>'

neg20.fs(373,22,373,41): typecheck error FS1124: Multiple types exist called 'OverloadedClassName', taking different numbers of generic parameters. Provide a type instantiation to disambiguate the type resolution, e.g. 'OverloadedClassName<_>'.

neg20.fs(382,19,382,40): typecheck error FS1124: Multiple types exist called 'OverloadedClassName', taking different numbers of generic parameters. Provide a type instantiation to disambiguate the type resolution, e.g. 'OverloadedClassName<_>'.

neg20.fs(383,39,383,41): typecheck error FS0039: The type 'OverloadedClassName<T>' does not define the field, constructor or member 'S2'.

neg20.fs(428,19,428,38): typecheck error FS1133: No constructors are available for the type 'OverloadedClassName<'a,'b>'

neg20.fs(430,22,430,41): typecheck error FS1133: No constructors are available for the type 'OverloadedClassName<'a,'b>'

neg20.fs(444,39,444,41): typecheck error FS0039: The type 'OverloadedClassName' does not define the field, constructor or member 'S2'.
