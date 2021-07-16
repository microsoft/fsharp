module FSharp.Compiler.Service.Tests.PatternMatchCompilationTests

open FsUnit
open NUnit.Framework
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.IO
open FSharp.Compiler.Syntax


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Wrong type 01 - Match`` () =
    let _, checkResults = getParseAndCheckResults """
match () with
| "" -> ()
| x -> let y = () in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(3,2--3,4): This expression was expected to have type 'unit' but here has type 'string'"
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Wrong type 02 - Binding`` () =
    let _, checkResults = getParseAndCheckResults """
let ("": unit), (x: int) = let y = () in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(2,5--2,7): This expression was expected to have type 'unit' but here has type 'string'"
        "(2,41--2,43): This expression was expected to have type 'unit * int' but here has type 'unit'"
        "(2,4--2,24): Incomplete pattern matches on this expression."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Attributes 01 `` () =
    let _, checkResults = getParseAndCheckResults """
match () with
| [<CompiledName("Foo")>] x -> let y = () in ()
"""
    assertHasSymbolUsages ["x"; "y"; "CompiledNameAttribute"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(3,2--3,25): Attributes are not allowed within patterns"
        "(3,4--3,16): This attribute is not valid for use on this language element"
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Optional val 01 `` () =
    let _, checkResults = getParseAndCheckResults """
match () with
| ?x -> let y = () in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(3,2--3,4): Optional arguments are only permitted on type members"
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Null 01`` () =
    let _, checkResults = getParseAndCheckResults """
match 1, 2 with
| null -> let y = () in ()
"""
    assertHasSymbolUsages ["y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(3,2--3,6): The type '(int * int)' does not have 'null' as a proper value"
        "(2,6--2,10): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 01 - Missing field`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int * int * int

match A with
| B (x, _) -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,2--7,10): This union case expects 3 arguments in tupled form"        
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 02 - Extra args`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int

match A with
| B (_, _, x) -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,5--7,12): This expression was expected to have type 'int' but here has type ''a * 'b * 'c'"
        "(6,6--6,7): Incomplete pattern matches on this expression."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 03 - Extra args`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int * int

match A with
| B (_, _, x) -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,11--7,12): This constructor is applied to 3 argument(s) but expects 2"
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 04 - Extra args`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int

match A with
| A x -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,2--7,5): This union case does not take arguments"
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'B (_)' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 05 - Single arg, no errors`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int

match A with
| B x -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 06 - Named args - Wrong field name`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of field: int

match A with
| B (name = x) -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,5--7,9): The union case 'B' does not have a field named 'name'."
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 07 - Named args - Name used twice`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of field: int * int

match A with
| B (field = x; field = z) -> let y = x + z + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,16--7,21): Union case/exception field 'field' cannot be used more than once."
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Union case 08 - Multiple tupled args`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of field: int * int

match A with
| B x z -> let y = x + z + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,2--7,7): This union case expects 2 arguments in tupled form"
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
let ``Union case 09 - Single arg`` () =
    let _, checkResults = getParseAndCheckResults """
match None with
| None -> ()
| Some (x, z) -> let y = x + z + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual [
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Active pattern 01 - Named args`` () =
    let _, checkResults = getParseAndCheckResults """
let (|Foo|) x = x

match 1 with
| Foo (field = x) -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,2--5,17): Foo is an active pattern and cannot be treated as a discriminated union case with named fields."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Literal 01 - Args - F#`` () =
    let _, checkResults = getParseAndCheckResults """
let [<Literal>] Foo = 1

match 1 with
| Foo x -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,2--5,7): This literal pattern does not take arguments"
        "(4,6--4,7): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Literal 02 - Args - IL`` () =
    let _, checkResults = getParseAndCheckResults """
open System.Diagnostics

match TraceLevel.Off with
| TraceLevel.Off x -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,2--5,18): This literal pattern does not take arguments"
        "(4,6--4,20): Incomplete pattern matches on this expression. For example, the value 'TraceLevel.Error' may indicate a case not covered by the pattern(s)."
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Caseless DU`` () =
    let _, checkResults = getParseAndCheckResults """
type DU = Case of int

let f du =
    match du with
    | Case -> ()

let dowork () =
    f (Case 1)
    0 // return an integer exit code"""
    assertHasSymbolUsages ["DU"; "dowork"; "du"; "f"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(6,6--6,10): This constructor is applied to 0 argument(s) but expects 1"
    ]
    
[<Test>]
let ``Or 01 - No errors`` () =
    let _, checkResults = getParseAndCheckResults """
match 1 with
| x | x -> let y = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"] checkResults
    dumpErrors checkResults |> shouldEqual []


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Or 02 - Different names`` () =
    let _, checkResults = getParseAndCheckResults """
match 1 with
| x | z -> let y = x + z + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(3,2--3,7): The two sides of this 'or' pattern bind different sets of variables"
    ]


