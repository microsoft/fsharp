// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Threading
open System.Threading.Tasks
open System.ComponentModel.Composition
open System.Text

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text

open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Utilities

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Tokenization

type internal QuickInfo =
    { StructuredText: ToolTipText
      Span: TextSpan
      Symbol: FSharpSymbol option
      SymbolKind: LexerSymbolKind }

module internal FSharpQuickInfo =

    let userOpName = "QuickInfo"

    // when a construct has been declared in a signature file the documentation comments that are
    // written in that file are the ones that go into the generated xml when the project is compiled
    // therefore we should include these doccoms in our design time quick info
    let getQuickInfoFromRange
        (
            document: Document,
            declRange: range,
            cancellationToken: CancellationToken
        )
        : Async<QuickInfo option> =

        asyncMaybe {
            let userOpName = "getQuickInfoFromRange"
            let solution = document.Project.Solution
            // ascertain the location of the target declaration in the signature file
            let! extDocId = solution.GetDocumentIdsWithFilePath declRange.FileName |> Seq.tryHead
            let extDocument = solution.GetProject(extDocId.ProjectId).GetDocument extDocId
            let! extSourceText = extDocument.GetTextAsync cancellationToken
            let! extSpan = RoslynHelpers.TryFSharpRangeToTextSpan (extSourceText, declRange)
            let extLineText = (extSourceText.Lines.GetLineFromPosition extSpan.Start).ToString()

            // project options need to be retrieved because the signature file could be in another project
            let! extLexerSymbol = extDocument.TryFindFSharpLexerSymbolAsync(extSpan.Start, SymbolLookupKind.Greedy, true, true, userOpName)
            let! _, extCheckFileResults = extDocument.GetFSharpParseAndCheckResultsAsync(userOpName) |> liftAsync

            let extQuickInfoText = 
                extCheckFileResults.GetToolTip
                    (declRange.StartLine, extLexerSymbol.Ident.idRange.EndColumn, extLineText, extLexerSymbol.FullIsland, FSharpTokenTag.IDENT)

            match extQuickInfoText with
            | ToolTipText []
            | ToolTipText [ToolTipElement.None] -> return! None
            | extQuickInfoText  ->
                let! extSymbolUse =
                    extCheckFileResults.GetSymbolUseAtLocation(declRange.StartLine, extLexerSymbol.Ident.idRange.EndColumn, extLineText, extLexerSymbol.FullIsland)
                let! span = RoslynHelpers.TryFSharpRangeToTextSpan (extSourceText, extLexerSymbol.Range)

                return { StructuredText = extQuickInfoText
                         Span = span
                         Symbol = Some extSymbolUse.Symbol
                         SymbolKind = extLexerSymbol.Kind }
        }

    /// Get QuickInfo combined from doccom of Signature and definition
    let getQuickInfo
        (
            document: Document,
            position: int,
            cancellationToken: CancellationToken
        )
        : Async<(range * QuickInfo option * QuickInfo option) option> =

        asyncMaybe {
            let userOpName = "getQuickInfo"
            let! lexerSymbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, true, true, userOpName)
            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(userOpName) |> liftAsync
            let! sourceText = document.GetTextAsync cancellationToken
            let idRange = lexerSymbol.Ident.idRange  
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let lineText = (sourceText.Lines.GetLineFromPosition position).ToString()

            /// Gets the QuickInfo information for the orignal target
            let getTargetSymbolQuickInfo (symbol, tag) =
                asyncMaybe {
                    let targetQuickInfo =
                        checkFileResults.GetToolTip
                            (fcsTextLineNumber, idRange.EndColumn, lineText, lexerSymbol.FullIsland,tag)

                    match targetQuickInfo with
                    | ToolTipText []
                    | ToolTipText [ToolTipElement.None] -> return! None
                    | _ ->
                        let! targetTextSpan = RoslynHelpers.TryFSharpRangeToTextSpan (sourceText, lexerSymbol.Range)
                        return { StructuredText = targetQuickInfo
                                 Span = targetTextSpan
                                 Symbol = symbol
                                 SymbolKind = lexerSymbol.Kind }
                }

            match lexerSymbol.Kind with 
            | LexerSymbolKind.String ->
                let! targetQuickInfo = getTargetSymbolQuickInfo (None, FSharpTokenTag.STRING)
                return lexerSymbol.Range, None, Some targetQuickInfo
            
            | _ -> 
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation (fcsTextLineNumber, idRange.EndColumn, lineText, lexerSymbol.FullIsland)

            // if the target is in a signature file, adjusting the quick info is unnecessary
            if isSignatureFile document.FilePath then
                let! targetQuickInfo = getTargetSymbolQuickInfo (Some symbolUse.Symbol, FSharpTokenTag.IDENT)
                return symbolUse.Range, None, Some targetQuickInfo
            else
                // find the declaration location of the target symbol, with a preference for signature files
                let findSigDeclarationResult = checkFileResults.GetDeclarationLocation (idRange.StartLine, idRange.EndColumn, lineText, lexerSymbol.FullIsland, preferFlag=true)

                // it is necessary to retrieve the backup quick info because this acquires
                // the textSpan designating where we want the quick info to appear.
                let! targetQuickInfo = getTargetSymbolQuickInfo (Some symbolUse.Symbol, FSharpTokenTag.IDENT)

                let! result =
                    match findSigDeclarationResult with 
                    | FindDeclResult.DeclFound declRange when isSignatureFile declRange.FileName ->
                        asyncMaybe {
                            let! sigQuickInfo = getQuickInfoFromRange(document, declRange, cancellationToken)

                            // if the target was declared in a signature file, and the current file
                            // is not the corresponding module implementation file for that signature,
                            // the doccoms from the signature will overwrite any doccoms that might be
                            // present on the definition/implementation
                            let findImplDefinitionResult = checkFileResults.GetDeclarationLocation (idRange.StartLine, idRange.EndColumn, lineText, lexerSymbol.FullIsland, preferFlag=false)

                            match findImplDefinitionResult  with
                            | FindDeclResult.DeclNotFound _
                            | FindDeclResult.ExternalDecl _ ->
                                return symbolUse.Range, Some sigQuickInfo, None
                            | FindDeclResult.DeclFound declRange ->
                                let! implQuickInfo = getQuickInfoFromRange(document, declRange, cancellationToken)
                                return symbolUse.Range, Some sigQuickInfo, Some { implQuickInfo with Span = targetQuickInfo.Span }
                        }
                    | _ -> async.Return None
                    |> liftAsync

                return result |> Option.defaultValue (symbolUse.Range, None, Some targetQuickInfo)
        }

