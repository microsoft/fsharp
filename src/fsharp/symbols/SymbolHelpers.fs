// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Open up the compiler as an incremental service for parsing, 
// type checking and intellisense-like environment-reporting.
//--------------------------------------------------------------------------

namespace FSharp.Compiler.Diagnostics

open System

open Internal.Utilities.Library  
open Internal.Utilities.Library.Extras

open FSharp.Core.Printf
open FSharp.Compiler 
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Xml
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range

type FSharpDiagnostic(m: range, severity: FSharpDiagnosticSeverity, message: string, subcategory: string, errorNum: int, numberPrefix: string) =
    member _.Range = m

    member _.Severity = severity

    member _.Message = message

    member _.Subcategory = subcategory

    member _.ErrorNumber = errorNum

    member _.ErrorNumberPrefix = numberPrefix

    member _.ErrorNumberText = numberPrefix + errorNum.ToString("0000")

    member _.Start = m.Start

    member _.End = m.End

    member _.StartLine = m.Start.Line

    member _.EndLine = m.End.Line
    
    member _.StartColumn = m.Start.Column

    member _.EndColumn = m.End.Column

    member _.FileName = m.FileName

    member _.WithStart newStart =
        let m = mkFileIndexRange m.FileIndex newStart m.End
        FSharpDiagnostic(m, severity, message, subcategory, errorNum, numberPrefix)

    member _.WithEnd newEnd =
        let m = mkFileIndexRange m.FileIndex m.Start newEnd
        FSharpDiagnostic(m, severity, message, subcategory, errorNum, numberPrefix)

    override _.ToString() =
        let fileName = m.FileName
        let s = m.Start
        let e = m.End
        let severity = if severity=FSharpDiagnosticSeverity.Warning then "warning" else "error"
        sprintf "%s (%d,%d)-(%d,%d) %s %s %s" fileName s.Line (s.Column + 1) e.Line (e.Column + 1) subcategory severity message

    /// Decompose a warning or error into parts: position, severity, message, error number
    static member CreateFromException(exn, severity, fallbackRange: range, suggestNames: bool) =
        let m = match GetRangeOfDiagnostic exn with Some m -> m | None -> fallbackRange 
        let msg = bufs (fun buf -> OutputPhasedDiagnostic buf exn false suggestNames)
        let errorNum = GetDiagnosticNumber exn
        FSharpDiagnostic(m, severity, msg, exn.Subcategory(), errorNum, "FS")

    /// Decompose a warning or error into parts: position, severity, message, error number
    static member CreateFromExceptionAndAdjustEof(exn, severity, fallbackRange: range, (linesCount: int, lastLength: int), suggestNames: bool) =
        let r = FSharpDiagnostic.CreateFromException(exn, severity, fallbackRange, suggestNames)

        // Adjust to make sure that errors reported at Eof are shown at the linesCount
        let startline, schange = min (Line.toZ r.Range.StartLine, false) (linesCount, true)
        let endline, echange = min (Line.toZ r.Range.EndLine, false)  (linesCount, true)
        
        if not (schange || echange) then r
        else
            let r = if schange then r.WithStart(mkPos startline lastLength) else r
            if echange then r.WithEnd(mkPos endline (1 + lastLength)) else r

    static member NewlineifyErrorString(message) = ErrorLogger.NewlineifyErrorString(message)

    static member NormalizeErrorString(text) = ErrorLogger.NormalizeErrorString(text)
    
    static member Create(severity: FSharpDiagnosticSeverity, message: string, number: int, range: range, ?numberPrefix: string, ?subcategory: string) =
        let subcategory = defaultArg subcategory BuildPhaseSubcategory.TypeCheck
        let numberPrefix = defaultArg numberPrefix "FS"
        FSharpDiagnostic(range, severity, message, subcategory, number, numberPrefix)

