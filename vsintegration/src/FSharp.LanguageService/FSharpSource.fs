// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//------- DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS ---------------

#nowarn "40"

namespace Microsoft.VisualStudio.FSharp.LanguageService

open System
open System.Text
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open System.Runtime.InteropServices
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop 
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Formatting
open Microsoft.VisualStudio.TextManager.Interop 
open Microsoft.VisualStudio.OLE.Interop
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

#nowarn "45" // This method will be made public in the underlying IL because it may implement an interface or override a method

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal IDependencyFileChangeNotify_DEPRECATED = 

     abstract DependencyFileCreated : IProjectSite -> unit

     abstract DependencyFileChanged : string -> unit

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal IFSharpSource_DEPRECATED =
    /// Request colorization of the whole source file
    abstract RecolorizeWholeFile : unit -> unit
    abstract RecolorizeLine : line:int -> unit
    // Called to notify the source that the user has changed the source text in the editor.
    abstract RecordChangeToView: unit -> unit
    // Called to notify the source the file has been redisplayed.
    abstract RecordViewRefreshed: unit -> unit
    // If true, the file displayed has changed and needs to be redisplayed to some extent.
    abstract NeedsVisualRefresh : bool with get
    /// Number of most recent change to this file.
    abstract ChangeCount : int with get,set
    /// Timestamp of the last change
    abstract DirtyTime : int with get,set
    /// Whether or not this source is closed.
    abstract IsClosed: unit -> bool with get
    /// Store a ProjectSite for obtaining a task provider
    abstract ProjectSite : IProjectSite option with get,set
    /// Specify the files that should trigger a rebuild for the project behind this source
    abstract SetDependencyFiles : string[] -> bool
    
    

