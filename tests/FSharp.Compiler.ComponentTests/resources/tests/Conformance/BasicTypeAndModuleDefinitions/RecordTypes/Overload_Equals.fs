// #Regression #Conformance #TypesAndModules #Records #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of Equals()

type R =
    { x: int }
    member this.Equals(_:char) = true
    // member this.Equals(s:R) = 1.
    member this.Equals(?_s:char) = true

#if INTERACTIVE
exit 0;;
#endif
