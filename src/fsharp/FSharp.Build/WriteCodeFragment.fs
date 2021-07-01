// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Build

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Linq
open System.Text
open Microsoft.Build.Framework
open Microsoft.Build.Utilities

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
[<AutoOpen>]
// Shim to match nullness checking library support in preview
module UtilsWriteCodeFragment = 
    let inline (|Null|NonNull|) (x: 'T) : Choice<unit,'T> = match x with null -> Null | v -> NonNull v
#endif

type WriteCodeFragment() =
#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    let mutable _buildEngine : IBuildEngine = null
    let mutable _hostObject : ITaskHost = null
    let mutable _outputDirectory : ITaskItem = null
    let mutable _outputFile : ITaskItem = null
#else
    let mutable _buildEngine : IBuildEngine? = null
    let mutable _hostObject : ITaskHost? = null
    let mutable _outputDirectory : ITaskItem? = null
    let mutable _outputFile : ITaskItem? = null
#endif
    let mutable _language : string = ""
    let mutable _assemblyAttributes : ITaskItem[] = [||]

    static let escapeString (str:string) =
        let sb = str.ToCharArray() |> Seq.fold (fun (sb:StringBuilder) (c:char) ->
            match c with
            | '\n' | '\u2028' | '\u2028' -> sb.Append("\\n")
            | '\r' -> sb.Append("\\r")
            | '\t' -> sb.Append("\\t")
            | '\'' -> sb.Append("\\'")
            | '\\' -> sb.Append("\\\\")
            | '"' -> sb.Append("\\\"")
            | '\u0000' -> sb.Append("\\0")
            | _ -> sb.Append(c)) (StringBuilder().Append("\""))
        sb.Append("\"").ToString()

    static member GenerateAttribute (item:ITaskItem, language:string) =
        let attributeName = item.ItemSpec
        let args =
            // mimicking the behavior from https://github.com/Microsoft/msbuild/blob/70ce7e9ccb891b63f0859f1f7f0b955693ed3742/src/Tasks/WriteCodeFragment.cs#L355-L415
            // Split parameters into unnamed positional (e.g., key is "_Parameter1", etc.) and proper named parameters.
            let customMetadata = item.CloneCustomMetadata()
            let parameterPairs =
                // normalize everything to strings
                seq { for entry in customMetadata -> entry :?> DictionaryEntry }
                |> Seq.toList
                |> List.map (fun entry ->
                    let key = entry.Key :?> string
                    let value = match entry.Value with
                                | null -> "null"
                                | :? string as value -> escapeString value
                                | value -> value.ToString()
                    (key, value))
            let orderedParameters, namedParameters = parameterPairs |> List.partition (fun (key, _) -> key.StartsWith("_Parameter"))
            let orderedParametersWithIndex =
                orderedParameters
                |> List.map (fun (key, value) ->
                    let indexString = key.Substring("_Parameter".Length)
                    match Int32.TryParse indexString with
                    | (true, index) -> (index, value)
                    | (false, _) -> failwith (sprintf "Unable to parse '%s' as an index" indexString))
                |> List.sortBy fst
            // assign ordered parameters to array
            let orderedParametersArray =
                if List.isEmpty orderedParametersWithIndex then [||]
                else Array.create (List.last orderedParametersWithIndex |> fst) "null"
            List.iter (fun (index, value) -> orderedParametersArray.[index - 1] <- value) orderedParametersWithIndex
            // construct ordered parameter lists
            let combinedOrderedParameters = String.Join(", ", orderedParametersArray)
            let combinedNamedParameters = String.Join(", ", List.map (fun (key, value) -> sprintf "%s = %s" key value) namedParameters)
            // construct the final argument string; positional arguments followed by named
            match (combinedOrderedParameters.Length, combinedNamedParameters.Length) with
            | (0, 0) -> "" // no arguments
            | (0, _) -> combinedNamedParameters // only named arguments
            | (_, 0) -> combinedOrderedParameters // only positional arguments
            | (_, _) -> combinedOrderedParameters + ", " + combinedNamedParameters // both positional and named arguments
        match language.ToLowerInvariant() with
        | "f#" -> sprintf "[<assembly: %s(%s)>]" attributeName args
        | "c#" -> sprintf "[assembly: %s(%s)]" attributeName args
        | "vb" -> sprintf "<Assembly: %s(%s)>" attributeName args
        | _ -> failwith "Language name must be one of F#, C# or VB"

    // adding this property to maintain API equivalence with the MSBuild task
    member this.Language
        with get() = _language
         and set(value) = _language <- value

    member this.AssemblyAttributes
        with get() = _assemblyAttributes
         and set(value) = _assemblyAttributes <- value

    member this.OutputDirectory
        with get() = _outputDirectory
         and set(value) = _outputDirectory <- value

    [<Output>]
    member this.OutputFile
        with get() = _outputFile
         and set(value) = _outputFile <- value


    interface ITask with
        member this.BuildEngine
            with get() = _buildEngine
             and set(value) = _buildEngine <- value

        member this.HostObject
            with get() = _hostObject
             and set(value) = _hostObject <- value

        member this.Execute() =
            try
                match _outputFile with 
                | Null -> failwith "Output location must be specified"
                | NonNull outputFile -> 

                let boilerplate =
                    match _language.ToLowerInvariant() with
                    | "f#" -> "// <auto-generated>\n//     Generated by the FSharp WriteCodeFragment class.\n// </auto-generated>\nnamespace FSharp\n\nopen System\nopen System.Reflection\n"
                    | "c#" -> "// <auto-generated>\n//     Generated by the FSharp WriteCodeFragment class.\n// </auto-generated>\n\nusing System;\nusing System.Reflection;"
                    | "vb" -> "'------------------------------------------------------------------------------\n' <auto-generated>\n'     Generated by the FSharp WriteCodeFragment class.\n' </auto-generated>\n'------------------------------------------------------------------------------\n\nOption Strict Off\nOption Explicit On\n\nImports System\nImports System.Reflection"
                    | _ -> failwith "Language name must be one of F#, C# or VB"

                let sb = StringBuilder().AppendLine(boilerplate).AppendLine()
                let code = Array.fold (fun (sb:StringBuilder) (item:ITaskItem) -> sb.AppendLine(WriteCodeFragment.GenerateAttribute (item, _language.ToLowerInvariant()))) sb _assemblyAttributes

                if _language.ToLowerInvariant() = "f#" then code.AppendLine("do()") |> ignore

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
                let fileName = _outputFile.ItemSpec

                let outputFileItem =
                    if not (isNull _outputFile) && not (isNull _outputDirectory) && not (Path.IsPathRooted(fileName)) then
                        TaskItem(Path.Combine(_outputDirectory.ItemSpec, fileName)) :> ITaskItem
                    else
                        _outputFile
#else
                let fileName = outputFile.ItemSpec

                let outputFileItem =
                    match _outputDirectory with 
                    | Null -> outputFile
                    | NonNull outputDirectory -> 
                        if Path.IsPathRooted(fileName) then 
                            outputFile
                        else
                            TaskItem(Path.Combine(outputDirectory.ItemSpec, fileName)) :> ITaskItem
#endif

                let codeText = code.ToString()
                File.WriteAllText(fileName, codeText)
                _outputFile <- outputFileItem
                true

            with e ->
                printf "Error writing code fragment: %s" (e.ToString())
                false
