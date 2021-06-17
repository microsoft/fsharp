// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Allows projects to group outputs according to usage.
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    public class OutputGroup : IVsOutputGroup2
    {
        private ProjectConfig projectCfg;
        private ProjectNode project;

        private List<Output> outputs = new List<Output>();
        private Output keyOutput = null;
        private string name;
        private string targetName;

        /// <summary>
        /// Get the project configuration object associated with this output group
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Cfg")]
        public ProjectConfig ProjectCfg
        {
            get { return projectCfg; }
        }

        /// <summary>
        /// Get the project object that produces this output group.
        /// </summary>
        public ProjectNode Project
        {
            get { return project; }
        }

        /// <summary>
        /// Gets the msbuild target name which is assciated to the outputgroup.
        /// ProjectNode defines a static collection of output group names and their associated MsBuild target
        /// </summary>
        public string TargetName
        {
            get { return targetName; }
        }

        /// <summary>
        /// Easy access to the canonical name of the group.
        /// </summary>
        internal string CanonicalName
        {
            get
            {
                string canonicalName;
                ErrorHandler.ThrowOnFailure(get_CanonicalName(out canonicalName));
                return canonicalName;
            }
        }

        /// <summary>
        /// Easy access to outputs
        /// </summary>
        internal Output[] Outputs
        {
            get
            {
                Refresh();
                return outputs.ToArray();
            }
        }
 
        /// <summary>
        /// Constructor for IVSOutputGroup2 implementation
        /// </summary>
        /// <param name="outputName">Name of the output group. See VS_OUTPUTGROUP_CNAME_Build in vsshell.idl for the list of standard values</param>
        /// <param name="msBuildTargetName">MSBuild target name</param>
        /// <param name="projectManager">Project that produce this output</param>
        /// <param name="configuration">Configuration that produce this output</param>
        internal OutputGroup(string outputName, string msBuildTargetName, ProjectNode projectManager, ProjectConfig configuration)
        {
            if (outputName == null)
                throw new ArgumentNullException("outputName");
            if (msBuildTargetName == null)
                throw new ArgumentNullException("outputName");
            if (projectManager == null)
                throw new ArgumentNullException("projectManager");
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            name = outputName;
            targetName = msBuildTargetName;
            project = projectManager;
            projectCfg = configuration;
        }

        public virtual void Refresh()
        {
            // Let MSBuild know which configuration we are working with
            project.SetConfiguration(projectCfg.ConfigCanonicalName);


            var result = new BuildResult(MSBuildResult.Failed, null);
            if (project.ProjectMgr.BuildProject.Targets.ContainsKey(ProjectFileConstants.AllProjectOutputGroups))
            {
                result = project.InvokeMsBuild(ProjectFileConstants.AllProjectOutputGroups);
                if (!result.IsSuccessful)
                {
                    // we could not compute it, probably because there is a real build going on right now
                    project.SetCurrentConfiguration();
                    return;
                }
            }

            // Rebuild the content of our list of output
            string outputType = this.targetName + "Output";
            this.outputs.Clear();
            foreach (Microsoft.Build.Execution.ProjectItemInstance assembly in result.ProjectInstance.GetItems(outputType))
            {
                Output output = new Output(project, assembly);
                this.outputs.Add(output);

                // See if it is our key output
                if (String.Compare(MSBuildItem.GetMetadataValue(assembly, "IsKeyOutput"), true.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
                    keyOutput = output;
            }

            project.SetCurrentConfiguration();

            // Now that the group is built we have to check if it is invalidated by a property
            // change on the project.
            project.OnProjectPropertyChanged += new EventHandler<ProjectPropertyChangedArgs>(OnProjectPropertyChanged);
        }

        public virtual void InvalidateGroup()
        {
            // Set keyOutput to null so that a refresh will be performed the next time
            // a property getter is called.
            if (null != keyOutput)
            {
                // Once the group is invalidated there is no more reason to listen for events.
                project.OnProjectPropertyChanged -= new EventHandler<ProjectPropertyChangedArgs>(OnProjectPropertyChanged);
            }
            keyOutput = null;
        }

        private void OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs args)
        {
            // In theory here we should decide if we have to invalidate the group according with the kind of property
            // that is changed.
            InvalidateGroup();
        }

        public virtual int get_CanonicalName(out string pbstrCanonicalName)
        {
            pbstrCanonicalName = this.name;
            return VSConstants.S_OK;
        }

        public virtual int get_DeployDependencies(uint celt, IVsDeployDependency[] rgpdpd, uint[] pcActual)
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int get_Description(out string pbstrDescription)
        {
            pbstrDescription = null;

            string description;
            int hr = this.get_CanonicalName(out description);
            if (ErrorHandler.Succeeded(hr))
                pbstrDescription = this.Project.GetOutputGroupDescription(description);
            return hr;
        }

        public virtual int get_DisplayName(out string pbstrDisplayName)
        {
            pbstrDisplayName = null;

            string displayName;
            int hr = this.get_CanonicalName(out displayName);
            if (ErrorHandler.Succeeded(hr))
                pbstrDisplayName = this.Project.GetOutputGroupDisplayName(displayName);
            return hr;
        }

        public virtual int get_KeyOutput(out string pbstrCanonicalName)
        {
            pbstrCanonicalName = null;
            if (keyOutput == null)
                Refresh();
            if (keyOutput == null)
            {
                pbstrCanonicalName = String.Empty;
                return VSConstants.S_FALSE;
            }
            return keyOutput.get_CanonicalName(out pbstrCanonicalName);
        }

        public virtual int get_KeyOutputObject(out IVsOutput2 ppKeyOutput)
        {
            if (keyOutput == null)
                Refresh();
            ppKeyOutput = keyOutput;
            if (ppKeyOutput == null)
                return VSConstants.S_FALSE;
            return VSConstants.S_OK;
        }

        public virtual int get_Outputs(uint celt, IVsOutput2[] rgpcfg, uint[] pcActual)
        {
            // Ensure that we are refreshed.  This enables project to
            // project reference scenarios to work.  Normally, output groups are populated as part
            // of build.  However, in the project to project reference case, what ends up happening
            // is that the referencing projects requests the referenced project's output group
            // before a build is done on the referenced project.
            //
            // Furthermore, the project auto toolbox manager requires output groups to be populated
            // on project reopen as well...
            //
            // In the end, this is probably the right thing to do, though -- as it keeps the output
            // groups always up to date.
            Refresh();

            // See if only the caller only wants to know the count
            if (celt == 0 || rgpcfg == null)
            {
                if (pcActual != null && pcActual.Length > 0)
                    pcActual[0] = (uint)outputs.Count;
                return VSConstants.S_OK;
            }

            // Fill the array with our outputs
            uint count = 0;
            foreach (Output output in outputs)
            {
                if (rgpcfg.Length > count && celt > count && output != null)
                {
                    rgpcfg[count] = output;
                    ++count;
                }
            }

            if (pcActual != null && pcActual.Length > 0)
                pcActual[0] = count;

            // If the number asked for does not match the number returned, return S_FALSE
            return (count == celt) ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public virtual int get_ProjectCfg(out IVsProjectCfg2 ppIVsProjectCfg2)
        {
            ppIVsProjectCfg2 = (IVsProjectCfg2)this.projectCfg;
            return VSConstants.S_OK;
        }

        public virtual int get_Property(string pszProperty, out object pvar)
        {
            pvar = project.GetProjectProperty(pszProperty);
            return VSConstants.S_OK;
        }
    }
}
