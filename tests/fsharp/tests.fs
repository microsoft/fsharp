// To run these tests in F# Interactive , 'build net40', then send this chunk, then evaluate body of a test
#if INTERACTIVE
#r @"../../packages/NUnit.3.5.0/lib/net45/nunit.framework.dll"
#load "../../src/scripts/scriptlib.fsx"
#load "../FSharp.Test.Utilities/TestFramework.fs"
#load "single-test.fs"
#else
module FSharp.Tests.Core
#endif

open System
open System.IO
open System.Reflection
open System.Reflection.PortableExecutable
open NUnit.Framework
open TestFramework
open Scripting
open SingleTest
open HandleExpects
open FSharp.Test

#if NETCOREAPP
// Use these lines if you want to test CoreCLR
let FSC_BASIC = FSC_CORECLR
let FSC_BASIC_OPT_MINUS = FSC_CORECLR_OPT_MINUS
let FSC_BUILDONLY = FSC_CORECLR_BUILDONLY
let FSI_BASIC = FSI_CORECLR
#else
let FSC_BASIC = FSC_OPT_PLUS_DEBUG
let FSC_BASIC_OPT_MINUS = FSC_OPT_MINUS_DEBUG
let FSI_BASIC = FSI_FILE
#endif
// ^^^^^^^^^^^^ To run these tests in F# Interactive , 'build net40', then send this chunk, then evaluate body of a test ^^^^^^^^^^^^

let inline getTestsDirectory dir = getTestsDirectory __SOURCE_DIRECTORY__ dir
let singleTestBuildAndRun = getTestsDirectory >> singleTestBuildAndRun
let singleTestBuildAndRunVersion = getTestsDirectory >> singleTestBuildAndRunVersion
let testConfig = getTestsDirectory >> testConfig

[<NonParallelizable>]
module CoreTests =
    // These tests are enabled for .NET Framework and .NET Core
    [<Test>]
    let ``access-FSC_BASIC_OPT_MINUS``() = singleTestBuildAndRun "core/access" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``access-FSC_BASIC``() = singleTestBuildAndRun "core/access" FSC_BASIC

    [<Test>]
    let ``access-FSI_BASIC``() = singleTestBuildAndRun "core/access" FSI_BASIC

    [<Test>]
    let ``apporder-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/apporder" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``apporder-FSC_BASIC`` () = singleTestBuildAndRun "core/apporder" FSC_BASIC

    [<Test>]
    let ``apporder-FSI_BASIC`` () = singleTestBuildAndRun "core/apporder" FSI_BASIC

    [<Test>]
    let ``array-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/array" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``array-FSC_BASIC`` () = singleTestBuildAndRun "core/array" FSC_BASIC

    [<Test>]
    let ``array-FSI_BASIC`` () = singleTestBuildAndRun "core/array" FSI_BASIC

    [<Test>]
    let ``comprehensions-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/comprehensions" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``comprehensions-FSC_BASIC`` () = singleTestBuildAndRun "core/comprehensions" FSC_BASIC

    [<Test>]
    let ``comprehensions-FSI_BASIC`` () = singleTestBuildAndRun "core/comprehensions" FSI_BASIC

    [<Test>]
    let ``comprehensionshw-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/comprehensions-hw" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``comprehensionshw-FSC_BASIC`` () = singleTestBuildAndRun "core/comprehensions-hw" FSC_BASIC

    [<Test>]
    let ``comprehensionshw-FSI_BASIC`` () = singleTestBuildAndRun "core/comprehensions-hw" FSI_BASIC

    [<Test; Ignore("test fails in debug mode, see https://github.com/dotnet/fsharp/pull/11763")>]
    let ``genericmeasures-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/genericmeasures" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``genericmeasures-FSC_BASIC`` () = singleTestBuildAndRun "core/genericmeasures" FSC_BASIC

    [<Test>]
    let ``genericmeasures-FSI_BASIC`` () = singleTestBuildAndRun "core/genericmeasures" FSI_BASIC

    [<Test>]
    let ``innerpoly-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/innerpoly"  FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``innerpoly-FSC_BASIC`` () = singleTestBuildAndRun "core/innerpoly" FSC_BASIC

    [<Test>]
    let ``innerpoly-FSI_BASIC`` () = singleTestBuildAndRun "core/innerpoly" FSI_BASIC

    [<Test>]
    let ``namespaceAttributes-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/namespaces" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``namespaceAttributes-FSC_BASIC`` () = singleTestBuildAndRun "core/namespaces" FSC_BASIC

    [<Test>]
    let ``unicode2-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/unicode" FSC_BASIC_OPT_MINUS // TODO: fails on coreclr

    [<Test>]
    let ``unicode2-FSC_BASIC`` () = singleTestBuildAndRun "core/unicode" FSC_BASIC // TODO: fails on coreclr

    [<Test>]
    let ``unicode2-FSI_BASIC`` () = singleTestBuildAndRun "core/unicode" FSI_BASIC

    [<Test>]
    let ``lazy test-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/lazy" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``lazy test-FSC_BASIC`` () = singleTestBuildAndRun "core/lazy" FSC_BASIC

    [<Test>]
    let ``lazy test-FSI_BASIC`` () = singleTestBuildAndRun "core/lazy" FSI_BASIC

    [<Test>]
    let ``letrec-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/letrec" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``letrec-FSC_BASIC`` () = singleTestBuildAndRun "core/letrec" FSC_BASIC

    [<Test>]
    let ``letrec-FSI_BASIC`` () = singleTestBuildAndRun "core/letrec" FSI_BASIC

    [<Test>]
    let ``letrec (mutrec variations part one) FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/letrec-mutrec" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``letrec (mutrec variations part one) FSC_BASIC`` () = singleTestBuildAndRun "core/letrec-mutrec" FSC_BASIC

    [<Test>]
    let ``letrec (mutrec variations part one) FSI_BASIC`` () = singleTestBuildAndRun "core/letrec-mutrec" FSI_BASIC

    [<Test>]
    let ``libtest-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/libtest" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``libtest-FSC_BASIC`` () = singleTestBuildAndRun "core/libtest" FSC_BASIC

    [<Test>]
    let ``libtest-FSI_BASIC`` () = singleTestBuildAndRun "core/libtest" FSI_BASIC

    [<Test>]
    let ``lift-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/lift" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``lift-FSC_BASIC`` () = singleTestBuildAndRun "core/lift" FSC_BASIC

    [<Test>]
    let ``lift-FSI_BASIC`` () = singleTestBuildAndRun "core/lift" FSI_BASIC

    [<Test>]
    let ``map-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/map" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``map-FSC_BASIC`` () = singleTestBuildAndRun "core/map" FSC_BASIC

    [<Test>]
    let ``map-FSI_BASIC`` () = singleTestBuildAndRun "core/map" FSI_BASIC

    [<Test>]
    let ``measures-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/measures" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``measures-FSC_BASIC`` () = singleTestBuildAndRun "core/measures" FSC_BASIC

    [<Test>]
    let ``measures-FSI_BASIC`` () = singleTestBuildAndRun "core/measures" FSI_BASIC

    [<Test>]
    let ``nested-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/nested" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``nested-FSC_BASIC`` () = singleTestBuildAndRun "core/nested" FSC_BASIC

    [<Test>]
    let ``nested-FSI_BASIC`` () = singleTestBuildAndRun "core/nested" FSI_BASIC

    [<Test>]
    let ``members-ops-FSC_BASIC`` () = singleTestBuildAndRun "core/members/ops" FSC_BASIC

    [<Test>]
    let ``members-ops-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/members/ops" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``members-ops-FSI_BASIC`` () = singleTestBuildAndRun "core/members/ops" FSI_BASIC

    [<Test>]
    let ``members-ops-mutrec-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/members/ops-mutrec" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``members-ops-mutrec-FSC_BASIC`` () = singleTestBuildAndRun "core/members/ops-mutrec" FSC_BASIC

    [<Test>]
    let ``members-ops-mutrec-FSI_BASIC`` () = singleTestBuildAndRun "core/members/ops-mutrec" FSI_BASIC

    [<Test>]
    let ``seq-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/seq" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``seq-FSC_BASIC`` () = singleTestBuildAndRun "core/seq" FSC_BASIC

    [<Test>]
    let ``seq-FSI_BASIC`` () = singleTestBuildAndRun "core/seq" FSI_BASIC

    [<Test>]
    let ``math-numbers-FSC_BASIC`` () = singleTestBuildAndRun "core/math/numbers" FSC_BASIC

    [<Test>]
    let ``math-numbers-FSI_BASIC`` () = singleTestBuildAndRun "core/math/numbers" FSI_BASIC

    [<Test>]
    let ``members-ctree-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/members/ctree" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``members-ctree-FSC_BASIC`` () = singleTestBuildAndRun "core/members/ctree" FSC_BASIC

    [<Test>]
    let ``members-ctree-FSI_BASIC`` () = singleTestBuildAndRun "core/members/ctree" FSI_BASIC

    [<Test>]
    let ``members-factors-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/members/factors" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``members-factors-FSC_BASIC`` () = singleTestBuildAndRun "core/members/factors" FSC_BASIC

    [<Test>]
    let ``members-factors-FSI_BASIC`` () = singleTestBuildAndRun "core/members/factors" FSI_BASIC

    [<Test>]
    let ``members-factors-mutrec-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/members/factors-mutrec" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``members-factors-mutrec-FSC_BASIC`` () = singleTestBuildAndRun "core/members/factors-mutrec" FSC_BASIC

    [<Test>]
    let ``members-factors-mutrec-FSI_BASIC`` () = singleTestBuildAndRun "core/members/factors-mutrec" FSI_BASIC

    [<Test>]
    let ``graph-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "perf/graph" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``graph-FSC_BASIC`` () = singleTestBuildAndRun "perf/graph" FSC_BASIC

    [<Test>]
    let ``graph-FSI_BASIC`` () = singleTestBuildAndRun "perf/graph" FSI_BASIC

    [<Test>]
    let ``nbody-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "perf/nbody" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``nbody-FSC_BASIC`` () = singleTestBuildAndRun "perf/nbody" FSC_BASIC

    [<Test>]
    let ``nbody-FSI_BASIC`` () = singleTestBuildAndRun "perf/nbody" FSI_BASIC

    [<Test>]
    let ``letrec (mutrec variations part two) FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/letrec-mutrec2" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``letrec (mutrec variations part two) FSC_BASIC`` () = singleTestBuildAndRun "core/letrec-mutrec2" FSC_BASIC

    [<Test>]
    let ``letrec (mutrec variations part two) FSI_BASIC`` () = singleTestBuildAndRun "core/letrec-mutrec2" FSI_BASIC

    [<Test>]
    let ``printf`` () = singleTestBuildAndRunVersion "core/printf" FSC_BASIC "preview"

    [<Test>]
    let ``printf-interpolated`` () = singleTestBuildAndRunVersion "core/printf-interpolated" FSC_BASIC "preview"

    [<Test>]
    let ``tlr-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/tlr" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``tlr-FSC_BASIC`` () = singleTestBuildAndRun "core/tlr" FSC_BASIC

    [<Test>]
    let ``tlr-FSI_BASIC`` () = singleTestBuildAndRun "core/tlr" FSI_BASIC

    [<Test>]
    let ``subtype-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/subtype" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``subtype-FSC_BASIC`` () = singleTestBuildAndRun "core/subtype" FSC_BASIC

    [<Test>]
    let ``subtype-FSI_BASIC`` () = singleTestBuildAndRun "core/subtype" FSI_BASIC

    [<Test>]
    let ``syntax-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/syntax" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``syntax-FSC_BASIC`` () = singleTestBuildAndRun "core/syntax" FSC_BASIC

    [<Test>]
    let ``syntax-FSI_BASIC`` () = singleTestBuildAndRun "core/syntax" FSI_BASIC

    [<Test>]
    let ``test int32-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/int32" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``test int32-FSC_BASIC`` () = singleTestBuildAndRun "core/int32" FSC_BASIC

    [<Test>]
    let ``test int32-FSI_BASIC`` () = singleTestBuildAndRun "core/int32" FSI_BASIC

    [<Test>]
    let ``quotes-FSC-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/quotes" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``quotes-FSC-BASIC`` () = singleTestBuildAndRun "core/quotes" FSC_BASIC

    [<Test>]
    let ``quotes-FSI-BASIC`` () = singleTestBuildAndRun "core/quotes" FSI_BASIC

    [<Test>]
    let ``recordResolution-FSC_BASIC_OPT_MINUS`` () = singleTestBuildAndRun "core/recordResolution" FSC_BASIC_OPT_MINUS

    [<Test>]
    let ``recordResolution-FSC_BASIC`` () = singleTestBuildAndRun "core/recordResolution" FSC_BASIC

    [<Test>]
    let ``recordResolution-FSI_BASIC`` () = singleTestBuildAndRun "core/recordResolution" FSI_BASIC

    [<Test>]
    let ``SDKTests`` () =
        let cfg = testConfig "SDKTests"
        exec cfg cfg.DotNetExe ("msbuild " + Path.Combine(cfg.Directory, "AllSdkTargetsTests.proj") + " /p:Configuration=" + cfg.BUILD_CONFIG)

