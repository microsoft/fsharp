// #Conformance #DeclarationElements #PInvoke 
#light

// Verify ability to map an F# function to a differently named Win32 method
// via the 'EntryPoint' parameter.

// Testcase 

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
    inherit System.Attribute()
    
[<DllImport("kernel32.dll", EntryPoint="CopyFile")>]
extern bool CopyFile_Attrib([<SomeAttrib>] char [] lpExistingFileName, char []lpNewFileName, [<SomeAttrib()>] bool & bFailIfExists);

let result5 = CopyFile_Arrays(tempFile1.ToCharArray(), tempFile2.ToCharArray(), false)
printfn "WithAttribute %A" result5

// Cleanup
File.Delete(tempFile1)
File.Delete(tempFile2)

exit 0
