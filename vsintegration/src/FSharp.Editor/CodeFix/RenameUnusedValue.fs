// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "RenameUnusedValue"); Shared>]
type internal FSharpRenameUnusedValueCodeFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    
    inherit CodeFixProvider()
    static let userOpName = "RenameUnusedValueCodeFix"
    let fixableDiagnosticIds = set ["FS1182"]
    let checker = checkerProvider.Checker

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            // Don't show code fixes for unused values, even if they are compiler-generated.
            do! Option.guard context.Document.FSharpOptions.CodeFixes.UnusedDeclarations

            let document = context.Document
            let! sourceText = document.GetTextAsync(context.CancellationToken)
            let ident = sourceText.ToString(context.Span)
            // Prefixing operators and backticked identifiers does not make sense.
            // We have to use the additional check for backtickes because `IsOperatorOrBacktickedName` operates on display names
            // where backtickes are replaced with parens.
            if not (PrettyNaming.IsOperatorOrBacktickedName ident) && not (ident.StartsWith "``") then
                let! parsingOptions, projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, context.CancellationToken, userOpName)
                let! _, checkResults = checker.CheckDocumentInProject(document, projectOptions) |> liftAsync
                let m = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, context.Span, sourceText)
                let defines = CompilerEnvironment.GetCompilationDefinesForEditing parsingOptions
                let! lexerSymbol = Tokenizer.getSymbolAtPosition (document.Id, sourceText, context.Span.Start, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
                let lineText = (sourceText.Lines.GetLineFromPosition context.Span.Start).ToString()  
                let! symbolUse = checkResults.GetSymbolUseAtLocation(m.StartLine, m.EndColumn, lineText, lexerSymbol.FullIsland)
                let symbolName = symbolUse.Symbol.DisplayName

                let diagnostics =
                    context.Diagnostics
                    |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                    |> Seq.toImmutableArray

                match symbolUse.Symbol with
                | :? FSharpMemberOrFunctionOrValue as func ->
                    let prefixTitle = String.Format(SR.PrefixValueNameWithUnderscore(), symbolName)
                    let prefixCodeFix =
                        CodeFixHelpers.createTextChangeCodeFix(
                            prefixTitle,
                            context,
                            (fun () -> asyncMaybe.Return [| TextChange(TextSpan(context.Span.Start, 0), "_") |]))
                    context.RegisterCodeFix(prefixCodeFix, diagnostics)

                    if func.IsValue then
                        let replaceTitle = String.Format(SR.RenameValueToUnderscore(), symbolName)
                        let replaceCodeFix =
                            CodeFixHelpers.createTextChangeCodeFix(
                                replaceTitle,
                                context,
                                (fun () -> asyncMaybe.Return [| TextChange(context.Span, "_") |]))
                        context.RegisterCodeFix(replaceCodeFix, diagnostics)
                | _ -> ()
        } 
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
