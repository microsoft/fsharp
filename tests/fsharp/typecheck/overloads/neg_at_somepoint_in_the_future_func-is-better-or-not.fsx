open System

type C() =
    member _.M(a: Action<int>) = printfn "M(action)"
    member _.M(f: Func<int, unit>) = printfn "M(func)"
    member _.M2(f: Func<unit, unit>) = printfn "M2(func())"
    member _.M2(f: Action) = printfn "M2(action())"
    member _.M3([<ParamArray>] funcs: Func<unit,unit> array) = printfn "M3(funcs)"
    member _.M3([<ParamArray>] funcs: Action array) = printfn "M3(actions)"
let c = C()

// https://github.com/dotnet/fsharp/issues/11534
// we may want a warning here, or maybe an error proper at some point
c.M(fun _ -> ())

c.M2(fun () -> ())

c.M3(Action(fun () -> ()), (fun () -> ()))
c.M3(Action id, id, id, id)
c.M3(Func<_,_> id, id, id, id)
c.M3(Func<_,_>(fun () -> ()), (fun () -> ()))
c.M3((fun () -> ()), Func<_,_>(fun () -> ()))

// this one doesn't resolve, although without the param array, it would
c.M3((fun () -> ()), (fun () -> ()))


open System.Collections.Generic
open System.Linq

// here we verify different style of calling BCL overloaded methods
type Query =
  static member MinBy_ExplicitDelegate<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: IEnumerable<'T>, valueSelector: 'T -> 'Key) =
    Enumerable.Min(source, Func<'T, 'Key>(valueSelector))
  static member MinBy_ExplicitDelegateWithArgName<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: IEnumerable<'T>, valueSelector: 'T -> 'Key) =
    Enumerable.Min(source, selector = Func<'T, 'Key>(valueSelector))
  static member MinBy_Lambda<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: IEnumerable<'T>, valueSelector: 'T -> 'Key) =
    Enumerable.Min(source, (fun (t: 'T) -> (valueSelector t)))
  static member MinBy_LambdaWithArgName<'T, 'Q, 'Key when 'Key: equality and 'Key: comparison> (source: IEnumerable<'T>, valueSelector: 'T -> 'Key) =
    Enumerable.Min(source, selector = (fun (t: 'T) -> (valueSelector t)))

