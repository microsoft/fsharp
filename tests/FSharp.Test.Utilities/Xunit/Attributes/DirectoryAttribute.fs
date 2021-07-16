namespace FSharp.Test.Xunit.Attributes

open System
open System.IO
open System.Reflection
open Xunit.Sdk

open FSharp.Compiler.IO

open FSharp.Test
open FSharp.Test.Compiler

/// Attribute to use with Xunit's TheoryAttribute.
/// Takes a directory, relative to current test suite's root.
/// Returns a CompilationUnit with encapsulated source code, error baseline and IL baseline (if any).
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)>]
[<NoComparison; NoEquality>]
type DirectoryAttribute(dir: string) =
    inherit DataAttribute()
    do
        if String.IsNullOrWhiteSpace(dir) then
            invalidArg "dir" "Directory cannot be null, empty or whitespace only."

    let directory = dir

    let mutable includes = Array.empty<string>

    let readFileOrDefault (path: string) : string option =
        match FileSystem.FileExistsShim(path) with
            | true -> Some <| File.ReadAllText path
            | _ -> None

    let createCompilationUnit path fs =
        let filePath = path ++ fs
        let fsSource = File.ReadAllText filePath
        let bslFilePath = filePath + ".bsl"
        let ilFilePath  = filePath + ".il"
        let bslSource = readFileOrDefault bslFilePath
        let ilSource = readFileOrDefault ilFilePath

        { Source         = Text fsSource
          Baseline       =
                Some { SourceFilename = Some filePath
                       OutputBaseline = { FilePath = bslFilePath; Content = bslSource }
                       ILBaseline     = { FilePath = ilFilePath;  Content = ilSource  } }
          Options        = []
          OutputType     = Library
          SourceKind     = SourceKind.Fsx
          Name           = Some fs
          IgnoreWarnings = false
          References     = [] } |> FS

    member _.Includes with get() = includes and set v = includes <- v

    override _.GetData(_: MethodInfo) =
        let absolutePath = Path.GetFullPath(directory)

        if not (Directory.Exists(absolutePath)) then
            failwith (sprintf "Directory does not exist: \"%s\"." absolutePath)

        let allFiles : string[] = Directory.GetFiles(absolutePath, "*.fs")

        let filteredFiles =
            match (includes |> Array.map (fun f -> absolutePath ++ f)) with
                | [||] -> allFiles
                | incl -> incl

        let fsFiles = filteredFiles |> Array.map Path.GetFileName

        if fsFiles |> Array.length < 1 then
            failwith (sprintf "No required files found in \"%s\".\nAll files: %A.\nIncludes:%A." absolutePath allFiles includes)

        for f in filteredFiles do
            if not <| FileSystem.FileExistsShim(f) then
                failwithf "Requested file \"%s\" not found.\nAll files: %A.\nIncludes:%A." f allFiles includes

        fsFiles
        |> Array.map (fun fs -> createCompilationUnit absolutePath fs)
        |> Seq.map (fun c -> [| c |])
