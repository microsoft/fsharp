// #NoMT #CompilerOptions 
#light

namespace N
   module M =
      let f x = ()
      f 10

namespace N2
   module M2 =
      let f2 x = ()
      f2 10

   module M3 =
   exit 0

//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Import mscorlib and FSharp.Core.dll\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Parse inputs\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Import non-system references\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Typecheck\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Write XML docs\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Encode Interface Data\]</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[TAST -> IL\]</Expects>
//<Expects status="success">ilwrite: TIME.+Write Started</Expects>
//<Expects status="success">ilwrite: TIME.+Module Generation Preparation</Expects>
//<Expects status="success">ilwrite: TIME.+Module Generation Pass 1</Expects>
//<Expects status="success">ilwrite: TIME.+Module Generation Pass 2</Expects>
//<Expects status="success">ilwrite: TIME.+Module Generation Pass 3</Expects>
//<Expects status="success">ilwrite: TIME.+Module Generation Pass 4</Expects>
//<Expects status="success">ilwrite: TIME.+Finalize Module Generation Results</Expects>
//<Expects status="success">ilwrite: TIME.+Generated Tables and Code</Expects>
//<Expects status="success">ilwrite: TIME.+Layout Header of Tables</Expects>
//<Expects status="success">ilwrite: TIME.+Build String/Blob Address Tables</Expects>
//<Expects status="success">ilwrite: TIME.+Sort Tables</Expects>
//<Expects status="success">ilwrite: TIME.+Write Header of tablebuf</Expects>
//<Expects status="success">ilwrite: TIME.+Write Tables to tablebuf</Expects>
//<Expects status="success">ilwrite: TIME.+Layout Metadata</Expects>
//<Expects status="success">ilwrite: TIME.+Write Metadata Header</Expects>
//<Expects status="success">ilwrite: TIME.+Write Metadata Tables</Expects>
//<Expects status="success">ilwrite: TIME.+Write Metadata Strings</Expects>
//<Expects status="success">ilwrite: TIME.+Write Metadata User Strings</Expects>
//<Expects status="success">ilwrite: TIME.+Write Blob Stream</Expects>
//<Expects status="success">ilwrite: TIME.+Fixup Metadata</Expects>
//<Expects status="success">ilwrite: TIME.+Generated IL and metadata</Expects>
//<Expects status="success">ilwrite: TIME.+Layout image</Expects>
//<Expects status="success">ilwrite: TIME.+Writing Image</Expects>
//<Expects status="success">ilwrite: TIME.+Finalize PDB</Expects>
//<Expects status="success">ilwrite: TIME.+Signing Image</Expects>
//<Expects status="success">TIME:.+Delta:.+Mem:.+G0:.+G1:.+G2:.+\[Write .NET Binary\]</Expects>
