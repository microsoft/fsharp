// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.FSharp.ProjectSystem;
using VSLangProj;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    /// <summary>
    /// Represents the automation equivalent of ReferenceNode
    /// </summary>
    /// <typeparam name="RefType"></typeparam>
    [SuppressMessage("Microsoft.Naming", "CA1715:IdentifiersShouldHaveCorrectPrefix", MessageId = "T")]
    [ComVisible(true)]
    public class OAReferenceBase<RefType> : Reference
        where RefType : ReferenceNode
    {
        private RefType referenceNode;

        internal OAReferenceBase(RefType referenceNode)
        {
            this.referenceNode = referenceNode;
        }

        public RefType BaseReferenceNode
        {
            get { return referenceNode; }
        }

        public virtual int BuildNumber
        {
            get { return 0; }
        }

        public virtual References Collection
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return BaseReferenceNode.Parent.Object as References;
                });
            }
        }

        public virtual EnvDTE.Project ContainingProject
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return BaseReferenceNode.ProjectMgr.GetAutomationObject() as EnvDTE.Project;
                });
            }
        }

        public virtual bool CopyLocal
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return ((ReferenceNodeProperties)referenceNode.NodeProperties).CopyToLocal;
                });
            }
            set
            {
                UIThread.DoOnUIThread(delegate(){
                    ((ReferenceNodeProperties)referenceNode.NodeProperties).CopyToLocal = value;
                });
            }
        }

        public virtual string Culture
        {
            get { throw new NotImplementedException(); }
        }

        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return BaseReferenceNode.ProjectMgr.Site.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                });
            }
        }

        public virtual string Description
        {
            get
            {
                return this.Name;
            }
        }

        public virtual string ExtenderCATID
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return ((ReferenceNodeProperties)referenceNode.NodeProperties).ExtenderCATID;
                });
            }
        }

        public virtual object ExtenderNames
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return ((ReferenceNodeProperties)referenceNode.NodeProperties).ExtenderNames();
                });
            }
        }

        public virtual string Identity
        {
            get { throw new NotImplementedException(); }
        }

        public virtual int MajorVersion
        {
            get { return 0; }
        }

        public virtual int MinorVersion
        {
            get { return 0; }
        }

        public virtual string Name
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return ((ReferenceNodeProperties)referenceNode.NodeProperties).Name;
                });
            }
        }

        public virtual string Path
        {
            get
            {
                return UIThread.DoOnUIThread(delegate(){
                    return BaseReferenceNode.Url;
                });
            }
        }

        public virtual string PublicKeyToken
        {
            get { throw new NotImplementedException(); }
        }

        public virtual void Remove()
        {
            UIThread.DoOnUIThread(delegate(){
                BaseReferenceNode.Remove(removeFromStorage: false);
            });
        }

        public virtual int RevisionNumber
        {
            get { return 0; }
        }

        public virtual EnvDTE.Project SourceProject
        {
            get { return null; }
        }

        public virtual bool StrongName
        {
            get { return false; }
        }

        public virtual prjReferenceType Type
        {
            get { throw new NotImplementedException(); }
        }

        public virtual string Version
        {
            get { return new Version().ToString(); }
        }

        public virtual object get_Extender(string ExtenderName)
        {
            return UIThread.DoOnUIThread(delegate(){
                return ((ReferenceNodeProperties)referenceNode.NodeProperties).Extender(ExtenderName);
            });
        }
    }
}
