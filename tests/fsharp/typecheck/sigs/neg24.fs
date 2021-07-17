module Test
open Microsoft.FSharp.Quotations


let test2 (v : Expr<'a> -> Expr<'b>) = <@ fun (i: 'a) -> %v <@i@> @>

let test (v : 'a -> Expr<'b>) = <@ fun (i: 'a) -> %(v i) @>


module OldNegative = 
    let v1 = [ if true then 1 else 2 ]  // no longer an error or warning
    let v2 = [ if true then () else () ] // no longer an error or warning 
    let v6 = [ if true then () ]   // no longer an error or warning
    let a1 = [| if true then 1 else 2 |]   // no longer an error or warning
    let a2 = [| if true then () else () |]  // no longer an error or warning
    let a6 = [| if true then () |]  // no longer an error or warning
    let s3 = seq { (if true then 1 else 2) }  // no longer an error or warning

// expect no warning
module Positive = 
    let v3 = [ (if true then 1 else 2) ] 
    let v4 = [ if true then yield 1 else yield 2 ] 
    let v5 = [ if true then yield 1 ] 
    let a3 = [| (if true then 1 else 2) |] 
    let a4 = [| if true then yield 1 else yield 2 |] 
    let a5 = [| if true then yield 1 |] 
    let s2 = seq { if true then () else () } 
    let s6 = seq { if true then () } 
    let s4 = seq { if true then yield 1 else yield 2 }
    let s5 = seq { if true then yield 1  }


module BadCurriedExtensionMember = 
        type C() =
            member x.P = 1
            
        module M1 = 
            type C with 
                member x.M1 a b = a + b    
                member x.M2 (a,b) c = a + b + c

        module M2 = 
            type C with 
                member x.M1 a b = a + b    
                member x.M2 (a,b) c = a + b + c

        open M1
        open M2

        let c = C()

        // negative test - error expected here
        let x1 : int = c.M1 3 4
        // negative test - error expected here
        let x2 : int -> int = c.M1 3
        // negative test - error expected here
        let x3 : int -> int -> int = c.M1 
        
        // negative test - error expected here
        let y1 : int = c.M2 (3,4) 4
        // negative test - error expected here
        let y2 : int -> int = c.M2 (3,4)
        // negative test - error expected here
        let y3 : int * int -> int -> int = c.M2

type C() = 
    member x.M(abc:int,def:string) = abc + def.Length

// Check that the error for a named argument/setter that does not exist is located in a good place
let _ = C().M(qez=3)

module ListPositive2 = 
    // In this example, implicit yield is enabled becaue there is no explicit 'yield'
    let v3 =
        [ if true then 1 else 2 ]  

    // In this example, implicit yield is enabled because there is no explicit 'yield'.
    // When using implicit yield, statements are detected via a type-directed rule which tries
    // checks the statement without an expected type and then checks if the type of the resulting
    // expression has type 'unit'.
    let v3a = 
        [ printfn "hello"
          if true then 1 else 2  // implicit yield enabled
        ] 

    // In this example, implicit yield is enabled even though there is an explicit 'yield!'
    // This is because using 'yield!' is the only way to yield anything
    let v3b = 
        [ printfn "hello"
          if true then 1 else 2 
          yield! [] ] 

    // This example checks subsumption is permitted when using implicit yield
    let v3c : obj list = 
        [ printfn "hello"
          if true then 1 else obj() ] 

    // Another check that implicit yield is enabled , even though there is a `yield!`
    let v3d = 
        [ if true then 1 else 2 
          yield! [] ] 

    // Another check that implicit yield is enabled , even though there is a `yield!`
    let v3e = 
        [ yield! [] 
          if true then 1 else 2 
        ] 

    // Another check that implicit yield is enabled
    let v3f = 
        [ if true then 
            printfn "hello"
            1
          else 
            2 ] 

    // Another check that implicit yield is enabled
    let v3g = 
        [ if true then 
            1
          else
            printfn "hello"
            2 ] 

    // Another check that implicit yield is enabled
    let v3h = 
        [ for i in 1 .. 10 do 
             10
             printfn "hello" ] 

    // Another check that implicit yield is enabled
    let v5 =
        [ if true then 1 ] 

    // Another check that implicit yield is enabled
    let v5a = 
        [ printfn "hello";
          if true then 1 ] 

    // Another check that implicit yield is enabled
    let v5b =
        [ if true then 
            printfn "hello"
            1 
        ] 

module ArrayPositive2 = 
    let a3 = 
        [| (if true then 1 else 2) |] // simple single element sequence 

    let a5 = 
        [| if true then 1 |] 

    let l10 = 
        [ printfn "hello"; yield 1; yield 2 ] // expect ok - the printfn has type unit and is not interpreted as a yield
    
    // simple case of explicit yield
    let l12 : int list = 
        [ printfn "hello"
          if true then yield 1 else yield 2 ] 

    // check subsumption is allowed when using explicit yield
    let l13 : obj list = 
        [ printfn "hello"
          if true then yield 1 else yield 2 ] 

    // check subsumption is allowed when using explicit yield
    let l14 : obj list = 
        [ printfn "hello"
          if true then yield 1 ] 

module SeqPositive2 = 
    let s2 = 
        seq { if true then () else () } 

    let s6 = 
        seq { if true then () } 
    
    let s4 = 
        seq { if true then 1 else 2 }

    let s6 = 
        seq { if true then 1  }
    
    let s7 = 
        seq { match 1 with 1 -> 4 | 2 -> 5 | 3 -> 6 | _ -> ()  }
    
module BuilderPositive2 = 
    type L<'T> = { Make: (unit -> 'T list) }
    let L f = { Make = f }

    type ListBuilder() = 
        member __.Combine(x1: L<'T>, x2: L<'T>) = L(fun () -> x1.Make() @ x2.Make())
        member __.Delay(f: unit -> L<'T>) = L(fun () -> f().Make())
        member __.Zero() = L(fun () -> [])
        member __.Yield(a: 'T) = L(fun () -> [a])
        member __.YieldFrom(x: L<'T>) = x

    let list = ListBuilder()
    let empty<'T> : L<'T> = list.Zero()

    let v3 =
        list { if true then 1 else 2 }  // implicit yield enabled

    let v3y =
        list { if true then yield 1 else yield 2 }  // equivalent explicit yield

    let v3a = 
        list {
          printfn "hello"
          if true then 1 else 2  // implicit yield enabled
        }

    let v3ay = 
        list {
          printfn "hello"
          if true then yield 1 else yield 2  // equivalent explicit yield 
        }

    let v3b = 
        list {
          printfn "hello"
          if true then 1 else 2 // implicit yield, even though there is a `yield!`
          yield! empty
        }

    let v3bc = 
        list {
          printfn "hello"
          if true then yield 1 else yield 2 // equivalent explicit yield
          yield! empty
        }













    let v3d = 
        list {
          if true then 1 else 2 // implicit yield enabled , even though there is a `yield!`
          yield! empty
        }

    let v3dy = 
        list {
          if true then yield 1 else yield 2 // equivalent explicit yield
          yield! empty
        }

    let v3e = 
        list {
          yield! empty
          if true then 1 else 2 // implicit yield enabled , even though there is a `yield!`
        }

    let v3f = 
        list { 
          if true then 
            printfn "hello"
            1
          else 2 
        }

    let v3g = 
        list {
          if true then 
            1
          else
            printfn "hello"
            2 
        }

    let v5 =
        list {
          if true then 1 
        }

    let v5a = 
        list {
          printfn "hello";
          if true then 1 
        }

    let v5b =
        list {
          if true then 
            printfn "hello"
            1 
        }

module ListNegative2 = 
    let v4 = [ if true then 1 else yield 2 ] // expect warning about "1" being ignored. There is a 'yield' so statements are statements.
    let l11 = [ 4; yield 1; yield 2 ] // expect warning about "1" being ignored. There is a 'yield' so statements are statements.
    let l9 = [ printfn "hello"; 1; 2 ] // Note, this is interpreted as a "SimpleSemicolonSequence", so we get "All elements of a list must be implicitly convertible to the type of the first element, which here is 'unit'. This element..."
    let v3a : unit list = 
        [ printfn "hello"
          if true then 1 else 2 ] 

module ArrayNegative2 = 
    let a4 = [| if true then 1 else yield 2 |] // expect warning about "1" being ignored. There is a 'yield' so statements are statements.
    let a4 = [| (if true then 1) |] 

module SeqNegative2 = 
    let s5 = seq { if true then 1 else yield 2 } // expect warning about "1" being ignored. There is a 'yield' so statements are statements.
    let s8 = seq { match 1 with 1 -> 4 | 2 -> 5 | 3 -> yield 6 | _ -> ()  } // expect warning about "4" being ignored. There is a 'yield' so statements are statements.

module BuilderNegative2 = 
    type L<'T> = { Make: (unit -> 'T list) }
    let L f = { Make = f }

    type ListBuilder() = 
        member __.Combine(x1: L<'T>, x2: L<'T>) = L(fun () -> x1.Make() @ x2.Make())
        member __.Delay(f: unit -> L<'T>) = L(fun () -> f().Make())
        member __.Zero() = L(fun () -> [])
        member __.Yield(a: 'T) = L(fun () -> [a])
        member __.YieldFrom(x: L<'T>) = x

    let list = ListBuilder()

    let v3c : L<obj>  = 
        list {
          printfn "hello"
          if true then 1 else obj() 
        } 

    let v3c : L<obj>  = 
        list {
          printfn "hello"
          if true then yield 1 else obj() 
        } 

