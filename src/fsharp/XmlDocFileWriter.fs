// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.XmlDocFileWriter

open System.IO
open System.Reflection
open Internal.Utilities.Library
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

module XmlDocWriter =

    let hasDoc (doc: XmlDoc) = not doc.IsEmpty

    let ComputeXmlDocSigs (tcGlobals, generatedCcu: CcuThunk) =
        let g = tcGlobals
        let doValSig ptext (v: Val)  = if hasDoc v.XmlDoc then v.XmlDocSig <- XmlDocSigOfVal g false ptext v
        let doTyconSig ptext (tc: Tycon) =
            if (hasDoc tc.XmlDoc) then tc.XmlDocSig <- XmlDocSigOfTycon [ptext; tc.CompiledName]
            for vref in tc.MembersOfFSharpTyconSorted do
                doValSig ptext vref.Deref
            for uc in tc.UnionCasesArray do
                if (hasDoc uc.XmlDoc) then uc.XmlDocSig <- XmlDocSigOfUnionCase [ptext; tc.CompiledName; uc.Id.idText]
            for rf in tc.AllFieldsArray do
                if (hasDoc rf.XmlDoc) then
                    rf.XmlDocSig <-
                        if tc.IsRecordTycon && (not rf.IsStatic) then
                            // represents a record field, which is exposed as a property
                            XmlDocSigOfProperty [ptext; tc.CompiledName; rf.Id.idText]
                        else
                            XmlDocSigOfField [ptext; tc.CompiledName; rf.Id.idText]

        let doModuleMemberSig path (m: ModuleOrNamespace) = m.XmlDocSig <- XmlDocSigOfSubModul [path]
        (* moduleSpec - recurses *)
        let rec doModuleSig path (mspec: ModuleOrNamespace) =
            let mtype = mspec.ModuleOrNamespaceType
            let path =
                (* skip the first item in the path which is the assembly name *)
                match path with
                | None -> Some ""
                | Some "" -> Some mspec.LogicalName
                | Some p -> Some (p+"."+mspec.LogicalName)
            let ptext = match path with None -> "" | Some t -> t
            if mspec.IsModule then doModuleMemberSig ptext mspec
            let vals =
                mtype.AllValsAndMembers
                |> Seq.toList
                |> List.filter (fun x  -> not x.IsCompilerGenerated)
                |> List.filter (fun x -> x.MemberInfo.IsNone || x.IsExtensionMember)
            List.iter (doModuleSig  path)  mtype.ModuleAndNamespaceDefinitions
            List.iter (doTyconSig  ptext) mtype.ExceptionDefinitions
            List.iter (doValSig    ptext) vals
            List.iter (doTyconSig  ptext) mtype.TypeDefinitions

        doModuleSig None generatedCcu.Contents

    let WriteXmlDocFile (assemblyName, generatedCcu: CcuThunk, xmlfile) =
        if not (FileSystemUtils.hasSuffixCaseInsensitive "xml" xmlfile ) then
            error(Error(FSComp.SR.docfileNoXmlSuffix(), Range.rangeStartup))

        let mutable members = []
        let addMember id xmlDoc =
            if hasDoc xmlDoc then
                let doc = xmlDoc.GetXmlText()
                members <- (id, doc) :: members
        let doVal (v: Val) = addMember v.XmlDocSig v.XmlDoc
        let doUnionCase (uc: UnionCase) = addMember uc.XmlDocSig uc.XmlDoc
        let doField (rf: RecdField) = addMember rf.XmlDocSig rf.XmlDoc
        let doTycon (tc: Tycon) =
            addMember tc.XmlDocSig tc.XmlDoc
            for vref in tc.MembersOfFSharpTyconSorted do
                doVal vref.Deref
            for uc in tc.UnionCasesArray do
                doUnionCase uc
            for rf in tc.AllFieldsArray do
                doField rf

        let modulMember (m: ModuleOrNamespace) = addMember m.XmlDocSig m.XmlDoc

        let rec doModule (mspec: ModuleOrNamespace) =
            let mtype = mspec.ModuleOrNamespaceType
            if mspec.IsModule then modulMember mspec
            let vals =
                mtype.AllValsAndMembers
                |> Seq.toList
                |> List.filter (fun x  -> not x.IsCompilerGenerated)
                |> List.filter (fun x -> x.MemberInfo.IsNone || x.IsExtensionMember)
            List.iter doModule mtype.ModuleAndNamespaceDefinitions
            List.iter doTycon mtype.ExceptionDefinitions
            List.iter doVal vals
            List.iter doTycon mtype.TypeDefinitions

        doModule generatedCcu.Contents

        use os = FileSystem.OpenFileForWriteShim(xmlfile, FileMode.OpenOrCreate).GetWriter()

        fprintfn os ("<?xml version=\"1.0\" encoding=\"utf-8\"?>")
        fprintfn os ("<doc>")
        fprintfn os ("<assembly><name>%s</name></assembly>") assemblyName
        fprintfn os ("<members>")
        members |> List.iter (fun (id, doc) ->
            fprintfn os  "<member name=\"%s\">" id
            fprintfn os  "%s" doc
            fprintfn os  "</member>")
        fprintfn os "</members>"
        fprintfn os "</doc>"
