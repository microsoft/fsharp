// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows; 
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using System.Text;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    internal abstract class SolutionListener : IVsSolutionEvents3, IVsSolutionEvents4, IDisposable
    {
        private uint eventsCookie = (uint)ShellConstants.VSCOOKIE_NIL;
        private IVsSolution solution = null;
        private IServiceProvider serviceProvider = null;
        private bool isDisposed;
        /// <summary>
        /// Defines an object that will be a mutex for this object for synchronizing thread calls.
        /// </summary>
        private static volatile object Mutex = new object();

        public SolutionListener(IServiceProvider serviceProvider)
        {

            this.serviceProvider = serviceProvider;
            this.solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            Debug.Assert(this.solution != null, "Could not get the IVsSolution object from the services exposed by this project");

            if (this.solution == null)
            {
                throw new InvalidOperationException();
            }
        }

        public uint EventsCookie
        {
            get
            {
                return this.eventsCookie;
            }
        }

        public IVsSolution Solution
        {
            get
            {
                return this.solution;
            }
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                return this.serviceProvider;
            }
        }

        public virtual int OnAfterCloseSolution(object reserved)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterClosingChildren(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }
        
        public virtual int OnAfterMergeSolution(object pUnkReserved)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterOpenProject(IVsHierarchy hierarchy, int added)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterOpeningChildren(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnBeforeClosingChildren(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnBeforeOpeningChildren(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy rtubHierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnQueryCloseSolution(object pUnkReserved, ref int cancel)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int cancel)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterAsynchOpenProject(IVsHierarchy hierarchy, int added)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterChangeProjectParent(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnAfterRenameProject(IVsHierarchy hierarchy)
        {
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Fired before a project is moved from one parent to another in the solution explorer
        /// </summary>
        public virtual int OnQueryChangeProjectParent(IVsHierarchy hierarchy, IVsHierarchy newParentHier, ref int cancel)
        {
            return VSConstants.E_NOTIMPL;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Init()
        {
            if (this.solution != null)
            {
                this.solution.AdviseSolutionEvents(this, out this.eventsCookie);
            }
        }

        /// <summary>
        /// The method that does the cleanup.
        /// </summary>
        /// <param name="disposing"></param>
        public virtual void Dispose(bool disposing)
        {
            // Everybody can go here.
            if (!this.isDisposed)
            {
                // Synchronize calls to the Dispose simulteniously.
                lock (Mutex)
                {
                    if (!this.isDisposed)
                    {
                        if (disposing && this.eventsCookie != (uint)ShellConstants.VSCOOKIE_NIL && this.solution != null)
                        {
                            this.solution.UnadviseSolutionEvents((uint)this.eventsCookie);
                            this.eventsCookie = (uint)ShellConstants.VSCOOKIE_NIL;
                        }

                        this.isDisposed = true;
                    }
                }
            }
        }
    }
}
