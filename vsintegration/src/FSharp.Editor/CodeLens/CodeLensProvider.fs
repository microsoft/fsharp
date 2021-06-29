// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec Microsoft.VisualStudio.FSharp.Editor

open System
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor.Shared.Utilities

[<Export(typeof<IWpfTextViewCreationListener>)>]
[<Export(typeof<IViewTaggerProvider>)>]
[<TagType(typeof<CodeLensGeneralTag>)>]
[<ContentType(FSharpConstants.FSharpContentTypeName)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
type internal CodeLensProvider  
    [<ImportingConstructor>]
    (
        [<Import(typeof<SVsServiceProvider>)>] serviceProvider: IServiceProvider,
        textDocumentFactory: ITextDocumentFactoryService,
        metadataAsSource: FSharpMetadataAsSourceService,
        typeMap : FSharpClassificationTypeMap Lazy,
        settings: EditorOptions
    ) =

    let lineLensProvider = ResizeArray()
    let taggers = ResizeArray()
    let componentModel = Package.GetGlobalService(typeof<ComponentModelHost.SComponentModel>) :?> ComponentModelHost.IComponentModel
    let workspace = componentModel.GetService<VisualStudioWorkspace>()

    /// Returns an provider for the textView if already one has been created. Else create one.
    let addCodeLensProviderOnce wpfView buffer =
        let res = taggers |> Seq.tryFind(fun (view, _) -> view = wpfView)
        match res with
        | Some (_, (tagger, _)) -> tagger
        | None ->
            let documentId = 
                lazy (
                    match textDocumentFactory.TryGetTextDocument(buffer) with
                    | true, textDocument ->
                         Seq.tryHead (workspace.CurrentSolution.GetDocumentIdsWithFilePath(textDocument.FilePath))
                    | _ -> None
                    |> Option.get
                )

            let tagger = CodeLensGeneralTagger(wpfView, buffer)
            let service = FSharpCodeLensService(serviceProvider, workspace, documentId, buffer, metadataAsSource, componentModel.GetService(), typeMap, tagger, settings)
            let provider = (wpfView, (tagger, service))
            wpfView.Closed.Add (fun _ -> taggers.Remove provider |> ignore)
            taggers.Add((wpfView, (tagger, service)))
            tagger

    /// Returns an provider for the textView if already one has been created. Else create one.
    let addLineLensProviderOnce wpfView buffer =
        let res = lineLensProvider |> Seq.tryFind(fun (view, _) -> view = wpfView)
        match res with
        | None ->
            let documentId = 
                lazy (
                    match textDocumentFactory.TryGetTextDocument(buffer) with
                    | true, textDocument ->
                         Seq.tryHead (workspace.CurrentSolution.GetDocumentIdsWithFilePath(textDocument.FilePath))
                    | _ -> None
                    |> Option.get
                )
            let service = FSharpCodeLensService(serviceProvider, workspace, documentId, buffer, metadataAsSource, componentModel.GetService(), typeMap, LineLensDisplayService(wpfView, buffer), settings)
            let provider = (wpfView, service)
            wpfView.Closed.Add (fun _ -> lineLensProvider.Remove provider |> ignore)
            lineLensProvider.Add(provider)
        | _ -> ()

    [<Export(typeof<AdornmentLayerDefinition>); Name("CodeLens");
      Order(Before = PredefinedAdornmentLayers.Text);
      TextViewRole(PredefinedTextViewRoles.Document)>]
    member val CodeLensAdornmentLayerDefinition : AdornmentLayerDefinition = null with get, set
    
    [<Export(typeof<AdornmentLayerDefinition>); Name("LineLens");
      Order(Before = PredefinedAdornmentLayers.Text);
      TextViewRole(PredefinedTextViewRoles.Document)>]
    member val LineLensAdornmentLayerDefinition : AdornmentLayerDefinition = null with get, set

    interface IViewTaggerProvider with
        override _.CreateTagger(view, buffer) =
            if settings.CodeLens.Enabled && not settings.CodeLens.ReplaceWithLineLens then
                let wpfView =
                    match view with
                    | :? IWpfTextView as view -> view
                    | _ -> failwith "error"
            
                box(addCodeLensProviderOnce wpfView buffer) :?> _
            else
                null

    interface IWpfTextViewCreationListener with
        override _.TextViewCreated view =
            if settings.CodeLens.Enabled && settings.CodeLens.ReplaceWithLineLens then
                addLineLensProviderOnce view (view.TextBuffer) |> ignore