namespace rec Microsoft.VisualStudio.FSharp.Editor.Tests.Roslyn

open System
open System.IO
open System.Text
open System.Reflection
open System.Linq
open System.Composition.Hosting
open System.Collections.Generic
open System.Collections.Immutable
open Microsoft.VisualStudio.Composition
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.Shell

[<AutoOpen>]
module MefHelpers =

    let getAssemblies() =
        let self = Assembly.GetExecutingAssembly()
        let here = AppContext.BaseDirectory

        let imports =   [|
                            "Microsoft.CodeAnalysis.Workspaces.dll"
                            "Microsoft.VisualStudio.Shell.15.0.dll"
                            "FSharp.Editor.dll"
                        |]

        let resolvedImports = imports.Select(fun name -> Path.Combine(here, name)).ToList()
        let missingDlls = resolvedImports.Where(fun path -> not(File.Exists(path))).ToList()
        if (missingDlls.Any()) then
            failwith "Missing imports"

        let loadedImports = resolvedImports.Select(fun p -> Assembly.LoadFrom(p)).ToList()

        let result = loadedImports.ToDictionary(fun k -> Path.GetFileNameWithoutExtension(k.Location))
        result.Values 
        |> Seq.append [|self|]
        |> Seq.append MefHostServices.DefaultAssemblies
        |> Array.ofSeq

    let createExportProvider() =
        let resolver = Resolver.DefaultInstance
        let catalog = 
            let asms = getAssemblies()
            let partDiscovery = PartDiscovery.Combine(new AttributedPartDiscoveryV1(resolver), new AttributedPartDiscovery(resolver, isNonPublicSupported = true));
            let parts = partDiscovery.CreatePartsAsync(asms).Result
            let catalog = ComposableCatalog.Create(resolver)
            catalog.AddParts(parts)

        let configuration = CompositionConfiguration.Create(catalog.WithCompositionService())
        let runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration)
        let exportProviderFactory = runtimeComposition.CreateExportProviderFactory()
        exportProviderFactory.CreateExportProvider()

type TestWorkspaceServiceMetadata(serviceType: string, layer: string) =

    member _.ServiceType = serviceType
    member _.Layer = layer

    new(data: IDictionary<string, obj>) =
        let serviceType =
            match data.TryGetValue("ServiceType") with
            | true, result -> result :?> string
            | _ -> Unchecked.defaultof<_>

        let layer =
            match data.TryGetValue("Layer") with
            | true, result -> result :?> string
            | _ -> Unchecked.defaultof<_>
        TestWorkspaceServiceMetadata(serviceType, layer)

    new(serviceType: Type, layer: string) =
        TestWorkspaceServiceMetadata(serviceType.AssemblyQualifiedName, layer)

type TestLanguageServiceMetadata(language: string, serviceType: string, layer: string, data: IDictionary<string, obj>) =

    member _.Language = language
    member _.ServiceType = serviceType
    member _.Layer = layer
    member _.Data = data

    new(data: IDictionary<string, obj>) =
        let language =
            match data.TryGetValue("Language") with
            | true, result -> result :?> string
            | _ -> Unchecked.defaultof<_>

        let serviceType =
            match data.TryGetValue("ServiceType") with
            | true, result -> result :?> string
            | _ -> Unchecked.defaultof<_>

        let layer =
            match data.TryGetValue("Layer") with
            | true, result -> result :?> string
            | _ -> Unchecked.defaultof<_>
        TestLanguageServiceMetadata(language, serviceType, layer, data)