#if !NETCOREAPP
    [<Test>]
    let ``attributes-FSC_BASIC`` () = singleTestBuildAndRun "core/attributes" FSC_BASIC

    [<Test>]
    let ``attributes-FSI_BASIC`` () = singleTestBuildAndRun "core/attributes" FSI_BASIC

    [<Test>]
    let byrefs () =

        let cfg = testConfig "core/byrefs"

        begin
            use testOkFile = fileguard cfg "test.ok"

            fsc cfg "%s -o:test.exe -g --langversion:4.7" cfg.fsc_flags ["test.fsx"]

            singleVersionedNegTest cfg "4.7" "test"
            exec cfg ("." ++ "test.exe") ""

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"

            fsc cfg "%s -o:test.exe -g --langversion:5.0" cfg.fsc_flags ["test.fsx"]

            singleVersionedNegTest cfg "5.0" "test"

            exec cfg ("." ++ "test.exe") ""

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test2.ok"

            fsc cfg "%s -o:test2.exe -g" cfg.fsc_flags ["test2.fsx"]

            singleNegTest { cfg with fsc_flags = sprintf "%s --warnaserror-" cfg.fsc_flags } "test2"

            exec cfg ("." ++ "test2.exe") ""

            testOkFile.CheckExists()
        end

        begin
            csc cfg """/langversion:7.2 /nologo /target:library /out:cslib3.dll""" ["cslib3.cs"]

            use testOkFile = fileguard cfg "test3.ok"

            fsc cfg "%s -r:cslib3.dll -o:test3.exe -g" cfg.fsc_flags ["test3.fsx"]

            singleNegTest { cfg with fsc_flags = sprintf "%s -r:cslib3.dll" cfg.fsc_flags } "test3"

            exec cfg ("." ++ "test3.exe") ""

            testOkFile.CheckExists()
        end

    [<Test>]
    let span () =

        let cfg = testConfig "core/span"

        let cfg = { cfg with fsc_flags = sprintf "%s --test:StackSpan" cfg.fsc_flags}

        begin
            use testOkFile = fileguard cfg "test.ok"

            singleNegTest cfg "test"

            fsc cfg "%s -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

            // Execution is disabled until we can be sure .NET 4.7.2 is on the machine
            //exec cfg ("." ++ "test.exe") ""

            //testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test2.ok"

            singleNegTest cfg "test2"

            fsc cfg "%s -o:test2.exe -g" cfg.fsc_flags ["test2.fsx"]

            // Execution is disabled until we can be sure .NET 4.7.2 is on the machine
            //exec cfg ("." ++ "test.exe") ""

            //testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test3.ok"

            singleNegTest cfg "test3"

            fsc cfg "%s -o:test3.exe -g" cfg.fsc_flags ["test3.fsx"]

            // Execution is disabled until we can be sure .NET 4.7.2 is on the machine
            //exec cfg ("." ++ "test.exe") ""

            //testOkFile.CheckExists()
        end

    [<Test>]
    let asyncStackTraces () =
        let cfg = testConfig "core/asyncStackTraces"

        use testOkFile = fileguard cfg "test.ok"

        fsc cfg "%s -o:test.exe -g --tailcalls- --optimize-" cfg.fsc_flags ["test.fsx"]

        exec cfg ("." ++ "test.exe") ""

        testOkFile.CheckExists()

    [<Test>]
    let ``state-machines-non-optimized`` () = 
        let cfg = testConfig "core/state-machines"

        use testOkFile = fileguard cfg "test.ok"

        fsc cfg "%s -o:test.exe -g --tailcalls- --optimize- --langversion:preview" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        testOkFile.CheckExists()

    [<Test>]
    let ``state-machines-optimized`` () = 
        let cfg = testConfig "core/state-machines"

        use testOkFile = fileguard cfg "test.ok"

        fsc cfg "%s -o:test.exe -g --tailcalls+ --optimize+ --langversion:preview" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        testOkFile.CheckExists()

    [<Test>]
    let ``state-machines neg-resumable-01`` () =
        let cfg = testConfig "core/state-machines"
        singleVersionedNegTest cfg "preview" "neg-resumable-01"


    [<Test>]
    let ``state-machines neg-resumable-02`` () =
        let cfg = testConfig "core/state-machines"
        singleVersionedNegTest cfg "preview" "neg-resumable-02"

    [<Test>]
    let ``lots-of-conditionals``() =
        let cfg = testConfig "core/large/conditionals"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeConditionals-200.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-conditionals-maxtested``() =
        let cfg = testConfig "core/large/conditionals"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeConditionals-maxtested.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-lets``() =
        let cfg = testConfig "core/large/lets"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeLets-500.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-lets-maxtested``() =
        let cfg = testConfig "core/large/lets"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeLets-maxtested.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-lists``() =
        let cfg = testConfig "core/large/lists"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test-500.exe " cfg.fsc_flags ["LargeList-500.fs"]
        exec cfg ("." ++ "test-500.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-matches``() =
        let cfg = testConfig "core/large/matches"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeMatches-200.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-matches-maxtested``() =
        let cfg = testConfig "core/large/matches"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeMatches-maxtested.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-sequential-and-let``() =
        let cfg = testConfig "core/large/mixed"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeSequentialLet-500.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-sequential-and-let-maxtested``() =
        let cfg = testConfig "core/large/mixed"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeSequentialLet-maxtested.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-sequential``() =
        let cfg = testConfig "core/large/sequential"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeSequential-500.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

    [<Test>]
    let ``lots-of-sequential-maxtested``() =
        let cfg = testConfig "core/large/sequential"
        use testOkFile = fileguard cfg "test.ok"
        fsc cfg "%s -o:test.exe " cfg.fsc_flags ["LargeSequential-maxtested.fs"]
        exec cfg ("." ++ "test.exe") ""
        testOkFile.CheckExists()

#endif

    [<Test>]
    let ``control-FSC_BASIC`` () = singleTestBuildAndRun "core/control" FSC_BASIC

    [<Test>]
    let ``control-FSI_BASIC`` () = singleTestBuildAndRun "core/control" FSI_BASIC

    [<Test>]
    let ``control --tailcalls`` () =
        let cfg = testConfig "core/control"
        singleTestBuildAndRunAux {cfg with fsi_flags = " --tailcalls" } FSC_BASIC

    [<Test>]
    let ``controlChamenos-FSC_BASIC`` () =
        let cfg = testConfig "core/controlChamenos"
        singleTestBuildAndRunAux {cfg with fsi_flags = " --tailcalls" } FSC_BASIC

    [<Test>]
    let ``controlChamenos-FSI_BASIC`` () =
        let cfg = testConfig "core/controlChamenos"
        singleTestBuildAndRunAux {cfg with fsi_flags = " --tailcalls" } FSI_BASIC

    [<Test>]
    let ``controlMailbox-FSC_BASIC`` () = singleTestBuildAndRun "core/controlMailbox" FSC_BASIC

    [<Test>]
    let ``controlMailbox-FSI_BASIC`` () = singleTestBuildAndRun "core/controlMailbox" FSI_BASIC

    [<Test>]
    let ``controlMailbox --tailcalls`` () =
        let cfg = testConfig "core/controlMailbox"
        singleTestBuildAndRunAux {cfg with fsi_flags = " --tailcalls" } FSC_BASIC

    [<Test>]
    let ``csext-FSC_BASIC`` () = singleTestBuildAndRun "core/csext" FSC_BASIC

    [<Test>]
    let ``csext-FSI_BASIC`` () = singleTestBuildAndRun "core/csext" FSI_BASIC

    [<Test>]
    let ``enum-FSC_BASIC`` () = singleTestBuildAndRun "core/enum" FSC_BASIC

    [<Test>]
    let ``enum-FSI_BASIC`` () = singleTestBuildAndRun "core/enum" FSI_BASIC

#if !NETCOREAPP

    // Requires winforms will not run on coreclr
    [<Test>]
    let controlWpf () = singleTestBuildAndRun "core/controlwpf" FSC_BASIC

    // These tests are enabled for .NET Framework
    [<Test>]
    let ``anon-FSC_BASIC``() =
        let cfg = testConfig "core/anon"

        fsc cfg "%s -a -o:lib.dll" cfg.fsc_flags ["lib.fs"]

        peverify cfg "lib.dll"

        fsc cfg "%s -r:lib.dll" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        begin
            use testOkFile = fileguard cfg "test.ok"

            exec cfg ("." ++ "test.exe") ""

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"

            fsi cfg "-r:lib.dll" ["test.fsx"]

            testOkFile.CheckExists()
        end

    [<Test>]
    let events () =
        let cfg = testConfig "core/events"

        fsc cfg "%s -a -o:test.dll -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test.dll"

        csc cfg """/r:"%s" /reference:test.dll /debug+""" cfg.FSCOREDLLPATH ["testcs.cs"]

        peverify cfg "testcs.exe"

        use testOkFile = fileguard cfg "test.ok"

        fsi cfg "" ["test.fs"]

        testOkFile.CheckExists()

        exec cfg ("." ++ "testcs.exe") ""


    //
    // Shadowcopy does not work for public signed assemblies
    // =====================================================
    //
    //module ``FSI-Shadowcopy`` =
    //
    //    [<Test>]
    //    // "%FSI%" %fsi_flags%                          < test1.fsx
    //    [<FSharpSuiteTestCase("core/fsi-shadowcopy", "")
    //    // "%FSI%" %fsi_flags%  --shadowcopyreferences- < test1.fsx
    //    [<FSharpSuiteTestCase("core/fsi-shadowcopy", "--shadowcopyreferences-")
    //    let ``shadowcopy disabled`` (flags: string) =
    //        let cfg = testConfig' ()
    //
    //
    //
    //
    //
    //        // if exist test1.ok (del /f /q test1.ok)
    //        use testOkFile = fileguard cfg "test1.ok"
    //
    //        fsiStdin cfg "%s %s" cfg.fsi_flags flags "test1.fsx"
    //
    //        // if NOT EXIST test1.ok goto SetError
    //        testOkFile.CheckExists()
    //
    //
    //    [<Test>]
    //    // "%FSI%" %fsi_flags%  /shadowcopyreferences+  < test2.fsx
    //    [<FSharpSuiteTestCase("core/fsi-shadowcopy", "/shadowcopyreferences+")
    //    // "%FSI%" %fsi_flags%  --shadowcopyreferences  < test2.fsx
    //    [<FSharpSuiteTestCase("core/fsi-shadowcopy", "--shadowcopyreferences")
    //    let ``shadowcopy enabled`` (flags: string) =
    //        let cfg = testConfig' ()
    //
    //
    //
    //
    //
    //        // if exist test2.ok (del /f /q test2.ok)
    //        use testOkFile = fileguard cfg "test2.ok"
    //
    //        // "%FSI%" %fsi_flags%  /shadowcopyreferences+  < test2.fsx
    //        fsiStdin cfg "%s %s" cfg.fsi_flags flags "test2.fsx"
    //
    //        // if NOT EXIST test2.ok goto SetError
    //        testOkFile.CheckExists()
    //


    [<Test>]
    let forwarders () =
        let cfg = testConfig "core/forwarders"

        mkdir cfg "orig"
        mkdir cfg "split"

        csc cfg """/nologo  /target:library /out:orig\a.dll /define:PART1;PART2""" ["a.cs"]

        csc cfg """/nologo  /target:library /out:orig\b.dll /r:orig\a.dll""" ["b.cs"]

        fsc cfg """-a -o:orig\c.dll -r:orig\b.dll -r:orig\a.dll""" ["c.fs"]

        csc cfg """/nologo  /target:library /out:split\a-part1.dll /define:PART1;SPLIT""" ["a.cs"]

        csc cfg """/nologo  /target:library /r:split\a-part1.dll /out:split\a.dll /define:PART2;SPLIT""" ["a.cs"]

        copy_y cfg ("orig" ++ "b.dll") ("split" ++ "b.dll")

        copy_y cfg ("orig" ++ "c.dll") ("split" ++ "c.dll")

        fsc cfg """-o:orig\test.exe -r:orig\b.dll -r:orig\a.dll""" ["test.fs"]

        fsc cfg """-o:split\test.exe -r:split\b.dll -r:split\a-part1.dll -r:split\a.dll""" ["test.fs"]

        fsc cfg """-o:split\test-against-c.exe -r:split\c.dll -r:split\a-part1.dll -r:split\a.dll""" ["test.fs"]

        peverify cfg ("split" ++ "a-part1.dll")

        peverify cfg ("split" ++ "b.dll")

        peverify cfg ("split" ++ "c.dll")

    [<Test>]
    let fsfromcs () =
        let cfg = testConfig "core/fsfromcs"

        fsc cfg "%s -a --doc:lib.xml -o:lib.dll -g" cfg.fsc_flags ["lib.fs"]

        peverify cfg "lib.dll"

        csc cfg """/nologo /r:"%s" /r:System.Core.dll /r:lib.dll /out:test.exe""" cfg.FSCOREDLLPATH ["test.cs"]

        fsc cfg """%s -a --doc:lib--optimize.xml -o:lib--optimize.dll -g""" cfg.fsc_flags ["lib.fs"]

        peverify cfg "lib--optimize.dll"

        csc cfg """/nologo /r:"%s"  /r:System.Core.dll /r:lib--optimize.dll    /out:test--optimize.exe""" cfg.FSCOREDLLPATH ["test.cs"]

        exec cfg ("." ++ "test.exe") ""

        exec cfg ("." ++ "test--optimize.exe") ""

    [<Test>]
    let fsfromfsviacs () =
        let cfg = testConfig "core/fsfromfsviacs"

        fsc cfg "%s -a -o:lib.dll -g" cfg.fsc_flags ["lib.fs"]

        peverify cfg "lib.dll"

        csc cfg """/nologo /target:library /r:"%s" /r:lib.dll /out:lib2.dll /langversion:7.2""" cfg.FSCOREDLLPATH ["lib2.cs"]

        csc cfg """/nologo /target:library /r:"%s" /out:lib3.dll  /langversion:7.2""" cfg.FSCOREDLLPATH ["lib3.cs"]

        // some features missing in 4.7
        for version in ["4.7"] do
            let outFile = "compilation.langversion.old.output.txt"
            let expectedFile = "compilation.langversion.old.output.bsl"
            fscBothToOutExpectFail cfg outFile "%s -r:lib.dll -r:lib2.dll -r:lib3.dll -o:test.exe -g --nologo --define:LANGVERSION_%s --langversion:%s" cfg.fsc_flags (version.Replace(".","_")) version ["test.fsx"]

            let diffs = fsdiff cfg outFile expectedFile
            match diffs with
            | "" -> ()
            | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" outFile expectedFile diffs)

        // all features available in preview
        fsc cfg "%s -r:lib.dll -r:lib2.dll -r:lib3.dll -o:test.exe -g --define:LANGVERSION_PREVIEW --langversion:preview" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        // Same with library references the other way around
        fsc cfg "%s -r:lib.dll -r:lib3.dll -r:lib2.dll -o:test.exe -g --define:LANGVERSION_PREVIEW --langversion:preview" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        // Same without the reference to lib.dll - testing an incomplete reference set, but only compiling a subset of the code
        fsc cfg "%s --define:NO_LIB_REFERENCE -r:lib3.dll -r:lib2.dll -o:test.exe -g --define:LANGVERSION_PREVIEW --langversion:preview" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        // check error messages for some cases
        let outFile = "compilation.errors.output.txt"
        let expectedFile = "compilation.errors.output.bsl"
        fscBothToOutExpectFail cfg outFile "%s -r:lib.dll -r:lib2.dll -r:lib3.dll -o:test.exe -g --nologo --define:LANGVERSION_PREVIEW --langversion:preview --define:CHECK_ERRORS" cfg.fsc_flags ["test.fsx"]

        let diffs = fsdiff cfg outFile expectedFile
        match diffs with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" outFile expectedFile diffs)

    [<Test>]
    let ``fsi-reference`` () =

        let cfg = testConfig "core/fsi-reference"

        begin
            use testOkFile = fileguard cfg "test.ok"
            fsc cfg @"--target:library -o:ImplementationAssembly\ReferenceAssemblyExample.dll" ["ImplementationAssembly.fs"]
            fsc cfg @"--target:library -o:ReferenceAssembly\ReferenceAssemblyExample.dll" ["ReferenceAssembly.fs"]
            fsiStdin cfg "test.fsx" "" []
            testOkFile.CheckExists()
        end

    [<Test>]
    let ``fsi-reload`` () =
        let cfg = testConfig "core/fsi-reload"

        begin
            use testOkFile = fileguard cfg "test.ok"
            fsiStdin cfg "test1.ml"  "--maxerrors:1" []
            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"
            fsi cfg "%s  --maxerrors:1" cfg.fsi_flags ["load1.fsx"]
            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"
            fsi cfg "%s  --maxerrors:1" cfg.fsi_flags ["load2.fsx"]
            testOkFile.CheckExists()
        end

        fsc cfg "" ["load1.fsx"]
        fsc cfg "" ["load2.fsx"]


    [<Test>]
    let fsiAndModifiers () =
        let cfg = testConfig "core/fsiAndModifiers"

        do if fileExists cfg "TestLibrary.dll" then rm cfg "TestLibrary.dll"

        fsiStdin cfg "prepare.fsx" "--maxerrors:1" []

        use testOkFile = fileguard cfg "test.ok"

        fsiStdin cfg "test.fsx" "--maxerrors:1"  []

        testOkFile.CheckExists()

    [<Test>]
    let ``genericmeasures-AS_DLL`` () = singleTestBuildAndRun "core/genericmeasures" AS_DLL


    [<Test>]
    let hiding () =
        let cfg = testConfig "core/hiding"

        fsc cfg "%s -a --optimize -o:lib.dll" cfg.fsc_flags ["lib.mli";"lib.ml";"libv.ml"]

        peverify cfg "lib.dll"

        fsc cfg "%s -a --optimize -r:lib.dll -o:lib2.dll" cfg.fsc_flags ["lib2.mli";"lib2.ml";"lib3.ml"]

        peverify cfg "lib2.dll"

        fsc cfg "%s --optimize -r:lib.dll -r:lib2.dll -o:client.exe" cfg.fsc_flags ["client.ml"]

        peverify cfg "client.exe"

    [<Test>]
    let ``innerpoly-AS_DLL`` () = singleTestBuildAndRun "core/innerpoly"  AS_DLL

    [<Test>]
    let queriesCustomQueryOps () =
        let cfg = testConfig "core/queriesCustomQueryOps"

        fsc cfg """%s -o:test.exe -g""" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg """%s --optimize -o:test--optimize.exe -g""" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        singleNegTest cfg "negativetest"

        begin
            use testOkFile = fileguard cfg "test.ok"

            fsi cfg "%s" cfg.fsi_flags ["test.fsx"]

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"

            exec cfg ("." ++ "test.exe") ""

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"

            exec cfg ("." ++ "test--optimize.exe") ""

            testOkFile.CheckExists()
        end

    // Debug with
    //     ..\..\..\..\debug\net40\bin\fsi.exe --nologo < test.fsx >a.out 2>a.err
    // then
    ///    windiff z.output.test.default.stdout.bsl a.out
    let printing flag diffFileOut expectedFileOut diffFileErr expectedFileErr =
       let cfg = testConfig "core/printing"

       if requireENCulture () then

        let copy from' = Commands.copy_y cfg.Directory from' >> checkResult

        let ``fsi <a >b 2>c`` =
            // "%FSI%" %fsc_flags_errors_ok%  --nologo                                    <test.fsx >z.raw.output.test.default.txt 2>&1
            let ``exec <a >b 2>c`` (inFile, outFile, errFile) p =
                Command.exec cfg.Directory cfg.EnvironmentVariables { Output = OutputAndError(Overwrite(outFile), Overwrite(errFile)); Input = Some(RedirectInput(inFile)); } p
                >> checkResult
            Printf.ksprintf (fun flags (inFile, outFile, errFile) -> Commands.fsi (``exec <a >b 2>c`` (inFile, outFile, errFile)) cfg.FSI flags [])

        let fsc_flags_errors_ok = ""

        let rawFileOut = tryCreateTemporaryFileName ()
        let rawFileErr = tryCreateTemporaryFileName ()
        ``fsi <a >b 2>c`` "%s --nologo %s" fsc_flags_errors_ok flag ("test.fsx", rawFileOut, rawFileErr)

        // REM REVIEW: want to normalise CWD paths, not suppress them.
        let ``findstr /v`` text = Seq.filter (fun (s: string) -> not <| s.Contains(text))
        let removeCDandHelp from' to' =
            File.ReadLines from' |> (``findstr /v`` cfg.Directory) |> (``findstr /v`` "--help' for options") |> (fun lines -> File.WriteAllLines(getfullpath cfg to', lines))

        removeCDandHelp rawFileOut diffFileOut
        removeCDandHelp rawFileErr diffFileErr

        let withDefault default' to' =
            if not (fileExists cfg to') then copy default' to'

        expectedFileOut |> withDefault diffFileOut
        expectedFileErr |> withDefault diffFileErr


        match fsdiff cfg diffFileOut expectedFileOut with
        | "" -> ()
        | diffs -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" diffFileOut expectedFileOut diffs)

        match fsdiff cfg diffFileErr expectedFileErr with
        | "" -> ()
        | diffs -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" diffFileErr expectedFileErr diffs)

    [<Test>]
    let ``printing-1 --langversion:4.7`` () =
         printing "--langversion:4.7" "z.output.test.default.stdout.47.txt" "z.output.test.default.stdout.47.bsl" "z.output.test.default.stderr.txt" "z.output.test.default.stderr.bsl"

    [<Test>]
    let ``printing-1 --langversion:5.0`` () =
         printing "--langversion:5.0" "z.output.test.default.stdout.50.txt" "z.output.test.default.stdout.50.bsl" "z.output.test.default.stderr.txt" "z.output.test.default.stderr.bsl"

    [<Test>]
    let ``printing-2 --langversion:4.7`` () =
         printing "--langversion:4.7 --use:preludePrintSize1000.fsx" "z.output.test.1000.stdout.47.txt" "z.output.test.1000.stdout.47.bsl" "z.output.test.1000.stderr.txt" "z.output.test.1000.stderr.bsl"

    [<Test>]
    let ``printing-2 --langversion:5.0`` () =
         printing "--langversion:5.0 --use:preludePrintSize1000.fsx" "z.output.test.1000.stdout.50.txt" "z.output.test.1000.stdout.50.bsl" "z.output.test.1000.stderr.txt" "z.output.test.1000.stderr.bsl"

    [<Test>]
    let ``printing-3  --langversion:4.7`` () =
         printing "--langversion:4.7 --use:preludePrintSize200.fsx" "z.output.test.200.stdout.47.txt" "z.output.test.200.stdout.47.bsl" "z.output.test.200.stderr.txt" "z.output.test.200.stderr.bsl"

    [<Test>]
    let ``printing-3  --langversion:5.0`` () =
         printing "--langversion:5.0 --use:preludePrintSize200.fsx" "z.output.test.200.stdout.50.txt" "z.output.test.200.stdout.50.bsl" "z.output.test.200.stderr.txt" "z.output.test.200.stderr.bsl"

    [<Test>]
    let ``printing-4  --langversion:4.7`` () =
         printing "--langversion:4.7 --use:preludeShowDeclarationValuesFalse.fsx" "z.output.test.off.stdout.47.txt" "z.output.test.off.stdout.47.bsl" "z.output.test.off.stderr.txt" "z.output.test.off.stderr.bsl"

    [<Test>]
    let ``printing-4  --langversion:5.0`` () =
         printing "--langversion:5.0 --use:preludeShowDeclarationValuesFalse.fsx" "z.output.test.off.stdout.50.txt" "z.output.test.off.stdout.50.bsl" "z.output.test.off.stderr.txt" "z.output.test.off.stderr.bsl"

    [<Test>]
    let ``printing-5`` () =
         printing "--quiet" "z.output.test.quiet.stdout.txt" "z.output.test.quiet.stdout.bsl" "z.output.test.quiet.stderr.txt" "z.output.test.quiet.stderr.bsl"

    type SigningType =
        | DelaySigned
        | PublicSigned
        | NotSigned

    let signedtest(programId:string, args:string, expectedSigning:SigningType) =

        let cfg = testConfig "core/signedtests"
        let newFlags = cfg.fsc_flags + " " + args

        let exefile = programId + ".exe"
        fsc cfg "%s -o:%s" newFlags exefile ["test.fs"]

        let assemblyPath = Path.Combine(cfg.Directory, exefile)
        let assemblyName = AssemblyName.GetAssemblyName(assemblyPath)
        let publicKeyToken = assemblyName.GetPublicKeyToken()
        let isPublicKeyTokenPresent = not (Array.isEmpty publicKeyToken)
        use exeStream = new FileStream(assemblyPath, FileMode.Open)
        let peHeader = PEHeaders(exeStream)
        let isSigned = peHeader.CorHeader.Flags.HasFlag(CorFlags.StrongNameSigned)
        let actualSigning =
            match isSigned, isPublicKeyTokenPresent with
            | true, true-> SigningType.PublicSigned
            | true, false -> failwith "unreachable"
            | false, true -> SigningType.DelaySigned
            | false, false -> SigningType.NotSigned

        Assert.AreEqual(expectedSigning, actualSigning)

    [<Test; Category("signedtest")>]
    let ``signedtest-1`` () = signedtest("test-unsigned", "", SigningType.NotSigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-2`` () = signedtest("test-sha1-full-cl", "--keyfile:sha1full.snk", SigningType.PublicSigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-3`` () = signedtest("test-sha256-full-cl", "--keyfile:sha256full.snk", SigningType.PublicSigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-4`` () = signedtest("test-sha512-full-cl", "--keyfile:sha512full.snk", SigningType.PublicSigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-5`` () = signedtest("test-sha1024-full-cl", "--keyfile:sha1024full.snk", SigningType.PublicSigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-6`` () = signedtest("test-sha1-delay-cl", "--keyfile:sha1delay.snk --delaysign", SigningType.DelaySigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-7`` () = signedtest("test-sha256-delay-cl", "--keyfile:sha256delay.snk --delaysign", SigningType.DelaySigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-8`` () = signedtest("test-sha512-delay-cl", "--keyfile:sha512delay.snk --delaysign", SigningType.DelaySigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-9`` () = signedtest("test-sha1024-delay-cl", "--keyfile:sha1024delay.snk --delaysign", SigningType.DelaySigned)

    // Test SHA1 key full signed  Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-10`` () = signedtest("test-sha1-full-attributes", "--define:SHA1", SigningType.PublicSigned)

    // Test SHA1 key delayl signed  Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-11`` () = signedtest("test-sha1-delay-attributes", "--keyfile:sha1delay.snk --define:SHA1 --define:DELAY", SigningType.DelaySigned)

    [<Test; Category("signedtest")>]
    let ``signedtest-12`` () = signedtest("test-sha256-full-attributes", "--define:SHA256", SigningType.PublicSigned)

    // Test SHA 256 bit key delay signed  Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-13`` () = signedtest("test-sha256-delay-attributes", "--define:SHA256 --define:DELAY", SigningType.DelaySigned)

    // Test SHA 512 bit key fully signed  Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-14`` () = signedtest("test-sha512-full-attributes", "--define:SHA512", SigningType.PublicSigned)

    // Test SHA 512 bit key delay signed Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-15`` () = signedtest("test-sha512-delay-attributes", "--define:SHA512 --define:DELAY", SigningType.DelaySigned)

    // Test SHA 1024 bit key fully signed  Attributes
    [<Test; Category("signedtest")>]
    let ``signedtest-16`` () = signedtest("test-sha1024-full-attributes", "--define:SHA1024", SigningType.PublicSigned)
#endif

#if !NETCOREAPP
    [<Test>]
    let quotes () =
        let cfg = testConfig "core/quotes"


        csc cfg """/nologo  /target:library /out:cslib.dll""" ["cslib.cs"]

        fsc cfg "%s --define:LANGVERSION_PREVIEW --langversion:preview -o:test.exe -r cslib.dll -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        begin
            use testOkFile = fileguard cfg "test.ok"
            exec cfg ("." ++ "test.exe") ""
            testOkFile.CheckExists()
        end

        fsc cfg "%s -o:test-with-debug-data.exe --quotations-debug+ -r cslib.dll -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test-with-debug-data.exe"

        fsc cfg "%s --optimize -o:test--optimize.exe -r cslib.dll -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        begin
            use testOkFile = fileguard cfg "test.ok"

            fsi cfg "%s -r cslib.dll" cfg.fsi_flags ["test.fsx"]

            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"
            exec cfg ("." ++ "test-with-debug-data.exe") ""
            testOkFile.CheckExists()
        end

        begin
            use testOkFile = fileguard cfg "test.ok"
            exec cfg ("." ++ "test--optimize.exe") ""
            testOkFile.CheckExists()
        end

    [<Test; Category("parsing")>]
    let parsing () =
        let cfg = testConfig "core/parsing"

        fsc cfg "%s -a -o:crlf.dll -g" cfg.fsc_flags ["crlf.ml"]

        fsc cfg "%s -o:toplet.exe -g" cfg.fsc_flags ["toplet.ml"]

        peverify cfg "toplet.exe"

    [<Test>]
    let unicode () =
        let cfg = testConfig "core/unicode"

        fsc cfg "%s -a -o:kanji-unicode-utf8-nosig-codepage-65001.dll -g" cfg.fsc_flags ["kanji-unicode-utf8-nosig-codepage-65001.fs"]

        fsc cfg "%s -a -o:kanji-unicode-utf8-nosig-codepage-65001.dll -g" cfg.fsc_flags ["kanji-unicode-utf8-nosig-codepage-65001.fs"]

        fsc cfg "%s -a -o:kanji-unicode-utf16.dll -g" cfg.fsc_flags ["kanji-unicode-utf16.fs"]

        fsc cfg "%s -a --codepage:65000 -o:kanji-unicode-utf7-codepage-65000.dll -g" cfg.fsc_flags ["kanji-unicode-utf7-codepage-65000.fs"]

        fsc cfg "%s -a -o:kanji-unicode-utf8-withsig-codepage-65001.dll -g" cfg.fsc_flags ["kanji-unicode-utf8-withsig-codepage-65001.fs"]

        fsi cfg "%s --utf8output" cfg.fsi_flags ["kanji-unicode-utf8-nosig-codepage-65001.fs"]

        fsi cfg "%s --utf8output --codepage:65001" cfg.fsi_flags ["kanji-unicode-utf8-withsig-codepage-65001.fs"]

        fsi cfg "%s --utf8output" cfg.fsi_flags ["kanji-unicode-utf8-withsig-codepage-65001.fs"]

        fsi cfg "%s --utf8output --codepage:65000" cfg.fsi_flags ["kanji-unicode-utf7-codepage-65000.fs"]

        fsi cfg "%s --utf8output" cfg.fsi_flags ["kanji-unicode-utf16.fs"]


    [<Test>]
    let internalsvisible () =
        let cfg = testConfig "core/internalsvisible"

        // Compiling F# Library
        fsc cfg "%s --version:1.2.3 --keyfile:key.snk -a --optimize -o:library.dll" cfg.fsc_flags ["library.fsi"; "library.fs"]

        peverify cfg "library.dll"

        // Compiling C# Library
        csc cfg "/target:library /keyfile:key.snk /out:librarycs.dll" ["librarycs.cs"]

        peverify cfg "librarycs.dll"

        // Compiling F# main referencing C# and F# libraries
        fsc cfg "%s --version:1.2.3 --keyfile:key.snk --optimize -r:library.dll -r:librarycs.dll -o:main.exe" cfg.fsc_flags ["main.fs"]

        peverify cfg "main.exe"

        // Run F# main. Quick test!
        exec cfg ("." ++ "main.exe") ""


    // Repro for https://github.com/Microsoft/visualfsharp/issues/1298
    [<Test>]
    let fileorder () =
        let cfg = testConfig "core/fileorder"

        log "== Compiling F# Library and Code, when empty file libfile2.fs IS NOT included"
        fsc cfg "%s -a --optimize -o:lib.dll " cfg.fsc_flags ["libfile1.fs"]

        peverify cfg "lib.dll"

        fsc cfg "%s -r:lib.dll -o:test.exe" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

        log "== Compiling F# Library and Code, when empty file libfile2.fs IS included"
        fsc cfg "%s -a --optimize -o:lib2.dll " cfg.fsc_flags ["libfile1.fs"; "libfile2.fs"]

        peverify cfg "lib2.dll"

        fsc cfg "%s -r:lib2.dll -o:test2.exe" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test2.exe"

        exec cfg ("." ++ "test2.exe") ""

    // Repro for https://github.com/Microsoft/visualfsharp/issues/2679
    [<Test>]
    let ``add files with same name from different folders`` () =
        let cfg = testConfig "core/samename"

        log "== Compiling F# Code with files with same name in different folders"
        fsc cfg "%s -o:test.exe" cfg.fsc_flags ["folder1/a.fs"; "folder1/b.fs"; "folder2/a.fs"; "folder2/b.fs"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

    [<Test>]
    let ``add files with same name from different folders including signature files`` () =
        let cfg = testConfig "core/samename"

        log "== Compiling F# Code with files with same name in different folders including signature files"
        fsc cfg "%s -o:test.exe" cfg.fsc_flags ["folder1/a.fsi"; "folder1/a.fs"; "folder1/b.fsi"; "folder1/b.fs"; "folder2/a.fsi"; "folder2/a.fs"; "folder2/b.fsi"; "folder2/b.fs"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

    [<Test>]
    let ``add files with same name from different folders including signature files that are not synced`` () =
        let cfg = testConfig "core/samename"

        log "== Compiling F# Code with files with same name in different folders including signature files"
        fsc cfg "%s -o:test.exe" cfg.fsc_flags ["folder1/a.fsi"; "folder1/a.fs"; "folder1/b.fs"; "folder2/a.fsi"; "folder2/a.fs"; "folder2/b.fsi"; "folder2/b.fs"]

        peverify cfg "test.exe"

        exec cfg ("." ++ "test.exe") ""

    [<Test>]
    let ``libtest-FSI_STDIN`` () = singleTestBuildAndRun "core/libtest" FSI_STDIN

    [<Test; Ignore("incorrect signature file generated, test has been disabled a long time")>]
    let ``libtest-GENERATED_SIGNATURE`` () = singleTestBuildAndRun "core/libtest" GENERATED_SIGNATURE

    [<Test>]
    let ``libtest-FSC_OPT_MINUS_DEBUG`` () = singleTestBuildAndRun "core/libtest" FSC_OPT_MINUS_DEBUG

    [<Test>]
    let ``libtest-AS_DLL`` () = singleTestBuildAndRun "core/libtest" AS_DLL

    [<Test>]
    let ``no-warn-2003-tests`` () =
        // see https://github.com/Microsoft/visualfsharp/issues/3139
        let cfg = testConfig "core/versionAttributes"
        let stdoutPath = "out.stdout.txt" |> getfullpath cfg
        let stderrPath = "out.stderr.txt" |> getfullpath cfg
        let stderrBaseline = "out.stderr.bsl" |> getfullpath cfg
        let stdoutBaseline = "out.stdout.bsl" |> getfullpath cfg
        let echo text =
            Commands.echoAppendToFile cfg.Directory text stdoutPath
            Commands.echoAppendToFile cfg.Directory text stderrPath

        File.WriteAllText(stdoutPath, "")
        File.WriteAllText(stderrPath, "")

        echo "Test 1================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo" ["NoWarn2003.fs"]

        echo "Test 2================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo" ["NoWarn2003_2.fs"]

        echo "Test 3================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo" ["Warn2003_1.fs"]

        echo "Test 4================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo" ["Warn2003_2.fs"]

        echo "Test 5================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo" ["Warn2003_3.fs"]

        echo "Test 6================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo --nowarn:2003" ["Warn2003_1.fs"]

        echo "Test 7================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo --nowarn:2003" ["Warn2003_2.fs"]

        echo "Test 8================================================="
        fscAppend cfg stdoutPath stderrPath "--nologo --nowarn:2003" ["Warn2003_3.fs"]

        let normalizePaths f =
            let text = File.ReadAllText(f)
            let dummyPath = @"D:\staging\staging\src\tests\fsharp\core\load-script"
            let contents = System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(cfg.Directory), dummyPath)
            File.WriteAllText(f, contents)

        normalizePaths stdoutPath
        normalizePaths stderrPath

        let diffs = fsdiff cfg stdoutPath stdoutBaseline

        match diffs with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" stdoutPath stdoutBaseline diffs)

        let diffs2 = fsdiff cfg stderrPath stderrBaseline

        match diffs2 with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" stderrPath stderrBaseline diffs2)

    [<Test>]
    let ``load-script`` () =
        let cfg = testConfig "core/load-script"

        let stdoutPath = "out.stdout.txt" |> getfullpath cfg
        let stderrPath = "out.stderr.txt" |> getfullpath cfg
        let stderrBaseline = "out.stderr.bsl" |> getfullpath cfg
        let stdoutBaseline = "out.stdout.bsl" |> getfullpath cfg

        let appendToFile from = Commands.appendToFile cfg.Directory from stdoutPath
        let echo text = Commands.echoAppendToFile cfg.Directory text stdoutPath

        File.WriteAllText(stdoutPath, "")
        File.WriteAllText(stderrPath, "")

        do if fileExists cfg "3.exe" then getfullpath cfg "3.exe" |> File.Delete

        ["1.fsx"; "2.fsx"; "3.fsx"] |> List.iter appendToFile

        echo "Test 1================================================="

        fscAppend cfg stdoutPath stderrPath "--nologo" ["3.fsx"]

        execAppendIgnoreExitCode cfg stdoutPath stderrPath ("." ++ "3.exe") ""

        rm cfg "3.exe"

        echo "Test 2================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["3.fsx"]

        echo "Test 3================================================="

        fsiStdinAppendBothIgnoreExitCode cfg stdoutPath stderrPath "pipescr" "--nologo" []

        echo "Test 4================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["usesfsi.fsx"]

        echo "Test 5================================================="

        fscAppendIgnoreExitCode cfg stdoutPath stderrPath "--nologo" ["usesfsi.fsx"]

        echo "Test 6================================================="

        fscAppend cfg stdoutPath stderrPath "--nologo -r \"%s\"" cfg.FSharpCompilerInteractiveSettings ["usesfsi.fsx"]

        echo "Test 7================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["1.fsx";"2.fsx";"3.fsx"]

        echo "Test 8================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["3.fsx";"2.fsx";"1.fsx"]

        echo "Test 9================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["multiple-load-1.fsx"]

        echo "Test 10================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["multiple-load-2.fsx"]

        echo "Test 11================================================="

        fscAppend cfg stdoutPath stderrPath "--nologo" ["FlagCheck.fs"]

        execAppendIgnoreExitCode cfg stdoutPath stderrPath ("." ++ "FlagCheck.exe") ""

        rm cfg "FlagCheck.exe"

        echo "Test 12================================================="

        fscAppend cfg stdoutPath stderrPath "-o FlagCheckScript.exe --nologo" ["FlagCheck.fsx"]

        execAppendIgnoreExitCode cfg stdoutPath stderrPath ("." ++ "FlagCheckScript.exe") ""

        rm cfg "FlagCheckScript.exe"

        echo "Test 13================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["load-FlagCheckFs.fsx"]

        echo "Test 14================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["FlagCheck.fsx"]

        echo "Test 15================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["ProjectDriver.fsx"]

        echo "Test 16================================================="

        fscAppend cfg stdoutPath stderrPath "--nologo" ["ProjectDriver.fsx"]

        execAppendIgnoreExitCode cfg stdoutPath stderrPath ("." ++ "ProjectDriver.exe") ""

        rm cfg "ProjectDriver.exe"

        echo "Test 17================================================="

        fsiAppendIgnoreExitCode cfg stdoutPath stderrPath "" ["load-IncludeNoWarn211.fsx"]

        echo "Done =================================================="

        // an extra case
        fsiExpectFail cfg "" ["loadfail3.fsx"]

        let normalizePaths f =
            let text = File.ReadAllText(f)
            let dummyPath = @"D:\staging\staging\src\tests\fsharp\core\load-script"
            let contents = System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(cfg.Directory), dummyPath)
            File.WriteAllText(f, contents)

        normalizePaths stdoutPath
        normalizePaths stderrPath

        let diffs = fsdiff cfg stdoutPath stdoutBaseline

        match diffs with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" stdoutPath stdoutBaseline diffs)

        let diffs2 = fsdiff cfg stderrPath stderrBaseline

        match diffs2 with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" stderrPath stderrBaseline diffs2)
#endif

    [<Test>]
    let ``longnames-FSC_BASIC`` () = singleTestBuildAndRun "core/longnames" FSC_BASIC

    [<Test>]
    let ``longnames-FSI_BASIC`` () = singleTestBuildAndRun "core/longnames" FSI_BASIC

    [<Test>]
    let ``math-numbersVS2008-FSC_BASIC`` () = singleTestBuildAndRun "core/math/numbersVS2008" FSC_BASIC

    [<Test>]
    let ``math-numbersVS2008-FSI_BASIC`` () = singleTestBuildAndRun "core/math/numbersVS2008" FSI_BASIC

    [<Test>]
    let ``patterns-FSC_BASIC`` () = singleTestBuildAndRun "core/patterns" FSC_BASIC

//BUGBUG: https://github.com/Microsoft/visualfsharp/issues/6601
//    [<Test>]
//    let ``patterns-FSI_BASIC`` () = singleTestBuildAndRun' "core/patterns" FSI_BASIC

    [<Test>]
    let ``pinvoke-FSC_BASIC`` () = singleTestBuildAndRun "core/pinvoke" FSC_BASIC

    [<Test>]
    let ``pinvoke-FSI_BASIC`` () =
        singleTestBuildAndRun "core/pinvoke" FSI_BASIC

    [<Test>]
    let ``fsi_load-FSC_BASIC`` () = singleTestBuildAndRun "core/fsi-load" FSC_BASIC

    [<Test>]
    let ``fsi_load-FSI_BASIC`` () = singleTestBuildAndRun "core/fsi-load" FSI_BASIC

#if !NETCOREAPP
    [<Test>]
    let ``measures-AS_DLL`` () = singleTestBuildAndRun "core/measures" AS_DLL

    [<Test>]
    let ``members-basics-AS_DLL`` () = singleTestBuildAndRun "core/members/basics" AS_DLL

    [<Test>]
    let ``members-basics-hw`` () = singleTestBuildAndRun "core/members/basics-hw" FSC_BASIC

    [<Test>]
    let ``members-basics-hw-mutrec`` () = singleTestBuildAndRun "core/members/basics-hw-mutrec" FSC_BASIC

    [<Test>]
    let ``members-incremental-FSC_BASIC`` () = singleTestBuildAndRun "core/members/incremental" FSC_BASIC

    [<Test>]
    let ``members-incremental-FSI_BASIC`` () = singleTestBuildAndRun "core/members/incremental" FSI_BASIC

    [<Test>]
    let ``members-incremental-hw-FSC_BASIC`` () = singleTestBuildAndRun "core/members/incremental-hw" FSC_BASIC

    [<Test>]
    let ``members-incremental-hw-FSI_BASIC`` () = singleTestBuildAndRun "core/members/incremental-hw" FSI_BASIC

    [<Test>]
    let ``members-incremental-hw-mutrec-FSC_BASIC`` () = singleTestBuildAndRun "core/members/incremental-hw-mutrec" FSC_BASIC

    [<Test>]
    let queriesLeafExpressionConvert () =
        let cfg = testConfig "core/queriesLeafExpressionConvert"

        fsc cfg "%s -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg "%s --optimize -o:test--optimize.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        use testOkFile = fileguard cfg "test.ok"

        fsi cfg "%s" cfg.fsi_flags ["test.fsx"]

        testOkFile.CheckExists()

        use testOkFile2 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile2.CheckExists()

        use testOkFile3 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test--optimize.exe") ""

        testOkFile3.CheckExists()


    [<Test>]
    let queriesNullableOperators () =
        let cfg = testConfig "core/queriesNullableOperators"

        fsc cfg "%s -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg "%s --optimize -o:test--optimize.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        use testOkFile = fileguard cfg "test.ok"
        fsi cfg "%s" cfg.fsi_flags ["test.fsx"]
        testOkFile.CheckExists()

        use testOkFile2 = fileguard cfg "test.ok"
        exec cfg ("." ++ "test.exe") ""
        testOkFile2.CheckExists()

        use testOkFile3 = fileguard cfg "test.ok"
        exec cfg ("." ++ "test--optimize.exe") ""
        testOkFile3.CheckExists()

    [<Test>]
    let queriesOverIEnumerable () =
        let cfg = testConfig "core/queriesOverIEnumerable"

        fsc cfg "%s -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg "%s --optimize -o:test--optimize.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        use testOkFile = fileguard cfg "test.ok"

        fsi cfg "%s" cfg.fsi_flags ["test.fsx"]

        testOkFile.CheckExists()

        use testOkFile2 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile2.CheckExists()

        use testOkFile3 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test--optimize.exe") ""

        testOkFile3.CheckExists()

    [<Test>]
    let queriesOverIQueryable () =
        let cfg = testConfig "core/queriesOverIQueryable"

        fsc cfg "%s -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg "%s --optimize -o:test--optimize.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        use testOkFile = fileguard cfg "test.ok"
        fsi cfg "%s" cfg.fsi_flags ["test.fsx"]

        testOkFile.CheckExists()


        use testOkFile2 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile2.CheckExists()


        use testOkFile3 = fileguard cfg "test.ok"
        exec cfg ("." ++ "test--optimize.exe") ""

        testOkFile3.CheckExists()


    [<Test>]
    let quotesDebugInfo () =
        let cfg = testConfig "core/quotesDebugInfo"

        fsc cfg "%s --quotations-debug+ --optimize -o:test.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test.exe"

        fsc cfg "%s --quotations-debug+ --optimize -o:test--optimize.exe -g" cfg.fsc_flags ["test.fsx"]

        peverify cfg "test--optimize.exe"

        use testOkFile = fileguard cfg "test.ok"
        fsi cfg "%s --quotations-debug+" cfg.fsi_flags ["test.fsx"]

        testOkFile.CheckExists()


        use testOkFile2 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile2.CheckExists()

        use testOkFile3 = fileguard cfg "test.ok"

        exec cfg ("." ++ "test--optimize.exe") ""

        testOkFile3.CheckExists()


    [<Test>]
    let quotesInMultipleModules () =
        let cfg = testConfig "core/quotesInMultipleModules"

        fsc cfg "%s -o:module1.dll --target:library" cfg.fsc_flags ["module1.fsx"]

        peverify cfg "module1.dll"

        fsc cfg "%s -o:module2.exe -r:module1.dll" cfg.fsc_flags ["module2.fsx"]

        peverify cfg "module2.exe"

        fsc cfg "%s --staticlink:module1 -o:module2-staticlink.exe -r:module1.dll" cfg.fsc_flags ["module2.fsx"]

        peverify cfg "module2-staticlink.exe"

        fsc cfg "%s -o:module1-opt.dll --target:library --optimize" cfg.fsc_flags ["module1.fsx"]

        peverify cfg "module1-opt.dll"

        fsc cfg "%s -o:module2-opt.exe -r:module1-opt.dll --optimize" cfg.fsc_flags ["module2.fsx"]

        peverify cfg "module2-opt.exe"

        use testOkFile = fileguard cfg "test.ok"

        fsi cfg "%s -r module1.dll" cfg.fsi_flags ["module2.fsx"]

        testOkFile.CheckExists()


        use testOkFile = fileguard cfg "test.ok"

        exec cfg ("." ++ "module2.exe") ""

        testOkFile.CheckExists()

        use testOkFile = fileguard cfg "test.ok"

        exec cfg ("." ++ "module2-opt.exe") ""

        testOkFile.CheckExists()

        use testOkFile = fileguard cfg "test.ok"

        exec cfg ("." ++ "module2-staticlink.exe") ""

        testOkFile.CheckExists()
#endif

    [<Test>]
    let ``reflect-FSC_BASIC`` () = singleTestBuildAndRun "core/reflect" FSC_BASIC

    [<Test>]
    let ``reflect-FSI_BASIC`` () = singleTestBuildAndRun "core/reflect" FSI_BASIC

#if !NETCOREAPP
    [<Test>]
    let refnormalization () =
        let cfg = testConfig "core/refnormalization"

        // Prepare by building multiple versions of the test assemblies
        fsc cfg @"%s --target:library -o:version1\DependentAssembly.dll -g --version:1.0.0.0 --keyfile:keyfile.snk" cfg.fsc_flags [@"DependentAssembly.fs"]
        fsc cfg @"%s --target:library -o:version1\AscendentAssembly.dll -g --version:1.0.0.0 --keyfile:keyfile.snk -r:version1\DependentAssembly.dll" cfg.fsc_flags [@"AscendentAssembly.fs"]

        fsc cfg @"%s --target:library -o:version2\DependentAssembly.dll -g --version:2.0.0.0" cfg.fsc_flags [@"DependentAssembly.fs"]
        fsc cfg @"%s --target:library -o:version2\AscendentAssembly.dll -g --version:2.0.0.0 -r:version2\DependentAssembly.dll" cfg.fsc_flags [@"AscendentAssembly.fs"]

        //TestCase1
        // Build a program that references v2 of ascendent and v1 of dependent.
        // Note that, even though ascendent v2 references dependent v2, the reference is marked as v1.
        use TestOk = fileguard cfg "test.ok"
        fsc cfg @"%s -o:test1.exe -r:version1\DependentAssembly.dll -r:version2\AscendentAssembly.dll --optimize- -g" cfg.fsc_flags ["test.fs"]
        exec cfg ("." ++ "test1.exe") "DependentAssembly-1.0.0.0 AscendentAssembly-2.0.0.0"
        TestOk.CheckExists()

        //TestCase2
        // Build a program that references v1 of ascendent and v2 of dependent.
        // Note that, even though ascendent v1 references dependent v1, the reference is marked as v2 which was passed in.
        use TestOk = fileguard cfg "test.ok"
        fsc cfg @"%s -o:test2.exe -r:version2\DependentAssembly.dll -r:version1\AscendentAssembly.dll --optimize- -g" cfg.fsc_flags ["test.fs"]
        exec cfg ("." ++ "test2.exe") "DependentAssembly-2.0.0.0 AscendentAssembly-1.0.0.0"
        TestOk.CheckExists()

        //TestCase3
        // Build a program that references v1 of ascendent and v1 and v2 of dependent.
        // Verifies that compiler uses first version of a duplicate assembly passed on command line.
        use TestOk = fileguard cfg "test.ok"
        fsc cfg @"%s -o:test3.exe -r:version1\DependentAssembly.dll -r:version2\DependentAssembly.dll -r:version1\AscendentAssembly.dll --optimize- -g" cfg.fsc_flags ["test.fs"]
        exec cfg ("." ++ "test3.exe") "DependentAssembly-1.0.0.0 AscendentAssembly-1.0.0.0"
        TestOk.CheckExists()


    [<Test>]
    let testResources () =
        let cfg = testConfig "core/resources"

        fsc cfg "%s  --resource:Resources.resources -o:test-embed.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test-embed.exe"

        fsc cfg "%s  --linkresource:Resources.resources -o:test-link.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test-link.exe"

        fsc cfg "%s  --resource:Resources.resources,ResourceName.resources -o:test-embed-named.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test-embed-named.exe"

        fsc cfg "%s  --linkresource:Resources.resources,ResourceName.resources -o:test-link-named.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test-link-named.exe"

        exec cfg ("." ++ "test-embed.exe") ""

        exec cfg ("." ++ "test-link.exe") ""

        exec cfg ("." ++ "test-link-named.exe") "ResourceName"

        exec cfg ("." ++ "test-embed-named.exe") "ResourceName"

    [<Test>]
    let topinit () =
        let cfg = testConfig "core/topinit"

        fsc cfg "%s --optimize -o both69514.exe -g" cfg.fsc_flags ["lib69514.fs"; "app69514.fs"]

        peverify cfg "both69514.exe"

        fsc cfg "%s --optimize- -o both69514-noopt.exe -g" cfg.fsc_flags ["lib69514.fs"; "app69514.fs"]

        peverify cfg "both69514-noopt.exe"

        fsc cfg "%s --optimize -a -g" cfg.fsc_flags ["lib69514.fs"]

        peverify cfg "lib69514.dll"

        fsc cfg "%s --optimize -r:lib69514.dll -g" cfg.fsc_flags ["app69514.fs"]

        peverify cfg "app69514.exe"

        fsc cfg "%s --optimize- -o:lib69514-noopt.dll -a -g" cfg.fsc_flags ["lib69514.fs"]

        peverify cfg "lib69514-noopt.dll"

        fsc cfg "%s --optimize- -r:lib69514-noopt.dll -o:app69514-noopt.exe -g" cfg.fsc_flags ["app69514.fs"]

        peverify cfg "app69514-noopt.exe"

        fsc cfg "%s --optimize- -o:lib69514-noopt-withsig.dll -a -g" cfg.fsc_flags ["lib69514.fsi"; "lib69514.fs"]

        peverify cfg "lib69514-noopt-withsig.dll"

        fsc cfg "%s --optimize- -r:lib69514-noopt-withsig.dll -o:app69514-noopt-withsig.exe -g" cfg.fsc_flags ["app69514.fs"]

        peverify cfg "app69514-noopt-withsig.exe"

        fsc cfg "%s -o:lib69514-withsig.dll -a -g" cfg.fsc_flags ["lib69514.fsi"; "lib69514.fs"]

        peverify cfg "lib69514-withsig.dll"

        fsc cfg "%s -r:lib69514-withsig.dll -o:app69514-withsig.exe -g" cfg.fsc_flags ["app69514.fs"]

        peverify cfg "app69514-withsig.exe"

        fsc cfg "%s -o:lib.dll -a -g" cfg.fsc_flags ["lib.ml"]

        peverify cfg "lib.dll"

        csc cfg """/nologo /r:"%s" /r:lib.dll /out:test.exe """ cfg.FSCOREDLLPATH ["test.cs"]

        fsc cfg "%s --optimize -o:lib--optimize.dll -a -g" cfg.fsc_flags ["lib.ml"]

        peverify cfg "lib--optimize.dll"

        csc cfg """/nologo /r:"%s" /r:lib--optimize.dll /out:test--optimize.exe""" cfg.FSCOREDLLPATH ["test.cs"]

        let dicases = ["flag_deterministic_init1.fs"; "lib_deterministic_init1.fs"; "flag_deterministic_init2.fs"; "lib_deterministic_init2.fs"; "flag_deterministic_init3.fs"; "lib_deterministic_init3.fs"; "flag_deterministic_init4.fs"; "lib_deterministic_init4.fs"; "flag_deterministic_init5.fs"; "lib_deterministic_init5.fs"; "flag_deterministic_init6.fs"; "lib_deterministic_init6.fs"; "flag_deterministic_init7.fs"; "lib_deterministic_init7.fs"; "flag_deterministic_init8.fs"; "lib_deterministic_init8.fs"; "flag_deterministic_init9.fs"; "lib_deterministic_init9.fs"; "flag_deterministic_init10.fs"; "lib_deterministic_init10.fs"; "flag_deterministic_init11.fs"; "lib_deterministic_init11.fs"; "flag_deterministic_init12.fs"; "lib_deterministic_init12.fs"; "flag_deterministic_init13.fs"; "lib_deterministic_init13.fs"; "flag_deterministic_init14.fs"; "lib_deterministic_init14.fs"; "flag_deterministic_init15.fs"; "lib_deterministic_init15.fs"; "flag_deterministic_init16.fs"; "lib_deterministic_init16.fs"; "flag_deterministic_init17.fs"; "lib_deterministic_init17.fs"; "flag_deterministic_init18.fs"; "lib_deterministic_init18.fs"; "flag_deterministic_init19.fs"; "lib_deterministic_init19.fs"; "flag_deterministic_init20.fs"; "lib_deterministic_init20.fs"; "flag_deterministic_init21.fs"; "lib_deterministic_init21.fs"; "flag_deterministic_init22.fs"; "lib_deterministic_init22.fs"; "flag_deterministic_init23.fs"; "lib_deterministic_init23.fs"; "flag_deterministic_init24.fs"; "lib_deterministic_init24.fs"; "flag_deterministic_init25.fs"; "lib_deterministic_init25.fs"; "flag_deterministic_init26.fs"; "lib_deterministic_init26.fs"; "flag_deterministic_init27.fs"; "lib_deterministic_init27.fs"; "flag_deterministic_init28.fs"; "lib_deterministic_init28.fs"; "flag_deterministic_init29.fs"; "lib_deterministic_init29.fs"; "flag_deterministic_init30.fs"; "lib_deterministic_init30.fs"; "flag_deterministic_init31.fs"; "lib_deterministic_init31.fs"; "flag_deterministic_init32.fs"; "lib_deterministic_init32.fs"; "flag_deterministic_init33.fs"; "lib_deterministic_init33.fs"; "flag_deterministic_init34.fs"; "lib_deterministic_init34.fs"; "flag_deterministic_init35.fs"; "lib_deterministic_init35.fs"; "flag_deterministic_init36.fs"; "lib_deterministic_init36.fs"; "flag_deterministic_init37.fs"; "lib_deterministic_init37.fs"; "flag_deterministic_init38.fs"; "lib_deterministic_init38.fs"; "flag_deterministic_init39.fs"; "lib_deterministic_init39.fs"; "flag_deterministic_init40.fs"; "lib_deterministic_init40.fs"; "flag_deterministic_init41.fs"; "lib_deterministic_init41.fs"; "flag_deterministic_init42.fs"; "lib_deterministic_init42.fs"; "flag_deterministic_init43.fs"; "lib_deterministic_init43.fs"; "flag_deterministic_init44.fs"; "lib_deterministic_init44.fs"; "flag_deterministic_init45.fs"; "lib_deterministic_init45.fs"; "flag_deterministic_init46.fs"; "lib_deterministic_init46.fs"; "flag_deterministic_init47.fs"; "lib_deterministic_init47.fs"; "flag_deterministic_init48.fs"; "lib_deterministic_init48.fs"; "flag_deterministic_init49.fs"; "lib_deterministic_init49.fs"; "flag_deterministic_init50.fs"; "lib_deterministic_init50.fs"; "flag_deterministic_init51.fs"; "lib_deterministic_init51.fs"; "flag_deterministic_init52.fs"; "lib_deterministic_init52.fs"; "flag_deterministic_init53.fs"; "lib_deterministic_init53.fs"; "flag_deterministic_init54.fs"; "lib_deterministic_init54.fs"; "flag_deterministic_init55.fs"; "lib_deterministic_init55.fs"; "flag_deterministic_init56.fs"; "lib_deterministic_init56.fs"; "flag_deterministic_init57.fs"; "lib_deterministic_init57.fs"; "flag_deterministic_init58.fs"; "lib_deterministic_init58.fs"; "flag_deterministic_init59.fs"; "lib_deterministic_init59.fs"; "flag_deterministic_init60.fs"; "lib_deterministic_init60.fs"; "flag_deterministic_init61.fs"; "lib_deterministic_init61.fs"; "flag_deterministic_init62.fs"; "lib_deterministic_init62.fs"; "flag_deterministic_init63.fs"; "lib_deterministic_init63.fs"; "flag_deterministic_init64.fs"; "lib_deterministic_init64.fs"; "flag_deterministic_init65.fs"; "lib_deterministic_init65.fs"; "flag_deterministic_init66.fs"; "lib_deterministic_init66.fs"; "flag_deterministic_init67.fs"; "lib_deterministic_init67.fs"; "flag_deterministic_init68.fs"; "lib_deterministic_init68.fs"; "flag_deterministic_init69.fs"; "lib_deterministic_init69.fs"; "flag_deterministic_init70.fs"; "lib_deterministic_init70.fs"; "flag_deterministic_init71.fs"; "lib_deterministic_init71.fs"; "flag_deterministic_init72.fs"; "lib_deterministic_init72.fs"; "flag_deterministic_init73.fs"; "lib_deterministic_init73.fs"; "flag_deterministic_init74.fs"; "lib_deterministic_init74.fs"; "flag_deterministic_init75.fs"; "lib_deterministic_init75.fs"; "flag_deterministic_init76.fs"; "lib_deterministic_init76.fs"; "flag_deterministic_init77.fs"; "lib_deterministic_init77.fs"; "flag_deterministic_init78.fs"; "lib_deterministic_init78.fs"; "flag_deterministic_init79.fs"; "lib_deterministic_init79.fs"; "flag_deterministic_init80.fs"; "lib_deterministic_init80.fs"; "flag_deterministic_init81.fs"; "lib_deterministic_init81.fs"; "flag_deterministic_init82.fs"; "lib_deterministic_init82.fs"; "flag_deterministic_init83.fs"; "lib_deterministic_init83.fs"; "flag_deterministic_init84.fs"; "lib_deterministic_init84.fs"; "flag_deterministic_init85.fs"; "lib_deterministic_init85.fs"]

        fsc cfg "%s --optimize- -o test_deterministic_init.exe" cfg.fsc_flags (dicases @ ["test_deterministic_init.fs"])

        peverify cfg "test_deterministic_init.exe"

        fsc cfg "%s --optimize -o test_deterministic_init--optimize.exe" cfg.fsc_flags (dicases @ ["test_deterministic_init.fs"])

        peverify cfg "test_deterministic_init--optimize.exe"

        fsc cfg "%s --optimize- -a -o test_deterministic_init_lib.dll" cfg.fsc_flags dicases

        peverify cfg "test_deterministic_init_lib.dll"

        fsc cfg "%s --optimize- -r test_deterministic_init_lib.dll -o test_deterministic_init_exe.exe" cfg.fsc_flags ["test_deterministic_init.fs"]

        peverify cfg "test_deterministic_init_exe.exe"

        fsc cfg "%s --optimize -a -o test_deterministic_init_lib--optimize.dll" cfg.fsc_flags dicases

        peverify cfg "test_deterministic_init_lib--optimize.dll"

        fsc cfg "%s --optimize -r test_deterministic_init_lib--optimize.dll -o test_deterministic_init_exe--optimize.exe" cfg.fsc_flags ["test_deterministic_init.fs"]

        peverify cfg "test_deterministic_init_exe--optimize.exe"

        let static_init_cases = [ "test0.fs"; "test1.fs"; "test2.fs"; "test3.fs"; "test4.fs"; "test5.fs"; "test6.fs" ]

        fsc cfg "%s --optimize- -o test_static_init.exe" cfg.fsc_flags (static_init_cases @ ["static-main.fs"])

        peverify cfg "test_static_init.exe"

        fsc cfg "%s --optimize -o test_static_init--optimize.exe" cfg.fsc_flags (static_init_cases @ [ "static-main.fs" ])

        peverify cfg "test_static_init--optimize.exe"

        fsc cfg "%s --optimize- -a -o test_static_init_lib.dll" cfg.fsc_flags static_init_cases

        peverify cfg "test_static_init_lib.dll"

        fsc cfg "%s --optimize- -r test_static_init_lib.dll -o test_static_init_exe.exe" cfg.fsc_flags ["static-main.fs"]

        peverify cfg "test_static_init_exe.exe"

        fsc cfg "%s --optimize -a -o test_static_init_lib--optimize.dll" cfg.fsc_flags static_init_cases

        peverify cfg "test_static_init_lib--optimize.dll"

        fsc cfg "%s --optimize -r test_static_init_lib--optimize.dll -o test_static_init_exe--optimize.exe" cfg.fsc_flags ["static-main.fs"]

        peverify cfg "test_static_init_exe--optimize.exe"

        exec cfg ("." ++ "test.exe") ""

        exec cfg ("." ++ "test--optimize.exe") ""

        exec cfg ("." ++ "test_deterministic_init.exe") ""

        exec cfg ("." ++ "test_deterministic_init--optimize.exe") ""

        exec cfg ("." ++ "test_deterministic_init_exe.exe") ""

        exec cfg ("." ++ "test_deterministic_init_exe--optimize.exe") ""

        exec cfg ("." ++ "test_static_init.exe") ""

        exec cfg ("." ++ "test_static_init--optimize.exe") ""

        exec cfg ("." ++ "test_static_init_exe.exe") ""

        exec cfg ("." ++ "test_static_init_exe--optimize.exe") ""

    [<Test>]
    let unitsOfMeasure () =
        let cfg = testConfig "core/unitsOfMeasure"

        fsc cfg "%s --optimize- -o:test.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test.exe"

        use testOkFile = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile.CheckExists()

    [<Test>]
    let verify () =
        let cfg = testConfig "core/verify"

        peverifyWithArgs cfg "/nologo" (cfg.FSharpBuild)

       // peverifyWithArgs cfg "/nologo /MD" (getDirectoryName(cfg.FSC) ++ "FSharp.Compiler.dll")

        peverifyWithArgs cfg "/nologo" (cfg.FSI)

        peverifyWithArgs cfg "/nologo" (cfg.FSharpCompilerInteractiveSettings)

        fsc cfg "%s -o:xmlverify.exe -g" cfg.fsc_flags ["xmlverify.fs"]

        peverifyWithArgs cfg "/nologo" "xmlverify.exe"
        
        
    [<Test>]
    let ``property setter in method or constructor`` () =
        let cfg = testConfig "core/members/set-only-property"
        csc cfg @"%s /target:library /out:cs.dll" cfg.csc_flags ["cs.cs"]
        vbc cfg @"%s /target:library /out:vb.dll" cfg.vbc_flags ["vb.vb"]
        fsc cfg @"%s /target:library /out:fs.dll" cfg.fsc_flags ["fs.fs"]
        singleNegTest cfg "calls"

#endif

[<NonParallelizable>]
module VersionTests =
    [<Test>]
    let ``member-selfidentifier-version4.6``() = singleTestBuildAndRunVersion "core/members/self-identifier/version46" FSC_BUILDONLY "4.6"

    [<Test>]
    let ``member-selfidentifier-version4.7``() = singleTestBuildAndRun "core/members/self-identifier/version47" FSC_BUILDONLY

    [<Test>]
    let ``indent-version4.6``() = singleTestBuildAndRunVersion "core/indent/version46" FSC_BUILDONLY "4.6"

    [<Test>]
    let ``indent-version4.7``() = singleTestBuildAndRun "core/indent/version47" FSC_BUILDONLY

    [<Test>]
    let ``nameof-version4.6``() = singleTestBuildAndRunVersion "core/nameof/version46" FSC_BUILDONLY "4.6"

    [<Test>]
    let ``nameof-versionpreview``() = singleTestBuildAndRunVersion "core/nameof/preview" FSC_BUILDONLY "preview"

    [<Test>]
    let ``nameof-execute``() = singleTestBuildAndRunVersion "core/nameof/preview" FSC_BASIC "preview"

    [<Test>]
    let ``nameof-fsi``() = singleTestBuildAndRunVersion "core/nameof/preview" FSI_BASIC "preview"

#if !NETCOREAPP
[<NonParallelizable>]
module ToolsTests =

    // This test is disabled in coreclr builds dependent on fixing : https://github.com/Microsoft/visualfsharp/issues/2600
    [<Test>]
    let bundle () =
        let cfg = testConfig "tools/bundle"

        fsc cfg "%s --progress --standalone -o:test-one-fsharp-module.exe -g" cfg.fsc_flags ["test-one-fsharp-module.fs"]

        peverify cfg "test-one-fsharp-module.exe"

        fsc cfg "%s -a -o:test_two_fsharp_modules_module_1.dll -g" cfg.fsc_flags ["test_two_fsharp_modules_module_1.fs"]

        peverify cfg "test_two_fsharp_modules_module_1.dll"

        fsc cfg "%s --standalone -r:test_two_fsharp_modules_module_1.dll -o:test_two_fsharp_modules_module_2.exe -g" cfg.fsc_flags ["test_two_fsharp_modules_module_2.fs"]

        peverify cfg "test_two_fsharp_modules_module_2.exe"

        fsc cfg "%s -a --standalone -r:test_two_fsharp_modules_module_1.dll -o:test_two_fsharp_modules_module_2_as_dll.dll -g" cfg.fsc_flags ["test_two_fsharp_modules_module_2.fs"]

        peverify cfg "test_two_fsharp_modules_module_2_as_dll.dll"
#endif

    [<Test>]
    let ``eval-FSC_BASIC`` () = singleTestBuildAndRun "tools/eval" FSC_BASIC
    [<Test>]
    let ``eval-FSI_BASIC`` () = singleTestBuildAndRun "tools/eval" FSI_BASIC

[<NonParallelizable>]
module RegressionTests =

    [<Test>]
    let ``literal-value-bug-2-FSC_BASIC`` () = singleTestBuildAndRun "regression/literal-value-bug-2" FSC_BASIC

    [<Test>]
    let ``literal-value-bug-2-FSI_BASIC`` () = singleTestBuildAndRun "regression/literal-value-bug-2" FSI_BASIC

    [<Test>]
    let ``OverloadResolution-bug-FSC_BASIC`` () = singleTestBuildAndRun "regression/OverloadResolution-bug" FSC_BASIC

    [<Test>]
    let ``OverloadResolution-bug-FSI_BASIC`` () = singleTestBuildAndRun "regression/OverloadResolution-bug" FSI_BASIC

    [<Test>]
    let ``struct-tuple-bug-1-FSC_BASIC`` () = singleTestBuildAndRun "regression/struct-tuple-bug-1" FSC_BASIC

    [<Test >]
    let ``tuple-bug-1-FSC_BASIC`` () = singleTestBuildAndRun "regression/tuple-bug-1" FSC_BASIC

#if !NETCOREAPP
    [<Test>]
    let ``SRTP doesn't handle calling member hiding hinherited members`` () =
        let cfg = testConfig "regression/5531"

        let outFile = "compilation.output.test.txt"
        let expectedFile = "compilation.output.test.bsl"

        fscBothToOut cfg outFile "%s --nologo -O" cfg.fsc_flags ["test.fs"]

        let diff = fsdiff cfg outFile expectedFile

        match diff with
        | "" -> ()
        | _ ->
            Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile) (getfullpath cfg expectedFile) diff)

        let outFile2 = "output.test.txt"
        let expectedFile2 = "output.test.bsl"

        execBothToOut cfg (cfg.Directory) outFile2 (cfg.Directory ++ "test.exe") ""

        let diff2 = fsdiff cfg outFile2 expectedFile2
        match diff2 with
        | "" -> ()
        | _ ->
            Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile2) (getfullpath cfg expectedFile2) diff2)
#endif

    [<Test>]
    let ``26`` () = singleTestBuildAndRun "regression/26" FSC_BASIC

    [<Test >]
    let ``321`` () = singleTestBuildAndRun "regression/321" FSC_BASIC

#if !NETCOREAPP
    // This test is disabled in coreclr builds dependent on fixing : https://github.com/Microsoft/visualfsharp/issues/2600
    [<Test>]
    let ``655`` () =
        let cfg = testConfig "regression/655"

        fsc cfg "%s -a -o:pack.dll" cfg.fsc_flags ["xlibC.ml"]

        peverify cfg "pack.dll"

        fsc cfg "%s    -o:test.exe -r:pack.dll" cfg.fsc_flags ["main.fs"]

        peverify cfg "test.exe"

        use testOkFile = fileguard cfg "test.ok"

        exec cfg ("." ++ "test.exe") ""

        testOkFile.CheckExists()

    // This test is disabled in coreclr builds dependent on fixing : https://github.com/Microsoft/visualfsharp/issues/2600
    [<Test >]
    let ``656`` () =
        let cfg = testConfig "regression/656"

        fsc cfg "%s -o:pack.exe" cfg.fsc_flags ["misc.fs mathhelper.fs filehelper.fs formshelper.fs plot.fs traj.fs playerrecord.fs trackedplayers.fs form.fs"]

        peverify cfg  "pack.exe"
#endif

#if !NETCOREAPP
    // Requires WinForms
    [<Test>]
    let ``83`` () = singleTestBuildAndRun "regression/83" FSC_BASIC

    [<Test >]
    let ``84`` () = singleTestBuildAndRun "regression/84" FSC_BASIC

    [<Test >]
    let ``85`` () =
        let cfg = testConfig "regression/85"

        fsc cfg "%s -r:Category.dll -a -o:petshop.dll" cfg.fsc_flags ["Category.ml"]

        peverify cfg "petshop.dll"
#endif

    [<Test >]
    let ``86`` () = singleTestBuildAndRun "regression/86" FSC_BASIC

    [<Test >]
    let ``struct-tuple-bug-1-FSI_BASIC`` () = singleTestBuildAndRun "regression/struct-tuple-bug-1" FSI_BASIC

#if !NETCOREAPP
    // This test is disabled in coreclr builds dependent on fixing : https://github.com/Microsoft/visualfsharp/issues/2600
    [<Test>]
    let ``struct-measure-bug-1`` () =
        let cfg = testConfig "regression/struct-measure-bug-1"

        fsc cfg "%s --optimize- -o:test.exe -g" cfg.fsc_flags ["test.fs"]

        peverify cfg "test.exe"

[<NonParallelizable>]
module OptimizationTests =

    [<Test>]
    let functionSizes () =
        let cfg = testConfig "optimize/analyses"

        let outFile = "sizes.FunctionSizes.output.test.txt"
        let expectedFile = "sizes.FunctionSizes.output.test.bsl"

        log "== FunctionSizes"
        fscBothToOut cfg outFile "%s --nologo -O --test:FunctionSizes" cfg.fsc_flags ["sizes.fs"]

        let diff = fsdiff cfg outFile expectedFile

        match diff with
        | "" -> ()
        | _ ->
            Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile) (getfullpath cfg expectedFile) diff)


    [<Test>]
    let totalSizes () =
        let cfg = testConfig "optimize/analyses"

        let outFile = "sizes.TotalSizes.output.test.txt"
        let expectedFile = "sizes.TotalSizes.output.test.bsl"

        log "== TotalSizes"
        fscBothToOut cfg outFile "%s --nologo -O --test:TotalSizes" cfg.fsc_flags ["sizes.fs"]

        let diff = fsdiff cfg outFile expectedFile

        match diff with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile) (getfullpath cfg expectedFile) diff)


    [<Test>]
    let hasEffect () =
        let cfg = testConfig "optimize/analyses"

        let outFile = "effects.HasEffect.output.test.txt"
        let expectedFile = "effects.HasEffect.output.test.bsl"

        log "== HasEffect"
        fscBothToOut cfg outFile "%s --nologo -O --test:HasEffect" cfg.fsc_flags ["effects.fs"]

        let diff = fsdiff cfg outFile expectedFile

        match diff with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile) (getfullpath cfg expectedFile) diff)


    [<Test>]
    let noNeedToTailcall () =
        let cfg = testConfig "optimize/analyses"

        let outFile = "tailcalls.NoNeedToTailcall.output.test.txt"
        let expectedFile = "tailcalls.NoNeedToTailcall.output.test.bsl"

        log "== NoNeedToTailcall"
        fscBothToOut cfg outFile "%s --nologo -O --test:NoNeedToTailcall" cfg.fsc_flags ["tailcalls.fs"]

        let diff = fsdiff cfg outFile expectedFile

        match diff with
        | "" -> ()
        | _ -> Assert.Fail (sprintf "'%s' and '%s' differ; %A" (getfullpath cfg outFile) (getfullpath cfg expectedFile) diff)


    [<Test>]
    let ``inline`` () =
        let cfg = testConfig "optimize/inline"

        fsc cfg "%s -g --optimize- --target:library -o:lib.dll" cfg.fsc_flags ["lib.fs"; "lib2.fs"]

        peverify cfg "lib.dll "

        fsc cfg "%s -g --optimize- --target:library -o:lib3.dll -r:lib.dll " cfg.fsc_flags ["lib3.fs"]

        fsc cfg "%s -g --optimize- -o:test.exe -r:lib.dll -r:lib3.dll" cfg.fsc_flags ["test.fs "]

        fsc cfg "%s --optimize --target:library -o:lib--optimize.dll -g" cfg.fsc_flags ["lib.fs"; "lib2.fs"]

        fsc cfg "%s --optimize --target:library -o:lib3--optimize.dll -r:lib--optimize.dll -g" cfg.fsc_flags ["lib3.fs"]

        fsc cfg "%s --optimize -o:test--optimize.exe -g -r:lib--optimize.dll  -r:lib3--optimize.dll" cfg.fsc_flags ["test.fs "]

        ildasm cfg "/out=test.il" "test.exe"

        ildasm cfg "/out=test--optimize.il" "test--optimize.exe"

        let ``test--optimize.il`` =
            File.ReadLines (getfullpath cfg "test--optimize.il")
            |> Seq.filter (fun line -> line.Contains(".locals init"))
            |> List.ofSeq

        match ``test--optimize.il`` with
            | [] -> ()
            | lines ->
                Assert.Fail (sprintf "Error: optimizations not removed.  Relevant lines from IL file follow: %A" lines)

        let numElim =
            File.ReadLines (getfullpath cfg "test.il")
            |> Seq.filter (fun line -> line.Contains(".locals init"))
            |> Seq.length

        log "Ran ok - optimizations removed %d textual occurrences of optimizable identifiers from target IL" numElim

    [<Test>]
    let stats () =
        let cfg = testConfig "optimize/stats"

        ildasm cfg "/out=FSharp.Core.il" cfg.FSCOREDLLPATH

        let fscore = File.ReadLines(getfullpath cfg "FSharp.Core.il") |> Seq.toList

        let contains text (s: string) = if s.Contains(text) then 1 else 0

        let typeFunc = fscore |> List.sumBy (contains "extends Microsoft.FSharp.TypeFunc")
        let classes = fscore |> List.sumBy (contains ".class")
        let methods = fscore |> List.sumBy (contains ".method")
        let fields = fscore |> List.sumBy (contains ".field")

        let date = DateTime.Today.ToString("dd/MM/yyyy") // 23/11/2006
        let time = DateTime.Now.ToString("HH:mm:ss.ff") // 16:03:23.40
        let m = sprintf "%s, %s, Microsoft.FSharp-TypeFunc, %d, Microsoft.FSharp-classes, %d,  Microsoft.FSharp-methods, %d, ,  Microsoft.FSharp-fields, %d,  " date time typeFunc classes methods fields

        log "now:"
        log "%s" m
