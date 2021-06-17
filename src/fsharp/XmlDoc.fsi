// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Xml

open System.Xml
open FSharp.Compiler.Text
open FSharp.Compiler.AbstractIL.IL

/// Represents collected XmlDoc lines
[<Class>]
type public XmlDoc =

    new: unprocessedLines:string [] * range:range -> XmlDoc

    /// Merge two XML documentation
    static member Merge: doc1:XmlDoc -> doc2:XmlDoc -> XmlDoc

    /// Check the XML documentation
    member internal Check: paramNamesOpt:string list option -> unit

    /// Get the lines after insertion of implicit summary tags and encoding
    member GetElaboratedXmlLines: unit -> string []

    /// Get the elaborated XML documentation as XML text
    member GetXmlText: unit -> string

    member IsEmpty: bool

    member NonEmpty: bool

    member Range: range

    /// Get the lines before insertion of implicit summary tags and encoding
    member UnprocessedLines: string []

    static member Empty: XmlDoc
  
/// Used to collect XML documentation during lexing and parsing.
type internal XmlDocCollector =

    new: unit -> XmlDocCollector

    member AddGrabPoint: pos: pos -> unit

    member AddXmlDocLine: line:string * range:range -> unit

    member LinesBefore: grabPointPos: pos -> (string * range) []
  
/// Represents the XmlDoc fragments as collected from the lexer during parsing
[<Sealed>]
type public PreXmlDoc =

    static member internal CreateFromGrabPoint: collector:XmlDocCollector * grabPointPos: pos -> PreXmlDoc

    static member Merge: a:PreXmlDoc -> b:PreXmlDoc -> PreXmlDoc
    
    static member Create: unprocessedLines:string [] * range:range -> PreXmlDoc

    member ToXmlDoc: check:bool * paramNamesOpt:string list option -> XmlDoc

    static member Empty: PreXmlDoc

[<Sealed>]
type internal XmlDocumentationInfo =

    member TryGetXmlDocBySig : xmlDocSig: string -> XmlDoc option

    static member TryCreateFromFile : xmlFileName: string -> XmlDocumentationInfo option

type internal IXmlDocumentationInfoLoader =

    abstract TryLoad : assemblyFileName: string * ILModuleDef -> XmlDocumentationInfo option
