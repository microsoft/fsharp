// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.Shell;
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
    /// The purpose of DesignPropertyDescriptor is to allow us to customize the
    /// display name of the property in the property grid.  None of the CLR
    /// implementations of PropertyDescriptor allow you to change the DisplayName.
    /// </summary>
    internal class DesignPropertyDescriptor : PropertyDescriptor
    {
        private string displayName; // Custom display name
        private PropertyDescriptor property;    // Base property descriptor
        private Hashtable editors = new Hashtable(); // Type -> editor instance
        private TypeConverter converter;


        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override string DisplayName
        {
            get
            {
                return this.displayName;
            }
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type ComponentType
        {
            get
            {
                return this.property.ComponentType;
            }
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool IsReadOnly
        {
            get
            {
                return this.property.IsReadOnly;
            }
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type PropertyType
        {
            get
            {
                return this.property.PropertyType;
            }
        }


        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override object GetEditor(Type editorBaseType)
        {
            object editor = this.editors[editorBaseType];
            if (editor == null)
            {
                for (int i = 0; i < this.Attributes.Count; i++)
                {
                    EditorAttribute attr = Attributes[i] as EditorAttribute;
                    if (attr == null)
                    {
                        continue;
                    }
                    Type editorType = Type.GetType(attr.EditorBaseTypeName);
                    if (editorBaseType == editorType)
                    {
                        Type type = GetTypeFromNameProperty(attr.EditorTypeName);
                        if (type != null)
                        {
                            editor = CreateInstance(type);
                            this.editors[type] = editor; // cache it
                            break;
                        }
                    }
                }
            }
            return editor;
        }


        /// <summary>
        /// Return type converter for property
        /// </summary>
        public override TypeConverter Converter
        {
            get
            {
                if (converter == null)
                {
                    PropertyPageTypeConverterAttribute attr = (PropertyPageTypeConverterAttribute)Attributes[typeof(PropertyPageTypeConverterAttribute)];
                    if (attr != null && attr.ConverterType != null)
                    {
                        converter = (TypeConverter)CreateInstance(attr.ConverterType);
                    }

                    if (converter == null)
                    {
                        converter = property.Converter ?? TypeDescriptor.GetConverter(this.PropertyType);
                    }
                }
                return converter;
            }
        }



        /// <summary>
        /// Convert name to a Type object.
        /// </summary>
        public virtual Type GetTypeFromNameProperty(string typeName)
        {
            return Type.GetType(typeName);
        }


        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool CanResetValue(object component)
        {
            bool result = this.property.CanResetValue(component);
            return result;
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override object GetValue(object component)
        {
            object value = this.property.GetValue(component);
            return value;
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void ResetValue(object component)
        {
            this.property.ResetValue(component);
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void SetValue(object component, object value)
        {
            this.property.SetValue(component, value);
            OnValueChanged(component, EventArgs.Empty);
        }

        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool ShouldSerializeValue(object component)
        {
            bool result = this.property.ShouldSerializeValue(component);
            return result;
        }

        /// <summary>
        /// Constructor.  Copy the base property descriptor and also hold a pointer
        /// to it for calling its overridden abstract methods.
        /// </summary>
        public DesignPropertyDescriptor(PropertyDescriptor prop) : base(prop)
        {
            this.property = prop;

            Attribute attr = prop.Attributes[typeof(DisplayNameAttribute)];

            if (attr is DisplayNameAttribute)
            {
                this.displayName = ((DisplayNameAttribute)attr).DisplayName;
            }
            else
            {
                this.displayName = prop.Name;
            }
        }
    }
}
