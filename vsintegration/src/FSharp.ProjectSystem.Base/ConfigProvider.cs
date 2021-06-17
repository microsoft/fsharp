// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using System.Diagnostics.CodeAnalysis;


/* This file provides a basefunctionallity for IVsCfgProvider2.
   Instead of using the IVsProjectCfgEventsHelper object we have our own little sink and call our own helper methods
   similiar to the interface. But there is no real benefit in inheriting from the interface in the first place. 
   Using the helper object seems to be:  
    a) undocumented
    b) not really wise in the managed world
*/
namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    [CLSCompliant(false)]
    [ComVisible(true)]
    public class ConfigProvider : IVsCfgProvider2, IVsProjectCfgProvider, IVsExtensibleObject
    {
        private ProjectNode project;
        private EventSinkCollection cfgEventSinks = new EventSinkCollection();
        private List<KeyValuePair<KeyValuePair<string, string>, string>> newCfgProps = new List<KeyValuePair<KeyValuePair<string, string>, string>>();
        private Dictionary<ConfigCanonicalName, ProjectConfig> configurationsList = new Dictionary<ConfigCanonicalName, ProjectConfig>();

        /// <summary>
        /// The associated project.
        /// </summary>
        public ProjectNode ProjectMgr
        {
            get
            {
                return this.project;
            }
        }
        /// <summary>
        /// If the project system wants to add custom properties to the property group then 
        /// they provide us with this data.
        /// Returns/sets the [(<propName, propCondition>) <propValue>] collection
        /// </summary>
        public virtual List<KeyValuePair<KeyValuePair<string, string>, string>> NewConfigProperties
        {
            get
            {
                return newCfgProps;
            }
            set
            {
                newCfgProps = value;
            }
        }

        internal ConfigProvider(ProjectNode manager)
        {
            this.project = manager;
        }

        /// <summary>
        /// Creates new Project Configuartion objects based on the configuration name.
        /// </summary>
        /// <param name="canonicalName">The name of the configuration</param>
        /// <returns>An instance of a ProjectConfig object.</returns>
        internal ProjectConfig GetProjectConfiguration(ConfigCanonicalName canonicalName)
        {
            // if we already created it, return the cached one
            if (configurationsList.ContainsKey(canonicalName))
            {
                return configurationsList[canonicalName];
            }

            ProjectConfig requestedConfiguration = CreateProjectConfiguration(canonicalName);
            configurationsList.Add(canonicalName, requestedConfiguration);

            return requestedConfiguration;
        }

        internal virtual ProjectConfig CreateProjectConfiguration(ConfigCanonicalName canonicalName)
        {
            return new ProjectConfig(this.project, canonicalName);
        }

        /// <summary>
        /// Provides access to the IVsProjectCfg interface implemented on a project's configuration object. 
        /// </summary>
        /// <param name="projectCfgCanonicalName">The canonical name of the configuration to access.</param>
        /// <param name="projectCfg">The IVsProjectCfg interface of the configuration identified by szProjectCfgCanonicalName.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int OpenProjectCfg(string projectCfgCanonicalName, out IVsProjectCfg projectCfg)
        {
            if (projectCfgCanonicalName == null)
            {
                throw new ArgumentNullException("projectCfgCanonicalName");
            }

            projectCfg = null;

            // Be robust in release
            if (projectCfgCanonicalName == null)
            {
                return VSConstants.E_INVALIDARG;
            }


            Debug.Assert(this.project != null && this.project.BuildProject != null);

            string[] configs = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);
            string[] platforms = GetPropertiesConditionedOn(ProjectFileConstants.Platform);
            var configCanonicalName = new ConfigCanonicalName(projectCfgCanonicalName);

            foreach (string config in configs)
            {
                foreach (string platform in platforms)
                {
                    if (configCanonicalName == new ConfigCanonicalName(config, platform))
                    {
                        projectCfg = this.GetProjectConfiguration(configCanonicalName);
                        if (projectCfg != null)
                        {
                            return VSConstants.S_OK;
                        }
                        else
                        {
                            return VSConstants.E_FAIL;
                        }
                    }
                }
            }

            return VSConstants.E_INVALIDARG;
        }

        /// <summary>
        /// Checks whether or not this configuration provider uses independent configurations. 
        /// </summary>
        /// <param name="usesIndependentConfigurations">true if independent configurations are used, false if they are not used. By default returns true.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int get_UsesIndependentConfigurations(out int usesIndependentConfigurations)
        {
            usesIndependentConfigurations = 1;
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Copies an existing configuration name or creates a new one. 
        /// </summary>
        /// <param name="name">The name of the new configuration.</param>
        /// <param name="cloneName">the name of the configuration to copy, or a null reference, indicating that AddCfgsOfCfgName should create a new configuration.</param>
        /// <param name="fPrivate">Flag indicating whether or not the new configuration is private. If fPrivate is set to true, the configuration is private. If set to false, the configuration is public. This flag can be ignored.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int AddCfgsOfCfgName(string name, string cloneName, int fPrivate)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            // Get all configs
            this.project.BuildProject.ReevaluateIfNecessary();
            List<Microsoft.Build.Construction.ProjectPropertyGroupElement> configGroup = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>(this.project.BuildProject.Xml.PropertyGroups);
            // platform -> property group
            var configToClone = new Dictionary<string,Microsoft.Build.Construction.ProjectPropertyGroupElement>(StringComparer.Ordinal);


            if (cloneName != null)
            {
                // Find the configuration to clone
                foreach (var currentConfig in configGroup)
                {
                    // Only care about conditional property groups
                    if (currentConfig.Condition == null || currentConfig.Condition.Length == 0)
                        continue;
                    var configCanonicalName = ConfigCanonicalName.OfCondition(currentConfig.Condition);

                    // Skip if it isn't the group we want
                    if (String.Compare(configCanonicalName.ConfigName, cloneName, StringComparison.OrdinalIgnoreCase) != 0)
                        continue;

                    if (!configToClone.ContainsKey(configCanonicalName.Platform))
                        configToClone.Add(configCanonicalName.Platform, currentConfig);
                }
            }


            var platforms = GetPlatformsFromProject();
            if (platforms.Length == 0) platforms = new[] { String.Empty };

            foreach (var platform in platforms)
            {
                // If we have any property groups to clone, and we do not have sourec for this platform, skip
                if (configToClone.Count > 0 && !configToClone.ContainsKey(platform)) continue;
                var newCanonicalName = new ConfigCanonicalName(name, platform);

                Microsoft.Build.Construction.ProjectPropertyGroupElement newConfig = null;
                if (configToClone.ContainsKey(platform))
                {
                    // Clone the configuration settings
                    newConfig = this.project.ClonePropertyGroup(configToClone[platform]);
                    //Will be added later with the new values to the path
                    foreach (Microsoft.Build.Construction.ProjectPropertyElement property in newConfig.Properties)
                    {
                        if (property.Name.Equals("OutputPath", StringComparison.OrdinalIgnoreCase))
                        {
                            property.Parent.RemoveChild(property);
                        }
                    }
                }
                else
                {
                    // no source to clone from, lets just create a new empty config
                    PopulateEmptyConfig(ref newConfig);
                    if (!String.IsNullOrEmpty(newCanonicalName.MSBuildPlatform))
                        newConfig.AddProperty(ProjectFileConstants.PlatformTarget, newCanonicalName.PlatformTarget);
                }


                //add the output path
                this.AddOutputPath(newConfig, name);

                // Set the condition that will define the new configuration
                string newCondition = newCanonicalName.ToMSBuildCondition();
                newConfig.Condition = newCondition;
            }
            NotifyOnCfgNameAdded(name);
            return VSConstants.S_OK;
        }


        private void PopulateEmptyConfig(ref Microsoft.Build.Construction.ProjectPropertyGroupElement newConfig)
        {
            newConfig = this.project.BuildProject.Xml.AddPropertyGroup();
            // Get the list of property name, condition value from the config provider
            IList<KeyValuePair<KeyValuePair<string, string>, string>> propVals = this.NewConfigProperties;
            foreach (KeyValuePair<KeyValuePair<string, string>, string> data in propVals)
            {
                KeyValuePair<string, string> propData = data.Key;
                string value = data.Value;
                Microsoft.Build.Construction.ProjectPropertyElement newProperty = newConfig.AddProperty(propData.Key, value);
                if (!String.IsNullOrEmpty(propData.Value))
                    newProperty.Condition = propData.Value;
            }

        }


        private void AddOutputPath(Microsoft.Build.Construction.ProjectPropertyGroupElement newConfig, string configName)
        {
            //add the output path
            string outputBasePath = this.ProjectMgr.OutputBaseRelativePath;
            if (outputBasePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                outputBasePath = Path.GetDirectoryName(outputBasePath);
                newConfig.AddProperty("OutputPath", Path.Combine(outputBasePath, configName) + Path.DirectorySeparatorChar.ToString());

        }

        /// <summary>
        /// Copies an existing platform name or creates a new one. 
        /// </summary>
        /// <param name="platformName">The name of the new platform.</param>
        /// <param name="clonePlatformName">The name of the platform to copy, or a null reference, indicating that AddCfgsOfPlatformName should create a new platform.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int AddCfgsOfPlatformName(string platformName, string clonePlatformName)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            // Get all configs
            this.project.BuildProject.ReevaluateIfNecessary();
            List<Microsoft.Build.Construction.ProjectPropertyGroupElement> configGroup = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>(this.project.BuildProject.Xml.PropertyGroups);
            // configName -> property group
            var configToClone = new Dictionary<string, Microsoft.Build.Construction.ProjectPropertyGroupElement>(StringComparer.Ordinal);


            if (clonePlatformName != null)
            {
                // Find the configuration to clone
                foreach (var currentConfig in configGroup)
                {
                    // Only care about conditional property groups
                    if (currentConfig.Condition == null || currentConfig.Condition.Length == 0)
                        continue;
                    var configCanonicalName = ConfigCanonicalName.OfCondition(currentConfig.Condition);

                    // Skip if it isn't the group we want
                    if (!configCanonicalName.MatchesPlatform(clonePlatformName))
                        continue;

                    if (!configToClone.ContainsKey(configCanonicalName.ConfigName))
                        configToClone.Add(configCanonicalName.ConfigName, currentConfig);
                }
            }


            var configNames = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);
            if (configNames.Length == 0) return VSConstants.E_FAIL;

            foreach (var configName in configNames)
            {
                // If we have any property groups to clone, and we do not have sourec for this config, skip
                if (configToClone.Count > 0 && !configToClone.ContainsKey(configName)) continue;
                var newCanonicalName = new ConfigCanonicalName(configName, platformName);

                Microsoft.Build.Construction.ProjectPropertyGroupElement newConfig = null;
                if (configToClone.ContainsKey(configName))
                {
                    // Clone the configuration settings
                    newConfig = this.project.ClonePropertyGroup(configToClone[configName]);
                    foreach (Microsoft.Build.Construction.ProjectPropertyElement property in newConfig.Properties)
                    {
                        if (property.Name.Equals(ProjectFileConstants.PlatformTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            property.Parent.RemoveChild(property);
                        }
                    }

                }
                else
                {
                    // no source to clone from, lets just create a new empty config
                    PopulateEmptyConfig(ref newConfig);
                    this.AddOutputPath(newConfig, configName);
                }

                newConfig.AddProperty(ProjectFileConstants.PlatformTarget, newCanonicalName.PlatformTarget);

                // Set the condition that will define the new configuration
                string newCondition = newCanonicalName.ToMSBuildCondition();
                newConfig.Condition = newCondition;
            }
            NotifyOnPlatformNameAdded(platformName);
            return VSConstants.S_OK;

        }

        /// <summary>
        /// Deletes a specified configuration name. 
        /// </summary>
        /// <param name="name">The name of the configuration to be deleted.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int DeleteCfgsOfCfgName(string name)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            if (name == null)
            {
                Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Name of the configuration should not be null if you want to delete it from project: {0}", MSBuildProject.GetFullPath((this.project.BuildProject))));
                // The configuration " '$(Configuration)' ==  " does not exist, so technically the goal
                // is achieved so return S_OK
                return VSConstants.S_OK;
            }

            this.project.BuildProject.ReevaluateIfNecessary();
            var configGroups = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>(this.project.BuildProject.Xml.PropertyGroups);
            var groupsToDelete = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>();

            foreach (var config in configGroups)
            {
                var configCanonicalName = ConfigCanonicalName.OfCondition(config.Condition);
                if (configCanonicalName.MatchesConfigName(name))
                {
                    groupsToDelete.Add(config);
                    configurationsList.Remove(configCanonicalName);
                }
            }

            foreach (var group in groupsToDelete)
            {
                group.Parent.RemoveChild(group);
            }

            NotifyOnCfgNameDeleted(name);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Deletes a specified platform name. 
        /// </summary>
        /// <param name="platName">The platform name to delete.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int DeleteCfgsOfPlatformName(string platName)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            if (platName == null)
            {
                Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Name of the platform should not be null if you want to delete it from project: {0}", MSBuildProject.GetFullPath((this.project.BuildProject))));
                return VSConstants.S_OK;
            }

            this.project.BuildProject.ReevaluateIfNecessary();
            var configGroups = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>(this.project.BuildProject.Xml.PropertyGroups);
            var groupsToDelete = new List<Microsoft.Build.Construction.ProjectPropertyGroupElement>();

            foreach (var config in configGroups)
            {
                var configCanonicalName = ConfigCanonicalName.OfCondition(config.Condition);
                if (configCanonicalName.MatchesPlatform(platName))
                {
                    groupsToDelete.Add(config);
                    configurationsList.Remove(configCanonicalName);
                }
            }

            foreach (var group in groupsToDelete)
            {
                group.Parent.RemoveChild(group);
            }

            NotifyOnPlatformNameDeleted(platName);

            return VSConstants.S_OK;

        }

        /// <summary>
        /// Returns the existing configurations stored in the project file.
        /// </summary>
        /// <param name="celt">Specifies the requested number of property names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of configuration property names specified by celt. This parameter can also be a null reference if the celt parameter is zero. 
        /// On output, names contains configuration property names.</param>
        /// <param name="actual">The actual number of property names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgNames(uint celt, string[] names, uint[] actual)
        {
            // get's called twice, once for allocation, then for retrieval            
            int i = 0;

            string[] configList = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);
            if (configList.Length == 0)
            {
                configList = new[] { ProjectConfig.Debug };
            }

            if (names != null)
            {
                foreach (string config in configList)
                {
                    names[i++] = config;
                    if (i == celt)
                        break;
                }
            }
            else
                i = configList.Length;

            if (actual != null)
            {
                actual[0] = (uint)i;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns the configuration associated with a specified configuration or platform name. 
        /// </summary>
        /// <param name="name">The name of the configuration to be returned.</param>
        /// <param name="platName">The name of the platform for the configuration to be returned.</param>
        /// <param name="cfg">The implementation of the IVsCfg interface.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgOfName(string name, string platName, out IVsCfg cfg)
        {
            cfg = null;
            cfg = this.GetProjectConfiguration(new ConfigCanonicalName(name, platName));

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns a specified configuration property. 
        /// </summary>
        /// <param name="propid">Specifies the property identifier for the property to return. For valid propid values, see __VSCFGPROPID.</param>
        /// <param name="var">The value of the property.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgProviderProperty(int propid, out object var)
        {
            var = false;
            switch ((__VSCFGPROPID)propid)
            {
                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgAdd:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgDelete:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgRename:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformAdd:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformDelete:
                    var = true;
                    break;
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns the per-configuration objects for this object. 
        /// </summary>
        /// <param name="celt">Number of configuration objects to be returned or zero, indicating a request for an unknown number of objects.</param>
        /// <param name="a">On input, pointer to an interface array or a null reference. On output, this parameter points to an array of IVsCfg interfaces belonging to the requested configuration objects.</param>
        /// <param name="actual">The number of configuration objects actually returned or a null reference, if this information is not necessary.</param>
        /// <param name="flags">Flags that specify settings for project configurations, or a null reference (Nothing in Visual Basic) if no additional flag settings are required. For valid prgrFlags values, see __VSCFGFLAGS.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags)
        {
            if (flags != null)
                flags[0] = 0;

            int i = 0;
            string[] configList = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);
            string[] platforms = GetPropertiesConditionedOn(ProjectFileConstants.Platform);
            if (configList.Length == 0)
            {
                configList = new[] { ProjectConfig.Debug };
            }
            if (platforms.Length == 0)
            {
                platforms = new[] { ProjectConfig.AnyCPU };
            }

            if (a != null)
            {
                foreach (string configName in configList)
                {
                    foreach (string platformName in platforms)
                    {
                        a[i] = this.GetProjectConfiguration(new ConfigCanonicalName(configName, platformName));

                        i++;
                        if (i == celt)
                            break;
                    }
                    if (i == celt)
                        break;
                }
            }
            else
                i = configList.Length * platforms.Length;

            if (actual != null)
                actual[0] = (uint)i;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns one or more platform names. 
        /// </summary>
        /// <param name="celt">Specifies the requested number of platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of platform names specified by celt. This parameter can also be a null reference if the celt parameter is zero. On output, names contains platform names.</param>
        /// <param name="actual">The actual number of platform names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetPlatformNames(uint celt, string[] names, uint[] actual)
        {
            string[] platforms = this.GetPlatformsFromProject();
            return GetPlatforms(celt, names, actual, platforms);
        }

        /// <summary>
        /// Returns the set of platforms that are installed on the user's machine. 
        /// </summary>
        /// <param name="celt">Specifies the requested number of supported platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of names specified by celt. This parameter can also be a null reference (Nothing in Visual Basic)if the celt parameter is zero. On output, names contains the names of supported platforms</param>
        /// <param name="actual">The actual number of platform names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual)
        {
            string[] platforms = this.GetSupportedPlatformsFromProject();
            return GetPlatforms(celt, names, actual, platforms);
        }

        /// <summary>
        /// Assigns a new name to a configuration. 
        /// </summary>
        /// <param name="old">The old name of the target configuration.</param>
        /// <param name="newname">The new name of the target configuration.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int RenameCfgsOfCfgName(string old, string newname)
        {
            this.project.BuildProject.ReevaluateIfNecessary();
            foreach (var config in this.project.BuildProject.Xml.PropertyGroups)
            {
                // Only care about conditional property groups
                if (config.Condition == null || config.Condition.Length == 0)
                    continue;

                var configCanonicalName = ConfigCanonicalName.OfCondition(config.Condition);
                // Skip if it isn't the group we want
                if (!configCanonicalName.MatchesConfigName(old))
                    continue;

                var newCanonicalName = new ConfigCanonicalName(newname, configCanonicalName.Platform);
                // Change the name 
                config.Condition = newCanonicalName.ToMSBuildCondition();
                var propertyCollection = config.Properties;
                var outputPathProperty = propertyCollection.Where(p => p.Name == ProjectFileConstants.OutputPath).FirstOrDefault();
                if (outputPathProperty != null)
                {
                    string outputBasePath = this.ProjectMgr.OutputBaseRelativePath;
                    if (outputBasePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        outputBasePath = Path.GetDirectoryName(outputBasePath);
                    var expectedOutputPathValue = Path.Combine(outputBasePath, old);
                    if (String.Equals(expectedOutputPathValue, outputPathProperty.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        var newOutputPathValue = Path.Combine(outputBasePath, newname);
                        config.SetProperty(ProjectFileConstants.OutputPath, newOutputPathValue);
                    }
                }
                // Update the name in our config list   
                if (configurationsList.ContainsKey(configCanonicalName))
                {
                    ProjectConfig configuration = configurationsList[configCanonicalName];
                    configurationsList.Remove(configCanonicalName);
                    configurationsList.Add(newCanonicalName, configuration);
                    // notify the configuration of its new name
                    configuration.ConfigName = newname;
                }                

            }
            NotifyOnCfgNameRenamed(old, newname);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Cancels a registration for configuration event notification. 
        /// </summary>
        /// <param name="cookie">The cookie used for registration.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int UnadviseCfgProviderEvents(uint cookie)
        {
            this.cfgEventSinks.RemoveAt(cookie);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Registers the caller for configuration event notification. 
        /// </summary>
        /// <param name="sink">Reference to the IVsCfgProviderEvents interface to be called to provide notification of configuration events.</param>
        /// <param name="cookie">Reference to a token representing the completed registration</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie)
        {
            cookie = this.cfgEventSinks.Add(sink);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Proved access to an IDispatchable object being a list of configuration properties
        /// </summary>
        /// <param name="configurationName">Combined Name and Platform for the configuration requested</param>
        /// <param name="configurationProperties">The IDispatchcable object</param>
        /// <returns>S_OK if successful</returns>
        public virtual int GetAutomationObject(string configurationName, out object configurationProperties)
        {
            //Init out param
            configurationProperties = null;

            var canonicalCfgName = new ConfigCanonicalName(configurationName);

            // Get the configuration
            IVsCfg cfg;
            ErrorHandler.ThrowOnFailure(this.GetCfgOfName(canonicalCfgName.ConfigName, canonicalCfgName.Platform, out cfg));

            // Get the properties of the configuration
            configurationProperties = ((ProjectConfig)cfg).ConfigurationProperties;

            return VSConstants.S_OK;

        }

        /// <summary>
        /// Called when a new configuration name was added.
        /// </summary>
        /// <param name="name">The name of configuration just added.</param>
        private void NotifyOnCfgNameAdded(string name)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameAdded(name));
            }
        }

        /// <summary>
        /// Called when a config name was deleted.
        /// </summary>
        /// <param name="name">The name of the configuration.</param>
        private void NotifyOnCfgNameDeleted(string name)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameDeleted(name));
            }
        }


        /// <summary>
        /// Called when a config name was renamed
        /// </summary>
        /// <param name="oldName">Old configuration name</param>
        /// <param name="newName">New configuration name</param>
        private void NotifyOnCfgNameRenamed(string oldName, string newName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameRenamed(oldName, newName));
            }
        }

        /// <summary>
        /// Called when a platform name was added
        /// </summary>
        /// <param name="platformName">The name of the platform.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private void NotifyOnPlatformNameAdded(string platformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnPlatformNameAdded(platformName));
            }
        }

        /// <summary>
        /// Called when a platform name was deleted
        /// </summary>
        /// <param name="platformName">The name of the platform.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private void NotifyOnPlatformNameDeleted(string platformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnPlatformNameDeleted(platformName));
            }
        }

        /// <summary>
        /// Gets all the platforms defined in the project
        /// </summary>
        /// <returns>An array of platform names.</returns>
        private string[] GetPlatformsFromProject()
        {
            string[] platforms = GetPropertiesConditionedOn(ProjectFileConstants.Platform);
            if (platforms.Length == 0)
            {
                platforms = new[] { ProjectConfig.AnyCPU };
            }

            for (int i = 0; i < platforms.Length; i++)
            {
                platforms[i] = new ConfigCanonicalName("", platforms[i]).Platform;
            }

            return platforms;
        }

        /// <summary>
        /// Return the supported platform names.
        /// </summary>
        /// <returns>An array of supported platform names.</returns>
        private string[] GetSupportedPlatformsFromProject()
        {
            this.project.BuildProject.ReevaluateIfNecessary();
            string platforms = this.ProjectMgr.BuildProject.GetPropertyValue(ProjectFileConstants.AvailablePlatforms);

            if (platforms == null)
            {
                return new string[] { };
            }

            if (platforms.Contains(","))
            {
                return platforms.Split(',');
            }

            return new string[] { platforms };
        }


        /// <summary>
        /// Common method for handling platform names.
        /// </summary>
        /// <param name="celt">Specifies the requested number of platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of platform names specified by celt. This parameter can also be null if the celt parameter is zero. On output, names contains platform names</param>
        /// <param name="actual">A count of the actual number of platform names returned.</param>
        /// <param name="platforms">An array of available platform names</param>
        /// <returns>A count of the actual number of platform names returned.</returns>
        /// <devremark>The platforms array is never null. It is assured by the callers.</devremark>
        private static int GetPlatforms(uint celt, string[] names, uint[] actual, string[] platforms)
        {
            Debug.Assert(platforms != null, "The plaforms array should never be null");
            if (names == null)
            {
                if (actual == null || actual.Length == 0)
                {
                    throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "actual");
                }

                actual[0] = (uint)platforms.Length;
                return VSConstants.S_OK;
            }

            //Degenarate case
            if (celt == 0)
            {
                if (actual != null && actual.Length != 0)
                {
                    actual[0] = (uint)platforms.Length;
                }

                return VSConstants.S_OK;
            }

            uint returned = 0;
            for (int i = 0; i < platforms.Length && names.Length > returned; i++)
            {
                names[returned] = platforms[i];
                returned++;
            }

            if (actual != null && actual.Length != 0)
            {
                actual[0] = returned;
            }

            if (celt > returned)
            {
                return VSConstants.S_FALSE;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Get all the configurations in the project.
        /// </summary>
        private string[] GetPropertiesConditionedOn(string constant)
        {
            List<string> configurations;            
            this.project.BuildProject.ReevaluateIfNecessary();
            this.project.BuildProject.ConditionedProperties.TryGetValue(constant, out configurations);
            return (configurations == null) ? new string[] { } : configurations.ToArray();
        }
    }
}
