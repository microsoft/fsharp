// #Regression #Conformance #ObjectOrientedTypes #Structs #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of Equals()

[<Struct>]
type S3 =
    member this.Equals(_s:char) = true
    //member this.Equals(s:S3) = 1.
    member this.Equals(?_s:char) = true

#if INTERACTIVE
;;
exit 0;;
#endif
