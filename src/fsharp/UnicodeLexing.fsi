// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.UnicodeLexing

open System.IO
open FSharp.Compiler.Features
open FSharp.Compiler.Text
open Internal.Utilities.Text.Lexing

type Lexbuf = LexBuffer<char>

val internal StringAsLexbuf: reportLibraryOnlyFeatures: bool * (LanguageFeature -> bool) * string -> Lexbuf

val public FunctionAsLexbuf: reportLibraryOnlyFeatures: bool * (LanguageFeature -> bool) * (char [] * int * int -> int) -> Lexbuf

val public SourceTextAsLexbuf: reportLibraryOnlyFeatures: bool * (LanguageFeature -> bool) * ISourceText -> Lexbuf

/// Will not dispose of the stream reader.
val public StreamReaderAsLexbuf: reportLibraryOnlyFeatures: bool * (LanguageFeature -> bool) * StreamReader -> Lexbuf
