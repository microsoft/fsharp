﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open NUnit.Framework

#if NETCOREAPP

[<TestFixture>]
module DefaultInterfaceMethodConsumptionTests_LanguageVersion_4_6 =

    [<Test>]
    let ``IL - Errors with lang version not supported`` () =
        let ilSource =
            """
.class interface public abstract auto ansi ILTest.ITest
{
    .method public hidebysig newslot virtual instance void  DefaultMethod() cil managed
    {
        .maxstack  8
        IL_0000:  ret
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open ILTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateILCompilation ilSource
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }        
            {
                Number = 366
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "No implementation was given for 'ITest.DefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# with explicit implementation - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write("ITest1." + nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method2()
        {
            Console.Write("ITest2" + nameof(Method2));
        }

        void Method3();
    }

    public interface ITest3 : ITest2
    {
        void ITest2.Method3()
        {
            Console.Write("ITest3" + nameof(Method3));
        }

        void Method4();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest3 with

        member __.Method1 () = Console.Write("FSharp-Method1")

        member __.Method2 () = Console.Write("FSharp-Method2")

        member __.Method3 () = Console.Write("FSharp-Method3")

        member __.Method4 () = Console.Write("FSharp-Method4")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest3
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    Console.Write("-")
    test.Method3 ()
    Console.Write("-")
    test.Method4 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "FSharp-Method1-FSharp-Method2-FSharp-Method3-FSharp-Method4", fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple - Errors with lang version not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open CSharpTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 366;
                StartLine = 8;
                StartColumn = 14;
                EndLine = 8;
                EndColumn = 19;
                Message = "No implementation was given for those members: 
	'ITest.DefaultMethod() : unit'
	'ITest.NonDefaultMethod() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple - Errors with lang version not supported - 2`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () = ()
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 366
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "No implementation was given for 'ITest.DefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple - Errors with lang version not supported - 3`` () =
        let csharpSource =
            """
namespace CSharpTest
{
    public interface ITest
    {
        void Method1()
        {
        }

        void Method2()
        {
        }
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open CSharpTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 366
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "No implementation was given for those members: 
	'ITest.Method1() : unit'
	'ITest.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple with internal DIM - Errors with lang version not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () = Console.Write("NonDefaultMethod")
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 366
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 19
                Message = "No implementation was given for 'ITest.DefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple with internal DIM - Errors with not accessible`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest with

        member __.DefaultMethod () = Console.Write("DefaultMethod")

        member __.NonDefaultMethod () = Console.Write("NonDefaultMethod")
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 855
                StartLine = 11
                StartColumn = 18
                EndLine = 11
                EndColumn = 31
                Message = "No abstract or interface member was found that corresponds to this override"
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple with static operator method - Errors with lang version not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface I1
    {
        public static int operator +(I1 x, I1 y)
        {
            Console.Write("I1.+");
            return 1;
        }
    }
 
    public interface I2 : I1
    {}
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface I2

let f () =
    let x = Test () :> I1
    let y = Test () :> I2
    x + y
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 14
                StartColumn = 6
                EndLine = 14
                EndColumn = 7
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 3302
                StartLine = 14
                StartColumn = 8
                EndLine = 14
                EndColumn = 9
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple with static method - Errors with lang version not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface I1
    {
        public static int StaticMethod(I1 x, I1 y)
        {
            Console.Write("I1.+");
            return 1;
        }
    }
 
    public interface I2 : I1
    {}
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface I2

let f () =
    let x = Test () :> I1
    let y = Test () :> I2
    I1.StaticMethod (x, y)
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3302
                StartLine = 14
                StartColumn = 4
                EndLine = 14
                EndColumn = 26
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
        ], fsharpLanguageVersion = "4.6")

#else

