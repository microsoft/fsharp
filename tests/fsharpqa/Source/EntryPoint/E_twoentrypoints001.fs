// #Regression #NoMT #EntryPoint 
// Regression test for FSHARP1.0:1304
// Explicit program entry point: [<ExtryPoint>]
// attribute on multiple functions
//<CmdLine>Hello</CmdLine>
//<Expects id="FS0433" span="(18,5-19,19)" status="error">A function labeled with the 'EntryPointAttribute' attribute must be the last declaration in the last file in the compilation sequence.</Expects>

#light
module M =
    let func (args : string[]) = 
        if(args.Length=1 && args.[0]="Hello") then 0 else 1

    [<EntryPoint>]
    let main args =
       let res = func(args)
       exit(res)

    [<EntryPoint>]
    let main2 args =
       let res = func(args)
       exit(res)
