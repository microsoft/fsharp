﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Diagnostics

type FSharpDiagnosticKind =
    | AddIndexerDot
    | ReplaceWithSuggestion of suggestion:string

[<RequireQualifiedAccess>]
module CompilerDiagnostics =

    let GetErrorMessage diagnosticKind =
        match diagnosticKind with
        | AddIndexerDot -> FSComp.SR.addIndexerDot()
        | ReplaceWithSuggestion s -> FSComp.SR.replaceWithSuggestion(s)