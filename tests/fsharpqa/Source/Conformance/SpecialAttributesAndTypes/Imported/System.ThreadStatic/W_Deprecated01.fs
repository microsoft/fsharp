// #Regression #Conformance #Attributes 
// Regression test for FSHARP1.0:4226
// We want to make sure the warning emits the correct suggestion (val and mutable were swapped)
//<Expects status="errors" span="(7,5-8,10)" id="FS0056">Thread static and context static 'let' bindings are deprecated\. Instead use a declaration of the form 'static val mutable <ident> : <type>' in a class\. Add the 'DefaultValue' attribute to this declaration to indicate that the value is initialized to the default value on each new thread\.$</Expects>
module M
module Foo =
    [<System.ThreadStatic>]
    let x = 42          // warning