[<TestFixture>]
module DefaultInterfaceMethodConsumptionTests_LanguageVersion_4_6_net472 =

    [<Test>]
    let ``IL - Errors with lang version and target runtime not supported`` () =
        let ilSource =
            """
.class interface public abstract auto ansi ILTest.ITest
{
    .method public hidebysig newslot virtual instance void  DefaultMethod() cil managed
    {
        .maxstack  8
        IL_0000:  ret
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open ILTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateILCompilation ilSource
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3303
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not supported by target runtime."
            }
            {
                Number = 3302
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
            {
                Number = 366;
                StartLine = 8;
                StartColumn = 14;
                EndLine = 8;
                EndColumn = 19;
                Message = "No implementation was given for 'ITest.DefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``IL - Errors with target runtime not supported when implemented`` () =
        let ilSource =
            """
.class interface public abstract auto ansi ILTest.ITest
{
    .method public hidebysig newslot virtual instance void  DefaultMethod() cil managed
    {
        .maxstack  8
        IL_0000:  ret
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open ILTest

type Test () =

    interface ITest with

        member __.DefaultMethod () = ()
            """

        let c = CompilationUtil.CreateILCompilation ilSource
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3303
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not supported by target runtime."
            }
        ], fsharpLanguageVersion = "4.6")

    [<Test>]
    let ``C# simple with static method - Errors with lang version and target runtime not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface I1
    {
        public static int StaticMethod(I1 x, I1 y)
        {
            Console.Write("I1.+");
            return 1;
        }
    }
 
    public interface I2 : I1
    {}
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface I2

let f () =
    let x = Test () :> I1
    let y = Test () :> I2
    I1.StaticMethod (x, y)
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3303
                StartLine = 14
                StartColumn = 4
                EndLine = 14
                EndColumn = 26
                Message = "Feature 'default interface method consumption' is not supported by target runtime."
            }
            {
                Number = 3302
                StartLine = 14
                StartColumn = 4
                EndLine = 14
                EndColumn = 26
                Message = "Feature 'default interface method consumption' is not available in F# 4.6. Please use language version 4.7 or greater."
            }
        ], fsharpLanguageVersion = "4.6")

#endif

#if NETCOREAPP

[<TestFixture>]
module DefaultInterfaceMethodConsumptionTests =

    [<Test>]
    let ``C# simple - Errors with un-implemented non-DIM`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open CSharpTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 366
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "No implementation was given for 'ITest.NonDefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# simple with static operator method - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface I1
    {
        public static int operator +(I1 x, I1 y)
        {
            Console.Write("I1.+");
            return 1;
        }
    }
 
    public interface I2 : I1
    {}
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface I2

[<EntryPoint>]
let main _ =
    let x = Test () :> I1
    let y = Test () :> I2
    Console.Write(string (x + y))
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "I1.+1")

    [<Test>]
    let ``C# simple - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    test.DefaultMethod ()
    Console.Write("-")
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "DefaultMethod-NonDefaultMethod")

    [<Test>]
    let ``C# simple with internal DIM - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "NonDefaultMethod")

    [<Test>]
    let ``C# simple with internal DIM - Errors with missing method`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

let f () =
    let test = Test () :> ITest
    test.DefaultMethod ()
    test.NonDefaultMethod ()
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 39
                StartLine = 16
                StartColumn = 9
                EndLine = 16
                EndColumn = 22
                Message = "The field, constructor or member 'DefaultMethod' is not defined. Maybe you want one of the following:
   NonDefaultMethod"
            }
        ])

    [<Test>]
    let ``C# simple with internal DIM - Errors with not accessible`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest with

        member __.DefaultMethod () =
            Console.Write("DefaultMethod")

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 855;
                StartLine = 11;
                StartColumn = 18;
                EndLine = 11;
                EndColumn = 31;
                Message = "No abstract or interface member was found that corresponds to this override"
            }
        ])

    [<Test>]
    let ``C# simple with internal DIM but with IVT - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        internal void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.DefaultMethod () =
            Console.Write("IVT-")

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    test.DefaultMethod ()
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30, flags = CSharpCompilationFlags.InternalsVisibleTo)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "IVT-NonDefaultMethod")

    [<Test>]
    let ``C# simple with one DIM for F# object expression - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

[<EntryPoint>]
let main _ =
    let test = { new ITest }
    test.DefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "DefaultMethod")

    [<Test>]
    let ``C# simple with one DIM and one non-DIM for F# object expression - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

[<EntryPoint>]
let main _ =
    let test = { new ITest with member __.NonDefaultMethod () = Console.Write("ObjExpr") }
    test.DefaultMethod ()
    Console.Write("-")
    test.NonDefaultMethod();
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "DefaultMethod-ObjExpr")

    [<Test>]
    let ``C# simple with one DIM and one non-DIM for F# object expression - Errors with lack of implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

let test = { new ITest }
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 366
                StartLine = 7
                StartColumn = 11
                EndLine = 7
                EndColumn = 24
                Message = "No implementation was given for 'ITest.NonDefaultMethod() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# simple with override - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.DefaultMethod () =
            Console.Write("OverrideDefaultMethod")

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    test.DefaultMethod ()
    Console.Write("-")
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "OverrideDefaultMethod-NonDefaultMethod")

    [<Test>]
    let ``C# simple with override for object expression - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest
    {
        void DefaultMethod()
        {
            Console.Write(nameof(DefaultMethod));
        }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

[<EntryPoint>]
let main _ =
    let test =
        { new ITest with
            member __.DefaultMethod () =
                Console.Write("ObjExprOverrideDefaultMethod")
            member __.NonDefaultMethod () =
                Console.Write("ObjExprNonDefaultMethod") }
    test.DefaultMethod ()
    Console.Write("-")
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "ObjExprOverrideDefaultMethod-ObjExprNonDefaultMethod")

    [<Test>]
    let ``C# from hierarchical interfaces - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest2

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest1
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "FromITest2-Method1-FromITest2-Method2")

    [<Test>]
    let ``C# diamond hierarchical interfaces - Errors with lack of explicit shared interface type`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest2
    interface ITest3
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 363
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "The interface 'ITest1' is included in multiple explicitly implemented interface types. Add an explicit implementation of this interface."
            }
            {
                Number = 3304
                StartLine = 10
                StartColumn = 14
                EndLine = 10
                EndColumn = 20
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 10
                StartColumn = 14
                EndLine = 10
                EndColumn = 20
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 10
                StartColumn = 14
                EndLine = 10
                EndColumn = 20
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces - Errors with no most specific implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface ITest1
    interface ITest2
    interface ITest3
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 20
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces but combined in one C# interface - Errors with no most specific implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest : ITest2, ITest3
    {
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface ICombinedTest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 27
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 27
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 9
                StartColumn = 14
                EndLine = 9
                EndColumn = 27
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces but combined in one F# interface - Errors with no most specific implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type ICombinedTest =
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])


    [<Test>]
    let ``C# diamond hierarchical interfaces but re-abstracted in one and then combined in one F# interface - Errors with no most specific implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type ICombinedTest =
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces but all re-abstracted and then combined in one F# interface - Errors with need to implement members`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }

    public interface ITest3 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type ICombinedTest =
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 3304
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "Interface member 'ITest1.Method2() : unit' does not have a most specific implementation."
            }
            {
                Number = 366
                StartLine = 13
                StartColumn = 14
                EndLine = 13
                EndColumn = 27
                Message = "No implementation was given for those members: 
	'ITest1.Method1() : unit'
	'ITest1.Method2() : unit'
Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces then combined in one F# interface and then implemented - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type ICombinedTest =
    inherit ITest1
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest with

        member __.Method1 () = Console.Write("FSharpICombinedTest-Method1")

        member __.Method2 () = Console.Write("FSharpICombinedTest-Method2")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest3
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "FSharpICombinedTest-Method1-FSharpICombinedTest-Method2")

    [<Test>]
    let ``C# diamond hierarchical interfaces but all re-abstracted and then combined in one F# interface and then implemented - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }

    public interface ITest3 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type ICombinedTest =
    inherit ITest1
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest with

        member __.Method1 () = Console.Write("FSharpICombinedTest-Method1")

        member __.Method2 () = Console.Write("FSharpICombinedTest-Method2")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest2
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "FSharpICombinedTest-Method1-FSharpICombinedTest-Method2")

    [<Test>]
    let ``C# diamond hierarchical interfaces but all re-abstracted and then combined in one F# interface and then implemented one method - Errors with no most specific implementation`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }

    public interface ITest3 : ITest1
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type ICombinedTest =
    inherit ITest1
    inherit ITest2
    inherit ITest3

type Test () =

    interface ICombinedTest with

        member __.Method2 () = Console.Write("FSharpICombinedTest-Method2")
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3304;
                StartLine = 14;
                StartColumn = 14;
                EndLine = 14;
                EndColumn = 27;
                Message = "Interface member 'ITest1.Method1() : unit' does not have a most specific implementation."
            }
            {
                Number = 366;
                StartLine = 14;
                StartColumn = 14;
                EndLine = 14;
                EndColumn = 27;
                Message = "No implementation was given for 'ITest1.Method1() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# diamond hierarchical interfaces then combined in one C# interface and then implemented - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedTest-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ICombinedTest

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest1
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "CSharpICombinedTest-Method1-CSharpICombinedTest-Method2")

    [<Test>]
    let ``C# diamond complex hierarchical interfaces then combined in one C# interface and then implemented - Runs`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest3-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest1 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method2));
        }
    }

    public interface ICombinedTest2 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedTest2-" + nameof(Method2));
        }
    }

    public interface ICombinedSideTest : ICombinedTest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedSideTest-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedSideTest-" + nameof(Method2));
        }
    }

    public interface IFinalCombinedTest : ICombinedTest1, ICombinedSideTest
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface IFinalCombinedTest

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest1
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "CSharpIFinalCombinedTest-Method1-CSharpIFinalCombinedTest-Method2")

    [<Test>]
    let ``C# diamond complex hierarchical interfaces then combined in one C# interface and then implemented - Runs - 2`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest1 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method2));
        }
    }

    public interface ICombinedTest2 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest2-" + nameof(Method1));
        }
    }

    public interface ICombinedSideTest : ICombinedTest1
    {
        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedSideTest-" + nameof(Method2));
        }
    }

    public interface IFinalCombinedTest : ICombinedTest2, ICombinedSideTest
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method1));
        }

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ICombinedSideTest with

        member __.Method2 () = ()

    interface IFinalCombinedTest

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest1
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "CSharpIFinalCombinedTest-Method1-")

    [<Test>]
    let ``C# diamond complex hierarchical interfaces then combined in one C# interface and then implemented - Runs - 3`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest1 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedTest1-" + nameof(Method2));
        }
    }

    public interface ICombinedTest2 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest2-" + nameof(Method1));
        }
    }

    public interface ICombinedSideTest : ICombinedTest1
    {
        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedSideTest-" + nameof(Method2));
        }
    }

    public interface IFinalCombinedTest : ICombinedTest2, ICombinedSideTest
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method2));
        }
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ICombinedSideTest
    interface IFinalCombinedTest
    interface ITest1

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest1
    test.Method1 ()
    Console.Write("-")
    test.Method2 ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "CSharpIFinalCombinedTest-Method1-CSharpIFinalCombinedTest-Method2")

    [<Test>]
    let ``C# diamond complex hierarchical interfaces then combined in one C# interface and then implemented - Errors with no impl`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface ITest1
    {
        void Method1()
        {
            Console.Write(nameof(Method1));
        }

        void Method2();
    }

    public interface ITest2 : ITest1
    {
        void ITest1.Method1()
        {
            Console.Write("FromITest2-" + nameof(Method1));
        }

        void ITest1.Method2()
        {
            Console.Write("FromITest2-" + nameof(Method2));
        }
    }

    public interface ITest3 : ITest1
    {
        void ITest1.Method2()
        {
            Console.Write("FromITest3-" + nameof(Method2));
        }
    }

    public interface ICombinedTest1 : ITest3, ITest2
    {
        abstract void ITest1.Method1();

        abstract void ITest1.Method2();
    }

    public interface ICombinedTest2 : ITest3, ITest2
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpICombinedTest2-" + nameof(Method1));
        }
    }

    public interface ICombinedSideTest : ICombinedTest1
    {
        void ITest1.Method2()
        {
            Console.Write("CSharpICombinedSideTest-" + nameof(Method2));
        }
    }

    public interface IFinalCombinedTest : ICombinedTest2, ICombinedSideTest
    {
        void ITest1.Method1()
        {
            Console.Write("CSharpIFinalCombinedTest-" + nameof(Method1));
        }

        abstract void ITest1.Method2();
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open System
open CSharpTest

type Test () =

    interface IFinalCombinedTest
    interface ICombinedSideTest

type Test2 () =
    inherit Test ()
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 366;
                StartLine = 10;
                StartColumn = 14;
                EndLine = 10;
                EndColumn = 31;
                Message = "No implementation was given for 'ITest1.Method2() : unit'. Note that all interface members must be implemented and listed under an appropriate 'interface' declaration, e.g. 'interface ... with member ...'."
            }
        ])

    [<Test>]
    let ``C# simple with property - Runs`` () =
        let csharpSource =
            """
namespace CSharpTest
{
    public interface ITest
    {
        string A { get { return "A"; } }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    Console.Write(test.A)
    Console.Write("-")
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "A-NonDefaultMethod")

    [<Test>]
    let ``C# simple with property and override - Runs`` () =
        let csharpSource =
            """
namespace CSharpTest
{
    public interface ITest
    {
        string A { get { return "A"; } }

        void NonDefaultMethod();
    }
}
            """

        let fsharpSource =
            """
open System
open CSharpTest

type Test () =

    interface ITest with

        member __.A with get () = "OverrideA"

        member __.NonDefaultMethod () =
            Console.Write("NonDefaultMethod")

[<EntryPoint>]
let main _ =
    let test = Test () :> ITest
    Console.Write(test.A)
    Console.Write("-")
    test.NonDefaultMethod ()
    0
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.CompileExeAndRun (fsharpSource, c, "OverrideA-NonDefaultMethod")

#else

[<TestFixture>]
module DefaultInterfaceMethodConsumptionTests_net472 =

    [<Test>]
    let ``IL - Errors with target runtime not supported`` () =
        let ilSource =
            """
.class interface public abstract auto ansi ILTest.ITest
{
    .method public hidebysig newslot virtual instance void  DefaultMethod() cil managed
    {
        .maxstack  8
        IL_0000:  ret
    }
}
            """

        let fsharpSource =
            """
namespace FSharpTest

open ILTest

type Test () =

    interface ITest
            """

        let c = CompilationUtil.CreateILCompilation ilSource
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3303
                StartLine = 8
                StartColumn = 14
                EndLine = 8
                EndColumn = 19
                Message = "Feature 'default interface method consumption' is not supported by target runtime."
            }
        ])

    [<Test>]
    let ``C# simple with static method - Errors with target runtime not supported`` () =
        let csharpSource =
            """
using System;

namespace CSharpTest
{
    public interface I1
    {
        public static int StaticMethod(I1 x, I1 y)
        {
            Console.Write("I1.+");
            return 1;
        }
    }
 
    public interface I2 : I1
    {}
}
            """

        let fsharpSource =
            """
module FSharpTest

open System
open CSharpTest

type Test () =

    interface I2

let f () =
    let x = Test () :> I1
    let y = Test () :> I2
    I1.StaticMethod (x, y)
            """

        let c = CompilationUtil.CreateCSharpCompilation (csharpSource, RoslynLanguageVersion.CSharp8, TargetFramework.NetCoreApp30)
        CompilerAssert.HasTypeCheckErrors (fsharpSource, c, [
            {
                Number = 3303
                StartLine = 14
                StartColumn = 4
                EndLine = 14
                EndColumn = 26
                Message = "Feature 'default interface method consumption' is not supported by target runtime."
            }
        ])

#endif