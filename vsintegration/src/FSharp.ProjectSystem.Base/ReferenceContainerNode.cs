// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Linq;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;


namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal enum AddReferenceDialogTab
    {
        DotNetTab = 0,
        BrowseTab = 1
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ReferenceContainerNode : HierarchyNode, IReferenceContainer
    {
        public const string ReferencesNodeVirtualName = "References";
        private List<ProjectReferenceNode> projectReferencesWithEnabledCaching = new List<ProjectReferenceNode>();
        
        internal ReferenceContainerNode(ProjectNode root) : base(root)
        {
            this.VirtualNodeName = ReferencesNodeVirtualName;
            this.ExcludeNodeFromScc = true;
        }

        private static string[] supportedReferenceTypes = new string[] {
            ProjectFileConstants.ProjectReference,
            ProjectFileConstants.Reference,
            ProjectFileConstants.COMReference
        };
        public virtual string[] SupportedReferenceTypes
        {
            get { return supportedReferenceTypes; }
        }

        public override int SortPriority
        {
            get 
            {
                return DefaultSortOrderNode.ReferenceContainerNode;
            }
        }
        
        public override int MenuCommandId
        {
            get { return VsMenus.IDM_VS_CTXT_REFERENCEROOT; }
        }


        public override Guid ItemTypeGuid
        {
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }


        public override string Url
        {
            get { return this.VirtualNodeName; }
        }

        public override string Caption
        {
            get
            {
                return SR.GetString(SR.ReferencesNodeName, CultureInfo.CurrentUICulture);
            }
        }


        private Automation.OAReferences references;
        public override object Object
        {
            get
            {
                if (null == references)
                {
                    references = new Automation.OAReferences(this);
                }
                return references;
            }
        }

        public override void AddChild(HierarchyNode node)
        {
            base.AddChild(node);
            EnableCachingForProjectReferencesInBatchUpdate(node);
        }

        /// <summary>
        /// Returns an instance of the automation object for ReferenceContainerNode
        /// </summary>
        /// <returns>An intance of the Automation.OAReferenceFolderItem type if succeeeded</returns>
        public override object GetAutomationObject()
        {
            if (this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return null;
            }

            return new Automation.OAReferenceFolderItem(this.ProjectMgr.GetAutomationObject() as Automation.OAProject, this);
        }

        /// <summary>
        /// Disable inline editing of Caption of a ReferendeContainerNode
        /// </summary>
        /// <returns>null</returns>
        public override string GetEditLabel()
        {
            return null;
        }


        public override object GetIconHandle(bool open)
        {
            return this.ProjectMgr.ImageHandler.GetIconHandle((int)ProjectNode.ImageName.ReferenceFolder);
        }

        
        /// <summary>
        /// References node cannot be dragged.
        /// </summary>
        /// <returns>A stringbuilder.</returns>
        public override StringBuilder PrepareSelectedNodesForClipBoard()
        {
            return null;
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public override int ExcludeFromProject()
        {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch ((VsCommands)cmd)
                {
                    case VsCommands.AddNewItem:
                    case VsCommands.AddExistingItem:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                if ((VsCommands2K)cmd == VsCommands2K.ADDREFERENCE)
                {
                    result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                    return VSConstants.S_OK;
                }
            }
            else if(cmdGroup == VSProjectConstants.FSharpSendReferencesToInteractiveCmd.Guid)
            {
                if (cmd == VSProjectConstants.FSharpSendReferencesToInteractiveCmd.ID)
                {
                    foreach(var reference in EnumReferences())
                    {
                        if (!reference.CanBeReferencedFromFSI())
                        {
                            continue;
                        }
                        result |= QueryStatusResult.SUPPORTED;
                        if (reference.GetReferenceForFSI() != null)
                        {
                            result |= QueryStatusResult.ENABLED;
                            break;
                        }
                    }
                    return VSConstants.S_OK;
                }
            }
            else
            {
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                switch ((VsCommands2K)cmd)
                {
                    case VsCommands2K.ADDREFERENCE:
                        return this.ProjectMgr.AddProjectReference();
                    case VsCommands2K.ADDWEBREFERENCE:
                        return this.ProjectMgr.AddWebReference();
                }
            }
            else if (cmdGroup == VSProjectConstants.FSharpSendReferencesToInteractiveCmd.Guid)
            {
                if (cmd == VSProjectConstants.FSharpSendReferencesToInteractiveCmd.ID)
                {
                    var references = 
                        EnumReferences()
                        .Select(node => node.CanBeReferencedFromFSI() ? node.GetReferenceForFSI() : null)
                        .Where(r => r != null)
                        .ToArray();                    
                    ProjectMgr.SendReferencesToFSI(references);
                    return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        public override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            return false;
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the refererences icon.
        /// </summary>
        /// <returns></returns>
        public override bool CanShowDefaultIcon()
        {
            if (!String.IsNullOrEmpty(this.VirtualNodeName))
            {
                return true;
            }
            return false;
        }

        public void BeginBatchUpdate()
        {
            foreach (var r in EnumReferences())
            {
                EnableCachingForProjectReferencesInBatchUpdate(r);
            }
        }

        public void EndBatchUpdate()
        {
            foreach (var projRef in projectReferencesWithEnabledCaching)
            {
                projRef.EnableCaching = false;
            }
            projectReferencesWithEnabledCaching.Clear();
        }

        public IList<ReferenceNode> EnumReferences()
        {
            List<ReferenceNode> refs = new List<ReferenceNode>();
            for (HierarchyNode node = this.FirstChild; node != null; node = node.NextSibling)
            {
                ReferenceNode refNode = node as ReferenceNode;
                if (refNode != null)
                {
                    refs.Add(refNode);
                }
            }

            return refs;
        }

        // Taken from vsproject\langbuild\langcompiler.cpp (ParseReferenceGroup)
        //
        // The reference group identity is like:
        //   - ".NETCore,Version=4.5" for .net core
        //   - "Windows,Version=8.0" for windows sdk winmds        
        private Version ParseVersionFromReferenceGrouping(string grouping)
        {
            // find the first ',', the left part is name, the right part is version
            var index = grouping.IndexOf(',');
            if (index == -1)
                return null;

            // search for the first digit character
            while (true)
            {
                if (index >= grouping.Length)
                    return null;

                if (char.IsDigit(grouping[index]))
                    break;
                else
                    index++;
            }

            var version = grouping.Substring(index);
            
            Version result;
            return Version.TryParse(version, out result) ? result : null;
        }

        /// <summary>
        /// Adds references to this container from a MSBuild project.
        /// </summary>
        public void LoadReferencesFromBuildProject(Microsoft.Build.Evaluation.Project buildProject)
        {            
            Build.Execution.ProjectInstance projectInstanceToSearchExpandedReferences = null;
            // if project uses implicitly expanded list of references, 
            // evaluate ImplicitlyExpandTargetFramework target and collect all resolved 'ReferencePath' items
            // later we'll group them and create special reference nodes that represent assembly groupings
            if (ProjectMgr.ImplicitlyExpandTargetFramework)
            {
                var res = ProjectMgr.Build(MsBuildTarget.ImplicitlyExpandTargetFramework);
                if (res.IsSuccessful)
                {
                    projectInstanceToSearchExpandedReferences = res.ProjectInstance;
                }                
            }

            // collect groupled framework references
            if (projectInstanceToSearchExpandedReferences != null)
            {
                // fetch all 'ReferencePath' items that were resolved from implicitly expanded references (metadata#ResolvedFrom = ImplicitlyExpandTargetFramework)
                var groupings =
                    projectInstanceToSearchExpandedReferences
                        .GetItems(ProjectFileConstants.ReferencePath)
                        .Where(item => string.Equals(item.GetMetadataValue(ProjectFileConstants.ResolvedFrom), MsBuildTarget.ImplicitlyExpandTargetFramework, StringComparison.OrdinalIgnoreCase))
                        .Select(
                            item =>
                                new
                                {
                                    referenceGroupingDisplayName = item.GetMetadataValue("ReferenceGroupingDisplayName"),
                                    referenceGrouping = item.GetMetadataValue("ReferenceGrouping"),
                                    file =  item.EvaluatedInclude
                                }
                            )
                        .Where(r =>
                            !string.IsNullOrEmpty(r.referenceGrouping) &&
                            !string.IsNullOrEmpty(r.referenceGroupingDisplayName) &&
                            !string.IsNullOrEmpty(r.file) &&
                            File.Exists(r.file)
                            )
                        .GroupBy(r => r.referenceGrouping);

                foreach (var grouping in groupings)
                {
                    var version = ParseVersionFromReferenceGrouping(grouping.Key);
                    if (version == null)
                    {
                        continue;
                    }
                    // pick property values from the first item - they should be the same for all elements in the grouping
                    var first = grouping.First();
                    var groupedFiles = grouping.Select(x => x.file).ToArray();

                    var versonText = string.Format(
                        "{0}.{1}.{2}.{3}",
                        version.Major,
                        version.Minor,
                        version.Build != -1 ? version.Build : 0,
                        version.Revision != -1 ? version.Revision : 0
                        );

                    var node = new GroupingReferenceNode(ProjectMgr, first.referenceGroupingDisplayName, first.referenceGrouping, Path.GetDirectoryName(first.file), versonText, groupedFiles);
                    AddChild(node);
                }
            }

            var extraProperties = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("DesignTimeBuild", "true"),
            }.AsEnumerable();
            BuildResult buildResult = this.ProjectMgr.BuildWithExtraProperties(MsBuildTarget.ResolveAssemblyReferences, extraProperties);
            foreach (string referenceType in SupportedReferenceTypes)
            {
                bool isAssemblyReference = referenceType == ProjectFileConstants.Reference;
                if (isAssemblyReference && !buildResult.IsSuccessful)
                {
                    continue;
                }

                foreach (var item in MSBuildProject.GetItems(buildProject, referenceType))
                {
                    ProjectElement element = new ProjectElement(this.ProjectMgr, item, false);

                    ReferenceNode node = CreateReferenceNode(referenceType, element, buildResult);
                    if (node != null)
                    {
                        this.AddChild(node);
                    }
                    if (isAssemblyReference)
                    {
                        ProjectMgr.UpdateValueOfCanUseTargetFSharpCoreReferencePropertyIfNecessary(node);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a reference to this container using the selector data structure to identify it.
        /// </summary>
        /// <param name="selectorData">data describing selected component</param>
        /// <returns>Reference in case of a valid reference node has been created or already existed. Otherwise null</returns>
        public ReferenceNode AddReferenceFromSelectorData(VSCOMPONENTSELECTORDATA selectorData)
        {
            //Make sure we can edit the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            //Create the reference node
            ReferenceNode node = null;
            try
            {
                node = CreateReferenceNode(selectorData);
            }
            catch (ArgumentException)
            {
                // Some selector data was not valid. 
            }

            //Add the reference node to the project if we have a valid reference node
            if (node != null)
            {
                ReferenceNode existingNode;
                if (!node.IsAlreadyAdded(out existingNode))
                {
                    // Try to add
                    node.AddReference();
                    if (null == node.Parent)
                    {
                        // The reference was not added, so we can not return this item because it
                        // is not inside the project.
                        return null;
                    }
                }
                else
                {
                    IVsDetermineWizardTrust wizardTrust = this.GetService(typeof(SVsDetermineWizardTrust)) as IVsDetermineWizardTrust;
                    var isWizardRunning = 0;
                    if (wizardTrust != null)
                    {
                        ErrorHandler.ThrowOnFailure(wizardTrust.IsWizardRunning(out isWizardRunning));
                    }

                    // if assembly already exists in project and we are running under the wizard - do not raise error
                    if (isWizardRunning != 0)
                    {
                        return existingNode;
                    }
                    else
                    {
                        string message = string.IsNullOrEmpty(node.SimpleName) 
                            ? String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ReferenceAlreadyExists, CultureInfo.CurrentUICulture), node.Caption, existingNode.Caption)
                            : String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.ReferenceWithAssemblyNameAlreadyExists, CultureInfo.CurrentUICulture), node.Caption, node.SimpleName, existingNode.Caption);
                        throw new InvalidOperationException(message);
                    }
                }
            }

            return node;
        }

        private void EnableCachingForProjectReferencesInBatchUpdate(HierarchyNode node)
        {
            if (!ProjectMgr.IsInBatchUpdate) return;

            var projectReference = node as ProjectReferenceNode;
            if (projectReference == null) return;
            projectReference.EnableCaching = true;
            projectReferencesWithEnabledCaching.Add(projectReference);
        }

        internal virtual ReferenceNode CreateReferenceNode(string referenceType, ProjectElement element, BuildResult buildResult)
        {
            ReferenceNode node = null;
            if (referenceType == ProjectFileConstants.COMReference)
            {
                node = this.CreateComReferenceNode(element);
            }
            else if (referenceType == ProjectFileConstants.Reference)
            {
                node = this.CreateAssemblyReferenceNode(element, buildResult);
            }
            else if (referenceType == ProjectFileConstants.ProjectReference)
            {
                node = this.CreateProjectReferenceNode(element);
                EnableCachingForProjectReferencesInBatchUpdate(node);
            }

            return node;
        }

        internal virtual ReferenceNode CreateReferenceNode(VSCOMPONENTSELECTORDATA selectorData)
        {
            ReferenceNode node = null;
            switch (selectorData.type)
            {
                case VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project:
                    node = this.CreateProjectReferenceNode(selectorData);
                    EnableCachingForProjectReferencesInBatchUpdate(node);
                    break;
                case VSCOMPONENTTYPE.VSCOMPONENTTYPE_File:
                    node = this.CreateFileComponent(selectorData, AddReferenceDialogTab.BrowseTab);
                    break;
                case VSCOMPONENTTYPE.VSCOMPONENTTYPE_ComPlus:
                    node = this.CreateFileComponent(selectorData, AddReferenceDialogTab.DotNetTab);
                    break;
                case VSCOMPONENTTYPE.VSCOMPONENTTYPE_Com2:
                    node = this.CreateComReferenceNode(selectorData);
                    break;
            }

            return node;
        }

        /// <summary>
        /// Creates a project reference node given an existing project element.
        /// </summary>
        internal virtual ProjectReferenceNode CreateProjectReferenceNode(ProjectElement element)
        {
            return new ProjectReferenceNode(this.ProjectMgr, element);
        }
        /// <summary>
        /// Create a Project to Project reference given a VSCOMPONENTSELECTORDATA structure
        /// </summary>
        public virtual ProjectReferenceNode CreateProjectReferenceNode(VSCOMPONENTSELECTORDATA selectorData)
        {
            return new ProjectReferenceNode(this.ProjectMgr, selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
        }

        /// <summary>
        /// Creates an assemby or com reference node given a selector data.
        /// </summary>
        internal virtual ReferenceNode CreateFileComponent(VSCOMPONENTSELECTORDATA selectorData, AddReferenceDialogTab tab)
        {
            if (null == selectorData.bstrFile)
            {
                throw new ArgumentNullException("selectordata.bstrFile");
            }

            // Support the "*<assemblyName>" format that Cider uses
            if (selectorData.bstrFile.StartsWith("*"))
            {
                var assemblyNameStr = selectorData.bstrFile.Substring(1);
                return this.CreateAssemblyReferenceNode(assemblyNameStr, tab, false /*isFullPath*/);
            }

            ReferenceNode node = null;

            try
            {
                node = this.CreateAssemblyReferenceNode(selectorData.bstrFile, tab, true);
            }
            catch(InvalidOperationException)
            {
                // If the selector doesn't have a guid, it means we didn't go through the COM tab. This means
                // that we are either adding a reference via automation, or manually browsing to a TLB.
                if (selectorData.guidTypeLibrary == Guid.Empty)
                {
                    try
                    {
                        node = this.CreateComReferenceNode(selectorData.bstrFile);
                    }
                    catch (COMException)
                    {
                        // Ignore all the TLB exceptions
                    }
                }
                else
                {
                    node = this.CreateComReferenceNode(selectorData);
                }
            }

            if (node == null)
            {
                this.ProjectMgr.AddReferenceCouldNotBeAddedErrorMessage(selectorData.bstrFile);
            }

            return node;
        }

        /// <summary>
        /// Creates an assembly refernce node from a project element.
        /// </summary>
        internal virtual AssemblyReferenceNode CreateAssemblyReferenceNode(ProjectElement element, BuildResult buildResult)
        {
            AssemblyReferenceNode node = null;
            try
            {
                node = AssemblyReferenceNode.CreateFromProjectFile(this.ProjectMgr, element, buildResult);
            }
            catch (ArgumentNullException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (FileNotFoundException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (BadImageFormatException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (FileLoadException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (System.Security.SecurityException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }

            return node;
        }
        /// <summary>
        /// Creates an assembly reference node from a file path.
        /// </summary>
        internal virtual AssemblyReferenceNode CreateAssemblyReferenceNode(string assemblyInclude, AddReferenceDialogTab tab, bool isFullPath)
        {
            AssemblyReferenceNode node = null;
            try
            {
                if (isFullPath)
                {
                    node = AssemblyReferenceNode.CreateFromFullPathViaUIAddReference(this.ProjectMgr, assemblyInclude, tab);
                }
                else
                {
                    node = AssemblyReferenceNode.CreateFromAssemblyNameViaUIAutomation(this.ProjectMgr, assemblyInclude);
                }
            }
            catch (ArgumentNullException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (FileNotFoundException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (BadImageFormatException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (FileLoadException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (System.Security.SecurityException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }

            return node;
        }

        /// <summary>
        /// Creates a com reference node from the project element.
        /// </summary>
        internal virtual ComReferenceNode CreateComReferenceNode(ProjectElement reference)
        {
            return new ComReferenceNode(this.ProjectMgr, reference);
        }
        /// <summary>
        /// Creates a com reference node from the string that represents the path to a file.
        /// </summary>
        private ComReferenceNode CreateComReferenceNode(string fileReference)
        {
            return new ComReferenceNode(this.ProjectMgr, fileReference);
        }
        /// <summary>
        /// Creates a com reference node from a selector data.
        /// </summary>
        public virtual ComReferenceNode CreateComReferenceNode(Microsoft.VisualStudio.Shell.Interop.VSCOMPONENTSELECTORDATA selectorData)
        {
            ComReferenceNode node = new ComReferenceNode(this.ProjectMgr, selectorData);
            return node;
        }
    }
}
