// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.ComponentModel.Composition

open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.OLE.Interop
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.TextManager.Interop
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.FSharp.Interactive

type internal FsiCommandFilter(serviceProvider: System.IServiceProvider) =

    let loadPackage (guidString: string) : Lazy<Package?> =
      lazy(
        let shell = serviceProvider.GetService(typeof<SVsShell>) :?> IVsShell
        let packageToBeLoadedGuid = ref (Guid(guidString))       
        match shell.LoadPackage packageToBeLoadedGuid with
        | VSConstants.S_OK, pkg -> unbox pkg 
        | _ -> null)

    let fsiPackage = loadPackage FSharpConstants.fsiPackageGuidString

    let mutable nextTarget = null

    member x.AttachToViewAdapter (viewAdapter: IVsTextView) =
        match viewAdapter.AddCommandFilter x with
        | VSConstants.S_OK, next ->
            nextTarget <- next
        | errorCode, _ -> 
            ErrorHandler.ThrowOnFailure errorCode |> ignore

    interface IOleCommandTarget with
        member x.Exec (pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut) =
            if pguidCmdGroup = VSConstants.VsStd11 && nCmdId = uint32 VSConstants.VSStd11CmdID.ExecuteSelectionInInteractive then
                Hooks.OnMLSend fsiPackage.Value FsiEditorSendAction.ExecuteSelection null null
                VSConstants.S_OK
            elif pguidCmdGroup = VSConstants.VsStd11 && nCmdId = uint32 VSConstants.VSStd11CmdID.ExecuteLineInInteractive then
                Hooks.OnMLSend fsiPackage.Value FsiEditorSendAction.ExecuteLine null null
                VSConstants.S_OK
            elif pguidCmdGroup = Guids.guidInteractive && nCmdId = uint32 Guids.cmdIDDebugSelection then
                Hooks.OnMLSend fsiPackage.Value FsiEditorSendAction.DebugSelection null null
                VSConstants.S_OK
            elif not (isNull nextTarget) then
                nextTarget.Exec(&pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut)
            else
                VSConstants.E_FAIL

        member x.QueryStatus (pguidCmdGroup, cCmds, prgCmds, pCmdText) =
            if pguidCmdGroup = VSConstants.VsStd11 then
                for i = 0 to int cCmds-1 do
                    if prgCmds.[i].cmdID = uint32 VSConstants.VSStd11CmdID.ExecuteSelectionInInteractive then
                        prgCmds.[i].cmdf <- uint32 (OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED)
                    elif prgCmds.[i].cmdID = uint32 VSConstants.VSStd11CmdID.ExecuteLineInInteractive then
                        prgCmds.[i].cmdf <- uint32 (OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED ||| OLECMDF.OLECMDF_DEFHIDEONCTXTMENU)
                VSConstants.S_OK
            elif pguidCmdGroup = Guids.guidInteractive then
                for i = 0 to int cCmds-1 do
                    if prgCmds.[i].cmdID = uint32 Guids.cmdIDDebugSelection then
                        let dbgState = Hooks.GetDebuggerState fsiPackage.Value
                        if dbgState = FsiDebuggerState.AttachedNotToFSI then
                            prgCmds.[i].cmdf <- uint32 OLECMDF.OLECMDF_INVISIBLE
                        else
                            prgCmds.[i].cmdf <- uint32 (OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED)
                VSConstants.S_OK
            elif not (isNull nextTarget) then
                nextTarget.QueryStatus(&pguidCmdGroup, cCmds, prgCmds, pCmdText)
            else
                VSConstants.E_FAIL

[<Export(typeof<IWpfTextViewCreationListener>)>]
[<ContentType(FSharpConstants.FSharpContentTypeName)>]
[<TextViewRole(PredefinedTextViewRoles.PrimaryDocument)>]
type internal FsiCommandFilterProvider [<ImportingConstructor>] 
    ([<Import(typeof<SVsServiceProvider>)>] serviceProvider: System.IServiceProvider,
     editorFactory: IVsEditorAdaptersFactoryService) =
    interface IWpfTextViewCreationListener with
        member _.TextViewCreated(textView) = 
            match editorFactory.GetViewAdapter(textView) with
            | null -> ()
            | textViewAdapter ->
                let commandFilter = FsiCommandFilter serviceProvider
                commandFilter.AttachToViewAdapter textViewAdapter
        