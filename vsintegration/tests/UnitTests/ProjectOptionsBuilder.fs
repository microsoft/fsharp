namespace VisualFSharp.UnitTests.Roslyn

open System
open System.IO
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis

module FileSystemHelpers =
    let safeDeleteFile (path: string) =
        try
            File.Delete(path)
        with
        | _ -> ()

    let safeDeleteDirectory (path: string) =
        try
            Directory.Delete(path)
        with
        | _ -> ()

type FSharpProject =
    {
        Directory: string
        Options: FSharpProjectOptions
        Files: (string * string) list
    }

    /// Strips cursor information from each file and returns the name and cursor position of the last file to specify it.
    member this.GetCaretPosition () =
        let caretSentinel = "$$"
        let mutable cursorInfo: (string * int) = (null, 0)
        this.Files
        |> List.iter (fun (name, contents) ->
            // find the '$$' sentinel that represents the cursor location
            let caretPosition = contents.IndexOf(caretSentinel)
            if caretPosition >= 0 then
                let newContents = contents.Substring(0, caretPosition) + contents.Substring(caretPosition + caretSentinel.Length)
                File.WriteAllText(Path.Combine(this.Directory, name), newContents)
                cursorInfo <- (name, caretPosition))
        cursorInfo
    interface IDisposable with
        member this.Dispose() =
            // delete each source file
            this.Files
            |> List.map fst
            |> List.iter FileSystemHelpers.safeDeleteFile
            // delete the directory
            FileSystemHelpers.safeDeleteDirectory (this.Directory)
            // project file doesn't really exist, nothing to delete
            ()

[<AutoOpen>]
module internal ProjectOptionsBuilder =
    let private FileName = XName.op_Implicit "File"
    let private NameName = XName.op_Implicit "Name"
    let private ProjectName = XName.op_Implicit "Project"
    let private ReferenceName = XName.op_Implicit "Reference"

    let private CreateSingleProjectFromMarkup(markup:XElement) =
        if markup.Name.LocalName <> "Project" then failwith "Expected root node to be <Project>"
        let projectRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        if Directory.Exists(projectRoot) then Directory.Delete(projectRoot, true)
        Directory.CreateDirectory(projectRoot) |> ignore
        let files = // filename -> fileContents
            markup.Elements(FileName)
            |> Seq.map (fun file ->
                let fileName = Path.Combine(projectRoot, file.Attribute(NameName).Value)
                let fileContents = file.Value
                File.WriteAllText(fileName, fileContents)
                (fileName, fileContents))
            |> List.ofSeq
        let options =
            {
                ProjectFileName = Path.Combine(projectRoot, markup.Attribute(NameName).Value)
                ProjectId = None
                SourceFiles = files |> Seq.map fst |> Array.ofSeq
                ReferencedProjects = [||] // potentially filled in later
                OtherOptions = [||]
                IsIncompleteTypeCheckEnvironment = true
                UseScriptResolutionRules = false
                LoadTime = DateTime.MaxValue
                OriginalLoadReferences = []
                UnresolvedReferences = None
                Stamp = None
            }
        {
            Directory = projectRoot
            Options = options
            Files = files
        }

    let private CreateMultipleProjectsFromMarkup(markup:XElement) =
        if markup.Name.LocalName <> "Projects" then failwith "Expected root node to be <Projects>"
        let projectsAndXml =
            markup.Elements(ProjectName)
            |> Seq.map (fun xml -> (CreateSingleProjectFromMarkup xml, xml))
            |> List.ofSeq
        // setup project references
        let projectMap =
            projectsAndXml
            |> List.map (fun (projectOptions, _xml) ->
                let normalizedProjectName = Path.GetFileName(projectOptions.Options.ProjectFileName)
                (normalizedProjectName, projectOptions))
            |> Map.ofList
        let projects =
            projectsAndXml
            |> List.map(fun (projectOptions, xml) ->
                // bind references to their `FSharpProjectOptions` counterpart
                let referenceList =
                    xml.Elements(ReferenceName)
                    |> Seq.map (fun reference -> reference.Value)
                    |> Seq.fold (fun list reference -> reference :: list) []
                    |> List.rev
                    |> List.map (fun referencedProject ->
                        let project = projectMap.[referencedProject]
                        let asmName = Path.GetFileNameWithoutExtension(project.Options.ProjectFileName)
                        let binaryPath = Path.Combine(project.Directory, "bin", asmName + ".dll")
                        (binaryPath, project.Options))
                    |> Array.ofList
                let binaryRefs =
                    referenceList
                    |> Array.map fst
                    |> Array.map (fun r -> "-r:" + r)
                let otherOptions = Array.append projectOptions.Options.OtherOptions binaryRefs
                { projectOptions with
                    Options = { projectOptions.Options with
                        ReferencedProjects = referenceList |> Array.map FSharpReferencedProject.CreateFSharp
                        OtherOptions = otherOptions
                    }
                })
        let rootProject = List.head projects
        rootProject
    
    let CreateProjectFromMarkup(markup:XElement) =
        match markup.Name.LocalName with
        | "Project" -> CreateSingleProjectFromMarkup markup
        | "Projects" -> CreateMultipleProjectsFromMarkup markup
        | name -> failwith <| sprintf "Unsupported root node name: %s" name

    let CreateProject(markup:string) =
        XDocument.Parse(markup).Root
        |> CreateProjectFromMarkup

    let SingleFileProject(code:string) =
        code
        |> sprintf @"
<Project Name=""testProject.fsproj"">
  <File Name=""testFile.fs"">
    <![CDATA[%s]]>
  </File>
</Project>
"
        |> CreateProject
