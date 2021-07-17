// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// # FSComp.SR.opts

module internal FSharp.Compiler.CompilerOptions

open System
open System.IO
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILPdbWriter
open FSharp.Compiler.AbstractIL.ILX
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Features
open FSharp.Compiler.Syntax
open FSharp.Compiler.IO
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.ErrorLogger

open Internal.Utilities

module Attributes =
    open System.Runtime.CompilerServices

    //[<assembly: System.Security.SecurityTransparent>]
    [<Dependency("FSharp.Core", LoadHint.Always)>]
    do()

//----------------------------------------------------------------------------
// Compiler option parser
//
// The argument parser is used by both the VS plug-in and the fsc.exe to
// parse the include file path and other front-end arguments.
//
// The language service uses this function too. It's important to continue
// processing flags even if an error is seen in one so that the best possible
// intellisense can be show.
//--------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type OptionSwitch =
    | On
    | Off

type OptionSpec =
    | OptionClear of bool ref
    | OptionFloat of (float -> unit)
    | OptionInt of (int -> unit)
    | OptionSwitch of (OptionSwitch -> unit)
    | OptionIntList of (int -> unit)
    | OptionIntListSwitch of (int -> OptionSwitch -> unit)
    | OptionRest of (string -> unit)
    | OptionSet of bool ref
    | OptionString of (string -> unit)
    | OptionStringList of (string -> unit)
    | OptionStringListSwitch of (string -> OptionSwitch -> unit)
    | OptionUnit of (unit -> unit)
    | OptionHelp of (CompilerOptionBlock list -> unit)                      // like OptionUnit, but given the "options"
    | OptionGeneral of (string list -> bool) * (string list -> string list) // Applies? * (ApplyReturningResidualArgs)

and  CompilerOption      = CompilerOption of name: string * argumentDescriptionString: string * actionSpec: OptionSpec * deprecationError: Option<exn> * helpText: string option
and  CompilerOptionBlock = PublicOptions  of heading: string * options: CompilerOption list | PrivateOptions of options: CompilerOption list

let GetOptionsOfBlock block =
    match block with
    | PublicOptions (_, opts) -> opts
    | PrivateOptions opts -> opts

let FilterCompilerOptionBlock pred block =
    match block with
    | PublicOptions(heading, opts) -> PublicOptions(heading, List.filter pred opts)
    | PrivateOptions opts        -> PrivateOptions(List.filter pred opts)

let compilerOptionUsage (CompilerOption(s, tag, spec, _, _)) =
    let s = if s="--" then "" else s (* s="flag" for "--flag" options. s="--" for "--" option. Adjust printing here for "--" case. *)
    match spec with
    | (OptionUnit _ | OptionSet _ | OptionClear _ | OptionHelp _) -> sprintf "--%s" s
    | OptionStringList _ -> sprintf "--%s:%s" s tag
    | OptionIntList _ -> sprintf "--%s:%s" s tag
    | OptionSwitch _ -> sprintf "--%s[+|-]" s
    | OptionStringListSwitch _ -> sprintf "--%s[+|-]:%s" s tag
    | OptionIntListSwitch _ -> sprintf "--%s[+|-]:%s" s tag
    | OptionString _ -> sprintf "--%s:%s" s tag
    | OptionInt _ -> sprintf "--%s:%s" s tag
    | OptionFloat _ ->  sprintf "--%s:%s" s tag
    | OptionRest _ -> sprintf "--%s ..." s
    | OptionGeneral _  -> if tag="" then sprintf "%s" s else sprintf "%s:%s" s tag (* still being decided *)

let PrintCompilerOption (CompilerOption(_s, _tag, _spec, _, help) as compilerOption) =
    let flagWidth = 42 // fixed width for printing of flags, e.g. --debug:{full|pdbonly|portable|embedded}
    let defaultLineWidth = 80 // the fallback width
    let lineWidth =
        try
            System.Console.BufferWidth
        with e -> defaultLineWidth
    let lineWidth = if lineWidth=0 then defaultLineWidth else lineWidth (* Have seen BufferWidth=0 on Linux/Mono *)
    // Lines have this form: <flagWidth><space><description>
    //   flagWidth chars - for flags description or padding on continuation lines.
    //   single space    - space.
    //   description     - words upto but excluding the final character of the line.
    printf "%-40s" (compilerOptionUsage compilerOption)
    let printWord column (word:string) =
        // Have printed upto column.
        // Now print the next word including any preceding whitespace.
        // Returns the column printed to (suited to folding).
        if column + 1 (*space*) + word.Length >= lineWidth then // NOTE: "equality" ensures final character of the line is never printed
          printfn "" (* newline *)
          printf  "%-40s %s" ""(*<--flags*) word
          flagWidth + 1 + word.Length
        else
          printf  " %s" word
          column + 1 + word.Length
    let words = match help with None -> [| |] | Some s -> s.Split [| ' ' |]
    let _finalColumn = Array.fold printWord flagWidth words
    printfn "" (* newline *)

let PrintPublicOptions (heading, opts) =
  if not (isNil opts) then
    printfn ""
    printfn ""
    printfn "\t\t%s" heading
    List.iter PrintCompilerOption opts

let PrintCompilerOptionBlocks blocks =
  let equals x y = x=y
  let publicBlocks = List.choose (function PrivateOptions _ -> None | PublicOptions (heading, opts) -> Some (heading, opts)) blocks
  let consider doneHeadings (heading, _opts) =
    if Set.contains heading doneHeadings then
      doneHeadings
    else
      let headingOptions = List.filter (fst >> equals heading) publicBlocks |> List.collect snd
      PrintPublicOptions (heading, headingOptions)
      Set.add heading doneHeadings
  List.fold consider Set.empty publicBlocks |> ignore<Set<string>>

(* For QA *)
let dumpCompilerOption prefix (CompilerOption(str, _, spec, _, _)) =
    printf "section='%-25s' ! option=%-30s kind=" prefix str
    match spec with
      | OptionUnit             _ -> printf "OptionUnit"
      | OptionSet              _ -> printf "OptionSet"
      | OptionClear            _ -> printf "OptionClear"
      | OptionHelp             _ -> printf "OptionHelp"
      | OptionStringList       _ -> printf "OptionStringList"
      | OptionIntList          _ -> printf "OptionIntList"
      | OptionSwitch           _ -> printf "OptionSwitch"
      | OptionStringListSwitch _ -> printf "OptionStringListSwitch"
      | OptionIntListSwitch    _ -> printf "OptionIntListSwitch"
      | OptionString           _ -> printf "OptionString"
      | OptionInt              _ -> printf "OptionInt"
      | OptionFloat            _ -> printf "OptionFloat"
      | OptionRest             _ -> printf "OptionRest"
      | OptionGeneral          _ -> printf "OptionGeneral"
    printf "\n"
let dumpCompilerOptionBlock = function
  | PublicOptions (heading, opts) -> List.iter (dumpCompilerOption heading)     opts
  | PrivateOptions opts          -> List.iter (dumpCompilerOption "NoSection") opts
let DumpCompilerOptionBlocks blocks = List.iter dumpCompilerOptionBlock blocks

let isSlashOpt (opt:string) =
    opt.[0] = '/' && (opt.Length = 1 || not (opt.[1..].Contains "/"))

module ResponseFile =

    type ResponseFileData = ResponseFileLine list
    and ResponseFileLine =
        | CompilerOptionSpec of string
        | Comment of string

    let parseFile path: Choice<ResponseFileData, Exception> =
        let parseLine (l: string) =
            match l with
            | s when String.IsNullOrWhiteSpace s -> None
            | s when l.StartsWithOrdinal("#") -> Some (ResponseFileLine.Comment (s.TrimStart('#')))
            | s -> Some (ResponseFileLine.CompilerOptionSpec (s.Trim()))

        try
            use stream = FileSystem.OpenFileForReadShim(path)
            use reader = new StreamReader(stream, true)
            let data =
                seq { while not reader.EndOfStream do yield reader.ReadLine () }
                |> Seq.choose parseLine
                |> List.ofSeq
            Choice1Of2 data
        with e ->
            Choice2Of2 e

