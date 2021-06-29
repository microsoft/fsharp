// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeFixes
open Microsoft.CodeAnalysis.CodeActions

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

[<ExportCodeFixProvider(FSharpConstants.FSharpLanguageName, Name = "AddOpen"); Shared>]
type internal FSharpAddOpenCodeFixProvider
    [<ImportingConstructor>]
    (
        assemblyContentProvider: AssemblyContentProvider
    ) =
    inherit CodeFixProvider()

    let fixableDiagnosticIds = ["FS0039"; "FS0043"]

    let fixUnderscoresInMenuText (text: string) = text.Replace("_", "__")

    let qualifySymbolFix (context: CodeFixContext) (fullName, qualifier) =
        CodeFixHelpers.createTextChangeCodeFix(
            fixUnderscoresInMenuText fullName,
            context,
            (fun () -> asyncMaybe.Return [| TextChange(context.Span, qualifier) |]))

    let openNamespaceFix (context: CodeFixContext) ctx name ns multipleNames = 
        let displayText = "open " + ns + if multipleNames then " (" + name + ")" else ""
        CodeAction.Create(
            fixUnderscoresInMenuText displayText,
            (fun (cancellationToken: CancellationToken) -> 
                async {
                    let! sourceText = context.Document.GetTextAsync(cancellationToken) |> Async.AwaitTask
                    let changedText, _ = OpenDeclarationHelper.insertOpenDeclaration sourceText ctx ns
                    return context.Document.WithText(changedText)
                } |> RoslynHelpers.StartAsyncAsTask(cancellationToken)),
            displayText)

    let addSuggestionsAsCodeFixes (context: CodeFixContext) (candidates: (InsertionContextEntity * InsertionContext) list) =
        let openNamespaceFixes =
            candidates
            |> Seq.choose (fun (entity, ctx) -> entity.Namespace |> Option.map (fun ns -> ns, entity.FullDisplayName, ctx))
            |> Seq.groupBy (fun (ns, _, _) -> ns)
            |> Seq.map (fun (ns, xs) -> 
                ns, 
                xs 
                |> Seq.map (fun (_, name, ctx) -> name, ctx) 
                |> Seq.distinctBy (fun (name, _) -> name)
                |> Seq.sortBy fst
                |> Seq.toArray)
            |> Seq.map (fun (ns, names) ->
                let multipleNames = names |> Array.length > 1
                names |> Seq.map (fun (name, ctx) -> ns, name, ctx, multipleNames))
            |> Seq.concat
            |> Seq.map (fun (ns, name, ctx, multipleNames) -> 
                openNamespaceFix context ctx name ns multipleNames)
            |> Seq.toList
            
        let qualifiedSymbolFixes =
            candidates
            |> Seq.filter (fun (entity,_) -> not(entity.LastIdent.StartsWith "op_")) // Don't include qualified operator names. The resultant codefix won't compile because it won't be an infix operator anymore.
            |> Seq.map (fun (entity, _) -> entity.FullRelativeName, entity.Qualifier)
            |> Seq.distinct
            |> Seq.sort
            |> Seq.map (qualifySymbolFix context)
            |> Seq.toList

        for codeFix in openNamespaceFixes @ qualifiedSymbolFixes do
            context.RegisterCodeFix(codeFix, context.Diagnostics |> Seq.filter (fun x -> fixableDiagnosticIds |> List.contains x.Id) |> Seq.toImmutableArray)

    override _.FixableDiagnosticIds = Seq.toImmutableArray fixableDiagnosticIds

    override _.RegisterCodeFixesAsync context : Task =
        asyncMaybe {
            let document = context.Document

            let! sourceText = document.GetTextAsync(context.CancellationToken)
            let! parseResults, checkResults = document.GetFSharpParseAndCheckResultsAsync(nameof(FSharpAddOpenCodeFixProvider)) |> liftAsync
            let line = sourceText.Lines.GetLineFromPosition(context.Span.End)
            let linePos = sourceText.Lines.GetLinePosition(context.Span.End)
            let! defines = document.GetFSharpCompilationDefinesAsync(nameof(FSharpAddOpenCodeFixProvider)) |> liftAsync
            
            let! symbol = 
                maybe {
                    let! lexerSymbol = Tokenizer.getSymbolAtPosition(document.Id, sourceText, context.Span.End, document.FilePath, defines, SymbolLookupKind.Greedy, false, false)
                    return checkResults.GetSymbolUseAtLocation(Line.fromZ linePos.Line, lexerSymbol.Ident.idRange.EndColumn, line.ToString(), lexerSymbol.FullIsland)
                }

            do! Option.guard symbol.IsNone

            let unresolvedIdentRange =
                let startLinePos = sourceText.Lines.GetLinePosition context.Span.Start
                let startPos = Position.fromZ startLinePos.Line startLinePos.Character
                let endLinePos = sourceText.Lines.GetLinePosition context.Span.End
                let endPos = Position.fromZ endLinePos.Line endLinePos.Character
                Range.mkRange context.Document.FilePath startPos endPos
            
            let isAttribute = ParsedInput.GetEntityKind(unresolvedIdentRange.Start, parseResults.ParseTree) = Some EntityKind.Attribute
            
            let entities =
                assemblyContentProvider.GetAllEntitiesInProjectAndReferencedAssemblies checkResults
                |> List.collect (fun s -> 
                     [ yield s.TopRequireQualifiedAccessParent, s.AutoOpenParent, s.Namespace, s.CleanedIdents
                       if isAttribute then
                          let lastIdent = s.CleanedIdents.[s.CleanedIdents.Length - 1]
                          if lastIdent.EndsWith "Attribute" && s.Kind LookupType.Precise = EntityKind.Attribute then
                              yield 
                                  s.TopRequireQualifiedAccessParent, 
                                  s.AutoOpenParent,
                                  s.Namespace,
                                  s.CleanedIdents 
                                  |> Array.replace (s.CleanedIdents.Length - 1) (lastIdent.Substring(0, lastIdent.Length - 9)) ])

            let longIdent = ParsedInput.GetLongIdentAt parseResults.ParseTree unresolvedIdentRange.End

            let! maybeUnresolvedIdents =
                longIdent 
                |> Option.map (fun longIdent ->
                    longIdent
                    |> List.map (fun ident ->
                        { Ident = ident.idText
                          Resolved = not (ident.idRange = unresolvedIdentRange)})
                    |> List.toArray)
                                                    
            let insertionPoint = 
                if document.Project.IsFSharpCodeFixesAlwaysPlaceOpensAtTopLevelEnabled then OpenStatementInsertionPoint.TopLevel
                else OpenStatementInsertionPoint.Nearest

            let createEntity = ParsedInput.TryFindInsertionContext unresolvedIdentRange.StartLine parseResults.ParseTree maybeUnresolvedIdents insertionPoint
            return entities |> Seq.map createEntity |> Seq.concat |> Seq.toList |> addSuggestionsAsCodeFixes context
        } 
        |> Async.Ignore 
        |> RoslynHelpers.StartAsyncUnitAsTask(context.CancellationToken)
 