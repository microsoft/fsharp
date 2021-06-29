// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Threading
open System.Threading.Tasks
open System.ComponentModel.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.Navigation
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Navigation

open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Shell

[<AllowNullLiteral>]
type internal FSharpNavigableSymbol(item: FSharpNavigableItem, span: SnapshotSpan, gtd: GoToDefinition, statusBar: StatusBar) =
    interface INavigableSymbol with
        member _.Navigate(_: INavigableRelationship) =
            gtd.NavigateToItem(item, statusBar)

        member _.Relationships = seq { yield PredefinedNavigableRelationships.Definition }

        member _.SymbolSpan = span

type internal FSharpNavigableSymbolSource(metadataAsSource, serviceProvider: IServiceProvider) =
    
    let mutable disposed = false
    let gtd = GoToDefinition(metadataAsSource)
    let statusBar = StatusBar(serviceProvider.GetService<SVsStatusbar,IVsStatusbar>())

    interface INavigableSymbolSource with
        member _.GetNavigableSymbolAsync(triggerSpan: SnapshotSpan, cancellationToken: CancellationToken) =
            // Yes, this is a code smell. But this is how the editor API accepts what we would treat as None.
            if disposed then null
            else
                asyncMaybe {
                    let snapshot = triggerSpan.Snapshot
                    let position = triggerSpan.Start.Position
                    let document = snapshot.GetOpenDocumentInCurrentContextWithChanges()
                    let! sourceText = document.GetTextAsync(cancellationToken) |> liftTaskAsync
                    
                    statusBar.Message(SR.LocatingSymbol())
                    use _ = statusBar.Animate()

                    let gtdTask = gtd.FindDefinitionTask(document, position, cancellationToken)

                    // Wrap this in a try/with as if the user clicks "Cancel" on the thread dialog, we'll be cancelled.
                    // Task.Wait throws an exception if the task is cancelled, so be sure to catch it.
                    try
                        // This call to Wait() is fine because we want to be able to provide the error message in the status bar.
                        gtdTask.Wait(cancellationToken)
                        statusBar.Clear()

                        if gtdTask.Status = TaskStatus.RanToCompletion && gtdTask.Result.IsSome then
                            let result, range = gtdTask.Result.Value

                            let declarationTextSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, range)
                            let declarationSpan = Span(declarationTextSpan.Start, declarationTextSpan.Length)
                            let symbolSpan = SnapshotSpan(snapshot, declarationSpan)

                            match result with
                            | FSharpGoToDefinitionResult.NavigableItem(navItem) ->
                                return FSharpNavigableSymbol(navItem, symbolSpan, gtd, statusBar) :> INavigableSymbol

                            | FSharpGoToDefinitionResult.ExternalAssembly(targetSymbolUse, metadataReferences) ->
                                let nav =
                                    { new INavigableSymbol with
                                        member _.Navigate(_: INavigableRelationship) =
                                            // Need to new up a CTS here instead of re-using the other one, since VS
                                            // will navigate disconnected from the outer routine, leading to an
                                            // OperationCancelledException if you use the one defined outside.
                                            use ct = new CancellationTokenSource()
                                            gtd.NavigateToExternalDeclaration(targetSymbolUse, metadataReferences, ct.Token, statusBar)

                                        member _.Relationships = seq { yield PredefinedNavigableRelationships.Definition }

                                        member _.SymbolSpan = symbolSpan }
                                return nav
                        else 
                            statusBar.TempMessage(SR.CannotDetermineSymbol())

                            // The NavigableSymbols API accepts 'null' when there's nothing to navigate to.
                            return null
                    with exc ->
                        statusBar.TempMessage(String.Format(SR.NavigateToFailed(), Exception.flattenMessage exc))

                        // The NavigableSymbols API accepts 'null' when there's nothing to navigate to.
                        return null
                }
                |> Async.map Option.toObj
                |> RoslynHelpers.StartAsyncAsTask cancellationToken
        
        member _.Dispose() =
            disposed <- true

[<Export(typeof<INavigableSymbolSourceProvider>)>]
[<Name("F# Navigable Symbol Service")>]
[<ContentType(Constants.FSharpContentType)>]
[<Order>]
type internal FSharpNavigableSymbolService
    [<ImportingConstructor>]
    (
        [<Import(typeof<SVsServiceProvider>)>] serviceProvider: IServiceProvider,
        metadataAsSource: FSharpMetadataAsSourceService
    ) =

    interface INavigableSymbolSourceProvider with
        member _.TryCreateNavigableSymbolSource(_: ITextView, _: ITextBuffer) =
            new FSharpNavigableSymbolSource(metadataAsSource, serviceProvider) :> INavigableSymbolSource