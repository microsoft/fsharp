// #Regression #Conformance #DeclarationElements #Attributes 
// Tests to ensure that you can't use StructLayout inappropriately
// Regression tests for FSHARP1.0:5931
//<Expects status="error" span="(12,1-13,1)" id="FS1206">The type 'SExplicitBroken' has been marked as having an Explicit layout, but the field 'v2' has not been marked with the 'FieldOffset' attribute$</Expects>
//<Expects status="error" span="(22,1-23,1)" id="FS1211">The FieldOffset attribute can only be placed on members of types marked with the StructLayout\(LayoutKind\.Explicit\)$</Expects>

module M

open System.Runtime.InteropServices

// Explicit layout without a field offset - will not build
[<Struct>]
[<StructLayout(LayoutKind.Explicit)>]
type SExplicitBroken =
    [<DefaultValue>]
    [<FieldOffset(0)>]
    val v1 : int
    [<DefaultValue>]
    val v2 : int    
    
// Sequential layout with a field offset - will not build
[<Struct>]
[<StructLayout(LayoutKind.Sequential)>]
type SSequentialBroken =
    [<DefaultValue>]
    val v1 : int
    [<DefaultValue>]
    [<FieldOffset(0)>]
    val v2 : int
