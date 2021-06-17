// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Runtime.Serialization;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.OLE.Interop;
using EnvDTE;
using Microsoft.VisualStudio.FSharp.ProjectSystem;


namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    [ComVisible(true), CLSCompliant(false)]
    public class OAProjectItem<T> : EnvDTE.ProjectItem
         where T : HierarchyNode
    {

        private T node;
        private OAProject project;

        public T Node
        {
            get
            {
                return this.node;
            }
        }

        /// <summary>
        /// Returns the automation project
        /// </summary>
        public OAProject Project
        {
            get
            {
                return this.project;
            }
        }

        internal OAProjectItem(OAProject project, T node)
        {
            this.node = node;
            this.project = project;
        }

        /// <summary>
        /// Gets the requested Extender if it is available for this object
        /// </summary>
        /// <param name="extenderName">The name of the extender.</param>
        /// <returns>The extender object.</returns>
        public virtual object get_Extender(string extenderName)
        {
            return null;
        }

        /// <summary>
        /// Gets an object that can be accessed by name at run time.
        /// </summary>
        public virtual object Object
        {
            get
            {
                return this.node.Object;
            }
        }

        /// <summary>
        /// Gets the Document associated with the item, if one exists.
        /// </summary>
        public virtual EnvDTE.Document Document
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the number of files associated with a ProjectItem.
        /// </summary>
        public virtual short FileCount
        {
            get
            {
                return (short)1;
            }
        }

        /// <summary>
        /// Gets a collection of all properties that pertain to the object. 
        /// </summary>
        public virtual EnvDTE.Properties Properties
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    if (this.node.NodeProperties == null)
                    {
                        return null;
                    }
                    return new OAProperties(this.node.NodeProperties);
                });
            }
        }


        /// <summary>
        /// Gets the FileCodeModel object for the project item.
        /// </summary>
        public virtual EnvDTE.FileCodeModel FileCodeModel
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a ProjectItems for the object.
        /// </summary>
        public virtual EnvDTE.ProjectItems ProjectItems
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a GUID string indicating the kind or type of the object.
        /// </summary>
        public virtual string Kind
        {
            get
            {
                Guid guid;
                ErrorHandler.ThrowOnFailure(this.node.GetGuidProperty((int)__VSHPROPID.VSHPROPID_TypeGuid, out guid));
                return guid.ToString("B").ToUpperInvariant();
            }
        }

        /// <summary>
        /// Saves the project item. 
        /// </summary>
        /// <param name="fileName">The name with which to save the project or project item.</param>
        /// <remarks>Implemented by subclasses.</remarks>
        public virtual void Save(string fileName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the top-level extensibility object.
        /// </summary>
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.DTE;
            }
        }

        /// <summary>
        /// Gets the ProjectItems collection containing the ProjectItem object supporting this property.
        /// </summary>
        public virtual EnvDTE.ProjectItems Collection
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    // Get the parent node
                    HierarchyNode parentNode = this.node.Parent;
                    Debug.Assert(parentNode != null, "Failed to get the parent node");

                    // Get the ProjectItems object for the parent node
                    if (parentNode is ProjectNode)
                    {
                        // The root node for the project
                        return ((OAProject)parentNode.GetAutomationObject()).ProjectItems;
                    }
                    else if (parentNode is FileNode && parentNode.FirstChild != null)
                    {
                        // The item has children
                        return ((OAProjectItem<FileNode>)parentNode.GetAutomationObject()).ProjectItems;
                    }
                    else if (parentNode is FolderNode)
                    {
                        return ((OAProjectItem<FolderNode>)parentNode.GetAutomationObject()).ProjectItems;
                    }
                    else
                    {
                        // Not supported. Override this method in derived classes to return appropriate collection object
                        throw new NotImplementedException();
                    }
                });
            }
        }
        /// <summary>
        /// Gets a list of available Extenders for the object.
        /// </summary>
        public virtual object ExtenderNames
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the ConfigurationManager object for this ProjectItem. 
        /// </summary>
        /// <remarks>We do not support config management based per item.</remarks>
        public virtual EnvDTE.ConfigurationManager ConfigurationManager
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the project hosting the ProjectItem.
        /// </summary>
        public virtual EnvDTE.Project ContainingProject
        {
            get
            {
                return this.project;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the object has been modified since last being saved or opened.
        /// </summary>
        public virtual bool Saved
        {
            get
            {
                return !this.IsDirty;

            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Gets the Extender category ID (CATID) for the object.
        /// </summary>
        public virtual string ExtenderCATID
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// If the project item is the root of a subproject, then the SubProject property returns the Project object for the subproject.
        /// </summary>
        public virtual EnvDTE.Project SubProject
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// For use by F# tooling only. Checks if the document associated to this item is dirty.
        /// </summary>
        public virtual bool IsDirty
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();

            }
        }

        public virtual string Name
        {
            get
            {
                return this.node.Caption;
            }
            set
            {
                UIThread.DoOnUIThread(delegate() {
                    if (this.node == null || this.node.ProjectMgr == null || this.node.ProjectMgr.IsClosed || this.node.ProjectMgr.Site == null)
                    {
                        throw new InvalidOperationException();
                    }

                    IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                    if (extensibility == null)
                    {
                        throw new InvalidOperationException();
                    }
                    extensibility.EnterAutomationFunction();

                    try
                    {
                        this.node.SetEditLabel(value);
                        // if it succeeded, this.node is now 'stale'.  update it.
                        var newItem = this.project.ProjectItems.Item(value) as OAProjectItem<T>;
                        if (newItem != null)
                        {
                            this.node = newItem.node;
                        }
                    }
                    finally
                    {
                        extensibility.ExitAutomationFunction();
                    }
                });
            }
        }
        /// <summary>
        /// Removes the project item from hierarchy.
        /// </summary>
        public virtual void Remove()
        {
            if (this.node == null || this.node.ProjectMgr == null || this.node.ProjectMgr.IsClosed || this.node.ProjectMgr.Site == null)
            {
                throw new InvalidOperationException();
            }

            UIThread.DoOnUIThread(delegate() {
                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();
                try
                {
                    this.node.Remove(removeFromStorage: false);
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            });
        }

        /// <summary>
        /// Removes the item from its project and its storage. 
        /// </summary>
        public virtual void Delete()
        {
            if (this.node == null || this.node.ProjectMgr == null || this.node.ProjectMgr.IsClosed || this.node.ProjectMgr.Site == null)
            {
                throw new InvalidOperationException();
            }

            UIThread.DoOnUIThread(delegate() {
                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();

                try
                {
                    this.node.Remove(removeFromStorage: true, promptSave: false);
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            });
        }

        /// <summary>
        /// Saves the project item.
        /// </summary>
        /// <param name="newFileName">The file name with which to save the solution, project, or project item. If the file exists, it is overwritten.</param>
        /// <returns>true if save was successful</returns>
        /// <remarks>This method is implemented on subclasses.</remarks>
        public virtual bool SaveAs(string newFileName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a value indicating whether the project item is open in a particular view type. 
        /// </summary>
        /// <param name="viewKind">A Constants.vsViewKind* indicating the type of view to check.</param>
        /// <returns>A Boolean value indicating true if the project is open in the given view type; false if not. </returns>
        public virtual bool get_IsOpen(string viewKind)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the full path and names of the files associated with a project item.
        /// </summary>
        /// <param name="index"> The index of the item</param>
        /// <returns>The full path of the associated item</returns>
        /// <exception cref="ArgumentOutOfRangeException">Is thrown if index is not one</exception>
        public virtual string get_FileNames(short index)
        {
            // This method should really only be called with 1 as the parameter, but
            // there used to be a bug in VB/C# that would work with 0. To avoid breaking
            // existing automation they are still accepting 0. To be compatible with them
            // we accept it as well.
            Debug.Assert(index > 0, "Index is 1 based.");
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            return this.node.Url;
        }

        /// <summary>
        /// Expands the view of Solution Explorer to show project items. 
        /// </summary>
        public virtual void ExpandView()
        {
            if (this.node == null || this.node.ProjectMgr == null || this.node.ProjectMgr.IsClosed || this.node.ProjectMgr.Site == null)
            {
                throw new InvalidOperationException();
            }

            UIThread.DoOnUIThread(delegate() {
                IVsExtensibility3 extensibility = this.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();

                try
                {
                    IVsUIHierarchyWindow uiHierarchy = UIHierarchyUtilities.GetUIHierarchyWindow(this.node.ProjectMgr.Site, HierarchyNode.SolutionExplorer);
                    if (uiHierarchy == null)
                    {
                        throw new InvalidOperationException();
                    }

                    uiHierarchy.ExpandItem(node.ProjectMgr.InteropSafeIVsUIHierarchy, this.node.ID, EXPANDFLAGS.EXPF_ExpandFolder);

                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            });
        }

        /// <summary>
        /// Opens the project item in the specified view. Not implemented because this abstract class dont know what to open
        /// </summary>
        /// <param name="ViewKind">Specifies the view kind in which to open the item</param>
        /// <returns>Window object</returns>
        public virtual EnvDTE.Window Open(string ViewKind)
        {
            throw new NotImplementedException();
        }
    }
}
