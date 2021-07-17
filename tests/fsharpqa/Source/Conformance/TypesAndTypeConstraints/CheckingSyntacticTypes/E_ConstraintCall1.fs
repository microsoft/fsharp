// #Conformance #ConstraintCall
//<Expects id="FS0001" status="error">The types 'int, bool, string' do not support the operator 'M'</Expects>
let inline h (x, y, z) = ((^a or ^b or ^c) : (static member M : ^a * ^b * ^c -> ^d) (x,y,z))
let _ : int = h  (1,false,"")   
