// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// This type of node is used for references to COM components.
    /// </summary>
    [CLSCompliant(false)]
    [ComVisible(true)]
    public class ComReferenceNode : ReferenceNode
    {

        private enum RegKind
        {
            RegKind_Default = 0,
            RegKind_Register = 1,
            RegKind_None = 2
        }

        [ DllImport( "oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false )]
        private static extern void LoadTypeLibEx(string strTypeLibName, RegKind regKind, [ MarshalAs( UnmanagedType.Interface )] out object typeLib );

        private string typeName;
        private Guid typeGuid;
        private string projectRelativeFilePath;
        private string installedFilePath;
        private string minorVersionNumber;
        private string majorVersionNumber;
        private readonly int lcid;

        public override string Caption
        {
            get { return this.typeName; }
        }

        public override string Url
        {
            get
            {
                return this.projectRelativeFilePath;
            }
        }

        /// <summary>
        /// Returns the Guid of the COM object.
        /// </summary>
        public Guid TypeGuid
        {
            get { return this.typeGuid; }
        }

        /// <summary>
        /// Returns the path where the COM object is installed.
        /// </summary>
        public string InstalledFilePath
        {
            get { return this.installedFilePath; }
        }

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "LCID")]
        public int LCID
        {
            get { return lcid; }
        }

        public int MajorVersionNumber
        {
            get
            {
                if (string.IsNullOrEmpty(majorVersionNumber))
                {
                    return 0;
                }
                return int.Parse(majorVersionNumber, CultureInfo.CurrentCulture);
            }
        }
        public int MinorVersionNumber
        {
            get
            {
                if (string.IsNullOrEmpty(minorVersionNumber))
                {
                    return 0;
                }
                return int.Parse(minorVersionNumber, CultureInfo.CurrentCulture);
            }
        }
        private Automation.OAComReference comReference;
        public override object Object
        {
            get
            {
                if (null == comReference)
                {
                    comReference = new Automation.OAComReference(this);
                }
                return comReference;
            }
        }

        internal ComReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element)
        {
            this.typeName = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
            string typeGuidAsString = this.ItemNode.GetMetadata(ProjectFileConstants.Guid);
            if (typeGuidAsString != null)
            {
                this.typeGuid = new Guid(typeGuidAsString);
            }

            this.majorVersionNumber = this.ItemNode.GetMetadata(ProjectFileConstants.VersionMajor);
            this.minorVersionNumber = this.ItemNode.GetMetadata(ProjectFileConstants.VersionMinor);
            this.lcid = int.Parse(this.ItemNode.GetMetadata(ProjectFileConstants.Lcid));
            this.SetProjectItemsThatRelyOnReferencesToBeResolved(false);
            this.SetInstalledFilePath();
        }

        /// <summary>
        /// Overloaded constructor for creating a ComReferenceNode from selector data
        /// </summary>
        /// <param name="root">The Project node</param>
        /// <param name="selectorData">The component selctor data.</param>
        internal ComReferenceNode(ProjectNode root, VSCOMPONENTSELECTORDATA selectorData)
            : base(root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }
            if (selectorData.type == VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project
                || selectorData.type == VSCOMPONENTTYPE.VSCOMPONENTTYPE_ComPlus)
            {
                throw new ArgumentException();
            }

            // Initialize private state
            this.typeName = selectorData.bstrTitle;
            this.typeGuid = selectorData.guidTypeLibrary;
            this.majorVersionNumber = selectorData.wTypeLibraryMajorVersion.ToString(CultureInfo.InvariantCulture);
            this.minorVersionNumber = selectorData.wTypeLibraryMinorVersion.ToString(CultureInfo.InvariantCulture);
            this.lcid = (int) selectorData.lcidTypeLibrary;

            // Check to see if the COM object actually exists.
            this.SetInstalledFilePath();
            // If the value cannot be set throw.
            if (String.IsNullOrEmpty(this.installedFilePath))
            {
                var message = string.Format(SR.GetString(SR.ReferenceCouldNotBeAdded, CultureInfo.CurrentUICulture), selectorData.bstrTitle);
                throw new InvalidOperationException(message);
            }
        }

        // Create a ComReferenceNode via a string to a TLB
        internal ComReferenceNode(ProjectNode root, string filePath)
            : base(root)
        {
            object otypeLib = null;
            ComTypes.ITypeLib typeLib = null;
            IntPtr ptrToLibAttr = IntPtr.Zero;
            try
            {
                LoadTypeLibEx( filePath, RegKind.RegKind_None, out otypeLib );
                typeLib = (ComTypes.ITypeLib)otypeLib;
                if (typeLib == null)
                {
                    throw new ArgumentException();
                }
                ComTypes.TYPELIBATTR typeAttr = new ComTypes.TYPELIBATTR();
                typeLib.GetLibAttr(out ptrToLibAttr);
                typeAttr = (ComTypes.TYPELIBATTR)Marshal.PtrToStructure(ptrToLibAttr, typeAttr.GetType());

                // Initialize state
                this.typeGuid = typeAttr.guid;
                this.majorVersionNumber = typeAttr.wMajorVerNum.ToString(CultureInfo.InvariantCulture);
                this.minorVersionNumber = typeAttr.wMinorVerNum.ToString(CultureInfo.InvariantCulture);
                this.lcid = typeAttr.lcid;

                // Check to see if the COM object actually exists.
                this.SetInstalledFilePath();
                // If the value cannot be set throw.
                if (String.IsNullOrEmpty(this.installedFilePath))
                {
                    var message = string.Format(SR.GetString(SR.ReferenceCouldNotBeAdded, CultureInfo.CurrentUICulture), filePath);
                    throw new InvalidOperationException(message);
                }
            }
            finally
            {
                if (typeLib != null) typeLib.ReleaseTLibAttr(ptrToLibAttr);
            }
        }

        /// <summary>
        /// Links a reference node to the project and hierarchy.
        /// </summary>
        public override void BindReferenceData()
        {
            Debug.Assert(this.ItemNode != null, "The AssemblyName field has not been initialized");

            // We need to create the project element at this point if it has not been created.
            // We cannot do that from the ctor if input comes from a component selector data, since had we been doing that we would have added a project element to the project file.  
            // The problem with that approach is that we would need to remove the project element if the item cannot be added to the hierachy (E.g. It already exists).
            // It is just safer to update the project file now. This is the intent of this method.
            // Call MSBuild to build the target ResolveComReferences
            if (this.ItemNode == null || this.ItemNode.Item == null)
            {
                this.ItemNode = this.GetProjectElementBasedOnInputFromComponentSelectorData();
            }

            this.SetProjectItemsThatRelyOnReferencesToBeResolved(true);
        }

        /// <summary>
        /// Checks if a reference is already added. The method parses all references and compares the the FinalItemSpec and the Guid.
        /// </summary>
        /// <returns>true if the assembly has already been added.</returns>
        public override bool IsAlreadyAdded(out ReferenceNode existingNode)
        {
            ReferenceContainerNode referencesFolder = this.ProjectMgr.FindChild(ReferenceContainerNode.ReferencesNodeVirtualName) as ReferenceContainerNode;
            Debug.Assert(referencesFolder != null, "Could not find the References node");

            for (HierarchyNode n = referencesFolder.FirstChild; n != null; n = n.NextSibling)
            {
                if (n is ComReferenceNode)
                {
                    ComReferenceNode referenceNode = n as ComReferenceNode;

                    // We check if the name and guids are the same
                    if (referenceNode.TypeGuid == this.TypeGuid && String.Compare(referenceNode.Caption, this.Caption, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        existingNode = referenceNode;
                        return true;
                    }
                }
            }

            existingNode = null;
            return false;
        }

        /// <summary>
        /// Determines if this is node a valid node for painting the default reference icon.
        /// </summary>
        /// <returns></returns>
        public override bool CanShowDefaultIcon()
        {
            return !String.IsNullOrEmpty(this.installedFilePath);
        }

        /// <summary>
        /// This is an helper method to convert the VSCOMPONENTSELECTORDATA recieved by the
        /// implementer of IVsComponentUser into a ProjectElement that can be used to create
        /// an instance of this class.
        /// This should not be called for project reference or reference to managed assemblies.
        /// </summary>
        /// <returns>ProjectElement corresponding to the COM component passed in</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        private ProjectElement GetProjectElementBasedOnInputFromComponentSelectorData()
        {
            ProjectElement element = new ProjectElement(this.ProjectMgr, this.typeName, ProjectFileConstants.COMReference);

            // Set the basic information regarding this COM component
            element.SetMetadata(ProjectFileConstants.Guid, this.typeGuid.ToString("B"));
            element.SetMetadata(ProjectFileConstants.VersionMajor, this.majorVersionNumber);
            element.SetMetadata(ProjectFileConstants.VersionMinor, this.minorVersionNumber);
            element.SetMetadata(ProjectFileConstants.Lcid, this.lcid.ToString());
            element.SetMetadata(ProjectFileConstants.Isolated, false.ToString());

            // See if a PIA exist for this component
            TypeLibConverter typelib = new TypeLibConverter();
            string assemblyName;
            string assemblyCodeBase;
            if (typelib.GetPrimaryInteropAssembly(this.typeGuid, Int32.Parse(this.majorVersionNumber, CultureInfo.InvariantCulture), Int32.Parse(this.minorVersionNumber, CultureInfo.InvariantCulture), this.lcid, out assemblyName, out assemblyCodeBase))
            {
                element.SetMetadata(ProjectFileConstants.WrapperTool, WrapperToolAttributeValue.Primary.ToString().ToLowerInvariant());
            }
            else
            {
                // MSBuild will have to generate an interop assembly
                element.SetMetadata(ProjectFileConstants.WrapperTool, WrapperToolAttributeValue.TlbImp.ToString().ToLowerInvariant());
                element.SetMetadata(ProjectFileConstants.Private, true.ToString());
            }
            return element;
        }

        private void SetProjectItemsThatRelyOnReferencesToBeResolved(bool renameItemNode)
        {
            // Call MSBuild to build the target ResolveComReferences
            bool success;
            ErrorHandler.ThrowOnFailure(this.ProjectMgr.BuildTarget(MsBuildTarget.ResolveComReferences, out success));
            if (!success)
                throw new InvalidOperationException();

            // Now loop through the generated COM References to find the corresponding one
            var instance = this.ProjectMgr.BuildProject.CreateProjectInstance();
            AssemblyReferenceNode.BuildInstance(this.ProjectMgr, ref instance, MsBuildTarget.ResolveAssemblyReferences);
            var comReferences = instance.GetItems(MsBuildGeneratedItemType.ComReferenceWrappers);
            foreach (var reference in comReferences)
            {
                if (String.Compare(MSBuildItem.GetMetadataValue(reference, ProjectFileConstants.Guid), this.typeGuid.ToString("B"), StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(MSBuildItem.GetMetadataValue(reference, ProjectFileConstants.VersionMajor), this.majorVersionNumber, StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(MSBuildItem.GetMetadataValue(reference, ProjectFileConstants.VersionMinor), this.minorVersionNumber, StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(MSBuildItem.GetMetadataValue(reference, ProjectFileConstants.Lcid), this.lcid.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
                {
                    string name = MSBuildItem.GetEvaluatedInclude(reference);
                    if (Path.IsPathRooted(name))
                    {
                        this.projectRelativeFilePath = name;
                    }
                    else
                    {
                        this.projectRelativeFilePath = Path.Combine(this.ProjectMgr.ProjectFolder, name);
                    }

                    if (renameItemNode)
                    {
                        this.ItemNode.Rename(Path.GetFileNameWithoutExtension(name));
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Verify that the TypeLib is registered and set the the installed file path of the com reference.
        /// </summary>
        /// <returns></returns>
        private void SetInstalledFilePath()
        {
            int major = this.MajorVersionNumber;
            int minor = this.MinorVersionNumber;
            string registryPath = string.Format(CultureInfo.InvariantCulture, @"TYPELIB\{0}\{1}.{2}", this.typeGuid.ToString("B"), major.ToString("x"), minor.ToString("x"));
            using (RegistryKey typeLib = Registry.ClassesRoot.OpenSubKey(registryPath))
            {
                if (typeLib != null)
                {
                    // Check if we need to set the name for this type.
                    if (string.IsNullOrEmpty(this.typeName))
                    {
                        this.typeName = typeLib.GetValue(string.Empty) as string;
                    }
                    // Now get the path to the file that contains this type library.

                    // lcid 
                    //   The hexadecimal string representation of the locale identifier (LCID). 
                    //   It is one to four hexadecimal digits with no 0x prefix and no leading zeros. 
                    using (RegistryKey installKey = typeLib.OpenSubKey(string.Format(CultureInfo.InvariantCulture, @"{0:X}\win32", this.lcid)))
                    {
                        if (installKey != null)
                        {
                            this.installedFilePath = installKey.GetValue(String.Empty) as String;
                        }
                    }
                }
            }
        }
    }
}
