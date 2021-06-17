// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Internal.Utilities

open Microsoft.FSharp.Core
open System.IO

module internal FSharpEnvironment =

    val FSharpBannerVersion: string

    val FSharpProductName: string

    val FSharpCoreLibRunningVersion: string option

    val FSharpBinaryMetadataFormatRevision: string

    val isRunningOnCoreClr: bool
  
    val tryCurrentDomain: unit -> string option

    // The default location of FSharp.Core.dll and fsc.exe based on the version of fsc.exe that is running
    // Used for
    //     - location of design-time copies of FSharp.Core.dll and FSharp.Compiler.Interactive.Settings.dll for the default assumed environment for scripts
    //     - default ToolPath in tasks in FSharp.Build.dll (for Fsc tasks, but note a probe location is given)
    //     - default F# binaries directory in service.fs (REVIEW: check this)
    //     - default location of fsi.exe in FSharp.VS.FSI.dll (REVIEW: check this)
    //     - default F# binaries directory in (project system) Project.fs
    val BinFolderOfDefaultFSharpCompiler: probePoint:string option -> string option

    val toolingCompatiblePaths: unit -> string list

    val searchToolPaths: path:string option -> compilerToolPaths:seq<string> -> seq<string>

    val getTypeProviderAssembly:
      runTimeAssemblyFileName:string *
      designTimeAssemblyName:string *
      compilerToolPaths:string list *
      raiseError:(string option -> exn -> System.Reflection.Assembly option)
           -> System.Reflection.Assembly option

    val getFSharpCompilerLocation: unit -> string

    val getDefaultFSharpCoreLocation: unit -> string

    val getDefaultFsiLibraryLocation: unit -> string

    val getCompilerToolsDesignTimeAssemblyPaths: compilerToolPaths:seq<string> -> seq<string>

    val fsiLibraryName: string

    val getFSharpCoreLibraryName: string

    val isWindows: bool

    val dotnet: string

    val getDotnetHostPath: unit -> string option

    val getDotnetHostDirectories: unit -> string []

    val getDotnetHostDirectory: unit -> string option

    val getDotnetHostSubDirectories: string -> DirectoryInfo []
