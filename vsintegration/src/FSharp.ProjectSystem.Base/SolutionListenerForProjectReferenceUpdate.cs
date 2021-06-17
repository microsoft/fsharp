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
using System.Linq;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal class SolutionListenerForProjectReferenceUpdate : SolutionListener
    {
        public SolutionListenerForProjectReferenceUpdate(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// Notifies listening clients that the project has been opened. 
        /// This method is called when on opening solution and when project is reloaded.
        /// If project was modified before opening (i.e. target framework moniker was changed) then it can:
        ///   1) have bad references - new version of target framework is lower than version of target framework in one of project references
        ///   2) be the cause of bad references - current project A was referenced by project B and new version of target framework in A is higher than in B
        /// To deal with this situation we renew state of error in:
        ///   - project references of current project (solve 1)
        ///   - project references of all project that point to current project (solve 2)
        /// </summary>
        public override int OnAfterOpenProject(IVsHierarchy hierarchy, int added)
        {
            var projectReferences = GetProjectReferencesContainingThisProject(hierarchy);
            foreach (var projRef in projectReferences)
            {
                // refresh all references to specified project
                projRef.RefreshProjectReferenceErrorState();
            }

            var refContainerProvider = hierarchy as IReferenceContainerProvider;
            if (refContainerProvider != null)
            {
                var refContainer = refContainerProvider.GetReferenceContainer();
                foreach (var projRef in refContainer.EnumReferences().OfType<ProjectReferenceNode>())
                {
                    projRef.RefreshProjectReferenceErrorState();
                }
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Delete this project from the references of projects of this type, if it is found.
        /// </summary>
        /// <param name="hierarchy"></param>
        /// <param name="removed"></param>
        /// <returns></returns>
        public override int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
        {
            if (removed != 0)
            {
                List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(hierarchy);

                foreach (ProjectReferenceNode projectReference in projectReferences)
                {
                    // Remove will delete error associated with reference
                    projectReference.Remove(false);
                    // Set back the remove state on the project refererence. The reason why we are doing this is that the OnBeforeUnloadProject immedaitely calls
                    // OnBeforeCloseProject, thus we would be deleting references when we should not. Unload should not remove references.
                    projectReference.CanRemoveReference = true;
                }
            }

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Needs to update the dangling reference on projects that contain this hierarchy as project reference.
        /// </summary>
        /// <param name="stubHierarchy"></param>
        /// <param name="realHierarchy"></param>
        /// <returns></returns>
        public override int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(realHierarchy);

            // Refersh the project reference node. That should trigger the drawing of the normal project reference icon.
            foreach (ProjectReferenceNode projectReference in projectReferences)
            {
                projectReference.CanRemoveReference = true;

                projectReference.OnInvalidateItems(projectReference.Parent);
            }

            return VSConstants.S_OK;
        }


        public override int OnAfterRenameProject(IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
            {
                return VSConstants.E_INVALIDARG;
            }

            try
            {
                List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(hierarchy);

                // Collect data that is needed to initialize the new project reference node.
                string projectRef;
                ErrorHandler.ThrowOnFailure(this.Solution.GetProjrefOfProject(hierarchy, out projectRef));

                object nameAsObject;
                ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out nameAsObject));
                string projectName = (string)nameAsObject;

                string projectPath = String.Empty;

                if (hierarchy is IVsProject3)
                {
                    IVsProject3 project = (IVsProject3)hierarchy;

                    ErrorHandler.ThrowOnFailure(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out projectPath));
                }

                // Remove and re add the node.
                foreach (ProjectReferenceNode projectReference in projectReferences)
                {
                    ProjectNode projectMgr = projectReference.ProjectMgr;
                    IReferenceContainer refContainer = projectMgr.GetReferenceContainer();
                    projectReference.Remove(false);

                    VSCOMPONENTSELECTORDATA selectorData = new VSCOMPONENTSELECTORDATA();
                    selectorData.type = VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project;
                    selectorData.bstrTitle = projectName;
                    selectorData.bstrFile = projectPath;
                    selectorData.bstrProjRef = projectRef;
                    refContainer.AddReferenceFromSelectorData(selectorData);
                }
            }
            catch (COMException e)
            {
                Trace.WriteLine("Exception :" + e.Message);
                return e.ErrorCode;
            }

            return VSConstants.S_OK;
        }


        public override int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(realHierarchy);

            // Refresh the project reference node. That should trigger the drawing of the dangling project reference icon.
            foreach (ProjectReferenceNode projectReference in projectReferences)
            {
                projectReference.IsNodeValid = true;
                projectReference.OnInvalidateItems(projectReference.Parent);
                projectReference.CanRemoveReference = false;
                projectReference.IsNodeValid = false;
                projectReference.DropReferencedProjectCache();

                // delete any 'reference' related errors that were induced by this project
                projectReference.CleanProjectReferenceErrorState();
            }

            return VSConstants.S_OK;

        }

        private List<ProjectReferenceNode> GetProjectReferencesContainingThisProject(IVsHierarchy inputHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = new List<ProjectReferenceNode>();
            if (this.Solution == null || inputHierarchy == null)
            {
                return projectReferences;
            }

            uint flags = (uint)(__VSENUMPROJFLAGS.EPF_ALLPROJECTS | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION);
            Guid enumOnlyThisType = Guid.Empty;
            IEnumHierarchies enumHierarchies = null;

            ErrorHandler.ThrowOnFailure(this.Solution.GetProjectEnum(flags, ref enumOnlyThisType, out enumHierarchies));
            Debug.Assert(enumHierarchies != null, "Could not get list of hierarchies in solution");

            IVsHierarchy[] hierarchies = new IVsHierarchy[1];
            uint fetched;
            int returnValue = VSConstants.S_OK;
            do
            {
                returnValue = enumHierarchies.Next(1, hierarchies, out fetched);
                Debug.Assert(fetched <= 1, "We asked one project to be fetched VSCore gave more than one. We cannot handle that");
                if (returnValue == VSConstants.S_OK && fetched == 1)
                {
                    IVsHierarchy hierarchy = hierarchies[0];
                    Debug.Assert(hierarchy != null, "Could not retrieve a hierarchy");
                    IReferenceContainerProvider provider = hierarchy as IReferenceContainerProvider;
                    if (provider != null)
                    {
                        IReferenceContainer referenceContainer = provider.GetReferenceContainer();

                        Debug.Assert(referenceContainer != null, "Could not found the References virtual node");
                        ProjectReferenceNode projectReferenceNode = this.GetProjectReferenceOnNodeForHierarchy(referenceContainer.EnumReferences(), inputHierarchy);
                        if (projectReferenceNode != null)
                        {
                            projectReferences.Add(projectReferenceNode);
                        }
                    }
                }
            } while (returnValue == VSConstants.S_OK && fetched == 1);

            return projectReferences;
        }

        private ProjectReferenceNode GetProjectReferenceOnNodeForHierarchy(IList<ReferenceNode> references, IVsHierarchy inputHierarchy)
        {
            if (references == null)
            {
                return null;
            }

            Guid projectGuid;
            inputHierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out projectGuid);

            string canonicalName;
            inputHierarchy.GetCanonicalName(VSConstants.VSITEMID_ROOT, out canonicalName);
            foreach (ReferenceNode refNode in references)
            {
                ProjectReferenceNode projRefNode = refNode as ProjectReferenceNode;
                if (projRefNode != null)
                {
                    if (projRefNode.ReferencedProjectGuid == projectGuid)
                    {
                        return projRefNode;
                    }

                    // Try with canonical names, if the project that is removed is an unloaded project than the above criteria will not pass.
                    if (!String.IsNullOrEmpty(projRefNode.Url) && NativeMethods.IsSamePath(projRefNode.Url, canonicalName))
                    {
                        return projRefNode;
                    }
                }
            }

            return null;

        }
    }
}
