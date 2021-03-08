// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Immutable
open System.ComponentModel.Composition
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Microsoft.VisualStudio.ProjectSystem
open Microsoft.VisualStudio.ProjectSystem.Build

// We can't use well-known constants here because `string + string` isn't a valid constant expression in F#.
[<AppliesTo("FSharp&LanguageService")>]
[<ExportBuildGlobalPropertiesProvider(designTimeBuildProperties = true)>]
[<ExportBuildGlobalPropertiesProvider(designTimeBuildProperties = false)>]
type internal SetGlobalPropertiesForSdkProjects
    [<ImportingConstructor>]
    (
        projectService: IProjectService
    ) =
    inherit StaticGlobalPropertiesProviderBase(projectService.Services)

    override _.GetGlobalPropertiesAsync(_cancellationToken: CancellationToken): Task<IImmutableDictionary<string, string>> =
        let properties = Empty.PropertiesMap.Add("FSharpCompilerPath", Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Tools"))
        Task.FromResult<IImmutableDictionary<string, string>>(properties)
