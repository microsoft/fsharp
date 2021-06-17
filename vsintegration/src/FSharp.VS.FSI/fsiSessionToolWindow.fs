// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Interactive

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.ComponentModel.Design
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.OLE.Interop
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.TextManager.Interop
open Microsoft.VisualStudio.Package
open EnvDTE

open Microsoft.VisualStudio.ComponentModelHost
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Utilities

type VSStd2KCmdID = VSConstants.VSStd2KCmdID // nested type
type VSStd97CmdID = VSConstants.VSStd97CmdID // nested type
type IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider

type internal ITestVFSI =
    /// Send a string; the ';;' will be added to the end; does not interact with history
    abstract SendTextInteraction : string -> unit
    /// Returns the n most recent lines in the view.  After SendTextInteraction, can poll for a prompt to know when interaction finished.
    abstract GetMostRecentLines : int -> string[]

#nowarn "40"
#nowarn "47"
module internal Locals = 
    let fsiFontsAndColorsCategory = new Guid("{00CCEE86-3140-4E06-A65A-A92665A40D6F}")
    let defaultVSRegistryRoot = @"Software\Microsoft\VisualStudio\15.0"
    let settingsRegistrySubKey = @"General"
    let debugPromptRegistryValue = "FSharpHideScriptDebugWarning"
    // Prompts come through as "SERVER-PROMPT>\n" (when in "server mode").
    // In fsi.exe, the newline is needed to get the output send through to VS.
    // Here the reverse mapping is applied.
    let prompt = "SERVER-PROMPT>"

    let fixServerPrompt (str:string) =
        (* Replace 'prompt' by ">" throughout string, add newline, unless ending in prompt *)
        let str = if str.EndsWith(prompt) then str + " " else str + Environment.NewLine
        let str = str.Replace(prompt,">")
        str


    let setSiteForObjectWithSite obj provider = (box obj :?> IObjectWithSite).SetSite(provider)

    type Response = StdOut | StdErr // keys for merge of stdout and stderr events from fsi.exe session

    let pair x y = x,y
    let equal x y = x=y
    /// Given a list of (key,value)
    /// Chunk into (key,values) where the values are keys of (key,value) with the same key.    
    /// Complexity: this code is linear in (length kxs).
    let chunkKeyValues allEntries =
        allEntries
        |> List.groupBy(fun (responseType, line) -> responseType)
        |> List.map(fun (responseType, entries) -> (responseType, entries |> List.map(fun (_, line) -> line)))

    
open Util
open Locals

type internal FsiDebuggerState =
    | NotRunning
    | AttachedToFSI
    | AttachedNotToFSI

type internal FsiEditorSendAction =
    | ExecuteSelection
    | ExecuteLine
    | DebugSelection

