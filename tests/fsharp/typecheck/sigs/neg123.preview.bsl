
neg123.fs(19,49,19,51): typecheck error FS0001: No overloads match for method 'pass'.

Known type parameter: < (unit -> unit) >

Available overloads:
 - static member Switcher.pass : (int ->  ^r) -> unit when (Switcher or  ^r) : (static member pass :  ^r -> unit) // Argument at index 1 doesn't match
 - static member Switcher.pass : (string ->  ^r) -> unit when (Switcher or  ^r) : (static member pass :  ^r -> unit) // Argument at index 1 doesn't match
 - static member Switcher.pass : int -> unit // Argument at index 1 doesn't match
 - static member Switcher.pass : unit -> unit // Argument at index 1 doesn't match
