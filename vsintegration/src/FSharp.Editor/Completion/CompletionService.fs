// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Collections.Immutable

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Completion
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Completion

open Microsoft.VisualStudio.Shell

type internal FSharpCompletionService
    (
        workspace: Workspace,
        serviceProvider: SVsServiceProvider,
        assemblyContentProvider: AssemblyContentProvider,
        settings: EditorOptions
    ) =
    inherit CompletionServiceWithProviders(workspace)

    let projectInfoManager = workspace.Services.GetRequiredService<IFSharpWorkspaceService>().FSharpProjectOptionsManager

    let builtInProviders = 
        ImmutableArray.Create<CompletionProvider>(
            FSharpCompletionProvider(workspace, serviceProvider, assemblyContentProvider),
            FSharpCommonCompletionProvider.Create(HashDirectiveCompletionProvider.Create(workspace, projectInfoManager)))

    override _.Language = FSharpConstants.FSharpLanguageName
    override _.GetBuiltInProviders() = builtInProviders
    override _.GetRules() =
        let enterKeyRule =
            match settings.IntelliSense.EnterKeySetting with
            | NeverNewline -> EnterKeyRule.Never
            | NewlineOnCompleteWord -> EnterKeyRule.AfterFullyTypedWord
            | AlwaysNewline -> EnterKeyRule.Always

        CompletionRules.Default
            .WithDismissIfEmpty(true)
            .WithDismissIfLastCharacterDeleted(true)
            .WithDefaultEnterKeyRule(enterKeyRule)

    /// Indicates the text span to be replaced by a committed completion list item.
    override _.GetDefaultCompletionListSpan(sourceText, caretIndex) =
        let documentId = workspace.GetDocumentIdInCurrentContext(sourceText.Container)
        let document = workspace.CurrentSolution.GetDocument(documentId)
        let defines = projectInfoManager.GetCompilationDefinesForEditingDocument(document)
        CompletionUtils.getDefaultCompletionListSpan(sourceText, caretIndex, documentId, document.FilePath, defines)

[<Shared>]
[<ExportLanguageServiceFactory(typeof<CompletionService>, FSharpConstants.FSharpLanguageName)>]
type internal FSharpCompletionServiceFactory 
    [<ImportingConstructor>] 
    (
        serviceProvider: SVsServiceProvider,
        assemblyContentProvider: AssemblyContentProvider,
        settings: EditorOptions
    ) =
    interface ILanguageServiceFactory with
        member _.CreateLanguageService(hostLanguageServices: HostLanguageServices) : ILanguageService =
            upcast new FSharpCompletionService(hostLanguageServices.WorkspaceServices.Workspace, serviceProvider, assemblyContentProvider, settings)


