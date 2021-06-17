// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Defines the constant strings for various msbuild targets
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
    internal static class MsBuildTarget
    {
        public const string ResolveProjectReferences = "ResolveProjectReferences";
        public const string ResolveAssemblyReferences = "ResolveAssemblyReferences";
        public const string ResolveComReferences = "ResolveComReferences";
        public const string Build = "Build";
        public const string Rebuild = "ReBuild";
        public const string Clean = "Clean";
        public const string ImplicitlyExpandTargetFramework = "ImplicitlyExpandTargetFramework";
    }

    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms")]
    internal static class MsBuildGeneratedItemType
    {
        public const string ReferenceCopyLocalPaths = "ReferenceCopyLocalPaths";
        public const string ComReferenceWrappers = "ComReferenceWrappers";
    }

    /// <summary>
    /// Defines the constant strings used with project files.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "COM")]
    internal static class ProjectFileConstants
    {
        public const string Include = "Include";
        public const string Name = "Name";
        public const string HintPath = "HintPath";
        public const string AssemblyName = "AssemblyName";
        public const string FinalOutputPath = "FinalOutputPath";
        public const string Project = "Project";
        public const string LinkedIntoProjectAt = "LinkedIntoProjectAt";
        public const string Link = "Link";
        public const string TypeGuid = "TypeGuid";
        public const string InstanceGuid = "InstanceGuid";
        public const string Private = "Private";
        public const string ProjectReference = "ProjectReference";
        public const string Reference = "Reference";
        public const string WebReference = "WebReference";
        public const string WebReferenceFolder = "WebReferenceFolder";
        public const string Folder = "Folder";
        public const string Content = "Content";
        public const string None = "None";
        public const string EmbeddedResource = "EmbeddedResource";
        public const string Resource = "Resource";
        public const string RootNamespace = "RootNamespace";
        public const string OutputType = "OutputType";
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "SubType")]
        public const string SubType = "SubType";
        public const string DependentUpon = "DependentUpon";
        public const string Compile = "Compile";
        public const string RawFileName = "{RawFileName}";
        public const string ResolvedFrom = "ResolvedFrom";
        public const string ReferencePath = "ReferencePath";
        public const string ResolvedProjectReferencePaths = "ResolvedProjectReferencePaths";
        public const string Configuration = "Configuration";
        public const string Platform = "Platform";
        public const string AvailablePlatforms = "AvailablePlatforms";
        public const string AvailableItemName = "AvailableItemName";
        public const string BuildVerbosity = "BuildVerbosity";
        public const string Template = "Template";
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "SubProject")]
        public const string SubProject = "SubProject";
        public const string BuildAction = "BuildAction";
        public const string CopyToOutputDirectory = "CopyToOutputDirectory";
        public const string SpecificVersion = "SpecificVersion";
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "COM")]
        public const string COMReference = "COMReference";
        public const string Guid = "Guid";
        public const string VersionMajor = "VersionMajor";
        public const string VersionMinor = "VersionMinor";
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Lcid")]
        public const string Lcid = "Lcid";
        public const string Isolated = "Isolated";
        public const string WrapperTool = "WrapperTool";
        public const string BuildingInsideVisualStudio = "BuildingInsideVisualStudio";
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public const string SccProjectName = "SccProjectName";
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public const string SccLocalPath = "SccLocalPath";
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public const string SccAuxPath = "SccAuxPath";
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        public const string SccProvider = "SccProvider";
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectTypeGuids = "ProjectTypeGuids";
        public const string Generator = "Generator";
        public const string CustomToolNamespace = "CustomToolNamespace";
        public const string FlavorProperties = "FlavorProperties";
        public const string VisualStudio = "VisualStudio";
        public const string User = "User";
        public const string TargetFrameworkVersion = "TargetFrameworkVersion";
        public const string TargetFrameworkProfile = "TargetFrameworkProfile";
        public const string TargetFrameworkMoniker = "TargetFrameworkMoniker";
        public const string Win32Resource = "Win32Resource";
        public const string ApplicationIcon = "ApplicationIcon";
        public const string ApplicationManifest = "ApplicationManifest";
        public const string StartupObject = "StartupObject";
        public const string StartURL = "StartURL";
        public const string StartArguments = "StartArguments";
        public const string StartWorkingDirectory = "StartWorkingDirectory";
        public const string StartProgram = "StartProgram";
        public const string StartAction = "StartAction";
        public const string EnableSQLServerDebugging = "EnableSQLServerDebugging";
        public const string EnableUnmanagedDebugging = "EnableUnmanagedDebugging";
        public const string RemoteDebugMachine = "RemoteDebugMachine";
        public const string RemoteDebugEnabled = "RemoteDebugEnabled";
        public const string UseVSHostingProcess = "UseVSHostingProcess";
        public const string DebugSymbols = "DebugSymbols";
        public const string DebugType = "DebugType";
        public const string Optimize = "Optimize";
        public const string Tailcalls = "Tailcalls";
        public const string UseStandardResourceNames = "UseStandardResourceNames";
        public const string Prefer32Bit = "Prefer32Bit";
        public const string OutputPath = "OutputPath";
        public const string DefineConstants = "DefineConstants";
        public const string WarningLevel = "WarningLevel";
        public const string NoWarn = "NoWarn";
        public const string TreatWarningsAsErrors = "TreatWarningsAsErrors";
        public const string WarningsAsErrors = "WarningsAsErrors";
        public const string WarningsNotAsErrors = "WarningsNotAsErrors";
        public const string DocumentationFile = "DocumentationFile";
        public const string OtherFlags = "OtherFlags";
        public const string PlatformTarget = "PlatformTarget";
        public const string PreBuildEvent = "PreBuildEvent";
        public const string PostBuildEvent = "PostBuildEvent";
        public const string RunPostBuildEvent = "RunPostBuildEvent";
        public const string EmbedInteropTypes = "EmbedInteropTypes";
        public const string CopyLocal = "CopyLocal";

        public const string AllProjectOutputGroups = "AllProjectOutputGroups";
        public const string TargetPath = "TargetPath";
        public const string TargetDir = "TargetDir";
        public const string DefaultNamespace = "DefaultNamespace";
        public const string TargetFSharpCoreVersion = "TargetFSharpCoreVersion";
        public const string TargetFSharpCoreVersionProperty = "$(TargetFSharpCoreVersion)";
    }

    internal static class ProjectFileAttributeValue
    {
        public const string Code = "Code";
        public const string Form = "Form";
        public const string Component = "Component";
        public const string Designer = "Designer";
        public const string UserControl = "UserControl";
    }

    internal static class ProjectSystemConstants
    {
        // VS_OUTPUTGROUP_CNAME_Built
        public const string VS_OUTPUTGROUP_CNAME_Built = "Built";

        public const string OUTPUTGROUP_PROPERTY_OUTPUTLOC = "OUTPUTLOC";

        public const string CanUseTargetFSharpCoreVersion = "CanUseTargetFSharpCoreVersion";
    }

    public static class ProjectFileValues
    {
        public const string AnyCPU = "AnyCPU";
    }

    internal enum WrapperToolAttributeValue
    {
        Primary,
        TlbImp
    }

    /// <summary>
    /// A set of constants that specify the default sort order for different types of hierarchy nodes.
    /// </summary>
    internal static class DefaultSortOrderNode
    {
        public const int HierarchyNode = 1000;
        public const int ReferenceContainerNode = 300;
    }

}
