// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Completion
open Microsoft.CodeAnalysis.Options
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Completion

open Microsoft.VisualStudio.Shell

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Tokenization

module Logger = Microsoft.VisualStudio.FSharp.Editor.Logger

type internal FSharpCompletionProvider
    (
        workspace: Workspace,
        serviceProvider: SVsServiceProvider,
        assemblyContentProvider: AssemblyContentProvider
    ) =

    inherit CompletionProvider()

    // Save the backing data in a cache, we need to save for at least the length of the completion session
    // See https://github.com/Microsoft/visualfsharp/issues/4714
    static let mutable declarationItems: DeclarationListItem[] = [||]
    static let [<Literal>] NameInCodePropName = "NameInCode"
    static let [<Literal>] FullNamePropName = "FullName"
    static let [<Literal>] IsExtensionMemberPropName = "IsExtensionMember"
    static let [<Literal>] NamespaceToOpenPropName = "NamespaceToOpen"
    static let [<Literal>] IndexPropName = "Index"
    static let [<Literal>] KeywordDescription = "KeywordDescription"

    static let keywordCompletionItems =
        FSharpKeywords.KeywordsWithDescription
        |> List.filter (fun (keyword, _) -> not (PrettyNaming.IsOperatorName keyword))
        |> List.sortBy (fun (keyword, _) -> keyword)
        |> List.mapi (fun n (keyword, description) ->
            FSharpCommonCompletionItem.Create(
                displayText = keyword,
                displayTextSuffix = "",
                rules = CompletionItemRules.Default,
                glyph = Nullable Glyph.Keyword,
                sortText = sprintf "%06d" (1000000 + n))
                .AddProperty(KeywordDescription, description))
    
    let settings: EditorOptions = workspace.Services.GetService()

    let documentationBuilder = XmlDocumentation.CreateDocumentationBuilder(serviceProvider.XMLMemberIndexService)
        
    static let noCommitOnSpaceRules = 
        // These are important.  They make sure we don't _commit_ autocompletion when people don't expect them to.  Some examples:
        //
        // * type Foo() =
        //       member val a = 12 with get, <<---- Don't commit autocomplete!
        //
        // * type MyRecord = { name: <<---- Don't commit autocomplete!
        //
        // * type My< <<---- Don't commit autocomplete!
        //
        // * let myClassInstance = MyClass(Date= <<---- Don't commit autocomplete!
        //
        // * let xs = [1..10] <<---- Don't commit autocomplete! (same for arrays)
        let noCommitChars = [|' '; '='; ','; '.'; '<'; '>'; '('; ')'; '!'; ':'; '['; ']'; '|'|].ToImmutableArray()

        CompletionItemRules.Default.WithCommitCharacterRules(ImmutableArray.Create (CharacterSetModificationRule.Create(CharacterSetModificationKind.Remove, noCommitChars)))
    
    static let getRules showAfterCharIsTyped = if showAfterCharIsTyped then noCommitOnSpaceRules else CompletionItemRules.Default

    static let mruItems = Dictionary<(* Item.FullName *) string, (* hints *) int>()
    
    static member ShouldTriggerCompletionAux(sourceText: SourceText, caretPosition: int, trigger: CompletionTriggerKind, getInfo: (unit -> DocumentId * string * string list), intelliSenseOptions: IntelliSenseOptions) =
        if caretPosition = 0 then
            false
        else
            let triggerPosition = caretPosition - 1
            let triggerChar = sourceText.[triggerPosition]

            if trigger = CompletionTriggerKind.Deletion && intelliSenseOptions.ShowAfterCharIsDeleted then
                Char.IsLetterOrDigit(sourceText.[triggerPosition]) || triggerChar = '.'
            elif not (trigger = CompletionTriggerKind.Insertion) then
                false
            else
                // Do not trigger completion if it's not single dot, i.e. range expression
                if not intelliSenseOptions.ShowAfterCharIsTyped && triggerPosition > 0 && sourceText.[triggerPosition - 1] = '.' then
                    false
                else
                    let documentId, filePath, defines = getInfo()
                    CompletionUtils.shouldProvideCompletion(documentId, filePath, defines, sourceText, triggerPosition) &&
                    (triggerChar = '.' || (intelliSenseOptions.ShowAfterCharIsTyped && CompletionUtils.isStartingNewWord(sourceText, triggerPosition)))
                

    static member ProvideCompletionsAsyncAux(document: Document, caretPosition: int, getAllSymbols: FSharpCheckFileResults -> AssemblySymbol list, intellisenseOptions: IntelliSenseOptions) = 

        asyncMaybe {
            let! parseResults, checkFileResults = document.GetFSharpParseAndCheckResultsAsync("ProvideCompletionsAsyncAux") |> liftAsync
            let! sourceText = document.GetTextAsync()
            let textLines = sourceText.Lines
            let caretLinePos = textLines.GetLinePosition(caretPosition)
            let caretLine = textLines.GetLineFromPosition(caretPosition)
            let fcsCaretLineNumber = Line.fromZ caretLinePos.Line  // Roslyn line numbers are zero-based, FSharp.Compiler.Service line numbers are 1-based
            let caretLineColumn = caretLinePos.Character
            let line = caretLine.ToString()
            let partialName = QuickParse.GetPartialLongNameEx(line, caretLineColumn - 1) 
            let getAllSymbols() =
                getAllSymbols checkFileResults 
                |> List.filter (fun assemblySymbol ->
                     assemblySymbol.FullName.Contains "." && not (PrettyNaming.IsOperatorName assemblySymbol.Symbol.DisplayName))
            let declarations = checkFileResults.GetDeclarationListInfo(Some(parseResults), fcsCaretLineNumber, line, partialName, getAllSymbols)
            let results = List<Completion.CompletionItem>()
            
            declarationItems <-
                declarations.Items
                |> Array.sortWith (fun x y ->
                    let mutable n = (not x.IsResolved).CompareTo(not y.IsResolved)
                    if n <> 0 then n else
                        n <- (CompletionUtils.getKindPriority x.Kind).CompareTo(CompletionUtils.getKindPriority y.Kind) 
                        if n <> 0 then n else
                            n <- (not x.IsOwnMember).CompareTo(not y.IsOwnMember)
                            if n <> 0 then n else
                                n <- String.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
                                if n <> 0 then n else
                                    x.MinorPriority.CompareTo(y.MinorPriority))

            let maxHints = if mruItems.Values.Count = 0 then 0 else Seq.max mruItems.Values

            declarationItems |> Array.iteri (fun number declarationItem ->
                let glyph = Tokenizer.FSharpGlyphToRoslynGlyph (declarationItem.Glyph, declarationItem.Accessibility)
                let namespaceName =
                    match declarationItem.NamespaceToOpen with
                    | Some namespaceToOpen -> namespaceToOpen
                    | _ -> null // Icky, but this is how roslyn handles it
                    
                let filterText =
                    match declarationItem.NamespaceToOpen, declarationItem.Name.Split '.' with
                    // There is no namespace to open and the item name does not contain dots, so we don't need to pass special FilterText to Roslyn.
                    | None, [|_|] -> null
                    // Either we have a namespace to open ("DateTime (open System)") or item name contains dots ("Array.map"), or both.
                    // We are passing last part of long ident as FilterText.
                    | _, idents -> Array.last idents

                let completionItem = 
                    FSharpCommonCompletionItem.Create(
                        declarationItem.Name,
                        null,
                        rules = getRules intellisenseOptions.ShowAfterCharIsTyped,
                        glyph = Nullable glyph,
                        filterText = filterText,
                        inlineDescription = namespaceName)
                        .AddProperty(FullNamePropName, declarationItem.FullName)
                        
                let completionItem =
                    match declarationItem.Kind with
                    | CompletionItemKind.Method (isExtension = true) ->
                            completionItem.AddProperty(IsExtensionMemberPropName, "")
                    | _ -> completionItem
                
                let completionItem =
                    if declarationItem.Name <> declarationItem.NameInCode then
                        completionItem.AddProperty(NameInCodePropName, declarationItem.NameInCode)
                    else completionItem

                let completionItem =
                    match declarationItem.NamespaceToOpen with
                    | Some ns -> completionItem.AddProperty(NamespaceToOpenPropName, ns)
                    | None -> completionItem

                let completionItem = completionItem.AddProperty(IndexPropName, string number)

                let priority = 
                    match mruItems.TryGetValue declarationItem.FullName with
                    | true, hints -> maxHints - hints
                    | _ -> number + maxHints + 1

                let sortText = priority.ToString("D6")
                let completionItem = completionItem.WithSortText(sortText)
                results.Add(completionItem))

            
            if results.Count > 0 && not declarations.IsForType && not declarations.IsError && List.isEmpty partialName.QualifyingIdents then
                let completionContext = ParsedInput.TryGetCompletionContext(Position.fromZ caretLinePos.Line caretLinePos.Character, parseResults.ParseTree, line)

                match completionContext with
                | None -> results.AddRange(keywordCompletionItems)
                | _ -> ()
            
            return results
        }

    override _.ShouldTriggerCompletion(sourceText: SourceText, caretPosition: int, trigger: CompletionTrigger, _: OptionSet) =
        use _logBlock = Logger.LogBlock LogEditorFunctionId.Completion_ShouldTrigger

        let getInfo() = 
            let documentId = workspace.GetDocumentIdInCurrentContext(sourceText.Container)
            let document = workspace.CurrentSolution.GetDocument(documentId)
            let defines = document.GetFSharpQuickDefines()
            (documentId, document.FilePath, defines)

        FSharpCompletionProvider.ShouldTriggerCompletionAux(sourceText, caretPosition, trigger.Kind, getInfo, settings.IntelliSense)
        
    override _.ProvideCompletionsAsync(context: Completion.CompletionContext) =
        asyncMaybe {
            use _logBlock = Logger.LogBlockMessage context.Document.Name LogEditorFunctionId.Completion_ProvideCompletionsAsync
            let document = context.Document
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let defines = document.GetFSharpQuickDefines()
            do! Option.guard (CompletionUtils.shouldProvideCompletion(document.Id, document.FilePath, defines, sourceText, context.Position))
            let getAllSymbols(fileCheckResults: FSharpCheckFileResults) =
                if settings.IntelliSense.IncludeSymbolsFromUnopenedNamespacesOrModules
                then assemblyContentProvider.GetAllEntitiesInProjectAndReferencedAssemblies(fileCheckResults)
                else []
            let! results = 
                FSharpCompletionProvider.ProvideCompletionsAsyncAux(context.Document, context.Position, getAllSymbols, settings.IntelliSense)
            context.AddItems(results)
        } |> Async.Ignore |> RoslynHelpers.StartAsyncUnitAsTask context.CancellationToken

    override _.GetDescriptionAsync(document: Document, completionItem: Completion.CompletionItem, cancellationToken: CancellationToken): Task<CompletionDescription> =
        async {
            use _logBlock = Logger.LogBlockMessage document.Name LogEditorFunctionId.Completion_GetDescriptionAsync
            match completionItem.Properties.TryGetValue IndexPropName with
            | true, completionItemIndexStr ->
                let completionItemIndex = int completionItemIndexStr
                if completionItemIndex < declarationItems.Length then
                    let declarationItem = declarationItems.[completionItemIndex]
                    let description = declarationItem.Description
                    let documentation = List()
                    let collector = RoslynHelpers.CollectTaggedText documentation
                    // mix main description and xmldoc by using one collector
                    XmlDocumentation.BuildDataTipText(documentationBuilder, collector, collector, collector, collector, collector, description) 
                    return CompletionDescription.Create(documentation.ToImmutableArray())
                else
                    return CompletionDescription.Empty
            | _ ->
                // Try keyword descriptions if they exist
                match completionItem.Properties.TryGetValue KeywordDescription with
                | true, keywordDescription ->
                    return CompletionDescription.FromText(keywordDescription)
                | false, _ ->
                    return CompletionDescription.Empty
        } |> RoslynHelpers.StartAsyncAsTask cancellationToken

    override _.GetChangeAsync(document, item, _, cancellationToken) : Task<CompletionChange> =
        async {
            use _logBlock = Logger.LogBlockMessage document.Name LogEditorFunctionId.Completion_GetChangeAsync

            let fullName =
                match item.Properties.TryGetValue FullNamePropName with
                | true, x -> Some x
                | _ -> None

            // do not add extension members and unresolved symbols to the MRU list
            if not (item.Properties.ContainsKey NamespaceToOpenPropName) && not (item.Properties.ContainsKey IsExtensionMemberPropName) then
                match fullName with
                | Some fullName ->
                    match mruItems.TryGetValue fullName with
                    | true, hints -> mruItems.[fullName] <- hints + 1
                    | _ -> mruItems.[fullName] <- 1
                | _ -> ()
            
            let nameInCode =
                match item.Properties.TryGetValue NameInCodePropName with
                | true, x -> x
                | _ -> item.DisplayText

            return!
                asyncMaybe {
                    let! ns = 
                        match item.Properties.TryGetValue NamespaceToOpenPropName with
                        | true, ns -> Some ns
                        | _ -> None
                    let! sourceText = document.GetTextAsync(cancellationToken)
                    let textWithItemCommitted = sourceText.WithChanges(TextChange(item.Span, nameInCode))
                    let line = sourceText.Lines.GetLineFromPosition(item.Span.Start)
                    let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpCompletionProvider)) |> liftAsync
                    let fullNameIdents = fullName |> Option.map (fun x -> x.Split '.') |> Option.defaultValue [||]
                    
                    let insertionPoint = 
                        if settings.CodeFixes.AlwaysPlaceOpensAtTopLevel then OpenStatementInsertionPoint.TopLevel
                        else OpenStatementInsertionPoint.Nearest

                    let ctx = ParsedInput.FindNearestPointToInsertOpenDeclaration line.LineNumber parseResults.ParseTree fullNameIdents insertionPoint
                    let finalSourceText, changedSpanStartPos = OpenDeclarationHelper.insertOpenDeclaration textWithItemCommitted ctx ns
                    let fullChangingSpan = TextSpan.FromBounds(changedSpanStartPos, item.Span.End)
                    let changedSpan = TextSpan.FromBounds(changedSpanStartPos, item.Span.End + (finalSourceText.Length - sourceText.Length))
                    let changedText = finalSourceText.ToString(changedSpan)
                    return CompletionChange.Create(TextChange(fullChangingSpan, changedText)).WithNewPosition(Nullable (changedSpan.End))
                }
                |> Async.map (Option.defaultValue (CompletionChange.Create(TextChange(item.Span, nameInCode))))

        } |> RoslynHelpers.StartAsyncAsTask cancellationToken