/// Use to reset error and warning handlers            
[<Sealed>]
type ErrorScope()  = 
    let mutable errors = [] 
    let mutable firstError = None
    let unwindBP = PushThreadBuildPhaseUntilUnwind BuildPhase.TypeCheck
    let unwindEL =        
        PushErrorLoggerPhaseUntilUnwind (fun _oldLogger -> 
            { new ErrorLogger("ErrorScope") with 
                member x.DiagnosticSink(exn, severity) = 
                      let err = FSharpDiagnostic.CreateFromException(exn, severity, range.Zero, false)
                      errors <- err :: errors
                      if severity = FSharpDiagnosticSeverity.Error && firstError.IsNone then 
                          firstError <- Some err.Message
                member x.ErrorCount = errors.Length })
        
    member x.Errors = errors |> List.filter (fun error -> error.Severity = FSharpDiagnosticSeverity.Error)

    member x.Warnings = errors |> List.filter (fun error -> error.Severity = FSharpDiagnosticSeverity.Warning)

    member x.Diagnostics = errors

    member x.TryGetFirstErrorText() =
        match x.Errors with 
        | error :: _ -> Some error.Message
        | [] -> None
    
    interface IDisposable with
          member d.Dispose() = 
              unwindEL.Dispose() (* unwind pushes when ErrorScope disposes *)
              unwindBP.Dispose()

    member x.FirstError with get() = firstError and set v = firstError <- v
    
    /// Used at entry points to FSharp.Compiler.Service (service.fsi) which manipulate symbols and
    /// perform other operations which might expose us to either bona-fide F# error messages such 
    /// "missing assembly" (for incomplete assembly reference sets), or, if there is a compiler bug, 
    /// may hit internal compiler failures.
    ///
    /// In some calling cases, we get a chance to report the error as part of user text. For example
    /// if there is a "missing assembly" error while formatting the text of the description of an
    /// autocomplete, then the error message is shown in replacement of the text (rather than crashing Visual
    /// Studio, or swallowing the exception completely)
    static member Protect<'a> (m: range) (f: unit->'a) (err: string->'a): 'a = 
        use errorScope = new ErrorScope()
        let res = 
            try 
                Some (f())
            with e -> 
                // Here we only call errorRecovery to save the error message for later use by TryGetFirstErrorText.
                try 
                    errorRecovery e m
                with _ -> 
                    // If error recovery fails, then we have an internal compiler error. In this case, we show the whole stack
                    // in the extra message, should the extra message be used.
                    errorScope.FirstError <- Some (e.ToString())
                None
        match res with 
        | Some res -> res
        | None -> 
            match errorScope.TryGetFirstErrorText() with 
            | Some text -> err text
            | None -> err ""

/// An error logger that capture errors, filtering them according to warning levels etc.
type internal CompilationErrorLogger (debugName: string, options: FSharpDiagnosticOptions) = 
    inherit ErrorLogger("CompilationErrorLogger("+debugName+")")
            
    let mutable errorCount = 0
    let diagnostics = new ResizeArray<_>()

    override x.DiagnosticSink(exn, severity) = 
        if severity = FSharpDiagnosticSeverity.Error || ReportWarningAsError options exn then
            diagnostics.Add(exn, FSharpDiagnosticSeverity.Error)
            errorCount <- errorCount + 1
        elif ReportWarning options exn then
            diagnostics.Add(exn, FSharpDiagnosticSeverity.Warning)

    override x.ErrorCount = errorCount

    member x.GetDiagnostics() = diagnostics.ToArray()

module DiagnosticHelpers =                            

    let ReportDiagnostic (options: FSharpDiagnosticOptions, allErrors, mainInputFileName, fileInfo, (exn, severity), suggestNames) = 
        [ let severity = 
               if (severity = FSharpDiagnosticSeverity.Error) then severity 
               elif ReportWarningAsError options exn then FSharpDiagnosticSeverity.Error
               else severity
          if (severity = FSharpDiagnosticSeverity.Error || ReportWarning options exn) then 
            let oneError exn =
                [ // We use the first line of the file as a fallbackRange for reporting unexpected errors.
                  // Not ideal, but it's hard to see what else to do.
                  let fallbackRange = rangeN mainInputFileName 1
                  let ei = FSharpDiagnostic.CreateFromExceptionAndAdjustEof (exn, severity, fallbackRange, fileInfo, suggestNames)
                  let fileName = ei.Range.FileName
                  if allErrors || fileName = mainInputFileName || fileName = TcGlobals.DummyFileNameForRangesWithoutASpecificLocation then
                      yield ei ]

            let mainError, relatedErrors = SplitRelatedDiagnostics exn 
            yield! oneError mainError
            for e in relatedErrors do 
                yield! oneError e ]

    let CreateDiagnostics (options, allErrors, mainInputFileName, errors, suggestNames) = 
        let fileInfo = (Int32.MaxValue, Int32.MaxValue)
        [| for (exn, severity) in errors do 
              yield! ReportDiagnostic (options, allErrors, mainInputFileName, fileInfo, (exn, severity), suggestNames) |]
                            

namespace FSharp.Compiler.Symbols

open System
open System.IO

open Internal.Utilities.Library  
open Internal.Utilities.Library.Extras
open FSharp.Core.Printf
open FSharp.Compiler 
open FSharp.Compiler.AbstractIL.Diagnostics 
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.IO 
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text.Layout
open FSharp.Compiler.Text.TaggedText
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TcGlobals 

/// Describe a comment as either a block of text or a file+signature reference into an intellidoc file.
[<RequireQualifiedAccess>]
type FSharpXmlDoc =
    | None
    | FromXmlText of XmlDoc
    | FromXmlFile of dllName: string * xmlSig: string

module EnvMisc2 =
    let maxMembers = GetEnvInteger "FCS_MaxMembersInQuickInfo" 10

[<AutoOpen>]
module internal SymbolHelpers = 

    let rangeOfValRef preferFlag (vref: ValRef) =
        match preferFlag with 
        | None -> vref.Range 
        | Some false -> vref.DefinitionRange 
        | Some true -> vref.SigRange

    let rangeOfEntityRef preferFlag (eref: EntityRef) =
        match preferFlag with 
        | None -> eref.Range 
        | Some false -> eref.DefinitionRange 
        | Some true -> eref.SigRange
   
    let rangeOfPropInfo preferFlag (pinfo: PropInfo) =
        match pinfo with
#if !NO_EXTENSIONTYPING 
        |   ProvidedProp(_, pi, _) -> Construct.ComputeDefinitionLocationOfProvidedItem pi
#endif
        |   _ -> pinfo.ArbitraryValRef |> Option.map (rangeOfValRef preferFlag)

    let rangeOfMethInfo (g: TcGlobals) preferFlag (minfo: MethInfo) = 
        match minfo with
#if !NO_EXTENSIONTYPING 
        |   ProvidedMeth(_, mi, _, _) -> Construct.ComputeDefinitionLocationOfProvidedItem mi
#endif
        |   DefaultStructCtor(_, AppTy g (tcref, _)) -> Some(rangeOfEntityRef preferFlag tcref)
        |   _ -> minfo.ArbitraryValRef |> Option.map (rangeOfValRef preferFlag)

    let rangeOfEventInfo preferFlag (einfo: EventInfo) = 
        match einfo with
#if !NO_EXTENSIONTYPING 
        | ProvidedEvent (_, ei, _) -> Construct.ComputeDefinitionLocationOfProvidedItem ei
#endif
        | _ -> einfo.ArbitraryValRef |> Option.map (rangeOfValRef preferFlag)
      
    let rangeOfUnionCaseInfo preferFlag (ucinfo: UnionCaseInfo) =      
        match preferFlag with 
        | None -> ucinfo.UnionCase.Range 
        | Some false -> ucinfo.UnionCase.DefinitionRange 
        | Some true -> ucinfo.UnionCase.SigRange

    let rangeOfRecdField preferFlag (rField: RecdField) =
        match preferFlag with
        | None -> rField.Range
        | Some false -> rField.DefinitionRange
        | Some true -> rField.SigRange

    let rangeOfRecdFieldInfo preferFlag (rfinfo: RecdFieldInfo) =
        rangeOfRecdField preferFlag rfinfo.RecdField

    let rec rangeOfItem (g: TcGlobals) preferFlag d = 
        match d with
        | Item.Value vref  | Item.CustomBuilder (_, vref) -> Some (rangeOfValRef preferFlag vref)
        | Item.UnionCase(ucinfo, _)     -> Some (rangeOfUnionCaseInfo preferFlag ucinfo)
        | Item.ActivePatternCase apref -> Some (rangeOfValRef preferFlag apref.ActivePatternVal)
        | Item.ExnCase tcref           -> Some tcref.Range
        | Item.AnonRecdField (_,_,_,m) -> Some m
        | Item.RecdField rfinfo        -> Some (rangeOfRecdFieldInfo preferFlag rfinfo)
        | Item.UnionCaseField (UnionCaseInfo (_, ucref), fieldIndex) -> Some (rangeOfRecdField preferFlag (ucref.FieldByIndex(fieldIndex)))
        | Item.Event einfo             -> rangeOfEventInfo preferFlag einfo
        | Item.ILField _               -> None
        | Item.Property(_, pinfos)      -> rangeOfPropInfo preferFlag pinfos.Head 
        | Item.Types(_, tys)     -> tys |> List.tryPick (tryNiceEntityRefOfTyOption >> Option.map (rangeOfEntityRef preferFlag))
        | Item.CustomOperation (_, _, Some minfo)  -> rangeOfMethInfo g preferFlag minfo
        | Item.TypeVar (_, tp)  -> Some tp.Range
        | Item.ModuleOrNamespaces modrefs -> modrefs |> List.tryPick (rangeOfEntityRef preferFlag >> Some)
        | Item.MethodGroup(_, minfos, _) 
        | Item.CtorGroup(_, minfos) -> minfos |> List.tryPick (rangeOfMethInfo g preferFlag)
        | Item.ActivePatternResult(APInfo _, _, _, m) -> Some m
        | Item.SetterArg (_, item) -> rangeOfItem g preferFlag item
        | Item.ArgName (id, _, _) -> Some id.idRange
        | Item.CustomOperation (_, _, implOpt) -> implOpt |> Option.bind (rangeOfMethInfo g preferFlag)
        | Item.ImplicitOp (_, {contents = Some(TraitConstraintSln.FSMethSln(_, vref, _, _))}) -> Some vref.Range
        | Item.ImplicitOp _ -> None
        | Item.UnqualifiedType tcrefs -> tcrefs |> List.tryPick (rangeOfEntityRef preferFlag >> Some)
        | Item.DelegateCtor ty 
        | Item.FakeInterfaceCtor ty -> ty |> tryNiceEntityRefOfTyOption |> Option.map (rangeOfEntityRef preferFlag)
        | Item.NewDef _ -> None

    // Provided type definitions do not have a useful F# CCU for the purposes of goto-definition.
    let computeCcuOfTyconRef (tcref: TyconRef) = 
#if !NO_EXTENSIONTYPING
        if tcref.IsProvided then None else 
#endif
        ccuOfTyconRef tcref

    let ccuOfMethInfo (g: TcGlobals) (minfo: MethInfo) = 
        match minfo with
        | DefaultStructCtor(_, AppTy g (tcref, _)) -> computeCcuOfTyconRef tcref
        | _ -> 
            minfo.ArbitraryValRef 
            |> Option.bind ccuOfValRef 
            |> Option.orElseWith (fun () -> minfo.DeclaringTyconRef |> computeCcuOfTyconRef)

    let rec ccuOfItem (g: TcGlobals) d = 
        match d with
        | Item.Value vref | Item.CustomBuilder (_, vref) -> ccuOfValRef vref 
        | Item.UnionCase(ucinfo, _)             -> computeCcuOfTyconRef ucinfo.TyconRef
        | Item.ActivePatternCase apref         -> ccuOfValRef apref.ActivePatternVal
        | Item.ExnCase tcref                   -> computeCcuOfTyconRef tcref
        | Item.RecdField rfinfo                -> computeCcuOfTyconRef rfinfo.RecdFieldRef.TyconRef
        | Item.UnionCaseField (ucinfo, _)      -> computeCcuOfTyconRef ucinfo.TyconRef
        | Item.Event einfo                     -> einfo.DeclaringTyconRef |> computeCcuOfTyconRef
        | Item.ILField finfo                   -> finfo.DeclaringTyconRef |> computeCcuOfTyconRef
        | Item.Property(_, pinfos)              -> 
            pinfos |> List.tryPick (fun pinfo -> 
                pinfo.ArbitraryValRef 
                |> Option.bind ccuOfValRef
                |> Option.orElseWith (fun () -> pinfo.DeclaringTyconRef |> computeCcuOfTyconRef))

        | Item.ArgName (_, _, Some (ArgumentContainer.Method minfo))  -> ccuOfMethInfo g minfo

        | Item.MethodGroup(_, minfos, _)
        | Item.CtorGroup(_, minfos) -> minfos |> List.tryPick (ccuOfMethInfo g)
        | Item.CustomOperation (_, _, Some minfo)       -> ccuOfMethInfo g minfo

        | Item.Types(_, tys)             -> tys |> List.tryPick (tryNiceEntityRefOfTyOption >> Option.bind computeCcuOfTyconRef)

        | Item.ArgName (_, _, Some (ArgumentContainer.Type eref)) -> computeCcuOfTyconRef eref

        | Item.ModuleOrNamespaces erefs 
        | Item.UnqualifiedType erefs -> erefs |> List.tryPick computeCcuOfTyconRef 

        | Item.SetterArg (_, item) -> ccuOfItem g item
        | Item.AnonRecdField (info, _, _, _) -> Some info.Assembly
        | Item.TypeVar _  -> None
        | _ -> None

    /// Work out the source file for an item and fix it up relative to the CCU if it is relative.
    let fileNameOfItem (g: TcGlobals) qualProjectDir (m: range) h =
        let file = m.FileName 
        if verbose then dprintf "file stored in metadata is '%s'\n" file
        if not (FileSystem.IsPathRootedShim file) then 
            match ccuOfItem g h with 
            | Some ccu -> 
                Path.Combine(ccu.SourceCodeDirectory, file)
            | None -> 
                match qualProjectDir with 
                | None     -> file
                | Some dir -> Path.Combine(dir, file)
         else file

    /// Cut long filenames to make them visually appealing 
    let cutFileName s = if String.length s > 40 then String.sub s 0 10 + "..."+String.sub s (String.length s - 27) 27 else s

    let libFileOfEntityRef x =
        match x with
        | ERefLocal _ -> None
        | ERefNonLocal nlref -> nlref.Ccu.FileName      

    let ParamNameAndTypesOfUnaryCustomOperation g minfo = 
        match minfo with 
        | FSMeth(_, _, vref, _) -> 
            let argInfos = ArgInfosOfMember g vref |> List.concat 
            // Drop the first 'seq<T>' argument representing the computation space
            let argInfos = if argInfos.IsEmpty then [] else argInfos.Tail
            [ for (ty, argInfo) in argInfos do
                  let isPP = HasFSharpAttribute g g.attrib_ProjectionParameterAttribute argInfo.Attribs
                  // Strip the tuple space type of the type of projection parameters
                  let ty = if isPP && isFunTy g ty then rangeOfFunTy g ty else ty
                  yield ParamNameAndType(argInfo.Name, ty) ]
        | _ -> []

    let mkXmlComment thing =
        match thing with
        | Some (Some fileName, xmlDocSig) -> FSharpXmlDoc.FromXmlFile(fileName, xmlDocSig)
        | _ -> FSharpXmlDoc.None

    let GetXmlDocFromLoader (infoReader: InfoReader) xmlDoc =
        match xmlDoc with
        | FSharpXmlDoc.None
        | FSharpXmlDoc.FromXmlText _ -> xmlDoc
        | FSharpXmlDoc.FromXmlFile(dllName, xmlSig) ->
            TryFindXmlDocByAssemblyNameAndSig infoReader (Path.GetFileNameWithoutExtension dllName) xmlSig
            |> Option.map FSharpXmlDoc.FromXmlText
            |> Option.defaultValue xmlDoc

    /// This function gets the signature to pass to Visual Studio to use its lookup functions for .NET stuff. 
    let GetXmlDocHelpSigOfItemForLookup (infoReader: InfoReader) m d = 
        let g = infoReader.g
        match d with
        | Item.ActivePatternCase (APElemRef(_, vref, _, _))        
        | Item.Value vref | Item.CustomBuilder (_, vref) -> 
            mkXmlComment (GetXmlDocSigOfValRef g vref)
        | Item.UnionCase  (ucinfo, _) -> mkXmlComment (GetXmlDocSigOfUnionCaseRef ucinfo.UnionCaseRef)
        | Item.ExnCase tcref -> mkXmlComment (GetXmlDocSigOfEntityRef infoReader m tcref)
        | Item.RecdField rfinfo -> mkXmlComment (GetXmlDocSigOfRecdFieldRef rfinfo.RecdFieldRef)
        | Item.NewDef _ -> FSharpXmlDoc.None
        | Item.ILField finfo -> mkXmlComment (GetXmlDocSigOfILFieldInfo infoReader m finfo)
        | Item.Types(_, ((TType_app(tcref, _)) :: _)) ->  mkXmlComment (GetXmlDocSigOfEntityRef infoReader m tcref)
        | Item.CustomOperation (_, _, Some minfo) -> mkXmlComment (GetXmlDocSigOfMethInfo infoReader  m minfo)
        | Item.TypeVar _  -> FSharpXmlDoc.None
        | Item.ModuleOrNamespaces(modref :: _) -> mkXmlComment (GetXmlDocSigOfEntityRef infoReader m modref)

        | Item.Property(_, (pinfo :: _)) -> mkXmlComment (GetXmlDocSigOfProp infoReader m pinfo)
        | Item.Event einfo -> mkXmlComment (GetXmlDocSigOfEvent infoReader m einfo)

        | Item.MethodGroup(_, minfo :: _, _) -> mkXmlComment (GetXmlDocSigOfMethInfo infoReader  m minfo)
        | Item.CtorGroup(_, minfo :: _) -> mkXmlComment (GetXmlDocSigOfMethInfo infoReader  m minfo)
        | Item.ArgName(_, _, Some argContainer) -> 
            match argContainer with 
            | ArgumentContainer.Method minfo -> mkXmlComment (GetXmlDocSigOfMethInfo infoReader m minfo)
            | ArgumentContainer.Type tcref -> mkXmlComment (GetXmlDocSigOfEntityRef infoReader m tcref)
        | Item.UnionCaseField (ucinfo, _) -> mkXmlComment (GetXmlDocSigOfUnionCaseRef ucinfo.UnionCaseRef)
        |  _ -> FSharpXmlDoc.None

        |> GetXmlDocFromLoader infoReader

    /// Produce an XmlComment with a signature or raw text, given the F# comment and the item
    let GetXmlCommentForItemAux (xmlDoc: XmlDoc option) (infoReader: InfoReader) m d = 
        match xmlDoc with 
        | Some xmlDoc when not xmlDoc.IsEmpty  -> 
            FSharpXmlDoc.FromXmlText xmlDoc
        | _ -> GetXmlDocHelpSigOfItemForLookup infoReader m d

    let GetXmlCommentForMethInfoItem infoReader m d (minfo: MethInfo) = 
        if minfo.HasDirectXmlComment || minfo.XmlDoc.NonEmpty then
            GetXmlCommentForItemAux (Some minfo.XmlDoc) infoReader m d 
        else
            mkXmlComment (GetXmlDocSigOfMethInfo infoReader m minfo)

    let FormatTyparMapping denv (prettyTyparInst: TyparInst) = 
        [ for (tp, ty) in prettyTyparInst -> 
            wordL (tagTypeParameter ("'" + tp.DisplayName))  ^^ wordL (tagText (FSComp.SR.descriptionWordIs())) ^^ NicePrint.layoutType denv ty  ]

    let (|ItemWhereTypIsPreferred|_|) item = 
        match item with 
        | Item.DelegateCtor ty
        | Item.CtorGroup(_, [DefaultStructCtor(_, ty)])
        | Item.FakeInterfaceCtor ty
        | Item.Types(_, [ty])  -> Some ty
        | _ -> None

    /// Specifies functions for comparing 'Item' objects with respect to the user 
    /// (this means that some values that are not technically equal are treated as equal 
    ///  if this is what we want to show to the user, because we're comparing just the name
    //   for some cases e.g. when using 'fullDisplayTextOfModRef')
    let ItemDisplayPartialEquality g = 
      { new IPartialEqualityComparer<_> with   
          member x.InEqualityRelation item = 
              match item  with 
              | Item.Types(_, [_]) -> true
              | Item.ILField(ILFieldInfo _) -> true
              | Item.RecdField _ -> true
              | Item.SetterArg _ -> true
              | Item.TypeVar _ -> true
              | Item.CustomOperation _ -> true
              | Item.ModuleOrNamespaces(_ :: _) -> true
              | Item.MethodGroup _ -> true
              | Item.Value _ | Item.CustomBuilder _ -> true
              | Item.ActivePatternCase _ -> true
              | Item.DelegateCtor _ -> true
              | Item.UnionCase _ -> true
              | Item.ExnCase _ -> true              
              | Item.Event _ -> true
              | Item.Property _ -> true
              | Item.CtorGroup _ -> true
              | Item.UnqualifiedType _ -> true
              | _ -> false
              
          member x.Equals(item1, item2) = 
            // This may explore assemblies that are not in the reference set.
            // In this case just bail out and assume items are not equal
            protectAssemblyExploration false (fun () -> 
              let equalHeadTypes(ty1, ty2) =
                  match tryTcrefOfAppTy g ty1 with
                  | ValueSome tcref1 ->
                    match tryTcrefOfAppTy g ty2 with
                    | ValueSome tcref2 -> tyconRefEq g tcref1 tcref2
                    | _ -> typeEquiv g ty1 ty2
                  | _ -> typeEquiv g ty1 ty2

              ItemsAreEffectivelyEqual g item1 item2 || 

              // Much of this logic is already covered by 'ItemsAreEffectivelyEqual'
              match item1, item2 with 
              | Item.DelegateCtor ty1, Item.DelegateCtor ty2 -> equalHeadTypes(ty1, ty2)
              | Item.Types(dn1, [ty1]), Item.Types(dn2, [ty2]) -> 
                  // Bug 4403: We need to compare names as well, because 'int' and 'Int32' are physically the same type, but we want to show both
                  dn1 = dn2 && equalHeadTypes(ty1, ty2) 
              
              // Prefer a type to a DefaultStructCtor, a DelegateCtor and a FakeInterfaceCtor 
              | ItemWhereTypIsPreferred ty1, ItemWhereTypIsPreferred ty2 -> equalHeadTypes(ty1, ty2) 

              | Item.ExnCase tcref1, Item.ExnCase tcref2 -> tyconRefEq g tcref1 tcref2
              | Item.ILField(ILFieldInfo(_, fld1)), Item.ILField(ILFieldInfo(_, fld2)) -> 
                  fld1 === fld2 // reference equality on the object identity of the AbstractIL metadata blobs for the fields
              | Item.CustomOperation (_, _, Some minfo1), Item.CustomOperation (_, _, Some minfo2) -> 
                    MethInfo.MethInfosUseIdenticalDefinitions minfo1 minfo2
              | Item.TypeVar (nm1, tp1), Item.TypeVar (nm2, tp2) -> 
                    (nm1 = nm2) && typarRefEq tp1 tp2
              | Item.ModuleOrNamespaces(modref1 :: _), Item.ModuleOrNamespaces(modref2 :: _) -> fullDisplayTextOfModRef modref1 = fullDisplayTextOfModRef modref2
              | Item.SetterArg(id1, _), Item.SetterArg(id2, _) -> Range.equals id1.idRange id2.idRange && id1.idText = id2.idText
              | Item.MethodGroup(_, meths1, _), Item.MethodGroup(_, meths2, _) -> 
                  Seq.zip meths1 meths2 |> Seq.forall (fun (minfo1, minfo2) ->
                    MethInfo.MethInfosUseIdenticalDefinitions minfo1 minfo2)
              | (Item.Value vref1 | Item.CustomBuilder (_, vref1)), (Item.Value vref2 | Item.CustomBuilder (_, vref2)) -> 
                  valRefEq g vref1 vref2
              | Item.ActivePatternCase(APElemRef(_apinfo1, vref1, idx1, _)), Item.ActivePatternCase(APElemRef(_apinfo2, vref2, idx2, _)) ->
                  idx1 = idx2 && valRefEq g vref1 vref2
              | Item.UnionCase(UnionCaseInfo(_, ur1), _), Item.UnionCase(UnionCaseInfo(_, ur2), _) -> 
                  g.unionCaseRefEq ur1 ur2
              | Item.RecdField(RecdFieldInfo(_, RecdFieldRef(tcref1, n1))), Item.RecdField(RecdFieldInfo(_, RecdFieldRef(tcref2, n2))) -> 
                  (tyconRefEq g tcref1 tcref2) && (n1 = n2) // there is no direct function as in the previous case
              | Item.Property(_, pi1s), Item.Property(_, pi2s) -> 
                  (pi1s, pi2s) ||> List.forall2 (fun pi1 pi2 -> PropInfo.PropInfosUseIdenticalDefinitions pi1 pi2)
              | Item.Event evt1, Item.Event evt2 -> 
                  EventInfo.EventInfosUseIdenticalDefinitions evt1 evt2
              | Item.AnonRecdField(anon1, _, i1, _), Item.AnonRecdField(anon2, _, i2, _) ->
                 anonInfoEquiv anon1 anon2 && i1 = i2
              | Item.CtorGroup(_, meths1), Item.CtorGroup(_, meths2) -> 
                  (meths1, meths2)
                  ||> List.forall2 (fun minfo1 minfo2 -> MethInfo.MethInfosUseIdenticalDefinitions minfo1 minfo2)
              | Item.UnqualifiedType tcRefs1, Item.UnqualifiedType tcRefs2 ->
                  (tcRefs1, tcRefs2)
                  ||> List.forall2 (fun tcRef1 tcRef2 -> tyconRefEq g tcRef1 tcRef2)
              | Item.Types(_, [TType.TType_app(tcRef1, _)]), Item.UnqualifiedType([tcRef2]) -> tyconRefEq g tcRef1 tcRef2
              | Item.UnqualifiedType([tcRef1]), Item.Types(_, [TType.TType_app(tcRef2, _)]) -> tyconRefEq g tcRef1 tcRef2
              | _ -> false)
              
          member x.GetHashCode item =
            // This may explore assemblies that are not in the reference set.
            // In this case just bail out and use a random hash code
            protectAssemblyExploration 1027 (fun () -> 
              match item with 
              | ItemWhereTypIsPreferred ty -> 
                  match tryTcrefOfAppTy g ty with
                  | ValueSome tcref -> hash tcref.LogicalName
                  | _ -> 1010
              | Item.ILField(ILFieldInfo(_, fld)) -> 
                  System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode fld // hash on the object identity of the AbstractIL metadata blob for the field
              | Item.TypeVar (nm, _tp) -> hash nm
              | Item.CustomOperation (_, _, Some minfo) -> minfo.ComputeHashCode()
              | Item.CustomOperation (_, _, None) -> 1
              | Item.ModuleOrNamespaces(modref :: _) -> hash (fullDisplayTextOfModRef modref)          
              | Item.SetterArg(id, _) -> hash (id.idRange, id.idText)
              | Item.MethodGroup(_, meths, _) -> meths |> List.fold (fun st a -> st + a.ComputeHashCode()) 0
              | Item.CtorGroup(name, meths) -> name.GetHashCode() + (meths |> List.fold (fun st a -> st + a.ComputeHashCode()) 0)
              | (Item.Value vref | Item.CustomBuilder (_, vref)) -> hash vref.LogicalName
              | Item.ActivePatternCase(APElemRef(_apinfo, vref, idx, _)) -> hash (vref.LogicalName, idx)
              | Item.ExnCase tcref -> hash tcref.LogicalName
              | Item.UnionCase(UnionCaseInfo(_, UnionCaseRef(tcref, n)), _) -> hash(tcref.Stamp, n)
              | Item.RecdField(RecdFieldInfo(_, RecdFieldRef(tcref, n))) -> hash(tcref.Stamp, n)
              | Item.AnonRecdField(anon, _, i, _) -> hash anon.SortedNames.[i]
              | Item.Event evt -> evt.ComputeHashCode()
              | Item.Property(_name, pis) -> hash (pis |> List.map (fun pi -> pi.ComputeHashCode()))
              | Item.UnqualifiedType(tcref :: _) -> hash tcref.LogicalName
              | _ -> failwith "unreachable") }

    let ItemWithTypeDisplayPartialEquality g = 
        let itemComparer = ItemDisplayPartialEquality g
        
        { new IPartialEqualityComparer<Item * _> with
            member x.InEqualityRelation ((item, _)) = itemComparer.InEqualityRelation item
            member x.Equals((item1, _), (item2, _)) = itemComparer.Equals(item1, item2)
            member x.GetHashCode ((item, _)) = itemComparer.GetHashCode item }
    
    /// Remove all duplicate items
    let RemoveDuplicateItems g (items: ItemWithInst list) =     
        if isNil items then items else
        items |> IPartialEqualityComparer.partialDistinctBy (IPartialEqualityComparer.On (fun item -> item.Item) (ItemDisplayPartialEquality g))

    let IsExplicitlySuppressed (g: TcGlobals) (item: Item) = 
        // This may explore assemblies that are not in the reference set.
        // In this case just assume the item is not suppressed.
        protectAssemblyExploration true (fun () -> 
            match item with 
            | Item.Types(it, [ty]) -> 
                match tryTcrefOfAppTy g ty with
                | ValueSome tcr1 ->
                    g.suppressed_types 
                    |> List.exists (fun supp ->
                        let generalizedSupp = generalizedTyconRef supp
                        // check the display name is precisely the one we're suppressing
                        match tryTcrefOfAppTy g generalizedSupp with
                        | ValueSome tcr2 ->
                            it = supp.DisplayName &&
                            // check if they are the same logical type (after removing all abbreviations) 
                            tyconRefEq g tcr1 tcr2
                        | _ -> false) 
                | _ -> false
            | _ -> false)

    /// Filter types that are explicitly suppressed from the IntelliSense (such as uppercase "FSharpList", "Option", etc.)
    let RemoveExplicitlySuppressed (g: TcGlobals) (items: ItemWithInst list) =
        items |> List.filter (fun item -> not (IsExplicitlySuppressed g item.Item))

    let SimplerDisplayEnv denv = 
        { denv with shortConstraints=true
                    showConstraintTyparAnnotations=false
                    abbreviateAdditionalConstraints=false
                    suppressNestedTypes=true
                    maxMembers=Some EnvMisc2.maxMembers }

    let rec FullNameOfItem g item = 
        let denv = DisplayEnv.Empty g
        match item with
        | Item.ImplicitOp(_, { contents = Some(TraitConstraintSln.FSMethSln(_, vref, _, _)) }) 
        | Item.Value vref | Item.CustomBuilder (_, vref) -> fullDisplayTextOfValRef vref
        | Item.UnionCase (ucinfo, _) -> fullDisplayTextOfUnionCaseRef  ucinfo.UnionCaseRef
        | Item.ActivePatternResult(apinfo, _ty, idx, _) -> apinfo.Names.[idx]
        | Item.ActivePatternCase apref -> FullNameOfItem g (Item.Value apref.ActivePatternVal)  + "." + apref.Name 
        | Item.ExnCase ecref -> fullDisplayTextOfExnRef ecref 
        | Item.AnonRecdField(anon, _argTys, i, _) -> anon.SortedNames.[i]
        | Item.RecdField rfinfo -> fullDisplayTextOfRecdFieldRef  rfinfo.RecdFieldRef
        | Item.NewDef id -> id.idText
        | Item.ILField finfo -> bufs (fun os -> NicePrint.outputType denv os finfo.ApparentEnclosingType; bprintf os ".%s" finfo.FieldName)
        | Item.Event einfo -> bufs (fun os -> NicePrint.outputTyconRef denv os einfo.DeclaringTyconRef; bprintf os ".%s" einfo.EventName)
        | Item.Property(_, (pinfo :: _)) -> bufs (fun os -> NicePrint.outputTyconRef denv os pinfo.DeclaringTyconRef; bprintf os ".%s" pinfo.PropertyName)
        | Item.CustomOperation (customOpName, _, _) -> customOpName
        | Item.CtorGroup(_, minfo :: _) -> bufs (fun os -> NicePrint.outputTyconRef denv os minfo.DeclaringTyconRef)
        | Item.MethodGroup(_, _, Some minfo) -> bufs (fun os -> NicePrint.outputTyconRef denv os minfo.DeclaringTyconRef; bprintf os ".%s" minfo.DisplayName)        
        | Item.MethodGroup(_, minfo :: _, _) -> bufs (fun os -> NicePrint.outputTyconRef denv os minfo.DeclaringTyconRef; bprintf os ".%s" minfo.DisplayName)        
        | Item.UnqualifiedType (tcref :: _) -> bufs (fun os -> NicePrint.outputTyconRef denv os tcref)
        | Item.FakeInterfaceCtor ty 
        | Item.DelegateCtor ty 
        | Item.Types(_, ty :: _) -> 
            match tryTcrefOfAppTy g ty with
            | ValueSome tcref -> bufs (fun os -> NicePrint.outputTyconRef denv os tcref)
            | _ -> ""
        | Item.ModuleOrNamespaces((modref :: _) as modrefs) -> 
            let definiteNamespace = modrefs |> List.forall (fun modref -> modref.IsNamespace)
            if definiteNamespace then fullDisplayTextOfModRef modref else modref.DemangledModuleOrNamespaceName
        | Item.TypeVar (id, _) -> id
        | Item.ArgName (id, _, _) -> id.idText
        | Item.SetterArg (_, item) -> FullNameOfItem g item
        | Item.ImplicitOp(id, _) -> id.idText
        | Item.UnionCaseField (UnionCaseInfo (_, ucref), fieldIndex) -> ucref.FieldByIndex(fieldIndex).Name
        // unreachable 
        | Item.UnqualifiedType([]) 
        | Item.Types(_, []) 
        | Item.CtorGroup(_, []) 
        | Item.MethodGroup(_, [], _) 
        | Item.ModuleOrNamespaces []
        | Item.Property(_, []) -> ""

    /// Output a the description of a language item
    let rec GetXmlCommentForItem (infoReader: InfoReader) m item = 
        let g = infoReader.g
        match item with
        | Item.ImplicitOp(_, { contents = Some(TraitConstraintSln.FSMethSln(_, vref, _, _)) }) -> 
            GetXmlCommentForItem infoReader m (Item.Value vref)

        | Item.Value vref | Item.CustomBuilder (_, vref) ->            
            GetXmlCommentForItemAux (if valRefInThisAssembly g.compilingFslib vref || vref.XmlDoc.NonEmpty then Some vref.XmlDoc else None) infoReader m item 

        | Item.UnionCase(ucinfo, _) -> 
            GetXmlCommentForItemAux (if tyconRefUsesLocalXmlDoc g.compilingFslib ucinfo.TyconRef || ucinfo.UnionCase.XmlDoc.NonEmpty then Some ucinfo.UnionCase.XmlDoc else None) infoReader m item 

        | Item.ActivePatternCase apref -> 
            GetXmlCommentForItemAux (Some apref.ActivePatternVal.XmlDoc) infoReader m item 

        | Item.ExnCase ecref -> 
            GetXmlCommentForItemAux (if tyconRefUsesLocalXmlDoc g.compilingFslib ecref || ecref.XmlDoc.NonEmpty then Some ecref.XmlDoc else None) infoReader m item 

        | Item.RecdField rfinfo ->
            let tcref = rfinfo.TyconRef
            let xmldoc =
                if tyconRefUsesLocalXmlDoc g.compilingFslib tcref || tcref.XmlDoc.NonEmpty then
                    if tcref.IsExceptionDecl then
                        Some tcref.XmlDoc
                    else
                        Some rfinfo.RecdField.XmlDoc
                else
                    None
            GetXmlCommentForItemAux xmldoc infoReader m item 

        | Item.Event einfo ->
            GetXmlCommentForItemAux (if einfo.HasDirectXmlComment || einfo.XmlDoc.NonEmpty then Some einfo.XmlDoc else None) infoReader m item 

        | Item.Property(_, pinfos) -> 
            let pinfo = pinfos.Head
            GetXmlCommentForItemAux (if pinfo.HasDirectXmlComment || pinfo.XmlDoc.NonEmpty then Some pinfo.XmlDoc else None) infoReader m item 

        | Item.CustomOperation (_, _, Some minfo) 
        | Item.CtorGroup(_, minfo :: _) 
        | Item.MethodGroup(_, minfo :: _, _) ->
            GetXmlCommentForMethInfoItem infoReader m item minfo

        | Item.Types(_, ((TType_app(tcref, _)) :: _)) -> 
            GetXmlCommentForItemAux (if tyconRefUsesLocalXmlDoc g.compilingFslib tcref  || tcref.XmlDoc.NonEmpty then Some tcref.XmlDoc else None) infoReader m item 

        | Item.ModuleOrNamespaces((modref :: _) as modrefs) -> 
            let definiteNamespace = modrefs |> List.forall (fun modref -> modref.IsNamespace)
            if not definiteNamespace then
                GetXmlCommentForItemAux (if entityRefInThisAssembly g.compilingFslib modref || modref.XmlDoc.NonEmpty  then Some modref.XmlDoc else None) infoReader m item 
            else
                GetXmlCommentForItemAux None infoReader m item

        | Item.ArgName (_, _, argContainer) -> 
            let xmldoc = 
                match argContainer with
                | Some(ArgumentContainer.Method minfo) ->
                    if minfo.HasDirectXmlComment || minfo.XmlDoc.NonEmpty  then Some minfo.XmlDoc else None 
                | Some(ArgumentContainer.Type tcref) ->
                    if tyconRefUsesLocalXmlDoc g.compilingFslib tcref || tcref.XmlDoc.NonEmpty  then Some tcref.XmlDoc else None
                | _ -> None
            GetXmlCommentForItemAux xmldoc infoReader m item

        | Item.UnionCaseField (ucinfo, _) ->
            let xmldoc =
                if tyconRefUsesLocalXmlDoc g.compilingFslib ucinfo.TyconRef || ucinfo.UnionCase.XmlDoc.NonEmpty then Some ucinfo.UnionCase.XmlDoc else None
            GetXmlCommentForItemAux xmldoc infoReader m item

        | Item.SetterArg (_, item) -> 
            GetXmlCommentForItem infoReader m item
        
        // In all these cases, there is no direct XML documentation from F# comments
        | Item.ActivePatternResult _ 
        | Item.NewDef _
        | Item.ILField _
        | Item.FakeInterfaceCtor _
        | Item.DelegateCtor _
        |  _ -> 
            GetXmlCommentForItemAux None infoReader m item

        |> GetXmlDocFromLoader infoReader

    let IsAttribute (infoReader: InfoReader) item =
        try
            let g = infoReader.g
            let amap = infoReader.amap
            match item with
            | Item.Types(_, ((TType_app(tcref, _)) :: _))
            | Item.UnqualifiedType(tcref :: _) ->
                let ty = generalizedTyconRef tcref
                Infos.ExistsHeadTypeInEntireHierarchy g amap range0 ty g.tcref_System_Attribute
            | _ -> false
        with _ -> false

#if !NO_EXTENSIONTYPING

    /// Determine if an item is a provided type 
    let (|ItemIsProvidedType|_|) g item =
        match item with
        | Item.Types(_name, tys) ->
            match tys with
            | [AppTy g (tyconRef, _typeInst)] ->
                if tyconRef.IsProvidedErasedTycon || tyconRef.IsProvidedGeneratedTycon then
                    Some tyconRef
                else
                    None
            | _ -> None
        | _ -> None

    /// Determine if an item is a provided type that has static parameters
    let (|ItemIsProvidedTypeWithStaticArguments|_|) m g item =
        match item with
        | Item.Types(_name, tys) ->
            match tys with
            | [AppTy g (tyconRef, _typeInst)] ->
                if tyconRef.IsProvidedErasedTycon || tyconRef.IsProvidedGeneratedTycon then
                    let typeBeforeArguments = 
                        match tyconRef.TypeReprInfo with 
                        | TProvidedTypeExtensionPoint info -> info.ProvidedType
                        | _ -> failwith "unreachable"
                    let staticParameters = typeBeforeArguments.PApplyWithProvider((fun (typeBeforeArguments, provider) -> typeBeforeArguments.GetStaticParameters provider), range=m) 
                    let staticParameters = staticParameters.PApplyArray(id, "GetStaticParameters", m)
                    Some staticParameters
                else
                    None
            | _ -> None
        | _ -> None

    let (|ItemIsProvidedMethodWithStaticArguments|_|) item =
        match item with
        // Prefer the static parameters from the uninstantiated method info
        | Item.MethodGroup(_, _, Some minfo) ->
            match minfo.ProvidedStaticParameterInfo  with 
            | Some (_, staticParameters) -> Some staticParameters
            | _ -> None
        | Item.MethodGroup(_, [minfo], _) ->
            match minfo.ProvidedStaticParameterInfo  with 
            | Some (_, staticParameters) -> Some staticParameters
            | _ -> None
        | _ -> None

    /// Determine if an item has static arguments
    let (|ItemIsWithStaticArguments|_|) m g item =
        match item with
        | ItemIsProvidedTypeWithStaticArguments m g staticParameters -> Some staticParameters
        | ItemIsProvidedMethodWithStaticArguments staticParameters -> Some staticParameters
        | _ -> None

#endif

    /// Get the "F1 Keyword" associated with an item, for looking up documentation help indexes on the web
    let rec GetF1Keyword (g: TcGlobals) item = 

        let getKeywordForMethInfo (minfo : MethInfo) =
            match minfo with 
            | FSMeth(_, _, vref, _) ->
                match vref.DeclaringEntity with
                | Parent tcref ->
                    (tcref |> ticksAndArgCountTextOfTyconRef) + "." + vref.CompiledName g.CompilerGlobalState |> Some
                | ParentNone -> None
                
            | ILMeth (_, minfo, _) ->
                let typeString = minfo.DeclaringTyconRef |> ticksAndArgCountTextOfTyconRef
                let paramString =
                    let nGenericParams = minfo.RawMetadata.GenericParams.Length 
                    if nGenericParams > 0 then "``"+(nGenericParams.ToString()) else ""
                sprintf "%s.%s%s" typeString minfo.RawMetadata.Name paramString |> Some

            | DefaultStructCtor _  -> None
#if !NO_EXTENSIONTYPING
            | ProvidedMeth _ -> None
#endif
             
        match item with
        | Item.Value vref | Item.CustomBuilder (_, vref) -> 
            let v = vref.Deref
            if v.IsModuleBinding && v.HasDeclaringEntity then
                let tyconRef = v.TopValDeclaringEntity
                let paramsString =
                    match v.Typars with
                    |   [] -> ""
                    |   l -> "``"+(List.length l).ToString() 
                
                sprintf "%s.%s%s" (tyconRef |> ticksAndArgCountTextOfTyconRef) (v.CompiledName g.CompilerGlobalState) paramsString |> Some
            else
                None

        | Item.ActivePatternCase apref ->
            GetF1Keyword g (Item.Value apref.ActivePatternVal)

        | Item.UnionCase(ucinfo, _) ->
            (ucinfo.TyconRef |> ticksAndArgCountTextOfTyconRef) + "."+ucinfo.Name |> Some

        | Item.RecdField rfi ->
            (rfi.TyconRef |> ticksAndArgCountTextOfTyconRef) + "." + rfi.Name |> Some
        
        | Item.AnonRecdField _ -> None
        
        | Item.ILField finfo ->
             match finfo with 
             | ILFieldInfo(tinfo, fdef) -> 
                 (tinfo.TyconRefOfRawMetadata |> ticksAndArgCountTextOfTyconRef) + "." + fdef.Name |> Some
#if !NO_EXTENSIONTYPING
             | ProvidedField _ -> None
#endif
        | Item.Types(_, ((AppTy g (tcref, _)) :: _)) 
        | Item.DelegateCtor(AppTy g (tcref, _))
        | Item.FakeInterfaceCtor(AppTy g (tcref, _))
        | Item.UnqualifiedType (tcref :: _)
        | Item.ExnCase tcref -> 
            // strip off any abbreviation
            match generalizedTyconRef tcref with 
            | AppTy g (tcref, _)  -> Some (ticksAndArgCountTextOfTyconRef tcref)
            | _ -> None

        // Pathological cases of the above
        | Item.Types _ 
        | Item.DelegateCtor _
        | Item.FakeInterfaceCtor _
        | Item.UnqualifiedType [] -> 
            None

        | Item.ModuleOrNamespaces modrefs -> 
            match modrefs with 
            | modref :: _ -> 
                // namespaces from type providers need to be handled separately because they don't have compiled representation
                // otherwise we'll fail at tast.fs
                match modref.Deref.TypeReprInfo with
#if !NO_EXTENSIONTYPING                
                | TProvidedNamespaceExtensionPoint _ -> 
                    modref.CompilationPathOpt
                    |> Option.bind (fun path ->
                        // works similar to generation of xml-docs at tastops.fs, probably too similar
                        // TODO: check if this code can be implemented using xml-doc generation functionality
                        let prefix = path.AccessPath |> Seq.map fst |> String.concat "."
                        let fullName = if prefix = "" then modref.CompiledName else prefix + "." + modref.CompiledName
                        Some fullName
                        )
#endif
                | _ -> modref.Deref.CompiledRepresentationForNamedType.FullName |> Some
            | [] ->  None // Pathological case of the above

        | Item.Property(_, (pinfo :: _)) -> 
            match pinfo with 
            | FSProp(_, _, Some vref, _) 
            | FSProp(_, _, _, Some vref) -> 
                // per spec, extension members in F1 keywords are qualified with definition class
                match vref.DeclaringEntity with 
                | Parent tcref ->
                    (tcref |> ticksAndArgCountTextOfTyconRef)+"."+vref.PropertyName|> Some                     
                | ParentNone -> None

            | ILProp(ILPropInfo(tinfo, pdef)) -> 
                let tcref = tinfo.TyconRefOfRawMetadata
                (tcref |> ticksAndArgCountTextOfTyconRef)+"."+pdef.Name |> Some
            | FSProp _ -> None
#if !NO_EXTENSIONTYPING
            | ProvidedProp _ -> None
#endif
        | Item.Property(_, []) -> None // Pathological case of the above
                   
        | Item.Event einfo -> 
            match einfo with 
            | ILEvent _  ->
                let tcref = einfo.DeclaringTyconRef
                (tcref |> ticksAndArgCountTextOfTyconRef)+"."+einfo.EventName |> Some
            | FSEvent(_, pinfo, _, _) ->
                match pinfo.ArbitraryValRef with 
                | Some vref ->
                   // per spec, members in F1 keywords are qualified with definition class
                   match vref.DeclaringEntity with 
                   | Parent tcref -> (tcref |> ticksAndArgCountTextOfTyconRef)+"."+vref.PropertyName|> Some                     
                   | ParentNone -> None
                | None -> None
#if !NO_EXTENSIONTYPING
            | ProvidedEvent _ -> None 
#endif
        | Item.CtorGroup(_, minfos) ->
            match minfos with 
            | [] -> None
            | FSMeth(_, _, vref, _) :: _ ->
                   match vref.DeclaringEntity with
                   | Parent tcref -> (tcref |> ticksAndArgCountTextOfTyconRef) + ".#ctor"|> Some
                   | ParentNone -> None
#if !NO_EXTENSIONTYPING
            | ProvidedMeth _ :: _ -> None
#endif
            | minfo :: _ ->
                let tcref = minfo.DeclaringTyconRef
                (tcref |> ticksAndArgCountTextOfTyconRef)+".#ctor" |> Some
        | Item.CustomOperation (_, _, Some minfo) -> getKeywordForMethInfo minfo
        | Item.MethodGroup(_, _, Some minfo) -> getKeywordForMethInfo minfo
        | Item.MethodGroup(_, minfo :: _, _) -> getKeywordForMethInfo minfo
        | Item.SetterArg (_, propOrField) -> GetF1Keyword g propOrField 
        | Item.MethodGroup(_, [], _) 
        | Item.CustomOperation (_, _, None)   // "into"
        | Item.NewDef _ // "let x$yz = ..." - no keyword
        | Item.ArgName _ // no keyword on named parameters 
        | Item.UnionCaseField _ 
        | Item.TypeVar _ 
        | Item.ImplicitOp _
        | Item.ActivePatternResult _ // "let (|Foo|Bar|) = .. Fo$o ..." - no keyword
            ->  None

    /// Get rid of groups of overloads an replace them with single items.
    let FlattenItems g (m: range) item =
        ignore m
        match item with 
        | Item.MethodGroup(nm, minfos, orig) -> minfos |> List.map (fun minfo -> Item.MethodGroup(nm, [minfo], orig))  
        | Item.CtorGroup(nm, cinfos) -> cinfos |> List.map (fun minfo -> Item.CtorGroup(nm, [minfo])) 
        | Item.FakeInterfaceCtor _
        | Item.DelegateCtor _ -> [item]
        | Item.NewDef _ 
        | Item.ILField _ -> []
        | Item.Event _ -> []
        | Item.RecdField rfinfo -> if isForallFunctionTy g rfinfo.FieldType then [item] else []
        | Item.Value v -> if isForallFunctionTy g v.Type then [item] else []
        | Item.UnionCase(ucr, _) -> if not ucr.UnionCase.IsNullary then [item] else []
        | Item.ExnCase ecr -> if isNil (recdFieldsOfExnDefRef ecr) then [] else [item]
        | Item.Property(_, pinfos) -> 
            let pinfo = List.head pinfos 
            if pinfo.IsIndexer then [item] else []
#if !NO_EXTENSIONTYPING
        | ItemIsWithStaticArguments m g _ -> [item] // we pretend that provided-types-with-static-args are method-like in order to get ParamInfo for them
#endif
        | Item.CustomOperation(_name, _helpText, _minfo) -> [item]
        | Item.TypeVar _ -> []
        | Item.CustomBuilder _ -> []
        | _ -> []

