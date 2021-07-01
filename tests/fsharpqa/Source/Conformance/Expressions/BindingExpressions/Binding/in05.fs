// #in #BindingExpressions
//
// These are pretty pathological cases related to ";;" and "in"
// I'm adding these cases to make sure we do not accidentally change the behavior from version to version
// Eventually, we will deprecated them - and the specs will be updated.
//<Expects status="error" span="(12,13-12,14)" id="FS0001">The type 'int' does not match the type 'unit'$</Expects>
//<Expects status="error" span="(13,5-13,10)" id="FS0020">The result of this expression has type 'bool' and is implicitly ignored\. Consider using 'ignore' to discard this value explicitly, e\.g\. 'expr \|> ignore', or 'let' to bind the result to a name, e\.g\. 'let result = expr'.$</Expects>

module E = 
    let a = 3 in
        a + 1 |> ignore
        a + 1 |> ignore
    a > 4
