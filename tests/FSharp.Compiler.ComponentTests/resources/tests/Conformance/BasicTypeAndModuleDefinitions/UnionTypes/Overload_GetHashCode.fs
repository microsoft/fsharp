// #Regression #Conformance #TypesAndModules #Unions #ReqNOMT 
// Regression test for FSHARP1.0:5223
// Overloading of GetHashCode()

type DU = | A
            member this.GetHashCode(_s:char) =  1
            //member this.GetHashCode() = 1.
            member this.GetHashCode(?_s:char) = 1 

#if INTERACTIVE
exit 0;;
#endif
