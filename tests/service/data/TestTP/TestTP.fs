namespace TestTP

open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open System.Reflection
open FSharp.Quotations

module Helper =
    let doNothing() = ()
    let doNothingOneArg(x:int) = ()
    let doNothingTwoArg(x:int, y: int) = ()
    let doNothingTwoArgCurried(x:int) (y: int) = ()
    [<CompiledName "DoNothingReally">]
    let doNothingWithCompiledName() = ()
    let doNothingGeneric(x:'T) = ()
    let doNothingGenericWithConstraint(x: 'T when 'T: equality) = ()
    let doNothingGenericWithTypeConstraint(x: 'T when 'T :> _ seq) = ()

    let mutable moduleValue = 0
    
    type I =
        abstract DoNothing: unit -> unit
    
    type B() =
        abstract VirtualDoNothing: unit -> unit
        default this.VirtualDoNothing() = () 

    type C() = 
        inherit B()
        let mutable p = 0
        static member DoNothing() = ()
        static member DoNothingOneArg(x:int) = ()
        static member DoNothingOneArg(x:string) = ()
        static member DoNothingTwoArg(c:C, x:int) = ()
        static member DoNothingTwoArgCurried (c:C) (x:int) = ()
        static member DoNothingGeneric(x:'T) = ()
        [<CompiledName "DoNothingReally">]
        static member DoNothingWithCompiledName() = ()
        member _.InstanceDoNothing() = ()
        member _.InstanceDoNothingOneArg(x:int) = ()
        member _.InstanceDoNothingOneArg(x:string) = ()
        member _.InstanceDoNothingTwoArg(c:C, x:int) = ()
        member _.InstanceDoNothingTwoArgCurried(c:C) (x:int) = ()
        member _.InstanceDoNothingGeneric(x:'T) = ()
        [<CompiledName "DoNothingReallyInst">]
        member _.InstanceDoNothingWithCompiledName() = ()
        override _.VirtualDoNothing() = ()

        member _.Property with get() = p and set v = p <- v
        member val AutoProperty = 0 with get, set
        static member val StaticAutoProperty = 0 with get, set

        interface I with 
            member this.DoNothing() = ()

    type G<'U>() = 
        static member DoNothing() = ()
        static member DoNothingOneArg(x:int) = ()
        static member DoNothingTwoArg(c:C, x:int) = ()
        static member DoNothingGeneric(x:'T) = ()
        member _.InstanceDoNothing() = ()
        member _.InstanceDoNothingOneArg(x:int) = ()
        member _.InstanceDoNothingTwoArg(c:C, x:int) = ()
        member _.InstanceDoNothingGeneric(x:'U) = ()

    type R = { A : int; mutable B : int }

open FSharp.Compiler.Service.Tests

[<TypeProvider>]
type BasicProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config)

    // resolve CSharp_Analysis from referenced assemblies
    do  System.AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->
            let name = AssemblyName(args.Name).Name.ToLowerInvariant()
            let an =
                config.ReferencedAssemblies
                |> Seq.tryFind (fun an ->
                    System.IO.Path.GetFileNameWithoutExtension(an).ToLowerInvariant() = name)
            match an with
            | Some f -> Assembly.LoadFrom f
            | None -> null
        )

    let ns = "ErasedWithConstructor.Provided"
    let asm = Assembly.GetExecutingAssembly()

    let createTypes () =
        let myType = ProvidedTypeDefinition(asm, ns, "MyType", Some typeof<obj>)

        let ctor = ProvidedConstructor([], invokeCode = fun args -> <@@ "My internal state" :> obj @@>)
        myType.AddMember(ctor)

        let ctor2 = ProvidedConstructor(
                        [ProvidedParameter("InnerState", typeof<string>)],
                        invokeCode = fun args -> <@@ (%%(args.[0]):string) :> obj @@>)
        myType.AddMember(ctor2)

        let innerState = ProvidedProperty("InnerState", typeof<string>,
                            getterCode = fun args -> <@@ (%%(args.[0]) :> obj) :?> string @@>)
        myType.AddMember(innerState)

        let someMethod = ProvidedMethod("DoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingOneArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingTwoArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingTwoArg(3, 4) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingTwoArgCurried", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingTwoArgCurried 3 4 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingWithCompiledName", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGeneric", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingGeneric(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGenericWithConstraint", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingGenericWithConstraint(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("DoNothingGenericWithTypeConstraint", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.doNothingGenericWithTypeConstraint([3]) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingGeneric", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothingGeneric(3) @@>)

        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingOneArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothingOneArg(3) @@>)

        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingTwoArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingTwoArgCurried", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothingTwoArgCurried (Helper.C()) 3 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassDoNothingWithCompiledName", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C.DoNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingGeneric", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothingGeneric(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingOneArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingTwoArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingTwoArgCurried", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothingTwoArgCurried (Helper.C()) 3 @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassInstanceDoNothingWithCompiledName", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().InstanceDoNothingWithCompiledName() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("InterfaceDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ (Helper.C() :> Helper.I).DoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("OverrideDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.C().VirtualDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>.DoNothing() @@>)
        myType.AddMember(someMethod)

        // These do not seem to compile correctly when used in provided expressions:
        //Helper.G<int>.DoNothingGeneric(3)

        // These do not seem to compile correctly when used in provided expressions:
        //Helper.G<int>().InstanceDoNothingGeneric(3)
                                                         
        let someMethod = ProvidedMethod("GenericClassDoNothingOneArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>.DoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassDoNothingTwoArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>.DoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothing", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>().InstanceDoNothing() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothingOneArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>().InstanceDoNothingOneArg(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("GenericClassInstanceDoNothingTwoArg", [], typeof<unit>,
                            invokeCode = fun args -> <@@ Helper.G<int>().InstanceDoNothingTwoArg(Helper.C(), 3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("OptionConstructionAndMatch", [], typeof<int>,
                            invokeCode = fun args -> <@@ match Some 1 with None -> 0 | Some x -> x @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ChoiceConstructionAndMatch", [], typeof<int>,
                            invokeCode = fun args -> <@@ match Choice1Of2 1 with Choice2Of2 _ -> 0 | Choice1Of2 _ -> 1 @@>)
            // TODO: fix type checker to recognize union generated subclasses coming from TPs
//                            invokeCode = fun args -> <@@ match Choice1Of2 1 with Choice2Of2 _ -> 0 | Choice1Of2 x -> x @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("RecordConstructionAndFieldGetSet", [], typeof<int>,
                            invokeCode = fun args -> <@@ let r : Helper.R = { A = 1; B = 0 } in r.B <- 1; r.A @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("TupleConstructionAndGet", [], typeof<int>,
                            invokeCode = fun args -> <@@ let t = (1, 2, 3) in (let (_, i, _) = t in i) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethod", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).Method("x") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodOptionalParam", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).Method2("x") + CSharpClass(0).Method2() @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodParamArray", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).Method3("x", "y") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGeneric", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).GenericMethod<int>(2) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGenericWithConstraint", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).GenericMethod2<obj>(obj()) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpMethodGenericWithTypeConstraint", [], typeof<unit>,
                            invokeCode = fun args -> <@@ CSharpClass(0).GenericMethod3<int>(3) @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("CSharpExplicitImplementationMethod", [], typeof<unit>,
                            invokeCode = fun args -> <@@ (CSharpClass(0) :> ICSharpExplicitInterface).ExplicitMethod("x") @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ModuleValue", [], typeof<int>,
                            invokeCode = fun args -> <@@ Helper.moduleValue <- 1; Helper.moduleValue @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassProperty", [], typeof<int>,
                            invokeCode = fun args -> <@@ let x = Helper.C() in x.Property <- 1; x.Property @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassAutoProperty", [], typeof<int>,
                            invokeCode = fun args -> <@@ let x = Helper.C() in x.AutoProperty <- 1; x.AutoProperty @@>)
        myType.AddMember(someMethod)

        let someMethod = ProvidedMethod("ClassStaticAutoProperty", [], typeof<int>,
                            invokeCode = fun args -> <@@ Helper.C.StaticAutoProperty <- 1; Helper.C.StaticAutoProperty @@>)
        myType.AddMember(someMethod)

        [myType]  

    do
        this.AddNamespace(ns, createTypes())

[<TypeProvider>]
type BasicGenerativeProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config)

    let ns = "GeneratedWithConstructor.Provided"
    let asm = Assembly.GetExecutingAssembly()

    let createType typeName (count:int) =
        let asm = ProvidedAssembly()
        let myType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)

        let ctor = ProvidedConstructor([], invokeCode = fun args -> <@@ "My internal state" :> obj @@>)
        myType.AddMember(ctor)

        let ctor2 = ProvidedConstructor([ProvidedParameter("InnerState", typeof<string>)], invokeCode = fun args -> <@@ (%%(args.[1]):string) :> obj @@>)
        myType.AddMember(ctor2)

        for i in 1 .. count do 
            let prop = ProvidedProperty("Property" + string i, typeof<int>, getterCode = fun args -> <@@ i @@>)
            myType.AddMember(prop)

        let meth = ProvidedMethod("StaticMethod", [], typeof<CSharpClass>, isStatic=true, invokeCode = (fun args -> Expr.Value(null, typeof<CSharpClass>)))
        myType.AddMember(meth)
        asm.AddTypes [ myType ]

        myType

    let myParamType = 
        let t = ProvidedTypeDefinition(asm, ns, "GenerativeProvider", Some typeof<obj>, isErased=false)
        t.DefineStaticParameters( [ProvidedStaticParameter("Count", typeof<int>)], fun typeName args -> createType typeName (unbox<int> args.[0]))
        t
    do
        this.AddNamespace(ns, [myParamType])

[<assembly:TypeProviderAssembly>]
do ()