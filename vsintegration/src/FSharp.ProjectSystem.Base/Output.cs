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

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal class Output : IVsOutput2
    {
        private Microsoft.Build.Execution.ProjectItemInstance output;
        private ProjectNode project;

        /// <summary>
        /// Easy access to canonical name
        /// </summary>
        internal string CanonicalName
        {
            get
            {
                string canonicalName;
                return ErrorHandler.Succeeded(get_CanonicalName(out canonicalName)) ? canonicalName : null;
            }
        }

        /// <summary>
        /// Constructor for IVSOutput2 implementation
        /// </summary>
        /// <param name="projectManager">Project that produce this output</param>
        /// <param name="outputAssembly">MSBuild generated item corresponding to the output assembly (by default, these would be of type MainAssembly</param>
        public Output(ProjectNode projectManager, Microsoft.Build.Execution.ProjectItemInstance outputAssembly)
        {
            if (projectManager == null)
                throw new ArgumentNullException("projectManager");
            if (outputAssembly == null)
                throw new ArgumentNullException("outputAssembly");

            project = projectManager;
            output = outputAssembly;
        }

        /// <summary>
        /// Easy access to output properties
        /// </summary>
        internal string GetMetadata(string name)
        {
            object value;
            return ErrorHandler.Succeeded(get_Property(name, out value)) ? value as string : null;
        }

        public int get_CanonicalName(out string pbstrCanonicalName)
        {
            pbstrCanonicalName = MSBuildItem.GetEvaluatedInclude(output);
            // Make sure we have a full path
            if (!System.IO.Path.IsPathRooted(pbstrCanonicalName))
            {
                pbstrCanonicalName = new Url(project.BaseURI, pbstrCanonicalName).AbsoluteUrl;
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This path must start with file:/// if it wants other project
        /// to be able to reference the output on disk.
        /// If the output is not on disk, then this requirement does not
        /// apply as other projects probably don't know how to access it.
        /// </summary>
        public virtual int get_DeploySourceURL(out string pbstrDeploySourceURL)
        {
            string path = MSBuildItem.GetEvaluatedInclude(output);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException();
            }
            if (path.Length < 9 || String.Compare(path.Substring(0, 8), "file:///", StringComparison.OrdinalIgnoreCase) != 0)
                path = "file:///" + path;  // Does not work with '#' char.
            pbstrDeploySourceURL = path;
            return VSConstants.S_OK;
        }

        public int get_DisplayName(out string pbstrDisplayName)
        {
            return this.get_CanonicalName(out pbstrDisplayName);
        }

        public virtual int get_Property(string szProperty, out object pvar)
        {
            pvar = null;
            var stringToQuery = szProperty;
            if (String.Equals(szProperty, "OUTPUTLOC", StringComparison.OrdinalIgnoreCase))
            {
                stringToQuery = "FinalOutputPath";
            }
            String value = output.GetMetadataValue(stringToQuery);
            // If we don't have a value, we are expected to return unimplemented
            if (String.IsNullOrEmpty(value))
                return VSConstants.E_NOTIMPL;
            if (String.Equals(szProperty, "COM2REG", StringComparison.OrdinalIgnoreCase))
            {
                pvar = true;
            }
            else
            {
                pvar = value;
            }
            return VSConstants.S_OK;

        }

        public int get_RootRelativeURL(out string pbstrRelativePath)
        {
            pbstrRelativePath = String.Empty;
            object variant;
            // get the corresponding property
            if (ErrorHandler.Succeeded(this.get_Property("TargetPath", out variant))
                && variant != null && variant is string)
            {
                pbstrRelativePath = (string)variant;
            }
            return VSConstants.S_OK;
        }

        public virtual int get_Type(out Guid pguidType)
        {
            pguidType = Guid.Empty;
            throw new NotImplementedException();
        }
}
}
