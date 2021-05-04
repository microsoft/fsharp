open System.Runtime.InteropServices


type I =
  interface
    [<RequireNamedArgument>]
    abstract member I: i:int -> unit
  end

[<AbstractClass>]
type Abstract() =
  [<RequireNamedArgument>]
  abstract Method: i:int -> unit
  [<RequireNamedArgument>]
  abstract DefaultMethod: i:int -> unit
  default this.DefaultMethod(i) = printfn $"{i}"

type Concrete() =
  inherit Abstract()
  interface I with
    member x.I(i:int) = printfn $"{i}"
  override x.Method(_) = printfn "method!"
  override x.DefaultMethod(a) = printfn "default method!"; base.DefaultMethod(a)

type A() =

  [<RequireNamedArgument>]
  member x.B(c:int, d:string) = ()

  [<RequireNamedArgument>]
  static member C(d:byte) = ()

  [<RequireNamedArgument>]
  static member C() = ()

type B() =
  inherit A()
  member x.Opt([<Optional;DefaultParameterValue(1)>]i: int, j: int) = ()
let a = A()

a.B(1, "")

A.C(1uy)

A.C()

let b = B()
b.Opt(j=2)
let i = Concrete() :> I
i.I(1)

let ii = { new I with member x.I(b:int) = printfn $"{b}"}

ii.I(2)

let concrete = Concrete()
concrete.DefaultMethod(1)
concrete.Method(1)
let baseClass = concrete :> Abstract
baseClass.DefaultMethod(1)
baseClass.Method(1)