// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Xml;
using System.Text;
using System.Net;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Used by a project to query the environment for permission to add, remove, or rename a file or directory in a solution
    /// </summary>
    internal class TrackDocumentsHelper
    {
        private ProjectNode projectMgr;

        public TrackDocumentsHelper(ProjectNode project)
        {
            this.projectMgr = project;
        }

        /// <summary>
        /// Gets the IVsTrackProjectDocuments2 object by asking the service provider for it.
        /// </summary>
        /// <returns>the IVsTrackProjectDocuments2 object</returns>
        private IVsTrackProjectDocuments2 GetIVsTrackProjectDocuments2()
        {
            Debug.Assert(this.projectMgr != null && !this.projectMgr.IsClosed && this.projectMgr.Site != null);

            IVsTrackProjectDocuments2 documentTracker = this.projectMgr.Site.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments2;
            if (documentTracker == null)
            {
                throw new InvalidOperationException();
            }

            return documentTracker;
        }

        /// <summary>
        /// Asks the environment for permission to add files.
        /// </summary>
        /// <param name="files">The files to add.</param>
        /// <param name="flags">The VSQUERYADDFILEFLAGS flags associated to the files added</param>
        /// <returns>true if the file can be added, false if not.</returns>
        public bool CanAddItems(string[] files, VSQUERYADDFILEFLAGS[] flags)
        {
            // If we are silent then we assume that the file can be added, since we do not want to trigger this event.
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) != 0)
            {
                return true;
            }

            if (files == null || files.Length == 0)
            {
                return false;
            }

            int len = files.Length;
            VSQUERYADDFILERESULTS[] summary = new VSQUERYADDFILERESULTS[1];
            ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnQueryAddFiles(projectMgr.InteropSafeIVsProject, len, files, flags, summary, null));
            if (summary[0] == VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddNotOK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Notify the environment about a file just added
        /// </summary>
        public void OnItemAdded(string file, VSADDFILEFLAGS flag)
        {
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) == 0)
            {
                ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnAfterAddFilesEx(projectMgr.InteropSafeIVsProject, 1, new string[1] { file }, new VSADDFILEFLAGS[1] { flag }));
            }
        }

        /// <summary>
        ///  Asks the environment for permission to remove files.
        /// </summary>
        /// <param name="files">an array of files to remove</param>
        /// <param name="flags">The VSQUERYREMOVEFILEFLAGS associated to the files to be removed.</param>
        /// <returns>true if the files can be removed, false if not.</returns>
        public bool CanRemoveItems(string[] files, VSQUERYREMOVEFILEFLAGS[] flags)
        {
            // If we are silent then we assume that the file can be removed, since we do not want to trigger this event.
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) != 0)
            {
                return true;
            }

            if (files == null || files.Length == 0)
            {
                return false;
            }
            int length = files.Length;

            VSQUERYREMOVEFILERESULTS[] summary = new VSQUERYREMOVEFILERESULTS[1];

            ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnQueryRemoveFiles(projectMgr.InteropSafeIVsProject, length, files, flags, summary, null));
            if (summary[0] == VSQUERYREMOVEFILERESULTS.VSQUERYREMOVEFILERESULTS_RemoveNotOK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Notify the environment about a file just removed
        /// </summary>
        public void OnItemRemoved(string file, VSREMOVEFILEFLAGS flag)
        {
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) == 0)
            {
                ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnAfterRemoveFiles(projectMgr.InteropSafeIVsProject, 1, new string[1] { file }, new VSREMOVEFILEFLAGS[1] { flag }));
            }
        }

        /// <summary>
        ///  Asks the environment for permission to rename files.
        /// </summary>
        /// <param name="oldFileName">Path to the file to be renamed.</param>
        /// <param name="newFileName">Path to the new file.</param>
        /// <param name="flag">The VSRENAMEFILEFLAGS associated with the file to be renamed.</param>
        /// <returns>true if the file can be renamed. Otherwise false.</returns>
        public bool CanRenameItem(string oldFileName, string newFileName, VSRENAMEFILEFLAGS flag)
        {
            // If we are silent then we assume that the file can be renamed, since we do not want to trigger this event.
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) != 0)
            {
                return true;
            }

            int iCanContinue = 0;
            ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnQueryRenameFile(projectMgr.InteropSafeIVsProject, oldFileName, newFileName, flag, out iCanContinue));
            return (iCanContinue != 0);
        }

        /// <summary>
        /// Get's called to tell the env that a file was renamed
        /// </summary>
        /// 
        public void OnItemRenamed(string strOldName, string strNewName, VSRENAMEFILEFLAGS flag)
        {
            if ((this.projectMgr.EventTriggeringFlag & ProjectNode.EventTriggering.DoNotTriggerTrackerEvents) == 0)
            {
                ErrorHandler.ThrowOnFailure(this.GetIVsTrackProjectDocuments2().OnAfterRenameFile(projectMgr.InteropSafeIVsProject, strOldName, strNewName, flag));
            }
        }
    }
}

