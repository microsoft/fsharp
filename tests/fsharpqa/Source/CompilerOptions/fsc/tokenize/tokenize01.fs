// #NoMT #CompilerOptions 
#light

namespace N
   module M =
      let f x = ()
      f 10

//<Expects status="success">tokenize - got NAMESPACE</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got OBLOCKBEGIN</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_COMING_SOON</Expects>
//<Expects status="success">tokenize - got MODULE_IS_HERE</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got EQUALS</Expects>
//<Expects status="success">tokenize - got OBLOCKBEGIN</Expects>
//<Expects status="success">tokenize - got OLET</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got EQUALS</Expects>
//<Expects status="success">tokenize - got OBLOCKBEGIN</Expects>
//<Expects status="success">tokenize - got LPAREN</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_COMING_SOON</Expects>
//<Expects status="success">tokenize - got RPAREN_IS_HERE</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_IS_HERE</Expects>
//<Expects status="success">tokenize - got ODECLEND</Expects>
//<Expects status="success">tokenize - got OBLOCKSEP</Expects>
//<Expects status="success">tokenize - got IDENT</Expects>
//<Expects status="success">tokenize - got INT32</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_IS_HERE</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_COMING_SOON</Expects>
//<Expects status="success">tokenize - got OBLOCKEND_IS_HERE</Expects>
//<Expects status="success">tokenize - got EOF</Expects>