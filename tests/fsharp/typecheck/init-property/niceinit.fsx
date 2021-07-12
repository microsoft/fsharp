#r @"niceinit.dll"

let niceinit = NiceInit(Val = 1);;

let niceinit2 = NiceInit();;
niceinit2.Val <- 5;;
niceinit2.set_Val 5;;

type StraightNewFactory =
  static member inline mkInlineNiceInit () = NiceInit()
  static member mkNiceInit () = NiceInit()
;;
let ni1 = StraightNewFactory.mkInlineNiceInit(Val=1);;
let ni2 = StraightNewFactory.mkNiceInit(Val=1);;

type IntermediateVariableWithInterleavedOpFactory =
  static member inline mkInlineNiceInit () = 
    let n = NiceInit()
    printfn "inline nice init"
    n
  static member mkNiceInit () =
    let n = NiceInit()
    printfn "nice init"
    n
;;
let ni3 = IntermediateVariableWithInterleavedOpFactory.mkInlineNiceInit(Val=1);;
let ni4 = IntermediateVariableWithInterleavedOpFactory.mkNiceInit(Val=1);;