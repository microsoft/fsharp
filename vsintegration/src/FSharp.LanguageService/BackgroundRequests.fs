// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//------- DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS ---------------

//------- DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS ---------------

namespace Microsoft.VisualStudio.FSharp.LanguageService

open System
open Microsoft.VisualStudio.TextManager.Interop 
open Microsoft.VisualStudio.Text
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open Microsoft.VisualStudio.FSharp.LanguageService.SiteProvider
open Microsoft.VisualStudio.FSharp.Interactive.Session

#nowarn "44" // use of obsolete CheckFileInProjectAllowingStaleCachedResults

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal FSharpBackgroundRequestExtraData_DEPRECATED =
    { ProjectSite : IProjectSite
      CheckOptions : FSharpProjectOptions
      ProjectFileName : string
      FSharpChecker : FSharpChecker
      Colorizer : Lazy<FSharpColorizer_DEPRECATED> }

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal FSharpBackgroundRequest_DEPRECATED
           (line, col, info, sourceText, snapshot : ITextSnapshot, 
            methodTipMiscellany : MethodTipMiscellany_DEPRECATED, fileName, reason, view, sink, 
            source:ISource, timestamp:int, synchronous:bool,
            extraData : Lazy<FSharpBackgroundRequestExtraData_DEPRECATED> option) = 

    inherit BackgroundRequest_DEPRECATED(line, col, info, sourceText, snapshot, methodTipMiscellany, fileName, reason, view, sink, source, timestamp, synchronous)

    member this.ExtraData = extraData

    member this.TryGetColorizer() = 
        match extraData with 
        | None -> None 
        | Some data -> Some (data.Force().Colorizer.Force())

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal FSharpLanguageServiceBackgroundRequests_DEPRECATED
                (getColorizer: IVsTextView -> FSharpColorizer_DEPRECATED, 
                 getInteractiveChecker: unit -> FSharpChecker, 
                 getProjectSitesAndFiles : unit -> ProjectSitesAndFiles,
                 getServiceProvider: unit -> System.IServiceProvider,
                 getDocumentationBuilder: unit -> IDocumentationBuilder_DEPRECATED) =    

    let mutable parseFileResults : FSharpParseFileResults option = None
    let mutable lastParseFileRequest : BackgroundRequest_DEPRECATED = null

    let outOfDateProjectFileNames = new System.Collections.Generic.HashSet<string>()

    member this.ParseFileResults with get() = parseFileResults and set v = parseFileResults <- v
    member this.AddOutOfDateProjectFileName nm =
        outOfDateProjectFileNames.Add(nm) |> ignore

    // This method is executed on the UI thread
    member this.CreateBackgroundRequest(line: int, col: int, info: TokenInfo, sourceText: string, snapshot: ITextSnapshot, methodTipMiscellany: MethodTipMiscellany_DEPRECATED, 
                                         fileName: string, reason: BackgroundRequestReason, view: IVsTextView,
                                         sink: AuthoringSink, source: ISource, timestamp: int, synchronous: bool) =
        let extraData =
            match sourceText with
            |   null -> 
                // sourceText being null indicates that the cached results for this request will be used, so 
                // ExecuteBackgroundRequest will not be called.                    
                None 
            |   _ ->       
                // For scripts, GetProjectOptionsFromScript involves parsing and sync op, so is run on the language service thread later
                // For projects, we need to access RDT on UI thread, so do it on the GUI thread now
                if CompilerEnvironment.MustBeSingleFileProject(fileName) then
                    let data = 
                        lazy // This portion is executed on the language service thread
                            let timestamp = if source=null then System.DateTime(2000,1,1) else source.OpenedTime // source is null in unit tests
                            let checker = getInteractiveChecker()
                            let checkOptions, _diagnostics = checker.GetProjectOptionsFromScript(fileName,  FSharp.Compiler.Text.SourceText.ofString sourceText, SessionsProperties.fsiPreview, timestamp, [| |]) |> Async.RunImmediate
                            let referencedProjectFileNames = [| |]
                            let projectSite = ProjectSitesAndFiles.CreateProjectSiteForScript(fileName, referencedProjectFileNames, checkOptions)
                            { ProjectSite = projectSite
                              CheckOptions = checkOptions 
                              ProjectFileName = projectSite.ProjectFileName
                              FSharpChecker = checker
                              Colorizer = lazy getColorizer(view) } 
                    Some data
                else 
                    // This portion is executed on the UI thread.
                    let rdt = getServiceProvider().RunningDocumentTable
                    let projectSite = getProjectSitesAndFiles().FindOwningProject_DEPRECATED(rdt,fileName)
                    let enableInMemoryCrossProjectReferences = true
                    let _, checkOptions = ProjectSitesAndFiles.GetProjectOptionsForProjectSite(enableInMemoryCrossProjectReferences, (fun _ -> None), projectSite, getServiceProvider(), fileName, false)
                    let projectFileName = projectSite.ProjectFileName
                    let data = 
                        {   ProjectSite = projectSite
                            CheckOptions = checkOptions 
                            ProjectFileName = projectFileName 
                            FSharpChecker = getInteractiveChecker()
                            Colorizer = lazy getColorizer(view) } 
                    Some (Lazy<_>.CreateFromValue data)

        new FSharpBackgroundRequest_DEPRECATED(line, col, info, sourceText, snapshot, methodTipMiscellany, fileName, reason, view, sink, source, timestamp, synchronous, extraData)

    member this.ExecuteBackgroundRequest(req:FSharpBackgroundRequest_DEPRECATED, source:IFSharpSource_DEPRECATED) = 
        try
            let data =
                match req.ExtraData with
                |   Some lazyData -> lazyData.Force()
                |   None -> failwith "ExecuteFSharpBackgroundRequest called for supposedly cached request"

            let projectSite = data.ProjectSite
            let checkOptions = data.CheckOptions
            let projectFileName = data.ProjectFileName
            let interactiveChecker = data.FSharpChecker
            let colorizer = data.Colorizer 
            source.ProjectSite <- Some projectSite
            
            // Do brace matching if required
            if req.ResultSink.BraceMatching then  
                // Record brace-matching
                let braceMatches = interactiveChecker.MatchBraces(req.FileName,req.Text,checkOptions) |> Async.RunImmediate
                    
                let mutable pri = 0
                for (b1,b2) in braceMatches do
                    req.ResultSink.MatchPair(TextSpanOfRange b1, TextSpanOfRange b2, pri)
                    pri<-pri+1
                          
            match req.Reason with 
            | BackgroundRequestReason.MatchBraces -> () // work has already been done above
            | BackgroundRequestReason.ParseFile ->

                // invoke ParseFile directly - relying on cache inside the interactiveChecker
                let parseResults = interactiveChecker.ParseFileInProject(req.FileName, req.Text, checkOptions) |> Async.RunImmediate

                parseFileResults <- Some parseResults

            | _ -> 
                let syncParseInfoOpt = 
                    if FSharpIntellisenseInfo_DEPRECATED.IsReasonRequiringSyncParse(req.Reason) then
                        let parseResults = interactiveChecker.ParseFileInProject(req.FileName,req.Text,checkOptions) |> Async.RunImmediate
                        Some parseResults
                    else None

                // Try to grab recent results, unless BackgroundRequestReason = Check
                // This may fail if the CompilerServices API decides that
                // it would like a chance to really check the contents of the file again,
                let parseResults,typedResults,containsFreshFullTypeCheck,aborted,resultTimestamp = 
                    let possibleShortcutResults = 
                        if (req.Reason = BackgroundRequestReason.FullTypeCheck) || req.RequireFreshResults = RequireFreshResults.Yes then
                            // Getting here means we're in second chance intellisense. For example, the user has pressed dot 
                            // we tried stale results and got nothing. Now we need real results even if we have to wait.
                            None
                        else                            
                            // This line represents a critical decision in the LS. If we're _not_
                            // doing a full typecheck, and some stale typecheck results are available, then
                            // use the stale results. This means, for example, that completion is fast,
                            // but less accurate (since we can't possibly afford to typecheck while generating a completion)
                            interactiveChecker.TryGetRecentCheckResultsForFile(req.FileName,checkOptions)
                    
                    match possibleShortcutResults with 
                    | Some (parseResults,typedResults,fileVersion) -> 
                        defaultArg syncParseInfoOpt parseResults,Some typedResults, false, false, fileVersion // Note: untypedparse and typed results have different timestamps/snapshots, typed may be staler
                    | None -> 
                        // Perform a fresh two-phase parse of the source file
                        let parseResults = 
                            match syncParseInfoOpt with 
                            | Some x -> x
                            | None -> interactiveChecker.ParseFileInProject(req.FileName,req.Text,checkOptions) |> Async.RunImmediate
                        
                        // Should never matter but don't let anything in FSharp.Compiler extend the lifetime of 'source'
                        let sr = ref (Some source)

                        // Type-checking
                        let typedResults,aborted = 
                            match interactiveChecker.CheckFileInProject(parseResults,req.FileName,req.Timestamp,FSharp.Compiler.Text.SourceText.ofString(req.Text),checkOptions) |> Async.RunImmediate with 
                            | FSharpCheckFileAnswer.Aborted -> 
                                // isResultObsolete returned true during the type check.
                                None,true
                            | FSharpCheckFileAnswer.Succeeded results -> Some results, false

                        sr := None
                        parseResults,typedResults,true,aborted,int64 req.Timestamp
                
                // Now that we have the parseResults, we can SetDependencyFiles().
                // 
                // If the set of dependencies changes, the file needs to be re-checked
                let dependencyFiles = 
                    match typedResults with 
                    | None -> parseResults.DependencyFiles
                    | Some r -> r.DependencyFiles
                
                let anyDependenciesChanged = source.SetDependencyFiles(dependencyFiles)
                if anyDependenciesChanged then
                    req.ResultClearsDirtinessOfFile <- false
                    // Furthermore, if the project is out-of-date behave just as if we were notified dependency files changed.  
                    if outOfDateProjectFileNames.Contains(projectFileName) then
                        interactiveChecker.InvalidateConfiguration(checkOptions)
                        interactiveChecker.ParseAndCheckProject(checkOptions)
                        |> Async.RunImmediate
                        |> ignore
                        outOfDateProjectFileNames.Remove(projectFileName) |> ignore

                else
                    parseFileResults <- Some parseResults
                    
                    match typedResults with 
                    | None -> 
                        // OK, the typed results were not available because the background state to typecheck the file is not yet
                        // ready.  However, we will be notified when it _is_ ready, courtesy of the background builder. Hence
                        // we can clear the dirty bit and wait for that notification.
                        req.ResultClearsDirtinessOfFile <- not aborted
                        req.IsAborted <- aborted
                        // On 'FullTypeCheck', send a message to the reactor to start the background compile for this project, just in case
                        if req.Reason = BackgroundRequestReason.FullTypeCheck then    
                            interactiveChecker.ParseAndCheckProject(checkOptions)
                            |> Async.RunImmediate
                            |> ignore

                    | Some typedResults -> 
                        // Post the parse errors. 
                        if containsFreshFullTypeCheck then 
                            for error in typedResults.Diagnostics do
                                let span = new TextSpan(iStartLine=error.StartLine-1,iStartIndex=error.StartColumn,iEndLine=error.EndLine-1,iEndIndex=error.EndColumn)                             
                                let sev = 
                                    match error.Severity with 
                                    | FSharpDiagnosticSeverity.Hidden -> Microsoft.VisualStudio.FSharp.LanguageService.Severity.Hint
                                    | FSharpDiagnosticSeverity.Info -> Microsoft.VisualStudio.FSharp.LanguageService.Severity.Hint
                                    | FSharpDiagnosticSeverity.Warning -> Microsoft.VisualStudio.FSharp.LanguageService.Severity.Warning
                                    | FSharpDiagnosticSeverity.Error -> Microsoft.VisualStudio.FSharp.LanguageService.Severity.Error
                                req.ResultSink.AddError(req.FileName, error.Subcategory, error.Message, span, sev)
                          

                        let provideMethodList = (req.Reason = BackgroundRequestReason.MethodTip || req.Reason = BackgroundRequestReason.MatchBracesAndMethodTip)

                        let scope = new FSharpIntellisenseInfo_DEPRECATED(parseResults, req.Line, req.Col, req.Snapshot, typedResults, projectSite, req.View, colorizer, getDocumentationBuilder(), provideMethodList) 

                        req.ResultIntellisenseInfo <- scope
                        req.ResultTimestamp <- int resultTimestamp  // This will be different from req.Timestamp when we're using stale results.
                        req.ResultClearsDirtinessOfFile <- containsFreshFullTypeCheck


                        // On 'FullTypeCheck', send a message to the reactor to start the background compile for this project, just in case
                        if req.Reason = BackgroundRequestReason.FullTypeCheck then    
                            interactiveChecker.ParseAndCheckProject(checkOptions)
                            |> Async.RunImmediate
                            |> ignore
                            
                        // On 'QuickInfo', get the text for the quick info while we're off the UI thread, instead of doing it later
                        if req.Reason = BackgroundRequestReason.QuickInfo then 
                            let text,span = scope.GetDataTipText(req.Line, req.Col)
                            req.ResultQuickInfoText <- text
                            req.ResultQuickInfoSpan <- span 

        with e ->
            req.IsAborted <- true
            Assert.Exception(e)
            reraise()                

    member fls.TriggerParseFile(view: IVsTextView, source: ISource) = 
        source.BeginBackgroundRequest(0, 0, new TokenInfo(), BackgroundRequestReason.ParseFile, view, RequireFreshResults.No, new BackgroundRequestResultHandler(source.HandleUntypedParseOrFullTypeCheckResponse))

    // Called before a Goto Definition to wait a moment to synchonize the parse
    member fls.TrySynchronizeParseFileInformation(view: IVsTextView, source: ISource, millisecondsTimeout:int) =

        if isNull lastParseFileRequest || lastParseFileRequest.Timestamp <> source.ChangeCount then
            let req = fls.TriggerParseFile(view, source)
                    
            if req <> null && (req.IsSynchronous || req.Result <> null) then
                // This blocks the UI thread. Give it a slice of time (1000ms) and then just give up on this particular synchronization.
                // If we end up aborting here then the caller has the option of just using the old untyped parse information 
                // for the active view if one is available. Sooner or later the request may complete and the new untyped parse information
                // will become available.
                lastParseFileRequest <- req
                req.Result.TryWaitForBackgroundRequestCompletion(millisecondsTimeout) 
            else
                false
        else
            // OK, the last request is still active, so try to wait again
            lastParseFileRequest.Result.TryWaitForBackgroundRequestCompletion(millisecondsTimeout) 

    member _.OnActiveViewChanged(_textView: IVsTextView) =
        parseFileResults <- None
        lastParseFileRequest <- null // abandon any request for untyped parse information, without cancellation

    // Check if we can shortcut executing the background request and just fill in the latest
    // cached scope for the active view from this.service.RecentFullTypeCheckResults.
    //
    // THIS MUST ONLY RETURN TRUE IF ---> ExecuteBackgroundRequest is equivalent to fetching a recent,
    // perhaps out-of-date scope.
    member _.IsRecentScopeSufficientForBackgroundRequest(reason:BackgroundRequestReason) = 
    
        match reason with 
        | BackgroundRequestReason.MatchBraces 
        | BackgroundRequestReason.MatchBracesAndMethodTip
        | BackgroundRequestReason.ParseFile 
        | BackgroundRequestReason.FullTypeCheck -> false
            
        // For QuickInfo, we grab the result while we're on the background thread,
        // so returning the scope alone is not sufficient
        | BackgroundRequestReason.QuickInfo -> false
        // For MethodTip, we need a fresh parse to get accurate position info for arguments
        | BackgroundRequestReason.MethodTip -> false
        // For all others, the request is identical to using the latest cached scope
        | BackgroundRequestReason.MemberSelect 
        | BackgroundRequestReason.MemberSelectAndHighlightBraces 
        | BackgroundRequestReason.CompleteWord 
        | BackgroundRequestReason.DisplayMemberList
        | BackgroundRequestReason.Goto
        | _ -> true


    // This is called on the UI thread after fresh full typecheck results are available
    member this.OnParseFileOrCheckFileComplete(req:BackgroundRequest_DEPRECATED) =
        match req.Source, req.ResultIntellisenseInfo, req.View with 
        | (:? IFSharpSource_DEPRECATED as source), (:? FSharpIntellisenseInfo_DEPRECATED as scope), textView when textView <> null && not req.Source.IsClosed -> 

             scope.OnParseFileOrCheckFileComplete(source)
             
        | _ -> ()