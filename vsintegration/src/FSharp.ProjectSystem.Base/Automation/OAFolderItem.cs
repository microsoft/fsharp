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
using EnvDTE;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.FSharp.ProjectSystem;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    /// <summary>
    /// Represents an automation object for a folder in a project
    /// </summary>
    [SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible")]
    [ComVisible(true), CLSCompliant(false)]
    public class OAFolderItem : OAProjectItem<FolderNode>
    {
        internal OAFolderItem(OAProject project, FolderNode node)
            : base(project, node)
        {
        }

        public override ProjectItems Collection
        {
            get
            {
                return UIThread.DoOnUIThread(delegate() {
                    ProjectItems items = new OAProjectItems(this.Project, this.Node);
                    return items;
                });
            }
        }

        public override ProjectItems ProjectItems
        {
            get
            {
                return this.Collection;
            }
        }
    }
}
