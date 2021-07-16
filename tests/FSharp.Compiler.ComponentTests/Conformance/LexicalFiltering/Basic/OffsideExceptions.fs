// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Conformance.LexicalFiltering.Basic

open Xunit
open FSharp.Test.Compiler
open FSharp.Test.Xunit.Attributes

module OffsideExceptions =
    type FileAttribute(file) =
        inherit DirectoryAttribute(__SOURCE_DIRECTORY__ + "/../../../resources/tests/Conformance/LexicalFiltering/Basic/OffsideExceptions", Includes=[|file|])

    // This test was automatically generated (moved from FSharpQA suite - Conformance/LexicalFiltering/Basic/OffsideExceptions)
    //<Expects status="success"></Expects>
    [<Theory; File "InfixTokenPlusOne.fs">]
    let InfixTokenPlusOne compilation =
        compilation
        |> asFsx
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Theory; File "RelaxWhitespace2.fs">]
    let RelaxWhitespace2 compilation =
        compilation
        |> asFsx
        |> withOptions ["--nowarn:25"] // Incomplete pattern matches on this expression.
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Theory; File "RelaxWhitespace2.fs">]
    let RelaxWhitespace2_Warning25 compilation =
        compilation
        |> asFsx
        |> verifyBaseline
        |> ignore
        
    [<Fact>]
    let RelaxWhitespace2_Indentation() =
        Fsx """
match
1
with 2 -> ()
let (
1
) = 2
type J(
x:int
) = class end
for i in [
1
] do ()
while (
true
) do ()
        """
        |> typecheck
        |> shouldFail
        |> withResults [
         { Error = Error 10
           Range = { StartLine = 2
                     StartColumn = 7
                     EndLine = 3
                     EndColumn = 1 }
           Message =
            "Incomplete structured construct at or before this point in expression" }
         { Error = Warning 58
           Range = { StartLine = 6
                     StartColumn = 1
                     EndLine = 6
                     EndColumn = 2 }
           Message =
            "Possible incorrect indentation: this token is offside of context started at position (5:1). Try indenting this token further or using standard formatting conventions." }
        ]
        |> ignore

    [<Fact>]
    let RelaxWhitespace2_AllowedBefore1() =
        Fsx """
module A
    let a = (
     1
)
        """
        |> withLangVersion50
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore2() =
        Fsx """
module A
    let a = id {| x = 1;
  y = 1
|}
        """
        |> withLangVersion50
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore3() =
        Fsx """
module A
    let a = {|
     y = 1
|}
        """
        |> withLangVersion50
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore4() =
        Fsx """
module A
    let a = {| y =
               1
|}
        """
        |> withLangVersion50
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore5() =
        Fsx """
            let a = (
                fun () -> seq {
                    1
                }, 2 = 3
            )
            let b : (unit -> int seq) * bool = id a
        """
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore6() =
        Fsx """
            let a = (
                function () -> seq {
                         1
                }, 2 = 3
            )
            let b : (unit -> int seq) * bool = id a
        """
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore7() =
        Fsx """module M = begin type K = member _.G = 3 end;; module M2 = do begin end;; module M3 = do ()"""
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore8() =
        Fsx """
module M = begin
    type K = member _.G = 3
end module M2 = do ignore begin 1
end module M3 = do ()
        """
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore9() =
        Fsx """
type __() =
    let a =
        fun () -> (
                    1
        ), 2
        |> printfn "%O"
        in let () = ignore<(unit -> int) * unit> a
    let b =
        fun () -> (
                    1
        ), 2
        |> printfn "%O"
        in do ignore<(unit -> int) * unit> b
    let c =
        do ignore (
                    1
        ), 2 |> printfn "%O"
        in do ignore<unit * unit> c
    member _.d() = seq {
        1
    }; static member e() = [
        1
    ]
        """
        |> typecheck
        |> shouldSucceed
        |> ignore
    [<Fact>]
    let RelaxWhitespace2_AllowedBefore10() =
        Fsx """
module [<
 Experimental "a"
 >] A = type [<
         Experimental "b"
         >] B() = let [<
                   Experimental "c"
                   >] c = 1
        """
        |> typecheck
        |> shouldSucceed
        |> ignore

    [<Fact>]
    let RelaxWhitespace2_Neg1() =
        Fsx """
module A
    let a = {|
    y = 1
|}
        """
        |> typecheck
        |> shouldFail
        |> withResult {
            Error = Warning 58
            Range = { StartLine = 4
                      StartColumn = 5
                      EndLine = 4
                      EndColumn = 6 }
            Message =
             "Possible incorrect indentation: this token is offside of context started at position (3:5). Try indenting this token further or using standard formatting conventions."
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg2() =
        Fsx """
module A
    let a = id {|
    y = 1
|}
        """
        |> typecheck
        |> shouldFail
        |> withResult {
            Error = Warning 58
            Range = { StartLine = 4
                      StartColumn = 5
                      EndLine = 4
                      EndColumn = 6 }
            Message =
             "Possible incorrect indentation: this token is offside of context started at position (3:5). Try indenting this token further or using standard formatting conventions."
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg3() =
        Fsx """
module A
    let a = if {| x = 1;
    y = 1
|}                            |> isNull then ()
        """
        |> typecheck
        |> shouldFail
        |> withResults [
             { Error = Error 10
               Range = { StartLine = 5
                         StartColumn = 1
                         EndLine = 5
                         EndColumn = 3 }
               Message =
                "Incomplete structured construct at or before this point in expression" };
             { Error = Error 589
               Range = { StartLine = 3
                         StartColumn = 13
                         EndLine = 3
                         EndColumn = 15 }
               Message =
                "Incomplete conditional. Expected 'if <expr> then <expr>' or 'if <expr> then <expr> else <expr>'." };
             { Error = Error 10
               Range = { StartLine = 5
                         StartColumn = 31
                         EndLine = 5
                         EndColumn = 33 }
               Message = "Unexpected infix operator in implementation file" }
        ] |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg4() =
        Fsx """
module A
    let a = {| x =
    1
|}
        """
        |> typecheck
        |> shouldFail
        |> withResult {
            Error = Warning 58
            Range = { StartLine = 4
                      StartColumn = 5
                      EndLine = 4
                      EndColumn = 6 }
            Message =
             "Possible incorrect indentation: this token is offside of context started at position (3:5). Try indenting this token further or using standard formatting conventions."
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg5() =
        Fsx """
module A
    let a = {| x =
              1
|}
        """
        |> withLangVersion50
        |> compile
        |> shouldFail
        |> withResult {
            Error = Warning 58
            Range = { StartLine = 4
                      StartColumn = 15
                      EndLine = 4
                      EndColumn = 16 }
            Message =
                "Possible incorrect indentation: this token is offside of context started at position (3:16). Try indenting this token further or using standard formatting conventions."
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg6() =
        Fsx """
match
    1
with 2 -> ()
        """
        |> withLangVersion50
        |> compile
        |> shouldFail
        |> withResult {
            Error = Error 10
            Range = { StartLine = 4
                      StartColumn = 6
                      EndLine = 4
                      EndColumn = 7 }
            Message =
                "Unexpected start of structured construct in expression"
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg7() =
        Fsx """
type R = { x : int
}
function
| {
    x = 1
} -> ()
        """
        |> typecheck
        |> shouldFail
        |> withResult {
            Error = Error 10
            Range = { StartLine = 7
                      StartColumn = 1
                      EndLine = 7
                      EndColumn = 2 }
            Message =
                "Incomplete structured construct at or before this point in pattern matching. Expected '->' or other token."
        } |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg8() =
        Fsx """
type R = { x : int }
function
{
    x = 1
} -> ()
        """
        |> typecheck
        |> shouldFail
        |> withResults [
            { Error = Warning 25
              Range = { StartLine = 3
                        StartColumn = 1
                        EndLine = 3
                        EndColumn = 9 }
              Message =
               "Incomplete pattern matches on this expression. For example, the value '{x=0}' may indicate a case not covered by the pattern(s)." };
            { Error = Warning 193
              Range = { StartLine = 3
                        StartColumn = 1
                        EndLine = 6
                        EndColumn = 8 }
              Message =
               "This expression is a function value, i.e. is missing arguments. Its type is R -> unit." }
        ] |> ignore
    [<Fact>]
    let RelaxWhitespace2_Neg9() =
        Fsx """
let _ = <@ type Foo(x : int) =
              member this.Length = x @>     

exit 1
  
        """
        |> compile
        |> shouldFail
        |> withResults [
            { Error = Error 10
              Range = { StartLine = 2
                        StartColumn = 12
                        EndLine = 2
                        EndColumn = 16 }
              Message =
               "Incomplete structured construct at or before this point in quotation literal" };
            { Error = Error 602
              Range = { StartLine = 2
                        StartColumn = 9
                        EndLine = 2
                        EndColumn = 11 }
              Message = "Unmatched '<@ @>'" };
            { Error = Error 10
              Range = { StartLine = 2
                        StartColumn = 12
                        EndLine = 2
                        EndColumn = 16 }
              Message =
               "Unexpected keyword 'type' in binding. Expected incomplete structured construct at or before this point or other token." };
            { Error = Error 3118
              Range = { StartLine = 2
                        StartColumn = 1
                        EndLine = 2
                        EndColumn = 4 }
              Message =
               "Incomplete value or function definition. If this is in an expression, the body of the expression must be indented to the same column as the 'let' keyword." };
            { Error = Error 10
              Range = { StartLine = 3
                        StartColumn = 38
                        EndLine = 3
                        EndColumn = 40 }
              Message =
               "Unexpected end of quotation in expression. Expected incomplete structured construct at or before this point or other token." }
        ] |> ignore

    [<Theory; File "RelaxWhitespace2.fs">]
    let RelaxWhitespace2_Fs50 compilation =
        compilation
        |> asFsx
        |> withLangVersion50
        |> compile
        |> shouldFail
        |> (Array.toList >> withResults) [|
        { Error = Warning 58
          Range = { StartLine = 14
                    StartColumn = 9
                    EndLine = 14
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (13:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 18
                    StartColumn = 13
                    EndLine = 18
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (17:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Error 10
          Range = { StartLine = 37
                    StartColumn = 5
                    EndLine = 37
                    EndColumn = 6 }
          Message =
           "Incomplete structured construct at or before this point in binding. Expected '=' or other token." }
        { Error = Error 10
          Range = { StartLine = 46
                    StartColumn = 5
                    EndLine = 46
                    EndColumn = 8 }
          Message =
           "Incomplete structured construct at or before this point in implementation file" }
        { Error = Warning 58
          Range = { StartLine = 48
                    StartColumn = 13
                    EndLine = 48
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (47:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 95
                    StartColumn = 9
                    EndLine = 95
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (94:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 98
                    StartColumn = 9
                    EndLine = 98
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (97:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 102
                    StartColumn = 13
                    EndLine = 102
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (101:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 106
                    StartColumn = 13
                    EndLine = 106
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (105:45). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 110
                    StartColumn = 13
                    EndLine = 110
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (109:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 112
                    StartColumn = 13
                    EndLine = 112
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (111:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 117
                    StartColumn = 9
                    EndLine = 117
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (116:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 132
                    StartColumn = 13
                    EndLine = 132
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (131:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 144
                    StartColumn = 9
                    EndLine = 144
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (143:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 148
                    StartColumn = 13
                    EndLine = 148
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (147:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 201
                    StartColumn = 9
                    EndLine = 201
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (200:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 204
                    StartColumn = 9
                    EndLine = 204
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (203:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 208
                    StartColumn = 13
                    EndLine = 208
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (207:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 212
                    StartColumn = 13
                    EndLine = 212
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (211:39). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 216
                    StartColumn = 13
                    EndLine = 216
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (215:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 218
                    StartColumn = 13
                    EndLine = 218
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (217:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 223
                    StartColumn = 9
                    EndLine = 223
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (222:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 332
                    StartColumn = 9
                    EndLine = 332
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (331:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 423
                    StartColumn = 9
                    EndLine = 423
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (422:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 490
                    StartColumn = 9
                    EndLine = 490
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (489:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 505
                    StartColumn = 13
                    EndLine = 505
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (504:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 507
                    StartColumn = 13
                    EndLine = 507
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (506:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 544
                    StartColumn = 9
                    EndLine = 544
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (543:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 560
                    StartColumn = 9
                    EndLine = 560
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (559:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 564
                    StartColumn = 13
                    EndLine = 564
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (563:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 574
                    StartColumn = 13
                    EndLine = 574
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (573:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 577
                    StartColumn = 9
                    EndLine = 577
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (576:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 582
                    StartColumn = 9
                    EndLine = 582
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (581:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 584
                    StartColumn = 9
                    EndLine = 584
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (583:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 587
                    StartColumn = 9
                    EndLine = 587
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (586:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 590
                    StartColumn = 9
                    EndLine = 590
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (589:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 594
                    StartColumn = 13
                    EndLine = 594
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (593:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 598
                    StartColumn = 13
                    EndLine = 598
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (597:46). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 602
                    StartColumn = 13
                    EndLine = 602
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (601:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 604
                    StartColumn = 13
                    EndLine = 604
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (603:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 609
                    StartColumn = 9
                    EndLine = 609
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (608:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 647
                    StartColumn = 5
                    EndLine = 647
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (646:38). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 651
                    StartColumn = 9
                    EndLine = 651
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (650:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 654
                    StartColumn = 9
                    EndLine = 654
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (653:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 658
                    StartColumn = 13
                    EndLine = 658
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (657:36). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 662
                    StartColumn = 13
                    EndLine = 662
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (661:53). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 666
                    StartColumn = 13
                    EndLine = 666
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (665:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 668
                    StartColumn = 13
                    EndLine = 668
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (667:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 671
                    StartColumn = 9
                    EndLine = 671
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (670:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 673
                    StartColumn = 9
                    EndLine = 673
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (672:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 676
                    StartColumn = 9
                    EndLine = 676
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (675:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 678
                    StartColumn = 9
                    EndLine = 678
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (677:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 681
                    StartColumn = 9
                    EndLine = 681
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (680:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 684
                    StartColumn = 9
                    EndLine = 684
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (683:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 688
                    StartColumn = 13
                    EndLine = 688
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (687:39). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 692
                    StartColumn = 13
                    EndLine = 692
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (691:56). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 696
                    StartColumn = 13
                    EndLine = 696
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (695:32). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 698
                    StartColumn = 13
                    EndLine = 698
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (697:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 703
                    StartColumn = 9
                    EndLine = 703
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (702:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 742
                    StartColumn = 9
                    EndLine = 742
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (741:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 745
                    StartColumn = 9
                    EndLine = 745
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (744:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 749
                    StartColumn = 13
                    EndLine = 749
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (748:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 753
                    StartColumn = 13
                    EndLine = 753
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (752:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 757
                    StartColumn = 13
                    EndLine = 757
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (756:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 759
                    StartColumn = 13
                    EndLine = 759
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (758:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 764
                    StartColumn = 9
                    EndLine = 764
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (763:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 769
                    StartColumn = 9
                    EndLine = 769
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (768:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 802
                    StartColumn = 9
                    EndLine = 802
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (801:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 805
                    StartColumn = 9
                    EndLine = 805
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (804:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 809
                    StartColumn = 13
                    EndLine = 809
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (808:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 813
                    StartColumn = 13
                    EndLine = 813
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (812:46). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 817
                    StartColumn = 13
                    EndLine = 817
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (816:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 819
                    StartColumn = 13
                    EndLine = 819
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (818:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 822
                    StartColumn = 9
                    EndLine = 822
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (821:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 824
                    StartColumn = 9
                    EndLine = 824
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (823:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 827
                    StartColumn = 9
                    EndLine = 827
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (826:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 829
                    StartColumn = 9
                    EndLine = 829
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (828:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 833
                    StartColumn = 9
                    EndLine = 833
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (832:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 836
                    StartColumn = 9
                    EndLine = 836
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (835:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 840
                    StartColumn = 13
                    EndLine = 840
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (839:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 844
                    StartColumn = 13
                    EndLine = 844
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (843:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 848
                    StartColumn = 13
                    EndLine = 848
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (847:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 850
                    StartColumn = 13
                    EndLine = 850
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (849:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 855
                    StartColumn = 9
                    EndLine = 855
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (854:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 860
                    StartColumn = 9
                    EndLine = 860
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (859:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 893
                    StartColumn = 9
                    EndLine = 893
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (892:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 896
                    StartColumn = 9
                    EndLine = 896
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (895:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 900
                    StartColumn = 13
                    EndLine = 900
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (899:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 904
                    StartColumn = 13
                    EndLine = 904
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (903:49). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 908
                    StartColumn = 13
                    EndLine = 908
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (907:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 910
                    StartColumn = 13
                    EndLine = 910
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (909:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 913
                    StartColumn = 9
                    EndLine = 913
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (912:31). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 915
                    StartColumn = 9
                    EndLine = 915
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (914:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 918
                    StartColumn = 9
                    EndLine = 918
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (917:31). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 920
                    StartColumn = 9
                    EndLine = 920
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (919:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 930
                    StartColumn = 9
                    EndLine = 930
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (929:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 935
                    StartColumn = 9
                    EndLine = 935
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (934:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 939
                    StartColumn = 13
                    EndLine = 939
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (938:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 943
                    StartColumn = 13
                    EndLine = 943
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (942:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 947
                    StartColumn = 13
                    EndLine = 947
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (946:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 949
                    StartColumn = 13
                    EndLine = 949
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (948:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 952
                    StartColumn = 9
                    EndLine = 952
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (951:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 957
                    StartColumn = 9
                    EndLine = 957
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (956:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 959
                    StartColumn = 9
                    EndLine = 959
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (958:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 962
                    StartColumn = 9
                    EndLine = 962
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (961:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 966
                    StartColumn = 13
                    EndLine = 966
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (965:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 970
                    StartColumn = 13
                    EndLine = 970
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (969:48). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 974
                    StartColumn = 13
                    EndLine = 974
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (973:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 976
                    StartColumn = 13
                    EndLine = 976
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (975:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 979
                    StartColumn = 9
                    EndLine = 979
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (978:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 981
                    StartColumn = 9
                    EndLine = 981
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (980:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 984
                    StartColumn = 9
                    EndLine = 984
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (983:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 986
                    StartColumn = 9
                    EndLine = 986
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (985:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 989
                    StartColumn = 9
                    EndLine = 989
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (988:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 993
                    StartColumn = 13
                    EndLine = 993
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (992:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 997
                    StartColumn = 13
                    EndLine = 997
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (996:49). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1001
                    StartColumn = 13
                    EndLine = 1001
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1000:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1003
                    StartColumn = 13
                    EndLine = 1003
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1002:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1006
                    StartColumn = 9
                    EndLine = 1006
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1005:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1008
                    StartColumn = 9
                    EndLine = 1008
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1007:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1011
                    StartColumn = 5
                    EndLine = 1011
                    EndColumn = 7 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1010:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1016
                    StartColumn = 5
                    EndLine = 1016
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1013:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1017
                    StartColumn = 9
                    EndLine = 1017
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1016:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1019
                    StartColumn = 5
                    EndLine = 1019
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1016:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1021
                    StartColumn = 13
                    EndLine = 1021
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1020:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1023
                    StartColumn = 5
                    EndLine = 1023
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1019:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1027
                    StartColumn = 5
                    EndLine = 1027
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1023:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1033
                    StartColumn = 5
                    EndLine = 1033
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1027:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1038
                    StartColumn = 5
                    EndLine = 1038
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1033:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1043
                    StartColumn = 5
                    EndLine = 1043
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1043
                    StartColumn = 12
                    EndLine = 1043
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1046
                    StartColumn = 5
                    EndLine = 1046
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1046
                    StartColumn = 12
                    EndLine = 1046
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1047
                    StartColumn = 9
                    EndLine = 1047
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1046:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1049
                    StartColumn = 5
                    EndLine = 1049
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1049
                    StartColumn = 12
                    EndLine = 1049
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1051
                    StartColumn = 13
                    EndLine = 1051
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1050:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1053
                    StartColumn = 5
                    EndLine = 1053
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1053
                    StartColumn = 12
                    EndLine = 1053
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1057
                    StartColumn = 5
                    EndLine = 1057
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1057
                    StartColumn = 12
                    EndLine = 1057
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1063
                    StartColumn = 5
                    EndLine = 1063
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1063
                    StartColumn = 12
                    EndLine = 1063
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1064
                    StartColumn = 9
                    EndLine = 1064
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1063:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1068
                    StartColumn = 5
                    EndLine = 1068
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1068
                    StartColumn = 12
                    EndLine = 1068
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1069
                    StartColumn = 9
                    EndLine = 1069
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1068:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1073
                    StartColumn = 5
                    EndLine = 1073
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1073
                    StartColumn = 21
                    EndLine = 1073
                    EndColumn = 22 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1076
                    StartColumn = 5
                    EndLine = 1076
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1076
                    StartColumn = 21
                    EndLine = 1076
                    EndColumn = 22 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1077
                    StartColumn = 9
                    EndLine = 1077
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1076:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1079
                    StartColumn = 5
                    EndLine = 1079
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1079
                    StartColumn = 21
                    EndLine = 1079
                    EndColumn = 22 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1081
                    StartColumn = 13
                    EndLine = 1081
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1080:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1083
                    StartColumn = 5
                    EndLine = 1083
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1083
                    StartColumn = 21
                    EndLine = 1083
                    EndColumn = 22 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1087
                    StartColumn = 5
                    EndLine = 1087
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1087
                    StartColumn = 21
                    EndLine = 1087
                    EndColumn = 22 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1093
                    StartColumn = 5
                    EndLine = 1093
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1094
                    StartColumn = 9
                    EndLine = 1094
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1093:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1095
                    StartColumn = 8
                    EndLine = 1095
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1098
                    StartColumn = 5
                    EndLine = 1098
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1099
                    StartColumn = 9
                    EndLine = 1099
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1098:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1103
                    StartColumn = 5
                    EndLine = 1103
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1103
                    StartColumn = 17
                    EndLine = 1103
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1106
                    StartColumn = 5
                    EndLine = 1106
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1106
                    StartColumn = 17
                    EndLine = 1106
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1107
                    StartColumn = 9
                    EndLine = 1107
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1106:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1109
                    StartColumn = 5
                    EndLine = 1109
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1109
                    StartColumn = 17
                    EndLine = 1109
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1111
                    StartColumn = 13
                    EndLine = 1111
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1110:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1113
                    StartColumn = 5
                    EndLine = 1113
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1113
                    StartColumn = 17
                    EndLine = 1113
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1117
                    StartColumn = 5
                    EndLine = 1117
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1117
                    StartColumn = 17
                    EndLine = 1117
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1123
                    StartColumn = 5
                    EndLine = 1123
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1124
                    StartColumn = 9
                    EndLine = 1124
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1123:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1125
                    StartColumn = 8
                    EndLine = 1125
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1128
                    StartColumn = 5
                    EndLine = 1128
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1129
                    StartColumn = 9
                    EndLine = 1129
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1128:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1133
                    StartColumn = 1
                    EndLine = 1133
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1134
                    StartColumn = 5
                    EndLine = 1134
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1133:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1140
                    StartColumn = 9
                    EndLine = 1140
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1139:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1144
                    StartColumn = 13
                    EndLine = 1144
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1143:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1170
                    StartColumn = 9
                    EndLine = 1170
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1169:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1174
                    StartColumn = 13
                    EndLine = 1174
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1173:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1187
                    StartColumn = 9
                    EndLine = 1187
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1186:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1192
                    StartColumn = 9
                    EndLine = 1192
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1191:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1200
                    StartColumn = 9
                    EndLine = 1200
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1199:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1204
                    StartColumn = 13
                    EndLine = 1204
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1203:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1217
                    StartColumn = 9
                    EndLine = 1217
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1216:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1222
                    StartColumn = 9
                    EndLine = 1222
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1221:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1230
                    StartColumn = 9
                    EndLine = 1230
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1229:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1234
                    StartColumn = 13
                    EndLine = 1234
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1233:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1247
                    StartColumn = 9
                    EndLine = 1247
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1246:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1252
                    StartColumn = 9
                    EndLine = 1252
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1251:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1257
                    StartColumn = 1
                    EndLine = 1257
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1418
                    StartColumn = 1
                    EndLine = 1418
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1470
                    StartColumn = 9
                    EndLine = 1470
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1469:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1475
                    StartColumn = 9
                    EndLine = 1475
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1474:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1500
                    StartColumn = 9
                    EndLine = 1500
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1499:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1505
                    StartColumn = 9
                    EndLine = 1505
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1504:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1530
                    StartColumn = 9
                    EndLine = 1530
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1529:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1535
                    StartColumn = 9
                    EndLine = 1535
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1534:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1539
                    StartColumn = 1
                    EndLine = 1539
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1591
                    StartColumn = 9
                    EndLine = 1591
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1590:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1596
                    StartColumn = 9
                    EndLine = 1596
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1595:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1621
                    StartColumn = 9
                    EndLine = 1621
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1620:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1626
                    StartColumn = 9
                    EndLine = 1626
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1625:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1651
                    StartColumn = 9
                    EndLine = 1651
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1650:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1656
                    StartColumn = 9
                    EndLine = 1656
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1655:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1660
                    StartColumn = 1
                    EndLine = 1660
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1661
                    StartColumn = 5
                    EndLine = 1661
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1660:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1714
                    StartColumn = 9
                    EndLine = 1714
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1713:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1719
                    StartColumn = 9
                    EndLine = 1719
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1718:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1744
                    StartColumn = 9
                    EndLine = 1744
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1743:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1749
                    StartColumn = 9
                    EndLine = 1749
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1748:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1774
                    StartColumn = 9
                    EndLine = 1774
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1773:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1779
                    StartColumn = 9
                    EndLine = 1779
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1778:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1783
                    StartColumn = 1
                    EndLine = 1783
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1784
                    StartColumn = 5
                    EndLine = 1784
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1783:25). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1790
                    StartColumn = 9
                    EndLine = 1790
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1789:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1794
                    StartColumn = 13
                    EndLine = 1794
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1793:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1804
                    StartColumn = 13
                    EndLine = 1804
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1803:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1807
                    StartColumn = 9
                    EndLine = 1807
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1806:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1812
                    StartColumn = 9
                    EndLine = 1812
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1811:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1814
                    StartColumn = 9
                    EndLine = 1814
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1813:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1820
                    StartColumn = 9
                    EndLine = 1820
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1819:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1824
                    StartColumn = 13
                    EndLine = 1824
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1823:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1834
                    StartColumn = 13
                    EndLine = 1834
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1833:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1837
                    StartColumn = 9
                    EndLine = 1837
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1836:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1842
                    StartColumn = 9
                    EndLine = 1842
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1841:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1844
                    StartColumn = 9
                    EndLine = 1844
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1843:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1850
                    StartColumn = 9
                    EndLine = 1850
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1849:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1854
                    StartColumn = 13
                    EndLine = 1854
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1853:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1864
                    StartColumn = 13
                    EndLine = 1864
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1863:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1867
                    StartColumn = 9
                    EndLine = 1867
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1866:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1872
                    StartColumn = 9
                    EndLine = 1872
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1871:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1874
                    StartColumn = 9
                    EndLine = 1874
                    EndColumn = 25 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1873:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1880
                    StartColumn = 9
                    EndLine = 1880
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1879:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1884
                    StartColumn = 13
                    EndLine = 1884
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1883:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1894
                    StartColumn = 13
                    EndLine = 1894
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1893:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1897
                    StartColumn = 9
                    EndLine = 1897
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1896:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1902
                    StartColumn = 9
                    EndLine = 1902
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1901:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1904
                    StartColumn = 9
                    EndLine = 1904
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1903:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1906
                    StartColumn = 1
                    EndLine = 1906
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1907
                    StartColumn = 5
                    EndLine = 1907
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1906:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1910
                    StartColumn = 9
                    EndLine = 1910
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1909:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1913
                    StartColumn = 9
                    EndLine = 1913
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1912:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1917
                    StartColumn = 13
                    EndLine = 1917
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1916:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1921
                    StartColumn = 13
                    EndLine = 1921
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1920:51). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1925
                    StartColumn = 13
                    EndLine = 1925
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1924:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1927
                    StartColumn = 13
                    EndLine = 1927
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1926:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1930
                    StartColumn = 9
                    EndLine = 1930
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1929:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1932
                    StartColumn = 9
                    EndLine = 1932
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1931:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1935
                    StartColumn = 9
                    EndLine = 1935
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1934:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1937
                    StartColumn = 9
                    EndLine = 1937
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1936:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1940
                    StartColumn = 9
                    EndLine = 1940
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1939:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1943
                    StartColumn = 9
                    EndLine = 1943
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1942:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1947
                    StartColumn = 13
                    EndLine = 1947
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1946:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1951
                    StartColumn = 13
                    EndLine = 1951
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1950:51). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1955
                    StartColumn = 13
                    EndLine = 1955
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1954:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1957
                    StartColumn = 13
                    EndLine = 1957
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1956:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1960
                    StartColumn = 9
                    EndLine = 1960
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1959:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1962
                    StartColumn = 9
                    EndLine = 1962
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1961:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1965
                    StartColumn = 9
                    EndLine = 1965
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1964:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1967
                    StartColumn = 9
                    EndLine = 1967
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1966:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1970
                    StartColumn = 9
                    EndLine = 1970
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1969:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1973
                    StartColumn = 9
                    EndLine = 1973
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1972:32). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1977
                    StartColumn = 13
                    EndLine = 1977
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1976:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1981
                    StartColumn = 13
                    EndLine = 1981
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1980:51). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1985
                    StartColumn = 13
                    EndLine = 1985
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1984:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1987
                    StartColumn = 13
                    EndLine = 1987
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1986:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1990
                    StartColumn = 9
                    EndLine = 1990
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1989:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1992
                    StartColumn = 9
                    EndLine = 1992
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1991:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1995
                    StartColumn = 9
                    EndLine = 1995
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1994:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 1997
                    StartColumn = 9
                    EndLine = 1997
                    EndColumn = 25 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1996:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2000
                    StartColumn = 9
                    EndLine = 2000
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1999:25). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2003
                    StartColumn = 9
                    EndLine = 2003
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2002:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2007
                    StartColumn = 13
                    EndLine = 2007
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2006:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2011
                    StartColumn = 13
                    EndLine = 2011
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2010:51). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2015
                    StartColumn = 13
                    EndLine = 2015
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2014:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2017
                    StartColumn = 13
                    EndLine = 2017
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2016:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2020
                    StartColumn = 9
                    EndLine = 2020
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2019:25). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2022
                    StartColumn = 9
                    EndLine = 2022
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2021:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2025
                    StartColumn = 9
                    EndLine = 2025
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2024:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2027
                    StartColumn = 9
                    EndLine = 2027
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2026:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2029
                    StartColumn = 1
                    EndLine = 2029
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2032
                    StartColumn = 9
                    EndLine = 2032
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2031:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2035
                    StartColumn = 9
                    EndLine = 2035
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2034:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2039
                    StartColumn = 13
                    EndLine = 2039
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2038:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2043
                    StartColumn = 13
                    EndLine = 2043
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2042:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2047
                    StartColumn = 13
                    EndLine = 2047
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2046:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2049
                    StartColumn = 13
                    EndLine = 2049
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2048:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2052
                    StartColumn = 9
                    EndLine = 2052
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2051:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2054
                    StartColumn = 9
                    EndLine = 2054
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2053:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2057
                    StartColumn = 9
                    EndLine = 2057
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2056:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2059
                    StartColumn = 9
                    EndLine = 2059
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2058:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2062
                    StartColumn = 9
                    EndLine = 2062
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2061:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2065
                    StartColumn = 9
                    EndLine = 2065
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2064:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2069
                    StartColumn = 13
                    EndLine = 2069
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2068:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2073
                    StartColumn = 13
                    EndLine = 2073
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2072:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2077
                    StartColumn = 13
                    EndLine = 2077
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2076:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2079
                    StartColumn = 13
                    EndLine = 2079
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2078:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2082
                    StartColumn = 9
                    EndLine = 2082
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2081:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2084
                    StartColumn = 9
                    EndLine = 2084
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2083:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2087
                    StartColumn = 9
                    EndLine = 2087
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2086:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2089
                    StartColumn = 9
                    EndLine = 2089
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2088:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2092
                    StartColumn = 9
                    EndLine = 2092
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2091:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2095
                    StartColumn = 9
                    EndLine = 2095
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2094:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2099
                    StartColumn = 13
                    EndLine = 2099
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2098:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2103
                    StartColumn = 13
                    EndLine = 2103
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2102:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2107
                    StartColumn = 13
                    EndLine = 2107
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2106:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2109
                    StartColumn = 13
                    EndLine = 2109
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2108:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2112
                    StartColumn = 9
                    EndLine = 2112
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2111:38). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2114
                    StartColumn = 9
                    EndLine = 2114
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2113:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2117
                    StartColumn = 9
                    EndLine = 2117
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2116:38). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2119
                    StartColumn = 9
                    EndLine = 2119
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2118:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2122
                    StartColumn = 9
                    EndLine = 2122
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2121:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2125
                    StartColumn = 9
                    EndLine = 2125
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2124:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2129
                    StartColumn = 13
                    EndLine = 2129
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2128:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2133
                    StartColumn = 13
                    EndLine = 2133
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2132:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2137
                    StartColumn = 13
                    EndLine = 2137
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2136:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2139
                    StartColumn = 13
                    EndLine = 2139
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2138:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2142
                    StartColumn = 9
                    EndLine = 2142
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2141:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2144
                    StartColumn = 9
                    EndLine = 2144
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2143:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2147
                    StartColumn = 9
                    EndLine = 2147
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2146:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2149
                    StartColumn = 9
                    EndLine = 2149
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2148:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2151
                    StartColumn = 1
                    EndLine = 2151
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2154
                    StartColumn = 9
                    EndLine = 2154
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2153:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2157
                    StartColumn = 9
                    EndLine = 2157
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2156:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2161
                    StartColumn = 13
                    EndLine = 2161
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2160:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2165
                    StartColumn = 13
                    EndLine = 2165
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2164:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2169
                    StartColumn = 13
                    EndLine = 2169
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2168:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2171
                    StartColumn = 13
                    EndLine = 2171
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2170:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2174
                    StartColumn = 9
                    EndLine = 2174
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2173:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2176
                    StartColumn = 9
                    EndLine = 2176
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2175:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2179
                    StartColumn = 9
                    EndLine = 2179
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2178:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2181
                    StartColumn = 9
                    EndLine = 2181
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2180:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2184
                    StartColumn = 9
                    EndLine = 2184
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2183:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2187
                    StartColumn = 9
                    EndLine = 2187
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2186:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2191
                    StartColumn = 13
                    EndLine = 2191
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2190:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2195
                    StartColumn = 13
                    EndLine = 2195
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2194:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2199
                    StartColumn = 13
                    EndLine = 2199
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2198:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2201
                    StartColumn = 13
                    EndLine = 2201
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2200:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2204
                    StartColumn = 9
                    EndLine = 2204
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2203:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2206
                    StartColumn = 9
                    EndLine = 2206
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2205:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2209
                    StartColumn = 9
                    EndLine = 2209
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2208:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2211
                    StartColumn = 9
                    EndLine = 2211
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2210:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2214
                    StartColumn = 9
                    EndLine = 2214
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2213:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2217
                    StartColumn = 9
                    EndLine = 2217
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2216:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2221
                    StartColumn = 13
                    EndLine = 2221
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2220:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2225
                    StartColumn = 13
                    EndLine = 2225
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2224:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2229
                    StartColumn = 13
                    EndLine = 2229
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2228:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2231
                    StartColumn = 13
                    EndLine = 2231
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2230:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2234
                    StartColumn = 9
                    EndLine = 2234
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2233:38). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2236
                    StartColumn = 9
                    EndLine = 2236
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2235:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2239
                    StartColumn = 9
                    EndLine = 2239
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2238:38). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2241
                    StartColumn = 9
                    EndLine = 2241
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2240:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2244
                    StartColumn = 9
                    EndLine = 2244
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2243:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2247
                    StartColumn = 9
                    EndLine = 2247
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2246:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2251
                    StartColumn = 13
                    EndLine = 2251
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2250:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2255
                    StartColumn = 13
                    EndLine = 2255
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2254:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2259
                    StartColumn = 13
                    EndLine = 2259
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2258:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2261
                    StartColumn = 13
                    EndLine = 2261
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2260:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2264
                    StartColumn = 9
                    EndLine = 2264
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2263:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2266
                    StartColumn = 9
                    EndLine = 2266
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2265:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2269
                    StartColumn = 9
                    EndLine = 2269
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2268:34). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2271
                    StartColumn = 9
                    EndLine = 2271
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2270:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2273
                    StartColumn = 1
                    EndLine = 2273
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2274
                    StartColumn = 5
                    EndLine = 2274
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2273:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2277
                    StartColumn = 9
                    EndLine = 2277
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2276:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2282
                    StartColumn = 9
                    EndLine = 2282
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2281:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2286
                    StartColumn = 13
                    EndLine = 2286
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2285:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2290
                    StartColumn = 13
                    EndLine = 2290
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2289:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2294
                    StartColumn = 13
                    EndLine = 2294
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2293:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2296
                    StartColumn = 13
                    EndLine = 2296
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2295:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2299
                    StartColumn = 9
                    EndLine = 2299
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2298:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2304
                    StartColumn = 9
                    EndLine = 2304
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2303:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2308
                    StartColumn = 13
                    EndLine = 2308
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2307:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2312
                    StartColumn = 13
                    EndLine = 2312
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2311:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2316
                    StartColumn = 13
                    EndLine = 2316
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2315:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2318
                    StartColumn = 13
                    EndLine = 2318
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2317:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2321
                    StartColumn = 9
                    EndLine = 2321
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2320:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2326
                    StartColumn = 9
                    EndLine = 2326
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2325:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2330
                    StartColumn = 13
                    EndLine = 2330
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2329:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2334
                    StartColumn = 13
                    EndLine = 2334
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2333:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2338
                    StartColumn = 13
                    EndLine = 2338
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2337:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2340
                    StartColumn = 13
                    EndLine = 2340
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2339:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2343
                    StartColumn = 9
                    EndLine = 2343
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2342:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2348
                    StartColumn = 9
                    EndLine = 2348
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2347:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2352
                    StartColumn = 13
                    EndLine = 2352
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2351:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2356
                    StartColumn = 13
                    EndLine = 2356
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2355:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2360
                    StartColumn = 13
                    EndLine = 2360
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2359:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2362
                    StartColumn = 13
                    EndLine = 2362
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2361:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2364
                    StartColumn = 1
                    EndLine = 2364
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (1038:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2369
                    StartColumn = 9
                    EndLine = 2369
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2368:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2373
                    StartColumn = 13
                    EndLine = 2373
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2372:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2403
                    StartColumn = 13
                    EndLine = 2403
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2402:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2450
                    StartColumn = 9
                    EndLine = 2450
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2449:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2453
                    StartColumn = 9
                    EndLine = 2453
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2452:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2457
                    StartColumn = 13
                    EndLine = 2457
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2456:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2461
                    StartColumn = 13
                    EndLine = 2461
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2460:45). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2465
                    StartColumn = 13
                    EndLine = 2465
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2464:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2467
                    StartColumn = 13
                    EndLine = 2467
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2466:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2472
                    StartColumn = 9
                    EndLine = 2472
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2471:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2487
                    StartColumn = 13
                    EndLine = 2487
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2486:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2493
                    StartColumn = 1
                    EndLine = 2493
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2364:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2498
                    StartColumn = 9
                    EndLine = 2498
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2497:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2502
                    StartColumn = 13
                    EndLine = 2502
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2501:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2555
                    StartColumn = 9
                    EndLine = 2555
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2554:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2558
                    StartColumn = 9
                    EndLine = 2558
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2557:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2562
                    StartColumn = 13
                    EndLine = 2562
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2561:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2566
                    StartColumn = 13
                    EndLine = 2566
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2565:39). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2570
                    StartColumn = 13
                    EndLine = 2570
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2569:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2572
                    StartColumn = 13
                    EndLine = 2572
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2571:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2577
                    StartColumn = 9
                    EndLine = 2577
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2576:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2587
                    StartColumn = 1
                    EndLine = 2587
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2493:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2628
                    StartColumn = 1
                    EndLine = 2628
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2587:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2690
                    StartColumn = 9
                    EndLine = 2690
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2689:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2721
                    StartColumn = 1
                    EndLine = 2721
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2628:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2783
                    StartColumn = 9
                    EndLine = 2783
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2782:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2814
                    StartColumn = 1
                    EndLine = 2814
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2721:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2846
                    StartColumn = 9
                    EndLine = 2846
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2845:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2861
                    StartColumn = 13
                    EndLine = 2861
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2860:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2863
                    StartColumn = 13
                    EndLine = 2863
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2862:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2900
                    StartColumn = 9
                    EndLine = 2900
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2899:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2910
                    StartColumn = 1
                    EndLine = 2910
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2814:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2915
                    StartColumn = 9
                    EndLine = 2915
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2914:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2919
                    StartColumn = 13
                    EndLine = 2919
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2918:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2929
                    StartColumn = 13
                    EndLine = 2929
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2928:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2932
                    StartColumn = 9
                    EndLine = 2932
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2931:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2937
                    StartColumn = 9
                    EndLine = 2937
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2936:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2939
                    StartColumn = 9
                    EndLine = 2939
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2938:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2942
                    StartColumn = 9
                    EndLine = 2942
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2941:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2945
                    StartColumn = 9
                    EndLine = 2945
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2944:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2949
                    StartColumn = 13
                    EndLine = 2949
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2948:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2953
                    StartColumn = 13
                    EndLine = 2953
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2952:46). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2957
                    StartColumn = 13
                    EndLine = 2957
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2956:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2959
                    StartColumn = 13
                    EndLine = 2959
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2958:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 2964
                    StartColumn = 9
                    EndLine = 2964
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2963:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3003
                    StartColumn = 1
                    EndLine = 3003
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (2910:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3005
                    StartColumn = 9
                    EndLine = 3005
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3004:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3008
                    StartColumn = 9
                    EndLine = 3008
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3007:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3012
                    StartColumn = 13
                    EndLine = 3012
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3011:36). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3016
                    StartColumn = 13
                    EndLine = 3016
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3015:53). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3020
                    StartColumn = 13
                    EndLine = 3020
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3019:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3022
                    StartColumn = 13
                    EndLine = 3022
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3021:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3025
                    StartColumn = 9
                    EndLine = 3025
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3024:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3027
                    StartColumn = 9
                    EndLine = 3027
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3026:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3030
                    StartColumn = 9
                    EndLine = 3030
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3029:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3032
                    StartColumn = 9
                    EndLine = 3032
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3031:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3035
                    StartColumn = 9
                    EndLine = 3035
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3034:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3038
                    StartColumn = 9
                    EndLine = 3038
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3037:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3042
                    StartColumn = 13
                    EndLine = 3042
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3041:39). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3046
                    StartColumn = 13
                    EndLine = 3046
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3045:56). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3050
                    StartColumn = 13
                    EndLine = 3050
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3049:32). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3052
                    StartColumn = 13
                    EndLine = 3052
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3051:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3057
                    StartColumn = 9
                    EndLine = 3057
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3056:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3096
                    StartColumn = 1
                    EndLine = 3096
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3003:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3098
                    StartColumn = 9
                    EndLine = 3098
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3097:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3101
                    StartColumn = 9
                    EndLine = 3101
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3100:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3105
                    StartColumn = 13
                    EndLine = 3105
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3104:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3109
                    StartColumn = 13
                    EndLine = 3109
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3108:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3113
                    StartColumn = 13
                    EndLine = 3113
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3112:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3115
                    StartColumn = 13
                    EndLine = 3115
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3114:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3120
                    StartColumn = 9
                    EndLine = 3120
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3119:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3125
                    StartColumn = 9
                    EndLine = 3125
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3124:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3158
                    StartColumn = 9
                    EndLine = 3158
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3157:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3161
                    StartColumn = 9
                    EndLine = 3161
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3160:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3165
                    StartColumn = 13
                    EndLine = 3165
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3164:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3169
                    StartColumn = 13
                    EndLine = 3169
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3168:46). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3173
                    StartColumn = 13
                    EndLine = 3173
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3172:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3175
                    StartColumn = 13
                    EndLine = 3175
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3174:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3178
                    StartColumn = 9
                    EndLine = 3178
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3177:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3180
                    StartColumn = 9
                    EndLine = 3180
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3179:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3183
                    StartColumn = 9
                    EndLine = 3183
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3182:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3185
                    StartColumn = 9
                    EndLine = 3185
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3184:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3189
                    StartColumn = 1
                    EndLine = 3189
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3096:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3191
                    StartColumn = 9
                    EndLine = 3191
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3190:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3194
                    StartColumn = 9
                    EndLine = 3194
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3193:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3198
                    StartColumn = 13
                    EndLine = 3198
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3197:29). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3202
                    StartColumn = 13
                    EndLine = 3202
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3201:37). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3206
                    StartColumn = 13
                    EndLine = 3206
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3205:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3208
                    StartColumn = 13
                    EndLine = 3208
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3207:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3213
                    StartColumn = 9
                    EndLine = 3213
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3212:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3218
                    StartColumn = 9
                    EndLine = 3218
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3217:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3251
                    StartColumn = 9
                    EndLine = 3251
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3250:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3254
                    StartColumn = 9
                    EndLine = 3254
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3253:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3258
                    StartColumn = 13
                    EndLine = 3258
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3257:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3262
                    StartColumn = 13
                    EndLine = 3262
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3261:49). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3266
                    StartColumn = 13
                    EndLine = 3266
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3265:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3268
                    StartColumn = 13
                    EndLine = 3268
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3267:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3271
                    StartColumn = 9
                    EndLine = 3271
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3270:31). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3273
                    StartColumn = 9
                    EndLine = 3273
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3272:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3276
                    StartColumn = 9
                    EndLine = 3276
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3275:31). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3278
                    StartColumn = 9
                    EndLine = 3278
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3277:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3282
                    StartColumn = 1
                    EndLine = 3282
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3189:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3287
                    StartColumn = 9
                    EndLine = 3287
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3286:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3292
                    StartColumn = 9
                    EndLine = 3292
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3291:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3296
                    StartColumn = 13
                    EndLine = 3296
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3295:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3300
                    StartColumn = 13
                    EndLine = 3300
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3299:35). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3304
                    StartColumn = 13
                    EndLine = 3304
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3303:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3306
                    StartColumn = 13
                    EndLine = 3306
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3305:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3313
                    StartColumn = 9
                    EndLine = 3313
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3312:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3315
                    StartColumn = 9
                    EndLine = 3315
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3314:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3318
                    StartColumn = 9
                    EndLine = 3318
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3317:19). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3322
                    StartColumn = 13
                    EndLine = 3322
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3321:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3326
                    StartColumn = 13
                    EndLine = 3326
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3325:48). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3330
                    StartColumn = 13
                    EndLine = 3330
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3329:27). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3332
                    StartColumn = 13
                    EndLine = 3332
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3331:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3335
                    StartColumn = 9
                    EndLine = 3335
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3334:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3337
                    StartColumn = 9
                    EndLine = 3337
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3336:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3340
                    StartColumn = 9
                    EndLine = 3340
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3339:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3342
                    StartColumn = 9
                    EndLine = 3342
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3341:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3345
                    StartColumn = 9
                    EndLine = 3345
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3344:20). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3349
                    StartColumn = 13
                    EndLine = 3349
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3348:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3353
                    StartColumn = 13
                    EndLine = 3353
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3352:49). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3357
                    StartColumn = 13
                    EndLine = 3357
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3356:28). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3359
                    StartColumn = 13
                    EndLine = 3359
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3358:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3362
                    StartColumn = 9
                    EndLine = 3362
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3361:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3364
                    StartColumn = 9
                    EndLine = 3364
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3363:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3369
                    StartColumn = 1
                    EndLine = 3369
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3282:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3370
                    StartColumn = 5
                    EndLine = 3370
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3369:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3375
                    StartColumn = 9
                    EndLine = 3375
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3374:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3376
                    StartColumn = 9
                    EndLine = 3376
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3374:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3380
                    StartColumn = 9
                    EndLine = 3380
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3379:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3384
                    StartColumn = 13
                    EndLine = 3384
                    EndColumn = 17 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3385
                    StartColumn = 13
                    EndLine = 3385
                    EndColumn = 18 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:17). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3390
                    StartColumn = 9
                    EndLine = 3390
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3392
                    StartColumn = 9
                    EndLine = 3392
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3393
                    StartColumn = 9
                    EndLine = 3393
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3395
                    StartColumn = 9
                    EndLine = 3395
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3396
                    StartColumn = 9
                    EndLine = 3396
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3398
                    StartColumn = 9
                    EndLine = 3398
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3399
                    StartColumn = 9
                    EndLine = 3399
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3400
                    StartColumn = 9
                    EndLine = 3400
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3408
                    StartColumn = 9
                    EndLine = 3408
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3409
                    StartColumn = 9
                    EndLine = 3409
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3417
                    StartColumn = 9
                    EndLine = 3417
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3418
                    StartColumn = 5
                    EndLine = 3418
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3419
                    StartColumn = 9
                    EndLine = 3419
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3418:22). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3420
                    StartColumn = 9
                    EndLine = 3420
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3423
                    StartColumn = 1
                    EndLine = 3423
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3383:9). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3424
                    StartColumn = 1
                    EndLine = 3424
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3423:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3427
                    StartColumn = 1
                    EndLine = 3427
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3424:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3430
                    StartColumn = 1
                    EndLine = 3430
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3427:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3431
                    StartColumn = 1
                    EndLine = 3431
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3427:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3434
                    StartColumn = 1
                    EndLine = 3434
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3431:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3435
                    StartColumn = 5
                    EndLine = 3435
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3434:12). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3439
                    StartColumn = 1
                    EndLine = 3439
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3434:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3445
                    StartColumn = 1
                    EndLine = 3445
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3439:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3450
                    StartColumn = 1
                    EndLine = 3450
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3439:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3451
                    StartColumn = 5
                    EndLine = 3451
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3450:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3456
                    StartColumn = 1
                    EndLine = 3456
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3439:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3457
                    StartColumn = 5
                    EndLine = 3457
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3456:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3467
                    StartColumn = 1
                    EndLine = 3467
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3439:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3471
                    StartColumn = 1
                    EndLine = 3471
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3439:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3491
                    StartColumn = 1
                    EndLine = 3491
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3471:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3503
                    StartColumn = 1
                    EndLine = 3503
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3491:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3524
                    StartColumn = 1
                    EndLine = 3524
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3503:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3525
                    StartColumn = 1
                    EndLine = 3525
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3524:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3527
                    StartColumn = 1
                    EndLine = 3527
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3525:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3529
                    StartColumn = 1
                    EndLine = 3529
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3527:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3530
                    StartColumn = 1
                    EndLine = 3530
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3529:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3533
                    StartColumn = 5
                    EndLine = 3533
                    EndColumn = 142 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3532:7). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3537
                    StartColumn = 1
                    EndLine = 3537
                    EndColumn = 2 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3530:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3538
                    StartColumn = 5
                    EndLine = 3538
                    EndColumn = 142 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3537:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3544
                    StartColumn = 1
                    EndLine = 3544
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3530:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3545
                    StartColumn = 5
                    EndLine = 3545
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3546
                    StartColumn = 5
                    EndLine = 3546
                    EndColumn = 34 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3549
                    StartColumn = 1
                    EndLine = 3549
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3550
                    StartColumn = 5
                    EndLine = 3550
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3549:48). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3556
                    StartColumn = 1
                    EndLine = 3556
                    EndColumn = 7 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3556
                    StartColumn = 8
                    EndLine = 3556
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3556
                    StartColumn = 10
                    EndLine = 3556
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3573
                    StartColumn = 1
                    EndLine = 3573
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3574
                    StartColumn = 13
                    EndLine = 3574
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3573:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3575
                    StartColumn = 13
                    EndLine = 3575
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3573:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3576
                    StartColumn = 13
                    EndLine = 3576
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3573:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3577
                    StartColumn = 13
                    EndLine = 3577
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3573:23). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3580
                    StartColumn = 1
                    EndLine = 3580
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3581
                    StartColumn = 23
                    EndLine = 3581
                    EndColumn = 24 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3580:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3582
                    StartColumn = 23
                    EndLine = 3582
                    EndColumn = 24 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3580:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3583
                    StartColumn = 23
                    EndLine = 3583
                    EndColumn = 24 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3580:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3584
                    StartColumn = 23
                    EndLine = 3584
                    EndColumn = 24 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3580:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3586
                    StartColumn = 1
                    EndLine = 3586
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3588
                    StartColumn = 10
                    EndLine = 3588
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3586:24). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3594
                    StartColumn = 1
                    EndLine = 3594
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3600
                    StartColumn = 1
                    EndLine = 3600
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3603
                    StartColumn = 1
                    EndLine = 3603
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3604
                    StartColumn = 1
                    EndLine = 3604
                    EndColumn = 7 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3604
                    StartColumn = 8
                    EndLine = 3604
                    EndColumn = 13 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3604
                    StartColumn = 14
                    EndLine = 3604
                    EndColumn = 15 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3607
                    StartColumn = 9
                    EndLine = 3607
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3606:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3609
                    StartColumn = 1
                    EndLine = 3609
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3544:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3610
                    StartColumn = 2
                    EndLine = 3610
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3609:5). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3612
                    StartColumn = 1
                    EndLine = 3612
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3609:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3617
                    StartColumn = 17
                    EndLine = 3617
                    EndColumn = 39 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3616:26). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3624
                    StartColumn = 1
                    EndLine = 3624
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3612:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3627
                    StartColumn = 1
                    EndLine = 3627
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3624:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3649
                    StartColumn = 5
                    EndLine = 3649
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3648:30). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3670
                    StartColumn = 1
                    EndLine = 3670
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3627:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3680
                    StartColumn = 1
                    EndLine = 3680
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3670:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3691
                    StartColumn = 1
                    EndLine = 3691
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3680:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3696
                    StartColumn = 1
                    EndLine = 3696
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3691:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3703
                    StartColumn = 1
                    EndLine = 3703
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3704
                    StartColumn = 5
                    EndLine = 3704
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3703:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3706
                    StartColumn = 1
                    EndLine = 3706
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3707
                    StartColumn = 5
                    EndLine = 3707
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3706:7). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3709
                    StartColumn = 1
                    EndLine = 3709
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3710
                    StartColumn = 5
                    EndLine = 3710
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3709:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3713
                    StartColumn = 1
                    EndLine = 3713
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3714
                    StartColumn = 1
                    EndLine = 3714
                    EndColumn = 2 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3720
                    StartColumn = 1
                    EndLine = 3720
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3721
                    StartColumn = 1
                    EndLine = 3721
                    EndColumn = 2 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3724
                    StartColumn = 5
                    EndLine = 3724
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3723:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3726
                    StartColumn = 5
                    EndLine = 3726
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3725:7). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3731
                    StartColumn = 1
                    EndLine = 3731
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3734
                    StartColumn = 10
                    EndLine = 3734
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3733:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3736
                    StartColumn = 10
                    EndLine = 3736
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3735:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3742
                    StartColumn = 1
                    EndLine = 3742
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3743
                    StartColumn = 5
                    EndLine = 3743
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3742:16). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3748
                    StartColumn = 1
                    EndLine = 3748
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3751
                    StartColumn = 1
                    EndLine = 3751
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3754
                    StartColumn = 1
                    EndLine = 3754
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3755
                    StartColumn = 5
                    EndLine = 3755
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3754:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3757
                    StartColumn = 1
                    EndLine = 3757
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3758
                    StartColumn = 5
                    EndLine = 3758
                    EndColumn = 8 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3757:21). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3761
                    StartColumn = 9
                    EndLine = 3761
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3760:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3766
                    StartColumn = 1
                    EndLine = 3766
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3768
                    StartColumn = 9
                    EndLine = 3768
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3767:15). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3773
                    StartColumn = 1
                    EndLine = 3773
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3774
                    StartColumn = 5
                    EndLine = 3774
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3773:10). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3776
                    StartColumn = 1
                    EndLine = 3776
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3777
                    StartColumn = 5
                    EndLine = 3777
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3776:14). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3779
                    StartColumn = 1
                    EndLine = 3779
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3780
                    StartColumn = 5
                    EndLine = 3780
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3779:7). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3782
                    StartColumn = 1
                    EndLine = 3782
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3783
                    StartColumn = 5
                    EndLine = 3783
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3782:11). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3785
                    StartColumn = 1
                    EndLine = 3785
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3789
                    StartColumn = 1
                    EndLine = 3789
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3793
                    StartColumn = 1
                    EndLine = 3793
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3796
                    StartColumn = 1
                    EndLine = 3796
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3797
                    StartColumn = 5
                    EndLine = 3797
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3796:8). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3799
                    StartColumn = 1
                    EndLine = 3799
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3802
                    StartColumn = 1
                    EndLine = 3802
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3803
                    StartColumn = 5
                    EndLine = 3803
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3802:57). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3805
                    StartColumn = 1
                    EndLine = 3805
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3808
                    StartColumn = 1
                    EndLine = 3808
                    EndColumn = 3 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3809
                    StartColumn = 5
                    EndLine = 3809
                    EndColumn = 6 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3808:64). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3811
                    StartColumn = 1
                    EndLine = 3811
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3814
                    StartColumn = 1
                    EndLine = 3814
                    EndColumn = 9 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3820
                    StartColumn = 1
                    EndLine = 3820
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3821
                    StartColumn = 1
                    EndLine = 3821
                    EndColumn = 5 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3822
                    StartColumn = 1
                    EndLine = 3822
                    EndColumn = 7 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3822
                    StartColumn = 8
                    EndLine = 3822
                    EndColumn = 11 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3822
                    StartColumn = 12
                    EndLine = 3822
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3822
                    StartColumn = 15
                    EndLine = 3822
                    EndColumn = 16 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3829
                    StartColumn = 1
                    EndLine = 3829
                    EndColumn = 7 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3829
                    StartColumn = 8
                    EndLine = 3829
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3829
                    StartColumn = 11
                    EndLine = 3829
                    EndColumn = 12 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3837
                    StartColumn = 1
                    EndLine = 3837
                    EndColumn = 4 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3696:1). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3843
                    StartColumn = 9
                    EndLine = 3843
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3842:13). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3862
                    StartColumn = 9
                    EndLine = 3862
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3861:31). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3871
                    StartColumn = 13
                    EndLine = 3871
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3870:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3888
                    StartColumn = 9
                    EndLine = 3888
                    EndColumn = 10 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3887:18). Try indenting this token further or using standard formatting conventions." }
        { Error = Warning 58
          Range = { StartLine = 3902
                    StartColumn = 13
                    EndLine = 3902
                    EndColumn = 14 }
          Message =
           "Possible incorrect indentation: this token is offside of context started at position (3901:16). Try indenting this token further or using standard formatting conventions." }
        |]
