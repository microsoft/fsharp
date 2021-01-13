// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.DotNet.DependencyManager

open System
open System.Collections.Concurrent
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open Internal.Utilities
open Internal.Utilities.FSharpEnvironment

/// Signature for Native library resolution probe callback
/// host implements this, it's job is to return a list of package roots to probe.
type NativeResolutionProbe = delegate of Unit -> seq<string>

type IRegisterResolvers =
    inherit IDisposable
    abstract RegisterAssemblyNativeResolvers: Assembly -> unit
    abstract RegisterPackageRoots: string seq -> unit

/// Type that encapsulates Native library probing for managed packages
type NativeDllResolveHandlerCoreClr (nativeProbingRoots: NativeResolutionProbe) =

    static let pathLock = new obj()

    let nativeLibraryType: Type = Type.GetType("System.Runtime.InteropServices.NativeLibrary, System.Runtime.InteropServices", false)
    let nativeDllImportResolverType: Type = Type.GetType("System.Runtime.InteropServices.DllImportResolver, System.Runtime.InteropServices", false)

    let nativeLibraryTryLoad =
        nativeLibraryType.GetMethod("TryLoad", [| typeof<string>; typeof<IntPtr>.MakeByRefType() |])

    let _nativeLibrarySetDllImportResolver =
        nativeLibraryType.GetMethod("SetDllImportResolver", [| typeof<Assembly>; nativeDllImportResolverType |])

    let loadNativeLibrary path =
        let arguments = [| path:>obj; IntPtr.Zero:>obj |]
        if nativeLibraryTryLoad.Invoke(null, arguments) :?> bool then
            arguments.[1] :?> IntPtr
        else
            IntPtr.Zero

    let probingFileNames (name: string) =
        // coreclr native library probing algorithm: https://github.com/dotnet/coreclr/blob/9773db1e7b1acb3ec75c9cc0e36bd62dcbacd6d5/src/System.Private.CoreLib/shared/System/Runtime/Loader/LibraryNameVariation.Unix.cs
        let isRooted = Path.IsPathRooted name
        let useSuffix s = not (name.Contains(s + ".") || name.EndsWith(s))          // linux devs often append version # to libraries I.e mydll.so.5.3.2
        let usePrefix = name.IndexOf(Path.DirectorySeparatorChar) = -1              // If name has directory information no add no prefix
                        && name.IndexOf(Path.AltDirectorySeparatorChar) = -1
                        && name.IndexOf(Path.PathSeparator) = -1
                        && name.IndexOf(Path.VolumeSeparatorChar) = -1
        let prefix = [| "lib" |]
        let suffix = [|
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    ".dll"
                    ".exe"
                elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    ".dylib"
                else
                    ".so"
            |]

        [|
            yield name                                                                              // Bare name
            if not (isRooted) then
                for s in suffix do
                    if useSuffix s then                                                             // Suffix without prefix
                        yield (sprintf "%s%s" name s)
                        if usePrefix then
                            for p in prefix do                                                      // Suffix with prefix
                                yield (sprintf "%s%s%s" p name s)
                    elif usePrefix then
                        for p in prefix do                                                          // Prefix
                            yield (sprintf "%s%s" p name)
        |]

    let resolveUnmanagedDll (_: Assembly) (name: string): IntPtr =
        // Enumerate probing roots looking for a dll that matches the probing name in the probed locations
        let probeForNativeLibrary root rid name =
            // Look for name in root
            probingFileNames name |> Array.tryPick(fun name ->
                let path = Path.Combine(root, "runtimes", rid, "native", name)
                if File.Exists(path) then
                    Some path
                else
                    None)

        let probe =
            match nativeProbingRoots with
            | null -> None
            | _ ->
                nativeProbingRoots.Invoke()
                |> Seq.tryPick(fun root ->
                    probingFileNames name |> Seq.tryPick(fun name ->
                        let path = Path.Combine(root, name)
                        if File.Exists(path) then
                            Some path
                        else
                            RidHelpers.probingRids |> Seq.tryPick(fun rid -> probeForNativeLibrary root rid name)))

        match probe with
        | Some path ->
            loadNativeLibrary path
        | None -> IntPtr.Zero

    // netstandard 2.1 has this property, unfortunately we don't build with that yet
    //public event Func<Assembly, string, IntPtr> ResolvingUnmanagedDll
    let assemblyLoadContextType: Type = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", false)
    let eventInfo, handler, defaultAssemblyLoadContext =
        assemblyLoadContextType.GetEvent("ResolvingUnmanagedDll"), 
        Func<Assembly, string, IntPtr> (resolveUnmanagedDll), 
        assemblyLoadContextType.GetProperty("Default", BindingFlags.Static ||| BindingFlags.Public).GetValue(null, null)

    do eventInfo.AddEventHandler(defaultAssemblyLoadContext, handler)

    let ensureTrailingPathSeparator (p: string) =
        if not(p.EndsWith(Path.PathSeparator.ToString(), StringComparison.OrdinalIgnoreCase)) then
            p + Path.PathSeparator.ToString()
        else
            p

    let useOSSpecificDirectorySeparator (p: string) =
        p.Replace('/', Path.DirectorySeparatorChar)

    let addedPaths = ConcurrentBag<string>()

    let addProbeToProcessPath probePath =
        let probe = useOSSpecificDirectorySeparator (ensureTrailingPathSeparator probePath)
        lock pathLock (fun () ->
            let path = ensureTrailingPathSeparator (Environment.GetEnvironmentVariable("PATH"))
            if not (path.Contains(probe)) then
                Environment.SetEnvironmentVariable("PATH", probe + path)
                addedPaths.Add probe)

    let removeProbeFromProcessPath probePath =
        lock pathLock (fun () ->
            if not(String.IsNullOrWhiteSpace(probePath)) then
                let probe = useOSSpecificDirectorySeparator (ensureTrailingPathSeparator probePath)
                let path = ensureTrailingPathSeparator (Environment.GetEnvironmentVariable("PATH"))
                if path.Contains(probe) then Environment.SetEnvironmentVariable("PATH", path.Replace(probe, "")))

    static member private ImportResolver<'T>(libraryName:string, assembly:Assembly, searchPath: 'T) : IntPtr =
     //(*DllImportSearchPath? searchPath*)
     (*
            IntPtr libHandle = IntPtr.Zero;
            if (libraryName == MyLibrary)
            {
                // Try using the system library 'libmylibrary.so.5'
                NativeLibrary.TryLoad("libmylibrary.so.5", assembly, DllImportSearchPath.System32, out libHandle);
            }
            return libHandle;
     *)
        ignore libraryName
        ignore assembly
        ignore searchPath
        IntPtr.Zero

    interface IRegisterResolvers with
        member _.RegisterAssemblyNativeResolvers(assembly: Assembly) =
            ignore assembly

        member _.RegisterPackageRoots(roots: string seq) =
            for probePath in roots do
                addProbeToProcessPath probePath

    interface IDisposable with
        member _x.Dispose() =
            eventInfo.RemoveEventHandler(defaultAssemblyLoadContext, handler)
            let mutable probe:string = null
            while (addedPaths.TryTake(&probe)) do
                removeProbeFromProcessPath probe

type NativeDllResolveHandler (nativeProbingRoots: NativeResolutionProbe) =
    let handler: IRegisterResolvers option =
        if isRunningOnCoreClr then
            Some (new NativeDllResolveHandlerCoreClr(nativeProbingRoots) :> IRegisterResolvers)
        else
            None

    interface IRegisterResolvers with
        member _.RegisterAssemblyNativeResolvers(assembly: Assembly) =
            match handler with
            | None -> ()
            | Some handler -> handler.RegisterAssemblyNativeResolvers(assembly)

        member _.RegisterPackageRoots(roots: string seq) =
            match handler with
            | None -> ()
            | Some handler -> handler.RegisterPackageRoots(roots)

    interface IDisposable with
        member _.Dispose() =
            match handler with
            | None -> ()
            | Some handler -> handler.Dispose()
