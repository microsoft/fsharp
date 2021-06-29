// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.IO
open System.Composition
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading.Tasks
open System.Runtime.Caching
open System.Globalization

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.PatternMatching
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Navigation
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.NavigateTo

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Syntax

type internal NavigableItem(document: Document, sourceSpan: TextSpan, glyph: Glyph, name: string, kind: string, additionalInfo: string) =
    inherit FSharpNavigableItem(glyph, ImmutableArray.Create (TaggedText(TextTags.Text, name)), document, sourceSpan)

    member _.Name = name
    member _.Kind = kind
    member _.AdditionalInfo = additionalInfo

type internal NavigateToSearchResult(item: NavigableItem, matchKind: FSharpNavigateToMatchKind) =
    inherit FSharpNavigateToSearchResult(item.AdditionalInfo, item.Kind, matchKind, item.Name, item)

module private Index =
    [<System.Diagnostics.DebuggerDisplay("{DebugString()}")>]
    type private IndexEntry(str: string, offset: int, item: NavigableItem) =
        member _.String = str
        member _.Offset = offset
        member _.Length = str.Length - offset
        member _.Item = item
        member x.StartsWith (s: string) = 
            if s.Length > x.Length then false
            else CultureInfo.CurrentCulture.CompareInfo.IndexOf(str, s, offset, s.Length, CompareOptions.IgnoreCase) = offset

    let private indexEntryComparer =
        { new IComparer<IndexEntry> with
            member _.Compare(a, b) = 
                let res = CultureInfo.CurrentCulture.CompareInfo.Compare(a.String, a.Offset, b.String, b.Offset, CompareOptions.IgnoreCase)
                if res = 0 then a.Offset.CompareTo(b.Offset) else res }

    type IIndexedNavigableItems =
        abstract Find: searchValue: string -> FSharpNavigateToSearchResult []
        abstract AllItems: NavigableItem []

    let private navigateToSearchResultComparer =
        { new IEqualityComparer<FSharpNavigateToSearchResult> with 
            member _.Equals(x: FSharpNavigateToSearchResult, y: FSharpNavigateToSearchResult) =
                match x, y with
                | null, _ | _, null -> false
                | _ -> x.NavigableItem.Document.Id = y.NavigableItem.Document.Id &&
                       x.NavigableItem.SourceSpan = y.NavigableItem.SourceSpan
            
            member _.GetHashCode(x: FSharpNavigateToSearchResult) =
                if isNull x then 0
                else 23 * (17 * 23 + x.NavigableItem.Document.Id.GetHashCode()) + x.NavigableItem.SourceSpan.GetHashCode() }

    let build (items: NavigableItem []) =
        let entries = ResizeArray()

        for item in items do
            let name = 
                if PrettyNaming.IsMangledOpName item.Name then 
                    PrettyNaming.DecompileOpName item.Name 
                else 
                    item.Name
            for i = 0 to name.Length - 1 do
                entries.Add(IndexEntry(name, i, item))

        entries.Sort(indexEntryComparer)
        { new IIndexedNavigableItems with
              member _.Find (searchValue) =
                  let result = HashSet(navigateToSearchResultComparer)
                  if entries.Count > 0 then 
                     let entryToFind = IndexEntry(searchValue, 0, Unchecked.defaultof<_>)
                     
                     let initial = 
                         let p = entries.BinarySearch(entryToFind, indexEntryComparer)
                         if p < 0 then ~~~p else p
                     
                     let handle index =
                         let entry = entries.[index]
                         let matchKind = 
                             if entry.Offset = 0 then
                                 if entry.Length = searchValue.Length then FSharpNavigateToMatchKind.Exact
                                 else FSharpNavigateToMatchKind.Prefix
                             else FSharpNavigateToMatchKind.Substring
                         let item = entry.Item
                         result.Add (NavigateToSearchResult(item, matchKind) :> FSharpNavigateToSearchResult) |> ignore
                    
                     // in case if there are multiple matching items binary search might return not the first one.
                     // in this case we'll walk backwards searching for the applicable answers
                     let mutable pos = initial
                     while pos >= 0 && pos < entries.Count && entries.[pos].StartsWith searchValue do
                         handle pos
                         pos <- pos - 1
                    
                     // value of 'initial' position was already handled on the previous step so here we'll bump it
                     let mutable pos = initial + 1
                     while pos < entries.Count && entries.[pos].StartsWith searchValue do
                         handle pos
                         pos <- pos + 1
                  Seq.toArray result 
              member _.AllItems = items }