[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``Or 03 - Different names and types`` () =
    let _, checkResults = getParseAndCheckResults """
type U =
    | A
    | B of int * string

match A with
| B (x, y) | B (a, x) -> let z = x + 1 in ()
"""
    assertHasSymbolUsages ["x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(7,2--7,21): The two sides of this 'or' pattern bind different sets of variables"
        "(7,19--7,20): This expression was expected to have type 'int' but here has type 'string'"
        "(6,6--6,7): Incomplete pattern matches on this expression. For example, the value 'A' may indicate a case not covered by the pattern(s)."
    ]
    
[<Test>]
let ``As 01 - names and wildcards`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
match 1 with
| _ as w -> let x = w + 1 in ()

match 2 with
| y as _ -> let z = y + 1 in ()

match 3 with
| a as b -> let c = a + b in ()
"""
    assertHasSymbolUsages ["a"; "b"; "c"; "w"; "x"; "y"; "z"] checkResults
    dumpErrors checkResults |> shouldEqual []
    
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 02 - type testing`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let (|Id|) = id
match box 1 with
| :? int as a -> let b = a + 1 in ()
| c & d as :? uint -> let z = c + d + 2u in () // Tighten typing left-to-right: https://github.com/fsharp/fslang-design/pull/595#issuecomment-860591709
| :? int64 as Id e -> let f = e + 3L in ()
| :? uint64 as Id g & h -> let y = g + 4UL + h in () // (:? uint64 as Id h) & (i : obj)
| :? int8 as Id i as j -> let x = i + 5y + j in () // Only the first "as" will have the derived type
"""
    assertHasSymbolUsages (List.map string ['a'..'j']) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,34--5,35): The type 'obj' does not support the operator '+'"
        "(5,32--5,33): The type 'obj' does not support the operator '+'"
        "(7,45--7,46): The type 'obj' does not match the type 'uint64'"
        "(7,43--7,44): The type 'obj' does not match the type 'uint64'"
        "(8,43--8,44): The type 'obj' does not match the type 'int8'"
        "(8,41--8,42): The type 'obj' does not match the type 'int8'"
        "(3,6--3,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
    ]
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 03 - impossible type testing`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
match Unchecked.defaultof<System.ValueType> with
| :? System.Enum as (:? System.ConsoleKey as a) -> let b = a + enum 1 in ()
| :? System.Enum as (:? System.ConsoleKey as c) -> let d = c + enum 1 in ()
| :? System.Enum as (:? int as x) -> let w = x + 1 in ()
| :? string as y -> let z = y + "" in ()
| _ -> ()
"""
    assertHasSymbolUsages ["a"; "b"; "c"; "d"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,21--5,27): Type constraint mismatch. The type 'int' is not compatible with type 'System.Enum' "
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 04 - duplicate type testing`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
match Unchecked.defaultof<System.ValueType> with
| :? System.Enum as (a & b) -> let c = a = b in ()
| :? System.Enum as (:? System.ConsoleKey as (d & e)) -> let f = d + e + enum 1 in ()
| g -> ()
"""
    assertHasSymbolUsages ["a"; "b"; "c"; "d"; "e"; "f"; "g"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(4,2--4,85): This rule will never be matched"
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 05 - inferred type testing`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
match Unchecked.defaultof<obj> with
| :? _ as a -> let _ = a in ()

match Unchecked.defaultof<int> with
| :? _ as z -> let _ = z in ()
"""
    assertHasSymbolUsages ["a"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(2,6--2,25): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(6,2--6,6): The type 'int' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
    ]

[<Test>]
let ``As 06 - completeness`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
match Unchecked.defaultof<bool> with
| true as a -> if a then ()
| b as false -> if not b then ()

match Unchecked.defaultof<bool> with
| c as true as d -> if c && d then ()
| e as (f as false) -> if e || f then ()

match Unchecked.defaultof<bool> with
| (g & h) as (i as true) as (_ as j) -> if g && h && i && j then ()
| k & l as (m as (false as n)) as (o as _) -> if k || l || m || n || o then ()
"""
    assertHasSymbolUsages (List.map string ['a'..'o']) checkResults
    dumpErrors checkResults |> shouldEqual []

[<Test>]
let ``As 07 - syntactical precedence matrix testing right - total patterns`` () =
    (*
bindingPattern:
  | headBindingPattern
headBindingPattern:
  | headBindingPattern AS constrPattern 
  | headBindingPattern BAR headBindingPattern  
  | headBindingPattern COLON_COLON  headBindingPattern 
  | tuplePatternElements  %prec pat_tuple 
  | conjPatternElements   %prec pat_conj
  | constrPattern 
constrPattern:
  | atomicPatternLongIdent explicitValTyparDecls
  | atomicPatternLongIdent opt_explicitValTyparDecls2 atomicPatsOrNamePatPairs %prec pat_app 
  | atomicPatternLongIdent opt_explicitValTyparDecls2 HIGH_PRECEDENCE_PAREN_APP atomicPatsOrNamePatPairs
  | atomicPatternLongIdent opt_explicitValTyparDecls2 HIGH_PRECEDENCE_BRACK_APP atomicPatsOrNamePatPairs
  | COLON_QMARK atomTypeOrAnonRecdType  %prec pat_isinst
  | atomicPattern
atomicPattern:
  | quoteExpr
  | CHAR DOT_DOT CHAR
  | LBRACE recordPatternElementsAux rbrace
  | LBRACK listPatternElements RBRACK
  | LBRACK_BAR listPatternElements  BAR_RBRACK
  | UNDERSCORE
  | QMARK ident
  | atomicPatternLongIdent %prec prec_atompat_pathop
  | constant
  | FALSE
  | TRUE
  | NULL
  | LPAREN parenPatternBody rparen
  | LPAREN parenPatternBody recover
  | LPAREN error rparen
  | LPAREN recover
  | STRUCT LPAREN tupleParenPatternElements rparen
  | STRUCT LPAREN tupleParenPatternElements recover
  | STRUCT LPAREN error rparen
  | STRUCT LPAREN recover 
parenPatternBody: 
  | parenPattern 
  | /* EMPTY */
parenPattern:
  | parenPattern AS constrPattern
  | parenPattern BAR parenPattern
  | tupleParenPatternElements
  | conjParenPatternElements
  | parenPattern COLON  typeWithTypeConstraints %prec paren_pat_colon
  | attributes parenPattern  %prec paren_pat_attribs
  | parenPattern COLON_COLON  parenPattern
  | constrPattern
    *)
    let _, checkResults = getParseAndCheckResultsPreview $"""
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Id0|) = ignore
let (|Id1|) = id
let (|Id2|) _ = id
type AAA = {{ aaa : int }}
let a = 1
let b as c = 2
let d as e | d & e = 3
let f as g, h = 4, 5
let i as j & k = 6
let l as Id1 m = 7
let n as Id2 a o = 8
let p as {{ aaa = q }} = {{ aaa = 9 }}
let r as _ = 10
let s as Id0 = 11
let t as (u) = 12
let v as struct(w, x) = 13, 14
let y as z : int = 15{set { 'a'..'x' } - set [ 'p'; 'v' ] |> Set.map (sprintf " + %c") |> System.String.Concat}
Some p |> eq<AAA>
Some v |> eq<struct(int * int)>
()
"""
    assertHasSymbolUsages (List.map string ['a'..'z']) checkResults
    dumpErrors checkResults |> shouldEqual []
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 08 - syntactical precedence matrix testing right - partial patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Unit1|_|) x = if System.Random().NextDouble() < 0.5 then Some Unit1 else None
let (|Unit2|_|) _ = (|Unit1|_|)
let (|Id1|_|) x = if System.Random().NextDouble() < 0.5 then Some x else None
let (|Id2|_|) _ = (|Id1|_|)
let a = 1
let b as c::d as e = 2::[3]
let f as Unit1 = 4
let g as Unit2 a h = 5
let i as Id1 j = 6
let k as Id2 a l = 7
box 8 |> function
| m as :? int ->
box {| aaa = 9 |} |> function
| n as :? {| aaa : int |} ->
let o as [p] = [10]
let q as [|r|] = [|11|]
let s as 12 = 12
let t as false = false
let u as true = true
let v as null = null
let w as (null) = null
let x as y : int = 13 + a + b + c + f + g + i + j + k + l + p + r + s
Some d |> eq<int list>
Some e |> eq<int list>
Some h |> eq<unit>
Some m |> eq<obj>
Some n |> eq<obj>
Some o |> eq<int list>
Some q |> eq<int[]>
Some t |> eq<bool>
Some u |> eq<bool>
Some v |> eq<obj>
Some w |> eq<obj>
()
"""
    assertHasSymbolUsages (List.map string ['a'..'y']) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(8,4--8,18): Incomplete pattern matches on this expression. For example, the value '[]' may indicate a case not covered by the pattern(s)."
        "(9,4--9,14): Incomplete pattern matches on this expression."
        "(10,4--10,18): Incomplete pattern matches on this expression."
        "(11,4--11,14): Incomplete pattern matches on this expression."
        "(12,4--12,16): Incomplete pattern matches on this expression."
        "(23,4--23,15): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(22,4--22,13): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(21,4--21,13): Incomplete pattern matches on this expression. For example, the value 'false' may indicate a case not covered by the pattern(s)."
        "(20,4--20,14): Incomplete pattern matches on this expression. For example, the value 'true' may indicate a case not covered by the pattern(s)."
        "(19,4--19,11): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
        "(18,4--18,14): Incomplete pattern matches on this expression. For example, the value '[|_; _|]' may indicate a case not covered by the pattern(s)."
        "(17,4--17,12): Incomplete pattern matches on this expression. For example, the value '[_;_]' may indicate a case not covered by the pattern(s)."
        "(15,21--15,29): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(13,9--13,17): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 09 - syntactical precedence matrix testing right - erroneous patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let (|DefinedPattern|) = id
let a as 1 = true
let b as true = 2
let c as :? int = box 3
let d as :? int = 4
let e as UndefinedPattern = 5
let f as DefinedPattern () = 6
let g as DefinedPattern = 7
let h as , i = 8
let j as : k = 9
let l as :: m = 10
let n as & o = 11
let p as | q = 12
let r as ( s = 13
let t as ) u = 14
let v as struct w = 15
let x as () = y
let z as
"""
    dumpErrors checkResults |> shouldEqual [
        "(10,9--10,10): Unexpected symbol ',' in binding"
        "(11,9--11,10): Unexpected symbol ':' in binding"
        "(12,9--12,11): Unexpected symbol '::' in binding"
        "(13,9--13,10): Unexpected symbol '&' in binding"
        "(14,9--14,10): Unexpected symbol '|' in binding"
        "(15,13--15,14): Unexpected symbol '=' in pattern. Expected ')' or other token."
        "(15,9--15,10): Unmatched '('"
        "(16,0--16,3): Possible incorrect indentation: this token is offside of context started at position (15:1). Try indenting this token further or using standard formatting conventions."
        "(17,16--17,17): Unexpected identifier in pattern. Expected '(' or other token."
        "(20,0--20,0): Incomplete structured construct at or before this point in binding"
        "(3,13--3,17): This expression was expected to have type 'int' but here has type 'bool'"
        "(3,4--3,10): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
        "(4,16--4,17): This expression was expected to have type 'bool' but here has type 'int'"
        "(4,4--4,13): Incomplete pattern matches on this expression. For example, the value 'false' may indicate a case not covered by the pattern(s)."
        "(5,9--5,15): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(6,9--6,15): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(8,29--8,30): This expression was expected to have type 'unit' but here has type 'int'"
        "(9,26--9,27): This expression was expected to have type 'unit' but here has type 'int'"
        "(18,14--18,15): The value or constructor 'y' is not defined."
    ]

