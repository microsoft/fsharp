// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Construction;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Creates projects within the solution
    /// </summary>
    internal abstract class ProjectFactory : Microsoft.VisualStudio.Shell.Flavor.FlavoredProjectFactoryBase
    {
        private Microsoft.VisualStudio.Shell.Package package;
        private System.IServiceProvider site;

        private Microsoft.Build.Evaluation.ProjectCollection buildEngine;
        private Microsoft.Build.Evaluation.Project buildProject;

        public Microsoft.VisualStudio.Shell.Package Package
        {
            get
            {
                return this.package;
            }
        }

        public System.IServiceProvider Site
        {
            get
            {
                return this.site;
            }
        }

        /// <summary>
        /// The msbuild engine that we are going to use.
        /// </summary>
        public Microsoft.Build.Evaluation.ProjectCollection BuildEngine
        {
            get
            {
                return this.buildEngine;
            }
        }

        /// <summary>
        /// The msbuild project for the temporary project file.
        /// </summary>
        public Microsoft.Build.Evaluation.Project BuildProject
        {
            get
            {
                return this.buildProject;
            }
            set
            {
                this.buildProject = value;
            }
        }

        public ProjectFactory(Microsoft.VisualStudio.Shell.Package package)
        {
            this.package = package;
            this.site = package;
            this.buildEngine = Utilities.InitializeMsBuildEngine(this.buildEngine);
        }

        protected abstract ProjectNode CreateProject();

        /// <summary>
        /// Rather than directly creating the project, ask VS to initate the process of
        /// creating an aggregated project in case we are flavored. We will be called
        /// on the IVsAggregatableProjectFactory to do the real project creation.
        /// </summary>
        /// <param name="fileName">Project file</param>
        /// <param name="location">Path of the project</param>
        /// <param name="name">Project Name</param>
        /// <param name="flags">Creation flags</param>
        /// <param name="projectGuid">Guid of the project</param>
        /// <param name="project">Project that end up being created by this method</param>
        /// <param name="canceled">Was the project creation canceled</param>
        protected override void CreateProject(string fileName, string location, string name, uint flags, ref Guid projectGuid, out IntPtr project, out int canceled)
        {
            project = IntPtr.Zero;
            canceled = 0;

            if ((flags & ((uint)__VSCREATEPROJFLAGS2.CPF_DEFERREDSAVE)) != 0)
            {
                throw new NotSupportedException(SR.GetString(SR.NoZeroImpactProjects));
            }

            if ((flags & ((uint)__VSCREATEPROJFLAGS.CPF_OPENFILE)) != 0)
            {
                if (new ProjectInspector(fileName).IsPoisoned(Site))
                {
                    // error out
                    int ehr = unchecked((int)0x80042003); // VS_E_INCOMPATIBLEPROJECT
                    ErrorHandler.ThrowOnFailure(ehr);
                }
            }

            // Solution properties
            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            Debug.Assert(solution != null, "Could not retrieve the solution service from the global service provider");

            // We do not want to throw. If we cannot set the solution related constants we set them to empty string.
            string solutionDirectory, solutionPath, userOptionsFile;
            solution.GetSolutionInfo(out solutionDirectory, out solutionPath, out userOptionsFile);
            if (solutionDirectory == null)
            {
                solutionDirectory = String.Empty;
            }
            if (solutionPath == null)
            {
                solutionPath = String.Empty;
            }
            string solutionFileName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileName(solutionPath);
            string solutionName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileNameWithoutExtension(solutionPath);
            var solutionExtension = Path.GetExtension(solutionPath);

            // DevEnvDir property
            IVsShell shell = this.Site.GetService(typeof(SVsShell)) as IVsShell;
            Debug.Assert(shell != null, "Could not retrieve the IVsShell service from the global service provider");
            object installDirAsObject;

            // We do not want to throw. If we cannot set the solution related constants we set them to empty string.
            shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out installDirAsObject);
            string installDir = ((string)installDirAsObject);
            if (String.IsNullOrEmpty(installDir))
            {
                installDir = String.Empty;
            }
            else
            {
                // Ensure that we have trailing backslash as this is done for the langproj macros too.
                if (installDir[installDir.Length - 1] != Path.DirectorySeparatorChar)
                {
                    installDir += Path.DirectorySeparatorChar;
                }
            }

            // Get the list of GUIDs from the project/template
            string guidsList = this.ProjectTypeGuids(fileName);

            // Launch the aggregate creation process (we should be called back on our IVsAggregatableProjectFactoryCorrected implementation)
            IVsCreateAggregateProject aggregateProjectFactory = (IVsCreateAggregateProject)this.Site.GetService(typeof(SVsCreateAggregateProject));
            int hr = aggregateProjectFactory.CreateAggregateProject(guidsList, fileName, location, name, flags, ref projectGuid, out project);
            if (hr == VSConstants.E_ABORT)
            {
                canceled = 1;
            }
            ErrorHandler.ThrowOnFailure(hr);

            this.buildProject = null;
        }

        /// <summary>
        /// Instantiate the project class, but do not proceed with the
        /// initialization just yet.
        /// Delegate to CreateProject implemented by the derived class.
        /// </summary>
        protected override object PreCreateForOuter(IntPtr outerProjectIUnknown)
        {
            Debug.Assert(this.buildProject != null, "The build project should have been initialized before calling PreCreateForOuter.");

            // Please be very carefull what is initialized here on the ProjectNode. Normally this should only instantiate and return a project node.
            // The reason why one should very carefully add state to the project node here is that at this point the aggregation has not yet been created and anything that would cause a CCW for the project to be created would cause the aggregation to fail
            // Our reasoning is that there is no other place where state on the project node can be set that is known by the Factory and has to execute before the Load method.
            ProjectNode node = this.CreateProject();
            Debug.Assert(node != null, "The project failed to be created");
            node.BuildEngine = this.buildEngine;
            node.BuildProject = this.buildProject;
            node.Package = this.package as ProjectPackage;
            node.ProjectEventsProvider = GetProjectEventsProvider();
            return node;
        }

        internal Microsoft.Build.Evaluation.Project ReinitializeMsBuildProject(string filename)
        {
            // Solution properties
            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            Debug.Assert(solution != null, "Could not retrieve the solution service from the global service provider");

            // We do not want to throw.If we cannot set the solution related constants we set them to empty string.
            string solutionDirectory, solutionPath, userOptionsFile;
            solution.GetSolutionInfo(out solutionDirectory, out solutionPath, out userOptionsFile);
            if (solutionDirectory == null)
            {
                solutionDirectory = String.Empty;
            }
            if (solutionPath == null)
            {
                solutionPath = String.Empty;
            }
            string solutionFileName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileName(solutionPath);
            string solutionName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileNameWithoutExtension(solutionPath);
            string solutionExtension = String.Empty;
            if (solutionPath.Length > 0 && Path.HasExtension(solutionPath))
            {
                solutionExtension = Path.GetExtension(solutionPath);
            }

            //DevEnvDir property
            IVsShell shell = this.Site.GetService(typeof(SVsShell)) as IVsShell;
            Debug.Assert(shell != null, "Could not retrieve the IVsShell service from the global service provider");
            object installDirAsObject;

            //We do not want to throw.If we cannot set the solution related constants we set them to empty string.
            shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out installDirAsObject);
            string installDir = ((string)installDirAsObject);
            if (String.IsNullOrEmpty(installDir))
            {
                installDir = String.Empty;
            }
            else
            {
                //Ensure that we have trailing backslash as this is done for the langproj macros too.
                if (installDir[installDir.Length - 1] != Path.DirectorySeparatorChar)
                {
                    installDir += Path.DirectorySeparatorChar;
                }
            }

            var projectGlobalPropertiesThatAllProjectSystemsMustSet = new Dictionary<string, string>()
            {
                { GlobalProperty.SolutionDir.ToString(), solutionDirectory },
                { GlobalProperty.SolutionPath.ToString(), solutionPath },
                { GlobalProperty.SolutionFileName.ToString(), solutionFileName },
                { GlobalProperty.SolutionName.ToString(), solutionName },
                { GlobalProperty.SolutionExt.ToString(), solutionExtension },
                { GlobalProperty.BuildingInsideVisualStudio.ToString(), "true" },
                { GlobalProperty.Configuration.ToString(), "" },
                { GlobalProperty.Platform.ToString(), "" },
                { GlobalProperty.DevEnvDir.ToString(), installDir }
            };
            return Utilities.ReinitializeMsBuildProject(this.buildEngine, filename, projectGlobalPropertiesThatAllProjectSystemsMustSet, this.buildProject);
        }

        /// <summary>
        /// Retrives the list of project guids from the project file.
        /// If you don't want your project to be flavorable, override
        /// to only return your project factory Guid:
        ///      return this.GetType().GUID.ToString("B");
        /// </summary>
        /// <param name="file">Project file to look into to find the Guid list</param>
        /// <returns>List of semi-colon separated GUIDs</returns>
        protected override string ProjectTypeGuids(string file)
        {
            // Load the project so we can extract the list of GUIDs

            this.buildProject = this.ReinitializeMsBuildProject(file);

            // Retrieve the list of GUIDs, if it is not specify, make it our GUID
            string guids = buildProject.GetPropertyValue(ProjectFileConstants.ProjectTypeGuids);
            if (String.IsNullOrEmpty(guids))
                guids = this.GetType().GUID.ToString("B");

            return guids;
        }

        private class ProjectInspector
        {
            private Microsoft.Build.Construction.ProjectRootElement xmlProj;
            private const string MinimumVisualStudioVersion = "MinimumVisualStudioVersion";

            public ProjectInspector(string filename)
            {
                try
                {
                    xmlProj = Microsoft.Build.Construction.ProjectRootElement.Open(filename);
                }
                catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
                {
                    // leave xmlProj non-initialized, other methods will check its state in prologue
                }                
            }
            
            /// we consider project to be Dev10- if it doesn't have any imports that belong to higher versions
            public bool IsLikeDev10MinusProject()
            {
                if (xmlProj == null)
                    return false;
                
                const string fsharpFS45TargetsPath      = @"$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets";
                const string fsharpPortableDev11TargetsPath = @"$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.Portable.FSharp.Targets";
                // Dev12+ projects import *.targets files using property
                const string fsharpDev12PlusImportsValue = @"$(FSharpTargetsPath)";

                foreach(var import in xmlProj.Imports)
                {
                    if (
                        IsEqual(import.Project, fsharpFS45TargetsPath) || 
                        IsEqual(import.Project, fsharpPortableDev11TargetsPath) ||
                        IsEqual(import.Project, fsharpDev12PlusImportsValue)
                       )
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool IsEqual(string s1, string s2)
            {
                return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
            }

            public bool IsPoisoned(System.IServiceProvider sp)
            {
                if (xmlProj == null)
                    return false;

                // use IVsAppCompat service to determine current VS version
                var appCompatService = (IVsAppCompat)sp.GetService(typeof(SVsSolution));
                string currentDesignTimeCompatVersionString = null;
                appCompatService.GetCurrentDesignTimeCompatVersion(out currentDesignTimeCompatVersionString);
                // currentDesignTimeCompatVersionString can look like 12.0 
                // code in vsproject\vsproject\vsprjfactory.cpp uses _wtoi that will ignore part of string after dot
                // we'll do the same trick
                var indexOfDot = currentDesignTimeCompatVersionString.IndexOf('.');
                if (indexOfDot != -1)
                {
                    currentDesignTimeCompatVersionString = currentDesignTimeCompatVersionString.Substring(0, indexOfDot);
                }
                var currentDesignTimeCompatVersion = int.Parse(currentDesignTimeCompatVersionString);
                foreach (var pg in xmlProj.PropertyGroups)
                {
                    foreach (var p in pg.Properties)
                    {
                        if (string.CompareOrdinal(p.Name, MinimumVisualStudioVersion) == 0)
                        {
                            var s = p.Value;
                            if (!string.IsNullOrEmpty(s))
                            {
                                int ver;
                                int.TryParse(s, out ver);
                                if (ver > currentDesignTimeCompatVersion)
                                    return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        private IProjectEvents GetProjectEventsProvider()
        {
            ProjectPackage projectPackage = this.package as ProjectPackage;
            Debug.Assert(projectPackage != null, "Package not inherited from framework");
            if (projectPackage != null)
            {
                foreach (SolutionListener listener in projectPackage.SolutionListeners)
                {
                    IProjectEvents projectEvents = listener as IProjectEvents;
                    if (projectEvents != null)
                    {
                        return projectEvents;
                    }
                }
            }

            return null;
        }

    }
}