[<AutoOpen>]
module private Utils =

    let navigateToItemKindToRoslynKind = function
        | NavigableItemKind.Module -> FSharpNavigateToItemKind.Module
        | NavigableItemKind.ModuleAbbreviation -> FSharpNavigateToItemKind.Module
        | NavigableItemKind.Exception -> FSharpNavigateToItemKind.Class
        | NavigableItemKind.Type -> FSharpNavigateToItemKind.Class
        | NavigableItemKind.ModuleValue -> FSharpNavigateToItemKind.Field
        | NavigableItemKind.Field -> FSharpNavigateToItemKind.Field
        | NavigableItemKind.Property -> FSharpNavigateToItemKind.Property
        | NavigableItemKind.Constructor -> FSharpNavigateToItemKind.Method
        | NavigableItemKind.Member -> FSharpNavigateToItemKind.Method
        | NavigableItemKind.EnumCase -> FSharpNavigateToItemKind.EnumItem
        | NavigableItemKind.UnionCase -> FSharpNavigateToItemKind.EnumItem

    let navigateToItemKindToGlyph = function
        | NavigableItemKind.Module -> Glyph.ModulePublic
        | NavigableItemKind.ModuleAbbreviation -> Glyph.ModulePublic
        | NavigableItemKind.Exception -> Glyph.ClassPublic
        | NavigableItemKind.Type -> Glyph.ClassPublic
        | NavigableItemKind.ModuleValue -> Glyph.FieldPublic
        | NavigableItemKind.Field -> Glyph.FieldPublic
        | NavigableItemKind.Property -> Glyph.PropertyPublic
        | NavigableItemKind.Constructor -> Glyph.MethodPublic
        | NavigableItemKind.Member -> Glyph.MethodPublic
        | NavigableItemKind.EnumCase -> Glyph.EnumPublic
        | NavigableItemKind.UnionCase -> Glyph.EnumPublic

    let containerToString (container: NavigableContainer) (document: Document) =
        let project = document.Project
        let typeAsString =
            match container.Type with
            | NavigableContainerType.File -> "project "
            | NavigableContainerType.Namespace -> "namespace "
            | NavigableContainerType.Module -> "module "
            | NavigableContainerType.Exception -> "exception "
            | NavigableContainerType.Type -> "type "
        let name =
            match container.Type with
            | NavigableContainerType.File ->
                (Path.GetFileNameWithoutExtension project.Name) + ", " + (Path.GetFileName container.Name)
            | _ -> container.Name

        let combined = typeAsString + name

        if isSignatureFile document.FilePath then
            "signature for: " + combined
        else
            combined

    type PerDocumentSavedData = { Hash: int; Items: Index.IIndexedNavigableItems }

