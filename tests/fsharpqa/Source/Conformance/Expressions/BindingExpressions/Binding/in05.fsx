// #in #BindingExpressions
//
// These are pretty pathological cases related to ";;" and "in"
// I'm adding these cases to make sure we do not accidentally change the behavior from version to version
// Eventually, we will deprecated them - and the specs will be updated.
//<Expects status="error" span="(10,9-10,10)" id="FS0001">The types 'unit, int' do not support the operator</Expects>

let a = 3 in
    a + 1 |> ignore
    a + 1 |> ignore
a > 4
