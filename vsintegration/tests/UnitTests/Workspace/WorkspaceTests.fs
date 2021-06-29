namespace VisualFSharp.UnitTests

open System
open System.IO
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

    let createOnDiskScript src =
        let tmpFilePath = Path.GetTempFileName()
        let tmpRealFilePath = Path.ChangeExtension(tmpFilePath, ".fsx")
        try File.Delete(tmpFilePath) with | _ -> ()
        File.WriteAllText(tmpRealFilePath, src)
        tmpRealFilePath

    let createWorkspace() =
        new AdhocWorkspace(TestHostServices())

    let createMiscFileWorkspace() =
        createWorkspace()

    let openDocument (workspace: Workspace) (docId: DocumentId) =
        use waitHandle = new ManualResetEventSlim(false)
        use _sub = workspace.DocumentOpened.Subscribe(fun _ ->
            waitHandle.Set()
        )
        workspace.OpenDocument(docId)
        waitHandle.Wait()

    let getDocument (workspace: Workspace) filePath =
        let solution = workspace.CurrentSolution
        solution.GetDocumentIdsWithFilePath(filePath) 
        |> Seq.exactlyOne
        |> solution.GetDocument

    let addProject (workspace: Workspace) projInfo =
        if not (workspace.TryApplyChanges(workspace.CurrentSolution.AddProject(projInfo))) then
            failwith "Unable to apply workspace changes."

    let removeProject (workspace: Workspace) projId =
        if not (workspace.TryApplyChanges(workspace.CurrentSolution.RemoveProject(projId))) then
            failwith "Unable to apply workspace changes."

    let assertEmptyDocumentDiagnostics (doc: Document) =
        let parseResults, checkResults = doc.GetFSharpParseAndCheckResultsAsync("assertEmptyDocumentDiagnostics") |> Async.RunSynchronously
        
        Assert.IsEmpty(parseResults.Diagnostics)
        Assert.IsEmpty(checkResults.Diagnostics)

    let assertHasDocumentDiagnostics (doc: Document) =
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

                mainProj.ProjectReferences
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
        let workspace = createWorkspace()
        let miscFilesWorkspace = createMiscFileWorkspace()
        let projectContextFactory = TestFSharpWorkspaceProjectContextFactory(workspace, miscFilesWorkspace)

        let _miscFileService = FSharpMiscellaneousFileService(workspace, miscFilesWorkspace, projectContextFactory)

        let filePath = 
            createOnDiskScript 
                """
module Script1

let x = 1
                """
        
        try
            let projInfo = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath, filePath)
            addProject miscFilesWorkspace projInfo
            
            let doc = getDocument miscFilesWorkspace filePath

            Assert.IsFalse(miscFilesWorkspace.IsDocumentOpen(doc.Id))
            Assert.AreEqual(0, workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).Length)

            openDocument miscFilesWorkspace doc.Id
            // Although we opened the document, this is false as it has been transferred to the other workspace.
            Assert.IsFalse(miscFilesWorkspace.IsDocumentOpen(doc.Id))

            Assert.AreEqual(0, miscFilesWorkspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).Length)
            Assert.AreEqual(1, workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).Length)

            let doc = getDocument workspace filePath

            Assert.IsFalse(workspace.IsDocumentOpen(doc.Id))

            assertEmptyDocumentDiagnostics doc

        finally
            try File.Delete(filePath) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script should have no diagnostics``() =
        let workspace = createWorkspace()
        let miscFilesWorkspace = createMiscFileWorkspace()
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
            let projInfo2 = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath2, filePath2)

            addProject miscFilesWorkspace projInfo2

            openDocument miscFilesWorkspace (getDocument miscFilesWorkspace filePath2).Id            

            let doc2 = getDocument workspace filePath2
            assertEmptyDocumentDiagnostics doc2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script will correct update when the referenced script file changes``() =
        let workspace = createWorkspace()
        let miscFilesWorkspace = createMiscFileWorkspace()
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
            let projInfo1 = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath1, filePath1)
            let projInfo2 = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath2, filePath2)

            addProject miscFilesWorkspace projInfo1
            addProject miscFilesWorkspace projInfo2

            openDocument miscFilesWorkspace (getDocument miscFilesWorkspace filePath1).Id
            openDocument miscFilesWorkspace (getDocument miscFilesWorkspace filePath2).Id            
            
            let doc1 = getDocument workspace filePath1
            assertEmptyDocumentDiagnostics doc1

            let doc2 = getDocument workspace filePath2
            assertHasDocumentDiagnostics doc2

            File.WriteAllText(filePath1,
                """
module Script1

let x = 1
                """)

            assertEmptyDocumentDiagnostics doc2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()

    [<Test>]
    let ``Script file referencing another script will correct update when the referenced script file changes with opening in reverse order``() =
        let workspace = createWorkspace()
        let miscFilesWorkspace = createMiscFileWorkspace()
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
            let projInfo1 = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath1, filePath1)
            let projInfo2 = RoslynTestHelpers.CreateProjectInfoWithSingleDocument(filePath2, filePath2)

            addProject miscFilesWorkspace projInfo1
            addProject miscFilesWorkspace projInfo2

            openDocument miscFilesWorkspace (getDocument miscFilesWorkspace filePath2).Id
            openDocument miscFilesWorkspace (getDocument miscFilesWorkspace filePath1).Id          
            
            let doc2 = getDocument workspace filePath2
            assertHasDocumentDiagnostics doc2

            let doc1 = getDocument workspace filePath1
            assertEmptyDocumentDiagnostics doc1

            File.WriteAllText(filePath1,
                """
module Script1

let x = 1
                """)

            assertEmptyDocumentDiagnostics doc2

        finally
            try File.Delete(filePath1) with | _ -> ()
            try File.Delete(filePath2) with | _ -> ()
