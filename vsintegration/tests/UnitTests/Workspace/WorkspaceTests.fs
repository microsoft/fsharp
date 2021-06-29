namespace VisualFSharp.UnitTests

open System
open System.IO
open System.Text
open System.Reflection
open System.Linq
open System.Composition.Hosting
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading
open Microsoft.VisualStudio.Composition
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn
open NUnit.Framework

[<TestFixture>]
module WorkspaceTests =

    let compileFileAsDll (workspace: Workspace) filePath outputFilePath =
        let workspaceService = workspace.Services.GetRequiredService<IFSharpWorkspaceService>()

        try
            let result, _ =
                workspaceService.Checker.Compile([|"fsc.dll";filePath;$"-o:{ outputFilePath }";"--deterministic+";"--optimize+";"--target:library"|])
                |> Async.RunSynchronously

            if result.Length > 0 then
                failwith "Compilation has errors."
        with
        | _ ->
            try File.Delete(outputFilePath) with | _ -> ()
            reraise()

    let createOnDiskScript src =
        let tmpFilePath = Path.GetTempFileName()
        let tmpRealFilePath = Path.ChangeExtension(tmpFilePath, ".fsx")
        try File.Delete(tmpFilePath) with | _ -> ()
        File.WriteAllText(tmpRealFilePath, src)
        tmpRealFilePath

    let createOnDiskCompiledScriptAsDll (workspace: Workspace) src =
        let tmpFilePath = Path.GetTempFileName()
        let tmpRealFilePath = Path.ChangeExtension(tmpFilePath, ".fsx")
        try File.Delete(tmpFilePath) with | _ -> ()
        File.WriteAllText(tmpRealFilePath, src)

        let outputFilePath = Path.ChangeExtension(tmpRealFilePath, ".dll")

        try
            compileFileAsDll workspace tmpRealFilePath outputFilePath
            outputFilePath
        finally
            try File.Delete(tmpRealFilePath) with | _ -> ()

    let createWorkspace() =
        new AdhocWorkspace(TestHostServices())

    let createMiscFileWorkspace() =
        createWorkspace()

    let createProjectInfoWithFileOnDisk filePath =
        RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath, filePath)

    let getDocumentId (workspace: Workspace) filePath =
        let solution = workspace.CurrentSolution
        solution.GetDocumentIdsWithFilePath(filePath) 
        |> Seq.exactlyOne

    let getDocument (workspace: Workspace) filePath =
        let solution = workspace.CurrentSolution
        solution.GetDocumentIdsWithFilePath(filePath) 
        |> Seq.exactlyOne
        |> solution.GetDocument

    let openDocument (workspace: Workspace) (filePath: string) =
        let docId =
            workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath) 
            |> Seq.exactlyOne
        use waitHandle = new ManualResetEventSlim(false)
        use _sub = workspace.DocumentOpened.Subscribe(fun _ ->
            waitHandle.Set()
        )
        workspace.OpenDocument(docId)
        waitHandle.Wait()

    let updateDocumentOnDisk (workspace: Workspace) filePath src =
        File.WriteAllText(filePath, src)

        let doc = getDocument workspace filePath
        // The adhoc workspaces do not listen for files changing on disk,
        // so we simulate the update here.
        let newSolution = 
            doc.Project.Solution.WithDocumentTextLoader(
                doc.Id, 
                new FileTextLoader(doc.FilePath, Encoding.Default),
                PreservationMode.PreserveIdentity)
        workspace.TryApplyChanges(newSolution) |> ignore

    let updateCompiledDllOnDisk workspace (dllPath: string) src =
        if not (File.Exists dllPath) then
            failwith $"File {dllPath} does not exist."

        let filePath = createOnDiskScript src

        try
            compileFileAsDll workspace filePath dllPath

            let newMetadataReference = 
                PortableExecutableReference.CreateFromFile(
                    dllPath, 
                    MetadataReferenceProperties.Assembly
                )
            // The adhoc workspaces do not listen for files changing on disk,
            // so we simulate the update here.
            let solution = workspace.CurrentSolution
            let projects = solution.Projects
            let newSolution =
                (solution, projects)
                ||> Seq.fold (fun solution proj ->
                    let mutable mustUpdate = false
                    let metadataReferences =
                        proj.MetadataReferences
                        |> Array.ofSeq
                        |> Array.map (fun x ->
                            match x with
                            | :? PortableExecutableReference as x ->
                                if String.Equals(x.FilePath, newMetadataReference.FilePath, StringComparison.OrdinalIgnoreCase) then
                                    mustUpdate <- true
                                    newMetadataReference :> MetadataReference
                                else
                                    x :> MetadataReference
                            | _ ->
                                x
                        )

                    if mustUpdate then
                        solution.WithProjectMetadataReferences(proj.Id, metadataReferences)
                    else
                        solution
                )
            workspace.TryApplyChanges(newSolution) |> ignore
        finally
            try File.Delete(filePath) with | _ -> ()

    let isDocumentOpen (workspace: Workspace) filePath =
        let docId = getDocumentId workspace filePath
        workspace.IsDocumentOpen(docId)

    let hasDocument (workspace: Workspace) filePath =
        workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).Length > 0

    let addProject (workspace: Workspace) projInfo =
        if not (workspace.TryApplyChanges(workspace.CurrentSolution.AddProject(projInfo))) then
            failwith "Unable to apply workspace changes."

    let removeProject (workspace: Workspace) projId =
        if not (workspace.TryApplyChanges(workspace.CurrentSolution.RemoveProject(projId))) then
            failwith "Unable to apply workspace changes."

    let assertEmptyDocumentDiagnostics (workspace: Workspace) (filePath: string) =
        let doc = getDocument workspace filePath
        let parseResults, checkResults = doc.GetFSharpParseAndCheckResultsAsync("assertEmptyDocumentDiagnostics") |> Async.RunSynchronously
        
        Assert.IsEmpty(parseResults.Diagnostics)
        Assert.IsEmpty(checkResults.Diagnostics)

    let assertHasDocumentDiagnostics (workspace: Workspace) (filePath: string) =
        let doc = getDocument workspace filePath
        let parseResults, checkResults = doc.GetFSharpParseAndCheckResultsAsync("assertHasDocumentDiagnostics") |> Async.RunSynchronously
        
        Assert.IsEmpty(parseResults.Diagnostics)
        Assert.IsNotEmpty(checkResults.Diagnostics)

    type TestFSharpWorkspaceProjectContext(mainProj: Project) =

        let mutable mainProj = mainProj

        interface IFSharpWorkspaceProjectContext with

            member this.Dispose(): unit = ()

            member this.FilePath: string = mainProj.FilePath

            member this.HasProjectReference(filePath: string): bool = 
                mainProj.ProjectReferences
                |> Seq.exists (fun x ->
                    let projRef = mainProj.Solution.GetProject(x.ProjectId)
                    if projRef <> null then
                        String.Equals(filePath, projRef.FilePath, StringComparison.OrdinalIgnoreCase)
                    else
                        false
                )

            member this.Id: ProjectId = mainProj.Id

            member this.ProjectReferenceCount: int = mainProj.ProjectReferences.Count()

            member this.SetProjectReferences(projRefs: seq<IFSharpWorkspaceProjectContext>): unit = 
                let currentProj = mainProj
                let mutable solution = currentProj.Solution

                currentProj.ProjectReferences
                |> Seq.iter (fun projRef ->
                    solution <- solution.RemoveProjectReference(currentProj.Id, projRef)
                )

                projRefs
                |> Seq.iter (fun projRef ->
                    solution <- 
                        solution.AddProjectReference(
                            currentProj.Id, 
                            ProjectReference(projRef.Id)
                        )
                )

                not (solution.Workspace.TryApplyChanges(solution)) |> ignore

                mainProj <- solution.GetProject(currentProj.Id)

            member this.MetadataReferenceCount: int = mainProj.MetadataReferences.Count

            member this.HasMetadataReference(referencePath: string): bool =
                mainProj.MetadataReferences
                |> Seq.exists (fun x -> 
                    match x with
                    | :? PortableExecutableReference as r ->
                        String.Equals(r.FilePath, referencePath, StringComparison.OrdinalIgnoreCase)
                    | _ ->
                        false)

            member this.SetMetadataReferences(referencePaths: string seq): unit =
                let currentProj = mainProj
                let mutable solution = currentProj.Solution

                currentProj.MetadataReferences
                |> Seq.iter (fun r ->
                    solution <- solution.RemoveMetadataReference(currentProj.Id, r)
                )

                referencePaths
                |> Seq.iter (fun referencePath ->
                    solution <- 
                        solution.AddMetadataReference(
                            currentProj.Id, 
                            PortableExecutableReference.CreateFromFile(
                                referencePath,
                                MetadataReferenceProperties.Assembly
                            )
                        )
                )

                not (solution.Workspace.TryApplyChanges(solution)) |> ignore

                mainProj <- solution.GetProject(currentProj.Id)

    type TestFSharpWorkspaceProjectContextFactory(workspace: Workspace, miscFilesWorkspace: Workspace) =
                
        interface IFSharpWorkspaceProjectContextFactory with
            member this.CreateProjectContext(filePath: string): IFSharpWorkspaceProjectContext =
                match miscFilesWorkspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath) |> Seq.tryExactlyOne with
                | Some docId ->
                    let doc = miscFilesWorkspace.CurrentSolution.GetDocument(docId)
                    removeProject miscFilesWorkspace doc.Project.Id
                | _ ->
                    ()

                let projInfo = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(FSharpConstants.FSharpMiscellaneousFilesName, filePath)
                addProject workspace projInfo

                let proj = workspace.CurrentSolution.GetProject(projInfo.Id)
                new TestFSharpWorkspaceProjectContext(proj) :> IFSharpWorkspaceProjectContext

    [<Test>]
    let ``Script file opened in misc files workspace will get transferred to normal workspace``() =
        use workspace = createWorkspace()
        use miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let filePath = 
            createOnDiskScript 
                """
module Script1

let x = 1
                """
        
        try
            let projInfo = createProjectInfoWithFileOnDisk filePath
            addProject miscFilesWorkspace projInfo

            Assert.IsTrue(hasDocument miscFilesWorkspace filePath)
            Assert.IsFalse(hasDocument workspace filePath)

            Assert.IsFalse(isDocumentOpen miscFilesWorkspace filePath)
            openDocument miscFilesWorkspace filePath

            // Although we opened the document, it has been transferred to the other workspace.
            Assert.IsFalse(hasDocument miscFilesWorkspace filePath)
            Assert.IsTrue(hasDocument workspace filePath)

            // Should not be automatically opened when transferred.
            Assert.IsFalse(isDocumentOpen workspace filePath)

            assertEmptyDocumentDiagnostics workspace filePath

        finally
            try File.Delete(filePath) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script should have no diagnostics``() =
        use workspace = createWorkspace()
        use miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let filePath1 = 
            createOnDiskScript 
                """
module Script1

let x = 1
                """

        let filePath2 = 
            createOnDiskScript 
                $"""
module Script2
#load "{ Path.GetFileName(filePath1) }"

let x = Script1.x
                """
        
        try
            let projInfo2 = createProjectInfoWithFileOnDisk filePath2
            addProject miscFilesWorkspace projInfo2
            openDocument miscFilesWorkspace filePath2       
            assertEmptyDocumentDiagnostics workspace filePath2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script will correctly update when the referenced script file changes``() =
        use workspace = createWorkspace()
        use miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let filePath1 = 
            createOnDiskScript 
                """
module Script1
                """

        let filePath2 = 
            createOnDiskScript 
                $"""
module Script2
#load "{ Path.GetFileName(filePath1) }"

let x = Script1.x
                """
        
        try
            let projInfo1 = createProjectInfoWithFileOnDisk filePath1
            let projInfo2 = createProjectInfoWithFileOnDisk filePath2

            addProject miscFilesWorkspace projInfo1
            addProject miscFilesWorkspace projInfo2

            openDocument miscFilesWorkspace filePath1
            openDocument miscFilesWorkspace filePath2           
            
            assertEmptyDocumentDiagnostics workspace filePath1
            assertHasDocumentDiagnostics workspace filePath2

            updateDocumentOnDisk workspace filePath1
                """
module Script1

let x = 1
                """

            assertEmptyDocumentDiagnostics workspace filePath2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script will correctly update when the referenced script file changes with opening in reverse order``() =
        use workspace = createWorkspace()
        use miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let filePath1 = 
            createOnDiskScript 
                """
module Script1
                """

        let filePath2 = 
            createOnDiskScript 
                $"""
module Script2
#load "{ Path.GetFileName(filePath1) }"

let x = Script1.x
                """
        
        try
            let projInfo1 = createProjectInfoWithFileOnDisk filePath1
            let projInfo2 = createProjectInfoWithFileOnDisk filePath2

            addProject miscFilesWorkspace projInfo1
            addProject miscFilesWorkspace projInfo2

            openDocument miscFilesWorkspace filePath2
            openDocument miscFilesWorkspace filePath1         
            
            assertHasDocumentDiagnostics workspace filePath2 
            assertEmptyDocumentDiagnostics workspace filePath1

            updateDocumentOnDisk workspace filePath1
                """
module Script1

let x = 1
                """

            assertEmptyDocumentDiagnostics workspace filePath2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()

    [<Test>]
    let ``Script file referencing a DLL will correctly update when the referenced DLL file changes``() =
        use workspace = createWorkspace()
        use miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let dllPath1 = 
            createOnDiskCompiledScriptAsDll workspace
                """
module Script1

let x = 1
                """

        let filePath1 = 
            createOnDiskScript 
                $"""
module Script2
#r "{ Path.GetFileName(dllPath1) }"

let x = Script1.x
                """
        
        try
            let projInfo1 = createProjectInfoWithFileOnDisk filePath1

            addProject miscFilesWorkspace projInfo1

            openDocument miscFilesWorkspace filePath1         

            assertEmptyDocumentDiagnostics workspace filePath1

            updateDocumentOnDisk workspace filePath1
                $"""
module Script2
#r "{ Path.GetFileName(dllPath1) }"

let x = Script1.x
let y = Script1.y
                """

            assertHasDocumentDiagnostics workspace filePath1

            updateCompiledDllOnDisk workspace dllPath1
                """
module Script1

let x = 1
let y = 1
                """

            assertEmptyDocumentDiagnostics workspace filePath1

        finally
            try File.Delete(dllPath1) with | _ -> ()
            try File.Delete(filePath1) with | _ -> ()
