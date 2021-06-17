// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Functions for Unicode char-based lexing
module internal FSharp.Compiler.UnicodeLexing

open System.IO
open Internal.Utilities.Text.Lexing

type Lexbuf =  LexBuffer<char>

let StringAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, s: string) =
    LexBuffer<char>.FromChars (reportLibraryOnlyFeatures, supportsFeature, s.ToCharArray())

let FunctionAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, bufferFiller) =
    LexBuffer<char>.FromFunction(reportLibraryOnlyFeatures, supportsFeature, bufferFiller)

let SourceTextAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, sourceText) =
    LexBuffer<char>.FromSourceText(reportLibraryOnlyFeatures, supportsFeature, sourceText)

let StreamReaderAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, reader: StreamReader) =
    let mutable isFinished = false
    FunctionAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, fun (chars, start, length) ->
        if isFinished then 0
        else
            let nBytesRead = reader.Read(chars, start, length)
            if nBytesRead = 0 then
                isFinished <- true
                0
            else
                nBytesRead
    )
