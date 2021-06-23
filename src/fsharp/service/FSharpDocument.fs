// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.IO.MemoryMappedFiles
open System.Xml
open System.Runtime.InteropServices
open System.Threading
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.CreateILModule
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.EditorServices
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.Syntax
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps

[<RequireQualifiedAccess>]
type DocumentText =
    | OnDisk
    | Stream of Stream
    | SourceText of ISourceText

    interface IDisposable with

        member this.Dispose() =
            match this with
            | Stream stream -> stream.Dispose()
            | _ -> ()

[<AbstractClass>]
type FSharpDocument internal () =

    abstract FilePath : string

    abstract TimeStamp : DateTime

    abstract IsOpen : bool

    abstract GetText : unit -> DocumentText

type private FSharpDocumentMemoryMappedFile(filePath: string, timeStamp: DateTime, isOpen, openStream: unit -> Stream) =
    inherit FSharpDocument()

    override _.FilePath = filePath

    override _.TimeStamp = timeStamp

    override _.IsOpen = isOpen

    override _.GetText() =
        openStream () |> DocumentText.Stream

type private FSharpDocumentByteArray(filePath: string, timeStamp: DateTime, isOpen, bytes: byte[]) =
    inherit FSharpDocument()

    override _.FilePath = filePath

    override _.TimeStamp = timeStamp

    override _.IsOpen = isOpen

    override _.GetText() =
        DocumentText.Stream(new MemoryStream(bytes, 0, bytes.Length, false) :> Stream)

type private FSharpDocumentFromFile(filePath: string) =
    inherit FSharpDocument()

    override _.FilePath = filePath

    override _.TimeStamp = FileSystem.GetLastWriteTimeShim(filePath)

    override _.IsOpen = false

    override _.GetText() = DocumentText.OnDisk

type private FSharpDocumentCustom(filePath: string, isOpen, getTimeStamp, getSourceText) =
    inherit FSharpDocument()

    override _.FilePath = filePath

    override _.TimeStamp = getTimeStamp()

    override _.IsOpen = isOpen

    override _.GetText() =
        DocumentText.SourceText(getSourceText())

type FSharpDocument with

    static member Create(filePath, isOpen, getTimeStamp, getSourceText) =
        FSharpDocumentCustom(filePath, isOpen, getTimeStamp, getSourceText) :> FSharpDocument

    static member CreateFromFile(filePath: string) =
        FSharpDocumentFromFile(filePath) :> FSharpDocument

    static member CreateCopyFromFile(filePath: string, isOpen) =
        let timeStamp = FileSystem.GetLastWriteTimeShim(filePath)

        // We want to use mmaped documents only when
        // not running on mono, since its MemoryMappedFile implementation throws when "mapName" is not provided (is null), (see: https://github.com/mono/mono/issues/10245)
        if runningOnMono then
            let bytes = FileSystem.OpenFileForReadShim(filePath, useMemoryMappedFile = false).ReadAllBytes()
            FSharpDocumentByteArray(filePath, timeStamp, isOpen, bytes) :> FSharpDocument
        else
            let openStream = fun () ->
                FileSystem.OpenFileForReadShim(filePath, useMemoryMappedFile = true, shouldShadowCopy = true)
            FSharpDocumentMemoryMappedFile(filePath, timeStamp, isOpen, openStream) :> FSharpDocument
            
