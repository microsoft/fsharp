#light

// Test origionally from grammar production coverage, making sure we put spaces in odd places
// allowed by F# language grammar

// RULE: 319      cArg -> opt_attributes cType 
// RULE 322 cType -> cType opt_HIGH_PRECEDENCE_APP LBRACK RBRACK 
// 324      cType -> cType AMP 

//For example, find a P/Invoke where the attributes on arguments matter:
//For example, find a P/Invoke where the C# signature uses a C# array type:

//    [<DllImport(@"blas.dll",EntryPoint="dgemm_")>]
//    extern void DoubleMatrixMultiply_([<FooBaz>] char* transa,        <----
//                                      char[] transb,                  <---- 322
//                                      char  [] transb,                <---- 322  , note also put in a space!!
//                                      char& transb,                   <---- 322  , note also put in a space!!
//                                      int* m, int* n, int *k,
//                                      double* alpha, double* A, int* lda,double* B, int* ldb,
//                                      double* beta,
//                                      double* C, int* ldc);

open System.IO
open System.Runtime.InteropServices

// Get two temp files, write data into one of them
let tempFile1, tempFile2 = tryCreateTemporaryFileName (), tryCreateTemporaryFileName ()
let writer = new StreamWriter (tempFile1)
writer.WriteLine("Some Data")
writer.Close()

// Origional signature
//[<DllImport("kernel32.dll")>]
//extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);

[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_Arrays(char[] lpExistingFileName, char[] lpNewFileName, bool bFailIfExists);

let result = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "Array %A" result

[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_ArraySpaces(char [] lpExistingFileName, char []lpNewFileName, bool bFailIfExists);

let result2 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "Array Space %A" result2

[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_PassByRef(char [] lpExistingFileName, char []lpNewFileName, bool& bFailIfExists);

let result3 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "ByRef %A" result3

[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_PassByRefSpace(char [] lpExistingFileName, char []lpNewFileName, bool & bFailIfExists);

let result4 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "ByRef Space %A" result4

type SomeAttrib() = 
    inherit System.Attribute() as base
    
[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_Attrib([<SomeAttrib>] char [] lpExistingFileName, char []lpNewFileName, [<SomeAttrib()>] bool & bFailIfExists);

let result5 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "WithAttribute %A" result5

// Cleanup
File.Delete(tempFile1)
File.Delete(tempFile2)

exit 0