// #Regression #NoMT #CompilerOptions #RequiresENU 
// Test that without [--flaterrors] flag multi-line errors are emitted in a regular way, i.e. spanned to more that one line
//<Expects id="FS0001" status="error" span="(8,11)">This expression was expected to have type</Expects>
//<Expects status="success">        ''a list'</Expects>
//<Expects status="success">but here has type</Expects>
//<Expects status="success">        'seq<'b>'</Expects>

List.rev {1..10}
