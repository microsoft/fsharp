// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Interactive

open System
open System.ComponentModel.Design
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.TextManager.Interop
open Util

module internal Hooks =
    let fsiServiceCreatorCallback(package:Package) = 
        new ServiceCreatorCallback(fun container typ ->
            if typ = typeof<FsiLanguageService> then
                let service = new FsiLanguageService()
                service.SetSite(box package)
                box service
            else
                null
        )

    // This should be called from the Package ctor, to do unsited initialisation.
    let fsiConsoleWindowPackageCtorUnsited (this:Package) =        

        // This seems an alternative to the boiler plate proffering above. Gives delayed creation?
        let callback  = fsiServiceCreatorCallback(this)
        let container = this :> IServiceContainer
        container.AddService(typeof<FsiLanguageService>, callback, true)       
        ()

    // Show the ToolWindow, e.g. as a result of the Menu button click.
    let ShowToolWindow (this:Package) (sender:obj) (e:EventArgs) =
        try
            // Get the instance number 0 of this tool window.
            // The window is single instance so this instance will be the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            let window = this.FindToolWindow(typeof<FsiToolWindow>, 0, true)
            if null = window || null = window.Frame then
                raise (new NotSupportedException(VFSIstrings.SR.cannotCreateToolWindow()))
            let windowFrame = window.Frame :?> IVsWindowFrame
            windowFrame.Show() |> throwOnFailure0            
        with e2 ->
            (System.Windows.Forms.MessageBox.Show(e2.ToString()) |> ignore)

    let private queryFSIToolWindow tryOpen (this:Package) (f:FsiToolWindow -> 't) (defaultValue:'t) =
        try            
            let window = this.FindToolWindow(typeof<FsiToolWindow>, 0, true)
            let windowFrame = window.Frame :?> IVsWindowFrame
            if tryOpen && windowFrame.IsVisible() <> VSConstants.S_OK then
                windowFrame.Show() |> throwOnFailure0
            match window with
            | :? FsiToolWindow as window -> f window
            | _ -> defaultValue
        with e ->
            (System.Windows.Forms.MessageBox.Show(VFSIstrings.SR.exceptionRaisedWhenRequestingToolWindow(e.ToString())) |> ignore)
            defaultValue

    let private withFSIToolWindow (this:Package) f =
        queryFSIToolWindow true this f ()

    let OnMLSend (this:Package) (action : FsiEditorSendAction) (sender:obj) (e:EventArgs?) =
        withFSIToolWindow this (fun window ->
            match action with
            | ExecuteSelection -> window.MLSendSelection(sender, e)
            | ExecuteLine -> window.MLSendLine(sender, e)
            | DebugSelection -> window.MLDebugSelection(sender, e)
        )

    let AddReferencesToFSI (this:Package) references =
        withFSIToolWindow this (fun window -> window.AddReferences references)

    let GetDebuggerState (this:Package) =
        queryFSIToolWindow false this (fun window -> window.GetDebuggerState()) FsiDebuggerState.AttachedNotToFSI

    // FxCop request this function not be public
    let private supportWhenFSharpDocument (sender:obj) (e:EventArgs) =    
        let command = sender :?> OleMenuCommand       
        if command <> null then                        
            let looksLikeFSharp,haveSelection = 
                try // catch all exceptions from this block
                    let providerGlobal   = Package.GetGlobalService(typeof<IOleServiceProvider>) :?> IOleServiceProvider
                    let provider         = new ServiceProvider(providerGlobal) :> System.IServiceProvider                    
                    let selectionMonitor = provider.GetService(typeof<IVsMonitorSelection>) :?> IVsMonitorSelection                    
                    // 
                    // Gets the currently selected Document Frame and projects out the DocView as a CodeWindow.
                    // This has TextLines and a LanguageService guid.
                    // Is this the fsharp language service guid? (the source file one, not the FSI one).
                    let docFrame   = selectionMonitor.GetCurrentElementValue(uint32 Constants.SEID_DocumentFrame) |> throwOnFailure1
                    let docFrame   = docFrame :?> IVsWindowFrame
                    let docView    = docFrame.GetProperty(int __VSFPROPID.VSFPROPID_DocView) |> throwOnFailure1
                    let codeWindow = docView :?> IVsCodeWindow
                    // Does the CodeWindow have an F# language service?
                    let looksLikeFSharp = 
                        let textLines  = codeWindow.GetBuffer() |> throwOnFailure1                    
                        let langGuid   = textLines.GetLanguageServiceID() |> throwOnFailure1
                        langGuid = Guids.guidFsharpLanguageService
                    // Is there a selection? (only relevant if it looks like F#)
                    let haveFSharpSelection =
                        looksLikeFSharp &&
                           (// do not proceed and get selection unless it's F#...
                            let textView   = codeWindow.GetPrimaryView() |> throwOnFailure1
                            let res,text   = textView.GetSelectedText()                    
                            res = VSConstants.S_OK && text <> "")
                    looksLikeFSharp,haveFSharpSelection                         
                with
                    e -> false,false
                    
            command.Supported  <- true
            command.Visible    <- looksLikeFSharp
            command.Enabled    <- true

    let mutable private hasBeenInitialized = false

    // This should be called from the Package.Initialize() override, to do sited initialisation.
    let fsiConsoleWindowPackageInitalizeSited (this:Package) (commandService : OleMenuCommandService) =
        if not hasBeenInitialized then
            hasBeenInitialized <- true
            if null <> commandService then

                // Create the command for the tool window
                let id  = new CommandID(Guids.guidFsiPackageCmdSet,int32 Guids.cmdIDLaunchFsiToolWindow)
                let cmd = new MenuCommand(new EventHandler(ShowToolWindow this), id)
                commandService.AddCommand(cmd)
