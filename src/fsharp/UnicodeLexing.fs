// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Functions for Unicode char-based lexing
module internal FSharp.Compiler.UnicodeLexing

open System.IO
open Internal.Utilities.Text.Lexing

type Lexbuf =  LexBuffer<char>

let StringAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, s: string) =
    LexBuffer<char>.FromChars (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, s.ToCharArray())

let FunctionAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, bufferFiller) =
    LexBuffer<char>.FromFunction(reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, bufferFiller)

let SourceTextAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, sourceText) =
    LexBuffer<char>.FromSourceText(reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, sourceText)

let StreamReaderAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, reader: StreamReader) =
    let mutable isFinished = false
    FunctionAsLexbuf (reportLibraryOnlyFeatures, supportsFeature, checkLanguageFeatureErrorRecover, fun (chars, start, length) ->
        if isFinished then 0
        else
            let nBytesRead = reader.Read(chars, start, length)
            if nBytesRead = 0 then
                isFinished <- true
                0
            else
                nBytesRead
    )
