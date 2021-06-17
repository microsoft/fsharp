namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Runtime.InteropServices
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.TextManager.Interop
open Microsoft.VisualStudio.Utilities
open Microsoft.VisualStudio.ComponentModelHost

type ShellPackage = Microsoft.VisualStudio.Shell.Package

[<RequireQualifiedAccess>]
module Constants =

    [<Literal>]
    let FSharpEditorFactoryIdString = "8a5aa6cf-46e3-4520-a70a-7393d15233e9"

    [<Literal>]
    let FSharpContentType = "F#"

    // _VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview = 2
    // F# doesn't allow to express a cast of an enum as a literal; we have to put the number here directly.
    [<Literal>]
    let FSharpEditorFactoryPhysicalViewAttributes = 2

    [<Literal>]
    let FSharpAnalysisSaveFileHandler = "FSharp Analysis Save File Handler"

[<Guid(Constants.FSharpEditorFactoryIdString)>]
type FSharpEditorFactory(parentPackage: ShellPackage) =

    let serviceProvider = 
        if parentPackage = null then
            nullArg "parentPackage"
        parentPackage :> IServiceProvider
    let componentModel = serviceProvider.GetService(typeof<SComponentModel>) :?> IComponentModel
    let editorAdaptersFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>()
    let contentTypeRegistryService = componentModel.GetService<IContentTypeRegistryService>()

    let setWindowBuffer oleServiceProvider (textBuffer: IVsTextBuffer) (ppunkDocView: byref<nativeint>) (ppunkDocData: byref<nativeint>) (pbstrEditorCaption: byref<string>) =   
        // If the text buffer is marked as read-only, ensure that the padlock icon is displayed
        // next the new window's title and that [Read Only] is appended to title.
        let readOnlyFlags = 
            (BUFFERSTATEFLAGS.BSF_FILESYS_READONLY ||| BUFFERSTATEFLAGS.BSF_USER_READONLY)
            |> LanguagePrimitives.EnumToValue 
            |> uint32

        let mutable textBufferFlags = 0u
        let readOnlyStatus = 
            if (ErrorHandler.Succeeded(textBuffer.GetStateFlags(&textBufferFlags)) && 0u <> (textBufferFlags &&& readOnlyFlags)) then
                READONLYSTATUS.ROSTATUS_ReadOnly
            else
                READONLYSTATUS.ROSTATUS_NotReadOnly
                
        let codeWindow = editorAdaptersFactoryService.CreateVsCodeWindowAdapter(oleServiceProvider);
        codeWindow.SetBuffer(textBuffer :?> IVsTextLines) 
        |> ignore
        codeWindow.GetEditorCaption(readOnlyStatus, &pbstrEditorCaption) 
        |> ignore

        ppunkDocView <- Marshal.GetIUnknownForObject(codeWindow);
        ppunkDocData <- Marshal.GetIUnknownForObject(textBuffer);

        VSConstants.S_OK;

    let mutable oleServiceProviderOpt = None
    
    interface IVsEditorFactory with

        member _.Close() = VSConstants.S_OK

        member _.CreateEditorInstance(_grfCreateDoc, _pszMkDocument, _pszPhysicalView, _pvHier, _itemid, punkDocDataExisting, ppunkDocView, ppunkDocData, pbstrEditorCaption, pguidCmdUI, pgrfCDW) =
            ppunkDocView <- IntPtr.Zero
            ppunkDocData <- IntPtr.Zero
            pbstrEditorCaption <- String.Empty

            //pguidCmdUI is the highest priority Guid that Visual Studio Shell looks at when translating key strokes into editor commands.
            //Here we intentionally set it to Guid.Empty so it will not play a part in translating keystrokes at all. The next highest priority 
            //will be commands tied to this FSharpEditorFactory (such as Alt-Enter).
            //However, because we are setting pguidCmdUI, we are not going to get typical text editor commands bound to this editor unless we inherit 
            //those keybindings on the IVsWindowFrame in which our editor lives.
            pguidCmdUI <- Guid.Empty
            pgrfCDW <- 0

            match oleServiceProviderOpt with
            | None -> VSConstants.E_FAIL
            | Some oleServiceProvider ->
                // Is this document already open? If so, let's see if it's a IVsTextBuffer we should re-use. This allows us
                // to properly handle multiple windows open for the same document.
                if punkDocDataExisting <> IntPtr.Zero then
                    match Marshal.GetObjectForIUnknown(punkDocDataExisting) with
                    | :? IVsTextBuffer as textBuffer -> 
                        setWindowBuffer oleServiceProvider textBuffer &ppunkDocView &ppunkDocData &pbstrEditorCaption
                    | _ -> 
                        VSConstants.VS_E_INCOMPATIBLEDOCDATA
                else
                    // We need to create a text buffer now.
                    let contentType = contentTypeRegistryService.GetContentType(Constants.FSharpContentType)
                    let textBuffer = editorAdaptersFactoryService.CreateVsTextBufferAdapter(oleServiceProvider, contentType)
                    setWindowBuffer oleServiceProvider textBuffer &ppunkDocView &ppunkDocData &pbstrEditorCaption

        member _.MapLogicalView(rguidLogicalView, pbstrPhysicalView) =
            pbstrPhysicalView <- null

            match rguidLogicalView with
            | x when 
                    x = VSConstants.LOGVIEWID.Primary_guid   ||
                    x = VSConstants.LOGVIEWID.Debugging_guid ||
                    x = VSConstants.LOGVIEWID.Code_guid      ||
                    x = VSConstants.LOGVIEWID.TextView_guid  ->
                VSConstants.S_OK
            | _ ->
                VSConstants.E_NOTIMPL

        member _.SetSite(packageServiceProvider) =
            oleServiceProviderOpt <- Some packageServiceProvider
            VSConstants.S_OK
        