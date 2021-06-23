// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.IO
open FSharp.Compiler.Text

[<RequireQualifiedAccess>]
type internal DocumentText =
    | OnDisk
    | Stream of Stream
    | SourceText of ISourceText

    interface IDisposable

[<AbstractClass>]
type FSharpDocument =

    /// The file path of the document.
    abstract FilePath : string

    /// The timestamp of the document.
    abstract TimeStamp : DateTime

    /// Is the document open in an editor?
    /// This is used to allow the background build to provide rich information on the document.
    abstract IsOpen : bool

    abstract internal GetText : unit -> DocumentText

    static member internal CreateFromFile : filePath: string -> FSharpDocument

    static member CreateCopyFromFile : filePath: string * isOpen: bool -> FSharpDocument

    static member Create : filePath: string * isOpen: bool * getTimeStamp: (unit -> DateTime) * getSourceText: (unit -> ISourceText) -> FSharpDocument