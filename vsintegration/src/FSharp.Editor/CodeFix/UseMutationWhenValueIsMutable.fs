// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "UseMutationWhenValueIsMutable"); Shared>]
type internal FSharpUseMutationWhenValueIsMutableFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    inherit CodeFixProvider()

    static let userOpName = "UseMutationWhenValueIsMutable"

    let fixableDiagnosticIds = set ["FS0020"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let document = context.Document
            do! Option.guard (not(isSignatureFile document.FilePath))
            let checker = checkerProvider.Checker
            let! parsingOptions, projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, CancellationToken.None, userOpName)
            let! sourceText = document.GetTextAsync () |> liftTaskAsync
            let defines = CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
            
            let adjustedPosition =
                let rec loop ch pos =
                    if Char.IsWhiteSpace(ch) then
                        pos
                    else
                        loop sourceText.[pos + 1] (pos + 1)

                loop sourceText.[context.Span.Start] context.Span.Start

            let textLine = sourceText.Lines.GetLineFromPosition adjustedPosition
            let textLinePos = sourceText.Lines.GetLinePosition adjustedPosition
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! _, checkFileResults = checker.CheckDocumentInProject(document, projectOptions) |> liftAsync
            let! lexerSymbol = Tokenizer.getSymbolAtPosition (document.Id, sourceText, adjustedPosition, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
            let! symbolUse = checkFileResults.GetSymbolUseAtLocation(fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland)

            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as mfv when mfv.IsMutable || mfv.HasSetterMethod ->
                let title = SR.UseMutationWhenValueIsMutable()
                let! symbolSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, symbolUse.Range)
                let mutable pos = symbolSpan.End
                let mutable ch = sourceText.[pos]

                // We're looking for the possibly erroneous '='
                while pos <= context.Span.Length && ch <> '=' do
                    pos <- pos + 1
                    ch <- sourceText.[pos]

                let codeFix =
                    CodeFixHelpers.createTextChangeCodeFix(
                        title,
                        context,
                        (fun () -> asyncMaybe.Return [| TextChange(TextSpan(pos + 1, 1), "<-") |]))

                context.RegisterCodeFix(codeFix, diagnostics)
            | _ -> ()
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)  