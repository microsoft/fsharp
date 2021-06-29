// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System

open Microsoft.CodeAnalysis

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open Microsoft.VisualStudio.Shell.Interop

type internal QuickInfoNavigation
    (
        statusBar: StatusBar,
        metadataAsSource: FSharpMetadataAsSourceService,
        initialDoc: Document,
        thisSymbolUseRange: range
    ) =

    let workspace = initialDoc.Project.Solution.Workspace
    let solution = workspace.CurrentSolution

    member _.IsTargetValid (range: range) =
        range <> rangeStartup &&
        range <> thisSymbolUseRange &&
        solution.TryGetDocumentIdFromFSharpRange (range, initialDoc.Project.Id) |> Option.isSome

    member _.RelativePath (range: range) =
        let relativePathEscaped =
            match solution.FilePath with
            | null -> range.FileName
            | sfp ->
                let targetUri = Uri(range.FileName)
                Uri(sfp).MakeRelativeUri(targetUri).ToString()
        relativePathEscaped |> Uri.UnescapeDataString

    member _.NavigateTo (range: range) =
        asyncMaybe {
            let targetPath = range.FileName
            let! targetDoc = solution.TryGetDocumentFromFSharpRange (range, initialDoc.Project.Id)
            let! targetSource = targetDoc.GetTextAsync()
            let! targetTextSpan = RoslynHelpers.TryFSharpRangeToTextSpan (targetSource, range)
            let gtd = GoToDefinition(metadataAsSource)

            // To ensure proper navigation decsions, we need to check the type of document the navigation call
            // is originating from and the target we're provided by default:
            //  - signature files (.fsi) should navigate to other signature files 
            //  - implementation files (.fs) should navigate to other implementation files
            let (|Signature|Implementation|) filepath =
                if isSignatureFile filepath then Signature else Implementation
           
            match initialDoc.FilePath, targetPath with
            | Signature, Signature
            | Implementation, Implementation ->
                return gtd.TryNavigateToTextSpan(targetDoc, targetTextSpan, statusBar)

            // Adjust the target from signature to implementation.
            | Implementation, Signature  ->
                return! gtd.NavigateToSymbolDefinitionAsync(targetDoc, targetSource, range, statusBar)
                
            // Adjust the target from implmentation to signature.
            | Signature, Implementation ->
                return! gtd.NavigateToSymbolDeclarationAsync(targetDoc, targetSource, range, statusBar)
        } |> Async.Ignore |> Async.StartImmediate
