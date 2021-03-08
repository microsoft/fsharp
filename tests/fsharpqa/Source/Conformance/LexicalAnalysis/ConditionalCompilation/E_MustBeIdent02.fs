// #Regression #Conformance #LexicalAnalysis
// Regression test for FSHARP1.0:1419
//<Expects id="FS0039" span="(6,14-6,17)" status="error">The type 'if_' is not defined.</Expects>
//<Expects id="FS0039" span="(7,14-7,20)" status="error">The type 'endif_' is not defined.</Expects>
#light
let t8 (x : #if_) = ()
let t7 (x : #endif_) = ()

exit 1