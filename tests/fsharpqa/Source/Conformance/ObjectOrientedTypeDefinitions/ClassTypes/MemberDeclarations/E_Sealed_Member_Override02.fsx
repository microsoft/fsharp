// #Regression #Conformance #ObjectOrientedTypes #Classes #Inheritance 
// Regression test DevDiv:370485 ([Portable] Cannot override sealed method)

namespace N

type T1() =
    inherit CSLib.B1()
    //override x.M(o : obj) = 12
    override x.M(i : int) = 2     // ERROR {expected}

// Same as above, just use a hierarchy 3 levels deep
type T2() =
    inherit CSLib2.B2()
    //override x.M(o : obj) = 12
    override x.M(i : int) = 2     // ERROR {expected}

type T3() =
    inherit CSLib4.B1()
    override x.M(i : int) = 2     // ERROR {expected}

type T4() =
    inherit CSLib5.B1()
    override x.M(i : int) = 2     // ERROR {expected}

//<Expects status="error" span="(9,16-9,17)" id="FS3070">Cannot override inherited member 'CSLib.B1::M' because it is sealed$</Expects>
//<Expects status="error" span="(15,16-15,17)" id="FS3070">Cannot override inherited member 'CSLib2.B1::M' because it is sealed$</Expects>
//<Expects status="error" span="(19,16-19,17)" id="FS3070">Cannot override inherited member 'CSLib4.B1::M' because it is sealed$</Expects>
//<Expects status="error" span="(21,6-21,8)" id="FS0365">No implementation was given for those members:</Expects>
//<Expects span="(21,6-21,8)">	'CSLib5\.B0\.M\(c: char, a: int\) : int'</Expects>
//<Expects span="(21,6-21,8)">	'CSLib5\.B0\.N\(c: char, a: int\) : int'</Expects>
//<Expects status="error" span="(21,6-21,8)" id="FS0054">This type is 'abstract' since some abstract members have not been given an implementation\. If this is intentional then add the '\[<AbstractClass>\]' attribute to your type\.$</Expects>
//<Expects status="error" span="(23,16-23,17)" id="FS3070">Cannot override inherited member 'CSLib5.B1::M' because it is sealed$</Expects>