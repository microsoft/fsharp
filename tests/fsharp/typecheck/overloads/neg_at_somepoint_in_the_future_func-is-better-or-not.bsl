
neg_at_somepoint_in_the_future_func-is-better-or-not.fsx(14,1,14,17): typecheck error FS0193: The method 'M' has overload with 'System.Func' delegate in the signature, to fix the warning, you need to specify the type explicitly.

neg_at_somepoint_in_the_future_func-is-better-or-not.fsx(16,1,16,19): typecheck error FS0193: The method 'M2' has overload with 'System.Func' delegate in the signature, to fix the warning, you need to specify the type explicitly.

neg_at_somepoint_in_the_future_func-is-better-or-not.fsx(25,1,25,37): typecheck error FS0041: A unique overload for method 'M3' could not be determined based on type information prior to this program point. A type annotation may be needed.

Known types of arguments: (unit -> unit) * (unit -> unit)

Candidates:
 - member C.M3 : [<ParamArray>] funcs:Action array -> unit
 - member C.M3 : [<ParamArray>] funcs:Func<unit,unit> array -> unit

neg_at_somepoint_in_the_future_func-is-better-or-not.fsx(38,5,38,63): typecheck error FS0193: The method 'Min' has overload with 'System.Func' delegate in the signature, to fix the warning, you need to specify the type explicitly.

neg_at_somepoint_in_the_future_func-is-better-or-not.fsx(40,5,40,74): typecheck error FS0193: The method 'Min' has overload with 'System.Func' delegate in the signature, to fix the warning, you need to specify the type explicitly.
