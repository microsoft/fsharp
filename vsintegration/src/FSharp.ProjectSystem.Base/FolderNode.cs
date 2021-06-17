// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    [CLSCompliant(false)]
    [ComVisible(true)]
    public class FolderNode : HierarchyNode
    {
        /// <summary>
        /// Constructor for the FolderNode
        /// </summary>
        /// <param name="root">Root node of the hierarchy</param>
        /// <param name="relativePath">relative path from root i.e.: "NewFolder1\\NewFolder2\\NewFolder3</param>
        /// <param name="element">Associated project element</param>
        internal FolderNode(ProjectNode root, string relativePath, ProjectElement element)
            : base(root, element)
        {
            this.VirtualNodeName = relativePath.TrimEnd('\\');
        }

        /// <summary>
        /// This relates to the SCC glyph
        /// </summary>
        public override VsStateIcon StateIconIndex
        {
            get
            {
                // The SCC manager does not support being asked for the state icon of a folder (result of the operation is undefined)
                return VsStateIcon.STATEICON_NOSTATEICON;
            }
        }

        public override NodeProperties CreatePropertiesObject()
        {
            return new FolderNodeProperties(this);
        }

        public override void DeleteFromStorage(string path)
        {
            this.DeleteFolder(path);
        }

        /// <summary>
        /// Get the automation object for the FolderNode
        /// </summary>
        /// <returns>An instance of the Automation.OAFolderNode type if succeeded</returns>
        public override object GetAutomationObject()
        {
            if (this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return null;
            }

            return new Automation.OAFolderItem(this.ProjectMgr.GetAutomationObject() as Automation.OAProject, this);
        }

        public override object GetIconHandle(bool open)
        {
            return this.ProjectMgr.ImageHandler.GetIconHandle(open ? (int)ProjectNode.ImageName.OpenFolder : (int)ProjectNode.ImageName.Folder);
        }

        /// <summary>
        /// Rename Folder
        /// </summary>
        /// <param name="label">new Name of Folder</param>
        /// <returns>VSConstants.S_OK, if succeeded</returns>
        public override int SetEditLabel(string label)
        {
            if (String.Compare(Path.GetFileName(this.Url.TrimEnd('\\')), label, StringComparison.Ordinal) == 0)
            {
                // Label matches current Name
                return VSConstants.S_OK;
            }

            string newPath = Path.Combine(new DirectoryInfo(this.Url).Parent.FullName, label);

            // Verify that No Directory/file already exists with the new name among siblings
            for (HierarchyNode n = Parent.FirstChild; n != null; n = n.NextSibling)
            {
                if (n != this && String.Compare(n.Caption, label, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return ShowErrorMessage(SR.FileOrFolderAlreadyExists, newPath);
                }
            }

            try
            {
                RenameFolder(label);

                //Refresh the properties in the properties window
                IVsUIShell shell = this.ProjectMgr.GetService(typeof(SVsUIShell)) as IVsUIShell;
                Debug.Assert(shell != null, "Could not get the ui shell from the project");
                ErrorHandler.ThrowOnFailure(shell.RefreshPropertyBrowser(0));

                // Notify the listeners that the name of this folder is changed. This will
                // also force a refresh of the SolutionExplorer's node.
                this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.RenameFolder, CultureInfo.CurrentUICulture), e.Message));
            }
            return VSConstants.S_OK;
        }


        public override int MenuCommandId
        {
            get { return VsMenus.IDM_VS_CTXT_FOLDERNODE; }
        }

        public override Guid ItemTypeGuid
        {
            get
            {
                return VSConstants.GUID_ItemType_PhysicalFolder;
            }
        }

        public override string Url
        {
            get
            {
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(this.ProjectMgr.Url), this.VirtualNodeName)) + "\\";
            }
        }

        public override string Caption
        {
            get
            {
                // it might have a backslash at the end... 
                // and it might consist of Grandparent\parent\this\
                string caption = this.VirtualNodeName;
                string[] parts;
                parts = caption.Split(Path.DirectorySeparatorChar);
                caption = parts[parts.GetUpperBound(0)];
                return caption;
            }
        }

        public override string GetMkDocument()
        {
            Debug.Assert(this.Url != null, "No url sepcified for this node");

            return this.Url;
        }

        /// <summary>
        /// Enumerate the files associated with this node.
        /// A folder node is not a file and as such no file to enumerate.
        /// </summary>
        /// <param name="files">The list of files to be placed under source control.</param>
        /// <param name="flags">The flags that are associated to the files.</param>
        public override void GetSccFiles(System.Collections.Generic.IList<string> files, System.Collections.Generic.IList<tagVsSccFilesFlags> flags)
        {
            return;
        }

        /// <summary>
        /// This method should be overridden to provide the list of special files and associated flags for source control.
        /// </summary>
        /// <param name="sccFile">One of the file associated to the node.</param>
        /// <param name="files">The list of files to be placed under source control.</param>
        /// <param name="flags">The flags that are associated to the files.</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "scc")]
        public override void GetSccSpecialFiles(string sccFile, IList<string> files, IList<tagVsSccFilesFlags> flags)
        {
            if (this.ExcludeNodeFromScc)
            {
                return;
            }

            if (files == null)
            {
                throw new ArgumentNullException("files");
            }

            if (flags == null)
            {
                throw new ArgumentNullException("flags");
            }

            if (string.IsNullOrEmpty(sccFile))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "sccFile");
            }
        }

        /// <summary>
        /// Recursevily walks the folder nodes and redraws the state icons
        /// </summary>
        public override void UpdateSccStateIcons()
        {
            for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
            {
                child.UpdateSccStateIcons();
            }
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch ((VsCommands)cmd)
                {
                    case VsCommands.Copy:
                    case VsCommands.Paste:
                    case VsCommands.Cut:
                    case VsCommands.Rename:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands.NewFolder:
                    case VsCommands.AddNewItem:
                    case VsCommands.AddExistingItem:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                if ((VsCommands2K)cmd == VsCommands2K.EXCLUDEFROMPROJECT)
                {
                    result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                    return VSConstants.S_OK;
                }
                if ((VsCommands2K) cmd == VsMenus.OpenFolderInExplorerCmdId)
                {
                    result |= QueryStatusResult.SUPPORTED;
                    result |= CanOpenFolderInExplorer()? QueryStatusResult.ENABLED : QueryStatusResult.INVISIBLE;
                    return VSConstants.S_OK;
                }
            }
            else
            {
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public bool CanOpenFolderInExplorer()
        {
            return Directory.Exists(this.Url);
        }

        public void OpenFolderInExplorer()
        {
            if (CanOpenFolderInExplorer())
                Process.Start(this.Url);
        }

        public override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                if ((VsCommands2K) cmd == VsMenus.OpenFolderInExplorerCmdId)
                {
                    OpenFolderInExplorer();
                    return VSConstants.S_OK;
                }
            }

            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        public override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage)
            {
                return this.ProjectMgr.CanProjectDeleteItems;
            }
            return false;
        }

        /// <summary>
        /// Override if your node is not a file system folder so that
        /// it does nothing or it deletes it from your storage location.
        /// </summary>
        /// <param name="path">Path to the folder to delete</param>
        public virtual void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        /// <summary>
        /// creates the physical directory for a folder node
        /// Override if your node does not use file system folder
        /// </summary>
        public virtual void CreateDirectory()
        {
            try
            {
                if (Directory.Exists(this.Url) == false)
                {
                    Directory.CreateDirectory(this.Url);
                }
            }
            //TODO - this should not digest all exceptions.
            catch (System.Exception e)
            {
                CCITracing.Trace(e);
                throw;
            }
        }
        /// <summary>
        /// Creates a folder nodes physical directory
        /// Override if your node does not use file system folder
        /// </summary>
        /// <param name="newName"></param>
        /// <returns></returns>
        public virtual void CreateDirectory(string newName)
        {
            if (String.IsNullOrEmpty(newName))
            {
                throw new ArgumentException(SR.GetString(SR.ParameterCannotBeNullOrEmpty, CultureInfo.CurrentUICulture), "newName");
            }

            try
            {
                // on a new dir && enter, we get called with the same name (so do nothing if name is the same
                char[] dummy = new char[1];
                dummy[0] = Path.DirectorySeparatorChar;
                string oldDir = this.Url;
                oldDir = oldDir.TrimEnd(dummy);
                string strNewDir = Path.Combine(Path.GetDirectoryName(oldDir), newName);

                if (String.Compare(strNewDir, oldDir, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    if (Directory.Exists(strNewDir))
                    {
                        throw new InvalidOperationException(SR.GetString(SR.DirectoryExistError, CultureInfo.CurrentUICulture));
                    }
                    Directory.CreateDirectory(strNewDir);
                }
            }
            //TODO - this should not digest all exceptions.
            catch (System.Exception e)
            {
                CCITracing.Trace(e);
                throw;
            }
        }

        /// <summary>
        /// Rename the physical directory for a folder node
        /// Override if your node does not use file system folder
        /// </summary>
        /// <returns></returns>
        public virtual void RenameDirectory(string newPath)
        {
            if (Directory.Exists(this.Url))
            {
                if (Directory.Exists(newPath))
                {
                    ShowErrorMessage(SR.FileOrFolderAlreadyExists, newPath);
                    return;
                }

                Directory.Move(this.Url, newPath);
            }
        }

        private void RenameFolder(string newName)
        {
            string newPath = Path.Combine(this.Parent.VirtualNodeName, newName);
            string newFullPath = Path.Combine(this.ProjectMgr.ProjectFolder, newPath);

            // Only do the physical rename if the leaf name changed
            if (String.Compare(Path.GetFileName(VirtualNodeName), newName, StringComparison.Ordinal) != 0)
            {
                // Verify that no directory/file already exists with the new name on disk.
                // If it does, just subsume that name if our directory is empty.
                if (Directory.Exists(newFullPath) || File.Exists(newFullPath))
                {
                    // We can't delete our old directory as it is not empty
                    if (Directory.EnumerateFileSystemEntries(this.Url).Any())
                    {
                        ShowErrorMessage(SR.FolderCannotBeRenamed, newPath);
                        return;
                    }

                    // Try to delete the old (empty) directory.
                    // Note that we don't want to delete recursively in case a file was added between
                    // when we checked and when we went to delete (potential race condition).
                    Directory.Delete(this.Url, false);
                }
                else
                {
                    this.RenameDirectory(newFullPath);
                }
            }

            this.VirtualNodeName = newPath;

            this.ItemNode.Rename(VirtualNodeName);

            // Let all children know of the new path
            for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
            {
                if (!(child is FolderNode))
                {
                    child.SetEditLabel(child.Caption);
                }
                else
                {
                    FolderNode childFolderNode = (FolderNode)child;
                    childFolderNode.RenameFolder(childFolderNode.Caption);
                }
            }

            // Some of the previous operation may have changed the selection so set it back to us
            IVsUIHierarchyWindow uiWindow = UIHierarchyUtilities.GetUIHierarchyWindow(this.ProjectMgr.Site, SolutionExplorer);
            ErrorHandler.ThrowOnFailure(uiWindow.ExpandItem(this.ProjectMgr.InteropSafeIVsUIHierarchy, this.ID, EXPANDFLAGS.EXPF_SelectItem));

        }

        /// <summary>
        /// Show error message if not in automation mode, otherwise throw exception
        /// </summary>
        /// <param name="parameter">Parameter for resource string format</param>
        /// <returns>S_OK</returns>
        private int ShowErrorMessage(string resourceName, string parameter)
        {
            // Most likely the cause of:
            // A file or folder with the name '{0}' already exists on disk at this location. Please choose another name.
            // -or-
            // This folder cannot be renamed to '{0}' as it already exists on disk.
            string errorMessage = String.Format(CultureInfo.CurrentCulture, SR.GetStringWithCR(resourceName), parameter);
            if (!Utilities.IsInAutomationFunction(this.ProjectMgr.Site))
            {
                string title = null;
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, errorMessage, icon, buttons, defaultButton);
                return VSConstants.S_OK;
            }
            else
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
