// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.Build.Utilities;
using VSConstants = Microsoft.VisualStudio.VSConstants;
using Task = Microsoft.VisualStudio.Shell.Task;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    [CLSCompliant(false), ComVisible(true)]
    public class ProjectReferenceNode : ReferenceNode
    {
        /// <summary>
        /// Containes either null if project reference is OK or instance of Task with error message if project reference is invalid
        /// i.e. project A references project B when target framework version for B is higher that for A
        /// </summary>
        private Shell.Task projectRefError;

        /// <summary>
        /// The name of the assembly this refernce represents
        /// </summary>
        private Guid referencedProjectGuid;

        private string referencedProjectName = String.Empty;

        private string referencedProjectRelativePath = String.Empty;

        private string referencedProjectFullPath = String.Empty;

        private BuildDependency buildDependency = null;

        /// <summary>
        /// This is a reference to the automation object for the referenced project.
        /// </summary>
        private EnvDTE.Project referencedProject;
        private bool referencedProjectIsCached = false;

        /// <summary>
        /// This state is controlled by the solution events.
        /// The state is set to false by OnBeforeUnloadProject.
        /// The state is set to true by OnBeforeCloseProject event.
        /// </summary>
        private bool canRemoveReference = true;

        /// <summary>
        /// Possibility for solution listener to update the state on the dangling reference.
        /// It will be set in OnBeforeUnloadProject then the nopde is invalidated then it is reset to false.
        /// </summary>
        private bool isNodeValid = false;

        private bool enableCaching;
        private string cachedReferencedProjectOutputPath;

        internal bool EnableCaching
        {
            get { return enableCaching; }
            set
            {
                if (enableCaching != value)
                {
                    cachedReferencedProjectOutputPath = null;
                }
                enableCaching = value;
            }
        }

        public override string Url
        {
            get
            {
                return this.referencedProjectFullPath;
            }
        }

        public override string SimpleName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.ReferencedProjectOutputPath);
            }
        }

        public override string Caption
        {
            get
            {
                return this.referencedProjectName;
            }
        }

        public Guid ReferencedProjectGuid
        {
            get
            {
                return this.referencedProjectGuid;
            }
        }

        /// <summary>
        /// Possiblity to shortcut and set the dangling project reference icon.
        /// It is ussually manipulated by solution listsneres who handle reference updates.
        /// </summary>
        public bool IsNodeValid
        {
            get
            {
                return this.isNodeValid;
            }
            set
            {
                this.isNodeValid = value;
            }
        }

        /// <summary>
        /// Controls the state whether this reference can be removed or not. Think of the project unload scenario where the project reference should not be deleted.
        /// </summary>
        public bool CanRemoveReference
        {
            get
            {
                return this.canRemoveReference;
            }
            set
            {
                this.canRemoveReference = value;
            }
        }

        public string ReferencedProjectName
        {
            get { return this.referencedProjectName; }
        }

        private void InitReferencedProject(IVsSolution solution)
        {
            IVsHierarchy hier;
            var hr = solution.GetProjectOfGuid(ref referencedProjectGuid, out hier);
            if (!ErrorHandler.Succeeded(hr))
                return; // check if project is missing or non-loaded!
            
            object obj;
            hr = hier.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);
            if (!ErrorHandler.Succeeded(hr))
                return;

            EnvDTE.Project prj = obj as EnvDTE.Project;
            if (prj == null) 
                return;

            // Skip if has no properties (e.g. "miscellaneous files")
            if (prj.Properties == null)
            {
                return;
            }

            // Get the full path of the current project.
            EnvDTE.Property pathProperty = null;
            try
            {
                pathProperty = prj.Properties.Item("FullPath");
                if (null == pathProperty)
                {
                    // The full path should alway be availabe, but if this is not the
                    // case then we have to skip it.
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }
            string prjPath = pathProperty.Value.ToString();
            EnvDTE.Property fileNameProperty = null;
            // Get the name of the project file.
            try
            {
                fileNameProperty = prj.Properties.Item("FileName");
                if (null == fileNameProperty)
                {
                    // Again, this should never be the case, but we handle it anyway.
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }
            prjPath = System.IO.Path.Combine(prjPath, fileNameProperty.Value.ToString());

            // If the full path of this project is the same as the one of this
            // reference, then we have found the right project.
            if (NativeMethods.IsSamePath(prjPath, referencedProjectFullPath))
            {
                this.referencedProject = prj;
            }
        }

        /// <summary>
        /// Gets the automation object for the referenced project.
        /// </summary>
        private EnvDTE.Project ReferencedProject
        {
            get
            {
                if (!this.referencedProjectIsCached)
                {

                    // Search for the project in the collection of the projects in the
                    // current solution.
                    var solution = (IVsSolution)this.ProjectMgr.GetService(typeof(IVsSolution));
                    if (null == solution)
                    {
                        return null;
                    }
                    InitReferencedProject(solution);
                    this.referencedProjectIsCached = true;
                }

                return this.referencedProject;
            }
        }

        public void DropReferencedProjectCache()
        {
            referencedProjectIsCached = false;
        }

        /// <summary>
        /// Resets error (if any) associated with current project reference
        /// </summary>
        public void CleanProjectReferenceErrorState()
        {
            UIThread.DoOnUIThread(() =>
                {
                    if (projectRefError != null)
                    {
                        ProjectMgr.ProjectErrorsTaskListProvider.Tasks.Remove(projectRefError);
                        projectRefError = null;
                    }
                }
            );
        }

        /// <summary>
        /// Creates new error with given text and associates it with current project reference.
        /// Old error is removed
        /// </summary>
        /// <param name="text"></param>
        private void SetError(string text)
        {
            UIThread.DoOnUIThread(() =>
                {
                    // delete existing error if exists
                    CleanProjectReferenceErrorState();

                    projectRefError = new Shell.ErrorTask
                    {
                        ErrorCategory = Shell.TaskErrorCategory.Warning,
                        HierarchyItem = ProjectMgr.InteropSafeIVsHierarchy,
                        Text = text
                    };

                    ProjectMgr.ProjectErrorsTaskListProvider.Tasks.Add(projectRefError);
                }
            );
        }

        /// <summary>
        /// Helper method: creates and set message with ID SR.ProjectReferencesHigherVersionWarning
        /// </summary>
        private void SetProjectReferencesHigherVersionWarningMessage()
        {
            var myFrameworkName = GetProjectTargetFrameworkName(ProjectMgr);
            var otherFrameworkName = GetProjectTargetFrameworkName(ProjectMgr.Site, referencedProjectGuid);
            var msg = string.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ProjectReferencesHigherVersionWarning, CultureInfo.CurrentUICulture), referencedProjectName, otherFrameworkName.Version, myFrameworkName.Version);
            SetError(msg);
        }

        /// <summary>
        /// Actualizes error associated with this project reference 
        /// </summary>
        public void RefreshProjectReferenceErrorState()
        {
            UIThread.DoOnUIThread(() =>
                {
                    CleanProjectReferenceErrorState();

                    switch(CheckFrameworksCompatibility(ProjectMgr, referencedProjectGuid))
                    {
                        case FrameworkCompatibility.Ok:
                            /* This is OK - do nothing */
                            break;
                        case FrameworkCompatibility.HigherVersion:
                            SetProjectReferencesHigherVersionWarningMessage();
                            break;
                        case FrameworkCompatibility.DifferentFamily:
                            {
                                var myFramework = GetProjectTargetFrameworkName(ProjectMgr);
                                var otherFramework = GetProjectTargetFrameworkName(ProjectMgr.Site, referencedProjectGuid);
                                var msg = GetDifferentFamilyErrorMessage(myFramework, referencedProjectName, otherFramework);
                                SetError(msg);
                            }
                            break;
                    }
                }
            );
        }

        public override bool CanBeReferencedFromFSI()
        {
            return true;
        }

        public override string GetReferenceForFSI()
        {
            var path = ReferencedProjectOutputPath; ;
            return path != null && File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Gets the full path to the assembly generated by this project.
        /// </summary>
        public string ReferencedProjectOutputPath
        {
            get 
            {
                if (EnableCaching)
                {
                    return cachedReferencedProjectOutputPath ?? (cachedReferencedProjectOutputPath = GetReferencedProjectOutputPath());
                }
                return GetReferencedProjectOutputPath();
            }
        }

        private string GetReferencedProjectOutputPath()
        {
            // Make sure that the referenced project implements the automation object.
            if (null == this.ReferencedProject)
            {
                return null;
            }

            // Get the configuration manager from the project.
            EnvDTE.ConfigurationManager confManager = this.ReferencedProject.ConfigurationManager;
            if (null == confManager)
            {
                return null;
            }


            // Get the active configuration.
            EnvDTE.Configuration config = null;
            try
            {
                config = confManager.ActiveConfiguration;
            }
            catch (ArgumentException)
            {
                // 4951: exeception happens sometimes when ToolBox queries references on worker thread 
                // (apparently hitting a race or bad state of referenced project's ConfigurationManager)
                return null;
            }
            if (null == config)
            {
                return null;
            }

            // Get the output path for the current configuration.
            string outputPath = null;
            try
            {
                EnvDTE.Property outputPathProperty = config.Properties.Item("OutputPath");
                if (null == outputPathProperty)
                {
                    return null;
                }
                outputPath = outputPathProperty.Value.ToString();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // just catch the exception and return null, which means the user will see an empty 'FullPath' field of a ProjectReference property.
                return null;
            }

            // Ususally the output path is relative to the project path, but it is possible
            // to set it as an absolute path. If it is not absolute, then evaluate its value
            // based on the project directory.
            if (!System.IO.Path.IsPathRooted(outputPath))
            {
                string projectDir = System.IO.Path.GetDirectoryName(referencedProjectFullPath);
                outputPath = System.IO.Path.Combine(projectDir, outputPath);
            }

            // Now get the name of the assembly from the project.
            // Some project system throw if the property does not exist. We expect an ArgumentException.
            string assemblyName = null;
            try
            {
                assemblyName = this.ReferencedProject.Properties.Item("OutputFileName").Value.ToString();
            }
            catch (ArgumentException)
            {
            }
            catch (NullReferenceException)
            {
            }

            if (null == assemblyName)
            {
                try
                {
                    var group = config.OutputGroups.Item("Built");
                    var files = (object[])group.FileNames;
                    if (files.Length > 0)
                    {
                        assemblyName = (string)files[0];
                    }
                }
                catch (InvalidCastException)
                {
                    return null;
                }
                catch (ArgumentException)
                {
                    return null;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
            }
            // build the full path adding the name of the assembly to the output path.
            outputPath = System.IO.Path.Combine(outputPath, assemblyName);

            return outputPath;

        }

        private Automation.OAProjectReference projectReference;
        public override object Object
        {
            get
            {
                if (null == projectReference)
                {
                    projectReference = new Automation.OAProjectReference(this);
                }
                return projectReference;
            }
        }

        /// <summary>
        /// Constructor for the ReferenceNode. It is called when the project is reloaded, when the project element representing the refernce exists. 
        /// </summary>
        internal ProjectReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element)
        {
            this.referencedProjectRelativePath = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
            Debug.Assert(!String.IsNullOrEmpty(this.referencedProjectRelativePath), "Could not retrive referenced project path form project file");

            string guidString = this.ItemNode.GetMetadata(ProjectFileConstants.Project);

            // Continue even if project setttings cannot be read.
            try
            {
                this.referencedProjectGuid = new Guid(guidString);

                this.buildDependency = new BuildDependency(this.ProjectMgr, this.referencedProjectGuid);
                this.ProjectMgr.AddBuildDependency(this.buildDependency);
            }
            finally
            {
                Debug.Assert(this.referencedProjectGuid != Guid.Empty, "Could not retrive referenced project guidproject file");

                this.referencedProjectName = this.ItemNode.GetMetadata(ProjectFileConstants.Name);

                Debug.Assert(!String.IsNullOrEmpty(this.referencedProjectName), "Could not retrive referenced project name form project file");
            }

            Uri uri = new Uri(this.ProjectMgr.BaseURI.Uri, this.referencedProjectRelativePath);

            if (uri != null)
            {
                this.referencedProjectFullPath = Microsoft.VisualStudio.Shell.Url.Unescape(uri.LocalPath, true);
            }
        }

        internal ProjectReferenceNode(ProjectNode root, string referencedProjectName, string projectPath, string projectReference)
            : base(root)
        {
            Debug.Assert(root != null && !String.IsNullOrEmpty(referencedProjectName) && !String.IsNullOrEmpty(projectReference)
                && !String.IsNullOrEmpty(projectPath), "Can not add a reference because the input for adding one is invalid.");
            this.referencedProjectName = referencedProjectName;

            int indexOfSeparator = projectReference.IndexOf('|');


            string fileName = String.Empty;

            // Unfortunately we cannot use the path part of the projectReference string since it is not resolving correctly relative pathes.
            if (indexOfSeparator != -1)
            {
                string projectGuid = projectReference.Substring(0, indexOfSeparator);
                this.referencedProjectGuid = new Guid(projectGuid);
                if (indexOfSeparator + 1 < projectReference.Length)
                {
                    string remaining = projectReference.Substring(indexOfSeparator + 1);
                    indexOfSeparator = remaining.IndexOf('|');

                    if (indexOfSeparator == -1)
                    {
                        fileName = remaining;
                    }
                    else
                    {
                        fileName = remaining.Substring(0, indexOfSeparator);
                    }
                }
            }

            Debug.Assert(!String.IsNullOrEmpty(fileName), "Can not add a project reference because the input for adding one is invalid.");

            if (!System.IO.Path.IsPathRooted(projectPath))
            {
                IVsSolution sln = this.ProjectMgr.Site.GetService(typeof(SVsSolution)) as IVsSolution;
                string slnDir, slnFile, slnOptsFile;
                sln.GetSolutionInfo(out slnDir, out slnFile, out slnOptsFile);
                projectPath = Path.Combine(slnDir, projectPath);
            }
            Uri uri = new Uri(projectPath);

            string referenceDir = Path.GetDirectoryName(PackageUtilities.GetPathDistance(this.ProjectMgr.BaseURI.Uri, uri));

            Debug.Assert(!String.IsNullOrEmpty(referenceDir), "Can not add a project reference because the input for adding one is invalid.");

            string justTheFileName = Path.GetFileName(fileName);
            this.referencedProjectRelativePath = Path.Combine(referenceDir, justTheFileName);

            this.referencedProjectFullPath = Path.Combine(Path.GetDirectoryName(projectPath), justTheFileName);

            this.buildDependency = new BuildDependency(this.ProjectMgr, this.referencedProjectGuid);

        }

        public override NodeProperties CreatePropertiesObject()
        {
            return new ProjectReferencesProperties(this);
        }


        /// <summary>
        /// The node is added to the hierarchy and then updates the build dependency list.
        /// </summary>
        public override bool AddReference()
        {
            if (this.ProjectMgr == null)
            {
                return false;
            }
            IVsSolution vsSolution = this.ProjectMgr.GetService(typeof(SVsSolution)) as IVsSolution;
            IVsHierarchy projHier = null;

            var hr = vsSolution.GetProjectOfGuid(ref this.referencedProjectGuid, out projHier);
            Debug.Assert(hr == VSConstants.S_OK, "GetProjectOfGuid was not able to locate a project from a project reference");
            if (projHier != null)
            {
                var keyOutput = string.Empty;
                try
                {
                    keyOutput = this.ProjectMgr.GetKeyOutputForGroup(projHier, ProjectSystemConstants.VS_OUTPUTGROUP_CNAME_Built);
                }
                catch (Exception e) 
                {
                    Debug.Fail("Error getting key output", e.ToString());
                }

                bool valid = false;
                try
                {
                    var ext = Path.GetExtension(keyOutput);
                    valid = String.Equals(".dll", ext, StringComparison.OrdinalIgnoreCase) || String.Equals(".exe", ext, StringComparison.OrdinalIgnoreCase);
                }
                catch(ArgumentException) 
                {
                    // bad paths are invalid
                }
                if (!valid)
                {
                    IVsUIShell shell = this.ProjectMgr.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
                    Guid dummy = Guid.NewGuid();
                    int result;
                    string text = string.Format(SR.GetString(SR.ProjectRefOnlyExeOrDll), this.Caption);                    
                    if (!Utilities.IsInAutomationFunction(this.ProjectMgr.Site))
                    {
                        shell.ShowMessageBox(0, ref dummy, null, text, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_CRITICAL, 1, out result);
                    }
                    return false;
                }
            }

            bool success = base.AddReference();
            if (success) 
            {
                this.ProjectMgr.AddBuildDependency(this.buildDependency);
            }
            return success;
        }

        /// <summary>
        /// Overridden method. The method updates the build dependency list before removing the node from the hierarchy.
        /// </summary>
        public override void Remove(bool removeFromStorage, bool promptSave = true)
        {
            if (this.ProjectMgr == null || !this.canRemoveReference)
            {
                return;
            }
            this.ProjectMgr.RemoveBuildDependency(this.buildDependency);
            base.Remove(removeFromStorage, promptSave);
            
            // current reference is removed - delete associated error from list
            CleanProjectReferenceErrorState();
        }

        /// <summary>
        /// Links a reference node to the project file.
        /// </summary>
        public override void BindReferenceData()
        {
            Debug.Assert(!String.IsNullOrEmpty(this.referencedProjectName), "The referencedProjectName field has not been initialized");
            Debug.Assert(this.referencedProjectGuid != Guid.Empty, "The referencedProjectName field has not been initialized");

            this.ItemNode = new ProjectElement(this.ProjectMgr, this.referencedProjectRelativePath, ProjectFileConstants.ProjectReference);

            this.ItemNode.SetMetadata(ProjectFileConstants.Name, this.referencedProjectName);
            this.ItemNode.SetMetadata(ProjectFileConstants.Project, this.referencedProjectGuid.ToString("B"));
            this.ItemNode.SetMetadata(ProjectFileConstants.Private, true.ToString());

            if (CheckFrameworksCompatibility(ProjectMgr, referencedProjectGuid) == FrameworkCompatibility.HigherVersion)
            {
                // we are referencing assembly that targets higher framework version than we are - set error
                SetProjectReferencesHigherVersionWarningMessage();
            }
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the refererence icon.
        /// </summary>
        /// <returns></returns>
        public override bool CanShowDefaultIcon()
        {
            if (this.referencedProjectGuid == Guid.Empty || this.ProjectMgr == null || this.ProjectMgr.IsClosed || this.isNodeValid)
            {
                return false;
            }

            // For multitargeting, we need to find the target framework moniker for the
            // reference we are adding, and display a warning icon if the reference targets a higher
            // framework then what we have.

            IVsSolution vsSolution = this.ProjectMgr.GetService(typeof(SVsSolution)) as IVsSolution;
            IVsHierarchy projHier = null;

            var hr = vsSolution.GetProjectOfGuid(this.ReferencedProjectGuid, out projHier);
            //Debug.Assert(hr == VSConstants.S_OK, "GetProjectOfGuid was not able to locate a project from a project reference");
            if (hr == VSConstants.S_OK)
            {
                object otargetFrameworkMoniker = null;
                hr = projHier.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID4.VSHPROPID_TargetFrameworkMoniker, out otargetFrameworkMoniker);
                if (!ErrorHandler.Succeeded(hr))
                {
                    // Safety check
                    return false;
                }

                if (hr == VSConstants.S_OK)
                {
                    var thisFrameworkName = new System.Runtime.Versioning.FrameworkName(GetTargetFrameworkMoniker());
                    var frameworkName = new System.Runtime.Versioning.FrameworkName((string)otargetFrameworkMoniker);
                    if (thisFrameworkName.Version < frameworkName.Version)
                    {
                        return false;
                    }
                }
            }

            IVsHierarchy hierarchy = null;

            hierarchy = VsShellUtilities.GetHierarchy(this.ProjectMgr.Site, this.referencedProjectGuid);

            if (hierarchy == null)
            {
                return false;
            }

            //If the Project is unloaded return false
            if (this.ReferencedProject == null)
            {
                return false;
            }

            return (!String.IsNullOrEmpty(this.referencedProjectFullPath) && File.Exists(this.referencedProjectFullPath));
        }

        /// <summary>
        /// Checks if a project reference can be added to the hierarchy. It calls base to see if the reference is not already there, then checks for circular references.
        /// </summary>
        /// <returns></returns>
        internal override AddReferenceCheckResult CheckIfCanAddReference()
        {
            // When this method is called this refererence has not yet been added to the hierarchy, only instantiated.
            var checkResult = base.CheckIfCanAddReference();
            if (!checkResult.Ok)
                return checkResult;

            if (this.IsThisProjectReferenceInCycle())
            {
                return AddReferenceCheckResult.Failed(
                    String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ProjectContainsCircularReferences, CultureInfo.CurrentUICulture), this.referencedProjectName)
                    );
            }

            switch (CheckFrameworksCompatibility(ProjectMgr, referencedProjectGuid))
            {
                case FrameworkCompatibility.Ok:
                    return AddReferenceCheckResult.Success;
                case FrameworkCompatibility.HigherVersion:
                    // if project tries to target another project with higher framework version - allow to do this - we'll set up error later in BindReferenceData                    
                    return AddReferenceCheckResult.Success;
                case FrameworkCompatibility.DifferentFamily:
                    var myFrameworkName = GetProjectTargetFrameworkName(ProjectMgr);
                    var otherFrameworkName = GetProjectTargetFrameworkName(ProjectMgr.Site, referencedProjectGuid);
                    var errorMessage = GetDifferentFamilyErrorMessage(myFrameworkName, referencedProjectName, otherFrameworkName);
                    return AddReferenceCheckResult.Failed(errorMessage);
                default:
                    System.Diagnostics.Debug.Assert(false);
                    throw new InvalidOperationException("impossible");
            }
        } 

        private bool IsThisProjectReferenceInCycle()
        {
            return IsReferenceInCycle(this.referencedProjectGuid);
        }

        private bool IsReferenceInCycle(Guid projectGuid)
        {
            // use same logic as C#:
            //   vsproject\langbuild\langref.cpp
            //   BOOL CLanguageReferences::IsCircularReference(CLangReference *pclref, BOOL fCalculateDependencies)
            int isCircular = 0;
            IVsHierarchy otherHier = VsShellUtilities.GetHierarchy(this.ProjectMgr.Site, projectGuid);
            IVsSolutionBuildManager2 vsSBM = this.ProjectMgr.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
            vsSBM.CalculateProjectDependencies();
            vsSBM.QueryProjectDependency(otherHier, this.ProjectMgr.InteropSafeIVsHierarchy, out isCircular);
            return isCircular != 0;
        }

        public override Guid GetBrowseLibraryGuid()
        {
            if (this.Url.EndsWith(".csproj"))
            {
                return VSProjectConstants.guidCSharpBrowseLibrary;
            }
            else if (this.Url.EndsWith(".vbproj")) 
            {
                return VSProjectConstants.guidVBBrowseLibrary;
            } 
            else 
            {
                // TODO: Need to determine a generic way of finding browse library guids other than hardcoding
                // those for .csproj and .vbproj
                CCITracing.AddTraceLog("Unknown project type");
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Checks the compatibility of target frameworks for given projects
        /// </summary>
        private static FrameworkCompatibility CheckFrameworksCompatibility(ProjectNode thisProject, Guid referencedProjectGuid)
        {
            // copying logic from C#'s project system, langref.cpp: IsProjectReferenceReferenceable()
            var otherFrameworkName = GetProjectTargetFrameworkName(thisProject.Site, referencedProjectGuid);
            if (otherFrameworkName == null)
                return FrameworkCompatibility.Ok;

            if (String.Compare(otherFrameworkName.Identifier, ".NETPortable", StringComparison.OrdinalIgnoreCase) == 0)
            {
                 // we always allow references to projects that are targeted to the Portable/".NETPortable" fx family
                return FrameworkCompatibility.Ok;
            }

            if (String.Compare(otherFrameworkName.Identifier, ".NETStandard", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // we always allow references to projects that are targeted to the ".NETStandard" family
                return FrameworkCompatibility.Ok;
            }

            var myFrameworkName = GetProjectTargetFrameworkName(thisProject);
            if (String.Compare(otherFrameworkName.Identifier, myFrameworkName.Identifier, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // same family, compare version
                if (myFrameworkName.Version.CompareTo(otherFrameworkName.Version) >= 0)
                {
                    return FrameworkCompatibility.Ok;
                }
                else
                {
                    // trying to reference a higher version
                    return FrameworkCompatibility.HigherVersion;
                }
            }
            else
            {
                // different family, e.g. immersive v .NET v Silverlight
                return FrameworkCompatibility.DifferentFamily;
            }            
        }

        /// <summary>
        /// Returns target framework for the given project Guid
        /// </summary>
        private static System.Runtime.Versioning.FrameworkName GetProjectTargetFrameworkName(System.IServiceProvider serviceProvider, Guid referencedProjectGuid)
        {
            var hierarchy = VsShellUtilities.GetHierarchy(serviceProvider, referencedProjectGuid);
            if (hierarchy == null)
                return null;

            object otherTargetFrameworkMonikerObj;

            int hr = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID4.VSHPROPID_TargetFrameworkMoniker, out otherTargetFrameworkMonikerObj);
            if (!ErrorHandler.Succeeded(hr))
                return null;

            string targetFrameworkMoniker = (string)otherTargetFrameworkMonikerObj;
            return new System.Runtime.Versioning.FrameworkName(targetFrameworkMoniker);
        }

        /// <summary>
        /// Returns target framework for the given project node
        /// </summary>
        /// <param name="projectNode"></param>
        /// <returns></returns>
        private static System.Runtime.Versioning.FrameworkName GetProjectTargetFrameworkName(ProjectNode projectNode)
        {
            string targetFrameworkMoniker = projectNode.GetTargetFrameworkMoniker();
            return new System.Runtime.Versioning.FrameworkName(targetFrameworkMoniker);
        }

        private static string GetDifferentFamilyErrorMessage(System.Runtime.Versioning.FrameworkName currentFrameworkName, string referencedProjectName, System.Runtime.Versioning.FrameworkName otherFrameworkName)
        {
            return string.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ProjectReferencesDifferentFramework, CultureInfo.CurrentUICulture), referencedProjectName, currentFrameworkName.Identifier, otherFrameworkName.Identifier);
        }

        enum FrameworkCompatibility
        {
            Ok,
            DifferentFamily,
            HigherVersion
        }
    }

}
