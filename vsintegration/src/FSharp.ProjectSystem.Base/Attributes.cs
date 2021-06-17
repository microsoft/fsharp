// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{ 
    /// <summary>
    /// Defines a type converter.
    /// </summary>
    /// <remarks>This is needed to get rid of the type TypeConverter type that could not give back the Type we were passing to him.
    /// We do not want to use reflection to get the type back from the  ConverterTypeName. Also the GetType methos does not spwan converters from other assemblies.</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class PropertyPageTypeConverterAttribute : Attribute
    {
        Type converterType;

        public PropertyPageTypeConverterAttribute(Type type)
        {
            this.converterType = type;
        } 

        public Type ConverterType
        {
            get
            {
                return this.converterType;
            }
        } 
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class LocDisplayNameAttribute : DisplayNameAttribute
    {
        string name;

        public LocDisplayNameAttribute(string name)
        {
            this.name = name;
        } 

        public override string DisplayName
        {
            get
            {
                string result = SR.GetString(this.name, CultureInfo.CurrentUICulture);
                if (result == null)
                {
                    Debug.Assert(false, "String resource '" + this.name + "' is missing");
                    result = this.name;
                }
                return result;
            }
        } 
    }
}
