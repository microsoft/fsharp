// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Reflection;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.FSharp.ProjectSystem;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    /// <summary>
    /// This object defines a so called null object that is returned as instead of null. This is because callers in VSCore usually crash if a null propery is returned for them.
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    public class OANullProperty : EnvDTE.Property
    {
        private OAProperties parent;

        internal OANullProperty(OAProperties parent)
        {
            this.parent = parent;
        }

        public object Application
        {
            get { return String.Empty; }
        }

        public EnvDTE.Properties Collection
        {
            get
            {
                //todo: EnvDTE.Property.Collection
                return this.parent;
            }
        }

        public EnvDTE.DTE DTE
        {
            get { return null; }
        }

        public object get_IndexedValue(object index1, object index2, object index3, object index4)
        {
            return String.Empty;
        }

        public void let_Value(object value)
        {
            //todo: let_Value
        }

        public string Name
        {
            get { return String.Empty; }
        }

        public short NumIndices
        {
            get { return 0; }
        }

        public object Object
        {
            get { return this.parent.Target; }
            set
            {
            }
        }

        public EnvDTE.Properties Parent
        {
            get { return this.parent; }
        }

        public void set_IndexedValue(object index1, object index2, object index3, object index4, object value)
        {

        }

        public object Value
        {
            get { return String.Empty; }
            set { }
        }
    }
}
