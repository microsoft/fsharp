// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

#nowarn "25" // Incomplete match expressions 
#nowarn "35" // This construct is deprecated: the treatment of this operator is now handled directly by the F# compiler and its meaning may not be redefined.
#nowarn "44" // This construct is deprecated. This function is for use by compiled F# code and should not be used directly
#nowarn "52" // The value has been copied to ensure the original is not mutated by this operation
#nowarn "60" // Override implementations in augmentations are now deprecated. Override implementations should be given as part of the initial declaration of a type.
#nowarn "61" // The containing type can use 'null' as a representation value for its nullary union case. This member will be compiled as a static member.
#nowarn "69" // Interface implementations in augmentations are now deprecated. Interface implementations should be given on the initial declaration...
#nowarn "77" // Member constraints with the name 'Exp' are given special status by the F# compiler...
#nowarn "3218" // mismatch of parameter name for 'fst' and 'snd'

namespace Microsoft.FSharp.Core

    open System
    open System.Collections
    open System.Collections.Generic
    open System.Diagnostics
    open System.Globalization
    open System.Reflection
    open System.Text
    
    type Unit() =
        override x.GetHashCode() = 0
        override x.Equals(obj:obj) = 
            match obj with null -> true | :? Unit -> true | _ -> false
        interface System.IComparable with 
            member x.CompareTo(_obj:obj) = 0
        
    and unit = Unit

    type SourceConstructFlags = 
       | None = 0
       | SumType = 1
       | RecordType = 2
       | ObjectType = 3
       | Field = 4
       | Exception = 5
       | Closure = 6
       | Module = 7
       | UnionCase = 8
       | Value = 9
       | KindMask = 31
       | NonPublicRepresentation = 32

    [<Flags>]
    type CompilationRepresentationFlags = 
       | None = 0
       | Static = 1
       | Instance = 2      
       /// append 'Module' to the end of a non-unique module
       | ModuleSuffix = 4  
       | UseNullAsTrueValue = 8
       | Event = 16

    [<AttributeUsage(AttributeTargets.Class, AllowMultiple=false)>]
    type SealedAttribute(value:bool) =
        inherit System.Attribute()
        member x.Value = value
        new() = new SealedAttribute(true)
      
    [<AttributeUsage(AttributeTargets.Class, AllowMultiple=false)>]
    [<Sealed>]
    type AbstractClassAttribute() =
        inherit System.Attribute()
      
    [<AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple=false)>]
    [<Sealed>]
    type EqualityConditionalOnAttribute() =
        inherit System.Attribute()
      
    [<AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple=false)>]
    [<Sealed>]
    type ComparisonConditionalOnAttribute() =
        inherit System.Attribute()
      
    [<AttributeUsage(AttributeTargets.Class, AllowMultiple=false)>]
    [<Sealed>]
    type AllowNullLiteralAttribute(value: bool) =
        inherit System.Attribute()
        member x.Value = value
        new () = new AllowNullLiteralAttribute(true)
      
    [<AttributeUsage(AttributeTargets.Field, AllowMultiple=false)>]
    [<Sealed>]
    type VolatileFieldAttribute() =
        inherit System.Attribute()
      
    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type DefaultAugmentationAttribute(value:bool) = 
        inherit System.Attribute()
        member x.Value = value

    [<AttributeUsage (AttributeTargets.Property, AllowMultiple=false)>]  
    [<Sealed>]
    type CLIEventAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type CLIMutableAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type AutoSerializableAttribute(value:bool) = 
        inherit System.Attribute()
        member x.Value = value

    [<AttributeUsage (AttributeTargets.Field ||| AttributeTargets.Method, AllowMultiple=false)>]  
    [<Sealed>]
    type DefaultValueAttribute(check:bool) = 
        inherit System.Attribute()
        member x.Check = check
        new() = new DefaultValueAttribute(true)

    [<AttributeUsage (AttributeTargets.Method, AllowMultiple=false)>]  
    [<Sealed>]
    type EntryPointAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type ReferenceEqualityAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type StructuralComparisonAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type StructuralEqualityAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Interface |||
                      AttributeTargets.Delegate ||| AttributeTargets.Struct |||
                      AttributeTargets.Enum, AllowMultiple=false)>]  
    [<Sealed>]
    type NoEqualityAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple=false)>]  
    [<Sealed>]
    type CustomEqualityAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple=false)>]  
    [<Sealed>]
    type CustomComparisonAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Interface |||
                      AttributeTargets.Delegate ||| AttributeTargets.Struct |||
                      AttributeTargets.Enum, AllowMultiple=false)>]  
    [<Sealed>]
    type NoComparisonAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Parameter |||
                      AttributeTargets.Method ||| AttributeTargets.Property |||
                      AttributeTargets.Constructor, AllowMultiple=false)>]  
    [<Sealed>]
    type ReflectedDefinitionAttribute(includeValue: bool) =
        inherit System.Attribute()
        new() = ReflectedDefinitionAttribute(false)
        member x.IncludeValue = includeValue

    [<AttributeUsage (AttributeTargets.Method ||| AttributeTargets.Class |||
                      AttributeTargets.Field ||| AttributeTargets.Interface |||
                      AttributeTargets.Struct ||| AttributeTargets.Delegate |||
                      AttributeTargets.Enum ||| AttributeTargets.Property, AllowMultiple=false)>]  
    [<Sealed>]
    type CompiledNameAttribute(compiledName:string) =
        inherit System.Attribute()
        member x.CompiledName = compiledName

    [<AttributeUsage (AttributeTargets.Struct ||| AttributeTargets.ReturnValue, AllowMultiple=false)>]  
    [<Sealed>]
    type StructAttribute() =
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.GenericParameter ||| AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type MeasureAttribute() =
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type MeasureAnnotatedAbbreviationAttribute() =
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Interface, AllowMultiple=false)>]  
    [<Sealed>]
    type InterfaceAttribute() =
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class, AllowMultiple=false)>]  
    [<Sealed>]
    type ClassAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Field, AllowMultiple=false)>]
    [<Sealed>]
    type LiteralAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Assembly, AllowMultiple=false)>]
    [<Sealed>]
    type FSharpInterfaceDataVersionAttribute(major:int,minor:int,release:int)  =
        inherit System.Attribute()
        member x.Major = major
        member x.Minor = minor
        member x.Release = release

    [<AttributeUsage(AttributeTargets.All, AllowMultiple=false)>]
    [<Sealed>]
    type CompilationMappingAttribute(sourceConstructFlags:SourceConstructFlags,
                                     variantNumber:int,
                                     sequenceNumber:int,
                                     resourceName:string,
                                     typeDefinitions:System.Type[])  =
        inherit System.Attribute()
        member x.SourceConstructFlags = sourceConstructFlags
        member x.SequenceNumber = sequenceNumber
        member x.VariantNumber = variantNumber
        new(sourceConstructFlags) = CompilationMappingAttribute(sourceConstructFlags,0,0)
        new(sourceConstructFlags,sequenceNumber) = CompilationMappingAttribute(sourceConstructFlags,0,sequenceNumber)
        new(sourceConstructFlags,variantNumber,sequenceNumber) = CompilationMappingAttribute(sourceConstructFlags,variantNumber,sequenceNumber,null,null)
        new(resourceName, typeDefinitions) = CompilationMappingAttribute(SourceConstructFlags.None,0,0,resourceName, typeDefinitions)
        member x.TypeDefinitions = typeDefinitions
        member x.ResourceName = resourceName

    [<AttributeUsage(AttributeTargets.All, AllowMultiple=false)>]
    [<Sealed>]
    type CompilationSourceNameAttribute(sourceName:string)  =
        inherit System.Attribute()
        member x.SourceName = sourceName

    //-------------------------------------------------------------------------
    [<AttributeUsage(AttributeTargets.All, AllowMultiple=false)>]
    [<Sealed>]
    type CompilationRepresentationAttribute (flags : CompilationRepresentationFlags) =
        inherit System.Attribute()
        member x.Flags = flags


    module internal ExperimentalAttributeMessages =
        [<Literal>]
        let RequiresPreview : string = "Experimental library feature, requires '--langversion:preview'"

        [<Literal>]
        let NotSupportedYet : string = "This construct is not supported by your version of the F# compiler"

    [<AttributeUsage(AttributeTargets.All, AllowMultiple=false)>]
    [<Sealed>]
    type ExperimentalAttribute(message:string) =
        inherit System.Attribute()

        member x.Message = message

    [<AttributeUsage (AttributeTargets.Parameter,AllowMultiple=false)>]  
    [<Sealed>]
    type InlineIfLambdaAttribute() = 
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
    [<Sealed>]
    type CompilationArgumentCountsAttribute(counts:int[]) =
        inherit System.Attribute()
        member x.Counts = 
           let unboxPrim(x:obj) = (# "unbox.any !0" type ('T) x : 'T #)
           (unboxPrim(counts.Clone()) : System.Collections.Generic.IEnumerable<int>)

    [<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
    [<Sealed>]
    type CustomOperationAttribute(name:string) =
        inherit System.Attribute()
        let mutable isBinary = false
        let mutable allowInto = false
        let mutable isJoin = false
        let mutable isGroupJoin = false
        let mutable maintainsVarSpace = false
        let mutable maintainsVarSpaceWithBind = false
        let mutable joinOnWord = ""
        member x.Name = name
        member x.AllowIntoPattern with get() = allowInto and set v = allowInto <- v
        member x.IsLikeZip with get() = isBinary and set v = isBinary <- v
        member x.IsLikeJoin with get() = isJoin and set v = isJoin <- v
        member x.IsLikeGroupJoin with get() = isGroupJoin and set v = isGroupJoin <- v
        member x.JoinConditionWord with get() = joinOnWord and set v = joinOnWord <- v

        member x.MaintainsVariableSpace with get() = maintainsVarSpace and set v = maintainsVarSpace <- v
        member x.MaintainsVariableSpaceUsingBind with get() = maintainsVarSpaceWithBind and set v = maintainsVarSpaceWithBind <- v

    [<AttributeUsage(AttributeTargets.Parameter, AllowMultiple=false)>]
    [<Sealed>]
    type ProjectionParameterAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Interface ||| AttributeTargets.Struct ||| AttributeTargets.Delegate ||| AttributeTargets.Enum, AllowMultiple=false)>]
    [<Sealed>]
    type StructuredFormatDisplayAttribute(value:string) =
        inherit System.Attribute()
        member x.Value = value

    [<AttributeUsage(AttributeTargets.All, AllowMultiple=false)>]
    [<Sealed>]
    type CompilerMessageAttribute(message:string, messageNumber : int) =
        inherit System.Attribute()
        let mutable isError = false
        let mutable isHidden = false
        member x.Message = message
        member x.MessageNumber = messageNumber
        member x.IsError with get() = isError and set v = isError <- v
        member x.IsHidden with get() = isHidden and set v = isHidden <- v

    [<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple=false)>]
    [<Sealed>]
    type UnverifiableAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple=false)>]
    [<Sealed>]
    type NoDynamicInvocationAttribute(isLegacy: bool) =

        inherit System.Attribute()

        new () = NoDynamicInvocationAttribute(false)

        member x.IsLegacy = isLegacy

    [<AttributeUsage(AttributeTargets.Parameter,AllowMultiple=false)>]
    [<Sealed>]
    type OptionalArgumentAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
    [<Sealed>]
    type GeneralizableValueAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
    [<Sealed>]
    type RequiresExplicitTypeArgumentsAttribute() =
        inherit System.Attribute()
      
    [<AttributeUsage(AttributeTargets.Class, AllowMultiple=false)>]
    [<Sealed>]
    type RequireQualifiedAccessAttribute() =
        inherit System.Attribute()

    [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Assembly, AllowMultiple=true)>]  
    [<Sealed>]
    type AutoOpenAttribute(path:string) =
        inherit System.Attribute()
        member x.Path = path
        new() =  AutoOpenAttribute("")

    /// This Attribute is used to make Value bindings like
    ///      let x = some code
    /// operate like static properties.
    [<AttributeUsage(AttributeTargets.Property, AllowMultiple=false)>]
    [<Sealed>]
    type ValueAsStaticPropertyAttribute() =
        inherit System.Attribute()

    [<MeasureAnnotatedAbbreviation>] type float<[<Measure>] 'Measure> = float 
    [<MeasureAnnotatedAbbreviation>] type float32<[<Measure>] 'Measure> = float32
    [<MeasureAnnotatedAbbreviation>] type decimal<[<Measure>] 'Measure> = decimal
    [<MeasureAnnotatedAbbreviation>] type int<[<Measure>] 'Measure> = int
    [<MeasureAnnotatedAbbreviation>] type sbyte<[<Measure>] 'Measure> = sbyte
    [<MeasureAnnotatedAbbreviation>] type int16<[<Measure>] 'Measure> = int16
    [<MeasureAnnotatedAbbreviation>] type int64<[<Measure>] 'Measure> = int64
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type nativeint<[<Measure>] 'Measure> = nativeint
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type uint<[<Measure>] 'Measure> = uint
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type byte<[<Measure>] 'Measure> = byte
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type uint16<[<Measure>] 'Measure> = uint16
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type uint64<[<Measure>] 'Measure> = uint64
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
    [<MeasureAnnotatedAbbreviation>] 
    type unativeint<[<Measure>] 'Measure> = unativeint
    
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type double<[<Measure>] 'Measure> = float<'Measure>
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type single<[<Measure>] 'Measure> = float32<'Measure>
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type int8<[<Measure>] 'Measure> = sbyte<'Measure>
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type int32<[<Measure>] 'Measure> = int<'Measure>
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type uint8<[<Measure>] 'Measure> = byte<'Measure>
    [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>] type uint32<[<Measure>] 'Measure> = uint<'Measure>

    /// <summary>Represents a managed pointer in F# code.</summary>
    type byref<'T> = (# "!0&" #)

    /// <summary>Represents a managed pointer in F# code.</summary> 
    type byref<'T, 'Kind> = (# "!0&" #)

    /// Represents the types of byrefs in F# 4.5+
    module ByRefKinds = 

        /// Represents a byref that can be written
        [<Sealed>]
        type Out() = class end

        /// Represents a byref that can be read
        [<Sealed>]
        type In() = class end

        /// Represents a byref that can be both read and written
        [<Sealed>]
        type InOut = class end 

    /// <summary>Represents a in-argument or readonly managed pointer in F# code. This type should only be used with F# 4.5+.</summary>
    type inref<'T> = byref<'T, ByRefKinds.In>

    /// <summary>Represents a out-argument managed pointer in F# code. This type should only be used with F# 4.5+.</summary>
    type outref<'T> = byref<'T, ByRefKinds.Out>

    module internal BasicInlinedOperations =  
        let inline unboxPrim<'T>(x:obj) = (# "unbox.any !0" type ('T) x : 'T #)
        let inline box     (x:'T) = (# "box !0" type ('T) x : obj #)
        let inline convPrim<'T1, 'T2>(x: 'T1) : 'T2 = unboxPrim<'T2> (box x) 
        let inline not     (b:bool) = (# "ceq" b false : bool #)
        let inline (=)     (x:int)   (y:int)    = (# "ceq" x y : bool #) 
        let inline (<>)    (x:int)   (y:int)    = not(# "ceq" x y : bool #)
        let inline (<=)    (x:int)   (y:int)    = not(# "cgt" x y : bool #)
        let inline (>=)    (x:int)   (y:int)    = not(# "clt" x y : bool #)
        let inline (>=.)   (x:int64) (y:int64)  = not(# "clt" x y : bool #)
        let inline (>=...) (x:char)  (y:char)   = not(# "clt" x y : bool #)
        let inline (<=...) (x:char)  (y:char)   = not(# "cgt" x y : bool #)

        let inline (/)     (x:int)    (y:int)    = (# "div" x y : int #)
        let inline (+)     (x:int)    (y:int)    = (# "add" x y : int #)
        let inline (+.)    (x:int64)  (y:int64)  = (# "add" x y : int64 #)
        let inline (+..)   (x:uint64) (y:uint64) = (# "add" x y : uint64 #)
        let inline ( *. )  (x:int64)  (y:int64)  = (# "mul" x y : int64 #)
        let inline ( *.. ) (x:uint64) (y:uint64) = (# "mul" x y : uint64 #)
        let inline (^)     (x:string) (y:string) = System.String.Concat(x,y)
        let inline (<<<)   (x:int)    (y:int)    = (# "shl" x y : int #)
        let inline ( * )   (x:int)    (y:int)    = (# "mul" x y : int #)
        let inline (-)     (x:int)    (y:int)    = (# "sub" x y : int #)
        let inline (-.)    (x:int64)  (y:int64)  = (# "sub" x y : int64 #)
        let inline (-..)   (x:uint64) (y:uint64) = (# "sub" x y : uint64 #)
        let inline (>)     (x:int)    (y:int)    = (# "cgt" x y : bool #)
        let inline (<)     (x:int)    (y:int)    = (# "clt" x y : bool #)
        
        let inline ignore _ = ()
        let inline intOfByte (b:byte) = (# "" b : int #)
        let inline raise (e: System.Exception) = (# "throw" e : 'U #)
        let inline length (x: 'T[]) = (# "ldlen conv.i4" x : int #)
        let inline zeroCreate (n:int) = (# "newarr !0" type ('T) n : 'T[] #)
        let inline get (arr: 'T[]) (n:int) =  (# "ldelem.any !0" type ('T) arr n : 'T #)
        let set (arr: 'T[]) (n:int) (x:'T) =  (# "stelem.any !0" type ('T) arr n x #)


        let inline objEq (xobj:obj) (yobj:obj) = (# "ceq" xobj yobj : bool #)
        let inline int64Eq (x:int64) (y:int64) = (# "ceq" x y : bool #) 
        let inline int32Eq (x:int32) (y:int32) = (# "ceq" x y : bool #) 
        let inline floatEq (x:float) (y:float) = (# "ceq" x y : bool #) 
        let inline float32Eq (x:float32) (y:float32) = (# "ceq" x y : bool #) 
        let inline charEq (x:char) (y:char) = (# "ceq" x y : bool #) 
        let inline intOrder (x:int) (y:int) = if (# "clt" x y : bool #) then (0-1) else (# "cgt" x y : int #)
        let inline int64Order (x:int64) (y:int64) = if (# "clt" x y : bool #) then (0-1) else (# "cgt" x y : int #)
        let inline byteOrder (x:byte) (y:byte) = if (# "clt" x y : bool #) then (0-1) else (# "cgt" x y : int #)
        let inline byteEq (x:byte) (y:byte) = (# "ceq" x y : bool #) 
        let inline int64 (x:int) = (# "conv.i8" x  : int64 #)
        let inline int32 (x:int64) = (# "conv.i4" x  : int32 #)

        let inline typeof<'T> =
            let tok = (# "ldtoken !0" type('T) : System.RuntimeTypeHandle #)
            System.Type.GetTypeFromHandle(tok)

        let inline typedefof<'T> = 
            let ty = typeof<'T>
            if ty.IsGenericType then ty.GetGenericTypeDefinition() else ty
        
        let inline sizeof<'T>  =
            (# "sizeof !0" type('T) : int #) 

        let inline unsafeDefault<'T> : 'T = (# "ilzero !0" type ('T) : 'T #)
        let inline isinstPrim<'T>(x:obj) = (# "isinst !0" type ('T) x : obj #)
        let inline castclassPrim<'T>(x:obj) = (# "castclass !0" type ('T) x : 'T #)
        let inline notnullPrim<'T when 'T : not struct>(x:'T) = (# "ldnull cgt.un" x : bool #)

        let inline iscastPrim<'T when 'T : not struct>(x:obj) = (# "isinst !0" type ('T) x : 'T #)

        let inline mask (n:int) (m:int) = (# "and" n m : int #)

    open BasicInlinedOperations

    
    module TupleUtils =
    
        // adapted from System.Tuple::CombineHashCodes
        let inline opshl (x:int) (n:int) : int =  (# "shl" x (mask n 31) : int #)
        let inline opxor (x:int) (y:int) : int = (# "xor" x y : int32 #)
        let inline combineTupleHashes (h1 : int) (h2 : int) = (opxor ((opshl h1  5) + h1)  h2)

        let combineTupleHashCodes (codes : int []) =
            let mutable (num : int32) = codes.Length - 1
            
            while (num > 1) do
                let mutable i = 0
                while ((i * 2) < (num+1)) do
                    let index = i * 2
                    let num' = index + 1
                    if index = num then
                        set codes i (get codes index)
                        num <- i
                    else
                        set codes i (combineTupleHashes (get codes index) (get codes num))
                        if num' = num then
                            num <- i
                    i <- i + 1
            combineTupleHashes (get codes 0) (get codes 1)


    //-------------------------------------------------------------------------
    // The main aim here is to bootstrap the definition of structural hashing 
    // and comparison.  Calls to these form part of the auto-generated 
    // code for each new datatype.

    module LanguagePrimitives =  

        module (* internal *) ErrorStrings =
            // inline functions cannot call GetString, so we must make these bits public 
            [<ValueAsStaticProperty>]
            let AddressOpNotFirstClassString = SR.GetString(SR.addressOpNotFirstClass)

            [<ValueAsStaticProperty>]
            let NoNegateMinValueString = SR.GetString(SR.noNegateMinValue)

            // needs to be public to be visible from inline function 'average' and others
            [<ValueAsStaticProperty>]
            let InputSequenceEmptyString = SR.GetString(SR.inputSequenceEmpty) 

            // needs to be public to be visible from inline function 'average' and others
            [<ValueAsStaticProperty>]
            let InputArrayEmptyString = SR.GetString(SR.arrayWasEmpty) 

            // needs to be public to be visible from inline function 'average' and others
            [<ValueAsStaticProperty>]
            let InputMustBeNonNegativeString = SR.GetString(SR.inputMustBeNonNegative)
            
        [<CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")>]  // nested module OK              
        module IntrinsicOperators =        
            //-------------------------------------------------------------------------
            // Lazy and/or.  Laziness added by the F# compiler.
            
            let (&) e1 e2 = if e1 then e2 else false
            let (&&) e1 e2 = if e1 then e2 else false
            [<CompiledName("Or")>]
            let (or) e1 e2 = if e1 then true else e2
            let (||) e1 e2 = if e1 then true else e2
            
            //-------------------------------------------------------------------------
            // Address-of
            // Note, "raise<'T> : exn -> 'T" is manually inlined below.
            // Byref usage checks prohibit type instantiations involving byrefs.

            [<NoDynamicInvocation>]
            let inline (~&)  (obj : 'T) : byref<'T>     = 
                ignore obj // pretend the variable is used
                let e = new System.ArgumentException(ErrorStrings.AddressOpNotFirstClassString) 
                (# "throw" (e :> System.Exception) : byref<'T> #)
                 
            [<NoDynamicInvocation>]
            let inline (~&&) (obj : 'T) : nativeptr<'T> = 
                ignore obj // pretend the variable is used
                let e = new System.ArgumentException(ErrorStrings.AddressOpNotFirstClassString) 
                (# "throw" (e :> System.Exception) : nativeptr<'T> #)     

        open IntrinsicOperators
        [<CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")>]  // nested module OK
        module IntrinsicFunctions =        
            
            // Unboxing, type casts, type tests

            type TypeNullnessSemantics = int
            // CLI reference types
            let TypeNullnessSemantics_NullIsExtraValue = 1
            // F# types with [<UseNullAsTrueValue>]
            let TypeNullnessSemantics_NullTrueValue = 2
            // F# record, union, tuple, function types
            let TypeNullnessSemantics_NullNotLiked = 3
            // structs
            let TypeNullnessSemantics_NullNever = 4
            
            // duplicated from above since we're using integers in this section
            let CompilationRepresentationFlags_PermitNull = 8

            let getTypeInfo (ty:Type) =
                if ty.IsValueType 
                then TypeNullnessSemantics_NullNever else
                let mappingAttrs = ty.GetCustomAttributes(typeof<CompilationMappingAttribute>, false)
                if mappingAttrs.Length = 0 
                then TypeNullnessSemantics_NullIsExtraValue
                elif ty.Equals(typeof<unit>) then 
                    TypeNullnessSemantics_NullTrueValue
                elif typeof<Delegate>.IsAssignableFrom(ty) then 
                    TypeNullnessSemantics_NullIsExtraValue
                elif ty.GetCustomAttributes(typeof<AllowNullLiteralAttribute>, false).Length > 0 then
                    TypeNullnessSemantics_NullIsExtraValue
                else
                    let reprAttrs = ty.GetCustomAttributes(typeof<CompilationRepresentationAttribute>, false)
                    if reprAttrs.Length = 0 then 
                        TypeNullnessSemantics_NullNotLiked 
                    else
                        let reprAttr = get reprAttrs 0
                        let reprAttr = (# "unbox.any !0" type (CompilationRepresentationAttribute) reprAttr : CompilationRepresentationAttribute #)
                        if (# "and" reprAttr.Flags CompilationRepresentationFlags_PermitNull : int #) = 0
                        then TypeNullnessSemantics_NullNotLiked
                        else TypeNullnessSemantics_NullTrueValue

             
            type TypeInfo<'T>() = 
               // Compute an on-demand per-instantiation static field
               static let info = getTypeInfo typeof<'T>

               // Publish the results of that computation
               static member TypeInfo = info
                         

            // Note: cheap nullness test for generic value:
            //  IL_0000:  ldarg.1
            //  IL_0001:  box        !TKey
            //  IL_0006:  brtrue.s   IL_000e

            // worst case: nothing known about source or destination
            let UnboxGeneric<'T>(source: obj) = 
                if notnullPrim(source) or TypeInfo<'T>.TypeInfo <> TypeNullnessSemantics_NullNotLiked then 
                    unboxPrim<'T>(source)
                else
                    //System.Console.WriteLine("UnboxGeneric, x = {0}, 'T = {1}", x, typeof<'T>)
                    raise (System.NullReferenceException()) 

            // better: source is NOT TypeNullnessSemantics_NullNotLiked 
            let inline UnboxFast<'T>(source: obj) = 
                // assert not(TypeInfo<'T>.TypeInfo = TypeNullnessSemantics_NullNotLiked)
                unboxPrim<'T>(source)


            // worst case: nothing known about source or destination
            let TypeTestGeneric<'T>(source: obj) = 
                if notnullPrim(isinstPrim<'T>(source)) then true
                elif notnullPrim(source) then false
                else (TypeInfo<'T>.TypeInfo = TypeNullnessSemantics_NullTrueValue)

            // quick entry: source is NOT TypeNullnessSemantics_NullTrueValue 
            let inline TypeTestFast<'T>(source: obj) = 
                //assert not(TypeInfo<'T>.TypeInfo = TypeNullnessSemantics_NullTrueValue)
                notnullPrim(isinstPrim<'T>(source)) 

            let Dispose<'T when 'T :> IDisposable >(resource:'T) = 
                match box resource with 
                | null -> ()
                | _ -> resource.Dispose()

            let FailInit() : unit = raise (InvalidOperationException(SR.GetString(SR.checkInit)))

            let FailStaticInit() : unit = raise (InvalidOperationException(SR.GetString(SR.checkStaticInit)))

            let CheckThis (x : 'T when 'T : not struct) = 
                match box x with 
                | null -> raise (InvalidOperationException(SR.GetString(SR.checkInit)))
                | _ -> x

            let inline MakeDecimal low medium high isNegative scale =  Decimal(low,medium,high,isNegative,scale)

            let inline GetString (source: string) (index:int) =   source.Chars(index)

            let inline CreateInstance<'T when 'T : (new : unit -> 'T) >() = 
                 (System.Activator.CreateInstance() : 'T)

            let inline GetArray (source: 'T array) (index:int) =  (# "ldelem.any !0" type ('T) source index : 'T #)  

            let inline SetArray (target: 'T array) (index:int) (value:'T) =  (# "stelem.any !0" type ('T) target index value #)  

            let inline GetArraySub arr (start:int) (len:int) =
                let len = if len < 0 then 0 else len
                let dst = zeroCreate len   
                for i = 0 to len - 1 do 
                    SetArray dst i (GetArray arr (start + i))
                dst

            let inline SetArraySub arr (start:int) (len:int) (src:_[]) =
                for i = 0 to len - 1 do 
                    SetArray arr (start+i) (GetArray src i)


            let inline GetArray2D (source: 'T[,]) (index1: int) (index2: int) = (# "ldelem.multi 2 !0" type ('T) source index1 index2 : 'T #)  

            let inline SetArray2D (target: 'T[,]) (index1: int) (index2: int) (value: 'T) = (# "stelem.multi 2 !0" type ('T) target index1 index2 value #)  

            let inline GetArray2DLength1 (arr: 'T[,]) =  (# "ldlen.multi 2 0" arr : int #)  
            let inline GetArray2DLength2 (arr: 'T[,]) =  (# "ldlen.multi 2 1" arr : int #)  

            let inline GetArray2DLength (arr: 'T[,]) (dim: int) = 
                match dim with
                | 0 -> GetArray2DLength1 arr
                | 1 -> GetArray2DLength2 arr
                | _ -> raise (System.IndexOutOfRangeException())

            let inline Array2DZeroCreate (n:int) (m:int) = (# "newarr.multi 2 !0" type ('T) n m : 'T[,] #)
            
            let inline GetArray2DSub (src: 'T[,]) src1 src2 len1 len2 =
                let len1 = (if len1 < 0 then 0 else len1)
                let len2 = (if len2 < 0 then 0 else len2)
                let dst = Array2DZeroCreate len1 len2
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        SetArray2D dst i j (GetArray2D src (src1 + i) (src2 + j))
                dst

            let inline SetArray2DSub (dst: 'T[,]) src1 src2 len1 len2 src =
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        SetArray2D dst (src1+i) (src2+j) (GetArray2D src i j)


            let inline GetArray3D (source: 'T[,,]) (index1: int) (index2: int) (index3: int) = 
                (# "ldelem.multi 3 !0" type ('T) source index1 index2 index3 : 'T #)  

            let inline SetArray3D (target: 'T[,,]) (index1: int) (index2: int) (index3: int) (value:'T) = 
                (# "stelem.multi 3 !0" type ('T) target index1 index2 index3 value #)  

            let inline GetArray3DLength1 (arr: 'T[,,]) =  (# "ldlen.multi 3 0" arr : int #)  

            let inline GetArray3DLength2 (arr: 'T[,,]) =  (# "ldlen.multi 3 1" arr : int #)  

            let inline GetArray3DLength3 (arr: 'T[,,]) =  (# "ldlen.multi 3 2" arr : int #)  

            let inline GetArray3DLength (arr: 'T[,,]) (dim: int) = 
                match dim with
                | 0 -> GetArray3DLength1 arr 
                | 1 -> GetArray3DLength2 arr
                | 2 -> GetArray3DLength3 arr
                | _ -> raise (System.IndexOutOfRangeException())

            let inline Array3DZeroCreate (n1:int) (n2:int) (n3:int) = (# "newarr.multi 3 !0" type ('T) n1 n2 n3 : 'T[,,] #)

            let inline GetArray3DSub (src: 'T[,,]) src1 src2 src3 len1 len2 len3 =
                let len1 = (if len1 < 0 then 0 else len1)
                let len2 = (if len2 < 0 then 0 else len2)
                let len3 = (if len3 < 0 then 0 else len3)
                let dst = Array3DZeroCreate len1 len2 len3
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                            SetArray3D dst i j k (GetArray3D src (src1+i) (src2+j) (src3+k))
                dst

            let inline SetArray3DSub (dst: 'T[,,]) src1 src2 src3 len1 len2 len3 src =
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                            SetArray3D dst (src1+i) (src2+j) (src3+k) (GetArray3D src i j k)


            let inline GetArray4D (source: 'T[,,,]) (index1: int) (index2: int) (index3: int) (index4: int) = 
                (# "ldelem.multi 4 !0" type ('T) source index1 index2 index3 index4 : 'T #)  

            let inline SetArray4D (target: 'T[,,,]) (index1: int) (index2: int) (index3: int) (index4: int) (value:'T) = 
                (# "stelem.multi 4 !0" type ('T) target index1 index2 index3 index4 value #)  

            let inline GetArray4DLength1 (arr: 'T[,,,]) =  (# "ldlen.multi 4 0" arr : int #)  

            let inline GetArray4DLength2 (arr: 'T[,,,]) =  (# "ldlen.multi 4 1" arr : int #)  

            let inline GetArray4DLength3 (arr: 'T[,,,]) =  (# "ldlen.multi 4 2" arr : int #)  

            let inline GetArray4DLength4 (arr: 'T[,,,]) =  (# "ldlen.multi 4 3" arr : int #)  

            let inline GetArray4DLength (arr: 'T[,,,]) (dim: int) =
                match dim with
                | 0 -> GetArray4DLength1 arr
                | 1 -> GetArray4DLength2 arr
                | 2 -> GetArray4DLength3 arr
                | 3 -> GetArray4DLength4 arr
                | _ -> raise (System.IndexOutOfRangeException())

            let inline Array4DZeroCreate (n1:int) (n2:int) (n3:int) (n4:int) = (# "newarr.multi 4 !0" type ('T) n1 n2 n3 n4 : 'T[,,,] #)

            let inline GetArray4DSub (src: 'T[,,,]) src1 src2 src3 src4 len1 len2 len3 len4 =
                let len1 = (if len1 < 0 then 0 else len1)
                let len2 = (if len2 < 0 then 0 else len2)
                let len3 = (if len3 < 0 then 0 else len3)
                let len4 = (if len4 < 0 then 0 else len4)
                let dst = Array4DZeroCreate len1 len2 len3 len4
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                          for m = 0 to len4 - 1 do
                              SetArray4D dst i j k m (GetArray4D src (src1+i) (src2+j) (src3+k) (src4+m))
                dst

            let inline SetArray4DSub (dst: 'T[,,,]) src1 src2 src3 src4 len1 len2 len3 len4 src =
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                          for m = 0 to len4 - 1 do
                            SetArray4D dst (src1+i) (src2+j) (src3+k) (src4+m) (GetArray4D src i j k m)

        let inline anyToString nullStr x = 
            match box x with 
            | :? IFormattable as f -> f.ToString(null, CultureInfo.InvariantCulture)
            | null -> nullStr
            | _ ->  x.ToString()

        let anyToStringShowingNull x = anyToString "null" x

        module HashCompare = 
        
            //-------------------------------------------------------------------------
            // LanguagePrimitives.HashCompare: Physical Equality
            //------------------------------------------------------------------------- 

            // NOTE: compiler/optimizer is aware of this function and optimizes calls to it in many situations
            // where it is known that PhysicalEqualityObj is identical to reference comparison
            let PhysicalEqualityIntrinsic (x:'T) (y:'T) : bool when 'T : not struct = 
                objEq (box x) (box y)

            let inline PhysicalEqualityFast (x:'T) (y:'T) : bool when 'T : not struct  = 
                PhysicalEqualityIntrinsic x y
          
            let PhysicalHashIntrinsic (input: 'T) : int when 'T : not struct  = 
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(box input)

            let inline PhysicalHashFast (input: 'T) = 
                PhysicalHashIntrinsic input


            //-------------------------------------------------------------------------
            // LanguagePrimitives.HashCompare: Comparison
            //
            // Bi-modal generic comparison helper implementation.
            //
            // The comparison implementation is run in either Equivalence Relation or Partial 
            // Equivalence Relation (PER) mode which governs what happens when NaNs are compared.
            //
            // Some representations chosen by F# are legitimately allowed to be null, e.g. the None value.
            // However, null values don't support the polymorphic virtual comparison operation CompareTo 
            // so the test for nullness must be made on the caller side.
            //------------------------------------------------------------------------- 

            let FailGenericComparison (obj: obj)  = 
                raise (new System.ArgumentException(String.Format(SR.GetString(SR.genericCompareFail1), obj.GetType().ToString())))
            
               
            /// This type has two instances - fsComparerER and fsComparerThrow.
            ///   - fsComparerER  = ER semantics = no throw on NaN comparison = new GenericComparer(false) = GenericComparer = GenericComparison
            ///   - fsComparerPER  = PER semantics = local throw on NaN comparison = new GenericComparer(true) = LessThan/GreaterThan etc.
            type GenericComparer(throwsOnPER:bool) = 
                interface System.Collections.IComparer 
                member  c.ThrowsOnPER = throwsOnPER

            /// The unique exception object that is thrown locally when NaNs are compared in PER mode (by fsComparerPER)
            /// This exception should never be observed by user code.
            let NaNException = new System.Exception()                                                 
                    
            /// Implements generic comparison between two objects. This corresponds to the pseudo-code in the F#
            /// specification.  The treatment of NaNs is governed by "comp".
            let rec GenericCompare (comp:GenericComparer) (xobj:obj,yobj:obj) = 
                  match xobj,yobj with 
                   | null,null -> 0
                   | null,_ -> -1
                   | _,null -> 1

                   // Use Ordinal comparison for strings
                   | (:? string as x),(:? string as y) ->
                       System.String.CompareOrdinal(x, y)

                   // Permit structural comparison on arrays
                   | (:? System.Array as arr1),_ -> 
                       match arr1,yobj with 
                       // Fast path
                       | (:? (obj[]) as arr1), (:? (obj[]) as arr2) ->
                           GenericComparisonObjArrayWithComparer comp arr1 arr2
                       // Fast path
                       | (:? (byte[]) as arr1), (:? (byte[]) as arr2) ->
                           GenericComparisonByteArray arr1 arr2
                       | _, (:? System.Array as arr2) ->
                           GenericComparisonArbArrayWithComparer comp arr1 arr2
                       | _ ->
                           FailGenericComparison xobj

                   // Check for IStructuralComparable
                   | (:? IStructuralComparable as x),_ ->
                       x.CompareTo(yobj,comp)

                   // Check for IComparable
                   | (:? System.IComparable as x),_ -> 
                       if comp.ThrowsOnPER then 
                           match xobj,yobj with 
                           | (:? float as x),(:? float as y) -> 
                                if (System.Double.IsNaN x || System.Double.IsNaN y) then 
                                    raise NaNException
                           | (:? float32 as x),(:? float32 as y) -> 
                                if (System.Single.IsNaN x || System.Single.IsNaN y) then 
                                    raise NaNException
                           | _ -> ()
                       x.CompareTo(yobj)

                   | (:? nativeint as x),(:? nativeint as y) ->
                       if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)

                   | (:? unativeint as x),(:? unativeint as y) ->
                       if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)

                   | _,(:? IStructuralComparable as yc) ->
                       let res = yc.CompareTo(xobj,comp)
                       if res < 0 then 1 elif res > 0 then -1 else 0

                   | _,(:? System.IComparable as yc) -> 
                       // Note -c doesn't work here: be careful of comparison function returning minint
                       let c = yc.CompareTo(xobj)
                       if c < 0 then 1 elif c > 0 then -1 else 0

                   | _ -> FailGenericComparison xobj

            /// specialcase: Core implementation of structural comparison on arbitrary arrays.
            and GenericComparisonArbArrayWithComparer (comp:GenericComparer) (x:System.Array) (y:System.Array) : int  =
                if x.Rank = 1 && y.Rank = 1 then 
                    let lenx = x.LongLength
                    let leny = y.LongLength 
                    let c = int64Order lenx leny 
                    if c <> 0 then c else
                    let basex = int64 (x.GetLowerBound(0))
                    let basey = int64 (y.GetLowerBound(0))
                    let c = int64Order basex basey
                    if c <> 0 then c else
                    let rec check i =
                       if i >=. lenx then 0 else 
                       let c = GenericCompare comp ((x.GetValue(i +. basex)), (y.GetValue(i +. basey)))
                       if c <> 0 then c else check (i +. 1L)
                    check 0L
                elif x.Rank = 2 && y.Rank = 2 then 
                    let lenx0 = x.GetLongLength(0)
                    let leny0 = y.GetLongLength(0)
                    let c = int64Order lenx0 leny0 
                    if c <> 0 then c else
                    let lenx1 = x.GetLongLength(1)
                    let leny1 = y.GetLongLength(1)
                    let c = int64Order lenx1 leny1 
                    if c <> 0 then c else
                    let basex0 = int64 (x.GetLowerBound(0))
                    let basey0 = int64 (y.GetLowerBound(0))
                    let c = int64Order basex0 basey0
                    if c <> 0 then c else
                    let basex1 = int64 (x.GetLowerBound(1))
                    let basey1 = int64 (y.GetLowerBound(1))
                    let c = int64Order basex1 basey1
                    if c <> 0 then c else
                    let rec check0 i =
                       let rec check1 j =
                           if j >=. lenx1 then 0 else
                           let c = GenericCompare comp ((x.GetValue(i +. basex0,j +. basex1)), (y.GetValue(i +. basey0,j +. basey1)))
                           if c <> 0 then c else check1 (j +. 1L)
                       if i >=. lenx0 then 0 else
                       let c = check1 0L
                       if c <> 0 then c else
                       check0 (i +. 1L)
                    check0 0L
                else
                    let c = intOrder x.Rank y.Rank
                    if c <> 0 then c else
                    let ndims = x.Rank
                    // check lengths
                    let rec precheck k =
                        if k >= ndims then 0 else
                        let c = int64Order (x.GetLongLength(k)) (y.GetLongLength(k))
                        if c <> 0 then c else
                        let c = intOrder (x.GetLowerBound(k)) (y.GetLowerBound(k))
                        if c <> 0 then c else
                        precheck (k+1)
                    let c = precheck 0 
                    if c <> 0 then c else
                    let idxs : int64[] = zeroCreate ndims 
                    let rec checkN k baseIdx i lim =
                       if i >=. lim then 0 else
                       set idxs k (baseIdx +. i)
                       let c = 
                           if k = ndims - 1
                           then GenericCompare comp ((x.GetValue(idxs)), (y.GetValue(idxs)))
                           else check (k+1) 
                       if c <> 0 then c else 
                       checkN k baseIdx (i +. 1L) lim
                    and check k =
                       if k >= ndims then 0 else
                       let baseIdx = x.GetLowerBound(k)
                       checkN k (int64 baseIdx) 0L (x.GetLongLength(k))
                    check 0

            /// optimized case: Core implementation of structural comparison on object arrays.
            and GenericComparisonObjArrayWithComparer (comp:GenericComparer) (x:obj[]) (y:obj[]) : int =
                let lenx = x.Length 
                let leny = y.Length 
                let c = intOrder lenx leny 
                if c <> 0 then c 
                else
                    let mutable i = 0
                    let mutable res = 0  
                    while i < lenx do 
                        let c = GenericCompare comp ((get x i), (get y i))
                        if c <> 0 then (res <- c; i <- lenx)
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural comparison on arrays.
            and GenericComparisonByteArray (x:byte[]) (y:byte[]) : int =
                let lenx = x.Length 
                let leny = y.Length 
                let c = intOrder lenx leny 
                if c <> 0 then c 
                else
                    let mutable i = 0 
                    let mutable res = 0 
                    while i < lenx do 
                        let c = byteOrder (get x i) (get y i) 
                        if c <> 0 then (res <- c; i <- lenx) 
                        else i <- i + 1
                    res

            type GenericComparer with
                interface System.Collections.IComparer with
                    override c.Compare(x:obj,y:obj) = GenericCompare c (x,y)

            /// The unique object for comparing values in PER mode (where local exceptions are thrown when NaNs are compared)
            let fsComparerPER        = GenericComparer(true)  

            /// The unique object for comparing values in ER mode (where "0" is returned when NaNs are compared)
            let fsComparerER = GenericComparer(false) 

            /// Compare two values of the same generic type, using "comp".
            //
            // "comp" is assumed to be either fsComparerPER or fsComparerER (and hence 'Compare' is implemented via 'GenericCompare').
            //
            // NOTE: the compiler optimizer is aware of this function and devirtualizes in the 
            // cases where it is known how a particular type implements generic comparison.
            let GenericComparisonWithComparerIntrinsic<'T> (comp:System.Collections.IComparer) (x:'T) (y:'T) : int = 
                comp.Compare(box x, box y)

            /// Compare two values of the same generic type, in either PER or ER mode, but include static optimizations
            /// for various well-known cases.
            //
            // "comp" is assumed to be either fsComparerPER or fsComparerER (and hence 'Compare' is implemented via 'GenericCompare').
            //
            let inline GenericComparisonWithComparerFast<'T> (comp:System.Collections.IComparer) (x:'T) (y:'T) : int = 
                 GenericComparisonWithComparerIntrinsic comp x y
                 when 'T : bool   = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : sbyte  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int16  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int32  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int64  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : nativeint  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : byte   = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint16 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint32 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint64 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : unativeint = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 // Note, these bail out to GenericComparisonWithComparerIntrinsic if called with NaN values, because clt and cgt and ceq all return "false" for that case.
                 when 'T : float  = if   (# "clt" x y : bool #) then (-1)
                                    elif (# "cgt" x y : bool #) then (1)
                                    elif (# "ceq" x y : bool #) then (0)
                                    else GenericComparisonWithComparerIntrinsic comp x y
                 when 'T : float32 = if   (# "clt" x y : bool #) then (-1)
                                     elif (# "cgt" x y : bool #) then (1)
                                     elif (# "ceq" x y : bool #) then (0)
                                     else GenericComparisonWithComparerIntrinsic comp x y
                 when 'T : char   = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : string = 
                     // NOTE: we don't have to null check here because System.String.CompareOrdinal
                     // gives reliable results on null values.
                     System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))
                 when 'T : decimal     = System.Decimal.Compare((# "" x:decimal #), (# "" y:decimal #))
                 when 'T : DateTime = System.DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #))


            /// Generic comparison. Implements ER mode (where "0" is returned when NaNs are compared)
            //
            // The compiler optimizer is aware of this function  (see use of generic_comparison_inner_vref in opt.fs)
            // and devirtualizes calls to it based on "T".
            let GenericComparisonIntrinsic<'T> (x:'T) (y:'T) : int = 
                GenericComparisonWithComparerIntrinsic (fsComparerER :> IComparer) x y


            /// Generic less-than. Uses comparison implementation in PER mode but catches 
            /// the local exception that is thrown when NaN's are compared.
            let GenericLessThanIntrinsic (x:'T) (y:'T) = 
                try
                    (# "clt" (GenericComparisonWithComparerIntrinsic fsComparerPER x y) 0 : bool #)
                with
                    | e when System.Runtime.CompilerServices.RuntimeHelpers.Equals(e, NaNException) -> false
                    
            
            /// Generic greater-than. Uses comparison implementation in PER mode but catches 
            /// the local exception that is thrown when NaN's are compared.
            let GenericGreaterThanIntrinsic (x:'T) (y:'T) = 
                try
                    (# "cgt" (GenericComparisonWithComparerIntrinsic fsComparerPER x y) 0 : bool #)
                with
                    | e when System.Runtime.CompilerServices.RuntimeHelpers.Equals(e, NaNException) -> false
            
             
            /// Generic greater-than-or-equal. Uses comparison implementation in PER mode but catches 
            /// the local exception that is thrown when NaN's are compared.
            let GenericGreaterOrEqualIntrinsic (x:'T) (y:'T) = 
                try
                    (# "cgt" (GenericComparisonWithComparerIntrinsic fsComparerPER x y) (-1) : bool #)
                with
                    | e when System.Runtime.CompilerServices.RuntimeHelpers.Equals(e, NaNException) -> false
                    
            
            
            /// Generic less-than-or-equal. Uses comparison implementation in PER mode but catches 
            /// the local exception that is thrown when NaN's are compared.
            let GenericLessOrEqualIntrinsic (x:'T) (y:'T) = 
                try
                    (# "clt" (GenericComparisonWithComparerIntrinsic fsComparerPER x y) 1 : bool #)
                with
                    | e when System.Runtime.CompilerServices.RuntimeHelpers.Equals(e, NaNException) -> false


            /// Compare two values of the same generic type, in ER mode, with static optimizations 
            /// for known cases.
            let inline GenericComparisonFast<'T> (x:'T) (y:'T) : int = 
                 GenericComparisonIntrinsic x y
                 when 'T : bool   = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : sbyte  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int16  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int32  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : int64  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : nativeint  = if (# "clt" x y : bool #) then (-1) else (# "cgt" x y : int #)
                 when 'T : byte   = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint16 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint32 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : uint64 = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : unativeint = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : float  = if   (# "clt" x y : bool #) then (-1)
                                    elif (# "cgt" x y : bool #) then (1)
                                    elif (# "ceq" x y : bool #) then (0)
                                    elif (# "ceq" y y : bool #) then (-1)
                                    else (# "ceq" x x : int #)
                 when 'T : float32 = if   (# "clt" x y : bool #) then (-1)
                                     elif (# "cgt" x y : bool #) then (1)
                                     elif (# "ceq" x y : bool #) then (0)
                                     elif (# "ceq" y y : bool #) then (-1)
                                     else (# "ceq" x x : int #)
                 when 'T : char   = if (# "clt.un" x y : bool #) then (-1) else (# "cgt.un" x y : int #)
                 when 'T : string = 
                     // NOTE: we don't have to null check here because System.String.CompareOrdinal
                     // gives reliable results on null values.
                     System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))
                 when 'T : decimal     = System.Decimal.Compare((# "" x:decimal #), (# "" y:decimal #))
                 when 'T : DateTime = System.DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #))

            /// Generic less-than with static optimizations for some well-known cases.
            let inline GenericLessThanFast (x:'T) (y:'T) = 
                GenericLessThanIntrinsic x y
                when 'T : bool   = (# "clt" x y : bool #)
                when 'T : sbyte  = (# "clt" x y : bool #)
                when 'T : int16  = (# "clt" x y : bool #)
                when 'T : int32  = (# "clt" x y : bool #)
                when 'T : int64  = (# "clt" x y : bool #)
                when 'T : byte   = (# "clt.un" x y : bool #)
                when 'T : uint16 = (# "clt.un" x y : bool #)
                when 'T : uint32 = (# "clt.un" x y : bool #)
                when 'T : uint64 = (# "clt.un" x y : bool #)
                when 'T : unativeint = (# "clt.un" x y : bool #)
                when 'T : nativeint  = (# "clt" x y : bool #)
                when 'T : float  = (# "clt" x y : bool #) 
                when 'T : float32= (# "clt" x y : bool #) 
                when 'T : char   = (# "clt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_LessThan ((# "" x:decimal #), (# "" y:decimal #))
                when 'T : DateTime = DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #)) < 0
              
            /// Generic greater-than with static optimizations for some well-known cases.
            let inline GenericGreaterThanFast (x:'T) (y:'T) = 
                GenericGreaterThanIntrinsic x y
                when 'T : bool       = (# "cgt" x y : bool #)
                when 'T : sbyte      = (# "cgt" x y : bool #)
                when 'T : int16      = (# "cgt" x y : bool #)
                when 'T : int32      = (# "cgt" x y : bool #)
                when 'T : int64      = (# "cgt" x y : bool #)
                when 'T : nativeint  = (# "cgt" x y : bool #)
                when 'T : byte       = (# "cgt.un" x y : bool #)
                when 'T : uint16     = (# "cgt.un" x y : bool #)
                when 'T : uint32     = (# "cgt.un" x y : bool #)
                when 'T : uint64     = (# "cgt.un" x y : bool #)
                when 'T : unativeint = (# "cgt.un" x y : bool #)
                when 'T : float      = (# "cgt" x y : bool #) 
                when 'T : float32    = (# "cgt" x y : bool #) 
                when 'T : char       = (# "cgt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_GreaterThan ((# "" x:decimal #), (# "" y:decimal #))
                when 'T : DateTime = DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #)) > 0

            /// Generic less-than-or-equal with static optimizations for some well-known cases.
            let inline GenericLessOrEqualFast (x:'T) (y:'T) = 
                GenericLessOrEqualIntrinsic x y
                when 'T : bool       = not (# "cgt" x y : bool #)
                when 'T : sbyte      = not (# "cgt" x y : bool #)
                when 'T : int16      = not (# "cgt" x y : bool #)
                when 'T : int32      = not (# "cgt" x y : bool #)
                when 'T : int64      = not (# "cgt" x y : bool #)
                when 'T : nativeint  = not (# "cgt" x y : bool #)
                when 'T : byte       = not (# "cgt.un" x y : bool #)
                when 'T : uint16     = not (# "cgt.un" x y : bool #)
                when 'T : uint32     = not (# "cgt.un" x y : bool #)
                when 'T : uint64     = not (# "cgt.un" x y : bool #)
                when 'T : unativeint = not (# "cgt.un" x y : bool #)
                when 'T : float      = not (# "cgt.un" x y : bool #) 
                when 'T : float32    = not (# "cgt.un" x y : bool #) 
                when 'T : char       = not(# "cgt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_LessThanOrEqual ((# "" x:decimal #), (# "" y:decimal #))
                when 'T : DateTime = DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #)) <= 0

            /// Generic greater-than-or-equal with static optimizations for some well-known cases.
            let inline GenericGreaterOrEqualFast (x:'T) (y:'T) = 
                GenericGreaterOrEqualIntrinsic x y
                when 'T : bool       = not (# "clt" x y : bool #)
                when 'T : sbyte      = not (# "clt" x y : bool #)
                when 'T : int16      = not (# "clt" x y : bool #)
                when 'T : int32      = not (# "clt" x y : bool #)
                when 'T : int64      = not (# "clt" x y : bool #)
                when 'T : nativeint  = not (# "clt" x y : bool #)
                when 'T : byte       = not (# "clt.un" x y : bool #)
                when 'T : uint16     = not (# "clt.un" x y : bool #)
                when 'T : uint32     = not (# "clt.un" x y : bool #)
                when 'T : uint64     = not (# "clt.un" x y : bool #)
                when 'T : unativeint = not (# "clt.un" x y : bool #)
                when 'T : float      = not (# "clt.un" x y : bool #) 
                when 'T : float32    = not (# "clt.un" x y : bool #)
                when 'T : char       = not (# "clt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_GreaterThanOrEqual ((# "" x:decimal #), (# "" y:decimal #))
                
                when 'T : DateTime = DateTime.Compare((# "" x : DateTime #), (# "" y : DateTime #)) >= 0


            //-------------------------------------------------------------------------
            // LanguagePrimitives.HashCompare: EQUALITY
            //------------------------------------------------------------------------- 


            /// optimized case: Core implementation of structural equality on arrays.
            let GenericEqualityByteArray (x:byte[]) (y:byte[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = byteEq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural equality on arrays.
            let GenericEqualityInt32Array (x:int[]) (y:int[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = int32Eq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural equality on arrays
            let GenericEqualitySingleArray er (x:float32[]) (y:float32[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let f32eq x y = if er && not(float32Eq x x) && not(float32Eq y y) then true else (float32Eq x y)
                let c = (lenx = leny)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = f32eq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural equality on arrays.
            let GenericEqualityDoubleArray er (x:float[]) (y:float[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny)
                let feq x y = if er && not(floatEq x x) && not(floatEq y y) then true else (floatEq x y)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = feq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural equality on arrays.
            let GenericEqualityCharArray (x:char[]) (y:char[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = charEq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res

            /// optimized case: Core implementation of structural equality on arrays.
            let GenericEqualityInt64Array (x:int64[]) (y:int64[]) : bool=
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny)
                if not c then c 
                else
                    let mutable i = 0 
                    let mutable res = true
                    while i < lenx do 
                        let c = int64Eq (get x i) (get y i) 
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res



            /// The core implementation of generic equality between two objects.  This corresponds
            /// to th e pseudo-code in the F# language spec.
            //
            // Run in either PER or ER mode.  In PER mode, equality involving a NaN returns "false".
            // In ER mode, equality on two NaNs returns "true".
            //
            // If "er" is true the "iec" is fsEqualityComparerNoHashingER
            // If "er" is false the "iec" is fsEqualityComparerNoHashingPER
            let rec GenericEqualityObj (er:bool) (iec:System.Collections.IEqualityComparer) ((xobj:obj),(yobj:obj)) : bool = 
                (*if objEq xobj yobj then true else  *)
                match xobj,yobj with 
                 | null,null -> true
                 | null,_ -> false
                 | _,null -> false
                 | (:? string as xs),(:? string as ys) -> System.String.Equals(xs,ys)
                 // Permit structural equality on arrays
                 | (:? System.Array as arr1),_ -> 
                     match arr1,yobj with 
                     // Fast path
                     | (:? (obj[]) as arr1),    (:? (obj[]) as arr2)      -> GenericEqualityObjArray er iec arr1 arr2
                     // Fast path
                     | (:? (byte[]) as arr1),    (:? (byte[]) as arr2)     -> GenericEqualityByteArray arr1 arr2
                     | (:? (int32[]) as arr1),   (:? (int32[]) as arr2)   -> GenericEqualityInt32Array arr1 arr2
                     | (:? (int64[]) as arr1),   (:? (int64[]) as arr2)   -> GenericEqualityInt64Array arr1 arr2
                     | (:? (char[]) as arr1),    (:? (char[]) as arr2)     -> GenericEqualityCharArray arr1 arr2
                     | (:? (float32[]) as arr1), (:? (float32[]) as arr2) -> GenericEqualitySingleArray er arr1 arr2
                     | (:? (float[]) as arr1),   (:? (float[]) as arr2)     -> GenericEqualityDoubleArray er arr1 arr2
                     | _,    (:? System.Array as arr2) -> GenericEqualityArbArray er iec arr1 arr2
                     | _ -> xobj.Equals(yobj)
                 | (:? IStructuralEquatable as x1),_ -> x1.Equals(yobj,iec)
                 // Ensure ER NaN semantics on recursive calls
                 | (:? float as f1), (:? float as f2) ->
                    if er && (not (# "ceq" f1 f1 : bool #)) && (not (# "ceq" f2 f2 : bool #)) then true // NAN with ER semantics
                    else (# "ceq" f1 f2 : bool #) // PER semantics
                 | (:? float32 as f1), (:? float32 as f2) ->
                    if er && (not (# "ceq" f1 f1 : bool #)) && (not (# "ceq" f2 f2 : bool #)) then true // NAN with ER semantics
                    else (# "ceq" f1 f2 : bool #)  // PER semantics
                 | _ -> xobj.Equals(yobj)

            /// specialcase: Core implementation of structural equality on arbitrary arrays.
            and GenericEqualityArbArray er (iec:System.Collections.IEqualityComparer) (x:System.Array) (y:System.Array) : bool =
                if x.Rank = 1 && y.Rank = 1 then 
                    // check lengths 
                    let lenx = x.LongLength
                    let leny = y.LongLength 
                    (int64Eq lenx leny) &&
                    // check contents
                    let basex = int64 (x.GetLowerBound(0))
                    let basey = int64 (y.GetLowerBound(0))
                    (int64Eq basex basey) &&
                    let rec check i = (i >=. lenx) || (GenericEqualityObj er iec ((x.GetValue(basex +. i)),(y.GetValue(basey +. i))) && check (i +. 1L))
                    check 0L
                elif x.Rank = 2 && y.Rank = 2 then 
                    // check lengths 
                    let lenx0 = x.GetLongLength(0)
                    let leny0 = y.GetLongLength(0)
                    (int64Eq lenx0 leny0) && 
                    let lenx1 = x.GetLongLength(1)
                    let leny1 = y.GetLongLength(1)
                    (int64Eq lenx1 leny1) && 
                    let basex0 = int64 (x.GetLowerBound(0))
                    let basex1 = int64 (x.GetLowerBound(1))
                    let basey0 = int64 (y.GetLowerBound(0))
                    let basey1 = int64 (y.GetLowerBound(1))
                    (int64Eq basex0 basey0) && 
                    (int64Eq basex1 basey1) && 
                    // check contents
                    let rec check0 i =
                       let rec check1 j = (j >=. lenx1) || (GenericEqualityObj er iec ((x.GetValue(basex0 +. i,basex1 +. j)),(y.GetValue(basey0 +. i,basey1 +. j))) && check1 (j +. 1L))
                       (i >=. lenx0) || (check1 0L && check0 (i +. 1L))
                    check0 0L
                else 
                    (x.Rank = y.Rank) &&
                    let ndims = x.Rank
                    // check lengths
                    let rec precheck k =
                        (k >= ndims) ||
                        (int64Eq (x.GetLongLength(k)) (y.GetLongLength(k)) &&
                         int32Eq (x.GetLowerBound(k)) (y.GetLowerBound(k)) &&
                         precheck (k+1))
                    precheck 0 &&
                    let idxs : int64[] = zeroCreate ndims
                    // check contents
                    let rec checkN k baseIdx i lim =
                       (i >=. lim) ||
                       (set idxs k (baseIdx +. i);
                        (if k = ndims - 1
                         then GenericEqualityObj er iec ((x.GetValue(idxs)),(y.GetValue(idxs)))
                         else check (k+1)) && 
                        checkN k baseIdx (i +. 1L) lim)
                    and check k = 
                       (k >= ndims) || 
                       (let baseIdx = x.GetLowerBound(k)
                        checkN k (int64 baseIdx) 0L (x.GetLongLength(k)))
                    check 0

            /// optimized case: Core implementation of structural equality on object arrays.
            and GenericEqualityObjArray er iec (x:obj[]) (y:obj[]) : bool =
                let lenx = x.Length 
                let leny = y.Length 
                let c = (lenx = leny )
                if not c then c 
                else
                    let mutable i = 0
                    let mutable res = true
                    while i < lenx do 
                        let c = GenericEqualityObj er iec ((get x i),(get y i))
                        if not c then (res <- false; i <- lenx) 
                        else i <- i + 1
                    res


            /// One of the two unique instances of System.Collections.IEqualityComparer. Implements PER semantics
            /// where equality on NaN returns "false".
            let fsEqualityComparerNoHashingPER = 
                { new System.Collections.IEqualityComparer with
                    override iec.Equals(x:obj,y:obj) = GenericEqualityObj false iec (x,y)  // PER Semantics
                    override iec.GetHashCode(x:obj) = raise (InvalidOperationException (SR.GetString(SR.notUsedForHashing))) }
                    
            /// One of the two unique instances of System.Collections.IEqualityComparer. Implements ER semantics
            /// where equality on NaN returns "true".
            let fsEqualityComparerNoHashingER = 
                { new System.Collections.IEqualityComparer with
                    override iec.Equals(x:obj,y:obj) = GenericEqualityObj true iec (x,y)  // ER Semantics
                    override iec.GetHashCode(x:obj) = raise (InvalidOperationException (SR.GetString(SR.notUsedForHashing))) }

            /// Implements generic equality between two values, with PER semantics for NaN (so equality on two NaN values returns false)
            //
            // The compiler optimizer is aware of this function  (see use of generic_equality_per_inner_vref in opt.fs)
            // and devirtualizes calls to it based on "T".
            let GenericEqualityIntrinsic (x : 'T) (y : 'T) : bool = 
                GenericEqualityObj false fsEqualityComparerNoHashingPER ((box x), (box y))
                
            /// Implements generic equality between two values, with ER semantics for NaN (so equality on two NaN values returns true)
            //
            // ER semantics is used for recursive calls when implementing .Equals(that) for structural data, see the code generated for record and union types in augment.fs
            //
            // The compiler optimizer is aware of this function (see use of generic_equality_er_inner_vref in opt.fs)
            // and devirtualizes calls to it based on "T".
            let GenericEqualityERIntrinsic (x : 'T) (y : 'T) : bool =
                GenericEqualityObj true fsEqualityComparerNoHashingER ((box x), (box y))
                
            /// Implements generic equality between two values using "comp" for recursive calls.
            //
            // The compiler optimizer is aware of this function  (see use of generic_equality_withc_inner_vref in opt.fs)
            // and devirtualizes calls to it based on "T", and under the assumption that "comp" 
            // is either fsEqualityComparerNoHashingER or fsEqualityComparerNoHashingPER.
            let GenericEqualityWithComparerIntrinsic (comp : System.Collections.IEqualityComparer) (x : 'T) (y : 'T) : bool =
                comp.Equals((box x),(box y))
                

            /// Implements generic equality between two values, with ER semantics for NaN (so equality on two NaN values returns true)
            //
            // ER semantics is used for recursive calls when implementing .Equals(that) for structural data, see the code generated for record and union types in augment.fs
            //
            // If no static optimization applies, this becomes GenericEqualityERIntrinsic.
            let inline GenericEqualityERFast (x : 'T) (y : 'T) : bool = 
                  GenericEqualityERIntrinsic x y
                  when 'T : bool    = (# "ceq" x y : bool #)
                  when 'T : sbyte   = (# "ceq" x y : bool #)
                  when 'T : int16   = (# "ceq" x y : bool #)
                  when 'T : int32   = (# "ceq" x y : bool #)
                  when 'T : int64   = (# "ceq" x y : bool #)
                  when 'T : byte    = (# "ceq" x y : bool #)
                  when 'T : uint16  = (# "ceq" x y : bool #)
                  when 'T : uint32  = (# "ceq" x y : bool #)
                  when 'T : uint64  = (# "ceq" x y : bool #)
                  when 'T : nativeint  = (# "ceq" x y : bool #)
                  when 'T : unativeint  = (# "ceq" x y : bool #)
                  when 'T : float = 
                    if (# "ceq" x y : bool #) then
                        true
                    else
                        not (# "ceq" x x : bool #) && not (# "ceq" y y : bool #)
                  when 'T : float32 =
                    if (# "ceq" x y : bool #) then
                        true
                    else
                        not (# "ceq" x x : bool #) && not (# "ceq" y y : bool #)
                  when 'T : char    = (# "ceq" x y : bool #)
                  when 'T : string  = System.String.Equals((# "" x : string #),(# "" y : string #))
                  when 'T : decimal     = System.Decimal.op_Equality((# "" x:decimal #), (# "" y:decimal #))
                  when 'T : DateTime = DateTime.Equals((# "" x : DateTime #), (# "" y : DateTime #))
                               
            /// Implements generic equality between two values, with PER semantics for NaN (so equality on two NaN values returns false)
            //
            // If no static optimization applies, this becomes GenericEqualityIntrinsic.
            let inline GenericEqualityFast (x : 'T) (y : 'T) : bool = 
                  GenericEqualityIntrinsic x y
                  when 'T : bool    = (# "ceq" x y : bool #)
                  when 'T : sbyte   = (# "ceq" x y : bool #)
                  when 'T : int16   = (# "ceq" x y : bool #)
                  when 'T : int32   = (# "ceq" x y : bool #)
                  when 'T : int64   = (# "ceq" x y : bool #)
                  when 'T : byte    = (# "ceq" x y : bool #)
                  when 'T : uint16  = (# "ceq" x y : bool #)
                  when 'T : uint32  = (# "ceq" x y : bool #)
                  when 'T : uint64  = (# "ceq" x y : bool #)
                  when 'T : float   = (# "ceq" x y : bool #)
                  when 'T : float32 = (# "ceq" x y : bool #)
                  when 'T : char    = (# "ceq" x y : bool #)
                  when 'T : nativeint  = (# "ceq" x y : bool #)
                  when 'T : unativeint  = (# "ceq" x y : bool #)
                  when 'T : string  = System.String.Equals((# "" x : string #),(# "" y : string #))
                  when 'T : decimal     = System.Decimal.op_Equality((# "" x:decimal #), (# "" y:decimal #))
                  when 'T : DateTime = DateTime.Equals((# "" x : DateTime #), (# "" y : DateTime #))
    
                  
            /// A compiler intrinsic generated during optimization of calls to GenericEqualityIntrinsic on tuple values.
            //
            // If no static optimization applies, this becomes GenericEqualityIntrinsic.
            //
            // Note, although this function says "WithComparer", the static optimization conditionals for float and float32
            // mean that it has PER semantics. This is OK because calls to this function are only generated by 
            // the F# compiler, ultimately stemming from an optimization of GenericEqualityIntrinsic when used on a tuple type.
            let inline GenericEqualityWithComparerFast (comp : System.Collections.IEqualityComparer) (x : 'T) (y : 'T) : bool = 
                  GenericEqualityWithComparerIntrinsic comp x y
                  when 'T : bool    = (# "ceq" x y : bool #)
                  when 'T : sbyte   = (# "ceq" x y : bool #)
                  when 'T : int16   = (# "ceq" x y : bool #)
                  when 'T : int32   = (# "ceq" x y : bool #)
                  when 'T : int64   = (# "ceq" x y : bool #)
                  when 'T : byte    = (# "ceq" x y : bool #)
                  when 'T : uint16  = (# "ceq" x y : bool #)
                  when 'T : uint32  = (# "ceq" x y : bool #)
                  when 'T : uint64  = (# "ceq" x y : bool #)
                  when 'T : float   = (# "ceq" x y : bool #)        
                  when 'T : float32 = (# "ceq" x y : bool #)          
                  when 'T : char    = (# "ceq" x y : bool #)
                  when 'T : nativeint  = (# "ceq" x y : bool #)
                  when 'T : unativeint  = (# "ceq" x y : bool #)
                  when 'T : string  = System.String.Equals((# "" x : string #),(# "" y : string #))                  
                  when 'T : decimal     = System.Decimal.op_Equality((# "" x:decimal #), (# "" y:decimal #))
                  when 'T : DateTime = DateTime.Equals((# "" x : DateTime #), (# "" y : DateTime #))
                  

            let inline GenericInequalityFast (x:'T) (y:'T) = (not(GenericEqualityFast x y) : bool)
            let inline GenericInequalityERFast (x:'T) (y:'T) = (not(GenericEqualityERFast x y) : bool)


            //-------------------------------------------------------------------------
            // LanguagePrimitives.HashCompare: HASHING.  
            //------------------------------------------------------------------------- 



            let defaultHashNodes = 18 

            /// The implementation of IEqualityComparer, using depth-limited for hashing and PER semantics for NaN equality.
            type CountLimitedHasherPER(sz:int) =
                [<DefaultValue>]
                val mutable nodeCount : int
                
                member x.Fresh() = 
                    if (System.Threading.Interlocked.CompareExchange(&(x.nodeCount), sz, 0) = 0) then 
                        x
                    else
                        new CountLimitedHasherPER(sz)
                
                interface IEqualityComparer 

            /// The implementation of IEqualityComparer, using unlimited depth for hashing and ER semantics for NaN equality.
            type UnlimitedHasherER() =
                interface IEqualityComparer 
                
            /// The implementation of IEqualityComparer, using unlimited depth for hashing and PER semantics for NaN equality.
            type UnlimitedHasherPER() =
                interface IEqualityComparer
                    

            /// The unique object for unlimited depth for hashing and ER semantics for equality.
            let fsEqualityComparerUnlimitedHashingER = UnlimitedHasherER()

            /// The unique object for unlimited depth for hashing and PER semantics for equality.
            let fsEqualityComparerUnlimitedHashingPER = UnlimitedHasherPER()
             
            let inline HashCombine nr x y = (x <<< 1) + y + 631 * nr

            let GenericHashObjArray (iec : System.Collections.IEqualityComparer) (x: obj[]) : int =
                  let len = x.Length 
                  let mutable i = len - 1 
                  if i > defaultHashNodes then i <- defaultHashNodes // limit the hash
                  let mutable acc = 0   
                  while (i >= 0) do 
                      // NOTE: GenericHash* call decreases nr 
                      acc <- HashCombine i acc (iec.GetHashCode(x.GetValue(i)));
                      i <- i - 1
                  acc

            // optimized case - byte arrays 
            let GenericHashByteArray (x: byte[]) : int =
                  let len = length x 
                  let mutable i = len - 1 
                  if i > defaultHashNodes then i <- defaultHashNodes // limit the hash
                  let mutable acc = 0   
                  while (i >= 0) do 
                      acc <- HashCombine i acc (intOfByte (get x i));
                      i <- i - 1
                  acc

            // optimized case - int arrays 
            let GenericHashInt32Array (x: int[]) : int =
                  let len = length x 
                  let mutable i = len - 1 
                  if i > defaultHashNodes then i <- defaultHashNodes // limit the hash
                  let mutable acc = 0   
                  while (i >= 0) do 
                      acc <- HashCombine i acc (get x i);
                      i <- i - 1
                  acc

            // optimized case - int arrays 
            let GenericHashInt64Array (x: int64[]) : int =
                  let len = length x 
                  let mutable i = len - 1 
                  if i > defaultHashNodes then i <- defaultHashNodes // limit the hash
                  let mutable acc = 0   
                  while (i >= 0) do 
                      acc <- HashCombine i acc (int32 (get x i));
                      i <- i - 1
                  acc

            // special case - arrays do not by default have a decent structural hashing function
            let GenericHashArbArray (iec : System.Collections.IEqualityComparer) (x: System.Array) : int =
                  match x.Rank  with 
                  | 1 -> 
                    let b = x.GetLowerBound(0) 
                    let len = x.Length 
                    let mutable i = b + len - 1 
                    if i > b + defaultHashNodes  then i <- b + defaultHashNodes  // limit the hash
                    let mutable acc = 0                  
                    while (i >= b) do 
                        // NOTE: GenericHash* call decreases nr 
                        acc <- HashCombine i acc (iec.GetHashCode(x.GetValue(i)));
                        i <- i - 1
                    acc
                  | _ -> 
                     HashCombine 10 (x.GetLength(0)) (x.GetLength(1)) 

            // Core implementation of structural hashing, corresponds to pseudo-code in the 
            // F# Language spec.  Searches for the IStructuralHash interface, otherwise uses GetHashCode().
            // Arrays are structurally hashed through a separate technique.
            //
            // "iec" is either fsEqualityComparerUnlimitedHashingER, fsEqualityComparerUnlimitedHashingPER or a CountLimitedHasherPER.
            let rec GenericHashParamObj (iec : System.Collections.IEqualityComparer) (x: obj) : int =
                  match x with 
                  | null -> 0 
                  | (:? System.Array as a) -> 
                      match a with 
                      | :? (obj[]) as oa -> GenericHashObjArray iec oa 
                      | :? (byte[]) as ba -> GenericHashByteArray ba 
                      | :? (int[]) as ba -> GenericHashInt32Array ba 
                      | :? (int64[]) as ba -> GenericHashInt64Array ba 
                      | _ -> GenericHashArbArray iec a 
                  | :? IStructuralEquatable as a ->    
                      a.GetHashCode(iec)
                  | _ -> 
                      x.GetHashCode()


            /// Fill in the implementation of CountLimitedHasherPER
            type CountLimitedHasherPER with
                
                interface System.Collections.IEqualityComparer with
                    override iec.Equals(x:obj,y:obj) =
                        GenericEqualityObj false iec (x,y)
                    override iec.GetHashCode(x:obj) =
                        iec.nodeCount <- iec.nodeCount - 1
                        if iec.nodeCount > 0 then
                            GenericHashParamObj iec  x
                        else
                            -1
               
            /// Fill in the implementation of UnlimitedHasherER
            type UnlimitedHasherER with
                
                interface System.Collections.IEqualityComparer with
                    override iec.Equals(x:obj,y:obj) = GenericEqualityObj true iec (x,y)
                    override iec.GetHashCode(x:obj) = GenericHashParamObj iec  x
                   
            /// Fill in the implementation of UnlimitedHasherPER
            type UnlimitedHasherPER with
                interface System.Collections.IEqualityComparer with
                    override iec.Equals(x:obj,y:obj) = GenericEqualityObj false iec (x,y)
                    override iec.GetHashCode(x:obj) = GenericHashParamObj iec x

            /// Intrinsic for calls to depth-unlimited structural hashing that were not optimized by static conditionals.
            //
            // NOTE: The compiler optimizer is aware of this function (see uses of generic_hash_inner_vref in opt.fs)
            // and devirtualizes calls to it based on type "T".
            let GenericHashIntrinsic input = GenericHashParamObj fsEqualityComparerUnlimitedHashingPER (box input)

            /// Intrinsic for calls to depth-limited structural hashing that were not optimized by static conditionals.
            let LimitedGenericHashIntrinsic limit input = GenericHashParamObj (CountLimitedHasherPER(limit)) (box input)

            /// Intrinsic for a recursive call to structural hashing that was not optimized by static conditionals.
            //
            // "iec" is assumed to be either fsEqualityComparerUnlimitedHashingER, fsEqualityComparerUnlimitedHashingPER or 
            // a CountLimitedHasherPER.
            //
            // NOTE: The compiler optimizer is aware of this function (see uses of generic_hash_withc_inner_vref in opt.fs)
            // and devirtualizes calls to it based on type "T".
            let GenericHashWithComparerIntrinsic<'T> (comp : System.Collections.IEqualityComparer) (input : 'T) : int =
                GenericHashParamObj comp (box input)
                
            let inline HashString (s:string) = 
                 match s with 
                 | null -> 0 
                 | _ -> s.GetHashCode()
                    
            // from mscorlib v4.0.30319
            let inline HashChar (x:char) = (# "or" (# "shl" x 16 : int #) x : int #)
            let inline HashSByte (x:sbyte) = (# "xor" (# "shl" x 8 : int #) x : int #)
            let inline HashInt16 (x:int16) = (# "or" (# "conv.u2" x : int #) (# "shl" x 16 : int #) : int #)
            let inline HashInt64 (x:int64) = (# "xor" (# "conv.i4" x : int #) (# "conv.i4" (# "shr" x 32 : int #) : int #) : int #)
            let inline HashUInt64 (x:uint64) = (# "xor" (# "conv.i4" x : int #) (# "conv.i4" (# "shr.un" x 32 : int #) : int #) : int #)
            let inline HashIntPtr (x:nativeint) = (# "conv.i4" (# "conv.u8" x : uint64 #) : int #)
            let inline HashUIntPtr (x:unativeint) = (# "and" (# "conv.i4" (# "conv.u8" x : uint64 #) : int #) 0x7fffffff : int #)

            /// Core entry into structural hashing for either limited or unlimited hashing.  
            //
            // "iec" is assumed to be either fsEqualityComparerUnlimitedHashingER, fsEqualityComparerUnlimitedHashingPER or 
            // a CountLimitedHasherPER.
            let inline GenericHashWithComparerFast (iec : System.Collections.IEqualityComparer) (x:'T) : int = 
                GenericHashWithComparerIntrinsic iec x 
                when 'T : bool   = (# "" x : int #)
                when 'T : int32  = (# "" x : int #)
                when 'T : byte   = (# "" x : int #)
                when 'T : uint32 = (# "" x : int #)                          
                when 'T : char = HashChar (# "" x : char #)                         
                when 'T : sbyte = HashSByte (# "" x : sbyte #)                          
                when 'T : int16 = HashInt16 (# "" x : int16 #)
                when 'T : int64 = HashInt64 (# "" x : int64 #)
                when 'T : uint64 = HashUInt64 (# "" x : uint64 #)
                when 'T : nativeint = HashIntPtr (# "" x : nativeint #)
                when 'T : unativeint = HashUIntPtr (# "" x : unativeint #)
                when 'T : uint16 = (# "" x : int #)                    
                when 'T : string = HashString  (# "" x : string #)
                    
            /// Core entry into depth-unlimited structural hashing.  Hash to a given depth limit.
            let inline GenericHashFast (x:'T) : int = 
                GenericHashIntrinsic x 
                when 'T : bool   = (# "" x : int #)
                when 'T : int32  = (# "" x : int #)
                when 'T : byte   = (# "" x : int #)
                when 'T : uint32 = (# "" x : int #)
                when 'T : char = HashChar (# "" x : char #)
                when 'T : sbyte = HashSByte (# "" x : sbyte #)
                when 'T : int16 = HashInt16 (# "" x : int16 #)
                when 'T : int64 = HashInt64 (# "" x : int64 #)
                when 'T : uint64 = HashUInt64 (# "" x : uint64 #)
                when 'T : nativeint = HashIntPtr (# "" x : nativeint #)
                when 'T : unativeint = HashUIntPtr (# "" x : unativeint #)
                when 'T : uint16 = (# "" x : int #)                    
                when 'T : string = HashString  (# "" x : string #)

            /// Core entry into depth-limited structural hashing.  
            let inline GenericLimitedHashFast (limit:int) (x:'T) : int = 
                LimitedGenericHashIntrinsic limit x 
                when 'T : bool   = (# "" x : int #)
                when 'T : int32  = (# "" x : int #)
                when 'T : byte   = (# "" x : int #)
                when 'T : uint32 = (# "" x : int #)                    
                when 'T : char = HashChar (# "" x : char #)                          
                when 'T : sbyte = HashSByte (# "" x : sbyte #)                          
                when 'T : int16 = HashInt16 (# "" x : int16 #)
                when 'T : int64 = HashInt64 (# "" x : int64 #)
                when 'T : uint64 = HashUInt64 (# "" x : uint64 #)
                when 'T : nativeint = HashIntPtr (# "" x : nativeint #)
                when 'T : unativeint = HashUIntPtr (# "" x : unativeint #)
                when 'T : uint16 = (# "" x : int #)                    
                when 'T : string = HashString  (# "" x : string #)


            /// Compiler intrinsic generated for devirtualized calls to structural hashing on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericHashWithComparerIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericHashWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastHashTuple2 (comparer:System.Collections.IEqualityComparer) (x1,x2) = 
                TupleUtils.combineTupleHashes
                    (GenericHashWithComparerFast comparer x1)
                    (GenericHashWithComparerFast comparer x2)

            /// Compiler intrinsic generated for devirtualized calls to structural hashing on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericHashWithComparerIntrinsic is used 
            // statically with a tuple type.
            //
            // Because the function subsequently gets inlined, the calls to GenericHashWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastHashTuple3 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3) =
                TupleUtils.combineTupleHashes
                    (TupleUtils.combineTupleHashes
                        (GenericHashWithComparerFast comparer x1)
                        (GenericHashWithComparerFast comparer x2))
                    (GenericHashWithComparerFast comparer x3)

            /// Compiler intrinsic generated for devirtualized calls to structural hashing on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericHashWithComparerIntrinsic is used 
            // statically with a tuple type.
            //
            // Because the function subsequently gets inlined, the calls to GenericHashWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastHashTuple4 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3,x4) = 
                TupleUtils.combineTupleHashes
                    (TupleUtils.combineTupleHashes
                        (GenericHashWithComparerFast comparer x1)
                        (GenericHashWithComparerFast comparer x2))
                    (TupleUtils.combineTupleHashes
                        (GenericHashWithComparerFast comparer x3)
                        (GenericHashWithComparerFast comparer x4))

            /// Compiler intrinsic generated for devirtualized calls to structural hashing on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericHashWithComparerIntrinsic is used 
            // statically with a tuple type.
            //
            // Because the function subsequently gets inlined, the calls to GenericHashWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastHashTuple5 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3,x4,x5) = 
                TupleUtils.combineTupleHashes 
                    (TupleUtils.combineTupleHashes 
                        (TupleUtils.combineTupleHashes 
                            (GenericHashWithComparerFast comparer x1)
                            (GenericHashWithComparerFast comparer x2))
                        (TupleUtils.combineTupleHashes
                            (GenericHashWithComparerFast comparer x3)
                            (GenericHashWithComparerFast comparer x4)))
                    (GenericHashWithComparerFast comparer x5)

            /// Compiler intrinsic generated for devirtualized calls to PER-semantic structural equality on tuples
            //
            // The F# compiler optimizer generates calls to this function when GenericEqualityIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericEqualityWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastEqualsTuple2 (comparer:System.Collections.IEqualityComparer) (x1,x2) (y1,y2) = 
                GenericEqualityWithComparerFast comparer x1 y1 &&
                GenericEqualityWithComparerFast comparer x2 y2

            /// Compiler intrinsic generated for devirtualized calls to PER-semantic structural equality on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericEqualityIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericEqualityWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastEqualsTuple3 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3) (y1,y2,y3) = 
                GenericEqualityWithComparerFast comparer x1 y1 &&
                GenericEqualityWithComparerFast comparer x2 y2 &&
                GenericEqualityWithComparerFast comparer x3 y3

            /// Compiler intrinsic generated for devirtualized calls to PER-semantic structural equality on tuples (with PER semantics).  
            //
            // The F# compiler optimizer generates calls to this function when GenericEqualityIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericEqualityWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastEqualsTuple4 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3,x4) (y1,y2,y3,y4) = 
                GenericEqualityWithComparerFast comparer x1 y1 &&
                GenericEqualityWithComparerFast comparer x2 y2 &&
                GenericEqualityWithComparerFast comparer x3 y3 &&
                GenericEqualityWithComparerFast comparer x4 y4

            /// Compiler intrinsic generated for devirtualized calls to PER-semantic structural equality on tuples.  
            //
            // The F# compiler optimizer generates calls to this function when GenericEqualityIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericEqualityWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastEqualsTuple5 (comparer:System.Collections.IEqualityComparer) (x1,x2,x3,x4,x5) (y1,y2,y3,y4,y5) = 
                GenericEqualityWithComparerFast comparer x1 y1 &&
                GenericEqualityWithComparerFast comparer x2 y2 &&
                GenericEqualityWithComparerFast comparer x3 y3 &&
                GenericEqualityWithComparerFast comparer x4 y4 &&
                GenericEqualityWithComparerFast comparer x5 y5

            /// Compiler intrinsic generated for devirtualized calls to structural comparison on tuples (with ER semantics)
            //
            // The F# compiler optimizer generates calls to this function when GenericComparisonIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericComparisonWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastCompareTuple2 (comparer:System.Collections.IComparer)  (x1,x2) (y1,y2) =
                let  n = GenericComparisonWithComparerFast comparer x1 y1
                if n <> 0 then n else
                GenericComparisonWithComparerFast comparer x2 y2

            /// Compiler intrinsic generated for devirtualized calls to structural comparison on tuples (with ER semantics)
            //
            // The F# compiler optimizer generates calls to this function when GenericComparisonIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericComparisonWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastCompareTuple3 (comparer:System.Collections.IComparer) (x1,x2,x3) (y1,y2,y3) =
                let  n = GenericComparisonWithComparerFast comparer x1 y1
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x2 y2
                if n <> 0 then n else
                GenericComparisonWithComparerFast comparer x3 y3

            /// Compiler intrinsic generated for devirtualized calls to structural comparison on tuples (with ER semantics)
            //
            // The F# compiler optimizer generates calls to this function when GenericComparisonIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericComparisonWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastCompareTuple4 (comparer:System.Collections.IComparer) (x1,x2,x3,x4) (y1,y2,y3,y4) = 
                let  n = GenericComparisonWithComparerFast comparer x1 y1
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x2 y2
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x3 y3
                if n <> 0 then n else
                GenericComparisonWithComparerFast comparer x4 y4
            
            /// Compiler intrinsic generated for devirtualized calls to structural comparison on tuples (with ER semantics)
            //
            // The F# compiler optimizer generates calls to this function when GenericComparisonIntrinsic is used 
            // statically with a tuple type.
            // 
            // Because the function subsequently gets inlined, the calls to GenericComparisonWithComparerFast can be 
            // often statically optimized or devirtualized based on the statically known type.
            let inline FastCompareTuple5 (comparer:System.Collections.IComparer) (x1,x2,x3,x4,x5) (y1,y2,y3,y4,y5) =
                let  n = GenericComparisonWithComparerFast comparer x1 y1
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x2 y2
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x3 y3
                if n <> 0 then n else
                let  n = GenericComparisonWithComparerFast comparer x4 y4
                if n <> 0 then n else
                GenericComparisonWithComparerFast comparer x5 y5

        //-------------------------------------------------------------------------
        // LanguagePrimitives: PUBLISH HASH, EQUALITY AND COMPARISON FUNCTIONS.  
        //------------------------------------------------------------------------- 

        // Publish the intrinsic plus the static optimization conditionals
        let inline GenericEquality e1 e2 = HashCompare.GenericEqualityFast e1 e2

        let inline GenericEqualityER e1 e2 = HashCompare.GenericEqualityERFast e1 e2

        let inline GenericEqualityWithComparer comp e1 e2 = HashCompare.GenericEqualityWithComparerFast comp e1 e2

        let inline GenericComparison e1 e2 = HashCompare.GenericComparisonFast e1 e2

        let inline GenericComparisonWithComparer comp e1 e2 = HashCompare.GenericComparisonWithComparerFast comp e1 e2

        let inline GenericLessThan e1 e2 = HashCompare.GenericLessThanFast e1 e2

        let inline GenericGreaterThan e1 e2 = HashCompare.GenericGreaterThanFast e1 e2

        let inline GenericLessOrEqual e1 e2 = HashCompare.GenericLessOrEqualFast e1 e2

        let inline GenericGreaterOrEqual e1 e2 = HashCompare.GenericGreaterOrEqualFast e1 e2

        let inline retype<'T,'U> (x:'T) : 'U = (# "" x : 'U #)

        let inline GenericMinimum (e1: 'T) (e2: 'T) = 
            if HashCompare.GenericLessThanFast e1 e2 then e1 else e2
            when 'T : float         = (System.Math.Min : float * float -> float)(retype<_,float> e1, retype<_,float> e2)
            when 'T : float32       = (System.Math.Min : float32 * float32 -> float32)(retype<_,float32> e1, retype<_,float32> e2)

        let inline GenericMaximum (e1: 'T) (e2: 'T) = 
            if HashCompare.GenericLessThanFast e1 e2 then e2 else e1
            when 'T : float         = (System.Math.Max : float * float -> float)(retype<_,float> e1, retype<_,float> e2)
            when 'T : float32       = (System.Math.Max : float32 * float32 -> float32)(retype<_,float32> e1, retype<_,float32> e2)

        let inline PhysicalEquality e1 e2 = HashCompare.PhysicalEqualityFast e1 e2

        let inline PhysicalHash obj = HashCompare.PhysicalHashFast obj
        
        let inline typeeq<'T, 'U> = PhysicalEquality typeof<'T> typeof<'U>
        let inline type2eq<'T1, 'T2, 'U> = typeeq<'T1, 'U> && typeeq<'T2, 'U>  
        let inline type3eq<'T1, 'T2, 'T3, 'U> = typeeq<'T1, 'U> && typeeq<'T2, 'U> && typeeq<'T3, 'U> 

        let GenericComparer = HashCompare.fsComparerER :> IComparer

        let GenericEqualityComparer = HashCompare.fsEqualityComparerUnlimitedHashingPER :> IEqualityComparer

        let GenericEqualityERComparer = HashCompare.fsEqualityComparerUnlimitedHashingER :> IEqualityComparer

        let inline GenericHash obj = HashCompare.GenericHashFast obj

        let inline GenericLimitedHash limit obj = HashCompare.GenericLimitedHashFast limit obj

        let inline GenericHashWithComparer comparer obj = HashCompare.GenericHashWithComparerFast comparer obj

        //-------------------------------------------------------------------------
        // LanguagePrimitives: PUBLISH IEqualityComparer AND IComparer OBJECTS
        //------------------------------------------------------------------------- 


        let inline MakeGenericEqualityComparer<'T>() = 
            // type-specialize some common cases to generate more efficient functions 
            { new System.Collections.Generic.IEqualityComparer<'T> with 
                  member self.GetHashCode(x) = GenericHash x 
                  member self.Equals(x,y) = GenericEquality x y }

        let inline MakeGenericLimitedEqualityComparer<'T>(limit:int) = 
            // type-specialize some common cases to generate more efficient functions 
            { new System.Collections.Generic.IEqualityComparer<'T> with 
                  member self.GetHashCode(x) = GenericLimitedHash limit x 
                  member self.Equals(x,y) = GenericEquality x y }

        let BoolIEquality    = MakeGenericEqualityComparer<bool>()
        let CharIEquality    = MakeGenericEqualityComparer<char>()
        let StringIEquality  = MakeGenericEqualityComparer<string>()
        let SByteIEquality   = MakeGenericEqualityComparer<sbyte>()
        let Int16IEquality   = MakeGenericEqualityComparer<int16>()
        let Int32IEquality   = MakeGenericEqualityComparer<int32>()
        let Int64IEquality   = MakeGenericEqualityComparer<int64>()
        let IntPtrIEquality  = MakeGenericEqualityComparer<nativeint>()
        let ByteIEquality    = MakeGenericEqualityComparer<byte>()
        let UInt16IEquality  = MakeGenericEqualityComparer<uint16>()
        let UInt32IEquality  = MakeGenericEqualityComparer<uint32>()
        let UInt64IEquality  = MakeGenericEqualityComparer<uint64>()
        let UIntPtrIEquality = MakeGenericEqualityComparer<unativeint>()
        let FloatIEquality   = MakeGenericEqualityComparer<float>()
        let Float32IEquality = MakeGenericEqualityComparer<float32>()
        let DecimalIEquality = MakeGenericEqualityComparer<decimal>()

        [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]     
        type FastGenericEqualityComparerTable<'T>() = 
            static let f : System.Collections.Generic.IEqualityComparer<'T> = 
                match typeof<'T> with 
                | ty when ty.Equals(typeof<bool>)       -> unboxPrim (box BoolIEquality)
                | ty when ty.Equals(typeof<byte>)       -> unboxPrim (box ByteIEquality)
                | ty when ty.Equals(typeof<int32>)      -> unboxPrim (box Int32IEquality)
                | ty when ty.Equals(typeof<uint32>)     -> unboxPrim (box UInt32IEquality)
                | ty when ty.Equals(typeof<char>)       -> unboxPrim (box CharIEquality)
                | ty when ty.Equals(typeof<sbyte>)      -> unboxPrim (box SByteIEquality)
                | ty when ty.Equals(typeof<int16>)      -> unboxPrim (box Int16IEquality)
                | ty when ty.Equals(typeof<int64>)      -> unboxPrim (box Int64IEquality)
                | ty when ty.Equals(typeof<nativeint>)  -> unboxPrim (box IntPtrIEquality)
                | ty when ty.Equals(typeof<uint16>)     -> unboxPrim (box UInt16IEquality)
                | ty when ty.Equals(typeof<uint64>)     -> unboxPrim (box UInt64IEquality)
                | ty when ty.Equals(typeof<unativeint>) -> unboxPrim (box UIntPtrIEquality)
                | ty when ty.Equals(typeof<float>)      -> unboxPrim (box FloatIEquality)
                | ty when ty.Equals(typeof<float32>)    -> unboxPrim (box Float32IEquality)
                | ty when ty.Equals(typeof<decimal>)    -> unboxPrim (box DecimalIEquality)
                | ty when ty.Equals(typeof<string>)     -> unboxPrim (box StringIEquality)
                | _ -> MakeGenericEqualityComparer<'T>()
            static member Function : System.Collections.Generic.IEqualityComparer<'T> = f

        let FastGenericEqualityComparerFromTable<'T> = FastGenericEqualityComparerTable<'T>.Function

        // This is the implementation of HashIdentity.Structural.  In most cases this just becomes
        // FastGenericEqualityComparerFromTable.
        let inline FastGenericEqualityComparer<'T> = 
            // This gets used if 'T can't be resolved to anything interesting
            FastGenericEqualityComparerFromTable<'T>
            // When 'T is a primitive, just use the fixed entry in the table
            when 'T : bool   = FastGenericEqualityComparerFromTable<'T>
            when 'T : int32  = FastGenericEqualityComparerFromTable<'T>
            when 'T : byte   = FastGenericEqualityComparerFromTable<'T>
            when 'T : uint32 = FastGenericEqualityComparerFromTable<'T>
            when 'T : string = FastGenericEqualityComparerFromTable<'T>
            when 'T : sbyte  = FastGenericEqualityComparerFromTable<'T>
            when 'T : int16  = FastGenericEqualityComparerFromTable<'T>
            when 'T : int64  = FastGenericEqualityComparerFromTable<'T>
            when 'T : nativeint  = FastGenericEqualityComparerFromTable<'T>
            when 'T : uint16 = FastGenericEqualityComparerFromTable<'T>
            when 'T : uint64 = FastGenericEqualityComparerFromTable<'T>
            when 'T : unativeint = FastGenericEqualityComparerFromTable<'T>
            when 'T : float  = FastGenericEqualityComparerFromTable<'T>
            when 'T : float32 = FastGenericEqualityComparerFromTable<'T>
            when 'T : char   = FastGenericEqualityComparerFromTable<'T>
            when 'T : decimal = FastGenericEqualityComparerFromTable<'T>
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever 'T is resolved to a nominal or tuple type 
             // and none of the other rules above apply.
             //
             // When 'T is statically known to be nominal or tuple, it is better to inline the implementation of 
             // MakeGenericEqualityComparer. This is then reduced by further inlining to the primitives 
             // known to the F# compiler which are then often optimized for the particular nominal type involved.
            when 'T : 'T = MakeGenericEqualityComparer<'T>()

        let inline FastLimitedGenericEqualityComparer<'T>(limit) = MakeGenericLimitedEqualityComparer<'T>(limit) 

        let inline MakeGenericComparer<'T>()  = 
            { new System.Collections.Generic.IComparer<'T> with 
                 member _.Compare(x,y) = GenericComparison x y }

        let CharComparer    = MakeGenericComparer<char>()
        let StringComparer  = MakeGenericComparer<string>()
        let SByteComparer   = MakeGenericComparer<sbyte>()
        let Int16Comparer   = MakeGenericComparer<int16>()
        let Int32Comparer   = MakeGenericComparer<int32>()
        let Int64Comparer   = MakeGenericComparer<int64>()
        let IntPtrComparer  = MakeGenericComparer<nativeint>()
        let ByteComparer    = MakeGenericComparer<byte>()
        let UInt16Comparer  = MakeGenericComparer<uint16>()
        let UInt32Comparer  = MakeGenericComparer<uint32>()
        let UInt64Comparer  = MakeGenericComparer<uint64>()
        let UIntPtrComparer = MakeGenericComparer<unativeint>()
        let FloatComparer   = MakeGenericComparer<float>()
        let Float32Comparer = MakeGenericComparer<float32>()
        let DecimalComparer = MakeGenericComparer<decimal>()
        let BoolComparer    = MakeGenericComparer<bool>()

        /// Use a type-indexed table to ensure we only create a single FastStructuralComparison function
        /// for each type
        [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]     
        type FastGenericComparerTable<'T>() = 

            // The CLI implementation of mscorlib optimizes array sorting
            // when the comparer is either null or precisely
            // reference-equals to System.Collections.Generic.Comparer<'T>.Default.
            // This is an indication that a "fast" array sorting helper can be used.
            //
            // So, for all the types listed below, we want to pass in a value of "null" for
            // the comparer object.  Note that F# generic comparison coincides precisely with 
            // System.Collections.Generic.Comparer<'T>.Default for these types.
            //
            // A "null" comparer is only valid if the values do not have identity, e.g. integers.
            // That is, an unstable sort of the array must be the semantically the 
            // same as a stable sort of the array. See Array.stableSortInPlace.
            //
            // REVIEW: in a future version we could extend this to include additional types 
            static let fCanBeNull : System.Collections.Generic.IComparer<'T>  = 
                match typeof<'T> with 
                | ty when ty.Equals(typeof<nativeint>)  -> unboxPrim (box IntPtrComparer)
                | ty when ty.Equals(typeof<unativeint>) -> unboxPrim (box UIntPtrComparer)
                | ty when ty.Equals(typeof<byte>)       -> null    
                | ty when ty.Equals(typeof<char>)       -> null    
                | ty when ty.Equals(typeof<sbyte>)      -> null     
                | ty when ty.Equals(typeof<int16>)      -> null    
                | ty when ty.Equals(typeof<int32>)      -> null    
                | ty when ty.Equals(typeof<int64>)      -> null    
                | ty when ty.Equals(typeof<uint16>)     -> null    
                | ty when ty.Equals(typeof<uint32>)     -> null    
                | ty when ty.Equals(typeof<uint64>)     -> null    
                | ty when ty.Equals(typeof<float>)      -> null    
                | ty when ty.Equals(typeof<float32>)    -> null    
                | ty when ty.Equals(typeof<decimal>)    -> null    
                | ty when ty.Equals(typeof<string>)     -> unboxPrim (box StringComparer)
                | ty when ty.Equals(typeof<bool>)       -> null
                | _ -> MakeGenericComparer<'T>()

            static let f : System.Collections.Generic.IComparer<'T>  = 
                match typeof<'T> with 
                | ty when ty.Equals(typeof<byte>)       -> unboxPrim (box ByteComparer)
                | ty when ty.Equals(typeof<char>)       -> unboxPrim (box CharComparer)
                | ty when ty.Equals(typeof<sbyte>)      -> unboxPrim (box SByteComparer)
                | ty when ty.Equals(typeof<int16>)      -> unboxPrim (box Int16Comparer)
                | ty when ty.Equals(typeof<int32>)      -> unboxPrim (box Int32Comparer)
                | ty when ty.Equals(typeof<int64>)      -> unboxPrim (box Int64Comparer)
                | ty when ty.Equals(typeof<nativeint>)  -> unboxPrim (box IntPtrComparer)
                | ty when ty.Equals(typeof<uint16>)     -> unboxPrim (box UInt16Comparer)
                | ty when ty.Equals(typeof<uint32>)     -> unboxPrim (box UInt32Comparer)
                | ty when ty.Equals(typeof<uint64>)     -> unboxPrim (box UInt64Comparer)
                | ty when ty.Equals(typeof<unativeint>) -> unboxPrim (box UIntPtrComparer)
                | ty when ty.Equals(typeof<float>)      -> unboxPrim (box FloatComparer)
                | ty when ty.Equals(typeof<float32>)    -> unboxPrim (box Float32Comparer)
                | ty when ty.Equals(typeof<decimal>)    -> unboxPrim (box DecimalComparer)
                | ty when ty.Equals(typeof<string>)     -> unboxPrim (box StringComparer)
                | ty when ty.Equals(typeof<bool>)       -> unboxPrim (box BoolComparer)
                | _ -> 
                    // Review: There are situations where we should be able
                    // to return System.Collections.Generic.Comparer<'T>.Default here.
                    // For example, for any value type.
                    MakeGenericComparer<'T>()

            static member Value : System.Collections.Generic.IComparer<'T> = f

            static member ValueCanBeNullIfDefaultSemantics : System.Collections.Generic.IComparer<'T> = fCanBeNull
        
        let FastGenericComparerFromTable<'T> = 
            FastGenericComparerTable<'T>.Value

        let inline FastGenericComparer<'T> = 
            // This gets used is 'T can't be resolved to anything interesting
            FastGenericComparerFromTable<'T>
            // When 'T is a primitive, just use the fixed entry in the table
            when 'T : bool   = FastGenericComparerFromTable<'T>
            when 'T : sbyte  = FastGenericComparerFromTable<'T>
            when 'T : int16  = FastGenericComparerFromTable<'T>
            when 'T : int32  = FastGenericComparerFromTable<'T>
            when 'T : int64  = FastGenericComparerFromTable<'T>
            when 'T : nativeint  = FastGenericComparerFromTable<'T>
            when 'T : byte   = FastGenericComparerFromTable<'T>
            when 'T : uint16 = FastGenericComparerFromTable<'T>
            when 'T : uint32 = FastGenericComparerFromTable<'T>
            when 'T : uint64 = FastGenericComparerFromTable<'T>
            when 'T : unativeint = FastGenericComparerFromTable<'T>
            when 'T : float  = FastGenericComparerFromTable<'T>
            when 'T : float32 = FastGenericComparerFromTable<'T>
            when 'T : char   = FastGenericComparerFromTable<'T>
            when 'T : string = FastGenericComparerFromTable<'T>
            when 'T : decimal = FastGenericComparerFromTable<'T>
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever 'T is resolved by inlining to be a nominal type 
             // and none of the other rules above apply
             //
             // In this case it is better to inline the implementation of MakeGenericComparer so that
             // the comparison object is eventually reduced to the primitives known to the F# compiler
             // which are then optimized for the particular nominal type involved.
            when 'T : 'T = MakeGenericComparer<'T>()
            
        let FastGenericComparerCanBeNull<'T> = FastGenericComparerTable<'T>.ValueCanBeNullIfDefaultSemantics

        //-------------------------------------------------------------------------
        // LanguagePrimitives: ENUMS
        //------------------------------------------------------------------------- 

        let inline EnumOfValue (value : 'T) : 'Enum when 'Enum : enum<'T> = 
            unboxPrim<'Enum>(box value)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever 'Enum is resolved to a nominal
            when 'Enum : 'Enum = (retype value : 'Enum)

        let inline EnumToValue (enum : 'Enum) : 'T when 'Enum : enum<'T> = 
            unboxPrim<'T>(box enum)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever 'Enum is resolved to a nominal type
            when 'Enum : 'Enum = (retype enum : 'T)

        //-------------------------------------------------------------------------
        // LanguagePrimitives: MEASURES
        //------------------------------------------------------------------------- 

        let inline FloatWithMeasure (f : float) : float<'Measure> = retype f
        let inline Float32WithMeasure (f : float32) : float32<'Measure> = retype f
        let inline DecimalWithMeasure (f : decimal) : decimal<'Measure> = retype f
        let inline Int32WithMeasure (f : int) : int<'Measure> = retype f
        let inline Int16WithMeasure (f : int16) : int16<'Measure> = retype f
        let inline SByteWithMeasure (f : sbyte) : sbyte<'Measure> = retype f
        let inline Int64WithMeasure (f : int64) : int64<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline IntPtrWithMeasure (f : nativeint) : nativeint<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline UInt32WithMeasure (f : uint) : uint<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline UInt16WithMeasure (f : uint16) : uint16<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline UInt64WithMeasure (f : uint64) : uint64<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline ByteWithMeasure (f : byte) : byte<'Measure> = retype f
        
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        let inline UIntPtrWithMeasure (f : unativeint) : unativeint<'Measure> = retype f

        let inline formatError() = raise (new System.FormatException(SR.GetString(SR.badFormatString)))

        // Parse formats
        //      DDDDDDDD
        //      -DDDDDDDD
        //      0xHHHHHHHH
        //      -0xHHHHHHHH
        //      0bBBBBBBB
        //      -0bBBBBBBB
        //      0oOOOOOOO
        //      -0oOOOOOOO
        // without leading/trailing spaces.
        ///
        // Note: Parse defaults to NumberStyles.Integer =  AllowLeadingWhite ||| AllowTrailingWhite ||| AllowLeadingSign
        // However, that is not the required behaviour of 'int32', 'int64' etc. when used on string
        // arguments: we explicitly disallow AllowLeadingWhite ||| AllowTrailingWhite 
        // and only request AllowLeadingSign.

        let isOXB c = 
            let c = System.Char.ToLowerInvariant c
            charEq c 'x' || charEq c 'o' || charEq c 'b'

        let is0OXB (s:string) p l = 
            l >= p + 2 && charEq (s.Chars(p)) '0' && isOXB (s.Chars(p+1))

        let get0OXB (s:string) (p:byref<int>)  l = 
            if is0OXB s p l
            then let r = System.Char.ToLowerInvariant(s.Chars(p+1)) in p <- p + 2; r
            else 'd' 

        let getSign32 (s:string) (p:byref<int>) l = 
            if (l >= p + 1 && charEq (s.Chars(p)) '-') 
            then p <- p + 1; -1
            else 1 

        let getSign64 (s:string) (p:byref<int>) l = 
            if (l >= p + 1 && charEq (s.Chars(p)) '-') 
            then p <- p + 1; -1L
            else 1L 

        let parseBinaryUInt64 (s:string) = 
            Convert.ToUInt64(s, 2)
        
        let parseOctalUInt64 (s:string) =
            Convert.ToUInt64(s, 8)

        let inline removeUnderscores (s:string) =
            match s with
            | null -> null
            | s -> s.Replace("_", "")

        let ParseUInt32 (s:string) = 
            if System.Object.ReferenceEquals(s,null) then
                raise( new System.ArgumentNullException("s") )
            let s = removeUnderscores (s.Trim())
            let l = s.Length 
            let mutable p = 0 
            let specifier = get0OXB s &p l 
            if p >= l then formatError() else
            match specifier with 
            | 'x' -> UInt32.Parse( s.Substring(p), NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture)
            | 'b' -> Convert.ToUInt32(parseBinaryUInt64 (s.Substring(p)))
            | 'o' -> Convert.ToUInt32(parseOctalUInt64  (s.Substring(p)))
            | _ -> UInt32.Parse(s.Substring(p), NumberStyles.Integer, CultureInfo.InvariantCulture) in

        let inline int32OfUInt32 (x:uint32) = (# "" x  : int32 #)
        let inline int64OfUInt64 (x:uint64) = (# "" x  : int64 #)

        let ParseInt32 (s:string) = 
            if System.Object.ReferenceEquals(s,null) then
                raise( new System.ArgumentNullException("s") )
            let s = removeUnderscores (s.Trim())
            let l = s.Length 
            let mutable p = 0 
            let sign = getSign32 s &p l 
            let specifier = get0OXB s &p l 
            if p >= l then formatError() else
            match Char.ToLowerInvariant(specifier) with 
            | 'x' -> sign * (int32OfUInt32 (Convert.ToUInt32(UInt64.Parse(s.Substring(p), NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture))))
            | 'b' -> sign * (int32OfUInt32 (Convert.ToUInt32(parseBinaryUInt64 (s.Substring(p)))))
            | 'o' -> sign * (int32OfUInt32 (Convert.ToUInt32(parseOctalUInt64  (s.Substring(p)))))
            | _ -> Int32.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)

        let ParseInt64 (s:string) = 
            if System.Object.ReferenceEquals(s,null) then
                raise( new System.ArgumentNullException("s") )
            let s = removeUnderscores (s.Trim())
            let l = s.Length 
            let mutable p = 0 
            let sign = getSign64 s &p l 
            let specifier = get0OXB s &p l 
            if p >= l then formatError() else
            match Char.ToLowerInvariant(specifier) with 
            | 'x' -> sign *. Int64.Parse(s.Substring(p), NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture)
            | 'b' -> sign *. (int64OfUInt64 (parseBinaryUInt64 (s.Substring(p))))
            | 'o' -> sign *. (int64OfUInt64 (parseOctalUInt64  (s.Substring(p))))
            | _ -> Int64.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)

        let ParseUInt64     (s:string) : uint64 = 
            if System.Object.ReferenceEquals(s,null) then
                raise( new System.ArgumentNullException("s") )
            let s = removeUnderscores (s.Trim())
            let l = s.Length 
            let mutable p = 0 
            let specifier = get0OXB s &p l 
            if p >= l then formatError() else
            match specifier with 
            | 'x' -> UInt64.Parse(s.Substring(p), NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture)
            | 'b' -> parseBinaryUInt64 (s.Substring(p))
            | 'o' -> parseOctalUInt64  (s.Substring(p))
            | _ -> UInt64.Parse(s.Substring(p), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) 

        let inline ParseByte (s:string)       = (# "conv.ovf.u1" (ParseUInt32 s) : byte #)

        let inline ParseSByte (s:string)      = (# "conv.ovf.i1" (ParseInt32 s)  : sbyte #)

        let inline ParseInt16 (s:string)      = (# "conv.ovf.i2" (ParseInt32 s)  : int16 #)

        let inline ParseUInt16 (s:string)     = (# "conv.ovf.u2" (ParseUInt32 s) : uint16 #)

        let inline ParseIntPtr (s:string)  = (# "conv.ovf.i"  (ParseInt64 s)  : nativeint #)

        let inline ParseUIntPtr (s:string) = (# "conv.ovf.u"  (ParseInt64 s)  : unativeint #)

        let inline ParseDouble (s:string)   = Double.Parse(removeUnderscores s,NumberStyles.Float, CultureInfo.InvariantCulture)

        let inline ParseSingle (s:string) = Single.Parse(removeUnderscores s,NumberStyles.Float, CultureInfo.InvariantCulture)
            
        [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
        type GenericZeroDynamicImplTable<'T>() = 
            static let result : 'T = 
                // The dynamic implementation
                let aty = typeof<'T>
                if   aty.Equals(typeof<sbyte>)      then unboxPrim<'T> (box 0y)
                elif aty.Equals(typeof<int16>)      then unboxPrim<'T> (box 0s)
                elif aty.Equals(typeof<int32>)      then unboxPrim<'T> (box 0)
                elif aty.Equals(typeof<int64>)      then unboxPrim<'T> (box 0L)
                elif aty.Equals(typeof<nativeint>)  then unboxPrim<'T> (box 0n)
                elif aty.Equals(typeof<byte>)       then unboxPrim<'T> (box 0uy)
                elif aty.Equals(typeof<uint16>)     then unboxPrim<'T> (box 0us)
                elif aty.Equals(typeof<char>)       then unboxPrim<'T> (box '\000')
                elif aty.Equals(typeof<uint32>)     then unboxPrim<'T> (box 0u)
                elif aty.Equals(typeof<uint64>)     then unboxPrim<'T> (box 0UL)
                elif aty.Equals(typeof<unativeint>) then unboxPrim<'T> (box 0un)
                elif aty.Equals(typeof<decimal>)    then unboxPrim<'T> (box 0M)
                elif aty.Equals(typeof<float>)      then unboxPrim<'T> (box 0.0)
                elif aty.Equals(typeof<float32>)    then unboxPrim<'T> (box 0.0f)
                else 
                   let pinfo = aty.GetProperty("Zero")
                   unboxPrim<'T> (pinfo.GetValue(null,null))
            static member Result : 'T = result
                   
        [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
        type GenericOneDynamicImplTable<'T>() = 
            static let result : 'T = 
                // The dynamic implementation
                let aty = typeof<'T>
                if   aty.Equals(typeof<sbyte>)      then unboxPrim<'T> (box 1y)
                elif aty.Equals(typeof<int16>)      then unboxPrim<'T> (box 1s)
                elif aty.Equals(typeof<int32>)      then unboxPrim<'T> (box 1)
                elif aty.Equals(typeof<int64>)      then unboxPrim<'T> (box 1L)
                elif aty.Equals(typeof<nativeint>)  then unboxPrim<'T> (box 1n)
                elif aty.Equals(typeof<byte>)       then unboxPrim<'T> (box 1uy)
                elif aty.Equals(typeof<uint16>)     then unboxPrim<'T> (box 1us)
                elif aty.Equals(typeof<char>)       then unboxPrim<'T> (box '\001')
                elif aty.Equals(typeof<uint32>)     then unboxPrim<'T> (box 1u)
                elif aty.Equals(typeof<uint64>)     then unboxPrim<'T> (box 1UL)
                elif aty.Equals(typeof<unativeint>) then unboxPrim<'T> (box 1un)
                elif aty.Equals(typeof<decimal>)    then unboxPrim<'T> (box 1M)
                elif aty.Equals(typeof<float>)      then unboxPrim<'T> (box 1.0)
                elif aty.Equals(typeof<float32>)    then unboxPrim<'T> (box 1.0f)
                else 
                   let pinfo = aty.GetProperty("One")
                   unboxPrim<'T> (pinfo.GetValue(null,null))

            static member Result : 'T = result

        let GenericZeroDynamic<'T>() : 'T = GenericZeroDynamicImplTable<'T>.Result
        let GenericOneDynamic<'T>() : 'T = GenericOneDynamicImplTable<'T>.Result

        let inline GenericZero< ^T when ^T : (static member Zero : ^T) > : ^T =
            GenericZeroDynamic<(^T)>()
            when ^T : int32       = 0
            when ^T : float       = 0.0
            when ^T : float32     = 0.0f
            when ^T : int64       = 0L
            when ^T : uint64      = 0UL
            when ^T : uint32      = 0ul
            when ^T : nativeint   = 0n
            when ^T : unativeint  = 0un
            when ^T : int16       = 0s
            when ^T : uint16      = 0us
            when ^T : char        = '\000'
            when ^T : sbyte       = 0y
            when ^T : byte        = 0uy
            when ^T : decimal     = 0M
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type
            when ^T : ^T = (^T : (static member Zero : ^T) ())


        let inline GenericOne< ^T when ^T : (static member One : ^T) > : ^T =
            GenericOneDynamic<(^T)>()
            when ^T : int32       = 1
            when ^T : float       = 1.0
            when ^T : float32     = 1.0f
            when ^T : int64       = 1L
            when ^T : uint64      = 1UL
            when ^T : uint32      = 1ul
            when ^T : nativeint   = 1n
            when ^T : unativeint  = 1un
            when ^T : int16       = 1s
            when ^T : uint16      = 1us
            when ^T : char        = '\001'
            when ^T : sbyte       = 1y
            when ^T : byte        = 1uy
            when ^T : decimal     = 1M
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type
             // That is, not in the generic implementation of '+'
            when ^T : ^T = (^T : (static member One : ^T) ())

        type System.Type with
    
            member inline this.GetSingleStaticMethodByTypes(name: string, parameterTypes: Type[]) =
               let staticBindingFlags = (# "" 0b111000 : BindingFlags #) // BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic
               this.GetMethod(name, staticBindingFlags, null, parameterTypes, null )

        let UnaryDynamicImpl nm : ('T -> 'U) =
             let aty = typeof<'T>
             let minfo = aty.GetSingleStaticMethodByTypes(nm, [| aty |])
             (fun x -> unboxPrim<_>(minfo.Invoke(null,[| box x|])))

        let BinaryDynamicImpl nm : ('T -> 'U -> 'V) =
             let aty = typeof<'T>
             let bty = typeof<'U>
             let minfo = aty.GetSingleStaticMethodByTypes(nm, [| aty; bty |])
             (fun x y -> unboxPrim<_>(minfo.Invoke(null,[| box x; box y|])))

        // Legacy dynamic implementation of operator resolution if no built in solution is used and no witness passed
        type UnaryOpDynamicImplTable<'OpInfo,'T,'U>() = 
            static let mutable meth : MethodInfo = null

            static member Invoke opName (x: 'T) : 'U = 
                // The dynamic implementation
                let aty = typeof<'T>

                match meth with 
                | null ->
                    let ameth = aty.GetSingleStaticMethodByTypes(opName, [| aty |])
                    match ameth with
                    | null -> raise (NotSupportedException (SR.GetString(SR.dyInvOpAddCoerce)))
                    | res -> 
                        meth <- res
                | _ -> ()
                unboxPrim<'U> (meth.Invoke(null,[| box x |]))

        // Legacy dynamic implementation of operator resolution, if no built in solution is used and no witness passed
        type BinaryOpDynamicImplTable<'OpInfo,'T1,'T2,'U>() = 
            static let mutable meth : MethodInfo = null

            static member Invoke opName (x: 'T1) (y: 'T2) : 'U =
                match meth with 
                | null -> 
                    // The dynamic implementation
                    let aty = typeof<'T1>
                    let bty = typeof<'T2>

                    let ameth = aty.GetSingleStaticMethodByTypes(opName, [| aty; bty |])
                    let bmeth = 
                        if aty.Equals(bty) then null else 
                        bty.GetSingleStaticMethodByTypes(opName, [| aty; bty |])
                    match ameth, bmeth with
                    | null, null -> raise (NotSupportedException (SR.GetString(SR.dyInvOpAddCoerce)))
                    | m, null | null, m ->
                        meth <- m
                    | _ -> raise (NotSupportedException (SR.GetString(SR.dyInvOpAddOverload)))
                | _ -> ()
                unboxPrim<'U> (meth.Invoke(null,[| box x; box y |]))

        type OpAdditionInfo = class end

        let AdditionDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "add" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "add" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "add" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "add" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "add" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "add" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "add" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "add" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.i2" (# "add" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.u2" (# "add" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, char> then convPrim<_,'U> (# "conv.u2" (# "add" (convPrim<_,char> x) (convPrim<_,char> y) : uint32 #) : char #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.i1" (# "add" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.u1" (# "add" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, string> then convPrim<_,'U> (String.Concat(convPrim<_,string> x, convPrim<_,string> y))
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Addition(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpAdditionInfo, 'T1, 'T2, 'U>.Invoke "op_Addition" x y

        type OpSubtractionInfo = class end 
        let SubtractionDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "sub" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "sub" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "sub" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "sub" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "sub" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "sub" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "sub" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "sub" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.i2" (# "sub" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.u2" (# "sub" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.i1" (# "sub" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.u1" (# "sub" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Subtraction(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpSubtractionInfo, 'T1, 'T2, 'U>.Invoke "op_Subtraction" x y

        type OpMultiplyInfo = class end
        let MultiplyDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U,  int32> then convPrim<_,'U> (# "mul" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U,  float> then convPrim<_,'U> (# "mul" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U,  float32> then convPrim<_,'U> (# "mul" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U,  int64> then convPrim<_,'U> (# "mul" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U,  uint64> then convPrim<_,'U> (# "mul" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U,  uint32> then convPrim<_,'U> (# "mul" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U,  nativeint> then convPrim<_,'U> (# "mul" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U,  unativeint> then convPrim<_,'U> (# "mul" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U,  int16> then convPrim<_,'U> (# "conv.i2" (# "mul" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U,  uint16> then convPrim<_,'U> (# "conv.u2" (# "mul" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U,  sbyte> then convPrim<_,'U> (# "conv.i1" (# "mul" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U,  byte> then convPrim<_,'U> (# "conv.u1" (# "mul" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U,  decimal> then convPrim<_,'U> (Decimal.op_Multiply(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpMultiplyInfo, 'T1, 'T2, 'U>.Invoke "op_Multiply" x y

        type OpDivisionInfo = class end
        let DivisionDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U,int32> then convPrim<_,'U> (# "div" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U,float> then convPrim<_,'U> (# "div" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U,float32> then convPrim<_,'U> (# "div" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U,int64> then convPrim<_,'U> (# "div" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U,uint64> then convPrim<_,'U> (# "div.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U,uint32> then convPrim<_,'U> (# "div.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U,nativeint> then convPrim<_,'U> (# "div" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U,unativeint> then convPrim<_,'U> (# "div.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U,int16> then convPrim<_,'U> (# "conv.i2" (# "div" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U,uint16> then convPrim<_,'U> (# "conv.u2" (# "div.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U,sbyte> then convPrim<_,'U> (# "conv.i1" (# "div" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U,byte> then convPrim<_,'U> (# "conv.u1" (# "div.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Division(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpDivisionInfo, 'T1, 'T2, 'U>.Invoke "op_Division" x y

        type OpModulusInfo = class end
        let ModulusDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U  =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "rem" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "rem" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "rem" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "rem" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "rem.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "rem.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "rem" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "rem.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.i2" (# "rem" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.u2" (# "rem.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.i1" (# "rem" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.u1" (# "rem.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Modulus(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpModulusInfo, 'T1, 'T2, 'U>.Invoke "op_Modulus" x y

        type OpUnaryNegationInfo = class end
        let UnaryNegationDynamic<'T,'U> (value: 'T) : 'U =
            if type2eq<'T, 'U, int32> then convPrim<_,'U> (# "neg" (convPrim<_,int32> value) : int32 #) 
            elif type2eq<'T, 'U, float> then convPrim<_,'U> (# "neg" (convPrim<_,float> value) : float #) 
            elif type2eq<'T, 'U, float32> then convPrim<_,'U> (# "neg" (convPrim<_,float32> value) : float32 #) 
            elif type2eq<'T, 'U, int64> then convPrim<_,'U> (# "neg" (convPrim<_,int64> value) : int64 #) 
            elif type2eq<'T, 'U, nativeint> then convPrim<_,'U> (# "neg" (convPrim<_,nativeint> value) : nativeint #) 
            elif type2eq<'T, 'U, int16> then convPrim<_,'U> (# "conv.i2" (# "neg" (convPrim<_,int16> value) : int32 #) : int16 #)
            elif type2eq<'T, 'U, sbyte> then convPrim<_,'U> (# "conv.i1" (# "neg" (convPrim<_,sbyte> value) : int32 #) : sbyte #)
            elif type2eq<'T, 'U, decimal> then convPrim<_,'U> (Decimal.op_UnaryNegation(convPrim<_,decimal> value))
            else UnaryOpDynamicImplTable<OpUnaryNegationInfo,'T,'U>.Invoke "op_UnaryNegation" value

        type OpCheckedAdditionInfo = class end
        let CheckedAdditionDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "add.ovf" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "add" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "add" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "add.ovf" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "add.ovf.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "add.ovf.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "add.ovf" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "add.ovf.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.ovf.i2" (# "add.ovf" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.ovf.u2.un" (# "add.ovf.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, char> then convPrim<_,'U> (# "conv.ovf.u2.un" (# "add.ovf.un" (convPrim<_,char> x) (convPrim<_,char> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.ovf.i1" (# "add.ovf" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.ovf.u1.un" (# "add.ovf.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, string> then convPrim<_,'U> (String.Concat(convPrim<_,string> x, convPrim<_,string> y))
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Addition(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpCheckedAdditionInfo, 'T1, 'T2, 'U>.Invoke "op_Addition" x y

        type OpCheckedSubtractionInfo = class end
        let CheckedSubtractionDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "sub.ovf" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "sub" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "sub" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "sub.ovf" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "sub.ovf.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "sub.ovf.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "sub.ovf" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "sub.ovf.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.ovf.i2" (# "sub.ovf" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.ovf.u2.un" (# "sub.ovf.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.ovf.i1" (# "sub.ovf" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.ovf.u1.un" (# "sub.ovf.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Subtraction(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpCheckedSubtractionInfo, 'T1, 'T2, 'U>.Invoke "op_Subtraction" x y

        type OpCheckedMultiplyInfo = class end
        let CheckedMultiplyDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "mul.ovf" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, float> then convPrim<_,'U> (# "mul" (convPrim<_,float> x) (convPrim<_,float> y) : float #) 
            elif type3eq<'T1, 'T2, 'U, float32> then convPrim<_,'U> (# "mul" (convPrim<_,float32> x) (convPrim<_,float32> y) : float32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "mul.ovf" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "mul.ovf.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "mul.ovf.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "mul.ovf" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "mul.ovf.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "conv.ovf.i2" (# "mul.ovf" (convPrim<_,int16> x) (convPrim<_,int16> y) : int32 #) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "conv.ovf.u2.un" (# "mul.ovf.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint32 #) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "conv.ovf.i1" (# "mul.ovf" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : int32 #) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "conv.ovf.u1.un" (# "mul.ovf.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : uint32 #) : byte #)
            elif type3eq<'T1, 'T2, 'U, decimal> then convPrim<_,'U> (Decimal.op_Multiply(convPrim<_,decimal> x, convPrim<_,decimal> y))
            else BinaryOpDynamicImplTable<OpCheckedMultiplyInfo, 'T1, 'T2, 'U>.Invoke "op_Multiply" x y

        type OpCheckedUnaryNegationInfo = class end
        let CheckedUnaryNegationDynamic<'T,'U> value =
            if type2eq<'T, 'U, int32> then convPrim<_,'U> (# "sub.ovf" 0 (convPrim<_,int32> value) : int32 #) 
            elif type2eq<'T, 'U, float> then convPrim<_,'U> (# "neg" (convPrim<_,float> value) : float #) 
            elif type2eq<'T, 'U, float32> then convPrim<_,'U> (# "neg" (convPrim<_,float32> value) : float32 #) 
            elif type2eq<'T, 'U, int64> then convPrim<_,'U> (# "sub.ovf" 0L (convPrim<_,int64> value) : int64 #) 
            elif type2eq<'T, 'U, nativeint> then convPrim<_,'U> (# "sub.ovf" 0n (convPrim<_,nativeint> value) : nativeint #) 
            elif type2eq<'T, 'U, int16> then convPrim<_,'U> (# "sub.ovf" 0s (convPrim<_,int16> value) : int16 #)
            elif type2eq<'T, 'U, sbyte> then convPrim<_,'U> (# "sub.ovf" 0y (convPrim<_,sbyte> value) : sbyte #)
            elif type2eq<'T, 'U, decimal> then convPrim<_,'U> (Decimal.op_UnaryNegation(convPrim<_,decimal> value))
            else UnaryOpDynamicImplTable<OpCheckedUnaryNegationInfo,'T,'U>.Invoke "op_UnaryNegation" value

        type OpLeftShiftInfo = class end
        let LeftShiftDynamic<'T1, 'T2, 'U> (value: 'T1) (shift: 'T2) : 'U =
            if type2eq<'T1, 'U, sbyte> && typeeq<'T2, int> then convPrim<_,'U> (# "conv.i1" (# "shl" (convPrim<_,sbyte> value) (mask (convPrim<_,int32> shift) 7) : int32 #) : sbyte #)
            elif type2eq<'T1, 'U, byte> && typeeq<'T2, int> then convPrim<_,'U> (# "conv.u1" (# "shl" (convPrim<_,byte> value) (mask (convPrim<_,int32> shift) 7) : uint32 #) : byte #)
            elif type2eq<'T1, 'U, int16> && typeeq<'T2, int> then convPrim<_,'U> (# "conv.i2" (# "shl" (convPrim<_,int16> value) (mask (convPrim<_,int32> shift) 15) : int32 #) : int16 #)
            elif type2eq<'T1, 'U, uint16> && typeeq<'T2, int> then convPrim<_,'U> (# "conv.u2" (# "shl" (convPrim<_,uint16> value) (mask (convPrim<_,int32> shift) 15) : uint32 #) : uint16 #)
            elif type2eq<'T1, 'U, int32> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,int32> value) (mask (convPrim<_,int32> shift) 31) : int32 #) 
            elif type2eq<'T1, 'U, uint32> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,uint32> value) (mask (convPrim<_,int32> shift) 31) : uint32 #) 
            elif type2eq<'T1, 'U, int64> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,int64> value) (mask (convPrim<_,int32> shift) 63) : int64 #) 
            elif type2eq<'T1, 'U, uint64> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,uint64> value)  (mask (convPrim<_,int32> shift) 63) : uint64 #) 
            elif type2eq<'T1, 'U, nativeint> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,nativeint> value) (convPrim<_,int32> shift) : nativeint #) 
            elif type2eq<'T1, 'U, unativeint> && typeeq<'T2, int> then convPrim<_,'U> (# "shl" (convPrim<_,unativeint> value) (convPrim<_,int32> shift) : unativeint #) 
            else BinaryOpDynamicImplTable<OpLeftShiftInfo, 'T1, 'T2, 'U>.Invoke "op_LeftShift" value shift

        type OpRightShiftInfo = class end
        let RightShiftDynamic<'T1, 'T2, 'U> (value: 'T1) (shift: 'T2) : 'U =
            if type2eq<'T1, 'U, sbyte> && typeeq<'T2, int> then convPrim<_,'U> (# "shr" (convPrim<_,sbyte> value) (mask (convPrim<_,int32> shift) 7) : sbyte #)
            elif type2eq<'T1, 'U, byte> && typeeq<'T2, int> then convPrim<_,'U> (# "shr.un" (convPrim<_,byte> value) (mask (convPrim<_,int32> shift) 7) : byte #)
            elif type2eq<'T1, 'U, int16> && typeeq<'T2, int> then convPrim<_,'U> (# "shr" (convPrim<_,int16> value) (mask (convPrim<_,int32> shift) 15): int16 #)
            elif type2eq<'T1, 'U, uint16> && typeeq<'T2, int> then convPrim<_,'U> (# "shr.un" (convPrim<_,uint16> value) (mask (convPrim<_,int32> shift) 15) : uint16 #)
            elif type2eq<'T1, 'U, int32> && typeeq<'T2, int> then convPrim<_,'U> (# "shr" (convPrim<_,int32> value) (mask (convPrim<_,int32> shift) 31) : int32 #) 
            elif type2eq<'T1, 'U, uint32> && typeeq<'T2, int> then convPrim<_,'U> (# "shr.un" (convPrim<_,uint32> value) (mask (convPrim<_,int32> shift) 31) : uint32 #) 
            elif type2eq<'T1, 'U, int64> && typeeq<'T2, int> then convPrim<_,'U> (# "shr" (convPrim<_,int64> value) (mask (convPrim<_,int32> shift) 63) : int64 #) 
            elif type2eq<'T1, 'U, uint64> && typeeq<'T2, int> then convPrim<_,'U> (# "shr.un" (convPrim<_,uint64> value)  (mask (convPrim<_,int32> shift) 63) : uint64 #) 
            elif type2eq<'T1, 'U, nativeint> && typeeq<'T2, int> then convPrim<_,'U> (# "shr" (convPrim<_,nativeint> value) (convPrim<_,int32> shift) : nativeint #) 
            elif type2eq<'T1, 'U, unativeint> && typeeq<'T2, int> then convPrim<_,'U> (# "shr.un" (convPrim<_,unativeint> value) (convPrim<_,int32> shift) : unativeint #) 
            else BinaryOpDynamicImplTable<OpRightShiftInfo, 'T1, 'T2, 'U>.Invoke "op_RightShift" value shift

        type OpBitwiseAndInfo = class end
        let BitwiseAndDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "and" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "and" (convPrim<_,byte> x) (convPrim<_,byte> y) : byte #)
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "and" (convPrim<_,int16> x) (convPrim<_,int16> y) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "and" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "and" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "and" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "and" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "and" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "and" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "and" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            else BinaryOpDynamicImplTable<OpBitwiseAndInfo, 'T1, 'T2, 'U>.Invoke "op_BitwiseAnd" x y

        type OpBitwiseOrInfo = class end
        let BitwiseOrDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "or" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "or" (convPrim<_,byte> x) (convPrim<_,byte> y) : byte #)
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "or" (convPrim<_,int16> x) (convPrim<_,int16> y) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "or" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "or" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "or" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "or" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "or" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "or" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "or" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            else BinaryOpDynamicImplTable<OpBitwiseOrInfo, 'T1, 'T2, 'U>.Invoke "op_BitwiseOr" x y

        type OpExclusiveOrInfo = class end
        let ExclusiveOrDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type3eq<'T1, 'T2, 'U, sbyte> then convPrim<_,'U> (# "xor" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : sbyte #)
            elif type3eq<'T1, 'T2, 'U, byte> then convPrim<_,'U> (# "xor" (convPrim<_,byte> x) (convPrim<_,byte> y) : byte #)
            elif type3eq<'T1, 'T2, 'U, int16> then convPrim<_,'U> (# "xor" (convPrim<_,int16> x) (convPrim<_,int16> y) : int16 #)
            elif type3eq<'T1, 'T2, 'U, uint16> then convPrim<_,'U> (# "xor" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : uint16 #)
            elif type3eq<'T1, 'T2, 'U, int32> then convPrim<_,'U> (# "xor" (convPrim<_,int32> x) (convPrim<_,int32> y) : int32 #) 
            elif type3eq<'T1, 'T2, 'U, uint32> then convPrim<_,'U> (# "xor" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : uint32 #) 
            elif type3eq<'T1, 'T2, 'U, int64> then convPrim<_,'U> (# "xor" (convPrim<_,int64> x) (convPrim<_,int64> y) : int64 #) 
            elif type3eq<'T1, 'T2, 'U, uint64> then convPrim<_,'U> (# "xor" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : uint64 #) 
            elif type3eq<'T1, 'T2, 'U, nativeint> then convPrim<_,'U> (# "xor" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : nativeint #) 
            elif type3eq<'T1, 'T2, 'U, unativeint> then convPrim<_,'U> (# "xor" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : unativeint #) 
            else BinaryOpDynamicImplTable<OpExclusiveOrInfo, 'T1, 'T2, 'U>.Invoke "op_ExclusiveOr" x y

        type OpLogicalNotInfo = class end
        let LogicalNotDynamic<'T,'U> (value: 'T) : 'U =
            if type2eq<'T, 'U, sbyte> then convPrim<_,'U> (# "conv.i1" (# "not" (convPrim<_,sbyte> value) : int32 #) : sbyte #)
            elif type2eq<'T, 'U, byte> then convPrim<_,'U> (# "conv.u1" (# "not" (convPrim<_,byte> value) : uint32 #) : byte #)
            elif type2eq<'T, 'U, int16> then convPrim<_,'U> (# "conv.i2" (# "not" (convPrim<_,int16> value) : int32 #) : int16 #)
            elif type2eq<'T, 'U, uint16> then convPrim<_,'U> (# "conv.u2" (# "not" (convPrim<_,uint16> value) : uint32 #) : uint16 #)
            elif type2eq<'T, 'U, int32> then convPrim<_,'U> (# "not" (convPrim<_,int32> value) : int32 #) 
            elif type2eq<'T, 'U, uint32> then convPrim<_,'U> (# "not" (convPrim<_,uint32> value) : uint32 #) 
            elif type2eq<'T, 'U, int64> then convPrim<_,'U> (# "not" (convPrim<_,int64> value) : int64 #) 
            elif type2eq<'T, 'U, uint64> then convPrim<_,'U> (# "not" (convPrim<_,uint64> value) : uint64 #) 
            elif type2eq<'T, 'U, nativeint> then convPrim<_,'U> (# "not" (convPrim<_,nativeint> value) : nativeint #) 
            elif type2eq<'T, 'U, unativeint> then convPrim<_,'U> (# "not" (convPrim<_,unativeint> value) : unativeint #) 
            else UnaryOpDynamicImplTable<OpLogicalNotInfo,'T,'U>.Invoke "op_LogicalNot" value

        type OpExplicitInfo = class end
        let ExplicitDynamic<'T, 'U> (value: 'T) : 'U =
            if typeeq<'U, byte> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.u1" (convPrim<_,sbyte> value) : byte #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u1" (convPrim<_,byte> value) : byte #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.u1" (convPrim<_,int16> value) : byte #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u1" (convPrim<_,uint16> value) : byte #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.u1" (convPrim<_,int32> value) : byte #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u1" (convPrim<_,uint32> value) : byte #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.u1" (convPrim<_,int64> value) : byte #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u1" (convPrim<_,uint64> value) : byte #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.u1" (convPrim<_,nativeint> value) : byte #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u1" (convPrim<_,unativeint> value) : byte #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u1" (convPrim<_,float> value) : byte #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u1" (convPrim<_,float32> value) : byte #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u1" (convPrim<_,char> value) : byte #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseByte (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, sbyte> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i1" (convPrim<_,sbyte> value) : sbyte #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.i1" (convPrim<_,byte> value) : sbyte #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i1" (convPrim<_,int16> value) : sbyte #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.i1" (convPrim<_,uint16> value) : sbyte #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i1" (convPrim<_,int32> value) : sbyte #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.i1" (convPrim<_,uint32> value) : sbyte #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.i1" (convPrim<_,int64> value) : sbyte #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.i1" (convPrim<_,uint64> value) : sbyte #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.i1" (convPrim<_,nativeint> value) : sbyte #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.i1" (convPrim<_,unativeint> value) : sbyte #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.i1" (convPrim<_,float> value) : sbyte #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.i1" (convPrim<_,float32> value) : sbyte #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.i1" (convPrim<_,char> value) : sbyte #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseSByte (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, uint16> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.u2" (convPrim<_,sbyte> value) : uint16 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u2" (convPrim<_,byte> value) : uint16 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int16> value) : uint16 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint16> value) : uint16 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int32> value) : uint16 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint32> value) : uint16 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int64> value) : uint16 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint64> value) : uint16 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.u2" (convPrim<_,nativeint> value) : uint16 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u2" (convPrim<_,unativeint> value) : uint16 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u2" (convPrim<_,float> value) : uint16 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,float32> value) : uint16 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u2" (convPrim<_,char> value) : uint16 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseUInt16 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, int16> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i2" (convPrim<_,sbyte> value) : int16 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.i2" (convPrim<_,byte> value) : int16 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i2" (convPrim<_,int16> value) : int16 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.i2" (convPrim<_,uint16> value) : int16 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i2" (convPrim<_,int32> value) : int16 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.i2" (convPrim<_,uint32> value) : int16 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.i2" (convPrim<_,int64> value) : int16 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.i2" (convPrim<_,uint64> value) : int16 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.i2" (convPrim<_,nativeint> value) : int16 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.i2" (convPrim<_,unativeint> value) : int16 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.i2" (convPrim<_,float> value) : int16 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.i2" (convPrim<_,float32> value) : int16 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.i2" (convPrim<_,char> value) : int16 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseInt16 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, uint32> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.u4" (convPrim<_,sbyte> value) : uint32 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u4" (convPrim<_,byte> value) : uint32 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.u4" (convPrim<_,int16> value) : uint32 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u4" (convPrim<_,uint16> value) : uint32 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.u4" (convPrim<_,int32> value) : uint32 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u4" (convPrim<_,uint32> value) : uint32 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.u4" (convPrim<_,int64> value) : uint32 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u4" (convPrim<_,uint64> value) : uint32 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.u4" (convPrim<_,nativeint> value) : uint32 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u4" (convPrim<_,unativeint> value) : uint32 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u4" (convPrim<_,float> value) : uint32 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u4" (convPrim<_,float32> value) : uint32 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u4" (convPrim<_,char> value) : uint32 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseUInt32 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, int32> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i4" (convPrim<_,sbyte> value) : int32 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.i4" (convPrim<_,byte> value) : int32 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i4" (convPrim<_,int16> value) : int32 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.i4" (convPrim<_,uint16> value) : int32 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i4" (convPrim<_,int32> value) : int32 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.i4" (convPrim<_,uint32> value) : int32 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.i4" (convPrim<_,int64> value) : int32 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.i4" (convPrim<_,uint64> value) : int32 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.i4" (convPrim<_,nativeint> value) : int32 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.i4" (convPrim<_,unativeint> value) : int32 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.i4" (convPrim<_,float> value) : int32 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.i4" (convPrim<_,float32> value) : int32 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.i4" (convPrim<_,char> value) : int32 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseInt32 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, uint64> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i8" (convPrim<_,sbyte> value) : uint64 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u8" (convPrim<_,byte> value) : uint64 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i8" (convPrim<_,int16> value) : uint64 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u8" (convPrim<_,uint16> value) : uint64 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i8" (convPrim<_,int32> value) : uint64 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u8" (convPrim<_,uint32> value) : uint64 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "" (convPrim<_,int64> value) : uint64 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.i8" (convPrim<_,uint64> value) : uint64 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.i8" (convPrim<_,nativeint> value) : uint64 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u8" (convPrim<_,unativeint> value) : uint64 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u8" (convPrim<_,float> value) : uint64 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u8" (convPrim<_,float32> value) : uint64 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u8" (convPrim<_,char> value) : uint64 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseUInt64 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, int64> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i8" (convPrim<_,sbyte> value) : int64 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u8" (convPrim<_,byte> value) : int64 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i8" (convPrim<_,int16> value) : int64 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u8" (convPrim<_,uint16> value) : int64 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i8" (convPrim<_,int32> value) : int64 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u8" (convPrim<_,uint32> value) : int64 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (convPrim<_,int64> value) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "" (convPrim<_,uint64> value) : int64 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.i8" (convPrim<_,nativeint> value) : int64 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u8" (convPrim<_,unativeint> value) : int64 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u8" (convPrim<_,float> value) : int64 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u8" (convPrim<_,float32> value) : int64 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u8" (convPrim<_,char> value) : int64 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseInt64 (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, float32> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.r4" (convPrim<_,sbyte> value) : float32 #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,byte> value) : float32 #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.r4" (convPrim<_,int16> value) : float32 #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,uint16> value) : float32 #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.r4" (convPrim<_,int32> value) : float32 #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,uint32> value) : float32 #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.r4" (convPrim<_,int64> value) : float32 #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,uint64> value) : float32 #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.r4" (convPrim<_,nativeint> value) : float32 #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,unativeint> value) : float32 #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.r4" (convPrim<_,float> value) : float32 #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.r4" (convPrim<_,float32> value) : float32 #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.r.un conv.r4" (convPrim<_,char> value) : float32 #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseSingle (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, float> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.r8" (convPrim<_,sbyte> value) : float #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,byte> value) : float #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.r8" (convPrim<_,int16> value) : float #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,uint16> value) : float #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.r8" (convPrim<_,int32> value) : float #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,uint32> value) : float #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.r8" (convPrim<_,int64> value) : float #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,uint64> value) : float #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.r8" (convPrim<_,nativeint> value) : float #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,unativeint> value) : float #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.r8" (convPrim<_,float> value) : float #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.r8" (convPrim<_,float32> value) : float #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.r.un conv.r8" (convPrim<_,char> value) : float #) 
                elif typeeq<'T, decimal> then convPrim<_,'U> (Convert.ToDouble(convPrim<_,decimal> value))
                elif typeeq<'T, string> then convPrim<_,'U> (ParseDouble (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, unativeint> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i" (convPrim<_,sbyte> value) : unativeint #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u" (convPrim<_,byte> value) : unativeint #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i" (convPrim<_,int16> value) : unativeint #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint16> value) : unativeint #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i" (convPrim<_,int32> value) : unativeint #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint32> value) : unativeint #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.i" (convPrim<_,int64> value) : unativeint #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint64> value) : unativeint #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "" (convPrim<_,nativeint> value) : unativeint #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "" (convPrim<_,unativeint> value) : unativeint #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u" (convPrim<_,float> value) : unativeint #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u" (convPrim<_,float32> value) : unativeint #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u" (convPrim<_,char> value) : unativeint #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseUIntPtr (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, nativeint> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.i" (convPrim<_,sbyte> value) : nativeint #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u" (convPrim<_,byte> value) : nativeint #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.i" (convPrim<_,int16> value) : nativeint #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint16> value) : nativeint #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.i" (convPrim<_,int32> value) : nativeint #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint32> value) : nativeint #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.i" (convPrim<_,int64> value) : nativeint #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u" (convPrim<_,uint64> value) : nativeint #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "" (convPrim<_,nativeint> value) : nativeint #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "" (convPrim<_,unativeint> value) : nativeint #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.i" (convPrim<_,float> value) : nativeint #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.i" (convPrim<_,float32> value) : nativeint #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u" (convPrim<_,char> value) : nativeint #) 
                elif typeeq<'T, string> then convPrim<_,'U> (ParseIntPtr (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, char> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (# "conv.u2" (convPrim<_,sbyte> value) : char #)
                elif typeeq<'T, byte> then convPrim<_,'U> (# "conv.u2" (convPrim<_,byte> value) : char #)
                elif typeeq<'T, int16> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int16> value) : char #)
                elif typeeq<'T, uint16> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint16> value) : char #)
                elif typeeq<'T, int32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int32> value) : char #) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint32> value) : char #) 
                elif typeeq<'T, int64> then convPrim<_,'U> (# "conv.u2" (convPrim<_,int64> value) : char #) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (# "conv.u2" (convPrim<_,uint64> value) : char #) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (# "conv.u2" (convPrim<_,nativeint> value) : char #) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (# "conv.u2" (convPrim<_,unativeint> value) : char #) 
                elif typeeq<'T, float> then convPrim<_,'U> (# "conv.u2" (convPrim<_,float> value) : char #) 
                elif typeeq<'T, float32> then convPrim<_,'U> (# "conv.u2" (convPrim<_,float32> value) : char #) 
                elif typeeq<'T, char> then convPrim<_,'U> (# "conv.u2" (convPrim<_,char> value) : char #) 
                elif typeeq<'T, string> then convPrim<_,'U> (System.Char.Parse (convPrim<_,string> value)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            elif typeeq<'U, decimal> then 
                if typeeq<'T, sbyte> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,sbyte> value))
                elif typeeq<'T, byte> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,byte> value))
                elif typeeq<'T, int16> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,int16> value))
                elif typeeq<'T, uint16> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,uint16> value))
                elif typeeq<'T, int32> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,int32> value)) 
                elif typeeq<'T, uint32> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,uint32> value)) 
                elif typeeq<'T, int64> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,int64> value)) 
                elif typeeq<'T, uint64> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,uint64> value)) 
                elif typeeq<'T, nativeint> then convPrim<_,'U> (Convert.ToDecimal (# "conv.i8" (convPrim<_,nativeint> value) : int64 #)) 
                elif typeeq<'T, unativeint> then convPrim<_,'U> (Convert.ToDecimal (# "conv.u8" (convPrim<_,unativeint> value) : uint64 #)) 
                elif typeeq<'T, float> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,float> value)) 
                elif typeeq<'T, float32> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,float32> value)) 
                elif typeeq<'T, char> then convPrim<_,'U> (Convert.ToDecimal (convPrim<_,char> value)) 
                elif typeeq<'T, decimal> then convPrim<'T,'U> value 
                elif typeeq<'T, string> then convPrim<_,'U> (Decimal.Parse(convPrim<_,string> value, NumberStyles.Float,CultureInfo.InvariantCulture)) 
                else UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value
            else
                UnaryOpDynamicImplTable<OpExplicitInfo, 'T, 'U>.Invoke "op_Explicit" value

        type OpLessThanInfo = class end
        let LessThanDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #)
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #)
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #)
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #)
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,float> x) (convPrim<_,float> y) : bool #) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (# "clt.un" (convPrim<_,char> x) (convPrim<_,char> y) : bool #) 
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_LessThan (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (# "clt" (String.CompareOrdinal (convPrim<_,string> x, convPrim<_,string> y)) 0 : bool #)
            else BinaryOpDynamicImplTable<OpLessThanInfo, 'T1, 'T2, 'U>.Invoke "op_LessThan" x y

        type OpGreaterThanInfo = class end
        let GreaterThanDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #)
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #)
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #)
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #)
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,float> x) (convPrim<_,float> y) : bool #) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt.un" (convPrim<_,char> x) (convPrim<_,char> y) : bool #) 
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_GreaterThan (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (# "cgt" (String.CompareOrdinal (convPrim<_,string> x, convPrim<_,string> y)) 0 : bool #)
            else BinaryOpDynamicImplTable<OpGreaterThanInfo, 'T1, 'T2, 'U>.Invoke "op_GreaterThan" x y

        type OpLessThanOrEqualInfo = class end
        let LessThanOrEqualDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #))
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #))
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #))
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #))
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #)) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #)) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,float> x) (convPrim<_,float> y) : bool #)) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #)) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt.un" (convPrim<_,char> x) (convPrim<_,char> y) : bool #)) 
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_LessThanOrEqual (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (not (# "cgt" (String.CompareOrdinal (convPrim<_,string> x, convPrim<_,string> y)) 0 : bool #))
            else BinaryOpDynamicImplTable<OpLessThanOrEqualInfo, 'T1, 'T2, 'U>.Invoke "op_LessThanOrEqual" x y

        type OpGreaterThanOrEqualInfo = class end
        let GreaterThanOrEqualDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #))
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #))
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #))
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #))
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #)) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #)) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,float> x) (convPrim<_,float> y) : bool #)) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #)) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt.un" (convPrim<_,char> x) (convPrim<_,char> y) : bool #)) 
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_GreaterThanOrEqual (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (not (# "clt" (String.CompareOrdinal (convPrim<_,string> x, convPrim<_,string> y)) 0 : bool #))
            else BinaryOpDynamicImplTable<OpGreaterThanOrEqualInfo, 'T1, 'T2, 'U>.Invoke "op_GreaterThanOrEqual" x y

        type OpEqualityInfo = class end
        let EqualityDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #)
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #)
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #)
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #)
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,float> x) (convPrim<_,float> y) : bool #) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (# "ceq" (convPrim<_,char> x) (convPrim<_,char> y) : bool #) 
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_Equality (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (String.Equals (convPrim<_,string> x, convPrim<_,string> y))
            else BinaryOpDynamicImplTable<OpEqualityInfo, 'T1, 'T2, 'U>.Invoke "op_Equality" x y

        type OpInequalityInfo = class end
        let InequalityDynamic<'T1, 'T2, 'U> (x: 'T1) (y: 'T2) : 'U =
            if type2eq<'T1, 'T2, sbyte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,sbyte> x) (convPrim<_,sbyte> y) : bool #))
            elif type2eq<'T1, 'T2, byte> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,byte> x) (convPrim<_,byte> y) : bool #))
            elif type2eq<'T1, 'T2, int16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,int16> x) (convPrim<_,int16> y) : bool #))
            elif type2eq<'T1, 'T2, uint16> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,uint16> x) (convPrim<_,uint16> y) : bool #))
            elif type2eq<'T1, 'T2, int32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,int32> x) (convPrim<_,int32> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,uint32> x) (convPrim<_,uint32> y) : bool #)) 
            elif type2eq<'T1, 'T2, int64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,int64> x) (convPrim<_,int64> y) : bool #)) 
            elif type2eq<'T1, 'T2, uint64> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,uint64> x) (convPrim<_,uint64> y) : bool #)) 
            elif type2eq<'T1, 'T2, nativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,nativeint> x) (convPrim<_,nativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, unativeint> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,unativeint> x) (convPrim<_,unativeint> y) : bool #)) 
            elif type2eq<'T1, 'T2, float> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,float> x) (convPrim<_,float> y) : bool #)) 
            elif type2eq<'T1, 'T2, float32> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,float32> x) (convPrim<_,float32> y) : bool #)) 
            elif type2eq<'T1, 'T2, char> && typeeq<'U, bool> then convPrim<_,'U> (not (# "ceq" (convPrim<_,char> x) (convPrim<_,char> y) : bool #))
            elif type2eq<'T1, 'T2, decimal> && typeeq<'U, bool> then convPrim<_,'U> (Decimal.op_Inequality (convPrim<_,decimal> x, convPrim<_,decimal> y))
            elif type2eq<'T1, 'T2, string> && typeeq<'U, bool> then convPrim<_,'U> (not (String.Equals (convPrim<_,string> x, convPrim<_,string> y)))
            else BinaryOpDynamicImplTable<OpEqualityInfo, 'T1, 'T2, 'U>.Invoke "op_Inequality" x y

        type DivideByIntInfo = class end
        let DivideByIntDynamic<'T> (x: 'T) (n: int) : 'T =
            if typeeq<'T, float> then convPrim<_,'T> (# "div" (convPrim<_,float> x) (# "conv.r8" n : float #) : float #)
            elif typeeq<'T, float32> then convPrim<_,'T> (# "div" (convPrim<_,float32> x) (# "conv.r4" n : float32 #) : float32 #) 
            elif typeeq<'T, decimal> then convPrim<_,'T> (Decimal.Divide(convPrim<_,decimal> x, Convert.ToDecimal(n))) 
            else BinaryOpDynamicImplTable<DivideByIntInfo, 'T, int, 'T>.Invoke "DivideByInt" x n

        let inline DivideByInt< ^T when ^T : (static member DivideByInt : ^T * int -> ^T) > (x: ^T) (y: int) : ^T =
            DivideByIntDynamic<'T> x y
            when ^T : float = (# "div" x ((# "conv.r8" y  : float #)) : float #)
            when ^T : float32 = (# "div" x ((# "conv.r4" y  : float32 #)) : float32 #)
            when ^T : decimal = Decimal.Divide((# "" x : decimal #), Convert.ToDecimal(y))
            when ^T : ^T = (^T : (static member DivideByInt : ^T * int -> ^T) (x, y))

namespace Microsoft.FSharp.Core

    open System
    open System.Collections
    open System.Collections.Generic
    open System.Diagnostics
    open System.Globalization
    open System.Text
    open Microsoft.FSharp.Core.BasicInlinedOperations
    open Microsoft.FSharp.Core.LanguagePrimitives
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicFunctions

    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`2")>]
    type Choice<'T1,'T2> = 
      | Choice1Of2 of 'T1 
      | Choice2Of2 of 'T2
    
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`3")>]
    type Choice<'T1,'T2,'T3> = 
      | Choice1Of3 of 'T1 
      | Choice2Of3 of 'T2
      | Choice3Of3 of 'T3
    
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`4")>]
    type Choice<'T1,'T2,'T3,'T4> = 
      | Choice1Of4 of 'T1 
      | Choice2Of4 of 'T2
      | Choice3Of4 of 'T3
      | Choice4Of4 of 'T4
    
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`5")>]
    type Choice<'T1,'T2,'T3,'T4,'T5> = 
      | Choice1Of5 of 'T1 
      | Choice2Of5 of 'T2
      | Choice3Of5 of 'T3
      | Choice4Of5 of 'T4
      | Choice5Of5 of 'T5
    
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`6")>]
    type Choice<'T1,'T2,'T3,'T4,'T5,'T6> = 
      | Choice1Of6 of 'T1
      | Choice2Of6 of 'T2
      | Choice3Of6 of 'T3
      | Choice4Of6 of 'T4
      | Choice5Of6 of 'T5
      | Choice6Of6 of 'T6
    
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpChoice`7")>]
    type Choice<'T1,'T2,'T3,'T4,'T5,'T6,'T7> = 
      | Choice1Of7 of 'T1
      | Choice2Of7 of 'T2
      | Choice3Of7 of 'T3
      | Choice4Of7 of 'T4
      | Choice5Of7 of 'T5
      | Choice6Of7 of 'T6
      | Choice7Of7 of 'T7
          
    //-------------------------------------------------------------------------
    // F#-specific Exceptions

    [<StructuralEquality; NoComparison>]
    exception MatchFailureException of string * int * int with 
        override x.Message  = SR.GetString(SR.matchCasesIncomplete)

    //-------------------------------------------------------------------------
    // Function Values

    [<AbstractClass>]
    type FSharpTypeFunc [<DebuggerHidden>] () = 
        abstract Specialize<'T> : unit -> obj

    [<AbstractClass>]
    type FSharpFunc<'T,'Res> [<DebuggerHidden>] () = 
        abstract Invoke : 'T -> 'Res

    module OptimizedClosures = 

          [<AbstractClass>]
          type FSharpFunc<'T,'U,'V> [<DebuggerHidden>] () = 
              inherit FSharpFunc<'T,('U -> 'V)>()
              abstract Invoke : 'T * 'U -> 'V
              override f.Invoke(t) = (fun u -> f.Invoke(t,u))
              static member Adapt(func : 'T -> 'U -> 'V) = 
                  match box func with 
                  // Does it take two arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V> as f -> f

                  | _ -> { new FSharpFunc<'T,'U,'V>() with 
                              member x.Invoke(t,u) = (retype func : FSharpFunc<'T,FSharpFunc<'U,'V>>).Invoke(t).Invoke(u) }

          [<AbstractClass>]
          type FSharpFunc<'T,'U,'V,'W> [<DebuggerHidden>] () = 
              inherit FSharpFunc<'T,('U -> 'V -> 'W)>()
              abstract Invoke : 'T * 'U * 'V -> 'W
              override f.Invoke(t) = (fun u v -> f.Invoke(t,u,v))
              static member Adapt(func : 'T -> 'U -> 'V -> 'W) = 
                  match box func with 
                  // Does it take three arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,'W> as f -> f

                  // Does it take two arguments without side effect?
                  | :? FSharpFunc<'T,'U,FSharpFunc<'V,'W>> as f ->
                         { new FSharpFunc<'T,'U,'V,'W>() with 
                              member x.Invoke(t,u,v) = f.Invoke(t,u).Invoke(v) }

                  | _ -> { new FSharpFunc<'T,'U,'V,'W>() with 
                              member x.Invoke(t,u,v) = (retype func : FSharpFunc<'T,('U -> 'V -> 'W)>).Invoke(t) u v }

          [<AbstractClass>]
          type FSharpFunc<'T,'U,'V,'W,'X> [<DebuggerHidden>] () = 
              inherit FSharpFunc<'T,('U -> 'V -> 'W -> 'X)>()
              abstract Invoke : 'T * 'U * 'V * 'W -> 'X
              static member Adapt(func : 'T -> 'U -> 'V -> 'W -> 'X) = 
                  match box func with 
                  // Does it take four arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,'W,'X> as f -> f

                  // Does it take three arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,FSharpFunc<'W,'X>> as f ->
                         { new FSharpFunc<'T,'U,'V,'W,'X>() with 
                              member x.Invoke(t,u,v,w) = f.Invoke(t,u,v).Invoke(w) }

                  // Does it take two arguments without side effect?
                  | :? FSharpFunc<'T,'U,('V -> 'W -> 'X)> as f ->
                         { new FSharpFunc<'T,'U,'V,'W,'X>() with 
                              member x.Invoke(t,u,v,w) = f.Invoke(t,u) v w }

                  | _ -> { new FSharpFunc<'T,'U,'V,'W,'X>() with 
                              member x.Invoke(t,u,v,w) = ((retype func : FSharpFunc<'T,('U -> 'V -> 'W -> 'X)>).Invoke(t)) u v w   }
              override f.Invoke(t) = (fun u v w -> f.Invoke(t,u,v,w))

          [<AbstractClass>]
          type FSharpFunc<'T,'U,'V,'W,'X,'Y> [<DebuggerHidden>] () =
              inherit FSharpFunc<'T,('U -> 'V -> 'W -> 'X -> 'Y)>()
              abstract Invoke : 'T * 'U * 'V * 'W * 'X -> 'Y
              override f.Invoke(t) = (fun u v w x -> f.Invoke(t,u,v,w,x))
              static member Adapt(func : 'T -> 'U -> 'V -> 'W -> 'X -> 'Y) = 
                  match box func with 

                  // Does it take five arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,'W,'X,'Y> as f -> f

                  // Does it take four arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,'W,FSharpFunc<'X,'Y>> as f ->
                         { new FSharpFunc<'T,'U,'V,'W,'X,'Y>() with 
                              member ff.Invoke(t,u,v,w,x) = f.Invoke(t,u,v,w).Invoke(x) }

                  // Does it take three arguments without side effect?
                  | :? FSharpFunc<'T,'U,'V,('W -> 'X -> 'Y)> as f ->
                         { new FSharpFunc<'T,'U,'V,'W,'X,'Y>() with 
                              member ff.Invoke(t,u,v,w,x) = f.Invoke(t,u,v) w x }

                  // Does it take two arguments without side effect?
                  | :? FSharpFunc<'T,'U,('V -> 'W -> 'X -> 'Y)> as f ->
                         { new FSharpFunc<'T,'U,'V,'W,'X,'Y>() with 
                              member ff.Invoke(t,u,v,w,x) = f.Invoke(t,u) v w x }

                  | _ -> { new FSharpFunc<'T,'U,'V,'W,'X,'Y>() with 
                              member ff.Invoke(t,u,v,w,x) = ((retype func : FSharpFunc<'T,('U -> 'V -> 'W -> 'X -> 'Y)>).Invoke(t)) u v w x  }
          
          let inline invokeFast2((f1 : FSharpFunc<'T,('U -> 'V)>), t,u) =
              match f1 with
              | :? FSharpFunc<'T,'U,'V> as f2 -> f2.Invoke(t,u)
              | _                            -> (f1.Invoke(t)) u     
          
          let inline invokeFast3((f1 : FSharpFunc<'T,('U -> 'V -> 'W)>), t,u,v) =
               match f1 with
               | :? FSharpFunc<'T,'U,'V,'W>      as f3 -> f3.Invoke(t,u,v)
               | :? FSharpFunc<'T,'U,('V -> 'W)> as f2 -> (f2.Invoke(t,u)) v
               | _                                    -> (f1.Invoke(t)) u v
               
          let inline invokeFast4((f1 : FSharpFunc<'T,('U -> 'V -> 'W -> 'X)>), t,u,v,w) =
               match f1 with
               | :? FSharpFunc<'T,'U,'V,'W,'X>         as f4 -> f4.Invoke(t,u,v,w)
               | :? FSharpFunc<'T,'U,'V,('W -> 'X)>    as f3 -> (f3.Invoke(t,u,v)) w
               | :? FSharpFunc<'T,'U,('V -> 'W -> 'X)> as f2 -> (f2.Invoke(t,u)) v w
               | _                                          -> (f1.Invoke(t)) u v w

          let inline invokeFast5((f1 : FSharpFunc<'T,('U -> 'V -> 'W -> 'X -> 'Y)>), t,u,v,w,x) =
               match f1 with
               | :? FSharpFunc<'T,'U,'V,'W,'X,'Y>             as f5 -> f5.Invoke(t,u,v,w,x)
               | :? FSharpFunc<'T,'U,'V,'W,('X -> 'Y)>        as f4 -> (f4.Invoke(t,u,v,w)) x
               | :? FSharpFunc<'T,'U,'V,('W -> 'X -> 'Y)>     as f3 -> (f3.Invoke(t,u,v)) w x
               | :? FSharpFunc<'T,'U,('V -> 'W -> 'X -> 'Y)>  as f2 -> (f2.Invoke(t,u)) v w x
               | _                                                 -> (f1.Invoke(t)) u v w x


    type FSharpFunc<'T,'Res> with

        // Note: this is not made public in the signature, because of conflicts with the Converter overload.
        // The method remains in case someone is calling it via reflection.
        [<CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")>]
        static member op_Implicit(converter : System.Func<_,_>) : ('T -> 'Res) =  (fun t -> converter.Invoke(t))

        // Note: this is not made public in the signature, because of conflicts with the Converter overload.
        // The method remains in case someone is calling it via reflection.
        [<CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")>]
        static member op_Implicit(func : ('T -> 'Res) ) =  new System.Func<'T,'Res>(func)

        [<CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")>]
        static member op_Implicit(f : System.Converter<_,_>) : ('T -> 'Res) =  (fun t -> f.Invoke(t))

        [<CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")>]
        static member op_Implicit (func : ('T -> 'Res) ) =  new System.Converter<'T,'Res>(func)

        static member FromConverter (converter: System.Converter<_,_>) : ('T -> 'Res) =  (fun t -> converter.Invoke(t))

        static member ToConverter (func: ('T -> 'Res) ) =  new System.Converter<'T,'Res>(func)

        static member InvokeFast (func:FSharpFunc<_,_>, arg1: 'T, arg2: 'Res)                   = OptimizedClosures.invokeFast2(func, arg1, arg2) 

        static member InvokeFast (func:FSharpFunc<_,_>, arg1: 'T, arg2: 'Res, arg3)             = OptimizedClosures.invokeFast3(func, arg1, arg2, arg3)

        static member InvokeFast (func:FSharpFunc<_,_>, arg1: 'T, arg2: 'Res, arg3, arg4)       = OptimizedClosures.invokeFast4(func, arg1, arg2, arg3, arg4)

        static member InvokeFast (func:FSharpFunc<_,_>, arg1: 'T, arg2: 'Res, arg3, arg4, arg5) = OptimizedClosures.invokeFast5(func, arg1, arg2, arg3, arg4, arg5)

    [<AbstractClass>]
    [<Sealed>]
    type FuncConvert = 

        static member  inline ToFSharpFunc (action: Action<_>) = (fun t -> action.Invoke(t))

        static member  inline ToFSharpFunc (converter : Converter<_,_>) = (fun t -> converter.Invoke(t))

        // Note: this is not made public in the signature, because of conflicts with the Converter overload.
        // The method remains in case someone is calling it via reflection.
        static member  inline ToFSharpFunc (converter: System.Func<_, _>) = (fun t -> converter.Invoke(t))

        static member  inline FromFunc (func: System.Func<_>) = (fun () -> func.Invoke())

        static member  inline FromFunc (func: System.Func<_, _>) = (fun t -> func.Invoke(t))

        static member  inline FromFunc (func: System.Func<_, _, _>) = (fun t1 t2 -> func.Invoke(t1,t2))

        static member  inline FromFunc (func: System.Func<_, _, _, _>) = (fun t1 t2 t3 -> func.Invoke(t1,t2,t3))

        static member  inline FromFunc (func: System.Func<_, _, _, _, _>) = (fun t1 t2 t3 t4 -> func.Invoke(t1,t2,t3,t4))

        static member  inline FromFunc (func: System.Func<_, _, _, _, _, _>) = (fun t1 t2 t3 t4 t5 -> func.Invoke(t1,t2,t3,t4,t5))

        static member  inline FromAction (action: System.Action) = (fun () -> action.Invoke())

        static member  inline FromAction (action: System.Action<_>) = (fun t -> action.Invoke(t))

        static member  inline FromAction (action: System.Action<_, _>) = (fun t1 t2 -> action.Invoke(t1,t2))

        static member  inline FromAction (action: System.Action<_, _, _>) = (fun t1 t2 t3 -> action.Invoke(t1,t2,t3))

        static member  inline FromAction (action: System.Action<_, _, _, _>) = (fun t1 t2 t3 t4 -> action.Invoke(t1,t2,t3,t4))

        static member  inline FromAction (action: System.Action<_, _, _, _, _>) = (fun t1 t2 t3 t4 t5 -> action.Invoke(t1,t2,t3,t4,t5))

        static member inline FuncFromTupled (func: 'T1 * 'T2 -> 'Res) = (fun a b -> func (a, b))

        static member inline FuncFromTupled (func: 'T1 * 'T2 * 'T3 -> 'Res) = (fun a b c -> func (a, b, c))

        static member inline FuncFromTupled (func: 'T1 * 'T2 * 'T3 * 'T4 -> 'Res) = (fun a b c d -> func (a, b, c, d))

        static member inline FuncFromTupled (func: 'T1 * 'T2 * 'T3 * 'T4 * 'T5 -> 'Res) = (fun a b c d e -> func (a, b, c, d, e))

    //-------------------------------------------------------------------------
    // Refs
    //-------------------------------------------------------------------------

    [<DebuggerDisplay("{contents}")>]
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpRef`1")>]
    type Ref<'T> = 
        { 
          [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
          mutable contents: 'T }
        member x.Value 
            with get() = x.contents
            and  set v = x.contents <- v

    and 'T ref = Ref<'T> 

    //-------------------------------------------------------------------------
    // Options
    //-------------------------------------------------------------------------

    [<DefaultAugmentation(false)>]
    [<DebuggerDisplay("{get_DebugDisplay(this),nq}")>]
    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    [<CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId="Option")>]
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpOption`1")>]
    type Option<'T> = 
        | None :       'T option
        | Some : Value:'T -> 'T option 

        [<CompilationRepresentation(CompilationRepresentationFlags.Instance)>]
        member x.Value = match x with Some x -> x | None -> raise (new System.InvalidOperationException("Option.Value"))

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member x.IsNone = match x with None -> true | _ -> false

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member x.IsSome = match x with Some _ -> true | _ -> false

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        static member None : 'T option = None

        static member Some (value) : 'T option = Some(value)

        static member op_Implicit (value) : 'T option = Some(value)
        
        member private x.DebugDisplay =
            match x with
            | None -> "None"
            | Some _ -> String.Format("Some({0})", anyToStringShowingNull x.Value)

        override x.ToString() = 
           // x is non-null, hence Some
           "Some("^anyToStringShowingNull x.Value^")"

    and 'T option = Option<'T> 

    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpResult`2")>]
    [<Struct>]
    type Result<'T,'TError> = 
      | Ok of ResultValue:'T 
      | Error of ErrorValue:'TError

    [<StructuralEquality; StructuralComparison>]
    [<Struct>]
    [<CompiledName("FSharpValueOption`1")>]
    [<DebuggerDisplay("{DebugDisplay,nq}")>]
    type ValueOption<'T> =
        | ValueNone : 'T voption
        | ValueSome : 'T -> 'T voption

        member x.Value = match x with ValueSome x -> x | ValueNone -> raise (new System.InvalidOperationException("ValueOption.Value"))

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        static member None : 'T voption = ValueNone

        static member Some (value) : 'T voption = ValueSome(value)

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member x.IsNone = match x with ValueNone -> true | _ -> false

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member x.IsSome = match x with ValueSome _ -> true | _ -> false

        static member op_Implicit (value) : 'T voption = ValueSome(value)
        
        member private x.DebugDisplay =
            match x with
            | ValueNone -> "ValueNone"
            | ValueSome _ -> String.Format("ValueSome({0})", anyToStringShowingNull x.Value)

        override x.ToString() =
            match x with
            | ValueNone -> "ValueNone"
            | ValueSome _ -> anyToStringShowingNull x.Value

    and 'T voption = ValueOption<'T>

namespace Microsoft.FSharp.Collections

    //-------------------------------------------------------------------------
    // Lists
    //-------------------------------------------------------------------------

    open System
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicFunctions
    open Microsoft.FSharp.Core.BasicInlinedOperations

    [<DefaultAugmentation(false)>]
    [<DebuggerTypeProxyAttribute(typedefof<ListDebugView<_>>)>]
    [<DebuggerDisplay("{DebugDisplay,nq}")>]
    [<CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")>]
    [<StructuralEquality; StructuralComparison>]
    [<CompiledName("FSharpList`1")>]
    type List<'T> = 
       | ([])  :                  'T list
       | ( :: )  : Head: 'T * Tail: 'T list -> 'T list
       interface System.Collections.Generic.IEnumerable<'T>
       interface System.Collections.IEnumerable
       interface System.Collections.Generic.IReadOnlyCollection<'T>
       interface System.Collections.Generic.IReadOnlyList<'T>
        
    and 'T list = List<'T>

    //-------------------------------------------------------------------------
    // List (debug view)
    //-------------------------------------------------------------------------

    and
       ListDebugView<'T>(l:list<'T>) =

           let ListDebugViewMaxLength = 50                          // default displayed Max Length
           let ListDebugViewMaxFullLength = 5000                    // display only when FullList opened (5000 is a super big display used to cut-off an infinite list or undebuggably huge one)
           let rec count l n max =
               match l with
               | [] -> n
               | _ :: t -> if n > max then n else count t (n+1) max

           let items length =
               let items = zeroCreate length
               let rec copy (items: 'T[]) l i = 
                   match l with
                   | [] -> () 
                   | h :: t -> 
                       if i < length then 
                           SetArray items i h
                           copy items t (i+1)

               copy items l 0
               items

           [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
           member x.Items = items (count l 0 ListDebugViewMaxLength)

           [<DebuggerBrowsable(DebuggerBrowsableState.Collapsed)>]
           member x._FullList = items (count l 0 ListDebugViewMaxFullLength)

    type ResizeArray<'T> = System.Collections.Generic.List<'T>

    //-------------------------------------------------------------------------
    // List (augmentation)
    //-------------------------------------------------------------------------

    module PrivateListHelpers = 

        let notStarted() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationNotStarted)))
        let alreadyFinished() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationAlreadyFinished)))
        let outOfRange() = raise (System.IndexOutOfRangeException(SR.GetString(SR.indexOutOfBounds)))

        let nonempty x = match x with [] -> false | _ -> true
        // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
        // tail cons cells is permitted in carefully written library code.
        let inline setFreshConsTail cons t = cons.( :: ).1 <- t
        let inline freshConsNoTail h = h :: (# "ldnull" : 'T list #)

        // Return the last cons it the chain
        let rec appendToFreshConsTail cons xs = 
            match xs with 
            | [] -> cons
            | h :: t -> 
                let cons2 = [h]
                setFreshConsTail cons cons2
                appendToFreshConsTail cons2 t

        type ListEnumerator<'T> (s: 'T list) = 
             let mutable curr = s 
             let mutable started = false 

             member x.GetCurrent() = 
                 if started then 
                     match curr with 
                     | [] -> alreadyFinished()
                     | h :: _ -> h
                 else 
                     notStarted()

             interface IEnumerator<'T> with 
                 member x.Current = x.GetCurrent()

             interface System.Collections.IEnumerator with 
                  member x.MoveNext() = 
                      if started then 
                          match curr with 
                          | _ :: t -> 
                              curr <- t; 
                              nonempty curr
                          | _ -> false
                      else 
                          started <- true; 
                          nonempty curr

                  member x.Current = box (x.GetCurrent())

                  member x.Reset() = 
                      started <- false; 
                      curr <- s

             interface System.IDisposable with 
                  member x.Dispose() = () 

        let mkListEnumerator s = (new ListEnumerator<'T>(s) :> IEnumerator<'T>)

        let rec lengthAcc acc xs = match xs with [] -> acc | _ :: t -> lengthAcc (acc+1) t 

        let rec nth l n = 
            match l with 
            | [] -> raise (new System.ArgumentException(SR.GetString(SR.indexOutOfBounds),"n"))
            | h :: t -> 
               if n < 0 then raise (new System.ArgumentException((SR.GetString(SR.inputMustBeNonNegative)),"n"))
               elif n = 0 then h
               else nth t (n - 1)

        let rec sliceFreshConsTail cons n l =
            if n = 0 then setFreshConsTail cons [] else
            match l with
            | [] -> setFreshConsTail cons []
            | x :: xs ->
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                sliceFreshConsTail cons2 (n - 1) xs

        let sliceTake n l =
            if n < 0 then [] else
            match l with
            | [] -> []
            | x :: xs ->
                let cons = freshConsNoTail x
                sliceFreshConsTail cons n xs
                cons

        let sliceSkip n l =
            let n2 = if n < 0 then 0 else n
            let rec loop i lst =
                match lst with
                | _ when i = 0 -> lst
                | _ :: t -> loop (i-1) t
                | [] -> []
            loop n2 l

    type List<'T> with
        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member l.Length = PrivateListHelpers.lengthAcc 0 l

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member l.DebugDisplay = 
           let n = l.Length
           let txt = 
               if n > 1000 then "Length > 1000"
               else System.String.Concat( [| "Length = "; n.ToString() |])
           txt

        member l.Head   = match l with a :: _ -> a | [] -> raise (System.InvalidOperationException(SR.GetString(SR.inputListWasEmpty)))
        member l.Tail   = match l with _ :: b -> b | [] -> raise (System.InvalidOperationException(SR.GetString(SR.inputListWasEmpty)))

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        member l.IsEmpty  = match l with [] -> true | _ -> false
        member l.Item with get(index) = PrivateListHelpers.nth l index

        [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
        static member Empty       : 'T list = []

        static member Cons(head,tail) : 'T list = head :: tail
        override x.ToString() = 
           match x with 
           | [] -> "[]"
           | [h1] -> System.Text.StringBuilder().Append("[").Append(anyToStringShowingNull h1).Append("]").ToString()
           | [h1;h2] -> System.Text.StringBuilder().Append("[").Append(anyToStringShowingNull h1).Append("; ").Append(anyToStringShowingNull h2).Append("]").ToString()
           | [h1;h2;h3] -> System.Text.StringBuilder().Append("[").Append(anyToStringShowingNull h1).Append("; ").Append(anyToStringShowingNull h2).Append("; ").Append(anyToStringShowingNull h3).Append("]").ToString()
           | h1 :: h2 :: h3 :: _ -> System.Text.StringBuilder().Append("[").Append(anyToStringShowingNull h1).Append("; ").Append(anyToStringShowingNull h2).Append("; ").Append(anyToStringShowingNull h3).Append("; ... ]").ToString() 

        member l.GetSlice(startIndex: int option, endIndex: int option ) = 
            match (startIndex, endIndex) with
            | None, None -> l
            | Some(i), None -> PrivateListHelpers.sliceSkip i l
            | None, Some(j) -> PrivateListHelpers.sliceTake j l
            | Some(i), Some(j) ->
                if i > j then [] else
                let start = if i < 0 then 0 else i
                PrivateListHelpers.sliceTake (j - start) (PrivateListHelpers.sliceSkip start l)

        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        member l.GetReverseIndex(_: int, offset: int) = l.Length - offset - 1

        interface IEnumerable<'T> with
            member l.GetEnumerator() = PrivateListHelpers.mkListEnumerator l

        interface System.Collections.IEnumerable with
            member l.GetEnumerator() = (PrivateListHelpers.mkListEnumerator l :> System.Collections.IEnumerator)

        interface IReadOnlyCollection<'T> with
            member l.Count = l.Length

        interface IReadOnlyList<'T> with
            member l.Item with get(index) = l.[index]

    type seq<'T> = IEnumerable<'T>

        
//-------------------------------------------------------------------------
// Operators
//-------------------------------------------------------------------------


namespace Microsoft.FSharp.Core

    open System
    open System.Diagnostics              
    open System.Collections.Generic
    open System.Globalization
    open System.Text
    open System.Numerics
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicFunctions
    open Microsoft.FSharp.Core.BasicInlinedOperations
    open Microsoft.FSharp.Collections

    [<CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1046:DoNotOverloadOperatorEqualsOnReferenceTypes")>]
    module Operators = 

        [<CompiledName("CreateSequence")>]
        let seq (sequence: seq<'T>) = sequence 

        [<CompiledName("Unbox")>]
        let inline unbox (value: obj) = UnboxGeneric(value)

        [<CompiledName("Box")>]
        let inline box (value: 'T)  = (# "box !0" type ('T) value : obj #)

        [<CompiledName("TryUnbox")>]
        let inline tryUnbox (value:obj)  = 
            match value with 
            | :? 'T as v -> Some v
            | _ -> None

        [<CompiledName("IsNull")>]
        let inline isNull (value : 'T) = 
            match value with 
            | null -> true 
            | _ -> false

        [<CompiledName("IsNotNull")>]
        let inline internal isNotNull (value : 'T) = 
            match value with 
            | null -> false 
            | _ -> true

        [<CompiledName("Raise")>]
        let inline raise (exn: exn) = (# "throw" exn : 'T #)

        let Failure message = new System.Exception(message)
        
        [<CompiledName("FailurePattern")>]
        let (|Failure|_|) (error: exn) = if error.GetType().Equals(typeof<System.Exception>) then Some error.Message else None

        let inline (<) x y = GenericLessThan x y
        let inline (>) x y = GenericGreaterThan x y
        let inline (>=) x y = GenericGreaterOrEqual x y
        let inline (<=) x y = GenericLessOrEqual x y
        let inline (=) x y = GenericEquality x y
        let inline (<>) x y = not (GenericEquality x y)

        [<CompiledName("Compare")>]
        let inline compare (e1: 'T) (e2: 'T) = GenericComparison e1 e2

        [<CompiledName("Max")>]
        let inline max e1 e2 = GenericMaximum e1 e2

        [<CompiledName("Min")>]
        let inline min e1 e2 = GenericMinimum e1 e2

        [<CompiledName("FailWith")>]
        let inline failwith message = raise (Failure(message))

        [<CompiledName("InvalidArg")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline invalidArg (argumentName:string) (message:string) = 
            raise (new System.ArgumentException(message,argumentName))

        [<CompiledName("NullArg")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline nullArg (argumentName:string) = 
            raise (new System.ArgumentNullException(argumentName))        

        [<CompiledName("InvalidOp")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline invalidOp message = raise (System.InvalidOperationException(message))

        [<CompiledName("Rethrow")>]
        [<NoDynamicInvocation>]
        let inline rethrow() = unbox(# "rethrow ldnull" : System.Object #)

        [<CompiledName("Reraise")>]
        [<NoDynamicInvocation>]
        let inline reraise() = unbox(# "rethrow ldnull" : System.Object #)

        [<CompiledName("Fst")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline fst (a, _) = a

        [<CompiledName("Snd")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline snd (_, b) = b

        [<CompiledName("Ignore")>]
        let inline ignore _ = ()

        [<CompiledName("Ref")>]
        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let ref value = { contents = value }

        let (:=) cell value = cell.contents <- value

        let (!) cell = cell.contents

        let inline (|>) arg func = func arg

        let inline (||>) (arg1, arg2) func = func arg1 arg2

        let inline (|||>) (arg1, arg2, arg3) func = func arg1 arg2 arg3

        let inline (<|) func arg1 = func arg1

        let inline (<||) func (arg1, arg2) = func arg1 arg2

        let inline (<|||) func (arg1, arg2, arg3) = func arg1 arg2 arg3

        let inline (>>) func1 func2 x = func2 (func1 x)

        let inline (<<) func2 func1 x = func2 (func1 x)

        let (^) (s1: string) (s2: string) = System.String.Concat(s1, s2)

        [<CompiledName("DefaultArg")>]
        let defaultArg arg defaultValue = match arg with None -> defaultValue | Some v -> v
        
        [<CompiledName("DefaultValueArg")>]
        let defaultValueArg arg defaultValue = match arg with ValueNone -> defaultValue | ValueSome v -> v

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (~-) (n: ^T) : ^T = 
             UnaryNegationDynamic<(^T), (^T)> n
             when ^T : int32     = (# "neg" n  : int32 #)
             when ^T : float     = (# "neg" n  : float #)
             when ^T : float32   = (# "neg" n  : float32 #)
             when ^T : int64     = (# "neg" n  : int64 #)
             when ^T : int16     = (# "neg" n  : int16 #)
             when ^T : nativeint = (# "neg" n  : nativeint #)
             when ^T : sbyte     = (# "neg" n  : sbyte #)
             when ^T : decimal   = (# "" (System.Decimal.op_UnaryNegation((# "" n : decimal #))) : ^T #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             // That is, not in the generic implementation of '*'
             when ^T : ^T = (^T : (static member (~-) : ^T -> ^T) (n))

        let inline (+) (x: ^T) (y: ^U) : ^V = 
             AdditionDynamic<(^T),(^U),(^V)>  x y 
             when ^T : int32       and ^U : int32      = (# "add" x y : int32 #)
             when ^T : float       and ^U : float      = (# "add" x y : float #)
             when ^T : float32     and ^U : float32    = (# "add" x y : float32 #)
             when ^T : int64       and ^U : int64      = (# "add" x y : int64 #)
             when ^T : uint64      and ^U : uint64     = (# "add" x y : uint64 #)
             when ^T : uint32      and ^U : uint32     = (# "add" x y : uint32 #)
             when ^T : nativeint   and ^U : nativeint  = (# "add" x y : nativeint #)
             when ^T : unativeint  and ^U : unativeint = (# "add" x y : unativeint #)
             when ^T : int16       and ^U : int16      = (# "conv.i2" (# "add" x y : int32 #) : int16 #)
             when ^T : uint16      and ^U : uint16     = (# "conv.u2" (# "add" x y : uint32 #) : uint16 #)
             when ^T : char        and ^U : char       = (# "conv.u2" (# "add" x y : uint32 #) : char #)
             when ^T : sbyte       and ^U : sbyte      = (# "conv.i1" (# "add" x y : int32 #) : sbyte #)
             when ^T : byte        and ^U : byte       = (# "conv.u1" (# "add" x y : uint32 #) : byte #)
             when ^T : string      and ^U : string     = (# "" (System.String.Concat((# "" x : string #),(# "" y : string #))) : ^T #)
             when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Addition((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = ((^T or ^U): (static member (+) : ^T * ^U -> ^V) (x,y))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (-) (x: ^T) (y: ^U) : ^V = 
             SubtractionDynamic<(^T),(^U),(^V)>  x y 
             when ^T : int32      and ^U : int32      = (# "sub" x y : int32 #)
             when ^T : float      and ^U : float      = (# "sub" x y : float #)
             when ^T : float32    and ^U : float32    = (# "sub" x y : float32 #)
             when ^T : int64      and ^U : int64      = (# "sub" x y : int64 #)
             when ^T : uint64     and ^U : uint64     = (# "sub" x y : uint64 #)
             when ^T : uint32     and ^U : uint32     = (# "sub" x y : uint32 #)
             when ^T : nativeint  and ^U : nativeint  = (# "sub" x y : nativeint #)
             when ^T : unativeint and ^U : unativeint = (# "sub" x y : unativeint #)
             when ^T : int16       and ^U : int16      = (# "conv.i2" (# "sub" x y : int32 #) : int16 #)
             when ^T : uint16      and ^U : uint16     = (# "conv.u2" (# "sub" x y : uint32 #) : uint16 #)
             when ^T : sbyte       and ^U : sbyte      = (# "conv.i1" (# "sub" x y : int32 #) : sbyte #)
             when ^T : byte        and ^U : byte       = (# "conv.u1" (# "sub" x y : uint32 #) : byte #)
             when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Subtraction((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = ((^T or ^U): (static member (-) : ^T * ^U -> ^V) (x,y))

        let inline ( * ) (x: ^T) (y: ^U) : ^V = 
             MultiplyDynamic<(^T),(^U),(^V)>  x y 
             when ^T : int32      and ^U : int32      = (# "mul" x y : int32 #)
             when ^T : float      and ^U : float      = (# "mul" x y : float #)
             when ^T : float32    and ^U : float32    = (# "mul" x y : float32 #)
             when ^T : int64      and ^U : int64      = (# "mul" x y : int64 #)
             when ^T : uint64     and ^U : uint64     = (# "mul" x y : uint64 #)
             when ^T : uint32     and ^U : uint32     = (# "mul" x y : uint32 #)
             when ^T : nativeint  and ^U : nativeint  = (# "mul" x y : nativeint #)
             when ^T : unativeint and ^U : unativeint = (# "mul" x y : unativeint #)
             when ^T : int16       and ^U : int16      = (# "conv.i2" (# "mul" x y : int32 #) : int16 #)
             when ^T : uint16      and ^U : uint16     = (# "conv.u2" (# "mul" x y : uint32 #) : uint16 #)
             when ^T : sbyte       and ^U : sbyte      = (# "conv.i1" (# "mul" x y : int32 #) : sbyte #)
             when ^T : byte        and ^U : byte       = (# "conv.u1" (# "mul" x y : uint32 #) : byte #)
             when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Multiply((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = ((^T or ^U): (static member (*) : ^T * ^U -> ^V) (x,y))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline ( / ) (x: ^T) (y: ^U) : ^V = 
             DivisionDynamic<(^T),(^U),(^V)>  x y 
             when ^T : int32       and ^U : int32      = (# "div" x y : int32 #)
             when ^T : float       and ^U : float      = (# "div" x y : float #)
             when ^T : float32     and ^U : float32    = (# "div" x y : float32 #)
             when ^T : int64       and ^U : int64      = (# "div" x y : int64 #)
             when ^T : uint64      and ^U : uint64     = (# "div.un" x y : uint64 #)
             when ^T : uint32      and ^U : uint32     = (# "div.un" x y : uint32 #)
             when ^T : nativeint   and ^U : nativeint  = (# "div" x y : nativeint #)
             when ^T : unativeint  and ^U : unativeint = (# "div.un" x y : unativeint #)
             when ^T : int16       and ^U : int16      = (# "conv.i2" (# "div" x y : int32 #) : int16 #)
             when ^T : uint16      and ^U : uint16     = (# "conv.u2" (# "div.un" x y : uint32 #) : uint16 #)
             when ^T : sbyte       and ^U : sbyte      = (# "conv.i1" (# "div" x y : int32 #) : sbyte #)
             when ^T : byte        and ^U : byte       = (# "conv.u1" (# "div.un" x y : uint32 #) : byte #)
             when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Division((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = ((^T or ^U): (static member (/) : ^T * ^U -> ^V) (x,y))
        
        [<NoDynamicInvocation(isLegacy=true)>]
        let inline ( % ) (x: ^T) (y: ^U) : ^V = 
             ModulusDynamic<(^T),(^U),(^V)>  x y 
             when ^T : int32       and ^U : int32      = (# "rem" x y : int32 #)
             when ^T : float       and ^U : float      = (# "rem" x y : float #)
             when ^T : float32     and ^U : float32    = (# "rem" x y : float32 #)
             when ^T : int64       and ^U : int64      = (# "rem" x y : int64 #)
             when ^T : uint64      and ^U : uint64     = (# "rem.un" x y : uint64 #)
             when ^T : uint32      and ^U : uint32     = (# "rem.un" x y : uint32 #)
             when ^T : nativeint   and ^U : nativeint  = (# "rem" x y : nativeint #)
             when ^T : unativeint  and ^U : unativeint = (# "rem.un" x y : unativeint #)
             when ^T : int16       and ^U : int16      = (# "conv.i2" (# "rem"    x y : int32  #) : int16  #)
             when ^T : uint16      and ^U : uint16     = (# "conv.u2" (# "rem.un" x y : uint32 #) : uint16 #)
             when ^T : sbyte       and ^U : sbyte      = (# "conv.i1" (# "rem"    x y : int32  #) : sbyte  #)
             when ^T : byte        and ^U : byte       = (# "conv.u1" (# "rem.un" x y : uint32 #) : byte   #)
             when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Modulus((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = ((^T or ^U): (static member (%) : ^T * ^U -> ^V) (x,y))
        
        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (~+) (value: ^T) : ^T =
             value
             when ^T : int32      = value
             when ^T : float      = value
             when ^T : float32    = value
             when ^T : int64      = value
             when ^T : uint64     = value
             when ^T : uint32     = value
             when ^T : int16      = value
             when ^T : uint16     = value
             when ^T : nativeint  = value
             when ^T : unativeint = value
             when ^T : sbyte      = value
             when ^T : byte       = value
             when ^T : decimal    = value
             when ^T : ^T = (^T: (static member (~+) : ^T -> ^T) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (<<<) (value: ^T) (shift:int) : ^T = 
             LeftShiftDynamic<(^T),int,(^T)> value shift
             when ^T : int32      = (# "shl" value (mask shift 31) : int #)
             when ^T : uint32     = (# "shl" value (mask shift 31) : uint32 #)
             when ^T : int64      = (# "shl" value (mask shift 63) : int64 #)
             when ^T : uint64     = (# "shl" value (mask shift 63) : uint64 #)
             when ^T : nativeint  = (# "shl" value shift : nativeint #)
             when ^T : unativeint = (# "shl" value shift : unativeint #)
             when ^T : int16      = (# "conv.i2" (# "shl" value (mask shift 15) : int32  #) : int16 #)
             when ^T : uint16     = (# "conv.u2" (# "shl" value (mask shift 15) : uint32 #) : uint16 #)
             when ^T : sbyte      = (# "conv.i1" (# "shl" value (mask shift 7 ) : int32  #) : sbyte #)
             when ^T : byte       = (# "conv.u1" (# "shl" value (mask shift 7 ) : uint32 #) : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (<<<) : ^T * int -> ^T) (value,shift))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (>>>) (value: ^T) (shift:int) : ^T = 
             RightShiftDynamic<(^T),int,(^T)> value shift
             when ^T : int32      = (# "shr"    value (mask shift 31) : int32 #)
             when ^T : uint32     = (# "shr.un" value (mask shift 31) : uint32 #)
             when ^T : int64      = (# "shr"    value (mask shift 63) : int64 #)
             when ^T : uint64     = (# "shr.un" value (mask shift 63) : uint64 #)
             when ^T : nativeint  = (# "shr"    value shift : nativeint #)
             when ^T : unativeint = (# "shr.un" value shift : unativeint #)
             when ^T : int16      = (# "conv.i2" (# "shr"    value (mask shift 15) : int32  #) : int16 #)
             when ^T : uint16     = (# "conv.u2" (# "shr.un" value (mask shift 15) : uint32 #) : uint16 #)
             when ^T : sbyte      = (# "conv.i1" (# "shr"    value (mask shift 7 ) : int32  #) : sbyte #)
             when ^T : byte       = (# "conv.u1" (# "shr.un" value (mask shift 7 ) : uint32 #) : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (>>>) : ^T * int -> ^T) (value, shift))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (&&&) (x: ^T) (y: ^T) : ^T = 
             BitwiseAndDynamic<(^T),(^T),(^T)> x y
             when ^T : int32      = (# "and" x y : int32 #)
             when ^T : int64      = (# "and" x y : int64 #)
             when ^T : uint64     = (# "and" x y : uint64 #)
             when ^T : uint32     = (# "and" x y : uint32 #)
             when ^T : int16      = (# "and" x y : int16 #)
             when ^T : uint16     = (# "and" x y : uint16 #)
             when ^T : nativeint  = (# "and" x y : nativeint #)
             when ^T : unativeint = (# "and" x y : unativeint #)
             when ^T : sbyte      = (# "and" x y : sbyte #)
             when ^T : byte       = (# "and" x y : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (&&&) : ^T * ^T -> ^T) (x, y))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (|||) (x: ^T) (y: ^T) : ^T = 
             BitwiseOrDynamic<(^T),(^T),(^T)> x y
             when ^T : int32      = (# "or" x y : int32 #)
             when ^T : int64      = (# "or" x y : int64 #)
             when ^T : uint64     = (# "or" x y : uint64 #)
             when ^T : uint32     = (# "or" x y : uint32 #)
             when ^T : int16      = (# "or" x y : int16 #)
             when ^T : uint16     = (# "or" x y : uint16 #)
             when ^T : nativeint  = (# "or" x y : nativeint #)
             when ^T : unativeint = (# "or" x y : unativeint #)
             when ^T : sbyte      = (# "or" x y : sbyte #)
             when ^T : byte       = (# "or" x y : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (|||) : ^T * ^T -> ^T) (x, y))

        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (^^^) (x: ^T) (y: ^T) : ^T = 
             ExclusiveOrDynamic<(^T),(^T),(^T)> x y
             when ^T : int32      = (# "xor" x y : int32 #)
             when ^T : int64      = (# "xor" x y : int64 #)
             when ^T : uint64     = (# "xor" x y : uint64 #)
             when ^T : uint32     = (# "xor" x y : uint32 #)
             when ^T : int16      = (# "xor" x y : int16 #)
             when ^T : uint16     = (# "xor" x y : uint16 #)
             when ^T : nativeint  = (# "xor" x y : nativeint #)
             when ^T : unativeint = (# "xor" x y : unativeint #)
             when ^T : sbyte      = (# "xor" x y : sbyte #)
             when ^T : byte       = (# "xor" x y : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (^^^) : ^T * ^T -> ^T) (x, y))
        
        [<NoDynamicInvocation(isLegacy=true)>]
        let inline (~~~) (value: ^T) : ^T = 
             LogicalNotDynamic<(^T),(^T)> value
             when ^T : int32      = (# "not" value : int32 #)
             when ^T : int64      = (# "not" value : int64 #)
             when ^T : uint64     = (# "not" value : uint64 #)
             when ^T : uint32     = (# "not" value : uint32 #)
             when ^T : nativeint  = (# "not" value : nativeint #)
             when ^T : unativeint = (# "not" value : unativeint #)
             when ^T : int16      = (# "conv.i2" (# "not" value : int32  #) : int16 #)
             when ^T : uint16     = (# "conv.u2" (# "not" value : uint32 #) : uint16 #)
             when ^T : sbyte      = (# "conv.i1" (# "not" value : int32  #) : sbyte #)
             when ^T : byte       = (# "conv.u1" (# "not" value : uint32 #) : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T: (static member (~~~) : ^T -> ^T) (value))

        let inline castToString (x:'T) = (# "" x : string #)  // internal

        // let rec (@) x y = match x with [] -> y | (h :: t) -> h :: (t @ y)
        let (@) list1 list2 = 
            match list1 with
            | [] -> list2
            | (h :: t) -> 
            match list2 with
            | [] -> list1
            | _ ->
              match t with
              | [] -> h :: list2
              | _ ->
                  let res = [h] 
                  let lastCons = PrivateListHelpers.appendToFreshConsTail res t 
                  PrivateListHelpers.setFreshConsTail lastCons list2
                  res

        [<CompiledName("Increment")>]
        let incr cell = cell.contents <- cell.contents + 1

        [<CompiledName("Decrement")>]
        let decr cell = cell.contents <- cell.contents - 1

        [<CompiledName("Exit")>]
        let exit (exitcode:int) = System.Environment.Exit(exitcode); failwith "System.Environment.Exit did not exit!"

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToByte")>]
        let inline byte (value: ^T) = 
             ExplicitDynamic<(^T), byte> value
             when ^T : string     = ParseByte (castToString value)
             when ^T : float      = (# "conv.u1" value  : byte #)
             when ^T : float32    = (# "conv.u1" value  : byte #)
             when ^T : int64      = (# "conv.u1" value  : byte #)
             when ^T : int32      = (# "conv.u1" value  : byte #)
             when ^T : int16      = (# "conv.u1" value  : byte #)
             when ^T : nativeint  = (# "conv.u1" value  : byte #)
             when ^T : sbyte      = (# "conv.u1" value  : byte #)
             when ^T : uint64     = (# "conv.u1" value  : byte #)
             when ^T : uint32     = (# "conv.u1" value  : byte #)
             when ^T : uint16     = (# "conv.u1" value  : byte #)
             when ^T : char       = (# "conv.u1" value  : byte #)
             when ^T : unativeint = (# "conv.u1" value  : byte #)
             when ^T : byte       = (# "conv.u1" value  : byte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> byte) (value))
            
        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToSByte")>]
        let inline sbyte (value: ^T) = 
             ExplicitDynamic<(^T), sbyte> value
             when ^T : string     = ParseSByte (castToString value)
             when ^T : float     = (# "conv.i1" value  : sbyte #)
             when ^T : float32   = (# "conv.i1" value  : sbyte #)
             when ^T : int64     = (# "conv.i1" value  : sbyte #)
             when ^T : int32     = (# "conv.i1" value  : sbyte #)
             when ^T : int16     = (# "conv.i1" value  : sbyte #)
             when ^T : nativeint = (# "conv.i1" value  : sbyte #)
             when ^T : sbyte     = (# "conv.i1" value  : sbyte #)
             when ^T : uint64     = (# "conv.i1" value  : sbyte #)
             when ^T : uint32     = (# "conv.i1" value  : sbyte #)
             when ^T : uint16     = (# "conv.i1" value  : sbyte #)
             when ^T : char       = (# "conv.i1" value  : sbyte #)
             when ^T : unativeint = (# "conv.i1" value  : sbyte #)
             when ^T : byte     = (# "conv.i1" value  : sbyte #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> sbyte) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToUInt16")>]
        let inline uint16 (value: ^T) = 
             ExplicitDynamic<(^T), uint16> value
             when ^T : string     = ParseUInt16 (castToString value)
             when ^T : float     = (# "conv.u2" value  : uint16 #)
             when ^T : float32   = (# "conv.u2" value  : uint16 #)
             when ^T : int64     = (# "conv.u2" value  : uint16 #)
             when ^T : int32     = (# "conv.u2" value  : uint16 #)
             when ^T : int16     = (# "conv.u2" value  : uint16 #)
             when ^T : nativeint = (# "conv.u2" value  : uint16 #)
             when ^T : sbyte     = (# "conv.u2" value  : uint16 #)
             when ^T : uint64     = (# "conv.u2" value  : uint16 #)
             when ^T : uint32     = (# "conv.u2" value  : uint16 #)
             when ^T : uint16     = (# "conv.u2" value  : uint16 #)
             when ^T : char       = (# "conv.u2" value  : uint16 #)
             when ^T : unativeint = (# "conv.u2" value  : uint16 #)
             when ^T : byte     = (# "conv.u2" value  : uint16 #)
             // According to the somewhat subtle rules of static optimizations,
             // this condition is used whenever ^T is resolved to a nominal type or witnesses are available
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint16) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToInt16")>]
        let inline int16 (value: ^T) = 
             ExplicitDynamic<(^T), int16> value
             when ^T : string     = ParseInt16 (castToString value)
             when ^T : float     = (# "conv.i2" value  : int16 #)
             when ^T : float32   = (# "conv.i2" value  : int16 #)
             when ^T : int64     = (# "conv.i2" value  : int16 #)
             when ^T : int32     = (# "conv.i2" value  : int16 #)
             when ^T : int16     = (# "conv.i2" value  : int16 #)
             when ^T : nativeint = (# "conv.i2" value  : int16 #)
             when ^T : sbyte     = (# "conv.i2" value  : int16 #)
             when ^T : uint64     = (# "conv.i2" value  : int16 #)
             when ^T : uint32     = (# "conv.i2" value  : int16 #)
             when ^T : uint16     = (# "conv.i2" value  : int16 #)
             when ^T : char       = (# "conv.i2" value  : int16 #)
             when ^T : unativeint = (# "conv.i2" value  : int16 #)
             when ^T : byte     = (# "conv.i2" value  : int16 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> int16) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToUInt32")>]
        let inline uint32 (value: ^T) = 
             ExplicitDynamic<(^T), uint32> value
             when ^T : string     = ParseUInt32 (castToString value)
             when ^T : float     = (# "conv.u4" value  : uint32 #)
             when ^T : float32   = (# "conv.u4" value  : uint32 #)
             when ^T : int64     = (# "conv.u4" value  : uint32 #)
             when ^T : nativeint = (# "conv.u4" value  : uint32 #)
             // For integers shorter that 32 bits, we must first 
             // sign-widen the signed integer to 32 bits, and then 
             // "convert" from signed int32 to unsigned int32
             // This is a no-op on IL stack (ECMA 335 Part III 1.5 Tables 8 & 9)
             when ^T : int32     = (# "" value : uint32 #)
             when ^T : int16     = (# "" value : uint32 #)
             when ^T : sbyte     = (# "" value : uint32 #)             
             when ^T : uint64     = (# "conv.u4" value  : uint32 #)
             when ^T : uint32     = (# "conv.u4" value  : uint32 #)
             when ^T : uint16     = (# "conv.u4" value  : uint32 #)
             when ^T : char       = (# "conv.u4" value  : uint32 #)
             when ^T : unativeint = (# "conv.u4" value  : uint32 #)
             when ^T : byte     = (# "conv.u4" value  : uint32 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint32) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToInt32")>]
        let inline int32 (value: ^T) = 
             ExplicitDynamic<(^T), int32> value
             when ^T : string     = ParseInt32 (castToString value)
             when ^T : float     = (# "conv.i4" value  : int32 #)
             when ^T : float32   = (# "conv.i4" value  : int32 #)
             when ^T : int64     = (# "conv.i4" value  : int32 #)
             when ^T : nativeint = (# "conv.i4" value  : int32 #)
             // For integers shorter that 32 bits, we sign-widen the signed integer to 32 bits
             // This is a no-op on IL stack (ECMA 335 Part III 1.5 Tables 8 & 9)
             when ^T : int32     = (# "" value  : int32 #)
             when ^T : int16     = (# "" value  : int32 #)
             when ^T : sbyte     = (# "" value  : int32 #)
             when ^T : uint64     = (# "conv.i4" value  : int32 #)             
             when ^T : uint32     = (# "" value  : int32 #) // Signed<->Unsigned conversion is a no-op on IL stack
             when ^T : uint16     = (# "conv.i4" value  : int32 #)
             when ^T : char       = (# "conv.i4" value  : int32 #)
             when ^T : unativeint = (# "conv.i4" value  : int32 #)
             when ^T : byte     = (# "conv.i4" value  : int32 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> int32) (value))

        [<CompiledName("ToInt")>]
        let inline int value = int32  value

        [<CompiledName("ToUInt")>]
        let inline uint value = uint32 value

        [<CompiledName("ToEnum")>]
        let inline enum< ^T when ^T : enum<int32> > (value:int32) : ^T = EnumOfValue value

        [<CompiledName("KeyValuePattern")>]
        let (|KeyValue|) (keyValuePair : KeyValuePair<'T,'U>) = (keyValuePair.Key, keyValuePair.Value)

        [<CompiledName("Infinity")>]
        let infinity = System.Double.PositiveInfinity

        [<CompiledName("NaN")>]
        let nan = System.Double.NaN 

        [<CompiledName("InfinitySingle")>]
        let infinityf = System.Single.PositiveInfinity

        [<CompiledName("NaNSingle")>]
        let nanf = System.Single.NaN 

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToUInt64")>]
        let inline uint64 (value: ^T) = 
             ExplicitDynamic<(^T), uint64> value
             when ^T : string     = ParseUInt64 (castToString value)
             when ^T : float     = (# "conv.u8" value  : uint64 #)
             when ^T : float32   = (# "conv.u8" value  : uint64 #)
             // we must first sign-widen the signed integer to 64 bits, and then 
             // "convert" from signed int64 to unsigned int64             
             // conv.i8 sign-widens the input, and on IL stack, 
             // conversion from signed to unsigned is a no-op (ECMA 335 Part III 1.5 Table 8)
             when ^T : int64     = (# "" value  : uint64 #)
             when ^T : int32     = (# "conv.i8" value  : uint64 #)
             when ^T : int16     = (# "conv.i8" value  : uint64 #)
             when ^T : nativeint = (# "conv.i8" value  : uint64 #)
             when ^T : sbyte     = (# "conv.i8" value  : uint64 #)
             when ^T : uint64     = (# "" value  : uint64 #)
             when ^T : uint32     = (# "conv.u8" value  : uint64 #)
             when ^T : uint16     = (# "conv.u8" value  : uint64 #)
             when ^T : char       = (# "conv.u8" value  : uint64 #)
             when ^T : unativeint = (# "conv.u8" value  : uint64 #)
             when ^T : byte     = (# "conv.u8" value  : uint64 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint64) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToInt64")>]
        let inline int64 (value: ^T) = 
             ExplicitDynamic<(^T), int64> value
             when ^T : string     = ParseInt64 (castToString value)
             when ^T : float     = (# "conv.i8" value  : int64 #)
             when ^T : float32   = (# "conv.i8" value  : int64 #)
             when ^T : int64     = (# "conv.i8" value  : int64 #)
             when ^T : int32     = (# "conv.i8" value  : int64 #)
             when ^T : int16     = (# "conv.i8" value  : int64 #)
             when ^T : nativeint = (# "conv.i8" value  : int64 #)
             when ^T : sbyte     = (# "conv.i8" value  : int64 #)
             // When converting unsigned integer, we should zero-widen them, NOT sign-widen 
             // No-op for uint64, conv.u8 for uint32, for smaller types conv.u8 and conv.i8 are identical.
             // For nativeint, conv.u8 works correctly both in 32 bit and 64 bit case.
             when ^T : uint64     = (# "" value  : int64 #)             
             when ^T : uint32     = (# "conv.u8" value  : int64 #)
             when ^T : uint16     = (# "conv.u8" value  : int64 #)
             when ^T : char       = (# "conv.u8" value  : int64 #)
             when ^T : unativeint = (# "conv.u8" value  : int64 #)
             when ^T : byte     = (# "conv.u8" value  : int64 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> int64) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToSingle")>]
        let inline float32 (value: ^T) = 
             ExplicitDynamic<(^T), float32> value
             when ^T : string     = ParseSingle (castToString value)
             when ^T : float     = (# "conv.r4" value  : float32 #)
             // NOTE: float32 should convert its argument to 32-bit float even when applied to a higher precision float stored in a register. See devdiv2#49888.
             when ^T : float32   = (# "conv.r4" value  : float32 #)
             when ^T : int64     = (# "conv.r4" value  : float32 #)
             when ^T : int32     = (# "conv.r4" value  : float32 #)
             when ^T : int16     = (# "conv.r4" value  : float32 #)
             when ^T : nativeint = (# "conv.r4" value  : float32 #)
             when ^T : sbyte     = (# "conv.r4" value  : float32 #)
             when ^T : uint64     = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : uint32     = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : uint16     = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : char       = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : unativeint = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : byte     = (# "conv.r.un conv.r4" value  : float32 #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> float32) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToDouble")>]
        let inline float (value: ^T) = 
             ExplicitDynamic<(^T), float> value
             when ^T : string     = ParseDouble (castToString value)
             // NOTE: float should convert its argument to 64-bit float even when applied to a higher precision float stored in a register. See devdiv2#49888.
             when ^T : float     = (# "conv.r8" value  : float #)
             when ^T : float32   = (# "conv.r8" value  : float #)
             when ^T : int64     = (# "conv.r8" value  : float #)
             when ^T : int32     = (# "conv.r8" value  : float #)
             when ^T : int16     = (# "conv.r8" value  : float #)
             when ^T : nativeint = (# "conv.r8" value  : float #)
             when ^T : sbyte     = (# "conv.r8" value  : float #)
             when ^T : uint64     = (# "conv.r.un conv.r8" value  : float #)
             when ^T : uint32     = (# "conv.r.un conv.r8" value  : float #)
             when ^T : uint16     = (# "conv.r.un conv.r8" value  : float #)
             when ^T : char       = (# "conv.r.un conv.r8" value  : float #)
             when ^T : unativeint = (# "conv.r.un conv.r8" value  : float #)
             when ^T : byte       = (# "conv.r.un conv.r8" value  : float #)
             when ^T : decimal    = (Convert.ToDouble((# "" value : decimal #))) 
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> float) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToDecimal")>]
        let inline decimal (value: ^T) = 
             ExplicitDynamic<(^T), decimal> value
             when ^T : string     = (System.Decimal.Parse(castToString value,NumberStyles.Float,CultureInfo.InvariantCulture))
             when ^T : float      = (System.Convert.ToDecimal((# "" value : float #))) 
             when ^T : float32    = (System.Convert.ToDecimal((# "" value : float32 #))) 
             when ^T : int64      = (System.Convert.ToDecimal((# "" value : int64 #))) 
             when ^T : int32      = (System.Convert.ToDecimal((# "" value : int32 #))) 
             when ^T : int16      = (System.Convert.ToDecimal((# "" value : int16 #))) 
             when ^T : nativeint  = (System.Convert.ToDecimal(int64 (# "" value : nativeint #))) 
             when ^T : sbyte      = (System.Convert.ToDecimal((# "" value : sbyte #))) 
             when ^T : uint64     = (System.Convert.ToDecimal((# "" value : uint64 #))) 
             when ^T : uint32     = (System.Convert.ToDecimal((# "" value : uint32 #))) 
             when ^T : uint16     = (System.Convert.ToDecimal((# "" value : uint16 #))) 
             when ^T : unativeint = (System.Convert.ToDecimal(uint64 (# "" value : unativeint #))) 
             when ^T : byte       = (System.Convert.ToDecimal((# "" value : byte #))) 
             when ^T : decimal    = (# "" value : decimal #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> decimal) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToUIntPtr")>]
        let inline unativeint (value: ^T) = 
             ExplicitDynamic<(^T), unativeint> value
             when ^T : string     = ParseUIntPtr (castToString value)
             when ^T : float     = (# "conv.u" value  : unativeint #)
             when ^T : float32   = (# "conv.u" value  : unativeint #)
             // Narrower signed types we sign-extend.
             // Same length signed types we leave as such (so -1 gets reinterpreted as unsigned MaxValue).
             // Wider signed types we truncate.
             // conv.i does just that for both 32 and 64 bit case of nativeint, and conversion from nativeint is no-op.
             when ^T : int64     = (# "conv.i" value  : unativeint #)
             when ^T : int32     = (# "conv.i" value  : unativeint #)
             when ^T : int16     = (# "conv.i" value  : unativeint #)
             when ^T : nativeint = (# "" value  : unativeint #)
             when ^T : sbyte     = (# "conv.i" value  : unativeint #)
             when ^T : uint64     = (# "conv.u" value  : unativeint #)
             when ^T : uint32     = (# "conv.u" value  : unativeint #)
             when ^T : uint16     = (# "conv.u" value  : unativeint #)
             when ^T : char       = (# "conv.u" value  : unativeint #)
             when ^T : unativeint = (# "" value  : unativeint #)
             when ^T : byte       = (# "conv.u" value  : unativeint #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> unativeint) (value))

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToIntPtr")>]
        let inline nativeint (value: ^T) = 
             ExplicitDynamic<(^T), nativeint> value
             when ^T : string     = ParseIntPtr (castToString value)
             when ^T : float      = (# "conv.i" value  : nativeint #)
             when ^T : float32    = (# "conv.i" value  : nativeint #)
             when ^T : int64      = (# "conv.i" value  : nativeint #)
             when ^T : int32      = (# "conv.i" value  : nativeint #)
             when ^T : int16      = (# "conv.i" value  : nativeint #)
             when ^T : nativeint  = (# "conv.i" value  : nativeint #)
             when ^T : sbyte      = (# "conv.i" value  : nativeint #)
             // Narrower unsigned types we zero-extend.
             // Same length unsigned types we leave as such (so unsigned MaxValue (all-bits-set) gets reinterpreted as -1).
             // Wider unsigned types we truncate.
             // conv.u does just that for both 32- and 64-bit-wide nativeint, and conversion from unativeint is no-op.
             when ^T : uint64     = (# "conv.u" value  : nativeint #)
             when ^T : uint32     = (# "conv.u" value  : nativeint #)
             when ^T : uint16     = (# "conv.u" value  : nativeint #)
             when ^T : char       = (# "conv.u" value  : nativeint #)
             when ^T : unativeint = (# "" value  : nativeint #)
             when ^T : byte       = (# "conv.i" value  : nativeint #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> nativeint) (value))

        [<CompiledName("ToString")>]
        let inline string (value: 'T) = 
             anyToString "" value

             when 'T : string =
                if value = unsafeDefault<'T> then ""
                else (# "" value : string #)     // force no-op

             // Using 'let x = (# ... #) in x.ToString()' leads to better IL, without it, an extra stloc and ldloca.s (get address-of)
             // gets emitted, which are unnecessary. With it, the extra address-of-variable is not created
             when 'T : float      = let x = (# "" value : float #)      in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : float32    = let x = (# "" value : float32 #)    in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : decimal    = let x = (# "" value : decimal #)    in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : BigInteger = let x = (# "" value : BigInteger #) in x.ToString(null, CultureInfo.InvariantCulture)
             
             // no IFormattable
             when 'T : char       = let x = (# "" value : 'T #)         in x.ToString()     // use 'T, because char can be an enum  
             when 'T : bool       = let x = (# "" value : bool #)       in x.ToString()
             when 'T : nativeint  = let x = (# "" value : nativeint #)  in x.ToString()
             when 'T : unativeint = let x = (# "" value : unativeint #) in x.ToString()

             // Integral types can be enum:
             // It is not possible to distinguish statically between Enum and (any type of) int. For signed types we have 
             // to use IFormattable::ToString, as the minus sign can be overridden. Using boxing we'll print their symbolic
             // value if it's an enum, e.g.: 'ConsoleKey.Backspace' gives "Backspace", rather than "8")
             when 'T : sbyte      = (box value :?> IFormattable).ToString(null, CultureInfo.InvariantCulture)
             when 'T : int16      = (box value :?> IFormattable).ToString(null, CultureInfo.InvariantCulture)
             when 'T : int32      = (box value :?> IFormattable).ToString(null, CultureInfo.InvariantCulture)
             when 'T : int64      = (box value :?> IFormattable).ToString(null, CultureInfo.InvariantCulture)

             // unsigned integral types have equal behavior with 'T::ToString() vs IFormattable::ToString
             // this allows us to issue the 'constrained' opcode with 'callvirt'
             when 'T : byte       = let x = (# "" value : 'T #) in x.ToString()
             when 'T : uint16     = let x = (# "" value : 'T #) in x.ToString()
             when 'T : uint32     = let x = (# "" value : 'T #) in x.ToString()
             when 'T : uint64     = let x = (# "" value : 'T #) in x.ToString()


             // other common mscorlib System struct types
             when 'T : DateTime         = let x = (# "" value : DateTime #) in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : DateTimeOffset   = let x = (# "" value : DateTimeOffset #) in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : TimeSpan         = let x = (# "" value : TimeSpan #) in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T : Guid             = let x = (# "" value : Guid #) in x.ToString(null, CultureInfo.InvariantCulture)
             when 'T struct = 
                match box value with
                | :? IFormattable as f -> f.ToString(null, CultureInfo.InvariantCulture)
                | _ -> value.ToString()

             // other commmon mscorlib reference types
             when 'T : StringBuilder =
                if value = unsafeDefault<'T> then ""
                else let x = (# "" value : StringBuilder #) in x.ToString()

             when 'T : IFormattable =
                if value = unsafeDefault<'T> then ""
                else let x = (# "" value : IFormattable #) in x.ToString(null, CultureInfo.InvariantCulture)

        [<NoDynamicInvocation(isLegacy=true)>]
        [<CompiledName("ToChar")>]
        let inline char (value: ^T) = 
             ExplicitDynamic<(^T), char> value
             when ^T : string     = (System.Char.Parse(castToString value))
             when ^T : float      = (# "conv.u2" value  : char #)
             when ^T : float32    = (# "conv.u2" value  : char #)
             when ^T : int64      = (# "conv.u2" value  : char #)
             when ^T : int32      = (# "conv.u2" value  : char #)
             when ^T : int16      = (# "conv.u2" value  : char #)
             when ^T : nativeint  = (# "conv.u2" value  : char #)
             when ^T : sbyte      = (# "conv.u2" value  : char #)
             when ^T : uint64     = (# "conv.u2" value  : char #)
             when ^T : uint32     = (# "conv.u2" value  : char #)
             when ^T : uint16     = (# "conv.u2" value  : char #)
             when ^T : char       = (# "conv.u2" value  : char #)
             when ^T : unativeint = (# "conv.u2" value  : char #)
             when ^T : byte       = (# "conv.u2" value  : char #)
             when ^T : ^T = (^T : (static member op_Explicit: ^T -> char) (value))
        
        module NonStructuralComparison = 
            /// Static less-than with static optimizations for some well-known cases.
            let inline (<) (x:^T) (y:^U) = 
                LessThanDynamic<(^T), (^U), bool> x y
                when ^T : bool   = (# "clt" x y : bool #)
                when ^T : sbyte  = (# "clt" x y : bool #)
                when ^T : int16  = (# "clt" x y : bool #)
                when ^T : int32  = (# "clt" x y : bool #)
                when ^T : int64  = (# "clt" x y : bool #)
                when ^T : byte   = (# "clt.un" x y : bool #)
                when ^T : uint16 = (# "clt.un" x y : bool #)
                when ^T : uint32 = (# "clt.un" x y : bool #)
                when ^T : uint64 = (# "clt.un" x y : bool #)
                when ^T : unativeint = (# "clt.un" x y : bool #)
                when ^T : nativeint  = (# "clt" x y : bool #)
                when ^T : float  = (# "clt" x y : bool #) 
                when ^T : float32= (# "clt" x y : bool #) 
                when ^T : char   = (# "clt" x y : bool #)
                when ^T : decimal     = System.Decimal.op_LessThan ((# "" x:decimal #), (# "" y:decimal #))
                when ^T : string     = (# "clt" (System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))) 0 : bool #)             
                when ^T : ^T = ((^T or ^U): (static member (<) : ^T * ^U -> bool) (x,y))

            /// Static greater-than with static optimizations for some well-known cases.
            let inline (>) (x:^T) (y:^U) = 
                GreaterThanDynamic<(^T), (^U), bool> x y
                when 'T : bool       = (# "cgt" x y : bool #)
                when 'T : sbyte      = (# "cgt" x y : bool #)
                when 'T : int16      = (# "cgt" x y : bool #)
                when 'T : int32      = (# "cgt" x y : bool #)
                when 'T : int64      = (# "cgt" x y : bool #)
                when 'T : nativeint  = (# "cgt" x y : bool #)
                when 'T : byte       = (# "cgt.un" x y : bool #)
                when 'T : uint16     = (# "cgt.un" x y : bool #)
                when 'T : uint32     = (# "cgt.un" x y : bool #)
                when 'T : uint64     = (# "cgt.un" x y : bool #)
                when 'T : unativeint = (# "cgt.un" x y : bool #)
                when 'T : float      = (# "cgt" x y : bool #) 
                when 'T : float32    = (# "cgt" x y : bool #) 
                when 'T : char       = (# "cgt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_GreaterThan ((# "" x:decimal #), (# "" y:decimal #))
                when ^T : string     = (# "cgt" (System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))) 0 : bool #)             
                when ^T : ^T = ((^T or ^U): (static member (>) : ^T * ^U -> bool) (x,y))

            /// Static less-than-or-equal with static optimizations for some well-known cases.
            let inline (<=) (x:^T) (y:^U) = 
                LessThanOrEqualDynamic<(^T), (^U), bool> x y
                when 'T : bool       = not (# "cgt" x y : bool #)
                when 'T : sbyte      = not (# "cgt" x y : bool #)
                when 'T : int16      = not (# "cgt" x y : bool #)
                when 'T : int32      = not (# "cgt" x y : bool #)
                when 'T : int64      = not (# "cgt" x y : bool #)
                when 'T : nativeint  = not (# "cgt" x y : bool #)
                when 'T : byte       = not (# "cgt.un" x y : bool #)
                when 'T : uint16     = not (# "cgt.un" x y : bool #)
                when 'T : uint32     = not (# "cgt.un" x y : bool #)
                when 'T : uint64     = not (# "cgt.un" x y : bool #)
                when 'T : unativeint = not (# "cgt.un" x y : bool #)
                when 'T : float      = not (# "cgt.un" x y : bool #) 
                when 'T : float32    = not (# "cgt.un" x y : bool #) 
                when 'T : char       = not (# "cgt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_LessThanOrEqual ((# "" x:decimal #), (# "" y:decimal #))
                when ^T : string     = not (# "cgt" (System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))) 0 : bool #)             
                when ^T : ^T = ((^T or ^U): (static member (<=) : ^T * ^U -> bool) (x,y))

            /// Static greater-than-or-equal with static optimizations for some well-known cases.
            let inline (>=) (x:^T) (y:^U) = 
                GreaterThanOrEqualDynamic<(^T), (^U), bool> x y
                when 'T : bool       = not (# "clt" x y : bool #)
                when 'T : sbyte      = not (# "clt" x y : bool #)
                when 'T : int16      = not (# "clt" x y : bool #)
                when 'T : int32      = not (# "clt" x y : bool #)
                when 'T : int64      = not (# "clt" x y : bool #)
                when 'T : nativeint  = not (# "clt" x y : bool #)
                when 'T : byte       = not (# "clt.un" x y : bool #)
                when 'T : uint16     = not (# "clt.un" x y : bool #)
                when 'T : uint32     = not (# "clt.un" x y : bool #)
                when 'T : uint64     = not (# "clt.un" x y : bool #)
                when 'T : unativeint = not (# "clt.un" x y : bool #)
                when 'T : float      = not (# "clt.un" x y : bool #) 
                when 'T : float32    = not (# "clt.un" x y : bool #)
                when 'T : char       = not (# "clt" x y : bool #)
                when 'T : decimal     = System.Decimal.op_GreaterThanOrEqual ((# "" x:decimal #), (# "" y:decimal #))
                when ^T : string     = not (# "clt" (System.String.CompareOrdinal((# "" x : string #),(# "" y : string #))) 0 : bool #)             
                when ^T : ^T = ((^T or ^U): (static member (>=) : ^T * ^U -> bool) (x,y))

            /// Static greater-than-or-equal with static optimizations for some well-known cases.
            let inline (=) (x:^T) (y:^T) = 
                EqualityDynamic<(^T), (^T), bool> x y
                when ^T : bool    = (# "ceq" x y : bool #)
                when ^T : sbyte   = (# "ceq" x y : bool #)
                when ^T : int16   = (# "ceq" x y : bool #)
                when ^T : int32   = (# "ceq" x y : bool #)
                when ^T : int64   = (# "ceq" x y : bool #)
                when ^T : byte    = (# "ceq" x y : bool #)
                when ^T : uint16  = (# "ceq" x y : bool #)
                when ^T : uint32  = (# "ceq" x y : bool #)
                when ^T : uint64  = (# "ceq" x y : bool #)
                when ^T : float   = (# "ceq" x y : bool #)
                when ^T : float32 = (# "ceq" x y : bool #)
                when ^T : char    = (# "ceq" x y : bool #)
                when ^T : nativeint  = (# "ceq" x y : bool #)
                when ^T : unativeint  = (# "ceq" x y : bool #)
                when ^T : string  = String.Equals((# "" x : string #),(# "" y : string #))
                when ^T : decimal = Decimal.op_Equality((# "" x:decimal #), (# "" y:decimal #))
                when ^T : ^T = (^T : (static member (=) : ^T * ^T -> bool) (x,y))

            let inline (<>) (x:^T) (y:^T) = 
                InequalityDynamic<(^T), (^T), bool> x y
                when ^T : bool    = not (# "ceq" x y : bool #)
                when ^T : sbyte   = not (# "ceq" x y : bool #)
                when ^T : int16   = not (# "ceq" x y : bool #)
                when ^T : int32   = not (# "ceq" x y : bool #)
                when ^T : int64   = not (# "ceq" x y : bool #)
                when ^T : byte    = not (# "ceq" x y : bool #)
                when ^T : uint16  = not (# "ceq" x y : bool #)
                when ^T : uint32  = not (# "ceq" x y : bool #)
                when ^T : uint64  = not (# "ceq" x y : bool #)
                when ^T : float   = not (# "ceq" x y : bool #)
                when ^T : float32 = not (# "ceq" x y : bool #)
                when ^T : char    = not (# "ceq" x y : bool #)
                when ^T : nativeint  = not (# "ceq" x y : bool #)
                when ^T : unativeint  = not (# "ceq" x y : bool #)
                when ^T : string  = not (System.String.Equals((# "" x : string #),(# "" y : string #)))
                when ^T : decimal     = System.Decimal.op_Inequality((# "" x:decimal #), (# "" y:decimal #))
                when ^T : ^T = (^T : (static member (<>) : ^T * ^T -> bool) (x,y))

            // static comparison (ER mode) with static optimizations for some well-known cases
            [<CompiledName("Compare")>]
            let inline compare (e1: ^T) (e2: ^T) : int = 
                 (if e1 < e2 then -1 elif e1 > e2 then 1 else 0)
                 when ^T : bool   = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : sbyte  = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : int16  = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : int32  = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : int64  = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : nativeint  = if (# "clt" e1 e2 : bool #) then (-1) else (# "cgt" e1 e2 : int #)
                 when ^T : byte   = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : uint16 = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : uint32 = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : uint64 = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : unativeint = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : float  = if   (# "clt" e1 e2 : bool #) then (-1)
                                    elif (# "cgt" e1 e2 : bool #) then (1)
                                    elif (# "ceq" e1 e2 : bool #) then (0)
                                    elif (# "ceq" e2 e2 : bool #) then (-1)
                                    else (# "ceq" e1 e1 : int #)
                 when ^T : float32 = if   (# "clt" e1 e2 : bool #) then (-1)
                                     elif (# "cgt" e1 e2 : bool #) then (1)
                                     elif (# "ceq" e1 e2 : bool #) then (0)
                                     elif (# "ceq" e2 e2 : bool #) then (-1)
                                     else (# "ceq" e1 e1 : int #)
                 when ^T : char   = if (# "clt.un" e1 e2 : bool #) then (-1) else (# "cgt.un" e1 e2 : int #)
                 when ^T : string = 
                     // NOTE: we don't have to null check here because System.String.CompareOrdinal
                     // gives reliable results on null values.
                     System.String.CompareOrdinal((# "" e1 : string #),(# "" e2 : string #))
                 when ^T : decimal     = System.Decimal.Compare((# "" e1:decimal #), (# "" e2:decimal #))

            [<CompiledName("Max")>]
            let inline max (e1: ^T) (e2: ^T) = 
                (if e1 < e2 then e2 else e1)
                when ^T : float         = (System.Math.Max : float * float -> float)(retype<_,float> e1, retype<_,float> e2)
                when ^T : float32       = (System.Math.Max : float32 * float32 -> float32)(retype<_,float32> e1, retype<_,float32> e2)

            [<CompiledName("Min")>]
            let inline min (e1: ^T) (e2: ^T) = 
                (if e1 < e2 then e1 else e2)
                when ^T : float         = (System.Math.Min : float * float -> float)(retype<_,float> e1, retype<_,float> e2)
                when ^T : float32       = (System.Math.Min : float32 * float32 -> float32)(retype<_,float32> e1, retype<_,float32> e2)

            [<CompiledName("Hash")>]
            let inline hash (value:'T) = 
                value.GetHashCode()
                when 'T : bool   = (# "" value : int #)
                when 'T : int32  = (# "" value : int #)
                when 'T : byte   = (# "" value : int #)
                when 'T : uint32 = (# "" value : int #)
                when 'T : char = HashCompare.HashChar (# "" value : char #)
                when 'T : sbyte = HashCompare.HashSByte (# "" value : sbyte #)
                when 'T : int16 = HashCompare.HashInt16 (# "" value : int16 #)
                when 'T : int64 = HashCompare.HashInt64 (# "" value : int64 #)
                when 'T : uint64 = HashCompare.HashUInt64 (# "" value : uint64 #)
                when 'T : nativeint = HashCompare.HashIntPtr (# "" value : nativeint #)
                when 'T : unativeint = HashCompare.HashUIntPtr (# "" value : unativeint #)
                when 'T : uint16 = (# "" value : int #)                    
                when 'T : string = HashCompare.HashString  (# "" value : string #)

        module Attributes = 
            open System.Runtime.CompilerServices

            [<assembly: System.Runtime.InteropServices.ComVisible(false)>]
            [<assembly: System.CLSCompliant(true)>]
            [<assembly: System.Security.SecurityTransparent>] // assembly is fully transparent
#if CROSS_PLATFORM_COMPILER
#else
            [<assembly: System.Security.SecurityRules(System.Security.SecurityRuleSet.Level2)>] // v4 transparency; soon to be the default, but not yet
#endif
            do ()

#if FX_NO_MONITOR_REPORTS_LOCKTAKEN
        [<CompiledName("Lock")>]
        let inline lock (lockobj : 'T when 'T : not struct) f  = 
            System.Threading.Monitor.Enter(lockobj);
            try f()
            finally
                System.Threading.Monitor.Exit(lockobj)
#else
        [<CompiledName("Lock")>]
        let inline lock (lockObject : 'T when 'T : not struct) action  = 
            let mutable lockTaken = false
            try 
                System.Threading.Monitor.Enter(lockObject, &lockTaken);
                action()
            finally
                if lockTaken then
                    System.Threading.Monitor.Exit(lockObject)
#endif


        [<CompiledName("Using")>]
        let using (resource : 'T when 'T :> System.IDisposable) action = 
            try action(resource)
            finally match (box resource) with null -> () | _ -> resource.Dispose()

        [<CompiledName("TypeOf")>]
        let inline typeof<'T> = BasicInlinedOperations.typeof<'T>

        [<CompiledName("NameOf"); CompilerMessage(ExperimentalAttributeMessages.NotSupportedYet, 3501, IsError=true)>]
        let inline nameof (_: 'T) : string = raise (Exception "may not call directly, should always be optimized away")

        [<CompiledName("MethodHandleOf")>]
        let methodhandleof (_call: ('T -> 'TResult)) : System.RuntimeMethodHandle = raise (Exception "may not call directly, should always be optimized away")

        [<CompiledName("TypeDefOf")>]
        let inline typedefof<'T> = BasicInlinedOperations.typedefof<'T>

        [<CompiledName("SizeOf")>]
        let inline sizeof<'T> = BasicInlinedOperations.sizeof<'T>

        [<CompiledName("Hash")>]
        let inline hash (obj: 'T) = LanguagePrimitives.GenericHash obj

        [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
        let inline limitedHash (limit:int) (obj: 'T) = LanguagePrimitives.GenericLimitedHash limit obj

        [<CompiledName("Identity")>]
        let id x = x

        [<CompiledName("Not")>]
        let inline not (value: bool) = match value with true -> false | _ -> true

        // std* are TypeFunctions with the effect of reading the property on instantiation.
        // So, direct uses of stdout should capture the current System.Console.Out at that point.
        [<CompiledName("ConsoleIn")>]
        let stdin<'T>  = System.Console.In

        [<CompiledName("ConsoleOut")>]
        let stdout<'T> = System.Console.Out

        [<CompiledName("ConsoleError")>]
        let stderr<'T> = System.Console.Error

        module Unchecked =

            [<CompiledName("Unbox")>]
            let inline unbox<'T> (v:obj) = unboxPrim<'T> v

            [<CompiledName("DefaultOf")>]
            let inline defaultof<'T> = unsafeDefault<'T>

            [<CompiledName("Compare")>]
            let inline compare x y = GenericComparison x y

            [<CompiledName("Equals")>]
            let inline equals x y = GenericEquality x y

            [<CompiledName("Hash")>]
            let inline hash x = GenericHash x

        module Checked = 
        
            let inline (+) (x: ^T) (y: ^U) : ^V = 
                 CheckedAdditionDynamic<(^T),(^U),(^V)>  x y 
                 when ^T : int32       and ^U : int32      = (# "add.ovf" x y : int32 #)
                 when ^T : float       and ^U : float      = (# "add" x y : float #)
                 when ^T : float32     and ^U : float32    = (# "add" x y : float32 #)
                 when ^T : int64       and ^U : int64      = (# "add.ovf" x y : int64 #)
                 when ^T : uint64      and ^U : uint64     = (# "add.ovf.un" x y : uint64 #)
                 when ^T : uint32      and ^U : uint32     = (# "add.ovf.un" x y : uint32 #)
                 when ^T : nativeint   and ^U : nativeint  = (# "add.ovf" x y : nativeint #)
                 when ^T : unativeint  and ^U : unativeint = (# "add.ovf.un" x y : unativeint #)
                 when ^T : int16       and ^U : int16      = (# "conv.ovf.i2" (# "add.ovf" x y : int32 #) : int16 #)
                 when ^T : uint16      and ^U : uint16     = (# "conv.ovf.u2.un" (# "add.ovf.un" x y : uint32 #) : uint16 #)
                 when ^T : char        and ^U : char       = (# "conv.ovf.u2.un" (# "add.ovf.un" x y : uint32 #) : char #)
                 when ^T : sbyte       and ^U : sbyte      = (# "conv.ovf.i1" (# "add.ovf" x y : int32 #) : sbyte #)
                 when ^T : byte        and ^U : byte       = (# "conv.ovf.u1.un" (# "add.ovf.un" x y : uint32 #) : byte #)
                 when ^T : string      and ^U : string     = (# "" (System.String.Concat((# "" x : string #),(# "" y : string #))) : ^T #)
                 when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Addition((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
                 // According to the somewhat subtle rules of static optimizations,
                 // this condition is used whenever ^T is resolved to a nominal type
                 // That is, not in the generic implementation of '+'
                 when ^T : ^T = ((^T or ^U): (static member (+) : ^T * ^U -> ^V) (x,y))

            let inline (-) (x: ^T) (y: ^U) : ^V = 
                 CheckedSubtractionDynamic<(^T),(^U),(^V)>  x y 
                 when ^T : int32      and ^U : int32      = (# "sub.ovf" x y : int32 #)
                 when ^T : float      and ^U : float      = (# "sub" x y : float #)
                 when ^T : float32    and ^U : float32    = (# "sub" x y : float32 #)
                 when ^T : int64      and ^U : int64      = (# "sub.ovf" x y : int64 #)
                 when ^T : uint64     and ^U : uint64     = (# "sub.ovf.un" x y : uint64 #)
                 when ^T : uint32     and ^U : uint32     = (# "sub.ovf.un" x y : uint32 #)
                 when ^T : nativeint  and ^U : nativeint  = (# "sub.ovf" x y : nativeint #)
                 when ^T : unativeint and ^U : unativeint = (# "sub.ovf.un" x y : unativeint #)
                 when ^T : int16       and ^U : int16      = (# "conv.ovf.i2" (# "sub.ovf" x y : int32 #) : int16 #)
                 when ^T : uint16      and ^U : uint16     = (# "conv.ovf.u2.un" (# "sub.ovf.un" x y : uint32 #) : uint16 #)
                 when ^T : sbyte       and ^U : sbyte      = (# "conv.ovf.i1" (# "sub.ovf" x y : int32 #) : sbyte #)
                 when ^T : byte        and ^U : byte       = (# "conv.ovf.u1.un" (# "sub.ovf.un" x y : uint32 #) : byte #)
                 when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Subtraction((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
                 when ^T : ^T = ((^T or ^U): (static member (-) : ^T * ^U -> ^V) (x,y))

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline (~-) (value: ^T) : ^T = 
                 CheckedUnaryNegationDynamic<(^T),(^T)>  value 
                 when ^T : int32     = (# "sub.ovf" 0 value  : int32 #)
                 when ^T : float     = (# "neg" value  : float #)
                 when ^T : float32   = (# "neg" value  : float32 #)
                 when ^T : int64     = (# "sub.ovf" 0L value  : int64 #)
                 when ^T : int16     = (# "sub.ovf" 0s value  : int16 #)
                 when ^T : nativeint = (# "sub.ovf" 0n value  : nativeint #)
                 when ^T : sbyte     = (# "sub.ovf" 0y value  : sbyte #)
                 when ^T : decimal   = (# "" (System.Decimal.op_UnaryNegation((# "" value : decimal #))) : ^T #)
                 when ^T : ^T = (^T : (static member (~-) : ^T -> ^T) (value))

            let inline ( * ) (x: ^T) (y: ^U) : ^V = 
                 CheckedMultiplyDynamic<(^T),(^U),(^V)>  x y 
                 when ^T : sbyte       and ^U : sbyte      = (# "conv.ovf.i1" (# "mul.ovf" x y : int32 #) : sbyte #)
                 when ^T : int16       and ^U : int16      = (# "conv.ovf.i2" (# "mul.ovf" x y : int32 #) : int16 #)
                 when ^T : int64      and ^U : int64      = (# "mul.ovf" x y : int64 #)
                 when ^T : int32      and ^U : int32      = (# "mul.ovf" x y : int32 #)
                 when ^T : nativeint  and ^U : nativeint  = (# "mul.ovf" x y : nativeint #)
                 when ^T : byte        and ^U : byte       = (# "conv.ovf.u1.un" (# "mul.ovf.un" x y : uint32 #) : byte #)
                 when ^T : uint16      and ^U : uint16     = (# "conv.ovf.u2.un" (# "mul.ovf.un" x y : uint32 #) : uint16 #)
                 when ^T : uint32     and ^U : uint32     = (# "mul.ovf.un" x y : uint32 #)
                 when ^T : uint64     and ^U : uint64     = (# "mul.ovf.un" x y : uint64 #)
                 when ^T : unativeint and ^U : unativeint = (# "mul.ovf.un" x y : unativeint #)
                 when ^T : float      and ^U : float      = (# "mul" x y : float #)
                 when ^T : float32    and ^U : float32    = (# "mul" x y : float32 #)
                 when ^T : decimal     and ^U : decimal    = (# "" (System.Decimal.op_Multiply((# "" x : decimal #),(# "" y : decimal #))) : ^V #)
                 // According to the somewhat subtle rules of static optimizations,
                 // this condition is used whenever ^T is resolved to a nominal type
                 // That is, not in the generic implementation of '*'
                 when ^T : ^T = ((^T or ^U): (static member (*) : ^T * ^U -> ^V) (x,y))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToByte")>]
            let inline byte (value: ^T) = 
                 ExplicitDynamic<(^T),byte> value 
                 when ^T : string     = ParseByte (castToString value)
                 when ^T : float     = (# "conv.ovf.u1" value  : byte #)
                 when ^T : float32   = (# "conv.ovf.u1" value  : byte #)
                 when ^T : int64     = (# "conv.ovf.u1" value  : byte #)
                 when ^T : int32     = (# "conv.ovf.u1" value  : byte #)
                 when ^T : int16     = (# "conv.ovf.u1" value  : byte #)
                 when ^T : nativeint = (# "conv.ovf.u1" value  : byte #)
                 when ^T : sbyte     = (# "conv.ovf.u1" value  : byte #)
                 when ^T : uint64     = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : uint32     = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : uint16     = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : char       = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : unativeint = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : byte     = (# "conv.ovf.u1.un" value  : byte #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> byte) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToSByte")>]
            let inline sbyte (value: ^T) = 
                 ExplicitDynamic<(^T),sbyte> value 
                 when ^T : string     = ParseSByte (castToString value)
                 when ^T : float     = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : float32   = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : int64     = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : int32     = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : int16     = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : nativeint = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : sbyte     = (# "conv.ovf.i1" value  : sbyte #)
                 when ^T : uint64     = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : uint32     = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : uint16     = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : char       = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : unativeint = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : byte     = (# "conv.ovf.i1.un" value  : sbyte #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> sbyte) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToUInt16")>]
            let inline uint16 (value: ^T) = 
                 ExplicitDynamic<(^T),uint16> value 
                 when ^T : string     = ParseUInt16 (castToString value)
                 when ^T : float      = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : float32    = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : int64      = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : int32      = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : int16      = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : nativeint  = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : sbyte      = (# "conv.ovf.u2" value  : uint16 #)
                 when ^T : uint64     = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : uint32     = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : uint16     = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : char       = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : unativeint = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : byte       = (# "conv.ovf.u2.un" value  : uint16 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint16) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToChar")>]
            let inline char (value: ^T) = 
                 ExplicitDynamic<(^T), char> value 
                 when ^T : string     = (System.Char.Parse(castToString value))
                 when ^T : float      = (# "conv.ovf.u2" value  : char #)
                 when ^T : float32    = (# "conv.ovf.u2" value  : char #)
                 when ^T : int64      = (# "conv.ovf.u2" value  : char #)
                 when ^T : int32      = (# "conv.ovf.u2" value  : char #)
                 when ^T : int16      = (# "conv.ovf.u2" value  : char #)
                 when ^T : nativeint  = (# "conv.ovf.u2" value  : char #)
                 when ^T : sbyte      = (# "conv.ovf.u2" value  : char #)
                 when ^T : uint64     = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : uint32     = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : uint16     = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : char       = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : unativeint = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : byte       = (# "conv.ovf.u2.un" value  : char #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> char) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToInt16")>]
            let inline int16 (value: ^T) = 
                 ExplicitDynamic<(^T), int16> value 
                 when ^T : string     = ParseInt16 (castToString value)
                 when ^T : float     = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : float32   = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : int64     = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : int32     = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : int16     = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : nativeint = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : sbyte     = (# "conv.ovf.i2" value  : int16 #)
                 when ^T : uint64     = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : uint32     = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : uint16     = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : char       = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : unativeint = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : byte     = (# "conv.ovf.i2.un" value  : int16 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> int16) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToUInt32")>]
            let inline uint32 (value: ^T) = 
                 ExplicitDynamic<(^T), uint32> value 
                 when ^T : string     = ParseUInt32 (castToString value)
                 when ^T : float     = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : float32   = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : int64     = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : int32     = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : int16     = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : nativeint = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : sbyte     = (# "conv.ovf.u4" value  : uint32 #)
                 when ^T : uint64     = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : uint32     = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : uint16     = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : char       = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : unativeint = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : byte     = (# "conv.ovf.u4.un" value  : uint32 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint32) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToInt32")>]
            let inline int32 (value: ^T) = 
                 ExplicitDynamic<(^T), int32> value 
                 when ^T : string     = ParseInt32 (castToString value)
                 when ^T : float     = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : float32   = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : int64     = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : int32     = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : int16     = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : nativeint = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : sbyte     = (# "conv.ovf.i4" value  : int32 #)
                 when ^T : uint64     = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : uint32     = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : uint16     = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : char       = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : unativeint = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : byte     = (# "conv.ovf.i4.un" value  : int32 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> int32) (value))

            [<CompiledName("ToInt")>]
            let inline int value = int32 value

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToUInt64")>]
            let inline uint64 (value: ^T) = 
                 ExplicitDynamic<(^T), uint64> value 
                 when ^T : string     = ParseUInt64 (castToString value)
                 when ^T : float     = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : float32   = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : int64     = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : int32     = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : int16     = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : nativeint = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : sbyte     = (# "conv.ovf.u8" value  : uint64 #)
                 when ^T : uint64     = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : uint32     = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : uint16     = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : char       = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : unativeint = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : byte     = (# "conv.ovf.u8.un" value  : uint64 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> uint64) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToInt64")>]
            let inline int64 (value: ^T) = 
                 ExplicitDynamic<(^T), int64> value 
                 when ^T : string     = ParseInt64 (castToString value)
                 when ^T : float     = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : float32   = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : int64     = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : int32     = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : int16     = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : nativeint = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : sbyte     = (# "conv.ovf.i8" value  : int64 #)
                 when ^T : uint64     = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : uint32     = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : uint16     = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : char       = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : unativeint = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : byte     = (# "conv.ovf.i8.un" value  : int64 #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> int64) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToUIntPtr")>]
            let inline unativeint (value: ^T) = 
                 ExplicitDynamic<(^T), unativeint> value 
                 when ^T : string     = ParseUIntPtr (castToString value)
                 when ^T : float     = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : float32   = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : int64     = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : int32     = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : int16     = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : nativeint = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : sbyte     = (# "conv.ovf.u" value  : unativeint #)
                 when ^T : uint64     = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : uint32     = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : uint16     = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : char       = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : unativeint = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : byte     = (# "conv.ovf.u.un" value  : unativeint #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> unativeint) (value))

            [<NoDynamicInvocation(isLegacy=true)>]
            [<CompiledName("ToIntPtr")>]
            let inline nativeint (value: ^T) = 
                 ExplicitDynamic<(^T), nativeint> value 
                 when ^T : string     = ParseIntPtr (castToString value)
                 when ^T : float     = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : float32   = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : int64     = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : int32     = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : int16     = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : nativeint = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : sbyte     = (# "conv.ovf.i" value  : nativeint #)
                 when ^T : uint64     = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : uint32     = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : uint16     = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : char       = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : unativeint = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : byte     = (# "conv.ovf.i.un" value  : nativeint #)
                 when ^T : ^T = (^T : (static member op_Explicit: ^T -> nativeint) (value))

        module OperatorIntrinsics =

            open System.Collections

            let notStarted() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationNotStarted)))
            let alreadyFinished() = raise (new System.InvalidOperationException(SR.GetString(SR.enumerationAlreadyFinished)))

            // Notes on "inline" with range ienumerable generation.
            // "inline" is used to ensure that primitive ops like add,sub etc. are direct calls.
            // However, it is not used to ensure all explicit lambda arguments can be reduced by the optimiser.

            type Mode = 
                | NotStarted = 0
                | Running = 1
                | Finished = 2

            [<AbstractClass>]
            type BaseRangeEnumerator<'T>() =
                // Generate enumerator from mutable state "z".
                // Marked "inline" to ensure argument functions are reduced (by optimiser).
                let mutable mode = Mode.NotStarted
                let getCurrent(x:BaseRangeEnumerator<'T>) = 
                    match mode with
                    | Mode.NotStarted -> notStarted()
                    | Mode.Running    -> x.Current
                    | _          -> alreadyFinished()
                interface IEnumerator<'T> with
                    member x.Current = getCurrent(x)
                interface System.Collections.IEnumerator with 
                    member x.Current = box (getCurrent(x))
                    member x.MoveNext() = 
                        match mode with
                        | Mode.NotStarted -> if x.CanStart then (mode <- Mode.Running; true) else (mode <- Mode.Finished; false)
                        | Mode.Running    -> if x.CanStep then true                          else (mode <- Mode.Finished; false)
                        | _           -> false
                    member x.Reset() = 
                        mode <- Mode.NotStarted
                        x.DoReset()
                interface System.IDisposable with 
                    member x.Dispose() = ()
                abstract CanStart : bool
                abstract CanStep : bool
                abstract DoReset : unit -> unit
                abstract Current : 'T
                

            type EmptyEnumerator<'T>() = 
                inherit BaseRangeEnumerator<'T>()
                override x.CanStart = false
                override x.CanStep = false
                override x.DoReset() = ()
                override x.Current = Unchecked.defaultof<_>

            type SingletonEnumerator<'T>(v:'T) = 
                inherit BaseRangeEnumerator<'T>()
                override x.CanStart = true
                override x.CanStep = false
                override x.DoReset() = ()
                override x.Current = v

            [<AbstractClass>]
            type ProperIntegralRangeEnumerator<'State,'T>(n:'State, m:'State) = 
                // NOTE: The ordering << is an argument.
                // << will be < or > depending on direction.
                // Assumes n << m and zero << step (i.e. it's a "proper" range).
                //--------
                // NOTE: "inline" so << becomes direct operation (should be IL comparison operation)
                inherit BaseRangeEnumerator<'T>()
                let mutable z : 'State = n
                override obj.DoReset() = z <- n
                override obj.Current = obj.Result z
                override obj.CanStep = 
                    let x  = z
                    let x' = obj.Step z 
                    if    obj.Before x' x                       then false           // x+step has wrapped around
                    elif  obj.Equal x' x                          then false           // x+step has not moved (unexpected, step<>0)
                    elif  obj.Before x x' && obj.Before x' m  then (z <- x'; true) // x+step has moved towards end point
                    elif  obj.Equal x' m                          then (z <- x'; true) // x+step has reached end point
                    else                                          false           // x+step has passed end point
                abstract Before: 'State -> 'State -> bool
                abstract Equal: 'State -> 'State -> bool
                abstract Step: 'State -> 'State
                abstract Result: 'State -> 'T

            let inline enumerator (x : IEnumerator<_>) = x
            
            let inline integralRangeStepEnumerator (zero,add,n,step,m,f) : IEnumerator<_> =
                // Generates sequence z_i where z_i = f (n + i.step) while n + i.step is in region (n,m)
                if n = m then
                    new SingletonEnumerator<_> (f n) |> enumerator 
                else
                    let up = (n < m)
                    let canStart = not (if up then step < zero else step > zero) // check for interval increasing, step decreasing 
                    // generate proper increasing sequence
                    { new ProperIntegralRangeEnumerator<_,_>(n,m) with 
                          member x.CanStart = canStart
                          member x.Before a b = if up then (a < b) else (a > b)
                          member x.Equal a b = (a = b)
                          member x.Step a = add a step
                          member x.Result a = f a } |> enumerator 

            // For RangeGeneric, one and add are functions representing the static resolution of GenericOne and (+)
            // for the particular static type. 
            let inline integralRange<'T> (one:'T, add:'T -> 'T -> 'T, n:'T, m:'T) =
                let gen() = 
                    // Generates sequence z_i where z_i = (n + i.step) while n + i.step is in region (n,m)
                    if n = m then
                        new SingletonEnumerator<_>(n) |> enumerator 
                    else
                        let canStart = (n < m)
                        // generate proper increasing sequence
                        { new ProperIntegralRangeEnumerator<_,_>(n,m) with 
                              member x.CanStart = canStart
                              member x.Before a b = (a < b)
                              member x.Equal a b = (a = b)
                              member x.Step a = add a one
                              member x.Result a = a } |> enumerator 

                { new IEnumerable<'T> with 
                      member x.GetEnumerator() = gen() 
                  interface IEnumerable with 
                      member x.GetEnumerator() = (gen() :> IEnumerator) }

            [<NoEquality; NoComparison>]
            type VariableStepIntegralRangeState<'T> = {
                mutable Started  : bool
                mutable Complete : bool
                mutable Current  : 'T
            }
            let inline variableStepIntegralRange n step m =
                if step = LanguagePrimitives.GenericZero then
                    invalidArg "step" (SR.GetString(SR.stepCannotBeZero));

                let variableStepRangeEnumerator () =
                    let state = {
                        Started  = false
                        Complete = false
                        Current  = Unchecked.defaultof<'T>
                    }

                    let current () = 
                        // according to IEnumerator<int>.Current documentation, the result of of Current
                        // is undefined prior to the first call of MoveNext and post called to MoveNext
                        // that return false (see https://msdn.microsoft.com/en-us/library/58e146b7%28v=vs.110%29.aspx)
                        // so we should be able to just return value here, and we could get rid of the 
                        // complete variable which would be faster
                        if not state.Started then
                            notStarted ()
                        elif state.Complete then
                            alreadyFinished ()
                        else
                            state.Current

                    { new IEnumerator<'T> with
                        member _.Current = current ()

                      interface System.IDisposable with
                        member _.Dispose () = ()

                      interface IEnumerator with 
                        member _.Current = box (current ())

                        member _.Reset () =
                            state.Started <- false
                            state.Complete <- false
                            state.Current <- Unchecked.defaultof<_> 

                        member _.MoveNext () =
                            if not state.Started then
                                state.Started <- true
                                state.Current <- n
                                state.Complete <- 
                                    (  (step > LanguagePrimitives.GenericZero && state.Current > m)
                                    || (step < LanguagePrimitives.GenericZero && state.Current < m))
                            else
                                let next = state.Current + step
                                if   (step > LanguagePrimitives.GenericZero && next > state.Current && next <= m)
                                    || (step < LanguagePrimitives.GenericZero && next < state.Current && next >= m) then
                                    state.Current <- next
                                else
                                    state.Complete <- true

                            not state.Complete}

                { new IEnumerable<'T> with
                    member _.GetEnumerator () = variableStepRangeEnumerator ()

                  interface IEnumerable with
                    member this.GetEnumerator () = (variableStepRangeEnumerator ()) :> IEnumerator }

            let inline simpleIntegralRange minValue maxValue n step m =
                if step <> LanguagePrimitives.GenericOne || n > m || n = minValue || m = maxValue then 
                    variableStepIntegralRange n step m
                else 
                    // a constrained, common simple iterator that is fast.
                    let singleStepRangeEnumerator () =
                        let mutable value = n - LanguagePrimitives.GenericOne

                        let inline current () =
                            // according to IEnumerator<int>.Current documentation, the result of of Current
                            // is undefined prior to the first call of MoveNext and post called to MoveNext
                            // that return false (see https://msdn.microsoft.com/en-us/library/58e146b7%28v=vs.110%29.aspx)
                            // so we should be able to just return value here, which would be faster
                            let derefValue = value
                            if derefValue < n then
                                notStarted ()
                            elif derefValue > m then
                                alreadyFinished ()
                            else 
                                derefValue

                        { new IEnumerator<'T> with
                            member _.Current = current ()

                          interface System.IDisposable with
                            member _.Dispose () = ()

                          interface IEnumerator with
                            member _.Current = box (current ())
                            member _.Reset () = value <- n - LanguagePrimitives.GenericOne
                            member _.MoveNext () =
                                let derefValue = value
                                if derefValue < m then
                                    value <- derefValue + LanguagePrimitives.GenericOne
                                    true
                                elif derefValue = m then 
                                    value <- derefValue + LanguagePrimitives.GenericOne
                                    false
                                else false }

                    { new IEnumerable<'T> with
                        member _.GetEnumerator () = singleStepRangeEnumerator ()

                      interface IEnumerable with
                        member _.GetEnumerator () = (singleStepRangeEnumerator ()) :> IEnumerator }

            // For RangeStepGeneric, zero and add are functions representing the static resolution of GenericZero and (+)
            // for the particular static type. 
            let inline integralRangeStep<'T,'Step> (zero:'Step) (add:'T -> 'Step -> 'T) (n:'T, step:'Step, m:'T) =
                if step = zero then invalidArg "step" (SR.GetString(SR.stepCannotBeZero));
                let gen() = integralRangeStepEnumerator (zero, add, n, step, m, id)
                { new IEnumerable<'T> with 
                      member x.GetEnumerator() = gen() 
                  interface IEnumerable with 
                      member x.GetEnumerator() = (gen() :> IEnumerator) }

            let inline isNaN x = x <> x // NaN is the only value that does not equal itself.
            
            [<AbstractClass>]
            type ProperFloatingRangeStepEnumerator<'T>(n:'T, m:'T) = 
                inherit BaseRangeEnumerator<'T>()
                let mutable z = n
                override obj.DoReset() = z <- n
                override obj.Current = z
                override obj.CanStep = 
                    let x  = z
                    let x' = obj.Step z 
                    // NOTE: The following code is similar to the integer case, but there are differences.
                    // * With floats, there is a NaN case to consider.
                    // * With floats, there should not be any wrapping around arithmetic.
                    // * With floats, x + step == x is possible when step>0.                                 
                    if   obj.Equal x' x                           then false              // no movement, loss of precision
                    elif obj.Before x x' && obj.Before x' m       then (z <- x'; true)    // advanced towards end point
                    elif obj.Equal x' m                           then (z <- x'; true)    // hit end point
                    elif obj.Before m x'                          then false              // passed beyond end point
                                                                                          // [includes x' infinite, m finite case]
                    elif not (obj.Equal x' x')                    then false              // x' has become NaN, which is possible...
                                                                                          // e.g. -infinity + infinity = NaN
                    //elif lt x' x               then failwith           // x + step should not move against <<
                    //                                  "Broken invariant in F# floating point range"
                    else                                               false              

                // NOTE: The ordering Before is an argument. It will be < or > depending on direction.
                // We assume assume "Before n m" 
                abstract Before: 'T -> 'T -> bool
                abstract Equal: 'T -> 'T -> bool
                abstract Step: 'T -> 'T 

            let inline floatingRangeStepEnumerator n step m =
                if step = GenericZero then invalidArg "step" (SR.GetString(SR.stepCannotBeZero));
                if isNaN n            then invalidArg "n"    (SR.GetString(SR.startCannotBeNaN));
                if isNaN step         then invalidArg "step" (SR.GetString(SR.stepCannotBeNaN));
                if isNaN m            then invalidArg "m"    (SR.GetString(SR.endCannotBeNaN));
                if n = m then
                    new SingletonEnumerator<_>(n) |> enumerator
                else 
                    let up = (n < m)
                    let canStart = not (if up then step < GenericZero else step > GenericZero) // interval increasing, step decreasing 
                    // generate proper increasing sequence
                    { new ProperFloatingRangeStepEnumerator<_>(n, m) with 
                          member x.CanStart = canStart
                          member x.Before a b = if up then (a < b) else (a > b)
                          member x.Equal a b = (a = b)
                          member x.Step a = a + step } |> enumerator 

            // When is a System.Double an System.Int32?
            let minIntR = -2147483648.0
            let maxIntR =  2147483647.0
            let isPreciseInt x = minIntR <= x && x <= maxIntR && System.Math.Floor x = x 

            // When a floating range looks like an exact number of steps, generate using {n+i.step} for i from an integer range.
            let inline semiPreciseFloatingRangeEnumerator ofInt n dx m =                                                 
                let numSteps = float ((m - n) / dx)
                if isPreciseInt numSteps then
                    integralRangeStepEnumerator(0, (+), 0, 1, int numSteps, (fun i -> n + (ofInt i * dx)))
                else
                    floatingRangeStepEnumerator n dx m

            let inline floatingRange ofInt (n,step,m) =
                let gen() = semiPreciseFloatingRangeEnumerator ofInt n step m 
                { new IEnumerable<'T> with 
                      member x.GetEnumerator() = gen() 
                  interface System.Collections.IEnumerable with 
                      member x.GetEnumerator() = (gen() :> System.Collections.IEnumerator) }

            let RangeInt32   start step stop : seq<int>        = simpleIntegralRange Int32.MinValue Int32.MaxValue start step stop
            let RangeInt64   start step stop : seq<int64>      = simpleIntegralRange Int64.MinValue Int64.MaxValue start step stop
            let RangeUInt64  start step stop : seq<uint64>     = simpleIntegralRange UInt64.MinValue UInt64.MaxValue start step stop
            let RangeUInt32  start step stop : seq<uint32>     = simpleIntegralRange UInt32.MinValue UInt32.MaxValue start step stop
            let RangeIntPtr  start step stop : seq<nativeint>  = variableStepIntegralRange start step stop
            let RangeUIntPtr start step stop : seq<unativeint> = variableStepIntegralRange start step stop
            let RangeInt16   start step stop : seq<int16>      = simpleIntegralRange Int16.MinValue Int16.MaxValue start step stop
            let RangeUInt16  start step stop : seq<uint16>     = simpleIntegralRange UInt16.MinValue UInt16.MaxValue start step stop
            let RangeSByte   start step stop : seq<sbyte>      = simpleIntegralRange SByte.MinValue SByte.MaxValue start step stop
            let RangeByte    start step stop : seq<byte>       = simpleIntegralRange Byte.MinValue Byte.MaxValue start step stop
            let RangeDouble  start step stop : seq<float>      = floatingRange float   (start,step,stop)
            let RangeSingle  start step stop : seq<float32>    = floatingRange float32 (start,step,stop)
            let RangeGeneric   one add start stop : seq<'T> = integralRange (one,add,start,stop)
            let RangeStepGeneric   zero add start step stop : seq<'T> = integralRangeStep zero add  (start,step,stop)
            let RangeChar (start:char) (stop:char) = 
                integralRange ((retype 1us : char),(fun (x:char) (y:char) -> retype ((retype x : uint16) + (retype y : uint16)) ),start,stop)


            let inline toFloat (x:float32) = (# "conv.r8" x : float #)
            let inline toFloat32 (x:float) = (# "conv.r4" x : float32 #)

            let inline ComputePowerGenericInlined one mul x n =
                let rec loop n = 
                    match n with 
                    | 0 -> one
                    | 1 -> x
                    | 2 -> mul x x 
                    | 3 -> mul (mul x x) x
                    | 4 -> let v = mul x x in mul v v
                    | _ -> 
                        let v = loop (n/2) in 
                        let v = mul v v in
                        if n%2 = 0 then v else mul v x in
                loop n


            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowByte (x:byte) n = ComputePowerGenericInlined  1uy Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowSByte (x:sbyte) n = ComputePowerGenericInlined  1y Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowInt16 (x:int16) n = ComputePowerGenericInlined  1s Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowUInt16 (x:uint16) n = ComputePowerGenericInlined  1us Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowInt32 (x:int32) n = ComputePowerGenericInlined  1 Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowUInt32 (x:uint32) n = ComputePowerGenericInlined  1u Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowInt64 (x:int64) n = ComputePowerGenericInlined  1L Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowUInt64 (x:uint64) n = ComputePowerGenericInlined  1UL Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowIntPtr (x:nativeint) n = ComputePowerGenericInlined  1n Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowUIntPtr (x:unativeint) n = ComputePowerGenericInlined  1un Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowSingle (x:float32) n = ComputePowerGenericInlined  1.0f Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowDouble (x:float) n = ComputePowerGenericInlined  1.0 Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowDecimal (x:decimal) n = ComputePowerGenericInlined  1.0M Checked.( * ) x n 

            [<CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly");  CodeAnalysis.SuppressMessage("Microsoft.Naming","CA1704:IdentifiersShouldBeSpelledCorrectly")>]
            let PowGeneric (one, mul, value: 'T, exponent) = ComputePowerGenericInlined  one mul value exponent 

            let inline ComputeSlice bound start finish length =
                let low = 
                    match start with
                    | Some n when n >= bound -> n
                    | _ -> bound
                let high = 
                    match finish with 
                    | Some m when m < bound + length -> m
                    | _ -> bound + length - 1

                low, high

            let inline GetArraySlice (source: _[]) start finish =
                let start, finish = ComputeSlice 0 start finish source.Length
                GetArraySub source start (finish - start + 1)

            let inline SetArraySlice (target: _[]) start finish (source: _[]) = 
                let start  = (match start with None -> 0 | Some n -> n) 
                let finish = (match finish with None -> target.Length - 1 | Some n -> n) 
                SetArraySub target start (finish - start + 1) source

            let inline GetArraySlice2D (source: _[,]) start1 finish1 start2 finish2 =
                let bound1 = source.GetLowerBound(0)
                let bound2 = source.GetLowerBound(1)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray2DLength1 source)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray2DLength2 source)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                GetArray2DSub source start1 start2 len1 len2

            let inline GetArraySlice2DFixed (source: _[,]) start finish index nonFixedDim = 
                let bound = source.GetLowerBound(nonFixedDim)
                let start, finish = ComputeSlice bound start finish (GetArray2DLength source nonFixedDim)
                let len = (finish - start + 1)
                let dst = zeroCreate (if len < 0 then 0 else len)
                let getArrayElem = 
                    match nonFixedDim with
                    | 1 -> (fun i -> GetArray2D source index (start+i))
                    | 0 -> (fun i -> GetArray2D source (start+i) index)
                    | _ -> raise (System.IndexOutOfRangeException())
                for j = 0 to len - 1 do 
                    SetArray dst j (getArrayElem j)
                dst

            let inline GetArraySlice2DFixed1 (source: _[,]) index1 start2 finish2 = GetArraySlice2DFixed source start2 finish2 index1 1

            let inline GetArraySlice2DFixed2 (source: _[,]) start1 finish1 index2 = GetArraySlice2DFixed source start1 finish1 index2 0

            let inline SetArraySlice2DFixed (target: _[,]) (source: _[]) index start finish nonFixedDim  = 
                let bound = target.GetLowerBound(nonFixedDim)
                let start, finish = ComputeSlice bound start finish (GetArray2DLength target nonFixedDim)
                let len = (finish - start + 1)
                let setArrayElem = 
                    match nonFixedDim with
                    | 1 -> (fun j -> SetArray2D target index (bound + start + j) (GetArray source j)) 
                    | 0 -> (fun i -> SetArray2D target (bound + start + i) index (GetArray source i))
                    | _ -> raise (System.IndexOutOfRangeException())
                for j = 0 to len - 1 do
                    setArrayElem j

            let inline SetArraySlice2DFixed1 (target: _[,]) index1 start2 finish2 (source: _[]) = SetArraySlice2DFixed target source index1 start2 finish2 1 

            let inline SetArraySlice2DFixed2 (target: _[,]) start1 finish1 index2 (source:_[]) = SetArraySlice2DFixed target source index2 start1 finish1 0

            let inline SetArraySlice2D (target: _[,]) start1 finish1 start2 finish2 (source: _[,]) = 
                let bound1 = target.GetLowerBound(0)
                let bound2 = target.GetLowerBound(1)
                let start1  = (match start1 with None -> bound1 | Some n -> n) 
                let start2  = (match start2 with None -> bound2 | Some n -> n) 
                let finish1 = (match finish1 with None -> bound1 + GetArray2DLength1 target - 1 | Some n -> n) 
                let finish2 = (match finish2 with None -> bound2 + GetArray2DLength2 target - 1 | Some n -> n) 
                SetArray2DSub target start1 start2 (finish1 - start1 + 1) (finish2 - start2 + 1) source

            let inline GetArraySlice3D (source: _[,,]) start1 finish1 start2 finish2 start3 finish3 =
                let bound1 = source.GetLowerBound(0)
                let bound2 = source.GetLowerBound(1)
                let bound3 = source.GetLowerBound(2)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray3DLength1 source)              
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray3DLength2 source)              
                let start3, finish3 = ComputeSlice bound3 start3 finish3 (GetArray3DLength3 source)              
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                let len3 = (finish3 - start3 + 1)
                GetArray3DSub source start1 start2 start3 len1 len2 len3

            let inline GetArraySlice3DFixedSingle (source: _[,,]) start1 finish1 start2 finish2 index nonFixedDim1 nonFixedDim2 =
                let bound1 = source.GetLowerBound(nonFixedDim1)
                let bound2 = source.GetLowerBound(nonFixedDim2)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray3DLength source nonFixedDim1)              
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray3DLength source nonFixedDim2)              
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)

                let dst = Array2DZeroCreate (max 0 len1) (max 0 len2)
                let getArrayElem = 
                    match nonFixedDim1, nonFixedDim2 with
                    | 1, 2 -> (fun i j -> GetArray3D source index (start1 + i) (start2 + j))
                    | 0, 2 -> (fun i j -> GetArray3D source (start1 + i) index (start2 + j))
                    | 0, 1 -> (fun i j -> GetArray3D source (start1 + i) (start2 + j) index)
                    | _ -> raise (System.IndexOutOfRangeException())

                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        SetArray2D dst i j (getArrayElem i j)

                dst

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedSingle1 (source: _[,,]) index1 start2 finish2 start3 finish3 = GetArraySlice3DFixedSingle source start2 finish2 start3 finish3 index1 1 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedSingle2 (source: _[,,]) start1 finish1 index2 start3 finish3 = GetArraySlice3DFixedSingle source start1 finish1 start3 finish3 index2 0 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedSingle3 (source: _[,,]) start1 finish1 start2 finish2 index3 = GetArraySlice3DFixedSingle source start1 finish1 start2 finish2 index3 0 1

            let inline GetArraySlice3DFixedDouble (source: _[,,]) start finish index1 index2 nonFixedDim = 
                let bound = source.GetLowerBound(nonFixedDim)
                let start, finish = ComputeSlice bound start finish (GetArray3DLength source nonFixedDim)
                let len = (finish - start + 1)
                let dst = zeroCreate (if len < 0 then 0 else len)
                let getArrayElem = 
                    match nonFixedDim with
                    | 2 -> (fun i -> GetArray3D source index1 index2 i)
                    | 1 -> (fun i -> GetArray3D source index1 (start + i) index2)
                    | 0 -> (fun i -> GetArray3D source (start+i) index1 index2)
                    | _ -> raise (System.IndexOutOfRangeException())
                for j = 0 to len - 1 do 
                    SetArray dst j (getArrayElem j)
                dst

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedDouble1 (source: _[,,]) index1 index2 start3 finish3 = 
                GetArraySlice3DFixedDouble source start3 finish3 index1 index2 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedDouble2 (source: _[,,]) index1 start2 finish2 index3 = 
                GetArraySlice3DFixedDouble source start2 finish2 index1 index3 1

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice3DFixedDouble3 (source: _[,,]) start1 finish1 index2 index3 = 
                GetArraySlice3DFixedDouble source start1 finish1 index2 index3 0

            let inline SetArraySlice3D (target: _[,,]) start1 finish1 start2 finish2 start3 finish3 (source:_[,,]) = 
                let bound1 = target.GetLowerBound(0)
                let bound2 = target.GetLowerBound(1)
                let bound3 = target.GetLowerBound(2)
                let start1  = (match start1 with None -> bound1 | Some n -> n) 
                let start2  = (match start2 with None -> bound2 | Some n -> n) 
                let start3  = (match start3 with None -> bound3 | Some n -> n) 
                let finish1 = (match finish1 with None -> bound1 + GetArray3DLength1 target - 1 | Some n -> n) 
                let finish2 = (match finish2 with None -> bound2 + GetArray3DLength2 target - 1 | Some n -> n) 
                let finish3 = (match finish3 with None -> bound3 + GetArray3DLength3 target - 1 | Some n -> n) 
                SetArray3DSub target start1 start2 start3 (finish1 - start1 + 1) (finish2 - start2 + 1) (finish3 - start3 + 1) source

            let inline SetArraySlice3DFixedSingle (target: _[,,]) (source: _[,]) index start1 finish1 start2 finish2 nonFixedDim1 nonFixedDim2  = 
                let bound1 = target.GetLowerBound(nonFixedDim1)
                let bound2 = target.GetLowerBound(nonFixedDim2)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray3DLength target nonFixedDim1)              
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray3DLength target nonFixedDim2)              
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                let setArrayElem = 
                    match nonFixedDim1, nonFixedDim2 with
                    | 1, 2 -> (fun i j -> SetArray3D target index (bound1 + start1 + i) (bound2 + start2 + j) (GetArray2D source i j)) 
                    | 0, 2 -> (fun i j -> SetArray3D target (bound1 + start1 + i) index (bound2+ start2 + j) (GetArray2D source i j))
                    | 0, 1 -> (fun i j -> SetArray3D target (bound1 + start1 + i) (bound2+ start2 + j) index (GetArray2D source i j))
                    | _ -> raise (System.IndexOutOfRangeException())

                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        setArrayElem i j

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedSingle1 (target: _[,,]) index start2 finish2 start3 finish3 (source: _[,]) = 
                SetArraySlice3DFixedSingle target source index start2 finish2 start3 finish3 1 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedSingle2 (target: _[,,]) start1 finish1 index start3 finish3 (source: _[,]) = 
                SetArraySlice3DFixedSingle target source index start1 finish1 start3 finish3 0 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedSingle3 (target: _[,,]) start1 finish1 start2 finish2 index (source: _[,]) = 
                SetArraySlice3DFixedSingle target source index start1 finish1 start2 finish2 0 1

            let inline SetArraySlice3DFixedDouble (target: _[,,]) (source: _[]) index1 index2 start finish nonFixedDim = 
                let bound = target.GetLowerBound(nonFixedDim)
                let start, finish = ComputeSlice bound start finish (GetArray3DLength target nonFixedDim)
                let len = (finish - start + 1)
                let setArrayElem = 
                    match nonFixedDim with
                    | 2 -> (fun k -> SetArray3D target index1 index2 (bound + start + k) (GetArray source k)) 
                    | 1 -> (fun j -> SetArray3D target index1 (bound + start + j) index2 (GetArray source j)) 
                    | 0 -> (fun i -> SetArray3D target (bound + start + i) index1 index2 (GetArray source i))
                    | _ -> raise (System.IndexOutOfRangeException())
                for j = 0 to len - 1 do 
                    setArrayElem j

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedDouble1 (target: _[,,]) index1 index2 start3 finish3 (source: _[]) = 
                SetArraySlice3DFixedDouble target source index1 index2 start3 finish3 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedDouble2 (target: _[,,]) index1 start2 finish2 index3 (source: _[]) = 
                SetArraySlice3DFixedDouble target source index1 index3 start2 finish2 1

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice3DFixedDouble3 (target: _[,,]) start1 finish1 index2 index3 (source: _[]) = 
                SetArraySlice3DFixedDouble target source index2 index3 start1 finish1 0

            let inline GetArraySlice4D (source: _[,,,]) start1 finish1 start2 finish2 start3 finish3 start4 finish4 =
                let bound1 = source.GetLowerBound(0)
                let bound2 = source.GetLowerBound(1)
                let bound3 = source.GetLowerBound(2)
                let bound4 = source.GetLowerBound(3)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength1 source)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray4DLength2 source)
                let start3, finish3 = ComputeSlice bound3 start3 finish3 (GetArray4DLength3 source)
                let start4, finish4 = ComputeSlice bound4 start4 finish4 (GetArray4DLength4 source)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                let len3 = (finish3 - start3 + 1)
                let len4 = (finish4 - start4 + 1)
                GetArray4DSub source start1 start2 start3 start4 len1 len2 len3 len4

            let inline GetArraySlice4DFixedSingle (source: _[,,,]) start1 finish1 start2 finish2 start3 finish3 index nonFixedDim1 nonFixedDim2 nonFixedDim3 = 
                let bound1 = source.GetLowerBound(nonFixedDim1)
                let bound2 = source.GetLowerBound(nonFixedDim2)
                let bound3 = source.GetLowerBound(nonFixedDim3)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength source nonFixedDim1)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray4DLength source nonFixedDim2)
                let start3, finish3 = ComputeSlice bound3 start3 finish3 (GetArray4DLength source nonFixedDim3)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                let len3 = (finish3 - start3 + 1)

                let dst = Array3DZeroCreate (max len1 0) (max len2 0) (max len3 0)
                let getArrayElem = 
                    match nonFixedDim1, nonFixedDim2, nonFixedDim3 with
                    | 1, 2, 3 -> (fun i j k-> GetArray4D source index i j k)
                    | 0, 2, 3 -> (fun i j k -> GetArray4D source i index j k)
                    | 0, 1, 3 -> (fun i j k -> GetArray4D source i j index k)
                    | 0, 1, 2 -> (fun i j k -> GetArray4D source i j k index)
                    | _ -> raise (System.IndexOutOfRangeException())
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                            SetArray3D dst i j k (getArrayElem i j k)

                dst

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedSingle1 (source: _[,,,]) index1 start2 finish2 start3 finish3 start4 finish4 =
                GetArraySlice4DFixedSingle source start2 finish2 start3 finish3 start4 finish4 index1 1 2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedSingle2 (source: _[,,,]) start1 finish1 index2 start3 finish3 start4 finish4 = 
                GetArraySlice4DFixedSingle source start1 finish1 start3 finish3 start4 finish4 index2 0 2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedSingle3 (source: _[,,,]) start1 finish1 start2 finish2 index3 start4 finish4 = 
                GetArraySlice4DFixedSingle source start1 finish1 start2 finish2 start4 finish4 index3 0 1 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedSingle4 (source: _[,,,]) start1 finish1 start2 finish2 start3 finish3 index4 = 
                GetArraySlice4DFixedSingle source start1 finish1 start2 finish2 start3 finish3 index4 0 1 2

            let inline GetArraySlice4DFixedDouble (source: _[,,,]) start1 finish1 start2 finish2 index1 index2 nonFixedDim1 nonFixedDim2 = 
                let bound1 = source.GetLowerBound(nonFixedDim1)
                let bound2 = source.GetLowerBound(nonFixedDim2)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength source nonFixedDim1)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray4DLength source nonFixedDim2)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)

                let dst = Array2DZeroCreate (max len1 0) (max len2 0)
                let getArrayElem = 
                    match nonFixedDim1, nonFixedDim2 with
                    | 2, 3 -> (fun i j -> GetArray4D source index1 index2 i j)
                    | 1, 3 -> (fun i j -> GetArray4D source index1 i index2 j)
                    | 1, 2 -> (fun i j -> GetArray4D source index1 i j index2)
                    | 0, 3 -> (fun i j -> GetArray4D source i index1 index2 j)
                    | 0, 2 -> (fun i j -> GetArray4D source i index1 j index2)
                    | 0, 1 -> (fun i j -> GetArray4D source i j index1 index2)
                    | _ -> raise (System.IndexOutOfRangeException())
                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                            SetArray2D dst i j (getArrayElem i j)

                dst

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble1 (source: _[,,,]) index1 index2 start3 finish3 start4 finish4 =
                GetArraySlice4DFixedDouble source start3 finish3 start4 finish4 index1 index2 2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble2 (source: _[,,,]) index1 start2 finish2 index3 start4 finish4 =
                GetArraySlice4DFixedDouble source start2 finish2 start4 finish4 index1 index3 1 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble3 (source: _[,,,]) index1 start2 finish2 start3 finish3 index4 =
                GetArraySlice4DFixedDouble source start2 finish2 start3 finish3 index1 index4 1 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble4 (source: _[,,,]) start1 finish1 index2 index3 start4 finish4 =
                GetArraySlice4DFixedDouble source start1 finish1 start4 finish4 index2 index3 0 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble5 (source: _[,,,]) start1 finish1 index2 start3 finish3 index4 = 
                GetArraySlice4DFixedDouble source start1 finish1 start3 finish3 index2 index4 0 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedDouble6 (source: _[,,,]) start1 finish1 start2 finish2 index3 index4 = 
                GetArraySlice4DFixedDouble source start1 finish1 start2 finish2 index3 index4 0 1

            let inline GetArraySlice4DFixedTriple (source: _[,,,]) start1 finish1 index1 index2 index3 nonFixedDim1 = 
                let bound1 = source.GetLowerBound(nonFixedDim1)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength source nonFixedDim1)
                let len1 = (finish1 - start1 + 1)
                let dst = zeroCreate (max len1 0)
                let getArrayElem = 
                    match nonFixedDim1 with
                    | 0 -> (fun i -> GetArray4D source i index1 index2 index3)
                    | 1 -> (fun i -> GetArray4D source index1 i index2 index3)
                    | 2 -> (fun i -> GetArray4D source index1 index2 i index3)
                    | 3 -> (fun i -> GetArray4D source index1 index2 index3 i)
                    | _ -> raise (System.IndexOutOfRangeException())
                for i = 0 to len1 - 1 do
                    SetArray dst i (getArrayElem i) 

                dst

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedTriple1 (source: _[,,,]) start1 finish1 index2 index3 index4 = 
                GetArraySlice4DFixedTriple source start1 finish1 index2 index3 index4 0

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedTriple2 (source: _[,,,]) index1 start2 finish2 index3 index4 =
                GetArraySlice4DFixedTriple source start2 finish2 index1 index3 index4 1

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedTriple3 (source: _[,,,]) index1 index2 start3 finish3 index4 =
                GetArraySlice4DFixedTriple source start3 finish3 index1 index2 index4 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline GetArraySlice4DFixedTriple4 (source: _[,,,]) index1 index2 index3 start4 finish4 = 
                GetArraySlice4DFixedTriple source start4 finish4 index1 index2 index3 3

            let inline SetArraySlice4D (target: _[,,,]) start1 finish1 start2 finish2 start3 finish3 start4 finish4 (source:_[,,,]) = 
                let bound1 = target.GetLowerBound(0)
                let bound2 = target.GetLowerBound(1)
                let bound3 = target.GetLowerBound(2)
                let bound4 = target.GetLowerBound(3)
                let start1  = (match start1 with None -> bound1 | Some n -> n) 
                let start2  = (match start2 with None -> bound2 | Some n -> n) 
                let start3  = (match start3 with None -> bound3 | Some n -> n) 
                let start4  = (match start4 with None -> bound4 | Some n -> n) 
                let finish1 = (match finish1 with None -> bound1 + GetArray4DLength1 target - 1 | Some n -> n) 
                let finish2 = (match finish2 with None -> bound2 + GetArray4DLength2 target - 1 | Some n -> n) 
                let finish3 = (match finish3 with None -> bound3 + GetArray4DLength3 target - 1 | Some n -> n) 
                let finish4 = (match finish4 with None -> bound4 + GetArray4DLength4 target - 1 | Some n -> n) 
                SetArray4DSub target start1 start2 start3 start4 (finish1 - start1 + 1) (finish2 - start2 + 1) (finish3 - start3 + 1) (finish4 - start4 + 1) source

            let inline SetArraySlice4DFixedSingle (target: _[,,,]) (source: _[,,]) index start1 finish1 start2 finish2 start3 finish3 nonFixedDim1 nonFixedDim2 nonFixedDim3 = 
                let bound1 = target.GetLowerBound(nonFixedDim1)
                let bound2 = target.GetLowerBound(nonFixedDim2)
                let bound3 = target.GetLowerBound(nonFixedDim3)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength target nonFixedDim1)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray4DLength target nonFixedDim2)
                let start3, finish3 = ComputeSlice bound3 start3 finish3 (GetArray4DLength target nonFixedDim3)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)
                let len3 = (finish3 - start3 + 1)

                let setArrayElem = 
                    match nonFixedDim1, nonFixedDim2, nonFixedDim3 with
                    | 1, 2, 3 -> (fun i j k -> SetArray4D target index (bound1 + start1 + i) (bound2 + start2 + j) (bound3 + start3 + k) (GetArray3D source i j k))
                    | 0, 2, 3 -> (fun i j k -> SetArray4D target (bound1 + start1 + i) index (bound2 + start2 + j) (bound3 + start3 + k) (GetArray3D source i j k))
                    | 0, 1, 3 -> (fun i j k -> SetArray4D target (bound1 + start1 + i) (bound2 + start2 + j) index (bound3 + start3 + k) (GetArray3D source i j k))
                    | 0, 1, 2 -> (fun i j k -> SetArray4D target (bound1 + start1 + i) (bound2 + start2 + j) (bound3 + start3 + k) index (GetArray3D source i j k))
                    | _ -> raise (System.IndexOutOfRangeException())

                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        for k = 0 to len3 - 1 do
                            setArrayElem i j k

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedSingle1 (target: _[,,,]) index1 start2 finish2 start3 finish3 start4 finish4 (source: _[,,]) = 
                SetArraySlice4DFixedSingle target source index1 start2 finish2 start3 finish3 start4 finish4 1 2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedSingle2 (target: _[,,,]) start1 finish1 index2 start3 finish3 start4 finish4 (source: _[,,]) = 
                SetArraySlice4DFixedSingle target source index2 start1 finish1 start3 finish3 start4 finish4 0 2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedSingle3 (target: _[,,,]) start1 finish1 start2 finish2 index3 start4 finish4 (source: _[,,]) = 
                SetArraySlice4DFixedSingle target source index3 start1 finish1 start2 finish2 start4 finish4 0 1 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedSingle4 (target: _[,,,]) start1 finish1 start2 finish2 start3 finish3 index4 (source: _[,,]) = 
                SetArraySlice4DFixedSingle target source index4 start1 finish1 start2 finish2 start3 finish3 0 1 2

            let inline SetArraySlice4DFixedDouble (target: _[,,,]) (source: _[,]) index1 index2 start1 finish1 start2 finish2 nonFixedDim1 nonFixedDim2 = 
                let bound1 = target.GetLowerBound(nonFixedDim1)
                let bound2 = target.GetLowerBound(nonFixedDim2)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength target nonFixedDim1)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray4DLength target nonFixedDim2)
                let len1 = (finish1 - start1 + 1)
                let len2 = (finish2 - start2 + 1)

                let setArrayElem = 
                    match nonFixedDim1, nonFixedDim2 with
                    | 2, 3 -> (fun i j -> SetArray4D target index1 index2 (bound1 + start1 + i) (bound2 + start2 + j) (GetArray2D source i j))
                    | 1, 3 -> (fun i j -> SetArray4D target index1 (bound1 + start1 + i) index2 (bound2 + start2 + j) (GetArray2D source i j))
                    | 1, 2 -> (fun i j -> SetArray4D target index1 (bound1 + start1 + i) (bound2 + start2 + j) index2 (GetArray2D source i j))
                    | 0, 3 -> (fun i j -> SetArray4D target (bound1 + start1 + i) index1 index2 (bound2 + start2 + j) (GetArray2D source i j))
                    | 0, 2 -> (fun i j -> SetArray4D target (bound1 + start1 + i) index1 (bound2 + start2 + j) index2 (GetArray2D source i j))
                    | 0, 1 -> (fun i j -> SetArray4D target (bound1 + start1 + i) (bound2 + start2 + j) index1 index2 (GetArray2D source i j))
                    | _ -> raise (System.IndexOutOfRangeException())

                for i = 0 to len1 - 1 do
                    for j = 0 to len2 - 1 do
                        setArrayElem i j 

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble1 (target: _[,,,]) index1 index2 start3 finish3 start4 finish4 (source: _[,]) =
                SetArraySlice4DFixedDouble target source index1 index2 start3 finish3 start4 finish4  2 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble2 (target: _[,,,]) index1 start2 finish2 index3 start4 finish4 (source: _[,]) =
                SetArraySlice4DFixedDouble target source index1 index3 start2 finish2 start4 finish4 1 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble3 (target: _[,,,]) index1 start2 finish2 start3 finish3 index4 (source: _[,]) = 
                SetArraySlice4DFixedDouble target source index1 index4 start2 finish2 start3 finish3 1 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble4 (target: _[,,,]) start1 finish1 index2 index3 start4 finish4 (source: _[,]) =
                SetArraySlice4DFixedDouble target source index2 index3 start1 finish1 start4 finish4 0 3

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble5 (target: _[,,,]) start1 finish1 index2 start3 finish3 index4 (source: _[,]) =
                SetArraySlice4DFixedDouble target source index2 index4 start1 finish1 start3 finish3 0 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedDouble6 (target: _[,,,]) start1 finish1 start2 finish2 index3 index4 (source: _[,]) =
                SetArraySlice4DFixedDouble target source index3 index4 start1 finish1 start2 finish2 0 1

            let inline SetArraySlice4DFixedTriple (target: _[,,,]) (source: _[]) index1 index2 index3 start1 finish1 nonFixedDim1 = 
                let bound1 = target.GetLowerBound(nonFixedDim1)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray4DLength target nonFixedDim1)
                let len1 = (finish1 - start1 + 1)
                let setArrayElem = 
                    match nonFixedDim1 with
                    | 0 -> (fun i -> SetArray4D target (bound1 + start1 + i) index1 index2 index3 (GetArray source i))
                    | 1 -> (fun i -> SetArray4D target index1 (bound1 + start1 + i) index2 index3 (GetArray source i))
                    | 2 -> (fun i -> SetArray4D target index1 index2 (bound1 + start1 + i) index3 (GetArray source i))
                    | 3 -> (fun i -> SetArray4D target index1 index2 index3 (bound1 + start1 + i) (GetArray source i))
                    | _ -> raise (System.IndexOutOfRangeException())
                for i = 0 to len1 - 1 do
                    setArrayElem i

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedTriple1 (target: _[,,,]) start1 finish1 index2 index3 index4 (source: _[]) = 
                SetArraySlice4DFixedTriple target source index2 index3 index4 start1 finish1 0

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedTriple2 (target: _[,,,]) index1 start2 finish2 index3 index4 (source: _[]) = 
                SetArraySlice4DFixedTriple target source index1 index3 index4 start2 finish2 1

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedTriple3 (target: _[,,,]) index1 index2 start3 finish3 index4 (source: _[]) =
                SetArraySlice4DFixedTriple target source index1 index2 index4 start3 finish3 2

            [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
            let inline SetArraySlice4DFixedTriple4 (target: _[,,,]) index1 index2 index3 start4 finish4 (source: _[]) =
                SetArraySlice4DFixedTriple target source index1 index2 index3 start4 finish4 3

            let inline GetStringSlice (source: string) start finish =
                let start, finish = ComputeSlice 0 start finish source.Length
                let len = finish-start+1
                if len <= 0 then String.Empty
                else source.Substring(start, len)

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline absImpl (x: ^T) : ^T = 
                 (^T: (static member Abs : ^T -> ^T) (x))
                 when ^T : int32       = let x : int32     = retype x in System.Math.Abs(x)
                 when ^T : float       = let x : float     = retype x in System.Math.Abs(x)
                 when ^T : float32     = let x : float32   = retype x in System.Math.Abs(x)
                 when ^T : int64       = let x : int64     = retype x in System.Math.Abs(x)
                 when ^T : nativeint   = 
                    let x : nativeint = retype x in 
                    if x >= 0n then x else 
                    let res = -x in 
                    if res < 0n then raise (System.OverflowException(ErrorStrings.NoNegateMinValueString))
                    res
                 when ^T : int16       = let x : int16     = retype x in System.Math.Abs(x)
                 when ^T : sbyte       = let x : sbyte     = retype x in System.Math.Abs(x)
                 when ^T : decimal     = System.Math.Abs(retype x : decimal) 

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  acosImpl(x: ^T) : ^T = 
                 (^T: (static member Acos : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Acos(retype x)
                 when ^T : float32     = System.Math.Acos(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  asinImpl(x: ^T) : ^T = 
                 (^T: (static member Asin : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Asin(retype x)
                 when ^T : float32     = System.Math.Asin(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  atanImpl(x: ^T) : ^T = 
                 (^T: (static member Atan : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Atan(retype x)
                 when ^T : float32     = System.Math.Atan(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  atan2Impl(x: ^T) (y: ^T) : 'U = 
                 (^T: (static member Atan2 : ^T * ^T -> 'U) (x,y))
                 when ^T : float       = System.Math.Atan2(retype x, retype y)
                 when ^T : float32     = System.Math.Atan2(toFloat (retype x), toFloat(retype y)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  ceilImpl(x: ^T) : ^T = 
                 (^T: (static member Ceiling : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Ceiling(retype x : float)
                 when ^T : float32     = System.Math.Ceiling(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  expImpl(x: ^T) : ^T = 
                 (^T: (static member Exp : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Exp(retype x)
                 when ^T : float32     = System.Math.Exp(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline floorImpl (x: ^T) : ^T = 
                 (^T: (static member Floor : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Floor(retype x : float)
                 when ^T : float32     = System.Math.Floor(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline truncateImpl (x: ^T) : ^T = 
                 (^T: (static member Truncate : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Truncate(retype x : float) 
                 when ^T : float32     = System.Math.Truncate(toFloat (retype x))  |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline roundImpl (x: ^T) : ^T = 
                 (^T: (static member Round : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Round(retype x : float)
                 when ^T : float32     = System.Math.Round(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline signImpl (x: ^T) : int = 
                 (^T: (member Sign : int) (x))
                 when ^T : int32       = System.Math.Sign(retype x : int32)
                 when ^T : int64       = System.Math.Sign(retype x : int64)
                 when ^T : nativeint   = if (retype x : nativeint) < 0n then -1 else if (retype x : nativeint) > 0n then 1 else 0
                 when ^T : int16       = System.Math.Sign(retype x : int16)
                 when ^T : sbyte       = System.Math.Sign(retype x : sbyte)
                 when ^T : float       = System.Math.Sign(retype x : float)
                 when ^T : float32     = System.Math.Sign(toFloat (retype x)) 
                 when ^T : decimal     = System.Math.Sign(retype x : decimal) 

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  logImpl(x: ^T) : ^T = 
                 (^T: (static member Log : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Log(retype x)
                 when ^T : float32     = System.Math.Log(toFloat (retype x)) |> toFloat32
            
            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  log10Impl(x: ^T) : ^T = 
                 (^T: (static member Log10 : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Log10(retype x)
                 when ^T : float32     = System.Math.Log10(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  sqrtImpl(x: ^T) : ^U = 
                 (^T: (static member Sqrt : ^T -> ^U) (x))
                 when ^T : float       = System.Math.Sqrt(retype x : float)
                 when ^T : float32     = System.Math.Sqrt(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  cosImpl(x: ^T) : ^T = 
                 (^T: (static member Cos : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Cos(retype x)
                 when ^T : float32     = System.Math.Cos(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  coshImpl(x: ^T) : ^T = 
                 (^T: (static member Cosh : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Cosh(retype x)
                 when ^T : float32     = System.Math.Cosh(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  sinImpl(x: ^T) : ^T = 
                 (^T: (static member Sin : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Sin(retype x)
                 when ^T : float32     = System.Math.Sin(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  sinhImpl(x: ^T) : ^T = 
                 (^T: (static member Sinh : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Sinh(retype x)
                 when ^T : float32     = System.Math.Sinh(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  tanImpl(x: ^T) : ^T = 
                 (^T: (static member Tan : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Tan(retype x)
                 when ^T : float32     = System.Math.Tan(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  tanhImpl(x: ^T) : ^T = 
                 (^T: (static member Tanh : ^T -> ^T) (x))
                 when ^T : float       = System.Math.Tanh(retype x)
                 when ^T : float32     = System.Math.Tanh(toFloat (retype x)) |> toFloat32

            [<NoDynamicInvocation(isLegacy=true)>]
            let inline  powImpl (x: ^T) (y: ^U) : ^T = 
                 (^T: (static member Pow : ^T * ^U -> ^T) (x,y))
                 when ^T : float       = System.Math.Pow((retype x : float), (retype y: float))
                 when ^T : float32     = System.Math.Pow(toFloat (retype x), toFloat(retype y)) |> toFloat32

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type AbsDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if   aty.Equals(typeof<sbyte>)      then unboxPrim<_>(fun (x:sbyte)     -> absImpl x)
                    elif aty.Equals(typeof<int16>)      then unboxPrim<_>(fun (x:int16)     -> absImpl x)
                    elif aty.Equals(typeof<int32>)      then unboxPrim<_>(fun (x:int32)     -> absImpl x)
                    elif aty.Equals(typeof<int64>)      then unboxPrim<_>(fun (x:int64)     -> absImpl x)
                    elif aty.Equals(typeof<nativeint>)  then unboxPrim<_>(fun (x:nativeint) -> absImpl x)
                    elif aty.Equals(typeof<float>)      then unboxPrim<_>(fun (x:float)     -> absImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> absImpl x)
                    elif aty.Equals(typeof<decimal>)    then unboxPrim<_>(fun (x:decimal)   -> absImpl x)
                    else UnaryDynamicImpl "Abs" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type AcosDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> acosImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> acosImpl x)
                    else UnaryDynamicImpl "Acos" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type AsinDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> asinImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> asinImpl x)
                    else UnaryDynamicImpl "Asin" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type AtanDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> atanImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> atanImpl x)
                    else UnaryDynamicImpl "Atan" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type Atan2DynamicImplTable<'T,'U>() = 
                static let result : ('T -> 'T -> 'U) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)  (y:float)   -> atan2Impl x y)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32) (y:float32)  -> atan2Impl x y)
                    else BinaryDynamicImpl "Atan2"
                static member Result : ('T -> 'T -> 'U) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type CeilingDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> ceilImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> ceilImpl x)
                    else UnaryDynamicImpl "Ceiling" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type ExpDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> expImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> expImpl x)
                    else UnaryDynamicImpl "Exp" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type FloorDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> floorImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> floorImpl x)
                    else UnaryDynamicImpl "Floor" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type TruncateDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> truncateImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> truncateImpl x)
                    else UnaryDynamicImpl "Truncate" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type RoundDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> roundImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> roundImpl x)
                    else UnaryDynamicImpl "Round" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type SignDynamicImplTable<'T>() = 
                static let result : ('T -> int) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> signImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> signImpl x)
                    elif aty.Equals(typeof<nativeint>)    then unboxPrim<_>(fun (x:nativeint)   -> signImpl x)
                    elif aty.Equals(typeof<decimal>)    then unboxPrim<_>(fun (x:decimal)   -> signImpl x)
                    elif aty.Equals(typeof<int16>)    then unboxPrim<_>(fun (x:int16)   -> signImpl x)
                    elif aty.Equals(typeof<int32>)    then unboxPrim<_>(fun (x:int32)   -> signImpl x)
                    elif aty.Equals(typeof<int64>)    then unboxPrim<_>(fun (x:int64)   -> signImpl x)
                    elif aty.Equals(typeof<sbyte>)    then unboxPrim<_>(fun (x:sbyte)   -> signImpl x)
                    else UnaryDynamicImpl "Sign" 
                static member Result : ('T -> int) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type LogDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> logImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> logImpl x)
                    else UnaryDynamicImpl "Log" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type Log10DynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> log10Impl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> log10Impl x)
                    else UnaryDynamicImpl "Log10" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type SqrtDynamicImplTable<'T,'U>() = 
                static let result : ('T -> 'U) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> sqrtImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> sqrtImpl x)
                    else UnaryDynamicImpl "Sqrt" 
                static member Result : ('T -> 'U) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type CosDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> cosImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> cosImpl x)
                    else UnaryDynamicImpl "Cos" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type CoshDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> coshImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> coshImpl x)
                    else UnaryDynamicImpl "Cosh" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type SinDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> sinImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> sinImpl x)
                    else UnaryDynamicImpl "Sin" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type SinhDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> sinhImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> sinhImpl x)
                    else UnaryDynamicImpl "Sinh" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type TanDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> tanImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> tanImpl x)
                    else UnaryDynamicImpl "Tan" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type TanhDynamicImplTable<'T>() = 
                static let result : ('T -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)     -> tanhImpl x)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32)   -> tanhImpl x)
                    else UnaryDynamicImpl "Tanh" 
                static member Result : ('T -> 'T) = result

            [<CodeAnalysis.SuppressMessage("Microsoft.Performance","CA1812:AvoidUninstantiatedInternalClasses")>]
            type PowDynamicImplTable<'T,'U>() = 
                static let result : ('T -> 'U -> 'T) = 
                    let aty = typeof<'T>
                    if aty.Equals(typeof<float>)        then unboxPrim<_>(fun (x:float)   (y:float)  -> powImpl x y)
                    elif aty.Equals(typeof<float32>)    then unboxPrim<_>(fun (x:float32) (y:float32)  -> powImpl x y)
                    else BinaryDynamicImpl "Pow" 
                static member Result : ('T -> 'U -> 'T) = result

            let AbsDynamic x            = AbsDynamicImplTable<_>.Result x
            let AcosDynamic x           = AcosDynamicImplTable<_>.Result x
            let AsinDynamic x           = AsinDynamicImplTable<_>.Result x
            let AtanDynamic x           = AtanDynamicImplTable<_>.Result x
            let Atan2Dynamic y x        = Atan2DynamicImplTable<_,_>.Result y x
            let CeilingDynamic x        = CeilingDynamicImplTable<_>.Result x 
            let ExpDynamic x            = ExpDynamicImplTable<_>.Result x 
            let FloorDynamic x          = FloorDynamicImplTable<_>.Result x 
            let TruncateDynamic x       = TruncateDynamicImplTable<_>.Result x 
            let RoundDynamic x          = RoundDynamicImplTable<_>.Result x 
            let SignDynamic x           = SignDynamicImplTable<_>.Result x 
            let LogDynamic x            = LogDynamicImplTable<_>.Result x 
            let Log10Dynamic x          = Log10DynamicImplTable<_>.Result x 
            let SqrtDynamic x           = SqrtDynamicImplTable<_,_>.Result x 
            let CosDynamic x            = CosDynamicImplTable<_>.Result x 
            let CoshDynamic x           = CoshDynamicImplTable<_>.Result x 
            let SinDynamic x            = SinDynamicImplTable<_>.Result x 
            let SinhDynamic x           = SinhDynamicImplTable<_>.Result x 
            let TanDynamic x            = TanDynamicImplTable<_>.Result x 
            let TanhDynamic x           = TanhDynamicImplTable<_>.Result x 
            let PowDynamic x y          = PowDynamicImplTable<_,_>.Result x y

        open OperatorIntrinsics
                   
        let inline (..) (start:^T) (finish:^T) = 
           RangeGeneric (GenericOne< (^T) >)  Checked.(+) start finish
           when ^T : int32       = RangeInt32   (retype start) 1    (retype finish)
           when ^T : float       = RangeDouble  (retype start) 1.0  (retype finish)
           when ^T : float32     = RangeSingle  (retype start) 1.0f (retype finish)
           when ^T : int64       = RangeInt64   (retype start) 1L   (retype finish)
           when ^T : uint64      = RangeUInt64  (retype start) 1UL  (retype finish)
           when ^T : uint32      = RangeUInt32  (retype start) 1ul  (retype finish)
           when ^T : nativeint   = RangeIntPtr  (retype start) 1n   (retype finish)
           when ^T : unativeint  = RangeUIntPtr (retype start) 1un  (retype finish)
           when ^T : int16       = RangeInt16   (retype start) 1s   (retype finish)
           when ^T : uint16      = RangeUInt16  (retype start) 1us  (retype finish)
           when ^T : sbyte       = RangeSByte   (retype start) 1y   (retype finish)
           when ^T : byte        = RangeByte    (retype start) 1uy  (retype finish)
           when ^T : char        = RangeChar    (retype start) (retype finish)

        let inline (.. ..) (start: ^T) (step: ^U) (finish: ^T) = 
           RangeStepGeneric (GenericZero< (^U) >) Checked.(+) start step finish
           when ^T : int32       = RangeInt32   (retype start) (retype step) (retype finish)
           when ^T : float       = RangeDouble  (retype start) (retype step) (retype finish)
           when ^T : float32     = RangeSingle  (retype start) (retype step) (retype finish)
           when ^T : int64       = RangeInt64   (retype start) (retype step) (retype finish)
           when ^T : uint64      = RangeUInt64  (retype start) (retype step) (retype finish)
           when ^T : uint32      = RangeUInt32  (retype start) (retype step) (retype finish)
           when ^T : nativeint   = RangeIntPtr  (retype start) (retype step) (retype finish)
           when ^T : unativeint  = RangeUIntPtr (retype start) (retype step) (retype finish)
           when ^T : int16       = RangeInt16   (retype start) (retype step) (retype finish)
           when ^T : uint16      = RangeUInt16  (retype start) (retype step) (retype finish)
           when ^T : sbyte       = RangeSByte   (retype start) (retype step) (retype finish)
           when ^T : byte        = RangeByte    (retype start) (retype step) (retype finish)
        

        [<CompiledName("Abs")>]
        let inline abs (value: ^T) : ^T = 
             AbsDynamic value
             when ^T : ^T = absImpl value

        [<CompiledName("Acos")>]
        let inline  acos (value: ^T) : ^T = 
             AcosDynamic value
             when ^T : ^T       = acosImpl value

        [<CompiledName("Asin")>]
        let inline  asin (value: ^T) : ^T = 
             AsinDynamic value
             when ^T : ^T       = asinImpl value

        [<CompiledName("Atan")>]
        let inline  atan (value: ^T) : ^T = 
             AtanDynamic value
             when ^T : ^T       = atanImpl value

        [<CompiledName("Atan2")>]
        let inline  atan2(y: ^T) (x: ^T) : 'U = 
             Atan2Dynamic y x
             when ^T : ^T       = (atan2Impl y x : 'U)

        [<CompiledName("Ceiling")>]
        let inline  ceil (value: ^T) : ^T = 
             CeilingDynamic value
             when ^T : ^T       = ceilImpl value

        [<CompiledName("Exp")>]
        let inline  exp(value: ^T) : ^T = 
             ExpDynamic value
             when ^T : ^T       = expImpl value

        [<CompiledName("Floor")>]
        let inline floor (value: ^T) : ^T = 
             FloorDynamic value
             when ^T : ^T       = floorImpl value

        [<CompiledName("Truncate")>]
        let inline truncate (value: ^T) : ^T = 
             TruncateDynamic value
             when ^T : ^T       = truncateImpl value

        [<CompiledName("Round")>]
        let inline round (value: ^T) : ^T = 
             RoundDynamic value
             when ^T : ^T       = roundImpl value

        [<CompiledName("Sign")>]
        let inline sign (value: ^T) : int = 
             SignDynamic value
             when ^T : ^T       = signImpl value

        [<CompiledName("Log")>]
        let inline  log (value: ^T) : ^T = 
             LogDynamic value
             when ^T : ^T       = logImpl value

        [<CompiledName("Log10")>]
        let inline  log10 (value: ^T) : ^T = 
             Log10Dynamic value
             when ^T : ^T       = log10Impl value

        [<CompiledName("Sqrt")>]
        let inline  sqrt (value: ^T) : ^U = 
             SqrtDynamic value
             when ^T : ^T       = (sqrtImpl value : ^U)

        [<CompiledName("Cos")>]
        let inline  cos (value: ^T) : ^T = 
             CosDynamic value
             when ^T : ^T       = cosImpl value

        [<CompiledName("Cosh")>]
        let inline cosh (value: ^T) : ^T = 
             CoshDynamic value
             when ^T : ^T       = coshImpl value

        [<CompiledName("Sin")>]
        let inline sin (value: ^T) : ^T = 
             SinDynamic value
             when ^T : ^T       = sinImpl value

        [<CompiledName("Sinh")>]
        let inline sinh (value: ^T) : ^T = 
             SinhDynamic value
             when ^T : ^T       = sinhImpl value

        [<CompiledName("Tan")>]
        let inline tan (value: ^T) : ^T = 
             TanDynamic value
             when ^T : ^T       = tanImpl value

        [<CompiledName("Tanh")>]
        let inline tanh (value: ^T) : ^T = 
             TanhDynamic value
             when ^T : ^T       = tanhImpl value

        let inline ( ** ) (x: ^T) (y: ^U) : ^T = 
             PowDynamic x y
             when ^T : ^T       = powImpl x y

        let inline gpown  (x: ^T) n =
            let v = PowGeneric (GenericOne< (^T) >, Checked.( * ), x,n) 
            if n < 0 then GenericOne< (^T) > / v
            else v

        [<CompiledName("PowInteger")>]
        let inline pown  (x: ^T) n =
             (if n = Int32.MinValue then gpown x (n+1) / x else gpown x n)
             when ^T : int32 = 
                         (let x = (retype x : int32) in
                          if  x = 2 && n >= 0 && n < 31 then 1 <<< n 
                          elif n >= 0 then PowInt32 x n 
                          else 1 / PowInt32 x n)
             when ^T : int64 = 
                         (let x = (retype x : int64) in
                          if  x = 2L && n >= 0 && n < 63 then 1L <<< n 
                          elif n >= 0 then PowInt64 x n 
                          else 1L / PowInt64 x n)
             when ^T : int16 = 
                         (let x = (retype x : int16) in
                          if  x = 2s && n >= 0 && n < 15 then 1s <<< n 
                          elif n >= 0 then PowInt16 x n 
                          else 1s / PowInt16 x n)
             when ^T : sbyte = 
                         (let x = (retype x : sbyte) in
                          if  x = 2y && n >= 0 && n < 7 then 1y <<< n 
                          elif n >= 0 then PowSByte x n 
                          else 1y / PowSByte x n)
             when ^T : nativeint = 
                         (let x = (retype x : nativeint) in
                          if  x = 2n && n >= 0 && n < 31 then 1n <<< n 
                          elif n >= 0 then PowIntPtr x n 
                          else 1n / PowIntPtr x n)
             when ^T : uint32 = 
                         (let x = (retype x : uint32) in
                          if  x = 2u && n >= 0 && n <= 31 then 1u <<< n 
                          elif n >= 0 then PowUInt32 x n 
                          else 1u / PowUInt32 x n)
             when ^T : uint64 = 
                         (let x = (retype x : uint64) in
                          if  x = 2UL && n >= 0 && n <= 63 then 1UL <<< n 
                          elif n >= 0 then PowUInt64 x n 
                          else 1UL / PowUInt64 x n)
             when ^T : uint16 = 
                         (let x = (retype x : uint16) in
                          if  x = 2us && n >= 0 && n <= 15 then 1us <<< n 
                          elif n >= 0 then PowUInt16 x n 
                          else 1us / PowUInt16 x n)
             when ^T : byte = 
                         (let x = (retype x : byte) in
                          if  x = 2uy && n >= 0 && n <= 7 then 1uy <<< n 
                          elif n >= 0 then PowByte x n 
                          else 1uy / PowByte x n)
             when ^T : unativeint = 
                         (let x = (retype x : unativeint) in
                          if  x = 2un && n >= 0 && n <= 31 then 1un <<< n 
                          elif n >= 0 then PowUIntPtr x n 
                          else 1un / PowUIntPtr x n)

             when ^T : float       = 
                         (let x = (retype x : float) in
                         if n >= 0 then PowDouble x n else 1.0 /  PowDouble x n)
             when ^T : float32     = 
                         (let x = (retype x : float32) in
                          if n >= 0 then PowSingle x n else 1.0f /  PowSingle x n)
             when ^T : decimal     = 
                         (let x = (retype x : decimal) in
                          if n >= 0 then PowDecimal x n else 1.0M /  PowDecimal x n)

        [<AutoOpen>]
        [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
        module ArrayExtensions =
            type ``[,,,]``<'T> with
                [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
                member arr.GetReverseIndex(dim: int, offset: int) = 
                    let len = 
                        match dim with
                        | 0 -> GetArray4DLength1 arr
                        | 1 -> GetArray4DLength2 arr
                        | 2 -> GetArray4DLength3 arr
                        | 3 -> GetArray4DLength4 arr
                        | _ -> raise (System.IndexOutOfRangeException())

                    len - offset - 1

            type ``[,,]``<'T> with
                [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
                member arr.GetReverseIndex(dim: int, offset: int) = 
                    let len = 
                        match dim with
                        | 0 -> GetArray3DLength1 arr
                        | 1 -> GetArray3DLength2 arr
                        | 2 -> GetArray3DLength3 arr
                        | _ -> raise (System.IndexOutOfRangeException())

                    len - offset - 1

            type ``[,]``<'T> with
                [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
                member arr.GetReverseIndex(dim: int, offset: int) = 
                    let len = 
                        match dim with
                        | 0 -> GetArray2DLength1 arr
                        | 1 -> GetArray2DLength2 arr
                        | _ -> raise (System.IndexOutOfRangeException())

                    len - offset - 1

            type ``[]``<'T> with
                [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
                member arr.GetReverseIndex (_: int, offset: int) = arr.Length - offset - 1

            type System.String with
                [<Experimental(ExperimentalAttributeMessages.RequiresPreview)>]
                member str.GetReverseIndex (_: int, offset: int) = str.Length - offset - 1

namespace Microsoft.FSharp.Control

    open System    
    open System.Threading    
    open System.Diagnostics
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.Operators

    module LazyExtensions = 
        type System.Lazy<'T> with
            [<CompiledName("Create")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            static member Create(creator : unit -> 'T) : Lazy<'T> =
                let creator = Func<'T>(creator)
                Lazy<'T>(creator, true)

            [<CompiledName("CreateFromValue")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            static member CreateFromValue(value : 'T) : Lazy<'T> =
                Lazy<'T>.Create(fun () -> value)

            [<CompiledName("IsDelayedDeprecated")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.IsDelayed = not(x.IsValueCreated)

            [<CompiledName("IsForcedDeprecated")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.IsForced = x.IsValueCreated

            [<CompiledName("Force")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.Force() = x.Value

            [<CompiledName("SynchronizedForceDeprecated")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.SynchronizedForce() = x.Value

            [<CompiledName("UnsynchronizedForceDeprecated")>] // give the extension member a 'nice', unmangled compiled name, unique within this module
            member x.UnsynchronizedForce() = x.Value
            
    type Lazy<'T> = System.Lazy<'T>

    type 'T ``lazy`` = Lazy<'T>       


namespace Microsoft.FSharp.Control

    open System
    open Microsoft.FSharp.Core

    type IDelegateEvent<'Delegate when 'Delegate :> System.Delegate > =
        abstract AddHandler: handler:'Delegate -> unit
        abstract RemoveHandler: handler:'Delegate -> unit 

    type IEvent<'Delegate,'Args when 'Delegate : delegate<'Args,unit> and 'Delegate :> System.Delegate > =
        inherit IDelegateEvent<'Delegate>
        inherit IObservable<'Args>

    [<CompiledName("FSharpHandler`1")>]
    type Handler<'Args> =  delegate of sender:obj * args:'Args -> unit 

    type IEvent<'Args> = IEvent<Handler<'Args>, 'Args>

    do()
