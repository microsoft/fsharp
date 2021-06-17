// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Xml;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using ShellConstants = Microsoft.VisualStudio.Shell.Interop.Constants;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    [StructLayoutAttribute(LayoutKind.Sequential)]
    internal struct _DROPFILES
    {
        public Int32 pFiles;
        public Int32 X;
        public Int32 Y;
        public Int32 fNC;
        public Int32 fWide;
    }

    /// <summary>
    /// Defines possible types of output that can produced by a language project
    /// </summary>
    [PropertyPageTypeConverterAttribute(typeof(OutputTypeConverter))]
    public enum OutputType
    {
        /// <summary>
        /// The output type is a windows executable.
        /// </summary>
        WinExe,

        /// <summary>
        /// The output type is an executable.
        /// </summary>
        Exe,

        /// <summary>
        /// The output type is a class library.
        /// </summary>
        Library
    }

    /// <summary>
    /// Debug values used by DebugModeConverter.
    /// </summary>
    [PropertyPageTypeConverterAttribute(typeof(DebugModeConverter))]
    public enum DebugMode
    {
        Project,
        Program,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "URL")]
        URL
    }


    [PropertyPageTypeConverterAttribute(typeof(CopyToOutputDirectoryConverter))]
    public enum CopyToOutputDirectory
    {
        DoNotCopy,
        Always,
        PreserveNewest,
    }

    /// <summary>
    /// Defines the version of the CLR that is appropriate to the project.
    /// </summary>
    [PropertyPageTypeConverterAttribute(typeof(PlatformTypeConverter))]
    public enum PlatformType
    {
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "not")]
        notSpecified,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "v")]
        v1,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "v")]
        v11,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "v")]
        v2,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "cli")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "cli")]
        cli1
    }

    /// <summary>
    /// Defines the currect state of a property page.
    /// </summary>
    [Flags]
    internal enum PropPageStatus
    {

        Dirty = 0x1,

        Validate = 0x2,

        Clean = 0x4
    }

    [Flags]
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    internal enum ModuleKindFlags
    {

        ConsoleApplication,

        WindowsApplication,

        DynamicallyLinkedLibrary,

        ManifestResourceFile,

        UnmanagedDynamicallyLinkedLibrary
    }

    /// <summary>
    /// Defines the status of the command being queried
    /// </summary>
    [Flags]
    [SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames")]
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    internal enum QueryStatusResult
    {
        /// <summary>
        /// The command is not supported.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "NOTSUPPORTED")]
        NOTSUPPORTED = 0,

        /// <summary>
        /// The command is supported
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SUPPORTED")]
        SUPPORTED = 1,

        /// <summary>
        /// The command is enabled
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ENABLED")]
        ENABLED = 2,

        /// <summary>
        /// The command is toggled on
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "LATCHED")]
        LATCHED = 4,

        /// <summary>
        /// The command is toggled off (the opposite of LATCHED).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "NINCHED")]
        NINCHED = 8,

        /// <summary>
        /// The command is invisible.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "INVISIBLE")]
        INVISIBLE = 16
    }

    /// <summary>
    /// Defines the type of item to be added to the hierarchy.
    /// </summary>
    internal enum HierarchyAddType
    {
        AddNewItem,
        AddExistingItem
    }

    /// <summary>
    /// Defines the component from which a command was issued.
    /// </summary>
    internal enum CommandOrigin
    {
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ui")]
        UiHierarchy,
        OleCommandTarget
    }

    /// <summary>
    /// Defines the current status of the build process.
    /// </summary>
    internal enum MSBuildResult
    {
        /// <summary>
        /// The build is currently suspended.
        /// </summary>
        Suspended,

        /// <summary>
        /// The build has been restarted.
        /// </summary>
        Resumed,

        /// <summary>
        /// The build failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The build was successful.
        /// </summary>
        Successful,
    }

    /// <summary>
    /// Defines the type of action to be taken in showing the window frame.
    /// </summary>
    internal enum WindowFrameShowAction
    {
        DontShow,
        Show,
        ShowNoActivate,
        Hide,
    }

    /// <summary>
    /// Defines drop types
    /// </summary>
    internal enum DropDataType
    {
        None,
        Shell,
        VsStg,
        VsRef
    }

    /// <summary>
    /// Used by the hierarchy node to decide which element to redraw.
    /// </summary>
    [Flags]
    [SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames")]
    internal enum UIHierarchyElement
    {
        None = 0,

        /// <summary>
        /// This will be translated to VSHPROPID_IconIndex
        /// </summary>
        Icon = 1,

        /// <summary>
        /// This will be translated to VSHPROPID_StateIconIndex
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        SccState = 2,

        /// <summary>
        /// This will be translated to VSHPROPID_Caption
        /// </summary>
        Caption = 4
    }

    /// <summary>
    /// Defines the global propeties used by the msbuild project.
    /// </summary>
    internal enum GlobalProperty
    {
        /// <summary>
        /// Property specifying that we are building inside VS.
        /// </summary>
        BuildingInsideVisualStudio,

        /// <summary>
        /// The VS installation directory. This is the same as the $(DevEnvDir) macro.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Env")]
        DevEnvDir,

        /// <summary>
        /// The name of the solution the project is created. This is the same as the $(SolutionName) macro.
        /// </summary>
        SolutionName,

        /// <summary>
        /// The file name of the solution. This is the same as $(SolutionFileName) macro.
        /// </summary>
        SolutionFileName,

        /// <summary>
        /// The full path of the solution. This is the same as the $(SolutionPath) macro.
        /// </summary>
        SolutionPath,

        /// <summary>
        /// The directory of the solution. This is the same as the $(SolutionDir) macro.
        /// </summary>               
        SolutionDir,

        /// <summary>
        /// The extension of teh directory. This is the same as the $(SolutionExt) macro.
        /// </summary>
        SolutionExt,

        /// <summary>
        /// The fxcop installation directory.
        /// </summary>
        FxCopDir,

        /// <summary>
        /// The ResolvedNonMSBuildProjectOutputs msbuild property
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "VSIDE")]
        VSIDEResolvedNonMSBuildProjectOutputs,

        /// <summary>
        /// The Configuartion property.
        /// </summary>
        Configuration,

        /// <summary>
        /// The platform property.
        /// </summary>
        Platform,

        /// <summary>
        /// The RunCodeAnalysisOnce property
        /// </summary>
        RunCodeAnalysisOnce,

        /// <summary>
        /// The VisualStudioStyleErrors property.  We use this to determine correct error spans for build errors.
        /// </summary>
        VisualStudioStyleErrors,

        /// <summary>
        /// The SqmSessionGuid property
        /// </summary>
        SqmSessionGuid
    }

    public class AfterProjectFileOpenedEventArgs : EventArgs
    {
        private bool added;

        /// <summary>
        /// True if the project is added to the solution after the solution is opened. false if the project is added to the solution while the solution is being opened.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public bool Added
        {
            get { return this.added; }
        }

        public AfterProjectFileOpenedEventArgs(bool added)
        {
            this.added = added;
        }
    }

    public class BeforeProjectFileClosedEventArgs : EventArgs
    {
        private bool removed;

        /// <summary>
        /// true if the project was removed from the solution before the solution was closed. false if the project was removed from the solution while the solution was being closed.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public bool Removed
        {
            get { return this.removed; }
        }

        public BeforeProjectFileClosedEventArgs(bool removed)
        {
            this.removed = removed;
        }
    }

    /// <summary>
    /// This class is used for the events raised by a HierarchyNode object.
    /// </summary>
    internal class HierarchyNodeEventArgs : EventArgs
    {
        private HierarchyNode child;

        public HierarchyNodeEventArgs(HierarchyNode child)
        {
            this.child = child;
        }

        public HierarchyNode Child
        {
            get { return this.child; }
        }
    }

    /// <summary>
    /// Event args class for triggering file change event arguments.
    /// </summary>
    internal class FileChangedOnDiskEventArgs : EventArgs
    {
        /// <summary>
        /// File name that was changed on disk.
        /// </summary>
        private string fileName;

        /// <summary>
        /// The item ide of the file that has changed.
        /// </summary>
        private uint itemID;

        /// <summary>
        /// The reason the file has changed on disk.
        /// </summary>
        private _VSFILECHANGEFLAGS fileChangeFlag;

        /// <summary>
        /// Constructs a new event args.
        /// </summary>
        /// <param name="fileName">File name that was changed on disk.</param>
        /// <param name="id">The item id of the file that was changed on disk.</param>
        public FileChangedOnDiskEventArgs(string fileName, uint id, _VSFILECHANGEFLAGS flag)
        {
            this.fileName = fileName;
            this.itemID = id;
            this.fileChangeFlag = flag;
        }

        /// <summary>
        /// Gets the file name that was changed on disk.
        /// </summary>
        /// <value>The file that was changed on disk.</value>
        public string FileName
        {
            get
            {
                return this.fileName;
            }
        }

        /// <summary>
        /// Gets item id of the file that has changed
        /// </summary>
        /// <value>The file that was changed on disk.</value>
        public uint ItemID
        {
            get
            {
                return this.itemID;
            }
        }

        /// <summary>
        /// The reason while the file has chnaged on disk.
        /// </summary>
        /// <value>The reason while the file has chnaged on disk.</value>
        public _VSFILECHANGEFLAGS FileChangeFlag
        {
            get
            {
                return this.fileChangeFlag;
            }
        }
    }

    /// <summary>
    /// Defines the event args for the active configuration chnage event.
    /// </summary>
    internal class ActiveConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The hierarchy whose configuration has changed 
        /// </summary>
        private IVsHierarchy hierarchy;

        /// <summary>
        /// Constructs a new event args.
        /// </summary>
        /// <param name="hierarchy">The hierarchy that has changed its configuration.</param>
        public ActiveConfigurationChangedEventArgs(IVsHierarchy hierarchy)
        {
            this.hierarchy = hierarchy;
        }

        /// <summary>
        /// The hierarchy whose configuration has changed 
        /// </summary>
        public IVsHierarchy Hierarchy
        {
            get
            {
                return this.hierarchy;
            }
        }
    }

    /// <summary>
    /// Argument of the event raised when a project property is changed.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    internal class ProjectPropertyChangedArgs : EventArgs
    {
        private string propertyName;
        private string oldValue;
        private string newValue;

        public ProjectPropertyChangedArgs(string propertyName, string oldValue, string newValue)
        {
            this.propertyName = propertyName;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }

        public string NewValue
        {
            get { return newValue; }
        }

        public string OldValue
        {
            get { return oldValue; }
        }

        public string PropertyName
        {
            get { return propertyName; }
        }
    }
}
