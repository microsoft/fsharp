// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.IO;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal class EnumDependencies : IVsEnumDependencies
    {
        private List<IVsDependency> dependencyList = new List<IVsDependency>();

        private uint nextIndex = 0;

        public EnumDependencies(IList<IVsDependency> dependencyList)
        {
            foreach (IVsDependency dependency in dependencyList)
            {
                this.dependencyList.Add(dependency);
            }
        }

        public EnumDependencies(IList<IVsBuildDependency> dependencyList)
        {
            foreach (IVsBuildDependency dependency in dependencyList)
            {
                this.dependencyList.Add(dependency);
            }
        }

        public int Clone(out IVsEnumDependencies enumDependencies)
        {
            enumDependencies = new EnumDependencies(this.dependencyList);
            enumDependencies.Skip(this.nextIndex);
            return VSConstants.S_OK;
        }

        public int Next(uint elements, IVsDependency[] dependencies, out uint elementsFetched)
        {
            uint fetched = 0;
            int count = this.dependencyList.Count;

            while (this.nextIndex < count && elements > 0 && fetched < count)
            {
                dependencies[fetched] = this.dependencyList[(int)this.nextIndex];
                this.nextIndex++;
                fetched++;
                elements--;

            }

            elementsFetched = fetched;

            // Did we get 'em all?
            return (elements == 0 ? VSConstants.S_OK : VSConstants.S_FALSE);
        }

        public int Reset()
        {
            this.nextIndex = 0;
            return VSConstants.S_OK;
        }

        public int Skip(uint elements)
        {
            this.nextIndex += elements;
            uint count = (uint)this.dependencyList.Count;

            if (this.nextIndex > count)
            {
                this.nextIndex = count;
                return VSConstants.S_FALSE;
            }
            return VSConstants.S_OK;
        }
    }
}
