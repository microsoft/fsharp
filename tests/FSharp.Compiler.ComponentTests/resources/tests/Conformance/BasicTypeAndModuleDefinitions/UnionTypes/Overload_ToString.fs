// #Regression #Conformance #TypesAndModules #Unions #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of ToString()

type DU = | A
          member this.ToString(_s:char) = true
          member this.ToString() =       true
          member this.ToString(?_s:char) = true

#if INTERACTIVE
exit 0;;
#endif
