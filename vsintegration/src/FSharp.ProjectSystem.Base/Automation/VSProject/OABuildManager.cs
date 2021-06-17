// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.FSharp.ProjectSystem;
using VSLangProj;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    internal class OABuildManager : ConnectionPointContainer,
                                    IEventSource<_dispBuildManagerEvents>,
                                    BuildManager, 
                                    BuildManagerEvents
    {
        private ProjectNode projectManager;

        internal OABuildManager(ProjectNode project)
        {
            projectManager = project;
            AddEventSource<_dispBuildManagerEvents>(this as IEventSource<_dispBuildManagerEvents>);
        }


        public string BuildDesignTimeOutput(string bstrOutputMoniker)
        {
            throw new NotImplementedException();
        }

        public EnvDTE.Project ContainingProject
        {
            get { return projectManager.GetAutomationObject() as EnvDTE.Project; }
        }

        public EnvDTE.DTE DTE
        {
            get { return projectManager.Site.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE; }
        }

        public object DesignTimeOutputMonikers
        {
            // Notes:
            // Called by .xaml designer to get project design-time outputs (generated files). F# project system doesn't have any generated files.
            get { return new object[0] { }; }
        }

        public object Parent
        {
            get { throw new NotImplementedException(); }
        }

        public event _dispBuildManagerEvents_DesignTimeOutputDeletedEventHandler DesignTimeOutputDeleted;

        public event _dispBuildManagerEvents_DesignTimeOutputDirtyEventHandler DesignTimeOutputDirty;

        private void OnDesignTimeOutputDeleted(object sender, EventArgs args)
        {
            if (DesignTimeOutputDeleted == null)
                return;

            string moniker = OABuildManager.GetOutputMoniker(sender);
            if (!String.IsNullOrEmpty(moniker))
                DesignTimeOutputDeleted(moniker);
        }

        private void OnDesignTimeOutputDirty(object sender, EventArgs args)
        {
            if (DesignTimeOutputDirty == null)
            {
                return;
            }

            string moniker = GetOutputMoniker(sender);
            if (!String.IsNullOrEmpty(moniker))
            {
                DesignTimeOutputDirty(moniker);
            }
        }

        private static string GetOutputMoniker(object sender)
        {
            IVsOutput2 output = sender as IVsOutput2;
            if (output == null)
                return null;
            string moniker;
            output.get_CanonicalName(out moniker);
            return moniker;
        }

        void IEventSource<_dispBuildManagerEvents>.OnSinkAdded(_dispBuildManagerEvents sink)
        {
            DesignTimeOutputDeleted += new _dispBuildManagerEvents_DesignTimeOutputDeletedEventHandler(sink.DesignTimeOutputDeleted);
            DesignTimeOutputDirty += new _dispBuildManagerEvents_DesignTimeOutputDirtyEventHandler(sink.DesignTimeOutputDirty);
        }

        void IEventSource<_dispBuildManagerEvents>.OnSinkRemoved(_dispBuildManagerEvents sink)
        {
            DesignTimeOutputDeleted -= new _dispBuildManagerEvents_DesignTimeOutputDeletedEventHandler(sink.DesignTimeOutputDeleted);
            DesignTimeOutputDirty -= new _dispBuildManagerEvents_DesignTimeOutputDirtyEventHandler(sink.DesignTimeOutputDirty);
        }
    }
}
