// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using EnvDTE;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using System.Globalization;
using System.Collections.ObjectModel;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Defines abstract package.
    /// </summary>
    [ComVisible(true)]
    [CLSCompliant(false)]
    public abstract class ProjectPackage : Microsoft.VisualStudio.Shell.Package
    {
        /// <summary>
        /// This is the place to register all the solution listeners.
        /// </summary>
        private List<SolutionListener> solutionListeners = new List<SolutionListener>();

        /// <summary>
        /// Add your listener to this list. They should be added in the overridden Initialize befaore calling the base.
        /// </summary>
        internal IList<SolutionListener> SolutionListeners
        {
            get
            {
                return this.solutionListeners;
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Subscribe to the solution events
            this.solutionListeners.Add(new SolutionListenerForProjectReferenceUpdate(this));
            this.solutionListeners.Add(new SolutionListenerForProjectOpen(this));
            this.solutionListeners.Add(new SolutionListenerForProjectEvents(this));

            foreach (SolutionListener solutionListener in this.solutionListeners)
            {
                solutionListener.Init();
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Unadvise solution listeners.
            try
            {
                if (disposing)
                {
                    foreach (SolutionListener solutionListener in this.solutionListeners)
                    {
                        solutionListener.Dispose();
                    }
                }
            }
            finally
            {

                base.Dispose(disposing);
            }
        }
    }
}
