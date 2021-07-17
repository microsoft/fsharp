namespace CrashFSC

open System

// debatable code, but it was the minimalist example I could come up 
// with after analysis in the original project
module OhOh =

    type MyByte = MyByte of byte with
        static member inline op_Explicit (MyByte x): int64 = int64 x
        static member inline op_Explicit (MyByte x): double = double x

        static member inline op_Explicit (x: int64): MyByte = MyByte (byte x)
        static member inline op_Explicit (x: float): MyByte = MyByte (byte x)        
        //static member inline op_Explicit (MyByte x): 'a = failwith "cannot convert"
        
    /// testing testing
    let inline ( !>>> ) (a: ^a) min: ^b option =
        if a < (^b : (static member op_Explicit: ^b -> ^a) min)  
            then None
        else    
            Some (^b : (static member op_Explicit: ^a -> ^b) a)

    let inline crashMe (a: ^a) min =
        let (result: MyByte option) = !>>> a min
        result