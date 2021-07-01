// #Regression #NoMT #Import 
// Dev11 Bug 90642
//<Expects status="error" span="(8,19)" id="FS3070">Cannot override inherited member 'ClassLibrary1.Class2::F' because it is sealed$</Expects>

type MyClass() =
    inherit ClassLibrary1.Class2()
 
    override this.F() = 
        printfn "aha!"
 
let x = new MyClass() 
let y = x :> ClassLibrary1.Class1 
y.F()
