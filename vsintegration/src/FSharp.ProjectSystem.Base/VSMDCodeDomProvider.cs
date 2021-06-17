// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.CodeDom.Compiler;
using Microsoft.VisualStudio.Designer.Interfaces;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal class VSMDCodeDomProvider : IVSMDCodeDomProvider
    {
        private CodeDomProvider provider;
        public VSMDCodeDomProvider(CodeDomProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            this.provider = provider;
        }

        object IVSMDCodeDomProvider.CodeDomProvider
        {
            get { return provider; }
        }
    }
}