[<Guid(Guids.guidFsiSessionToolWindow)>]
type internal FsiToolWindow() as this = 
    inherit ToolWindowPane(null)
    
    let providerGlobal = Package.GetGlobalService(typeof<IOleServiceProvider>) :?> IOleServiceProvider
    let provider       = new ServiceProvider(providerGlobal) :> System.IServiceProvider
    let textViewAdapter, contentTypeRegistry =
        // end of 623708 workaround. 
        let componentModel = provider.GetService(typeof<SComponentModel>) :?> IComponentModel
        componentModel.GetService<IVsEditorAdaptersFactoryService>(), componentModel.GetService<IContentTypeRegistryService>()

    // REVIEW: trap provider nulls?    
    let providerNative = provider.GetService(typeof<IOleServiceProvider>) :?> IOleServiceProvider            
    let textLines      = Util.CreateObjectT<VsTextBufferClass,IVsTextLines> provider  
    do  setSiteForObjectWithSite textLines providerNative
    do  textLines.InitializeContent("", 0) |> throwOnFailure0
    let textView       = Util.CreateObjectT<VsTextViewClass,IVsTextView> provider
    do  setSiteForObjectWithSite textView  providerNative
    
    do  textView.Initialize(textLines,
                            IntPtr.Zero,
                            uint32 TextViewInitFlags.VIF_VSCROLL ||| uint32 TextViewInitFlags.VIF_HSCROLL ||| uint32 TextViewInitFlags3.VIF_NO_HWND_SUPPORT,
                            null) |> throwOnFailure0

    // Remove: after next submit (passing through SD)       
    // vsTextManager did not seem to yield current selection...

    // The IP sample called GetService() to obtain the LanguageService.
    let fsiLangService = provider.GetService(typeof<FsiLanguageService>) |> unbox : FsiLanguageService
    do  if isNull(box fsiLangService) then
            // This would be unexpected, since this package provides the service.
            System.Windows.Forms.MessageBox.Show(VFSIstrings.SR.couldNotObtainFSharpLS()) |> ignore
            failwith "No FsiLanguageService"
            // Q: what is the graceful way to error out inside VS?

    let scanner    = new FsiScanner(textLines)
    let colorizer  = new Colorizer(fsiLangService,textLines,scanner)
    let source     = new FsiSource(fsiLangService,textLines,colorizer)  
    let codeWinMan = fsiLangService.CreateCodeWindowManager(null,source)
    do  fsiLangService.AddCodeWindowManager(codeWinMan)
    do  codeWinMan.OnNewView(textView)  |> throwOnFailure0

    //  Create the stream on top of the text buffer.
    let textStream = new TextBufferStream(textViewAdapter.GetDataBuffer(textLines), contentTypeRegistry)
    let synchronizationContext = System.Threading.SynchronizationContext.Current
    let win32win = { new System.Windows.Forms.IWin32Window with member this.Handle = textView.GetWindowHandle()}
    let mutable textView       = textView
    let mutable textLines      = textLines
    let mutable commandService = null
    let mutable commandList : OleMenuCommand list = []

    // Set a function that gets the current ReadOnly span for the text of this instance.
    do  fsiLangService.ReadOnlySpanGetter <- (fun () -> textStream.ReadOnlyMarkerSpan)

    // RE: Allowing WORD-WRAP to be enabled in the VFSI window.
    // It seems that WORD-WRAP is forced off by default.
    //   See "Forcing View Settings" at http://msdn.microsoft.com/en-us/library/bb164694.aspx.    
    //   See cmdwin.cpp which contains the following comment prior to removing VSEDITPROPID_ViewLangOpt_WordWrap.      
    //        "// remove this from the forced settings group, allowing it to toggle freely (but detached)".
    // 
    // removeWordWrapForcedProperty() follows cmdwin.cpp.
    // Removing VSEDITPROPID.VSEDITPROPID_ViewLangOpt_WordWrap allows word-wrap to toggle on/off, (e.g. ctrl-E, ctrl-W).
    //
    // REVIEW: Next question, can WORD-WRAP be toggled on by default? Do we want that? Maybe not!
    let setTextViewProperties() =
          let wpfTextView = textViewAdapter.GetWpfTextView(textView)
          // Enable find in the text view without implementing the IVsFindTarget interface (by allowing                
          // the active text view to directly respond to the find manager via the locate find target                
          // command)  
          wpfTextView.Options.SetOptionValue("Enable Autonomous Find", true)
          match textView with
            | :? IVsTextEditorPropertyCategoryContainer as vsTextEditorPropertyCategoryContainer -> 
                let mutable fontAndColorGuid = Locals.fsiFontsAndColorsCategory
                let mutable GUID_EditPropCategory_View_MasterSettings = new Guid("{D1756E7C-B7FD-49a8-B48E-87B14A55655A}") // see {VSIP}/Common/Inc/textmgr.h
                let viewMasterSettingsCategory = vsTextEditorPropertyCategoryContainer.GetPropertyCategory(&GUID_EditPropCategory_View_MasterSettings) |> throwOnFailure1
                viewMasterSettingsCategory.RemoveProperty(VSEDITPROPID.VSEDITPROPID_ViewLangOpt_WordWrap) |> throwOnFailure0
                viewMasterSettingsCategory.SetProperty(VSEDITPROPID.VSEDITPROPID_ViewGeneral_FontCategory, fontAndColorGuid) |> throwOnFailure0
                viewMasterSettingsCategory.SetProperty(VSEDITPROPID.VSEDITPROPID_ViewGeneral_ColorCategory, fontAndColorGuid) |> throwOnFailure0

            | _ -> ()
    do  setTextViewProperties()

#if TurnWordWrapOnByDefault
    // Currently off, but maybe on, depending on feedback.
    let toggleWordWrap() =  
        // From /Program Files/Microsoft Visual Studio 2008 SDK/VisualStudioIntegration/Common/Inc/stdidcmd.h
        let guid_CMDSETID_StandardCommandSet2K = Guid("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}")
        let ECMD_TOGGLEWORDWRAP = 121u
        let commandTarget = textView :?> IOleCommandTarget // object is VsTextViewClass :> VsTextView :> IOleCommandTarget
        commandTarget.Exec(ref guid_CMDSETID_StandardCommandSet2K,ECMD_TOGGLEWORDWRAP,0u,0n,0n) |> throwOnFailure0
    do  toggleWordWrap()