[<Export(typeof<IFSharpNavigateToSearchService>)>]
type internal FSharpNavigateToSearchService 
    [<ImportingConstructor>] 
    (
    ) =

    let kindsProvided = ImmutableHashSet.Create(FSharpNavigateToItemKind.Module, FSharpNavigateToItemKind.Class, FSharpNavigateToItemKind.Field, FSharpNavigateToItemKind.Property, FSharpNavigateToItemKind.Method, FSharpNavigateToItemKind.Enum, FSharpNavigateToItemKind.EnumItem) :> IImmutableSet<string>

    // Save the backing navigation data in a memory cache held in a sliding window
    let itemsByDocumentId = new MemoryCache("FSharp.Editor.FSharpNavigateToSearchService")

    let GetNavigableItems(document: Document, kinds: IImmutableSet<string>) =
        async {
            let! cancellationToken = Async.CancellationToken
            let! parseResults = document.GetFSharpParseResultsAsync(nameof(FSharpNavigateToSearchService))
            let! sourceText = document.GetTextAsync(cancellationToken) |> Async.AwaitTask
            let navItems parsedInput =
                NavigateTo.GetNavigableItems parsedInput
                |> Array.filter (fun i -> kinds.Contains(navigateToItemKindToRoslynKind i.Kind))

            let items = parseResults.ParseTree |> navItems
            let navigableItems =
                [|
                    for item in items do
                        match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, item.Range) with 
                        | None -> ()
                        | Some sourceSpan ->
                            let glyph = navigateToItemKindToGlyph item.Kind
                            let kind = navigateToItemKindToRoslynKind item.Kind
                            let additionalInfo = containerToString item.Container document
                            let _name =
                                if isSignatureFile document.FilePath then
                                    item.Name + " (signature)"
                                else
                                    item.Name
                            yield NavigableItem(document, sourceSpan, glyph, item.Name, kind, additionalInfo)
                |]
            return navigableItems
        }

    let getCachedIndexedNavigableItems(document: Document, kinds: IImmutableSet<string>) =
        async {
            let! cancellationToken = Async.CancellationToken
            let! textVersion = document.GetTextVersionAsync(cancellationToken)  |> Async.AwaitTask
            let textVersionHash = hash textVersion
            let key = document.Id.ToString()
            match itemsByDocumentId.Get(key) with
            | :? PerDocumentSavedData as data when data.Hash = textVersionHash -> return data.Items
            | _ -> 
                let! items = GetNavigableItems(document, kinds)
                let indexedItems = Index.build items
                let data = { Hash= textVersionHash; Items = indexedItems }
                let cacheItem = CacheItem(key, data)
                let policy = CacheItemPolicy(SlidingExpiration=DefaultTuning.PerDocumentSavedDataSlidingWindow)
                itemsByDocumentId.Set(cacheItem, policy)
                return indexedItems }

    let patternMatchKindToNavigateToMatchKind = function
        | PatternMatchKind.Exact -> FSharpNavigateToMatchKind.Exact
        | PatternMatchKind.Prefix -> FSharpNavigateToMatchKind.Prefix
        | PatternMatchKind.Substring -> FSharpNavigateToMatchKind.Substring
        | PatternMatchKind.CamelCase -> FSharpNavigateToMatchKind.Regular
        | PatternMatchKind.Fuzzy -> FSharpNavigateToMatchKind.Regular
        | _ -> FSharpNavigateToMatchKind.Regular

    interface IFSharpNavigateToSearchService with
        member _.SearchProjectAsync(project, _priorityDocuments, searchPattern, kinds, cancellationToken) : Task<ImmutableArray<FSharpNavigateToSearchResult>> =
            asyncMaybe {
                let! items =
                    project.Documents
                    |> Seq.map (fun document -> getCachedIndexedNavigableItems(document, kinds))
                    |> Async.Parallel
                    |> liftAsync
                
                let items =
                    if searchPattern.Length = 1 then
                        items 
                        |> Array.map (fun items -> items.Find(searchPattern))
                        |> Array.concat
                        |> Array.filter (fun x -> x.Name.Length = 1 && String.Equals(x.Name, searchPattern, StringComparison.InvariantCultureIgnoreCase))
                    else
                        [| yield! items |> Array.map (fun items -> items.Find(searchPattern)) |> Array.concat
                           use patternMatcher = new PatternMatcher(searchPattern, allowFuzzyMatching = true)
                           yield! items
                                  |> Array.collect (fun item -> item.AllItems)
                                  |> Array.Parallel.collect (fun x -> 
                                      patternMatcher.GetMatches(x.Name)
                                      |> Seq.map (fun pm ->
                                          NavigateToSearchResult(x, patternMatchKindToNavigateToMatchKind pm.Kind) :> FSharpNavigateToSearchResult)
                                      |> Seq.toArray) |]

                return items |> Array.distinctBy (fun x -> x.NavigableItem.Document.Id, x.NavigableItem.SourceSpan)
            } 
            |> Async.map (Option.defaultValue [||])
            |> Async.map Seq.toImmutableArray
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)

        member _.SearchDocumentAsync(document, searchPattern, kinds, cancellationToken) : Task<ImmutableArray<FSharpNavigateToSearchResult>> =
            asyncMaybe {
                let! items = getCachedIndexedNavigableItems(document, kinds) |> liftAsync
                return items.Find(searchPattern)
            }
            |> Async.map (Option.defaultValue [||])
            |> Async.map Seq.toImmutableArray
            |> RoslynHelpers.StartAsyncAsTask(cancellationToken)

        member _.KindsProvided = kindsProvided

        member _.CanFilter = true