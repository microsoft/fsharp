// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.DependencyManager.Nuget

module internal FSharpDependencyManager =
    val formatPackageReference: PackageReference -> seq<string>
    val parsePackageReference: scriptExt: string -> string list -> PackageReference list * string option option * int option
    val parsePackageDirective: scriptExt: string -> (string * string) list -> PackageReference list * string option option * int option

/// The results of ResolveDependencies
[<Class>]
type ResolveDependenciesResult =

    /// Succeded?
    member Success: bool

    /// The resolution output log
    member StdOut: string[]

    /// The resolution error log (process stderr)
    member StdError: string[]

    /// The resolution paths - the full paths to selected resolved dll's.
    /// In scripts this is equivalent to #r @"c:\somepath\to\packages\ResolvedPackage\1.1.1\lib\netstandard2.0\ResolvedAssembly.dll"
    member Resolutions: seq<string>

    /// The source code file paths
    member SourceFiles: seq<string>

    /// The roots to package directories
    ///     This points to the root of each located package.
    ///     The layout of the package manager will be package manager specific.
    ///     however, the dependency manager dll understands the nuget package layout
    ///     and so if the package contains folders similar to the nuget layout then
    ///     the dependency manager will be able to probe and resolve any native dependencies
    ///     required by the nuget package.
    ///
    /// This path is also equivant to
    ///     #I @"c:\somepath\to\packages\ResolvedPackage\1.1.1\"
    member Roots: seq<string>

[<DependencyManagerAttribute>]
type FSharpDependencyManager =
    new: outputDirectory:string option -> FSharpDependencyManager

    member Name: string

    member Key:string

    member HelpMessages:string[]

    member ResolveDependencies: scriptDirectory: string * scriptName: string * scriptExt: string * packageManagerTextLines: (string * string) seq * targetFrameworkMoniker: string * runtimeIdentifier: string * timeout: int-> obj