//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal FSharpSourceTestable_DEPRECATED
                    (recolorizeWholeFile:unit->unit, 
                     recolorizeLine:int->unit, 
                     currentFileName:unit -> string, 
                     isClosed:unit->bool, 
                     vsFileWatch:IVsFileChangeEx, 
                     depFileChange: IDependencyFileChangeNotify_DEPRECATED) =         
            
        let mutable projectSite : IProjectSite option = None

        let mutable isDisposed = false
        let lastDependencies = new Dictionary<string,uint32>()  // filename, cookie
        let fileChangeFlags = 
            uint32 (_VSFILECHANGEFLAGS.VSFILECHG_Add ||| 
                    // _VSFILECHANGEFLAGS.VSFILECHG_Del ||| // don't listen for deletes - if a file (such as a 'Clean'ed project reference) is deleted, just keep using stale info
                    _VSFILECHANGEFLAGS.VSFILECHG_Time)
        
        let mutable needsVisualRefresh = true
        let mutable changeCount = 0
        let mutable dirtyTime = 0
        
        let IncrementWithWrap(v:int) =
            if v = Int32.MaxValue then 0 else v + 1    
        
        interface IFSharpSource_DEPRECATED with 
            member source.RecolorizeWholeFile() = recolorizeWholeFile() 
            member source.RecolorizeLine line = recolorizeLine line
            
            member source.ChangeCount
                with get() = changeCount
                and set(value) = changeCount <- value

            member source.DirtyTime
                with get() = dirtyTime
                and set(value) = dirtyTime <- value   

            member source.RecordChangeToView() = 
                needsVisualRefresh <- true
                dirtyTime <- System.Environment.TickCount; // NOTE: If called fast enough, it is possible for dirtyTime to have the same value as it had before.
                changeCount <- IncrementWithWrap(changeCount)

            member source.RecordViewRefreshed() = 
                needsVisualRefresh <- false

            member source.NeedsVisualRefresh = needsVisualRefresh

            member source.IsClosed = isClosed()

            member source.ProjectSite
                with get() = projectSite // REVIEW: Could get this from IVsTextBuffer->IVsHierarchy->IProjectSite
                and set(value) = projectSite <- value

            /// returns true if the set of dependency files we're watching changes, false otherwise (except that it always returns false on the first call to this method on this object)
            member source.SetDependencyFiles(files) = 
                let changeEvents = source :> IVsFileChangeEvents
                // Note that adding a new dependency for a file and then removing the old one (for that same file) risks missing notifications, 
                // As a result, we compute the diffs, and only act on the diff.
                
                // figure out dependencies that are no longer needed
                let mutable cookiesToRemove = []
                let mutable filesToRemove = []
                let newFilesSet = new System.Collections.Generic.HashSet<string>(files)  // may do lots of .Contains() calls, so do this to avoid O(N^2)
                for key in lastDependencies.Keys do
                    if not( newFilesSet.Contains(key) ) then
                        filesToRemove <- key :: filesToRemove
                        cookiesToRemove <- lastDependencies.[key] :: cookiesToRemove
                // remove from local dictionary
                for key in filesToRemove do
                    lastDependencies.Remove(key) |> ignore
                    
                // figure out new dependencies that must be added
                let mutable filesToAdd = []
                for file in files do
                    if not( lastDependencies.ContainsKey(file) ) then
                        filesToAdd <- file :: filesToAdd

                match filesToAdd, cookiesToRemove with
                | [], [] -> false // nothing changed
                | filesToAdd, cookiesToRemove ->
                    // talk to IVsFileChangeEx to update set of notifications we want
                    match vsFileWatch with
                    |   null -> false
                    |   _ ->
                            UIThread.RunSync(fun () ->
                                if not isDisposed then
                                    for file in filesToAdd do
                                        if not (lastDependencies.ContainsKey file) then 
                                            try
                                                let cookie = Com.ThrowOnFailure1(vsFileWatch.AdviseFileChange(file, fileChangeFlags, changeEvents))
                                                lastDependencies.Add(file, cookie)
                                            with _ -> ()  // can throw, e.g. when file/directory to watch does not exist; this is safe to ignore
                                    for cookie in cookiesToRemove do
                                        Com.ThrowOnFailure0(vsFileWatch.UnadviseFileChange(cookie))
                            )
                            true
                        
        // Hook file change events in dependency files.
        interface IVsFileChangeEvents with 
            member changes.FilesChanged(_count : uint32, _files: string [], changeFlags : uint32 []) = 
                let changeFlags = changeFlags |> Array.map int |> Array.map enum<_VSFILECHANGEFLAGS>
        
                match projectSite with
                | Some projectSite -> 
                    depFileChange.DependencyFileChanged(currentFileName())
                    if changeFlags |> Array.exists (fun cf -> cf &&& (_VSFILECHANGEFLAGS.VSFILECHG_Add ||| _VSFILECHANGEFLAGS.VSFILECHG_Del) <> enum 0) then
                        depFileChange.DependencyFileCreated(projectSite)                               
                | None -> ()
                0

            member changes.DirectoryChanged(_directory: string) = 0
            
        interface IDisposable with
            member disp.Dispose() =         
                UIThread.MustBeCalledFromUIThread()
                isDisposed <- true
                match vsFileWatch with
                |   null -> ()
                |   _ ->
                    for KeyValue(_,cookie) in lastDependencies do
                        vsFileWatch.UnadviseFileChange(cookie) |> ignore
                lastDependencies.Clear()