let ParseCompilerOptions (collectOtherArgument: string -> unit, blocks: CompilerOptionBlock list, args) =
  use unwindBuildPhase = PushThreadBuildPhaseUntilUnwind BuildPhase.Parameter

  let specs = List.collect GetOptionsOfBlock blocks

  // returns a tuple - the option token, the option argument string
  let parseOption (s: string) =
    // grab the option token
    let opts = s.Split([|':'|])
    let mutable opt = opts.[0]
    if opt = "" then
        ()
    // if it doesn't start with a '-' or '/', reject outright
    elif opt.[0] <> '-' && opt.[0] <> '/' then
      opt <- ""
    elif opt <> "--" then
      // is it an abbreviated or MSFT-style option?
      // if so, strip the first character and move on with your life
      if opt.Length = 2 || isSlashOpt opt then
        opt <- opt.[1 ..]
      // else, it should be a non-abbreviated option starting with "--"
      elif opt.Length > 3 && opt.StartsWithOrdinal("--") then
        opt <- opt.[2 ..]
      else
        opt <- ""

    // get the argument string
    let optArgs = if opts.Length > 1 then String.Join(":", opts.[1 ..]) else ""
    opt, optArgs

  let getOptionArg compilerOption (argString: string) =
    if argString = "" then
      errorR(Error(FSComp.SR.buildOptionRequiresParameter(compilerOptionUsage compilerOption), rangeCmdArgs))
    argString

  let getOptionArgList compilerOption (argString: string) =
    if argString = "" then
      errorR(Error(FSComp.SR.buildOptionRequiresParameter(compilerOptionUsage compilerOption), rangeCmdArgs))
      []
    else
      argString.Split([|',';';'|]) |> List.ofArray

  let getSwitchOpt (opt: string) =
    // if opt is a switch, strip the  '+' or '-'
    if opt <> "--" && opt.Length > 1 && (opt.EndsWithOrdinal("+") || opt.EndsWithOrdinal("-")) then
      opt.[0 .. opt.Length - 2]
    else
      opt

  let getSwitch (s: string) =
    let s = (s.Split([|':'|])).[0]
    if s <> "--" && s.EndsWithOrdinal("-") then OptionSwitch.Off else OptionSwitch.On

  let rec processArg args =
    match args with
    | [] -> ()
    | ((rsp: string) :: t) when rsp.StartsWithOrdinal("@") ->
        let responseFileOptions =
            let fullpath =
                try
                    Some (rsp.TrimStart('@') |> FileSystem.GetFullPathShim)
                with _ ->
                    None

            match fullpath with
            | None ->
                errorR(Error(FSComp.SR.optsResponseFileNameInvalid rsp, rangeCmdArgs))
                []
            | Some path when not (FileSystem.FileExistsShim path) ->
                errorR(Error(FSComp.SR.optsResponseFileNotFound(rsp, path), rangeCmdArgs))
                []
            | Some path ->
                match ResponseFile.parseFile path with
                | Choice2Of2 _ ->
                    errorR(Error(FSComp.SR.optsInvalidResponseFile(rsp, path), rangeCmdArgs))
                    []
                | Choice1Of2 rspData ->
                    let onlyOptions l =
                        match l with
                        | ResponseFile.ResponseFileLine.Comment _ -> None
                        | ResponseFile.ResponseFileLine.CompilerOptionSpec opt -> Some opt
                    rspData |> List.choose onlyOptions

        processArg (responseFileOptions @ t)
    | opt :: t ->
        let optToken, argString = parseOption opt

        let reportDeprecatedOption errOpt =
          match errOpt with
          | Some e -> warning e
          | None -> ()

        let rec attempt l =
          match l with
          | (CompilerOption(s, _, OptionHelp f, d, _) :: _) when optToken = s  && argString = "" ->
              reportDeprecatedOption d
              f blocks; t
          | (CompilerOption(s, _, OptionUnit f, d, _) :: _) when optToken = s  && argString = "" ->
              reportDeprecatedOption d
              f (); t
          | (CompilerOption(s, _, OptionSwitch f, d, _) :: _) when getSwitchOpt optToken = s && argString = "" ->
              reportDeprecatedOption d
              f (getSwitch opt); t
          | (CompilerOption(s, _, OptionSet f, d, _) :: _) when optToken = s && argString = "" ->
              reportDeprecatedOption d
              f := true; t
          | (CompilerOption(s, _, OptionClear f, d, _) :: _) when optToken = s && argString = "" ->
              reportDeprecatedOption d
              f := false; t
          | (CompilerOption(s, _, OptionString f, d, _) as compilerOption :: _) when optToken = s ->
              reportDeprecatedOption d
              let oa = getOptionArg compilerOption argString
              if oa <> "" then
                  f (getOptionArg compilerOption oa)
              t
          | (CompilerOption(s, _, OptionInt f, d, _) as compilerOption :: _) when optToken = s ->
              reportDeprecatedOption d
              let oa = getOptionArg compilerOption argString
              if oa <> "" then
                  f (try int32 oa with _ ->
                      errorR(Error(FSComp.SR.buildArgInvalidInt(getOptionArg compilerOption argString), rangeCmdArgs)); 0)
              t
          | (CompilerOption(s, _, OptionFloat f, d, _) as compilerOption :: _) when optToken = s ->
              reportDeprecatedOption d
              let oa = getOptionArg compilerOption argString
              if oa <> "" then
                  f (try float oa with _ ->
                      errorR(Error(FSComp.SR.buildArgInvalidFloat(getOptionArg compilerOption argString), rangeCmdArgs)); 0.0)
              t
          | (CompilerOption(s, _, OptionRest f, d, _) :: _) when optToken = s ->
              reportDeprecatedOption d
              List.iter f t; []
          | (CompilerOption(s, _, OptionIntList f, d, _) as compilerOption :: _) when optToken = s ->
              reportDeprecatedOption d
              let al = getOptionArgList compilerOption argString
              if al <> [] then
                  List.iter (fun i -> f (try int32 i with _ -> errorR(Error(FSComp.SR.buildArgInvalidInt i, rangeCmdArgs)); 0)) al
              t
          | (CompilerOption(s, _, OptionIntListSwitch f, d, _) as compilerOption :: _) when getSwitchOpt optToken = s ->
              reportDeprecatedOption d
              let al = getOptionArgList compilerOption argString
              if al <> [] then
                  let switch = getSwitch opt
                  List.iter (fun i -> f (try int32 i with _ -> errorR(Error(FSComp.SR.buildArgInvalidInt i, rangeCmdArgs)); 0) switch) al
              t
              // here
          | (CompilerOption(s, _, OptionStringList f, d, _) as compilerOption :: _) when optToken = s ->
              reportDeprecatedOption d
              let al = getOptionArgList compilerOption argString
              if al <> [] then
                  List.iter f (getOptionArgList compilerOption argString)
              t
          | (CompilerOption(s, _, OptionStringListSwitch f, d, _) as compilerOption :: _) when getSwitchOpt optToken = s ->
              reportDeprecatedOption d
              let al = getOptionArgList compilerOption argString
              if al <> [] then
                  let switch = getSwitch opt
                  List.iter (fun s -> f s switch) (getOptionArgList compilerOption argString)
              t
          | (CompilerOption(_, _, OptionGeneral (pred, exec), d, _) :: _) when pred args ->
              reportDeprecatedOption d
              let rest = exec args in rest // arguments taken, rest remaining
          | (_ :: more) -> attempt more
          | [] ->
              if opt.Length = 0 || opt.[0] = '-' || isSlashOpt opt
               then
                  // want the whole opt token - delimiter and all
                  let unrecOpt = (opt.Split([|':'|]).[0])
                  errorR(Error(FSComp.SR.buildUnrecognizedOption unrecOpt, rangeCmdArgs))
                  t
              else
                 (collectOtherArgument opt; t)
        let rest = attempt specs
        processArg rest

  processArg args

//----------------------------------------------------------------------------
// Compiler options
//--------------------------------------------------------------------------

let lexFilterVerbose = false
let mutable enableConsoleColoring = true // global state

let setFlag r n =
    match n with
    | 0 -> r false
    | 1 -> r true
    | _ -> raise (Failure "expected 0/1")

let SetOptimizeOff(tcConfigB: TcConfigBuilder) =
    tcConfigB.optSettings <- { tcConfigB.optSettings with jitOptUser = Some false }
    tcConfigB.optSettings <- { tcConfigB.optSettings with localOptUser = Some false }
    tcConfigB.optSettings <- { tcConfigB.optSettings with crossModuleOptUser = Some false }
    tcConfigB.optSettings <- { tcConfigB.optSettings with lambdaInlineThreshold = 0 }
    tcConfigB.doDetuple <- false
    tcConfigB.doTLR <- false
    tcConfigB.doFinalSimplify <- false

let SetOptimizeOn(tcConfigB: TcConfigBuilder) =
    tcConfigB.optSettings <- { tcConfigB.optSettings with jitOptUser = Some true }
    tcConfigB.optSettings <- { tcConfigB.optSettings with localOptUser = Some true }
    tcConfigB.optSettings <- { tcConfigB.optSettings with crossModuleOptUser = Some true }
    tcConfigB.optSettings <- { tcConfigB.optSettings with lambdaInlineThreshold = 6 }
    tcConfigB.doDetuple <- true
    tcConfigB.doTLR <- true
    tcConfigB.doFinalSimplify <- true

let SetOptimizeSwitch (tcConfigB: TcConfigBuilder) switch =
    if (switch = OptionSwitch.On) then SetOptimizeOn tcConfigB else SetOptimizeOff tcConfigB

let SetTailcallSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.emitTailcalls <- (switch = OptionSwitch.On)

let SetDeterministicSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.deterministic <- (switch = OptionSwitch.On)

let AddPathMapping (tcConfigB: TcConfigBuilder) (pathPair: string) =
    match pathPair.Split([|'='|], 2) with
    | [| oldPrefix; newPrefix |] ->
        tcConfigB.AddPathMapping (oldPrefix, newPrefix)
    | _ ->
        error(Error(FSComp.SR.optsInvalidPathMapFormat(), rangeCmdArgs))

let jitoptimizeSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.optSettings <- { tcConfigB.optSettings with jitOptUser = Some (switch = OptionSwitch.On) }

let localoptimizeSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.optSettings <- { tcConfigB.optSettings with localOptUser = Some (switch = OptionSwitch.On) }

let crossOptimizeSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.optSettings <- { tcConfigB.optSettings with crossModuleOptUser = Some (switch = OptionSwitch.On) }

let splittingSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.optSettings <- { tcConfigB.optSettings with abstractBigTargets = switch = OptionSwitch.On }

let callVirtSwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.alwaysCallVirt <- switch = OptionSwitch.On

let useHighEntropyVASwitch (tcConfigB: TcConfigBuilder) switch =
    tcConfigB.useHighEntropyVA <- switch = OptionSwitch.On

let subSystemVersionSwitch (tcConfigB: TcConfigBuilder) (text: string) =
    let fail() = error(Error(FSComp.SR.optsInvalidSubSystemVersion text, rangeCmdArgs))

    // per spec for 357994: Validate input string, should be two positive integers x.y when x>=4 and y>=0 and both <= 65535
    if System.String.IsNullOrEmpty text then
       fail()
    else
        match text.Split('.') with
        | [| majorStr; minorStr|] ->
            match (Int32.TryParse majorStr), (Int32.TryParse minorStr) with
            | (true, major), (true, minor)
                 when major >= 4 && major <= 65535
                      && minor >=0 && minor <= 65535 ->
                 tcConfigB.subsystemVersion <- (major, minor)
            | _ -> fail()
        | _ -> fail()

let SetUseSdkSwitch (tcConfigB: TcConfigBuilder) switch =
    let useSdkRefs = (switch = OptionSwitch.On)
    tcConfigB.SetUseSdkRefs useSdkRefs

