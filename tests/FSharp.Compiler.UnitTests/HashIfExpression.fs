// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.UnitTests

open System
open System.Text

open Xunit
open FSharp.Test.Utilities

open Internal.Utilities
open Internal.Utilities.Text.Lexing

open FSharp.Compiler
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Lexer
open FSharp.Compiler.Lexhelp
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Features
open FSharp.Compiler.ParseHelpers
open FSharp.Compiler.Syntax

type public HashIfExpression() =
    let preludes    = [|"#if "; "#elif "|]
    let epilogues   = [|""; " // Testing"|]

    let ONE         = IfdefId "ONE"
    let TWO         = IfdefId "TWO"
    let THREE       = IfdefId "THREE"

    let isSet l r = (l &&& r) <> 0

    let (!!)  e     = IfdefNot(e)
    let (&&&) l r   = IfdefAnd(l,r)
    let (|||) l r   = IfdefOr(l,r)


    let exprAsString (e : LexerIfdefExpression) : string =
        let sb                  = StringBuilder()
        let append (s : string) = ignore <| sb.Append s
        let rec build (e : LexerIfdefExpression) : unit =
            match e with
            | IfdefAnd (l,r)-> append "("; build l; append " && "; build r; append ")"
            | IfdefOr (l,r) -> append "("; build l; append " || "; build r; append ")"
            | IfdefNot ee   -> append "!"; build ee
            | IfdefId nm    -> append nm

        build e

        sb.ToString ()

    let createParser () =
        let errors = ResizeArray<PhasedDiagnostic>()
        let warnings = ResizeArray<PhasedDiagnostic>()

        let errorLogger =
            {
                new ErrorLogger("TestErrorLogger") with
                    member x.DiagnosticSink(e, sev)    = if sev = FSharpDiagnosticSeverity.Error then errors.Add e else warnings.Add e
                    member x.ErrorCount         = errors.Count
            }

        let lightSyntax = LightSyntaxStatus(true, false)
        let resourceManager = LexResourceManager ()
        let defines= []
        let startPos = Position.Empty
        let args = mkLexargs (defines, lightSyntax, resourceManager, [], errorLogger, PathMap.empty)

        CompileThreadStatic.ErrorLogger <- errorLogger

        let parser (s : string) =
            let isFeatureSupported (_featureId:LanguageFeature) = true
            let checkLanguageFeatureErrorRecover _featureId _range = ()
            let lexbuf          = LexBuffer<char>.FromChars (true, isFeatureSupported, checkLanguageFeatureErrorRecover, s.ToCharArray ())
            lexbuf.StartPos     <- startPos
            lexbuf.EndPos       <- startPos
            let tokenStream     = PPLexer.tokenstream args

            PPParser.start tokenStream lexbuf

        errors, warnings, parser

    do // Setup
        CompileThreadStatic.BuildPhase  <- BuildPhase.Compile
    interface IDisposable with // Teardown
        member _.Dispose() =
            CompileThreadStatic.BuildPhase  <- BuildPhase.DefaultPhase
            CompileThreadStatic.ErrorLogger <- CompileThreadStatic.ErrorLogger

    [<Fact>]
    member this.PositiveParserTestCases()=

        let errors, warnings, parser = createParser ()

        let positiveTestCases =
            [|
                "ONE"                       , ONE
                "ONE//"                     , ONE
                "ONE // Comment"            , ONE
                "!ONE"                      , !!ONE
                "!!ONE"                     , !! (!!ONE)
                "DEBUG"                     , (IfdefId "DEBUG")
                "!DEBUG"                    , !! (IfdefId "DEBUG")
                "O_s1"                      , IfdefId "O_s1"
                "(ONE)"                     , (ONE)
                "ONE&&TWO"                  , ONE &&& TWO
                "ONE||TWO"                  , ONE ||| TWO
                "( ONE && TWO )"            , ONE &&& TWO
                "ONE && TWO && THREE"       , (ONE &&& TWO) &&& THREE
                "ONE || TWO || THREE"       , (ONE ||| TWO) ||| THREE
                "ONE || TWO && THREE"       , ONE ||| (TWO &&& THREE)
                "ONE && TWO || THREE"       , (ONE &&& TWO) ||| THREE
                "ONE || (TWO && THREE)"     , ONE ||| (TWO &&& THREE)
                "ONE && (TWO || THREE)"     , ONE &&& (TWO ||| THREE)
                "!ONE || TWO && THREE"      , (!!ONE) ||| (TWO &&& THREE)
                "ONE && !TWO || THREE"      , (ONE &&& (!!TWO)) ||| THREE
                "ONE || !(TWO && THREE)"    , ONE ||| (!!(TWO &&& THREE))
                "true"                      , IfdefId "true"
                "false"                     , IfdefId "false"
            |]

        let failures    = ResizeArray<string> ()
        let fail        = failures.Add

        for test,expected in positiveTestCases do
            for prelude in preludes do
                let test = prelude + test
                for epilogue in epilogues do
                    let test = test + epilogue
                    try
                        let expr    = parser test

                        if expected <> expr then
                            fail <| sprintf "'%s', expected %A, actual %A" test (exprAsString expected) (exprAsString expr)
                    with
                    | e ->  fail <| sprintf "'%s', expected %A, actual %s,%A" test (exprAsString expected) (e.GetType().Name) e.Message


        let fs =
            failures
            |> Seq.append (errors   |> Seq.map (fun pe -> pe.DebugDisplay ()))
            |> Seq.append (warnings |> Seq.map (fun pe -> pe.DebugDisplay ()))
            |> Seq.toArray

        let failure = String.Join ("\n", fs)

        Assert.shouldBe "" failure

        ()

    [<Fact>]
    member this.NegativeParserTestCases()=

        let errors, warnings, parser = createParser ()

        let negativeTests =
            [|
                ""
                "!"
                "&&"
                "||"
                "@"
                "ONE ONE"
                "ONE@"
                "@ONE"
                "$"
                "ONE$"
                "$ONE"
                "ONE!"
                "(ONE"
                "ONE)"
                // TODO: Investigate why this raises a parse failure
                // "(ONE ||)"
                "ONE&&"
                "ONE ||"
                "&& ONE"
                "||ONE"
                "ONE TWO"
                "ONE(* Comment"
                "ONE(* Comment *)"
                "ONE(**)"
                "ONE (* Comment"
                "ONE (* Comment *)"
                "ONE (**)"
                "ONE )(@$&%*@^#%#!$)"
            |]

        let failures    = ResizeArray<string> ()
        let fail        = failures.Add

        for test in negativeTests do
            for prelude in preludes do
                let test = prelude + test
                for epilogue in epilogues do
                    let test    = test + epilogue
                    try
                        let bec     = errors.Count
                        let expr    = parser test
                        let aec     = errors.Count

                        if bec = aec then   // No new errors discovered
                            fail <| sprintf "'%s', expected 'parse error', actual %A" test (exprAsString expr)
                    with
                    | e ->  fail <| sprintf "'%s', expected 'parse error', actual %s,%A" test (e.GetType().Name) e.Message

        let fs = failures |> Seq.toArray

        let fails = String.Join ("\n", fs)

        Assert.shouldBe "" fails

    [<Fact>]
    member this.LexerIfdefEvalTestCases()=

        let failures    = ResizeArray<string> ()
        let fail        = failures.Add

        for i in 0..7 do
            let one     = isSet i 1
            let two     = isSet i 2
            let three   = isSet i 4

            let lookup s =
                match s with
                | "ONE"     -> one
                | "TWO"     -> two
                | "THREE"   -> three
                | _         -> false

            let testCases =
                [|
                    ONE                         , one
                    !!ONE                       , not one
                    !! (!!ONE)                  , not (not one)
                    TWO                         , two
                    !!TWO                       , not two
                    !! (!!TWO)                  , not (not two)
                    ONE &&& TWO                 , one && two
                    ONE ||| TWO                 , one || two
                    (ONE &&& TWO) &&& THREE     , (one && two) && three
                    (ONE ||| TWO) ||| THREE     , (one || two) || three
                    ONE ||| (TWO &&& THREE)     , one || (two && three)
                    (ONE &&& TWO) ||| THREE     , (one && two) || three
                    ONE ||| (TWO &&& THREE)     , one || (two && three)
                    ONE &&& (TWO ||| THREE)     , one && (two || three)
                    (!!ONE) ||| (TWO &&& THREE) , (not one) || (two && three)
                    (ONE &&& (!!TWO)) ||| THREE , (one && (not two)) || three
                    ONE ||| (!!(TWO &&& THREE)) , one || (not (two && three))
                |]

            let eval = LexerIfdefEval lookup
            for expr, expected in testCases do
                let actual = eval expr

                if actual <> expected then
                    fail <| sprintf "For ONE=%A, TWO=%A, THREE=%A the expression %A is expected to be %A but was %A" one two three (exprAsString expr) expected actual


        let fs = failures |> Seq.toArray

        let fails = String.Join ("\n", fs)

        Assert.shouldBe "" fails
