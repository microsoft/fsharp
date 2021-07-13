// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Tests.Compiler.Watson

#nowarn "52" // The value has been copied to ensure the original is not mutated

open FSharp.Compiler
open FSharp.Compiler.IO
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CodeAnalysis
open Internal.Utilities.Library 
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.Driver
open NUnit.Framework
open System.IO

type Check = 
    static member public FscLevelException<'TException when 'TException :> exn>(simulationCode)  =
        try 
            try
#if DEBUG
                FSharp.Compiler.CompilerDiagnostics.CompilerService.showAssertForUnexpectedException := false
#endif
                if (FileSystem.FileExistsShim("watson-test.fs")) then
                    FileSystem.FileDeleteShim("watson-test.fs")
                FileSystem.OpenFileForWriteShim("watson-test.fs").Write("// Hello watson" )
                let argv =
                    [|  "--simulateException:"+simulationCode
                        "--nowarn:988" // don't show `watson-test.fs(1,16): warning FS0988: Main module of program is empty: nothing will happen when it is run`
                        "watson-test.fs"
                    |]

                let ctok = AssumeCompilationThreadWithoutEvidence ()
                let _code = mainCompile (ctok, argv, LegacyMSBuildReferenceResolver.getResolver(), false, ReduceMemoryFlag.No, CopyFSharpCoreFlag.No, FSharp.Compiler.ErrorLogger.QuitProcessExiter, ConsoleLoggerProvider(), None, None)
                ()
            with 
            | :? 'TException as e -> 
                let msg = e.ToString();
                if msg.Contains("ReportTime") || msg.Contains("TypeCheckOneInput") then ()
                else
                    printfn "%s" msg
                    Assert.Fail("The correct callstack was not reported to watson.")
            | (FSharp.Compiler.ErrorLogger.ReportedError (Some (FSharp.Compiler.ErrorLogger.InternalError (msg, range) as e)))
            | (FSharp.Compiler.ErrorLogger.InternalError (msg, range) as e) -> 
                printfn "InternalError Exception: %s, range = %A, stack = %s" msg range (e.ToString())
                Assert.Fail("An InternalError exception occurred.")
        finally               
#if DEBUG
            FSharp.Compiler.CompilerDiagnostics.CompilerService.showAssertForUnexpectedException := true 
#endif
        FileSystem.FileDeleteShim("watson-test.fs")


[<TestFixture>] 
module WatsonTests = 

    [<Test>]
    let FscOutOfMemory() = Check.FscLevelException<System.OutOfMemoryException>("fsc-oom")

    [<Test>]
    let FscArgumentNull() = Check.FscLevelException<System.ArgumentNullException>("fsc-an")        
    
    [<Test>]
    let FscInvalidOperation() = Check.FscLevelException<System.InvalidOperationException>("fsc-invop")        

