// #Regression #Conformance #TypesAndModules #Unions #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of Equals()

type DU = | A
          member this.Equals(_s:char) = true
          //member this.Equals(s:DU) = 1.
          member this.Equals(?_s:char) = true

#if INTERACTIVE
exit 0;;
#endif
