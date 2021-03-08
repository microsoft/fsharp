
#r "System.dll"

printfn "hello from %s" __SOURCE_FILE__

System.Environment.CurrentDirectory

#r "netstandard.dll"

// A .netcoreapp3.1 api
let f x = System.Runtime.InteropServices.NativeLibrary.Free(x)
 

1+1
