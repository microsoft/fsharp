// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;

using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    /// <summary>
    /// All public properties on Nodeproperties or derived classes are assumed to be used by Automation by default.
    /// Set this attribute to false on Properties that should not be visible for Automation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true), CLSCompliant(false)]
    public class AutomationBrowsableAttribute : System.Attribute
    {
        internal AutomationBrowsableAttribute(bool browsable)
        {
            this.browsable = browsable;
        }

        public bool Browsable
        {
            get
            {
                return this.browsable;
            }
        }

        private bool browsable;
    }

    /// <summary>
    ///  Encapsulates BuildAction enumeration
    /// </summary>
    public sealed class BuildAction
    {
        private readonly string actionName;

        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction None = new BuildAction("None");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction Compile = new BuildAction("Compile");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction Content = new BuildAction("Content");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction EmbeddedResource = new BuildAction("EmbeddedResource");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction ApplicationDefinition = new BuildAction("ApplicationDefinition");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction Page = new BuildAction("Page");
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "BuildAction type is immutable")]
        public static readonly BuildAction Resource = new BuildAction("Resource");

        internal BuildAction(string actionName)
        {
            this.actionName = actionName;
        }


        public string Name { get { return this.actionName; } }

        public override bool Equals(object other)
        {
            BuildAction action = other as BuildAction;
            return action != null && this.actionName == action.actionName;
        }

        public override int GetHashCode()
        {
            return this.actionName.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("BuildAction={0}", this.actionName);
        }

        public static bool operator ==(BuildAction b1, BuildAction b2)
        {
            return Equals(b1, b2);
        }

        public static bool operator !=(BuildAction b1, BuildAction b2)
        {
            return !(b1 == b2);
        }
    }

    /// <summary>
    /// To create your own localizable node properties, subclass this and add public properties
    /// decorated with your own localized display name, category and description attributes.
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    public class NodeProperties : LocalizableProperties,
        ISpecifyPropertyPages,
        IVsGetCfgProvider,
        IVsSpecifyProjectDesignerPages,
        EnvDTE80.IInternalExtenderProvider,
        IVsBrowseObject,
        IVsBuildMacroInfo
    {
        private HierarchyNode node;

        [Browsable(false)]
        [AutomationBrowsable(false)]
        public HierarchyNode Node
        {
            get { return this.node; }
        }

        /// <summary>
        /// Used by Property Pages Frame to set it's title bar. The Caption of the Hierarchy Node is returned.
        /// </summary>
        [Browsable(false)]
        [AutomationBrowsable(false)]
        public virtual string Name
        {
            get { return this.node.Caption; }
        }

        internal NodeProperties(HierarchyNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }
            this.node = node;
        }

        public virtual void GetPages(CAUUID[] pages)
        {
            // We do not check whether the supportsProjectDesigner is set to false on the ProjectNode.
            // We rely that the caller knows what to call on us.
            if (pages == null)
            {
                throw new ArgumentNullException("pages");
            }

            if (pages.Length == 0)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "pages");
            }

            // behave similar to C#\VB - return empty array
            pages[0] = new CAUUID();
            pages[0].cElems = 0;
        }

        /// <summary>
        /// We support this interface so the build event command dialog can display a list
        /// of tokens for the user to select from.
        /// </summary>
        public int GetBuildMacroValue(string buildMacroName, out string buildMacroValue)
        {
            buildMacroValue = string.Empty;

            if (string.IsNullOrEmpty(buildMacroName) == true)
                return NativeMethods.S_OK;

            if (buildMacroName.Equals("SolutionDir", StringComparison.OrdinalIgnoreCase) ||
                buildMacroName.Equals("SolutionPath", StringComparison.OrdinalIgnoreCase) ||
                buildMacroName.Equals("SolutionName", StringComparison.OrdinalIgnoreCase) ||
                buildMacroName.Equals("SolutionFileName", StringComparison.OrdinalIgnoreCase) ||
                buildMacroName.Equals("SolutionExt", StringComparison.OrdinalIgnoreCase))
            {
                string solutionPath = Environment.GetEnvironmentVariable("SolutionPath");

                if (buildMacroName.Equals("SolutionDir", StringComparison.OrdinalIgnoreCase))
                {
                    buildMacroValue = Path.GetDirectoryName(solutionPath);
                    return NativeMethods.S_OK;
                }
                else if (buildMacroName.Equals("SolutionPath", StringComparison.OrdinalIgnoreCase))
                {
                    buildMacroValue = solutionPath;
                    return NativeMethods.S_OK;
                }
                else if (buildMacroName.Equals("SolutionName", StringComparison.OrdinalIgnoreCase))
                {
                    buildMacroValue = Path.GetFileNameWithoutExtension((string)solutionPath);
                    return NativeMethods.S_OK;
                }
                else if (buildMacroName.Equals("SolutionFileName", StringComparison.OrdinalIgnoreCase))
                {
                    buildMacroValue = Path.GetFileName((string)solutionPath);
                    return NativeMethods.S_OK;
                }
                else if (buildMacroName.Equals("SolutionExt", StringComparison.OrdinalIgnoreCase))
                {
                    buildMacroValue = Path.GetExtension((string)solutionPath);
                    return NativeMethods.S_OK;
                }
            }

            buildMacroValue = this.Node.ProjectMgr.GetBuildMacroValue(buildMacroName);
            return NativeMethods.S_OK;
        }

        /// <summary>
        /// Implementation of the IVsSpecifyProjectDesignerPages. It will retun the pages that are configuration independent.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        /// <returns></returns>
        public virtual int GetProjectDesignerPages(CAUUID[] pages)
        {
            this.GetCommonPropertyPages(pages);
            return VSConstants.S_OK;
        }

        public virtual int GetCfgProvider(out IVsCfgProvider p)
        {
            p = null;
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Maps back to the hierarchy or project item object corresponding to the browse object.
        /// </summary>
        /// <param name="hier">Reference to the hierarchy object.</param>
        /// <param name="itemid">Reference to the project item.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int GetProjectItem(out IVsHierarchy hier, out uint itemid)
        {
            if (this.node == null)
            {
                throw new InvalidOperationException();
            }
            hier = Node.ProjectMgr.InteropSafeIVsHierarchy;
            itemid = this.node.ID;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Get the Caption of the Hierarchy Node instance. If Caption is null or empty we delegate to base
        /// </summary>
        /// <returns>Caption of Hierarchy node instance</returns>
        public override string GetComponentName()
        {
            string caption = this.Node.Caption;
            if (string.IsNullOrEmpty(caption))
            {
                return base.GetComponentName();
            }
            else
            {
                return caption;
            }
        }

        public string GetProperty(string name, string def)
        {
            string a = this.Node.ItemNode.GetMetadata(name);
            return (a == null) ? def : a;
        }

        public void SetProperty(string name, string value)
        {
            this.Node.ItemNode.SetMetadata(name, value);
        }

        /// <summary>
        /// Retrieves the common property pages. The NodeProperties is the BrowseObject and that will be called to support 
        /// configuration independent properties.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        private void GetCommonPropertyPages(CAUUID[] pages)
        {
            // We do not check whether the supportsProjectDesigner is set to false on the ProjectNode.
            // We rely that the caller knows what to call on us.
            if (pages == null)
            {
                throw new ArgumentNullException("pages");
            }

            if (pages.Length == 0)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "pages");
            }

            // Only the project should show the property page the rest should show the project properties.
            if (this.node != null && (this.node is ProjectNode))
            {
                // Retrieve the list of guids from hierarchy properties.
                // Because a flavor could modify that list we must make sure we are calling the outer most implementation of IVsHierarchy
                string guidsList = String.Empty;
                IVsHierarchy hierarchy = Node.ProjectMgr.InteropSafeIVsHierarchy;
                object variant = null;
                ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList, out variant));
                guidsList = (string)variant;

                Guid[] guids = Utilities.GuidsArrayFromSemicolonDelimitedStringOfGuids(guidsList);
                if (guids == null || guids.Length == 0)
                {
                    pages[0] = new CAUUID();
                    pages[0].cElems = 0;
                }
                else
                {
                    pages[0] = PackageUtilities.CreateCAUUIDFromGuidArray(guids);
                }
            }
            else
            {
                pages[0] = new CAUUID();
                pages[0].cElems = 0;
            }
        }

        bool EnvDTE80.IInternalExtenderProvider.CanExtend(string extenderCATID, string extenderName, object extendeeObject)
        {
            IVsHierarchy outerHierarchy = Node.ProjectMgr.InteropSafeIVsHierarchy;
            if (outerHierarchy is EnvDTE80.IInternalExtenderProvider)
            {
                return ((EnvDTE80.IInternalExtenderProvider)outerHierarchy).CanExtend(extenderCATID, extenderName, extendeeObject);
            }
            return false;
        }

        object EnvDTE80.IInternalExtenderProvider.GetExtender(string extenderCATID, string extenderName, object extendeeObject, EnvDTE.IExtenderSite extenderSite, int cookie)
        {
            IVsHierarchy outerHierarchy = Node.ProjectMgr.InteropSafeIVsHierarchy;
            if (outerHierarchy is EnvDTE80.IInternalExtenderProvider)
            {
                return ((EnvDTE80.IInternalExtenderProvider)outerHierarchy).GetExtender(extenderCATID, extenderName, extendeeObject, extenderSite, cookie);
            }
            return null;
        }

        object EnvDTE80.IInternalExtenderProvider.GetExtenderNames(string extenderCATID, object extendeeObject)
        {
            IVsHierarchy outerHierarchy = Node.ProjectMgr.InteropSafeIVsHierarchy;
            if (outerHierarchy is EnvDTE80.IInternalExtenderProvider)
            {
                return ((EnvDTE80.IInternalExtenderProvider)outerHierarchy).GetExtenderNames(extenderCATID, extendeeObject);
            }
            return null;
        }

        [Browsable(false)]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "CATID")]
        public virtual string ExtenderCATID
        {
            get
            {
                Guid catid = this.Node.ProjectMgr.GetCATIDForType(this.GetType());
                if (Guid.Empty.CompareTo(catid) == 0)
                {
                    return null;
                }
                return catid.ToString("B");
            }
        }

        [Browsable(false)]
        public object ExtenderNames()
        {
            EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)this.Node.GetService(typeof(EnvDTE.ObjectExtenders));
            Debug.Assert(extenderService != null, "Could not get the ObjectExtenders object from the services exposed by this property object");
            if (extenderService == null)
            {
                throw new InvalidOperationException();
            }
            return extenderService.GetExtenderNames(this.ExtenderCATID, this);
        }

        public object Extender(string extenderName)
        {
            EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)this.Node.GetService(typeof(EnvDTE.ObjectExtenders));
            Debug.Assert(extenderService != null, "Could not get the ObjectExtenders object from the services exposed by this property object");
            if (extenderService == null)
            {
                throw new InvalidOperationException();
            }
            return extenderService.GetExtender(this.ExtenderCATID, extenderName, this);
        }
    }

    internal sealed class BuildActionPropertyDescriptor : DesignPropertyDescriptor
    {
        private readonly BuildActionConverter converter;

        /// <summary>
        /// Return type converter for property
        /// </summary>
        public override TypeConverter Converter
        {
            get
            {
                return this.converter;
            }
        }

        /// <summary>
        /// Constructor.  Copy the base property descriptor and also hold a pointer
        /// to it for calling its overridden abstract methods.
        /// </summary>
        internal BuildActionPropertyDescriptor(PropertyDescriptor prop, BuildActionConverter converter)
            : base(prop)
        {
            this.converter = converter;
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public abstract class BuildableNodeProperties : NodeProperties
    {
        /// <summary>
        /// Specifies the build action as a projBuildAction so that automation can get the
        /// expected enum value.
        /// </summary>
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        [TypeConverter(typeof(BuildActionTypeConverter))]
        public VSLangProj.prjBuildAction BuildAction
        {
            get
            {
                var res = BuildActionTypeConverter.Instance.ConvertFromString(this.Node.ProjectMgr.BuildActionConverter, this.Node.ItemNode.ItemName);
                if (res is VSLangProj.prjBuildAction)
                {
                    return (VSLangProj.prjBuildAction)res;
                }
                return VSLangProj.prjBuildAction.prjBuildActionNone;
            }
            set
            {
                this.Node.ItemNode.ItemName = BuildActionTypeConverter.Instance.ConvertToString(this.Node.ProjectMgr.BuildActionConverter, value);
            }
        }

        [Browsable(false)]
        [AutomationBrowsable(true)]
        public virtual string ItemType
        {
            get 
            {
                return BuildActionTypeConverter.Instance.ConvertToString(this.Node.ProjectMgr.BuildActionConverter, this.BuildAction);
            }
            set
            {
                var res = BuildActionTypeConverter.Instance.ConvertFromString(this.Node.ProjectMgr.BuildActionConverter, value);
                if (res is VSLangProj.prjBuildAction)
                {
                    this.BuildAction = (VSLangProj.prjBuildAction)res;
                }
                else
                {
                    this.BuildAction = VSLangProj.prjBuildAction.prjBuildActionNone;
                }                
            }
        }

        public override PropertyDescriptor CreateDesignPropertyDescriptor(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.PropertyType.Equals(typeof(BuildAction)))
            {
                return new BuildActionPropertyDescriptor(propertyDescriptor, this.Node.ProjectMgr.BuildActionConverter);
            }
            return base.CreateDesignPropertyDescriptor(propertyDescriptor);
        }


        internal BuildableNodeProperties(HierarchyNode node) : base(node) { }
    }

    class BuildActionTypeConverter : StringConverter
    {
        internal static readonly BuildActionTypeConverter Instance = new BuildActionTypeConverter();

        public BuildActionTypeConverter()
        {
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return (sourceType == typeof(string)) ? true : base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return (destinationType == typeof(string)) ? true : base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                var reply = ConvertToString(GetBuildActionConverter(context), value);
                if (reply != null) return reply;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public string ConvertToString(BuildActionConverter buildActionConverter, object value)
        {
            switch ((VSLangProj.prjBuildAction)value)
            {
            case VSLangProj.prjBuildAction.prjBuildActionCompile:
                return "Compile";
            case VSLangProj.prjBuildAction.prjBuildActionContent:
                return "Content";
            case VSLangProj.prjBuildAction.prjBuildActionEmbeddedResource:
                return "EmbeddedResource";
            case VSLangProj.prjBuildAction.prjBuildActionNone:
                return "None";
            default:
                if (buildActionConverter != null)
                {
                    // Not standard buildAction, so must have been registered.
                    // Convert it to the name of the BuildAction at position index in the StandardValues from the BuildActionConverter
                    int index = (int)value;
                    var actions = buildActionConverter.RegisteredBuildActions;
                    if (index >= 0 && index < actions.Count)
                    {
                        return actions[index].Name;
                    }
                }
                return "None";
            }
        }
        
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                var reply = ConvertFromString(GetBuildActionConverter(context), (string)value);
                if (reply != null) return reply;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public object ConvertFromString(BuildActionConverter buildActionConverter, string value)
        {
            if (value.Equals("Compile", StringComparison.OrdinalIgnoreCase))
            {
                return VSLangProj.prjBuildAction.prjBuildActionCompile;
            }
            else if (value.Equals("Content", StringComparison.OrdinalIgnoreCase))
            {
                return VSLangProj.prjBuildAction.prjBuildActionContent;
            }
            else if (value.Equals("EmbeddedResource", StringComparison.OrdinalIgnoreCase))
            {
                return VSLangProj.prjBuildAction.prjBuildActionEmbeddedResource;
            }
            else if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return VSLangProj.prjBuildAction.prjBuildActionNone;
            }
            else
            {
                if (buildActionConverter != null)
                {
                    // Not standard buildAction, so must have been registered.
                    // Convert it to the index in the StandardValues from the BuildActionConverter.
                    var actions = buildActionConverter.RegisteredBuildActions;
                    var reply = actions.ToList().FindIndex(i => value.Equals(i.Name, StringComparison.OrdinalIgnoreCase));
                    if (reply != -1) return (VSLangProj.prjBuildAction)reply;
                }
            }
            return VSLangProj.prjBuildAction.prjBuildActionNone;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var buildActionConverter = GetBuildActionConverter(context);
            if(buildActionConverter != null)
            {
                var results = new List<VSLangProj.prjBuildAction>();
                foreach (var a in buildActionConverter.RegisteredBuildActions)
                {
                    results.Add((VSLangProj.prjBuildAction)this.ConvertFrom(context, CultureInfo.CurrentUICulture, a.Name));
                }
                return new StandardValuesCollection(results);
            }
            else
            {
                return new StandardValuesCollection(new[] { VSLangProj.prjBuildAction.prjBuildActionNone, VSLangProj.prjBuildAction.prjBuildActionCompile, VSLangProj.prjBuildAction.prjBuildActionContent, VSLangProj.prjBuildAction.prjBuildActionEmbeddedResource });
            }
        }

        private static BuildActionConverter GetBuildActionConverter(ITypeDescriptorContext context)
        {
            if (context != null)
            {
                BuildableNodeProperties nodeProperties = context.Instance as BuildableNodeProperties;
                if (nodeProperties != null)
                {
                    return nodeProperties.Node.ProjectMgr.BuildActionConverter;
                }
            }
            return null;
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class FileNodeProperties : BuildableNodeProperties
    {
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.CopyToOutputDirectory)]
        [SRDescriptionAttribute(SR.CopyToOutputDirectoryDescription)]
        public virtual CopyToOutputDirectory CopyToOutputDirectory
        {
            get
            {
                string value = this.Node.ItemNode.GetEvaluatedMetadata(ProjectFileConstants.CopyToOutputDirectory);
                if (string.IsNullOrEmpty(value))
                {
                    return CopyToOutputDirectory.DoNotCopy;
                }
                return (CopyToOutputDirectory)Enum.Parse(typeof(CopyToOutputDirectory), value);
            }
            set
            {
                if (value == CopyToOutputDirectory.DoNotCopy)
                    this.Node.ItemNode.SetMetadata(ProjectFileConstants.CopyToOutputDirectory, null);
                else
                    this.Node.ItemNode.SetMetadata(ProjectFileConstants.CopyToOutputDirectory, value.ToString());
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FileName)]
        [SRDescriptionAttribute(SR.FileNameDescription)]
        public string FileName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }

        [Browsable(false)]
        public string Extension
        {
            get
            {
                return Path.GetExtension(this.Node.Caption);
            }
        }

        internal FileNodeProperties(HierarchyNode node)
            : base(node)
        {
        }

        public override string GetClassName()
        {
            return SR.GetString(SR.FileProperties, CultureInfo.CurrentUICulture);
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class DependentFileNodeProperties : BuildableNodeProperties
    {
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FileName)]
        [SRDescriptionAttribute(SR.FileNameDescription)]
        public virtual string FileName
        {
            get
            {
                return this.Node.Caption;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }

        internal DependentFileNodeProperties(HierarchyNode node)
            : base(node)
        {
        }

        public override string GetClassName()
        {
            return SR.GetString(SR.FileProperties, CultureInfo.CurrentUICulture);
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ProjectNodeProperties : NodeProperties
        , VSLangProj.ProjectProperties
    {
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFolder)]
        [SRDescriptionAttribute(SR.ProjectFolderDescription)]
        [AutomationBrowsable(false)]
        public string ProjectFolder
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFolder;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFile)]
        [SRDescriptionAttribute(SR.ProjectFileDescription)]
        [AutomationBrowsable(false)]
        public string ProjectFile
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFile;
            }
            set
            {
                this.Node.ProjectMgr.ProjectFile = value;
            }
        }

        [Browsable(false)]
        public string FileName
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFile;
            }
            set
            {
                this.Node.ProjectMgr.ProjectFile = value;
            }
        }

        [Browsable(false)]
        public string ReferencePath
        {
            get
            {
                return this.Node.ProjectMgr.GetProjectProperty(ProjectFileConstants.ReferencePath, true);
            }
            set
            {
                this.Node.ProjectMgr.SetProjectProperty(ProjectFileConstants.ReferencePath, value);
            }
        }

        [Browsable(false)]
        public string FullPath
        {
            get
            {
                string fullPath = this.Node.ProjectMgr.ProjectFolder;
                if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    return fullPath + Path.DirectorySeparatorChar;
                }
                else
                {
                    return fullPath;
                }
            }
        }

        internal ProjectNodeProperties(ProjectNode node)
            : base(node)
        {
        }

        public override string GetClassName()
        {
            return SR.GetString(SR.ProjectProperties, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// ICustomTypeDescriptor.GetEditor
        /// To enable the "Property Pages" button on the properties browser
        /// the browse object (project properties) need to be unmanaged
        /// or it needs to provide an editor of type ComponentEditor.
        /// </summary>
        /// <param name="editorBaseType">Type of the editor</param>
        /// <returns>Editor</returns>
        public override object GetEditor(Type editorBaseType)
        {
            // Override the scenario where we are asked for a ComponentEditor
            // as this is how the Properties Browser calls us
            if (editorBaseType == typeof(ComponentEditor))
            {
                IOleServiceProvider sp;
                ErrorHandler.ThrowOnFailure(this.Node.GetSite(out sp));
                return new PropertiesEditorLauncher(new ServiceProvider(sp));
            }

            return base.GetEditor(editorBaseType);
        }

        public override int GetCfgProvider(out IVsCfgProvider p)
        {
            if (this.Node != null && this.Node.ProjectMgr != null)
            {
                return this.Node.ProjectMgr.GetCfgProvider(out p);
            }

            return base.GetCfgProvider(out p);
        }

        string VSLangProj.ProjectProperties.__id
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.FullPath;
                    return Path.GetFileNameWithoutExtension(path);
                });
            }
        }

        VSLangProj.ProjectConfigurationProperties VSLangProj.ProjectProperties.ActiveConfigurationSettings
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {

                    var buildMgr = this.Node.ProjectMgr.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
                    var activeCfg = new IVsProjectCfg[] { null };
                    var hr = buildMgr.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, Node.ProjectMgr.InteropSafeIVsHierarchy, activeCfg);
                    if (ErrorHandler.Succeeded(hr))
                    {
                        return (VSLangProj.ProjectConfigurationProperties)((ProjectConfig)activeCfg[0]).ConfigurationProperties;
                    }
                    else
                    {
                        return null;
                    }
                });
            }
        }


        object VSLangProj.ProjectProperties.__project
        {
            get { return this.Node; }
        }

        string VSLangProj.ProjectProperties.AbsoluteProjectDirectory
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.FullPath;
                    return Path.GetDirectoryName(path);
                });
            }
        }

        string VSLangProj.ProjectProperties.LocalPath
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.DirectoryPath;
                    return path;
                });
            }
        }

        string VSLangProj.ProjectProperties.URL
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.FullPath;
                    return new Uri(path).AbsoluteUri;
                });
            }
        }

        string VSLangProj.ProjectProperties.ActiveFileSharePath
        {
            get { throw new NotImplementedException(); } // E_NOTIMPL
        }

        VSLangProj.prjWebAccessMethod VSLangProj.ProjectProperties.ActiveWebAccessMethod
        {
            get { throw new NotImplementedException(); } // E_NOTIMPL
        }


        string VSLangProj.ProjectProperties.ApplicationIcon
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    return this.Node.ProjectMgr.GetProjectProperty("ApplicationIcon");
                });
            }
            set
            {
                UIThread.DoOnUIThread(() =>
                {
                    this.Node.ProjectMgr.SetProjectProperty("ApplicationIcon", value);
                });
            }
        }

        private string GetProp(string name)
        {
            return UIThread.DoOnUIThread(() =>
            {
                return this.Node.ProjectMgr.GetProjectProperty(name);
            });
        }
        private void SetProp(string name, string value)
        {
            UIThread.DoOnUIThread(() =>
            {
                this.Node.ProjectMgr.SetProjectProperty(name, value);
            });
        }

        string VSLangProj.ProjectProperties.AssemblyKeyContainerName
        {
            get { return this.GetProp("AssemblyKeyContainerName"); }
            set { this.SetProp("AssemblyKeyContainerName", value); }
        }

        string VSLangProj.ProjectProperties.AssemblyName
        {
            get { return this.GetProp("AssemblyName"); }
            set { this.SetProp("AssemblyName", value); }
        }

        string VSLangProj.ProjectProperties.AssemblyOriginatorKeyFile
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        VSLangProj.prjOriginatorKeyMode VSLangProj.ProjectProperties.AssemblyOriginatorKeyMode
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        VSLangProj.prjScriptLanguage VSLangProj.ProjectProperties.DefaultClientScript
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        VSLangProj.prjTargetSchema VSLangProj.ProjectProperties.DefaultTargetSchema
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        VSLangProj.prjHTMLPageLayout VSLangProj.ProjectProperties.DefaultHTMLPageLayout
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        bool VSLangProj.ProjectProperties.DelaySign
        {
            get
            {
                var p = this.GetProp("DelaySign");
                return String.Equals(p, "true", StringComparison.OrdinalIgnoreCase);
            }
            set
            {
                this.SetProp("DelaySign", value ? "true" : "false");
            }
        }

        string VSLangProj.ProjectProperties.FileName
        {
            get
            {
                return UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.FullPath;
                    return Path.GetFileName(path);
                });
            }
            set
            {
                UIThread.DoOnUIThread(() =>
                {
                    var path = this.Node.ProjectMgr.BuildProject.FullPath;
                    var newPath = Path.Combine(Path.GetDirectoryName(path), value);
                    this.Node.ProjectMgr.RenameProjectFile(newPath);
                });
            }
        }

        string VSLangProj.ProjectProperties.FileSharePath
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        bool VSLangProj.ProjectProperties.LinkRepair
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }

        string VSLangProj.ProjectProperties.OfflineURL
        {
            get { throw new NotImplementedException(); }
        }

        VSLangProj.prjCompare VSLangProj.ProjectProperties.OptionCompare
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        VSLangProj.prjOptionExplicit VSLangProj.ProjectProperties.OptionExplicit
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        VSLangProj.prjOptionStrict VSLangProj.ProjectProperties.OptionStrict
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        private string RootNamespace
        {
            get { return this.GetProp("RootNamespace"); }
            set { this.SetProp("RootNamespace", value); }

        }
        string VSLangProj.ProjectProperties.DefaultNamespace
        {
            get { return this.RootNamespace; }
            set { this.RootNamespace = value; }
        }


        string VSLangProj.ProjectProperties.RootNamespace
        {
            get { return this.RootNamespace; }
            set { this.RootNamespace = value; }
        }

        string VSLangProj.ProjectProperties.ServerExtensionsVersion
        {
            get { throw new NotImplementedException(); }
        }

        string VSLangProj.ProjectProperties.StartupObject
        {
            get { return this.GetProp("StartupObject"); }
            set { this.SetProp("StartupObject", value); }
        }


        VSLangProj.prjWebAccessMethod VSLangProj.ProjectProperties.WebAccessMethod
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        string VSLangProj.ProjectProperties.WebServer
        {
            get { throw new NotImplementedException(); }
        }

        string VSLangProj.ProjectProperties.WebServerVersion
        {
            get { throw new NotImplementedException(); }
        }

        object VSLangProj.ProjectProperties.get_Extender(string name)
        {
            return UIThread.DoOnUIThread(() => this.Extender(name));
        }

        object VSLangProj.ProjectProperties.ExtenderNames
        {
            get { return UIThread.DoOnUIThread(() => this.ExtenderNames()); }
        }

        VSLangProj.prjProjectType VSLangProj.ProjectProperties.ProjectType
        {
            get { return VSLangProj.prjProjectType.prjProjectTypeLocal; }
        }

        private VSLangProj.prjOutputType OutputType
        {
            get
            {
                var typ = this.GetProp(ProjectFileConstants.OutputType);
                switch (typ)
                {
                    case "WinExe": return VSLangProj.prjOutputType.prjOutputTypeWinExe;
                    case "Library": return VSLangProj.prjOutputType.prjOutputTypeLibrary;
                    default: return VSLangProj.prjOutputType.prjOutputTypeExe;
                }
            }
            set
            {
                string propVal;
                switch (value)
                {
                    case VSLangProj.prjOutputType.prjOutputTypeWinExe:
                        propVal = "WinExe";
                        break;
                    case VSLangProj.prjOutputType.prjOutputTypeLibrary:
                        propVal = "Library";
                        break;
                    default:
                        propVal = "Exe";
                        break;
                }
                this.SetProp(ProjectFileConstants.OutputType, propVal);
            }
        }

        VSLangProj.prjOutputType VSLangProj.ProjectProperties.OutputType
        {
            get { return this.OutputType; }
            set { this.OutputType = value; }
        }

        string VSLangProj.ProjectProperties.OutputFileName
        {
            get
            {
                var assemblyName = this.GetProp("AssemblyName");
                string extension;
                switch (this.OutputType)
                {
                    case VSLangProj.prjOutputType.prjOutputTypeLibrary:
                        extension = ".dll";
                        break;
                    default:
                        extension = ".exe";
                        break;
                }
                return assemblyName + extension;
            }
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class FolderNodeProperties : NodeProperties
    {
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
                this.Node.ReDraw(UIHierarchyElement.Caption);
            }
        }

        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
            }
        }

        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FullPath
        {
            get
            {
                string fullPath = this.Node.GetMkDocument();
                if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    return fullPath + Path.DirectorySeparatorChar;
                }
                else
                {
                    return fullPath;
                }
            }
        }

        internal FolderNodeProperties(HierarchyNode node)
            : base(node)
        {
        }

        public override string GetClassName()
        {
            return SR.GetString(SR.FolderProperties, CultureInfo.CurrentUICulture);
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ReferenceNodeProperties : NodeProperties
    {
        bool copyLocalDefault;

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.RefName)]
        [SRDescriptionAttribute(SR.RefNameDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public override string Name
        {
            get
            {
                return this.Node.Caption;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.CopyToLocal)]
        [SRDescriptionAttribute(SR.CopyToLocalDescription)]
        public bool CopyToLocal
        {
            get
            {
                string copyLocal = this.GetProperty(ProjectFileConstants.Private, null);
                if (copyLocal == null || copyLocal.Length == 0)
                    return copyLocalDefault;
                return bool.Parse(copyLocal);
            }
            set
            {
                this.SetProperty(ProjectFileConstants.Private, value.ToString());
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public virtual string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }

        internal ReferenceNodeProperties(HierarchyNode node)
            : this(node, false)
        {
        }

        internal ReferenceNodeProperties(HierarchyNode node, bool copyLocalDefault)
            : base(node)
        {
            this.copyLocalDefault = copyLocalDefault;
        }

        public override string GetClassName()
        {
            return SR.GetString(SR.ReferenceProperties, CultureInfo.CurrentUICulture);
        }
    }

    class FSharpCoreVersionConverter : StringConverter
    {
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var props = (FSharpCoreAssemblyReferenceProperties)context.Instance;
            var fsharpCoreVersionLookup = (IFSharpCoreVersionLookupService)props.Node.ProjectMgr.Site.GetService(typeof(IFSharpCoreVersionLookupService));
            var frameworkName = new FrameworkName(props.Node.ProjectMgr.GetTargetFrameworkMoniker());
            var versions = fsharpCoreVersionLookup.ListAvailableFSharpCoreVersions(frameworkName);
            return new StandardValuesCollection(Array.ConvertAll(versions, x => x.Version));
        }
    }

    [ComVisible(true)]
    public class FSharpCoreAssemblyReferenceProperties : ReferenceNodeProperties
    {
        internal FSharpCoreAssemblyReferenceProperties(AssemblyReferenceNode node, bool copyLocalDefault)
            : base(node, copyLocalDefault)
        {
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.Version)]
        [SRDescriptionAttribute(SR.VersionDescription)]
        [TypeConverter(typeof(FSharpCoreVersionConverter))]
        public string Version
        {
            get { return Node.ProjectMgr.TargetFSharpCoreVersion; }
            set
            {
                try
                {
                    Node.ProjectMgr.TargetFSharpCoreVersion = value;
                }
                catch (COMException e)
                {
                    // OLE_E_PROMPTSAVECANCELLED means that user has pressed cancel in 'Proceed with changing reference' dialog
                    // do not propagate it
                    if (e.HResult != VSConstants.OLE_E_PROMPTSAVECANCELLED)
                        throw;
                }
            }
        }
    }

    [ComVisible(true)]
    public class AssemblyReferenceProperties : ReferenceNodeProperties
    {
        internal AssemblyReferenceProperties(AssemblyReferenceNode node, bool copyLocalDefault)
            : base(node, copyLocalDefault)
        {
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.SpecificVersion)]
        [SRDescriptionAttribute(SR.SpecificVersionDescription)]
        public bool SpecificVersion
        {
            get
            {
                string specificVersion = this.GetProperty(ProjectFileConstants.SpecificVersion, null);
                if (string.IsNullOrEmpty(specificVersion))
                {
                    string name = this.GetProperty(ProjectFileConstants.Include, null);
                    return name.IndexOf("Version=", StringComparison.Ordinal) != -1;
                }
                return bool.Parse(specificVersion);
            }
            set
            {
                if (value)
                {
                    AssemblyReferenceNode me = (AssemblyReferenceNode)Node;
                    // we want to set a specific version, e.g. "System, Version=2.0.0.0", 
                    // but this requires having successfully resolved the reference
                    if (me.ResolvedAssembly == null)
                    {
                        me.DoOneOffResolve();
                    }
                    if (me.ResolvedAssembly != null)
                    {
                        this.Node.ItemNode.Rename(me.ResolvedAssembly.FullName);
                        // Note: line above is logical equivalent of line below, but Include is a special property with special Rename() OM
                        //this.SetProperty(ProjectFileConstants.Include, me.AssemblyName.FullName);
                        this.SetProperty(ProjectFileConstants.SpecificVersion, "True");
                    }
                    else
                    {
                        // Could not resolve the reference, e.g. because someone changed the .fsproj file
                        // to point to a non-existent file on disk.  In this case there is nothing we can do,
                        // so fail silently (that's what C# does).
                    }
                }
                else
                {
                    this.SetProperty(ProjectFileConstants.SpecificVersion, "False");
                }
            }
        }

    }

    [ComVisible(true)]
    public class ProjectReferencesProperties : ReferenceNodeProperties
    {
        internal ProjectReferencesProperties(ProjectReferenceNode node)
            : base(node, true)
        {
        }

        public override string FullPath
        {
            get
            {
                return ((ProjectReferenceNode)Node).ReferencedProjectOutputPath;
            }
        }
    }
}