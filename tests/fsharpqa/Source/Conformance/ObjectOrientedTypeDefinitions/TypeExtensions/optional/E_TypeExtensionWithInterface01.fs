// #Conformance #ObjectOrientedTypes #TypeExtensions #Regression
// Regression for 909525, previously there was no error here
// <Expects status="error" id="FS0090" span="(5,15-5,33)">Interface implementations should be given on the initial declaration of a type.</Expects>
type System.Random with
    interface System.IComparable 
    static member Factory() = 1

exit 1
