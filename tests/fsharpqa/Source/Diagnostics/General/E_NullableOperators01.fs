// #Regression #Diagnostics #Nullable #Operators
// Trying to use a nullable operator without opening Microsoft.FSharp.Linq.NullableOperators should result in a helpful error message
// See DevDiv:190800
//open Microsoft.FSharp.Linq.NullableOperators
let iq = System.Nullable<int>(10)

let _ = iq ?>= 10
let _ = iq ?> iq
let _ = iq ?<= iq
let _ = iq ?< iq
let _ = iq ?= iq
let _ = iq ?<> iq

let _ = iq ?>=? iq
let _ = iq ?>? iq
let _ = iq ?<=? iq
let _ = iq ?<? iq
let _ = iq ?=? iq
let _ = iq ?<>? iq

let _ = iq >=? iq
let _ = iq >? iq
let _ = iq <=? iq
let _ = iq <? iq
let _ = iq =? iq
let _ = iq <>? iq

//<Expects status="error" span="(7,12-7,15)" id="FS0043">The types 'System\.Nullable<int>, int' do not support the operator '\?>='\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(8,12-8,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?>'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(9,12-9,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<='\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(10,12-10,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(11,12-11,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?='\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(12,12-12,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<>'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(14,12-14,16)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?>=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(15,12-15,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?>\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(16,12-16,16)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(17,12-17,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(18,12-18,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(19,12-19,16)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '\?<>\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(21,12-21,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '>=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(22,12-22,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '>\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(23,12-23,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '<=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(24,12-24,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '<\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(25,12-25,14)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '=\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
//<Expects status="error" span="(26,12-26,15)" id="FS0043">The type 'System\.Nullable<int>' does not support the operator '<>\?'\. Consider opening the module 'Microsoft\.FSharp\.Linq\.NullableOperators'\.$</Expects>
