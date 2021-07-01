// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Runtime.InteropServices
open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.TextManager.Interop

/// Helper methods for interoperating with COM
module internal Com =      
    let ThrowOnFailure0(hr) = 
        ErrorHandler.ThrowOnFailure(hr)  |> ignore
        
    let ThrowOnFailure1(hr,res) = 
        ErrorHandler.ThrowOnFailure(hr) |> ignore
        res
        
    let ThrowOnFailure2(hr,res1,res2) = 
        ErrorHandler.ThrowOnFailure(hr) |> ignore
        res1,res2
        
    let ThrowOnFailure3(hr,res1,res2,res3) = 
        ErrorHandler.ThrowOnFailure(hr) |> ignore
        res1,res2,res3

    let ThrowOnFailure4(hr,res1,res2,res3,res4) = 
        ErrorHandler.ThrowOnFailure(hr) |> ignore
        res1,res2,res3,res4
        
    let Succeeded hr = 
        // REVIEW: Not the correct check for succeeded
        hr = VSConstants.S_OK

module internal VsUserData = 

    let vsBufferMoniker = Guid("978A8E17-4DF8-432A-9623-D530A26452BC")

    // This is the file name of the buffer.
    let GetBufferMonker(ud:IVsUserData) : string = 
        downcast Com.ThrowOnFailure1(ud.GetData(ref vsBufferMoniker))

module internal VsTextLines =
    /// Get the length of the given line.
    let LengthOfLine (buffer:IVsTextBuffer) (line:int) : int = 
        Com.ThrowOnFailure1(buffer.GetLengthOfLine(line))

    /// Get the text for a particular line.
    let LineText (buffer:IVsTextLines) line = 
        Com.ThrowOnFailure1(buffer.GetLineText(line, 0, line, LengthOfLine buffer line))

    /// Get the color state
    let TextColorState (buffer:IVsTextLines) : IVsTextColorState= unbox(box(buffer))

    /// Get the filename of the given buffer (via IVsUserData). Not all buffers have a file. This will be an exception.
    let GetFilename(buffer : IVsTextLines) =
        let ud = (box buffer) :?> IVsUserData
        VsUserData.GetBufferMonker(ud)

    /// Get the string contents of a given buffer (the current snapshot).
    let GetFileContents(buffer: IVsTextBuffer, editorAdaptersFactoryService: IVsEditorAdaptersFactoryService) =
        let dataBuffer = editorAdaptersFactoryService.GetDataBuffer(buffer)
        dataBuffer.CurrentSnapshot.GetText()

module internal VsRunningDocumentTable = 
    let FindDocumentWithoutLocking(rdt:IVsRunningDocumentTable, url:string) : (IVsHierarchy? * IVsTextLines?) option =
        let (hr:int, hier:IVsHierarchy?, _itemid:uint32, unkData:IntPtr, _cookie:uint32) = rdt.FindAndLockDocument(uint32 _VSRDTFLAGS.RDT_NoLock, url)
        try
            if Com.Succeeded(hr) then 
                let bufferObject = 
                    if unkData=IntPtr.Zero then null
                    else Marshal.GetObjectForIUnknown(unkData)
                let buffer : IVsTextLines? = 
                    match bufferObject with 
                    | :? IVsTextLines as tl -> tl
                    | _ -> null
                Some(hier, buffer)
            else None
        finally 
            if IntPtr.Zero <> unkData then Marshal.Release(unkData)|>ignore

[<AutoOpen>]
module internal ServiceProviderExtensions =
    type internal System.IServiceProvider with 
        member sp.GetService<'S,'T>() = sp.GetService(typeof<'S>) :?> 'T

        member sp.TextManager = sp.GetService<SVsTextManager, IVsTextManager>()
        member sp.RunningDocumentTable = sp.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable>()
        member sp.XMLMemberIndexService = sp.GetService<SVsXMLMemberIndexService, IVsXMLMemberIndexService>()
