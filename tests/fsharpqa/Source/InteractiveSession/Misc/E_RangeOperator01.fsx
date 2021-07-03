// #Regression #NoMT #FSI 
// Regression test for FSharp1.0:4164 - FSI emit "please report to fsbugs" error on malformed usage of .. operator

//<Expects status="notin">fsbug</Expects>
//<Expects status="notin">nonTerminalId\.GetTag</Expects>
//<Expects status="error" span="(8,1)" id="FS0751">Incomplete expression or invalid use of indexer syntax</Expects>

aaaa..;;

