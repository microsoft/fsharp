// #Regression #Conformance #TypeConstraints 
// Regression test for FSHARP1.0:1419
// Tokens beginning with # should not match greedily with directives
//<Expects id="FS0001" span="(14,13)" status="error">The type 'float' is not compatible with the type 'light_'</Expects>
#light

type light_() = class
                end

let t = new light_()

let t5 (x : #light_) = x

let r1 = t5 1.0
let r2 = t5 t

exit 0
