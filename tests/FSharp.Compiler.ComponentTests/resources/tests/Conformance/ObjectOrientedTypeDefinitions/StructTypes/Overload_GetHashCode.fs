// #Regression #Conformance #ObjectOrientedTypes #Structs #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of GetHashCode()

[<Struct>]
type S2 =
    member this.GetHashCode(_s:char) =  1
    //member this.GetHashCode() = 1.
    member this.GetHashCode(?_s:char) = 1 

#if INTERACTIVE
;;
exit 0;;
#endif