//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
[<AllowNullLiteralAttribute>]
type internal VSFontsAndColorsHelper private(fontFamily, pointSize, excludedCodeForegroundColorBrush, backgroundBrush) =
        static let Compute(site:System.IServiceProvider) =
            UIThread.MustBeCalledFromUIThread()
            let mutable guidStatementCompletionFC = new Guid("C1614BB1-734F-4a31-BD42-5AE6275E16D2") // GUID_StatementCompletionFC
            let vsFontAndColorStorage = site.GetService(typeof<SVsFontAndColorStorage>) :?> IVsFontAndColorStorage
            let mutable guidTextEditorFontCategory = new Guid("A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0") // Guid for the code editor font and color category. GUID_TextEditorFC
            vsFontAndColorStorage.OpenCategory(&guidTextEditorFontCategory, (uint32 __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS) ||| (uint32 __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS) ||| (uint32 __FCSTORAGEFLAGS.FCSF_READONLY)) |> ignore
            let itemInfo : ColorableItemInfo[] = Array.zeroCreate 1  
            vsFontAndColorStorage.GetItem("Excluded Code", itemInfo) |> ignore
            let fgColorInfo = itemInfo.[0].crForeground 
            let winFormColor = System.Drawing.ColorTranslator.FromOle(int fgColorInfo)
            let color = System.Windows.Media.Color.FromArgb(winFormColor.A, winFormColor.R, winFormColor.G, winFormColor.B)
            let excludedCodeForegroundColorBrush = new System.Windows.Media.SolidColorBrush(color)
            vsFontAndColorStorage.GetItem("Plain Text", itemInfo) |> ignore
            let bgColorInfo = itemInfo.[0].crBackground
            let winFormColor = System.Drawing.ColorTranslator.FromOle(int bgColorInfo)
            let color = System.Windows.Media.Color.FromArgb(winFormColor.A, winFormColor.R, winFormColor.G, winFormColor.B)
            let backgroundBrush = new System.Windows.Media.SolidColorBrush(color)
            vsFontAndColorStorage.CloseCategory() |> ignore
            vsFontAndColorStorage.OpenCategory(&guidStatementCompletionFC, (uint32 __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS) ||| (uint32 __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS) ||| (uint32 __FCSTORAGEFLAGS.FCSF_READONLY)) |> ignore
            let fontInfo : FontInfo[] = Array.zeroCreate 1
            vsFontAndColorStorage.GetFont(null, fontInfo) |> ignore
            let fontFamily = fontInfo.[0].bstrFaceName 
            let pointSize = fontInfo.[0].wPointSize 
            vsFontAndColorStorage.CloseCategory() |> ignore
            fontFamily, pointSize, excludedCodeForegroundColorBrush, backgroundBrush

        static let mutable theInstance = null  // only ever updated on UI thread
        static let WM_SYSCOLORCHANGE : uint32 = 0x0015u
        static let WM_THEMECHANGED : uint32 = 0x031Au

        member private this.Contents = fontFamily, pointSize, excludedCodeForegroundColorBrush, backgroundBrush

        static member GetContents(site:System.IServiceProvider) =
            if theInstance=null then
                theInstance <- new VSFontsAndColorsHelper(Compute(site))
                let vsShell = site.GetService(typeof<SVsShell>) :?> IVsShell
                let mutable k = 0u
                vsShell.AdviseBroadcastMessages({new IVsBroadcastMessageEvents with
                                                    member this.OnBroadcastMessage(msg, _wParam, _lParam) =
                                                        if msg = WM_THEMECHANGED || msg = WM_SYSCOLORCHANGE then
                                                            theInstance <- new VSFontsAndColorsHelper(Compute(site))
                                                        NativeMethods.S_OK
                                                 }, &k) |> ignore
            theInstance.Contents
           
