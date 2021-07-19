
namespace Tests

open System

#if !PREVIEW

[<AttributeUsage (AttributeTargets.Parameter,AllowMultiple=false)>]  
[<Sealed>]
type InlineIfLambdaAttribute() = 
    inherit Attribute()

#endif

