// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition
open System.Threading.Tasks

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics

open FSharp.Compiler.Text

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "RemoveUnusedOpens"); Shared>]
type internal FSharpRemoveUnusedOpensCodeFixProvider
    [<ImportingConstructor>]
    (
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = [FSharpIDEDiagnosticIds.RemoveUnnecessaryImportsDiagnosticId]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let document = context.Document
            let! sourceText = document.GetTextAsync()
            let! unusedOpens = UnusedOpensDiagnosticAnalyzer.GetUnusedOpenRanges(document)
            let changes =
                unusedOpens
                |> List.map (fun m ->
                    let span = sourceText.Lines.[Line.toZ m.StartLine].SpanIncludingLineBreak
                    TextChange(span, ""))
                |> List.toArray

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> List.contains x.Id)
                |> Seq.toImmutableArray

            let title = SR.RemoveUnusedOpens()

            let codefix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return changes))

            context.RegisterCodeFix(codefix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)

    override _.GetFixAllProvider() = WellKnownFixAllProviders.BatchFixer
 