//
// Note: DEPRECATED CODE ONLY ACTIVE IN UNIT TESTING VIA "UNROSLYNIZED" UNIT TESTS. 
//
// Note: Tests using this code should either be adjusted to test the corresponding feature in
// FSharp.Editor, or deleted.  However, the tests may be exercising underlying F# Compiler 
// functionality and thus have considerable value, they should ony be deleted if we are sure this 
// is not the case.
//
type internal FSharpIntelliSenseToAppearAdornment_DEPRECATED(view: IWpfTextView, cursorPoint: SnapshotPoint, site: System.IServiceProvider) as this =
        let fontFamily, pointSize, excludedCodeForegroundColorBrush, backgroundBrush = VSFontsAndColorsHelper.GetContents(site)
        // TODO: We should really create our own adornment layer.  It is possible (unlikely) that pre-existing layers may be re-ordered, or that
        // code 'owning' the layer will choose to clear all adornments, for example.  But creating a new adornment layer can only be done via MEF-export, and
        // as of yet, we have not done any MEF-exporting in the language service.  So for now, use the existing VisibleWhitespace layer, and incur some risk, just to 
        // unblock the feature.
        let layer = view.GetAdornmentLayer("VisibleWhitespace") 
        let tag = "FSharpIntelliSenseToAppearAdornment_DEPRECATED"
        let pointSize = float pointSize * 96.0 / 72.0  // need to convert from pt to px
        do
            // draw it now
            for line in view.TextViewLines do
                if line.VisibilityState = VisibilityState.FullyVisible then
                    this.CreateVisuals(line)

        member this.CreateVisuals(line : ITextViewLine) =
            let textViewLines = view.TextViewLines
            let cursorPoint = cursorPoint.TranslateTo(line.Snapshot, PointTrackingMode.Positive)
            if line.ContainsBufferPosition(cursorPoint) then
                let i = cursorPoint.Position
                if view.TextSnapshot.Length <> 0 then  // if there are no characters in the buffer, then there is no character to text-relative adorn
                    let span = 
                        // The cursor is just after e.g. the '.' they just pressed, so get character just before it...
                        if i-1 < 0 then
                            // ... unless we are at the very start of the buffer and there is no prior character
                            new SnapshotSpan(view.TextSnapshot, Span.FromBounds(i, i+1))
                        else
                            new SnapshotSpan(view.TextSnapshot, Span.FromBounds(i-1, i))
                    let g = textViewLines.GetMarkerGeometry(span)
                    let tb = new System.Windows.Controls.TextBlock(Text=Strings.IntelliSenseLoading(), FontFamily=System.Windows.Media.FontFamily(fontFamily), FontSize=pointSize)
                    tb.Foreground <- excludedCodeForegroundColorBrush
                    let sp = new System.Windows.Controls.StackPanel(Orientation=System.Windows.Controls.Orientation.Horizontal)
                    System.Windows.Documents.TextElement.SetForeground(sp, excludedCodeForegroundColorBrush.GetAsFrozen() :?> System.Windows.Media.Brush)
                    sp.Margin <- System.Windows.Thickness(3.0)
                    let sa = new Microsoft.Internal.VisualStudio.PlatformUI.SpinAnimationControl(Width=pointSize, Height=pointSize, IsSpinning=true)
                    sp.Children.Add(sa) |> ignore
                    sp.Children.Add(new System.Windows.Controls.Canvas(Width=4.0)) |> ignore // spacing between
                    sp.Children.Add(tb) |> ignore
                    let border = new System.Windows.Controls.Border()
                    border.BorderBrush <- System.Windows.SystemColors.ActiveBorderBrush 
                    border.BorderThickness <- System.Windows.Thickness(1.0)
                    border.Background <- backgroundBrush
                    border.Child <- sp
                    System.Windows.Controls.Canvas.SetLeft(border, g.Bounds.Right)
                    System.Windows.Controls.Canvas.SetTop(border, g.Bounds.Bottom + 3.0)
                    layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, System.Nullable(span), tag, border, null) |> ignore

        member this.RemoveSelf() =
            layer.RemoveAdornmentsByTag(tag)

