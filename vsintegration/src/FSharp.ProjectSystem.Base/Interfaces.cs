// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    /// <summary>
    /// This interface defines the rules for handling build dependency on a project container.
    /// </summary>
    [ComVisible(true)]
    [CLSCompliant(false)]
    public interface IBuildDependencyOnProjectContainer
    {
        /// <summary>
        /// Defines whether the nested projects should be build with the parent project.
        /// </summary>
        bool BuildNestedProjectsOnBuild
        {
            get;
            set;
        }

        /// <summary>
        /// Enumerates the nested hierachies present that will participate in the build dependency update.
        /// </summary>
        /// <returns>A list of hierrachies.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Hierachies")]
        IVsHierarchy[] EnumNestedHierachiesForBuildDependency();
    }

    /// <summary>
    /// Interface for manipulating build dependency
    /// </summary>
    [ComVisible(true)]
    [CLSCompliant(false)]
    public interface IBuildDependencyUpdate
    {
        /// <summary>
        /// Defines a container for storing BuildDependencies
        /// </summary>
        IVsBuildDependency[] BuildDependencies
        {
            get;
        }

        /// <summary>
        /// Adds a BuildDependency to the container
        /// </summary>
        /// <param name="dependency">The dependency to add</param>
        void AddBuildDependency(IVsBuildDependency dependency);

        /// <summary>
        /// Removes the builddependency from teh container.
        /// </summary>
        /// <param name="dependency">The dependency to add</param>
        void RemoveBuildDependency(IVsBuildDependency dependency);

    }

    /// <summary>
    /// Provides access to the reference data container.
    /// </summary>
    [ComVisible(true)]
    public interface IReferenceContainerProvider
    {
        IReferenceContainer GetReferenceContainer();
    }

    /// <summary>
    /// Defines a container for manipulating references
    /// </summary>
    [ComVisible(true)]
    public interface IReferenceContainer
    {
        IList<ReferenceNode> EnumReferences();
        ReferenceNode AddReferenceFromSelectorData(VSCOMPONENTSELECTORDATA selectorData);
        void LoadReferencesFromBuildProject(Microsoft.Build.Evaluation.Project buildProject);
    }

    /// <summary>
    /// Defines the events that are internally defined for communication with other subsytems.
    /// </summary>
    [ComVisible(true)]
    public interface IProjectEvents
    {
        /// <summary>
        /// Event raised just after the project file opened.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        event EventHandler<AfterProjectFileOpenedEventArgs> AfterProjectFileOpened;

        /// <summary>
        /// Event raised before the project file closed.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        event EventHandler<BeforeProjectFileClosedEventArgs> BeforeProjectFileClosed;
    }

    /// <summary>
    /// Defines the interface that will specify ehethrr the object is a project events listener.
    /// </summary>
    [ComVisible(true)]
    public interface IProjectEventsListener
    {
        /// <summary>
        /// Is the object a project events listener.
        /// </summary>
        /// <returns></returns>
        bool IsProjectEventsListener
        { get; set; }

    }

    /// <summary>
    /// Defines support for single file generator
    /// </summary>
    internal interface ISingleFileGenerator
    {
        ///<summary>
        /// Runs the generator on the item represented by the document moniker.
        /// </summary>
        /// <param name="document"></param>
        void RunGenerator(string document);
    }
}
