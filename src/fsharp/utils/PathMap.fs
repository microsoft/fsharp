// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Functions to map real paths to paths to be written to PDB/IL
namespace Internal.Utilities

open System
open System.IO

open FSharp.Compiler.IO

type PathMap = PathMap of Map<string, string>

[<RequireQualifiedAccess>]
module internal PathMap =

    let dirSepStr = Path.DirectorySeparatorChar.ToString()

    let empty = PathMap Map.empty

    let addMapping (src : string) (dst : string) (PathMap map) : PathMap =
        // Normalise the path
        let normalSrc = FileSystem.GetFullPathShim src

        let oldPrefix =
            if normalSrc.EndsWith dirSepStr then normalSrc
            else normalSrc + dirSepStr

        // Always add a path separator
        map |> Map.add oldPrefix dst |> PathMap

    // Map a file path with its replacement.
    // This logic replicates C#'s PathUtilities.NormalizePathPrefix
    let apply (PathMap map) (filePath : string) : string =
        // Find the first key in the path map that matches a prefix of the
        // normalized path. We expect the client to use consistent capitalization;
        // we use ordinal (case-sensitive) comparisons.
        map
        |> Map.tryPick (fun oldPrefix replacementPrefix ->
            // oldPrefix always ends with a path separator, so there's no need
            // to check if it was a partial match
            // e.g. for the map /goo=/bar and filename /goooo
            if filePath.StartsWith(oldPrefix, StringComparison.Ordinal) then
                let replacement = replacementPrefix + filePath.Substring (oldPrefix.Length - 1)

                // Normalize the path separators if used uniformly in the replacement
                let hasSlash = replacementPrefix.IndexOf '/' >= 0
                let hasBackslash = replacementPrefix.IndexOf '\\' >= 0

                if hasSlash && not hasBackslash then replacement.Replace('\\', '/')
                elif hasBackslash && not hasSlash then replacement.Replace('/', '\\')
                else replacement
                |> Some
            else
                None
        )
        |> Option.defaultValue filePath

    let applyDir pathMap (dirName : string) : string =
        if dirName.EndsWith dirSepStr then apply pathMap dirName
        else
            let mapped = apply pathMap (dirName + dirSepStr)
            mapped.TrimEnd (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
