module FsiExtension
open System.IO
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open NUnit.Framework

[<Test>]
let ``test 1 `` () =
  let checker = FSharpChecker.Create(suggestNamesForErrors=true, keepAssemblyContents=true)
  let sourceText = """
  #r "nuget: FSharp.Data"
  let v = FSharp.Data.JsonValue.Boolean true
  """                           
  let projectOptions = 
    checker.GetProjectOptionsFromScript("test.fsx", SourceText.ofString sourceText, otherFlags = [| "/langversion:preview"; |] )
    |> Async.RunSynchronously
    |> fst
  
  let wholeProjectResults, answer = checker.ParseAndCheckFileInProject("test.fsx", 0, SourceText.ofString sourceText, projectOptions) |> Async.RunSynchronously
  printfn "answeer %A" answer
  let errors,a,assembly = checker.CompileToDynamicAssembly([wholeProjectResults.ParseTree.Value], "foo", [], None) |> Async.RunSynchronously
  
  printfn "errors: %A" errors 
  printfn "a %A" a 
  printfn "assembly %A" assembly 
  printfn "%A" (wholeProjectResults).ParseTree 
  ()