// As of .NET 4.0 some exception types cannot be caught. As a result, we cannot test this case. I did visually confirm a Watson report is sent, though.
//    [<Test>]
//    let FscAccessViolation() = Check.FscLevelException<System.AccessViolationException>("fsc-ac")        

    [<Test>]
    let FscArgumentOutOfRange() = Check.FscLevelException<System.ArgumentOutOfRangeException>("fsc-aor")        

    [<Test>]
    let FscDivideByZero() = Check.FscLevelException<System.DivideByZeroException>("fsc-dv0")        

    [<Test>]
    let FscNotFiniteNumber() = Check.FscLevelException<System.NotFiniteNumberException>("fsc-nfn")        

    [<Test>]
    let FscOverflow() = Check.FscLevelException<System.OverflowException>("fsc-oe")        

    [<Test>]
    let FscArrayTypeMismatch() = Check.FscLevelException<System.ArrayTypeMismatchException>("fsc-atmm")        

    [<Test>]
    let FscBadImageFormat() = Check.FscLevelException<System.BadImageFormatException>("fsc-bif")        

    [<Test>]
    let FscKeyNotFound() = Check.FscLevelException<System.Collections.Generic.KeyNotFoundException>("fsc-knf")        

    [<Test>]
    let FscIndexOutOfRange() = Check.FscLevelException<System.IndexOutOfRangeException>("fsc-ior")        

    [<Test>]
    let FscInvalidCast() = Check.FscLevelException<System.InvalidCastException>("fsc-ic")        

    [<Test>]
    let FscInvalidProgram() = Check.FscLevelException<System.InvalidProgramException>("fsc-ip")        

    [<Test>]
    let FscMemberAccess() = Check.FscLevelException<System.MemberAccessException>("fsc-ma")        

    [<Test>]
    let FscNotImplemented() = Check.FscLevelException<System.NotImplementedException>("fsc-ni")        

    [<Test>]
    let FscNullReference() = Check.FscLevelException<System.NullReferenceException>("fsc-nr")        

    [<Test>]
    let FscOperationCancelled() = Check.FscLevelException<System.OperationCanceledException>("fsc-oc")        
    
    //[<Test>]
    //let FscFailure() = Check.FscLevelException<Microsoft.FSharp.Core.FailureException>("fsc-fail")            
      
    [<Test>]
    let TypeCheckOutOfMemory() = Check.FscLevelException<System.OutOfMemoryException>("tc-oom")

    [<Test>]
    let TypeCheckArgumentNull() = Check.FscLevelException<System.ArgumentNullException>("tc-an")        
    
    [<Test>]
    let TypeCheckInvalidOperation() = Check.FscLevelException<System.InvalidOperationException>("tc-invop")        

// As of .NET 4.0 some exception types cannot be caught. As a result, we cannot test this case. I did visually confirm a Watson report is sent, though.
//    [<Test>]
//    let TypeCheckAccessViolation() = Check.FscLevelException<System.AccessViolationException>("tc-ac")        

    [<Test>]
    let TypeCheckArgumentOutOfRange() = Check.FscLevelException<System.ArgumentOutOfRangeException>("tc-aor")        

    [<Test>]
    let TypeCheckDivideByZero() = Check.FscLevelException<System.DivideByZeroException>("tc-dv0")        

    [<Test>]
    let TypeCheckNotFiniteNumber() = Check.FscLevelException<System.NotFiniteNumberException>("tc-nfn")        

    [<Test>]
    let TypeCheckOverflow() = Check.FscLevelException<System.OverflowException>("tc-oe")        

    [<Test>]
    let TypeCheckArrayTypeMismatch() = Check.FscLevelException<System.ArrayTypeMismatchException>("tc-atmm")        

    [<Test>]
    let TypeCheckBadImageFormat() = Check.FscLevelException<System.BadImageFormatException>("tc-bif")        

    [<Test>]
    let TypeCheckKeyNotFound() = Check.FscLevelException<System.Collections.Generic.KeyNotFoundException>("tc-knf")        

    [<Test>]
    let TypeCheckIndexOutOfRange() = Check.FscLevelException<System.IndexOutOfRangeException>("tc-ior")        

    [<Test>]
    let TypeCheckInvalidCast() = Check.FscLevelException<System.InvalidCastException>("tc-ic")        

    [<Test>]
    let TypeCheckInvalidProgram() = Check.FscLevelException<System.InvalidProgramException>("tc-ip")        

    [<Test>]
    let TypeCheckMemberAccess() = Check.FscLevelException<System.MemberAccessException>("tc-ma")        

    [<Test>]
    let TypeCheckNotImplemented() = Check.FscLevelException<System.NotImplementedException>("tc-ni")        

    [<Test>]
    let TypeCheckNullReference() = Check.FscLevelException<System.NullReferenceException>("tc-nr")        

    [<Test>]
    let TypeCheckOperationCancelled() = Check.FscLevelException<System.OperationCanceledException>("tc-oc")        

    [<Test>]
    let TypeCheckFailure() = Check.FscLevelException<System.Exception>("tc-fail")            

