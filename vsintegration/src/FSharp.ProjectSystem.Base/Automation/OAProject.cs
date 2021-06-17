// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Reflection;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.OLE.Interop;
using EnvDTE;
using System.Globalization;
using Microsoft.VisualStudio.FSharp.ProjectSystem;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    [ComVisible(true), CLSCompliant(false)]
    public class OAProject : EnvDTE.Project, EnvDTE.ISupportVSProperties
    {
        private ProjectNode project;
        EnvDTE.ConfigurationManager configurationManager;

        public ProjectNode Project
        {
            get { return this.project; }
        }

        internal OAProject(ProjectNode project)
        {
            this.project = project;
        }

        public virtual string Name
        {
            get
            {
                return project.Caption;
            }
            set
            {
                if (this.project == null || this.project.Site == null || this.project.IsClosed)
                {
                    throw new InvalidOperationException();
                }
                UIThread.DoOnUIThread(delegate() {
                    IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }

                    extensibility.EnterAutomationFunction();

                    try
                    {
                        project.SetEditLabel(value);
                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }
                });
            }
        }

        /// <summary>
        /// For use by F# tooling only.  Gets the file name of the project.
        /// </summary>
        public virtual string FileName
        {
            get
            {
                // 4763: Aligning with C#/VB
                return this.FullName;
            }
        }

        /// <summary>
        /// For use by F# tooling only. Specfies if the project is dirty.
        /// </summary>
        public virtual bool IsDirty
        {
            get
            {
                int dirty;

                ErrorHandler.ThrowOnFailure(project.IsDirty(out dirty));
                return dirty != 0;
            }
            set
            {
                if (this.project == null || this.project.Site == null || this.project.IsClosed)
                {
                    throw new InvalidOperationException();
                }

                UIThread.DoOnUIThread(delegate() {
                    IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }
                    extensibility.EnterAutomationFunction();

                    try
                    {
                        project.SetProjectFileDirty(value);
                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }
                });
            }
        }

        /// <summary>
        /// Gets the Projects collection containing the Project object supporting this property.
        /// </summary>
        public virtual EnvDTE.Projects Collection
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the top-level extensibility object.
        /// </summary>
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE));
            }
        }

        /// <summary>
        /// Gets a GUID string indicating the kind or type of the object.
        /// </summary>
        public virtual string Kind
        {
            get { return project.ProjectGuid.ToString("B"); }
        }

        /// <summary>
        /// Gets a ProjectItems collection for the Project object.
        /// </summary>
        public virtual EnvDTE.ProjectItems ProjectItems
        {
            get
            {
                return new OAProjectItems(this, project);
            }
        }

        /// <summary>
        /// Gets a collection of all properties that pertain to the Project object.
        /// </summary>
        public virtual EnvDTE.Properties Properties
        {
            get
            {
                return new OAProperties(this.project.NodeProperties);
            }
        }

        /// <summary>
        /// Returns the name of project as a relative path from the directory containing the solution file to the project file
        /// </summary>
        /// <value>Unique name if project is in a valid state. Otherwise null</value>
        public virtual string UniqueName
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    if (this.project == null || this.project.IsClosed)
                    {
                        return null;
                    }
                    else
                    {
                        // Get Solution service
                        IVsSolution solution = this.project.GetService(typeof(IVsSolution)) as IVsSolution;
                        if (solution == null)
                        {
                            throw new InvalidOperationException();
                        }

                        // Ask solution for unique name of project
                        string uniqueName = string.Empty;
                        ErrorHandler.ThrowOnFailure(solution.GetUniqueNameOfProject(this.project.InteropSafeIVsHierarchy, out uniqueName));
                        return uniqueName;
                    }
                });
            }
        }

        /// <summary>
        /// Gets an interface or object that can be accessed by name at run time.
        /// </summary>
        public virtual object Object
        {
            get { return this.project.Object; }
        }

        /// <summary>
        /// Gets the requested Extender object if it is available for this object.
        /// </summary>
        /// <param name="name">The name of the extender object.</param>
        /// <returns>An Extender object. </returns>
        public virtual object get_Extender(string name)
        {
            return null;
        }

        /// <summary>
        /// Gets a list of available Extenders for the object.
        /// </summary>
        public virtual object ExtenderNames
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the Extender category ID (CATID) for the object.
        /// </summary>
        public virtual string ExtenderCATID
        {
            get { return String.Empty; }
        }

        /// <summary>
        /// Gets the full path and name of the Project object's file.
        /// </summary>
        public virtual string FullName
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    string filename;
                    uint format;
                    ErrorHandler.ThrowOnFailure(project.GetCurFile(out filename, out format));
                    return filename;
                });
            }
        }

        /// <summary>
        /// Gets or sets a value indicatingwhether the object has not been modified since last being saved or opened.
        /// </summary>
        public virtual bool Saved
        {
            get
            {
                return !this.IsDirty;
            }
            set
            {
                UIThread.DoOnUIThread(delegate() {
                    if (this.project == null || this.project.Site == null || this.project.IsClosed)
                    {
                        throw new InvalidOperationException();
                    }

                    IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }

                    extensibility.EnterAutomationFunction();

                    try
                    {
                        project.SetProjectFileDirty(!value);
                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }
                });
            }
        }

        /// <summary>
        /// Gets the ConfigurationManager object for this Project .
        /// </summary>
        public virtual EnvDTE.ConfigurationManager ConfigurationManager
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    if (this.configurationManager == null)
                    {
                        IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                        if (extensibility == null)
                        {
                            throw new InvalidOperationException();
                        }

                        object configurationManagerAsObject;
                        extensibility.GetConfigMgr(this.project.InteropSafeIVsHierarchy, VSConstants.VSITEMID_ROOT, out configurationManagerAsObject);

                        if (configurationManagerAsObject == null)
                        {
                            throw new InvalidOperationException();
                        }
                        else
                        {
                            this.configurationManager = (ConfigurationManager)configurationManagerAsObject;
                        }
                    }

                    return this.configurationManager;
                });
            }
        }

        /// <summary>
        /// Gets the Globals object containing add-in values that may be saved in the solution (.sln) file, the project file, or in the user's profile data.
        /// </summary>
        public virtual EnvDTE.Globals Globals
        {
            get { return null; }
        }

        /// <summary>
        /// Gets a ProjectItem object for the nested project in the host project. 
        /// </summary>
        public virtual EnvDTE.ProjectItem ParentProjectItem
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    var hier = project.InteropSafeIVsHierarchy; 
                    object parentHierarchy;
                    var hr1 = hier.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ParentHierarchy, out parentHierarchy);
                    if (ErrorHandler.Succeeded(hr1) && parentHierarchy != null)
                    {
                        object variantParentItemid;
                        var hr2 = hier.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ParentHierarchyItemid, out variantParentItemid);
                        if (ErrorHandler.Succeeded(hr2) && variantParentItemid != null)
                        {
                            object extObject;
                            uint parentItemid;
                            // Decoding parentItemid from marshalled VARIANT
                            if (variantParentItemid is int)
                                parentItemid = (uint)(int)variantParentItemid;
                            else if (variantParentItemid is uint)
                                parentItemid = (uint)variantParentItemid;
                            else return null;

                            var hr3 = ((IVsHierarchy)parentHierarchy).GetProperty((uint)parentItemid, (int)__VSHPROPID.VSHPROPID_ExtObject,  out extObject);
                            if (ErrorHandler.Succeeded(hr3))
                            {
                                return extObject as EnvDTE.ProjectItem;
                            }
                        }
                    }
                    return null;
                });
            }
        }

        /// <summary>
        /// Gets the CodeModel object for the project.
        /// </summary>
        public virtual EnvDTE.CodeModel CodeModel
        {
            get { return null; }
        }

        /// <summary>
        /// Saves the project. 
        /// </summary>
        /// <param name="fileName">The file name with which to save the solution, project, or project item. If the file exists, it is overwritten</param>
        /// <exception cref="InvalidOperationException">Is thrown if the save operation failes.</exception>
        /// <exception cref="ArgumentNullException">Is thrown if fileName is null.</exception>        
        public virtual void SaveAs(string fileName)
        {
            this.DoSave(true, fileName);
        }

        /// <summary>
        /// Saves the project
        /// </summary>
        /// <param name="fileName">The file name of the project</param>
        /// <exception cref="InvalidOperationException">Is thrown if the save operation failes.</exception>
        public virtual void Save(string fileName)
        {
            this.DoSave(false, fileName ?? string.Empty);
        }

        /// <summary>
        /// Removes the project from the current solution. 
        /// </summary>
        public virtual void Delete()
        {
            if (this.project == null || this.project.Site == null || this.project.IsClosed)
            {
                throw new InvalidOperationException();
            }

            UIThread.DoOnUIThread(delegate() {
                IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }
                extensibility.EnterAutomationFunction();

                try
                {
                    this.project.Remove(false);
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            });
        }

        /// <summary>
        /// For use by F# tooling only. 
        /// </summary>
        public virtual void NotifyPropertiesDelete()
        {
        }

        /// <summary>
        /// Saves or Save Asthe project.
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

                if (this.project == null || this.project.Site == null || this.project.IsClosed)
                {
                    throw new InvalidOperationException();
                }

                IVsExtensibility3 extensibility = this.project.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();

                try
                {
                    // If an empty file name is passed in for Save then make the file name the project name.
                    if (!isCalledFromSaveAs && string.IsNullOrEmpty(fileName))
                    {
                        // Use the solution service to save the project file. Note that we have to use the service
                        // so that all the shell's elements are aware that we are inside a save operation and
                        // all the file change listenters registered by the shell are suspended.

                        // Get the cookie of the project file from the RTD.
                        IVsRunningDocumentTable rdt = this.project.Site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                        if (null == rdt)
                        {
                            throw new InvalidOperationException();
                        }

                        IVsHierarchy hier;
                        uint itemid;
                        IntPtr unkData = IntPtr.Zero;
                        uint cookie;
                        try
                        {
                            ErrorHandler.ThrowOnFailure(rdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, this.project.Url, out hier,
                                                                                out itemid, out unkData, out cookie));
                        } finally {
                            if (IntPtr.Zero != unkData)
                            {
                                Marshal.Release(unkData);
                            }
                        }

                        // Verify that we have a cookie.
                        if (0 == cookie)
                        {
                            // This should never happen because if the project is open, then it must be in the RDT.
                            throw new InvalidOperationException();
                        }

                        // Get the IVsHierarchy for the project.
                        IVsHierarchy prjHierarchy = project.InteropSafeIVsHierarchy;

                        // Now get the soulution.
                        IVsSolution solution = this.project.Site.GetService(typeof(SVsSolution)) as IVsSolution;
                        // Verify that we have both solution and hierarchy.
                        if ((null == prjHierarchy) || (null == solution))
                        {
                            throw new InvalidOperationException();
                        }

                        ErrorHandler.ThrowOnFailure(solution.SaveSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_SaveIfDirty, prjHierarchy, cookie));
                    }
                    else
                    {

                        // We need to make some checks before we can call the save method on the project node.
                        // This is mainly because it is now us and not the caller like in  case of SaveAs or Save that should validate the file name.
                        // The IPersistFileFormat.Save method only does a validation that is necesseray to be performed. Example: in case of Save As the  
                        // file name itself is not validated only the whole path. (thus a file name like file\file is accepted, since as a path is valid)

                        // 1. The file name has to be valid. 
                        string fullPath = fileName;
                        try
                        {
                            if (!Path.IsPathRooted(fileName))
                            {
                                fullPath = Path.Combine(this.project.ProjectFolder, fileName);
                            }
                        }
                        // We want to be consistent in the error message and exception we throw. ArgumentException on Path.IsRooted shoud become InvalidOperationException.
                        catch (ArgumentException)
                        {
                            throw new InvalidOperationException(SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture));
                        }

                        // It might be redundant but we validate the file and the full path of the file being valid. The SaveAs would also validate the path.
                        // If we decide that this is performance critical then this should be refactored.
                        Utilities.ValidateFileName(this.project.Site, fullPath);

                        if (!isCalledFromSaveAs)
                        {
                            // 2. The file name has to be the same 
                            if (!NativeMethods.IsSamePath(fullPath, this.project.Url))
                            {
                                throw new InvalidOperationException();
                            }

                            ErrorHandler.ThrowOnFailure(this.project.Save(fullPath, 1, 0));
                        }
                        else
                        {
                            ErrorHandler.ThrowOnFailure(this.project.Save(fullPath, 0, 0));
                        }
                    }
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            });
        }
    }
}
