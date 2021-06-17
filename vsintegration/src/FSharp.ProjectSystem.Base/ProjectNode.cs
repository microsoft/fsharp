// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.FSharp.ProjectSystem.Automation;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Net;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using IOleDataObject = Microsoft.VisualStudio.OLE.Interop.IDataObject;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VSConstants = Microsoft.VisualStudio.VSConstants;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;
using EnvDTE;

using Microsoft.Win32;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    internal delegate void MSBuildCoda(MSBuildResult result, ProjectInstance instance);

    // Abstracts over the most common differences between the Dev9 and Dev10 MSBuild project OM
    internal static class MSBuildProject
    {
        private static bool ItemIsVisible(Microsoft.Build.Evaluation.ProjectItem item)
        {
            // MSBuilds tasks/targets can create items (such as object files),
            // such items are not part of the project per say, and should not be displayed.
            // so ignore those items.
            string strInProject = "";
            string inProject = MSBuildItem.GetMetadataValue(item, "InProject");
            string isVisible = MSBuildItem.GetMetadataValue(item, "Visible");
            if (!string.IsNullOrEmpty(isVisible))
                strInProject = isVisible;
            else if (!string.IsNullOrEmpty(inProject))
                strInProject = inProject;

            bool invisible =
                ((item.IsImported && (0 != string.Compare(strInProject, "true", StringComparison.OrdinalIgnoreCase))) ||
                (!item.IsImported && (0 == string.Compare(strInProject, "false", StringComparison.OrdinalIgnoreCase))));
            return !invisible;
        }

        public static void FullyUnloadProject(Microsoft.Build.Evaluation.ProjectCollection buildEngine, Microsoft.Build.Evaluation.Project project)
        {
            var xml = project.Xml;
            buildEngine.UnloadProject(project);  // unload evaluated project (Evaluation)
            try
            {
                buildEngine.UnloadProject(xml);      // unload Xml cache (Construction)
            }
            catch (InvalidOperationException)
            {
                // May throw
            }
        }
        public static string GetFullPath(Microsoft.Build.Evaluation.Project project)
        {
            return project.FullPath;
        }
        public static System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectItem> GetItems(Microsoft.Build.Evaluation.Project project)
        {
            return project.Items;
        }
        public static System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectItem> GetItems(Microsoft.Build.Evaluation.Project project, string name)
        {
            return project.GetItems(name);
        }
        public static System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectItem> GetStaticItemsInOrder(Microsoft.Build.Evaluation.Project project)
        {
            project.ReevaluateIfNecessary();
            return project.AllEvaluatedItems;
        }
        public static void SetGlobalProperty(Microsoft.Build.Evaluation.Project project, string name, string value)
        {
            project.SetGlobalProperty(name, value);
        }
        public static System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectItem> GetStaticAndVisibleItemsInOrder(Microsoft.Build.Evaluation.Project project)
        {
            // see corresponding comment for Dev9 side
            foreach (var item in MSBuildProject.GetStaticItemsInOrder(project))
            {
                if (!ItemIsVisible(item))
                    continue;
                yield return item;
            }
        }
    }

    internal class CannotOpenProjectsWithWildcardsException : Exception
    {
        public CannotOpenProjectsWithWildcardsException(string projectFileName, string itemType, string itemSpecification)
            : base(string.Format(SR.GetStringWithCR(SR.NoWildcardsInProject), projectFileName, itemType, itemSpecification))
        {
            this.ProjectFileName = projectFileName;
            this.ItemType = itemType;
            this.ItemSpecification = itemSpecification;
        }

        public string ProjectFileName { private set; get; }

        public string ItemType { private set; get; }

        public string ItemSpecification { private set; get; }
    }

    internal class CannotAddItemToProjectWithWildcardsException : Exception
    {
        public CannotAddItemToProjectWithWildcardsException(string projectFileName, string itemType, string itemSpecification, string fileName)
            : base(string.Format(SR.GetStringWithCR(SR.CannotAddItemToProjectWithWildcards), projectFileName, itemType, itemSpecification, fileName))
        {
            this.ProjectFileName = projectFileName;
            this.ItemType = itemType;
            this.ItemSpecification = itemSpecification;
            this.FileName = fileName;
        }

        public string ProjectFileName { private set; get; }

        public string ItemType { private set; get; }

        public string ItemSpecification { private set; get; }

        public string FileName { private set; get; }
    }


    internal sealed class ExtensibilityEventsHelper
    {
        private readonly ProjectNode myProjectNode;
        private bool myCanFire;

        public ExtensibilityEventsHelper(ProjectNode projectNode)
        {
            myProjectNode = projectNode;
            myCanFire = true;
        }

        private class SafetyLock : IDisposable
        {
            private readonly bool myOld;
            private readonly ExtensibilityEventsHelper myOwner;

            public SafetyLock(ExtensibilityEventsHelper owner)
            {
                myOwner = owner;
                myOld = myOwner.myCanFire;
                myOwner.myCanFire = false;
            }
            void IDisposable.Dispose() { myOwner.myCanFire = myOld; }

        }
        public IDisposable SuspendEvents()
        {
            return new SafetyLock(this);
        }

        private void Fire(HierarchyNode node, Action<IVsExtensibility3, EnvDTE.ProjectItem> fireForProjectItem)
        {
            if (!myCanFire) return;
            if (!myProjectNode.IsProjectOpened) return;

            // We do not fire for references, aligning with C#.
            // Those interested in references have to listen to our VSProject.Events.ReferencesEvent
            if (node is ReferenceNode) return; 

            // SVsExtensibility isn't exposed to managed code, but it's the same as EnvDTE.IVsExtensibility
            var ext = myProjectNode.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;
            if (ext != null)
            {
                object automationObject = node.GetAutomationObject();
                var projectItem = automationObject as EnvDTE.ProjectItem;
                if (projectItem!= null)
                    fireForProjectItem(ext, projectItem);
            }
        }

        public void FireItemAdded(HierarchyNode node)
        {
            Fire(node, (ext, pi) => ext.FireProjectItemsEvent_ItemAdded(pi));
        }

        public void FireItemRemoved(HierarchyNode node)
        {
            Fire(node, (ext, pi) => ext.FireProjectItemsEvent_ItemRemoved(pi));
        }

        public void FireItemRenamed(HierarchyNode node, string oldName)
        {
            // Our project system never fires rename, because all our renames are combinations of Remove and Add
            Fire(node, (ext, pi) => ext.FireProjectItemsEvent_ItemRenamed(pi, oldName));
        }

    }

    internal enum BuildKind
    {
        SYNC,
        ASYNC
    }

    internal static class VsBuildManagerAccessorExtensionMethods
    {
        public static bool IsInProgress(this IVsBuildManagerAccessor buildManagerAccessor)
        {
            if (buildManagerAccessor == null)
            {
                return false;
            }

            uint batchBuildId = 0;
            ErrorHandler.ThrowOnFailure(buildManagerAccessor.GetCurrentBatchBuildId(out batchBuildId));
            return batchBuildId != 0;
        }
    }

    internal struct BuildResult
    {
        private MSBuildResult buildResult;
        public static readonly BuildResult FAILED = new BuildResult(MSBuildResult.Failed, null);
        ProjectInstance projectInstance;
        public BuildResult(MSBuildResult buildResult, ProjectInstance projectInstance)
        {
            this.buildResult = buildResult;
            this.projectInstance = projectInstance;
            Debug.Assert(!this.IsSuccessful || this.ProjectInstance != null, "All successfull build results should have project istances");
        }
        public BuildResult(BuildSubmission submission, ProjectInstance projectInstance) :
                this(submission.BuildResult.OverallResult == BuildResultCode.Success ? MSBuildResult.Successful : MSBuildResult.Failed, projectInstance)
        {
        }
        public ProjectInstance ProjectInstance { get { return this.projectInstance; } }
        public bool Equals(BuildResult other)
        {
            return this.buildResult == other.buildResult && this.projectInstance == other.ProjectInstance;
        }

        public bool IsSuccessful { get { return this.buildResult == MSBuildResult.Successful; } }


        public override bool Equals(object obj)
        {
            if (obj is BuildResult) return this.Equals((BuildResult)obj);
            return false;
        }

        public override int GetHashCode()
        {
            return this.buildResult.GetHashCode() * 29 + (this.projectInstance != null ? this.projectInstance.GetHashCode() : 0);
        }

        public static bool operator ==(BuildResult left, BuildResult right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(BuildResult left, BuildResult right)
        {
            return !left.Equals(right);
        }

    }

    /// <summary>
    /// Manages the persistent state of the project (References, options, files, etc.) and deals with user interaction via a GUI in the form a hierarchy.
    /// </summary>
    [CLSCompliant(false)]
    [ComVisible(true)]
    public abstract partial class ProjectNode : HierarchyNode,
        IVsGetCfgProvider,
        IVsProject3,
        IVsAggregatableProject,
        IVsProjectFlavorCfgProvider,
        IPersistFileFormat,
        IVsProjectBuildSystem,
        IVsBuildPropertyStorage,
        IVsComponentUser,
        IVsDependencyProvider,
        IVsSccProject2,
        IBuildDependencyUpdate,
        IProjectEventsListener,
        IReferenceContainerProvider,
        IVsProjectSpecialFiles, 
        IVsDesignTimeAssemblyResolution, 
        IVsProjectUpgrade,
        IVsSupportItemHandoff
    {
        /// <summary>
        /// This class stores mapping from ids -> objects. Uses as a replacement of EventSinkCollection (ESC)
        /// Operations:
        /// - Add(HierarchyNode) -> adds object to mapping, returns id that will be assigned to the new object
        /// - SetAt(id, HierarchyNode) -> associates object with given id - required for rename operation, because new item should have the same id with the old one.
        /// Why ESC is not fit our needs: it internally stores items in arraylist (id corresponds to item index) and performs compaction during remove operation. 
        /// This means that if we remove last node with id X, then we cannot use SetAt operation 
        /// (because internal array list is already compacted and X is outside the valid range for indexes).
        /// </summary>
        public class IdItemMapping
        {
            private Dictionary<uint, HierarchyNode> items = new Dictionary<uint,HierarchyNode>();
            private uint counter = 1;

            public int Count
            {
                get { return items.Count; }
            }

            public uint Add(HierarchyNode node)
            {
                var currentId = ++counter;
                items.Add(currentId, node);
                return currentId;
            }

            public void SetAt(uint cookie, HierarchyNode node)
            {
                items[cookie] = node;
            }

            public HierarchyNode this[uint cookie]
            {
                get 
                {
                    HierarchyNode node = null;
                    return items.TryGetValue(cookie, out node) ? node : null;
                }
            }

            public void Remove(HierarchyNode node)
            {
                items.Remove(node.ID);
            }

            public void Clear()
            {
                items.Clear();
            }
        }

        public enum ImageName
        {
            OfflineWebApp = 0,
            WebReferencesFolder = 1,
            OpenReferenceFolder = 2,
            ReferenceFolder = 3,
            Reference = 4,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDL")]
            SDLWebReference = 5,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "DISCO")]
            DISCOWebReference = 6,
            Folder = 7,
            OpenFolder = 8,
            ExcludedFolder = 9,
            OpenExcludedFolder = 10,
            ExcludedFile = 11,
            DependentFile = 12,
            MissingFile = 13,
            WindowsForm = 14,
            WindowsUserControl = 15,
            WindowsComponent = 16,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "XML")]
            XMLSchema = 17,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "XML")]
            XMLFile = 18,
            WebForm = 19,
            WebService = 20,
            WebUserControl = 21,
            WebCustomUserControl = 22,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ASP")]
            ASPPage = 23,
            GlobalApplicationClass = 24,
            WebConfig = 25,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "HTML")]
            HTMLPage = 26,
            StyleSheet = 27,
            ScriptFile = 28,
            TextFile = 29,
            SettingsFile = 30,
            Resources = 31,
            Bitmap = 32,
            Icon = 33,
            Image = 34,
            ImageMap = 35,
            XWorld = 36,
            Audio = 37,
            Video = 38,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "CAB")]
            CAB = 39,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "JAR")]
            JAR = 40,
            DataEnvironment = 41,
            PreviewFile = 42,
            DanglingReference = 43,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "XSLT")]
            XSLTFile = 44,
            Cursor = 45,
            AppDesignerFolder = 46,
            Data = 47,
            Application = 48,
            DataSet = 49,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "PFX")]
            PFX = 50,
            [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SNK")]
            SNK = 51,

            ImageLast = 51
        }

        /// <summary>
        /// Flags for specifying which events to stop triggering.
        /// </summary>
        [Flags]
        public enum EventTriggering
        {
            TriggerAll = 0,
            DoNotTriggerHierarchyEvents = 1,
            DoNotTriggerTrackerEvents = 2
        }

        /// <summary>
        /// The user file extension.
        /// </summary>
        public const string PerUserFileExtension = ".user";

        /// <summary>
        /// List of output groups names and their associated target
        /// </summary>
        private static KeyValuePair<string, string>[] outputGroupNames =
        {                                      // Name                    Target (MSBuild)
            new KeyValuePair<string, string>("Built",                 "BuiltProjectOutputGroup"),
            new KeyValuePair<string, string>("ContentFiles",          "ContentFilesProjectOutputGroup"),
            new KeyValuePair<string, string>("LocalizedResourceDlls", "SatelliteDllsProjectOutputGroup"),
            new KeyValuePair<string, string>("Documentation",         "DocumentationProjectOutputGroup"),
            new KeyValuePair<string, string>("Symbols",               "DebugSymbolsProjectOutputGroup"),
            new KeyValuePair<string, string>("SourceFiles",           "SourceFilesProjectOutputGroup"),
            new KeyValuePair<string, string>("XmlSerializer",         "SGenFilesOutputGroup"),
        };

        /// <summary>Maps integer ids to project item instances</summary>
        private IdItemMapping itemIdMap = new IdItemMapping();

        /// <summary>A service provider call back object provided by the IDE hosting the project manager</summary>
        private IServiceProvider site;

        private TrackDocumentsHelper tracker;

        private bool isInBatchUpdate;

        /// <summary>
        /// This property returns the time of the last change made to this project.
        /// It is not the time of the last change on the project file, but actually of
        /// the in memory project settings.  In other words, it is the last time that 
        /// SetProjectDirty was called.
        /// </summary>
        private DateTime lastModifiedTime;

        /// <summary>
        /// MSBuild engine we are going to use 
        /// </summary>
        private Microsoft.Build.Evaluation.ProjectCollection buildEngine;

        private IDEBuildLogger buildLogger;

        private Microsoft.Build.Evaluation.Project buildProject;

        // TODO cache an instance for perf; but be sure not to be stale (correctness)
        private BuildActionConverter buildActionConverter = new BuildActionConverter();

        private ConfigProvider configProvider;

        private Shell.ErrorListProvider projectErrorListProvider;

        private ExtensibilityEventsHelper myExtensibilityEventsHelper;

        private string filename;

        private Microsoft.VisualStudio.Shell.Url baseUri;

        private bool isDirty;

        private bool isNewProject;

        private bool projectOpened;

        private bool buildIsPrepared;

        private ImageHandler imageHandler;

        private string errorString;

        private string warningString;

        private Guid projectIdGuid;

        private ProjectOptions options;


        private bool isClosed;

        private EventTriggering eventTriggeringFlag = EventTriggering.TriggerAll;

        private bool canFileNodesHaveChilds = false;

        private bool isProjectEventsListener = true;

        /// <summary>
        /// The build dependency list passed to IVsDependencyProvider::EnumDependencies 
        /// </summary>
        private List<IVsBuildDependency> buildDependencyList = new List<IVsBuildDependency>();

        /// <summary>
        /// Defines if Project System supports Project Designer
        /// </summary>
        private bool supportsProjectDesigner;

        private bool showProjectInSolutionPage = true;

        /// <summary>
        /// Field for determining whether sourcecontrol should be disabled.
        /// </summary>
        private bool disableScc;

        private string sccProjectName;

        private string sccLocalPath;

        private string sccAuxPath;

        private string sccProvider;

        /// <summary>
        /// Flag for controling how many times we register with the Scc manager.
        /// </summary>
        private bool isRegisteredWithScc;

        /// <summary>
        /// Flag for controling query edit should communicate with the scc manager.
        /// </summary>
        private bool disableQueryEdit;

        /// <summary>
        /// Control if command with potential destructive behavior such as delete should
        /// be enabled for nodes of this project.
        /// </summary>
        private bool canProjectDeleteItems = true;

        private Microsoft.Build.Framework.ILogger myDebugLogger;
        private static readonly System.Runtime.Versioning.FrameworkName DefaultTargetFrameworkMoniker = new System.Runtime.Versioning.FrameworkName(".NETFramework", new Version(4, 0));

        /// <summary>
        /// Member to store output base relative path. Used by OutputBaseRelativePath property
        /// </summary>
        private string outputBaseRelativePath = "bin";

        private IProjectEvents projectEventsProvider;

        /// <summary>
        /// Used for flavoring to hold the XML fragments
        /// </summary>
        private XmlDocument xmlFragments = null;

        /// <summary>
        /// Used to map types to CATID. This provide a generic way for us to do this
        /// and make it simpler for a project to provide it's CATIDs for the different type of objects
        /// for which it wants to support extensibility. This also enables us to have multiple
        /// type mapping to the same CATID if we choose to.
        /// </summary>
        private Dictionary<Type, Guid> catidMapping = new Dictionary<Type, Guid>();

        /// <summary>
        /// The public package implementation.
        /// </summary>
        private ProjectPackage package;

        private bool isDisposed;

        /// <summary>
        /// This Guid must match the Guid you registered under
        /// HKLM\Software\Microsoft\VisualStudio\%version%\Projects.
        /// Among other things, the Project framework uses this 
        /// guid to find your project and item templates.
        /// </summary>
        public abstract Guid ProjectGuid
        {
            get;
        }

        public abstract string TargetFSharpCoreVersion { get; set; }

        internal Shell.ErrorListProvider ProjectErrorsTaskListProvider 
        {
            get { return projectErrorListProvider; }
        }

        /// <summary>
        /// Returns a caption for VSHPROPID_TypeName.
        /// </summary>
        /// <returns></returns>
        public abstract string ProjectType
        {
            get;
        }

        /// <summary>
        /// True if project contains implicitly expanded lists of references.
        /// For example, portable libraries doesn't include the whole list of separate Reference items for the target profile.
        /// Instead MSBuild itself can resolve list of references for the target framework.
        /// </summary>
        public bool ImplicitlyExpandTargetFramework
        {
            get
            {
                var value = GetProjectProperty(MsBuildTarget.ImplicitlyExpandTargetFramework);
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) && buildProject.Targets.ContainsKey(MsBuildTarget.ImplicitlyExpandTargetFramework);
            }
        }

        /// <summary>
        /// This is the project instance guid that is peristed in the project file
        /// </summary>
        [System.ComponentModel.BrowsableAttribute(false)]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ID")]
        public virtual Guid ProjectIDGuid
        {
            get
            {
                return this.projectIdGuid;
            }
            set
            {
                if (this.projectIdGuid != value)
                {
                    this.projectIdGuid = value;
                    if (this.buildProject != null)
                    {
                        this.SetProjectProperty("ProjectGuid", this.projectIdGuid.ToString("B"));
                    }
                }
            }
        }

        /// <summary>
        /// Denotes if FSharp.Core reference is relying on TargetFSharpCore property
        /// </summary>
        public bool CanUseTargetFSharpCoreReference { get; set; }

        /// <summary>
        /// Easy access to the collection of visible, user-defined project items
        /// </summary>
        public IEnumerable<Build.Evaluation.ProjectItem> VisibleItems
        {
            get { return MSBuildProject.GetStaticAndVisibleItemsInOrder(this.buildProject); }
        }

        public override int MenuCommandId
        {
            get
            {
                return VsMenus.IDM_VS_CTXT_PROJNODE;
            }
        }

        public override string Url
        {
            get
            {
                return this.GetMkDocument();
            }
        }

        public void SetDebugLogger(Microsoft.Build.Framework.ILogger debugLogger)
        {
            myDebugLogger = debugLogger;
        }


        public override string Caption
        {
            get
            {
                // Default to file name
                string caption = MSBuildProject.GetFullPath(this.buildProject);
                if (String.IsNullOrEmpty(caption))
                {
                    if (this.buildProject.GetProperty(ProjectFileConstants.Name) != null)
                    {
                        caption = this.buildProject.GetProperty(ProjectFileConstants.Name).EvaluatedValue;
                        if (caption == null || caption.Length == 0)
                        {
                            caption = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
                        }
                    }
                }
                else
                {
                    caption = Path.GetFileNameWithoutExtension(caption);
                }

                return caption;
            }
        }

        public override Guid ItemTypeGuid
        {
            get
            {
                return this.ProjectGuid;
            }
        }

        public override int ImageIndex
        {
            get
            {
                return (int)ProjectNode.ImageName.Application;
            }
        }


        public virtual string ErrorString
        {
            get
            {
                if (this.errorString == null)
                {
                    this.errorString = SR.GetString(SR.Error, CultureInfo.CurrentUICulture);
                }

                return this.errorString;
            }
        }

        public virtual string WarningString
        {
            get
            {
                if (this.warningString == null)
                {
                    this.warningString = SR.GetString(SR.Warning, CultureInfo.CurrentUICulture);
                }

                return this.warningString;
            }
        }

        /// <summary>
        /// The target name that will be used for evaluating the project file (i.e., pseudo-builds).
        /// This target is used to trigger a build with when the project system changes. 
        /// Example: The language projrcts are triggering a build with the Compile target whenever 
        /// the project system changes.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "ReEvaluate")]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Re")]
        public virtual string ReEvaluateProjectFileTargetName
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// This is the object that will be returned by EnvDTE.Project.Object for this project
        /// </summary>
        public virtual object ProjectObject
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Override this property to specify when the project file is dirty.
        /// </summary>
        public virtual bool IsProjectFileDirty
        {
            get
            {
                string document = this.GetMkDocument();

                if (String.IsNullOrEmpty(document))
                {
                    return this.isDirty;
                }

                return (this.isDirty || !File.Exists(document));
            }
        }

        /// <summary>
        /// True if the project uses the Project Designer Editor instead of the property page frame to edit project properties.
        /// </summary>
        public virtual bool SupportsProjectDesigner
        {
            get
            {
                return this.supportsProjectDesigner;
            }
            set
            {
                this.supportsProjectDesigner = value;
            }

        }

        public virtual Guid ProjectDesignerEditor
        {
            get
            {
                return VSConstants.GUID_ProjectDesignerEditor;
            }
        }

        /// <summary>
        /// Defines the flag that supports the VSHPROPID.ShowProjInSolutionPage
        /// </summary>
        public virtual bool ShowProjectInSolutionPage
        {
            get
            {
                return this.showProjectInSolutionPage;
            }
            set
            {
                this.showProjectInSolutionPage = value;
            }
        }

        public bool IsInBatchUpdate
        {
            get { return isInBatchUpdate; }
        }

        /// <summary>
        /// Gets or sets the ability of a project filenode to have child nodes (sub items).
        /// Example would be C#/VB forms having resx and designer files.
        /// </summary>
        public bool CanFileNodesHaveChilds
        {
            get
            {
                return canFileNodesHaveChilds;
            }
            set
            {
                canFileNodesHaveChilds = value;
            }
        }

        public IVsHierarchy InteropSafeIVsHierarchy { get; protected set; }
        public IVsUIHierarchy InteropSafeIVsUIHierarchy { get; protected set; }
        public IVsProject InteropSafeIVsProject { get; protected set; }
        public IVsSccProject2 InteropSafeIVsSccProject2 { get; protected set; }
        public IVsProjectFlavorCfgProvider InteropSafeIVsProjectFlavorCfgProvider { get; protected set; }

        /// <summary>
        /// Gets a service provider object provided by the IDE hosting the project
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public IServiceProvider Site
        {
            get
            {
                return this.site;
            }
        }

        /// <summary>
        /// Gets an ImageHandler for the project node.
        /// </summary>
        internal ImageHandler ImageHandler
        {
            get
            { 
                if (null == imageHandler)
                {
                    imageHandler = new ImageHandler(typeof(ProjectNode).Assembly.GetManifestResourceStream("Resources.imagelis.bmp"));
                }
                return imageHandler;
            }
        }

        /// <summary>
        /// This property returns the time of the last change made to this project.
        /// It is not the time of the last change on the project file, but actually of
        /// the in memory project settings.  In other words, it is the last time that 
        /// SetProjectDirty was called.
        /// </summary>
        public DateTime LastModifiedTime
        {
            get
            {
                return this.lastModifiedTime;
            }
        }

        /// <summary>
        /// Determines whether this project is a new project.
        /// </summary>
        public bool IsNewProject
        {
            get
            {
                return this.isNewProject;
            }
        }

        /// <summary>
        /// Gets the path to the folder containing the project.
        /// </summary>
        public string ProjectFolder
        {
            get
            {
                return Path.GetDirectoryName(this.filename);
            }
        }

        /// <summary>
        /// Gets or sets the project filename.
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return Path.GetFileName(this.filename);
            }
            set
            {
                this.SetEditLabel(value);
            }
        }

        /// <summary>
        /// Gets the Base Uniform Resource Identifier (URI).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "URI")]
        public Microsoft.VisualStudio.Shell.Url BaseURI
        {
            get
            {
                if (baseUri == null && this.buildProject != null)
                {
                    string path = System.IO.Path.GetDirectoryName(MSBuildProject.GetFullPath(this.buildProject));
                    // Uri/Url behave differently when you have trailing slash and when you dont
                    if (!path.EndsWith("\\", StringComparison.Ordinal) && !path.EndsWith("/", StringComparison.Ordinal))
                        path += "\\";
                    baseUri = new Url(path);
                }

                Debug.Assert(baseUri != null, "Base URL should not be null. Did you call BaseURI before loading the project?");
                return baseUri;
            }
        }

        /// <summary>
        /// Gets whether or not the project is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                return this.isClosed;
            }
        }


        /// <summary>
        /// Gets or set the relative path to the folder containing the project ouput. 
        /// </summary>
        public virtual string OutputBaseRelativePath
        {
            get
            {
                return this.outputBaseRelativePath;
            }
            set
            {
                if (Path.IsPathRooted(value))
                {
                    throw new ArgumentException("Path must not be rooted.");
                }

                this.outputBaseRelativePath = value;
            }
        }

        /// <summary>
        /// Gets or sets the flag whether query edit should communicate with the scc manager.
        /// </summary>
        public bool DisableQueryEdit
        {
            get
            {
                return this.disableQueryEdit;
            }
            set
            {
                this.disableQueryEdit = value;
            }
        }

        /// <summary>
        /// Gets a collection of integer ids that maps to project item instances
        /// </summary>
        public IdItemMapping ItemIdMap
        {
            get
            {
                return this.itemIdMap;
            }
        }

        /// <summary>
        /// Get the helper object that track document changes.
        /// </summary>
        internal TrackDocumentsHelper Tracker
        {
            get
            {
                return this.tracker;
            }
        }

        /// <summary>
        /// Gets whether or not the readonly file attribute is set for this project.
        /// </summary>
        public bool IsReadOnly
        {
            get
            {
                return (File.GetAttributes(filename) & FileAttributes.ReadOnly) != 0;
            }
        }

        /// <summary>
        /// Gets the project file name.
        /// </summary>
        public string FileName
        {
            get
            {
                return this.filename;
            }
        }


        /// <summary>
        /// Gets the configuration provider.
        /// </summary>
        public ConfigProvider ConfigProvider
        {
            get
            {
                if (this.configProvider == null)
                {
                    this.configProvider = CreateConfigProvider();
                }

                return this.configProvider;
            }
        }

        /// <summary>
        /// Gets BuildActionConverter for this project, enumerating all build actions available
        /// </summary>
        internal BuildActionConverter BuildActionConverter
        {
            get
            {
                return this.buildActionConverter;
            }
        }


        /// <summary>
        /// Gets or sets whether or not source code control is disabled for this project.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public bool IsSccDisabled
        {
            get
            {
                return this.disableScc;
            }
            set
            {
                this.disableScc = value;
            }
        }

        /// <summary>
        /// Gets or set whether items can be deleted for this project.
        /// Enabling this feature can have the potential destructive behavior such as deleting files from disk.
        /// </summary>
        public bool CanProjectDeleteItems
        {
            get
            {
                return canProjectDeleteItems;
            }
            set
            {
                canProjectDeleteItems = value;
            }
        }

        /// <summary>
        /// Determines whether the project was fully opened. This is set when the OnAfterOpenProject has triggered.
        /// </summary>
        public bool HasProjectOpened
        {
            get
            {
                return this.projectOpened;
            }
        }

        /// <summary>
        /// Gets or sets event triggering flags.
        /// </summary>
        public EventTriggering EventTriggeringFlag
        {
            get
            {
                return this.eventTriggeringFlag;
            }
            set
            {
                this.eventTriggeringFlag = value;
            }
        }

        /// <summary>
        /// Defines the build project that has loaded the project file.
        /// </summary>
        public Microsoft.Build.Evaluation.Project BuildProject
        {
            get
            {
                return this.buildProject;
            }
            set
            {
                Debug.Assert(this.buildProject == null, "Trying to set a new build project. Is this really the intention");
                SetBuildProject(value);
            }
        }

        /// <summary>
        /// Defines the build engine that is used to build the project file.
        /// </summary>
        public Microsoft.Build.Evaluation.ProjectCollection BuildEngine
        {
            get
            {
                return this.buildEngine;
            }
            set
            {
                Debug.Assert(this.buildEngine == null, "Trying to set a new build engine. Is this really the intention");
                this.buildEngine = value;
            }
        }

		/// <summary>
		/// The public package implementation.
		/// </summary>
		public ProjectPackage Package
		{
			get
			{
				return this.package;
			}
			set
			{
				this.package = value;
			}
		}

        protected ProjectNode()
        {
            this.OleServiceProvider.AddService(typeof(SVsDesignTimeAssemblyResolution), this, false);
            myExtensibilityEventsHelper = new ExtensibilityEventsHelper(this);
            this.Initialize();
        }

        public override NodeProperties CreatePropertiesObject()
        {
            return new ProjectNodeProperties(this);
        }

        /// <summary>
        /// Sets the properties for the project node.
        /// </summary>
        /// <param name="propid">Identifier of the hierarchy property. For a list of propid values, <see cref="__VSHPROPID"/> </param>
        /// <param name="value">The value to set. </param>
        /// <returns>A success or failure value.</returns>
        public override int SetProperty(int propid, object value)
        {
            __VSHPROPID id = (__VSHPROPID)propid;

            switch (id)
            {
                case __VSHPROPID.VSHPROPID_ShowProjInSolutionPage:
                    this.ShowProjectInSolutionPage = (bool)value;
                    return VSConstants.S_OK;
                case __VSHPROPID.VSHPROPID_DefaultNamespace:
                    SetProjectProperty(ProjectFileConstants.DefaultNamespace, (string)value);
                    return VSConstants.S_OK;
            }

            return base.SetProperty(propid, value);
        }

        /// <summary>
        /// Renames the project node.
        /// </summary>
        /// <param name="label">The new name</param>
        /// <returns>A success or failure value.</returns>
        public override int SetEditLabel(string label)
        {
            // Validate the filename. 
            if (String.IsNullOrEmpty(label))
            {
                throw new InvalidOperationException(SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture));
            }
            else if (this.ProjectFolder.Length + label.Length + 1 > NativeMethods.MAX_PATH)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.PathTooLong, CultureInfo.CurrentUICulture), label));
            }
            else if (Utilities.IsFileNameInvalid(label))
            {
                throw new InvalidOperationException(SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture));
            }

            string fileName = Path.GetFileNameWithoutExtension(label);

            // if there is no filename or it starts with a leading dot issue an error message and quit.
            if (String.IsNullOrEmpty(fileName) || fileName[0] == '.')
            {
                throw new InvalidOperationException(SR.GetString(SR.FileNameCannotContainALeadingPeriod, CultureInfo.CurrentUICulture));
            }

            // Nothing to do if the name is the same
            string oldFileName = Path.GetFileNameWithoutExtension(this.Url);
            if (String.Compare(oldFileName, label, StringComparison.Ordinal) == 0)
            {
                return VSConstants.S_FALSE;
            }

            // Now check whether the original file is still there. It could have been renamed.
            if (!File.Exists(this.Url))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileOrFolderCannotBeFound, CultureInfo.CurrentUICulture), this.ProjectFile));
            }

            // Get the full file name and then rename the project file.
            string newFile = Path.Combine(this.ProjectFolder, label);
            string extension = Path.GetExtension(this.Url);

            // Make sure it has the correct extension
            if (String.Compare(Path.GetExtension(newFile), extension, StringComparison.OrdinalIgnoreCase) != 0)
            {
                newFile += extension;
            }

            this.RenameProjectFile(newFile);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the automation object for the project node.
        /// </summary>
        /// <returns>An instance of an EnvDTE.Project implementation object representing the automation object for the project.</returns>
        public override object GetAutomationObject()
        {
            return new Automation.OAProject(this);
        }

        /// <summary>
        /// Closes the project node.
        /// </summary>
        /// <returns>A success or failure value.</returns>
        public override int Close()
        {
            int hr = VSConstants.S_OK;
            if (!this.isClosed)
            {
                try
                {
                    // Walk the tree and close all nodes.
                    // This has to be done before the project closes, since we want still state available for the ProjectMgr on the nodes 
                    // when nodes are closing.
                    try
                    {
                        CloseAllSubNodes(this);
                    }
                    finally
                    {
                        ErrorHandler.ThrowOnFailure(base.Close());  // calls this.Dispose(true)
                    } 
                }
                catch (COMException e)
                {
                    hr = e.ErrorCode;
                }

                this.isClosed = true;
            }
            return hr;
        }

        /// <summary>
        /// Sets the service provider from which to access the services. 
        /// </summary>
        /// <param name="site">An instance to an Microsoft.VisualStudio.OLE.Interop object</param>
        /// <returns>A success or failure value.</returns>
        public override int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site)
        {
            CCITracing.TraceCall();
            this.site = new ServiceProvider(site);

            if (projectErrorListProvider != null)
            {
                projectErrorListProvider.Dispose();
            }

            projectErrorListProvider = new Shell.ErrorListProvider (this.site);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the properties of the project node. 
        /// </summary>
        /// <param name="propId">The __VSHPROPID of the property.</param>
        /// <returns>A property dependent value. See: <see cref="__VSHPROPID"/> for details.</returns>
        public override object GetProperty(int propId)
        {
            switch ((__VSHPROPID)propId)
            {
                case __VSHPROPID.VSHPROPID_ConfigurationProvider:
                    return this.ConfigProvider;

                case __VSHPROPID.VSHPROPID_ProjectName:
                    return this.Caption;

                case __VSHPROPID.VSHPROPID_ProjectDir:
                    return this.ProjectFolder;

                case __VSHPROPID.VSHPROPID_TypeName:
                    return this.ProjectType;
                
                case __VSHPROPID.VSHPROPID_OwnerKey:
                    return this.projectIdGuid.ToString();

                case __VSHPROPID.VSHPROPID_ShowProjInSolutionPage:
                    return this.ShowProjectInSolutionPage;

                case __VSHPROPID.VSHPROPID_ExpandByDefault:
                    return true;

                // Use the same icon as if the folder was closed
                case __VSHPROPID.VSHPROPID_OpenFolderIconIndex:
                    return GetProperty((int)__VSHPROPID.VSHPROPID_IconIndex);
                case __VSHPROPID.VSHPROPID_DefaultNamespace:
                    return GetProjectProperty(ProjectFileConstants.DefaultNamespace);
            }

            switch ((__VSHPROPID2)propId)
            {
                case __VSHPROPID2.VSHPROPID_SupportsProjectDesigner:
                    return this.SupportsProjectDesigner;

                case __VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList:
                    return Utilities.CreateSemicolonDelimitedListOfStringFromGuids(this.GetConfigurationIndependentPropertyPages());

                case __VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList:
                    return Utilities.CreateSemicolonDelimitedListOfStringFromGuids(this.GetConfigurationDependentPropertyPages());

                case __VSHPROPID2.VSHPROPID_PriorityPropertyPagesCLSIDList:
                    return Utilities.CreateSemicolonDelimitedListOfStringFromGuids(this.GetPriorityProjectDesignerPages());

                case __VSHPROPID2.VSHPROPID_Container:
                    return true;
                default:
                    break;
            }

            return base.GetProperty(propId);
        }

        /// <summary>
        /// Gets the GUID value of the node. 
        /// </summary>
        /// <param name="propid">A __VSHPROPID or __VSHPROPID2 value of the guid property</param>
        /// <param name="guid">The guid to return for the property.</param>
        /// <returns>A success or failure value.</returns>
        public override int GetGuidProperty(int propid, out Guid guid)
        {
            guid = Guid.Empty;
            if ((__VSHPROPID)propid == __VSHPROPID.VSHPROPID_ProjectIDGuid)
            {
                guid = this.ProjectIDGuid;
            }
            else if (propid == (int)__VSHPROPID.VSHPROPID_CmdUIGuid)
            {
                guid = this.ProjectGuid;
            }
            else if ((__VSHPROPID2)propid == __VSHPROPID2.VSHPROPID_ProjectDesignerEditor && this.SupportsProjectDesigner)
            {
                guid = this.ProjectDesignerEditor;
            }
            else
            {
                base.GetGuidProperty(propid, out guid);
            }

            if (guid.CompareTo(Guid.Empty) == 0)
            {
                return VSConstants.DISP_E_MEMBERNOTFOUND;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Sets Guid properties for the project node.
        /// </summary>
        /// <param name="propid">A __VSHPROPID or __VSHPROPID2 value of the guid property</param>
        /// <param name="guid">The guid value to set.</param>
        /// <returns>A success or failure value.</returns>
        public override int SetGuidProperty(int propid, ref Guid guid)
        {
            switch ((__VSHPROPID)propid)
            {
                case __VSHPROPID.VSHPROPID_ProjectIDGuid:
                    this.ProjectIDGuid = guid;
                    return VSConstants.S_OK;
            }
            CCITracing.TraceCall(String.Format(CultureInfo.CurrentCulture, "Property {0} not found", propid));
            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        /// <summary>
        /// Removes items from the hierarchy. 
        /// </summary>
        /// <devdoc>Project overwrites this.</devdoc>
        public override void Remove(bool removeFromStorage, bool promptSave = true)
        {
            // the project will not be deleted from disk, just removed      
            if (removeFromStorage)
            {
                return;
            }

            Debug.Assert(promptSave, "Non-save prompting removal is not supported");

            // Remove the entire project from the solution
            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            uint iOption = 1; // SLNSAVEOPT_PromptSave
            ErrorHandler.ThrowOnFailure(solution.CloseSolutionElement(iOption, InteropSafeIVsHierarchy, 0));
        }

        /// <summary>
        /// Gets the moniker for the project node. That is the full path of the project file.
        /// </summary>
        /// <returns>The moniker for the project file.</returns>
        public override string GetMkDocument()
        {
            Debug.Assert(!String.IsNullOrEmpty(this.filename));
            Debug.Assert(this.BaseURI != null && !String.IsNullOrEmpty(this.BaseURI.AbsoluteUrl));
            return Path.Combine(this.BaseURI.AbsoluteUrl, this.filename);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            try
            {
                try
                {
                    this.UnRegisterProject();
                }
                finally
                {
                    try
                    {
                        this.RegisterClipboardNotifications(false);
                    }
                    finally
                    {
                        try
                        {
                            if (this.projectEventsProvider != null)
                            {
                                this.projectEventsProvider.AfterProjectFileOpened -= this.OnAfterProjectOpen;
                            }
                            if (projectErrorListProvider != null)
                            {
                                projectErrorListProvider.Dispose();
                                projectErrorListProvider = null;
                            }
                            if (buildLogger != null)
                            {
                               buildLogger = null;
                            }
                            this.site = null;
                        }
                        finally
                        {
                        }
                    }
                }

                if (this.buildProject != null)
                {
                    //this.projectInstance = null;
                    MSBuildProject.FullyUnloadProject(this.buildProject.ProjectCollection, this.buildProject);
                    SetBuildProject(null);
                }

                if (null != imageHandler)
                {
                    imageHandler.Close();
                    imageHandler = null;
                }

                tracker = null;
                itemIdMap.Clear();
                configProvider = null;
                myExtensibilityEventsHelper = null;
                buildActionConverter = null;
                buildDependencyList = null;

                // IMPORTANT: drop references to RCWs so GC can reclaim them.
                // Otherwise cycle CAggregator -> ProjectNode -> RCW that implicitly hold CAggregator will never be collected
                InteropSafeIVsHierarchy = null;
                InteropSafeIVsProject = null;
                InteropSafeIVsSccProject2 = null;
                InteropSafeIVsUIHierarchy = null;
                InteropSafeIVsProjectFlavorCfgProvider = null;
                buildEngine = null;
                buildLogger = null;
                catidMapping.Clear();                
            }
            finally
            {
                base.Dispose(disposing);
                this.isDisposed = true;
            }
        }

        /// <summary>
        /// Handles command status on the project node. If a command cannot be handled then the base should be called.
        /// </summary>
        /// <param name="cmdGroup">A unique identifier of the command group. The pguidCmdGroup parameter can be NULL to specify the standard group.</param>
        /// <param name="cmd">The command to query status for.</param>
        /// <param name="pCmdText">Pointer to an OLECMDTEXT structure in which to return the name and/or status information of a single command. Can be NULL to indicate that the caller does not require this information.</param>
        /// <param name="result">An out parameter specifying the QueryStatusResult of the command.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch ((VsCommands)cmd)
                {
                    case VsCommands.Copy:
                    case VsCommands.Paste:
                    case VsCommands.Cut:
                    case VsCommands.Rename:
                    case VsCommands.Exit:
                    case VsCommands.ProjectSettings:
                    case VsCommands.UnloadProject:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands.ViewForm:
                        if (this.HasDesigner)
                        {
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                            return VSConstants.S_OK;
                        }
                        break;

                    case VsCommands.NewFolder:
                    case VsCommands.AddNewItem:
                    case VsCommands.AddExistingItem:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands.SetStartupProject:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {

                switch ((VsCommands2K)cmd)
                {
                    case VsCommands2K.ADDREFERENCE:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands2K.EXCLUDEFROMPROJECT:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            }
            else if(cmdGroup == VSProjectConstants.FSharpSendProjectOutputToInteractiveCmd.Guid)
            {
                if (cmd == VSProjectConstants.FSharpSendProjectOutputToInteractiveCmd.ID)
                {
                    result |= QueryStatusResult.SUPPORTED;
                    if (options == null)
                    {
                        var currentConfigName = GetCurrentConfigurationName();
                        if (currentConfigName != null)
                        {
                            GetProjectOptions(currentConfigName.Value);
                        }
                    }
                    if (options != null && File.Exists(options.OutputAssembly))
                    {
                        result |= QueryStatusResult.ENABLED;
                    }
                    return VSConstants.S_OK;
                }
            }
            else
            {
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        /// <summary>
        /// Handles command execution.
        /// </summary>
        /// <param name="cmdGroup">Unique identifier of the command group</param>
        /// <param name="cmd">The command to be executed.</param>
        /// <param name="nCmdexecopt">Values describe how the object should execute the command.</param>
        /// <param name="pvaIn">Pointer to a VARIANTARG structure containing input arguments. Can be NULL</param>
        /// <param name="pvaOut">VARIANTARG structure to receive command output. Can be NULL.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch ((VsCommands)cmd)
                {

                    case VsCommands.UnloadProject:
                        return this.UnloadProject();
                    case VsCommands.CleanSel:
                    case VsCommands.CleanCtx:
                        return this.CleanProject();
                }
            }
            else if (cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                switch ((VsCommands2K)cmd)
                {
                    case VsCommands2K.ADDREFERENCE:
                        return this.AddProjectReference();

                    case VsCommands2K.ADDWEBREFERENCE:
                    case VsCommands2K.ADDWEBREFERENCECTX:
                        return this.AddWebReference();
                }
            }
            else if (cmdGroup == VSProjectConstants.FSharpSendProjectOutputToInteractiveCmd.Guid)
            {
                if (cmd == VSProjectConstants.FSharpSendProjectOutputToInteractiveCmd.ID)
                {
                    if (options == null)
                    {
                        var currentConfigName = GetCurrentConfigurationName();
                        if (currentConfigName != null)
                        {
                            GetProjectOptions(currentConfigName.Value);
                        }                        
                    }

                    if (options != null)
                    {
                        var path = options.OutputAssembly;
                        if (File.Exists(path))
                        {
                            SendReferencesToFSI(new[] { path });
                        }
                    }
                    return VSConstants.S_OK;
                }
            }

            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        internal abstract void SendReferencesToFSI(string[] reference);

        /// <summary>
        /// Get the boolean value for the deletion of a project item
        /// </summary>
        /// <param name="deleteOperation">A flag that specifies the type of delete operation (delete from storage or remove from project)</param>
        /// <returns>true if item can be deleted from project</returns>
        public override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            if (deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a specific Document manager to handle opening and closing of the Project(Application) Designer if projectdesigner is supported.
        /// </summary>
        /// <returns>Document manager object</returns>
        internal override DocumentManager GetDocumentManager()
        {
            if (this.SupportsProjectDesigner)
            {
                return new ProjectDesignerDocumentManager(this);
            }
            return null;
        }

        public void UpdateValueOfCanUseTargetFSharpCoreReferencePropertyIfNecessary(ReferenceNode node)
        {
            // property is already set, not need to make one more check
            if (CanUseTargetFSharpCoreReference)
                return;

            var element = node.ItemNode;
            if (AssemblyReferenceNode.IsFSharpCoreReference(node) && AssemblyReferenceNode.ContainsUsagesOfTargetFSharpCoreVersionProperty(node))
            {
                ProjectMgr.CanUseTargetFSharpCoreReference = true;
            }
        }

        /// <summary>
        /// Executes a wizard.
        /// </summary>
        /// <param name="parentNode">The node to which the wizard should add item(s).</param>
        /// <param name="itemName">The name of the file that the user typed in.</param>
        /// <param name="wizardToRun">The name of the wizard to run.</param>
        /// <param name="dlgOwner">The owner of the dialog box.</param>
        /// <returns>A VSADDRESULT enum value describing success or failure.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dlg")]
        public virtual VSADDRESULT RunWizard(HierarchyNode parentNode, string itemName, string wizardToRun, IntPtr dlgOwner)
        {
            Debug.Assert(!String.IsNullOrEmpty(itemName), "The Add item dialog was passing in a null or empty item to be added to the hierrachy.");
            Debug.Assert(!String.IsNullOrEmpty(this.ProjectFolder), "The Project Folder is not specified for this project.");

            // We just validate for length, since we assume other validation has been performed by the dlgOwner.
            if (this.ProjectFolder.Length + itemName.Length + 1 > NativeMethods.MAX_PATH)
            {
                string errorMessage = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.PathTooLong, CultureInfo.CurrentUICulture), itemName);
                if (!Utilities.IsInAutomationFunction(this.Site))
                {
                    string title = null;
                    OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                    OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                    OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                    VsShellUtilities.ShowMessageBox(this.Site, title, errorMessage, icon, buttons, defaultButton);
                    return VSADDRESULT.ADDRESULT_Failure;
                }
                else
                {
                    throw new InvalidOperationException(errorMessage);
                }
            }


            // Build up the ContextParams safearray
            //  [0] = Wizard type guid  (bstr)
            //  [1] = Project name  (bstr)
            //  [2] = ProjectItems collection (bstr)
            //  [3] = Local Directory (bstr)
            //  [4] = Filename the user typed (bstr)
            //  [5] = Product install Directory (bstr)
            //  [6] = Run silent (bool)

            object[] contextParams = new object[7];
            contextParams[0] = EnvDTE.Constants.vsWizardAddItem;
            contextParams[1] = this.Caption;
            object automationObject = parentNode.GetAutomationObject();
            if (automationObject is EnvDTE.Project)
            {
                EnvDTE.Project project = (EnvDTE.Project)automationObject;
                contextParams[2] = project.ProjectItems;
            }
            else
            {
                // This would normally be a folder unless it is an item with subitems
                ProjectItem item = (ProjectItem)automationObject;
                contextParams[2] = item.ProjectItems;
            }

            contextParams[3] = this.ProjectFolder;

            contextParams[4] = itemName;

            object objInstallationDir = null;
            IVsShell shell = (IVsShell)this.GetService(typeof(IVsShell));
            ErrorHandler.ThrowOnFailure(shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out objInstallationDir));
            string installDir = (string)objInstallationDir;

            // append a '\' to the install dir to mimic what the shell does (though it doesn't add one to destination dir)
            if (!installDir.EndsWith("\\", StringComparison.Ordinal))
            {
                installDir += "\\";
            }

            contextParams[5] = installDir;

            contextParams[6] = true;

            IVsExtensibility3 ivsExtensibility = this.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;
            Debug.Assert(ivsExtensibility != null, "Failed to get IVsExtensibility3 service");
            if (ivsExtensibility == null)
            {
                return VSADDRESULT.ADDRESULT_Failure;
            }

            // Determine if we have the trust to run this wizard.
            IVsDetermineWizardTrust wizardTrust = this.GetService(typeof(SVsDetermineWizardTrust)) as IVsDetermineWizardTrust;
            if (wizardTrust != null)
            {
                Guid guidProjectAdding = Guid.Empty;
                object guidProjectAddingAsObject = this.GetProperty((int)__VSHPROPID2.VSHPROPID_AddItemTemplatesGuid);
                wizardTrust.OnWizardInitiated(wizardToRun, ref guidProjectAdding);
            }

            int wizResultAsInt;
            try
            {
                Array contextParamsAsArray = contextParams;

                int result = ivsExtensibility.RunWizardFile(wizardToRun, (int)dlgOwner, ref contextParamsAsArray, out wizResultAsInt);

                if (!ErrorHandler.Succeeded(result) && result != VSConstants.OLE_E_PROMPTSAVECANCELLED)
                {
                    ErrorHandler.ThrowOnFailure(result);
                }
            }
            finally
            {
                if (wizardTrust != null)
                {
                    wizardTrust.OnWizardCompleted();
                }
            }

            EnvDTE.wizardResult wizardResult = (EnvDTE.wizardResult)wizResultAsInt;

            switch (wizardResult)
            {
                default:
                    return VSADDRESULT.ADDRESULT_Cancel;
                case wizardResult.wizardResultSuccess:
                    return VSADDRESULT.ADDRESULT_Success;
                case wizardResult.wizardResultFailure:
                    return VSADDRESULT.ADDRESULT_Failure;
            }
        }

        /// <summary>
        /// Override this method if you want to modify the behavior of the Add Reference dialog
        /// By example you could change which pages are visible and which is visible by default.
        /// </summary>
        /// <returns></returns>
        public abstract int AddProjectReference();

        /// <summary>
        /// Returns the Compiler associated to the project 
        /// </summary>
        /// <returns>Null</returns>
        public virtual ICodeCompiler GetCompiler()
        {

            return null;
        }

        /// <summary>
        /// Override this method if you have your own project specific
        /// subclass of ProjectOptions
        /// </summary>
        /// <returns>This method returns a new instance of the ProjectOptions base class.</returns>
        internal virtual ProjectOptions CreateProjectOptions(ConfigCanonicalName config)
        {
            return new ProjectOptions(config);
        }

        /// <summary>
        /// Loads a project file. Called from the factory CreateProject to load the project.
        /// </summary>
        /// <param name="fileName">File name of the project that will be created. </param>
        /// <param name="location">Location where the project will be created.</param>
        /// <param name="name">If applicable, the name of the template to use when cloning a new project.</param>
        /// <param name="flags">Set of flag values taken from the VSCREATEPROJFLAGS enumeration.</param>
        /// <param name="iidProject">Identifier of the interface that the caller wants returned. </param>
        /// <param name="canceled">An out parameter specifying if the project creation was canceled</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "iid")]
        public virtual void Load(string fileName, string location, string name, uint flags, ref Guid iidProject, out int canceled)
        {
            try
            {
                this.disableQueryEdit = true;

                // set up public members and icons
                canceled = 0;

                this.ProjectMgr = this;
                this.isNewProject = false;

                if ((flags & (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE) == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE)
                {
                // we need to generate a new guid for the project
                    this.projectIdGuid = Guid.NewGuid();
                }
                else
                {
                    this.SetProjectGuidFromProjectFile(false);
                }

                this.buildEngine = Utilities.InitializeMsBuildEngine(this.buildEngine);

                // based on the passed in flags, this either reloads/loads a project, or tries to create a new one
                // now we create a new project... we do that by loading the template and then saving under a new name
                // we also need to copy all the associated files with it.                    
                if ((flags & (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE) == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE)
                {
                    Debug.Assert(!String.IsNullOrEmpty(fileName) && File.Exists(fileName), "Invalid filename passed to load the project. A valid filename is expected");

                    this.isNewProject = true;

                    // This should be a very fast operation if the build project is already initialized by the Factory.
                    SetBuildProject(Utilities.ReinitializeMsBuildProject(this.buildEngine, fileName, this.ProjectGlobalPropertiesThatAllProjectSystemsMustSet, this.buildProject));


                    // Compute the file name
                    // We try to solve two problems here. When input comes from a wizzard in case of zipped based projects 
                    // the parameters are different.
                    // In that case the filename has the new filename in a temporay path.

                    // First get the extension from the template.
                    // Then get the filename from the name.
                    // Then create the new full path of the project.
                    string extension = Path.GetExtension(fileName);

                    string tempName = String.Empty;

                    // We have to be sure that we are not going to loose data here. If the project name is a.b.c then for a project that was based on a zipped template(the wizzard calls us) GetFileNameWithoutExtension will suppress "c".
                    // We are going to check if the parameter "name" is extension based and the extension is the same as the one from the "filename" parameter.
                    string tempExtension = Path.GetExtension(name);
                    if (!String.IsNullOrEmpty(tempExtension))
                    {
                        bool isSameExtension = (String.Compare(tempExtension, extension, StringComparison.OrdinalIgnoreCase) == 0);

                        if (isSameExtension)
                        {
                            tempName = Path.GetFileNameWithoutExtension(name);
                        }
                        // If the tempExtension is not the same as the extension that the project name comes from then assume that the project name is a dotted name.
                        else
                        {
                            tempName = Path.GetFileName(name);
                        }
                    }
                    else
                    {
                        tempName = Path.GetFileName(name);
                    }

                    Debug.Assert(!String.IsNullOrEmpty(tempName), "Could not compute project name");
                    string tempProjectFileName = tempName + extension;
                    this.filename = Path.Combine(location, tempProjectFileName);

                    // Initialize the common project properties.
                    this.InitializeProjectProperties();

                    ErrorHandler.ThrowOnFailure(this.Save(this.filename, 1, 0));

                    // now we do have the project file saved. we need to create embedded files.
                    foreach (var item in MSBuildProject.GetStaticAndVisibleItemsInOrder(this.buildProject))
                    {
                        // Ignore the item if it is a reference or folder
                        if (this.FilterItemTypeToBeAddedToHierarchy(MSBuildItem.GetItemType(item)))
                        {
                            continue;
                        }

                        string strRelFilePath = MSBuildItem.GetEvaluatedInclude(item);
                        string basePath = Path.GetDirectoryName(fileName);
                        string strPathToFile;
                        string newFileName;
                        // taking the base name from the project template + the relative pathname,
                        // and you get the filename
                        strPathToFile = Path.Combine(basePath, strRelFilePath);
                        // the new path should be the base dir of the new project (location) + the rel path of the file
                        newFileName = Path.Combine(location, strRelFilePath);
                        // now the copy file
                        AddFileFromTemplate(strPathToFile, newFileName);
                    }
                }
                else
                {
                    this.filename = fileName;
                }

                // now reload to fix up references
                this.Reload();
            }
            finally
            {
                this.disableQueryEdit = false;
            }
        }

        /// <summary>
        /// Called to add a file to the project from a template.
        /// Override to do it yourself if you want to customize the file
        /// </summary>
        /// <param name="source">Full path of template file</param>
        /// <param name="target">Full path of file once added to the project</param>
        public virtual void AddFileFromTemplate(string source, string target)
        {
            if (String.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException("source");
            }
            if (String.IsNullOrEmpty(target))
            {
                throw new ArgumentNullException("target");
            }

            try
            {
                string directory = Path.GetDirectoryName(target);
                if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                FileInfo fiOrg = new FileInfo(source);
                FileInfo fiNew = fiOrg.CopyTo(target, true);

                fiNew.Attributes = FileAttributes.Normal; // remove any read only attributes.
            }
            catch (IOException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
            catch (NotSupportedException e)
            {
                Trace.WriteLine("Exception : " + e.Message);
            }
        }

        /// <summary>
        /// Called when the project opens an editor window for the given file
        /// </summary>
        public virtual void OnOpenItem(string fullPathToSourceFile)
        {
        }

        /// <summary>
        /// This is called from the main thread before the background build starts.
        ///  cleanBuild is not part of the vsopts, but passed down as the callpath is differently
        ///  PrepareBuild mainly creates directories and cleans house if cleanBuild is true
        /// </summary>
        /// <param name="config"></param>
        /// <param name="cleanBuild"></param>
        internal virtual void PrepareBuild(ConfigCanonicalName config, bool cleanBuild)
        {
            if (this.buildIsPrepared && !cleanBuild) return;

            ProjectOptions options = this.GetProjectOptions(config);
            string outputPath = Path.GetDirectoryName(options.OutputAssembly);

            if (cleanBuild && this.buildProject.Targets.ContainsKey(MsBuildTarget.Clean))
            {
                this.InvokeMsBuild(MsBuildTarget.Clean);
            }


            PackageUtilities.EnsureOutputPath(outputPath);
            if (!String.IsNullOrEmpty(options.XMLDocFileName))
            {
                PackageUtilities.EnsureOutputPath(Path.GetDirectoryName(options.XMLDocFileName));
            }

            this.buildIsPrepared = true;
        }

        // Helper for sharing common code between Build() and BuildAsync()
        private bool BuildPrelude(IVsOutputWindowPane output)
        {
            bool engineLogOnlyCritical = false;
            // If there is some output, then we can ask the build engine to log more than
            // just the critical events.
            if (null != output)
            {
                engineLogOnlyCritical = BuildEngine.OnlyLogCriticalEvents;
                BuildEngine.OnlyLogCriticalEvents = false;
            }
            this.SetOutputLogger(output);
            return engineLogOnlyCritical;
        }
        // Helper for sharing common code between Build() and BuildAsync()
        private void BuildCoda(IVsOutputWindowPane output, bool engineLogOnlyCritical)
        {
            // Unless someone specifically request to use an output window pane, we should not output to it
            if (null != output)
            {
                this.SetOutputLogger(null);
                BuildEngine.OnlyLogCriticalEvents = engineLogOnlyCritical;
            }
        }
        internal virtual void Build(uint vsopts, ConfigCanonicalName configCanonicalName, IVsOutputWindowPane output, string target, MSBuildCoda coda)
        {
            bool engineLogOnlyCritical = BuildPrelude(output);
            MSBuildCoda fullCoda = (res,instance) =>
                {
                    BuildCoda(output, engineLogOnlyCritical);
                    coda(res,instance);
                };
            try
            {
                this.SetBuildConfigurationProperties(configCanonicalName);
                ProjectInstance ignoreMeToo = null;
                this.DoMSBuildSubmission(BuildKind.ASYNC, target, ref ignoreMeToo, fullCoda);
            }
            catch (Exception)
            {
                fullCoda(MSBuildResult.Failed, null);
                throw;
            }
        }
        /// <summary>
        /// Do the build by invoking msbuild
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "vsopts")]
        internal virtual BuildResult Build(ConfigCanonicalName configCanonicalName, IVsOutputWindowPane output, string target, IEnumerable<KeyValuePair<string, string>> extraProperties)
        {
            bool engineLogOnlyCritical = BuildPrelude(output);
            BuildResult result = BuildResult.FAILED;

            try
            {
                this.SetBuildConfigurationProperties(configCanonicalName);
                result = this.InvokeMsBuild(target, extraProperties);
            }
            finally
            {
                BuildCoda(output, engineLogOnlyCritical);
            }

            return result;
        }

        public string GetBuildMacroValue(string propertyName)
        {
            // This is performance optimization; only these two of build macro values require a build to get right
            if (ProjectFileConstants.TargetDir.Equals(propertyName, StringComparison.OrdinalIgnoreCase) ||
                ProjectFileConstants.TargetPath.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                this.SetCurrentConfiguration();
                this.UpdateMSBuildState();
                var result = this.InvokeMsBuild(ProjectFileConstants.AllProjectOutputGroups);
                if (result.ProjectInstance != null) return result.ProjectInstance.GetPropertyValue(propertyName);
            };
            return this.GetProjectProperty(propertyName, true);
        }

        /// <summary>
        /// Return the value of a project property
        /// </summary>
        /// <param name="propertyName">Name of the property to get</param>
        /// <param name="resetCache">True to avoid using the cache</param>
        /// <returns>null if property does not exist, otherwise value of the property</returns>
        public virtual string GetProjectProperty(string propertyName, bool resetCache)
        {
            Microsoft.Build.Evaluation.ProjectProperty property = GetMsBuildProperty(propertyName, resetCache);
            if (property == null)
                return null;

            return property.EvaluatedValue;
        }

        /// <summary>
        /// Return the value of a project property, unevaluated
        /// </summary>
        /// <param name="propertyName">Name of the property to get</param>
        /// <param name="resetCache">True to avoid using the cache</param>
        /// <returns>null if property does not exist, otherwise value of the property</returns>
        public virtual string GetUnevaluatedProjectProperty(string propertyName, bool resetCache)
        {
            var property = GetMsBuildProperty(propertyName, resetCache);
            if (property == null)
                return null;

            return property.UnevaluatedValue;
        }

        /// <summary>
        /// Set value of project property
        /// </summary>
        /// <param name="propertyName">Name of property</param>
        /// <param name="propertyValue">Value of property</param>
        public virtual void SetProjectProperty(string propertyName, string propertyValue)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", "Cannot set a null project property");

            string oldValue = GetProjectProperty(propertyName, true);
            if (propertyValue == null)
            {
                // if property already null, do nothing
                if (oldValue == null)
                    return;
                // otherwise, set it to empty
                propertyValue = String.Empty;
            }

            // Only do the work if this is different to what we had before
            if (String.Compare(oldValue, propertyValue, StringComparison.Ordinal) != 0)
            {
                // Check out the project file.
                if (!this.ProjectMgr.QueryEditProjectFile(false))
                {
                    throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                }

                this.buildProject.SetProperty(propertyName, propertyValue);  

                // property cache will need to be updated
                //this.projectInstance = null;
                try
                {
                    this.SetProjectFileDirty(true);
                    RaiseProjectPropertyChanged(propertyName, oldValue, propertyValue);
                }
                catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
                {
                    // they set some illegal value, such as '<'
                    // we must set things back to the old value to avoid getting caught in an infinite loop of exceptions
                    this.buildProject.SetProperty(propertyName, oldValue);
                    throw;
                }
            }
            return;
        }

        /// <summary>
        /// Utility routine to set or create build event properties.  Both must be 
        /// created at the end of the project file.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        public virtual void SetOrCreateBuildEventProperty(string propertyName, string propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName) == false)
            {
                string varXml = this.GetProjectProperty(propertyName, true);
                if (string.IsNullOrEmpty(varXml))
                {
                    Microsoft.Build.Construction.ProjectPropertyGroupElement newGroup = this.ProjectMgr.BuildProject.Xml.CreatePropertyGroupElement();
                    this.ProjectMgr.BuildProject.Xml.AppendChild(newGroup);  // must go last, see e.g. dev10 bug 757461
                    newGroup.AddProperty(propertyName, string.Empty);
                }

                this.SetProjectProperty(propertyName, propertyValue);
                SetProjectFileDirty(true);
            }
        }

        /// <remarks>Support hex format (like 0xFF)</remarks>
        /// <exception cref="T:System.Exception">
        /// Raise if invalid format
        /// The inner exception contains the real exception, of type FormatException, StackOverflowException
        /// </exception>
        public static long? ParsePropertyValueToInt64(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            var converter = new System.ComponentModel.Int64Converter();
            var result = converter.ConvertFromInvariantString(s);
            if (result == null)
                return null;

            return (long) result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        internal virtual ProjectOptions GetProjectOptions(ConfigCanonicalName configCanonicalName)
        {
            if (this.options != null && this.options.ConfigCanonicalName == configCanonicalName)
                return this.options;

            ProjectOptions options = this.options = CreateProjectOptions(configCanonicalName);

            options.GenerateExecutable = true;

            this.SetConfiguration(configCanonicalName);

            string outputPath = this.GetOutputPath();
            if (!String.IsNullOrEmpty(outputPath))
            {
                // absolutize relative to project folder location
                outputPath = Path.Combine(this.ProjectFolder, outputPath);
            }

            // Set some default values
            options.OutputAssembly = outputPath + this.Caption + ".exe";
            options.ModuleKind = ModuleKindFlags.ConsoleApplication;

            options.RootNamespace = GetProjectProperty(ProjectFileConstants.RootNamespace, false);
            options.OutputAssembly = outputPath + this.GetAssemblyName(configCanonicalName);

            string outputtype = GetProjectProperty(ProjectFileConstants.OutputType, false);
            if (!string.IsNullOrEmpty(outputtype))
            {
                outputtype = outputtype.ToLower(CultureInfo.InvariantCulture);
            }

            if (outputtype == "library")
            {
                options.ModuleKind = ModuleKindFlags.DynamicallyLinkedLibrary;
                options.GenerateExecutable = false; // DLL's have no entry point.
            }
            else if (outputtype == "winexe")
                options.ModuleKind = ModuleKindFlags.WindowsApplication;
            else
                options.ModuleKind = ModuleKindFlags.ConsoleApplication;

            options.Win32Icon = GetProjectProperty("ApplicationIcon", false);
            options.MainClass = GetProjectProperty("StartupObject", false);

            string targetPlatform = GetProjectProperty("TargetPlatform", false);

            if (targetPlatform != null && targetPlatform.Length > 0)
            {
                try
                {
                    options.TargetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform);
                }
                catch (ArgumentException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                options.TargetPlatformLocation = GetProjectProperty("TargetPlatformLocation", false);
                this.SetTargetPlatform(options);
            }

            //    other settings from CSharp we may want to adopt at some point...
            //    AssemblyKeyContainerName = ""  //This is the key file used to sign the interop assembly generated when importing a com object via add reference
            //    AssemblyOriginatorKeyFile = ""
            //    DelaySign = "false"
            //    DefaultClientScript = "JScript"
            //    DefaultHTMLPageLayout = "Grid"
            //    DefaultTargetSchema = "IE50"
            //    PreBuildEvent = ""
            //    PostBuildEvent = ""
            //    RunPostBuildEvent = "OnBuildSuccess"

            // transfer all config build options...
            if (GetBoolAttr("AllowUnsafeBlocks"))
            {
                options.AllowUnsafeCode = true;
            }

            string baseAddressPropertyString = GetProjectProperty("BaseAddress", false);
            try
            {
                var result = ParsePropertyValueToInt64(baseAddressPropertyString);
                if (result.HasValue)
                    options.BaseAddress = result.Value;
            }
            catch (Exception e)
            {
                Trace.WriteLine(string.Format("Exception parsing property {0}='{1}': {2}", "BaseAddress", baseAddressPropertyString, e.Message));
            }

            if (GetBoolAttr("CheckForOverflowUnderflow"))
            {
                options.CheckedArithmetic = true;
            }

            if (GetProjectProperty("DefineConstants", false) != null)
            {
                options.DefinedPreProcessorSymbols = new StringCollection();
                foreach (string s in GetProjectProperty("DefineConstants", false).Replace(" \t\r\n", "").Split(';'))
                {
                    options.DefinedPreProcessorSymbols.Add(s);
                }
            }

            string docFile = GetProjectProperty("DocumentationFile", false);
            if (!String.IsNullOrEmpty(docFile))
            {
                options.XMLDocFileName = Path.Combine(this.ProjectFolder, docFile);
            }

            if (GetBoolAttr("DebugSymbols"))
            {
                options.IncludeDebugInformation = true;
            }

            if (GetProjectProperty("FileAlignment", false) != null)
            {
                try
                {
                    options.FileAlignment = Int32.Parse(GetProjectProperty("FileAlignment", false), CultureInfo.InvariantCulture);
                }
                catch (ArgumentNullException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (ArgumentException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (FormatException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (OverflowException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
            }

            if (GetBoolAttr("IncrementalBuild"))
            {
                options.IncrementalCompile = true;
            }

            if (GetBoolAttr("Optimize"))
            {
                options.Optimize = true;
            }

            if (GetBoolAttr("RegisterForComInterop"))
            {
            }

            if (GetBoolAttr("RemoveIntegerChecks"))
            {
            }

            if (GetBoolAttr("TreatWarningsAsErrors"))
            {
                options.TreatWarningsAsErrors = true;
            }

            if (GetProjectProperty("WarningLevel", false) != null)
            {
                try
                {
                    options.WarningLevel = Int32.Parse(GetProjectProperty("WarningLevel", false), CultureInfo.InvariantCulture);
                }
                catch (ArgumentNullException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (ArgumentException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (FormatException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
                catch (OverflowException e)
                {
                    Trace.WriteLine("Exception : " + e.Message);
                }
            }

            return options;
        }

        internal virtual void SetTargetPlatform(ProjectOptions options)
        {
        }

        /// <summary>
        /// Get the assembly name for a give configuration
        /// </summary>
        /// <param name="config">the matching configuration in the msbuild file</param>
        /// <returns>assembly name</returns>
        internal virtual string GetAssemblyName(ConfigCanonicalName config)
        {
            this.SetConfiguration(config);
            return GetAssemblyName();
        }

        /// <summary>
        /// Create a file node based on an msbuild item.
        /// </summary>
        /// <param name="item">msbuild item</param>
        /// <returns>FileNode added</returns>
        internal virtual LinkedFileNode CreateFileNode(ProjectElement item, uint? newItemId = null)
        {
            return new LinkedFileNode(this, item, newItemId);
        }

        /// <summary>
        /// Create a file node based on a string.
        /// </summary>
        /// <param name="file">filename of the new filenode</param>
        /// <returns>File node added</returns>
        internal virtual LinkedFileNode CreateFileNode(string file, uint? newItemId = null)
        {
            ProjectElement item = this.AddFileToMsBuild(file);
            return this.CreateFileNode(item, newItemId);
        }

        /// <summary>
        /// Return an absolute path that is normalized (e.g. no ".." portions)
        /// </summary>
        /// <param name="includePath">Original path.  Can be relative (in which case relative to ProjectFolder) or absolute.</param>
        /// <returns></returns>
        public string NormalizePath(string includePath)
        {
            if (!Path.IsPathRooted(includePath))
            {
                includePath = Path.Combine(this.ProjectFolder, includePath);
            }
            // now path is absolute

            Uri u = new Uri(includePath);  // normalize away the ".." parts
            includePath = u.LocalPath;
            // now path is normalized

            return includePath;
        }

        public bool IsContainedWithinProjectDirectory(string includePath)
        {
            includePath = NormalizePath(includePath);
            return String.Compare(this.ProjectFolder, 0, includePath, 0, this.ProjectFolder.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Walks the subpaths of a project relative path and checks if the folder nodes hierarchy is already there, if not creates it.
        /// </summary>
        /// <param name="path">Path of the folder, can be relative to project or absolute</param>
        public virtual HierarchyNode CreateFolderNodes(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            if (Path.IsPathRooted(path))
            {
                // Ensure we are using a relative path
                if (String.Compare(this.ProjectFolder, 0, path, 0, this.ProjectFolder.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    // TODO you get here if you have
                    //     <Folder Include="D:\Depot\staging\users\<{username}\Application19\Library1\" />
                    // and the folder is not 'under the project' - need a better error message here, as displays in VS dialog and project does not open
                    throw new ArgumentException();

                path = path.Substring(this.ProjectFolder.Length);
            }

            string[] parts;
            HierarchyNode curParent;

            parts = path.Split(Path.DirectorySeparatorChar);
            path = String.Empty;
            curParent = this;

            // now we have an array of subparts....
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    path += parts[i] + "\\";
                    curParent = VerifySubFolderExists(path, curParent);
                }
            }
            return curParent;
        }

        /// <summary>
        /// Defines if Node has Designer. By default we do not support designers for nodes
        /// </summary>
        /// <param name="itemPath">Path to item to query for designer support</param>
        /// <returns>true if node has designer</returns>
        public virtual bool NodeHasDesigner(string itemPath)
        {
            return false;
        }

        /// <summary>
		/// List of Guids of the config independent property pages. It is called by the GetProperty for VSHPROPID_PropertyPagesCLSIDList property.
		/// </summary>
		/// <returns></returns>
		public virtual Guid[] GetConfigurationIndependentPropertyPages()
		{
			return new Guid[] { Guid.Empty };
		}

        /// <summary>
        /// Returns a list of Guids of the configuration dependent property pages. It is called by the GetProperty for VSHPROPID_CfgPropertyPagesCLSIDList property.
        /// </summary>
        /// <returns></returns>
        public virtual Guid[] GetConfigurationDependentPropertyPages()
        {
            return new Guid[0];
        }

        /// <summary>
        /// An ordered list of guids of the prefered property pages. See <see cref="__VSHPROPID.VSHPROPID_PriorityPropertyPagesCLSIDList"/>
        /// </summary>
        /// <returns>An array of guids.</returns>
        public virtual Guid[] GetPriorityProjectDesignerPages()
        {
            return new Guid[] { Guid.Empty };
        }

        /// <summary>
        /// Takes a path and verifies that we have a node with that name.
        /// It is meant to be a helper method for CreateFolderNodes().
        /// For some scenario it may be useful to override.
        /// </summary>
        /// <param name="path">full path to the subfolder we want to verify.</param>
        /// <param name="parent">the parent node where to add the subfolder if it does not exist.</param>
        /// <returns>the foldernode correcsponding to the path.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "SubFolder")]
        public virtual FolderNode VerifySubFolderExists(string path, HierarchyNode parent)
        {
            FolderNode folderNode = null;
            uint uiItemId;
            Url url = new Url(this.BaseURI, path);
            string strFullPath = url.AbsoluteUrl;
            var hr = this.ParseCanonicalName(strFullPath, out uiItemId);
            if (ErrorHandler.Succeeded(hr))
            {
                Debug.Assert(this.NodeFromItemId(uiItemId) is FolderNode, "Not a FolderNode");
                folderNode = (FolderNode)this.NodeFromItemId(uiItemId);
            }

            if (folderNode == null)
            {
                // folder does not exist yet...
                // We could be in the process of loading so see if msbuild knows about it
                ProjectElement item = null;
                foreach (Microsoft.Build.Evaluation.ProjectItem folder in MSBuildProject.GetItems(buildProject, ProjectFileConstants.Folder))
                {
                    if (String.Compare(MSBuildItem.GetEvaluatedInclude(folder).TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        item = new ProjectElement(this, folder, false);
                        break;
                    }
                }
                // If MSBuild did not know about it, create a virtual one
                if (item == null)
                    item = new ProjectElement(this, null, true);
                folderNode = this.CreateFolderNode(path, item);
                parent.AddChild(folderNode);
            }

            return folderNode;
        }

        /// <summary>
        /// To support virtual folders, override this method to return your own folder nodes
        /// </summary>
        /// <param name="path">Path to store for this folder</param>
        /// <param name="element">Element corresponding to the folder</param>
        /// <returns>A FolderNode that can then be added to the hierarchy</returns>
        internal virtual FolderNode CreateFolderNode(string path, ProjectElement element)
        {
            FolderNode folderNode = new FolderNode(this, path, element);
            return folderNode;
        }

        /// <summary>
        /// Gets the list of selected HierarchyNode objects
        /// </summary>
        /// <returns>A list of HierarchyNode objects</returns>
        public virtual IList<HierarchyNode> GetSelectedNodes()
        {
            // Retrieve shell interface in order to get current selection
            IVsMonitorSelection monitorSelection = this.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
            Debug.Assert(monitorSelection != null, "Could not get the IVsMonitorSelection object from the services exposed by this project");
            if (monitorSelection == null)
            {
                throw new InvalidOperationException();
            }

            List<HierarchyNode> selectedNodes = new List<HierarchyNode>();
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainer = IntPtr.Zero;
            try
            {
                // Get the current project hierarchy, project item, and selection container for the current selection
                // If the selection spans multiple hierachies, hierarchyPtr is Zero
                uint itemid;
                IVsMultiItemSelect multiItemSelect = null;
                ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainer));

                // We only care if there are one ore more nodes selected in the tree
                if (itemid != VSConstants.VSITEMID_NIL && hierarchyPtr != IntPtr.Zero)
                {
                    IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;

                    if (itemid != VSConstants.VSITEMID_SELECTION)
                    {
                        // This is a single selection. Compare hirarchy with our hierarchy and get node from itemid
                        if (Utilities.IsSameComObject(this, hierarchy))
                        {
                            HierarchyNode node = this.NodeFromItemId(itemid);
                            if (node != null)
                            {
                                selectedNodes.Add(node);
                            }
                        }
                    }
                    else if (multiItemSelect != null)
                    {
                        // This is a multiple item selection.

                        //Get number of items selected and also determine if the items are located in more than one hierarchy
                        uint numberOfSelectedItems;
                        int isSingleHierarchyInt;
                        ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt));
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        // Now loop all selected items and add to the list only those that are selected within this hierarchy
                        if (!isSingleHierarchy || (isSingleHierarchy && Utilities.IsSameComObject(this, hierarchy)))
                        {
                            Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
                            VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            uint flags = (isSingleHierarchy) ? (uint)__VSGSIFLAGS.GSI_fOmitHierPtrs : 0;
                            ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(flags, numberOfSelectedItems, vsItemSelections));
                            foreach (VSITEMSELECTION vsItemSelection in vsItemSelections)
                            {
                                if (isSingleHierarchy || Utilities.IsSameComObject(this, vsItemSelection.pHier))
                                {
                                    HierarchyNode node = this.NodeFromItemId(vsItemSelection.itemid);
                                    if (node != null)
                                    {
                                        selectedNodes.Add(node);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
                if (selectionContainer != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainer);
                }
            }

            return selectedNodes;
        }

        /// <summary>
        /// Recursevily walks the hierarchy nodes and redraws the state icons
        /// </summary>
        public override void UpdateSccStateIcons()
        {
            if (this.FirstChild == null)
            {
                return;
            }

            for (HierarchyNode n = this.FirstChild; n != null; n = n.NextSibling)
            {
                n.UpdateSccStateIcons();
            }
        }


        /// <summary>
        /// Handles the shows all objects command.
        /// </summary>
        /// <returns></returns>
        public virtual int ShowAllFiles()
        {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Handles the Add web reference command.
        /// </summary>
        /// <returns></returns>
        public virtual int AddWebReference()
        {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Unloads the project.
        /// </summary>
        /// <returns></returns>
        public virtual int UnloadProject()
        {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Handles the clean project command.
        /// </summary>
        /// <returns></returns>
        public virtual int CleanProject()
        {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Reload project from project file
        /// </summary>
        public virtual void Reload()
        {
            Debug.Assert(this.buildEngine != null, "There is no build engine defined for this project");

            try
            {
                this.disableQueryEdit = true;

                this.isClosed = false;
                this.eventTriggeringFlag = ProjectNode.EventTriggering.DoNotTriggerHierarchyEvents | ProjectNode.EventTriggering.DoNotTriggerTrackerEvents;

                SetBuildProject(Utilities.ReinitializeMsBuildProject(this.buildEngine, this.filename, this.ProjectGlobalPropertiesThatAllProjectSystemsMustSet, this.buildProject));
                
                // Load the guid
                this.SetProjectGuidFromProjectFile(true);

                this.ProcessReferences();

                this.ProcessCustomBuildActions();

                this.ProcessFilesAndFolders();
                
                this.LoadNonBuildInformation();

                this.InitSccInfo();

                this.RegisterSccProject();
            }
            finally
            {
                this.SetProjectFileDirty(false);
                this.eventTriggeringFlag = ProjectNode.EventTriggering.TriggerAll;
                this.disableQueryEdit = false;
            }
        }

        /// <summary>
        /// Renames the project file
        /// </summary>
        /// <param name="newFile">The full path of the new project file.</param>
        public virtual void RenameProjectFile(string newFile)
        {
            IVsUIShell shell = this.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
            Debug.Assert(shell != null, "Could not get the ui shell from the project");
            if (shell == null)
            {
                throw new InvalidOperationException();
            }

            // Do some name validation
            if (Utilities.ContainsInvalidFileNameChars(newFile))
            {
                throw new InvalidOperationException(SR.GetString(SR.ErrorInvalidProjectName, CultureInfo.CurrentUICulture));
            }

            // Figure out what the new full name is
            string oldFile = this.Url;

            int canContinue = 0;
            IVsSolution vsSolution = (IVsSolution)this.GetService(typeof(SVsSolution));
            if (ErrorHandler.Succeeded(vsSolution.QueryRenameProject(InteropSafeIVsProject, oldFile, newFile, 0, out canContinue))
                && canContinue != 0)
            {
                bool isFileSame = NativeMethods.IsSamePath(oldFile, newFile);

                // If file already exist and is not the same file with different casing
                if (!isFileSame && File.Exists(newFile))
                {
                    // Prompt the user for replace
                    string message = SR.GetString(SR.FileAlreadyExists, newFile);

                    if (!Utilities.IsInAutomationFunction(this.Site))
                    {
                        if (!VsShellUtilities.PromptYesNo(message, null, OLEMSGICON.OLEMSGICON_WARNING, shell))
                        {
                            throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(message);
                    }

                    // Delete the destination file after making sure it is not read only
                    File.SetAttributes(newFile, FileAttributes.Normal);
                    File.Delete(newFile);
                }

                SuspendFileChanges fileChanges = new SuspendFileChanges(this.Site, this.filename);
                fileChanges.Suspend();
                try
                {
                    // Actual file rename
                    this.SaveMSBuildProjectFileAs(newFile);

                    this.SetProjectFileDirty(false);

                    if (!isFileSame)
                    {
                        // Now that the new file name has been created delete the old one.
                        // TODO: Handle source control issues.
                        File.SetAttributes(oldFile, FileAttributes.Normal);
                        File.Delete(oldFile);
                    }

                    this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);

                    // Update solution
                    ErrorHandler.ThrowOnFailure(vsSolution.OnAfterRenameProject((IVsProject)this, oldFile, newFile, 0));

                    shell.RefreshPropertyBrowser(0);
                }
                finally
                {
                    fileChanges.Resume();
                }
            }
            else
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }
        }

        /// <summary>
        /// Filter items that should not be processed as file items. Example: Folders and References.
        /// </summary>
        public virtual bool FilterItemTypeToBeAddedToHierarchy(string itemType)
        {
            return (String.Compare(itemType, ProjectFileConstants.Reference, StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(itemType, ProjectFileConstants.ProjectReference, StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(itemType, ProjectFileConstants.COMReference, StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(itemType, ProjectFileConstants.Folder, StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(itemType, ProjectFileConstants.WebReference, StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(itemType, ProjectFileConstants.WebReferenceFolder, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// Associate window output pane to the build logger
        /// </summary>
        /// <param name="output"></param>
        public virtual void SetOutputLogger(IVsOutputWindowPane output)
        {
            var errorReporter = this.GetBuildErrorReporter();

            // Create our logger, if it was not specified
            if (buildLogger == null)
            {
                // Because we may be aggregated, we need to make sure to get the outer IVsHierarchy
                // Create the logger
                buildLogger = new IDEBuildLogger(output, this.InteropSafeIVsHierarchy, errorReporter);

                // To retrive the verbosity level, the build logger depends on the registry root 
                // (otherwise it will used an hardcoded default)
                ILocalRegistry2 registry = this.GetService(typeof(SLocalRegistry)) as ILocalRegistry2;
                if (null != registry)
                {
                    string registryRoot;
                    registry.GetLocalRegistryRoot(out registryRoot);
                    if (!String.IsNullOrEmpty(registryRoot) && (buildLogger != null))
                    {
                        buildLogger.BuildVerbosityRegistryRoot = registryRoot;
                        buildLogger.ErrorString = this.ErrorString;
                        buildLogger.WarningString = this.WarningString;
                    }
                }
            }
            else
            {
                buildLogger.OutputWindowPane = output;
                buildLogger.ErrorReporter = errorReporter;
            }
        }

        public virtual void SetBuildProject(Microsoft.Build.Evaluation.Project newBuildProject)
        {
            this.buildProject = newBuildProject;
            if (newBuildProject != null)
            {
                SetupProjectGlobalPropertiesThatAllProjectSystemsMustSet();
            }
            //this.projectInstance = null;
        }

        /// <summary>
        /// Set configuration properties for a specific configuration
        /// </summary>
        /// <param name="config">configuration name</param>
        internal virtual void SetBuildConfigurationProperties(ConfigCanonicalName config)
        {
            ProjectOptions options = null;

            options = this.GetProjectOptions(config);


            if (options != null && this.buildProject != null)
            {
                // Make sure the project configuration is set properly
                this.SetConfiguration(config);
            }
        }

        public abstract void ComputeSourcesAndFlags();

        public abstract IVsLanguageServiceBuildErrorReporter2 GetBuildErrorReporter();

        internal abstract int FixupAppConfigOnTargetFXChange(string newTargetFramework, string targetFSharpCoreVersion, bool autoGenerateBindingRedirects);

        /// <summary>
        /// This execute an MSBuild target.
        /// If you depend on the items/properties generated by the target
        /// you should be aware that any call to BuildTarget on any project
        /// will reset the list of generated items/properties
        /// </summary>
        /// <param name="target">Name of the MSBuild target to execute</param>
        /// <returns>Result from executing the target (success/failure)</returns>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
        internal virtual BuildResult InvokeMsBuild(string target, IEnumerable<KeyValuePair<string, string>> extraProperties = null)
        {
            UIThread.MustBeCalledFromUIThread();
            ProjectInstance projectInstance = null;

            BuildSubmission submission = DoMSBuildSubmission(BuildKind.SYNC, target, ref projectInstance, null, extraProperties);
            
            if (submission != null)
            {
                MSBuildResult result = (submission.BuildResult.OverallResult == BuildResultCode.Success) ? MSBuildResult.Successful : MSBuildResult.Failed;
                return new BuildResult(result, projectInstance);
            }
            else
            {
                return new BuildResult(MSBuildResult.Failed, null);
            }
        }

        internal IDictionary<string, string> ProjectGlobalPropertiesThatAllProjectSystemsMustSet { get; set; }

        void SetupProjectGlobalPropertiesThatAllProjectSystemsMustSet()
        {
            // Much of the code for this method is stolen from GlobalPropertyHandler.cs.  That file is dev-9 only;
            // whereas this code is for dev10 and specific to the actual contract for project systems in dev10.
            UIThread.MustBeCalledFromUIThread();

            // Solution properties
            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            Debug.Assert(solution != null, "Could not retrieve the solution service from the global service provider");

            string solutionDirectory, solutionPath, userOptionsFile;

            // We do not want to throw. If we cannot set the solution related constants we set them to empty string.
            solution.GetSolutionInfo(out solutionDirectory, out solutionPath, out userOptionsFile);

            if (solutionDirectory == null)
            {
                solutionDirectory = String.Empty;
            }


            if (solutionPath == null)
            {
                solutionPath = String.Empty;
            }


            string solutionFileName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileName(solutionPath);

            string solutionName = (solutionPath.Length == 0) ? String.Empty : Path.GetFileNameWithoutExtension(solutionPath);

            string solutionExtension = String.Empty;
            if (solutionPath.Length > 0 && Path.HasExtension(solutionPath))
            {
                solutionExtension = Path.GetExtension(solutionPath);
            }

            this.buildProject.SetGlobalProperty(GlobalProperty.SolutionDir.ToString(), solutionDirectory);
            this.buildProject.SetGlobalProperty(GlobalProperty.SolutionPath.ToString(), solutionPath);
            this.buildProject.SetGlobalProperty(GlobalProperty.SolutionFileName.ToString(), solutionFileName);
            this.buildProject.SetGlobalProperty(GlobalProperty.SolutionName.ToString(), solutionName);
            this.buildProject.SetGlobalProperty(GlobalProperty.SolutionExt.ToString(), solutionExtension);

            // Other misc properties
            this.buildProject.SetGlobalProperty(GlobalProperty.BuildingInsideVisualStudio.ToString(), "true");
            this.buildProject.SetGlobalProperty(GlobalProperty.Configuration.ToString(), "");
            this.buildProject.SetGlobalProperty(GlobalProperty.Platform.ToString(), "");

            // DevEnvDir property
            IVsShell shell = this.Site.GetService(typeof(SVsShell)) as IVsShell;
            Debug.Assert(shell != null, "Could not retrieve the IVsShell service from the global service provider");

            object installDirAsObject;

            // We do not want to throw. If we cannot set the solution related constants we set them to empty string.
            shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out installDirAsObject);

            string installDir = ((string)installDirAsObject);

            if (String.IsNullOrEmpty(installDir))
            {
                installDir = String.Empty;
            }
            else 
            {
                // Ensure that we have traimnling backslash as this is done for the langproj macros too.
                if (installDir[installDir.Length - 1] != Path.DirectorySeparatorChar)
                {
                    installDir += Path.DirectorySeparatorChar;
                }
            }

            this.buildProject.SetGlobalProperty(GlobalProperty.DevEnvDir.ToString(), installDir);

            this.ProjectGlobalPropertiesThatAllProjectSystemsMustSet = new Dictionary<string, string>()
            {
                { GlobalProperty.SolutionDir.ToString(), solutionDirectory },
                { GlobalProperty.SolutionPath.ToString(), solutionPath },
                { GlobalProperty.SolutionFileName.ToString(), solutionFileName },
                { GlobalProperty.SolutionName.ToString(), solutionName },
                { GlobalProperty.SolutionExt.ToString(), solutionExtension },
                { GlobalProperty.BuildingInsideVisualStudio.ToString(), "true" },
                { GlobalProperty.Configuration.ToString(), "" },
                { GlobalProperty.Platform.ToString(), "" },
                { GlobalProperty.DevEnvDir.ToString(), installDir }
            };
        }

        private class BuildAccessorAccess
        {
            private readonly BuildKind buildKind;
            private readonly bool uiThreadClaimed;
            private readonly bool designTimeBuildStarted;
            private readonly IVsBuildManagerAccessor accessor;
            public BuildAccessorAccess(BuildKind buildKind, IVsBuildManagerAccessor accessor) 
            {
                this.buildKind = buildKind;
                this.accessor = accessor;
                if (buildKind == BuildKind.SYNC)
                {
                    var claimResult = accessor.ClaimUIThreadForBuild();
                    if (ErrorHandler.Failed(claimResult)) return;
                    this.uiThreadClaimed = true;
                    var designTimeStartResult = accessor.BeginDesignTimeBuild();
                    if (ErrorHandler.Failed(designTimeStartResult)) return;
                    this.designTimeBuildStarted = true;
                }
            }

            public bool IsOk { get { return this.buildKind == BuildKind.ASYNC || this.uiThreadClaimed && this.designTimeBuildStarted; } }
            public IVsBuildManagerAccessor Accessor { get { return this.accessor;  } }

            public void Dispose()
            {
                if (this.designTimeBuildStarted)
                {
                    accessor.EndDesignTimeBuild();
                }
                if (this.uiThreadClaimed)
                {
                    accessor.ReleaseUIThreadForBuild();
                }
            }
        }

        /// <summary>
        /// Start MSBuild build submission
        /// </summary>
        /// If buildKind is ASYNC, this method starts the submission ane returns. uiThreadCallback will be called on UI thread once submissions completes.
        /// if buildKind is SYNC, this method executes the submission and runs uiThreadCallback
        /// <param name="buildKind">Is it a SYNC or ASYNC build</param>
        /// <param name="target">target to build</param>
        /// <param name="projectInstance">project instance to build; if null, this.BuildProject.CreateProjectInstance() is used to populate</param>
        /// <param name="uiThreadCallback">callback to be run UI thread </param>
        /// <returns></returns>
        internal virtual BuildSubmission DoMSBuildSubmission(BuildKind buildKind, string target, ref ProjectInstance projectInstance, MSBuildCoda uiThreadCallback, IEnumerable<KeyValuePair<string, string>> extraProperties = null)
        {
            UIThread.MustBeCalledFromUIThread();

            IVsBuildManagerAccessor accessor = null;
            Microsoft.Build.Framework.ILogger[] loggers;
            BuildSubmission submission;
            BuildAccessorAccess buildAccessorAccess = null;
            try
            {
                accessor = (IVsBuildManagerAccessor)this.Site.GetService(typeof(SVsBuildManagerAccessor));

                this.SetHostObject("CoreCompile", "Fsc", this);

                // Do the actual Build
                var loggerList = new System.Collections.Generic.List<Microsoft.Build.Framework.ILogger>();
                if (buildLogger != null) loggerList.Add(buildLogger);
                if (myDebugLogger != null) loggerList.Add(myDebugLogger);
                loggers = loggerList.ToArray();

                var ba = new BuildAccessorAccess(buildKind, accessor);
                if (!ba.IsOk)
                {
                    ba.Dispose();
                    if (uiThreadCallback != null) uiThreadCallback(MSBuildResult.Failed, projectInstance);
                    return null;
                }
                buildAccessorAccess = ba;

                string[] targetsToBuild = new string[target != null ? 1 : 0];
                if (target != null)
                {
                    targetsToBuild[0] = target;
                }

                if (projectInstance == null)
                {
                    projectInstance = BuildProject.CreateProjectInstance();
                }
                // F#-specific properties
                projectInstance.SetProperty(GlobalProperty.VisualStudioStyleErrors.ToString(), "true");
                
                if (extraProperties != null)
                {
                    foreach (var prop in extraProperties)
                    {
                        projectInstance.SetProperty(prop.Key, prop.Value);
                    }
                }
                
                projectInstance.SetProperty("UTFOutput", "true");

                // The CoreCLR build of FSC will use the CultureName since lcid doesn't apply very well
                // so that the errors reported by fsc.exe are in the right locale
                projectInstance.SetProperty("PREFERREDUILANG", System.Threading.Thread.CurrentThread.CurrentUICulture.Name);
                this.BuildProject.ProjectCollection.HostServices.SetNodeAffinity(projectInstance.FullPath, NodeAffinity.InProc);
                BuildRequestData requestData = new BuildRequestData(projectInstance, targetsToBuild, this.BuildProject.ProjectCollection.HostServices, BuildRequestDataFlags.ReplaceExistingProjectInstance);
                submission = BuildManager.DefaultBuildManager.PendBuildRequest(requestData);
            }
            catch (Exception)
            {
                if (buildAccessorAccess != null)
                {
                    buildAccessorAccess.Dispose();
                }
                throw;
            }
            try
            {
                foreach (var logger in loggers)
                {
                    accessor.RegisterLogger(submission.SubmissionId, logger);
                }

                ProjectInstance tempProjectInstance = projectInstance;
                if (buildKind == BuildKind.ASYNC)
                {
                    submission.ExecuteAsync(sub =>
                    {
                        UIThread.Run(() =>
                        {
                            FinishSubmission(sub, buildAccessorAccess);
                            uiThreadCallback((sub.BuildResult.OverallResult == BuildResultCode.Success) ? MSBuildResult.Successful : MSBuildResult.Failed, tempProjectInstance);
                        });
                    }, null);
                }
                else
                {
                    submission.Execute();

                }
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
                // our callback may not have been registered, but must always do coda
                FinishSubmission(submission, buildAccessorAccess);
                if (uiThreadCallback != null)
                    uiThreadCallback(MSBuildResult.Failed, projectInstance);
                throw;
            }

            if (buildKind == BuildKind.SYNC)
            {
                FinishSubmission(submission, buildAccessorAccess);
                var msbuildResult = (submission.BuildResult.OverallResult == BuildResultCode.Success) ? MSBuildResult.Successful : MSBuildResult.Failed;
                if (uiThreadCallback != null) uiThreadCallback(msbuildResult, projectInstance);
            }
            return submission;
        }

        private void FinishSubmission(BuildSubmission submission, BuildAccessorAccess buildAccessorAccess)
        {
            UIThread.MustBeCalledFromUIThread();
            Debug.Assert(submission.IsCompleted, "MSBuild submission was not completed");
            try
            {
                buildAccessorAccess.Accessor.UnregisterLoggers(submission.SubmissionId);
            }
            finally
            {
                buildAccessorAccess.Dispose();
            }
        }

        /// <summary>
        /// Initialize common project properties with default value if they are empty
        /// </summary>
        /// <remarks>The following common project properties are defaulted to projectName (if empty):
        ///    AssemblyName, Name and RootNamespace.
        /// If the project filename is not set then no properties are set</remarks>
        public virtual void InitializeProjectProperties()
        {
            // Get projectName from project filename. Return if not set
            string projectName = Path.GetFileNameWithoutExtension(this.filename);
            if (String.IsNullOrEmpty(projectName))
            {
                return;
            }

            if (String.IsNullOrEmpty(GetProjectProperty(ProjectFileConstants.AssemblyName)))
            {
                SetProjectProperty(ProjectFileConstants.AssemblyName, projectName);
            }
            if (String.IsNullOrEmpty(GetProjectProperty(ProjectFileConstants.Name)))
            {
                SetProjectProperty(ProjectFileConstants.Name, projectName);
            }
            if (String.IsNullOrEmpty(GetProjectProperty(ProjectFileConstants.RootNamespace)))
            {
                SetProjectProperty(ProjectFileConstants.RootNamespace, projectName);
            }
        }

        /// <summary>
        /// Factory method for configuration provider
        /// </summary>
        /// <returns>Configuration provider created</returns>
        public virtual ConfigProvider CreateConfigProvider()
        {
            return new ConfigProvider(this);
        }

        /// <summary>
        /// Factory method for reference container node
        /// </summary>
        /// <returns>ReferenceContainerNode created</returns>
        public virtual ReferenceContainerNode CreateReferenceContainerNode()
        {
            return new ReferenceContainerNode(this);
        }

        /// <summary>
        /// Saves the project file on a new name.
        /// </summary>
        /// <param name="newFileName">The new name of the project file.</param>
        /// <param name="saveCopyAs">if <c>true</c> - then SaveAs operation is performed on the copy of of current project file</param>
        /// <returns>Success value or an error code.</returns>
        public virtual int SaveAs(string newFileName, bool saveCopyAs)
        {
            Debug.Assert(!String.IsNullOrEmpty(newFileName), "Cannot save project file for an empty or null file name");

            newFileName = newFileName.Trim();

            string errorMessage = String.Empty;

            if (newFileName.Length > NativeMethods.MAX_PATH)
            {
                errorMessage = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.PathTooLong, CultureInfo.CurrentUICulture), newFileName);
            }
            else
            {
                string fileName = String.Empty;

                try
                {
                    fileName = Path.GetFileNameWithoutExtension(newFileName);
                }
                // We want to be consistent in the error message and exception we throw. fileName could be for example #?&%"?&"%  and that would trigger an ArgumentException on Path.IsRooted.
                catch (ArgumentException)
                {
                    errorMessage = SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture);
                }

                if (errorMessage.Length == 0)
                {
                    // If there is no filename or it starts with a leading dot issue an error message and quit.
                    // For some reason the save as dialog box allows to save files like "......ext"
                    if (String.IsNullOrEmpty(fileName) || fileName[0] == '.')
                    {
                        errorMessage = SR.GetString(SR.FileNameCannotContainALeadingPeriod, CultureInfo.CurrentUICulture);
                    }
                    else if (Utilities.ContainsInvalidFileNameChars(newFileName))
                    {
                        errorMessage = SR.GetString(SR.ErrorInvalidFileName, CultureInfo.CurrentUICulture);
                    }
                    else if (!saveCopyAs)
                    {   
                        // verify folder only when we save project file itself, not its copy
                        string url = Path.GetDirectoryName(newFileName);
                        string oldUrl = Path.GetDirectoryName(this.Url);

                        if (!NativeMethods.IsSamePath(oldUrl, url))
                        {
                            errorMessage = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.SaveOfProjectFileOutsideCurrentDirectory, CultureInfo.CurrentUICulture), this.ProjectFolder);
                        }
                    }
                }
            }
            if (errorMessage.Length > 0)
            {
                // If it is not called from an automation method show a dialog box.
                if (!Utilities.IsInAutomationFunction(this.Site))
                {
                    string title = null;
                    OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                    OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                    OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                    VsShellUtilities.ShowMessageBox(this.Site, title, errorMessage, icon, buttons, defaultButton);
                    return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                }

                throw new InvalidOperationException(errorMessage);
            }

            string oldName = this.filename;

            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            Debug.Assert(solution != null, "Could not retrieve the solution form the service provider");
            if (solution == null)
            {
                throw new InvalidOperationException();
            }

            SuspendFileChanges fileChanges = new SuspendFileChanges(this.Site, oldName);
            fileChanges.Suspend();
            try
            {
                if (saveCopyAs)
                {
                    // preserve encoding of BuildProject.
                    // if encoding of StreamWriter differs from encoding used by BuildProject then Save will change encoding in xml processing instruction
                    var targetEncoding = 
                        this.BuildProject.Xml.Encoding
                        ;
                    using (var writer = new StreamWriter(newFileName, false, targetEncoding))
                    {
                        // Save(TextWriter) overload is used to avoid internal modification of the FullName property
                        // all we need for 'saveCopyAs' is store content of this project file to another location without extra changes
                        this.BuildProject.Save(writer);
                    }
                }
                else
                {
                    int canRenameContinue = 0;
                    ErrorHandler.ThrowOnFailure(solution.QueryRenameProject(InteropSafeIVsProject, this.filename, newFileName, 0, out canRenameContinue));

                    if (canRenameContinue == 0)
                    {
                        return VSConstants.OLE_E_PROMPTSAVECANCELLED;
                    }

                    // Save the project file and project file related properties.
                    this.SaveMSBuildProjectFileAs(newFileName);

                    this.SetProjectFileDirty(false);


                    // TODO: If source control is enabled check out the project file.

                    //Redraw.
                    this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);

                    ErrorHandler.ThrowOnFailure(solution.OnAfterRenameProject(InteropSafeIVsProject, oldName, this.filename, 0));

                    IVsUIShell shell = this.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
                    Debug.Assert(shell != null, "Could not get the ui shell from the project");
                    if (shell == null)
                    {
                        throw new InvalidOperationException();
                    }
                    shell.RefreshPropertyBrowser(0);
                }

            }
            finally
            {
                fileChanges.Resume();
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Saves project file related information to the new file name. It also calls msbuild API to save the project file.
        /// It is called by the SaveAs method and the SetEditLabel before the project file rename related events are triggered. 
        /// An implementer can override this method to provide specialized semantics on how the project file is renamed in the msbuild file.
        /// </summary>
        /// <param name="newFileName">The new full path of the project file</param>
        public virtual void SaveMSBuildProjectFileAs(string newFileName)
        {
            Debug.Assert(!String.IsNullOrEmpty(newFileName), "Cannot save project file for an empty or null file name");

            this.buildProject.FullPath = newFileName;

            this.filename = newFileName;

            string newFileNameWithoutExtension = Path.GetFileNameWithoutExtension(newFileName);

            // Refresh solution explorer
            this.SetProjectProperty(ProjectFileConstants.Name, newFileNameWithoutExtension);

            // Saves the project file on disk.
            this.buildProject.Save(newFileName);

        }

        public virtual string DefaultBuildAction(string itemPath)
        {
            return null;
        }

        /// <summary>
        /// Adds a file to the msbuild project.
        /// </summary>
        /// <param name="file">The file to be added.</param>
        /// <returns>A Projectelement describing the newly added file.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "ToMs")]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
        internal virtual ProjectElement AddFileToMsBuild(string file)
        {
            ProjectElement newItem;

            string itemPath = PackageUtilities.MakeRelative(this.BaseURI.AbsoluteUrl, file);
            Debug.Assert(!Path.IsPathRooted(itemPath), "Cannot add item with full path.");

            string defaultBuildAction = this.DefaultBuildAction(itemPath);
            Debug.Assert(!string.IsNullOrEmpty(defaultBuildAction), "ProjectNode.DefaultBuildAction() must be overridden");
            newItem = this.CreateMsBuildFileItem(itemPath, defaultBuildAction);

            return newItem;
        }

        /// <summary>
        /// Adds a folder to the msbuild project.
        /// </summary>
        /// <param name="folder">The folder to be added.</param>
        /// <returns>A Projectelement describing the newly added folder.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "ToMs")]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
        internal virtual ProjectElement AddFolderToMsBuild(string folder)
        {
            ProjectElement newItem;

            string itemPath = PackageUtilities.MakeRelativeIfRooted(folder, this.BaseURI);
            Debug.Assert(!Path.IsPathRooted(itemPath), "Cannot add item with full path.");

            newItem = this.CreateMsBuildFileItem(itemPath, ProjectFileConstants.Folder);

            return newItem;
        }

        /// <summary>
        /// Determines whether an item can be owerwritten in the hierarchy.
        /// </summary>
        /// <param name="originalFileName">The orginal filname.</param>
        /// <param name="computedNewFileName">The computed new file name, that will be copied to the project directory or into the folder .</param>
        /// <returns>S_OK for success, or an error message</returns>
        public virtual int CanOverwriteExistingItem(string originalFileName, string computedNewFileName)
        {
            if (String.IsNullOrEmpty(originalFileName) || String.IsNullOrEmpty(computedNewFileName))
            {
                return VSConstants.E_INVALIDARG;
            }

            string message = String.Empty;
            string title = String.Empty;
            OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
            OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
            OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;

            // If the document is open then return error message.
            IVsUIHierarchy hier;
            IVsWindowFrame windowFrame;
            uint itemid = VSConstants.VSITEMID_NIL;

            bool isOpen = VsShellUtilities.IsDocumentOpen(this.Site, computedNewFileName, Guid.Empty, out hier, out itemid, out windowFrame);

            if (isOpen)
            {
                message = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.CannotAddFileThatIsOpenInEditor, CultureInfo.CurrentUICulture), Path.GetFileName(computedNewFileName));
                VsShellUtilities.ShowMessageBox(this.Site, title, message, icon, buttons, defaultButton);
                return VSConstants.E_ABORT;
            }


            // File already exists in project... message box
            message = SR.GetString(SR.FileAlreadyInProject, CultureInfo.CurrentUICulture);
            icon = OLEMSGICON.OLEMSGICON_QUERY;
            buttons = OLEMSGBUTTON.OLEMSGBUTTON_YESNO;
            int msgboxResult = VsShellUtilities.ShowMessageBox(this.Site, title, message, icon, buttons, defaultButton);
            if (msgboxResult != NativeMethods.IDYES)
            {
                return (int)OleConstants.OLECMDERR_E_CANCELED;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Handle owerwriting of an existing item in the hierarchy.
        /// </summary>
        /// <param name="existingNode">The node that exists.</param>
        public virtual void OverwriteExistingItem(HierarchyNode existingNode)
        {

        }

        /// <summary>
        /// Adds a new file node to the hierarchy.
        /// </summary>
        /// <param name="parentNode">The parent of the new fileNode</param>
        /// <param name="fileName">The file name</param>
        public virtual HierarchyNode AddNewFileNodeToHierarchy(HierarchyNode parentNode, string fileName)
        {
            var ret = AddNewFileNodeToHierarchyCore(parentNode, fileName);
            FireAddNodeEvent(fileName);
            return ret;
        }

        private HierarchyNode AddNewFileNodeToHierarchyCore(HierarchyNode parentNode, string fileName, uint? newItemId = null)
        {
            //Create and add new filenode to the project
            HierarchyNode child = this.CreateFileNode(fileName, newItemId);

            parentNode.AddChild(child);

            return child;
        }

        private void FireAddNodeEvent(string fileName)
        {
            // TODO : Revisit the VSADDFILEFLAGS here. Can it be a nested project?
            this.tracker.OnItemAdded(fileName, VSADDFILEFLAGS.VSADDFILEFLAGS_NoFlags);
        }

        /// <summary>
        /// Defines whther the current mode of the project is in a supress command mode.
        /// </summary>
        /// <returns></returns>
        public virtual bool IsCurrentStateASuppressCommandsMode()
        {
            if (VsShellUtilities.IsSolutionBuilding(this.Site))
            {
                return true;
            }

            DBGMODE dbgMode = VsShellUtilities.GetDebugMode(this.Site) & ~DBGMODE.DBGMODE_EncMask;
            if (dbgMode == DBGMODE.DBGMODE_Run || dbgMode == DBGMODE.DBGMODE_Break)
            {
                return true;
            }

            return false;

        }


        /// <summary>
        /// This is the list of output groups that the configuration object should
        /// provide.
        /// The first string is the name of the group.
        /// The second string is the target name (MSBuild) for that group.
        /// 
        /// To add/remove OutputGroups, simply override this method and edit the list.
        /// 
        /// To get nice display names and description for your groups, override:
        ///        - GetOutputGroupDisplayName
        ///        - GetOutputGroupDescription
        /// </summary>
        /// <returns>List of output group name and corresponding MSBuild target</returns>
        public virtual IList<KeyValuePair<string, string>> GetOutputGroupNames()
        {
            return new List<KeyValuePair<string, string>>(outputGroupNames);
        }

        /// <summary>
        /// Get the display name of the given output group.
        /// </summary>
        /// <param name="canonicalName">Canonical name of the output group</param>
        /// <returns>Display name</returns>
        public virtual string GetOutputGroupDisplayName(string canonicalName)
        {
            string result = SR.GetString(String.Format(CultureInfo.InvariantCulture, "Output{0}", canonicalName), CultureInfo.CurrentUICulture);
            if (String.IsNullOrEmpty(result))
                result = canonicalName;
            return result;
        }

        /// <summary>
        /// Get the description of the given output group.
        /// </summary>
        /// <param name="canonicalName">Canonical name of the output group</param>
        /// <returns>Description</returns>
        public virtual string GetOutputGroupDescription(string canonicalName)
        {
            string result = SR.GetString(String.Format(CultureInfo.InvariantCulture, "Output{0}Description", canonicalName), CultureInfo.CurrentUICulture);
            if (String.IsNullOrEmpty(result))
                result = canonicalName;
            return result;
        }

        /// <summary>
        /// Set the configuration in MSBuild.
        /// This does not get persisted and is used to evaluate msbuild conditions
        /// which are based on the $(Configuration) property.
        /// </summary>
        public virtual void SetCurrentConfiguration()
        {
            if ((this.GetService(typeof(SVsBuildManagerAccessor)) as IVsBuildManagerAccessor).IsInProgress())
            {
                // we are building so this should already be the current configuration
                return;
            }

            // Can't ask for the active config until the project is opened, so do nothing in that scenario
            if (!this.projectOpened)
                return;

            TellMSBuildCurrentSolutionConfiguration();
        }

        /// <summary>
        /// Set the configuration property in MSBuild.
        /// This does not get persisted and is used to evaluate msbuild conditions
        /// which are based on the $(Configuration) property.
        /// </summary>
        /// <param name="configCanonicalName">Configuration name</param>
        internal virtual void SetConfiguration(ConfigCanonicalName configCanonicalName)
        {
            if (string.IsNullOrEmpty(configCanonicalName.ConfigName) || string.IsNullOrEmpty(configCanonicalName.MSBuildPlatform))
                return;  // see bugs 388784\577933

            // We cannot change properties during the build so if the config
            // we want to se is the current, we do nothing otherwise we fail.
            if ((this.GetService(typeof(SVsBuildManagerAccessor)) as IVsBuildManagerAccessor).IsInProgress())
            {
                if (this.projectOpened)
                {
                    // if the project is opened, the call may still succeed if the config is already set
                    // REVIEW/TODO: shall we abandon accessing automation here and just look at MSBuild state?
                    EnvDTE.Project automationObject = this.GetAutomationObject() as EnvDTE.Project;
                    ConfigCanonicalName currentConfigName;
                    if (Utilities.TryGetActiveConfigurationAndPlatform(this.Site, this.ProjectIDGuid, out currentConfigName))
                    {
                        if (currentConfigName == configCanonicalName) return;
                    }
                }
                throw new InvalidOperationException(); 
            }

            MSBuildProject.SetGlobalProperty(this.buildProject, ProjectFileConstants.Configuration, configCanonicalName.ConfigName);
            MSBuildProject.SetGlobalProperty(this.buildProject, ProjectFileConstants.Platform, configCanonicalName.MSBuildPlatform);
            this.UpdateMSBuildState();
        }


        internal void UpdateMSBuildState()
        {
            this.buildProject.ReevaluateIfNecessary();
        }

        /// <summary>
        /// Loads reference items from the project file into the hierarchy.
        /// </summary>
        public virtual void ProcessReferences()
        {
            IReferenceContainer container = GetReferenceContainer();
            if (null == container)
            {
                // Process References
                ReferenceContainerNode referencesFolder = CreateReferenceContainerNode();
                if (null == referencesFolder)
                {
                    // This project type does not support references or there is a problem
                    // creating the reference container node.
                    // In both cases there is no point to try to process references, so exit.
                    return;
                }
                this.AddChild(referencesFolder);
                container = referencesFolder;
            }

            // Load the referernces.
            container.LoadReferencesFromBuildProject(buildProject);
        }
        /// <summary>
        /// Loads build actions for a project
        /// </summary>
        public virtual void ProcessCustomBuildActions()
        {
            // The buildActionConverter has a set of default actions that are always there:
            this.buildActionConverter.ResetBuildActionsToDefaults();
            // To that list, we need to add:
            // - any <AvailableItemName Include="CustomAction">s that the user added to the project:
            foreach (var item in MSBuildProject.GetItems(this.buildProject, ProjectFileConstants.AvailableItemName))
            {
                this.buildActionConverter.RegisterBuildAction(new BuildAction(MSBuildItem.GetEvaluatedInclude(item)));
            }
            // - any other <CustomAction Include="...">s that the user has in the project:
            foreach (var item in MSBuildProject.GetStaticAndVisibleItemsInOrder(this.buildProject))
            {
                this.buildActionConverter.RegisterBuildAction(new BuildAction(MSBuildItem.GetItemType(item)));
            }
            // Now that comprises the final list of item types that should be available.
        }

        public void CheckForWildcards()
        {
            var dict = new Dictionary<Microsoft.Build.Construction.ProjectItemElement, Microsoft.Build.Evaluation.ProjectItem>();
            foreach (var item in MSBuildProject.GetStaticAndVisibleItemsInOrder(this.buildProject))
            {
                // File with "Content" or "None" build action is not compiled, so it's safe to use wildcard
                if (item.ItemType == "Content" || item.ItemType == "None") continue;

                Microsoft.Build.Evaluation.ProjectItem previousItem;
                var key = item.Xml;
                if (dict.TryGetValue(key, out previousItem))
                {
                    throw new CannotOpenProjectsWithWildcardsException(this.filename, item.ItemType, item.UnevaluatedInclude);
                }
                dict.Add(key, item);
            }
        }

        /// <summary>
        /// Loads file items from the project file into the hierarchy.
        /// </summary>
        public virtual void ProcessFilesAndFolders()
        {
            List<String> subitemsKeys = new List<String>();
            Dictionary<String, Microsoft.Build.Evaluation.ProjectItem> subitems = new Dictionary<String, Microsoft.Build.Evaluation.ProjectItem>();

            // Define a set for our build items. The value does not really matter here.
            Dictionary<String, Microsoft.Build.Evaluation.ProjectItem> items = new Dictionary<String, Microsoft.Build.Evaluation.ProjectItem>();

            // Process Files
            CheckForWildcards();

            foreach (var item in MSBuildProject.GetStaticAndVisibleItemsInOrder(this.buildProject))
            {
                if (string.Compare(MSBuildItem.GetItemType(item), ProjectFileConstants.Folder, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    this.CreateFolderNodes(MSBuildItem.GetEvaluatedInclude(item));
                    continue;
                }

                // Ignore the item if it is a reference
                if (this.FilterItemTypeToBeAddedToHierarchy(MSBuildItem.GetItemType(item)))
                    continue;

                // If the item is already contained do nothing.
                // TODO: possibly report in the error list that the the item is already contained in the project file similar to Language projects.
                if (items.ContainsKey(MSBuildItem.GetEvaluatedInclude(item).ToUpperInvariant()))
                    continue;

                // Make sure that we do not want to add the item, dependent, or independent twice to the ui hierarchy
                items.Add(MSBuildItem.GetEvaluatedInclude(item).ToUpperInvariant(), item);

                string dependentOf = MSBuildItem.GetMetadataValue(item, ProjectFileConstants.DependentUpon);

                if (!this.CanFileNodesHaveChilds || String.IsNullOrEmpty(dependentOf))
                {
                    AddIndependentFileNode(item);
                }
                else
                {
                    // We will process dependent items later.
                    // Note that we use 2 lists as we want to remove elements from
                    // the collection as we loop through it
                    subitemsKeys.Add(MSBuildItem.GetEvaluatedInclude(item));
                    subitems.Add(MSBuildItem.GetEvaluatedInclude(item), item);
                }
            }
        }

        /// <summary>
        /// For flavored projects which implement IPersistXMLFragment, load the information now
        /// </summary>
        public virtual void LoadNonBuildInformation()
        {
            IVsHierarchy outerHierarchy = InteropSafeIVsHierarchy;
            if (outerHierarchy is IPersistXMLFragment)
            {
                this.LoadXmlFragment((IPersistXMLFragment)outerHierarchy, null);
            }
        }

        /// <summary>
        /// Used to sort nodes in the hierarchy.
        /// </summary>
        public abstract int CompareNodes(HierarchyNode node1, HierarchyNode node2);

        /// <summary>
        /// Handles global properties related to configuration and platform changes invoked by a change in the active configuration.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="eventArgs">The event args</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers",
            Justification = "This method will give the opportunity to update global properties based on active configuration change. " +
            "There is no security threat that could otherwise not be reached by listening to configuration chnage events.")]
        internal virtual void OnHandleConfigurationRelatedGlobalProperties(object sender, ActiveConfigurationChangedEventArgs eventArgs)
        {
            Debug.Assert(eventArgs != null, "Wrong hierarchy passed as event arg for the configuration change listener.");

            // If (eventArgs.Hierarchy == NULL) then we received this event because the solution configuration
            // was changed.
            // If it is not null we got the event because a project in teh configuration manager has changed its active configuration.
            // We care only about our project in the default implementation.
            if (eventArgs.Hierarchy == null || !Utilities.IsSameComObject(eventArgs.Hierarchy, this))
            {
                return;
            }

            ConfigCanonicalName configCanonicalName;
            if (!Utilities.TryGetActiveConfigurationAndPlatform(this.Site, this.ProjectIDGuid, out configCanonicalName))
            {
                throw new InvalidOperationException();
            }

            MSBuildProject.SetGlobalProperty(this.buildProject, GlobalProperty.Configuration.ToString(), configCanonicalName.ConfigName);            

            MSBuildProject.SetGlobalProperty(this.buildProject, GlobalProperty.Platform.ToString(), configCanonicalName.MSBuildPlatform);
        }

        public void BeginBatchUpdate()
        {
            // refresh current state
            this.SetCurrentConfiguration();
            this.UpdateMSBuildState();

            isInBatchUpdate = true;
            ((ReferenceContainerNode)GetReferenceContainer()).BeginBatchUpdate();
        }

        public void EndBatchUpdate()
        {
            try
            {
                ((ReferenceContainerNode)GetReferenceContainer()).EndBatchUpdate();
            }
            finally
            {
                // isBatchUpdate should be reset in any case to avoid 'stucking' in Batch Update Mode
                isInBatchUpdate = false;
            }
            // extra defensive check
            if (BuildProject != null)
                BuildProject.ReevaluateIfNecessary();

            ComputeSourcesAndFlags();
        }


        private string GetFilenameFromOutput(IVsOutput2 output)
        {
            object result;
            int hr = output.get_Property(ProjectSystemConstants.OUTPUTGROUP_PROPERTY_OUTPUTLOC, out result);
            string s = result as string;
            if (ErrorHandler.Succeeded(hr) && s != null)
                return s;
            string url;
            ErrorHandler.ThrowOnFailure(output.get_DeploySourceURL(out url));
            return new Uri(url).LocalPath;
        }
        internal string GetKeyOutputForGroup(IVsHierarchy hier, string groupName)
        {
            var buildMgr = this.Site.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
            var activeCfg = new IVsProjectCfg[]{null};
            buildMgr.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, hier, activeCfg);
            var cfg2 = activeCfg[0] as IVsProjectCfg2;
            if (cfg2 != null)
            {
                IVsOutputGroup outputGroup;
                ErrorHandler.ThrowOnFailure(cfg2.OpenOutputGroup(groupName, out outputGroup));
                var outputGroup2 = outputGroup as IVsOutputGroup2;
                if (outputGroup2 != null)
                {
                    IVsOutput2 output2;
                    ErrorHandler.ThrowOnFailure(outputGroup2.get_KeyOutputObject(out output2));
                    if (output2 == null) return String.Empty;
                    return GetFilenameFromOutput(output2);
                }
                // if outputGroup does not support IVsOutputGroup2
                string keyOutputCanonicalName;
                ErrorHandler.ThrowOnFailure(outputGroup.get_KeyOutput(out keyOutputCanonicalName));
                var outputCount = new uint[]{0};
                ErrorHandler.ThrowOnFailure(outputGroup.get_Outputs(0u, null, outputCount));
                if (outputCount[0] == 0) return String.Empty;
                var outputs = new IVsOutput2[outputCount[0]];
                ErrorHandler.ThrowOnFailure(outputGroup.get_Outputs((uint)outputs.Length, outputs, null));
                for (uint i = 0; i < outputs.Length; i++)
                {
                    var output2 = outputs[i];
                    string canonicalName;
                    int hr = output2.get_CanonicalName(out canonicalName);
                    if (!ErrorHandler.Succeeded(hr)) continue;
                    if (String.Equals(keyOutputCanonicalName, canonicalName, StringComparison.Ordinal))
                    {
                        return GetFilenameFromOutput(output2);
                    }
                }
                
            }
            return String.Empty;
        }

        private void TellMSBuildCurrentSolutionConfiguration()
        {
            var canonicalCfgNameOpt = GetCurrentConfigurationName();
            if (canonicalCfgNameOpt == null)
                return;
            var canonicalCfgName = canonicalCfgNameOpt.Value;
            if (String.IsNullOrEmpty(canonicalCfgName.Platform))
            {
                // cfgName is not conventional, just do something reasonable
                MSBuildProject.SetGlobalProperty(this.buildProject, ProjectFileConstants.Configuration, canonicalCfgName.ConfigName);
            }
            else
            {
                MSBuildProject.SetGlobalProperty(this.buildProject, ProjectFileConstants.Configuration, canonicalCfgName.ConfigName);
                MSBuildProject.SetGlobalProperty(this.buildProject, ProjectFileConstants.Platform, canonicalCfgName.MSBuildPlatform);
            }
            this.UpdateMSBuildState();
        }

        private ConfigCanonicalName? GetCurrentConfigurationName()
        {
            if (Site == null)
                return null;
            IVsSolutionBuildManager buildMgr = Site.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
            IVsProjectCfg[] cfgs = new IVsProjectCfg[] { null };
            buildMgr.FindActiveProjectCfg(System.IntPtr.Zero, System.IntPtr.Zero, InteropSafeIVsHierarchy, cfgs);
            if (cfgs[0] == null)
            {
                return null;
            }
            string cfgName = "";
            cfgs[0].get_CanonicalName(out cfgName);
            // cfgName conventionally has form "Configuration|Platform"
            return new ConfigCanonicalName(cfgName);            
        }

        internal string GetCurrentOutputAssembly()
        {
            var currentConfigName = GetCurrentConfigurationName();
            if (currentConfigName != null)
            {
                GetProjectOptions(currentConfigName.Value);
                if (options != null)
                    return options.OutputAssembly;
            }
            return null;
        }

        /// <summary>
        /// Overloaded method. Invokes MSBuild using the default configuration and does without logging on the output window pane.
        /// </summary>
        internal BuildResult Build(string target)
        {
            return this.Build(new ConfigCanonicalName(), null, target, null);
        }

        /// <summary>
        /// Overloaded method. Invokes MSBuild using the default configuration and does without logging on the output window pane.
        /// </summary>
        internal BuildResult BuildWithExtraProperties(string target, IEnumerable<KeyValuePair<string, string>> extraProperties)
        {
            return this.Build(new ConfigCanonicalName(), null, target, extraProperties);
        }

        /// <summary>
        /// Overloaded method. Invokes MSBuild using the default configuration.
        /// </summary>
        internal BuildResult BuildToOutput(string target, IVsOutputWindowPane output, IEnumerable<KeyValuePair<string, string>> extraProperties)
        {
            return this.Build(new ConfigCanonicalName(), output, target, extraProperties);
        }

        /// <summary>
        /// Get value of Project property
        /// </summary>
        /// <param name="propertyName">Name of Property to retrieve</param>
        /// <returns>Value of property</returns>
        public string GetProjectProperty(string propertyName)
        {
            return this.GetProjectProperty(propertyName, true);
        }

        public void ReevaluateBuildProjectIfNecessary()
        {
            // skip reevaluation when in BatchUpdate mode - ReevaluateIfNecessary will be called from EndBatchUpdate
            if (!isInBatchUpdate && BuildProject != null)
            {
                BuildProject.ReevaluateIfNecessary();
            }
        }

        /// <summary>
        /// Set dirty state of project
        /// </summary>
        /// <param name="value">boolean value indicating dirty state</param>
        public void SetProjectFileDirty(bool value)
        {
            this.options = null;
            //this.projectInstance = null;
            ReevaluateBuildProjectIfNecessary();
            this.isDirty = value;
            if (this.isDirty)
            {
                this.lastModifiedTime = DateTime.UtcNow;
                this.buildIsPrepared = false;
            }
        }

        /// <summary>
        /// Get output assembly for a specific configuration name
        /// </summary>
        /// <param name="configCanonicalName">Name of configuration</param>
        /// <returns>Name of output assembly</returns>
        internal string GetOutputAssembly(ConfigCanonicalName configCanonicalName)
        {
            ProjectOptions options = this.GetProjectOptions(configCanonicalName);

            return options.OutputAssembly;
        }

        /// <summary>
        /// Get Node from ItemID.
        /// </summary>
        /// <param name="itemId">ItemID for the requested node</param>
        /// <returns>Node if found</returns>
        public HierarchyNode NodeFromItemId(uint itemId)
        {
            if (VSConstants.VSITEMID_ROOT == itemId)
            {
                return this;
            }
            else if (VSConstants.VSITEMID_NIL == itemId)
            {
                return null;
            }
            else if (VSConstants.VSITEMID_SELECTION == itemId)
            {
                throw new NotImplementedException();
            }

            return (HierarchyNode)this.ItemIdMap[itemId];
        }

        /// <summary>
        /// This method return new project element, and add new MSBuild item to the project/build hierarchy
        /// </summary>
        /// <param name="file">file name</param>
        /// <param name="itemType">MSBuild item type</param>
        /// <returns>new project element</returns>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
        internal ProjectElement CreateMsBuildFileItem(string file, string itemType)
        {
            return new ProjectElement(this, file, itemType);
        }

        /// <summary>
        /// This method returns new project element based on existing MSBuild item. It does not modify/add project/build hierarchy at all.
        /// </summary>
        /// <param name="item">MSBuild item instance</param>
        /// <returns>wrapping project element</returns>
        internal ProjectElement GetProjectElement(Microsoft.Build.Evaluation.ProjectItem item)
        {
            return new ProjectElement(this, item, false);
        }

        /// <summary>
        /// Create FolderNode from Path
        /// </summary>
        /// <param name="path">Path to folder</param>
        /// <returns>FolderNode created that can be added to the hierarchy</returns>
        internal FolderNode CreateFolderNode(string path)
        {
            ProjectElement item = this.AddFolderToMsBuild(path);
            FolderNode folderNode = CreateFolderNode(path, item);
            return folderNode;
        }

        /// <summary>
        /// Verify if the file can be written to.
        /// Return false if the file is read only and/or not checked out
        /// and the user did not give permission to change it.
        /// Note that exact behavior can also be affected based on the SCC
        /// settings under Tools->Options.
        /// </summary>
        internal bool QueryEditProjectFile(bool suppressUI)
        {
            bool result = true;
            if (this.site == null)
            {
                // We're already zombied. Better return FALSE.
                result = false;
            }
            else if (this.disableQueryEdit)
            {
                return true;
            }
            else
            {
                IVsQueryEditQuerySave2 queryEditQuerySave = this.GetService(typeof(SVsQueryEditQuerySave)) as IVsQueryEditQuerySave2;
                if (queryEditQuerySave != null)
                {   // Project path dependends on server/client project
                    string path = this.filename;

                    tagVSQueryEditFlags qef = tagVSQueryEditFlags.QEF_AllowInMemoryEdits;
                    if (suppressUI)
                        qef |= tagVSQueryEditFlags.QEF_SilentMode;

                    // If we are debugging, we want to prevent our project from being reloaded. To 
                    // do this, we pass the QEF_NoReload flag
                    if (!Utilities.IsVisualStudioInDesignMode(this.Site))
                        qef |= tagVSQueryEditFlags.QEF_NoReload;

                    uint verdict;
                    uint moreInfo;
                    string[] files = new string[1];
                    files[0] = path;
                    uint[] flags = new uint[1];
                    VSQEQS_FILE_ATTRIBUTE_DATA[] attributes = new VSQEQS_FILE_ATTRIBUTE_DATA[1];
                    int hr = queryEditQuerySave.QueryEditFiles(
                                    (uint)qef,
                                    1, // 1 file
                                    files, // array of files
                                    flags, // no per file flags
                                    attributes, // no per file file attributes
                                    out verdict,
                                    out moreInfo /* ignore additional results */);

                    tagVSQueryEditResult qer = (tagVSQueryEditResult)verdict;
                    if (ErrorHandler.Failed(hr) || (qer != tagVSQueryEditResult.QER_EditOK))
                    {
                        if (!suppressUI && !Utilities.IsInAutomationFunction(this.Site))
                        {
                            string message = SR.GetString(SR.CancelQueryEdit, path);
                            string title = string.Empty;
                            OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                            OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                            OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                            VsShellUtilities.ShowMessageBox(this.Site, title, message, icon, buttons, defaultButton);
                        }
                        result = false;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Given a node determines what is the directory that can accept files.
        /// If the node is a FoldeNode than it is the Url of the Folder.
        /// If the node is a ProjectNode it is the project folder.
        /// Otherwise (such as FileNode subitem) it delegate the resolution to the parent node.
        /// </summary>
        public string GetBaseDirectoryForAddingFiles(HierarchyNode nodeToAddFile)
        {
            string baseDir = String.Empty;

            if (nodeToAddFile is FolderNode)
            {
                baseDir = nodeToAddFile.Url;
            }
            else if (nodeToAddFile is ProjectNode)
            {
                baseDir = this.ProjectFolder;
            }
            else if (nodeToAddFile != null)
            {
                baseDir = GetBaseDirectoryForAddingFiles(nodeToAddFile.Parent);
            }

            return baseDir;
        }

        /// <summary>
        /// For public use only.
        /// This creates a copy of an existing configuration and add it to the project.
        /// Caller should change the condition on the PropertyGroup.
        /// If derived class want to accomplish this, they should call ConfigProvider.AddCfgsOfCfgName()
        /// It is expected that in the future MSBuild will have support for this so we don't have to
        /// do it manually.
        /// </summary>
        /// <param name="group">PropertyGroup to clone</param>
        /// <returns></returns>
        public Microsoft.Build.Construction.ProjectPropertyGroupElement ClonePropertyGroup(Microsoft.Build.Construction.ProjectPropertyGroupElement group)
        {
            // Create a new (empty) PropertyGroup
            Microsoft.Build.Construction.ProjectPropertyGroupElement newPropertyGroup = this.buildProject.Xml.AddPropertyGroup();

            // Now copy everything from the group we are trying to clone to the group we are creating
            if (!String.IsNullOrEmpty(group.Condition))
                newPropertyGroup.Condition = group.Condition;
            foreach (Microsoft.Build.Construction.ProjectPropertyElement prop in group.Properties)
            {
                Microsoft.Build.Construction.ProjectPropertyElement newProperty = newPropertyGroup.AddProperty(prop.Name, prop.Value);
                if (!String.IsNullOrEmpty(prop.Condition))
                    newProperty.Condition = prop.Condition;
            }

            SetProjectFileDirty(true);

            return newPropertyGroup;
        }

        /// <summary>
        /// Register the project with the Scc manager.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public void RegisterSccProject()
        {

            if (this.IsSccDisabled || this.isRegisteredWithScc || String.IsNullOrEmpty(this.sccProjectName))
            {
                return;
            }

            IVsSccManager2 sccManager = this.Site.GetService(typeof(SVsSccManager)) as IVsSccManager2;

            if (sccManager != null)
            {
                ErrorHandler.ThrowOnFailure(sccManager.RegisterSccProject(this.InteropSafeIVsSccProject2, this.sccProjectName, this.sccAuxPath, this.sccLocalPath, this.sccProvider));

                this.isRegisteredWithScc = true;
            }
        }

        /// <summary>
        ///  Unregisters us from the SCC manager
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "UnRegister")]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Un")]
        public void UnRegisterProject()
        {
            if (this.IsSccDisabled || !this.isRegisteredWithScc)
            {
                return;
            }

            IVsSccManager2 sccManager = this.Site.GetService(typeof(SVsSccManager)) as IVsSccManager2;

            if (sccManager != null)
            {
                ErrorHandler.ThrowOnFailure(sccManager.UnregisterSccProject(this.InteropSafeIVsSccProject2));
                this.isRegisteredWithScc = false;
            }
        }

        /// <summary>
        /// Get the CATID corresponding to the specified type.
        /// </summary>
        /// <param name="type">Type of the object for which you want the CATID</param>
        /// <returns>CATID</returns>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "CATID")]
        public Guid GetCATIDForType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            if (catidMapping.ContainsKey(type))
                return catidMapping[type];
            // If you get here and you want your object to be extensible, then add a call to AddCATIDMapping() in your project constructor
            return Guid.Empty;
        }

        /// <summary>
        /// This is used to specify a CATID corresponding to a BrowseObject or an ExtObject.
        /// The CATID can be any GUID you choose. For types which are your owns, you could use
        /// their type GUID, while for other types (such as those provided in the MPF) you should
        /// provide a different GUID.
        /// </summary>
        /// <param name="type">Type of the extensible object</param>
        /// <param name="catid">GUID that extender can use to uniquely identify your object type</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "catid")]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "CATID")]
        public void AddCATIDMapping(Type type, Guid catid)
        {
            catidMapping.Add(type, catid);
        }

        /// <summary>
        /// Initialize an object with an XML fragment.
        /// </summary>
        /// <param name="persistXmlFragment">Object that support being initialized with an XML fragment</param>
        /// <param name="configName">Name of the configuration being initialized, null if it is the project</param>
        public void LoadXmlFragment(IPersistXMLFragment persistXmlFragment, string configName)
        {
            if (xmlFragments == null)
            {
                // Retrieve the xml fragments from MSBuild
                xmlFragments = new XmlDocument();
                xmlFragments.XmlResolver = null;
                var ext = GetProjectExtensions();
                string fragments = ext != null ? ext[ProjectFileConstants.VisualStudio] : null; 
                if (!String.IsNullOrEmpty(fragments))
                {
                    fragments = String.Format(CultureInfo.InvariantCulture, "<root>{0}</root>", fragments);
                    using(StringReader stream = new StringReader(fragments))
                    using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    {
                        xmlFragments.Load(reader);
                    }
                }
            }

            // We need to loop through all the flavors
            string flavorsGuid;
            ErrorHandler.ThrowOnFailure(((IVsAggregatableProject)this).GetAggregateProjectTypeGuids(out flavorsGuid));
            foreach (Guid flavor in Utilities.GuidsArrayFromSemicolonDelimitedStringOfGuids(flavorsGuid))
            {
                // Look for a matching fragment
                string flavorGuidString = flavor.ToString("B");
                string fragment = null;
                XmlNode node = null;
                if (xmlFragments.FirstChild != null)
                {
                    foreach (XmlNode child in xmlFragments.FirstChild.ChildNodes)
                    {
                        if (child.Attributes.Count > 0)
                        {
                            string guid = String.Empty;
                            string configuration = String.Empty;
                            if (child.Attributes[ProjectFileConstants.Guid] != null)
                                guid = child.Attributes[ProjectFileConstants.Guid].Value;
                            if (child.Attributes[ProjectFileConstants.Configuration] != null)
                                configuration = child.Attributes[ProjectFileConstants.Configuration].Value;

                            if (String.Compare(child.Name, ProjectFileConstants.FlavorProperties, StringComparison.OrdinalIgnoreCase) == 0
                                    && String.Compare(guid, flavorGuidString, StringComparison.OrdinalIgnoreCase) == 0
                                    && ((String.IsNullOrEmpty(configName) && String.IsNullOrEmpty(configuration))
                                        || (String.Compare(configuration, configName, StringComparison.OrdinalIgnoreCase) == 0)))
                            {
                                // we found the matching fragment
                                fragment = child.InnerXml;
                                node = child;
                                break;
                            }
                        }
                    }
                }

                Guid flavorGuid = flavor;
                if (String.IsNullOrEmpty(fragment))
                {
                    // the fragment was not found so init with default values
                    ErrorHandler.ThrowOnFailure(persistXmlFragment.InitNew(ref flavorGuid, (uint)_PersistStorageType.PST_PROJECT_FILE));
                    // While we don't yet support user files, our flavors might, so we will store that in the project file until then
                    // TODO: Refactor this code when we support user files
                    ErrorHandler.ThrowOnFailure(persistXmlFragment.InitNew(ref flavorGuid, (uint)_PersistStorageType.PST_USER_FILE));
                }
                else
                {
                    ErrorHandler.ThrowOnFailure(persistXmlFragment.Load(ref flavorGuid, (uint)_PersistStorageType.PST_PROJECT_FILE, fragment));
                    // While we don't yet support user files, our flavors might, so we will store that in the project file until then
                    // TODO: Refactor this code when we support user files
                    if (node.NextSibling != null && node.NextSibling.Attributes[ProjectFileConstants.User] != null)
                        ErrorHandler.ThrowOnFailure(persistXmlFragment.Load(ref flavorGuid, (uint)_PersistStorageType.PST_USER_FILE, node.NextSibling.InnerXml));
                }
            }
        }

        /// <summary>
        /// Retrieve all XML fragments that need to be saved from the flavors and store the information in msbuild.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "XML")]
        public void PersistXMLFragments()
        {
            if (this.IsFlavorDirty() != 0)
            {
                XmlDocument doc = new XmlDocument();
                XmlElement root = doc.CreateElement("ROOT");

                // We will need the list of configuration inside the loop, so get it before entering the loop
                uint[] count = new uint[1];
                IVsCfg[] configs = null;
                int hr = this.ConfigProvider.GetCfgs(0, null, count, null);
                if (ErrorHandler.Succeeded(hr) && count[0] > 0)
                {
                    configs = new IVsCfg[count[0]];
                    hr = this.ConfigProvider.GetCfgs((uint)configs.Length, configs, count, null);
                    if (ErrorHandler.Failed(hr))
                        count[0] = 0;
                }
                if (count[0] == 0)
                    configs = new IVsCfg[0];

                // We need to loop through all the flavors
                string flavorsGuid;
                ErrorHandler.ThrowOnFailure(((IVsAggregatableProject)this).GetAggregateProjectTypeGuids(out flavorsGuid));
                foreach (Guid flavor in Utilities.GuidsArrayFromSemicolonDelimitedStringOfGuids(flavorsGuid))
                {
                    IVsHierarchy outerHierarchy = this.InteropSafeIVsHierarchy;
                    // First check the project
                    if (outerHierarchy is IPersistXMLFragment)
                    {
                        // Retrieve the XML fragment
                        string fragment = string.Empty;
                        Guid flavorGuid = flavor;
                        ErrorHandler.ThrowOnFailure(((IPersistXMLFragment)outerHierarchy).Save(ref flavorGuid, (uint)_PersistStorageType.PST_PROJECT_FILE, out fragment, 1));
                        if (!String.IsNullOrEmpty(fragment))
                        {
                            // Add the fragment to our XML
                            WrapXmlFragment(doc, root, flavor, null, fragment);
                        }
                        // While we don't yet support user files, our flavors might, so we will store that in the project file until then
                        // TODO: Refactor this code when we support user files
                        fragment = String.Empty;
                        ErrorHandler.ThrowOnFailure(((IPersistXMLFragment)outerHierarchy).Save(ref flavorGuid, (uint)_PersistStorageType.PST_USER_FILE, out fragment, 1));
                        if (!String.IsNullOrEmpty(fragment))
                        {
                            // Add the fragment to our XML
                            XmlElement node = WrapXmlFragment(doc, root, flavor, null, fragment);
                            node.Attributes.Append(doc.CreateAttribute(ProjectFileConstants.User));
                        }
                    }

                    // Then look at the configurations
                    foreach (IVsCfg config in configs)
                    {
                        // Get the fragment for this flavor/config pair
                        string fragment;
                        ErrorHandler.ThrowOnFailure(((ProjectConfig)config).GetXmlFragment(flavor, _PersistStorageType.PST_PROJECT_FILE, out fragment));
                        if (!String.IsNullOrEmpty(fragment))
                        {
                            string configName;
                            config.get_DisplayName(out configName);
                            WrapXmlFragment(doc, root, flavor, configName, fragment);
                        }
                    }
                }
                if (root.ChildNodes != null && root.ChildNodes.Count > 0)
                {
                    // Save our XML (this is only the non-build information for each flavor) in msbuild
                    SetProjectExtensions(ProjectFileConstants.VisualStudio, root.InnerXml.ToString());
                }
            }
        }

        //=================================================================================

        public virtual int GetCfgProvider(out IVsCfgProvider p)
        {
            CCITracing.TraceCall();
            // Be sure to call the property here since that is doing a polymorhic ProjectConfig creation.
            p = this.ConfigProvider;
            return (p == null ? VSConstants.E_NOTIMPL : VSConstants.S_OK);
        }

        public int GetClassID(out Guid clsid)
        {
            clsid = this.ProjectGuid;
            return VSConstants.S_OK;
        }

        int IPersistFileFormat.GetClassID(out Guid clsid)
        {
            clsid = this.ProjectGuid;
            return VSConstants.S_OK;
        }

        public virtual int GetCurFile(out string name, out uint formatIndex)
        {
            name = this.filename;
            formatIndex = 0;
            return VSConstants.S_OK;
        }

        public virtual int GetFormatList(out string formatlist)
        {
            formatlist = String.Empty;
            return VSConstants.S_OK;
        }

        public virtual int InitNew(uint formatIndex)
        {
            return VSConstants.S_OK;
        }

        public virtual int IsDirty(out int isDirty)
        {
            isDirty = 0;
            if (this.buildProject.IsDirty || this.IsProjectFileDirty)
            {
                isDirty = 1;
                return VSConstants.S_OK;
            }

            isDirty = IsFlavorDirty();

            return VSConstants.S_OK;
        }

        public int IsFlavorDirty()
        {
            int isDirty = 0;
            // See if one of our flavor consider us dirty
            IVsHierarchy outerHierarchy = this.InteropSafeIVsHierarchy;
            if (outerHierarchy is IPersistXMLFragment)
            {
                // First check the project
                ((IPersistXMLFragment)outerHierarchy).IsFragmentDirty((uint)_PersistStorageType.PST_PROJECT_FILE, out isDirty);
                // While we don't yet support user files, our flavors might, so we will store that in the project file until then
                // TODO: Refactor this code when we support user files
                if (isDirty == 0)
                    ((IPersistXMLFragment)outerHierarchy).IsFragmentDirty((uint)_PersistStorageType.PST_USER_FILE, out isDirty);
            }
            if (isDirty == 0)
            {
                // Then look at the configurations
                uint[] count = new uint[1];
                int hr = this.ConfigProvider.GetCfgs(0, null, count, null);
                if (ErrorHandler.Succeeded(hr) && count[0] > 0)
                {
                    // We need to loop through the configurations
                    IVsCfg[] configs = new IVsCfg[count[0]];
                    hr = this.ConfigProvider.GetCfgs((uint)configs.Length, configs, count, null);
                    Debug.Assert(ErrorHandler.Succeeded(hr), "failed to retrieve configurations");
                    foreach (IVsCfg config in configs)
                    {
                        isDirty = ((ProjectConfig)config).IsFlavorDirty(_PersistStorageType.PST_PROJECT_FILE);
                        if (isDirty != 0)
                            break;
                    }
                }
            }
            return isDirty;
        }

        public virtual int Load(string fileName, uint mode, int readOnly)
        {
            this.filename = fileName;
            this.Reload();
            return VSConstants.S_OK;
        }

        public virtual int Save(string fileToBeSaved, int remember, uint formatIndex)
        {

            // The file name can be null. Then try to use the Url.
            string tempFileToBeSaved = fileToBeSaved;
            if (String.IsNullOrEmpty(tempFileToBeSaved) && !String.IsNullOrEmpty(this.Url))
            {
                tempFileToBeSaved = this.Url;
            }

            if (String.IsNullOrEmpty(tempFileToBeSaved))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "fileToBeSaved");
            }

            bool setProjectFileDirtyAfterSave = false;
            if (remember == 0)
            {
                setProjectFileDirtyAfterSave = this.IsProjectFileDirty;
            }

            // Update the project with the latest flavor data (if needed)
            PersistXMLFragments();

            int result = VSConstants.S_OK;
            bool saveAs = true;
            if (NativeMethods.IsSamePath(tempFileToBeSaved, this.filename))
            {
                saveAs = false;
            }
            if (!saveAs)
            {
                SuspendFileChanges fileChanges = new SuspendFileChanges(this.Site, this.filename);
                fileChanges.Suspend();
                try 
                {
                    this.buildProject.Save(tempFileToBeSaved);
                    this.SetProjectFileDirty(false);
                }
                finally
                {
                    fileChanges.Resume();
                }
            }
            else
            {
                result = this.SaveAs(tempFileToBeSaved, remember == 0);
                if (result != VSConstants.OLE_E_PROMPTSAVECANCELLED)
                {
                    ErrorHandler.ThrowOnFailure(result);
                }

            }

            if (setProjectFileDirtyAfterSave)
            {
                this.SetProjectFileDirty(true);
            }

            return result;
        }

        public virtual int SaveCompleted(string filename)
        {
            // TODO: turn file watcher back on.
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Callback from the additem dialog. Deals with adding new and existing items
        /// </summary>
        public virtual int GetMkDocument(uint itemId, out string mkDoc)
        {
            mkDoc = null;
            if (itemId == VSConstants.VSITEMID_SELECTION)
            {
                return VSConstants.E_UNEXPECTED;
            }

            HierarchyNode n = this.NodeFromItemId(itemId);
            if (n == null)
            {
                return VSConstants.E_INVALIDARG;
            }

            mkDoc = n.GetMkDocument();

            if (String.IsNullOrEmpty(mkDoc))
            {
                return VSConstants.E_FAIL;
            }

            return VSConstants.S_OK;
        }

        public abstract void MoveFileToBottomIfNoOtherPendingMove(FileNode node);

        public int AddItem(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, VSADDRESULT[] result)
        {
            return DoAddItem(itemIdLoc, op, itemName, filesToOpen, files, dlgOwner, result);
        }

        internal int DoAddItem(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, VSADDRESULT[] result, AddItemContext addItemContext = AddItemContext.Unknown)
        {
            // Note that when executing UI actions from the F# project system, any pending 'moves' (for add/add above/add below) are already handled at another level (in Project.fs).
            Guid empty = Guid.Empty;

            // When Adding an item, pass true to let AddItemWithSpecific know to fire the tracker events.
            return AddItemWithSpecific(itemIdLoc, op, itemName, filesToOpen, files, dlgOwner, 0, ref empty, null, ref empty, result, true, context: addItemContext);
        }

        /// <summary>
        /// Creates new items in a project, adds existing files to a project, or causes Add Item wizards to be run
        /// </summary>
        /// <param name="itemIdLoc"></param>
        /// <param name="op"></param>
        /// <param name="itemName"></param>
        /// <param name="filesToOpen"></param>
        /// <param name="files">Array of file names. 
        /// If dwAddItemOperation is VSADDITEMOP_CLONEFILE the first item in the array is the name of the file to clone. 
        /// If dwAddItemOperation is VSADDITEMOP_OPENDIRECTORY, the first item in the array is the directory to open. 
        /// If dwAddItemOperation is VSADDITEMOP_RUNWIZARD, the first item is the name of the wizard to run, 
        /// and the second item is the file name the user supplied (same as itemName).</param>
        /// <param name="dlgOwner"></param>
        /// <param name="editorFlags"></param>
        /// <param name="editorType"></param>
        /// <param name="physicalView"></param>
        /// <param name="logicalView"></param>
        /// <param name="result"></param>
        /// <returns>S_OK if it succeeds </returns>
        /// <remarks>The result array is initalized to failure.</remarks>
        public virtual int AddItemWithSpecific(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, VSADDRESULT[] result)
        {
            // When Adding an item, pass true to let AddItemWithSpecific know to fire the tracker events.
            return AddItemWithSpecific(itemIdLoc, op, itemName, filesToOpen, files, dlgOwner, editorFlags, ref editorType, physicalView, ref logicalView, result, true);
        }

        /// <summary>
        /// Generates Copy Of ... name for a given fileName. <c>baseDir</c> will be containing folder for the result file.
        /// Candidates will look like:
        /// - Copy of fileName
        /// - Copy (2) of fileName etc
        /// </summary>
        private string GenerateCopyOfFileName(string baseDir, string fileName)
        {
            var candidate = Path.Combine(baseDir, SR.GetString(SR.CopyOf, fileName));
            if (FindChild(candidate) == null)
                return candidate;

            for (var i = 2; ; ++i)
            {
                candidate = Path.Combine(baseDir, SR.GetString(SR.CopyOf2, i, fileName));
                if (FindChild(candidate) == null)
                    return candidate;
            } 
        }

        internal int AddItemWithSpecific(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, VSADDRESULT[] result, bool bTrackChanges, Func<uint> getIdOfExisingItem = null, AddItemContext context = AddItemContext.Unknown)
        {
            if (files == null || result == null || files.Length == 0 || result.Length == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            if (getIdOfExisingItem != null && files.Length != 1)
            {
                // only 1 file can participate in renaming
                return VSConstants.E_INVALIDARG;
            }

            // Locate the node to be the container node for the file(s) being added
            // only projectnode or foldernode and file nodes are valid container nodes
            // We need to locate the parent since the item wizard expects the parent to be passed.
            HierarchyNode n = this.NodeFromItemId(itemIdLoc);
            if (n == null)
            {
                return VSConstants.E_INVALIDARG;
            }

            while (!(n is ProjectNode) && !(n is FolderNode))
            {
                n = n.Parent;
            }
            Debug.Assert(n != null, "We should at this point have either a ProjectNode or FolderNode or a FileNode as a container for the new filenodes");

            // handle link and runwizard operations at this point
            switch (op)
            {
                case VSADDITEMOPERATION.VSADDITEMOP_LINKTOFILE:
                    return AddLinkedItem(n, files, result);

                case VSADDITEMOPERATION.VSADDITEMOP_RUNWIZARD:
                    result[0] = this.RunWizard(n, itemName, files[0], dlgOwner);
                    return VSConstants.S_OK;
            }

            string[] actualFiles = new string[files.Length];


            VSQUERYADDFILEFLAGS[] flags = this.GetQueryAddFileFlags(files);

            string baseDir = this.GetBaseDirectoryForAddingFiles(n);
            // If we did not get a directory for node that is the parent of the item then fail.
            if (String.IsNullOrEmpty(baseDir))
            {
                return VSConstants.E_FAIL;
            }

            // Pre-calculates some paths that we can use when calling CanAddItems
            List<string> filesToAdd = new List<string>();
            for (int index = 0; index < files.Length; index++)
            {
                string newFileName = String.Empty;

                string file = files[index];

                switch (op)
                {
                    case VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE:
                        // New item added. Need to copy template to new location and then add new location 
                        newFileName = Path.Combine(baseDir, itemName);
                        break;

                    case VSADDITEMOPERATION.VSADDITEMOP_OPENFILE:
                        {
                            string fileName = Path.GetFileName(file);
                            
                            if (context == AddItemContext.Paste && FindChild(file) != null)
                            {
                                newFileName = Path.Combine(baseDir, fileName);
                                if (FindChild(newFileName) != null)
                                {
                                    // if we are doing 'Paste' and source file belongs to current project - generate fresh unique name
                                    newFileName = GenerateCopyOfFileName(baseDir, fileName);
                                }
                            }
                            else if (!IsContainedWithinProjectDirectory(file))
                            {
                                // if the file isn't contained within the project directory,
                                // copy it to be a child of the node we're adding to.
                                newFileName = Path.Combine(baseDir, fileName);
                            }
                            else
                            {
                                newFileName = file;
                            }
                        }
                        break;
                }
                filesToAdd.Add(newFileName);
            }

            // Ask tracker objects if we can add files
            if (!this.tracker.CanAddItems(filesToAdd.ToArray(), flags))
            {
                // We were not allowed to add the files
                return VSConstants.E_FAIL;
            }

            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            // Add the files to the hierarchy
            int actualFilesAddedIndex = 0;
            for (int index = 0; index < filesToAdd.Count; index++)
            {
                HierarchyNode child;
                bool overwrite = false;
                string newFileName = filesToAdd[index];

                string file = files[index];
                result[0] = VSADDRESULT.ADDRESULT_Failure;

                child = this.FindChild(newFileName);
                if (child != null)
                {
                    // If the file to be added is an existing file part of the hierarchy then continue.
                    if (NativeMethods.IsSamePath(file, newFileName))
                    {
                        result[0] = VSADDRESULT.ADDRESULT_Cancel;
                        continue;
                    }

                    int canOverWriteExistingItem = this.CanOverwriteExistingItem(file, newFileName);

                    if (canOverWriteExistingItem == (int)OleConstants.OLECMDERR_E_CANCELED)
                    {
                        result[0] = VSADDRESULT.ADDRESULT_Cancel;
                        return canOverWriteExistingItem;
                    }
                    else if (canOverWriteExistingItem == VSConstants.S_OK)
                    {
                        overwrite = true;
                    }
                    else
                    {
                        return canOverWriteExistingItem;
                    }
                }

                // If the file to be added is not in the same path copy it.
                if (NativeMethods.IsSamePath(file, newFileName) == false)
                {
                    if (!overwrite && File.Exists(newFileName))
                    {
                        string message = String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileAlreadyExists, CultureInfo.CurrentUICulture), newFileName);
                        string title = string.Empty;
                        OLEMSGICON icon = OLEMSGICON.OLEMSGICON_QUERY;
                        OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_YESNO;
                        OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                        int messageboxResult = VsShellUtilities.ShowMessageBox(this.Site, title, message, icon, buttons, defaultButton);
                        if (messageboxResult == NativeMethods.IDNO)
                        {
                            result[0] = VSADDRESULT.ADDRESULT_Cancel;
                            return (int)OleConstants.OLECMDERR_E_CANCELED;
                        }
                    }

                    // Copy the file to the correct location.
                    // We will suppress the file change events to be triggered to this item, since we are going to copy over the existing file and thus we will trigger a file change event. 
                    // We do not want the filechange event to ocur in this case, similar that we do not want a file change event to occur when saving a file.
                    IVsFileChangeEx fileChange = this.site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
                    if (fileChange == null)
                    {
                        throw new InvalidOperationException();
                    }

                    try
                    {
                        fileChange.IgnoreFile(VSConstants.VSCOOKIE_NIL, newFileName, 1);
                        if (op == VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE)
                        {
                            this.AddFileFromTemplate(file, newFileName);
                        }
                        else
                        {
                            PackageUtilities.CopyUrlToLocal(new Uri(file), newFileName);
                        }
                    }
                    finally
                    {
                        fileChange.IgnoreFile(VSConstants.VSCOOKIE_NIL, newFileName, 0);
                    }
                }

                if (overwrite)
                {
                    this.OverwriteExistingItem(child);
                }
                else
                {
                    if (getIdOfExisingItem != null)
                    {
                        // this is rename operation
                        //Add new filenode/dependentfilenode
                        var exisingId = getIdOfExisingItem();
                        this.AddNewFileNodeToHierarchyCore(n, newFileName, exisingId);
                    }
                    else
                    {
                        // ordinary add
                        //Add new filenode/dependentfilenode
                        this.AddNewFileNodeToHierarchyCore(n, newFileName);
                    }

                    if (bTrackChanges)
                    {
                        FireAddNodeEvent(newFileName);
                    }
                }

                result[0] = VSADDRESULT.ADDRESULT_Success;
                actualFiles[actualFilesAddedIndex++] = newFileName;
            }

            // Notify listeners that items were appended.
            if (actualFilesAddedIndex > 0)
                n.OnItemsAppended(n);

            //Open files if this was requested through the editorFlags
            bool openFiles = (editorFlags & (uint)__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_DoOpen) != 0;
            if (openFiles && actualFiles.Length <= filesToOpen)
            {
                for (int i = 0; i < filesToOpen; i++)
                {
                    if (!String.IsNullOrEmpty(actualFiles[i]))
                    {
                        string name = actualFiles[i];
                        HierarchyNode child = this.FindChild(name);
                        Debug.Assert(child != null, "We should have been able to find the new element in the hierarchy");
                        if (child != null)
                        {
                            IVsWindowFrame frame;
                            if (editorType == Guid.Empty)
                            {
                                Guid view = Guid.Empty;
                                ErrorHandler.ThrowOnFailure(this.OpenItem(child.ID, ref view, IntPtr.Zero, out frame));
                            }
                            else
                            {
                                ErrorHandler.ThrowOnFailure(this.OpenItemWithSpecific(child.ID, editorFlags, ref editorType, physicalView, ref logicalView, IntPtr.Zero, out frame));
                            }

                            // Show the window frame in the UI and make it the active window
                            if (frame != null)
                            {
                                ErrorHandler.ThrowOnFailure(frame.Show());
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < actualFilesAddedIndex; ++i)
            {
                string absolutePath = actualFiles[i];
                var fileNode = this.FindChild(absolutePath) as FileNode;
                Debug.Assert(fileNode != null, $"Unable to find added child node {absolutePath}");
                MoveFileToBottomIfNoOtherPendingMove(fileNode);
            }

            return VSConstants.S_OK;
        }

        public virtual int AddLinkedItem(HierarchyNode node, string[] files, VSADDRESULT[] result)
        {
            // This is truely outside of our hierarchy so add the node as a link.

            // Check to verify this node isn't already in the hierarchy
            // files[index] will be the absolute location to the linked file
            for (int index = 0; index < files.Length; index++)
            {
                LinkedFileNode linkedNode = this.AddNewFileNodeToHierarchyCore(node, files[index]) as LinkedFileNode;
                if (linkedNode == null)
                {
                    return VSConstants.E_FAIL;
                }
                
                if (node == this)
                {
                    // parent we are adding to is project root
                    linkedNode.ItemNode.SetMetadata(ProjectFileConstants.Link, linkedNode.Caption);
                }
                else
                {
                    // parent we are adding to is a sub-folder of the project
                    string relativePathOfParent = node.Url.Substring(Path.GetDirectoryName(this.Url).Length + 1 /*trailing slash*/);
                    linkedNode.ItemNode.SetMetadata(ProjectFileConstants.Link, relativePathOfParent + linkedNode.Caption);
                }
                linkedNode.SetIsLinkedFile(true);
                linkedNode.OnInvalidateItems(node);

                // fire the node added event now that we've set the item metadata
                FireAddNodeEvent(files[index]);

                MoveFileToBottomIfNoOtherPendingMove(linkedNode);

                result[0] = VSADDRESULT.ADDRESULT_Success;
            }
            return VSConstants.S_OK;

        }

        /// <summary>
        /// for now used by add folder. Called on the ROOT, as only the project should need
        /// to implement this.
        /// for folders, called with parent folder, blank extension and blank suggested root
        /// </summary>
        public virtual int GenerateUniqueItemName(uint itemIdLoc, string ext, string suggestedRoot, out string itemName)
        {
            string rootName = "";
            string extToUse;
            int cb = 1;//force new items to have a number
            bool found = false;
            bool fFolderCase = false;
            HierarchyNode parent = this.NodeFromItemId(itemIdLoc);
            
            if (parent is FileNode)
                parent = parent.Parent;

            extToUse = ext.Trim();
            if (String.Compare(extToUse, ".config", StringComparison.OrdinalIgnoreCase) == 0 ||
                String.Compare(extToUse, ".manifest", StringComparison.OrdinalIgnoreCase) == 0)
            {
                cb = 0; // For these two extensions, do not have a number.
            }

            suggestedRoot = suggestedRoot.Trim();
            if (suggestedRoot.Length == 0)
            {
                // foldercase, we assume... 
                suggestedRoot = "NewFolder";
                fFolderCase = true;
            }

            while (!found)
            {
                rootName = suggestedRoot;
                if (cb > 0)
                    rootName += cb.ToString(CultureInfo.CurrentCulture);

                if (extToUse.Length > 0)
                {
                    rootName += extToUse;
                }

                cb++;
                found = true;
                for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling)
                {
                    if (rootName == n.GetEditLabel())
                    {
                        found = false;
                        break;
                    }

                    //if parent is a folder, we need the whole url
                    string parentFolder = parent.Url;
                    if (parent is ProjectNode)
                        parentFolder = Path.GetDirectoryName(parent.Url);

                    string checkFile = Path.Combine(parentFolder, rootName);

                    if (fFolderCase)
                    {
                        if (Directory.Exists(checkFile))
                        {
                            found = false;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(checkFile))
                        {
                            found = false;
                            break;
                        }
                    }
                }
            }

            itemName = rootName;
            return VSConstants.S_OK;
        }


        public virtual int GetItemContext(uint itemId, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            CCITracing.TraceCall();
            psp = null;
            HierarchyNode child = this.NodeFromItemId(itemId);
            if (child != null)
            {
                psp = child.OleServiceProvider as IOleServiceProvider;
            }
            return VSConstants.S_OK;
        }

        public virtual int IsDocumentInProject(string mkDoc, out int found, VSDOCUMENTPRIORITY[] pri, out uint itemId)
        {
            CCITracing.TraceCall();
            if (pri != null && pri.Length >= 1)
            {
                pri[0] = VSDOCUMENTPRIORITY.DP_Unsupported;
            }
            found = 0;
            itemId = 0;

            // If it is the project file just return.
            if (NativeMethods.IsSamePath(mkDoc, this.GetMkDocument()))
            {
                found = 1;
                itemId = VSConstants.VSITEMID_ROOT;
            }
            else
            {
                HierarchyNode child = this.FindChild(mkDoc);

        // 3504: we should only claim nodes that can edit its documents as our documents, so that our OpenItem does not fail for them.
        // Here we needlessly create a document manager, but it looks that IsDocumentInProject is called rarely enough for this not to be a problem,
        // and DocumentManager is not IDisposable, so there is no cleanup involved.
        // REVIEW: Shall we change the monikers (as returned by HierarchyNode.GetMkDocument()) so that for references they are not the paths and are 
        // not equal to referenced assembly/project file path?
                if (child != null && child.GetDocumentManager() != null)
                {
                    found = 1;
                    itemId = child.ID;
                }
            }

            if (found == 1)
            {
                if (pri != null && pri.Length >= 1)
                {
                    pri[0] = VSDOCUMENTPRIORITY.DP_Standard;
                }
            }

            return VSConstants.S_OK;

        }


        public virtual int OpenItem(uint itemId, ref Guid logicalView, IntPtr punkDocDataExisting, out IVsWindowFrame frame)
        {
            // Init output params
            frame = null;

            HierarchyNode n = this.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidItemId, CultureInfo.CurrentUICulture), "itemId");
            }

            // Delegate to the document manager object that knows how to open the item
            DocumentManager documentManager = n.GetDocumentManager();
            if (documentManager != null)
            {
                return documentManager.Open(ref logicalView, punkDocDataExisting, out frame, WindowFrameShowAction.DontShow);
            }

            // This node does not have an associated document manager and we must fail
            return VSConstants.E_FAIL;
        }


        public virtual int OpenItemWithSpecific(uint itemId, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame)
        {
            // Init output params
            frame = null;

            HierarchyNode n = this.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidItemId, CultureInfo.CurrentUICulture), "itemId");
            }

            // Delegate to the document manager object that knows how to open the item
            DocumentManager documentManager = n.GetDocumentManager();
            if (documentManager != null)
            {
                return documentManager.OpenWithSpecific(editorFlags, ref editorType, physicalView, ref logicalView, docDataExisting, out frame, WindowFrameShowAction.DontShow);
            }

            // This node does not have an associated document manager and we must fail
            return VSConstants.E_FAIL;
        }


        public virtual int RemoveItem(uint reserved, uint itemId, out int result)
        {
            HierarchyNode n = this.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidItemId, CultureInfo.CurrentUICulture), "itemId");
            }
            n.Remove(removeFromStorage: true, promptSave: false);
            result = 1;
            return VSConstants.S_OK;
        }


        public virtual int ReopenItem(uint itemId, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame)
        {
            // Init output params
            frame = null;

            HierarchyNode n = this.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.ParameterMustBeAValidItemId, CultureInfo.CurrentUICulture), "itemId");
            }

            // Delegate to the document manager object that knows how to open the item
            DocumentManager documentManager = n.GetDocumentManager();
            if (documentManager != null)
            {
                return documentManager.OpenWithSpecific(0, ref editorType, physicalView, ref logicalView, docDataExisting, out frame, WindowFrameShowAction.DontShow);
            }

            // This node does not have an associated document manager and we must fail
            return VSConstants.E_FAIL;
        }


        /// <summary>
        /// Implements IVsProject3::TransferItem
        /// This function is called when an open miscellaneous file is being transferred
        /// to our project. The sequence is for the shell to call AddItemWithSpecific and
        /// then use TransferItem to transfer the open document to our project.
        /// </summary>
        /// <param name="oldMkDoc">Old document name</param>
        /// <param name="newMkDoc">New document name</param>
        /// <param name="frame">Optional frame if the document is open</param>
        /// <returns></returns>
        public virtual int TransferItem(string oldMkDoc, string newMkDoc, IVsWindowFrame frame)
        {
            // Fail if hierarchy already closed
            if (this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return VSConstants.E_FAIL;
            }
            
            // Fail if the document names passed are null.
            if (oldMkDoc == null || newMkDoc == null)
                return VSConstants.E_INVALIDARG;

            int hr = VSConstants.S_OK;
            VSDOCUMENTPRIORITY[] priority = new VSDOCUMENTPRIORITY[1];
            uint itemid = VSConstants.VSITEMID_NIL;
            uint cookie = 0;
            uint grfFlags = 0;

            IVsRunningDocumentTable pRdt = GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (pRdt == null)
                return VSConstants.E_ABORT;

            string doc;
            int found;
            IVsHierarchy pHier;
            uint id, readLocks, editLocks;
            IntPtr docdataForCookiePtr = IntPtr.Zero;
            IntPtr docDataPtr = IntPtr.Zero;
            IntPtr hierPtr = IntPtr.Zero;

            // We get the document from the running doc table so that we can see if it is transient
            try
            {
                ErrorHandler.ThrowOnFailure(pRdt.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, oldMkDoc, out pHier, out id, out docdataForCookiePtr, out cookie));
            }
            finally
            {
                if (docdataForCookiePtr != IntPtr.Zero)
                    Marshal.Release(docdataForCookiePtr);
            }

            //Get the document info
            try
            {
                ErrorHandler.ThrowOnFailure(pRdt.GetDocumentInfo(cookie, out grfFlags, out readLocks, out editLocks, out doc, out pHier, out id, out docDataPtr));
            }
            finally
            {
                if (docDataPtr != IntPtr.Zero)
                    Marshal.Release(docDataPtr);
            }

            // Now see if the document is in the project. If not, we fail
            try
            {
                ErrorHandler.ThrowOnFailure(IsDocumentInProject(newMkDoc, out found, priority, out itemid));
                Debug.Assert(itemid != VSConstants.VSITEMID_NIL && itemid != VSConstants.VSITEMID_ROOT);
                hierPtr = Marshal.GetComInterfaceForObject(this, typeof(IVsUIHierarchy));
                // Now rename the document
                ErrorHandler.ThrowOnFailure(pRdt.RenameDocument(oldMkDoc, newMkDoc, hierPtr, itemid));
            }
            finally
            {
                if (hierPtr != IntPtr.Zero)
                    Marshal.Release(hierPtr);
            }

            //Change the caption if we are passed a window frame
            if (frame != null)
            {
                string caption = "%2";
                hr = frame.SetProperty((int)(__VSFPROPID.VSFPROPID_OwnerCaption), caption);
            }
            return hr;
        }

        public virtual int SetHostObject(string targetName, string taskName, object hostObject)
        {
            Debug.Assert(targetName != null && taskName != null && this.buildProject != null && this.buildProject.Targets != null);

            if (targetName == null || taskName == null || this.buildProject == null || this.buildProject.Targets == null)
            {
                return VSConstants.E_INVALIDARG;
            }


            var prjColl = this.buildProject.ProjectCollection;
            var hostSvc = prjColl.HostServices;
            hostSvc.RegisterHostObject(this.BuildProject.FullPath, targetName, taskName, (Microsoft.Build.Framework.ITaskHost)hostObject);
            return VSConstants.S_OK;
        }

        public int BuildTarget(string targetName, out bool success)
        {
            success = false;

            var result = this.Build(targetName);

            if (result.IsSuccessful)
            {
                success = true;
            }

            return VSConstants.S_OK;
        }

        public virtual int CancelBatchEdit()
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int EndBatchEdit()
        {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int StartBatchEdit()
        {
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Used to determine the kind of build system; BSK_MSBUILD_VS10 maps to 
        /// MSBuild 4.0
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        public virtual int GetBuildSystemKind(out uint kind)
        {
            kind = (uint)_BuildSystemKindFlags2.BSK_MSBUILD_VS10;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Add Components to the Project.
        /// Used by the environment to add components specified by the user in the Component Selector dialog 
        /// to the specified project
        /// </summary>
        /// <param name="dwAddCompOperation">The component operation to be performed.</param>
        /// <param name="cComponents">Number of components to be added</param>
        /// <param name="rgpcsdComponents">array of component selector data</param>
        /// <param name="hwndDialog">Handle to the component picker dialog</param>
        /// <param name="pResult">Result to be returned to the caller</param>
        public virtual int AddComponent(VSADDCOMPOPERATION dwAddCompOperation, uint cComponents, System.IntPtr[] rgpcsdComponents, System.IntPtr hwndDialog, VSADDCOMPRESULT[] pResult)
        {
            //initalize the out parameter
            pResult[0] = VSADDCOMPRESULT.ADDCOMPRESULT_Success;

            IReferenceContainer references = GetReferenceContainer();
            if (null == references)
            {
                // This project does not support references or the reference container was not created.
                // In both cases this operation is not supported.
                return VSConstants.E_NOTIMPL;
            }
            for (int cCount = 0; cCount < cComponents; cCount++)
            {
                VSCOMPONENTSELECTORDATA selectorData = new VSCOMPONENTSELECTORDATA();
                IntPtr ptr = rgpcsdComponents[cCount];
                selectorData = (VSCOMPONENTSELECTORDATA)Marshal.PtrToStructure(ptr, typeof(VSCOMPONENTSELECTORDATA));
                if (null == references.AddReferenceFromSelectorData(selectorData))
                {
                    //Skip further proccessing since a reference has to be added
                    pResult[0] = VSADDCOMPRESULT.ADDCOMPRESULT_Failure;
                    return VSConstants.S_OK;
                }
            }
            return VSConstants.S_OK;
        }

        public int EnumDependencies(out IVsEnumDependencies enumDependencies)
        {
            enumDependencies = new EnumDependencies(this.buildDependencyList);
            return VSConstants.S_OK;
        }

        public int OpenDependency(string szDependencyCanonicalName, out IVsDependency dependency)
        {
            dependency = null;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This method is called to determine which files should be placed under source control for a given VSITEMID within this hierarchy.
        /// </summary>
        /// <param name="itemid">Identifier for the VSITEMID being queried.</param>
        /// <param name="stringsOut">Pointer to an array of CALPOLESTR strings containing the file names for this item.</param>
        /// <param name="flagsOut">Pointer to a CADWORD array of flags stored in DWORDs indicating that some of the files have special behaviors.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int GetSccFiles(uint itemid, CALPOLESTR[] stringsOut, CADWORD[] flagsOut)
        {
            if (itemid == VSConstants.VSITEMID_SELECTION)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "itemid");
            }

            HierarchyNode n = this.NodeFromItemId(itemid);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "itemid");
            }

            List<string> files = new List<string>();
            List<tagVsSccFilesFlags> flags = new List<tagVsSccFilesFlags>();

            n.GetSccFiles(files, flags);

            if (stringsOut != null && stringsOut.Length > 0)
            {
                stringsOut[0] = Utilities.CreateCALPOLESTR(files);
            }

            if (flagsOut != null && flagsOut.Length > 0)
            {
                flagsOut[0] = Utilities.CreateCADWORD(flags);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// This method is called to discover special (hidden files) associated with a given VSITEMID within this hierarchy. 
        /// </summary>
        /// <param name="itemid">Identifier for the VSITEMID being queried.</param>
        /// <param name="sccFile">One of the files associated with the node</param>
        /// <param name="stringsOut">Pointer to an array of CALPOLESTR strings containing the file names for this item.</param>
        /// <param name="flagsOut">Pointer to a CADWORD array of flags stored in DWORDs indicating that some of the files have special behaviors.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        /// <remarks>This method is called to discover any special or hidden files associated with an item in the project hierarchy. It is called when GetSccFiles returns with the SFF_HasSpecialFiles flag set for any of the files associated with the node.</remarks>
        public virtual int GetSccSpecialFiles(uint itemid, string sccFile, CALPOLESTR[] stringsOut, CADWORD[] flagsOut)
        {
            if (itemid == VSConstants.VSITEMID_SELECTION)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "itemid");
            }

            HierarchyNode n = this.NodeFromItemId(itemid);
            if (n == null)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "itemid");
            }

            List<string> files = new List<string>();

            List<tagVsSccFilesFlags> flags = new List<tagVsSccFilesFlags>();

            n.GetSccSpecialFiles(sccFile, files, flags);

            if (stringsOut != null && stringsOut.Length > 0)
            {
                stringsOut[0] = Utilities.CreateCALPOLESTR(files);
            }

            if (flagsOut != null && flagsOut.Length > 0)
            {
                flagsOut[0] = Utilities.CreateCADWORD(flags);
            }

            return VSConstants.S_OK;

        }

        /// <summary>
        /// This method is called by the source control portion of the environment to inform the project of changes to the source control glyph on various nodes. 
        /// </summary>
        /// <param name="affectedNodes">Count of changed nodes.</param>
        /// <param name="itemidAffectedNodes">An array of VSITEMID identifiers of the changed nodes.</param>
        /// <param name="newGlyphs">An array of VsStateIcon glyphs representing the new state of the corresponding item in rgitemidAffectedNodes.</param>
        /// <param name="newSccStatus">An array of status flags from SccStatus corresponding to rgitemidAffectedNodes. </param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int SccGlyphChanged(int affectedNodes, uint[] itemidAffectedNodes, VsStateIcon[] newGlyphs, uint[] newSccStatus)
        {
            // if all the paramaters are null adn the count is 0, it means scc wants us to updated everything
            if (affectedNodes == 0 && itemidAffectedNodes == null && newGlyphs == null && newSccStatus == null)
            {
                this.ReDraw(UIHierarchyElement.SccState);
                this.UpdateSccStateIcons();
            }
            else if (affectedNodes > 0 && itemidAffectedNodes != null && newGlyphs != null && newSccStatus != null)
            {
                for (int i = 0; i < affectedNodes; i++)
                {
                    HierarchyNode n = this.NodeFromItemId(itemidAffectedNodes[i]);
                    if (n == null)
                    {
                        throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "itemidAffectedNodes");
                    }

                    n.ReDraw(UIHierarchyElement.SccState);
                }
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This method is called by the source control portion of the environment when a project is initially added to source control, or to change some of the project's settings.
        /// </summary>
        /// <param name="sccProjectName">String, opaque to the project, that identifies the project location on the server. Persist this string in the project file. </param>
        /// <param name="sccLocalPath">String, opaque to the project, that identifies the path to the server. Persist this string in the project file.</param>
        /// <param name="sccAuxPath">String, opaque to the project, that identifies the local path to the project. Persist this string in the project file.</param>
        /// <param name="sccProvider">String, opaque to the project, that identifies the source control package. Persist this string in the project file.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int SetSccLocation(string sccProjectName, string sccAuxPath, string sccLocalPath, string sccProvider)
        {
            if (this.IsSccDisabled)
            {
                throw new NotImplementedException();
            }

            if (sccProjectName == null)
            {
                throw new ArgumentNullException("sccProjectName");
            }

            if (sccAuxPath == null)
            {
                throw new ArgumentNullException("sccAuxPath");
            }

            if (sccLocalPath == null)
            {
                throw new ArgumentNullException("sccLocalPath");
            }

            if (sccProvider == null)
            {
                throw new ArgumentNullException("sccProvider");
            }

            // Save our settings (returns true if something changed)
            if (!this.SetSccSettings(sccProjectName, sccLocalPath, sccAuxPath, sccProvider))
            {
                return VSConstants.S_OK;
            }

            bool unbinding = (sccProjectName.Length == 0 && sccProvider.Length == 0);

            if (unbinding || this.QueryEditProjectFile(false))
            {
                this.buildProject.SetProperty(ProjectFileConstants.SccProjectName, sccProjectName);
                this.buildProject.SetProperty(ProjectFileConstants.SccProvider, sccProvider);
                this.buildProject.SetProperty(ProjectFileConstants.SccAuxPath, sccAuxPath);
                this.buildProject.SetProperty(ProjectFileConstants.SccLocalPath, sccLocalPath);
            }

            this.isRegisteredWithScc = true;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Allows you to query the project for special files and optionally create them. 
        /// </summary>
        /// <param name="fileId">__PSFFILEID of the file</param>
        /// <param name="flags">__PSFFLAGS flags for the file</param>
        /// <param name="itemid">The itemid of the node in the hierarchy</param>
        /// <param name="fileName">The file name of the special file.</param>
        /// <returns></returns>
        public virtual int GetFile(int fileId, uint flags, out uint itemid, out string fileName)
        {
            itemid = VSConstants.VSITEMID_NIL;
            fileName = String.Empty;

            // right now we only handle app.config

            if (fileId == (int)__PSFFILEID.PSFFILEID_AppConfig)
            {
                for (HierarchyNode x = this.FirstChild; x != null; x = x.NextSibling)
                {
                    if (x is FileNode)
                    {
                        FileNode y = (FileNode)x;
                        if (String.Equals(y.FileName, "app.config", StringComparison.OrdinalIgnoreCase))
                        {
                            itemid = y.ID;
                            fileName = y.Url;
                            return VSConstants.S_OK;
                        }
                    }
                }

                // ok, we couldn't find one - did they want us to create one?

                if ((flags & (uint)__PSFFLAGS.PSFF_CreateIfNotExist) > 0)
                {
                    string defaultValue = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<configuration>\r\n</configuration>";
                    string pathToAppConfig = Path.Combine(this.ProjectFolder, "App.config");
                    File.WriteAllText(pathToAppConfig, defaultValue);
                    HierarchyNode appConfig = this.AddNewFileNodeToHierarchy(this, pathToAppConfig);
                    Debug.Assert(appConfig is FileNode, "App.config is not a file?");
                    if (appConfig is FileNode)
                    {
                        FileNode y = (FileNode)appConfig;
                        itemid = y.ID;
                        fileName = y.Url;
                        FileNodeProperties props = y.NodeProperties as FileNodeProperties;
                        if (props != null)
                        {
                            props.CopyToOutputDirectory = CopyToOutputDirectory.Always;
                        }
                        return VSConstants.S_OK;
                    }
                }
            }

            // We need to return S_OK, otherwise the property page tabs will not be shown.
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Get the inner object of an aggregated hierarchy
        /// </summary>
        /// <returns>A HierarchyNode</returns>
        public virtual HierarchyNode GetInner()
        {
            return this;
        }

        public virtual IVsBuildDependency[] BuildDependencies
        {
            get
            {
                return this.buildDependencyList.ToArray();
            }
        }

        public virtual void AddBuildDependency(IVsBuildDependency dependency)
        {
            if (this.isClosed || dependency == null)
            {
                return;
            }

            if (!this.buildDependencyList.Contains(dependency))
            {
                this.buildDependencyList.Add(dependency);
            }
        }

        public virtual void RemoveBuildDependency(IVsBuildDependency dependency)
        {
            if (this.isClosed || dependency == null)
            {
                return;
            }

            if (this.buildDependencyList.Contains(dependency))
            {
                this.buildDependencyList.Remove(dependency);
            }
        }

        /// <summary>
        /// Returns the reference container node.
        /// </summary>
        /// <returns></returns>
        public IReferenceContainer GetReferenceContainer()
        {
            return this.FindChild(ReferenceContainerNode.ReferencesNodeVirtualName) as IReferenceContainer;
        }

        public bool IsProjectEventsListener
        {
            get { return this.isProjectEventsListener; }
            set { this.isProjectEventsListener = value; }
        }

        /// <summary>
        /// Defines the provider for the project events
        /// </summary>
        internal IProjectEvents ProjectEventsProvider
        {
            get
            {
                return this.projectEventsProvider;
            }
            set
            {
                if (null != this.projectEventsProvider)
                {
                    this.projectEventsProvider.AfterProjectFileOpened -= this.OnAfterProjectOpen;
                }
                this.projectEventsProvider = value;
                if (null != this.projectEventsProvider)
                {
                    this.projectEventsProvider.AfterProjectFileOpened += this.OnAfterProjectOpen;
                }
            }
        }

        /// <summary>
        /// Retrieve the list of project GUIDs that are aggregated together to make this project.
        /// </summary>
        /// <param name="projectTypeGuids">Semi colon separated list of Guids. Typically, the last GUID would be the GUID of the base project factory</param>
        /// <returns>HResult</returns>
        public int GetAggregateProjectTypeGuids(out string projectTypeGuids)
        {
            projectTypeGuids = this.GetProjectProperty(ProjectFileConstants.ProjectTypeGuids);
            // In case someone manually removed this from our project file, default to our project without flavors
            if (String.IsNullOrEmpty(projectTypeGuids))
                projectTypeGuids = this.ProjectGuid.ToString("B");
            return VSConstants.S_OK;
        }

        /// <summary>
        /// This is where the initialization occurs.
        /// </summary>
        public virtual int InitializeForOuter(string filename, string location, string name, uint flags, ref Guid iid, out IntPtr projectPointer, out int canceled)
        {
            canceled = 0;
            projectPointer = IntPtr.Zero;

            // initialize interop-safe properties that points to the outer object.
            // instance of project node should never be passed to the unmanaged code,
            // these properties should be used instead
            InteropSafeIVsHierarchy = GetOuterAs<IVsHierarchy>();
            InteropSafeIVsUIHierarchy = GetOuterAs<IVsUIHierarchy>();
            InteropSafeIVsProject = GetOuterAs<IVsProject>();
            InteropSafeIVsSccProject2 = GetOuterAs<IVsSccProject2>();
            InteropSafeIVsProjectFlavorCfgProvider = GetOuterAs<IVsProjectFlavorCfgProvider>();

            // Initialize the project
            this.Load(filename, location, name, flags, ref iid, out canceled);

            if (canceled != 1)
            {
                var pUnk = Marshal.GetIUnknownForObject(this);
                // Set ourself as the project
                try
                {
                    return Marshal.QueryInterface(pUnk, ref iid, out projectPointer);
                }
                finally
                {
                    if (pUnk != IntPtr.Zero) Marshal.Release(pUnk);
                }
            }

            return VSConstants.OLE_E_PROMPTSAVECANCELLED;
        }

        /// <summary>
        /// This is called after the project is done initializing the different layer of the aggregations
        /// </summary>
        /// <returns>HResult</returns>
        public virtual int OnAggregationComplete()
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Set the list of GUIDs that are aggregated together to create this project.
        /// </summary>
        /// <param name="projectTypeGuids">Semi-colon separated list of GUIDs, the last one is usually the project factory of the base project factory</param>
        /// <returns>HResult</returns>
        public int SetAggregateProjectTypeGuids(string projectTypeGuids)
        {
            this.SetProjectProperty(ProjectFileConstants.ProjectTypeGuids, projectTypeGuids);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// We are always the inner most part of the aggregation
        /// and as such we don't support setting an inner project
        /// </summary>
        public int SetInnerProject(object innerProject)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IVsProjectFlavorCfgProvider.CreateProjectFlavorCfg(IVsCfg pBaseProjectCfg, out IVsProjectFlavorCfg ppFlavorCfg)
        {
            // Our config object is also our IVsProjectFlavorCfg object
            ppFlavorCfg = pBaseProjectCfg as IVsProjectFlavorCfg;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Get the property of an item
        /// </summary>
        /// <param name="item">ItemID</param>
        /// <param name="attributeName">Name of the property</param>
        /// <param name="attributeValue">Value of the property (out parameter)</param>
        /// <returns>HRESULT</returns>
        int IVsBuildPropertyStorage.GetItemAttribute(uint item, string attributeName, out string attributeValue)
        {
            attributeValue = null;

            HierarchyNode node = NodeFromItemId(item);
            if (node == null)
                throw new ArgumentException("item");

            attributeValue = node.ItemNode.GetMetadata(attributeName);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Get the value of the property in the project file
        /// </summary>
        /// <param name="propertyName">Name of the property to remove</param>
        /// <param name="configName">Configuration for which to remove the property</param>
        /// <param name="storage">Project or user file (_PersistStorageType)</param>
        /// <param name="propertyValue">Value of the property (out parameter)</param>
        /// <returns>HRESULT</returns>
        int IVsBuildPropertyStorage.GetPropertyValue(string propertyName, string configName, uint storage, out string propertyValue)
        {
            // TODO: when adding support for User files, we need to update this method
            propertyValue = null;
            if (string.IsNullOrEmpty(configName))
            {
                propertyValue = this.GetProjectProperty(propertyName);
                if (propertyValue == null)
                    return unchecked((int)0x8004C738); // ERR_XML_ATTRIBUTE_NOT_FOUND
            }
            else
            {
                IVsCfg configurationInterface;
                string onlyConfigName, platformName;
                ConfigCanonicalName.TrySplitConfigurationCanonicalName(configName, out onlyConfigName, out platformName);
                ErrorHandler.ThrowOnFailure(this.ConfigProvider.GetCfgOfName(onlyConfigName, platformName, out configurationInterface));
                ProjectConfig config = (ProjectConfig)configurationInterface;
                propertyValue = config.GetConfigurationProperty(propertyName, true);
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Delete a property
        /// In our case this simply mean defining it as null
        /// </summary>
        /// <param name="propertyName">Name of the property to remove</param>
        /// <param name="configName">Configuration for which to remove the property</param>
        /// <param name="storage">Project or user file (_PersistStorageType)</param>
        /// <returns>HRESULT</returns>
        int IVsBuildPropertyStorage.RemoveProperty(string propertyName, string configName, uint storage)
        {
            return ((IVsBuildPropertyStorage)this).SetPropertyValue(propertyName, configName, storage, null);
        }

        /// <summary>
        /// Set a property on an item
        /// </summary>
        /// <param name="item">ItemID</param>
        /// <param name="attributeName">Name of the property</param>
        /// <param name="attributeValue">New value for the property</param>
        /// <returns>HRESULT</returns>
        int IVsBuildPropertyStorage.SetItemAttribute(uint item, string attributeName, string attributeValue)
        {
            HierarchyNode node = NodeFromItemId(item);

            if (node == null)
                throw new ArgumentException("item");

            node.ItemNode.SetMetadata(attributeName, attributeValue);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Set a project property
        /// </summary>
        /// <param name="propertyName">Name of the property to set</param>
        /// <param name="configName">Configuration for which to set the property</param>
        /// <param name="storage">Project file or user file (_PersistStorageType)</param>
        /// <param name="propertyValue">New value for that property</param>
        /// <returns>HRESULT</returns>
        int IVsBuildPropertyStorage.SetPropertyValue(string propertyName, string configName, uint storage, string propertyValue)
        {
            // TODO: when adding support for User files, we need to update this method
            if (string.IsNullOrEmpty(configName))
            {
                this.SetProjectProperty(propertyName, propertyValue);
            }
            else
            {
                var canonicalConfigName = new ConfigCanonicalName(configName);
                IVsCfg configurationInterface;
                ErrorHandler.ThrowOnFailure(this.ConfigProvider.GetCfgOfName(canonicalConfigName.ConfigName, canonicalConfigName.Platform, out configurationInterface));
                ProjectConfig config = (ProjectConfig)configurationInterface;
                config.SetConfigurationProperty(propertyName, propertyValue);
            }
            return VSConstants.S_OK;
        }

        public bool IsUsingMicrosoftNetSdk()
        {
            // Nasty hack to see if we are using dotnet sdk, the SDK team will add a property in the future.
            var c = GetProjectProperty("MSBuildAllProjects");
            if (!string.IsNullOrWhiteSpace(c))
            {
                return c.Contains("Microsoft.NET.Sdk.props");
            }
            return false;
        }

        public int UpgradeProject(uint grfUpgradeFlags)
        {
            if (!IsUsingMicrosoftNetSdk())
            {
                var hasTargetFramework = IsTargetFrameworkInstalled();
                if (!hasTargetFramework)
                {
                    hasTargetFramework = ShowRetargetingDialog();
                }
                // VSConstants.OLE_E_PROMPTSAVECANCELLED causes the shell to leave project unloaded
                return hasTargetFramework ? VSConstants.S_OK : VSConstants.OLE_E_PROMPTSAVECANCELLED;
            }
            return VSConstants.S_OK;
}

        /// <summary>
        /// Initialize projectNode
        /// </summary>
        private void Initialize()
        {
            this.ID = VSConstants.VSITEMID_ROOT;
            this.tracker = new TrackDocumentsHelper(this);
        }

        /// <summary>
        /// Add an item to the hierarchy based on the item path
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <returns>Added node</returns>
        private HierarchyNode AddIndependentFileNode(Microsoft.Build.Evaluation.ProjectItem item)
        {
            bool hasLink = !string.IsNullOrEmpty(MSBuildItem.GetMetadataValue(item, ProjectFileConstants.Link));
            if (!hasLink && !IsContainedWithinProjectDirectory(MSBuildItem.GetEvaluatedInclude(item)))
            {
                MSBuildItem.SetMetadataValue(item, ProjectFileConstants.Link, Path.GetFileName(MSBuildItem.GetEvaluatedInclude(item)));
                hasLink = true;
            }
            HierarchyNode currentParent = GetItemParentNode(item);
            LinkedFileNode node = AddFileNodeToNode(item, currentParent);
            node.SetIsLinkedFile(hasLink);
            return node;
        }

        /// <summary>
        /// Add a file node to the hierarchy
        /// </summary>
        /// <param name="item">msbuild item to add</param>
        /// <param name="parentNode">Parent Node</param>
        /// <returns>Added node</returns>
        private LinkedFileNode AddFileNodeToNode(Microsoft.Build.Evaluation.ProjectItem item, HierarchyNode parentNode)
        {
            LinkedFileNode node = this.CreateFileNode(new ProjectElement(this, item, false));
            parentNode.AddChild(node);
            return node; 
        }

        /// <summary>
        /// Get the parent node of an msbuild item
        /// </summary>
        /// <param name="item">msbuild item</param>
        /// <returns>parent node</returns>
        private HierarchyNode GetItemParentNode(Microsoft.Build.Evaluation.ProjectItem item)
        {
            HierarchyNode currentParent = this;
            string strPath = MSBuildItem.GetMetadataValue(item, ProjectFileConstants.Link);
            if (string.IsNullOrEmpty(strPath))
            {
                strPath = MSBuildItem.GetEvaluatedInclude(item);
            }

            strPath = Path.GetDirectoryName(strPath);
            if (strPath.Length > 0)
            {
                // Use the relative to verify the folders...
                currentParent = this.CreateFolderNodes(strPath);
            }
            return currentParent;
        }

        private Microsoft.Build.Evaluation.ProjectProperty GetMsBuildProperty(string propertyName, bool resetCache)
        {
            if (this.buildProject == null)
                throw new Exception(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FailedToRetrieveProperties, CultureInfo.CurrentUICulture), propertyName));

            if (resetCache && !isInBatchUpdate)
            {
                // Get properties from project file and cache it
                this.SetCurrentConfiguration();
                this.UpdateMSBuildState();
            }

            ReevaluateBuildProjectIfNecessary();

            // return property asked for
            return this.buildProject.GetProperty(propertyName);
        }

        private string GetOutputPath()
        {
            string outputPath = GetProjectProperty("OutputPath");

            if (!String.IsNullOrEmpty(outputPath))
            {
                outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);
                if (outputPath[outputPath.Length - 1] != Path.DirectorySeparatorChar)
                    outputPath += Path.DirectorySeparatorChar;
            }

            return outputPath;
        }

        private bool GetBoolAttr(string name)
        {
            string s = GetProjectProperty(name);

            return (s != null && s.ToUpperInvariant().Trim() == "TRUE");
        }

        private string GetAssemblyName()
        {
            string name = null;

            name = GetProjectProperty(ProjectFileConstants.AssemblyName);
            if (name == null)
                name = this.Caption;

            string outputtype = GetProjectProperty(ProjectFileConstants.OutputType, false);

            if (string.Equals(outputtype, "library", StringComparison.OrdinalIgnoreCase))
            {
                name += ".dll";
            }
            else
            {
                name += ".exe";
            }

            return name;
        }

        /// <summary>
        /// Updates our scc project settings. 
        /// </summary>
        /// <param name="sccProjectName">String, opaque to the project, that identifies the project location on the server. Persist this string in the project file. </param>
        /// <param name="sccLocalPath">String, opaque to the project, that identifies the path to the server. Persist this string in the project file.</param>
        /// <param name="sccAuxPath">String, opaque to the project, that identifies the local path to the project. Persist this string in the project file.</param>
        /// <param name="sccProvider">String, opaque to the project, that identifies the source control package. Persist this string in the project file.</param>
        /// <returns>Returns true if something changed.</returns>
        private bool SetSccSettings(string sccProjectName, string sccLocalPath, string sccAuxPath, string sccProvider)
        {
            bool changed = false;
            Debug.Assert(sccProjectName != null && sccLocalPath != null && sccAuxPath != null && sccProvider != null);
            if (String.Compare(sccProjectName, this.sccProjectName, StringComparison.OrdinalIgnoreCase) != 0 ||
                String.Compare(sccLocalPath, this.sccLocalPath, StringComparison.OrdinalIgnoreCase) != 0 ||
                String.Compare(sccAuxPath, this.sccAuxPath, StringComparison.OrdinalIgnoreCase) != 0 ||
                String.Compare(sccProvider, this.sccProvider, StringComparison.OrdinalIgnoreCase) != 0)
            {
                changed = true;
                this.sccProjectName = sccProjectName;
                this.sccLocalPath = sccLocalPath;
                this.sccAuxPath = sccAuxPath;
                this.sccProvider = sccProvider;
            }


            return changed;
        }

        /// <summary>
        /// Sets the scc info from the project file.
        /// </summary>
        private void InitSccInfo()
        {
            this.sccProjectName = this.GetProjectProperty(ProjectFileConstants.SccProjectName);
            this.sccLocalPath = this.GetProjectProperty(ProjectFileConstants.SccLocalPath);
            this.sccProvider = this.GetProjectProperty(ProjectFileConstants.SccProvider);
            this.sccAuxPath = this.GetProjectProperty(ProjectFileConstants.SccAuxPath);
        }

        private void OnAfterProjectOpen(object sender, AfterProjectFileOpenedEventArgs e)
        {
            this.projectOpened = true;
        }

        private static XmlElement WrapXmlFragment(XmlDocument document, XmlElement root, Guid flavor, string configuration, string fragment)
        {
            XmlElement node = document.CreateElement(ProjectFileConstants.FlavorProperties);
            XmlAttribute attribute = document.CreateAttribute(ProjectFileConstants.Guid);
            attribute.Value = flavor.ToString("B");
            node.Attributes.Append(attribute);
            if (!String.IsNullOrEmpty(configuration))
            {
                attribute = document.CreateAttribute(ProjectFileConstants.Configuration);
                attribute.Value = configuration;
                node.Attributes.Append(attribute);
            }
            node.InnerXml = fragment;
            root.AppendChild(node);
            return node;
        }

        internal int AddReferenceCouldNotBeAddedErrorMessage(string pathToReference)
        {
            string errorMessage = SR.GetString(SR.ErrorReferenceCouldNotBeAdded, pathToReference);
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// Sets the project guid from the project file. If no guid is found in the file, 
        /// and we're loading for the first time, a new guid is created and assigned for the instance project guid.
        /// </summary>
        private void SetProjectGuidFromProjectFile(bool reloading)
        {
            string projectGuid = this.GetProjectProperty(ProjectFileConstants.ProjectGuid);
            if (String.IsNullOrEmpty(projectGuid))
            {
                if (!reloading)  // don't change the existing Guid if 'Reload' and no guid in the .fsproj file
                {
                    this.projectIdGuid = Guid.NewGuid();
                }
            }
            else
            {
                Guid guid = new Guid(projectGuid);
                if (guid != this.projectIdGuid)
                {
                    this.projectIdGuid = guid;
                }
            }
        }

        /// <summary>
        /// Recusively parses the tree and closes all nodes, including "node".
        /// </summary>
        /// <param name="node">The subtree to close.</param>
        private static void CloseAllNodes(HierarchyNode node)
        {
            for (HierarchyNode n = node.FirstChild; n != null; n = n.NextSibling)
            {
                CloseAllNodes(n);
            }

            node.Close();
        }
        /// <summary>
        /// Recusively parses the tree and closes all nodes below "node", but don't close "node".
        /// </summary>
        /// <param name="node">The subtree to close.</param>
        private static void CloseAllSubNodes(HierarchyNode node)
        {
            for (HierarchyNode n = node.FirstChild; n != null; n = n.NextSibling)
            {
                CloseAllNodes(n);
            }
        }

        /// <summary>
        /// Debug method to assert that the project file and the solution explorer are in sync.
        /// </summary>
        [Conditional("DEBUG")]
        public virtual void EnsureMSBuildAndSolutionExplorerAreInSync()
        {
        }

        /// <summary>
        /// Get the project extensions
        /// </summary>
        /// <returns></returns>
        internal Microsoft.Build.Construction.ProjectExtensionsElement GetProjectExtensions()
        {
            foreach (Microsoft.Build.Construction.ProjectElement child in this.buildProject.Xml.ChildrenReversed)
            {
                Microsoft.Build.Construction.ProjectExtensionsElement extensions = child as Microsoft.Build.Construction.ProjectExtensionsElement;

                if (extensions != null)
                {
                    return extensions;
                }
            }

            return null;
        }

        public void SetProjectExtensions(string id, string xmlText)
        {
            Microsoft.Build.Construction.ProjectExtensionsElement element = GetProjectExtensions();

            // If it doesn't already have a value and we're asked to set it to
            // nothing, don't do anything. Same as old OM. Keeps project neat.
            if (element == null)
            {
                if (xmlText.Length == 0)
                {
                    return;
                }

                element = this.buildProject.Xml.CreateProjectExtensionsElement();
                this.buildProject.Xml.AppendChild(element);
            }

            element[id] = xmlText;
        }

        public bool IsProjectOpened { get { return this.projectOpened;  } }
        internal ExtensibilityEventsHelper ExtensibilityEventsHelper 
        {
            get { return myExtensibilityEventsHelper; }
        }


        private bool IsTargetFrameworkInstalled()
        {
           var targetFrameworkMoniker = new System.Runtime.Versioning.FrameworkName(GetTargetFrameworkMoniker());
           //Only check for .NetFramework. The expectation is that other frameworks will perform any checks themselves.
           if (targetFrameworkMoniker.Identifier != ".NetFramework")
              return true;

           var multiTargeting = this.site.GetService(typeof(SVsFrameworkMultiTargeting)) as IVsFrameworkMultiTargeting;
           Array frameworks;
           Marshal.ThrowExceptionForHR(multiTargeting.GetSupportedFrameworks(out frameworks));
           foreach (string fx in frameworks)
           {
               uint compat;
               int hr = multiTargeting.CheckFrameworkCompatibility(targetFrameworkMoniker.FullName, fx, out compat);
               if (hr < 0)
               {
                   break;
               }
    
               if ((__VSFRAMEWORKCOMPATIBILITY)compat == __VSFRAMEWORKCOMPATIBILITY.VSFRAMEWORKCOMPATIBILITY_COMPATIBLE)
               {
                   return true;
               }
           }
    
           return false;
        }

        private bool IsIdeInCommandLineMode()
        {
            bool cmdline = false;
            var shell = this.site.GetService(typeof(SVsShell)) as IVsShell;
            if (shell != null)
            {
                object obj;
                Marshal.ThrowExceptionForHR(shell.GetProperty((int)__VSSPROPID.VSSPROPID_IsInCommandLineMode, out obj));
                cmdline = (bool)obj;
            }
            return cmdline;
        }

        private bool ShowRetargetingDialog()
        {
            var retargetDialog = this.site.GetService(typeof(SVsFrameworkRetargetingDlg)) as IVsFrameworkRetargetingDlg;
            var targetFrameworkMoniker = new System.Runtime.Versioning.FrameworkName(GetTargetFrameworkMoniker());
            if (retargetDialog == null)
            {
                throw new InvalidOperationException("Missing SVsFrameworkRetargetingDlg service.");
            }
 
            // We can only display the retargeting dialog if the IDE is not in command-line mode.
            if (IsIdeInCommandLineMode())
            {
                string message = SR.GetString(SR.CannotLoadUnknownTargetFrameworkProject, this.ProjectFile, targetFrameworkMoniker);
                var outputWindow = this.site.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    IVsOutputWindowPane outputPane;
                    Guid outputPaneGuid = VSConstants.GUID_BuildOutputWindowPane;
                    if (outputWindow.GetPane(ref outputPaneGuid, out outputPane) >= 0 && outputPane != null)
                    {
                        Marshal.ThrowExceptionForHR(outputPane.OutputString(message));
                    }
                }
 
                throw new InvalidOperationException(message);
            }
            else
            {
                uint outcome;
                int dontShowAgain;
                Marshal.ThrowExceptionForHR(retargetDialog.ShowFrameworkRetargetingDlg(
                    SR.GetString(SR.ProductName),
                    this.ProjectFile,
                    targetFrameworkMoniker.FullName,
                    (uint)__FRD_FLAGS.FRDF_DEFAULT,
                    out outcome,
                    out dontShowAgain));
                switch ((__FRD_OUTCOME)outcome)
                {
                    case __FRD_OUTCOME.FRDO_GOTO_DOWNLOAD_SITE:
                        Marshal.ThrowExceptionForHR(retargetDialog.NavigateToFrameworkDownloadUrl());
                        return false;
                    case __FRD_OUTCOME.FRDO_LEAVE_UNLOADED:
                        return false;
                    case __FRD_OUTCOME.FRDO_RETARGET_TO_40:
                        // If we are retargeting to 4.0, then set the flag to set the appropriate Target Framework.
                        // This will dirty the project file, so we check it out of source control now -- so that
                        // the user can associate getting the checkout prompt with the "No Framework" dialog.
                        if (QueryEditProjectFile(false /* bSuppressUI */))
                        {
                            var retargetingService = this.site.GetService(typeof(SVsTrackProjectRetargeting)) as IVsTrackProjectRetargeting;
                            // We surround our batch retargeting request with begin/end because in individual project load
                            // scenarios the solution load context hasn't done it for us.
                            Marshal.ThrowExceptionForHR(retargetingService.BeginRetargetingBatch());
                            Marshal.ThrowExceptionForHR(retargetingService.BatchRetargetProject(this.InteropSafeIVsHierarchy, DefaultTargetFrameworkMoniker.FullName, true));
                            Marshal.ThrowExceptionForHR(retargetingService.EndRetargetingBatch());

                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    default:
                        throw new ArgumentException("Unexpected outcome from retargeting dialog.");
                }
            }
        }

        int IVsDesignTimeAssemblyResolution.GetTargetFramework(out string targetFramework)
        {
            targetFramework = GetTargetFrameworkMoniker();
            return VSConstants.S_OK;
        }

        Tuple<uint, int> ResolveAssemblyPathInTargetFxImpl(string[] prgAssemblySpecs, uint cAssembliesToResolve, VsResolvedAssemblyPath[] prgResolvedAssemblyPaths)
        {
            string target = "DesignTimeResolveAssemblyReferences";  // a.k.a. "DTAR"
            if (!this.buildProject.Targets.ContainsKey(target))
            {
                return new Tuple<uint, int>(0, VSConstants.E_FAIL);
            }

            var projectInstance = this.buildProject.CreateProjectInstance();
            var extraProperties = new KeyValuePair<string,string>[] 
            {
                new KeyValuePair<string, string>("DesignTimeBuild", "true"),
                new KeyValuePair<string, string>("DesignTimeReference", String.Join(";", prgAssemblySpecs))
            }.AsEnumerable();

            BuildSubmission submission = DoMSBuildSubmission(BuildKind.SYNC, target, ref projectInstance, null, extraProperties);
            if (submission.BuildResult.OverallResult != BuildResultCode.Success)
            {
                // can fail, e.g. if call happens during project unload/close, in which case failure is ok
                return new Tuple<uint, int>(0, VSConstants.E_FAIL);
            }
            uint count = 0u;
            foreach (var item in projectInstance.GetItems("DesignTimeReferencePath"))
            {
                GetResolvedAssemblyReferencePathForDesignTime(item, count, prgResolvedAssemblyPaths);
                count++;
            }
            return new Tuple<uint, int>(count, VSConstants.S_OK);
            
        }
        // all of the logic for the following methods
        //     ResolveAssemblyPathInTargetFx
        //     GetResolvedAssemblyReferencePathForDesignTime
        //     ValidateResolvedAssembly
        //     ValidateResolvedAsmVersion
        // comes from ResolveReferencesForDesignTime() et al. in
        //     \\ddindex2\sources_tfs\Dev10_Main\vsproject\langbuild\langref.cpp
        int IVsDesignTimeAssemblyResolution.ResolveAssemblyPathInTargetFx(string[] prgAssemblySpecs, uint cAssembliesToResolve, VsResolvedAssemblyPath[] prgResolvedAssemblyPaths, out uint pcResolvedAssemblyPaths)
        {
            Tuple<uint, int> result = UIThread.DoOnUIThread(() => ResolveAssemblyPathInTargetFxImpl(prgAssemblySpecs, cAssembliesToResolve, prgResolvedAssemblyPaths));
            pcResolvedAssemblyPaths = result.Item1;
            return result.Item2;
        }
        void GetResolvedAssemblyReferencePathForDesignTime(Microsoft.Build.Execution.ProjectItemInstance item, uint count, VsResolvedAssemblyPath[] prgResolvedAssemblyPaths)
        {
            prgResolvedAssemblyPaths[count].bstrOrigAssemblySpec = item.GetMetadataValue("OriginalItemSpec");
            if (ValidateResolvedAssembly(item))
            {
                prgResolvedAssemblyPaths[count].bstrResolvedAssemblyPath = item.EvaluatedInclude;
            }
            else
            {
                prgResolvedAssemblyPaths[count].bstrResolvedAssemblyPath = null;
            }
        }
        bool ValidateResolvedAssembly(Microsoft.Build.Execution.ProjectItemInstance item)
        {
            if (!ValidateResolvedAsmVersion(item))
                return false;
            string oord = item.GetMetadataValue("OutOfRangeDependencies");
            if (!String.IsNullOrEmpty(oord))
            {
                // This metadata is a semi-colon delimited list of dependent assembly names which target
                // a higher framework. If this metadata is NOT EMPTY then
                // the current assembly does have dependencies which are greater than the current target framework

                // so let's exclude this assembly
                return false;
            }
            return true;
        }
        bool ValidateResolvedAsmVersion(Microsoft.Build.Execution.ProjectItemInstance item)
        {
            string highestRedistVer = item.GetMetadataValue("HighestVersionInRedist");
            if (!string.IsNullOrEmpty(highestRedistVer))
            {
                string asmVer = item.GetMetadataValue("Version");
                if (!string.IsNullOrEmpty(asmVer))
                {
                    //if assembly version is greater than highest redist version then the assembly version is invalid for the redist
                    if ((new System.Version(asmVer)).CompareTo(new System.Version(highestRedistVer)) > 0)
                        return false;
                }
            }
            return true; //if metadata is not available then consider valid
        }

        /// <summary>
        /// Get the outer T implementation.
        /// </summary>
        private T GetOuterAs<T>()
            where T : class
        {
            T hierarchy = null;
            // The hierarchy of a node is its project node hierarchy
            IntPtr projectUnknown =  Marshal.GetIUnknownForObject(this);
            try
            {
                hierarchy = (T)Marshal.GetTypedObjectForIUnknown(projectUnknown, typeof(T));
            }
            finally
            {
                if (projectUnknown != IntPtr.Zero)
                {
                    Marshal.Release(projectUnknown);
                }
            }
            return hierarchy;
        }

        public int HandoffItem(uint itemid, IVsProject3 pProjDest, string pszMkDocumentOld, string pszMkDocumentNew, IVsWindowFrame punkWindowFrame)
        {
            if (pProjDest == null)
            {
                return VSConstants.E_POINTER;
            }

            return pProjDest.TransferItem(pszMkDocumentOld, pszMkDocumentNew, punkWindowFrame);
        }
    }
    internal enum AddItemContext
    {
        Unknown = 0,
        Paste = 1
    }

}
