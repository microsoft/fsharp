// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.FSharp.ProjectSystem;
using VSLangProj;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    [SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible")]
    [ComVisible(true)]
    public class OAComReference : OAReferenceBase<ComReferenceNode>
    {
        internal OAComReference(ComReferenceNode comReference) :
            base(comReference)
        {
        }

        public override string Culture
        {
            get
            {
                var locale = BaseReferenceNode.LCID;

                if (0 == locale)
                {
                    return string.Empty;
                }
                CultureInfo culture = new CultureInfo(locale);
                return culture.Name;
            }
        }
        public override string Identity
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", BaseReferenceNode.TypeGuid.ToString("B"), this.Version);
            }
        }
        public override int MajorVersion
        {
            get { return BaseReferenceNode.MajorVersionNumber; }
        }
        public override int MinorVersion
        {
            get { return BaseReferenceNode.MinorVersionNumber; }
        }
        public override string Name
        {
            get { return BaseReferenceNode.Caption; }
        }
        public override VSLangProj.prjReferenceType Type
        {
            get
            {
                return VSLangProj.prjReferenceType.prjReferenceTypeActiveX;
            }
        }
        public override string Version
        {
            get
            {
                Version version = new Version(BaseReferenceNode.MajorVersionNumber, BaseReferenceNode.MinorVersionNumber);
                return version.ToString();
            }
        }
    }
}
