// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open System.Collections.Immutable
open NUnit.Framework
open FSharp.Test
open FSharp.Test.Utilities
open FSharp.Test.Compiler
open FSharp.Compiler.Diagnostics
open Microsoft.CodeAnalysis

[<TestFixture>]
module OptionalInteropTests =

    [<Test>]
    let ``C# method with an optional parameter and called with an option type should compile`` () =
        let csSrc =
            """
using Microsoft.FSharp.Core;

namespace CSharpTest
{
    public static class Test
    {
        public static void M(FSharpOption<int> x = null) { }
        public static int MethodTakingOptionals(int x = 3, string y = "abc", double d = 5.0)
        {
            return x + y.Length + (int) d;
        }
        public static int MethodTakingNullableOptionalsWithDefaults(int? x = 3, string y = "abc", double? d = 5.0)
        {
            return (x.HasValue ? x.Value : -100) + y.Length + (int) (d.HasValue ? d.Value : 0.0);
        }
        public static int MethodTakingNullableOptionals(int? x = null, string y = null, double? d = null)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0);
        }
        public static int OverloadedMethodTakingOptionals(int x = 3, string y = "abc", double d = 5.0)
        {
            return x + y.Length + (int) d;
        }
        public static int OverloadedMethodTakingOptionals(int x = 3, string y = "abc", System.Single d = 5.0f)
        {
            return x + y.Length + (int) d + 7;
        }
        public static int OverloadedMethodTakingNullableOptionalsWithDefaults(int? x = 3, string y = "abc", double? d = 5.0)
        {
            return (x.HasValue ? x.Value : -100) + y.Length + (int) (d.HasValue ? d.Value : 0.0);
        }
        public static int OverloadedMethodTakingNullableOptionalsWithDefaults(long? x = 3, string y = "abc", double? d = 5.0)
        {
            return (x.HasValue ? (int) x.Value : -100) + y.Length + (int) (d.HasValue ? d.Value : 0.0) + 7;
        }
        public static int OverloadedMethodTakingNullableOptionals(int? x = null, string y = null, double? d = null)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0);
        }
        public static int OverloadedMethodTakingNullableOptionals(long? x = null, string y = null, double? d = null)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? (int) x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0) + 7;
        }
        public static int MethodTakingNullables(int? x, string y, double? d)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0);
        }

        public static int OverloadedMethodTakingNullables(int? x, string y, double? d)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0);
        }
        public static int OverloadedMethodTakingNullables(long? x, string y, double? d)
        {
            int length;
            if (y == null)
                length = -1;
            else
                length = y.Length;
            return (x.HasValue ? (int) x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0) + 7;
        }
        public static int SimpleOverload(int? x = 3)
        {
            return (x.HasValue ? x.Value : 100);
        }

        public static int SimpleOverload(int x = 3)
        {
            return (x + 200);
        }
    }
}
            """

        let fsSrc =
            """
open System
open CSharpTest

Test.M(x = Some 1)
Test.MethodTakingNullables(6, "aaaaaa", 8.0) |> ignore
Test.MethodTakingNullables(6, "aaaaaa", Nullable 8.0) |> ignore
Test.MethodTakingNullables(6, "aaaaaa", Nullable ()) |> ignore
Test.MethodTakingNullables(Nullable (), "aaaaaa", 8.0) |> ignore
Test.MethodTakingNullables(Nullable 6, "aaaaaa", 8.0) |> ignore

Test.MethodTakingNullables(6, "aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(6, "aaaaaa", d=Nullable 8.0) |> ignore
Test.MethodTakingNullables(6, "aaaaaa", d=Nullable ()) |> ignore
Test.MethodTakingNullables(Nullable (), "aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(Nullable 6, "aaaaaa", d=8.0) |> ignore

Test.MethodTakingNullables(6, y="aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(6, y="aaaaaa", d=Nullable 8.0) |> ignore
Test.MethodTakingNullables(6, y="aaaaaa", d=Nullable ()) |> ignore
Test.MethodTakingNullables(Nullable (), y="aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(Nullable 6, y="aaaaaa", d=8.0) |> ignore

Test.MethodTakingNullables(6, y="aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(6, y="aaaaaa", d=Nullable 8.0) |> ignore
Test.MethodTakingNullables(6, y="aaaaaa", d=Nullable ()) |> ignore
Test.MethodTakingNullables(Nullable (), y="aaaaaa", d=8.0) |> ignore
Test.MethodTakingNullables(Nullable 6, y="aaaaaa", d=8.0) |> ignore
            """

        let fsharpCoreAssembly =
            typeof<_ list>.Assembly.Location
            |> MetadataReference.CreateFromFile

        let cs =
            CompilationUtil.CreateCSharpCompilation(csSrc, CSharpLanguageVersion.CSharp8, TargetFramework.NetCoreApp31, additionalReferences = ImmutableArray.CreateRange [fsharpCoreAssembly])
            |> CompilationReference.Create

        let fs = Compilation.Create(fsSrc, SourceKind.Fsx, CompileOutput.Exe, options = [|"--langversion:5.0"|], cmplRefs = [cs])
        CompilerAssert.Compile fs
