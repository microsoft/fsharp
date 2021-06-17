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
using Microsoft.VisualStudio.FSharp.ProjectSystem;
using EnvDTE;
using VSLangProj;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    /// <summary>
    /// Represents an automation friendly version of a language-specific project.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "OAVS")]
    [ComVisible(true), CLSCompliant(false)]
    public class OAVSProject : VSProject
    {
        private ProjectNode project;
        private OAVSProjectEvents events;

        internal OAVSProject(ProjectNode project)
        {
            this.project = project;
        }

        public ProjectItem AddWebReference(string bstrUrl)
        {
            Debug.Fail("VSProject.AddWebReference not implemented");
            throw new NotImplementedException();
        }

        public BuildManager BuildManager
        {
            get
            {
                return new OABuildManager(this.project);
            }
        }

        public void CopyProject(string bstrDestFolder, string bstrDestUNCPath, prjCopyProjectOption copyProjectOption, string bstrUsername, string bstrPassword)
        {
            Debug.Fail("VSProject.CopyProject not implemented");
            throw new NotImplementedException();
        }

        public ProjectItem CreateWebReferencesFolder()
        {
            Debug.Fail("VSProject.CreateWebReferencesFolder not implemented");
            throw new NotImplementedException();
        }

        public DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE));
            }
        }

        public VSProjectEvents Events
        {
            get
            {
                if (events == null)
                    events = new OAVSProjectEvents(this);
                return events;
            }
        }

        public void Exec(prjExecCommand command, int bSuppressUI, object varIn, out object pVarOut)
        {
            Debug.Fail("VSProject.Exec not implemented");
            throw new NotImplementedException();
        }

        public void GenerateKeyPairFiles(string strPublicPrivateFile, string strPublicOnlyFile)
        {
            Debug.Fail("VSProject.GenerateKeyPairFiles not implemented");
            throw new NotImplementedException();
        }

        public string GetUniqueFilename(object pDispatch, string bstrRoot, string bstrDesiredExt)
        {
            Debug.Fail("VSProject.GetUniqueFilename not implemented");
            throw new NotImplementedException();
        }

        public Imports Imports
        {
            get
            {
                Debug.Fail("VSProject.Imports not implemented");
                throw new NotImplementedException();
            }
        }

        public Project Project
        {
            get
            {
                return this.project.GetAutomationObject() as Project;
            }
        }

        public References References
        {
            get
            {
                ReferenceContainerNode references = project.GetReferenceContainer() as ReferenceContainerNode;
                if (null == references)
                {
                    return null;
                }
                return references.Object as References;
            }
        }

        /// <summary>
        /// Automation may call this function but nothing to do here.
        /// </summary>
        public void Refresh()
        {
        }

        public string TemplatePath
        {
            get
            {
                Debug.Fail("VSProject.TemplatePath not implemented");
                throw new NotImplementedException();
            }
        }

        public ProjectItem WebReferencesFolder
        {
            get
            {
                Debug.Fail("VSProject.WebReferencesFolder not implemented");
                throw new NotImplementedException();
            }
        }

        public bool WorkOffline
        {
            get
            {
                Debug.Fail("VSProject.WorkOffLine not implemented");
                throw new NotImplementedException();
            }
            set
            {
                Debug.Fail("VSProject.Set_WorkOffLine not implemented");
                throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// Provides access to language-specific project events
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "OAVS")]
    [ComVisible(true), CLSCompliant(false)]
    public class OAVSProjectEvents : VSProjectEvents
    {
        private OAVSProject vsProject;

        internal OAVSProjectEvents(OAVSProject vsProject)
        {
            this.vsProject = vsProject;
        }

        public BuildManagerEvents BuildManagerEvents
        {
            get
            {
                return vsProject.BuildManager as BuildManagerEvents;
            }
        }

        public ImportsEvents ImportsEvents
        {
            get
            {
                Debug.Fail("VSProjectEvents.ImportsEvents not implemented");
                throw new NotImplementedException();
            }
        }

        public ReferencesEvents ReferencesEvents
        {
            get
            {
                return vsProject.References as ReferencesEvents;
            }
        }
    }

}
