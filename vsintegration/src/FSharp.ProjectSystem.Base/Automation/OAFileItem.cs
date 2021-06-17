// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.OLE.Interop;
using EnvDTE;
using VSConstants = Microsoft.VisualStudio.VSConstants;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.VisualStudio.FSharp.ProjectSystem;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    /// <summary>
    /// Represents an automation object for a file in a project
    /// </summary>
    [SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible")]
    [ComVisible(true), CLSCompliant(false)]
    public class OAFileItem : OAProjectItem<FileNode>
    {
        internal OAFileItem(OAProject project, FileNode node)
            : base(project, node)
        {
        }

        /// <summary>
        /// Returns the dirty state of the document.
        /// </summary>
        /// <exception cref="InvalidOperationException">Is thrown if the project is closed or it the service provider attached to the project is invalid.</exception>
        /// <exception cref="COMException">Is thrown if the dirty state cannot be retrived.</exception>
        public override bool IsDirty
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    if (this.Node == null || this.Node.ProjectMgr == null || this.Node.ProjectMgr.IsClosed || this.Node.ProjectMgr.Site == null)
                    {
                        throw new InvalidOperationException();
                    }
                    IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }
                    bool isDirty = false;
                    extensibility.EnterAutomationFunction();

                    try
                    {
                        DocumentManager manager = this.Node.GetDocumentManager();

                        if (manager == null)
                        {
                            throw new InvalidOperationException();
                        }

                        bool isOpen, isOpenedByUs;
                        uint docCookie;
                        IVsPersistDocData persistDocData;
                        manager.GetDocInfo(out isOpen, out isDirty, out isOpenedByUs, out docCookie, out persistDocData);
                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }
                    return isDirty;
                });
            }
        }

        /// <summary>
        /// Gets the Document associated with the item, if one exists.
        /// </summary>
        public override EnvDTE.Document Document
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    if (this.Node == null || this.Node.ProjectMgr == null || this.Node.ProjectMgr.IsClosed || this.Node.ProjectMgr.Site == null)
                    {
                        throw new InvalidOperationException();
                    }

                    IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }

                    EnvDTE.Document document = null;
                    extensibility.EnterAutomationFunction();

                    try
                    {
                        IVsUIHierarchy hier;
                        uint itemid;

                        IVsWindowFrame windowFrame;

                        VsShellUtilities.IsDocumentOpen(this.Node.ProjectMgr.Site, this.Node.Url, VSConstants.LOGVIEWID_Any, out hier, out itemid, out windowFrame);

                        if (windowFrame != null)
                        {
                            object var;
                            ErrorHandler.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocCookie, out var));
                            object documentAsObject;
                            extensibility.GetDocumentFromDocCookie((int)var, out documentAsObject);
                            if (documentAsObject == null)
                            {
                                throw new InvalidOperationException();
                            }
                            else
                            {
                                document = (Document)documentAsObject;
                            }
                        }

                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }

                    return document;
                });
            }
        }


        /// <summary>
        /// Opens the file item in the specified view.
        /// </summary>
        /// <param name="viewKind">Specifies the view kind in which to open the item (file)</param>
        /// <returns>Window object</returns>
        public override EnvDTE.Window Open(string viewKind)
        {
            return UIThread.DoOnUIThread(delegate() {
                if (this.Node == null || this.Node.ProjectMgr == null || this.Node.ProjectMgr.IsClosed || this.Node.ProjectMgr.Site == null)
                {
                    throw new InvalidOperationException();
                }

                // tell extensibility we are entering automation
                IServiceProvider serviceProvider = this.Node.ProjectMgr.Site;

                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                IVsWindowFrame windowFrame = null;
                IntPtr docData = IntPtr.Zero;
                extensibility.EnterAutomationFunction();

                try
                {
                    // Validate input params
                    Guid logicalViewGuid = VSConstants.LOGVIEWID_Primary;
                    try
                    {
                        if (!(String.IsNullOrEmpty(viewKind)))
                        {
                            logicalViewGuid = new Guid(viewKind);
                        }
                    }
                    catch (FormatException)
                    {
                        // Not a valid guid
                        throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidGuid, CultureInfo.CurrentUICulture), "viewKind");
                    }

                    uint itemid;
                    IVsHierarchy ivsHierarchy;
                    uint docCookie;
                    IVsRunningDocumentTable rdt = this.Node.ProjectMgr.Site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                    Debug.Assert(rdt != null, " Could not get running document table from the services exposed by this project");
                    if (rdt == null)
                    {
                        throw new InvalidOperationException();
                    }

                    ErrorHandler.ThrowOnFailure(rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, this.Node.Url, out ivsHierarchy, out itemid, out docData, out docCookie));

                    // Open the file using the IVsProject3 interface
                    ErrorHandler.ThrowOnFailure(this.Node.ProjectMgr.OpenItem(this.Node.ID, ref logicalViewGuid, docData, out windowFrame));

                }
                finally
                {
                    // make sure we tell extensibility that we have left automation
                    extensibility.ExitAutomationFunction();

                    if (docData != IntPtr.Zero)
                    {
                        Marshal.Release(docData);
                    }
                }

                // Get the automation object and return it
                return ((windowFrame != null) ? VsShellUtilities.GetWindowObject(windowFrame) : null);
            });
        }

        /// <summary>
        /// Saves the project item.
        /// </summary>
        /// <param name="fileName">The name with which to save the project or project item.</param>
        /// <exception cref="InvalidOperationException">Is thrown if the save operation failes.</exception>
        public override void Save(string fileName)
        {
            this.DoSave(false, fileName ?? string.Empty);
        }

        /// <summary>
        /// Saves the project item.
        /// </summary>
        /// <param name="fileName">The file name with which to save the solution, project, or project item. If the file exists, it is overwritten</param>
        /// <returns>true if the rename was successful. False if Save as failes</returns>
        /// <exception cref="ArgumentNullException">Is thrown if fileName is null.</exception>
        public override bool SaveAs(string fileName)
        {
            try
            {
                this.DoSave(true, fileName);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the project item is open in a particular view type. 
        /// </summary>
        /// <param name="viewKind">A Constants.vsViewKind* indicating the type of view to check.</param>
        /// <returns>A Boolean value indicating true if the project is open in the given view type; false if not. </returns>
        public override bool get_IsOpen(string viewKind)
        {
            return UIThread.DoOnUIThread(delegate() {
                if (this.Node == null || this.Node.ProjectMgr == null || this.Node.ProjectMgr.IsClosed || this.Node.ProjectMgr.Site == null)
                {
                    throw new InvalidOperationException();
                }

                // Validate input params
                Guid logicalViewGuid = VSConstants.LOGVIEWID_Primary;
                try
                {
                    if (!(String.IsNullOrEmpty(viewKind)))
                    {
                        logicalViewGuid = new Guid(viewKind);
                    }
                }
                catch (FormatException)
                {
                    // Not a valid guid
                    throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidGuid, CultureInfo.CurrentUICulture), "viewKind");
                }

                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                bool isOpen = false;
                extensibility.EnterAutomationFunction();

                try
                {
                    IVsUIHierarchy hier;
                    uint itemid;

                    IVsWindowFrame windowFrame;

                    isOpen = VsShellUtilities.IsDocumentOpen(this.Node.ProjectMgr.Site, this.Node.Url, logicalViewGuid, out hier, out itemid, out windowFrame);

                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }

                return isOpen;
            });
        }

        public override ProjectItems ProjectItems
        {
            get
            {
                var projectItems = base.ProjectItems;
                return UIThread.DoOnUIThread(delegate() {
                    if (this.Project.Project.CanFileNodesHaveChilds)
                        return new OAProjectItems(this.Project, this.Node);
                    else
                        return projectItems;
                });
            }
        }

        /// <summary>
        /// Saves or Save As the  file
        /// </summary>
        /// <param name="isCalledFromSaveAs">Flag determining which Save method called , the SaveAs or the Save.</param>
        /// <param name="fileName">The name of the project file.</param>        
        private void DoSave(bool isCalledFromSaveAs, string fileName)
        {
            UIThread.DoOnUIThread(delegate() {
                if (fileName == null)
                {
                    throw new ArgumentNullException("fileName");
                }

                if (this.Node == null || this.Node.ProjectMgr == null || this.Node.ProjectMgr.IsClosed || this.Node.ProjectMgr.Site == null)
                {
                    throw new InvalidOperationException();
                }

                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();
                IntPtr docData = IntPtr.Zero;

                try
                {
                    IVsRunningDocumentTable rdt = this.Node.ProjectMgr.Site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                    Debug.Assert(rdt != null, " Could not get running document table from the services exposed by this project");
                    if (rdt == null)
                    {
                        throw new InvalidOperationException();
                    }

                    // First we see if someone else has opened the requested view of the file.
                    uint itemid;
                    IVsHierarchy ivsHierarchy;
                    uint docCookie;
                    IntPtr projectPtr = IntPtr.Zero;
                    int canceled;
                    string url = this.Node.Url;

                    ErrorHandler.ThrowOnFailure(rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, url, out ivsHierarchy, out itemid, out docData, out docCookie));

                    // If an empty file name is passed in for Save then make the file name the project name.
                    if (!isCalledFromSaveAs && fileName.Length == 0)
                    {
                        ErrorHandler.ThrowOnFailure(this.Node.ProjectMgr.SaveItem(VSSAVEFLAGS.VSSAVE_SilentSave, url, this.Node.ID, docData, out canceled));
                    }
                    else
                    {
                        Utilities.ValidateFileName(this.Node.ProjectMgr.Site, fileName);

                        // Compute the fullpath from the directory of the existing Url.
                        string fullPath = fileName;
                        if (!Path.IsPathRooted(fileName))
                        {
                            string directory = Path.GetDirectoryName(url);
                            fullPath = Path.Combine(directory, fileName);
                        }

                        if (!isCalledFromSaveAs)
                        {
                            if (!NativeMethods.IsSamePath(this.Node.Url, fullPath))
                            {
                                throw new InvalidOperationException();
                            }

                            ErrorHandler.ThrowOnFailure(this.Node.ProjectMgr.SaveItem(VSSAVEFLAGS.VSSAVE_SilentSave, fullPath, this.Node.ID, docData, out canceled));
                        }
                        else
                        {
                            ErrorHandler.ThrowOnFailure(this.Node.ProjectMgr.SaveItem(VSSAVEFLAGS.VSSAVE_SilentSave, fullPath, this.Node.ID, docData, out canceled));
                        }
                    }

                    if (canceled == 1)
                    {
                        throw new InvalidOperationException();
                    }
                }
                catch (COMException e)
                {
                    throw new InvalidOperationException(e.Message);
                }
                finally
                {
                    try
                    {
                        extensibility.ExitAutomationFunction();
                    }
                    finally
                    {
                        if (docData != IntPtr.Zero)
                        {
                            Marshal.Release(docData);
                        }
                    }
                }
            });
        }

    }
}
