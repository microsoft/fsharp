// #Regression #Conformance #LexicalAnalysis 
// Regression test for FSharp1.0#1076
// Usage of >> and .
// 1 space >> and .
//<Expects status="success"></Expects>

#light

type ID<'T> =
    static member id (x:'T) = x

let f _x = ID<ID<int>> .id  (* ok *)
