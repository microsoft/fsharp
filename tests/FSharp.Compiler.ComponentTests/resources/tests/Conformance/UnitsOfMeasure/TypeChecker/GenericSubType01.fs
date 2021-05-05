// #Regression #Conformance #UnitsOfMeasure #TypeInference #TypeConstraints 
// Regression test for FSHARP1.0:6167
// can solve T<x> :> S<'a> when generic in a type parameter but not when generic in a measure type parameter
[<Measure>] type kg

type SC< [<Measure>] 'u>() = class end
type TC< [<Measure>] 'u>() = inherit SC<'u>()
let wff (_x:SC<'u>) = ()


let wgg (x:TC<'v>) = wff x      // OK

let whh (x:TC<kg>) = wff x      // OK


