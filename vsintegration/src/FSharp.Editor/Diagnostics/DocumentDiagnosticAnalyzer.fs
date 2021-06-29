// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Immutable
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics

[<RequireQualifiedAccess>]
type internal DiagnosticsType =
    | Syntax
    | Semantic

[<Export(typeof<IFSharpDocumentDiagnosticAnalyzer>)>]
type internal FSharpDocumentDiagnosticAnalyzer
    [<ImportingConstructor>]
    (
    ) =

    static let errorInfoEqualityComparer =
        { new IEqualityComparer<FSharpDiagnostic> with 
            member _.Equals (x, y) =
                x.FileName = y.FileName &&
                x.StartLine = y.StartLine &&
                x.EndLine = y.EndLine &&
                x.StartColumn = y.StartColumn &&
                x.EndColumn = y.EndColumn &&
                x.Severity = y.Severity &&
                x.Message = y.Message &&
                x.Subcategory = y.Subcategory &&
                x.ErrorNumber = y.ErrorNumber
            member _.GetHashCode x =
                let mutable hash = 17
                hash <- hash * 23 + x.StartLine.GetHashCode()
                hash <- hash * 23 + x.EndLine.GetHashCode()
                hash <- hash * 23 + x.StartColumn.GetHashCode()
                hash <- hash * 23 + x.EndColumn.GetHashCode()
                hash <- hash * 23 + x.Severity.GetHashCode()
                hash <- hash * 23 + x.Message.GetHashCode()
                hash <- hash * 23 + x.Subcategory.GetHashCode()
                hash <- hash * 23 + x.ErrorNumber.GetHashCode()
                hash 
        }

    static member GetDiagnostics(document: Document, diagnosticType: DiagnosticsType) = 
        async {
            let! ct = Async.CancellationToken

            let! parseResults = document.GetFSharpParseResultsAsync("GetDiagnostics")

            let! sourceText = document.GetTextAsync(ct) |> Async.AwaitTask
            let filePath = document.FilePath

            let! errors = 
                async {
                    match diagnosticType with
                    | DiagnosticsType.Semantic ->
                        let! _, checkResults = document.GetFSharpParseAndCheckResultsAsync("GetDiagnostics")
                        // In order to eleminate duplicates, we should not return parse errors here because they are returned by `AnalyzeSyntaxAsync` method.
                        let allErrors = HashSet(checkResults.Diagnostics, errorInfoEqualityComparer)
                        allErrors.ExceptWith(parseResults.Diagnostics)
                        return Seq.toArray allErrors
                    | DiagnosticsType.Syntax ->
                        return parseResults.Diagnostics
                }
            
            let results = 
                HashSet(errors, errorInfoEqualityComparer)
                |> Seq.choose(fun error ->
                    if error.StartLine = 0 || error.EndLine = 0 then
                        // F# error line numbers are one-based. Compiler returns 0 for global errors (reported by ProjectDiagnosticAnalyzer)
                        None
                    else
                        // Roslyn line numbers are zero-based
                        let linePositionSpan = LinePositionSpan(LinePosition(error.StartLine - 1, error.StartColumn), LinePosition(error.EndLine - 1, error.EndColumn))
                        let textSpan = sourceText.Lines.GetTextSpan(linePositionSpan)
                        
                        // F# compiler report errors at end of file if parsing fails. It should be corrected to match Roslyn boundaries
                        let correctedTextSpan =
                            if textSpan.End <= sourceText.Length then 
                                textSpan 
                            else 
                                let start =
                                    min textSpan.Start (sourceText.Length - 1)
                                    |> max 0

                                TextSpan.FromBounds(start, sourceText.Length)
                        
                        let location = Location.Create(filePath, correctedTextSpan , linePositionSpan)
                        Some(RoslynHelpers.ConvertError(error, location)))
                |> Seq.toImmutableArray
            return results
        }

    interface IFSharpDocumentDiagnosticAnalyzer with

        member this.AnalyzeSyntaxAsync(document: Document, cancellationToken: CancellationToken): Task<ImmutableArray<Diagnostic>> =
            if document.Project.IsFSharpMetadata then Task.FromResult(ImmutableArray.Empty)
            else

            asyncMaybe {
                return! 
                    FSharpDocumentDiagnosticAnalyzer.GetDiagnostics(document, DiagnosticsType.Syntax)
                    |> liftAsync
            } 
            |> Async.map (Option.defaultValue ImmutableArray<Diagnostic>.Empty)
            |> RoslynHelpers.StartAsyncAsTask cancellationToken

        member this.AnalyzeSemanticsAsync(document: Document, cancellationToken: CancellationToken): Task<ImmutableArray<Diagnostic>> =
            if document.Project.IsFSharpMiscellaneousOrMetadata && not document.IsFSharpScript then Task.FromResult(ImmutableArray.Empty)
            else

            asyncMaybe {
                return! 
                    FSharpDocumentDiagnosticAnalyzer.GetDiagnostics(document, DiagnosticsType.Semantic)
                    |> liftAsync
            }
            |> Async.map (Option.defaultValue ImmutableArray<Diagnostic>.Empty)
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
