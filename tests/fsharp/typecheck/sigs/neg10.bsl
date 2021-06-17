
neg10.fsi(9,6,9,7): typecheck error FS0249: Two type definitions named 'x' occur in namespace 'N' in two parts of this assembly

neg10.fs(11,17,11,27): typecheck error FS0946: Cannot inherit from interface type. Use interface ... with instead.

neg10.fs(13,17,13,26): typecheck error FS0945: Cannot inherit a sealed type

neg10.fs(15,22,15,32): typecheck error FS0887: The type 'C1' is not an interface type

neg10.fs(16,32,16,34): typecheck error FS1207: Interfaces inherited by other interfaces should be declared using 'inherit ...' instead of 'interface ...'

neg10.fs(16,32,16,34): typecheck error FS0887: The type 'C1' is not an interface type

neg10.fs(16,32,16,34): typecheck error FS1207: Interfaces inherited by other interfaces should be declared using 'inherit ...' instead of 'interface ...'

neg10.fs(17,28,17,30): typecheck error FS0887: The type 'C1' is not an interface type

neg10.fs(19,17,19,22): typecheck error FS0870: Structs cannot have an object constructor with no arguments. This is a restriction imposed on all CLI languages as structs automatically support a default constructor.

neg10.fs(21,16,21,46): typecheck error FS0001: A generic construct requires that the type 'System.Enum' have a public default constructor

neg10.fs(22,16,22,51): typecheck error FS0001: A generic construct requires that the type 'System.ValueType' have a public default constructor

neg10.fs(23,16,23,38): typecheck error FS0001: A generic construct requires that the type 'obj' is a CLI or F# struct type

neg10.fs(24,16,24,41): typecheck error FS0001: A generic construct requires that the type 'string' have a public default constructor

neg10.fs(25,16,25,53): typecheck error FS0001: This type parameter cannot be instantiated to 'Nullable'. This is a restriction imposed in order to ensure the meaning of 'null' in some CLI languages is not confusing when used in conjunction with 'Nullable' values.

neg10.fs(54,17,54,20): typecheck error FS0060: Override implementations in augmentations are now deprecated. Override implementations should be given as part of the initial declaration of a type.

neg10.fs(66,19,66,21): typecheck error FS0069: Interface implementations in augmentations are now deprecated. Interface implementations should be given on the initial declaration of a type.

neg10.fs(77,13,77,34): typecheck error FS0896: Enumerations cannot have members

neg10.fs(84,13,84,29): typecheck error FS0896: Enumerations cannot have members

neg10.fs(90,23,90,41): typecheck error FS0907: Enumerations cannot have interface declarations

neg10.fs(99,23,99,29): typecheck error FS0907: Enumerations cannot have interface declarations

neg10.fs(107,10,107,17): typecheck error FS0964: Type abbreviations cannot have augmentations

neg10.fs(109,13,109,34): typecheck error FS0895: Type abbreviations cannot have members

neg10.fs(114,10,114,17): typecheck error FS0964: Type abbreviations cannot have augmentations

neg10.fs(116,13,116,29): typecheck error FS0895: Type abbreviations cannot have members

neg10.fs(120,10,120,17): typecheck error FS0964: Type abbreviations cannot have augmentations

neg10.fs(122,23,122,41): typecheck error FS0906: Type abbreviations cannot have interface declarations

neg10.fs(129,10,129,17): typecheck error FS0964: Type abbreviations cannot have augmentations

neg10.fs(131,23,131,29): typecheck error FS0906: Type abbreviations cannot have interface declarations

neg10.fs(169,32,169,35): typecheck error FS0035: This construct is deprecated: This form of object expression is not used in F#. Use 'member this.MemberName ... = ...' to define member implementations in object expressions.

neg10.fs(169,32,169,33): typecheck error FS3213: The member 'X : unit -> 'a' matches multiple overloads of the same method.
Please restrict it to one of the following:
   X : unit -> 'a
   X : unit -> 'a.

neg10.fs(169,19,169,26): typecheck error FS0783: At least one override did not correctly implement its corresponding abstract member

neg10.fs(174,9,175,20): typecheck error FS0951: Literal enumerations must have type int, uint, int16, uint16, int64, uint64, byte, sbyte or char

neg10.fs(180,10,180,11): typecheck error FS0866: Interfaces cannot contain definitions of object constructors

neg10.fs(193,39,193,46): typecheck error FS0767: The type Foo contains the member 'MyX' but it is not a virtual or abstract method that is available to override or implement.

neg10.fs(193,41,193,44): typecheck error FS0017: The member 'MyX : unit -> int' does not have the correct type to override any given virtual method

neg10.fs(193,20,193,23): typecheck error FS0783: At least one override did not correctly implement its corresponding abstract member

neg10.fs(200,11,200,18): typecheck error FS0745: This is not a valid name for an enumeration case

neg10.fs(209,13,209,14): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(210,13,210,14): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(211,13,211,14): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(212,13,212,14): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(213,22,213,23): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(213,33,213,34): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(214,10,214,11): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(216,5,216,18): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(217,13,217,15): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(218,13,218,15): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(219,13,219,15): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(220,13,220,15): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(221,23,221,25): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(221,35,221,37): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(222,10,222,12): typecheck error FS0044: This construct is deprecated. Obsolete

neg10.fs(239,47,239,48): typecheck error FS0001: A type parameter is missing a constraint 'when 'b :> C'

neg10.fs(239,47,239,48): typecheck error FS0193: Type constraint mismatch. The type 
    ''b'    
is not compatible with type
    'C'    


neg10.fs(245,50,245,51): typecheck error FS0193: A type parameter is missing a constraint 'when 'b :> C'

neg10.fs(245,17,245,40): typecheck error FS0043: A type parameter is missing a constraint 'when 'b :> C'

