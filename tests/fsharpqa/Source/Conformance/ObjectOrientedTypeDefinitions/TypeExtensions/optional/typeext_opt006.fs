// #Regression #Conformance #ObjectOrientedTypes #TypeExtensions  
//<Expects id="FS0090" span="(7,17-7,19)" status="error">Interface implementations should be given on the initial declaration of a type.</Expects>

namespace NS
  module M = 
    type Lib with
      interface IM 
          with member x.M i = i
  
  module F = exit 0



