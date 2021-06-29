namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.ComponentModel.Composition
open System.Runtime.InteropServices
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.FSharp.UIResources
open Microsoft.CodeAnalysis

module DefaultTuning =
    /// How long is the per-document data saved before it is eligible for eviction from the cache? 10 seconds.
    /// Re-tokenizing is fast so we don't need to save this data long.
    let PerDocumentSavedDataSlidingWindow = TimeSpan(0,0,10)(* seconds *)

type EnterKeySetting =
    | NeverNewline
    | NewlineOnCompleteWord
    | AlwaysNewline

// CLIMutable to make the record work also as a view model
[<CLIMutable>]
type IntelliSenseOptions =
  { ShowAfterCharIsTyped: bool
    ShowAfterCharIsDeleted: bool
    IncludeSymbolsFromUnopenedNamespacesOrModules : bool
    EnterKeySetting : EnterKeySetting }
    static member Default =
      { ShowAfterCharIsTyped = true
        ShowAfterCharIsDeleted = false
        IncludeSymbolsFromUnopenedNamespacesOrModules = false
        EnterKeySetting = EnterKeySetting.NeverNewline}


[<RequireQualifiedAccess>]
type QuickInfoUnderlineStyle = Dot | Dash | Solid

[<CLIMutable>]
type QuickInfoOptions =
    { DisplayLinks: bool
      UnderlineStyle: QuickInfoUnderlineStyle }
    static member Default =
      { DisplayLinks = true
        UnderlineStyle = QuickInfoUnderlineStyle.Solid }

[<CLIMutable>]
type CodeFixesOptions =
    { SimplifyName: bool
      AlwaysPlaceOpensAtTopLevel: bool
      UnusedOpens: bool 
      UnusedDeclarations: bool
      SuggestNamesForErrors: bool }
    static member Default =
      { // We have this off by default, disable until we work out how to make this low priority 
        // See https://github.com/Microsoft/visualfsharp/pull/3238#issue-237699595
        SimplifyName = false 
        AlwaysPlaceOpensAtTopLevel = true
        UnusedOpens = true 
        UnusedDeclarations = true
        SuggestNamesForErrors = true }

[<CLIMutable>]
type LanguageServicePerformanceOptions = 
    { EnableInMemoryCrossProjectReferences: bool
      AllowStaleCompletionResults: bool
      TimeUntilStaleCompletion: int
      ProjectCheckCacheSize: int }
    static member Default =
      { EnableInMemoryCrossProjectReferences = true
        AllowStaleCompletionResults = true
        TimeUntilStaleCompletion = 2000 // In ms, so this is 2 seconds
        ProjectCheckCacheSize = 200 }

[<CLIMutable>]
type CodeLensOptions =
  { Enabled : bool
    ReplaceWithLineLens: bool
    UseColors: bool
    Prefix : string }
    static member Default =
      { Enabled = false
        UseColors = false
        ReplaceWithLineLens = true
        Prefix = "// " }

[<CLIMutable>]
type AdvancedOptions =
    { IsBlockStructureEnabled: bool
      IsOutliningEnabled: bool }
    static member Default =
      { IsBlockStructureEnabled = true
        IsOutliningEnabled = true }

[<CLIMutable>]
type FormattingOptions =
    { FormatOnPaste: bool }
    static member Default =
        { FormatOnPaste = true }

[<Export>]
[<Export(typeof<IPersistSettings>)>]
type EditorOptions 
    [<ImportingConstructor>] 
    (
        [<Import(typeof<SVsServiceProvider>)>] serviceProvider: IServiceProvider
    ) =

    let store = SettingsStore(serviceProvider)
        
    do
        store.Register QuickInfoOptions.Default
        store.Register CodeFixesOptions.Default
        store.Register LanguageServicePerformanceOptions.Default
        store.Register AdvancedOptions.Default
        store.Register IntelliSenseOptions.Default
        store.Register CodeLensOptions.Default
        store.Register FormattingOptions.Default

    member _.IntelliSense : IntelliSenseOptions = store.Get()
    member _.QuickInfo : QuickInfoOptions = store.Get()
    member _.CodeFixes : CodeFixesOptions = store.Get()
    member _.LanguageServicePerformance : LanguageServicePerformanceOptions = store.Get()
    member _.Advanced: AdvancedOptions = store.Get()
    member _.CodeLens: CodeLensOptions = store.Get()
    member _.Formatting : FormattingOptions = store.Get()

    interface Microsoft.CodeAnalysis.Host.IWorkspaceService

    interface IPersistSettings with
        member _.LoadSettings() = store.LoadSettings()
        member _.SaveSettings(settings) = store.SaveSettings(settings)

