// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Text.RegularExpressions
open System.IO
open System.Collections.Immutable
open System.Threading
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Completion
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Completion

type internal HashCompletion =
    { DirectiveRegex: Regex
      AllowableExtensions: string list
      UseIncludeDirectives: bool }
    static member Create(directiveRegex, allowableExtensions, useIncludeDirectives) =
        { DirectiveRegex = Regex(directiveRegex, RegexOptions.Compiled ||| RegexOptions.ExplicitCapture)
          AllowableExtensions = allowableExtensions
          UseIncludeDirectives = useIncludeDirectives }

type internal HashDirectiveCompletionProvider(workspace: Workspace, completions: HashCompletion list) =

    let [<Literal>] NetworkPath = "\\\\"
    let commitRules = ImmutableArray.Create(CharacterSetModificationRule.Create(CharacterSetModificationKind.Replace, '"', '\\', ',', '/'))
    let rules = CompletionItemRules.Create(commitCharacterRules = commitRules)

    let getQuotedPathStart(text: SourceText, position: int, quotedPathGroup: Group) =
        text.Lines.GetLineFromPosition(position).Start + quotedPathGroup.Index

    let getPathThroughLastSlash(text: SourceText, position: int, quotedPathGroup: Group) =
        PathCompletionUtilities.GetPathThroughLastSlash(
            quotedPathGroup.Value,
            getQuotedPathStart(text, position, quotedPathGroup),
            position)

    let getFileGlyph (extention: string) =
        match extention with
        | ".exe" | ".dll" -> Some Glyph.Assembly
        | _ -> None

    let includeDirectiveCleanRegex = Regex("""#I\s+(@?"*(?<literal>[^"]*)"?)""", RegexOptions.Compiled ||| RegexOptions.ExplicitCapture)

    let getClassifiedSpans(text: SourceText, position: int) : ResizeArray<ClassifiedSpan> =
        let documentId = workspace.GetDocumentIdInCurrentContext(text.Container)
        let document = workspace.CurrentSolution.GetDocument(documentId)
        let defines = document.GetFSharpQuickDefines()
        let textLines = text.Lines
        let triggerLine = textLines.GetLineFromPosition(position)
        Tokenizer.getClassifiedSpans(documentId, text, triggerLine.Span, Some document.FilePath, defines, CancellationToken.None)

    let isInStringLiteral(text: SourceText, position: int) : bool =
        getClassifiedSpans(text, position)
        |> Seq.exists(fun classifiedSpan -> 
            classifiedSpan.TextSpan.IntersectsWith position &&
            classifiedSpan.ClassificationType = ClassificationTypeNames.StringLiteral)

    let getIncludeDirectives (text: SourceText, position: int) =
        let lines = text.Lines
        let caretLine = text.Lines.GetLinePosition(position).Line
        lines
        |> Seq.filter (fun x -> x.LineNumber < caretLine)
        |> Seq.choose (fun line ->
            let lineStr = line.ToString().Trim()
            // optimization: fail fast if the line does not start with "(optional spaces) #I"
            if not (lineStr.StartsWith "#I") then None
            else
                match includeDirectiveCleanRegex.Match lineStr with
                | m when m.Success ->
                    getClassifiedSpans(text, line.Start)
                    |> Seq.tryPick (fun span -> 
                        if span.TextSpan.IntersectsWith line.Start &&
                           (span.ClassificationType <> ClassificationTypeNames.Comment &&
                            span.ClassificationType <> ClassificationTypeNames.ExcludedCode) then
                            Some (m.Groups.["literal"].Value)
                        else None)
                | _ -> None
           )
        |> Seq.toList

    static member Create(workspace: Workspace) =
        let completions =
            [
                HashCompletion.Create("""\s*#load\s+(@?"*(?<literal>"[^"]*"?))""", [ ".fs"; ".fsx" ], useIncludeDirectives = true)
                HashCompletion.Create("""\s*#r\s+(@?"*(?<literal>"[^"]*"?))""", [ ".dll"; ".exe" ], useIncludeDirectives = true)
                HashCompletion.Create("""\s*#I\s+(@?"*(?<literal>"[^"]*"?))""", [ "\x00" ], useIncludeDirectives = false)
            ]
        HashDirectiveCompletionProvider(workspace, completions)

    interface IFSharpCommonCompletionProvider with

        member _.ProvideCompletionsAsync(context) =
            asyncMaybe {    
                let document = context.Document
                let position = context.Position
                do! let extension = Path.GetExtension document.FilePath
                    Option.guard (extension = ".fsx" || extension = ".fsscript")

                let! ct = liftAsync Async.CancellationToken
                let! text = document.GetTextAsync(ct)
                do! Option.guard (isInStringLiteral(text, position))
                let line = text.Lines.GetLineFromPosition(position)
                let lineText = text.ToString(TextSpan.FromBounds(line.Start, position))
            
                let! completion, quotedPathGroup =
                    completions |> List.tryPick (fun completion ->
                        match completion.DirectiveRegex.Match lineText with
                        | m when m.Success ->
                            let quotedPathGroup = m.Groups.["literal"]
                            let endsWithQuote = PathCompletionUtilities.EndsWithQuote(quotedPathGroup.Value)
                            if endsWithQuote && (position >= line.Start + m.Length) then
                                None
                            else
                                Some (completion, quotedPathGroup)
                        | _ -> None)

                let snapshot = text.FindCorrespondingEditorTextSnapshot()
            
                do! Option.guard (not (isNull snapshot))

                let extraSearchPaths =
                    if completion.UseIncludeDirectives then
                        getIncludeDirectives (text, position)
                    else []

                let defaultSearchPath = Path.GetDirectoryName document.FilePath
                let searchPaths = defaultSearchPath :: extraSearchPaths

                let helper = 
                    FSharpFileSystemCompletionHelper(
                        Glyph.OpenFolder,
                        completion.AllowableExtensions |> List.tryPick getFileGlyph |> Option.defaultValue Glyph.None,
                        Seq.toImmutableArray searchPaths,
                        null,
                        completion.AllowableExtensions |> Seq.toImmutableArray,
                        rules)
     
                let pathThroughLastSlash = getPathThroughLastSlash(text, position, quotedPathGroup)
                let! items = helper.GetItemsAsync(pathThroughLastSlash, ct) |> Async.AwaitTask |> liftAsync
                context.AddItems(items)
            } 
            |> Async.Ignore
            |> RoslynHelpers.StartAsyncUnitAsTask context.CancellationToken
 
        member _.IsInsertionTrigger(text, position, _) = 
            // Bring up completion when the user types a quote (i.e.: #r "), or if they type a slash
            // path separator character, or if they type a comma (#r "foo,version...").
            // Also, if they're starting a word.  i.e. #r "c:\W
            let ch = text.[position]
            let isTriggerChar = 
                ch = '"' || ch = '\\' || ch = ',' || ch = '/' ||
                    FSharpCommonCompletionUtilities.IsStartingNewWord(text, position, (fun x -> Char.IsLetter x), (fun x -> Char.IsLetterOrDigit x))
            isTriggerChar && isInStringLiteral(text, position)
 
        member _.GetTextChangeAsync(baseGetTextChangeAsync, selectedItem, ch, cancellationToken) = 
            // When we commit "\\" when the user types \ we have to adjust for the fact that the
            // controller will automatically append \ after we commit.  Because of that, we don't
            // want to actually commit "\\" as we'll end up with "\\\".  So instead we just commit
            // "\" and know that controller will append "\" and give us "\\".
            if selectedItem.DisplayText = NetworkPath && ch = Nullable '\\' then
                Task.FromResult(Nullable(TextChange(selectedItem.Span, "\\")))
            else
                baseGetTextChangeAsync.Invoke(selectedItem, ch, cancellationToken)