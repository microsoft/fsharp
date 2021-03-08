
#r "System.dll"

printfn "hello from %s" __SOURCE_FILE__

System.Environment.CurrentDirectory

#r "netstandard.dll"

// A .netcoreapp3.1 api
let f x = System.Runtime.InteropServices.NativeLibrary.Free(x)
 

1+1

//#r "nuget: DiffSharp.Core,1.0.0-preview-263264614"

//#r "nuget: Quack"




//#r "System.EnterpriseServices.dll"
//// A netfx api
//let f2 (x: System.EnterpriseServices.Activity) = ()

