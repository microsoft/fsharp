// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections;
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
using System.Diagnostics.CodeAnalysis;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using Microsoft.Build.Execution;
using System.Linq;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    [CLSCompliant(false)]
    [ComVisible(true)]
    public class AssemblyReferenceNode : ReferenceNode
    {
        private struct AssemblyResolvedInfo
        {
            public AssemblyName AssemblyName;
            public AssemblyName ResolvedAssemblyName;
            public bool CopyLocalDefault;
            public bool IsNoPIA; // <EmbedInteropTypes> is true
            public bool IsPlatformAssembly; // simple name resolution resolved to {TargetFrameworkDirectory}
            public bool WasSuccessfullyResolved;
        }
        private struct AssemblyMSBuildProjectionInfo
        {
            public bool WantHintPath;
            public bool WantFusionName; // as opposed to simple name
            public bool? WantSpecificVersion;  // null -> nothing in .fsproj, true/false mean serialize that value to .fsproj
        }

        private string myAssemblyPath = String.Empty;
        private FileChangeManager myFileChangeListener;
        private bool myIsDisposed = false;
        private AssemblyResolvedInfo resolvedInfo;
        private AssemblyMSBuildProjectionInfo msbuildProjectionInfo;
        private bool fsprojIncludeHasFilename = false;

        /// <summary>
        /// The name of the assembly this reference represents.
        /// </summary>
        /// <value></value>
        public System.Reflection.AssemblyName AssemblyName
        {
            get
            {
                return this.resolvedInfo.AssemblyName;
            }
        }

        /// <summary>
        /// Returns the name of the assembly this reference refers to on this specific
        /// machine. It can be different from the AssemblyName property because it can
        /// be more specific.
        /// </summary>
        public System.Reflection.AssemblyName ResolvedAssembly
        {
            get { return this.resolvedInfo.ResolvedAssemblyName; }
        }

        public override string Url
        {
            get
            {
                return this.myAssemblyPath;
            }
        }

        public override string SimpleName
        {
            get
            {
                if (this.resolvedInfo.WasSuccessfullyResolved)
                {
                    return this.resolvedInfo.AssemblyName.Name;
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public override string Caption
        {
            get
            {
                if (fsprojIncludeHasFilename)
                {
                    return MSBuildItem.GetEvaluatedInclude(this.ItemNode.Item);
                }
                if (this.resolvedInfo.AssemblyName != null)
                {
                    return this.resolvedInfo.AssemblyName.Name;
                }
                return MSBuildItem.GetEvaluatedInclude(this.ItemNode.Item);
            }
        }

        private Automation.OAAssemblyReference assemblyRef;
        public override object Object
        {
            get
            {
                if (null == assemblyRef)
                {
                    assemblyRef = new Automation.OAAssemblyReference(this);
                }
                return assemblyRef;
            }
        }

        internal static AssemblyReferenceNode CreateFromProjectFile(ProjectNode root, ProjectElement element, BuildResult buildResult)
        { return new AssemblyReferenceNode(root, element, buildResult); }

        internal static AssemblyReferenceNode CreateFromFullPathViaUIAddReference(ProjectNode root, string assemblyFullPath, AddReferenceDialogTab tab)
        { return new AssemblyReferenceNode(root, assemblyFullPath, tab); }
        internal static AssemblyReferenceNode CreateFromAssemblyNameViaUIAutomation(ProjectNode root, string assemblyName)
        { return new AssemblyReferenceNode(0, root, assemblyName); }

        /// <summary>
        /// Creating AssemblyReferenceNode from fsproj element
        /// </summary>
        private AssemblyReferenceNode(ProjectNode root, ProjectElement element, BuildResult buildResult)
            : base(root, element)
        {
            BindFromBuildResult(element.Item, buildResult);
        }

        /// <summary>
        /// Creating AssemblyReferenceNode via VS UI ("Add Reference" or otherwise)
        /// </summary>
        private AssemblyReferenceNode(ProjectNode root, string assemblyFullPath, AddReferenceDialogTab tab)
            : base(root)
        {
            // Validate the input parameters.
            if (null == root)
            {
                throw new ArgumentNullException("root");
            }
            if (string.IsNullOrEmpty(assemblyFullPath))
            {
                throw new ArgumentNullException("assemblyFullPath");
            }
            ResolveAssemblyReferenceByFullPath(assemblyFullPath, tab);
            this.InitializeFileChangeEvents();
        }

        /// <summary>
        /// Creating AssemblyReferenceNode via automation (Cider case)
        /// </summary>
        private AssemblyReferenceNode(int dummy, ProjectNode root, string assemblyName)
            : base(root)
        {
            // Validate the input parameters.
            if (null == root)
            {
                throw new ArgumentNullException("root");
            }
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException("assemblyName");
            }
            AddToProjectFileAndTryResolve(assemblyName);
            InitializeAssemblyName(assemblyName);

            if (!this.resolvedInfo.WasSuccessfullyResolved)
            {
                this.ProjectMgr.AddReferenceCouldNotBeAddedErrorMessage(assemblyName);
            }

            if (!resolvedInfo.IsPlatformAssembly)
            {
                this.msbuildProjectionInfo.WantFusionName = true;
            }

            this.InitializeFileChangeEvents();
        }

        internal static bool IsFSharpCoreReference(ReferenceNode node)
        {
            var isFSharpCore = false;
            try
            {
                var assemblyName = new AssemblyName(node.ItemNode.Item.EvaluatedInclude);
                isFSharpCore = assemblyName.Name == "FSharp.Core";
            }
            catch (FileLoadException)
            {
                // constructor of AssemblyName raises FileNotFoundException if supplied name cannot be parsed as valid assembly name
            }
            return isFSharpCore;
        }

        internal static bool ContainsUsagesOfTargetFSharpCoreVersionProperty(ReferenceNode node)
        {
            Debug.Assert(IsFSharpCoreReference(node));
            if (node.ItemNode.Item.UnevaluatedInclude.Contains(ProjectFileConstants.TargetFSharpCoreVersionProperty))
                return true;

            var hintPath = node.ItemNode.Item.GetMetadata(ProjectFileConstants.HintPath);
            return hintPath != null && hintPath.UnevaluatedValue.Contains(ProjectFileConstants.TargetFSharpCoreVersionProperty);
        }

        internal void RebindFSharpCoreAfterUpdatingVersion(BuildResult buildResult)
        {
            Debug.Assert(IsFSharpCoreReference(this));
            if (!ContainsUsagesOfTargetFSharpCoreVersionProperty(this))
                return;

            UnregisterFromFileChangeService();
            ItemNode.RefreshProperties();

            fsprojIncludeHasFilename = false;
            resolvedInfo = default(AssemblyResolvedInfo);
            myAssemblyPath = string.Empty;
            msbuildProjectionInfo = default(AssemblyMSBuildProjectionInfo);

            BindFromBuildResult(ItemNode.Item, buildResult);

            this.ReDraw(UIHierarchyElement.Icon);
            this.ReDraw(UIHierarchyElement.Caption);
        }

        private void BindFromBuildResult(Build.Evaluation.ProjectItem element, BuildResult buildResult)
        {
            var include = MSBuildItem.GetEvaluatedInclude(element);
            fsprojIncludeHasFilename = (include.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                             include.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            ResolveFromBuiltProject(include, buildResult);
            if (!fsprojIncludeHasFilename)
            {
                InitializeAssemblyName(include);
            }
            InitializeFileChangeEvents();
        }

        private void InitializeAssemblyName(string assemblyName)
        {
            // AssemblyName may be less specific than ResolvedAssemblyName (e.g. not have version/publickey)
            if (this.resolvedInfo.WasSuccessfullyResolved)
            {
                System.Reflection.AssemblyName name = null;
                try
                {
                    name = new System.Reflection.AssemblyName(assemblyName);
                }
                catch (Exception)
                {
                }
                if (name != null)
                {
                    this.resolvedInfo.AssemblyName = name;
                }
            }
        }

        public override int Close()
        {
            try
            {
                this.Dispose(true);
            }
            finally
            {
                base.Close();
            }

            return VSConstants.S_OK;
        }

        public override bool CanBeReferencedFromFSI()
        {
            // prohibit adding references to mscorlib\FSharp.Core
            if (IsFSharpCoreReference(this) || (resolvedInfo.WasSuccessfullyResolved && resolvedInfo.AssemblyName != null && resolvedInfo.AssemblyName.Name == "mscorlib"))
            {
                return false;
            }
            return true;            
        }

        public override string GetReferenceForFSI()
        {
            if (resolvedInfo.WasSuccessfullyResolved && File.Exists(Url))
            {
                return Url;
            }
            return null;
        }

        private bool IsSpecialFSharpCoreReference
        {
            get { return ProjectMgr.CanUseTargetFSharpCoreReference && IsFSharpCoreReference(this) && ContainsUsagesOfTargetFSharpCoreVersionProperty(this); }
        }

        public override NodeProperties CreatePropertiesObject()
        {
            if (IsSpecialFSharpCoreReference)
            {
                return new FSharpCoreAssemblyReferenceProperties(this, resolvedInfo.CopyLocalDefault);
            }

            return new AssemblyReferenceProperties(this, this.resolvedInfo.CopyLocalDefault);
        }

        /// <summary>
        /// Links a reference node to the project and hierarchy.
        /// </summary>
        public override void BindReferenceData()
        {
            // BindReferenceData only happens for newly created AssemblyReferenceNodes (as opposed to loaded from fsproj)
            Debug.Assert(this.resolvedInfo.WasSuccessfullyResolved, "assembly was not resolved, we should not be trying to link a node to put in .fsproj file");

            // Logic here follows that of
            // \\ddindex2\sources_tfs\Dev10_Main\vsproject\langbuild\langref.cpp
            // AddMSBuildItem()

            this.ItemNode = new ProjectElement(this.ProjectMgr, this.msbuildProjectionInfo.WantFusionName ? this.resolvedInfo.AssemblyName.FullName : this.resolvedInfo.AssemblyName.Name, ProjectFileConstants.Reference);

            if (this.msbuildProjectionInfo.WantSpecificVersion != null)
            {
                if (this.msbuildProjectionInfo.WantSpecificVersion.Value)
                {
                    // Note that we should never set <SpecificVersion>True from 
                    // UI 'add reference', as it breaks multi-targeting.  User must manually 
                    // opt in to changing SV to get 'true' if desired.
                }
                else
                {
                    if (this.msbuildProjectionInfo.WantFusionName)
                    {
                        this.ItemNode.SetMetadata(ProjectFileConstants.SpecificVersion, "False");
                    }
                }
            }
            if (this.resolvedInfo.IsNoPIA)
            {
                this.ItemNode.SetMetadata(ProjectFileConstants.EmbedInteropTypes, "True");
            }
            if (this.msbuildProjectionInfo.WantHintPath)
            {
                this.ItemNode.SetMetadata(ProjectFileConstants.HintPath, PackageUtilities.MakeRelative(this.ProjectMgr.ProjectFolder + "\\", this.myAssemblyPath));
            }
            if (this.resolvedInfo.CopyLocalDefault)
            {
                // In fact this is only set if CopyLocal 'is overridden', which is only as a result of explicit user action (or automation).
                // So simply 'add reference' should never set the metadata value ProjectFileConstants.Private
            }
            // TODO - Note that we don't currently support any logic for
            // LBXML_KEY_REFERENCE_ALIAS           "Name"
            // LBXML_KEY_REFERENCE_EXTENSION       "ExecutableExtension"
        }

        protected override void Dispose(bool disposing)
        {
            if (this.myIsDisposed)
            {
                return;
            }

            try
            {
                this.UnregisterFromFileChangeService();
            }
            finally
            {
                base.Dispose(disposing);
                this.myIsDisposed = true;
            }
        }

        public override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            // this is reference to FSharp.Core that uses value of TargetFSharpCoreVersion property - prohibit deletion
            if (IsSpecialFSharpCoreReference)
            {
                return false;
            }
            return base.CanDeleteItem(deleteOperation);
        }

        public override void Remove(bool removeFromStorage, bool promptSave = true)
        {
            // AssemblyReference doesn't backed by the document - its removal is simply modification of the project file
            // we disable IVsTrackProjectDocuments2 events to avoid confusing messages from SCC
            var oldFlag = ProjectMgr.EventTriggeringFlag;
            try
            {
                ProjectMgr.EventTriggeringFlag = oldFlag | ProjectNode.EventTriggering.DoNotTriggerTrackerEvents;

                base.Remove(removeFromStorage, promptSave);

                // invoke ComputeSourcesAndFlags to refresh compiler flags
                // it was the only useful thing performed by one of IVsTrackProjectDocuments2 listeners
                ProjectMgr.ComputeSourcesAndFlags();
            }
            finally
            {
                ProjectMgr.EventTriggeringFlag = oldFlag;
            }
        }

	    /// <summary>
	    /// Determines if this is node a valid node for painting the default reference icon.
	    /// </summary>
	    /// <returns></returns>
	    public override bool CanShowDefaultIcon()
 	    {
		    if (String.IsNullOrEmpty(this.myAssemblyPath) || !File.Exists(this.myAssemblyPath))
		    {
			   return false;
		    }

            return true;
        }

        private string GetFullPathFromPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            else
            {
                Uri uri = new Uri(this.ProjectMgr.BaseURI.Uri, path);

                if (uri != null)
                {
                    return Microsoft.VisualStudio.Shell.Url.Unescape(uri.LocalPath, true);
                }
            }

            return String.Empty;
        }

        public void DoOneOffResolve()
        {
            var result = this.ProjectMgr.Build(MsBuildTarget.ResolveAssemblyReferences);
            this.ResolveReference(result);
        }
        internal override void ResolveReference(BuildResult buildResult)
        {
            Debug.Assert(this.ItemNode != null && this.ItemNode.Item != null, "called ResolveReference before initializing ItemNode");
            this.ResolveFromBuiltProject(MSBuildItem.GetEvaluatedInclude(this.ItemNode.Item), buildResult);
        }

        internal static BuildResult BuildInstance(ProjectNode projectNode, ref ProjectInstance instance, string target)
        {
            var submission = projectNode.DoMSBuildSubmission(BuildKind.SYNC, target, ref instance, null);
            return new BuildResult(submission, instance);
        }

        private void ResolveAssemblyReferenceByFullPath(string assemblyFullPath, AddReferenceDialogTab tab)
        {
            if (this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return;
            }

            bool isValidPath = false;
            try
            {
                isValidPath = Path.IsPathRooted(assemblyFullPath);
            }
            catch (ArgumentException)
            {
            }
            Debug.Assert(isValidPath, string.Format("Expected assemblyFullPath to be a full path, but it was {0}", assemblyFullPath));

            this.msbuildProjectionInfo.WantHintPath = false;
            this.msbuildProjectionInfo.WantFusionName = false;
            this.msbuildProjectionInfo.WantSpecificVersion = null;

            try
            {
                var simpleName = System.IO.Path.GetFileNameWithoutExtension(assemblyFullPath);
                AddToProjectFileAndTryResolve(simpleName);
            }
            catch (Exception)
            {
            }
            if (!this.resolvedInfo.WasSuccessfullyResolved)
            {
                this.msbuildProjectionInfo.WantHintPath = true;
                AddToProjectFileAndTryResolve(assemblyFullPath);
            }
            else
            {
                this.myAssemblyPath = assemblyFullPath;
                // we successfully resolved it via simple name
                if (!this.resolvedInfo.IsPlatformAssembly)
                {
                    // not a platform assembly
                    if (resolvedInfo.AssemblyName != null)
                    {
                        // Project file contains different reference than picked/shown in UI
                        // code in this class tries to mimic the behavior in vsproject\langbuild\langref.cpp\785480\langref.cpp
                        // it also uses simple name for initial resolution attempt 
                        // however after that it repopulates ComPlus attributes from the source assembly via SetComPlusAttributesFromFullPath
                        // this part was previously skipped - as a result AssemblyName contained invalid data
                        var assemblyName = AssemblyName.GetAssemblyName(assemblyFullPath);
                        resolvedInfo.AssemblyName = assemblyName;
                        resolvedInfo.ResolvedAssemblyName = assemblyName;
                    }

                    if (tab == AddReferenceDialogTab.DotNetTab)
                    {
                        // from .Net tab
                        this.msbuildProjectionInfo.WantFusionName = true;
                        this.msbuildProjectionInfo.WantSpecificVersion = true;
                    }
                    else
                    {
                        Debug.Assert(tab == AddReferenceDialogTab.BrowseTab);
                        // not from .Net tab
                        this.msbuildProjectionInfo.WantHintPath = true;
                    }
                }
                else
                {
                    // platform assemblies can just resolve to simple name
                    // it was a platform assembly
                }
            }
            // TODO - not accounting for case described below
            // if <re-resolving fusion name with SpecificVersion fails> then
            // {
            // this is possible if this reference is being added through automation
            // in which case the file passed may have a different fusion name than
            // the assembly in the target framework/fx extensions.
            // in that case just add with a hint path
            // wantHintPath = true;
            // }
            if (this.msbuildProjectionInfo.WantHintPath)
            {
                this.msbuildProjectionInfo.WantSpecificVersion = false;
            }
            if (this.myAssemblyPath == null)
            {
                this.myAssemblyPath = assemblyFullPath;
            }
            if (!this.resolvedInfo.WasSuccessfullyResolved)
            {
                this.ProjectMgr.AddReferenceCouldNotBeAddedErrorMessage(assemblyFullPath);
            }
            // "finished: assemblyFullPath 
        }

        /// <summary>
        /// Initialize 'resolvedInfo' by having MSBuild resolve the assembly in the context of the current project
        /// </summary>
        /// <param name="assemblyInclude">Either a full path to a file on disk, or a simple name or fusion name</param>
        private void AddToProjectFileAndTryResolve(string assemblyInclude)
        {
            // starting: assemblyInclude 
            ProjectInstance instance = null;
            instance = this.ProjectMgr.BuildProject.CreateProjectInstance();   // use a fresh instance...
            instance.AddItem(ProjectFileConstants.Reference, assemblyInclude); // ...and mutate it as through there were another <Reference Include="blah"> there
            var result = BuildInstance(this.ProjectMgr, ref instance, MsBuildTarget.ResolveAssemblyReferences);
            this.ResolveFromBuiltProject(assemblyInclude, result);
        }

        private void ResolveFromBuiltProject(string assemblyInclude, BuildResult buildResult)
        {
            if (!buildResult.IsSuccessful)
            {
                // ResolveAssemblyReferences build failed.
                return;
            }
            System.Collections.Generic.IEnumerable<ProjectItemInstance> group = buildResult.ProjectInstance.GetItems(ProjectFileConstants.ReferencePath);
            if (group != null)
            {
                foreach (var item in group)
                {
                    // TODO, the logic here is too brittle - if a user adds a 'logical duplicate' assembly with a different name, it may not find resolution
                    // and then wind up with wrong diagnostic later because it failed to resolve (when in fact it would resolve if not for duplicate)
                    if (0 == string.Compare(assemblyInclude, MSBuildItem.GetMetadataValue(item, "OriginalItemSpec"), StringComparison.Ordinal))
                    {
                        var fusionName = MSBuildItem.GetMetadataValue(item, "FusionName");
                        if (!string.IsNullOrEmpty(fusionName))
                        {
                            this.resolvedInfo.ResolvedAssemblyName = new System.Reflection.AssemblyName(fusionName);
                            this.resolvedInfo.AssemblyName = this.resolvedInfo.ResolvedAssemblyName;
                        }
                        this.resolvedInfo.IsPlatformAssembly = 0 == string.Compare(MSBuildItem.GetMetadataValue(item, ProjectFileConstants.ResolvedFrom), "{TargetFrameworkDirectory}", StringComparison.OrdinalIgnoreCase);
                        this.resolvedInfo.IsNoPIA = 0 == string.Compare(MSBuildItem.GetMetadataValue(item, ProjectFileConstants.EmbedInteropTypes), "true", StringComparison.OrdinalIgnoreCase);
                        this.resolvedInfo.CopyLocalDefault = 0 == string.Compare(MSBuildItem.GetMetadataValue(item, ProjectFileConstants.CopyLocal), "true", StringComparison.OrdinalIgnoreCase);
                        this.resolvedInfo.WasSuccessfullyResolved = true;
                        this.myAssemblyPath = MSBuildItem.GetEvaluatedInclude(item);

                        if (!Path.IsPathRooted(this.myAssemblyPath))
                        {
                            this.myAssemblyPath = Path.Combine(this.ProjectMgr.ProjectFolder, this.myAssemblyPath);
                        }
                        // finished and found original item
                        return;
                    }
                }
            }
            // finished without finding original item
        }

        /// <summary>
        /// Registers with File change events
        /// </summary>
        private void InitializeFileChangeEvents()
        {
            this.myFileChangeListener = new FileChangeManager(this.ProjectMgr.Site);
            this.myFileChangeListener.FileChangedOnDisk += this.OnAssemblyReferenceChangedOnDisk;
        }

        /// <summary>
        /// Unregisters this node from file change notifications.
        /// </summary>
        private void UnregisterFromFileChangeService()
        {
            this.myFileChangeListener.FileChangedOnDisk -= this.OnAssemblyReferenceChangedOnDisk;
            this.myFileChangeListener.Dispose();
        }

        /// <summary>
        /// Event callback. Called when one of the assembly file is changed.
        /// </summary>
        /// <param name="sender">The FileChangeManager object.</param>
        /// <param name="e">Event args containing the file name that was updated.</param>
        private void OnAssemblyReferenceChangedOnDisk(object sender, FileChangedOnDiskEventArgs e)
        {
            Debug.Assert(e != null, "No event args specified for the FileChangedOnDisk event");

            // We only care about file deletes, so check for one before enumerating references.            
            if ((e.FileChangeFlag & _VSFILECHANGEFLAGS.VSFILECHG_Del) == 0)
            {
                return;
            }


            if (NativeMethods.IsSamePath(e.FileName, this.myAssemblyPath))
            {
                this.OnInvalidateItems(this.Parent);
            }
        }

        public override Guid GetBrowseLibraryGuid()
        {
            return VSConstants.guidCOMPLUSLibrary;
        }

        public override object GetProperty(int propId)
        {
            if (propId == (int)__VSHPROPID5.VSHPROPID_ProvisionalViewingStatus)
            {
                var objectBrowserGuid = VSProjectConstants.guidObjectBrowser;
                var logicalViewGuid = VSConstants.LOGVIEWID.Primary_guid;
                IVsUIShellOpenDocument3 shellOpenDocument3 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument3;
                return shellOpenDocument3.GetProvisionalViewingStatusForEditor(ref objectBrowserGuid, ref logicalViewGuid);
            }

            return base.GetProperty(propId);
        }
    }
}
