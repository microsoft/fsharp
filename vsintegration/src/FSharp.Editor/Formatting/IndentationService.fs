// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Formatting
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Tokenization

[<Export(typeof<IFSharpSynchronousIndentationService>)>]
type internal FSharpIndentationService
    [<ImportingConstructor>]
    () =

    static member IsSmartIndentEnabled (options: Microsoft.CodeAnalysis.Options.OptionSet) =
        options.GetOption(FormattingOptions.SmartIndent, FSharpConstants.FSharpLanguageName) = FormattingOptions.IndentStyle.Smart

    static member IndentShouldFollow (documentId: DocumentId, sourceText: SourceText, filePath: string, position: int, parsingOptions: FSharpParsingOptions) =
        let lastTokenOpt =
           let defines = CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
           let tokens = Tokenizer.tokenizeLine(documentId, sourceText, position, filePath, defines)

           tokens
           |> Array.rev
           |> Array.tryFind (fun x ->
               x.Tag <> FSharpTokenTag.WHITESPACE &&
               x.Tag <> FSharpTokenTag.COMMENT &&
               x.Tag <> FSharpTokenTag.LINE_COMMENT)

        let (|Eq|_|) y x =
            if x = y then Some()
            else None

        match lastTokenOpt with
        | None -> false
        | Some lastToken ->
            match lastToken.Tag with
            | Eq FSharpTokenTag.EQUALS // =
            | Eq FSharpTokenTag.LARROW // <-
            | Eq FSharpTokenTag.RARROW // ->
            | Eq FSharpTokenTag.LPAREN // (
            | Eq FSharpTokenTag.LBRACK // [
            | Eq FSharpTokenTag.LBRACK_BAR // [|
            | Eq FSharpTokenTag.LBRACK_LESS // [<
            | Eq FSharpTokenTag.LBRACE // {
            | Eq FSharpTokenTag.BEGIN // begin
            | Eq FSharpTokenTag.DO // do
            | Eq FSharpTokenTag.THEN // then
            | Eq FSharpTokenTag.ELSE // else
            | Eq FSharpTokenTag.STRUCT // struct
            | Eq FSharpTokenTag.CLASS // class
            | Eq FSharpTokenTag.TRY -> // try
                true
            | _ -> false

    static member GetDesiredIndentation(documentId: DocumentId, sourceText: SourceText, filePath: string, lineNumber: int, tabSize: int, indentStyle: FormattingOptions.IndentStyle, parsingOptions: FSharpParsingOptions): Option<int> =

        // Match indentation with previous line
        let rec tryFindPreviousNonEmptyLine l =
            if l <= 0 then None
            else
                let previousLine = sourceText.Lines.[l - 1]
                if not (String.IsNullOrEmpty(previousLine.ToString())) then
                    Some previousLine
                else
                    tryFindPreviousNonEmptyLine (l - 1)

        maybe {
            let! previousLine = tryFindPreviousNonEmptyLine lineNumber
            
            let lastIndent =
                previousLine.ToString()
                |> Seq.takeWhile ((=) ' ')
                |> Seq.length

            // Only use smart indentation after tokens that need indentation
            // if the option is enabled
            return
                if indentStyle = FormattingOptions.IndentStyle.Smart && FSharpIndentationService.IndentShouldFollow(documentId, sourceText, filePath, previousLine.Start, parsingOptions) then
                    (lastIndent/tabSize + 1) * tabSize
                else
                    lastIndent
        }

    interface IFSharpSynchronousIndentationService with
        member this.GetDesiredIndentation(document: Document, lineNumber: int, cancellationToken: CancellationToken): Nullable<FSharpIndentationResult> =
            async {
                let! cancellationToken = Async.CancellationToken
                let! sourceText = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                let! options = document.GetOptionsAsync(cancellationToken) |> Async.AwaitTask
                let tabSize = options.GetOption<int>(FormattingOptions.TabSize, FSharpConstants.FSharpLanguageName)
                let indentStyle = options.GetOption(FormattingOptions.SmartIndent, FSharpConstants.FSharpLanguageName)
                let parsingOptions = document.GetFSharpQuickParsingOptions()
                let indent = FSharpIndentationService.GetDesiredIndentation(document.Id, sourceText, document.FilePath, lineNumber, tabSize, indentStyle, parsingOptions)
                return
                    match indent with
                    | None -> Nullable()
                    | Some(indentation) -> Nullable<FSharpIndentationResult>(FSharpIndentationResult(sourceText.Lines.[lineNumber].Start, indentation))
            } |> (fun c -> Async.RunImmediateExceptOnUI(c,cancellationToken=cancellationToken))
