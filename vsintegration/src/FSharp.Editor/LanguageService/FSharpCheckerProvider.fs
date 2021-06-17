// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.ComponentModel.Composition
open System.Diagnostics
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.CodeAnalysis
open FSharp.NativeInterop
open Microsoft.CodeAnalysis
open Microsoft.VisualStudio
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.LanguageServices
open Microsoft.VisualStudio.LanguageServices.ProjectSystem
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Diagnostics

#nowarn "9" // NativePtr.toNativeInt

// Exposes FSharpChecker as MEF export
[<Export(typeof<FSharpCheckerProvider>); Composition.Shared>]
type internal FSharpCheckerProvider 
    [<ImportingConstructor>]
    (
        [<Import(typeof<VisualStudioWorkspace>)>] workspace: VisualStudioWorkspace,
        projectContextFactory: IWorkspaceProjectContextFactory,
        settings: EditorOptions
    ) =

    let metadataAsSource = FSharpMetadataAsSourceService(projectContextFactory)

    let tryGetMetadataSnapshot (path, timeStamp) = 
        try
            let md = Microsoft.CodeAnalysis.ExternalAccess.FSharp.LanguageServices.FSharpVisualStudioWorkspaceExtensions.GetMetadata(workspace, path, timeStamp)
            let amd = (md :?> AssemblyMetadata)
            let mmd = amd.GetModules().[0]
            let mmr = mmd.GetMetadataReader()

            // "lifetime is timed to Metadata you got from the GetMetadata(...). As long as you hold it strongly, raw 
            // memory we got from metadata reader will be alive. Once you are done, just let everything go and 
            // let finalizer handle resource rather than calling Dispose from Metadata directly. It is shared metadata. 
            // You shouldn't dispose it directly."

            let objToHold = box md

            // We don't expect any ilread WeakByteFile to be created when working in Visual Studio
            // Debug.Assert((FSharp.Compiler.AbstractIL.ILBinaryReader.GetStatistics().weakByteFileCount = 0), "Expected weakByteFileCount to be zero when using F# in Visual Studio. Was there a problem reading a .NET binary?")

            Some (objToHold, NativePtr.toNativeInt mmr.MetadataPointer, mmr.MetadataLength)
        with ex -> 
            // We catch all and let the backup routines in the F# compiler find the error
            Assert.Exception(ex)
            None 

    let checker = 
        lazy
            let checker = 
                FSharpChecker.Create(
                    projectCacheSize = settings.LanguageServicePerformance.ProjectCheckCacheSize, 
                    keepAllBackgroundResolutions = false,
                    // Enabling this would mean that if devenv.exe goes above 2.3GB we do a one-off downsize of the F# Compiler Service caches
                    (* , MaxMemory = 2300 *)
                    legacyReferenceResolver=LegacyMSBuildReferenceResolver.getResolver(),
                    tryGetMetadataSnapshot = tryGetMetadataSnapshot,
                    keepAllBackgroundSymbolUses = false,
                    enableBackgroundItemKeyStoreAndSemanticClassification = true,
                    enablePartialTypeChecking = true)
            checker

    member this.Checker = checker.Value

    member _.MetadataAsSource = metadataAsSource

