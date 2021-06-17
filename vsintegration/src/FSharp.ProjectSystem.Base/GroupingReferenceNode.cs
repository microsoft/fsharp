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
using Microsoft.Internal.VisualStudio.PlatformUI;
using System.ComponentModel;


namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Synthetic node that represents grouping of references in project (like the whole set of assemblies for some target framework profile)
    /// </summary>
    [ComVisible(true)]
    public class GroupingReferenceNode : ReferenceNode
    {
        public string Name { get; private set; }
        public string Identity { get; private set; }
        public string ResolutionPath { get; private set; }
        public string Version { get; private set; }
        public string[] GroupedItems { get; private set; }

        public GroupingReferenceNode(ProjectNode project, string name, string identity, string resolutionPath, string version, string[] groupedItems) 
            : base(project)
        {
            Name = name;
            Identity = identity;
            ResolutionPath = resolutionPath;
            Version = version;
            GroupedItems = groupedItems;
        }

        public override string Caption
        {
            get { return Name; }
        }
        
        public override bool CanShowDefaultIcon()
        {
            return true;
        }

        public override NodeProperties CreatePropertiesObject()
        {
            return new GroupingReferenceNodeProperties(this);
        }

        public override void BindReferenceData()
        {
            // this is a synthetic node that is not bound to any particular reference - do nothing
        }

        // 'Show in object browser' action for this menu item will just open ObjectBrowser (similar to C#)
        public override Guid GetBrowseLibraryGuid()
        {
            return VSConstants.guidCOMPLUSLibrary;
        }

        public override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            return false;
        }

        protected override bool CanShowUrlInOnObjectBrowser()
        {
            return !string.IsNullOrEmpty(Url) && Directory.Exists(Url);
        }

        public override string Url
        {
            get { return ResolutionPath; }
        }
    }

    /// <summary>
    /// Set of properties that are shown in property grid
    /// </summary>
    [ComVisible(true)]
    public class GroupingReferenceNodeProperties : NodeProperties
    {
        private GroupingReferenceNode owner;
        public GroupingReferenceNodeProperties(GroupingReferenceNode owner)
            : base(owner)
        {
            this.owner = owner;
        }
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.RefName)]
        [SRDescriptionAttribute(SR.RefNameDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public override string Name
        {
            get { return owner.Name; }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.Identity)]
        [SRDescriptionAttribute(SR.IdentityDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public string Identity
        {
            get { return owner.Identity; }
        }

        [SRCategoryAttribute(SR.Misc)]
        [Browsable(true)]
        [LocDisplayName(SR.Version)]
        [SRDescriptionAttribute(SR.VersionDescription)]
        [AutomationBrowsable(true)]
        public string Version
        {
            get { return owner.Version; }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public string ResolvedFrom
        {
            get { return owner.ResolutionPath; }
        }
        
        /// <summary>
        /// Caption of the property grid, prints 'Reference properties' similar to C#
        /// </summary>
        /// <returns></returns>
        public override string GetClassName()
        {
            return SR.GetString(SR.ReferenceProperties, CultureInfo.CurrentUICulture);
        }
    }
}