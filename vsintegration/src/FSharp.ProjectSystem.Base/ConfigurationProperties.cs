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
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    /// <summary>
    /// Implements the configuration dependent properties interface
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class ProjectConfigProperties : 
         VSLangProj.ProjectConfigurationProperties
    {
        private ProjectConfig projectConfig;

        internal ProjectConfigProperties(ProjectConfig projectConfig)
        {
            this.projectConfig = projectConfig;
        }

        public virtual string OutputPath
        {
            get
            {
                return this.projectConfig.GetConfigurationProperty(BuildPropertyPageTag.OutputPath.ToString(), true);
            }
            set
            {
                this.projectConfig.SetConfigurationProperty(BuildPropertyPageTag.OutputPath.ToString(), value);
            }
        }

        public string __id
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.ConfigName); }
        }

        public bool AllowUnsafeBlocks
        {
            get { return false;  }
            set { throw new NotImplementedException(); }
        }

        public uint BaseAddress
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool CheckForOverflowUnderflow
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }

        public bool RemoveIntegerChecks
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }


        public bool IncrementalBuild
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }

        public bool StartWithIE
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }

        public string ConfigurationOverrideFile
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public string IntermediatePath
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.GetConfigurationProperty("IntermediatePath", false)); }
            set { UIThread.DoOnUIThread(() => { projectConfig.SetConfigurationProperty("IntermediatePath", value); }); }
        }


        public bool DebugSymbols
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.DebugSymbols); }
            set { projectConfig.DebugSymbols = value; }
        }
        public string DefineConstants
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.DefineConstants);  }
            set { UIThread.DoOnUIThread(() => { projectConfig.DefineConstants = value; }); }
        }

        private bool IsDefined(string constant)
        {
            var constants = projectConfig.DefineConstants;
            var array = constants.Split(';');
            return array.Contains(constant);
        }
        private void SetDefine(string constant, bool value)
        {
            var constants = projectConfig.DefineConstants;
            var array = constants.Split(';');
            if (value && !array.Contains(constant))
            {
                var l = new List<string>(array);
                l.Add(constant);
                projectConfig.DefineConstants = String.Join(";", l);
            }
            else if (!value && array.Contains(constant))
            {
                projectConfig.DefineConstants = String.Join(";", array.All(s => s != constant));
            }

        }
        public bool DefineDebug
        {
            get { return UIThread.DoOnUIThread(() => this.IsDefined("DEBUG")); }
            set { UIThread.DoOnUIThread(() => { this.SetDefine("DEBUG", value); }); }
        }
        public bool DefineTrace
        {
            get { return UIThread.DoOnUIThread(() => this.IsDefined("TRACE")); }
            set { UIThread.DoOnUIThread(() => { this.SetDefine("TRACE", value); }); }
        }

        public bool EnableASPDebugging
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool EnableASPXDebugging
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }


        public string StartPage
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public VSLangProj.prjStartAction StartAction
        {
            get
            {
                switch(UIThread.DoOnUIThread(() => projectConfig.StartAction))
                {
                    case 1: return VSLangProj.prjStartAction.prjStartActionProgram;
                    case 2: return VSLangProj.prjStartAction.prjStartActionURL;
                    default: return VSLangProj.prjStartAction.prjStartActionProject;
                }

            }
            set
            {
                int result;
                switch(value)
                {
                    case VSLangProj.prjStartAction.prjStartActionProject:
                        result = 0;
                        break;
                    case VSLangProj.prjStartAction.prjStartActionURL:
                        result = 2;
                        break;
                    case VSLangProj.prjStartAction.prjStartActionProgram:
                        result = 1;
                        break;
                    default:
                        result = 0;
                        break;
                }
                UIThread.DoOnUIThread(() => { projectConfig.StartAction = result; });
            }
        }
        public VSLangProj.prjWarningLevel WarningLevel 
        {
            get 
            {
                var level = UIThread.DoOnUIThread(() => projectConfig.WarningLevel);
                switch(level)
                {
                    case 0: return VSLangProj.prjWarningLevel.prjWarningLevel0;
                    case 1: return VSLangProj.prjWarningLevel.prjWarningLevel1;
                    case 2: return VSLangProj.prjWarningLevel.prjWarningLevel2;
                    case 3: return VSLangProj.prjWarningLevel.prjWarningLevel3;
                    default: return VSLangProj.prjWarningLevel.prjWarningLevel4;
                }

            }
            set 
            {
                UIThread.DoOnUIThread(() => projectConfig.WarningLevel = (int)value);
            }
        }

        public uint FileAlignment
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool RegisterForComInterop
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public string ExtenderCATID
        {
            get 
            {
                Guid catid = projectConfig.ProjectMgr.GetCATIDForType(this.GetType());
                if (Guid.Empty.CompareTo(catid) == 0)
                {
                    return null;
                }
                return catid.ToString("B");
            }
        }

        public object ExtenderNames
        {
            get
            {
                EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)projectConfig.ProjectMgr.GetService(typeof(EnvDTE.ObjectExtenders));
                if (extenderService == null)
                {
                    throw new InvalidOperationException();
                }
                return extenderService.GetExtenderNames(this.ExtenderCATID, projectConfig);
            }
        }
        public object get_Extender(string extenderName)
        {
            EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)projectConfig.ProjectMgr.GetService(typeof(EnvDTE.ObjectExtenders));
            if (extenderService == null)
            {
                throw new InvalidOperationException();
            }
            return extenderService.GetExtender(this.ExtenderCATID, extenderName, projectConfig);

        }

        public string DocumentationFile
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.DocumentationFile); }
            set { UIThread.DoOnUIThread(() => { projectConfig.DocumentationFile = value; }); }
        }

        public string StartProgram
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.StartProgram); }
            set { UIThread.DoOnUIThread(() => { projectConfig.StartProgram = value; }); }
        }

        public string StartWorkingDirectory
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.StartWorkingDirectory); }
            set { UIThread.DoOnUIThread(() => { projectConfig.StartWorkingDirectory = value; }); }
        }

        public string StartURL
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.StartURL); }
            set { UIThread.DoOnUIThread(() => { projectConfig.StartURL = value; }); }
        }

        public string StartArguments
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.StartArguments); }
            set { UIThread.DoOnUIThread(() => { projectConfig.StartArguments = value; }); }
        }

        public bool Optimize
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.Optimize); }
            set { UIThread.DoOnUIThread(() => { projectConfig.Optimize = value; }); }
        }

        public bool EnableUnmanagedDebugging
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.EnableUnmanagedDebugging); }
            set { UIThread.DoOnUIThread(() => { projectConfig.EnableUnmanagedDebugging = value; }); }
        }

        public bool TreatWarningsAsErrors
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.TreatWarningsAsErrors); }
            set { UIThread.DoOnUIThread(() => { projectConfig.TreatWarningsAsErrors = value; }); }
        }

        public bool EnableSQLServerDebugging
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.EnableSQLServerDebugging); }
            set { UIThread.DoOnUIThread(() => { projectConfig.EnableSQLServerDebugging = value; }); }
        }

        public bool RemoteDebugEnabled
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.RemoteDebugEnabled); }
            set { UIThread.DoOnUIThread(() => { projectConfig.RemoteDebugEnabled = value; }); }
        }

        public string RemoteDebugMachine
        {
            get { return UIThread.DoOnUIThread(() => projectConfig.RemoteDebugMachine); }
            set { UIThread.DoOnUIThread(() => { projectConfig.RemoteDebugMachine = value; }); }
        }

    }
}