/// Implements ISource, IVsTextLinesEvents, IVsHiddenTextClient, IVsUserDataEvents etc. via FSharpSourceBase_DEPRECATED by filling in the remaining functionality 
type internal FSharpSource_DEPRECATED(service:LanguageService_DEPRECATED, textLines, colorizer, vsFileWatch:IVsFileChangeEx, depFileChange: IDependencyFileChangeNotify_DEPRECATED, getInteractiveChecker) as source = 
        inherit FSharpSourceBase_DEPRECATED(service, textLines, colorizer)
        
        let mutable lastCommentSpan = new TextSpan()
        let mutable vsFileWatch = vsFileWatch
        let mutable textLines = textLines

        let mutable fileName = VsTextLines.GetFilename textLines

        let recolorizeWholeFile() = 
            if source.ColorState<>null && textLines<>null then      // textlines is used by GetLineCount()
                    source.ColorState.ReColorizeLines(0, source.GetLineCount() - 1) |> ignore

        let recolorizeLine(line:int) = 
            if source.ColorState<>null && textLines<>null && line >= 0 && line < source.GetLineCount() then      // textlines is used by GetLineCount()
                    source.ColorState.ReColorizeLines(line, line) |> ignore

        let iSource = new FSharpSourceTestable_DEPRECATED(recolorizeWholeFile,recolorizeLine,(fun () -> VsTextLines.GetFilename textLines),(fun () -> source.IsClosed),vsFileWatch, depFileChange) :> IFSharpSource_DEPRECATED

        override source.NormalizeErrorString(message) = FSharpDiagnostic.NormalizeErrorString message
        override source.NewlineifyErrorString(message) = FSharpDiagnostic.NewlineifyErrorString message

        override source.GetExpressionAtPosition(line, col) = 
            let upi = source.GetParseTree()
            match ParsedInput.TryFindExpressionIslandInPosition(Position.fromZ line col, upi.ParseTree) with
            | Some islandToEvaluate -> islandToEvaluate
            | None -> null

        member source.GetParseTree() : FSharpParseFileResults =
            // get our hands on lss.Parser (FSharpChecker)
            let ic : FSharpChecker = getInteractiveChecker() 
            let flags = 
                [|
                    match iSource.ProjectSite with
                    | Some pi -> 
                        yield! pi.CompilationOptions |> Array.filter(fun flag -> flag.StartsWith("--define:"))
                    | None -> ()
                    yield "--noframework"
                    yield "--define:COMPILED"

                |]
            // get a sync parse of the file
            let co, _ = 
                { ProjectFileName = fileName + ".dummy.fsproj"
                  ProjectId = None
                  SourceFiles = [| fileName |]
                  OtherOptions = flags
                  ReferencedProjects = [| |]
                  IsIncompleteTypeCheckEnvironment = true
                  UseScriptResolutionRules = false
                  LoadTime = new System.DateTime(2000,1,1)   // dummy data, just enough to get a parse
                  UnresolvedReferences = None
                  OriginalLoadReferences = []
                  Stamp = None }
                |> ic.GetParsingOptionsFromProjectOptions

            ic.ParseFile(fileName,  FSharp.Compiler.Text.SourceText.ofString (source.GetText()), co) |> Async.RunImmediate

        override source.GetCommentFormat() = 
            let mutable info = new CommentInfo()
            info.BlockEnd<-"(*"
            info.BlockStart<-"*)"
            info.UseLineComments<-true
            info.LineStart <- "//"
            info
            
        member val FSharpIntelliSenseToAppearAdornment_DEPRECATED : FSharpIntelliSenseToAppearAdornment_DEPRECATED option = None with get, set
        member val CancellationTokenSource : CancellationTokenSource = null with get, set

        member source.ResetFSharpIntelliSenseToAppearAdornment() =
            UIThread.MustBeCalledFromUIThread()
            if source.CancellationTokenSource <> null && not source.CancellationTokenSource.IsCancellationRequested then
                source.CancellationTokenSource.Cancel()
            match source.FSharpIntelliSenseToAppearAdornment_DEPRECATED with
            | None -> ()
            | Some a -> 
                a.RemoveSelf()
                source.FSharpIntelliSenseToAppearAdornment_DEPRECATED <- None

        member source.AttachFSharpIntelliSenseToAppearAdornment(wpfTextView, cursorPoint, completionWasExplicitlyRequested) =
            UIThread.MustBeCalledFromUIThread()
            source.ResetFSharpIntelliSenseToAppearAdornment() 
            let cts = new CancellationTokenSource()
            source.CancellationTokenSource <- cts
            let timeUntilPopup =
                if not completionWasExplicitlyRequested then 
                    2000 // we could always tweak this number, intent is long enough to never show up with everyday 'fast' IntelliSense on '.', but short enough so you discover that waiting may be useful
                else 
                    500 // they explicitly pressed Ctrl-J or Ctrl-space, so we should apply the 'loading...' adornment more aggressively
            let cancelableTask =
                async {
                    do! Async.Sleep(timeUntilPopup)  
                    UIThread.MustBeCalledFromUIThread()
                    if source.FSharpIntelliSenseToAppearAdornment_DEPRECATED.IsNone then
                        source.FSharpIntelliSenseToAppearAdornment_DEPRECATED <- Some <| new FSharpIntelliSenseToAppearAdornment_DEPRECATED(wpfTextView, cursorPoint, service.Site)
                        // reset if they move cursor, (ideally would only reset if moved out of 'applicative span', but for now, any movement cancels)
                        let rec caretSubscription : IDisposable = wpfTextView.Caret.PositionChanged.Subscribe(fun _ea -> source.ResetFSharpIntelliSenseToAppearAdornment(); caretSubscription.Dispose())
                        // if the user types new chars right before the caret, the editor does not fire a Caret.PositionChanged event, but LayoutChanged will fire, so also hook that
                        let rec layoutSubscription : IDisposable = wpfTextView.LayoutChanged.Subscribe(fun _ea -> source.ResetFSharpIntelliSenseToAppearAdornment(); layoutSubscription.Dispose())
                        ()
                    else
                        Debug.Assert(false, "why was FSharpIntelliSenseToAppearAdornment_DEPRECATED already set?")
                }
            Async.StartImmediate(Async.TryCancelled(cancelableTask, ignore), source.CancellationTokenSource.Token)

        override source.HandleLostFocus() =
            source.ResetFSharpIntelliSenseToAppearAdornment()

        // entrypoint for autocompletion
        override source.Completion(textView, info, reason, requireFreshResults) =
            let line = ref 0
            let idx = ref 0
            NativeMethods.ThrowOnFailure(textView.GetCaretPos(line, idx)) |> ignore
            if requireFreshResults <> RequireFreshResults.Yes then
                // if it was Yes, then we are in second-chance intellisense and we already started the task for the first-chance
                let wpfTextView = FSharpSourceBase_DEPRECATED.GetWpfTextViewFromVsTextView(textView)
                let ss = wpfTextView.TextSnapshot
                let tsLine = ss.GetLineFromLineNumber(!line)
                let lineLen = tsLine.End.Position - tsLine.Start.Position 
                let toAdd = min lineLen !idx   // if we are in virtual whitespace after EOF, is illegal to get a snapshot point there
                let cursorPoint = ss.GetLineFromLineNumber(!line).Start.Add(toAdd)
                let completionWasExplicitlyRequested = 
                    (reason=BackgroundRequestReason.DisplayMemberList) // Ctrl-J
                    || (reason=BackgroundRequestReason.CompleteWord) // Ctrl-space
                    // Note: BackgroundRequestReason.MemberSelect is when they pressed '.' and it auto-popped up
                source.AttachFSharpIntelliSenseToAppearAdornment(wpfTextView, cursorPoint, completionWasExplicitlyRequested)
            let reason =
                if reason = BackgroundRequestReason.CompleteWord then
                    let upi = source.GetParseTree()
                    let isBetweenDotAndIdent =
                        match ParsedInput.TryFindExpressionASTLeftOfDotLeftOfCursor(Position.fromZ !line !idx, upi.ParseTree) with
                        | Some(_,isBetweenDotAndIdent) -> isBetweenDotAndIdent
                        | None -> false
                    if isBetweenDotAndIdent then
                        BackgroundRequestReason.DisplayMemberList  // be like C#; convert a Ctrl-space to a Ctrl-J if the cursor is here:   class.$bar()
                    else
                        reason
                else 
                    reason
            source.BeginBackgroundRequest(
                !line, !idx, info, reason, textView, requireFreshResults, 
                new BackgroundRequestResultHandler(source.HandleCompletionResponse)) |> ignore

        member source.HandleCompletionResponse(req) =
            match req with
            | null -> source.ResetFSharpIntelliSenseToAppearAdornment()
            | _ when isNull req.View || isNull req.ResultIntellisenseInfo -> source.ResetFSharpIntelliSenseToAppearAdornment()
            | _ when (req.Timestamp <> source.ChangeCount) -> source.ResetFSharpIntelliSenseToAppearAdornment()
            | _ ->
                  source.HandleResponseHelper(req)
                  let reason = req.Reason
                  if reason = BackgroundRequestReason.MemberSelectAndHighlightBraces then
                      source.HandleMatchBracesResponse(req)
                  async {
                      let! decls = req.ResultIntellisenseInfo.GetDeclarations(req.Snapshot, req.Line, req.Col, reason)
                      do! Async.SwitchToContext UIThread.TheSynchronizationContext
                      if (isNull decls || decls.IsEmpty()) && req.Timestamp <> req.ResultTimestamp then
                          // Second chance intellisense: we didn't get any result and the basis typecheck was stale. We need to retrigger the completion.
                          source.Completion(req.View, req.TokenInfo, req.Reason, RequireFreshResults.Yes)
                      else
                          match decls with
                          | null -> ()
                          | _ ->
                              let completeWord = (reason = BackgroundRequestReason.CompleteWord)
                              let line,idx =
                                  let mutable line = 0
                                  let mutable idx = 0
                                  req.View.GetCaretPos(&line, &idx) |> NativeMethods.ThrowOnFailure |> ignore
                                  (line,idx)
                              if decls.GetCount("") > 0 && 
                                  (source.Service.Preferences.AutoListMembers || completeWord || reason = BackgroundRequestReason.DisplayMemberList) &&
                                  line = req.Line && idx = req.Col // ensure user has not chaged cursor location (note: ideally, we would allow typing if still in 'applicative span' for completion)
                                  then
                                  source.CompletionSet.Init(req.View, decls, completeWord)
                          source.ResetFSharpIntelliSenseToAppearAdornment()
                  } |> Async.StartImmediate

        member source.PreFixupSpan(origSpan : TextSpan) =            
            let mutable span = new TextSpan(iEndIndex = origSpan.iEndIndex, iEndLine = origSpan.iEndLine, iStartIndex = origSpan.iStartIndex, iStartLine = origSpan.iStartLine)
            // if at start of next line, treat like end of previous line
            if span.iEndIndex = 0 && not(span.iEndLine = span.iStartLine) then
                span.iEndLine <- span.iEndLine - 1
            span
            
        member source.PostFixupSpan(origSpan : TextSpan) =            
            let mutable span = new TextSpan(iEndIndex = origSpan.iEndIndex, iEndLine = origSpan.iEndLine, iStartIndex = origSpan.iStartIndex, iStartLine = origSpan.iStartLine)
            // move highlight to start & end of line
            span.iStartIndex <- 0
            span.iEndIndex <- source.GetLine(span.iEndLine).Length
            span
        
        override source.CommentLines(origSpan, lineComment) =
            let adapter = source.getEditorAdapter()
            use edit = adapter.GetDataBuffer(textLines).CreateEdit(EditOptions.DefaultMinimalChange, Unchecked.defaultof<Nullable<int>>, None)
            let mutable span = source.PreFixupSpan(origSpan)
            for i = span.iStartLine to span.iEndLine do
                let line = edit.Snapshot.GetLineFromLineNumber(i)
                source.SetText(line, 0, 0, lineComment, edit)
            edit.Apply() |> ignore

            lastCommentSpan <- source.PostFixupSpan(span)
            lastCommentSpan

        override source.UncommentLines(origSpan, lineComment) =
            let adapter = source.getEditorAdapter()
            use edit = adapter.GetDataBuffer(textLines).CreateEdit(EditOptions.DefaultMinimalChange, Unchecked.defaultof<Nullable<int>>, None)
            let mutable span = source.PreFixupSpan(origSpan)
            let len = lineComment.Length
            for i = span.iStartLine to span.iEndLine do
                let startIndex = source.ScanToNonWhitespaceChar(i)
                let curLine = source.GetLine(i)
                if curLine.Length >= startIndex + len && curLine.Substring(startIndex, len) = lineComment then
                    let line = edit.Snapshot.GetLineFromLineNumber(i)
                    source.SetText(line, startIndex, len, "", edit)
            edit.Apply() |> ignore
            source.PostFixupSpan(span)

        override source.CommentSpan(span) =
            base.CommentSpan(span) |> ignore
            lastCommentSpan
            
        override source.RecordChangeToView() = iSource.RecordChangeToView()
        override source.RecordViewRefreshed() = iSource.RecordViewRefreshed()
        override source.NeedsVisualRefresh = iSource.NeedsVisualRefresh
            
        override source.ChangeCount
            with get() = iSource.ChangeCount
            and set(value) = iSource.ChangeCount <- value                
            
        override source.DirtyTime
            with get() = iSource.DirtyTime
            and set(value) = iSource.DirtyTime <- value                
                            
        override source.Dispose() =
            try 
                base.Dispose()       
            finally
                ((box iSource):?>IDisposable).Dispose()       
                vsFileWatch<-null
                textLines<-null

        override source.OnUserDataChange(riidKey, _vtNewValue) =
            let newfileName = VsTextLines.GetFilename textLines
            if not (String.Equals(fileName, newfileName, StringComparison.InvariantCultureIgnoreCase)) then
                // the filename of the text buffer is changing, could be changing e.g. .fsx to .fs or vice versa
                fileName <- newfileName
                iSource.RecolorizeWholeFile()

        // Just forward to IFSharpSource_DEPRECATED  
        interface IFSharpSource_DEPRECATED with
            member source.RecolorizeWholeFile() = iSource.RecolorizeWholeFile() 
            member source.RecolorizeLine line = iSource.RecolorizeLine line
            member source.RecordChangeToView() = iSource.RecordChangeToView()
            member source.RecordViewRefreshed() = iSource.RecordViewRefreshed()
            member source.NeedsVisualRefresh = iSource.NeedsVisualRefresh
            member source.IsClosed = iSource.IsClosed
            member source.ProjectSite with get() = iSource.ProjectSite and set(value) = iSource.ProjectSite <- value
            member source.ChangeCount with get() = iSource.ChangeCount and set(value) = iSource.ChangeCount <- value                
            member source.DirtyTime with get() = iSource.DirtyTime and set(value) = iSource.DirtyTime <- value                
            member source.SetDependencyFiles(files) = iSource.SetDependencyFiles(files)
                
        /// Hook file change events.  It's not clear that this implementation is ever utilized, since
        /// the implementation on FSharpSourceTestable_DEPRECATED is used instead.
        interface IVsFileChangeEvents with 
            member changes.FilesChanged(_count : uint32, _files: string [], _changeFlags : uint32 []) = 0
            member changes.DirectoryChanged(_directory: string) = 0
                
module internal Source = 
        /// This is the ideal implementation of the Source concept abstracted from MLS.  
        let CreateSourceTestable_DEPRECATED (recolorizeWholeFile, recolorizeLine, filename, isClosed, vsFileWatch, depFileChangeNotify) = 
            new FSharpSourceTestable_DEPRECATED(recolorizeWholeFile, recolorizeLine, filename, isClosed, vsFileWatch, depFileChangeNotify) :> IFSharpSource_DEPRECATED

        let CreateSource_DEPRECATED(service, textLines, colorizer, vsFileWatch, depFileChangeNotify, getInteractiveChecker) =
            new FSharpSource_DEPRECATED(service, textLines, colorizer, vsFileWatch, depFileChangeNotify, getInteractiveChecker) :> IFSharpSource_DEPRECATED
                
