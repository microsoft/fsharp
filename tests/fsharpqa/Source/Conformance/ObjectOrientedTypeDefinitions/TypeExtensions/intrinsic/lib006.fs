// #Regression #Conformance #ObjectOrientedTypes #TypeExtensions 
//<Expects id="FS0090" span="(7,17-7,19)" status="error">Interface implementations should be given on the initial declaration of a type.</Expects>

namespace NS
    type IM = 
      interface
        abstract M : int -> int
      end
      
    type Lib() =
      class
          member x.Prop = 1
     end
 
