// #Regression #Diagnostics #Async 
// Regression tests for FSHARP1.0:4394
//<Expects status="error" span="(7,18-7,19)" id="FS0001">All branches of an 'if' expression must return values implicitly convertible to the type of the first branch</Expects>
async { if true then 
          return ()
        else
          return 1 } |> ignore
