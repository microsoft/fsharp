namespace FSharp.Build

open System
open System.IO
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open System.Collections.Generic

(*
    This type is a translation of the matching MapSourceRoots task in Roslyn,
    which is planned to move to a shared location at some point in the future.

    Until then, this version will be used. The exact source used is:
        https://github.com/dotnet/roslyn/blob/69d3fb733e6c74a41c118bf905739163cf5aef2a/src/Compilers/Core/MSBuildTask/MapSourceRoots.cs,
    with matching targets usage at:
        https://github.com/dotnet/roslyn/blob/69d3fb733e6c74a41c118bf905739163cf5aef2a/src/Compilers/Core/MSBuildTask/Microsoft.Managed.Core.targets#L79-L127

*)

module Utilities =
    /// <summary>
    /// Copied from msbuild. ItemSpecs are normalized using this method.
    /// </summary>
    let FixFilePath (path: string) =
        if String.IsNullOrEmpty(path) || Path.DirectorySeparatorChar = '\\'
        then path
        else path.Replace('\\', '/');

/// <summary>
/// Given a list of SourceRoot items produces a list of the same items with added <c>MappedPath</c> metadata that
/// contains calculated deterministic source path for each SourceRoot.
/// </summary>
/// <remarks>
/// Does not perform any path validation.
///
/// The <c>MappedPath</c> is either the path (ItemSpec) itself, when <see cref="Deterministic"/> is false,
/// or a calculated deterministic source path (starting with prefix '/_/', '/_1/', etc.), otherwise.
/// </remarks>
type MapSourceRoots () =
    inherit Task ()

    static let MappedPath = "MappedPath"
    static let SourceControl = "SourceControl"
    static let NestedRoot = "NestedRoot"
    static let ContainingRoot = "ContainingRoot"
    static let RevisionId = "RevisionId"
    static let SourceLinkUrl = "SourceLinkUrl"
    static let knownMetadataNames =
        [
            SourceControl
            RevisionId
            NestedRoot
            ContainingRoot
            MappedPath
            SourceLinkUrl
        ]

    static let (|NullOrEmpty|HasValue|) (s: string) = if String.IsNullOrEmpty s then NullOrEmpty else HasValue s
    static let ensureEndsWithSlash (path: string) =
        if path.EndsWith "/"
        then path
        else path + "/"

    static let endsWithDirectorySeparator (path: string) =
        if path.Length = 0
        then false
        else
            let endChar = path.[path.Length - 1]
            endChar = Path.DirectorySeparatorChar || endChar = Path.AltDirectorySeparatorChar

    static let reportConflictingWellKnownMetadata (log: TaskLoggingHelper) (l: ITaskItem) (r: ITaskItem) =
        for name in knownMetadataNames do
            match l.GetMetadata name, r.GetMetadata name with
            | HasValue lValue, HasValue rValue when lValue <> rValue ->
                log.LogWarning(FSBuild.SR.mapSourceRootsContainsDuplicate(r.ItemSpec, name, lValue, rValue))
            | _, _ -> ()


    static member PerformMapping (log: TaskLoggingHelper) (sourceRoots: ITaskItem []) deterministic =
        let mappedSourceRoots = ResizeArray<_>()
        let rootByItemSpec = Dictionary<string, ITaskItem>();

        for sourceRoot in sourceRoots do
            // The SourceRoot is required to have a trailing directory separator.
            // We do not append one implicitly as we do not know which separator to append on Windows.
            // The usage of SourceRoot might be sensitive to what kind of separator is used (e.g. in SourceLink where it needs
            // to match the corresponding separators used in paths given to the compiler).
            if not (endsWithDirectorySeparator sourceRoot.ItemSpec)
            then
                log.LogError(FSBuild.SR.mapSourceRootsPathMustEndWithSlashOrBackslash sourceRoot.ItemSpec)

            match rootByItemSpec.TryGetValue sourceRoot.ItemSpec with
            | true, existingRoot ->
                reportConflictingWellKnownMetadata log existingRoot sourceRoot
                sourceRoot.CopyMetadataTo existingRoot
            | false, _ ->
                rootByItemSpec.[sourceRoot.ItemSpec] <- sourceRoot
                mappedSourceRoots.Add sourceRoot

        if log.HasLoggedErrors
        then None
        else
            if deterministic
            then
                let topLevelMappedPaths = Dictionary<_,_>()
                let setTopLevelMappedPaths isSourceControlled =

                    let mapNestedRootIfEmpty (root: ITaskItem) =
                        let localPath = root.ItemSpec
                        match root.GetMetadata NestedRoot with
                        | NullOrEmpty ->
                            // root isn't nested
                            if topLevelMappedPaths.ContainsKey(localPath)
                            then
                                log.LogError(FSBuild.SR.mapSourceRootsContainsDuplicate(localPath, NestedRoot, "", ""));
                            else
                                let index = topLevelMappedPaths.Count;
                                let mappedPath = "/_" + (if index = 0 then "" else string index) + "/"
                                topLevelMappedPaths.[localPath] <- mappedPath
                                root.SetMetadata(MappedPath, mappedPath)
                        | HasValue _ -> ()

                    for root in mappedSourceRoots do
                        match root.GetMetadata SourceControl with
                        | HasValue _ when isSourceControlled -> mapNestedRootIfEmpty root
                        | NullOrEmpty when not isSourceControlled -> mapNestedRootIfEmpty root
                        | _ -> ()

                // assign mapped paths to process source-controlled top-level roots first:
                setTopLevelMappedPaths true

                // then assign mapped paths to other source-controlled top-level roots:
                setTopLevelMappedPaths false

                if topLevelMappedPaths.Count = 0
                then
                    log.LogError(FSBuild.SR.mapSourceRootsNoTopLevelSourceRoot ())
                else
                    // finally, calculate mapped paths of nested roots:
                    for root in mappedSourceRoots do
                        match root.GetMetadata NestedRoot with
                        | HasValue nestedRoot ->
                            match root.GetMetadata ContainingRoot with
                            | HasValue containingRoot ->
                                // The value of ContainingRoot metadata is a file path that is compared with ItemSpec values of SourceRoot items.
                                // Since the paths in ItemSpec have backslashes replaced with slashes on non-Windows platforms we need to do the same for ContainingRoot.
                                match topLevelMappedPaths.TryGetValue(Utilities.FixFilePath(containingRoot)) with
                                | true, mappedTopLevelPath ->
                                    root.SetMetadata(MappedPath, mappedTopLevelPath + ensureEndsWithSlash(nestedRoot.Replace('\\', '/')));
                                | false, _ ->
                                    log.LogError(FSBuild.SR.mapSourceRootsNoSuchTopLevelSourceRoot containingRoot)
                            | NullOrEmpty ->
                                log.LogError(FSBuild.SR.mapSourceRootsNoSuchTopLevelSourceRoot "")
                        | NullOrEmpty -> ()
            else
                for root in mappedSourceRoots do
                    root.SetMetadata(MappedPath, root.ItemSpec)

            if log.HasLoggedErrors then None else Some (mappedSourceRoots.ToArray())


    /// <summary>
    /// SourceRoot items with the following optional well-known metadata:
    /// <list type="bullet">
    ///   <term>SourceControl</term><description>Indicates name of the source control system the source root is tracked by (e.g. Git, TFVC, etc.), if any.</description>
    ///   <term>NestedRoot</term><description>If a value is specified the source root is nested (e.g. git submodule). The value is a path to this root relative to the containing root.</description>
    ///   <term>ContainingRoot</term><description>Identifies another source root item that this source root is nested under.</description>
    /// </list>
    /// </summary>
    [<Required>]
    member val SourceRoots: ITaskItem[] = null with get, set

    /// <summary>
    /// True if the mapped paths should be deterministic.
    /// </summary>
    member val Deterministic = false with get, set

    /// <summary>
    /// SourceRoot items with <term>MappedPath</term> metadata set.
    /// Items listed in <see cref="SourceRoots"/> that have the same ItemSpec will be merged into a single item in this list.
    /// </summary>
    [<Output>]
    member val MappedSourceRoots: ITaskItem[] = null with get, set

    override this.Execute() =
        match MapSourceRoots.PerformMapping this.Log this.SourceRoots this.Deterministic with
        | None ->
            false
        | Some mappings ->
            this.MappedSourceRoots <- mappings
            true