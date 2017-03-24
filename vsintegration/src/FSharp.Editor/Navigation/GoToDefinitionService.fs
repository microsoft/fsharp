﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.IO
open System.Composition
open System.Collections.Generic
open System.Collections.Immutable
open System.Linq
open System.Threading
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Editor.Host
open Microsoft.CodeAnalysis.Navigation
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.CodeAnalysis.Text

open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.VisualStudio.FSharp.Editor.Logging
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop

type internal FSharpNavigableItem(document: Document, textSpan: TextSpan) =

    interface INavigableItem with
        member this.Glyph = Glyph.BasicFile
        member this.DisplayFileLocation = true
        member this.IsImplicitlyDeclared = false
        member this.Document = document
        member this.SourceSpan = textSpan
        member this.DisplayTaggedParts = ImmutableArray<TaggedText>.Empty
        member this.ChildItems = ImmutableArray<INavigableItem>.Empty


module internal FSharpGoToDefinition =

    /// Parse and check the provided document and try to find the defition of the symbol at the position
    // this is only used in Roslyn GotoDefinition calls
    let checkAndFindDefinition
        (checker: FSharpChecker, documentKey: DocumentId, sourceText: SourceText, filePath: string, position: int,
         defines: string list, options: FSharpProjectOptions, preferSignature:bool, textVersionHash: int) = asyncMaybe {
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! lexerSymbol = CommonHelpers.getSymbolAtPosition(documentKey, sourceText, position, filePath, defines, SymbolLookupKind.Greedy)
            let! _, _, checkFileResults = 
                checker.ParseAndCheckDocument 
                    (filePath, textVersionHash, sourceText.ToString(), options, allowStaleResults = preferSignature) 

            let! declarations = 
                checkFileResults.GetDeclarationLocationAlternate 
                    (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, preferSignature)|>liftAsync

            match declarations with
            | FSharpFindDeclResult.DeclFound range -> 
                return (lexerSymbol, range,checkFileResults)
            | _ -> return! None
    }

    let findSymbolDeclarationInFile
        (targetSymbolUse:FSharpSymbolUse, implFileName:string, implSource:string, checker:FSharpChecker, projectOptions:FSharpProjectOptions, fileVersion:int) = 
        asyncMaybe {
            let! (_parseResults, checkFileAnswer) = 
                checker.ParseAndCheckFileInProject (implFileName, fileVersion,implSource,projectOptions)|> liftAsync //(implDoc, projectOptions, allowStaleResults=true, sourceText=implSourceText)
            match checkFileAnswer with 
            | FSharpCheckFileAnswer.Aborted -> return! None
            | FSharpCheckFileAnswer.Succeeded checkFileResults ->
                let! symbolUses = checkFileResults.GetUsesOfSymbolInFile targetSymbolUse.Symbol |> liftAsync
                let! implSymbol  = symbolUses |> Array.tryHead 
                return implSymbol.RangeAlternate
        }

    /// Use an origin document to provide the solution & workspace used to 
    /// find the corresponding textSpan and INavigableItem for the range
    let rangeToNavigableItem (range:range, document:Document) = async {
        let fileName = try System.IO.Path.GetFullPath range.FileName with _ -> range.FileName
        let refDocumentIds = document.Project.Solution.GetDocumentIdsWithFilePath fileName
        if not refDocumentIds.IsEmpty then 
            let refDocumentId = refDocumentIds.First()
            let refDocument = document.Project.Solution.GetDocument refDocumentId
            let! refSourceText = refDocument.GetTextAsync()
            let refTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (refSourceText, range)
            return Some (FSharpNavigableItem (refDocument, refTextSpan))
        else return None
    }

    /// helper function that used to determine the navigation strategy to apply, can be tuned towards signatures or implementation files
    let private findSymbolHelper 
        (originDocument:Document, originRange:range, sourceText:SourceText, preferSignature:bool, checker: FSharpChecker, projectInfoManager: ProjectInfoManager) =
        asyncMaybe {
            let! projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject originDocument
            let defines = CompilerEnvironment.GetCompilationDefinesForEditing (originDocument.FilePath, projectOptions.OtherOptions |> Seq.toList)

            let originTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (sourceText, originRange)
            let position = originTextSpan.Start

            let! lexerSymbol = 
                CommonHelpers.getSymbolAtPosition 
                    (originDocument.Id, sourceText, position, originDocument.FilePath, defines, SymbolLookupKind.Greedy)
            
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let lineText = (sourceText.Lines.GetLineFromPosition position).ToString()  
            
            let! _, _, checkFileResults = 
                checker.ParseAndCheckDocument (originDocument,projectOptions,allowStaleResults=true,sourceText=sourceText)
            let idRange = lexerSymbol.Ident.idRange

            let! fsSymbolUse = checkFileResults.GetSymbolUseAtLocation (fcsTextLineNumber, idRange.EndColumn, lineText, lexerSymbol.FullIsland)
            let symbol = fsSymbolUse.Symbol
            // if the tooltip was spawned in an implementation file and we have a range targeting
            // a signature file, try to find the corresponding implementation file and target the
            // desired symbol
            if isSignatureFile fsSymbolUse.FileName && preferSignature = false then 
                let fsfilePath = Path.ChangeExtension (originRange.FileName,"fs")
                if not (File.Exists fsfilePath) then return! None else
                let! implDoc = originDocument.Project.Solution.TryGetDocumentFromPath fsfilePath
                let! implSourceText = implDoc.GetTextAsync ()
                let! projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject implDoc
                let! _, _, checkFileResults = 
                    checker.ParseAndCheckDocument (implDoc, projectOptions, allowStaleResults=true, sourceText=implSourceText)
                

                let! symbolUses = checkFileResults.GetUsesOfSymbolInFile symbol |> liftAsync
                let! implSymbol  = symbolUses |> Array.tryHead 
                let implTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (implSourceText, implSymbol.RangeAlternate)
                return FSharpNavigableItem (implDoc, implTextSpan)
            else
                let! targetDocument = originDocument.Project.Solution.TryGetDocumentFromFSharpRange fsSymbolUse.RangeAlternate
                return! rangeToNavigableItem (fsSymbolUse.RangeAlternate, targetDocument)
        }  

    /// find the declaration location (signature file/.fsi) of the target symbol if possible, fall back to definition 
    let findDeclarationOfSymbolAtRange
        (targetDocument:Document, symbolRange:range, targetSource:SourceText, checker: FSharpChecker, projectInfoManager: ProjectInfoManager) =
        findSymbolHelper (targetDocument, symbolRange, targetSource,true, checker, projectInfoManager) 


    /// find the definition location (implementation file/.fs) of the target symbol
    let findDefinitionOfSymbolAtRange
        (targetDocument:Document, symbolRange:range, targetSourceText:SourceText, checker: FSharpChecker, projectInfoManager: ProjectInfoManager) =
        findSymbolHelper (targetDocument, symbolRange, targetSourceText,false, checker, projectInfoManager)


