// #Regression #Conformance #Quotations 
// Verify restrictions for what can be written in a quotation
//<Expects id="FS0010" status="error">Incomplete structured construct at or before this point in quotation literal</Expects>
//<Expects id="FS0602" status="error">Unmatched '<@ @>'</Expects>
//<Expects id="FS0010" status="error">Unexpected keyword 'type' in quotation literal</Expects>
//<Expects id="FS3118" status="error">Incomplete value or function definition. If this is in an expression, the body of the expression must be indented to the same column as the 'let' keyword.</Expects>
//<Expects id="FS0010" status="error">Unexpected end of quotation in expression. Expected incomplete structured construct at or before this point or other token.</Expects>

let _ = <@ type Foo(x : int) =
              member this.Length = x @>     

exit 1