let (++) x s = x @ [s]

let SetTarget (tcConfigB: TcConfigBuilder)(s: string) =
    match s.ToLowerInvariant() with
    | "exe"     ->  tcConfigB.target <- CompilerTarget.ConsoleExe
    | "winexe"  ->  tcConfigB.target <- CompilerTarget.WinExe
    | "library" ->  tcConfigB.target <- CompilerTarget.Dll
    | "module"  ->  tcConfigB.target <- CompilerTarget.Module
    | _         ->  error(Error(FSComp.SR.optsUnrecognizedTarget s, rangeCmdArgs))

let SetDebugSwitch (tcConfigB: TcConfigBuilder) (dtype: string option) (s: OptionSwitch) =
    match dtype with
    | Some s ->
       match s with
       | "portable" ->
           tcConfigB.portablePDB <- true
           tcConfigB.embeddedPDB <- false
           tcConfigB.jitTracking <- true
           tcConfigB.ignoreSymbolStoreSequencePoints <- true
       | "pdbonly" ->
           tcConfigB.portablePDB <- false
           tcConfigB.embeddedPDB <- false
           tcConfigB.jitTracking <- false
       | "embedded" ->
           tcConfigB.portablePDB <- true
           tcConfigB.embeddedPDB <- true
           tcConfigB.jitTracking <- true
           tcConfigB.ignoreSymbolStoreSequencePoints <- true
#if FX_NO_PDB_WRITER
       // When building on the coreclr, full means portable
       | "full" ->
           tcConfigB.portablePDB <- true
           tcConfigB.embeddedPDB <- false
           tcConfigB.jitTracking <- true
#else
       | "full" ->
           tcConfigB.portablePDB <- false
           tcConfigB.embeddedPDB <- false
           tcConfigB.jitTracking <- true
#endif

       | _ -> error(Error(FSComp.SR.optsUnrecognizedDebugType s, rangeCmdArgs))
    | None ->           tcConfigB.portablePDB <- false; tcConfigB.embeddedPDB <- false; tcConfigB.jitTracking <- s = OptionSwitch.On
    tcConfigB.debuginfo <- s = OptionSwitch.On

let SetEmbedAllSourceSwitch (tcConfigB: TcConfigBuilder) switch =
    if (switch = OptionSwitch.On) then tcConfigB.embedAllSource <- true else tcConfigB.embedAllSource <- false

let setOutFileName tcConfigB path =
    let outputDir = Path.GetDirectoryName(path)
    tcConfigB.outputDir <- Some outputDir
    tcConfigB.outputFile <- Some path

let setSignatureFile tcConfigB s =
    tcConfigB.printSignature <- true
    tcConfigB.printSignatureFile <- s

let setAllSignatureFiles tcConfigB () =
    tcConfigB.printAllSignatureFiles <- true

// option tags
let tagString = "<string>"
let tagExe = "exe"
let tagWinExe = "winexe"
let tagLibrary = "library"
let tagModule = "module"
let tagFile = "<file>"
let tagFileList = "<file;...>"
let tagDirList = "<dir;...>"
let tagPathList = "<path;...>"
let tagResInfo = "<resinfo>"
let tagFullPDBOnlyPortable = "{full|pdbonly|portable|embedded}"
let tagWarnList = "<warn;...>"
let tagSymbolList = "<symbol;...>"
let tagAddress = "<address>"
let tagAlgorithm = "{SHA1|SHA256}"
let tagInt = "<n>"
let tagPathMap = "<path=sourcePath;...>"
let tagNone = ""
let tagLangVersionValues = "{?|version|latest|preview}"

// PrintOptionInfo
//----------------

/// Print internal "option state" information for diagnostics and regression tests.
let PrintOptionInfo (tcConfigB:TcConfigBuilder) =
    printfn "  jitOptUser . . . . . . : %+A" tcConfigB.optSettings.jitOptUser
    printfn "  localOptUser . . . . . : %+A" tcConfigB.optSettings.localOptUser
    printfn "  crossModuleOptUser . . : %+A" tcConfigB.optSettings.crossModuleOptUser
    printfn "  lambdaInlineThreshold  : %+A" tcConfigB.optSettings.lambdaInlineThreshold
    printfn "  ignoreSymStoreSeqPts . : %+A" tcConfigB.ignoreSymbolStoreSequencePoints
    printfn "  doDetuple  . . . . . . : %+A" tcConfigB.doDetuple
    printfn "  doTLR  . . . . . . . . : %+A" tcConfigB.doTLR
    printfn "  doFinalSimplify. . . . : %+A" tcConfigB.doFinalSimplify
    printfn "  jitTracking  . . . . . : %+A" tcConfigB.jitTracking
    printfn "  portablePDB. . . . . . : %+A" tcConfigB.portablePDB
    printfn "  embeddedPDB. . . . . . : %+A" tcConfigB.embeddedPDB
    printfn "  embedAllSource . . . . : %+A" tcConfigB.embedAllSource
    printfn "  embedSourceList. . . . : %+A" tcConfigB.embedSourceList
    printfn "  sourceLink . . . . . . : %+A" tcConfigB.sourceLink
    printfn "  debuginfo  . . . . . . : %+A" tcConfigB.debuginfo
    printfn "  resolutionEnvironment  : %+A" tcConfigB.resolutionEnvironment
    printfn "  product  . . . . . . . : %+A" tcConfigB.productNameForBannerText
    printfn "  copyFSharpCore . . . . : %+A" tcConfigB.copyFSharpCore
    tcConfigB.includes |> List.sort
                       |> List.iter (printfn "  include  . . . . . . . : %A")

// OptionBlock: Input files
//-------------------------

let inputFileFlagsBoth (tcConfigB : TcConfigBuilder) = [
    CompilerOption("reference", tagFile, OptionString (fun s -> tcConfigB.AddReferencedAssemblyByPath (rangeStartup, s)), None, Some (FSComp.SR.optsReference()))
    CompilerOption("compilertool", tagFile, OptionString (fun s -> tcConfigB.AddCompilerToolsByPath s), None, Some (FSComp.SR.optsCompilerTool()))
    ]

let referenceFlagAbbrev (tcConfigB : TcConfigBuilder) =
    CompilerOption("r", tagFile, OptionString (fun s -> tcConfigB.AddReferencedAssemblyByPath (rangeStartup, s)), None, Some(FSComp.SR.optsShortFormOf("--reference")))

let compilerToolFlagAbbrev (tcConfigB : TcConfigBuilder) =
    CompilerOption("t", tagFile, OptionString (fun s -> tcConfigB.AddCompilerToolsByPath s), None, Some(FSComp.SR.optsShortFormOf("--compilertool")))

let inputFileFlagsFsc tcConfigB = inputFileFlagsBoth tcConfigB

let inputFileFlagsFsiBase (_tcConfigB: TcConfigBuilder) =
#if NETSTANDARD
    [ CompilerOption("usesdkrefs", tagNone, OptionSwitch (SetUseSdkSwitch _tcConfigB), None, Some (FSComp.SR.useSdkRefs())) ]
#else
    List.empty<CompilerOption>
#endif

let inputFileFlagsFsi (tcConfigB: TcConfigBuilder) =
    List.append (inputFileFlagsBoth tcConfigB) (inputFileFlagsFsiBase tcConfigB)

// OptionBlock: Errors and warnings
//---------------------------------

let errorsAndWarningsFlags (tcConfigB: TcConfigBuilder) =
    let trimFS (s:string) = if s.StartsWithOrdinal "FS" then s.Substring 2 else s
    let trimFStoInt (s:string) =
        match Int32.TryParse (trimFS s) with
        | true, n ->  Some n
        | false, _ -> None
    [
        CompilerOption("warnaserror", tagNone, OptionSwitch(fun switch ->
            tcConfigB.errorSeverityOptions <-
                { tcConfigB.errorSeverityOptions with
                    GlobalWarnAsError = switch <> OptionSwitch.Off }), None, Some (FSComp.SR.optsWarnaserrorPM()))

        CompilerOption("warnaserror", tagWarnList, OptionStringListSwitch (fun n switch ->
            match trimFStoInt n with
            | Some n ->
                let options = tcConfigB.errorSeverityOptions
                tcConfigB.errorSeverityOptions <-
                    if switch = OptionSwitch.Off then
                        { options with
                            WarnAsError = ListSet.remove (=) n options.WarnAsError
                            WarnAsWarn = ListSet.insert (=) n options.WarnAsWarn }
                    else
                        { options with
                            WarnAsError = ListSet.insert (=) n options.WarnAsError
                            WarnAsWarn = ListSet.remove (=) n options.WarnAsWarn }
            | None -> ()), None, Some (FSComp.SR.optsWarnaserror()))

        CompilerOption("warn", tagInt, OptionInt (fun n ->
                 tcConfigB.errorSeverityOptions <-
                     { tcConfigB.errorSeverityOptions with
                         WarnLevel = if (n >= 0 && n <= 5) then n else error(Error (FSComp.SR.optsInvalidWarningLevel n, rangeCmdArgs)) }
            ), None, Some (FSComp.SR.optsWarn()))

        CompilerOption("nowarn", tagWarnList, OptionStringList (fun n ->
            tcConfigB.TurnWarningOff(rangeCmdArgs, trimFS n)), None, Some (FSComp.SR.optsNowarn()))

        CompilerOption("warnon", tagWarnList, OptionStringList (fun n ->
            tcConfigB.TurnWarningOn(rangeCmdArgs, trimFS n)), None, Some (FSComp.SR.optsWarnOn()))

        CompilerOption("consolecolors", tagNone, OptionSwitch (fun switch ->
            enableConsoleColoring <- switch = OptionSwitch.On), None, Some (FSComp.SR.optsConsoleColors()))
    ]


// OptionBlock: Output files
//--------------------------

let outputFileFlagsFsi (_tcConfigB: TcConfigBuilder) = []

