open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Runtime.InteropServices
type Default6 = class end
type Default5 = class inherit Default6 end
type Default4 = class inherit Default5 end
type Default3 = class inherit Default4 end
type Default2 = class inherit Default3 end
type Default1 = class inherit Default2 end

[<Extension; Sealed>]
type Plus =     
    inherit Default1
    static member inline ``+`` (x: 'Plus             , y: 'Plus             ,             _mthd: Default2) = (^Plus :  (static member (<|>) : _*_ -> _) x, y) : ^Plus
    static member inline ``+`` (x: 'Plus             , y: 'Plus             , [<Optional>]_mthd: Default1) = x + y : ^Plus

    
    static member inline Invoke (x: 'Plus) (y: 'Plus) : 'Plus =
        let inline call (mthd : ^M, input1 : ^I, input2 : ^I) = ((^M or ^I) : (static member ``+`` : _*_*_ -> _) input1, input2, mthd)
        call (Unchecked.defaultof<Plus>, x, y)

type FromInt32 =
  inherit Default1
  static member inline FromInt32 (_: ^R        , _: Default1 ) = fun (x: int32) -> (^R : (static member FromInt32 : _ -> ^R) x)
  static member inline FromInt32 (_: Default1  , _: Default1 ) = fun (x: int32) -> (^R : (static member FromInt32 : _ -> ^R) x)
  static member        FromInt32 (_: int32     , _: FromInt32) = fun (x: int32) ->                 x
  static member        FromInt32 (_: int64     , _: FromInt32) = fun (x: int32) -> int64           x
  #if !FABLE_COMPILER
  static member        FromInt32 (_: nativeint , _: FromInt32) = fun (x: int32) -> nativeint  (int x)
  static member        FromInt32 (_: unativeint, _: FromInt32) = fun (x: int32) -> unativeint (int x)
  #endif
  static member        FromInt32 (_: bigint    , _: FromInt32) = fun (x: int32) -> bigint          x
  static member        FromInt32 (_: float     , _: FromInt32) = fun (x: int32) -> float           x
  static member        FromInt32 (_: sbyte     , _: FromInt32) = fun (x: int32) -> sbyte           x
  static member        FromInt32 (_: int16     , _: FromInt32) = fun (x: int32) -> int16           x
  static member        FromInt32 (_: byte      , _: FromInt32) = fun (x: int32) -> byte            x
  static member        FromInt32 (_: uint16    , _: FromInt32) = fun (x: int32) -> uint16          x
  static member        FromInt32 (_: uint32    , _: FromInt32) = fun (x: int32) -> uint32          x
  static member        FromInt32 (_: uint64    , _: FromInt32) = fun (x: int32) -> uint64          x
  static member        FromInt32 (_: float32   , _: FromInt32) = fun (x: int32) -> float32         x
  static member        FromInt32 (_: decimal   , _: FromInt32) = fun (x: int32) -> decimal         x

  static member inline Invoke (x: int32) : 'Num =
      let inline call_2 (a: ^a, b: ^b) = ((^a or ^b) : (static member FromInt32 : _*_ -> _) b, a)
      let inline call (a: 'a) = fun (x: 'x) -> call_2 (a, Unchecked.defaultof<'r>) x : 'r
      call Unchecked.defaultof<FromInt32> x

type Zero =
  inherit Default1
  static member inline Zero (_: 't                             , _: Default3) = (^t : (static member Empty : ^t) ()) : 't
  static member inline Zero (_: 't                             , _: Default2) = FromInt32.Invoke 0             : 't

  static member inline Zero (_: 't                             , _: Default1) = LanguagePrimitives.GenericZero : 't

  static member        Zero (_: System.TimeSpan                , _: Zero    ) = System.TimeSpan ()
  static member        Zero (_: list<'a>                       , _: Zero    ) = []   :   list<'a>
  static member        Zero (_: option<'a>                     , _: Zero    ) = None : option<'a>
  static member        Zero (_: array<'a>                      , _: Zero    ) = [||] :  array<'a>
  static member        Zero (_: string                         , _: Zero    ) = ""
  static member        Zero (_: unit                           , _: Zero    ) = ()
  static member        Zero (_: Set<'a>                        , _: Zero    ) = Set.empty : Set<'a>
  static member        Zero (_: Map<'a,'b>                     , _: Zero    ) = Map.empty : Map<'a,'b>

  static member inline Invoke () = 
      let inline call_2 (a: ^a, b: ^b) = ((^a or ^b) : (static member Zero : _*_ -> _) b, a)
      let inline call (a: 'a) = call_2 (a, Unchecked.defaultof<'r>) : 'r
      call Unchecked.defaultof<Zero>

type Zero with
  static member inline Zero (_: 'T->'Monoid               , _: Zero) = (fun _ -> Zero.Invoke ()) : 'T->'Monoid
  static member inline Zero (_: Async<'a>                 , _: Zero) = let (v: 'a) = Zero.Invoke () in async.Return v
  static member inline Zero (_: Lazy<'a>                  , _: Zero) = let (v: 'a) = Zero.Invoke () in lazy v
  static member        Zero (_: ResizeArray<'a>           , _: Zero) = ResizeArray () : ResizeArray<'a>
  static member        Zero (_: seq<'a>                   , _: Zero) = Seq.empty      : seq<'a>

let inline (++) (x: 'Monoid) (y: 'Monoid) : 'Monoid = Plus.Invoke x y
let inline zero< ^Monoid when (Zero or ^Monoid) : (static member Zero : ^Monoid * Zero -> ^Monoid) > : ^Monoid = Zero.Invoke ()

type MonoidSample = 
  | MonoidSample of int

  static member getr_Zero () = MonoidSample 0
  //static member (+) (MonoidSample(x), MonoidSample(y)) = MonoidSample (x+y)
  static member (+) (MonoidSample(x), y) = MonoidSample (x+y), 1
  static member (+) (x,MonoidSample(y)) = MonoidSample (x+y)
  static member (+) (x, y) = MonoidSample (x+y), 3

let a = MonoidSample 1
let b = MonoidSample 2
let c : MonoidSample = zero