[<Test>]
let ``As 10 - syntactical precedence matrix testing left - total patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview $"""
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Id0|) = ignore
let (|Id1|) = id
let (|Id2|) _ = id
type AAA = {{ aaa : int }}
let a = 1
let b as c = 2
let d | d as e = 3
let f, g as h = 4, 5
let i & j as k = 6
let Id1 l as m = 7
let Id2 a n as o = 8
let {{ aaa = p }} as q = {{ aaa = 9 }}
let _ as r = 10
let Id0 as s = 11
let (t) as u = 12
let struct(w, v) as x = 13, 14
let (y : int) as z = 15{set { 'a'..'v' } - set [ 'h'; 'q' ] |> Set.map (sprintf " + %c") |> System.String.Concat}
Some h |> eq<int * int>
Some q |> eq<AAA>
Some x |> eq<struct(int * int)>
()
"""
    assertHasSymbolUsages (List.map string ['a'..'z']) checkResults
    dumpErrors checkResults |> shouldEqual []
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 11 - syntactical precedence matrix testing left - partial patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Unit1|_|) x = if System.Random().NextDouble() < 0.5 then Some Unit1 else None
let (|Unit2|_|) _ = (|Unit1|_|)
let (|Id1|_|) x = if System.Random().NextDouble() < 0.5 then Some x else None
let (|Id2|_|) _ = (|Id1|_|)
let a = 1
let b as (c::d as e) = 2::[3]
let Unit1 as f = 4
let Unit2 a g as h = 5
let Id1 i as j = 6
let Id2 a k as l = 7
box 8 |> function
| :? int as m ->
box {| aaa = 9 |} |> function
| :? {| aaa : int |} as n ->
let [o] as p = [10]
let [|q|] as r = [|11|]
let 12 as s = 12
let false as t = false
let true as u = true
let null as v = null
let (null) as w = null
let (x : int) as y = 13 + a + c + f + h + i + j + k + l + m + o + q + s
Some b |> eq<int list>
Some d |> eq<int list>
Some e |> eq<int list>
Some g |> eq<unit>
Some n |> eq<{| aaa : int |}>
Some p |> eq<int list>
Some r |> eq<int[]>
Some t |> eq<bool>
Some u |> eq<bool>
Some v |> eq<obj>
Some w |> eq<obj>
()
"""
    assertHasSymbolUsages (List.map string ['a'..'y']) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(8,4--8,20): Incomplete pattern matches on this expression. For example, the value '[]' may indicate a case not covered by the pattern(s)."
        "(9,4--9,14): Incomplete pattern matches on this expression."
        "(10,4--10,18): Incomplete pattern matches on this expression."
        "(11,4--11,14): Incomplete pattern matches on this expression."
        "(12,4--12,16): Incomplete pattern matches on this expression."
        "(23,4--23,15): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(22,4--22,13): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(21,4--21,13): Incomplete pattern matches on this expression. For example, the value 'false' may indicate a case not covered by the pattern(s)."
        "(20,4--20,14): Incomplete pattern matches on this expression. For example, the value 'true' may indicate a case not covered by the pattern(s)."
        "(19,4--19,11): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
        "(18,4--18,14): Incomplete pattern matches on this expression. For example, the value '[|_; _|]' may indicate a case not covered by the pattern(s)."
        "(17,4--17,12): Incomplete pattern matches on this expression. For example, the value '[_;_]' may indicate a case not covered by the pattern(s)."
        "(15,21--15,29): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(13,9--13,17): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 12 - syntactical precedence matrix testing left - erroneous patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let (|DefinedPattern|) = id
let 1 as a = true
let true as b = 2
let :? int as c = box 3
let :? int as d = 4
let UndefinedPattern as e = 5
let DefinedPattern () as f = 6
let DefinedPattern as g = 7
let h, as i = 8
let j : _ as k = 9
let l :: as m = 10
let n & as o = 11
let p | as q = 12
let r ( as s = 13
let t ) as u = 14
let v struct as w = 15
let () as x = y
let z as =
"""
    dumpErrors checkResults |> shouldEqual [
        "(10,7--10,9): Unexpected keyword 'as' in binding"
        "(11,10--11,12): Unexpected keyword 'as' in binding. Expected '=' or other token."
        "(12,9--12,11): Unexpected keyword 'as' in binding"
        "(13,8--13,10): Unexpected keyword 'as' in binding"
        "(14,8--14,10): Unexpected keyword 'as' in binding"
        "(15,8--15,10): Unexpected keyword 'as' in pattern. Expected ')' or other token."
        "(15,6--15,7): Unmatched '('"
        "(16,0--16,3): Possible incorrect indentation: this token is offside of context started at position (15:1). Try indenting this token further or using standard formatting conventions."
        "(16,0--16,3): Unexpected keyword 'let' or 'use' in binding. Expected incomplete structured construct at or before this point or other token."
        "(15,0--15,3): Incomplete value or function definition. If this is in an expression, the body of the expression must be indented to the same column as the 'let' keyword."
        "(17,0--17,3): Incomplete structured construct at or before this point in implementation file"
        "(20,0--20,0): Possible incorrect indentation: this token is offside of context started at position (19:1). Try indenting this token further or using standard formatting conventions."
        "(20,0--20,0): Possible incorrect indentation: this token is offside of context started at position (19:1). Try indenting this token further or using standard formatting conventions."
        "(3,13--3,17): This expression was expected to have type 'int' but here has type 'bool'"
        "(3,4--3,10): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
        "(4,16--4,17): This expression was expected to have type 'bool' but here has type 'int'"
        "(4,4--4,13): Incomplete pattern matches on this expression. For example, the value 'false' may indicate a case not covered by the pattern(s)."
        "(5,4--5,10): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(6,4--6,10): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(8,29--8,30): This expression was expected to have type 'unit' but here has type 'int'"
        "(9,26--9,27): This expression was expected to have type 'unit' but here has type 'int'"
        "(15,4--15,5): The pattern discriminator 'r' is not defined."
        "(15,4--15,12): Incomplete pattern matches on this expression."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 13 - syntactical precedence matrix testing right with type tests - total patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview $"""
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Id0|) = ignore
let (|Id1|) = id
let (|Id2|) _ = id
type AAA = {{ aaa : int }}
let a = 1
match box 2 with
| :? int as b as c ->
match box 3 with
| :? int as d | :? int & d as e -> // The left d has type 'int', the right d has type 'obj'
match box 4, 5 with
| :? int as f, g & h ->
match box 6 with
| :? int as i & j as k ->
match box 7 with
| :? int as Id1 l as m ->
match box 8 with
| :? int as Id2 a n as o ->
match box {{ aaa = 9 }} with
| :? AAA as {{ aaa = p }} as q ->
match box 10 with
| :? int as _ as r ->
match box 11 with
| :? int as Id0 as s ->
match box 12 with
| :? int as (t) as u ->
match box struct(13, 14) with
| :? struct(int * int) as struct(v, w) as x ->
let y as z : int = 15 + a + b + d + f + g + h + i + l + n + p + t + v + w
Some c |> eq<obj>
Some e |> eq<obj>
Some j |> eq<obj>
Some k |> eq<obj>
Some m |> eq<obj>
Some o |> eq<obj>
Some q |> eq<obj>
Some r |> eq<obj>
Some s |> eq<obj>
Some u |> eq<obj>
Some x |> eq<obj>
()
"""
    assertHasSymbolUsages (List.map string ['a'..'z']) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(11,25--11,26): This expression was expected to have type 'int' but here has type 'obj'"
        "(28,6--28,24): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(26,6--26,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(24,6--24,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(22,6--22,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(20,6--20,21): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(18,6--18,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(16,6--16,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(14,6--14,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(12,6--12,14): Incomplete pattern matches on this expression. For example, the value '(( some-other-subtype ),_)' may indicate a case not covered by the pattern(s)."
        "(10,6--10,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(8,6--8,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
    ]
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 14 - syntactical precedence matrix testing right with type tests - partial patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Unit1|_|) x = if System.Random().NextDouble() < 0.5 then Some Unit1 else None
let (|Unit2|_|) _ = (|Unit1|_|)
let (|Id1|_|) x = if System.Random().NextDouble() < 0.5 then Some x else None
let (|Id2|_|) _ = (|Id1|_|)
let a = 1
match box 2::[box 3] with
| :? int as b as c::d as e ->
match box 4 with
| :? int as Unit1 as f ->
match box 5 with
| :? int as Unit2 a g as h ->
match box 6 with
| :? int as Id1 i as j ->
match box 7 with
| :? int as Id2 a k as l ->
match box 8 with
| :? System.ValueType as :? int as m ->
match box {| aaa = 9 |} with
| :? obj as :? {| aaa : int |} as n ->
match box [10] with
| :? (int list) as [o] as p ->
match box [|11|] with
| :? (int[]) as [|q|] as r ->
match box 12 with
| :? int as 12 as s ->
match box false with
| :? bool as false as t ->
match box true with
| :? bool as true as u ->
match box null with
| :? System.ValueType as null as v ->
match box null with
| :? System.ValueType as (null) as w ->
let x as y : int = 13 + a + b + i + k + o + q
Some c |> eq<obj>
Some d |> eq<obj list>
Some e |> eq<obj list>
Some f |> eq<obj>
Some g |> eq<unit>
Some h |> eq<obj>
Some j |> eq<obj>
Some l |> eq<obj>
Some m |> eq<obj>
Some n |> eq<obj>
Some p |> eq<obj>
Some r |> eq<obj>
Some s |> eq<obj>
Some t |> eq<obj>
Some u |> eq<obj>
Some v |> eq<obj>
Some w |> eq<obj>
()
"""
    assertHasSymbolUsages (set ['a' .. 'y'] |> Set.remove 'n' |> Set.map string |> Set.toList) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(21,2--21,8): This type test or downcast will always hold"
        "(34,6--34,14): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(32,6--32,14): Incomplete pattern matches on this expression. For example, the value '( some-non-null-value )' may indicate a case not covered by the pattern(s)."
        "(30,6--30,14): Incomplete pattern matches on this expression. For example, the value 'false' may indicate a case not covered by the pattern(s)."
        "(28,6--28,15): Incomplete pattern matches on this expression. For example, the value 'true' may indicate a case not covered by the pattern(s)."
        "(26,6--26,12): Incomplete pattern matches on this expression. For example, the value '0' may indicate a case not covered by the pattern(s)."
        "(24,6--24,16): Incomplete pattern matches on this expression. For example, the value '[|_; _|]' may indicate a case not covered by the pattern(s)."
        "(22,6--22,14): Incomplete pattern matches on this expression. For example, the value '[_;_]' may indicate a case not covered by the pattern(s)."
        "(20,6--20,23): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(18,6--18,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(16,6--16,11): Incomplete pattern matches on this expression."
        "(14,6--14,11): Incomplete pattern matches on this expression."
        "(12,6--12,11): Incomplete pattern matches on this expression."
        "(10,6--10,11): Incomplete pattern matches on this expression."
        "(8,6--8,20): Incomplete pattern matches on this expression. For example, the value '[( some-other-subtype )]' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 15 - syntactical precedence matrix testing right with type tests - erroneous patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let (|DefinedPattern|) = id
let :? a as 1 = true
let :? b as true = 2
let :? c as :? int = box 3
let :? d as :? int = 4
let :? e as UndefinedPattern = 5
let :? f as DefinedPattern () = 6
let :? g as DefinedPattern = 7
let :? h as , i = 8
let :? j as : k = 9
let :? l as :: m = 10
let :? n as & o = 11
let :? p as | q = 12
let :? r as ( s = 13
let :? t as ) u = 14
let :? v as struct w = 15
let :? x as () = y
let :? z as
"""
    dumpErrors checkResults |> shouldEqual [
        "(10,12--10,13): Unexpected symbol ',' in binding"
        "(11,12--11,13): Unexpected symbol ':' in binding"
        "(12,12--12,14): Unexpected symbol '::' in binding"
        "(13,12--13,13): Unexpected symbol '&' in binding"
        "(14,12--14,13): Unexpected symbol '|' in binding"
        "(15,16--15,17): Unexpected symbol '=' in pattern. Expected ')' or other token."
        "(15,12--15,13): Unmatched '('"
        "(16,0--16,3): Possible incorrect indentation: this token is offside of context started at position (15:1). Try indenting this token further or using standard formatting conventions."
        "(17,19--17,20): Unexpected identifier in pattern. Expected '(' or other token."
        "(20,0--20,0): Incomplete structured construct at or before this point in binding"
        "(3,7--3,8): The type 'a' is not defined."
        "(3,4--3,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(4,7--4,8): The type 'b' is not defined."
        "(4,4--4,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(5,7--5,8): The type 'c' is not defined."
        "(5,4--5,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(6,7--6,8): The type 'd' is not defined."
        "(6,4--6,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(7,7--7,8): The type 'e' is not defined."
        "(7,4--7,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(8,7--8,8): The type 'f' is not defined."
        "(8,4--8,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(9,7--9,8): The type 'g' is not defined."
        "(9,4--9,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(15,7--15,8): The type 'r' is not defined."
        "(15,4--15,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(18,7--18,8): The type 'x' is not defined."
        "(18,4--18,8): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 16 - syntactical precedence matrix testing left with type tests - total patterns`` () =
    let validSet = set { 'a'..'x' } - set [ 'p'; 'q' ] |> Set.map string
    let _, checkResults = getParseAndCheckResultsPreview $"""
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Id0|) = ignore
let (|Id1|) = id
let (|Id2|) _ = id
type AAA = {{ aaa : int }}
let a = 1
match box 2 with
| b as :? int as c ->
match box 3 with
| d as :? int | d & :? int as e ->
match box 4, 5 with
| f as :? int, g as h ->
match box 6 with
| i as :? int & j as k ->
match box 7 with
| Id1 l as :? int as m ->
match box 8 with
| Id2 a n as :? int as o ->
match box 10 with
| _ as :? int as r ->
match box 11 with
| Id0 as :? int as s ->
match box 12 with
| (t) as :? int as u ->
match box struct(13, 14) with
| struct(v, w) as :? struct(int * int) as x ->
Some a |> eq<int>
Some g |> eq<int>
Some h |> eq<obj * int>
{validSet - set [ "a"; "g"; "h" ] |> Set.map (sprintf "Some %s |> eq<obj>\n") |> System.String.Concat}
match box {{ aaa = 9 }} with
| {{ aaa = p }} as :? AAA as q ->
Some "" |> eq<int> // No more type checks after the above line?
"""
    assertHasSymbolUsages (Set.toList validSet) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(27,2--27,14): This expression was expected to have type 'obj' but here has type 'struct ('a * 'b)'"
        "(52,2--52,13): This expression was expected to have type 'obj' but here has type 'AAA'"
        "(26,6--26,24): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(24,6--24,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(22,6--22,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(20,6--20,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(18,6--18,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(16,6--16,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(14,6--14,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(12,6--12,14): Incomplete pattern matches on this expression. For example, the value '(( some-other-subtype ),_)' may indicate a case not covered by the pattern(s)."
        "(10,6--10,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(8,6--8,11): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
    ]
    