let outputFileFlagsFsc (tcConfigB: TcConfigBuilder) =
    [
        CompilerOption
           ("out", tagFile,
            OptionString (setOutFileName tcConfigB), None,
            Some (FSComp.SR.optsNameOfOutputFile()) )

        CompilerOption
           ("target", tagExe,
            OptionString (SetTarget tcConfigB), None,
            Some (FSComp.SR.optsBuildConsole()))

        CompilerOption
           ("target", tagWinExe,
            OptionString (SetTarget tcConfigB), None,
            Some (FSComp.SR.optsBuildWindows()))

        CompilerOption
           ("target", tagLibrary,
            OptionString (SetTarget tcConfigB), None,
            Some (FSComp.SR.optsBuildLibrary()))

        CompilerOption
           ("target", tagModule,
            OptionString (SetTarget tcConfigB), None,
            Some (FSComp.SR.optsBuildModule()))

        CompilerOption
           ("delaysign", tagNone,
            OptionSwitch (fun s -> tcConfigB.delaysign <- (s = OptionSwitch.On)), None,
            Some (FSComp.SR.optsDelaySign()))

        CompilerOption
           ("publicsign", tagNone,
            OptionSwitch (fun s -> tcConfigB.publicsign <- (s = OptionSwitch.On)), None,
            Some (FSComp.SR.optsPublicSign()))

        CompilerOption
           ("doc", tagFile,
            OptionString (fun s -> tcConfigB.xmlDocOutputFile <- Some s), None,
            Some (FSComp.SR.optsWriteXml()))

        CompilerOption
           ("keyfile", tagFile,
            OptionString (fun s -> tcConfigB.signer <- Some s), None,
            Some (FSComp.SR.optsStrongKeyFile()))

        CompilerOption
           ("platform", tagString,
            OptionString (fun s ->
                tcConfigB.platform <-
                    match s with
                    | "x86" -> Some X86
                    | "x64" -> Some AMD64
                    | "Itanium" -> Some IA64
                    | "anycpu32bitpreferred" ->
                        tcConfigB.prefer32Bit <- true
                        None
                    | "anycpu" -> None
                    | _ -> error(Error(FSComp.SR.optsUnknownPlatform s, rangeCmdArgs))), None,
            Some(FSComp.SR.optsPlatform()))

        CompilerOption
           ("nooptimizationdata", tagNone,
            OptionUnit (fun () -> tcConfigB.onlyEssentialOptimizationData <- true), None,
            Some (FSComp.SR.optsNoOpt()))

        CompilerOption
           ("nointerfacedata", tagNone,
            OptionUnit (fun () -> tcConfigB.noSignatureData <- true), None,
            Some (FSComp.SR.optsNoInterface()))

        CompilerOption
           ("sig", tagFile,
            OptionString (setSignatureFile tcConfigB), None,
            Some (FSComp.SR.optsSig()))

        CompilerOption
           ("allsigs", tagNone,
            OptionUnit (setAllSignatureFiles tcConfigB), None,
            Some (FSComp.SR.optsAllSigs()))

        CompilerOption
           ("nocopyfsharpcore", tagNone,
            OptionUnit (fun () -> tcConfigB.copyFSharpCore <- CopyFSharpCoreFlag.No), None,
            Some (FSComp.SR.optsNoCopyFsharpCore()))
    ]


// OptionBlock: Resources
//-----------------------

let resourcesFlagsFsi (_tcConfigB: TcConfigBuilder) = []
let resourcesFlagsFsc (tcConfigB: TcConfigBuilder) =
    [
        CompilerOption
            ("win32icon", tagFile,
             OptionString (fun s -> tcConfigB.win32icon <- s), None,
             Some (FSComp.SR.optsWin32icon()))
        CompilerOption
           ("win32res", tagFile,
            OptionString (fun s -> tcConfigB.win32res <- s), None,
            Some (FSComp.SR.optsWin32res()))

        CompilerOption
           ("win32manifest", tagFile,
            OptionString (fun s -> tcConfigB.win32manifest <- s), None,
            Some (FSComp.SR.optsWin32manifest()))

        CompilerOption
           ("nowin32manifest", tagNone,
            OptionUnit (fun () -> tcConfigB.includewin32manifest <- false), None,
            Some (FSComp.SR.optsNowin32manifest()))

        CompilerOption
           ("resource", tagResInfo,
            OptionString (fun s -> tcConfigB.AddEmbeddedResource s), None,
            Some (FSComp.SR.optsResource()))

        CompilerOption
           ("linkresource", tagResInfo,
            OptionString (fun s -> tcConfigB.linkResources <- tcConfigB.linkResources ++ s), None,
            Some (FSComp.SR.optsLinkresource()))
    ]


// OptionBlock: Code generation
//-----------------------------

let codeGenerationFlags isFsi (tcConfigB: TcConfigBuilder) =
    let debug =
        [ CompilerOption
            ("debug", tagNone,
             OptionSwitch (SetDebugSwitch tcConfigB None), None,
             Some (FSComp.SR.optsDebugPM()))

          CompilerOption
             ("debug", tagFullPDBOnlyPortable,
              OptionString (fun s -> SetDebugSwitch tcConfigB (Some s) OptionSwitch.On), None,
              Some (FSComp.SR.optsDebug(if isFsi then "pdbonly" else "full")))
        ]
    let embed =
        [ CompilerOption
            ("embed", tagNone,
             OptionSwitch (SetEmbedAllSourceSwitch tcConfigB), None,
             Some (FSComp.SR.optsEmbedAllSource()))

          CompilerOption
            ("embed", tagFileList,
             OptionStringList (fun f -> tcConfigB.AddEmbeddedSourceFile f), None,
             Some ( FSComp.SR.optsEmbedSource()))

          CompilerOption
            ("sourcelink", tagFile,
             OptionString (fun f -> tcConfigB.sourceLink <- f), None,
             Some ( FSComp.SR.optsSourceLink()))
        ]

    let codegen =
        [ CompilerOption
            ("optimize", tagNone,
             OptionSwitch (SetOptimizeSwitch tcConfigB), None,
             Some (FSComp.SR.optsOptimize()))

          CompilerOption
           ("tailcalls", tagNone,
            OptionSwitch (SetTailcallSwitch tcConfigB), None,
            Some (FSComp.SR.optsTailcalls()))

          CompilerOption
           ("deterministic", tagNone,
            OptionSwitch (SetDeterministicSwitch tcConfigB), None,
            Some (FSComp.SR.optsDeterministic()))

          CompilerOption
           ("pathmap", tagPathMap,
            OptionStringList (AddPathMapping tcConfigB), None,
            Some (FSComp.SR.optsPathMap()))

          CompilerOption
           ("crossoptimize", tagNone,
            OptionSwitch (crossOptimizeSwitch tcConfigB), None,
            Some (FSComp.SR.optsCrossoptimize()))
        ]
    if isFsi then debug @ codegen
    else debug @ embed @ codegen

// OptionBlock: Language
//----------------------

let defineSymbol tcConfigB s = tcConfigB.conditionalCompilationDefines <- s :: tcConfigB.conditionalCompilationDefines

let mlCompatibilityFlag (tcConfigB: TcConfigBuilder) =
    CompilerOption
       ("mlcompatibility", tagNone,
        OptionUnit (fun () -> tcConfigB.mlCompatibility<-true; tcConfigB.TurnWarningOff(rangeCmdArgs, "62")), None,
        Some (FSComp.SR.optsMlcompatibility()))

/// LanguageVersion management
let setLanguageVersion (specifiedVersion) =

    let languageVersion = new LanguageVersion(specifiedVersion)
    let dumpAllowedValues () =
        printfn "%s" (FSComp.SR.optsSupportedLangVersions())
        for v in languageVersion.ValidOptions do printfn "%s" v
        for v in languageVersion.ValidVersions do printfn "%s" v
        exit 0

    if specifiedVersion = "?" then dumpAllowedValues ()
    if not (languageVersion.ContainsVersion specifiedVersion) then error(Error(FSComp.SR.optsUnrecognizedLanguageVersion specifiedVersion, rangeCmdArgs))
    languageVersion

let languageFlags tcConfigB =
    [
        // -langversion:?                Display the allowed values for language version
        // -langversion:<string>         Specify language version such as
        //                               'default' (latest major version), or
        //                               'latest' (latest version, including minor versions),
        //                               'preview' (features for preview)
        //                               or specific versions like '4.7'
        CompilerOption("langversion", tagLangVersionValues, OptionString (fun switch -> tcConfigB.langVersion <- setLanguageVersion(switch)), None, Some (FSComp.SR.optsLangVersion()))

        CompilerOption("checked", tagNone, OptionSwitch (fun switch -> tcConfigB.checkOverflow <- (switch = OptionSwitch.On)), None, Some (FSComp.SR.optsChecked()))
        CompilerOption("define", tagString, OptionString (defineSymbol tcConfigB), None, Some (FSComp.SR.optsDefine()))
        mlCompatibilityFlag tcConfigB
    ]

// OptionBlock: Advanced user options
//-----------------------------------