open FSharpGoToDefinition

[<Shared>]
[<ExportLanguageService(typeof<IGoToDefinitionService>, FSharpCommonConstants.FSharpLanguageName)>]
[<Export(typeof<FSharpGoToDefinitionService>)>]
type internal FSharpGoToDefinitionService [<ImportingConstructor>]
    (checkerProvider: FSharpCheckerProvider,
     projectInfoManager: ProjectInfoManager,
     [<ImportMany>] presenters: IEnumerable<INavigableItemsPresenter>) =

    let serviceProvider =  ServiceProvider.GlobalProvider  
    let statusBar = serviceProvider.GetService<SVsStatusbar,IVsStatusbar>()

    let tryNavigateToItem (navigableItem:#INavigableItem option) =
        match navigableItem with
        | Some navigableItem ->
            let workspace = navigableItem.Document.Project.Solution.Workspace
            let navigationService = workspace.Services.GetService<IDocumentNavigationService>()
            // prefer open documents in the preview tab
            let options = workspace.Options.WithChangedOption (NavigationOptions.PreferProvisionalTab, true)
            navigationService.TryNavigateToSpan (workspace, navigableItem.Document.Id, navigableItem.SourceSpan, options)
        | None ->
            statusBar.SetText "Could Not Navigate to Definition of Symbol Under Caret" |> ignore
            true


    /// Navigate to the positon of the textSpan in the provided document
    member this.TryNavigateToTextSpan (document:Document, textSpan:TextSpan) =
        let navigableItem = FSharpNavigableItem (document, textSpan) :> INavigableItem
        let workspace = document.Project.Solution.Workspace
        let navigationService = workspace.Services.GetService<IDocumentNavigationService>()
        let options = workspace.Options.WithChangedOption (NavigationOptions.PreferProvisionalTab, true)
        let result = navigationService.TryNavigateToSpan (workspace, navigableItem.Document.Id, navigableItem.SourceSpan, options)
        if result then true else
        statusBar.SetText "Could Not Navigate to Definition of Symbol Under Caret" |> ignore
        false


    /// find the declaration location (signature file/.fsi) of the target symbol if possible, fall back to definition 
    member this.NavigateToSymbolDeclarationAsync (targetDocument:Document, targetSourceText:SourceText, symbolRange:range) = async {
        statusBar.SetText "Trying to locate symbol..." |> ignore
        //let! navigableItem = 
        let! navresult = 
            FSharpGoToDefinition.findDeclarationOfSymbolAtRange 
                (targetDocument, symbolRange, targetSourceText, checkerProvider.Checker, projectInfoManager)
        return tryNavigateToItem navresult
    }
    

     /// find the definition location (implementation file/.fs) of the target symbol
    member this.NavigateToSymbolDefinitionAsync (targetDocument:Document, targetSourceText:SourceText, symbolRange:range)= async{
        statusBar.SetText "Trying to locate symbol..." |> ignore
        let! navresult = 
            FSharpGoToDefinition.findDefinitionOfSymbolAtRange 
                (targetDocument, symbolRange, targetSourceText, checkerProvider.Checker, projectInfoManager) 
        return tryNavigateToItem navresult
    }


    static member FindDefinition
        (checker: FSharpChecker, documentKey: DocumentId, sourceText: SourceText, filePath: string, position: int,
         defines: string list, options: FSharpProjectOptions, textVersionHash: int) : Option<range> = maybe {
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line
            let! lexerSymbol = CommonHelpers.getSymbolAtPosition(documentKey, sourceText, position, filePath, defines, SymbolLookupKind.Greedy)
            let! _, _, checkFileResults = 
                checker.ParseAndCheckDocument 
                    (filePath, textVersionHash, sourceText.ToString(), options, allowStaleResults = true)  |> Async.RunSynchronously

            let declarations = 
                checkFileResults.GetDeclarationLocationAlternate 
                    (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, false) |> Async.RunSynchronously
            
            match declarations with
            | FSharpFindDeclResult.DeclFound range -> return range
            | _ -> return! None
    }


    /// Construct a task that will return a navigation target for the implementation definition of the symbol 
    /// at the provided position in the document
    member this.FindDefinitionsTask (originDocument:Document, position:int, cancellationToken:CancellationToken) =
        asyncMaybe {
            try let results = List<INavigableItem>()
                let! projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject originDocument
                let! sourceText = originDocument.GetTextAsync () |> liftTaskAsync
                let defines = CompilerEnvironment.GetCompilationDefinesForEditing (originDocument.FilePath, projectOptions.OtherOptions |> Seq.toList)
                let textLine = sourceText.Lines.GetLineFromPosition position
                let textLinePos = sourceText.Lines.GetLinePosition position
                let fcsTextLineNumber = Line.fromZ textLinePos.Line
                let lineText = (sourceText.Lines.GetLineFromPosition position).ToString()  

                let preferSignature = isSignatureFile originDocument.FilePath

                let! _, _, checkFileResults = 
                    checkerProvider.Checker.ParseAndCheckDocument (originDocument, projectOptions, allowStaleResults=true, sourceText=sourceText)
                
                let! lexerSymbol = 
                    CommonHelpers.getSymbolAtPosition
                        (originDocument.Id, sourceText, position,originDocument.FilePath, defines, SymbolLookupKind.Greedy)
                let idRange = lexerSymbol.Ident.idRange

                let! declarations = 
                    checkFileResults.GetDeclarationLocationAlternate 
                        (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, preferSignature)
                        |> liftAsync
                let! targetSymbolUse = 
                    checkFileResults.GetSymbolUseAtLocation (fcsTextLineNumber, idRange.EndColumn, lineText, lexerSymbol.FullIsland)

                match declarations with
                | FSharpFindDeclResult.DeclFound targetRange -> 
                    // if goto definition is called at we are alread at the declaration location of a symbol in
                    // either a signature or an implementation file then we jump to it's respective postion in thethe
                    if lexerSymbol.Range = targetRange then
                        // jump from signature to corresponding implementation
                        if isSignatureFile originDocument.FilePath then
                            Logging.logInfo "jump from signture to implementation"
                            let implFilePath = Path.ChangeExtension (originDocument.FilePath,"fs")
                            if not (File.Exists implFilePath) then return! None else
                            let! implDocument = originDocument.Project.Solution.TryGetDocumentFromPath implFilePath
                            let! implSourceText = implDocument.GetTextAsync () |> liftTaskAsync
                            let! implVersion = implDocument.GetTextVersionAsync () |> liftTaskAsync
                            let! targetRange = 
                                findSymbolDeclarationInFile 
                                    (targetSymbolUse, implFilePath, implSourceText.ToString(), checkerProvider.Checker, projectOptions, implVersion.GetHashCode())

                            let implTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (implSourceText, targetRange)
                            let navItem = FSharpNavigableItem (implDocument, implTextSpan)
                            results.Add navItem
                            return results.AsEnumerable()
                        else // jump from implementation to corresponding signature
                            let! declarations = 
                                checkFileResults.GetDeclarationLocationAlternate 
                                    (fcsTextLineNumber, lexerSymbol.Ident.idRange.EndColumn, textLine.ToString(), lexerSymbol.FullIsland, true) 
                                    |> liftAsync
                            match declarations with
                            | FSharpFindDeclResult.DeclFound targetRange -> 
                                logInfof "target range -\n %A" targetRange
                                let! sigDocument = originDocument.Project.Solution.TryGetDocumentFromPath targetRange.FileName
                                let! sigSourceText = sigDocument.GetTextAsync () |> liftTaskAsync
                                let sigTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (sigSourceText, targetRange)
                                let navItem = FSharpNavigableItem (sigDocument, sigTextSpan)
                                results.Add navItem
                                return results.AsEnumerable()
                            | _ -> return! None
                    else
                        let! sigDocument = originDocument.Project.Solution.TryGetDocumentFromPath targetRange.FileName
                        let! sigSourceText = sigDocument.GetTextAsync () |> liftTaskAsync
                        let sigTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (sigSourceText, targetRange)
                        // if gotodef call originated in a signature and the returned target is a signature, navigate to there
                        if isSignatureFile targetRange.FileName && preferSignature then 
                            let navItem = FSharpNavigableItem (sigDocument, sigTextSpan)
                            results.Add navItem
                            return results.AsEnumerable()
                        else // we need to get an FSharpSymbol from the targetRange found in the signature
                                // that symbol will be used to find the destination in the corresponding implementation file
                            let implFilePath = Path.ChangeExtension (sigDocument.FilePath,"fs")
                            let! implDocument = originDocument.Project.Solution.TryGetDocumentFromPath implFilePath
                            let! implVersion = implDocument.GetTextVersionAsync () |> liftTaskAsync
                            let! implSourceText = implDocument.GetTextAsync () |> liftTaskAsync
                            let! projectOptions = projectInfoManager.TryGetOptionsForEditingDocumentOrProject implDocument
                            let! targetRange = 
                                findSymbolDeclarationInFile 
                                    (targetSymbolUse, implFilePath, implSourceText.ToString(), checkerProvider.Checker, projectOptions, implVersion.GetHashCode())
                                    |> Async.RunSynchronously
                            let implTextSpan = CommonRoslynHelpers.FSharpRangeToTextSpan (implSourceText, targetRange)
                            let navItem = FSharpNavigableItem (implDocument, implTextSpan)
                            results.Add navItem
                            return results.AsEnumerable()
                    | _ -> return! None
            with e ->
                debug "\n%s\n%s\n%s\n" e.Message (e.TargetSite.ToString()) e.StackTrace
                return Seq.empty
        }   |> Async.map (Option.defaultValue Seq.empty)
            |> CommonRoslynHelpers.StartAsyncAsTask cancellationToken        

   
    interface IGoToDefinitionService with
        
        member this.FindDefinitionsAsync(document: Document, position: int, cancellationToken: CancellationToken) =
            this.FindDefinitionsTask(document, position, cancellationToken)
        
        /// Try to navigate to the definiton of the symbol at the symbolRange in the originDocument
        member this.TryGoToDefinition(document: Document, position: int, cancellationToken: CancellationToken) =
            let definitionTask = this.FindDefinitionsTask(document, position, cancellationToken)
            
            definitionTask.RunSynchronously()

            // REVIEW: document this use of a blocking wait on the cancellation token, explaining why it is ok
            if definitionTask.Status = TaskStatus.RanToCompletion && definitionTask.Result.Any() then
                let navigableItem = definitionTask.Result.First() // F# API provides only one INavigableItem
                let workspace = document.Project.Solution.Workspace
                let navigationService = workspace.Services.GetService<IDocumentNavigationService>()
                ignore presenters
                // prefer open documents in the preview tab
                let options = workspace.Options.WithChangedOption (NavigationOptions.PreferProvisionalTab, true)
                navigationService.TryNavigateToSpan (workspace, navigableItem.Document.Id, navigableItem.SourceSpan, options)

                // FSROSLYNTODO: potentially display multiple results here
                // If GotoDef returns one result then it should try to jump to a discovered location. If it returns multiple results then it should use 
                // presenters to render items so user can choose whatever he needs. Given that per comment F# API always returns only one item then we 
                // should always navigate to definition and get rid of presenters.
                //
                //let refDisplayString = refSourceText.GetSubText(refTextSpan).ToString()
                //for presenter in presenters do
                //    presenter.DisplayResult(navigableItem.DisplayString, definitionTask.Result)
                //true

            else 
                statusBar.SetText "Could Not Navigate to Definition of Symbol Under Caret" |> ignore
                true
