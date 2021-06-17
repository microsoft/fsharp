// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    using System;
    using System.Reflection;
    using System.Globalization;
    using System.Resources;
    using System.Text;
    using System.Threading;
    using System.ComponentModel;
    using System.Security.Permissions;

    [AttributeUsage(AttributeTargets.All)]
    public sealed class SRDescriptionAttribute : DescriptionAttribute
    {

        private bool replaced = false;

        public SRDescriptionAttribute(string description)
            : base(description)
        {
        }

        public override string Description
        {
            get
            {
                if (!replaced)
                {
                    replaced = true;
                    DescriptionValue = SR.GetString(base.Description, CultureInfo.CurrentUICulture);
                }
                return base.Description;
            }
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class SRCategoryAttribute : CategoryAttribute
    {

        public SRCategoryAttribute(string category)
            : base(category)
        {
        }

        protected override string GetLocalizedString(string value)
        {
            return SR.GetString(value, CultureInfo.CurrentUICulture);
        }
    }
    public sealed class SR
    {
        public const string AddToNullProjectError = "AddToNullProjectError";
        public const string Advanced = "Advanced";
        public const string AssemblyReferenceAlreadyExists = "AssemblyReferenceAlreadyExists";
        public const string AttributeLoad = "AttributeLoad";
        public const string BuildAction = "BuildAction";
        public const string BuildActionDescription = "BuildActionDescription";
        public const string BuildCaption = "BuildCaption";
        public const string BuildVerbosity = "BuildVerbosity";
        public const string BuildVerbosityDescription = "BuildVerbosityDescription";
        public const string BuildEventError = "BuildEventError";
        public const string CancelQueryEdit = "CancelQueryEdit";
        public const string CannotAddFileThatIsOpenInEditor = "CannotAddFileThatIsOpenInEditor";
        public const string CannotAddItemToProjectWithWildcards = "CannotAddItemToProjectWithWildcards";
        public const string CanNotSaveFileNotOpeneInEditor = "CanNotSaveFileNotOpeneInEditor";
        public const string CannotStartLibraries = "CannotStartLibraries";
        public const string cli1 = "cli1";
        public const string Compile = "Compile";
        public const string ConfirmExtensionChange = "ConfirmExtensionChange";
        public const string Content = "Content";
        public const string CopyAlways = "CopyAlways";
        public const string CopyIfNewer = "CopyIfNewer";
        public const string CopyToLocal = "CopyToLocal";
        public const string CopyToLocalDescription = "CopyToLocalDescription";
        public const string CopyToOutputDirectory = "CopyToOutputDirectory";
        public const string CopyToOutputDirectoryDescription = "CopyToOutputDirectoryDescription";
        public const string CustomTool = "CustomTool";
        public const string CustomToolDescription = "CustomToolDescription";
        public const string CustomToolNamespace = "CustomToolNamespace";
        public const string CustomToolNamespaceDescription = "CustomToolNamespaceDescription";
        public const string DetailsImport = "DetailsImport";
        public const string DetailsUserImport = "DetailsUserImport";
        public const string DetailsItem = "DetailsItem";
        public const string DetailsItemLocation = "DetailsItemLocation";
        public const string DetailsProperty = "DetailsProperty";
        public const string DetailsTarget = "DetailsTarget";
        public const string DetailsUsingTask = "DetailsUsingTask";
        public const string Detailed = "Detailed";
        public const string Diagnostic = "Diagnostic";
        public const string DirectoryExistError = "DirectoryExistError";
        public const string DoNotCopy = "DoNotCopy";
        public const string EditorViewError = "EditorViewError";
        public const string EmbeddedResource = "EmbeddedResource";
        public const string Error = "Error";
        public const string ErrorInvalidFileName = "ErrorInvalidFileName";
        public const string ErrorInvalidProjectName = "ErrorInvalidProjectName";
        public const string ErrorReferenceCouldNotBeAdded = "ErrorReferenceCouldNotBeAdded";
        public const string ErrorMsBuildRegistration = "ErrorMsBuildRegistration";
        public const string ErrorSaving = "ErrorSaving";
        public const string Exe = "Exe";
        public const string ExpectedObjectOfType = "ExpectedObjectOfType";
        public const string FailedToGetService = "FailedToGetService";
        public const string FailedToRetrieveProperties = "FailedToRetrieveProperties";
        public const string FileNameCannotContainALeadingPeriod = "FileNameCannotContainALeadingPeriod";
        public const string FileCannotBeRenamedToAnExistingFile = "FileCannotBeRenamedToAnExistingFile";
        public const string FileAlreadyExistsAndCannotBeRenamed = "FileAlreadyExistsAndCannotBeRenamed";
        public const string FileAlreadyExists = "FileAlreadyExists";
        public const string FileAlreadyExistsCaption = "FileAlreadyExistsCaption";
        public const string FileAlreadyInProject = "FileAlreadyInProject";
        public const string FileAlreadyInProjectCaption = "FileAlreadyInProjectCaption";
        public const string FileCopyError = "FileCopyError";
        public const string FileName = "FileName";
        public const string FileNameDescription = "FileNameDescription";
        public const string FileOrFolderAlreadyExists = "FileOrFolderAlreadyExists";
        public const string FolderCannotBeRenamed = "FolderCannotBeRenamed";
        public const string FileOrFolderCannotBeFound = "FileOrFolderCannotBeFound";
        public const string FileProperties = "FileProperties";
        public const string FolderName = "FolderName";
        public const string FolderNameDescription = "FolderNameDescription";
        public const string FolderProperties = "FolderProperties";
        public const string FullPath = "FullPath";
        public const string FullPathDescription = "FullPathDescription";
        public const string ItemDoesNotExistInProjectDirectory = "ItemDoesNotExistInProjectDirectory";
        public const string InvalidAutomationObject = "InvalidAutomationObject";
        public const string InvalidLoggerType = "InvalidLoggerType";
        public const string InvalidParameter = "InvalidParameter";
        public const string Library = "Library";
        public const string LinkedItemsAreNotSupported = "LinkedItemsAreNotSupported";
        public const string Minimal = "Minimal";
        public const string Misc = "Misc";
        public const string None = "None";
        public const string NoWildcardsInProject = "NoWildcardsInProject";
        public const string NoZeroImpactProjects = "NoZeroImpactProjects";
        public const string Normal = "Normal";
        public const string NestedProjectFailedToReload = "NestedProjectFailedToReload";
        public const string OutputPath = "OutputPath";
        public const string OutputPathDescription = "OutputPathDescription";
        public const string PasteFailed = "PasteFailed";
        public const string ParameterMustBeAValidGuid = "ParameterMustBeAValidGuid";
        public const string ParameterMustBeAValidItemId = "ParameterMustBeAValidItemId";
        public const string ParameterCannotBeNullOrEmpty = "ParameterCannotBeNullOrEmpty";
        public const string PathTooLong = "PathTooLong";
        public const string ProjectContainsCircularReferences = "ProjectContainsCircularReferences";
        public const string ProjectReferencesDifferentFramework = "ProjectReferencesDifferentFramework";
        public const string Program = "Program";
        public const string Project = "Project";
        public const string ProjectFile = "ProjectFile";
        public const string ProjectFileDescription = "ProjectFileDescription";
        public const string ProjectFolder = "ProjectFolder";
        public const string ProjectFolderDescription = "ProjectFolderDescription";
        public const string ProjectProperties = "ProjectProperties";
        public const string Quiet = "Quiet";
        public const string QueryReloadNestedProject = "QueryReloadNestedProject";
        public const string ReferenceCouldNotBeAdded = "ReferenceCouldNotBeAdded";
        public const string ReferenceAlreadyExists = "ReferenceAlreadyExists";
        public const string ReferenceWithAssemblyNameAlreadyExists = "ReferenceWithAssemblyNameAlreadyExists";
        public const string ReferencesNodeName = "ReferencesNodeName";
        public const string ReferenceProperties = "ReferenceProperties";
        public const string RefName = "RefName";
        public const string RefNameDescription = "RefNameDescription";
        public const string RenameFolder = "RenameFolder";
        public const string RTL = "RTL";
        public const string SaveCaption = "SaveCaption";
        public const string SaveModifiedDocuments = "SaveModifiedDocuments";
        public const string SaveOfProjectFileOutsideCurrentDirectory = "SaveOfProjectFileOutsideCurrentDirectory";
        public const string SpecificVersion = "SpecificVersion";
        public const string SpecificVersionDescription = "SpecificVersionDescription";
        public const string StandardEditorViewError = "StandardEditorViewError";
        public const string URL = "URL";
        public const string UseOfDeletedItemError = "UseOfDeletedItemError";
        public const string v1 = "v1";
        public const string v11 = "v11";
        public const string v2 = "v2";
        public const string Warning = "Warning";
        public const string WinExe = "WinExe";
        public const string UpgradeCannotOpenProjectFileForEdit = "UpgradeCannotOpenProjectFileForEdit";
        public const string UpgradeNoNeedToUpgradeAfterCheckout = "UpgradeNoNeedToUpgradeAfterCheckout";        
        public const string InvalidOutputPath = "InvalidOutputPath";
        public const string ProjectRefOnlyExeOrDll = "ProjectRefOnlyExeOrDll";
        public const string CannotBuildWhenBuildInProgress = "CannotBuildWhenBuildInProgress";
        public const string WorkingDirectoryNotExists = "WorkingDirectoryNotExists";
        public const string CannotLoadUnknownTargetFrameworkProject = "CannotLoadUnknownTargetFrameworkProject";
        public const string ProductName = "ProductName";
        public const string ProjectReferencesHigherVersionWarning = "ProjectReferencesHigherVersionWarning";
        public const string ProjectConversionNotRequired = "ProjectConversionNotRequired";
        public const string ConversionNotRequired = "ConversionNotRequired";
        public const string ErrorMakingProjectBackup = "ErrorMakingProjectBackup";
        public const string ProjectBackupSuccessful = "ProjectBackupSuccessful";
        public const string BackupNameConflict = "BackupNameConflict";
        public const string ProjectContainsLinkedFile = "ProjectContainsLinkedFile";
        public const string ErrorMakingBackup = "ErrorMakingBackup";
        public const string BackupSuccessful = "BackupSuccessful";
        public const string Identity = "Identity";
        public const string IdentityDescription = "IdentityDescription";
        public const string Path = "Path";
        public const string PathDescription = "PathDescription";
        public const string Version = "Version";
        public const string VersionDescription = "VersionDescription";
        public const string CopyOf = "CopyOf";
        public const string CopyOf2 = "CopyOf2";

        static SR loader = null;
        ResourceManager resources;

        private static Object s_InternalSyncObject;
        private static Object InternalSyncObject
        {
            get
            {
                if (s_InternalSyncObject == null)
                {
                    Object o = new Object();
                    Interlocked.CompareExchange(ref s_InternalSyncObject, o, null);
                }
                return s_InternalSyncObject;
            }
        }

        public SR()
        {
            resources = new System.Resources.ResourceManager("Microsoft.VisualStudio.Package.Project", this.GetType().Assembly);
        }

        private static SR GetLoader()
        {
            if (loader == null)
            {
                lock (InternalSyncObject)
                {
                    if (loader == null)
                    {
                        loader = new SR();
                    }
                }
            }

            return loader;
        }

        private static CultureInfo Culture
        {
            get { return null/*use ResourceManager default, CultureInfo.CurrentUICulture*/; }
        }

        public static ResourceManager Resources
        {
            get
            {
                return GetLoader().resources;
            }
        }

        public static string GetString(string name, params object[] args)
        {
            SR sys = GetLoader();
            if (sys == null)
                return null;
            string res = sys.resources.GetString(name, SR.Culture);

            if (args != null && args.Length > 0)
            {
                return String.Format(CultureInfo.CurrentCulture, res, args);
            }
            else
            {
                return res;
            }
        }

        public static string GetString(string name)
        {
            SR sys = GetLoader();
            if (sys == null)
                return null;
            return sys.resources.GetString(name, SR.Culture);
        }

        public static string GetString(string name, CultureInfo culture)
        {
            SR sys = GetLoader();
            if (sys == null)
                return null;
            return sys.resources.GetString(name, culture);
        }

        public static string GetStringWithCR(string name)
        {
            var s = GetString(name);
            return s.Replace(@"\n", Environment.NewLine);
        }

        public static object GetObject(string name)
        {
            SR sys = GetLoader();
            if (sys == null)
                return null;
            return sys.resources.GetObject(name, SR.Culture);
        }
    }
}