let libFlag (tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("lib", tagDirList,
         OptionStringList (fun s -> tcConfigB.AddIncludePath (rangeStartup, s, tcConfigB.implicitIncludeDir)), None,
         Some (FSComp.SR.optsLib()))

let codePageFlag (tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("codepage", tagInt,
         OptionInt (fun n ->
            try
                System.Text.Encoding.GetEncoding n |> ignore
            with :? System.ArgumentException as err ->
                error(Error(FSComp.SR.optsProblemWithCodepage(n, err.Message), rangeCmdArgs))

            tcConfigB.inputCodePage <- Some n), None,
                Some (FSComp.SR.optsCodepage()))

let preferredUiLang (tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("preferreduilang", tagString,
         OptionString (fun s -> tcConfigB.preferredUiLang <- Some s), None,
         Some(FSComp.SR.optsPreferredUiLang()))

let utf8OutputFlag (tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("utf8output", tagNone,
         OptionUnit (fun () -> tcConfigB.utf8output <- true), None,
         Some (FSComp.SR.optsUtf8output()))

let fullPathsFlag  (tcConfigB: TcConfigBuilder)  =
    CompilerOption
        ("fullpaths", tagNone,
         OptionUnit (fun () -> tcConfigB.showFullPaths <- true), None,
         Some (FSComp.SR.optsFullpaths()))

let cliRootFlag (_tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("cliroot", tagString,
         OptionString (fun _  -> ()), Some(DeprecatedCommandLineOptionFull(FSComp.SR.optsClirootDeprecatedMsg(), rangeCmdArgs)),
         Some(FSComp.SR.optsClirootDescription()))

let SetTargetProfile (tcConfigB: TcConfigBuilder) v =
    let primaryAssembly =
        match v with
        // Indicates we assume "mscorlib.dll", i.e .NET Framework, Mono and Profile 47
        | "mscorlib" -> PrimaryAssembly.Mscorlib
        // Indicates we assume "System.Runtime.dll", i.e .NET Standard 1.x, .NET Core App 1.x and above, and Profile 7/78/259
        | "netcore"  -> PrimaryAssembly.System_Runtime
        // Indicates we assume "netstandard.dll", i.e .NET Standard 2.0 and above
        | "netstandard"  -> PrimaryAssembly.NetStandard
        | _ -> error(Error(FSComp.SR.optsInvalidTargetProfile v, rangeCmdArgs))
    tcConfigB.SetPrimaryAssembly  primaryAssembly

let advancedFlagsBoth tcConfigB =
    [
        yield codePageFlag tcConfigB
        yield utf8OutputFlag tcConfigB
        yield preferredUiLang tcConfigB
        yield fullPathsFlag tcConfigB
        yield libFlag tcConfigB
        yield CompilerOption
                 ("simpleresolution",
                  tagNone,
                  OptionUnit (fun () -> tcConfigB.useSimpleResolution<-true), None,
                  Some (FSComp.SR.optsSimpleresolution()))

        yield CompilerOption
                 ("targetprofile", tagString,
                  OptionString (SetTargetProfile tcConfigB), None,
                  Some(FSComp.SR.optsTargetProfile()))
    ]

let noFrameworkFlag isFsc tcConfigB =
    CompilerOption
        ("noframework", tagNone,
         OptionUnit (fun () ->
            tcConfigB.framework <- false
            if isFsc then
                tcConfigB.implicitlyResolveAssemblies <- false), None,
         Some (FSComp.SR.optsNoframework()))

let advancedFlagsFsi tcConfigB =
    advancedFlagsBoth tcConfigB  @
    [
        yield noFrameworkFlag false tcConfigB
    ]

let advancedFlagsFsc tcConfigB =
    advancedFlagsBoth tcConfigB @
    [
        yield CompilerOption
                  ("baseaddress", tagAddress,
                   OptionString (fun s -> tcConfigB.baseAddress <- Some(int32 s)), None,
                   Some (FSComp.SR.optsBaseaddress()))

        yield CompilerOption
                  ("checksumalgorithm", tagAlgorithm,
                   OptionString (fun s ->
                       tcConfigB.checksumAlgorithm <-
                        match s.ToUpperInvariant() with
                        | "SHA1" -> HashAlgorithm.Sha1
                        | "SHA256" -> HashAlgorithm.Sha256
                        | _ -> error(Error(FSComp.SR.optsUnknownChecksumAlgorithm s, rangeCmdArgs))), None,
                        Some (FSComp.SR.optsChecksumAlgorithm()))

        yield noFrameworkFlag true tcConfigB

        yield CompilerOption
                  ("standalone", tagNone,
                   OptionUnit (fun _ ->
                        tcConfigB.openDebugInformationForLaterStaticLinking <- true
                        tcConfigB.standalone <- true
                        tcConfigB.implicitlyResolveAssemblies <- true), None,
                   Some (FSComp.SR.optsStandalone()))

        yield CompilerOption
                  ("staticlink", tagFile,
                   OptionString (fun s ->
                       tcConfigB.extraStaticLinkRoots <- tcConfigB.extraStaticLinkRoots @ [s]
                       tcConfigB.implicitlyResolveAssemblies <- true), None,
                   Some (FSComp.SR.optsStaticlink()))

#if ENABLE_MONO_SUPPORT
        if runningOnMono then
            yield CompilerOption
                      ("resident", tagFile,
                       OptionUnit (fun () -> ()), None,
                       Some (FSComp.SR.optsResident()))
#endif

        yield CompilerOption
                  ("pdb", tagString,
                   OptionString (fun s -> tcConfigB.debugSymbolFile <- Some s), None,
                   Some (FSComp.SR.optsPdb()))

        yield CompilerOption
                  ("highentropyva", tagNone,
                   OptionSwitch (useHighEntropyVASwitch tcConfigB), None,
                   Some (FSComp.SR.optsUseHighEntropyVA()))

        yield CompilerOption
                  ("subsystemversion", tagString,
                   OptionString (subSystemVersionSwitch tcConfigB), None,
                   Some (FSComp.SR.optsSubSystemVersion()))

        yield CompilerOption
                  ("quotations-debug", tagNone,
                   OptionSwitch(fun switch -> tcConfigB.emitDebugInfoInQuotations <- switch = OptionSwitch.On), None,
                   Some(FSComp.SR.optsEmitDebugInfoInQuotations()))

    ]

// OptionBlock: Internal options (test use only)
//--------------------------------------------------

let testFlag tcConfigB =
        CompilerOption
            ("test", tagString,
             OptionString (fun s ->
                match s with
                | "StackSpan"        -> tcConfigB.internalTestSpanStackReferring <- true
                | "ErrorRanges"      -> tcConfigB.errorStyle <- ErrorStyle.TestErrors
                | "Tracking"         -> tracking <- true (* general purpose on/off diagnostics flag *)
                | "NoNeedToTailcall" -> tcConfigB.optSettings <- { tcConfigB.optSettings with reportNoNeedToTailcall = true }
                | "FunctionSizes"    -> tcConfigB.optSettings <- { tcConfigB.optSettings with reportFunctionSizes = true }
                | "TotalSizes"       -> tcConfigB.optSettings <- { tcConfigB.optSettings with reportTotalSizes = true }
                | "HasEffect"        -> tcConfigB.optSettings <- { tcConfigB.optSettings with reportHasEffect = true }
                | "NoErrorText"      -> FSComp.SR.SwallowResourceText <- true
                | "EmitFeeFeeAs100001" -> tcConfigB.testFlagEmitFeeFeeAs100001 <- true
                | "DumpDebugInfo"    -> tcConfigB.dumpDebugInfo <- true
                | "ShowLoadedAssemblies" -> tcConfigB.showLoadedAssemblies <- true
                | "ContinueAfterParseFailure" -> tcConfigB.continueAfterParseFailure <- true
                | "ParallelOff" -> tcConfigB.concurrentBuild <- false
#if DEBUG
                | "ShowParserStackOnParseError" -> showParserStackOnParseError <- true
#endif
                | str                -> warning(Error(FSComp.SR.optsUnknownArgumentToTheTestSwitch str, rangeCmdArgs))), None,
             None)

// Not shown in fsc.exe help, no warning on use, motivation is for use from tooling.
let editorSpecificFlags (tcConfigB: TcConfigBuilder) =
  [ CompilerOption("vserrors", tagNone, OptionUnit (fun () -> tcConfigB.errorStyle <- ErrorStyle.VSErrors), None, None)
    CompilerOption("validate-type-providers", tagNone, OptionUnit id, None, None)  // preserved for compatibility's sake, no longer has any effect
    CompilerOption("LCID", tagInt, OptionInt ignore, None, None)
    CompilerOption("flaterrors", tagNone, OptionUnit (fun () -> tcConfigB.flatErrors <- true), None, None)
    CompilerOption("sqmsessionguid", tagNone, OptionString ignore, None, None)
    CompilerOption("gccerrors", tagNone, OptionUnit (fun () -> tcConfigB.errorStyle <- ErrorStyle.GccErrors), None, None)
    CompilerOption("exename", tagNone, OptionString (fun s -> tcConfigB.exename <- Some s), None, None)
    CompilerOption("maxerrors", tagInt, OptionInt (fun n -> tcConfigB.maxErrors <- n), None, None)
    CompilerOption("noconditionalerasure", tagNone, OptionUnit (fun () -> tcConfigB.noConditionalErasure <- true), None, None) ]

let internalFlags (tcConfigB:TcConfigBuilder) =
  [
    CompilerOption
       ("stamps", tagNone,
        OptionSet DebugPrint.layoutStamps,
        Some(InternalCommandLineOption("--stamps", rangeCmdArgs)), None)

    CompilerOption
       ("ranges", tagNone,
        OptionSet DebugPrint.layoutRanges,
        Some(InternalCommandLineOption("--ranges", rangeCmdArgs)), None)

    CompilerOption
       ("terms", tagNone,
        OptionUnit (fun () -> tcConfigB.showTerms <- true),
        Some(InternalCommandLineOption("--terms", rangeCmdArgs)), None)

    CompilerOption
       ("termsfile", tagNone,
        OptionUnit (fun () -> tcConfigB.writeTermsToFiles <- true),
        Some(InternalCommandLineOption("--termsfile", rangeCmdArgs)), None)

#if DEBUG
    CompilerOption
       ("debug-parse", tagNone,
        OptionUnit (fun () -> Internal.Utilities.Text.Parsing.Flags.debug <- true),
        Some(InternalCommandLineOption("--debug-parse", rangeCmdArgs)), None)
#endif

    CompilerOption
       ("pause", tagNone,
        OptionUnit (fun () -> tcConfigB.pause <- true),
        Some(InternalCommandLineOption("--pause", rangeCmdArgs)), None)

    CompilerOption
       ("detuple", tagNone,
        OptionInt (setFlag (fun v -> tcConfigB.doDetuple <- v)),
        Some(InternalCommandLineOption("--detuple", rangeCmdArgs)), None)

    CompilerOption
       ("simulateException", tagNone,
        OptionString (fun s -> tcConfigB.simulateException <- Some s),
        Some(InternalCommandLineOption("--simulateException", rangeCmdArgs)), Some "Simulate an exception from some part of the compiler")

    CompilerOption
       ("stackReserveSize", tagNone,
        OptionString (fun s -> tcConfigB.stackReserveSize <- Some(int32 s)),
        Some(InternalCommandLineOption("--stackReserveSize", rangeCmdArgs)), Some ("for an exe, set stack reserve size"))

    CompilerOption
       ("tlr", tagInt,
        OptionInt (setFlag (fun v -> tcConfigB.doTLR <- v)),
        Some(InternalCommandLineOption("--tlr", rangeCmdArgs)), None)

    CompilerOption
       ("finalSimplify", tagInt,
        OptionInt (setFlag (fun v -> tcConfigB.doFinalSimplify <- v)),
        Some(InternalCommandLineOption("--finalSimplify", rangeCmdArgs)), None)

    CompilerOption
       ("parseonly", tagNone,
        OptionUnit (fun () -> tcConfigB.parseOnly <- true),
        Some(InternalCommandLineOption("--parseonly", rangeCmdArgs)), None)

    CompilerOption
       ("typecheckonly", tagNone,
        OptionUnit (fun () -> tcConfigB.typeCheckOnly <- true),
        Some(InternalCommandLineOption("--typecheckonly", rangeCmdArgs)), None)

    CompilerOption
       ("ast", tagNone,
        OptionUnit (fun () -> tcConfigB.printAst <- true),
        Some(InternalCommandLineOption("--ast", rangeCmdArgs)), None)

    CompilerOption
       ("tokenize", tagNone,
        OptionUnit (fun () -> tcConfigB.tokenize <- TokenizeOption.Only),
        Some(InternalCommandLineOption("--tokenize", rangeCmdArgs)), None)

    CompilerOption
       ("tokenize-unfiltered", tagNone,
        OptionUnit (fun () -> tcConfigB.tokenize <- TokenizeOption.Unfiltered),
        Some(InternalCommandLineOption("--tokenize-unfiltered", rangeCmdArgs)), None)
    
    CompilerOption
       ("testInteractionParser", tagNone,
        OptionUnit (fun () -> tcConfigB.testInteractionParser <- true),
        Some(InternalCommandLineOption("--testInteractionParser", rangeCmdArgs)), None)

    CompilerOption
       ("testparsererrorrecovery", tagNone,
        OptionUnit (fun () -> tcConfigB.reportNumDecls <- true),
        Some(InternalCommandLineOption("--testparsererrorrecovery", rangeCmdArgs)), None)

    CompilerOption
       ("inlinethreshold", tagInt,
        OptionInt (fun n -> tcConfigB.optSettings <- { tcConfigB.optSettings with lambdaInlineThreshold = n }),
        Some(InternalCommandLineOption("--inlinethreshold", rangeCmdArgs)), None)

    CompilerOption
       ("extraoptimizationloops", tagNone,
        OptionInt (fun n -> tcConfigB.extraOptimizationIterations <- n),
        Some(InternalCommandLineOption("--extraoptimizationloops", rangeCmdArgs)), None)

    CompilerOption
       ("abortonerror", tagNone,
        OptionUnit (fun () -> tcConfigB.abortOnError <- true),
        Some(InternalCommandLineOption("--abortonerror", rangeCmdArgs)), None)

    CompilerOption
       ("implicitresolution", tagNone,
        OptionUnit (fun _ -> tcConfigB.implicitlyResolveAssemblies <- true),
        Some(InternalCommandLineOption("--implicitresolution", rangeCmdArgs)), None)

    // "Display assembly reference resolution information")
    CompilerOption
       ("resolutions", tagNone,
        OptionUnit (fun () -> tcConfigB.showReferenceResolutions <- true),
        Some(InternalCommandLineOption("", rangeCmdArgs)), None)

    // "The base registry key to use for assembly resolution. This part in brackets here: HKEY_LOCAL_MACHINE\[SOFTWARE\Microsoft\.NETFramework]\v2.0.50727\AssemblyFoldersEx")
    CompilerOption
       ("resolutionframeworkregistrybase", tagString,
        OptionString (fun _ -> ()),
        Some(InternalCommandLineOption("", rangeCmdArgs)), None)

    // "The base registry key to use for assembly resolution. This part in brackets here: HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\v2.0.50727\[AssemblyFoldersEx]")
    CompilerOption
       ("resolutionassemblyfoldersuffix", tagString,
        OptionString (fun _ -> ()),
        Some(InternalCommandLineOption("resolutionassemblyfoldersuffix", rangeCmdArgs)), None)

    // "Additional reference resolution conditions. For example \"OSVersion=5.1.2600.0, PlatformID=id")
    CompilerOption
       ("resolutionassemblyfoldersconditions", tagString,
        OptionString (fun _ -> ()),
        Some(InternalCommandLineOption("resolutionassemblyfoldersconditions", rangeCmdArgs)), None)

    // "Resolve assembly references using MSBuild resolution rules rather than directory based (Default=true except when running fsc.exe under mono)")
    CompilerOption
       ("msbuildresolution", tagNone,
        OptionUnit (fun () -> tcConfigB.useSimpleResolution<-false),
        Some(InternalCommandLineOption("msbuildresolution", rangeCmdArgs)), None)

    CompilerOption
       ("alwayscallvirt", tagNone,
        OptionSwitch(callVirtSwitch tcConfigB),
        Some(InternalCommandLineOption("alwayscallvirt", rangeCmdArgs)), None)

    CompilerOption
       ("nodebugdata", tagNone,
        OptionUnit (fun () -> tcConfigB.noDebugData<-true),
        Some(InternalCommandLineOption("--nodebugdata", rangeCmdArgs)), None)

    testFlag tcConfigB  ] @

  editorSpecificFlags tcConfigB @
  [ CompilerOption
       ("jit", tagNone,
        OptionSwitch (jitoptimizeSwitch tcConfigB),
        Some(InternalCommandLineOption("jit", rangeCmdArgs)), None)

    CompilerOption
       ("localoptimize", tagNone,
        OptionSwitch(localoptimizeSwitch tcConfigB),
        Some(InternalCommandLineOption("localoptimize", rangeCmdArgs)), None)

    CompilerOption
       ("splitting", tagNone,
        OptionSwitch(splittingSwitch tcConfigB),
        Some(InternalCommandLineOption("splitting", rangeCmdArgs)), None)

    CompilerOption
       ("versionfile", tagString,
        OptionString (fun s -> tcConfigB.version <- VersionFile s),
        Some(InternalCommandLineOption("versionfile", rangeCmdArgs)), None)

    // "Display timing profiles for compilation"
    CompilerOption
       ("times", tagNone,
        OptionUnit  (fun () -> tcConfigB.showTimes <- true),
        Some(InternalCommandLineOption("times", rangeCmdArgs)), None)

#if !NO_EXTENSIONTYPING
    // "Display information about extension type resolution")
    CompilerOption
       ("showextensionresolution", tagNone,
        OptionUnit  (fun () -> tcConfigB.showExtensionTypeMessages <- true),
        Some(InternalCommandLineOption("showextensionresolution", rangeCmdArgs)), None)
#endif

    CompilerOption
       ("metadataversion", tagString,
        OptionString (fun s -> tcConfigB.metadataVersion <- Some s),
        Some(InternalCommandLineOption("metadataversion", rangeCmdArgs)), None)
  ]

// OptionBlock: Deprecated flags (fsc, service only)
//--------------------------------------------------

let compilingFsLibFlag (tcConfigB: TcConfigBuilder) =
    CompilerOption
        ("compiling-fslib", tagNone,
         OptionUnit (fun () ->
            tcConfigB.compilingFslib <- true
            tcConfigB.TurnWarningOff(rangeStartup, "42")),
         Some(InternalCommandLineOption("--compiling-fslib", rangeCmdArgs)), None)

let compilingFsLib20Flag =
    CompilerOption ("compiling-fslib-20", tagNone, OptionString (fun _ -> () ), None, None)

let compilingFsLib40Flag =
    CompilerOption ("compiling-fslib-40", tagNone, OptionUnit (fun () -> ()), None, None)

let compilingFsLibNoBigIntFlag =
    CompilerOption ("compiling-fslib-nobigint", tagNone, OptionUnit (fun () -> () ), None, None)

let mlKeywordsFlag =
    CompilerOption
        ("ml-keywords", tagNone,
         OptionUnit (fun () -> ()),
         Some(DeprecatedCommandLineOptionNoDescription("--ml-keywords", rangeCmdArgs)), None)

let gnuStyleErrorsFlag tcConfigB =
    CompilerOption
        ("gnu-style-errors", tagNone,
         OptionUnit (fun () -> tcConfigB.errorStyle <- ErrorStyle.EmacsErrors),
         Some(DeprecatedCommandLineOptionNoDescription("--gnu-style-errors", rangeCmdArgs)), None)

let deprecatedFlagsBoth tcConfigB =
    [
      CompilerOption
         ("light", tagNone,
          OptionUnit (fun () -> tcConfigB.light <- Some true),
          Some(DeprecatedCommandLineOptionNoDescription("--light", rangeCmdArgs)), None)

      CompilerOption
         ("indentation-syntax", tagNone,
          OptionUnit (fun () -> tcConfigB.light <- Some true),
          Some(DeprecatedCommandLineOptionNoDescription("--indentation-syntax", rangeCmdArgs)), None)

      CompilerOption
         ("no-indentation-syntax", tagNone,
          OptionUnit (fun () -> tcConfigB.light <- Some false),
          Some(DeprecatedCommandLineOptionNoDescription("--no-indentation-syntax", rangeCmdArgs)), None)
    ]

let deprecatedFlagsFsi tcConfigB = deprecatedFlagsBoth tcConfigB

let deprecatedFlagsFsc tcConfigB =
    deprecatedFlagsBoth tcConfigB @
    [
    cliRootFlag tcConfigB
    CompilerOption
       ("jit-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with jitOptUser = Some true }),
        Some(DeprecatedCommandLineOptionNoDescription("--jit-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("no-jit-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with jitOptUser = Some false }),
        Some(DeprecatedCommandLineOptionNoDescription("--no-jit-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("jit-tracking", tagNone,
        OptionUnit (fun _ -> (tcConfigB.jitTracking <- true) ),
        Some(DeprecatedCommandLineOptionNoDescription("--jit-tracking", rangeCmdArgs)), None)

    CompilerOption
       ("no-jit-tracking", tagNone,
        OptionUnit (fun _ -> (tcConfigB.jitTracking <- false) ),
        Some(DeprecatedCommandLineOptionNoDescription("--no-jit-tracking", rangeCmdArgs)), None)

    CompilerOption
       ("progress", tagNone,
        OptionUnit (fun () -> progress <- true),
        Some(DeprecatedCommandLineOptionNoDescription("--progress", rangeCmdArgs)), None)

    compilingFsLibFlag tcConfigB
    compilingFsLib20Flag
    compilingFsLib40Flag
    compilingFsLibNoBigIntFlag

    CompilerOption
       ("version", tagString,
        OptionString (fun s -> tcConfigB.version <- VersionString s),
        Some(DeprecatedCommandLineOptionNoDescription("--version", rangeCmdArgs)), None)

    CompilerOption
       ("local-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with localOptUser = Some true }),
        Some(DeprecatedCommandLineOptionNoDescription("--local-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("no-local-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with localOptUser = Some false }),
        Some(DeprecatedCommandLineOptionNoDescription("--no-local-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("cross-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with crossModuleOptUser = Some true }),
        Some(DeprecatedCommandLineOptionNoDescription("--cross-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("no-cross-optimize", tagNone,
        OptionUnit (fun _ -> tcConfigB.optSettings <- { tcConfigB.optSettings with crossModuleOptUser = Some false }),
        Some(DeprecatedCommandLineOptionNoDescription("--no-cross-optimize", rangeCmdArgs)), None)

    CompilerOption
       ("no-string-interning", tagNone,
        OptionUnit (fun () -> tcConfigB.internConstantStrings <- false),
        Some(DeprecatedCommandLineOptionNoDescription("--no-string-interning", rangeCmdArgs)), None)

    CompilerOption
       ("statistics", tagNone,
        OptionUnit (fun () -> tcConfigB.stats <- true),
        Some(DeprecatedCommandLineOptionNoDescription("--statistics", rangeCmdArgs)), None)

    CompilerOption
       ("generate-filter-blocks", tagNone,
        OptionUnit (fun () -> tcConfigB.generateFilterBlocks <- true),
        Some(DeprecatedCommandLineOptionNoDescription("--generate-filter-blocks", rangeCmdArgs)), None)

    //CompilerOption
    //    ("no-generate-filter-blocks", tagNone,
    //     OptionUnit (fun () -> tcConfigB.generateFilterBlocks <- false),
    //     Some(DeprecatedCommandLineOptionNoDescription("--generate-filter-blocks", rangeCmdArgs)), None)

    CompilerOption
       ("max-errors", tagInt,
        OptionInt (fun n -> tcConfigB.maxErrors <- n),
        Some(DeprecatedCommandLineOptionSuggestAlternative("--max-errors", "--maxerrors", rangeCmdArgs)), None)

    CompilerOption
       ("debug-file", tagNone,
        OptionString (fun s -> tcConfigB.debugSymbolFile <- Some s),
        Some(DeprecatedCommandLineOptionSuggestAlternative("--debug-file", "--pdb", rangeCmdArgs)), None)

    CompilerOption
       ("no-debug-file", tagNone,
        OptionUnit (fun () -> tcConfigB.debuginfo <- false),
        Some(DeprecatedCommandLineOptionSuggestAlternative("--no-debug-file", "--debug-", rangeCmdArgs)), None)

    CompilerOption
       ("Ooff", tagNone,
        OptionUnit (fun () -> SetOptimizeOff tcConfigB),
        Some(DeprecatedCommandLineOptionSuggestAlternative("-Ooff", "--optimize-", rangeCmdArgs)), None)


    CompilerOption
       ("keycontainer", tagString,
        OptionString(fun s ->
            if FSharpEnvironment.isRunningOnCoreClr then error(Error(FSComp.SR.containerSigningUnsupportedOnThisPlatform(), rangeCmdArgs))
            else tcConfigB.container <- Some s),
            if FSharpEnvironment.isRunningOnCoreClr then None
            else Some(DeprecatedCommandLineOptionSuggestAlternative("--keycontainer", "--keyfile", rangeCmdArgs))
        ,None)

    mlKeywordsFlag
    gnuStyleErrorsFlag tcConfigB ]


// OptionBlock: Miscellaneous options
//-----------------------------------

let DisplayBannerText tcConfigB =
    if tcConfigB.showBanner then (
        printfn "%s" tcConfigB.productNameForBannerText
        printfn "%s" (FSComp.SR.optsCopyright())
    )

/// FSC only help. (FSI has it's own help function).
let displayHelpFsc tcConfigB (blocks:CompilerOptionBlock list) =
    DisplayBannerText tcConfigB
    PrintCompilerOptionBlocks blocks
    exit 0

let displayVersion tcConfigB =
    printfn "%s" tcConfigB.productNameForBannerText
    exit 0

let miscFlagsBoth tcConfigB =
    [   CompilerOption("nologo", tagNone, OptionUnit (fun () -> tcConfigB.showBanner <- false), None, Some (FSComp.SR.optsNologo()))
        CompilerOption("version", tagNone, OptionUnit (fun () -> displayVersion tcConfigB), None, Some (FSComp.SR.optsVersion()))
    ]

let miscFlagsFsc tcConfigB =
    miscFlagsBoth tcConfigB @
    [   CompilerOption("help", tagNone, OptionHelp (fun blocks -> displayHelpFsc tcConfigB blocks), None, Some (FSComp.SR.optsHelp()))
        CompilerOption("@<file>", tagNone, OptionUnit ignore, None, Some (FSComp.SR.optsResponseFile()))
    ]
let miscFlagsFsi tcConfigB = miscFlagsBoth tcConfigB


// OptionBlock: Abbreviations of existing options
//-----------------------------------------------

let abbreviatedFlagsBoth tcConfigB =
    [
        CompilerOption("d", tagString, OptionString (defineSymbol tcConfigB), None, Some(FSComp.SR.optsShortFormOf("--define")))
        CompilerOption("O", tagNone, OptionSwitch (SetOptimizeSwitch tcConfigB), None, Some(FSComp.SR.optsShortFormOf("--optimize[+|-]")))
        CompilerOption("g", tagNone, OptionSwitch (SetDebugSwitch tcConfigB None), None, Some(FSComp.SR.optsShortFormOf("--debug")))
        CompilerOption("i", tagString, OptionUnit (fun () -> tcConfigB.printSignature <- true), None, Some(FSComp.SR.optsShortFormOf("--sig")))
        CompilerOption("r", tagFile, OptionString (fun s -> tcConfigB.AddReferencedAssemblyByPath (rangeStartup, s)),
            None, Some(FSComp.SR.optsShortFormOf("--reference")))
        CompilerOption("I", tagDirList, OptionStringList (fun s -> tcConfigB.AddIncludePath (rangeStartup, s, tcConfigB.implicitIncludeDir)),
            None, Some (FSComp.SR.optsShortFormOf("--lib")))
    ]

let abbreviatedFlagsFsi tcConfigB = abbreviatedFlagsBoth tcConfigB

let abbreviatedFlagsFsc tcConfigB =
    abbreviatedFlagsBoth tcConfigB @
    [   // FSC only abbreviated options
        CompilerOption
            ("o", tagString,
             OptionString (setOutFileName tcConfigB), None,
             Some(FSComp.SR.optsShortFormOf("--out")))

        CompilerOption
            ("a", tagString,
             OptionUnit (fun () -> tcConfigB.target <- CompilerTarget.Dll), None,
             Some(FSComp.SR.optsShortFormOf("--target library")))

        // FSC help abbreviations. FSI has it's own help options...
        CompilerOption
           ("?", tagNone,
            OptionHelp (fun blocks -> displayHelpFsc tcConfigB blocks), None,
            Some(FSComp.SR.optsShortFormOf("--help")))

        CompilerOption
            ("help", tagNone,
             OptionHelp (fun blocks -> displayHelpFsc tcConfigB blocks), None,
             Some(FSComp.SR.optsShortFormOf("--help")))

        CompilerOption
            ("full-help", tagNone,
             OptionHelp (fun blocks -> displayHelpFsc tcConfigB blocks), None,
             Some(FSComp.SR.optsShortFormOf("--help")))
    ]

let GetAbbrevFlagSet tcConfigB isFsc =
    let mutable argList: string list = []
    for c in ((if isFsc then abbreviatedFlagsFsc else abbreviatedFlagsFsi) tcConfigB) do
        match c with
        | CompilerOption(arg, _, OptionString _, _, _)
        | CompilerOption(arg, _, OptionStringList _, _, _) -> argList <- argList @ ["-"+arg;"/"+arg]
        | _ -> ()
    Set.ofList argList

// check for abbreviated options that accept spaces instead of colons, and replace the spaces
// with colons when necessary
let PostProcessCompilerArgs (abbrevArgs: string Set) (args: string []) =
    let mutable i = 0
    let mutable idx = 0
    let len = args.Length
    let mutable arga: string[] = Array.create len ""

    while i < len do
        if not(abbrevArgs.Contains(args.[i])) || i = (len - 1)  then
            arga.[idx] <- args.[i]
            i <- i+1
        else
            arga.[idx] <- args.[i] + ":" + args.[i+1]
            i <- i + 2
        idx <- idx + 1
    Array.toList arga.[0 .. (idx - 1)]

// OptionBlock: QA options
//------------------------

let testingAndQAFlags _tcConfigB =
  [
    CompilerOption
       ("dumpAllCommandLineOptions", tagNone,
        OptionHelp(fun blocks -> DumpCompilerOptionBlocks blocks),
        None, None) // "Command line options")
  ]


// Core compiler options, overview
//--------------------------------

(*  The "core" compiler options are "the ones defined here".
    Currently, fsi.exe has some additional options, defined in fsi.fs.

    The compiler options are put into blocks, named as <block>Flags.
    Some block options differ between fsc and fsi, in this case they split as <block>FlagsFsc and <block>FlagsFsi.

    The "service.fs" (language service) flags are the same as the fsc flags (except help options are removed).
    REVIEW: is this correct? what about fsx files in VS and fsi options?

    Block                      | notes
    ---------------------------|--------------------
    outputFileFlags            |
    inputFileFlags             |
    resourcesFlags             |
    codeGenerationFlags        |
    errorsAndWarningsFlags     |
    languageFlags              |
    miscFlags                  |
    advancedFlags              |
    internalFlags              |
    abbreviatedFlags           |
    deprecatedFlags            | REVIEW: some of these may have been valid for fsi.exe?
    fsiSpecificFlags           | These are defined later, in fsi.fs
    ---------------------------|--------------------
*)

// Core compiler options exported to fsc.fs, service.fs and fsi.fs
//----------------------------------------------------------------

/// The core/common options used by fsc.exe. [not currently extended by fsc.fs].
let GetCoreFscCompilerOptions (tcConfigB: TcConfigBuilder) =
  [ PublicOptions(FSComp.SR.optsHelpBannerOutputFiles(), outputFileFlagsFsc        tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerInputFiles(), inputFileFlagsFsc         tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerResources(), resourcesFlagsFsc         tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerCodeGen(), codeGenerationFlags false tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerErrsAndWarns(), errorsAndWarningsFlags    tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerLanguage(), languageFlags             tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerMisc(), miscFlagsFsc              tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerAdvanced(), advancedFlagsFsc tcConfigB)
    PrivateOptions(List.concat              [ internalFlags           tcConfigB
                                              abbreviatedFlagsFsc     tcConfigB
                                              deprecatedFlagsFsc      tcConfigB
                                              testingAndQAFlags       tcConfigB])
  ]

/// The core/common options used by the F# VS Language Service.
/// Filter out OptionHelp which does printing then exit. This is not wanted in the context of VS!!
let GetCoreServiceCompilerOptions (tcConfigB:TcConfigBuilder) =
  let isHelpOption = function CompilerOption(_, _, OptionHelp _, _, _) -> true | _ -> false
  List.map (FilterCompilerOptionBlock (isHelpOption >> not)) (GetCoreFscCompilerOptions tcConfigB)

/// The core/common options used by fsi.exe. [note, some additional options are added in fsi.fs].
let GetCoreFsiCompilerOptions (tcConfigB: TcConfigBuilder) =
  [ PublicOptions(FSComp.SR.optsHelpBannerOutputFiles(), outputFileFlagsFsi       tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerInputFiles(), inputFileFlagsFsi        tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerResources(), resourcesFlagsFsi        tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerCodeGen(), codeGenerationFlags true tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerErrsAndWarns(), errorsAndWarningsFlags   tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerLanguage(), languageFlags            tcConfigB)
    // Note: no HTML block for fsi.exe
    PublicOptions(FSComp.SR.optsHelpBannerMisc(), miscFlagsFsi            tcConfigB)
    PublicOptions(FSComp.SR.optsHelpBannerAdvanced(), advancedFlagsFsi        tcConfigB)
    PrivateOptions(List.concat              [ internalFlags           tcConfigB
                                              abbreviatedFlagsFsi     tcConfigB
                                              deprecatedFlagsFsi      tcConfigB
                                              testingAndQAFlags       tcConfigB])
  ]

let ApplyCommandLineArgs(tcConfigB: TcConfigBuilder, sourceFiles: string list, argv) =
    try
        let sourceFilesAcc = ResizeArray sourceFiles
        let collect name = if not (FileSystemUtils.isDll name) then sourceFilesAcc.Add name
        ParseCompilerOptions(collect, GetCoreServiceCompilerOptions tcConfigB, argv)
        ResizeArray.toList sourceFilesAcc
    with e ->
        errorRecovery e range0
        sourceFiles


//----------------------------------------------------------------------------
// PrintWholeAssemblyImplementation
//----------------------------------------------------------------------------

let mutable showTermFileCount = 0
let PrintWholeAssemblyImplementation g (tcConfig:TcConfig) outfile header expr =
    if tcConfig.showTerms then
        if tcConfig.writeTermsToFiles then
            let filename = outfile + ".terms"
            use f = FileSystem.OpenFileForWriteShim(filename + "-" + string showTermFileCount + "-" + header).GetWriter()
            showTermFileCount <- showTermFileCount + 1
            LayoutRender.outL f (Display.squashTo 192 (DebugPrint.implFilesL g expr))
        else
            dprintf "\n------------------\nshowTerm: %s:\n" header
            LayoutRender.outL stderr (Display.squashTo 192 (DebugPrint.implFilesL g expr))
            dprintf "\n------------------\n"

//----------------------------------------------------------------------------
// ReportTime
//----------------------------------------------------------------------------

let mutable tPrev = None
let mutable nPrev = None
let ReportTime (tcConfig:TcConfig) descr =

    match nPrev with
    | None -> ()
    | Some prevDescr ->
        if tcConfig.pause then
            dprintf "[done '%s', entering '%s'] press <enter> to continue... " prevDescr descr
            System.Console.ReadLine() |> ignore
        // Intentionally putting this right after the pause so a debugger can be attached.
        match tcConfig.simulateException with
        | Some("fsc-oom") -> raise(System.OutOfMemoryException())
        | Some("fsc-an") -> raise(System.ArgumentNullException("simulated"))
        | Some("fsc-invop") -> raise(System.InvalidOperationException())
        | Some("fsc-av") -> raise(System.AccessViolationException())
        | Some("fsc-aor") -> raise(System.ArgumentOutOfRangeException())
        | Some("fsc-dv0") -> raise(System.DivideByZeroException())
        | Some("fsc-nfn") -> raise(System.NotFiniteNumberException())
        | Some("fsc-oe") -> raise(System.OverflowException())
        | Some("fsc-atmm") -> raise(System.ArrayTypeMismatchException())
        | Some("fsc-bif") -> raise(System.BadImageFormatException())
        | Some("fsc-knf") -> raise(System.Collections.Generic.KeyNotFoundException())
        | Some("fsc-ior") -> raise(System.IndexOutOfRangeException())
        | Some("fsc-ic") -> raise(System.InvalidCastException())
        | Some("fsc-ip") -> raise(System.InvalidProgramException())
        | Some("fsc-ma") -> raise(System.MemberAccessException())
        | Some("fsc-ni") -> raise(System.NotImplementedException())
        | Some("fsc-nr") -> raise(System.NullReferenceException())
        | Some("fsc-oc") -> raise(System.OperationCanceledException())
        | Some("fsc-fail") -> failwith "simulated"
        | _ -> ()




    if (tcConfig.showTimes || verbose) then
        // Note that timing calls are relatively expensive on the startup path so we don't
        // make this call unless showTimes has been turned on.
        let timeNow = System.Diagnostics.Process.GetCurrentProcess().UserProcessorTime.TotalSeconds
        let maxGen = System.GC.MaxGeneration
        let gcNow = [| for i in 0 .. maxGen -> System.GC.CollectionCount i |]
        let ptime = System.Diagnostics.Process.GetCurrentProcess()
        let wsNow = ptime.WorkingSet64/1000000L

        match tPrev, nPrev with
        | Some (timePrev, gcPrev:int []), Some prevDescr ->
            let spanGC = [| for i in 0 .. maxGen -> System.GC.CollectionCount i - gcPrev.[i] |]
            dprintf "TIME: %4.1f Delta: %4.1f Mem: %3d"
                timeNow (timeNow - timePrev)
                wsNow
            dprintf " G0: %3d G1: %2d G2: %2d [%s]\n"
                spanGC.[Operators.min 0 maxGen] spanGC.[Operators.min 1 maxGen] spanGC.[Operators.min 2 maxGen]
                prevDescr

        | _ -> ()
        tPrev <- Some (timeNow, gcNow)

    nPrev <- Some descr

let ignoreFailureOnMono1_1_16 f = try f() with _ -> ()

let foreBackColor () =
    try
        let c = Console.ForegroundColor // may fail, perhaps on Mac, and maybe ForegroundColor is Black
        let b = Console.BackgroundColor // may fail, perhaps on Mac, and maybe BackgroundColor is White
        Some (c, b)
    with
        e -> None

let DoWithColor newColor f =
    match enableConsoleColoring, foreBackColor() with
    | false, _
    | true, None ->
        // could not get console colours, so no attempt to change colours, can not set them back
        f()
    | true, Some (c, _) ->
        try
            ignoreFailureOnMono1_1_16 (fun () -> Console.ForegroundColor <- newColor)
            f()
        finally
            ignoreFailureOnMono1_1_16 (fun () -> Console.ForegroundColor <- c)

let DoWithDiagnosticColor severity f =
    match foreBackColor() with
    | None -> f()
    | Some (_, backColor) ->
        let infoColor = if backColor = ConsoleColor.White then ConsoleColor.Blue else ConsoleColor.Green
        let warnColor = if backColor = ConsoleColor.White then ConsoleColor.DarkBlue else ConsoleColor.Cyan
        let errorColor = ConsoleColor.Red
        let color =
            match severity with
            | FSharpDiagnosticSeverity.Error -> errorColor
            | FSharpDiagnosticSeverity.Warning -> warnColor
            | _ -> infoColor
        DoWithColor color f