module internal OptionsUI =

    open OptionsUIHelpers

    [<Guid(Guids.intelliSenseOptionPageIdString)>]
    type internal IntelliSenseOptionPage() =
        inherit AbstractOptionPage<IntelliSenseOptions>()
        override this.CreateView() =
            let view = IntelliSenseOptionControl()
            view.charTyped.Unchecked.Add <| fun _ -> view.charDeleted.IsChecked <- System.Nullable false

            let path = "EnterKeySetting"
            bindRadioButton view.nevernewline path EnterKeySetting.NeverNewline 
            bindRadioButton view.newlinecompleteline path EnterKeySetting.NewlineOnCompleteWord 
            bindRadioButton view.alwaysnewline path EnterKeySetting.AlwaysNewline

            upcast view

    [<Guid(Guids.quickInfoOptionPageIdString)>]
    type internal QuickInfoOptionPage() =
        inherit AbstractOptionPage<QuickInfoOptions>()
        override this.CreateView() = 
            let view = QuickInfoOptionControl()
            let path = "UnderlineStyle"
            bindRadioButton view.solid path QuickInfoUnderlineStyle.Solid
            bindRadioButton view.dot path QuickInfoUnderlineStyle.Dot
            bindRadioButton view.dash path QuickInfoUnderlineStyle.Dash
            bindCheckBox view.displayLinks "DisplayLinks"
            upcast view

    [<Guid(Guids.codeFixesOptionPageIdString)>]
    type internal CodeFixesOptionPage() =
        inherit AbstractOptionPage<CodeFixesOptions>()
        override this.CreateView() =
            upcast CodeFixesOptionControl()

    [<Guid(Guids.languageServicePerformanceOptionPageIdString)>]
    type internal LanguageServicePerformanceOptionPage() =
        inherit AbstractOptionPage<LanguageServicePerformanceOptions>()
        override this.CreateView() =
            upcast LanguageServicePerformanceOptionControl()

    [<Guid(Guids.codeLensOptionPageIdString)>]
    type internal CodeLensOptionPage() =
        inherit AbstractOptionPage<CodeLensOptions>()
        override this.CreateView() =
            upcast CodeLensOptionControl()

    [<Guid(Guids.advancedSettingsPageIdSring)>]
    type internal AdvancedSettingsOptionPage() =
        inherit AbstractOptionPage<AdvancedOptions>()
        override _.CreateView() =
            upcast AdvancedOptionsControl()

    [<Guid(Guids.formattingOptionPageIdString)>]
    type internal FormattingOptionPage() =
        inherit AbstractOptionPage<FormattingOptions>()
        override _.CreateView() =
            upcast FormattingOptionsControl()

[<AutoOpen>]
module EditorOptionsExtensions =

    type Project with

        member this.AreFSharpInMemoryCrossProjectReferencesEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> true
            | _ -> editorOptions.LanguageServicePerformance.EnableInMemoryCrossProjectReferences

        member this.IsFSharpCodeFixesAlwaysPlaceOpensAtTopLevelEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.CodeFixes.AlwaysPlaceOpensAtTopLevel

        member this.IsFSharpCodeFixesUnusedDeclarationsEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.CodeFixes.UnusedDeclarations

        member this.IsFSharpStaleCompletionResultsEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.LanguageServicePerformance.AllowStaleCompletionResults

        member this.FSharpTimeUntilStaleCompletion =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> 0
            | _ -> editorOptions.LanguageServicePerformance.TimeUntilStaleCompletion

        member this.IsFSharpCodeFixesSimplifyNameEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.CodeFixes.SimplifyName

        member this.IsFSharpCodeFixesUnusedOpensEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.CodeFixes.UnusedOpens

        member this.IsFSharpBlockStructureEnabled =
            let editorOptions = this.Solution.Workspace.Services.GetService<EditorOptions>()
            match box editorOptions with
            | null -> false
            | _ -> editorOptions.Advanced.IsBlockStructureEnabled