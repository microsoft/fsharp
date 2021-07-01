// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Build

open System
open System.Text
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Internal.Utilities

[<assembly: System.Runtime.InteropServices.ComVisible(false)>]
[<assembly: System.CLSCompliant(true)>]
do()

type FSharpCommandLineBuilder () =

    // In addition to generating a command-line that will be handed to cmd.exe, we also generate
    // an array of individual arguments.  The former needs to be quoted (and cmd.exe will strip the
    // quotes while parsing), whereas the latter is not.  See bug 4357 for background; this helper
    // class gets us out of the business of unparsing-then-reparsing arguments.

    let builder = new CommandLineBuilder()
    let mutable args = []  // in reverse order
    let mutable srcs = []  // in reverse order

    /// Return a list of the arguments (with no quoting for the cmd.exe shell)
    member x.CapturedArguments() = List.rev args

    /// Return a list of the sources (with no quoting for the cmd.exe shell)
    member x.CapturedFilenames() = List.rev srcs

    /// Return a full command line (with quoting for the cmd.exe shell)
    override x.ToString() = builder.ToString()

    member x.AppendFileNamesIfNotNull(filenames:ITaskItem[], sep:string) =
        builder.AppendFileNamesIfNotNull(filenames, sep)
        // do not update "args", not used
        for item in filenames do
            let tmp = new CommandLineBuilder()
            tmp.AppendSwitchUnquotedIfNotNull("", item.ItemSpec)  // we don't want to quote the filename, this is a way to get that
            let s = tmp.ToString()
            if s <> String.Empty then
                srcs <- tmp.ToString() :: srcs

    member x.AppendSwitchesIfNotNull(switch:string, values:string[], sep:string) =
        builder.AppendSwitchIfNotNull(switch, values, sep)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, values, sep)
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- s :: args

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    member x.AppendSwitchIfNotNull(switch:string, value:string, ?metadataNames:string[]) =
#else
    member x.AppendSwitchIfNotNull(switch:string, value:string?, ?metadataNames:string[]) =
#endif
        let metadataNames = defaultArg metadataNames [||]
        builder.AppendSwitchIfNotNull(switch, value)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, value)
        let providedMetaData =
            metadataNames
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
        if providedMetaData.Length > 0 then
            tmp.AppendTextUnquoted ","
            tmp.AppendTextUnquoted (providedMetaData|> String.concat ",")
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- s :: args

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    member x.AppendSwitchUnquotedIfNotNull(switch:string, value:string) =
#else
    member x.AppendSwitchUnquotedIfNotNull(switch:string, value:string?) =
#endif
        assert(switch = "")  // we only call this method for "OtherFlags"
        // Unfortunately we still need to mimic what cmd.exe does, but only for "OtherFlags".
        let ParseCommandLineArgs(commandLine:string) = // returns list in reverse order
            let mutable args = []
            let mutable i = 0 // index into commandLine
            let len = commandLine.Length
            while i < len do
                // skip whitespace
                while i < len && System.Char.IsWhiteSpace(commandLine, i) do
                    i <- i + 1
                if i < len then
                    // parse an argument
                    let sb = new StringBuilder()
                    let mutable finished = false
                    let mutable insideQuote = false
                    while i < len && not finished do
                        match commandLine.[i] with
                        | '"' -> insideQuote <- not insideQuote; i <- i + 1
                        | c when not insideQuote && System.Char.IsWhiteSpace(c) -> finished <- true
                        | c -> sb.Append(c) |> ignore; i <- i + 1
                    args <- sb.ToString() :: args
            args
        builder.AppendSwitchUnquotedIfNotNull(switch, value)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, value)
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- ParseCommandLineArgs(s) @ args

    member x.AppendSwitch(switch:string) =
        builder.AppendSwitch(switch)
        args <- switch :: args

    member internal x.GetCapturedArguments() = 
        [|
            yield! x.CapturedArguments()
            yield! x.CapturedFilenames()
        |]