type TestHostLanguageServices(workspaceServices: HostWorkspaceServices, language: string, exportProvider: ExportProvider) as this =
    inherit HostLanguageServices()

    let services1 =
        exportProvider.GetExports<ILanguageService, TestLanguageServiceMetadata>()
        |> Seq.filter (fun x -> x.Metadata.Language = language)

    let factories1 =
        exportProvider.GetExports<ILanguageServiceFactory, TestLanguageServiceMetadata>()
        |> Seq.filter (fun x -> x.Metadata.Language = language)
        |> Seq.map (fun x ->
            Lazy<_, _>((fun () -> x.Value.CreateLanguageService(this)), x.Metadata)
        )

    let otherServices1 = Seq.append factories1 services1

    let otherServicesMap1 =
        otherServices1
        |> Seq.map (fun x ->
            KeyValuePair(x.Metadata.ServiceType, x)
        )
        |> Seq.distinctBy (fun x -> x.Key)
        |> System.Collections.Concurrent.ConcurrentDictionary

    override this.WorkspaceServices = workspaceServices

    override this.Language = language

    override this.GetService<'T when 'T :> ILanguageService>() : 'T =
        match otherServicesMap1.TryGetValue(typeof<'T>.AssemblyQualifiedName) with
        | true, otherService ->
            otherService.Value :?> 'T
        | _ ->
            try
                exportProvider.GetExport<'T>().Value
            with
            | _ ->
                Unchecked.defaultof<'T>

type TestHostWorkspaceServices(hostServices: HostServices, workspace: Workspace) as this =
    inherit HostWorkspaceServices()

    let exportProvider = createExportProvider()

    let services1 =
        exportProvider.GetExports<IWorkspaceService, TestWorkspaceServiceMetadata>()

    let factories1 =
        exportProvider.GetExports<IWorkspaceServiceFactory, TestWorkspaceServiceMetadata>()
        |> Seq.map (fun x ->
            Lazy<_, _>((fun () -> x.Value.CreateService(this)), x.Metadata)
        )

    let otherServices1 = 
        Seq.append factories1 services1

    let otherServicesMap1 =
        otherServices1
        |> Seq.map (fun x ->
            KeyValuePair(x.Metadata.ServiceType, x)
        )
        |> Seq.distinctBy (fun x -> x.Key)
        |> System.Collections.Concurrent.ConcurrentDictionary

    let langServices = TestHostLanguageServices(this, LanguageNames.FSharp, exportProvider)

    override _.Workspace = workspace

    override this.GetService<'T when 'T :> IWorkspaceService>() : 'T =
        let ty = typeof<'T>
        match otherServicesMap1.TryGetValue(ty.AssemblyQualifiedName) with
        | true, otherService ->
            otherService.Value :?> 'T
        | _ ->
            try
                exportProvider.GetExport<'T>().Value
            with
            | _ ->
                Unchecked.defaultof<'T>

    override _.FindLanguageServices(filter) = Seq.empty

    override _.GetLanguageServices(languageName) =
        match languageName with
        | LanguageNames.FSharp ->
            langServices :> HostLanguageServices
        | _ ->
            raise(NotSupportedException(sprintf "Language '%s' not supported in FSharp VS tests." languageName))

    override _.HostServices = hostServices

type TestHostServices() =
    inherit HostServices()

    override this.CreateWorkspaceServices(workspace) =
        TestHostWorkspaceServices(this, workspace) :> HostWorkspaceServices

[<AbstractClass;Sealed>]
type RoslynTestHelpers private () =

    static member CreateProjectInfoWithSingleDocument(projName, docFilePath) =
        let isScript = String.Equals(Path.GetExtension(docFilePath), ".fsx", StringComparison.OrdinalIgnoreCase)

        let projId = ProjectId.CreateNewId()
        let docId = DocumentId.CreateNewId(projId)

        let docInfo =
            DocumentInfo.Create(
                docId,
                docFilePath, 
                filePath=docFilePath,
                loader = new FileTextLoader(docFilePath, Encoding.Default),
                sourceCodeKind= if isScript then SourceCodeKind.Script else SourceCodeKind.Regular)

        let projFilePath = "C:\\test.fsproj"
        ProjectInfo.Create(
            projId,
            VersionStamp.Create(DateTime.UtcNow),
            projName, 
            "test.dll", 
            LanguageNames.FSharp,
            documents = [docInfo],
            filePath = projFilePath
        )

    static member CreateDocument (filePath, text: SourceText, ?options: FSharp.Compiler.CodeAnalysis.FSharpProjectOptions) =
        let isScript = String.Equals(Path.GetExtension(filePath), ".fsx", StringComparison.OrdinalIgnoreCase)

        let workspace = new AdhocWorkspace(TestHostServices())

        let projId = ProjectId.CreateNewId()
        let docId = DocumentId.CreateNewId(projId)

        let docInfo =
            DocumentInfo.Create(
                docId,
                filePath, 
                loader=TextLoader.From(text.Container, VersionStamp.Create(DateTime.UtcNow)),
                filePath=filePath,
                sourceCodeKind= if isScript then SourceCodeKind.Script else SourceCodeKind.Regular)

        let projFilePath = "C:\\test.fsproj"
        let projInfo =
            ProjectInfo.Create(
                projId,
                VersionStamp.Create(DateTime.UtcNow),
                projFilePath, 
                "test.dll", 
                LanguageNames.FSharp,
                documents = [docInfo],
                filePath = projFilePath
            )

        let solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(DateTime.UtcNow), "test.sln", [projInfo])

        let solution = workspace.AddSolution(solutionInfo)

        let workspaceService = workspace.Services.GetService<IFSharpWorkspaceService>()

        let document = solution.GetProject(projId).GetDocument(docId)

        match options with
        | Some options ->
            let options = { options with ProjectId = Some(Guid.NewGuid().ToString()) }
            workspaceService.FSharpProjectOptionsManager.SetCommandLineOptions(projId, options.SourceFiles, options.OtherOptions |> ImmutableArray.CreateRange)
            document.SetFSharpProjectOptionsForTesting(options)
        | _ ->
            workspaceService.FSharpProjectOptionsManager.SetCommandLineOptions(projId, [|filePath|], ImmutableArray.Empty)

        document

    static member CreateDocument (filePath, code: string) =
        let text = SourceText.From(code)
        RoslynTestHelpers.CreateDocument(filePath, text), text