#endif  
    
    let setScrollToEndOfBuffer() =
        if null <> textView then
            let horizontalScrollbar = 0
            let verticalScrollbar   = 1                
            // Make sure that the last line of the buffer is visible. [ignore errors].            
            let buffer = textViewAdapter.GetDataBuffer(textLines)
            let lastLine = buffer.CurrentSnapshot.LineCount - 1
            if lastLine >= 0 then
                let lineStart = buffer.CurrentSnapshot.GetLineFromLineNumber(lastLine).Start
                let wpfTextView = textViewAdapter.GetWpfTextView(textView)
                wpfTextView.DisplayTextLineContainingBufferPosition(lineStart, 0.0, ViewRelativePosition.Bottom)

    let setScrollToStartOfLine() =
        if null <> textView then
            let horizontalScrollbar = 0
            let verticalScrollbar   = 1                                        
            // Make sure that the text view is showing the beginning of the new line.
            let res,minUnit,maxUnit,visibleUnits,firstVisibleUnit = textView.GetScrollInfo(horizontalScrollbar)            
            if ErrorHandler.Succeeded(res) then                    
                textView.SetScrollPosition(horizontalScrollbar,minUnit) |> ignore (* ignore error *)

    // F# Interactive sessions
    let history  = HistoryBuffer()
    let sessions = Session.FsiSessions()
    do  fsiLangService.Sessions <- sessions    

    let writeText scroll (str:string) =
        if str <> null && textLines <> null then
            lock textLines (fun () ->
                textStream.DirectWrite(fixServerPrompt str)
                if scroll then 
                    setScrollToEndOfBuffer()
            )

    let writeTextAndScroll (str:string) = writeText true str

    let writeTextNoScroll (str:string) = writeText false str

    // Merge stdout/stderr events prior to buffering. Paired with StdOut/StdErr keys so we can split them afterwards.  
    let responseE = Observable.merge (Observable.map (pair StdOut) sessions.Output) (Observable.map (pair StdErr) sessions.Error)
            
    // Buffer the output and error events. This makes text updates *MUCH* faster (since they are done as a block).
    // Also, the buffering invokes to the GUI thread.
    let bufferMS = 50         
    let flushResponseBuffer,responseBufferE = Session.bufferEvent bufferMS  responseE

    // Wire up session outputs to write to textLines.
    // Recover the chunks (consecutive runs of stderr or stdout) and write as a single item.
    // responseEventE always triggers on Gui thread, so calling writeTextAndScroll is safe
    let writeKeyChunk = function
        | StdOut,strs -> writeTextAndScroll (String.concat Environment.NewLine strs)  // later: stdout and stderr may color differently
        | StdErr,strs -> writeTextAndScroll (String.concat Environment.NewLine strs)  // later: hence keep them split.
    do  responseBufferE.Add(fun keyStrings -> let keyChunks : (Response * string list) list = chunkKeyValues keyStrings
                                              List.iter writeKeyChunk keyChunks)
    let showInitialMessageNetCore scroll =
        if Session.SessionsProperties.fsiUseNetCore then
            writeText scroll ((VFSIstrings.SR.sessionInitialMessageNetCore()+Environment.NewLine+prompt))

    // Write message on a session termination. Should be called on Gui thread.
    let recordTermination () = 
        if not sessions.Alive then // check is likely redundant
            synchronizationContext.Post(
                System.Threading.SendOrPostCallback(
                    fun _ -> 
                        writeTextAndScroll ((VFSIstrings.SR.sessionTerminationDetected())+Environment.NewLine)
                        showInitialMessageNetCore true
            ), null)
            
    do  sessions.Exited.Add(fun _ -> recordTermination())

    // For .NET Core the session doesn't start automatically.  Rather it may optionally be started by an Alt-Enter from a script,
    // or else by pressing Enter in the REPL window.
    do  showInitialMessageNetCore false
        if not Session.SessionsProperties.fsiUseNetCore then 
            sessions.Restart(null)

    let clearUndoStack (textLines:IVsTextLines) = // Clear the UNDO stack.
        let undoManager = textLines.GetUndoManager() |> throwOnFailure1
        undoManager.DiscardFrom(null)
            
    let setCursorAtEndOfBuffer() =
        if null <> textView && null <> textLines then            
            let lastLine,lastIndex = textLines.GetLastLineIndex() |> throwOnFailure2
            textView.SetCaretPos(lastLine, lastIndex)             |> throwOnFailure0                
            setScrollToEndOfBuffer()
            setScrollToStartOfLine()
        
    /// Returns true if the current position is inside the writable section of the buffer.
    let isCurrentPositionInInputArea() =
        if (null = textView) (*|| (null = textStream.ReadOnlyMarker)*) then
            false
        else
            let span = textStream.ReadOnlyMarkerSpan
            let line,column = textView.GetCaretPos() |> throwOnFailure2
            (line > span.iEndLine) || ((line = span.iEndLine) && (column >= span.iEndIndex))
            
    let isSelectionIntersectsWithReadonly() =
        if null = textView then
            false
        else
            let span = textStream.ReadOnlyMarkerSpan
            let isInInputArea (line,column) = (line > span.iEndLine) || ((line = span.iEndLine) && (column >= span.iEndIndex))
            let (anchorLine,anchorCol,endLine,endCol) = textView.GetSelection() |> throwOnFailure4
            not (isInInputArea(anchorLine,anchorCol)) || not (isInInputArea(endLine,endCol))

    /// Returns true if the current position is at the start of the writable section of the buffer.
    let isCurrentPositionAtStartOfInputArea() =
        if (null = textView) (*|| (null = textStream.ReadOnlyMarker)*) then
            false
        else
            let line,column = textView.GetCaretPos() |> throwOnFailure2
            let span = textStream.ReadOnlyMarkerSpan
            (line = span.iEndLine && column <= span.iEndIndex)
            
    let getInputAreaText() = 
        let lastLine,lastIndex = textLines.GetLastLineIndex() |> throwOnFailure2
        let span = textStream.ReadOnlyMarkerSpan
        let text = textLines.GetLineText(span.iEndLine,span.iEndIndex,lastLine,lastIndex) |> throwOnFailure1
        text

    let setInputAreaText (str:string) =        
        lock textLines (fun () ->
            let  span = textStream.ReadOnlyMarkerSpan
            let lastLine,lastIndex = textLines.GetLastLineIndex() |> throwOnFailure2
            let strHandle = GCHandle.Alloc(str, GCHandleType.Pinned)
            try 
                textLines.ReplaceLines(span.iEndLine, span.iEndIndex, lastLine, lastIndex, strHandle.AddrOfPinnedObject(), str.Length, null) |> throwOnFailure0
            finally
                strHandle.Free()
        )

    let executeTextNoHistory sourceFile (text:string) =
        sessions.Ensure(sourceFile)
        textStream.DirectWriteLine()
        sessions.SendInput(text)
        setCursorAtEndOfBuffer()
        
    let executeUserInput() = 
        if isCurrentPositionInInputArea() then
            sessions.Ensure(null)
            let text = getInputAreaText()
            textStream.ExtendReadOnlyMarker()
            textStream.DirectWriteLine()
            history.Add(text)
            sessions.SendInput(text)
            setCursorAtEndOfBuffer()

    // NOTE: SupportWhen* functions are guard conditions for command handlers

    /// Supported command when input is permitted.
    let supportWhenInInputArea (sender:obj) (args:EventArgs) =    
        let command = sender :?> MenuCommand
        if null <> command then // are these null checks needed?
            let enabled = not source.IsCompletorActive && isCurrentPositionInInputArea()
            command.Supported <- enabled

    /// Support command except when completion is active.    
    let supportUnlessCompleting (sender:obj) (args:EventArgs) =    
        let command = sender :?> MenuCommand
        if null <> command then
            command.Supported <- not source.IsCompletorActive

    let haveTextViewSelection() =        
        let res,text = textView.GetSelectedText()
        (res = VSConstants.S_OK && text.Length>0)

    /// Support when at the start of the input area (e.g. to enable NoAction on LEFT).
    let supportWhenAtStartOfInputArea (sender:obj) (e:EventArgs) =
        let command = sender :?> MenuCommand       
        if command <> null then
            command.Supported <- not source.IsCompletorActive && isCurrentPositionAtStartOfInputArea()

    /// Support when at the start of the input area AND no-selection (e.g. to enable NoAction on BACKSPACE).
    let supportWhenAtStartOfInputAreaAndNoSelection (sender:obj) (e:EventArgs) =
        let command = sender :?> MenuCommand
        if command <> null then
            command.Supported  <- isCurrentPositionAtStartOfInputArea()
            
    let supportWhenSelectionIntersectsWithReadonlyOrNoSelection (sender:obj) (_:EventArgs) =
        let command = sender :?> MenuCommand
        if command <> null then
            command.Supported  <- isSelectionIntersectsWithReadonly() || not (haveTextViewSelection())

    // NOTE: On* are command handlers.

    /// Handles HOME command, move to either start of line (or end of read only region is applicable).    
    let onHome (sender:obj) (e:EventArgs) =
        let currentLine,currentColumn = textView.GetCaretPos() |> throwOnFailure2
        let span = textStream.ReadOnlyMarkerSpan
        if currentLine = span.iEndLine then
            textView.SetCaretPos(currentLine,span.iEndIndex) |> throwOnFailure0
        else
            textView.SetCaretPos(currentLine,0) |> throwOnFailure0            

    /// Handle 'Shift' + 'HOME', move to start of line (or end or readonly area if applicable).    
    let onShiftHome (sender:obj) (args:EventArgs) =        
        let line,endColumn = textView.GetCaretPos() |> throwOnFailure2
        let span = textStream.ReadOnlyMarkerSpan
        let startColumn = 
            if line = span.iEndLine (* && endColumn >= span.iEndIndex *) then
                span.iEndIndex
            else
                0
        textView.SetSelection(line, endColumn, line, startColumn) |> throwOnFailure0

    /// Hanlde no-op, used to overwrite some standard command with an empty action.
    let onNoAction (sender:obj) (e:EventArgs) = ()
    
    
    /// Handle "Clear Pane". Clear input and all but the last ReadOnly line (probably the prompt).    
    let onClearPane (sender:obj) (args:EventArgs) =
        lock textLines (fun () ->        
            // ReadOnly off, then upto the last line and then the input area, then ReadOnly on.
            let span = textStream.ReadOnlyMarkerSpan
            textStream.ResetReadOnlyMarker()
            if span.iEndLine > 0 then
                textLines.ReplaceLines(0, 0, span.iEndLine, 0, IntPtr.Zero, 0, null) |> throwOnFailure0

            // Clear (what is now) the input area text
            let lastLine,lastColumn = textLines.GetLastLineIndex() |> throwOnFailure2
            if lastLine > 0 || span.iEndIndex < lastColumn then
                textLines.ReplaceLines(0, span.iEndIndex, lastLine, lastColumn, IntPtr.Zero, 0, null) |> throwOnFailure0

            textStream.ExtendReadOnlyMarker()
            textView.SetCaretPos(0,span.iEndIndex) |> throwOnFailure0

            clearUndoStack textLines // ClearPane should not be an undo-able operation
        )

    let showContextMenu (sender:obj) (args:EventArgs) =
        let uiShell = provider.GetService(typeof<SVsUIShell>) :?> IVsUIShell
        if null <> uiShell then
            let pt   = System.Windows.Forms.Cursor.Position
            let pnts = [| new POINTS(x=int16 pt.X,y=int16 pt.Y) |]
            let mutable menuGuid = Guids.guidFsiConsoleCmdSet
            uiShell.ShowContextMenu(0u,&menuGuid, int32 Guids.cmdIDFsiConsoleContextMenu, pnts, (textView :?> IOleCommandTarget)) |> ignore // SDK doc says result is void not int?
  
    let onInterrupt (sender:obj) (args:EventArgs) =
        sessions.Interrupt() |> ignore
  
    let onRestart (sender:obj) (args:EventArgs) =
        sessions.Kill() // When Kill() returns there should be no more output/events from that session
        flushResponseBuffer()  // flush output and errors from the killed session that have been buffered, but have not yet come through.
        lock textLines (fun () ->        
            // Clear all prior to restart
            textStream.ResetReadOnlyMarker()            
            textView.SetCaretPos(0,0) |> throwOnFailure0
            let lastLine,lastColumn = textLines.GetLastLineIndex() |> throwOnFailure2
            textLines.ReplaceLines(0, 0, lastLine, lastColumn, IntPtr.Zero, 0, null) |> throwOnFailure0
        )
        clearUndoStack textLines // The reset clear should not be undoable.
        showInitialMessageNetCore true
        if not Session.SessionsProperties.fsiUseNetCore then 
            sessions.Restart(null)

    /// Handle RETURN, unless Intelisense completion is in progress.
    let onReturn (sender:obj) (e:EventArgs) =    
        lock textLines (fun () ->
            if not sessions.Alive then
                sessions.Restart(null)
            else
                if isCurrentPositionInInputArea() then                                            
                    executeUserInput()
                    setCursorAtEndOfBuffer()
        )

    let showNoActivate() = 
        let frame = this.Frame :?> IVsWindowFrame
        frame.ShowNoActivate() |> ignore
    
    let getDebuggerState () =
        let fsiProcId = sessions.ProcessID
        let dte = provider.GetService(typeof<DTE>) :?> DTE

        if isNull dte.Debugger.DebuggedProcesses || dte.Debugger.DebuggedProcesses.Count = 0 then
            FsiDebuggerState.NotRunning, None
        else
            let debuggedFsi =
                dte.Debugger.DebuggedProcesses
                |> Seq.cast<Process>
                |> Seq.tryFind (fun p -> p.ProcessID = fsiProcId)
            match debuggedFsi with
            | Some _ -> FsiDebuggerState.AttachedToFSI, debuggedFsi
            | None -> FsiDebuggerState.AttachedNotToFSI, None
    
    let getDebugAttachedFSIProcess () =
        match getDebuggerState () with
        | FsiDebuggerState.AttachedToFSI, opt -> opt
        | _ -> None

    let debuggerIsRunning () =
        match getDebuggerState () with
        | FsiDebuggerState.NotRunning, _ -> false
        | _ -> true

    // noop if debugger isn't attached to FSI
    let detachDebugger () =
        try
            match getDebugAttachedFSIProcess () with
            | Some(p) -> p.Detach(true)
            | _ -> ()
        with _ -> ()

    // noop if debugger is already running
    let attachDebugger () =
        if not (debuggerIsRunning ()) then
            let fsiProcId = sessions.ProcessID
            let dte = provider.GetService(typeof<DTE>) :?> DTE
            let fsiProc = 
                if isNull dte.Debugger.LocalProcesses then None else
                dte.Debugger.LocalProcesses
                |> Seq.cast<Process>
                |> Seq.tryFind (fun p -> p.ProcessID = fsiProcId)
            try
                match fsiProc with
                | Some(p) -> p.Attach()
                | _ -> ()
            with _ -> ()

    // checks if current session is configured such that debugging will work well
    // if not, pops a dialog warning the user
    let checkDebuggability () =       
        // debug experience is good when optimizations are off and debug info is produced
        if ArgParsing.debugInfoEnabled sessions.ProcessArgs && not (ArgParsing.optimizationsEnabled sessions.ProcessArgs) then
            true
        else
            match RegistryHelpers.tryReadHKCU (defaultVSRegistryRoot + "\\" + settingsRegistrySubKey) debugPromptRegistryValue with
            | Some(1) -> true  // warning dialog suppressed
            | _ ->
                let mutable suppressDiag = false
                let result =
                    Microsoft.VisualStudio.PlatformUI.MessageDialog.Show(
                        VFSIstrings.SR.fsharpInteractive(),
                        VFSIstrings.SR.sessionIsNotDebugFriendly(),
                        Microsoft.VisualStudio.PlatformUI.MessageDialogCommandSet.YesNo,
                        VFSIstrings.SR.doNotShowWarningInFuture(),
                        &suppressDiag
                    )
                
                if suppressDiag && result <> Microsoft.VisualStudio.PlatformUI.MessageDialogCommand.Abort then
                    RegistryHelpers.writeHKCU (defaultVSRegistryRoot + "\\" + settingsRegistrySubKey) debugPromptRegistryValue 1

                // if user picks YES, allow debugging anyways
                result = Microsoft.VisualStudio.PlatformUI.MessageDialogCommand.Yes

    let onAttachDebugger (sender:obj) (args:EventArgs) =
        if checkDebuggability() then
            attachDebugger()
            showNoActivate()

    let onDetachDebugger (sender:obj) (args:EventArgs) =
        detachDebugger()
        showNoActivate()

    let sendTextToFSI text = 
        try
            showNoActivate()
            let directiveC  = sprintf "# 1 \"stdin\""    (* stdin line number reset code *)                
            let text = "\n" + text + "\n" + directiveC + "\n;;\n"
            executeTextNoHistory null text
        with _ -> ()

    let executeInteraction dbgBreak dir (filename: string) topLine text =
        // Preserving previous functionality, including the #directives...
        let interaction =
            "\n"
          + (sprintf "# silentCd @\"%s\" ;; " dir) + "\n"
          + (if dbgBreak then "# dbgbreak\n" else "")
          + (sprintf "# %d @\"%s\" " topLine filename) + "\n"
          + text + "\n"
          + "# 1 \"stdin\"" + "\n" (* stdin line number reset code *)
          + ";;" + "\n"

        executeTextNoHistory filename interaction

    let sendSelectionToFSI action =
        let dbgBreak,selectLine = 
            match action with
            | ExecuteSelection -> false, false
            | ExecuteLine -> false, true
            | DebugSelection -> true, false

        try
            let dte = provider.GetService(typeof<DTE>) :?> DTE        
            let activeD = dte.ActiveDocument            
            match activeD.Selection with
            | :? TextSelection as selection when selectLine || selection.Text = "" ->
                selection.SelectLine()
                showNoActivate()
                executeInteraction dbgBreak (System.IO.Path.GetDirectoryName(activeD.FullName)) activeD.FullName selection.TopLine selection.Text 
                // This has the effect of moving the line and de-selecting it.
                selection.LineDown(false, 0)
                selection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstColumn, false)
            | :? TextSelection as selection ->
                showNoActivate()
                executeInteraction dbgBreak (System.IO.Path.GetDirectoryName(activeD.FullName)) activeD.FullName selection.TopLine selection.Text 
            | _ ->
                ()
        with
            e -> ()
                 // REVIEW: log error into Trace.
                 // Example errors include no active document.

    let onMLSendSelection (sender:obj) (e:EventArgs) =       
        sendSelectionToFSI ExecuteSelection

    let onMLSendLine (sender:obj) (e:EventArgs) =       
        sendSelectionToFSI ExecuteLine

    let onMLDebugSelection (sender:obj) (e:EventArgs) = 
        if checkDebuggability () then
            attachDebugger ()
        sendSelectionToFSI DebugSelection

    /// Handle UP and DOWN. Cycle history.    
    let onHistory (sender:obj) (e:EventArgs) =
        let command = sender :?> OleMenuCommand
        if null <> command && command.CommandID.Guid = typeof<VSConstants.VSStd2KCmdID>.GUID then
            // sanity check command and it's group
            let current = getInputAreaText()
            let nextO =
                if command.CommandID.ID = int32 VSConstants.VSStd2KCmdID.UP then
                    history.CycleUp(current)
                else if command.CommandID.ID = int32 VSConstants.VSStd2KCmdID.DOWN then
                    history.CycleDown(current)
                else
                    None
            match nextO with
              | None      -> ()
              | Some text -> setInputAreaText text
                             setScrollToEndOfBuffer()
                             setScrollToStartOfLine()
                
    let guidVSStd2KCmdID = typeof<VSConstants.VSStd2KCmdID>.GUID
    let guidVSStd97CmdID = typeof<VSConstants.VSStd97CmdID>.GUID
    
    let onCutDoCopy (_:obj) (_:EventArgs) =
        let oleCommandTarget = commandService :> IOleCommandTarget
        let mutable cmdSetGuid = guidVSStd97CmdID
        oleCommandTarget.Exec(&cmdSetGuid, uint32 VSStd97CmdID.Copy, 0u, IntPtr.Zero, IntPtr.Zero) |> ignore

    // Set the image that will appear on the tab of the window frame
    // when docked with an other window
    // The resource ID correspond to the one defined in the resx file
    // while the Index is the offset in the bitmap strip. Each image in
    // the strip being 16x16.
    do  this.BitmapResourceID <- 4200 
    do  this.BitmapIndex      <- 0  
    do  this.Caption          <- VFSIstrings.SR.fsharpInteractive()
   
    member this.MLSendSelection(obj,e) = onMLSendSelection obj e
    member this.MLSendLine(obj,e) = onMLSendLine obj e
    member this.MLDebugSelection(obj,e) = onMLDebugSelection obj e

    member this.GetDebuggerState() =
        let (state, _) = getDebuggerState ()
        state

    member this.AddReferences(references : string[]) = 
        let text = 
            references
            |> Array.map (sprintf "#r @\"%s\"")
            |> String.concat "\n"
        sendTextToFSI text
    
    override this.Dispose(disposing) =
        try 
            if disposing then
                sessions.Kill() 

                codeWinMan.Close()                                                         
                colorizer.Dispose()
                source.Dispose()
            
                if null <> commandService then
                    List.iter (fun mc -> (commandService :> MenuCommandService).RemoveCommand(mc)) commandList
                    commandService.Dispose()
                    commandService <- null
                                    
                // Q: Are explicit .Dispose() calls required for these objects? They are managed.
                if null <> textView then
                    textView.RemoveCommandFilter(this :> IOleCommandTarget) |> ignore                  
                    textView.CloseView() |> ignore                    
                    textView <- null
                if null <> textLines then
                    let persistDocData = textLines :?> IVsPersistDocData
                    persistDocData.Close() |> ignore
                    textLines <- null      
        finally
            base.Dispose(disposing)

    /// Function called when the window frame is set on this tool window.    
    override this.OnToolWindowCreated() =  
            base.OnToolWindowCreated()
            // Register this object as command filter for the text view so that it will be possible to intercept some command.

            let originalFilter = textView.AddCommandFilter(this :> IOleCommandTarget) |> throwOnFailure1
            // Create a command service that will use the previous command target
            // as parent target and will route to it the commands that it can not handle.
            if isNull commandService then
                commandService <-             
                    if isNull originalFilter then                    
                        new OleMenuCommandService(this)            
                    else
                        new OleMenuCommandService(this, originalFilter)

            let addCommand guid cmdId handler guard =
                let id  = new CommandID(guid,cmdId)
                let cmd = new OleMenuCommand(new EventHandler(handler),id)
                match guard with
                | None       -> ()
                | Some guard -> cmd.BeforeQueryStatus.AddHandler(new EventHandler(guard))
                commandService.AddCommand(cmd)
                commandList <- cmd :: commandList
                        
            //         GUID             commandID                                HandlerFun     OptionalGuardFun
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.RETURN)              onReturn       (Some supportUnlessCompleting)
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.BOL)                 onHome          None
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.BOL_EXT)             onShiftHome    (Some supportWhenInInputArea)
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.LEFT)                onNoAction     (Some supportWhenAtStartOfInputArea)
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.BACKSPACE)           onNoAction     (Some supportWhenAtStartOfInputAreaAndNoSelection)
            addCommand guidVSStd97CmdID (int32 VSStd97CmdID.Cut)                 onCutDoCopy    (Some supportWhenSelectionIntersectsWithReadonlyOrNoSelection)
            addCommand guidVSStd97CmdID (int32 VSStd97CmdID.ClearPane)           onClearPane     None
            addCommand guidVSStd2KCmdID (int32 VSStd2KCmdID.SHOWCONTEXTMENU)     showContextMenu None
            addCommand Guids.guidInteractiveCommands Guids.cmdIDSessionInterrupt onInterrupt     None
            addCommand Guids.guidInteractiveCommands Guids.cmdIDSessionRestart   onRestart       None
            addCommand Guids.guidFsiConsoleCmdSet Guids.cmdIDAttachDebugger      onAttachDebugger  None
            addCommand Guids.guidFsiConsoleCmdSet Guids.cmdIDDetachDebugger      onDetachDebugger  None
            
            addCommand Guids.guidInteractiveShell Guids.cmdIDSendSelection       onMLSendSelection   None
            addCommand Guids.guidInteractiveShell Guids.cmdIDSendLine            onMLSendLine        None

            addCommand Guids.guidInteractive Guids.cmdIDDebugSelection           onMLDebugSelection  None
            
            addCommand guidVSStd2KCmdID (int32 VSConstants.VSStd2KCmdID.UP)      onHistory      (Some supportWhenInInputArea)
            addCommand guidVSStd2KCmdID (int32 VSConstants.VSStd2KCmdID.DOWN)    onHistory      (Some supportWhenInInputArea)            
            // Now set the key binding for this frame to the same value as the text editor,
            // so that there will be the same mapping for the commands. [IronPython comment]
            let frame = this.Frame :?> IVsWindowFrame
            let CMDUIGUID_TextEditor = new Guid("{8B382828-6202-11d1-8870-0000F87579D2}") // Copied over from IP sample.
            let mutable commandUiGuid = CMDUIGUID_TextEditor
            let CMDUIGUID_ToolWindow = new Guid("{dee22b65-9761-4a26-8fb2-759b971d6dfc}")
            let mutable toolWindowGuid = CMDUIGUID_ToolWindow            
            frame.SetGuidProperty(int32 __VSFPROPID.VSFPROPID_InheritKeyBindings,&commandUiGuid) |> ignore
            frame.SetGuidProperty(int32 __VSFPROPID.VSFPROPID_CmdUIGuid, &toolWindowGuid) |> ignore
            let mutable obj = null
            frame.GetProperty(int32 __VSFPROPID.VSFPROPID_UserContext, &obj) |> ignore
            match obj with
            |   :? IVsUserContext as context ->
                    context.AddAttribute(VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_LookupF1, "Keyword", "VS.FSharpInteractive") |> ignore
            |   _ -> Debug.Assert(false)

    member _.QueryCommandStatus(guidCmdGroup:Guid, nCmdId:uint32) =
        match () with
        | _ when guidCmdGroup = Guids.guidFsiConsoleCmdSet && nCmdId = uint32 Guids.cmdIDAttachDebugger ->
            if debuggerIsRunning () then Some(OLECMDF.OLECMDF_INVISIBLE)
            else Some(OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED)

        | _ when guidCmdGroup = Guids.guidFsiConsoleCmdSet && nCmdId = uint32 Guids.cmdIDDetachDebugger ->
            if getDebugAttachedFSIProcess () |> Option.isSome then Some(OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED)
            else Some(OLECMDF.OLECMDF_INVISIBLE)

        | _ -> None
                 
    interface ITestVFSI with
        /// Send a string; the ';;' will be added to the end; does not interact with history
        member this.SendTextInteraction(s:string) =
            let dummyLineNum = 1
            executeInteraction false (System.IO.Path.GetTempPath()) "DummyTestFilename.fs" 1 s
        /// Returns the n most recent lines in the view.  After SendTextInteraction, can poll for a prompt to know when interaction finished.
        member this.GetMostRecentLines(n:int) : string[] =
            lock textLines (fun () ->
                try
                    let lineCount = ref 0
                    textLines.GetLineCount(&lineCount.contents) |> throwOnFailure0            

                    let lastLineLen = ref 0
                    textLines.GetLengthOfLine(!lineCount - 1, &lastLineLen.contents) |> throwOnFailure0            

                    let text = ref ""

                    // Cap number of lines returned to the total number of lines
                    let mutable startLine = max (!lineCount - 1 - n) 0
                    let mutable endLine   = max (!lineCount - 1) 0
                    let mutable startCol  = 0
                    let mutable endCol    = max (!lastLineLen - 1) 0

                    textLines.GetLineText(startLine, startCol, endLine, endCol, &text.contents) |> throwOnFailure0            
                    (!text).Split([|"\r\n"; "\r"; "\n"|], StringSplitOptions.RemoveEmptyEntries) 
                with 
                | ex -> 
                    let returnVal = [| "Unhandled Exception"; ex.Message |]
                    returnVal
            )
            
    interface IOleCommandTarget with
        member this.QueryStatus (guid, cCmds, prgCmds, pCmdText)=

            // Added to prevent command processing when the zoom control in the margin is focused
            let wpfTextView = textViewAdapter.GetWpfTextView(textView)

            // Can't search in the F# Interactive window
            // InterceptsCommandRouting property denotes whether this element requires normal input as opposed to VS commands
            // if InterceptsCommandRouting then command should be suppressed
            // this is necessary i.e. in case when focused element is search adornment (that has InterceptsCommandRouting=true)
            // in this case we need to stop command execution and let WPF do the processing
            let isFocusedElementInterceptsCommandRouting() = 
                // focus is not on textview - exit immediately
                if not wpfTextView.VisualElement.IsKeyboardFocusWithin then false
                else
                match System.Windows.Input.Keyboard.FocusedElement with
                | :? System.Windows.DependencyObject as focused -> Microsoft.VisualStudio.Editor.CommandRouting.GetInterceptsCommandRouting focused
                | _ -> false
            if not (wpfTextView.HasAggregateFocus) || isFocusedElementInterceptsCommandRouting() then
                (int Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED)
            else
                let mutable allHandled = true
                for i = 0 to ((int cCmds) - 1) do
                    match this.QueryCommandStatus(guid, prgCmds.[i].cmdID) with
                    | Some(commandStatus) ->
                        prgCmds.[i].cmdf <- uint32 commandStatus
                    | None ->
                        allHandled <- false

                if allHandled then 0 else
                let target : IOleCommandTarget = upcast commandService
                target.QueryStatus(&guid, cCmds, prgCmds, pCmdText)
       
        member this.Exec (guid, nCmdId, nCmdExcept, pIn, pOut) =
            let target : IOleCommandTarget = upcast commandService
               
            // for typing, Delete and Paste:
            // if either caret in not in the iput area or selection is not fully in the input area, remove selection and move caret
            // to the end of input area
            if (guid = guidVSStd2KCmdID && (nCmdId = uint32 VSStd2KCmdID.TYPECHAR || nCmdId = uint32 VSStd2KCmdID.DELETE)) ||
               (guid = guidVSStd97CmdID && (nCmdId = uint32 VSStd97CmdID.Delete || nCmdId = uint32 VSStd97CmdID.Paste))
                then            
                if not (isCurrentPositionInInputArea()) || isSelectionIntersectsWithReadonly() then               
                    setScrollToEndOfBuffer ()
                    setCursorAtEndOfBuffer()            
            target.Exec(&guid, nCmdId, nCmdExcept, pIn, pOut)

    /// Return the service of the given type.
    /// This override supplies a different command service from the one implemented in the base class.
    override this.GetService(serviceType:Type) =
        let intercept = typeof<IOleCommandTarget> = serviceType (*|| typeof<System.ComponentModel.Design.IMenuCommandService> = serviceType*)
        if intercept && null <> commandService then
            commandService |> box
        else
            base.GetService(serviceType)

    override this.PreProcessMessage msg =
        // we do not want to process any keyboard commands; all shortcut are prrocessed by standard VS command routing machanism
        false

    interface IVsUIElementPane with
        member this.CloseUIElementPane() =
            let mutable hr = VSConstants.S_OK
            if null <> textView then
                hr <- (textView :?> IVsUIElementPane).CloseUIElementPane()
            this.Dispose(true)
            hr

        member this.CreateUIElementPane o =
            (textView :?> IVsUIElementPane).CreateUIElementPane(&o)
            
        member this.GetDefaultUIElementSize(pSize:SIZE[]) =
            (textView :?> IVsUIElementPane).GetDefaultUIElementSize(pSize)

        member this.LoadUIElementState(pStream:IStream) =
            (textView :?> IVsUIElementPane).LoadUIElementState(pStream)

        member this.SaveUIElementState(pStream:IStream) =
            (textView :?> IVsUIElementPane).SaveUIElementState(pStream)

        member this.SetUIElementSite(psp:Microsoft.VisualStudio.OLE.Interop.IServiceProvider) =
            (textView :?> IVsUIElementPane).SetUIElementSite(psp)

        member this.TranslateUIElementAccelerator(lpmsg:MSG[]) =
            (textView :?> IVsUIElementPane).TranslateUIElementAccelerator(lpmsg)

    // This follows directly the IronPython sample.
    interface IVsWindowPane with
        member this.ClosePane() =         
            let mutable hr = VSConstants.S_OK
            if null <> textView then
                hr <- (textView :?> IVsWindowPane).ClosePane()
            this.Dispose(true)
            hr

        member this.CreatePaneWindow(hwndParent:IntPtr,x:int,y:int,cx:int,cy:int,hwnd:IntPtr byref) =
            (textView :?> IVsWindowPane).CreatePaneWindow(hwndParent, x, y, cx, cy, &hwnd)

        member this.GetDefaultSize(pSize:SIZE[]) =
            (textView :?> IVsWindowPane).GetDefaultSize(pSize)

        member this.LoadViewState(pStream:IStream) =
            (textView :?> IVsWindowPane).LoadViewState(pStream)

        member this.SaveViewState(pStream:IStream) =
            (textView :?> IVsWindowPane).SaveViewState(pStream)

        member this.SetSite(psp:Microsoft.VisualStudio.OLE.Interop.IServiceProvider) =
            (textView :?> IVsWindowPane).SetSite(psp)

        member this.TranslateAccelerator(lpmsg:MSG[]) =
            (textView :?> IVsWindowPane).TranslateAccelerator(lpmsg)