neg10.fs(251,49,251,61): typecheck error FS0001: The type '('a -> 'a)' does not support the 'equality' constraint because it is a function type

neg10.fs(252,45,252,57): typecheck error FS0001: The type '('a -> 'a)' does not support the 'comparison' constraint. For example, it does not support the 'System.IComparable' interface

neg10.fs(253,36,253,48): typecheck error FS0001: The type '('a -> 'a)' does not support the 'equality' constraint because it is a function type

neg10.fs(297,17,297,24): typecheck error FS1187: An indexer property must be given at least one argument

neg10.fs(298,17,298,24): typecheck error FS1187: An indexer property must be given at least one argument

neg10.fs(299,17,299,24): typecheck error FS0807: Property 'S2' is not readable

neg10.fs(300,17,300,24): typecheck error FS0807: Property 'S3' is not readable

neg10.fs(301,17,301,24): typecheck error FS0807: Property 'S4' is not readable

neg10.fs(303,17,303,22): typecheck error FS0807: Property 'SS2' is not readable

neg10.fs(304,17,304,22): typecheck error FS0807: Property 'SS3' is not readable

neg10.fs(305,17,305,22): typecheck error FS0807: Property 'SS4' is not readable

neg10.fs(316,17,316,28): typecheck error FS0807: Property 'X' is not readable

neg10.fs(324,17,324,29): typecheck error FS1187: An indexer property must be given at least one argument

neg10.fs(333,17,333,29): typecheck error FS1187: An indexer property must be given at least one argument

neg10.fs(335,17,335,39): typecheck error FS0501: The member or object constructor 'X' takes 2 argument(s) but is here given 3. The required signature is 'member T3.X : a:int -> int * int with set'.

neg10.fs(345,23,345,24): typecheck error FS0001: The type 'SqlDecimal' does not support a conversion to the type 'float'

neg10.fs(362,25,362,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'single'

neg10.fs(363,24,363,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'sbyte'

neg10.fs(364,24,364,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int16'

neg10.fs(365,25,365,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint16'

neg10.fs(366,25,366,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint32'

neg10.fs(367,25,367,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint64'

neg10.fs(368,24,368,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int64'

neg10.fs(369,26,369,27): typecheck error FS0001: The type 'C' does not support a conversion to the type 'decimal'

neg10.fs(388,25,388,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'single'

neg10.fs(389,24,389,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'sbyte'

neg10.fs(390,24,390,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int16'

neg10.fs(391,25,391,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint16'

neg10.fs(392,25,392,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint32'

neg10.fs(393,25,393,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint64'

neg10.fs(394,24,394,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int64'

neg10.fs(395,26,395,27): typecheck error FS0001: The type 'C' does not support a conversion to the type 'decimal'

neg10.fs(399,25,399,40): typecheck error FS0093: The type 'C' does not support a conversion to the type 'char'

neg10.fs(401,25,401,40): typecheck error FS0093: The type 'C' does not support a conversion to the type 'byte'

neg10.fs(403,27,403,42): typecheck error FS0093: The type 'C' does not support a conversion to the type 'single'

neg10.fs(404,26,404,41): typecheck error FS0093: The type 'C' does not support a conversion to the type 'sbyte'

neg10.fs(405,26,405,41): typecheck error FS0093: The type 'C' does not support a conversion to the type 'int16'

neg10.fs(406,27,406,42): typecheck error FS0093: The type 'C' does not support a conversion to the type 'uint16'

neg10.fs(407,27,407,42): typecheck error FS0093: The type 'C' does not support a conversion to the type 'uint32'

neg10.fs(408,27,408,42): typecheck error FS0093: The type 'C' does not support a conversion to the type 'uint64'

neg10.fs(409,26,409,41): typecheck error FS0093: The type 'C' does not support a conversion to the type 'int64'

neg10.fs(410,28,410,43): typecheck error FS0093: The type 'C' does not support a conversion to the type 'decimal'

neg10.fs(422,24,422,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'float'

neg10.fs(423,23,423,24): typecheck error FS0001: The type 'C' does not support a conversion to the type 'char'

neg10.fs(424,23,424,24): typecheck error FS0001: The type 'C' does not support a conversion to the type 'byte'

neg10.fs(426,25,426,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'single'

neg10.fs(427,24,427,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'sbyte'

neg10.fs(428,24,428,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int16'

neg10.fs(429,25,429,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint16'

neg10.fs(430,25,430,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint32'

neg10.fs(431,25,431,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint64'

neg10.fs(432,24,432,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int64'

neg10.fs(433,26,433,27): typecheck error FS0001: The type 'C' does not support a conversion to the type 'decimal'

neg10.fs(446,24,446,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'float'

neg10.fs(447,23,447,24): typecheck error FS0001: The type 'C' does not support a conversion to the type 'char'

neg10.fs(448,23,448,24): typecheck error FS0001: The type 'C' does not support a conversion to the type 'byte'

neg10.fs(450,25,450,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'single'

neg10.fs(451,24,451,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'sbyte'

neg10.fs(452,24,452,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int16'

neg10.fs(453,25,453,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint16'

neg10.fs(454,25,454,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint32'

neg10.fs(455,25,455,26): typecheck error FS0001: The type 'C' does not support a conversion to the type 'uint64'

neg10.fs(456,24,456,25): typecheck error FS0001: The type 'C' does not support a conversion to the type 'int64'

neg10.fs(457,26,457,27): typecheck error FS0001: The type 'C' does not support a conversion to the type 'decimal'

neg10.fsi(1,1,1,1): typecheck error FS0240: The signature file 'Neg10' does not have a corresponding implementation file. If an implementation file exists then check the 'module' and 'namespace' declarations in the signature and implementation files match.
