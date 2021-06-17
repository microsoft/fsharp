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
using EnvDTE;
using Microsoft.VisualStudio.FSharp.ProjectSystem;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem.Automation
{
    [CLSCompliant(false), ComVisible(true)]
    public class OAProperty : EnvDTE.Property
    {
        private OAProperties parent;
        private PropertyInfo pi;

        internal OAProperty(OAProperties parent, PropertyInfo pi)
        {
            this.parent = parent;
            this.pi = pi;
        }

        /// <summary>
        /// For use by F# tooling only.
        /// </summary>
        public object Application
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the Collection containing the Property object supporting this property.
        /// </summary>
        public EnvDTE.Properties Collection
        {
            get
            {
                //todo: EnvDTE.Property.Collection
                return this.parent;
            }
        }

        /// <summary>
        /// Gets the top-level extensibility object.
        /// </summary>
        public EnvDTE.DTE DTE
        {
            get
            {
                return this.parent.DTE;
            }
        }

        /// <summary>
        /// Returns one element of a list. 
        /// </summary>
        /// <param name="index1">The index of the item to display.</param>
        /// <param name="index2">The index of the item to display. Reserved for future use.</param>
        /// <param name="index3">The index of the item to display. Reserved for future use.</param>
        /// <param name="index4">The index of the item to display. Reserved for future use.</param>
        /// <returns>The value of a property</returns>
        public object get_IndexedValue(object index1, object index2, object index3, object index4)
        {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0) return this.Value;
            object[] index = new object[len];
            Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);
            return this.pi.GetValue(this.parent.Target, index);
        }

        /// <summary>
        /// Setter function to set properties values. 
        /// </summary>
        /// <param name="value"></param>
        public void let_Value(object value)
        {
            this.Value = value;
        }

        /// <summary>
        /// Gets the name of the object.
        /// </summary>
        public string Name
        {
            get
            {
                return pi.Name;
            }
        }

        /// <summary>
        /// Gets the number of indices required to access the value.
        /// </summary>
        public short NumIndices
        {
            get { return (short)pi.GetIndexParameters().Length; }
        }

        /// <summary>
        /// Sets or gets the object supporting the Property object.
        /// </summary>
        public object Object
        {
            get
            {
                return this.parent.Target;
            }
            set
            {
            }
        }

        /// <summary>
        /// For use by F# tooling only.
        /// </summary>
        public EnvDTE.Properties Parent
        {
            get { return this.parent; }
        }

        /// <summary>
        /// Sets the value of the property at the specified index.
        /// </summary>
        /// <param name="index1">The index of the item to set.</param>
        /// <param name="index2">Reserved for future use.</param>
        /// <param name="index3">Reserved for future use.</param>
        /// <param name="index4">Reserved for future use.</param>
        /// <param name="value">The value to set.</param>
        public void set_IndexedValue(object index1, object index2, object index3, object index4, object value)
        {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0)
            {
                this.Value = value;
            }
            else
            {
                object[] index = new object[len];
                Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);

                IVsExtensibility3 extensibility = this.parent.Target.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();

                try
                {
                    this.pi.SetValue(this.parent.Target, value, index);
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            }

        }

        /// <summary>
        /// Gets or sets the value of the property returned by the Property object.
        /// </summary>
        public object Value
        {
            get { return pi.GetValue(this.parent.Target, null); }
            set
            {
                IVsExtensibility3 extensibility = this.parent.Target.Node.ProjectMgr.Site.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;

                if (extensibility == null)
                {
                    throw new InvalidOperationException();
                }

                extensibility.EnterAutomationFunction();

                try
                {
                    this.pi.SetValue(this.parent.Target, value, null);
                }
                finally
                {
                    extensibility.ExitAutomationFunction();
                }
            }
        }
    }
}
