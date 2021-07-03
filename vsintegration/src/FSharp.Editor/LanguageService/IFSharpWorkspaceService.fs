// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Immutable
open System.Collections.Concurrent
open Microsoft.CodeAnalysis
open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.CodeAnalysis.Host

type internal IFSharpWorkspaceService =
    inherit IWorkspaceService

    /// Store command line options, used for Cps projects
    abstract CommandLineOptions : ConcurrentDictionary<ProjectId, string[] * string[]>

    /// Legacy project mappings 
    abstract LegacyProjectSites : ConcurrentDictionary<ProjectId, IProjectSite>

    abstract ScriptUpdatedEvent : Event<FSharpProjectOptions>