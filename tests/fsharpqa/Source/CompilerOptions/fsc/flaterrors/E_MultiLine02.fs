// #Regression #NoMT #CompilerOptions 
// Test that using [--flaterrors] flag multi-line errors are flattened, i.e. concatenated into one-line error message.
//<Expects status="error" span="(5,11)" id="FS0001">This expression was expected to have type.    ''a list'    .but here has type.    'seq<'b>'</Expects>

List.rev {1..10} |> ignore
