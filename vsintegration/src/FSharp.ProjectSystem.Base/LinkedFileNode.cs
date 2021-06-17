// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// A file that may or may not be a linked file.
    /// </summary>
    internal class LinkedFileNode : FileNode
    {
        private bool _linkFile;

        /// <summary>
        /// Constructor for the LinkedFileNode
        /// </summary>
        /// <param name="root">Root of the hierarchy</param>
        /// <param name="e">Associated project element</param>
        /// <param name="hierarchyId">Optional hierarchy id - used during rename</param>
        internal LinkedFileNode(ProjectNode root, ProjectElement e, uint? hierarchyId = null)
            : base(root, e, hierarchyId)
        {
            this._linkFile = false;
        }

        /// <summary>
        /// Is this file a link file?
        /// </summary>
        public bool IsLinkFile
        {
            get { return _linkFile; }
        }

        /// <summary>
        /// Sets this file to linked or nonlinked.
        /// </summary>
        /// <param name="isLinked"></param>
        public void SetIsLinkedFile(bool isLinked)
        {
            this._linkFile = isLinked;
            this.ItemNode.RefreshProperties();
            this.ReDraw(UIHierarchyElement.Icon | UIHierarchyElement.SccState);
        }

        public override string Caption
        {
            get
            {
                string caption = this.ItemNode.GetMetadata(ProjectFileConstants.Link);
                if (string.IsNullOrEmpty(caption))
                {
                    return base.Caption;
                }
                return Path.GetFileName(caption);
            }
        }

        public override string GetEditLabel()
        {
            // prohibit renaming in linked files
            return IsLinkFile ? null : base.GetEditLabel();
        }

        /// <summary>
        /// Override GetProperty so we can support linked files.
        /// </summary>
        /// <param name="propId"></param>
        /// <returns></returns>
        public override object GetProperty(int propId)
        {
            switch ((__VSHPROPID)propId)
            {
                case __VSHPROPID.VSHPROPID_OverlayIconIndex:
                    {
                        // Valid range of Overlay State Icon (see vsshell.idl):
                        //  OVERLAYICON_NONE         = 0,   
                        //  OVERLAYICON_SHORTCUT     = 1,
                        //  OVERLAYICON_POLICY         = 2,
                        //  OVERLAYICON_CONNECTED     = 3,
                        //  OVERLAYICON_DISCONNECTED = 4,
                        //  OVERLAYICON_MAXINDEX     = 4 //should be same as last valid overlay
                        if (this.IsLinkFile)
                        {
                            return VSOVERLAYICON.OVERLAYICON_SHORTCUT;
                        }
                        return VSOVERLAYICON.OVERLAYICON_NONE;
                    }
            }

            __VSHPROPID2 id2 = (__VSHPROPID2)propId;
            switch (id2)
            {
                case __VSHPROPID2.VSHPROPID_IsLinkFile:
                    {
                        if (this.IsLinkFile)
                        {
                            return true;
                        }
                        return false;
                    }
            }

            return base.GetProperty(propId);
        }

        /// <summary>
        /// Specifies if a Node is under source control.  For
        /// files we remove any items which are linked or imported
        /// </summary>
        public override bool ExcludeNodeFromScc
        {
            get
            {
                if (IsLinkFile)
                    return true;
                if (IsImported)
                    return true;
                return base.ExcludeNodeFromScc;
            }
            set
            {
                base.ExcludeNodeFromScc = value;
            }
        }

        /// <summary>
        /// Used to determine if the node is imported via an MSBuild Import statement.
        /// </summary>
        public bool IsImported
        {
            get
            {
                if (ItemNode != null &&
                    ItemNode.Item != null)
                {
                    return ItemNode.Item.IsImported;
                }
                return false;
            }
        }

        public override int MenuCommandId
        {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        public override string RelativeFilePath
        {
            get
            {
                string link = this.ItemNode.GetMetadata(ProjectFileConstants.Link);
                if (string.IsNullOrEmpty(link))
                {
                    return base.RelativeFilePath;
                }
                return link;
            }
        }
    }
}
