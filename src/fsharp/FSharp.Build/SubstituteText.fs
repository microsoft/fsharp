// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Build

open System
open System.Collections
open System.IO
open Microsoft.Build.Framework
open Microsoft.Build.Utilities

type SubstituteText () =

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    let mutable _buildEngine : IBuildEngine = null
    let mutable _hostObject : ITaskHost = null
    let mutable embeddedResources : ITaskItem[] = [||]
#else
    let mutable _buildEngine : IBuildEngine? = null
    let mutable _hostObject : ITaskHost? = null
    let mutable embeddedResources : ITaskItem[]? = [||]
#endif

    let mutable copiedFiles = new ResizeArray<ITaskItem>()

    [<Required>]
    member this.EmbeddedResources
        with get() = embeddedResources
         and set(value) = embeddedResources <- value

    [<Output>]
    member this.CopiedFiles
        with get() = copiedFiles.ToArray()

    interface ITask with
        member this.BuildEngine
            with get() = _buildEngine
             and set(value) = _buildEngine <- value

        member this.HostObject
            with get() = _hostObject
             and set(value) = _hostObject <- value

        member this.Execute() =
            copiedFiles.Clear()
            if not(isNull embeddedResources) then
                for item in embeddedResources do
                    // Update ITaskItem metadata to point to new location
                    let sourcePath = item.GetMetadata("FullPath")

                    let pattern1 = item.GetMetadata("Pattern1")
                    let pattern2 = item.GetMetadata("Pattern2")

                    // Is there any replacement to do?
                    if not (String.IsNullOrWhiteSpace(pattern1) && String.IsNullOrWhiteSpace(pattern2)) then
                        if not(String.IsNullOrWhiteSpace(sourcePath)) then
                            try
                                let getTargetPathFrom key =
                                    let md = item.GetMetadata(key)
                                    let path = Path.GetDirectoryName(md)
                                    let filename = Path.GetFileName(md)
                                    let target = Path.Combine(path, @"..\resources", filename)
                                    target

                                // Copy from the location specified in Identity
                                let sourcePath=item.GetMetadata("Identity")

                                // Copy to the location specified in TargetPath unless no TargetPath is provided, then use Identity
                                let targetPath=
                                    let identityPath = getTargetPathFrom "Identity"
                                    let intermediateTargetPath = item.GetMetadata("IntermediateTargetPath")
                                    if not (String.IsNullOrWhiteSpace(intermediateTargetPath)) then
                                        let filename = Path.GetFileName(identityPath)
                                        let target = Path.Combine(intermediateTargetPath, filename)
                                        target
                                    else
                                        identityPath

                                item.ItemSpec <- targetPath

                                // Transform file
                                let mutable contents = File.ReadAllText(sourcePath)
                                if not (String.IsNullOrWhiteSpace(pattern1)) then
                                    let replacement = item.GetMetadata("Replacement1")
                                    contents <- contents.Replace(pattern1, replacement)
                                if not (String.IsNullOrWhiteSpace(pattern2)) then
                                    let replacement = item.GetMetadata("Replacement2")
                                    contents <- contents.Replace(pattern2, replacement)

                                let directory = Path.GetDirectoryName(targetPath)
                                if not(Directory.Exists(directory)) then
                                    Directory.CreateDirectory(directory) |>ignore

                                File.WriteAllText(targetPath, contents)
                            with
                            | _ -> ()

                    copiedFiles.Add(item)
            true
