// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.OLE.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using IOleDataObject = Microsoft.VisualStudio.OLE.Interop.IDataObject;
using System.Security.Permissions;
using System.Globalization;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Manages the CopyPaste and Drag and Drop scenarios for a Project.
    /// </summary>
    /// <remarks>This is a partial class.</remarks>
    public partial class ProjectNode : IVsUIHierWinClipboardHelperEvents
    {
        private uint copyPasteCookie;
        private DropDataType dropDataType;

        /// <summary>
        /// Called as soon as the mouse drags an item over a new hierarchy or hierarchy window
        /// </summary>
        /// <param name="pDataObject">reference to interface IDataObject of the item being dragged</param>
        /// <param name="grfKeyState">Current state of the keyboard and the mouse modifier keys. See docs for a list of possible values</param>
        /// <param name="itemid">Item identifier for the item currently being dragged</param>
        /// <param name="pdwEffect">On entry, a pointer to the current DropEffect. On return, must contain the new valid DropEffect</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public override int DragEnter(IOleDataObject pDataObject, uint grfKeyState, uint itemid, ref uint pdwEffect)
        {            
            pdwEffect = (uint)DropEffect.None;

            this.dropDataType = QueryDropDataType(pDataObject);
            if (this.dropDataType != DropDataType.None)
            {
                pdwEffect = (uint)this.QueryDropEffect(this.dropDataType, grfKeyState);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when one or more items are dragged out of the hierarchy or hierarchy window, or when the drag-and-drop operation is cancelled or completed.
        /// </summary>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public override int DragLeave()
        {
            this.dropDataType = DropDataType.None;
            return VSConstants.S_OK;
        }
        
        /// <summary>
        /// Called when one or more items are dragged over the target hierarchy or hierarchy window. 
        /// </summary>
        /// <param name="grfKeyState">Current state of the keyboard keys and the mouse modifier buttons. See <seealso cref="IVsHierarchyDropDataTarget"/></param>
        /// <param name="itemid">Item identifier of the drop data target over which the item is being dragged</param>
        /// <param name="pdwEffect"> On entry, reference to the value of the pdwEffect parameter of the IVsHierarchy object, identifying all effects that the hierarchy supports. 
        /// On return, the pdwEffect parameter must contain one of the effect flags that indicate the result of the drop operation. For a list of pwdEffects values, see <seealso cref="DragEnter"/></param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public override int DragOver(uint grfKeyState, uint itemid, ref uint pdwEffect)
        {
            pdwEffect = (uint)DropEffect.None;

            // Dragging items to a project that is being debugged is not supported
            // (see VSWhidbey 144785)            
            DBGMODE dbgMode = VsShellUtilities.GetDebugMode(this.Site) & ~DBGMODE.DBGMODE_EncMask;
            if (dbgMode == DBGMODE.DBGMODE_Run || dbgMode == DBGMODE.DBGMODE_Break)
            {
                return VSConstants.S_OK;
            }

            if (this.isClosed || this.site == null)
            {
                return VSConstants.E_UNEXPECTED;
            }

            // We should also analyze if the node being dragged over can accept the drop.
            if (!this.CanTargetNodeAcceptDrop(itemid))
            {
                return VSConstants.E_NOTIMPL;
            }

            if (this.dropDataType != DropDataType.None)
            {
                pdwEffect = (uint)this.QueryDropEffect(this.dropDataType, grfKeyState);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when one or more items are dropped into the target hierarchy or hierarchy window when the mouse button is released.
        /// </summary>
        /// <param name="pDataObject">Reference to the IDataObject interface on the item being dragged. This data object contains the data being transferred in the drag-and-drop operation. 
        /// If the drop occurs, then this data object (item) is incorporated into the target hierarchy or hierarchy window.</param>
        /// <param name="grfKeyState">Current state of the keyboard and the mouse modifier keys. See <seealso cref="IVsHierarchyDropDataTarget"/></param>
        /// <param name="itemid">Item identifier of the drop data target over which the item is being dragged</param>
        /// <param name="pdwEffect">Visual effects associated with the drag-and drop-operation, such as a cursor, bitmap, and so on. 
        /// The value of dwEffects passed to the source object via the OnDropNotify method is the value of pdwEffects returned by the Drop method</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public override int Drop(IOleDataObject pDataObject, uint grfKeyState, uint itemid, ref uint pdwEffect)
        {
            if (pDataObject == null)
            {
                return VSConstants.E_INVALIDARG;
            }

            pdwEffect = (uint)DropEffect.None;

            // Get the node that is being dragged over and ask it which node should handle this call
            HierarchyNode targetNode = NodeFromItemId(itemid);
            if (targetNode != null)
            {
                targetNode = targetNode.GetDragTargetHandlerNode();
            }
            else
            {
                // There is no target node. The drop can not be completed.
                return VSConstants.S_FALSE;
            }

            int returnValue;
            try
            {
                DropDataType dropDataType = DropDataType.None;
                dropDataType = ProcessSelectionDataObject(pDataObject, targetNode);
                pdwEffect = (uint)this.QueryDropEffect(dropDataType, grfKeyState);

                // If it is a drop from windows and we get any kind of error we return S_FALSE and dropeffect none. This
                // prevents bogus messages from the shell from being displayed
                returnValue = (dropDataType != DropDataType.Shell) ? VSConstants.E_FAIL : VSConstants.S_OK;
            }
            catch (System.IO.FileNotFoundException e)
            {
                Trace.WriteLine("Exception : " + e.Message);

                if (!Utilities.IsInAutomationFunction(this.Site))
                {
                    string message = e.Message;
                    string title = string.Empty;
                    OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                    OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                    OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                    VsShellUtilities.ShowMessageBox(this.Site, title, message, icon, buttons, defaultButton);
                }

                returnValue = VSConstants.E_FAIL;
            }

            return returnValue;
        }

        /// <summary>
        /// Returns information about one or more of the items being dragged
        /// </summary>
        /// <param name="pdwOKEffects">Pointer to a DWORD value describing the effects displayed while the item is being dragged, 
        /// such as cursor icons that change during the drag-and-drop operation. 
        /// For example, if the item is dragged over an invalid target point 
        /// (such as the item's original location), the cursor icon changes to a circle with a line through it. 
        /// Similarly, if the item is dragged over a valid target point, the cursor icon changes to a file or folder.</param>
        /// <param name="ppDataObject">Pointer to the IDataObject interface on the item being dragged. 
        /// This data object contains the data being transferred in the drag-and-drop operation. 
        /// If the drop occurs, then this data object (item) is incorporated into the target hierarchy or hierarchy window.</param>
        /// <param name="ppDropSource">Pointer to the IDropSource interface of the item being dragged.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public override int GetDropInfo(out uint pdwOKEffects, out IOleDataObject ppDataObject, out IDropSource ppDropSource)
        {
            //init out params
            pdwOKEffects = (uint)DropEffect.None;
            ppDataObject = null;
            ppDropSource = null;

            IOleDataObject dataObject = PackageSelectionDataObject(false);
            if (dataObject == null)
            {
                return VSConstants.E_NOTIMPL;
            }

            this.SourceDraggedOrCutOrCopied = true;

            pdwOKEffects = (uint)(DropEffect.Move | DropEffect.Copy);

            ppDataObject = dataObject;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Notifies clients that the dragged item was dropped. 
        /// </summary>
        /// <param name="fDropped">If true, then the dragged item was dropped on the target. If false, then the drop did not occur.</param>
        /// <param name="dwEffects">Visual effects associated with the drag-and-drop operation, such as cursors, bitmaps, and so on. 
        /// The value of dwEffects passed to the source object via OnDropNotify method is the value of pdwEffects returned by Drop method.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public override int OnDropNotify(int fDropped, uint dwEffects)
        {
            if (!this.SourceDraggedOrCutOrCopied)
            {
                return VSConstants.S_FALSE;
            }

            this.CleanupSelectionDataObject(fDropped != 0, false, dwEffects == (uint)DropEffect.Move);

            this.SourceDraggedOrCutOrCopied = false;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Allows the drag source to prompt to save unsaved items being dropped. 
        /// Notifies the source hierarchy that information dragged from it is about to be dropped on a target. 
        /// This method is called immediately after the mouse button is released on a drop. 
        /// </summary>
        /// <param name="o">Reference to the IDataObject interface on the item being dragged. 
        /// This data object contains the data being transferred in the drag-and-drop operation. 
        /// If the drop occurs, then this data object (item) is incorporated into the hierarchy window of the new hierarchy.</param>
        /// <param name="dwEffect">Current state of the keyboard and the mouse modifier keys.</param>
        /// <param name="fCancelDrop">If true, then the drop is cancelled by the source hierarchy. If false, then the drop can continue.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public override int OnBeforeDropNotify(IOleDataObject o, uint dwEffect, out int fCancelDrop)
        {
            // If there is nothing to be dropped just return that drop should be cancelled.
            if (this.ItemsDraggedOrCutOrCopied == null)
            {
                fCancelDrop = 1;
                return VSConstants.S_OK;
            }
            
            fCancelDrop = 0;
            bool dirty = false;
            foreach (HierarchyNode node in this.ItemsDraggedOrCutOrCopied)
            {
                bool isDirty, isOpen, isOpenedByUs;
                uint docCookie;
                IVsPersistDocData ppIVsPersistDocData;
                DocumentManager manager = node.GetDocumentManager();
                if (manager != null)
                {
                    manager.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
                    if (isDirty && isOpenedByUs)
                    {
                        dirty = true;
                        break;
                    }
                }
            }

            // if there are no dirty docs we are ok to proceed
            if (!dirty)
            {
                return VSConstants.S_OK;
            }

            // Prompt to save if there are dirty docs
            string message = SR.GetString(SR.SaveModifiedDocuments, CultureInfo.CurrentUICulture);
            string title = string.Empty;
            OLEMSGICON icon = OLEMSGICON.OLEMSGICON_WARNING;
            OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL;
            OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
            int result = VsShellUtilities.ShowMessageBox(Site, title, message, icon, buttons, defaultButton);
            switch (result)
            {
                case NativeMethods.IDYES:
                    break;

                case NativeMethods.IDNO:
                    return VSConstants.S_OK;

                case NativeMethods.IDCANCEL: goto default;

                default:
                    fCancelDrop = 1;
                    return VSConstants.S_OK;
            }

            // Save all dirty documents
            foreach (HierarchyNode node in this.ItemsDraggedOrCutOrCopied)
            {
                DocumentManager manager = node.GetDocumentManager();
                if (manager != null)
                {
                    manager.Save(true);
                }
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called after your cut/copied items has been pasted
        /// </summary>
        ///<param name="wasCut">If true, then the IDataObject has been successfully pasted into a target hierarchy. 
        /// If false, then the cut or copy operation was cancelled.</param>
        /// <param name="dropEffect">Visual effects associated with the drag and drop operation, such as cursors, bitmaps, and so on. 
        /// These should be the same visual effects used in OnDropNotify</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int OnPaste(int wasCut, uint dropEffect)
        {
            if (!this.SourceDraggedOrCutOrCopied)
            {
                return VSConstants.S_FALSE;
            }

            if (dropEffect == (uint)DropEffect.None)
            {
                return OnClear(wasCut);
            }

            this.CleanupSelectionDataObject(false, wasCut != 0, dropEffect == (uint)DropEffect.Move);
            this.SourceDraggedOrCutOrCopied = false;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when your cut/copied operation is canceled
        /// </summary>
        /// <param name="wasCut">This flag informs the source that the Cut method was called (true), 
        /// rather than Copy (false), so the source knows whether to "un-cut-highlight" the items that were cut.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int OnClear(int wasCut)
        {
            if (!this.SourceDraggedOrCutOrCopied)
            {
                return VSConstants.S_FALSE;
            }

            this.CleanupSelectionDataObject(false, wasCut != 0, false, true);
            this.SourceDraggedOrCutOrCopied = false;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Determines if a node can accept drop opertaion.
        /// </summary>
        /// <param name="itemId">The id of the node.</param>
        /// <returns>true if the node acceots drag operation.</returns>
        public virtual bool CanTargetNodeAcceptDrop(uint itemId)
        {
            HierarchyNode targetNode = NodeFromItemId(itemId);
            if (targetNode is ReferenceContainerNode || targetNode is ReferenceNode)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Returns a dataobject from selected nodes
        /// </summary>
        /// <param name="cutHighlightItems">boolean that defines if the selected items must be cut</param>
        /// <returns>data object for selected items</returns>
        internal virtual DataObject PackageSelectionDataObject(bool cutHighlightItems)
        {
            this.CleanupSelectionDataObject(false, false, false);
            StringBuilder sb = new StringBuilder();

            DataObject dataObject = null;

            try
            {
                IList<HierarchyNode> selectedNodes = this.GetSelectedNodes();
                if (selectedNodes != null)
                {
                    this.InstantiateItemsDraggedOrCutOrCopiedList();

                    StringBuilder selectionContent = null;

                    // If there is a selection package the data
                    if (selectedNodes.Count > 1)
                    {
                        foreach (HierarchyNode node in selectedNodes)
                        {
                            selectionContent = node.PrepareSelectedNodesForClipBoard();
                            if (selectionContent != null)
                            {
                                sb.Append(selectionContent);
                            }
                        }
                    }
                    else if (selectedNodes.Count == 1)
                    {
                        HierarchyNode selectedNode = selectedNodes[0];
                        selectionContent = selectedNode.PrepareSelectedNodesForClipBoard();
                        if (selectionContent != null)
                        {
                            sb.Append(selectionContent);
                        }
                    }
                }

                // Add the project items first.
                IntPtr ptrToItems = this.PackageSelectionData(sb, false);
                if (ptrToItems == IntPtr.Zero)
                {
                    return null;
                }

                FORMATETC fmt = DragDropHelper.CreateFormatEtc(DragDropHelper.CF_VSSTGPROJECTITEMS);
                dataObject = new DataObject();
                dataObject.SetData(fmt, ptrToItems);

                // Now add the project path that sourced data. We just write the project file path.
                IntPtr ptrToProjectPath = this.PackageSelectionData(new StringBuilder(this.GetMkDocument()), true);

                if (ptrToProjectPath != IntPtr.Zero)
                {
                    dataObject.SetData(DragDropHelper.CreateFormatEtc(DragDropHelper.CF_VSPROJECTCLIPDESCRIPTOR), ptrToProjectPath);
                }

                if (cutHighlightItems)
                {
                    bool first = true;
                    IVsUIHierarchyWindow w = UIHierarchyUtilities.GetUIHierarchyWindow(this.site, HierarchyNode.SolutionExplorer);

                    foreach (HierarchyNode node in this.ItemsDraggedOrCutOrCopied)
                    {
                        ErrorHandler.ThrowOnFailure(w.ExpandItem((IVsUIHierarchy)this, node.ID, first ? EXPANDFLAGS.EXPF_CutHighlightItem : EXPANDFLAGS.EXPF_AddCutHighlightItem));
                        first = false;
                    }
                }
            }
            catch (COMException e)
            {
                Trace.WriteLine("Exception : " + e.Message);

                dataObject = null;
            }

            return dataObject;
        }


        /// <summary>
        /// This is used to recursively add a folder from an other project.
        /// Note that while we copy the folder content completely, we only
        /// add to the project items which are part of the source project.
        /// </summary>
        /// <param name="folderToAdd">Project reference (from data object) using the format: {Guid}|project|folderPath</param>
        /// <param name="targetNode">Node to add the new folder to</param>
        private void AddFolderFromOtherProject(string folderToAdd, HierarchyNode targetNode)
        {
            if (String.IsNullOrEmpty(folderToAdd))
                throw new ArgumentNullException("folderToAdd");
            if (targetNode == null)
                throw new ArgumentNullException("targetNode");

            // Split the reference in its 3 parts
            int index1 = Guid.Empty.ToString("B").Length;
            if (index1 + 1 >= folderToAdd.Length)
                throw new ArgumentException("folderToAdd");

            // Get the Guid
            string guidString = folderToAdd.Substring(1, index1 - 2);
            Guid projectInstanceGuid = new Guid(guidString);

            // Get the project path
            int index2 = folderToAdd.IndexOf('|', index1 + 1);
            if (index2 < 0 || index2 + 1 >= folderToAdd.Length)
                throw new ArgumentException("folderToAdd");

            // Finally get the source path
            string folder = folderToAdd.Substring(index2 + 1);

            // Get the target path
            string folderName = Path.GetFileName(Path.GetDirectoryName(folder));
            string targetPath = Path.Combine(GetBaseDirectoryForAddingFiles(targetNode), folderName);

            // Recursively copy the directory to the new location
            Utilities.RecursivelyCopyDirectory(folder, targetPath);

            // Retrieve the project from which the items are being copied
            IVsHierarchy sourceHierarchy;
            IVsSolution solution = (IVsSolution)GetService(typeof(SVsSolution));
            ErrorHandler.ThrowOnFailure(solution.GetProjectOfGuid(ref projectInstanceGuid, out sourceHierarchy));

            // Then retrieve the item ID of the item to copy
            uint itemID = VSConstants.VSITEMID_ROOT;
            ErrorHandler.ThrowOnFailure(sourceHierarchy.ParseCanonicalName(folder, out itemID));

            // Ensure we don't end up in an endless recursion
            if (Utilities.IsSameComObject(this, sourceHierarchy))
            {
                HierarchyNode cursorNode = targetNode;
                while (cursorNode != null)
                {
                    if (String.Compare(folder, cursorNode.GetMkDocument(), StringComparison.OrdinalIgnoreCase) == 0)
                        throw new ApplicationException();
                    cursorNode = cursorNode.Parent;
                }
            }

            // Now walk the source project hierarchy to see which node needs to be added.
            WalkSourceProjectAndAdd(sourceHierarchy, itemID, targetNode, false);
        }

        /// <summary>
        /// Recursive method that walk a hierarchy and add items it find to our project.
        /// Note that this is meant as an helper to the Copy&amp;Paste/Drag&amp;Drop functionality.
        /// </summary>
        /// <param name="sourceHierarchy">Hierarchy to walk</param>
        /// <param name="itemId">Item ID where to start walking the hierarchy</param>
        /// <param name="targetNode">Node to start adding to</param>
        /// <param name="addSiblings">Typically false on first call and true after that</param>
        public virtual void WalkSourceProjectAndAdd(IVsHierarchy sourceHierarchy, uint itemId, HierarchyNode targetNode, bool addSiblings)
        {
            // Before we start the walk, add the current node
            object variant = null;
            HierarchyNode newNode = targetNode;
            if (itemId != VSConstants.VSITEMID_NIL)
            {
                // Calculate the corresponding path in our project
                string source;
                ErrorHandler.ThrowOnFailure(((IVsProject)sourceHierarchy).GetMkDocument(itemId, out source));
                string name = Path.GetFileName(source.TrimEnd(new char[] { '/', '\\' }));
                string targetPath = Path.Combine(GetBaseDirectoryForAddingFiles(targetNode), name);

                // See if this is a linked item (file can be linked, not folders)
                ErrorHandler.ThrowOnFailure(sourceHierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_BrowseObject, out variant), VSConstants.E_NOTIMPL);
                VSLangProj.FileProperties fileProperties = variant as VSLangProj.FileProperties;
                if (fileProperties != null && fileProperties.IsLink)
                {
                    // Since we don't support linked item, we make a copy of the file into our storage where it would have been linked
                    File.Copy(source, targetPath, true);
                }

                newNode = AddNodeIfTargetExistInStorage(targetNode, name, targetPath);


                // Start with child nodes (depth first)
                variant = null;
                ErrorHandler.ThrowOnFailure(sourceHierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out variant));
                uint currentItemID = (uint)(int)variant;
                WalkSourceProjectAndAdd(sourceHierarchy, currentItemID, newNode, true);

                if (addSiblings)
                {
                    // Then look at siblings
                    currentItemID = itemId;
                    while (currentItemID != VSConstants.VSITEMID_NIL)
                    {
                        variant = null;
                        ErrorHandler.ThrowOnFailure(sourceHierarchy.GetProperty(currentItemID, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out variant));
                        currentItemID = (uint)(int)variant;
                        WalkSourceProjectAndAdd(sourceHierarchy, currentItemID, targetNode, true);
                    }
                }
            }
        }

        /// <summary>
        /// Add an existing item (file/folder) to the project if it already exist in our storage.
        /// </summary>
        /// <param name="parentNode">Node to that this item to</param>
        /// <param name="name">Name of the item being added</param>
        /// <param name="targetPath">Path of the item being added</param>
        /// <returns>Node that was added</returns>
        public virtual HierarchyNode AddNodeIfTargetExistInStorage(HierarchyNode parentNode, string name, string targetPath)
        {
            HierarchyNode newNode = parentNode;
            // If the file/directory exist, add a node for it
            if (File.Exists(targetPath))
            {
                VSADDRESULT[] result = new VSADDRESULT[1];
                ErrorHandler.ThrowOnFailure(this.AddItem(parentNode.ID, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, name, 1, new string[] { targetPath }, IntPtr.Zero, result));
                if (result[0] != VSADDRESULT.ADDRESULT_Success)
                    throw new ApplicationException();
                newNode = this.FindChild(targetPath);
                if (newNode == null)
                    throw new ApplicationException();
            }
            else if (Directory.Exists(targetPath))
            {
                newNode = this.CreateFolderNodes(targetPath);
            }
            return newNode;
        }    

        /// <summary>
        /// Handle the Cut operation to the clipboard
        /// </summary>
        public override int CutToClipboard()
        {
            int returnValue = (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            try
            {
                this.RegisterClipboardNotifications(true);

                // Create our data object and change the selection to show item(s) being cut
                IOleDataObject dataObject = this.PackageSelectionDataObject(true);
                if (dataObject != null)
                {
                    this.SourceDraggedOrCutOrCopied = true;

                    // Add our cut item(s) to the clipboard
                    ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleSetClipboard(dataObject));

                    // Inform VS (UiHierarchyWindow) of the cut
                    IVsUIHierWinClipboardHelper clipboardHelper = (IVsUIHierWinClipboardHelper)GetService(typeof(SVsUIHierWinClipboardHelper));
                    if (clipboardHelper == null)
                    {
                        return VSConstants.E_FAIL;
                    }
                    
                    returnValue = ErrorHandler.ThrowOnFailure(clipboardHelper.Cut(dataObject));
                }
            }
            catch (COMException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
                returnValue = e.ErrorCode;
            }

            return returnValue;
        }

        /// <summary>
        /// Handle the Copy operation to the clipboard
        /// </summary>
        public override int CopyToClipboard()
        {
            int returnValue = (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
            try
            {
                this.RegisterClipboardNotifications(true);

                // Create our data object and change the selection to show item(s) being copy
                IOleDataObject dataObject = this.PackageSelectionDataObject(false);
                if (dataObject != null)
                {
                    this.SourceDraggedOrCutOrCopied = true;

                    // Add our copy item(s) to the clipboard
                    ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleSetClipboard(dataObject));

                    // Inform VS (UiHierarchyWindow) of the copy
                    IVsUIHierWinClipboardHelper clipboardHelper = (IVsUIHierWinClipboardHelper)GetService(typeof(SVsUIHierWinClipboardHelper));
                    if (clipboardHelper == null)
                    {
                        return VSConstants.E_FAIL;
                    }
                    returnValue = ErrorHandler.ThrowOnFailure(clipboardHelper.Copy(dataObject));
                }
            }
            catch (COMException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
                returnValue = e.ErrorCode;
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
                returnValue = Marshal.GetHRForException(e);
            }

            return returnValue;
        }

        /// <summary>
        /// Handle the Paste operation to a targetNode
        /// </summary>
        public override int PasteFromClipboard(HierarchyNode targetNode)
        {
            int returnValue = (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;

            //Get the clipboardhelper service and use it after processing dataobject
            IVsUIHierWinClipboardHelper clipboardHelper = (IVsUIHierWinClipboardHelper)GetService(typeof(SVsUIHierWinClipboardHelper));
            if (clipboardHelper == null)
            {
                return VSConstants.E_FAIL;
            }

            try
            {
                //Get dataobject from clipboard
                IOleDataObject dataObject = null;
                ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleGetClipboard(out dataObject));
                if (dataObject == null)
                {
                    return VSConstants.E_UNEXPECTED;
                }

                DropEffect dropEffect = DropEffect.None;
                DropDataType dropDataType = DropDataType.None;
                try
                {
                    dropDataType = this.ProcessSelectionDataObject(dataObject, targetNode.GetDragTargetHandlerNode());
                    dropEffect = this.QueryDropEffect(dropDataType, 0);
                }
                catch (ExternalException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);

                    // If it is a drop from windows and we get any kind of error ignore it. This
                    // prevents bogus messages from the shell from being displayed
                    if (dropDataType != DropDataType.Shell)
                    {
                        throw;
                    }
                }
                finally
                {
                    // Inform VS (UiHierarchyWindow) of the paste
                    returnValue = clipboardHelper.Paste(dataObject, (uint)dropEffect);
                }
            }
            catch (COMException e)
            {
                Trace.WriteLine("Exception : " + e.Message);

                returnValue = e.ErrorCode;
            }

            return returnValue;
        }

        /// <summary>
        /// Determines if the paste command should be allowed.
        /// </summary>
        /// <returns></returns>
        public override bool AllowPasteCommand()
        {
            IOleDataObject dataObject = null;
            try
            {
                ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleGetClipboard(out dataObject));
                if (dataObject == null)
                {
                    return false;
                }

                // First see if this is a set of storage based items
                FORMATETC format = DragDropHelper.CreateFormatEtc((ushort)DragDropHelper.CF_VSSTGPROJECTITEMS);
                if (dataObject.QueryGetData(new FORMATETC[] { format }) == VSConstants.S_OK)
                    return true;
                // Try reference based items
                format = DragDropHelper.CreateFormatEtc((ushort)DragDropHelper.CF_VSREFPROJECTITEMS);
                if (dataObject.QueryGetData(new FORMATETC[] { format }) == VSConstants.S_OK)
                    return true;
                // Try windows explorer files format
                format = DragDropHelper.CreateFormatEtc((ushort)NativeMethods.CF_HDROP);
                return (dataObject.QueryGetData(new FORMATETC[] { format }) == VSConstants.S_OK);
            }
            // We catch External exceptions since it might be that it is not our data on the clipboard.
            catch (ExternalException e)
            {
                Trace.WriteLine("Exception :" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Register/Unregister for Clipboard events for the UiHierarchyWindow (solution explorer)
        /// </summary>
        /// <param name="register">true for register, false for unregister</param>
        public override void RegisterClipboardNotifications(bool register)
        {
            // Get the UiHierarchy window clipboard helper service
            IVsUIHierWinClipboardHelper clipboardHelper = (IVsUIHierWinClipboardHelper)GetService(typeof(SVsUIHierWinClipboardHelper));
            if (clipboardHelper == null)
            {
                return;
            }

            if (register && this.copyPasteCookie == 0)
            {
                // Register
                ErrorHandler.ThrowOnFailure(clipboardHelper.AdviseClipboardHelperEvents(this, out this.copyPasteCookie));
                Debug.Assert(this.copyPasteCookie != 0, "AdviseClipboardHelperEvents returned an invalid cookie");
            }
            else if (!register && this.copyPasteCookie != 0)
            {
                // Unregister
                ErrorHandler.ThrowOnFailure(clipboardHelper.UnadviseClipboardHelperEvents(this.copyPasteCookie));
                this.copyPasteCookie = 0;
            }
        }

        /// <summary>
        /// Process dataobject from Drag/Drop/Cut/Copy/Paste operation
        /// </summary>
        /// <remarks>The targetNode is set if the method is called from a drop operation, otherwise it is null</remarks>
        internal DropDataType ProcessSelectionDataObject(IOleDataObject dataObject, HierarchyNode targetNode)
        {
            DropDataType dropDataType = DropDataType.None;
            bool isWindowsFormat = false;

            // Try to get it as a directory based project.
            List<string> filesDropped = DragDropHelper.GetDroppedFiles(DragDropHelper.CF_VSSTGPROJECTITEMS, dataObject, out dropDataType);
            if (filesDropped.Count == 0)
            {
                filesDropped = DragDropHelper.GetDroppedFiles(DragDropHelper.CF_VSREFPROJECTITEMS, dataObject, out dropDataType);
            }
            if (filesDropped.Count == 0)
            {
                filesDropped = DragDropHelper.GetDroppedFiles(NativeMethods.CF_HDROP, dataObject, out dropDataType);
                isWindowsFormat = (filesDropped.Count > 0);
            }

            if (dropDataType != DropDataType.None && filesDropped.Count > 0)
            {
                string[] filesDroppedAsArray = filesDropped.ToArray();

                HierarchyNode node = (targetNode == null) ? this : targetNode;

                // For directory based projects the content of the clipboard is a double-NULL terminated list of Projref strings.
                if (isWindowsFormat)
                {
                    // This is the code path when source is windows explorer
                    VSADDRESULT[] vsaddresults = new VSADDRESULT[1];
                    vsaddresults[0] = VSADDRESULT.ADDRESULT_Failure;
                    int addResult = AddItem(node.ID, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, null, (uint)filesDropped.Count, filesDroppedAsArray, IntPtr.Zero, vsaddresults);
                    if (addResult != VSConstants.S_OK && addResult != VSConstants.S_FALSE && addResult != (int)OleConstants.OLECMDERR_E_CANCELED
                        && vsaddresults[0] != VSADDRESULT.ADDRESULT_Success)
                    {
                        ErrorHandler.ThrowOnFailure(addResult);
                    }
                    
                    return dropDataType;
                }
                else
                {
                    if (AddFilesFromProjectReferences(node, filesDroppedAsArray))
                    {
                        return dropDataType;
                    }
                }
            }

            // If we reached this point then the drop data must be set to None.
            // Otherwise the OnPaste will be called with a valid DropData and that would actually delete the item.
            return DropDataType.None;
        }

        /// <summary>
        /// Get the dropdatatype from the dataobject
        /// </summary>
        /// <param name="pDataObject">The dataobject to be analysed for its format</param>
        /// <returns>dropdatatype or none if dataobject does not contain known format</returns>
        internal static DropDataType QueryDropDataType(IOleDataObject pDataObject)
        {
            if (pDataObject == null)
            {
                return DropDataType.None;
            }
            
            // known formats include File Drops (as from WindowsExplorer),
            // VSProject Reference Items and VSProject Storage Items.
            FORMATETC fmt = DragDropHelper.CreateFormatEtc(NativeMethods.CF_HDROP);

            if (DragDropHelper.QueryGetData(pDataObject, ref fmt) == VSConstants.S_OK)
            {
                return DropDataType.Shell;
            }

            fmt.cfFormat = DragDropHelper.CF_VSREFPROJECTITEMS;
            if (DragDropHelper.QueryGetData(pDataObject, ref fmt) == VSConstants.S_OK)
            {
                // Data is from a Ref-based project.
                return DropDataType.VsRef;
            }

            fmt.cfFormat = DragDropHelper.CF_VSSTGPROJECTITEMS;
            if (DragDropHelper.QueryGetData(pDataObject, ref fmt) == VSConstants.S_OK)
            {
                return DropDataType.VsStg;
            }

            return DropDataType.None;
        }

        /// <summary>
        /// Returns the drop effect.
        /// </summary>
        /// <remarks>
        /// // A directory based project should perform as follow:
        ///        NO MODIFIER 
        ///            - COPY if not from current hierarchy, 
        ///            - MOVE if from current hierarchy
        ///        SHIFT DRAG - MOVE
        ///        CTRL DRAG - COPY
        ///        CTRL-SHIFT DRAG - NO DROP (used for reference based projects only)
        /// </remarks>
        internal DropEffect QueryDropEffect(DropDataType dropDataType, uint grfKeyState)
        {
            //Validate the dropdatatype
            if ((dropDataType != DropDataType.Shell) && (dropDataType != DropDataType.VsRef) && (dropDataType != DropDataType.VsStg))
            {
                return DropEffect.None;
            }

            // CTRL-SHIFT
            if ((grfKeyState & NativeMethods.MK_CONTROL) != 0 && (grfKeyState & NativeMethods.MK_SHIFT) != 0)
            {
                // Because we are not referenced base, we don't support link
                return DropEffect.None;
            }

            // CTRL
            if ((grfKeyState & NativeMethods.MK_CONTROL) != 0)
                return DropEffect.Copy;

            // SHIFT
            if ((grfKeyState & NativeMethods.MK_SHIFT) != 0)
                return DropEffect.Move;

            // no modifier
            if (this.SourceDraggedOrCutOrCopied)
            {
                return DropEffect.Move;
            }
            else
            {
                return DropEffect.Copy;
            }
        }

        public void CleanupSelectionDataObject(bool dropped, bool cut, bool moved)
        {
            this.CleanupSelectionDataObject(dropped, cut, moved, false);
        }

        /// <summary>
        ///  After a drop or paste, will use the dwEffects 
        ///  to determine whether we need to clean up the source nodes or not. If
        ///  justCleanup is set, it only does the cleanup work.
        /// </summary>
        public void CleanupSelectionDataObject(bool dropped, bool cut, bool moved, bool justCleanup)
        {
            if (this.ItemsDraggedOrCutOrCopied == null || this.ItemsDraggedOrCutOrCopied.Count == 0)
            {
                return;
            }

            try
            {
                IVsUIHierarchyWindow w = UIHierarchyUtilities.GetUIHierarchyWindow(this.site, HierarchyNode.SolutionExplorer);
                foreach (HierarchyNode node in this.ItemsDraggedOrCutOrCopied)
                {
                    if ((moved && (cut || dropped) && !justCleanup))
                    {
                        // do not close it if the doc is dirty or we do not own it
                        bool isDirty, isOpen, isOpenedByUs;
                        uint docCookie;
                        IVsPersistDocData ppIVsPersistDocData;
                        DocumentManager manager = node.GetDocumentManager();
                        if (manager != null)
                        {
                            manager.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out ppIVsPersistDocData);
                            if (isDirty || (isOpen && !isOpenedByUs))
                            {
                                continue;
                            }

                            // close it if opened
                            if (isOpen)
                            {
                                manager.Close(__FRAMECLOSE.FRAMECLOSE_NoSave);
                            }
                        }

                        node.Remove(removeFromStorage: true, promptSave: false);
                    }
                    else if (w != null)
                    {
                        ErrorHandler.ThrowOnFailure(w.ExpandItem((IVsUIHierarchy)this, node.ID, EXPANDFLAGS.EXPF_UnCutHighlightItem));
                    }
                }
            }
            finally
            {
                try
                {
                    // Now delete the memory allocated by the packaging of datasources.
                    // If we just did a cut, or we are told to cleanup, then we need to free the data object. Otherwise, we leave it
                    // alone so that you can continue to paste the data in new locations.
                    if (moved || cut || justCleanup)
                    {
                        this.ItemsDraggedOrCutOrCopied.Clear();
                        this.CleanAndFlushClipboard();
                    }
                }
                finally
                {
                    this.dropDataType = DropDataType.None;
                }
            }
        }

        /// <summary>
        /// Moves files from one part of our project to another.
        /// </summary>
        /// <param name="targetNode">the targetHandler node</param>
        /// <param name="projectReferences">List of projectref string</param>
        /// <returns>true if succeeded</returns>
        private bool AddFilesFromProjectReferences(HierarchyNode targetNode, string[] projectReferences)
        {
            //Validate input
            if (projectReferences == null)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "projectReferences");
            }
            if (targetNode == null)
            {
                throw new InvalidOperationException();
            }

            //Iteratively add files from projectref
            foreach (string projectReference in projectReferences)
            {
                if (projectReference == null)
                {
                    // bad projectref, bail out
                    return false;
                }
                if (projectReference.EndsWith("/", StringComparison.Ordinal) || projectReference.EndsWith("\\", StringComparison.Ordinal))
                {
                    AddFolderFromOtherProject(projectReference, targetNode);
                }
                else if (!AddFileToNodeFromProjectReference(projectReference, targetNode))
                {
                    return false;
                }
            }

            return true;
        }
        /// <summary>
        /// Adds an item from a project refererence to target node.
        /// </summary>
        /// <param name="projectRef"></param>
        /// <param name="targetNode"></param>
        private bool AddFileToNodeFromProjectReference(string projectRef, HierarchyNode targetNode)
        {
            if (String.IsNullOrEmpty(projectRef))
            {
                throw new ArgumentException(SR.GetString(SR.ParameterCannotBeNullOrEmpty, CultureInfo.CurrentUICulture), "projectRef");
            }

            if (targetNode == null)
            {
                throw new ArgumentNullException("targetNode");
            }

            IVsSolution solution = this.GetService(typeof(IVsSolution)) as IVsSolution;
            if (solution == null)
            {
                throw new InvalidOperationException();
            }

            uint itemidLoc;
            IVsHierarchy hierarchy;
            string str;
            VSUPDATEPROJREFREASON[] reason = new VSUPDATEPROJREFREASON[1];
            ErrorHandler.ThrowOnFailure(solution.GetItemOfProjref(projectRef, out hierarchy, out itemidLoc, out str, reason));
            if (hierarchy == null)
            {
                throw new InvalidOperationException();
            }

            // This will throw invalid cast exception if the hierrachy is not a project.
            IVsProject project = (IVsProject)hierarchy;

            string moniker;
            ErrorHandler.ThrowOnFailure(project.GetMkDocument(itemidLoc, out moniker));
            string[] files = new String[1] { moniker };
            VSADDRESULT[] vsaddresult = new VSADDRESULT[1];
            vsaddresult[0] = VSADDRESULT.ADDRESULT_Failure;
            int addResult = targetNode.ProjectMgr.DoAddItem(targetNode.ID, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, null, 0, files, IntPtr.Zero, vsaddresult, AddItemContext.Paste);
            if (addResult != VSConstants.S_OK && addResult != VSConstants.S_FALSE && addResult != (int)OleConstants.OLECMDERR_E_CANCELED)
            {
                ErrorHandler.ThrowOnFailure(addResult);
                return false;
            }
            return (vsaddresult[0] == VSADDRESULT.ADDRESULT_Success);
        }



        /// <summary>
        /// Empties all the data structures added to the clipboard and flushes the clipboard.
        /// </summary>
        private void CleanAndFlushClipboard()
        {
            IOleDataObject oleDataObject = null;
            ErrorHandler.ThrowOnFailure(UnsafeNativeMethods.OleGetClipboard(out oleDataObject));
            if (oleDataObject == null)
            {
                return;
            }


            string sourceProjectPath = DragDropHelper.GetSourceProjectPath(oleDataObject);

            if (!String.IsNullOrEmpty(sourceProjectPath) && NativeMethods.IsSamePath(sourceProjectPath, this.GetMkDocument()))
            {
                UnsafeNativeMethods.OleFlushClipboard();
                int clipboardOpened = 0;
                try
                {
                    clipboardOpened = UnsafeNativeMethods.OpenClipboard(IntPtr.Zero);
                    UnsafeNativeMethods.EmptyClipboard();
                }
                finally
                {
                    if (clipboardOpened == 1)
                    {
                        UnsafeNativeMethods.CloseClipboard();
                    }
                }
            }
        }

        private IntPtr PackageSelectionData(StringBuilder sb, bool addEndFormatDelimiter)
        {
            if (sb == null || sb.ToString().Length == 0 || this.ItemsDraggedOrCutOrCopied.Count == 0)
            {
                return IntPtr.Zero;
            }

            // Double null at end.
            if (addEndFormatDelimiter)
            {
                if (sb.ToString()[sb.Length - 1] != '\0')
                {
                    sb.Append('\0');
                }
            }

            // We request unmanaged permission to execute the below.
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();

            _DROPFILES df = new _DROPFILES();
            int dwSize = Marshal.SizeOf(df);
            Int16 wideChar = 0;
            int dwChar = Marshal.SizeOf(wideChar);
            int structSize = dwSize + ((sb.Length + 1) * dwChar);
            IntPtr ptr = Marshal.AllocHGlobal(structSize);
            df.pFiles = dwSize;
            df.fWide = 1;
            IntPtr data = IntPtr.Zero;
            try
            {
                data = UnsafeNativeMethods.GlobalLock(ptr);
                Marshal.StructureToPtr(df, data, false);
                IntPtr strData = new IntPtr((long)data + dwSize);
                DragDropHelper.CopyStringToHGlobal(sb.ToString(), strData, structSize);
            }
            finally
            {
                if (data != IntPtr.Zero)
                    UnsafeNativeMethods.GlobalUnLock(data);
            }

            return ptr;
        }
    }
}
