﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "MakeOuterBindingRecursive"); Shared>]
type internal FSharpMakeOuterBindingRecursiveCodeFixProvider
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider, 
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    inherit CodeFixProvider()

    static let userOpName = "MakeOuterBindingRecursive"
    let fixableDiagnosticIds = set ["FS0039"]

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context =
        asyncMaybe {
            let! sourceText = context.Document.GetTextAsync(context.CancellationToken)
            let! parsingOptions, _ = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(context.Document, context.CancellationToken, userOpName)
            let! parseResults = checkerProvider.Checker.ParseFile(context.Document.FilePath, sourceText.ToFSharpSourceText(), parsingOptions, userOpName) |> liftAsync

            let diagnosticRange = RoslynHelpers.TextSpanToFSharpRange(context.Document.FilePath, context.Span, sourceText)
            do! Option.guard (parseResults.IsPosContainedInApplication diagnosticRange.Start)

            let! outerBindingRange = parseResults.TryRangeOfNameOfNearestOuterBindingContainingPos diagnosticRange.Start
            let! outerBindingNameSpan = RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, outerBindingRange)

            // One last check to verify the names are the same
            do! Option.guard (sourceText.GetSubText(outerBindingNameSpan).ContentEquals(sourceText.GetSubText(context.Span)))

            let diagnostics =
                context.Diagnostics
                |> Seq.filter (fun x -> fixableDiagnosticIds |> Set.contains x.Id)
                |> Seq.toImmutableArray

            let title = SR.MakeOuterBindingRecursive()

            let codeFix =
                CodeFixHelpers.createTextChangeCodeFix(
                    title,
                    context,
                    (fun () -> asyncMaybe.Return [| TextChange(TextSpan(outerBindingNameSpan.Start, 0), "rec ") |]))

            context.RegisterCodeFix(codeFix, diagnostics)
        }
        |> Async.Ignore
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken) 
