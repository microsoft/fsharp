// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Win32;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal struct ConfigCanonicalName
    {
        private static readonly StringComparer CMP = StringComparer.Ordinal;
        private readonly string myConfigName;
        private readonly string myPlatform;

        public ConfigCanonicalName(string configName, string platform)
        {
            myConfigName = configName;
            if (CMP.Equals(platform,ProjectConfig.AnyCPU))
                myPlatform = ProjectConfig.Any_CPU;
            else
                myPlatform = platform;
        }

        public ConfigCanonicalName(string configCanonicalName)
        {
            string platform;
            TrySplitConfigurationCanonicalName(configCanonicalName, out myConfigName, out platform);
            if (CMP.Equals(platform, ProjectConfig.AnyCPU))
                myPlatform = ProjectConfig.Any_CPU;
            else
                myPlatform = platform;
        }

        public string ConfigName { get { return myConfigName != null ? myConfigName : String.Empty; } }
        public string Platform { get { return myPlatform != null ? myPlatform : String.Empty; } }

        public bool MatchesPlatform(string platform)
        {
            return CMP.Equals(platform, myPlatform);
        }

        public bool MatchesConfigName(string configName)
        {
            return CMP.Equals(configName, myConfigName);
        }

        public string MSBuildPlatform
        {
            get
            {
                if (CMP.Equals(myPlatform,ProjectConfig.Any_CPU))
                    return ProjectConfig.AnyCPU;
                else
                    return myPlatform != null ? myPlatform : String.Empty;
            }
        }

        public string PlatformTarget
        {
            get { return MSBuildPlatform; }
        }

        public override string ToString()
        {
            if (String.IsNullOrEmpty(myPlatform)) return myConfigName;
            return String.Format("{0}|{1}", myConfigName, myPlatform);
        }

        public string ToMSBuildCondition()
        {
            if (String.IsNullOrEmpty(myPlatform))
            {
                return String.Format(CultureInfo.InvariantCulture, " '$(Configuration)' == '{0}' ", myConfigName);
            }
            else 
            {
                return String.Format(CultureInfo.InvariantCulture, " '$(Configuration)|$(Platform)' == '{0}|{1}' ", myConfigName, this.MSBuildPlatform);
            }
        }

        public override int GetHashCode()
        {            
            return CMP.GetHashCode(myConfigName) * 29 + CMP.GetHashCode(myPlatform);
        }

        public bool Equals(ConfigCanonicalName other)
        {
            return CMP.Equals(myConfigName, other.myConfigName) && CMP.Equals(myPlatform, other.myPlatform);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ConfigCanonicalName)) return false;
            return Equals((ConfigCanonicalName)obj);
        }

        public static bool operator ==(ConfigCanonicalName left, ConfigCanonicalName right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(ConfigCanonicalName left, ConfigCanonicalName right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Splits the canonical configuration name into platform and configuration name.
        /// </summary>
        /// <param name="canonicalName">The canonicalName name.</param>
        /// <param name="configName">The name of the configuration.</param>
        /// <param name="platformName">The name of the platform.</param>
        /// <returns>true if successfull.</returns>
        internal static bool TrySplitConfigurationCanonicalName(string canonicalName, out string configName, out string platformName)
        {
            // TODO rationalize this code with callers and ProjectNode.OnHandleConfigurationRelatedGlobalProperties, ProjectNode.TellMSBuildCurrentSolutionConfiguration, etc
            configName = String.Empty;
            platformName = String.Empty;

            if (String.IsNullOrEmpty(canonicalName))
            {
                return false;
            }

            string[] splittedCanonicalName = canonicalName.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            if (splittedCanonicalName == null || (splittedCanonicalName.Length != 1 && splittedCanonicalName.Length != 2))
            {
                return false;
            }

            configName = splittedCanonicalName[0];
            if (splittedCanonicalName.Length == 2)
            {
                platformName = splittedCanonicalName[1];
            }

            return true;
        }

        public static ConfigCanonicalName OfCondition(string condition)
        {
            const string confOnly = "'$(Configuration)'";
            const string confAndPlatform = "'$(Configuration)|$(Platform)'";
            condition = condition.Trim();
            if (condition.StartsWith(confOnly) || condition.StartsWith(confAndPlatform))
            {
                var eqeqIdx = condition.IndexOf("==");
                if (eqeqIdx < 0) return new ConfigCanonicalName();
                var condTarget = condition.Substring(eqeqIdx + 2).Trim(' ');
                if (condTarget.StartsWith("'")) condTarget = condTarget.Substring(1);
                if (condTarget.EndsWith("'")) condTarget = condTarget.Substring(0, condTarget.Length - 1);
                // In confOnly case condTarget now contains "ConfName"
                // In confInPlatformCase condTarget now contains "ConfName|Platform"
                // ConfigCanonicalName constructor is ok with both
                return new ConfigCanonicalName(condTarget.Trim());
            }
            return new ConfigCanonicalName();
        }
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ProjectConfig :
        IVsCfg,
        IVsProjectCfg,
        IVsProjectCfg2,
        IVsProjectFlavorCfg,
        IVsDebuggableProjectCfg,
        IVsQueryDebuggableProjectCfg,
        ISpecifyPropertyPages,
        IVsSpecifyProjectDesignerPages,
        IVsCfgBrowseObject
    {
        public const string Debug = "Debug";
        public const string Release = "Release";
        public const string AnyCPU = "AnyCPU";
        public const string AnyCPU32BitPreferred = "AnyCPU32BitPreferred";
        public const string Any_CPU = "Any CPU";



        private ProjectNode project;
        private ConfigCanonicalName configCanonicalName;
        private DateTime lastCache;
        private string projectAssemblyNameCache;  // null means invalid
        private int fCanLaunchCache;  // -1 means invalid
        private string cachedOutputPath = "";
        private Microsoft.Build.Evaluation.Project evaluatedProject = null;
        private List<OutputGroup> outputGroups;        
        private IVsProjectFlavorCfg flavoredCfg = null;
        private BuildableProjectConfig buildableCfg = null;
        private readonly ProjectConfigProperties projectConfigurationProperties ;

        private string GetProjectAssemblyName()
        {
            if (this.lastCache < this.project.LastModifiedTime || this.projectAssemblyNameCache == null)
            {
                this.lastCache = this.project.LastModifiedTime;
                this.projectAssemblyNameCache = this.project.GetAssemblyName(this.configCanonicalName);
            }
            return this.projectAssemblyNameCache;
        }

        public ProjectNode ProjectMgr
        {
            get
            {
                return this.project;
            }
        }

        public string ConfigName
        {
            get
            {
                return this.configCanonicalName.ConfigName;
            }
            set
            {
                this.configCanonicalName = new ConfigCanonicalName(value, this.configCanonicalName.Platform);
                this.projectAssemblyNameCache = null;
            }
        }

        internal ConfigCanonicalName ConfigCanonicalName
        {
            get { return this.configCanonicalName; }
        }
        // Debug property page properties
        public string StartURL
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.StartURL, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.StartURL, value);
            }
        }
            
        public string StartArguments
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.StartArguments, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.StartArguments, value);
            }
        }

        public string StartWorkingDirectory
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.StartWorkingDirectory, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.StartWorkingDirectory, value);
            }
        }
            
        public string StartProgram
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.StartProgram, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.StartProgram, value);
            }
        }

        public int StartAction
        {
            get
            {
                string startAction = GetConfigurationProperty(ProjectFileConstants.StartAction, false);

                if ("Program" == startAction)
                    return 1;
                else if ("URL" == startAction)
                    return 2;
                else // "Project"
                    return 0;
            }
            set
            {
                string startAction = "";

                switch (value)
                {
                    case 0:
                        startAction = "Project";
                        break;
                    case 1:
                        startAction = "Program";
                        break;
                    case 2:
                        startAction = "URL";
                        break;
                    default:
                        throw new ArgumentException("Invalid StartAction value");
                }
                SetConfigurationProperty(ProjectFileConstants.StartAction, startAction);
            }
        }
            
        private bool getBool(string projectFileConstant)
        {
            return getNullableBool(projectFileConstant) ?? false;
        }

        private bool? getNullableBool(string projectFileConstant)
        {
            var p = GetConfigurationProperty(projectFileConstant, false);

            if (string.IsNullOrWhiteSpace(p))
                return null;

            return p == "true";
        }

        private void setBool(string projectFileConstant, bool p)
        {
            string boolString = p ? "true" : "false";
            SetConfigurationProperty(projectFileConstant, boolString);
        }

        public bool EnableSQLServerDebugging
        {
            get
            {
                return getBool(ProjectFileConstants.EnableSQLServerDebugging);
            }
            set
            {
                setBool(ProjectFileConstants.EnableSQLServerDebugging, value);
            }
        }
            
        public bool EnableUnmanagedDebugging
        {
            get
            {
                return getBool(ProjectFileConstants.EnableUnmanagedDebugging);
            }
            set
            {
                setBool(ProjectFileConstants.EnableUnmanagedDebugging, value);
            }
        }
            
        public string RemoteDebugMachine
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.RemoteDebugMachine, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.RemoteDebugMachine, value);
            }
        }
            
        public bool RemoteDebugEnabled
        {
            get
            {
                return getBool(ProjectFileConstants.RemoteDebugEnabled);
            }
            set
            {
                setBool(ProjectFileConstants.RemoteDebugEnabled, value);
            }
        }
            
        public bool UseVSHostingProcess
        {
            get
            {
                return getBool(ProjectFileConstants.UseVSHostingProcess);
            }
            set
            {
                setBool(ProjectFileConstants.UseVSHostingProcess, value);
            }
        }

        // Build Property Page properties
        public bool Optimize
        {
            get
            {
                return getNullableBool(ProjectFileConstants.Optimize) ?? true;
            }
            set
            {
                setBool(ProjectFileConstants.Optimize, value);
            }
        }

        public bool Tailcalls
        {
            get
            {
                return getNullableBool(ProjectFileConstants.Tailcalls) ?? true;
            }
            set
            {
                setBool(ProjectFileConstants.Tailcalls, value);
            }
        }

        public bool UseStandardResourceNames
        {
            get
            {
                return getNullableBool(ProjectFileConstants.UseStandardResourceNames) ?? true;
            }
            set
            {
                setBool(ProjectFileConstants.UseStandardResourceNames, value);
            }
        }

        public bool Prefer32Bit
        {
            get
            {
                return getBool(ProjectFileConstants.Prefer32Bit);
            }
            set
            {               
                setBool(ProjectFileConstants.Prefer32Bit, value);
            }
        }

        public bool DebugSymbols
        {
            get
            {
                return getBool(ProjectFileConstants.DebugSymbols);
            }
            set
            {
                setBool(ProjectFileConstants.DebugSymbols, value);
            }
        }

        public string DebugType
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.DebugType,false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.DebugType, value);
            }
        }

        public string OutputPath
        {
            get
            {
                if (this.cachedOutputPath == string.Empty)
                    this.cachedOutputPath = GetConfigurationProperty(ProjectFileConstants.OutputPath, false);
                    
                return this.cachedOutputPath;
            }
            set
            {
                // for an emtpy string, convert to the cwd
                if (value == string.Empty)
                    value = @".\";
                    
                try
                {
                    // first, throw an exception if the path contains any bad characters
                    if (value.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
                        throw new System.ArgumentException();
                    SetConfigurationProperty(ProjectFileConstants.OutputPath, value);
                    this.cachedOutputPath = value;
                }
                catch
                {
                    // Exception can be raised when the given path's format is not valid, so restore it
                    RestoreConfigurationProperty(ProjectFileConstants.OutputPath, this.cachedOutputPath);
                    throw new System.ArgumentException(SR.GetString(SR.InvalidOutputPath, CultureInfo.CurrentUICulture));
                }
            }
        }

        public string DefineConstants
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.DefineConstants, true);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.DefineConstants, value);
            }
        }

        public string NoWarn
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.NoWarn, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.NoWarn, value);
            }
        }

        public bool TreatWarningsAsErrors
        {
            get
            {
                return getBool(ProjectFileConstants.TreatWarningsAsErrors);
            }
            set
            {
                setBool(ProjectFileConstants.TreatWarningsAsErrors, value);
            }
        }

        public string TreatSpecificWarningsAsErrors
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.WarningsAsErrors, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.WarningsAsErrors, value);
            }
        }

        public string TreatSpecificWarningsAsWarnings
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.WarningsNotAsErrors, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.WarningsNotAsErrors, value);
            }
        }

        public string DocumentationFile
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.DocumentationFile, false);
            }
            set
            {
                string oldValue = GetConfigurationProperty(ProjectFileConstants.DocumentationFile, false);
                try {
                    SetConfigurationProperty(ProjectFileConstants.DocumentationFile, value);
                }
                catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
                {
                    // Exception can be raised when the given path's format is not valid, so restore it
                    RestoreConfigurationProperty(ProjectFileConstants.DocumentationFile, oldValue);
                    throw;                    
                }
            }
        }

        public int WarningLevel
        {
            get
            {
                switch (GetConfigurationProperty(ProjectFileConstants.WarningLevel, false))
                {
                    case "0": return 0;
                    case "1": return 1;
                    case "2": return 2;
                    case "3": return 3;
                    case "4": return 4;
                    case "5": return 5;
                    default: throw new ArgumentException("Invalid WarningLevel value in Project file.");
                }
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.WarningLevel, value.ToString());
            }
        }

        public string PlatformTarget
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.PlatformTarget, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.PlatformTarget, value);
            }
        }

        public string OtherFlags
        {
            get
            {
                return GetConfigurationProperty(ProjectFileConstants.OtherFlags, false);
            }
            set
            {
                SetConfigurationProperty(ProjectFileConstants.OtherFlags, value);
            }
        }

        public virtual object ConfigurationProperties
        {
            get
            {
                return this.projectConfigurationProperties;
            }
        }

        public IList<OutputGroup> OutputGroups
        {
            get
            {
                if (null == this.outputGroups)
                {
                    // Initialize output groups
                    this.outputGroups = new List<OutputGroup>();

                    // Get the list of group names from the project.
                    // The main reason we get it from the project is to make it easier for someone to modify
                    // it by simply overriding that method and providing the correct MSBuild target(s).
                    IList<KeyValuePair<string, string>> groupNames = project.GetOutputGroupNames();

                    if (groupNames != null)
                    {
                        // Populate the output array
                        foreach (KeyValuePair<string, string> group in groupNames)
                        {
                            OutputGroup outputGroup = CreateOutputGroup(project, group);
                            this.outputGroups.Add(outputGroup);
                        }
                    }

                }
                return this.outputGroups;
            }
        }

        internal ProjectConfig(ProjectNode project, ConfigCanonicalName configName)
        {
            this.project = project;
            this.configCanonicalName = configName;
            this.fCanLaunchCache = -1;
            this.lastCache = DateTime.MinValue;
            this.projectConfigurationProperties = new ProjectConfigProperties(this);

            ErrorHandler.ThrowOnFailure(ProjectMgr.InteropSafeIVsProjectFlavorCfgProvider.CreateProjectFlavorCfg(this, out flavoredCfg));

            // if the flavored object support XML fragment, initialize it
            IPersistXMLFragment persistXML = flavoredCfg as IPersistXMLFragment;
            if (null != persistXML)
            {
                this.project.LoadXmlFragment(persistXML, this.DisplayName);
            }
        }

        public virtual OutputGroup CreateOutputGroup(ProjectNode project, KeyValuePair<string, string> group)
        {
            OutputGroup outputGroup = new OutputGroup(group.Key, group.Value, project, this);
            return outputGroup;
        }

        public void PrepareBuild(bool clean)
        {
            project.PrepareBuild(this.configCanonicalName, clean);
        }

        public virtual string GetConfigurationProperty(string propertyName, bool resetCache)
        {
            Microsoft.Build.Evaluation.ProjectProperty property = GetMsBuildProperty(propertyName, resetCache);
            if (property == null)
                return null;

            return property.EvaluatedValue;
        }

        public virtual void SetConfigurationProperty(string propertyName, string propertyValue)
        {
            if (!this.project.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            string condition = this.configCanonicalName.ToMSBuildCondition();
            SetPropertyUnderCondition(propertyName, propertyValue, condition);

            // property cache will need to be updated
            this.evaluatedProject = null;
            UpdateOutputGroup();                
        }

        // Signal the output groups that something is changed
        private void UpdateOutputGroup() {            
            foreach (OutputGroup group in this.OutputGroups)
            {
                group.InvalidateGroup();
            }
            this.project.SetProjectFileDirty(true);
        }

        // This method is to restore the property value with old one when configuration goes wrong.
        // Unlike SetConfigurationProperty(), this method won't reevaluate the project prior to add the value, which may raise exceptions. 
        private void RestoreConfigurationProperty(string propertyName, string propertyValue)
        {
            string condition = this.configCanonicalName.ToMSBuildCondition();
            // Get properties for current configuration from project file and cache it
            MSBuildProject.SetGlobalProperty(this.project.BuildProject, ProjectFileConstants.Configuration, configCanonicalName.ConfigName);
            MSBuildProject.SetGlobalProperty(this.project.BuildProject, ProjectFileConstants.Platform, configCanonicalName.MSBuildPlatform);

            this.evaluatedProject = this.project.BuildProject;

            SetPropertyUnderConditionImpl(propertyName, propertyValue, condition);
            this.evaluatedProject = null;
            UpdateOutputGroup();                
        }

        /// <summary>
        /// Emulates the behavior of SetProperty(name, value, condition) on the old MSBuild object model.
        /// This finds a property group with the specified condition (or creates one if necessary) then sets the property in there.
        /// </summary>
        private void SetPropertyUnderCondition(string propertyName, string propertyValue, string condition)
        {
            this.EnsureCache();
            SetPropertyUnderConditionImpl(propertyName, propertyValue, condition);
        }

        private void SetPropertyUnderConditionImpl(string propertyName, string propertyValue, string condition)
        {
            string conditionTrimmed = (condition == null) ? String.Empty : condition.Trim();

            if (conditionTrimmed.Length == 0)
            {
                evaluatedProject.SetProperty(propertyName, propertyValue);
                return;
            }

            // New OM doesn't have a convenient equivalent for setting a property with a particular property group condition. 
            // So do it ourselves.
            Microsoft.Build.Construction.ProjectPropertyGroupElement newGroup = null;

            foreach (Microsoft.Build.Construction.ProjectPropertyGroupElement group in this.evaluatedProject.Xml.PropertyGroups)
            {
                if (String.Equals(group.Condition.Trim(), conditionTrimmed, StringComparison.OrdinalIgnoreCase))
                {
                    newGroup = group;
                    break;
                }
            }

            if (newGroup == null)
            {
                newGroup = this.evaluatedProject.Xml.AddPropertyGroup(); // Adds after last existing PG, else at start of project
                newGroup.Condition = condition;
            }

            Microsoft.Build.Construction.ProjectPropertyElement last = null; // If there's dupes, pick the last one so we win
            foreach (Microsoft.Build.Construction.ProjectPropertyElement property in newGroup.Properties) 
            {
                if (String.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) && property.Condition.Length == 0)
                {
                    last = property;
                }
            }
            if (last != null)
            {
                last.Value = propertyValue;
                return;
            }

            newGroup.AddProperty(propertyName, propertyValue);
        }


        /// <summary>
        /// If flavored, and if the flavor config can be dirty, ask it if it is dirty
        /// </summary>
        /// <param name="storageType">Project file or user file</param>
        /// <returns>0 = not dirty</returns>
        public int IsFlavorDirty(_PersistStorageType storageType)
        {
            int isDirty = 0;
            if (this.flavoredCfg != null && this.flavoredCfg is IPersistXMLFragment)
            {
                ((IPersistXMLFragment)this.flavoredCfg).IsFragmentDirty((uint)storageType, out isDirty);
            }
            return isDirty;
        }

        /// <summary>
        /// If flavored, ask the flavor if it wants to provide an XML fragment
        /// </summary>
        /// <param name="flavor">Guid of the flavor</param>
        /// <param name="storageType">Project file or user file</param>
        /// <param name="fragment">Fragment that the flavor wants to save</param>
        /// <returns>HRESULT</returns>
        public int GetXmlFragment(Guid flavor, _PersistStorageType storageType, out string fragment)
        {
            fragment = null;
            int hr = VSConstants.S_OK;
            if (this.flavoredCfg != null && this.flavoredCfg is IPersistXMLFragment)
            {
                Guid flavorGuid = flavor;
                hr = ((IPersistXMLFragment)this.flavoredCfg).Save(ref flavorGuid, (uint)storageType, out fragment, 1);
            }
            return hr;
        }

        public void GetPages(CAUUID[] pages)
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
        /// Implementation of the IVsSpecifyProjectDesignerPages. It will retun the pages that are configuration dependent.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        /// <returns>VSConstants.S_OK</returns>        
        public virtual int GetProjectDesignerPages(CAUUID[] pages)
        {
            this.GetCfgPropertyPages(pages);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// The display name is a two part item
        /// first part is the config name, 2nd part is the platform name
        /// </summary>
        public virtual int get_DisplayName(out string name)
        {
            name = DisplayName;
            return VSConstants.S_OK;
        }

        private string DisplayName
        {
            get
            {                
                return this.configCanonicalName.ToString();
            }
        }
        public virtual int get_IsDebugOnly(out int fDebug)
        {
            fDebug = 0;
            if (this.configCanonicalName.ConfigName == Debug)
            {
                fDebug = 1;
            }
            return VSConstants.S_OK;
        }
        public virtual int get_IsReleaseOnly(out int fRelease)
        {
            CCITracing.TraceCall();
            fRelease = 0;
            if (this.configCanonicalName.ConfigName == Release)
            {
                fRelease = 1;
            }
            return VSConstants.S_OK;
        }

        public virtual int EnumOutputs(out IVsEnumOutputs eo)
        {
            CCITracing.TraceCall();
            eo = null;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int get_BuildableProjectCfg(out IVsBuildableProjectCfg pb)
        {
            CCITracing.TraceCall();
            if (buildableCfg == null)
                buildableCfg = new BuildableProjectConfig(this);
            pb = buildableCfg;
            return VSConstants.S_OK;
        }

        public virtual int get_CanonicalName(out string name)
        {
            return ((IVsCfg)this).get_DisplayName(out name);
        }

        public virtual int get_IsPackaged(out int pkgd)
        {
            CCITracing.TraceCall();
            pkgd = 0;
            return VSConstants.S_OK;
        }

        public virtual int get_IsSpecifyingOutputSupported(out int f)
        {
            CCITracing.TraceCall();
            f = 1;
            return VSConstants.S_OK;
        }

        public virtual int get_Platform(out Guid platform)
        {
            CCITracing.TraceCall();
            platform = Guid.Empty;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int get_ProjectCfgProvider(out IVsProjectCfgProvider p)
        {
            CCITracing.TraceCall();
            p = null;
            IVsCfgProvider cfgProvider = null;
            this.project.GetCfgProvider(out cfgProvider);
            if (cfgProvider != null)
            {
                p = cfgProvider as IVsProjectCfgProvider;
            }

            return (null == p) ? VSConstants.E_NOTIMPL : VSConstants.S_OK;
        }

        public virtual int get_RootURL(out string root)
        {
            CCITracing.TraceCall();
            root = null;
            return VSConstants.S_OK;
        }

        public virtual int get_TargetCodePage(out uint target)
        {
            CCITracing.TraceCall();
            target = (uint)System.Text.Encoding.Default.CodePage;
            return VSConstants.S_OK;
        }

        public virtual int get_UpdateSequenceNumber(ULARGE_INTEGER[] li)
        {
            CCITracing.TraceCall();
            li[0] = new ULARGE_INTEGER();
            li[0].QuadPart = 0;
            return VSConstants.S_OK;
        }

        public virtual int OpenOutput(string name, out IVsOutput output)
        {
            CCITracing.TraceCall();
            output = null;
            return VSConstants.E_NOTIMPL;
        }

        private VsDebugTargetInfo GetDebugTargetInfo(uint grfLaunch, bool forLaunch)
        {
            VsDebugTargetInfo info = new VsDebugTargetInfo();
            info.cbSize = (uint)Marshal.SizeOf(info);
            info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

            // On first call, reset the cache, following calls will use the cached values
            string property = GetConfigurationProperty("StartAction", true);

            // Set the debug target
            if (0 == System.String.Compare("Program", property, StringComparison.OrdinalIgnoreCase))
            {
                string startProgram = StartProgram;
                if (!string.IsNullOrEmpty(startProgram))
                    info.bstrExe = startProgram;
            }
            else
            // property is either null or "Project"
            // we're ignoring "URL" for now
            {
                string outputType = project.GetProjectProperty(ProjectFileConstants.OutputType, false);
                if (forLaunch && 0 == string.Compare("library", outputType, StringComparison.OrdinalIgnoreCase))
                    throw new ClassLibraryCannotBeStartedDirectlyException();
                info.bstrExe = this.project.GetOutputAssembly(this.configCanonicalName);
            }

            property = GetConfigurationProperty("RemoteDebugMachine", false);
            if (property != null && property.Length > 0)
            {
                info.bstrRemoteMachine = property;
            }
            bool isRemoteDebug = (info.bstrRemoteMachine != null);

            property = GetConfigurationProperty("StartWorkingDirectory", false);
            if (string.IsNullOrEmpty(property))
            {
                // 3273: aligning with C#
                info.bstrCurDir = Path.GetDirectoryName(this.project.GetOutputAssembly(this.configCanonicalName));
            }
            else 
            {
                if (isRemoteDebug)
                {
                    info.bstrCurDir = property;
                }
                else
                {
                    string fullPath;
                    if (Path.IsPathRooted(property))
                    {
                        fullPath = property;
                    }
                    else
                    {
                        // use project folder as a base part when computing full path from given relative
                        var currentDir = Path.GetDirectoryName(this.project.GetOutputAssembly(this.configCanonicalName));
                        fullPath = Path.Combine(currentDir, property);
                    }

                    if (!Directory.Exists(fullPath))
                        throw new WorkingDirectoryNotExistsException(fullPath);
                    
                    info.bstrCurDir = fullPath;
                }
            }

            property = GetConfigurationProperty("StartArguments", false);
            if (!string.IsNullOrEmpty(property))
            {
                info.bstrArg = property;
            }

            info.fSendStdoutToOutputWindow = 0;

            property = GetConfigurationProperty("EnableUnmanagedDebugging", false);
            if (property != null && string.Compare(property, "true", StringComparison.OrdinalIgnoreCase) == 0)
            {
                //Set the unmanged debugger
                //TODO change to vsconstant when it is available in VsConstants. It should be guidCOMPlusNativeEng
                info.clsidCustom = new Guid("{92EF0900-2251-11D2-B72E-0000F87572EF}");
            }
            else
            {
                //Set the managed debugger
                info.clsidCustom = VSConstants.CLSID_ComPlusOnlyDebugEngine;
            }
            info.grfLaunch = grfLaunch;
            bool isConsoleApp = string.Compare("exe",
                project.GetProjectProperty(ProjectFileConstants.OutputType, false),
                StringComparison.OrdinalIgnoreCase) == 0;

            bool noDebug = ((uint)(((__VSDBGLAUNCHFLAGS)info.grfLaunch) & __VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug)) != 0;
            // Do not use cmd.exe to get "Press any key to continue." when remote debugging
            if (isConsoleApp && noDebug && !isRemoteDebug)
            {
                // VSWhidbey 573404: escape the characters ^<>& so that they are passed to the application rather than interpreted by cmd.exe.
                System.Text.StringBuilder newArg = new System.Text.StringBuilder(info.bstrArg);
                newArg.Replace("^", "^^")
                      .Replace("<", "^<")
                      .Replace(">", "^>")
                      .Replace("&", "^&");
                newArg.Insert(0, "\" ");
                newArg.Insert(0, info.bstrExe);
                newArg.Insert(0, "/c \"\"");
                newArg.Append(" & pause\"");
                string newExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");

                info.bstrArg = newArg.ToString();
                info.bstrExe = newExe;
            }
            return info;
        }

        /// <summary>
        /// Called by the vs shell to start debugging (managed or unmanaged).
        /// Override this method to support other debug engines.
        /// </summary>
        /// <param name="grfLaunch">A flag that determines the conditions under which to start the debugger. For valid grfLaunch values, see __VSDBGLAUNCHFLAGS</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code</returns>
        public virtual int DebugLaunch(uint grfLaunch)
        {
            CCITracing.TraceCall();

            try
            {
                VsDebugTargetInfo info = GetDebugTargetInfo(grfLaunch, forLaunch: true);
                VsShellUtilities.LaunchDebugger(this.project.Site, info);
            }
            catch (ClassLibraryCannotBeStartedDirectlyException)
            {
                throw;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception : " + e.Message);

                return Marshal.GetHRForException(e);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Determines whether the debugger can be launched, given the state of the launch flags.
        /// </summary>
        /// <param name="flags">Flags that determine the conditions under which to launch the debugger. 
        /// For valid grfLaunch values, see __VSDBGLAUNCHFLAGS or __VSDBGLAUNCHFLAGS2.</param>
        /// <param name="fCanLaunch">true if the debugger can be launched, otherwise false</param>
        /// <returns>S_OK if the method succeeds, otherwise an error code</returns>
        public virtual int QueryDebugLaunch(uint flags, out int fCanLaunch)
        {
            // This method is called ridiculously frequently, even in a relatively ambient environment,
            // hence it must be fast.
            CCITracing.TraceCall();
            if (this.fCanLaunchCache == -1 || this.projectAssemblyNameCache == null || this.lastCache < this.project.LastModifiedTime)
            {
                string assembly = GetProjectAssemblyName();
                this.fCanLaunchCache = (assembly != null && assembly.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
                if (this.fCanLaunchCache == 0)
                {
                    string property = GetConfigurationProperty("StartProgram", true);
                    this.fCanLaunchCache = (property != null && property.Length > 0) ? 1 : 0;
                }
            }
            fCanLaunch = this.fCanLaunchCache;
            return VSConstants.S_OK;
        }

        public virtual int QueryDebugTargets(uint grfLaunch, uint cTargets, VsDebugTargetInfo2[] debugTargetInfo, uint[] actualTargets)
        {
            if (debugTargetInfo == null) // caller only queries for number of targets
            {
                actualTargets[0] = 1;
                return VSConstants.S_OK;
            }
            if (cTargets == 0)
            {
                actualTargets[0] = 1;
                return VSConstants.E_FAIL;
            }

            VsDebugTargetInfo targetInfo;
            targetInfo  = GetDebugTargetInfo(grfLaunch, forLaunch: false);

            // Copying parameters from VsDebugTargetInfo to VsDebugTargetInfo2.
            // Only relevant fields are copied (that is, those that are set by GetDebugTargetInfo)
            var targetInfo2 = new VsDebugTargetInfo2()
            {
                cbSize = (uint) Marshal.SizeOf(typeof(VsDebugTargetInfo2)),
                bstrArg = targetInfo.bstrArg,
                bstrCurDir = targetInfo.bstrCurDir,
                bstrEnv = targetInfo.bstrEnv,
                bstrOptions = targetInfo.bstrOptions,
                bstrExe = targetInfo.bstrExe,
                bstrPortName = targetInfo.bstrPortName,
                bstrRemoteMachine = targetInfo.bstrRemoteMachine,
                fSendToOutputWindow = targetInfo.fSendStdoutToOutputWindow,
                guidLaunchDebugEngine = targetInfo.clsidCustom,
                LaunchFlags = targetInfo.grfLaunch,
                dlo = (uint)targetInfo.dlo,
            };
            targetInfo2.dwDebugEngineCount = 1;
            // Disposed by caller.
            targetInfo2.pDebugEngines = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Guid)));
            byte[] guidBytes = targetInfo.clsidCustom.ToByteArray();
            Marshal.Copy(guidBytes, 0, targetInfo2.pDebugEngines, guidBytes.Length);

            debugTargetInfo[0] = targetInfo2;
            if (actualTargets != null)
                actualTargets[0] = 1;
            return VSConstants.S_OK;
        }

        public virtual int OpenOutputGroup(string szCanonicalName, out IVsOutputGroup ppIVsOutputGroup)
        {
            ppIVsOutputGroup = null;
            // Search through our list of groups to find the one they are looking for groupName
            foreach (OutputGroup group in OutputGroups)
            {
                string groupName;
                group.get_CanonicalName(out groupName);
                if (String.Compare(groupName, szCanonicalName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ppIVsOutputGroup = group;
                    break;
                }
            }
            return (ppIVsOutputGroup != null) ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public virtual int OutputsRequireAppRoot(out int pfRequiresAppRoot)
        {
            pfRequiresAppRoot = 0;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int get_CfgType(ref Guid iidCfg, out IntPtr ppCfg)
        {
            // Delegate to the flavored configuration (to enable a flavor to take control)
            // Since we can be asked for Configuration we don't support, avoid throwing and return the HRESULT directly
            int hr = flavoredCfg.get_CfgType(ref iidCfg, out ppCfg);
            if (ppCfg !=IntPtr.Zero)
                return hr;

            IntPtr pUnk = IntPtr.Zero;
            try
            {
                pUnk = Marshal.GetIUnknownForObject(this);
                return Marshal.QueryInterface(pUnk, ref iidCfg, out ppCfg);
            }
            finally
            {
                if (pUnk != IntPtr.Zero) Marshal.Release(pUnk);
            }
        }

        public virtual int get_IsPrivate(out int pfPrivate)
        {
            pfPrivate = 0;
            return VSConstants.S_OK;
        }

        public virtual int get_OutputGroups(uint celt, IVsOutputGroup[] rgpcfg, uint[] pcActual)
        {
            // Are they only asking for the number of groups?
            if (celt == 0)
            {
                if ((null == pcActual) || (0 == pcActual.Length))
                {
                    throw new ArgumentNullException("pcActual");
                }
                pcActual[0] = (uint)OutputGroups.Count;
                return VSConstants.S_OK;
            }

            // Check that the array of output groups is not null
            if ((null == rgpcfg) || (rgpcfg.Length == 0))
            {
                throw new ArgumentNullException("rgpcfg");
            }

            // Fill the array with our output groups
            uint count = 0;
            foreach (OutputGroup group in OutputGroups)
            {
                if (rgpcfg.Length > count && celt > count && group != null)
                {
                    rgpcfg[count] = group;
                    ++count;
                }
            }

            if (pcActual != null && pcActual.Length > 0)
                pcActual[0] = count;

            // If the number asked for does not match the number returned, return S_FALSE
            return (count == celt) ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public virtual int get_VirtualRoot(out string pbstrVRoot)
        {
            pbstrVRoot = null;
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Maps back to the configuration corresponding to the browse object. 
        /// </summary>
        /// <param name="cfg">The IVsCfg object represented by the browse object</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int GetCfg(out IVsCfg cfg)
        {
            cfg = this;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Maps back to the hierarchy or project item object corresponding to the browse object.
        /// </summary>
        /// <param name="hier">Reference to the hierarchy object.</param>
        /// <param name="itemid">Reference to the project item.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int GetProjectItem(out IVsHierarchy hier, out uint itemid)
        {
            if (this.project == null || this.project.NodeProperties == null)
            {
                throw new InvalidOperationException();
            }
            return this.project.NodeProperties.GetProjectItem(out hier, out itemid);
        }

        private void EnsureCache()
        {
            // Get properties for current configuration from project file and cache it
            this.project.SetConfiguration(configCanonicalName);
            this.evaluatedProject = this.project.BuildProject;
            // REVIEW: The call below will set the Project configuration to the Solution configuration
            // for the purpose of evaluating properties - this is exactly what we don't want to do.
            // Can anyone think of a reason why we'd want to keep it?
            //project.SetCurrentConfiguration();
        }

        private Microsoft.Build.Evaluation.ProjectProperty GetMsBuildProperty(string propertyName, bool resetCache)
        {
            if (resetCache || this.evaluatedProject == null)
            {
                this.EnsureCache();
            }

            if (this.evaluatedProject == null)
               throw new Exception("Failed to retrive properties");

            // return property asked for
            return this.evaluatedProject.GetProperty(propertyName);
        }

        /// <summary>
        /// Retrieves the configuration dependent property pages.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        private void GetCfgPropertyPages(CAUUID[] pages)
        {
            // We do not check whether the supportsProjectDesigner is set to true on the ProjectNode.
            // We rely that the caller knows what to call on us.
            if (pages == null)
            {
                throw new ArgumentNullException("pages");
            }

            if (pages.Length == 0)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "pages");
            }

            // Retrive the list of guids from hierarchy properties.
            // Because a flavor could modify that list we must make sure we are calling the outer most implementation of IVsHierarchy
            string guidsList = String.Empty;
            IVsHierarchy hierarchy = project.InteropSafeIVsHierarchy;
            object variant = null;
            ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList, out variant), new int[] { VSConstants.DISP_E_MEMBERNOTFOUND, VSConstants.E_NOTIMPL });
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

        /// <summary>
        /// This is called to let the flavored config let go
        /// of any reference it may still be holding to the base config
        /// </summary>
        /// <returns></returns>
        int IVsProjectFlavorCfg.Close()
        {
            // This is used to release the reference the flavored config is holding
            // on the base config, but in our scenario these 2 are the same object
            // so we have nothing to do here.
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Actual implementation of get_CfgType.
        /// When not flavored or when the flavor delegate to use
        /// we end up creating the requested config if we support it.
        /// </summary>
        /// <param name="iidCfg">IID representing the type of config object we should create</param>
        /// <param name="ppCfg">Config object that the method created</param>
        /// <returns>HRESULT</returns>
        int IVsProjectFlavorCfg.get_CfgType(ref Guid iidCfg, out IntPtr ppCfg)
        {
            ppCfg = IntPtr.Zero;

            // See if this is an interface we support
            if (iidCfg == typeof(IVsDebuggableProjectCfg).GUID)
                ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsDebuggableProjectCfg));
            else if (iidCfg == typeof(IVsBuildableProjectCfg).GUID)
            {
                IVsBuildableProjectCfg buildableConfig;
                this.get_BuildableProjectCfg(out buildableConfig);
                ppCfg = Marshal.GetComInterfaceForObject(buildableConfig, typeof(IVsBuildableProjectCfg));
            }

            // If not supported
            if (ppCfg == IntPtr.Zero)
                return VSConstants.E_NOINTERFACE;

            return VSConstants.S_OK;
        }

        public string Platform { get { return this.configCanonicalName.Platform; } }

        private static bool IsPossibleOutputGroup(string groupName)
        {
            return groupName != "SourceFiles";
        }

        private static DateTime? TryGetLastWriteTimeUtc(string path, OutputWindowLogger logger)
        {
            Exception exn = null;

            try
            {
                if (File.Exists(path))
                    return File.GetLastWriteTimeUtc(path);
            }
            catch (Exception ex)
            {
                exn = ex;
            }

            if (exn != null)
            {
                logger.WriteLine("Failed to access {0}: {1}", path, exn.Message);
                logger.WriteLine(exn.ToString());
            }

            return null;
        }

        internal bool GetUTDCheckInputs(HashSet<string> inputs)
        {
            // the project file itself
            inputs.Add(Utilities.CanonicalizeFileNameNoThrow(this.project.BuildProject.FullPath));

            var projDir = this.project.BuildProject.DirectoryPath;

            // well-known types of input items
            var itemTypes = new string[] { "Compile", "Content", "Resource", "EmbeddedResource" };
            foreach (var itemType in itemTypes)
            {
                foreach (var item in this.project.BuildProject.GetItems(itemType))
                {
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow(Path.Combine(projDir, item.EvaluatedInclude)));
                }
            }

            // other well-known inputs exposed as properties
            var properties = new string[] { "Win32Manifest", "Win32Resource", "AssemblyOriginatorKeyFile", "KeyOriginatorFile", "ApplicationIcon", "VersionFile" };
            foreach (var prop in properties)
            {
                var propVal = this.project.BuildProject.GetPropertyValue(prop);
                if (!String.IsNullOrWhiteSpace(propVal))
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow(Path.Combine(projDir, propVal)));
            }

            // other well-known special files that were otherwise missed
            var specialFiles = this.project.InteropSafeIVsHierarchy as IVsProjectSpecialFiles;
            if (specialFiles == null)
                return false;

            for (int fileId = (int)__PSFFILEID5.PSFFILEID_FIRST5; fileId <= (int)__PSFFILEID.PSFFILEID_LAST; fileId++)
            {
                uint itemId;
                string fileName;
                if (ErrorHandler.Succeeded(specialFiles.GetFile(fileId, (uint)__PSFFLAGS.PSFF_FullPath, out itemId, out fileName))
                    && itemId != (uint)VSConstants.VSITEMID.Nil)
                {
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow(fileName));
                }
            }

            // assembly and project references
            foreach (var reference in this.project.GetReferenceContainer().EnumReferences())
            {
                if (reference is AssemblyReferenceNode)
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow(reference.Url));
                else if (reference is ProjectReferenceNode)
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow((reference as ProjectReferenceNode).ReferencedProjectOutputPath));
                else if (reference is ComReferenceNode)
                    inputs.Add(Utilities.CanonicalizeFileNameNoThrow((reference as ComReferenceNode).InstalledFilePath));
                else if (reference is GroupingReferenceNode)
                {
                    foreach (var groupedRef in ((GroupingReferenceNode)reference).GroupedItems)
                    {
                        inputs.Add(Utilities.CanonicalizeFileNameNoThrow(groupedRef));
                    }
                }
                else
                {
                    // some reference type we don't know about
                    System.Diagnostics.Debug.Assert(false, "Unexpected reference type", "{0}", reference);
                    return false;
                }
            }

            return true;
        }

        internal void GetUTDCheckOutputs(HashSet<string> inputs, HashSet<string> outputs, out List<Tuple<string, string>> preserveNewestOutputs)
        {
            preserveNewestOutputs = new List<Tuple<string, string>>();

            // Output groups give us the paths to the following outputs
            //   result EXE or DLL in "obj" dir
            //   PDB file in "obj" dir (if project is configured to create this)
            //   XML doc file in "bin" dir (if project is configured to create this)
            foreach (var output in OutputGroups
                                    .Where(g => IsPossibleOutputGroup(g.CanonicalName))
                                    .SelectMany(x => x.Outputs)
                                    .Select(o => Utilities.CanonicalizeFileNameNoThrow(o.CanonicalName))
                                    .Where((path) => !inputs.Contains(path)))  // some "outputs" are really inputs (e.g. app.config files)
            {
                outputs.Add(output);
            }

            // final binplace of built assembly
            var outputAssembly = this.project.GetOutputAssembly(this.ConfigCanonicalName);
            outputs.Add(Utilities.CanonicalizeFileNameNoThrow(outputAssembly));

            bool isExe = outputAssembly.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

            // final PDB path
            if (this.DebugSymbols &&
                (isExe || outputAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var pdbPath = outputAssembly.Remove(outputAssembly.Length - 4) + ".pdb";
                outputs.Add(Utilities.CanonicalizeFileNameNoThrow(pdbPath));
            }

            if (isExe)
            {
                var appConfig = inputs.FirstOrDefault(x => String.Compare(Path.GetFileName(x), "app.config", StringComparison.OrdinalIgnoreCase) == 0);
                if (appConfig != null)
                {
                    // the app.config is not removed from the inputs to maintain 
                    // the same behavior of a C# project:
                    // When a app.config is changed, after the build, the project 
                    // is not up-to-date until a rebuild
                    var exeConfig = Utilities.CanonicalizeFileNameNoThrow(outputAssembly + ".config");
                    preserveNewestOutputs.Add(Tuple.Create(appConfig, exeConfig));
                }
            }
        }

        // there is a well-known property users can specify that signals for UTD check to be disabled
        internal bool IsFastUpToDateCheckEnabled()
        {
            var fastUTDPropVal = this.project.BuildProject.GetPropertyValue("DisableFastUpToDateCheck");
            if (String.IsNullOrWhiteSpace(fastUTDPropVal))
                return true;
            if (String.Equals(fastUTDPropVal, "true", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        internal bool IsUpToDate(OutputWindowLogger logger, bool testing)
        {
            logger.WriteLine("Checking whether {0} needs to be rebuilt:", ProjectMgr.Caption);

            // in batch build it is possible that config is out of sync.
            // in this case, don't assume we are up to date
            ConfigCanonicalName activeConfig = default(ConfigCanonicalName);
           
            if(!Utilities.TryGetActiveConfigurationAndPlatform(ServiceProvider.GlobalProvider, this.project.ProjectIDGuid, out activeConfig) ||
                activeConfig != this.ConfigCanonicalName)
            {
                logger.WriteLine("Not up to date: active confic does not match project config. Active: {0} Project: {1}", activeConfig, this.ConfigCanonicalName);
                if (!testing)
                    return false;
            }

            var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!GetUTDCheckInputs(inputs))
                return false;

            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Tuple<string, string>> preserveNewestOutputs;
            GetUTDCheckOutputs(inputs, outputs, out preserveNewestOutputs);

            // determine the oldest output timestamp
            DateTime stalestOutputTime = DateTime.MaxValue.ToUniversalTime();
            foreach (var output in outputs)
            {
                var timeStamp = TryGetLastWriteTimeUtc(output, logger);
                if (!timeStamp.HasValue)
                {
                    logger.WriteLine("Declaring project NOT up to date, can't find expected output {0}", output);
                    return false;
                }

                logger.WriteLine("   Output: {0} {1}", timeStamp.Value.ToLocalTime(), output);

                if (stalestOutputTime > timeStamp.Value)
                    stalestOutputTime = timeStamp.Value;
            }

            // determine the newest input timestamp
            DateTime freshestInputTime = DateTime.MinValue.ToUniversalTime();
            foreach (var input in inputs)
            {
                var timeStamp = TryGetLastWriteTimeUtc(input, logger);
                if (!timeStamp.HasValue)
                {
                    logger.WriteLine("Declaring project NOT up to date, can't find expected input {0}", input);
                    return false;
                }

                logger.WriteLine("   Input: {0} {1}", timeStamp.Value.ToLocalTime(), input);

                if (freshestInputTime < timeStamp.Value)
                    freshestInputTime = timeStamp.Value;
            }

            // check 1-1 Preserve Newest mappings
            foreach (var kv in preserveNewestOutputs)
            {
                if (!IsUpToDatePreserveNewest(logger, TryGetLastWriteTimeUtc, kv.Item1, kv.Item2)) 
                    return false;
            }

            logger.WriteLine("Freshest input: {0}", freshestInputTime.ToLocalTime());
            logger.WriteLine("Stalest output: {0}", stalestOutputTime.ToLocalTime());
            logger.WriteLine("Up to date: {0}", freshestInputTime <= stalestOutputTime);

            // if all outputs are younger than all inputs, we are up to date
            return freshestInputTime <= stalestOutputTime;
        }

        public static bool IsUpToDatePreserveNewest(OutputWindowLogger logger, Func<string, OutputWindowLogger, DateTime?> tryGetLastWriteTimeUtc, string input, string output)
        {
            var inputTime = tryGetLastWriteTimeUtc(input, logger);
            if (!inputTime.HasValue)
            {
                logger.WriteLine("Declaring project NOT up to date, can't find expected input {0}", input);
                return false;
            }

            var outputTime = tryGetLastWriteTimeUtc(output, logger);
            if (!outputTime.HasValue)
            {
                logger.WriteLine("Declaring project NOT up to date, can't find expected output {0}", output);
                return false;
            }

            var inputTimeValue = inputTime.Value;
            var outputTimeValue = outputTime.Value;

            if (outputTimeValue < inputTimeValue)
            {
                logger.WriteLine("Declaring project NOT up to date, ouput {0} is stale", output);
                return false;
            }

            return true;
        }
    }

    internal class ClassLibraryCannotBeStartedDirectlyException : COMException 
    {
        public ClassLibraryCannotBeStartedDirectlyException() : base(SR.GetStringWithCR(SR.CannotStartLibraries)) { }
    }

    internal class WorkingDirectoryNotExistsException : COMException
    {
        public WorkingDirectoryNotExistsException(string path) : base(string.Format(SR.GetStringWithCR(SR.WorkingDirectoryNotExists), path)) { }
    }

    //=============================================================================
    // NOTE: advises on out of proc build execution to maximize
    // future cross-platform targeting capabilities of the VS tools.

    [CLSCompliant(false)]
    [ComVisible(true)]
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Buildable")]
    public class BuildableProjectConfig : IVsBuildableProjectCfg, IVsBuildableProjectCfg2
    {

        private bool IsInProgress()
        {
            return buildManagerAccessor.IsInProgress();
        }

        ProjectConfig config = null;
        EventSinkCollection callbacks = new EventSinkCollection();
        string[] filesWeCalledHandsOff = null;
        IVsBuildManagerAccessor buildManagerAccessor = null; 

        internal BuildableProjectConfig(ProjectConfig config)
        {
            this.config = config;
            this.buildManagerAccessor = this.config.ProjectMgr.GetService(typeof(SVsBuildManagerAccessor)) as IVsBuildManagerAccessor;
        }

        private const int VSBLDCFGPROPID_SupportsMTBuild = -16000;

        public int GetBuildCfgProperty(int propid, out object pvar)
        {
            switch (propid)
            {
                case VSBLDCFGPROPID_SupportsMTBuild:
                    // Indicate that we support multi-proc builds
                    pvar = true;
                    return VSConstants.S_OK;
                default:
                    pvar = null;
                    return VSConstants.E_NOTIMPL;
            }
        }

        public int StartBuildEx(uint dwBuildId, IVsOutputWindowPane pIVsOutputWindowPane, uint dwOptions)
        {
            return this.StartBuild(pIVsOutputWindowPane, dwOptions);
        }

        public virtual int AdviseBuildStatusCallback(IVsBuildStatusCallback callback, out uint cookie)
        {
            CCITracing.TraceCall();

            cookie = callbacks.Add(callback);
            return VSConstants.S_OK;
        }

        public virtual int get_ProjectCfg(out IVsProjectCfg p)
        {
            CCITracing.TraceCall();

            p = config;
            return VSConstants.S_OK;
        }

        public virtual int QueryStartBuild(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            if (supported != null && supported.Length > 0)
                supported[0] = 1;
            if (ready != null && ready.Length > 0)
                ready[0] = (IsInProgress()) ? 0 : 1;
            return VSConstants.S_OK;
        }

        public virtual int QueryStartClean(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);
            if (supported != null && supported.Length > 0)
                supported[0] = 1;
            if (ready != null && ready.Length > 0)
                ready[0] = (IsInProgress()) ? 0 : 1;
            return VSConstants.S_OK;
        }

        public virtual int QueryStartUpToDateCheck(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);

            // criteria (same as C# project system):
            // - Fast UTD never enabled for package operations
            // - Fast UTD always enabled for DTEE operations
            // - Otherwise fast UTD enabled as long as it's not explicitly disabled by the project
            bool utdSupported =
                ((options & VSConstants.VSUTDCF_PACKAGE) == 0) &&
                (((options & VSConstants.VSUTDCF_DTEEONLY) != 0) || config.IsFastUpToDateCheckEnabled());

            int utdSupportedFlag = utdSupported ? 1 : 0;

            if (supported != null && supported.Length > 0)
                supported[0] = utdSupportedFlag;
            if (ready != null && ready.Length > 0)
                ready[0] = utdSupportedFlag;

            return VSConstants.S_OK;
        }

        public virtual int QueryStatus(out int done)
        {
            CCITracing.TraceCall();

            done = (IsInProgress()) ? 0 : 1;
            return VSConstants.S_OK;
        }

        public virtual int StartBuild(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);

            // Current version of MSBuild wish to be called in an STA
            uint flags = VSConstants.VS_BUILDABLEPROJECTCFGOPTS_REBUILD;

            // If we are not asked for a rebuild, then we build the default target (by passing null)
            this.Build(options, pane, ((options & flags) != 0) ? MsBuildTarget.Rebuild : null);

            return VSConstants.S_OK;
        }

        public virtual int StartClean(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(true);

            // Current version of MSBuild wish to be called in an STA
            this.Build(options, pane, MsBuildTarget.Clean);
            return VSConstants.S_OK;
        }

        public virtual int StartUpToDateCheck(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();
            
            return config.IsUpToDate(OutputWindowLogger.CreateUpToDateCheckLogger(pane), false) ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public virtual int Stop(int fsync)
        {
            CCITracing.TraceCall();

            return VSConstants.S_OK;
        }

        public virtual int UnadviseBuildStatusCallback(uint cookie)
        {
            CCITracing.TraceCall();


            callbacks.RemoveAt(cookie);
            return VSConstants.S_OK;
        }

        public virtual int Wait(uint ms, int fTickWhenMessageQNotEmpty)
        {
            CCITracing.TraceCall();

            return VSConstants.E_NOTIMPL;
        }

        private bool NotifyBuildBegin()
        {

            foreach (IVsBuildStatusCallback cb in callbacks)
            {
                try
                {
                    int shouldContinue = 1;
                    ErrorHandler.ThrowOnFailure(cb.BuildBegin(ref shouldContinue));
                    if (shouldContinue == 0)
                        return false;
                }
                catch (Exception e)
                {
                    // If those who ask for status have bugs in their code it should not prevent the build/notification from happening
                    Debug.Fail(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.BuildEventError, CultureInfo.CurrentUICulture), e.Message));
                }
            }
            // (In theory it would be good to call HandsOff for all compiler outputs; in practice the .XML documentation file is the only one causing trouble.)
            // (Note: path to the documentation file (what gets passed to fsc.exe --doc: option), e.g. Path.Combine(ProjectFolder,DocumentationFile), is 'intermediate' location, intellisense providers are locking the 'final' doc location.)
            var coll1 = MSBuildProject.GetItems(this.config.ProjectMgr.BuildProject, "FinalDocFile");  // Microsoft.Common.targets sets this as part of its startup logic
            var coll = coll1;
            var item = System.Linq.Enumerable.FirstOrDefault(coll);                   // should be just one
            var finalDocLocation = item == null ? null : MSBuildItem.GetEvaluatedInclude(item);
            if (!string.IsNullOrEmpty(finalDocLocation))
            {
                if (!Path.IsPathRooted(finalDocLocation))
                {
                    finalDocLocation = Path.Combine(this.config.ProjectMgr.ProjectFolder, finalDocLocation);
                }
                IVsTrackProjectDocuments3 documentTracker = this.config.ProjectMgr.Site.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments3;
                var mode = __HANDSOFFMODE.HANDSOFFMODE_AsyncOperation | __HANDSOFFMODE.HANDSOFFMODE_FullAccess;
                this.filesWeCalledHandsOff = new string[] { finalDocLocation };
                var hr = documentTracker.HandsOffFiles((uint)mode, filesWeCalledHandsOff.Length, filesWeCalledHandsOff);
                if (!ErrorHandler.Succeeded(hr))
                {
                    // the call to HandsOff did not succeed, so do not subsequently call HandsOn
                    this.filesWeCalledHandsOff = null;
                }
            }
            return true;
        }
        private void NotifyBuildEnd(bool isSuccess)
        {
            int success = (isSuccess ? 1 : 0);

            if (filesWeCalledHandsOff != null)
            {
                IVsTrackProjectDocuments3 documentTracker = this.config.ProjectMgr.Site.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments3;
                documentTracker.HandsOnFiles(filesWeCalledHandsOff.Length, filesWeCalledHandsOff);
                filesWeCalledHandsOff = null;
            }

            foreach (IVsBuildStatusCallback cb in callbacks)
            {
                try
                {
                    ErrorHandler.ThrowOnFailure(cb.BuildEnd(success));
                }
                catch (Exception e)
                {
                    // If those who ask for status have bugs in their code it should not prevent the build/notification from happening
                    Debug.Fail(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.BuildEventError, CultureInfo.CurrentUICulture), e.Message));
                }
            }
        }
        public void Build(uint options, IVsOutputWindowPane output, string target)
        {
            // We want to refresh the references if we are building with the Build or Rebuild target.
            bool shouldRepaintReferences = (target == null || target == MsBuildTarget.Build || target == MsBuildTarget.Rebuild);

            if (!NotifyBuildBegin()) return;
            try
            {
                config.ProjectMgr.Build(options, this.config.ConfigCanonicalName, output, target, (result, projectInstance) =>
                    {
                        this.BuildCoda(new BuildResult(result, projectInstance), output, shouldRepaintReferences);
                    });
            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception : " + e.Message);
                ErrorHandler.ThrowOnFailure(output.OutputStringThreadSafe("Unhandled Exception:" + e.Message + "\n"));
                this.BuildCoda(new BuildResult(MSBuildResult.Failed, null), output, shouldRepaintReferences);
                throw;
            }
        }
        private void BuildCoda(BuildResult result, IVsOutputWindowPane output, bool shouldRepaintReferences)
        {
            try
            {
                // Now repaint references if that is needed. 
                // We hardly rely here on the fact the ResolveAssemblyReferences target has been run as part of the build.
                // One scenario to think at is when an assembly reference is renamed on disk thus becomming unresolvable, 
                // but msbuild can actually resolve it.
                // Another one if the project was opened only for browsing and now the user chooses to build or rebuild.
                if (shouldRepaintReferences && (result.IsSuccessful))
                {
                    this.RefreshReferences(result);
                }
            }
            finally
            {
                try
                {
                    ErrorHandler.ThrowOnFailure(output.FlushToTaskList());
                }
                finally
                {
                    NotifyBuildEnd(result.IsSuccessful);
                }
            }
        }
        /// <summary>
        /// Refreshes references and redraws them correctly.
        /// </summary>
        private void RefreshReferences(BuildResult buildResult)
        {
            // Refresh the reference container node for assemblies that could be resolved.
            IReferenceContainer referenceContainer = this.config.ProjectMgr.GetReferenceContainer();
            foreach (ReferenceNode referenceNode in referenceContainer.EnumReferences())
            {
                referenceNode.RefreshReference(buildResult);
            }
        }
    }
}
