
neg45.fs(12,5,12,11): typecheck error FS0685: The generic function 'Foo' must be given explicit type argument(s)

neg45.fs(14,5,14,11): typecheck error FS0685: The generic function 'Foo' must be given explicit type argument(s)

neg45.fs(23,5,23,11): typecheck error FS0685: The generic function 'Foo' must be given explicit type argument(s)

neg45.fs(25,5,25,11): typecheck error FS0685: The generic function 'Foo' must be given explicit type argument(s)

neg45.fs(34,25,34,26): typecheck error FS0465: Type inference problem too complicated (maximum iteration depth reached). Consider adding further type annotations.

neg45.fs(41,23,41,41): typecheck error FS0827: This is not a valid name for an active pattern

neg45.fs(52,14,52,17): typecheck error FS0039: The type 'FooBir' does not define the field, constructor or member 'Foo'.

neg45.fs(56,16,56,31): typecheck error FS0827: This is not a valid name for an active pattern

neg45.fs(72,26,72,31): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(72,10,72,19): typecheck error FS0035: This construct is deprecated: This type abbreviation has one or more declared type parameters that do not appear in the type being abbreviated. Type abbreviations must use all declared type parameters in the type being abbreviated. Consider removing one or more type parameters, or use a concrete type definition that wraps an underlying type, such as 'type C<'a> = C of ...'.

neg45.fs(73,36,73,41): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(73,36,73,41): typecheck error FS0193: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(73,20,73,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(74,42,74,47): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(75,41,75,46): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(76,35,76,40): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(77,30,77,35): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(77,30,77,35): typecheck error FS0193: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(77,20,77,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(78,38,78,43): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(79,28,79,33): typecheck error FS0193: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(79,20,79,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(79,20,79,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(80,28,80,33): typecheck error FS0193: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(80,20,80,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(80,20,80,22): typecheck error FS0340: The signature and implementation are not compatible because the declaration of the type parameter 'T' requires a constraint of the form 'T :> System.IComparable

neg45.fs(81,35,81,40): typecheck error FS0001: A type parameter is missing a constraint 'when 'T :> System.IComparable'

neg45.fs(89,26,89,40): typecheck error FS0041: A unique overload for method 'M' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: R1

Candidates:
 - member D.M : 'a -> 'b
 - member D.M : 'a -> 'b

neg45.fs(97,26,97,55): typecheck error FS0041: A unique overload for method 'M' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: (R1 * R1)

Candidates:
 - member D.M : 'a -> 'b
 - member D.M : 'a -> 'b

neg45.fs(104,26,104,31): typecheck error FS0041: A unique overload for method 'M' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known type of argument: int

Candidates:
 - member D.M : 'a -> 'b
 - member D.M : 'a -> 'b

neg45.fs(105,24,105,25): typecheck error FS0025: Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s).

neg45.fs(113,53,113,54): typecheck error FS0040: This and other recursive references to the object(s) being defined will be checked for initialization-soundness at runtime through the use of a delayed reference. This is because you are defining one or more recursive objects, rather than recursive functions. This warning may be suppressed by using '#nowarn "40"' or '--nowarn:40'.

neg45.fs(139,21,139,23): typecheck error FS0001: The type 'A1' does not support the operator 'set_Name'

neg45.fs(140,17,140,19): typecheck error FS0001: The type 'A1' does not support the operator 'get_Age'

neg45.fs(141,17,141,19): typecheck error FS0001: The type 'A2' does not support the operator 'get_Age'

neg45.fs(142,13,142,18): typecheck error FS0001: The type 'A1' does not support the operator 'Name'

neg45.fs(143,13,143,18): typecheck error FS0001: The type 'A1' does not support the operator 'Name'

neg45.fs(144,13,144,23): typecheck error FS0001: The type 'A1' does not support the operator 'get_Name'

neg45.fs(145,13,145,23): typecheck error FS0001: The type 'A2' does not support the operator 'get_Name'

neg45.fs(146,13,146,23): typecheck error FS0001: get_Name is not a static method

neg45.fs(147,15,147,25): typecheck error FS0001: The type 'StaticMutableClassExplicit' does not support the operator 'get_Name'
