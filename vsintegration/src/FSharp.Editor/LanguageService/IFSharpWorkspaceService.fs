// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.CodeAnalysis.Host

// Used to expose FSharpChecker/ProjectInfo manager to diagnostic providers
// Diagnostic providers can be executed in environment that does not use MEF so they can rely only
// on services exposed by the workspace
type internal IFSharpWorkspaceService =
    inherit IWorkspaceService
    abstract Checker: FSharpChecker
    abstract FSharpProjectOptionsManager: FSharpProjectOptionsManager