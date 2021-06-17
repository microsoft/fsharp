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

    internal abstract class SelectionListener : IVsSelectionEvents, IDisposable
    {
        private uint eventsCookie = (uint)ShellConstants.VSCOOKIE_NIL;
        private IVsMonitorSelection monSel = null;
        private ServiceProvider serviceProvider = null;
        private bool isDisposed;
        /// <summary>
        /// Defines an object that will be a mutex for this object for synchronizing thread calls.
        /// </summary>
        private static volatile object Mutex = new object();

        public SelectionListener(ServiceProvider serviceProvider)
        {

            this.serviceProvider = serviceProvider;
            this.monSel = serviceProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;

            Debug.Assert(this.monSel != null, "Could not get the IVsMonitorSelection object from the services exposed by this project");

            if (this.monSel == null)
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

        public IVsMonitorSelection SelectionMonitor
        {
            get
            {
                return this.monSel;
            }
        }

        public ServiceProvider ServiceProvider
        {
            get
            {
                return this.serviceProvider;
            }
        }

        public virtual int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
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
            if (this.SelectionMonitor != null)
            {
                this.SelectionMonitor.AdviseSelectionEvents(this, out this.eventsCookie);
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
                        if (disposing && this.eventsCookie != (uint)ShellConstants.VSCOOKIE_NIL && this.SelectionMonitor != null)
                        {
                            this.SelectionMonitor.UnadviseSelectionEvents((uint)this.eventsCookie);
                            this.eventsCookie = (uint)ShellConstants.VSCOOKIE_NIL;
                        }

                        this.isDisposed = true;
                    }
                }
            }
        }
    }
}
