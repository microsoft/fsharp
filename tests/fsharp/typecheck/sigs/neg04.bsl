
neg04.fs(5,21,5,94): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(7,21,7,35): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(11,8,11,81): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(14,8,14,22): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(22,8,22,17): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(26,8,26,17): typecheck error FS0912: This declaration element is not permitted in an augmentation

neg04.fs(32,8,32,11): typecheck error FS0039: The type 'Double' does not define the field, constructor or member 'Nan'. Maybe you want one of the following:
   IsNaN

neg04.fs(46,69,46,94): typecheck error FS0001: Type mismatch. Expecting a
    ''a * 'b * 'c * 'e'    
but given a
    ''a * 'b * 'c'    
The tuples have differing lengths of 4 and 3

neg04.fs(46,99,46,107): typecheck error FS0001: Type mismatch. Expecting a
    ''a * 'b * 'c * 'e'    
but given a
    ''a * 'b * 'c'    
The tuples have differing lengths of 4 and 3

neg04.fs(47,30,47,51): typecheck error FS0001: Type mismatch. Expecting a
    'seq<'a> -> 'f'    
but given a
    ''g list -> 'h'    
The type ''a list' does not match the type 'seq<'b>'

neg04.fs(47,49,47,51): typecheck error FS0784: This numeric literal requires that a module 'NumericLiteralN' defining functions FromZero, FromOne, FromInt32, FromInt64 and FromString be in scope

neg04.fs(47,30,47,51): typecheck error FS0001: Type mismatch. Expecting a
    'seq<'a> -> 'f'    
but given a
    ''g list -> 'h'    
The type ''a list' does not match the type 'seq<'c>'

neg04.fs(61,25,61,40): typecheck error FS0001: This expression was expected to have type
    'ClassType1'    
but here has type
    'Object'    

neg04.fs(70,21,70,36): typecheck error FS0698: Invalid constraint: the type used for the constraint is sealed, which means the constraint could only be satisfied by at most one solution

neg04.fs(70,21,70,36): typecheck error FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'a has been constrained to be type 'c<string>'.

neg04.fs(70,12,70,14): typecheck error FS0663: This type parameter has been used in a way that constrains it to always be 'c<string>'

neg04.fs(70,21,70,36): typecheck error FS0698: Invalid constraint: the type used for the constraint is sealed, which means the constraint could only be satisfied by at most one solution

neg04.fs(76,19,76,26): typecheck error FS0698: Invalid constraint: the type used for the constraint is sealed, which means the constraint could only be satisfied by at most one solution

neg04.fs(76,19,76,26): typecheck error FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'a has been constrained to be type 'd'.

neg04.fs(76,10,76,12): typecheck error FS0663: This type parameter has been used in a way that constrains it to always be 'd'

neg04.fs(76,19,76,26): typecheck error FS0698: Invalid constraint: the type used for the constraint is sealed, which means the constraint could only be satisfied by at most one solution

neg04.fs(81,58,81,61): typecheck error FS0001: This expression was expected to have type
    'int'    
but here has type
    ''a * 'b'    

neg04.fs(83,39,83,46): typecheck error FS0752: The operator 'expr.[idx]' has been used on an object of indeterminate type based on information prior to this program point. Consider adding further type constraints

neg04.fs(85,47,85,52): typecheck error FS0039: The type 'Int32' does not define the field, constructor or member 'Item'.

neg04.fs(87,73,87,78): typecheck error FS0752: The operator 'expr.[idx]' has been used on an object of indeterminate type based on information prior to this program point. Consider adding further type constraints

neg04.fs(91,72,91,77): typecheck error FS0752: The operator 'expr.[idx]' has been used on an object of indeterminate type based on information prior to this program point. Consider adding further type constraints

neg04.fs(121,12,121,16): typecheck error FS0193: Type constraint mismatch. The type 
    'R'    
is not compatible with type
    'IBar'    


neg04.fs(126,12,126,16): typecheck error FS0193: Type constraint mismatch. The type 
    'U'    
is not compatible with type
    'IBar'    


neg04.fs(131,12,131,21): typecheck error FS0193: Type constraint mismatch. The type 
    'Struct'    
is not compatible with type
    'IBar'    


neg04.fs(135,10,135,16): typecheck error FS0193: Type constraint mismatch. The type 
    'R'    
is not compatible with type
    'IBar'    


neg04.fs(138,10,138,16): typecheck error FS0193: Type constraint mismatch. The type 
    'U'    
is not compatible with type
    'IBar'    


neg04.fs(141,10,141,21): typecheck error FS0193: Type constraint mismatch. The type 
    'Struct'    
is not compatible with type
    'IBar'    


neg04.fs(144,10,144,26): typecheck error FS0193: Type constraint mismatch. The type 
    'int * int'    
is not compatible with type
    'IBar'    


neg04.fs(147,10,147,20): typecheck error FS0193: Type constraint mismatch. The type 
    'int []'    
is not compatible with type
    'IBar'    


neg04.fs(150,10,150,27): typecheck error FS0193: Type constraint mismatch. The type 
    'int -> int'    
is not compatible with type
    'IBar'    


neg04.fs(159,47,159,57): typecheck error FS0692: This function value is being used to construct a delegate type whose signature includes a byref argument. You must use an explicit lambda expression taking 1 arguments.

neg04.fs(163,54,163,67): typecheck error FS0692: This function value is being used to construct a delegate type whose signature includes a byref argument. You must use an explicit lambda expression taking 1 arguments.

neg04.fs(178,13,178,18): typecheck error FS0001: The type '('a -> unit)' does not support the 'equality' constraint because it is a function type
