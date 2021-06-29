// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Immutable
open System.Diagnostics
open System.Threading

open Microsoft.CodeAnalysis

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics

[<Export(typeof<IFSharpUnusedOpensDiagnosticAnalyzer>)>]
type internal UnusedOpensDiagnosticAnalyzer
    [<ImportingConstructor>]
    (
    ) =

    static member GetUnusedOpenRanges(document: Document) : Async<Option<range list>> =
        asyncMaybe {
            do! Option.guard document.Project.IsFSharpCodeFixesUnusedOpensEnabled
            let! sourceText = document.GetTextAsync()
            let! _, checkResults = document.GetFSharpParseAndCheckResultsAsync(nameof(UnusedOpensDiagnosticAnalyzer)) |> liftAsync
            let! unusedOpens = UnusedOpens.getUnusedOpens(checkResults, fun lineNumber -> sourceText.Lines.[Line.toZ lineNumber].ToString()) |> liftAsync
            return unusedOpens
        } 

    interface IFSharpUnusedOpensDiagnosticAnalyzer with

        member _.AnalyzeSemanticsAsync(descriptor, document: Document, cancellationToken: CancellationToken) =
            if document.Project.IsFSharpMiscellaneousOrMetadata && not document.IsFSharpScript then Tasks.Task.FromResult(ImmutableArray.Empty)
            else

            asyncMaybe {
                do Trace.TraceInformation("{0:n3} (start) UnusedOpensAnalyzer", DateTime.Now.TimeOfDay.TotalSeconds)
                let! sourceText = document.GetTextAsync()
                let! unusedOpens = UnusedOpensDiagnosticAnalyzer.GetUnusedOpenRanges(document)
            
                return 
                    unusedOpens
                    |> List.map (fun range ->
                          Diagnostic.Create(
                             descriptor,
                             RoslynHelpers.RangeToLocation(range, sourceText, document.FilePath)))
                    |> Seq.toImmutableArray
            } 
            |> Async.map (Option.defaultValue ImmutableArray.Empty)
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