[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 17 - syntactical precedence matrix testing left with type tests - partial patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let eq<'T> (x:'T option) = () // FS-1093-safe type assert function
let (|Unit1|_|) x = if System.Random().NextDouble() < 0.5 then Some Unit1 else None
let (|Unit2|_|) _ = (|Unit1|_|)
let (|Id1|_|) x = if System.Random().NextDouble() < 0.5 then Some x else None
let (|Id2|_|) _ = (|Id1|_|)
let a = 1
match box 2::[box 3] with
| b as :? int as c::d as e ->
match box 4 with
| Unit1 as :? int as f ->
match box 5 with
| Unit2 a g as :? int as h ->
match box 6 with
| Id1 i as :? int as j ->
match box 7 with
| Id2 a k as :? int as l ->
match box 12 with
| 12 as :? int as s ->
match box false with
| false as :? bool as t ->
match box true with
| true as :? bool as u ->
match box null with
| null as :? System.ValueType as v ->
match box null with
| (null) as :? System.ValueType as w ->
let x as y : int = 13 + a + b
Some a |> eq<int>
Some b |> eq<obj>
Some c |> eq<obj>
Some d |> eq<obj list>
Some e |> eq<obj list>
Some f |> eq<obj>
Some g |> eq<unit>
Some h |> eq<obj>
Some i |> eq<obj>
Some j |> eq<obj>
Some k |> eq<obj>
Some l |> eq<obj>
Some m |> eq<obj>
Some n |> eq<obj>
Some o |> eq<obj>
Some p |> eq<obj>
Some q |> eq<obj>
Some r |> eq<obj>
Some s |> eq<obj>
Some t |> eq<obj>
Some u |> eq<obj>
Some v |> eq<obj>
Some w |> eq<obj>
Some x |> eq<int>
Some y |> eq<int>
match box 8 with
| :? int as :? System.ValueType as m ->
match box {| aaa = 9 |} with
| :? {| aaa : int |} as :? obj as n ->
match box [10] with
| [o] as :? (int list) as p ->
match box [|11|] with
| [|q|] as :? (int[]) as r ->
// No more type checking after the above 4 matches
Some "" |> eq<int>
"""
    assertHasSymbolUsages (set ['a'..'y'] - set [ 'm'..'r' ] |> Set.map string |> Set.toList) checkResults
    dumpErrors checkResults |> shouldEqual [
        "(19,2--19,4): This expression was expected to have type 'obj' but here has type 'int'"
        "(21,2--21,7): This expression was expected to have type 'obj' but here has type 'bool'"
        "(23,2--23,6): This expression was expected to have type 'obj' but here has type 'bool'"
        "(28,28--28,29): The type 'obj' does not match the type 'int'"
        "(41,5--41,6): The value or constructor 'm' is not defined."
        "(42,5--42,6): The value or constructor 'n' is not defined."
        "(43,5--43,6): The value or constructor 'o' is not defined."
        "(44,5--44,6): The value or constructor 'p' is not defined."
        "(45,5--45,6): The value or constructor 'q' is not defined."
        "(46,5--46,6): The value or constructor 'r' is not defined."
        "(55,12--55,31): The type 'int' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
        "(26,6--26,14): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(24,6--24,14): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(22,6--22,14): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(20,6--20,15): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(18,6--18,12): Incomplete pattern matches on this expression. For example, the value '( some-other-subtype )' may indicate a case not covered by the pattern(s)."
        "(16,6--16,11): Incomplete pattern matches on this expression."
        "(14,6--14,11): Incomplete pattern matches on this expression."
        "(12,6--12,11): Incomplete pattern matches on this expression."
        "(10,6--10,11): Incomplete pattern matches on this expression."
        "(8,6--8,20): Incomplete pattern matches on this expression. For example, the value '[( some-other-subtype )]' may indicate a case not covered by the pattern(s)."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 18 - syntactical precedence matrix testing left with type tests - erroneous patterns`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
let (|DefinedPattern|) = id
let 1 as :? a = true
let true as :? b = 2
let :? int as :? c = box 3
let :? int as :? d = 4
let UndefinedPattern as :? e = 5
let DefinedPattern () as :? f = 6
let DefinedPattern as :? g = 7
let h, as :? i = 8
let j : _ as :? k = 9
let l :: as :? m = 10
let n & as :? o = 11
let p | as :? q = 12
let r struct as :? s = 13
let t ( as :? u = 14
let v [ as :? w = 15
let () as :? x = y
let as :? z =
"""
    dumpErrors checkResults |> shouldEqual [
        "(10,7--10,9): Unexpected keyword 'as' in binding"
        "(11,10--11,12): Unexpected keyword 'as' in binding. Expected '=' or other token."
        "(12,9--12,11): Unexpected keyword 'as' in binding"
        "(13,8--13,10): Unexpected keyword 'as' in binding"
        "(14,8--14,10): Unexpected keyword 'as' in binding"
        "(15,13--15,15): Unexpected keyword 'as' in pattern. Expected '(' or other token."
        "(16,8--16,10): Unexpected keyword 'as' in pattern. Expected ')' or other token."
        "(16,6--16,7): Unmatched '('"
        "(17,0--17,3): Possible incorrect indentation: this token is offside of context started at position (16:1). Try indenting this token further or using standard formatting conventions."
        "(17,0--17,3): Unexpected keyword 'let' or 'use' in binding. Expected incomplete structured construct at or before this point or other token."
        "(16,0--16,3): Incomplete value or function definition. If this is in an expression, the body of the expression must be indented to the same column as the 'let' keyword."
        "(17,8--17,10): Unexpected keyword 'as' in pattern. Expected ']' or other token."
        "(18,0--18,3): Possible incorrect indentation: this token is offside of context started at position (17:1). Try indenting this token further or using standard formatting conventions."
        "(19,0--19,3): Possible incorrect indentation: this token is offside of context started at position (18:1). Try indenting this token further or using standard formatting conventions."
        "(20,0--20,0): Possible incorrect indentation: this token is offside of context started at position (19:1). Try indenting this token further or using standard formatting conventions."
        "(20,0--20,0): Possible incorrect indentation: this token is offside of context started at position (19:1). Try indenting this token further or using standard formatting conventions."
        "(3,12--3,13): The type 'a' is not defined."
        "(3,9--3,13): The type 'int' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
        "(4,15--4,16): The type 'b' is not defined."
        "(4,12--4,16): The type 'bool' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
        "(5,4--5,10): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(6,4--6,10): This runtime coercion or type test from type 'a to int involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(7,27--7,28): The type 'e' is not defined."
        "(7,24--7,28): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
        "(8,28--8,29): The type 'f' is not defined."
        "(8,25--8,29): The type 'unit' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
        "(9,25--9,26): The type 'g' is not defined."
        "(9,22--9,26): The type 'unit' does not have any proper subtypes and cannot be used as the source of a type test or runtime coercion."
        "(16,4--16,5): The pattern discriminator 't' is not defined."
        "(16,14--16,15): The type 'u' is not defined."
        "(16,11--16,15): This runtime coercion or type test from type 'a to 'b involves an indeterminate type based on information prior to this program point. Runtime type tests are not allowed on some types. Further type annotations are needed."
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 19 - syntactical precedence matrix testing - valid syntactic patterns that cause type errors later`` () =
    let _, checkResults = getParseAndCheckResultsPreview """
type I() = inherit System.Attribute()
type M() = inherit I()
let 'a'..'b' as c = 'd'
let e as 'f'..'g' = 'h'
let [<I>] j as k = 1
let l as [<M>] n = 2
let <@ o @> as p = 3
let q as <@ r @> = 4
let <@@ s @@> as t = 5
let u as <@@ v @@> = 6
let ?w as x = 7
let y as ?z = 8
()
"""
    dumpErrors checkResults |> shouldEqual [
        "(7,9--7,11): Unexpected symbol '[<' in binding"
        "(4,4--4,12): This construct is deprecated: Character range matches have been removed in F#. Consider using a 'when' pattern guard instead."
        "(4,4--4,17): Incomplete pattern matches on this expression. For example, the value '' '' may indicate a case not covered by the pattern(s)."
        "(5,9--5,17): This construct is deprecated: Character range matches have been removed in F#. Consider using a 'when' pattern guard instead."
        "(5,4--5,17): Incomplete pattern matches on this expression. For example, the value '' '' may indicate a case not covered by the pattern(s)."
        "(8,4--8,11): This is not a valid pattern"
        "(8,4--8,16): Incomplete pattern matches on this expression."
        "(9,9--9,16): This is not a valid pattern"
        "(9,4--9,16): Incomplete pattern matches on this expression."
        "(10,4--10,13): This is not a valid pattern"
        "(10,4--10,18): Incomplete pattern matches on this expression."
        "(11,9--11,18): This is not a valid pattern"
        "(11,4--11,18): Incomplete pattern matches on this expression."
        "(12,4--12,6): Optional arguments are only permitted on type members"
        "(13,9--13,11): Optional arguments are only permitted on type members"
    ]

[<Test>]
#if !NETCOREAPP
[<Ignore("These tests weren't running on desktop and this test fails")>]
#endif
let ``As 20 - limit the right of 'as' patterns to only variable patterns in F# 5`` () =
    let _, checkResults = getParseAndCheckResults """
let f : obj -> _ =
    function
    | :? int as i -> i
    | :? uint as _ -> 0
    | a as :? int64 -> -1
()
"""
    assertHasSymbolUsages ["i"] checkResults
    dumpErrors checkResults |> shouldEqual [
        "(5,6--5,18): Feature 'non-variable patterns to the right of 'as' patterns' is not available in F# 5.0. Please use language version 'preview' or greater."
    ]