// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Threading
open System.Collections.Immutable
open System.Diagnostics
open System.IO
open System.Linq
open System.Text
open System.Runtime.InteropServices
open System.Reflection.PortableExecutable
open System.ComponentModel.Composition

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.FindSymbols
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.Navigation
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Navigation
open Microsoft.VisualStudio.ComponentModelHost
open Microsoft.VisualStudio.LanguageServices.ProjectSystem

open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.Threading
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.TextManager.Interop

module internal MetadataAsSource =

    let generateTemporaryDocument (asmIdentity: AssemblyIdentity, name: string, metadataReferences) =
        let rootPath = Path.Combine(Path.GetTempPath(), "MetadataAsSource")
        let extension = ".fsi"
        let directoryName = Guid.NewGuid().ToString("N")
        let temporaryFilePath = Path.Combine(rootPath, directoryName, name + extension)

        let projectId = ProjectId.CreateNewId()

        let generatedDocumentId = DocumentId.CreateNewId(projectId)
        let documentInfo = 
            DocumentInfo.Create(
                generatedDocumentId,
                Path.GetFileName(temporaryFilePath),
                filePath = temporaryFilePath,
                loader = FileTextLoader(temporaryFilePath, Encoding.UTF8))

        let projectInfo = 
            ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                name = FSharpConstants.FSharpMetadataName + " - " + asmIdentity.Name,
                assemblyName = asmIdentity.Name,
                language = LanguageNames.FSharp,
                documents = [|documentInfo|],
                metadataReferences = metadataReferences)

        (projectInfo, documentInfo)

    let showDocument (filePath, name, serviceProvider: IServiceProvider) =
        let vsRunningDocumentTable4 = serviceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable4>()
        let fileAlreadyOpen = vsRunningDocumentTable4.IsMonikerValid(filePath)

        let openDocumentService = serviceProvider.GetService<SVsUIShellOpenDocument, IVsUIShellOpenDocument>()

        let (_, _, _, _, windowFrame) = openDocumentService.OpenDocumentViaProject(filePath, ref VSConstants.LOGVIEWID.TextView_guid)

        let componentModel = serviceProvider.GetService<SComponentModel, IComponentModel>()
        let editorAdaptersFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>()
        let documentCookie = vsRunningDocumentTable4.GetDocumentCookie(filePath)
        let vsTextBuffer = vsRunningDocumentTable4.GetDocumentData(documentCookie) :?> IVsTextBuffer
        let textBuffer = editorAdaptersFactory.GetDataBuffer(vsTextBuffer)

        if not fileAlreadyOpen then
            ErrorHandler.ThrowOnFailure(vsTextBuffer.SetStateFlags(uint32 BUFFERSTATEFLAGS.BSF_USER_READONLY)) |> ignore
            ErrorHandler.ThrowOnFailure(windowFrame.SetProperty(int __VSFPROPID5.VSFPROPID_IsProvisional, true)) |> ignore
            ErrorHandler.ThrowOnFailure(windowFrame.SetProperty(int __VSFPROPID5.VSFPROPID_OverrideCaption, name)) |> ignore
            ErrorHandler.ThrowOnFailure(windowFrame.SetProperty(int __VSFPROPID5.VSFPROPID_OverrideToolTip, name)) |> ignore

        windowFrame.Show() |> ignore

        let textContainer = textBuffer.AsTextContainer()
        let mutable workspace = Unchecked.defaultof<_>
        if Workspace.TryGetWorkspace(textContainer, &workspace) then
            let solution = workspace.CurrentSolution
            let documentId = workspace.GetDocumentIdInCurrentContext(textContainer)
            match box documentId with
            | null -> None
            | _ -> solution.GetDocument(documentId) |> Some
        else
            None

[<Sealed>]
[<Export(typeof<FSharpMetadataAsSourceService>); Composition.Shared>]
type internal FSharpMetadataAsSourceService [<ImportingConstructor>] (projectContextFactory: IWorkspaceProjectContextFactory) =

    let serviceProvider = ServiceProvider.GlobalProvider
    let projs = System.Collections.Concurrent.ConcurrentDictionary<string, IWorkspaceProjectContext>()

    let createMetadataProjectContext (projInfo: ProjectInfo) (docInfo: DocumentInfo) =
        let projectContext = projectContextFactory.CreateProjectContext(LanguageNames.FSharp, projInfo.Id.ToString(), projInfo.FilePath, Guid.NewGuid(), null, null)
        projectContext.DisplayName <- projInfo.Name
        projectContext.AddSourceFile(docInfo.FilePath, sourceCodeKind = SourceCodeKind.Regular)
        
        for metaRef in projInfo.MetadataReferences do
            match metaRef with
            | :? PortableExecutableReference as peRef ->
                projectContext.AddMetadataReference(peRef.FilePath, MetadataReferenceProperties.Assembly)
            | _ ->
                ()

        projectContext

    let clear filePath (projectContext: IWorkspaceProjectContext) =
        projs.TryRemove(filePath) |> ignore
        projectContext.Dispose()
        try
            File.Delete filePath |> ignore
        with
        | _ -> ()

    member _.ClearGeneratedFiles() =
        let projsArr = projs.ToArray()
        projsArr
        |> Array.iter (fun pair ->
            clear pair.Key pair.Value
        )

    member _.ShowDocument(projInfo: ProjectInfo, filePath: string, text: Text.SourceText) =
        match projInfo.Documents |> Seq.tryFind (fun doc -> doc.FilePath = filePath) with
        | Some document ->
            let _ =
                let directoryName = Path.GetDirectoryName(filePath)
                if Directory.Exists(directoryName) |> not then
                    Directory.CreateDirectory(directoryName) |> ignore
                use fileStream = new FileStream(filePath, IO.FileMode.Create)
                use writer = new StreamWriter(fileStream)
                text.Write(writer)

            let projectContext = createMetadataProjectContext projInfo document

            projs.[filePath] <- projectContext

            MetadataAsSource.showDocument(filePath, Path.GetFileName(filePath), serviceProvider)
        | _ ->
            None