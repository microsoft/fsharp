// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Build

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Reflection
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Internal.Utilities

//There are a lot of flags on fsi.exe.
//For now, not all of them are represented in the "Fsi class" object model.
//The goal is to have the most common/important flags available via the Fsi class, and the
//rest can be "backdoored" through the .OtherFlags property.

[<Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")>]
type public Fsi () as this = 

    inherit ToolTask ()

    let mutable capturedArguments : string list = []  // list of individual args, to pass to HostObject Compile()
    let mutable capturedFilenames : string list = []  // list of individual source filenames, to pass to HostObject Compile()
    let mutable codePage : string = null
    let mutable commandLineArgs : ITaskItem list = []
    let mutable defineConstants : ITaskItem[] = [||]
    let mutable disabledWarnings : string = null
    let mutable dotnetFsiCompilerPath : string = null
    let mutable fsiExec = false
    let mutable langVersion : string = null
    let mutable noFramework = false
    let mutable optimize = true
    let mutable otherFlags : string = null
    let mutable preferredUILang = null
    let mutable provideCommandLineArgs = false
    let mutable references : ITaskItem[] = [||]
    let mutable referencePath : string = null
    let mutable resources : ITaskItem[] = [||]
    let mutable skipCompilerExecution = false
    let mutable sources : ITaskItem[] = [||]
    let mutable loadSources : ITaskItem[] = [||]
    let mutable useSources : ITaskItem[] = [||]
    let mutable tailcalls : bool = true
    let mutable targetProfile : string = null
    let mutable targetType : string = null
    let mutable toolExe : string = "fsi.exe"
    let mutable toolPath : string =
        let locationOfThisDll =
            try Some(Path.GetDirectoryName(typeof<Fsi>.Assembly.Location))
            with _ -> None
        match FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(locationOfThisDll) with
        | Some s -> s
        | None -> ""
    let mutable treatWarningsAsErrors : bool = false
    let mutable warningsAsErrors : string = null
    let mutable warningsNotAsErrors : string = null
    let mutable warningLevel : string = null
    let mutable vslcid : string = null
    let mutable utf8output : bool = false

    // See bug 6483; this makes parallel build faster, and is fine to set unconditionally
    do this.YieldDuringToolExecution <- true

    let generateCommandLineBuilder () =
        let builder = new FSharpCommandLineBuilder()

        builder.AppendSwitchIfNotNull("--codepage:", codePage)

        builder.AppendSwitchIfNotNull("--langversion:", langVersion)
        if noFramework then builder.AppendSwitch("--noframework")

        if defineConstants <> null then
            for item in defineConstants do
                builder.AppendSwitchIfNotNull("--define:", item.ItemSpec)

        if optimize then builder.AppendSwitch("--optimize+")
        else builder.AppendSwitch("--optimize-")

        if not tailcalls then
            builder.AppendSwitch("--tailcalls-")

        if references <> null then 
            for item in references do
                builder.AppendSwitchIfNotNull("-r:", item.ItemSpec)

        let referencePathArray = // create a array of strings
            match referencePath with
            | null -> null
            | _ -> referencePath.Split([|';'; ','|], StringSplitOptions.RemoveEmptyEntries)

        // NoWarn
        match disabledWarnings with
        | null -> ()
        | _ -> builder.AppendSwitchIfNotNull("--nowarn:", disabledWarnings.Split([|' '; ';'; ','; '\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries), ",")

        builder.AppendSwitchIfNotNull("--warn:", warningLevel)

        if treatWarningsAsErrors then builder.AppendSwitch("--warnaserror")

        // Change warning 76, HashReferenceNotAllowedInNonScript/HashDirectiveNotAllowedInNonScript/HashIncludeNotAllowedInNonScript, into an error
        let warningsAsErrorsArray =
            match warningsAsErrors with
            | null -> [| "76" |]
            | _ -> (warningsAsErrors + " 76 ").Split([|' '; ';'; ','|], StringSplitOptions.RemoveEmptyEntries)

        builder.AppendSwitchIfNotNull("--warnaserror:", warningsAsErrorsArray, ",")

        match warningsNotAsErrors with
        | null -> ()
        | _ -> builder.AppendSwitchIfNotNull("--warnaserror-:", warningsNotAsErrors.Split([|' '; ';'; ','|], StringSplitOptions.RemoveEmptyEntries), ",")

        builder.AppendSwitchIfNotNull("--LCID:", vslcid)

        builder.AppendSwitchIfNotNull("--preferreduilang:", preferredUILang)

        if utf8output then builder.AppendSwitch("--utf8output")

        builder.AppendSwitch("--fullpaths")
        builder.AppendSwitch("--flaterrors")

        builder.AppendSwitchIfNotNull("--targetprofile:", targetProfile)

        if loadSources <> null then
            for item in loadSources do
                builder.AppendSwitchIfNotNull("--load:", item.ItemSpec)

        if useSources <> null then 
            for item in useSources do
                builder.AppendSwitchIfNotNull("--use:", item.ItemSpec)

        // OtherFlags - must be second-to-last
        builder.AppendSwitchUnquotedIfNotNull("", otherFlags)
        capturedArguments <- builder.CapturedArguments()

        if fsiExec then builder.AppendSwitch("--exec")

        // Sources - these have to go last
        builder.AppendFileNamesIfNotNull(sources, " ")
        capturedFilenames <- builder.CapturedFilenames()

        builder

    // --codepage <int>: Specify the codepage to use when opening source files
    member fsc.CodePage
        with get() = codePage
        and set(s) = codePage <- s

    // --nowarn <string>: Do not report the given specific warning.
    member fsi.DisabledWarnings
        with get() = disabledWarnings
        and set(a) = disabledWarnings <- a

    // --define <string>: Define the given conditional compilation symbol.
    member fsi.DefineConstants
        with get() = defineConstants
        and set(a) = defineConstants <- a

    member fsi.DotnetFsiCompilerPath  
        with get() = dotnetFsiCompilerPath
        and set(p) = dotnetFsiCompilerPath <- p

    member fsi.FsiExec
        with get() = fsiExec
        and set(p) = fsiExec <- p

    member fsi.LCID
        with get() = vslcid
        and set(p) = vslcid <- p

    member fsc.LangVersion
        with get() = langVersion
        and set(s) = langVersion <- s

    // --noframework
    member fsi.NoFramework
        with get() = noFramework 
        and set(b) = noFramework <- b

    // --optimize
    member fsi.Optimize
        with get() = optimize
        and set(p) = optimize <- p

    // --tailcalls
    member fsi.Tailcalls
        with get() = tailcalls
        and set(p) = tailcalls <- p

    member fsi.OtherFlags
        with get() = otherFlags
        and set(s) = otherFlags <- s

    member fsi.PreferredUILang
        with get() = preferredUILang 
        and set(s) = preferredUILang <- s

    member fsi.ProvideCommandLineArgs
        with get() = provideCommandLineArgs
        and set(p) = provideCommandLineArgs <- p

    // -r <string>: Reference an F# or .NET assembly.
    member fsi.References
        with get() = references
        and set(a) = references <- a

    // --lib
    member fsi.ReferencePath
        with get() = referencePath
        and set(s) = referencePath <- s

    // -load:<string>: load an F# source file
    member fsi.LoadSources
        with get() = loadSources
        and set(a) = loadSources <- a

    member fsi.SkipCompilerExecution
        with get() = skipCompilerExecution
        and set(p) = skipCompilerExecution <- p

    // source files 
    member fsi.Sources
        with get() = sources
        and set(a) = sources <- a

    member fsi.TargetProfile
        with get() = targetProfile
        and set(p) = targetProfile <- p

    member fsi.TreatWarningsAsErrors
        with get() = treatWarningsAsErrors
        and set(p) = treatWarningsAsErrors <- p
        
    // For targeting other folders for "fsi.exe" (or ToolExe if different)
    member fsi.ToolPath
        with get() = toolPath
        and set(s) = toolPath <- s

    // --use:<string>: execute an F# source file on startup
    member fsi.UseSources
        with get() = useSources
        and set(a) = useSources <- a

    // For specifying the warning level (0-4)
    member fsi.WarningLevel
        with get() = warningLevel
        and set(s) = warningLevel <- s

    member fsi.WarningsAsErrors 
        with get() = warningsAsErrors
        and set(s) = warningsAsErrors <- s

    member fsi.WarningsNotAsErrors
        with get() = warningsNotAsErrors
        and set(s) = warningsNotAsErrors <- s

    member fsi.Utf8Output
        with get() = utf8output
        and set(p) = utf8output <- p

    [<Output>]
    member fsi.CommandLineArgs
        with get() = List.toArray commandLineArgs
        and set(p) = commandLineArgs <- (List.ofArray p)

    // ToolTask methods
    override fsi.ToolName = "fsi.exe" 
    override fsi.StandardErrorEncoding = if utf8output then System.Text.Encoding.UTF8 else base.StandardErrorEncoding
    override fsi.StandardOutputEncoding = if utf8output then System.Text.Encoding.UTF8 else base.StandardOutputEncoding
    override fsi.GenerateFullPathToTool() = 
        if toolPath = "" then raise (new System.InvalidOperationException(FSBuild.SR.toolpathUnknown()))
        System.IO.Path.Combine(toolPath, fsi.ToolExe)
    override fsi.LogToolCommand (message:string) =
        fsi.Log.LogMessageFromText(message, MessageImportance.Normal) |>ignore

    member internal fsi.InternalGenerateFullPathToTool() = fsi.GenerateFullPathToTool()             // expose for unit testing
    member internal fsi.BaseExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =    // F# does not allow protected members to be captured by lambdas, this is the standard workaround
        base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)

    /// Intercept the call to ExecuteTool to handle the host compile case.
    override fsi.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =
        if provideCommandLineArgs then
            commandLineArgs <-
                fsi.GetCapturedArguments()
                |> Array.map (fun (arg: string) -> TaskItem(arg) :> ITaskItem)
                |> Array.toList

        if skipCompilerExecution then
            0
        else
            let host = box fsi.HostObject
            match host with
            | null -> base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)
            | _ ->
                let sources = sources|>Array.map(fun i->i.ItemSpec)
                let invokeCompiler baseCallDelegate =
                    try
                        let ret =
                            (host.GetType()).InvokeMember("Compile", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.InvokeMethod ||| BindingFlags.Instance, null, host,
                                                        [| baseCallDelegate; box (capturedArguments |> List.toArray); box (capturedFilenames |> List.toArray) |],
                                                        CultureInfo.InvariantCulture)
                        unbox ret
                    with
                    // ok, this is what happens when VS IDE cancels the build, no need to assert, just log the build-canceled error and return -1 to denote task failed
                    // Do a string compare on the type name to do eliminate a compile time dependency on Microsoft.Build.dll
                    | :? TargetInvocationException as tie when not (isNull tie.InnerException) && (tie.InnerException).GetType().FullName = "Microsoft.Build.Exceptions.BuildAbortedException" ->
                        fsi.Log.LogError(tie.InnerException.Message, [| |])
                        -1
                    | e -> reraise()

                let baseCallDelegate = Func<int>(fun () -> fsi.BaseExecuteTool(pathToTool, responseFileCommands, commandLineCommands) )
                try
                    invokeCompiler baseCallDelegate
                with
                | e ->
                        Debug.Assert(false, "HostObject received by Fsi task did not have a Compile method or the compile method threw an exception. "+(e.ToString()))
                        reraise()

    override fsi.GenerateCommandLineCommands() =
        let builder = new FSharpCommandLineBuilder()
        if not (String.IsNullOrEmpty(dotnetFsiCompilerPath)) then builder.AppendSwitch(dotnetFsiCompilerPath)
        builder.ToString()

    override fsi.GenerateResponseFileCommands() =
        let builder = generateCommandLineBuilder ()
        builder.GetCapturedArguments() |> String.concat Environment.NewLine

    // expose this to internal components (for nunit testing)
    member internal fsi.InternalGenerateCommandLineCommands() =
        fsi.GenerateCommandLineCommands()

    // expose this to internal components (for nunit testing)
    member internal fsi.InternalGenerateResponseFileCommands() =
        fsi.GenerateResponseFileCommands()

    member internal fsi.InternalExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =
        fsi.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)

    member internal fsi.GetCapturedArguments() = [|
            yield! capturedArguments
            yield! capturedFilenames
        |]
