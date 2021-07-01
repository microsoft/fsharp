// #Regression #Conformance #ObjectOrientedTypes #Classes #Inheritance 
// Regression test DevDiv:370485 ([Portable] Cannot override sealed method)

namespace N

type T1() =
    inherit CSLib.B1()
    override x.M(o : obj) = 12
    override x.M(i : int) = 2     // ERROR {expected}

// Same as above, just use a hierarchy 3 levels deep
type T2() =
    inherit CSLib2.B2()
    override x.M(o : obj) = 12
    override x.M(i : int) = 2     // ERROR {expected}

//<Expects status="error" span="(9,16-9,17)" id="FS3070">Cannot override inherited member 'CSLib.B1::M' because it is sealed$</Expects>
//<Expects status="error" span="(15,16-15,17)" id="FS3070">Cannot override inherited member 'CSLib2.B1::M' because it is sealed$</Expects>