type internal FSharpAsyncQuickInfoSource
    (
        statusBar: StatusBar,
        xmlMemberIndexService: IVsXMLMemberIndexService,
        metadataAsSource: FSharpMetadataAsSourceService,
        textBuffer:ITextBuffer,
        _settings: EditorOptions
    ) =

    // test helper
    static member ProvideQuickInfo(document: Document, position:int) =
        asyncMaybe {
            let! sourceText = document.GetTextAsync()
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLineNumber = textLine.LineNumber + 1 // Roslyn line numbers are zero-based
            let textLineString = textLine.ToString()
            let! symbol = document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Precise, true, true, nameof(FSharpAsyncQuickInfoSource))

            let! _, checkFileResults = document.GetFSharpParseAndCheckResultsAsync(nameof(FSharpAsyncQuickInfoSource)) |> liftAsync
            let res = checkFileResults.GetToolTip (textLineNumber, symbol.Ident.idRange.EndColumn, textLineString, symbol.FullIsland, FSharpTokenTag.IDENT)
            match res with
            | ToolTipText []
            | ToolTipText [ToolTipElement.None] -> return! None
            | _ ->
                let! symbolUse = checkFileResults.GetSymbolUseAtLocation (textLineNumber, symbol.Ident.idRange.EndColumn, textLineString, symbol.FullIsland)
                let! symbolSpan = RoslynHelpers.TryFSharpRangeToTextSpan (sourceText, symbol.Range)
                return { StructuredText = res
                         Span = symbolSpan
                         Symbol = Some symbolUse.Symbol
                         SymbolKind = symbol.Kind }
        }

    static member BuildSingleQuickInfoItem (documentationBuilder:IDocumentationBuilder) (quickInfo:QuickInfo) =
        let mainDescription, documentation, typeParameterMap, usage, exceptions = ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray()
        XmlDocumentation.BuildDataTipText(documentationBuilder, mainDescription.Add, documentation.Add, typeParameterMap.Add, usage.Add, exceptions.Add, quickInfo.StructuredText)
        let docs = RoslynHelpers.joinWithLineBreaks [documentation; typeParameterMap; usage; exceptions]
        (mainDescription, docs)

    interface IAsyncQuickInfoSource with
        override _.Dispose() = () // no cleanup necessary

        // This method can be called from the background thread.
        // Do not call IServiceProvider.GetService here.
        override _.GetQuickInfoItemAsync(session:IAsyncQuickInfoSession, cancellationToken:CancellationToken) : Task<QuickInfoItem> =
            let triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot)
            match triggerPoint.HasValue with
            | false -> Task.FromResult<QuickInfoItem>(null)
            | true ->
                let triggerPoint = triggerPoint.GetValueOrDefault()
                asyncMaybe {
                    let document = textBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges()
                    let! symbolUseRange, sigQuickInfo, targetQuickInfo = FSharpQuickInfo.getQuickInfo(document, triggerPoint.Position, cancellationToken)
                    let getTrackingSpan (span:TextSpan) =
                        textBuffer.CurrentSnapshot.CreateTrackingSpan(span.Start, span.Length, SpanTrackingMode.EdgeInclusive)

                    let documentationBuilder = XmlDocumentation.CreateDocumentationBuilder(xmlMemberIndexService)
                    match sigQuickInfo, targetQuickInfo with
                    | None, None -> return null
                    | Some quickInfo, None
                    | None, Some quickInfo ->
                        let mainDescription, docs = FSharpAsyncQuickInfoSource.BuildSingleQuickInfoItem documentationBuilder quickInfo
                        let imageId = Tokenizer.GetImageIdForSymbol(quickInfo.Symbol, quickInfo.SymbolKind)
                        let navigation = QuickInfoNavigation(statusBar, metadataAsSource, document, symbolUseRange)
                        let content = QuickInfoViewProvider.provideContent(imageId, mainDescription, docs, navigation)
                        let span = getTrackingSpan quickInfo.Span
                        return QuickInfoItem(span, content)

                    | Some sigQuickInfo, Some targetQuickInfo ->
                        let mainDescription, targetDocumentation, sigDocumentation, typeParameterMap, exceptions, usage = ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray()
                        XmlDocumentation.BuildDataTipText(documentationBuilder, ignore, sigDocumentation.Add, ignore, ignore, ignore, sigQuickInfo.StructuredText)
                        XmlDocumentation.BuildDataTipText(documentationBuilder, mainDescription.Add, targetDocumentation.Add, typeParameterMap.Add, exceptions.Add, usage.Add, targetQuickInfo.StructuredText)
                        // get whitespace nomalized documentation text
                        let getText (tts: seq<TaggedText>) =
                            let text =
                                (StringBuilder(), tts)
                                ||> Seq.fold (fun sb tt ->
                                    if String.IsNullOrWhiteSpace tt.Text then sb else sb.Append tt.Text)
                                |> string
                            if String.IsNullOrWhiteSpace text then None else Some text

                        let documentation =
                            [ match getText targetDocumentation, getText sigDocumentation with
                              | None, None -> ()
                              | None, Some _ -> yield! sigDocumentation
                              | Some _, None -> yield! targetDocumentation
                              | Some implText, Some sigText when implText.Equals (sigText, StringComparison.OrdinalIgnoreCase) ->
                                    yield! sigDocumentation
                              | Some _  , Some _ ->
                                    yield! RoslynHelpers.joinWithLineBreaks [ sigDocumentation; [ TaggedText.tagText "-------------" ]; targetDocumentation ]
                            ] |> ResizeArray
                        let docs = RoslynHelpers.joinWithLineBreaks [documentation; typeParameterMap; usage; exceptions]
                        let imageId = Tokenizer.GetImageIdForSymbol(targetQuickInfo.Symbol, targetQuickInfo.SymbolKind)
                        let navigation = QuickInfoNavigation(statusBar, metadataAsSource, document, symbolUseRange)
                        let content = QuickInfoViewProvider.provideContent(imageId, mainDescription, docs, navigation)
                        let span = getTrackingSpan targetQuickInfo.Span
                        return QuickInfoItem(span, content)
                }   |> Async.map Option.toObj
                    |> RoslynHelpers.StartAsyncAsTask cancellationToken

[<Export(typeof<IAsyncQuickInfoSourceProvider>)>]
[<Name("F# Quick Info Provider")>]
[<ContentType(FSharpConstants.FSharpLanguageName)>]
[<Order>]
type internal FSharpAsyncQuickInfoSourceProvider
    [<ImportingConstructor>]
    (
        [<Import(typeof<SVsServiceProvider>)>] serviceProvider: IServiceProvider,
        metadataAsSource: FSharpMetadataAsSourceService,
        settings: EditorOptions
    ) =

    interface IAsyncQuickInfoSourceProvider with
        override _.TryCreateQuickInfoSource(textBuffer:ITextBuffer) : IAsyncQuickInfoSource =
            // GetService calls must be made on the UI thread
            // It is safe to do it here (see #4713)
            let statusBar = StatusBar(serviceProvider.GetService<SVsStatusbar,IVsStatusbar>())
            let xmlMemberIndexService = serviceProvider.XMLMemberIndexService
            new FSharpAsyncQuickInfoSource(statusBar, xmlMemberIndexService, metadataAsSource, textBuffer, settings) :> IAsyncQuickInfoSource
