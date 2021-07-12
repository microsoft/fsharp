// #NoMT #CompilerOptions 
#light

namespace N
   module M =
      let f x = ()
      f 10

//<Expects status="success">tokenize - got NAMESPACE</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got MODULE</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got EQUALS</Expects>
//<Expects status="success">tokenize - got LET</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got EQUALS</Expects>
//<Expects status="success">tokenize - got LPAREN</Expects>
//<Expects status="success">tokenize - got RPAREN</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got INT32</Expects>
//<Expects status="success">tokenize - got EOF</Expects>