#endif

[<NonParallelizable>]
module TypecheckTests =
    [<Test>]
    let ``full-rank-arrays`` () =
        let cfg = testConfig "typecheck/full-rank-arrays"
        SingleTest.singleTestBuildAndRunWithCopyDlls cfg "full-rank-arrays.dll" FSC_BASIC

    [<Test>]
    let misc () = singleTestBuildAndRun "typecheck/misc" FSC_BASIC

#if !NETCOREAPP

    [<Test>]
    let ``sigs pos26`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos26.exe" cfg.fsc_flags ["pos26.fsi"; "pos26.fs"]
        peverify cfg "pos26.exe"

    [<Test>]
    let ``sigs pos25`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos25.exe" cfg.fsc_flags ["pos25.fs"]
        peverify cfg "pos25.exe"

    [<Test>]
    let ``sigs pos27`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos27.exe" cfg.fsc_flags ["pos27.fs"]
        peverify cfg "pos27.exe"

    [<Test>]
    let ``sigs pos28`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos28.exe" cfg.fsc_flags ["pos28.fs"]
        peverify cfg "pos28.exe"

    [<Test>]
    let ``sigs pos29`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos29.exe" cfg.fsc_flags ["pos29.fsi"; "pos29.fs"; "pos29.app.fs"]
        peverify cfg "pos29.exe"

    [<Test>]
    let ``sigs pos30`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos30.exe --warnaserror+" cfg.fsc_flags ["pos30.fs"]
        peverify cfg "pos30.exe"

    [<Test>]
    let ``sigs pos24`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos24.exe" cfg.fsc_flags ["pos24.fs"]
        peverify cfg "pos24.exe"

    [<Test>]
    let ``sigs pos31`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos31.exe --warnaserror" cfg.fsc_flags ["pos31.fsi"; "pos31.fs"]
        peverify cfg "pos31.exe"

    [<Test>]
    let ``sigs pos32`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos32.dll --warnaserror" cfg.fsc_flags ["pos32.fs"]
        peverify cfg "pos32.dll"

    [<Test>]
    let ``sigs pos33`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos33.dll --warnaserror" cfg.fsc_flags ["pos33.fsi"; "pos33.fs"]
        peverify cfg "pos33.dll"

    [<Test>]
    let ``sigs pos34`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos34.dll --warnaserror" cfg.fsc_flags ["pos34.fs"]
        peverify cfg "pos34.dll"

    [<Test>]
    let ``sigs pos35`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos35.dll --warnaserror" cfg.fsc_flags ["pos35.fs"]
        peverify cfg "pos35.dll"

    [<Test>]
    let ``sigs pos36-srtp`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos36-srtp-lib.dll --warnaserror" cfg.fsc_flags ["pos36-srtp-lib.fs"]
        fsc cfg "%s --target:exe -r:pos36-srtp-lib.dll -o:pos36-srtp-app.exe --warnaserror" cfg.fsc_flags ["pos36-srtp-app.fs"]
        peverify cfg "pos36-srtp-lib.dll"
        peverify cfg "pos36-srtp-app.exe"
        exec cfg ("." ++ "pos36-srtp-app.exe") ""

    [<Test>]
    let ``sigs pos37`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos37.dll --warnaserror" cfg.fsc_flags ["pos37.fs"]
        peverify cfg "pos37.dll"

    [<Test>]
    let ``sigs pos38`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:library -o:pos38.dll --warnaserror" cfg.fsc_flags ["pos38.fs"]
        peverify cfg "pos38.dll"

    [<Test>]
    let ``sigs pos39`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos39.exe" cfg.fsc_flags ["pos39.fs"]
        peverify cfg "pos39.exe"
        exec cfg ("." ++ "pos39.exe") ""

    [<Test>]
    let ``sigs pos23`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos23.exe" cfg.fsc_flags ["pos23.fs"]
        peverify cfg "pos23.exe"
        exec cfg ("." ++ "pos23.exe") ""

    [<Test>]
    let ``sigs pos20`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos20.exe" cfg.fsc_flags ["pos20.fs"]
        peverify cfg "pos20.exe"
        exec cfg ("." ++ "pos20.exe") ""

    [<Test>]
    let ``sigs pos19`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos19.exe" cfg.fsc_flags ["pos19.fs"]
        peverify cfg "pos19.exe"
        exec cfg ("." ++ "pos19.exe") ""

    [<Test>]
    let ``sigs pos18`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos18.exe" cfg.fsc_flags ["pos18.fs"]
        peverify cfg "pos18.exe"
        exec cfg ("." ++ "pos18.exe") ""

    [<Test>]
    let ``sigs pos16`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos16.exe" cfg.fsc_flags ["pos16.fs"]
        peverify cfg "pos16.exe"
        exec cfg ("." ++ "pos16.exe") ""

    [<Test>]
    let ``sigs pos17`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos17.exe" cfg.fsc_flags ["pos17.fs"]
        peverify cfg "pos17.exe"
        exec cfg ("." ++ "pos17.exe") ""

    [<Test>]
    let ``sigs pos15`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos15.exe" cfg.fsc_flags ["pos15.fs"]
        peverify cfg "pos15.exe"
        exec cfg ("." ++ "pos15.exe") ""

    [<Test>]
    let ``sigs pos14`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos14.exe" cfg.fsc_flags ["pos14.fs"]
        peverify cfg "pos14.exe"
        exec cfg ("." ++ "pos14.exe") ""

    [<Test>]
    let ``sigs pos13`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s --target:exe -o:pos13.exe" cfg.fsc_flags ["pos13.fs"]
        peverify cfg "pos13.exe"
        exec cfg ("." ++ "pos13.exe") ""

    [<Test>]
    let ``sigs pos12 `` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos12.dll" cfg.fsc_flags ["pos12.fs"]

    [<Test>]
    let ``sigs pos11`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos11.dll" cfg.fsc_flags ["pos11.fs"]

    [<Test>]
    let ``sigs pos10`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos10.dll" cfg.fsc_flags ["pos10.fs"]
        peverify cfg "pos10.dll"

    [<Test>]
    let ``sigs pos09`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos09.dll" cfg.fsc_flags ["pos09.fs"]
        peverify cfg "pos09.dll"

    [<Test>]
    let ``sigs pos07`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos07.dll" cfg.fsc_flags ["pos07.fs"]
        peverify cfg "pos07.dll"

    [<Test>]
    let ``sigs pos08`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos08.dll" cfg.fsc_flags ["pos08.fs"]
        peverify cfg "pos08.dll"

    [<Test>]
    let ``sigs pos06`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos06.dll" cfg.fsc_flags ["pos06.fs"]
        peverify cfg "pos06.dll"

    [<Test>]
    let ``sigs pos03`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos03.dll" cfg.fsc_flags ["pos03.fs"]
        peverify cfg "pos03.dll"
        fsc cfg "%s -a -o:pos03a.dll" cfg.fsc_flags ["pos03a.fsi"; "pos03a.fs"]
        peverify cfg "pos03a.dll"

    [<Test>]
    let ``sigs pos01a`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos01a.dll" cfg.fsc_flags ["pos01a.fsi"; "pos01a.fs"]
        peverify cfg "pos01a.dll"

    [<Test>]
    let ``sigs pos02`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos02.dll" cfg.fsc_flags ["pos02.fs"]
        peverify cfg "pos02.dll"

    [<Test>]
    let ``sigs pos05`` () =
        let cfg = testConfig "typecheck/sigs"
        fsc cfg "%s -a -o:pos05.dll" cfg.fsc_flags ["pos05.fs"]

    [<Test>]
    let ``type check neg01`` () = singleNegTest (testConfig "typecheck/sigs") "neg01"

    [<Test>]
    let ``type check neg02`` () = singleNegTest (testConfig "typecheck/sigs") "neg02"

    [<Test>]
    let ``type check neg03`` () = singleNegTest (testConfig "typecheck/sigs") "neg03"

    [<Test>]
    let ``type check neg04`` () = singleNegTest (testConfig "typecheck/sigs") "neg04"

    [<Test>]
    let ``type check neg05`` () = singleNegTest (testConfig "typecheck/sigs") "neg05"

    [<Test>]
    let ``type check neg06`` () = singleNegTest (testConfig "typecheck/sigs") "neg06"

    [<Test>]
    let ``type check neg06_a`` () = singleNegTest (testConfig "typecheck/sigs") "neg06_a"

    [<Test>]
    let ``type check neg06_b`` () = singleNegTest (testConfig "typecheck/sigs") "neg06_b"

    [<Test>]
    let ``type check neg07`` () = singleNegTest (testConfig "typecheck/sigs") "neg07"

    [<Test>]
    let ``type check neg08`` () = singleNegTest (testConfig "typecheck/sigs") "neg08"

    [<Test>]
    let ``type check neg09`` () = singleNegTest (testConfig "typecheck/sigs") "neg09"

    [<Test>]
    let ``type check neg10`` () = singleNegTest (testConfig "typecheck/sigs") "neg10"

    [<Test>]
    let ``type check neg10_a`` () = singleNegTest (testConfig "typecheck/sigs") "neg10_a"

    [<Test>]
    let ``type check neg11`` () = singleNegTest (testConfig "typecheck/sigs") "neg11"

    [<Test>]
    let ``type check neg12`` () = singleNegTest (testConfig "typecheck/sigs") "neg12"

    [<Test>]
    let ``type check neg13`` () = singleNegTest (testConfig "typecheck/sigs") "neg13"

    [<Test>]
    let ``type check neg14`` () = singleNegTest (testConfig "typecheck/sigs") "neg14"

    [<Test>]
    let ``type check neg15`` () = singleNegTest (testConfig "typecheck/sigs") "neg15"

    [<Test>]
    let ``type check neg16`` () = singleNegTest (testConfig "typecheck/sigs") "neg16"

    [<Test>]
    let ``type check neg17`` () = singleNegTest (testConfig "typecheck/sigs") "neg17"

    [<Test>]
    let ``type check neg18`` () = singleNegTest (testConfig "typecheck/sigs") "neg18"

    [<Test>]
    let ``type check neg19`` () = singleNegTest (testConfig "typecheck/sigs") "neg19"

    [<Test>]
    let ``type check neg20`` () = singleNegTest (testConfig "typecheck/sigs") "neg20"

    [<Test>]
    let ``type check neg21`` () = singleNegTest (testConfig "typecheck/sigs") "neg21"

    [<Test>]
    let ``type check neg22`` () = singleNegTest (testConfig "typecheck/sigs") "neg22"

    [<Test>]
    let ``type check neg23`` () = singleNegTest (testConfig "typecheck/sigs") "neg23"

    [<Test>]
    let ``type check neg24 version 4.6`` () =
        let cfg = testConfig "typecheck/sigs/version46"
        // For some reason this warning is off by default in the test framework but in this case we are testing for it
        let cfg = { cfg with fsc_flags = cfg.fsc_flags.Replace("--nowarn:20", "") }
        singleVersionedNegTest cfg "4.6" "neg24"

    [<Test>]
    let ``type check neg24 version 4.7`` () =
        let cfg = testConfig "typecheck/sigs/version47"
        // For some reason this warning is off by default in the test framework but in this case we are testing for it
        let cfg = { cfg with fsc_flags = cfg.fsc_flags.Replace("--nowarn:20", "") }
        singleVersionedNegTest cfg "preview" "neg24"

    [<Test>]
    let ``type check neg25`` () = singleNegTest (testConfig "typecheck/sigs") "neg25"

    [<Test>]
    let ``type check neg26`` () = singleNegTest (testConfig "typecheck/sigs") "neg26"

    [<Test>]
    let ``type check neg27`` () = singleNegTest (testConfig "typecheck/sigs") "neg27"

    [<Test>]
    let ``type check neg28`` () = singleNegTest (testConfig "typecheck/sigs") "neg28"

    [<Test>]
    let ``type check neg29`` () = singleNegTest (testConfig "typecheck/sigs") "neg29"

    [<Test>]
    let ``type check neg30`` () = singleNegTest (testConfig "typecheck/sigs") "neg30"

    [<Test>]
    let ``type check neg31`` () = singleNegTest (testConfig "typecheck/sigs") "neg31"

    [<Test>]
    let ``type check neg32`` () = singleNegTest (testConfig "typecheck/sigs") "neg32"

    [<Test>]
    let ``type check neg33`` () = singleNegTest (testConfig "typecheck/sigs") "neg33"

    [<Test>]
    let ``type check neg34`` () = singleNegTest (testConfig "typecheck/sigs") "neg34"

    [<Test>]
    let ``type check neg35`` () = singleNegTest (testConfig "typecheck/sigs") "neg35"

    [<Test>]
    let ``type check neg36`` () = singleNegTest (testConfig "typecheck/sigs") "neg36"

    [<Test>]
    let ``type check neg37`` () = singleNegTest (testConfig "typecheck/sigs") "neg37"

    [<Test>]
    let ``type check neg37_a`` () = singleNegTest (testConfig "typecheck/sigs") "neg37_a"

    [<Test>]
    let ``type check neg38`` () = singleNegTest (testConfig "typecheck/sigs") "neg38"

    [<Test>]
    let ``type check neg39`` () = singleNegTest (testConfig "typecheck/sigs") "neg39"

    [<Test>]
    let ``type check neg40`` () = singleNegTest (testConfig "typecheck/sigs") "neg40"

    [<Test>]
    let ``type check neg41`` () = singleNegTest (testConfig "typecheck/sigs") "neg41"

    [<Test>]
    let ``type check neg42`` () = singleNegTest (testConfig "typecheck/sigs") "neg42"

    [<Test>]
    let ``type check neg43`` () = singleNegTest (testConfig "typecheck/sigs") "neg43"

    [<Test>]
    let ``type check neg44`` () = singleNegTest (testConfig "typecheck/sigs") "neg44"

    [<Test>]
    let ``type check neg45`` () = singleNegTest (testConfig "typecheck/sigs") "neg45"

    [<Test>]
    let ``type check neg46`` () = singleNegTest (testConfig "typecheck/sigs") "neg46"

    [<Test>]
    let ``type check neg47`` () = singleNegTest (testConfig "typecheck/sigs") "neg47"

    [<Test>]
    let ``type check neg48`` () = singleNegTest (testConfig "typecheck/sigs") "neg48"

    [<Test>]
    let ``type check neg49`` () = singleNegTest (testConfig "typecheck/sigs") "neg49"

    [<Test>]
    let ``type check neg50`` () = singleNegTest (testConfig "typecheck/sigs") "neg50"

    [<Test>]
    let ``type check neg51`` () = singleNegTest (testConfig "typecheck/sigs") "neg51"

    [<Test>]
    let ``type check neg52`` () = singleNegTest (testConfig "typecheck/sigs") "neg52"

    [<Test>]
    let ``type check neg53`` () = singleNegTest (testConfig "typecheck/sigs") "neg53"

    [<Test>]
    let ``type check neg54`` () = singleNegTest (testConfig "typecheck/sigs") "neg54"

    [<Test>]
    let ``type check neg55`` () = singleNegTest (testConfig "typecheck/sigs") "neg55"

    [<Test>]
    let ``type check neg56`` () = singleNegTest (testConfig "typecheck/sigs") "neg56"

    [<Test>]
    let ``type check neg56_a`` () = singleNegTest (testConfig "typecheck/sigs") "neg56_a"

    [<Test>]
    let ``type check neg56_b`` () = singleNegTest (testConfig "typecheck/sigs") "neg56_b"

    [<Test>]
    let ``type check neg57`` () = singleNegTest (testConfig "typecheck/sigs") "neg57"

    [<Test>]
    let ``type check neg58`` () = singleNegTest (testConfig "typecheck/sigs") "neg58"

    [<Test>]
    let ``type check neg59`` () = singleNegTest (testConfig "typecheck/sigs") "neg59"

    [<Test>]
    let ``type check neg60`` () = singleNegTest (testConfig "typecheck/sigs") "neg60"

    [<Test>]
    let ``type check neg61`` () = singleNegTest (testConfig "typecheck/sigs") "neg61"

    [<Test>]
    let ``type check neg62`` () = singleNegTest (testConfig "typecheck/sigs") "neg62"

    [<Test>]
    let ``type check neg63`` () = singleNegTest (testConfig "typecheck/sigs") "neg63"

    [<Test>]
    let ``type check neg64`` () = singleNegTest (testConfig "typecheck/sigs") "neg64"

    [<Test>]
    let ``type check neg65`` () = singleNegTest (testConfig "typecheck/sigs") "neg65"

    [<Test>]
    let ``type check neg66`` () = singleNegTest (testConfig "typecheck/sigs") "neg66"

    [<Test>]
    let ``type check neg67`` () = singleNegTest (testConfig "typecheck/sigs") "neg67"

    [<Test>]
    let ``type check neg68`` () = singleNegTest (testConfig "typecheck/sigs") "neg68"

    [<Test>]
    let ``type check neg69`` () = singleNegTest (testConfig "typecheck/sigs") "neg69"

    [<Test>]
    let ``type check neg70`` () = singleNegTest (testConfig "typecheck/sigs") "neg70"

    [<Test>]
    let ``type check neg71`` () = singleNegTest (testConfig "typecheck/sigs") "neg71"

    [<Test>]
    let ``type check neg72`` () = singleNegTest (testConfig "typecheck/sigs") "neg72"

    [<Test>]
    let ``type check neg73`` () = singleNegTest (testConfig "typecheck/sigs") "neg73"

    [<Test>]
    let ``type check neg74`` () = singleNegTest (testConfig "typecheck/sigs") "neg74"

    [<Test>]
    let ``type check neg75`` () = singleNegTest (testConfig "typecheck/sigs") "neg75"

    [<Test>]
    let ``type check neg76`` () = singleNegTest (testConfig "typecheck/sigs") "neg76"

    [<Test>]
    let ``type check neg77`` () = singleNegTest (testConfig "typecheck/sigs") "neg77"

    [<Test>]
    let ``type check neg78`` () = singleNegTest (testConfig "typecheck/sigs") "neg78"

    [<Test>]
    let ``type check neg79`` () = singleNegTest (testConfig "typecheck/sigs") "neg79"

    [<Test>]
    let ``type check neg80`` () = singleNegTest (testConfig "typecheck/sigs") "neg80"

    [<Test>]
    let ``type check neg81`` () = singleNegTest (testConfig "typecheck/sigs") "neg81"

    [<Test>]
    let ``type check neg82`` () = singleNegTest (testConfig "typecheck/sigs") "neg82"

    [<Test>]
    let ``type check neg83`` () = singleNegTest (testConfig "typecheck/sigs") "neg83"

    [<Test>]
    let ``type check neg84`` () = singleNegTest (testConfig "typecheck/sigs") "neg84"

    [<Test>]
    let ``type check neg85`` () = singleNegTest (testConfig "typecheck/sigs") "neg85"

    [<Test>]
    let ``type check neg86`` () = singleNegTest (testConfig "typecheck/sigs") "neg86"

    [<Test>]
    let ``type check neg87`` () = singleNegTest (testConfig "typecheck/sigs") "neg87"

    [<Test>]
    let ``type check neg88`` () = singleNegTest (testConfig "typecheck/sigs") "neg88"

    [<Test>]
    let ``type check neg89`` () = singleNegTest (testConfig "typecheck/sigs") "neg89"

    [<Test>]
    let ``type check neg90`` () = singleNegTest (testConfig "typecheck/sigs") "neg90"

    [<Test>]
    let ``type check neg91`` () = singleNegTest (testConfig "typecheck/sigs") "neg91"

    [<Test>]
    let ``type check neg92`` () = singleNegTest (testConfig "typecheck/sigs") "neg92"

    [<Test>]
    let ``type check neg93`` () = singleNegTest (testConfig "typecheck/sigs") "neg93"

    [<Test>]
    let ``type check neg94`` () = singleNegTest (testConfig "typecheck/sigs") "neg94"

    [<Test>]
    let ``type check neg95`` () = singleNegTest (testConfig "typecheck/sigs") "neg95"

    [<Test>]
    let ``type check neg96`` () = singleNegTest (testConfig "typecheck/sigs") "neg96"

    [<Test>]
    let ``type check neg97`` () = singleNegTest (testConfig "typecheck/sigs") "neg97"

    [<Test>]
    let ``type check neg98`` () = singleNegTest (testConfig "typecheck/sigs") "neg98"

    [<Test>]
    let ``type check neg99`` () = singleNegTest (testConfig "typecheck/sigs") "neg99"

    [<Test>]
    let ``type check neg100`` () =
        let cfg = testConfig "typecheck/sigs"
        let cfg = { cfg with fsc_flags = cfg.fsc_flags + " --warnon:3218" }
        singleNegTest cfg "neg100"

    [<Test>]
    let ``type check neg101`` () = singleNegTest (testConfig "typecheck/sigs") "neg101"

    [<Test>]
    let ``type check neg102`` () = singleNegTest (testConfig "typecheck/sigs") "neg102"

    [<Test>]
    let ``type check neg103`` () = singleNegTest (testConfig "typecheck/sigs") "neg103"

    [<Test>]
    let ``type check neg104`` () = singleNegTest (testConfig "typecheck/sigs") "neg104"

    [<Test>]
    let ``type check neg106`` () = singleNegTest (testConfig "typecheck/sigs") "neg106"

    [<Test>]
    let ``type check neg107`` () = singleNegTest (testConfig "typecheck/sigs") "neg107"

    [<Test>]
    let ``type check neg108`` () = singleNegTest (testConfig "typecheck/sigs") "neg108"

    [<Test>]
    let ``type check neg109`` () = singleNegTest (testConfig "typecheck/sigs") "neg109"

    [<Test>]
    let ``type check neg110`` () = singleNegTest (testConfig "typecheck/sigs") "neg110"

    [<Test>]
    let ``type check neg111`` () = singleNegTest (testConfig "typecheck/sigs") "neg111"

    [<Test>] 
    let ``type check neg112`` () = singleNegTest (testConfig "typecheck/sigs") "neg112"
    
    [<Test>]
    let ``type check neg113`` () = singleNegTest (testConfig "typecheck/sigs") "neg113"

    [<Test>]
    let ``type check neg114`` () = singleNegTest (testConfig "typecheck/sigs") "neg114"

    [<Test>]
    let ``type check neg115`` () = singleNegTest (testConfig "typecheck/sigs") "neg115"

    [<Test>]
    let ``type check neg116`` () = singleNegTest (testConfig "typecheck/sigs") "neg116"

    [<Test>]
    let ``type check neg117`` () = singleNegTest (testConfig "typecheck/sigs") "neg117"

    [<Test>]
    let ``type check neg118`` () = singleNegTest (testConfig "typecheck/sigs") "neg118"

    [<Test>]
    let ``type check neg119`` () = singleNegTest (testConfig "typecheck/sigs") "neg119"

    [<Test>]
    let ``type check neg120`` () = singleNegTest (testConfig "typecheck/sigs") "neg120"

    [<Test>]
    let ``type check neg121`` () = singleNegTest (testConfig "typecheck/sigs") "neg121"

    [<Test>]
    let ``type check neg122`` () = singleNegTest (testConfig "typecheck/sigs") "neg122"

    [<Test>]
    let ``type check neg123`` () = singleNegTest (testConfig "typecheck/sigs") "neg123"

    [<Test>]
    let ``type check neg124`` () = singleNegTest (testConfig "typecheck/sigs") "neg124"

    [<Test>]
    let ``type check neg125`` () = singleNegTest (testConfig "typecheck/sigs") "neg125"

    [<Test>]
    let ``type check neg126`` () = singleNegTest (testConfig "typecheck/sigs") "neg126"

    [<Test>]
    let ``type check neg127`` () = singleNegTest (testConfig "typecheck/sigs") "neg127"

    [<Test>]
    let ``type check neg128`` () = singleNegTest (testConfig "typecheck/sigs") "neg128"

    [<Test>]
    let ``type check neg129`` () = singleNegTest (testConfig "typecheck/sigs") "neg129"

    [<Test>]
    let ``type check neg130`` () = singleNegTest (testConfig "typecheck/sigs") "neg130"

    [<Test>]
    let ``type check neg_anon_1`` () = singleNegTest (testConfig "typecheck/sigs") "neg_anon_1"

    [<Test>]
    let ``type check neg_anon_2`` () = singleNegTest (testConfig "typecheck/sigs") "neg_anon_2"

    [<Test>]
    let ``type check neg_issue_3752`` () = singleNegTest (testConfig "typecheck/sigs") "neg_issue_3752"

    [<Test>]
    let ``type check neg_byref_1`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_1"

    [<Test>]
    let ``type check neg_byref_2`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_2"

    [<Test>]
    let ``type check neg_byref_3`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_3"

    [<Test>]
    let ``type check neg_byref_4`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_4"

    [<Test>]
    let ``type check neg_byref_5`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_5"

    [<Test>]
    let ``type check neg_byref_6`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_6"

    [<Test>]
    let ``type check neg_byref_7`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_7"

    [<Test>]
    let ``type check neg_byref_8`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_8"

    [<Test>]
    let ``type check neg_byref_10`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_10"

    [<Test>]
    let ``type check neg_byref_11`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_11"

    [<Test>]
    let ``type check neg_byref_12`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_12"

    [<Test>]
    let ``type check neg_byref_13`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_13"

    [<Test>]
    let ``type check neg_byref_14`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_14"

    [<Test>]
    let ``type check neg_byref_15`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_15"

    [<Test>]
    let ``type check neg_byref_16`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_16"

    [<Test>]
    let ``type check neg_byref_17`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_17"

    [<Test>]
    let ``type check neg_byref_18`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_18"

    [<Test>]
    let ``type check neg_byref_19`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_19"

    [<Test>]
    let ``type check neg_byref_20`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_20"

    [<Test>]
    let ``type check neg_byref_21`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_21"

    [<Test>]
    let ``type check neg_byref_22`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_22"

    [<Test>]
    let ``type check neg_byref_23`` () = singleNegTest (testConfig "typecheck/sigs") "neg_byref_23"

[<NonParallelizable>]
module FscTests =
    [<Test>]
    let ``should be raised if AssemblyInformationalVersion has invalid version`` () =
        let cfg = testConfig (Commands.createTempDir())

        let code  =
            """
namespace CST.RI.Anshun
open System.Reflection
[<assembly: AssemblyVersion("4.5.6.7")>]
[<assembly: AssemblyInformationalVersion("45.2048.main1.2-hotfix (upgrade Second Chance security)")>]
()
            """

        File.WriteAllText(cfg.Directory ++ "test.fs", code)

        fsc cfg "%s --nologo -o:lib.dll --target:library" cfg.fsc_flags ["test.fs"]

        let fv = Diagnostics.FileVersionInfo.GetVersionInfo(Commands.getfullpath cfg.Directory "lib.dll")

        fv.ProductVersion |> Assert.areEqual "45.2048.main1.2-hotfix (upgrade Second Chance security)"

        (fv.ProductMajorPart, fv.ProductMinorPart, fv.ProductBuildPart, fv.ProductPrivatePart)
        |> Assert.areEqual (45, 2048, 0, 2)


    [<Test>]
    let ``should set file version info on generated file`` () =
        let cfg = testConfig (Commands.createTempDir())

        let code =
            """
namespace CST.RI.Anshun
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
[<assembly: AssemblyTitle("CST.RI.Anshun.TreloarStation")>]
[<assembly: AssemblyDescription("Assembly is a part of Restricted Intelligence of Anshun planet")>]
[<assembly: AssemblyConfiguration("RELEASE")>]
[<assembly: AssemblyCompany("Compressed Space Transport")>]
[<assembly: AssemblyProduct("CST.RI.Anshun")>]
[<assembly: AssemblyCopyright("Copyright \u00A9 Compressed Space Transport 2380")>]
[<assembly: AssemblyTrademark("CST \u2122")>]
[<assembly: AssemblyVersion("12.34.56.78")>]
[<assembly: AssemblyFileVersion("99.88.77.66")>]
[<assembly: AssemblyInformationalVersion("17.56.2912.14")>]
()
            """

        File.WriteAllText(cfg.Directory ++ "test.fs", code)

        do fsc cfg "%s --nologo -o:lib.dll --target:library" cfg.fsc_flags ["test.fs"]

        let fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(Commands.getfullpath cfg.Directory "lib.dll")
        fv.CompanyName |> Assert.areEqual "Compressed Space Transport"
        fv.FileVersion |> Assert.areEqual "99.88.77.66"

        (fv.FileMajorPart, fv.FileMinorPart, fv.FileBuildPart, fv.FilePrivatePart)
        |> Assert.areEqual (99,88,77,66)

        fv.ProductVersion |> Assert.areEqual "17.56.2912.14"
        (fv.ProductMajorPart, fv.ProductMinorPart, fv.ProductBuildPart, fv.ProductPrivatePart)
        |> Assert.areEqual (17,56,2912,14)

        fv.LegalCopyright |> Assert.areEqual "Copyright \u00A9 Compressed Space Transport 2380"
        fv.LegalTrademarks |> Assert.areEqual "CST \u2122"
#endif

#if NET472
[<NonParallelizable>]
module ProductVersionTest =

    let informationalVersionAttrName = typeof<System.Reflection.AssemblyInformationalVersionAttribute>.FullName
    let fileVersionAttrName = typeof<System.Reflection.AssemblyFileVersionAttribute>.FullName

    let fallbackTestData () =
        let defAssemblyVersion = (1us,2us,3us,4us)
        let defAssemblyVersionString = let v1,v2,v3,v4 = defAssemblyVersion in sprintf "%d.%d.%d.%d" v1 v2 v3 v4
        [ defAssemblyVersionString, None, None, defAssemblyVersionString
          defAssemblyVersionString, (Some "5.6.7.8"), None, "5.6.7.8"
          defAssemblyVersionString, (Some "5.6.7.8" ), (Some "22.44.66.88"), "22.44.66.88"
          defAssemblyVersionString, None, (Some "22.44.66.88" ), "22.44.66.88" ]
        |> List.map (fun (a,f,i,e) -> (a, f, i, e))

    [<Test>]
    let ``should use correct fallback``() =

       for (assemblyVersion, fileVersion, infoVersion, expected) in fallbackTestData () do
        let cfg = testConfig (Commands.createTempDir())
        let dir = cfg.Directory

        printfn "Directory: %s" dir

        let code =
            let globalAssembly (attr: Type) attrValue =
                sprintf """[<assembly: %s("%s")>]""" attr.FullName attrValue

            let attrs =
                [ assemblyVersion |> (globalAssembly typeof<AssemblyVersionAttribute> >> Some)
                  fileVersion |> Option.map (globalAssembly typeof<AssemblyFileVersionAttribute>)
                  infoVersion |> Option.map (globalAssembly typeof<AssemblyInformationalVersionAttribute>) ]
                |> List.choose id

            sprintf """
namespace CST.RI.Anshun
%s
()
            """ (attrs |> String.concat Environment.NewLine)

        File.WriteAllText(cfg.Directory ++ "test.fs", code)

        fsc cfg "%s --nologo -o:lib.dll --target:library" cfg.fsc_flags ["test.fs"]

        let fileVersionInfo = Diagnostics.FileVersionInfo.GetVersionInfo(Commands.getfullpath cfg.Directory "lib.dll")

        fileVersionInfo.ProductVersion |> Assert.areEqual expected

module GeneratedSignatureTests =
    [<Test>]
    let ``members-basics-GENERATED_SIGNATURE`` () = singleTestBuildAndRun "core/members/basics" GENERATED_SIGNATURE

    [<Test; Ignore("Flaky w.r.t. PEVerify.  https://github.com/Microsoft/visualfsharp/issues/2616")>]
    let ``access-GENERATED_SIGNATURE``() = singleTestBuildAndRun "core/access" GENERATED_SIGNATURE

    [<Test>]
    let ``array-GENERATED_SIGNATURE``() = singleTestBuildAndRun "core/array" GENERATED_SIGNATURE

    [<Test; Ignore("incorrect signature file generated, test has been disabled a long time")>]
    let ``genericmeasures-GENERATED_SIGNATURE`` () = singleTestBuildAndRun "core/genericmeasures" GENERATED_SIGNATURE

    [<Test>]
    let ``innerpoly-GENERATED_SIGNATURE`` () = singleTestBuildAndRun "core/innerpoly" GENERATED_SIGNATURE

    [<Test>]
    let ``measures-GENERATED_SIGNATURE`` () = singleTestBuildAndRun "core/measures" GENERATED_SIGNATURE
#endif

#if !NETCOREAPP
[<NonParallelizable>]
module OverloadResolution =
    module ``fsharpqa migrated tests`` =
        let [<Test>] ``Conformance\Expressions\SyntacticSugar (E_Slices01.fs)`` () = singleNegTest (testConfig "conformance/expressions/syntacticsugar") "E_Slices01"
        let [<Test>] ``Conformance\Expressions\Type-relatedExpressions (E_RigidTypeAnnotation03.fsx)`` () = singleNegTest (testConfig "conformance/expressions/type-relatedexpressions") "E_RigidTypeAnnotation03"
        let [<Test>] ``Conformance\Inference (E_OneTypeVariable03.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_OneTypeVariable03"
        let [<Test>] ``Conformance\Inference (E_OneTypeVariable03rec.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_OneTypeVariable03rec"
        let [<Test>] ``Conformance\Inference (E_TwoDifferentTypeVariablesGen00.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoDifferentTypeVariablesGen00"
        let [<Test>] ``Conformance\Inference (E_TwoDifferentTypeVariables01.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoDifferentTypeVariables01"
        let [<Test>] ``Conformance\Inference (E_TwoDifferentTypeVariables01rec.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoDifferentTypeVariables01rec"
        let [<Test>] ``Conformance\Inference (E_TwoDifferentTypeVariablesGen00rec.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoDifferentTypeVariablesGen00rec"
        let [<Test>] ``Conformance\Inference (E_TwoEqualTypeVariables02.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoEqualTypeVariables02"
        let [<Test>] ``Conformance\Inference (E_TwoEqualYypeVariables02rec.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_TwoEqualYypeVariables02rec"
        let [<Test>] ``Conformance\Inference (E_LeftToRightOverloadResolution01.fs)`` () = singleNegTest (testConfig "conformance/inference") "E_LeftToRightOverloadResolution01"
        let [<Test>] ``Conformance\WellFormedness (E_Clashing_Values_in_AbstractClass01.fs)`` () = singleNegTest (testConfig "conformance/wellformedness") "E_Clashing_Values_in_AbstractClass01"
        let [<Test>] ``Conformance\WellFormedness (E_Clashing_Values_in_AbstractClass03.fs)`` () = singleNegTest (testConfig "conformance/wellformedness") "E_Clashing_Values_in_AbstractClass03"
        let [<Test>] ``Conformance\WellFormedness (E_Clashing_Values_in_AbstractClass04.fs)`` () = singleNegTest (testConfig "conformance/wellformedness") "E_Clashing_Values_in_AbstractClass04"
        // note: this test still exist in fsharpqa to assert the compiler doesn't crash
        // the part of the code generating a flaky error due to https://github.com/dotnet/fsharp/issues/6725
        // is elided here to focus on overload resolution error messages
        let [<Test>] ``Conformance\LexicalAnalysis\SymbolicOperators (E_LessThanDotOpenParen001.fs)`` () = singleNegTest (testConfig "conformance/lexicalanalysis") "E_LessThanDotOpenParen001"

    module ``error messages using BCL``=
        let [<Test>] ``neg_System.Convert.ToString.OverloadList``() = singleNegTest (testConfig "typecheck/overloads") "neg_System.Convert.ToString.OverloadList"
        let [<Test>] ``neg_System.Threading.Tasks.Task.Run.OverloadList``() = singleNegTest (testConfig "typecheck/overloads") "neg_System.Threading.Tasks.Task.Run.OverloadList"
        let [<Test>] ``neg_System.Drawing.Graphics.DrawRectangleOverloadList.fsx``() = singleNegTest (testConfig "typecheck/overloads") "neg_System.Drawing.Graphics.DrawRectangleOverloadList"

    module ``ad hoc code overload error messages``=
        let [<Test>] ``neg_many_many_overloads`` () = singleNegTest (testConfig "typecheck/overloads") "neg_many_many_overloads"
        let [<Test>] ``neg_interface_generics`` () = singleNegTest (testConfig "typecheck/overloads") "neg_interface_generics"
        let [<Test>] ``neg_known_return_type_and_known_type_arguments`` () = singleNegTest (testConfig "typecheck/overloads") "neg_known_return_type_and_known_type_arguments"
        let [<Test>] ``neg_generic_known_argument_types`` () = singleNegTest (testConfig "typecheck/overloads") "neg_generic_known_argument_types"
        let [<Test>] ``neg_tupled_arguments`` () = singleNegTest (testConfig "typecheck/overloads") "neg_tupled_arguments"
#endif
