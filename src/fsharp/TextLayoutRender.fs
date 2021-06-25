// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Text

open System
open System.Collections.Immutable
open System.IO
open FSharp.Compiler.Text
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Layout
open FSharp.Core.Printf

#nowarn "62" // This construct is for ML compatibility.

type NavigableTaggedText(taggedText: TaggedText, range: range) =
    inherit TaggedText(taggedText.Tag, taggedText.Text)
    member val Range = range

type WebLinkTaggedText(taggedText: TaggedText, uri: System.Uri) =
    inherit TaggedText(taggedText.Tag, taggedText.Text)
    member val Uri = uri

module SepL =
    let dot = sepL TaggedText.dot
    let star = sepL TaggedText.star
    let colon = sepL TaggedText.colon
    let questionMark = sepL TaggedText.questionMark
    let leftParen = sepL TaggedText.leftParen
    let comma = sepL TaggedText.comma
    let space = sepL TaggedText.space
    let leftBracket = sepL TaggedText.leftBracket
    let leftAngle = sepL TaggedText.leftAngle
    let lineBreak = sepL TaggedText.lineBreak
    let rightParen = sepL TaggedText.rightParen

module WordL =
    let arrow = wordL TaggedText.arrow
    let star = wordL TaggedText.star
    let colon = wordL TaggedText.colon
    let equals = wordL TaggedText.equals
    let keywordNew = wordL TaggedText.keywordNew
    let structUnit = wordL TaggedText.structUnit
    let keywordStatic = wordL TaggedText.keywordStatic
    let keywordMember = wordL TaggedText.keywordMember
    let keywordVal = wordL TaggedText.keywordVal
    let keywordEvent = wordL TaggedText.keywordEvent
    let keywordWith = wordL TaggedText.keywordWith
    let keywordSet = wordL TaggedText.keywordSet
    let keywordGet = wordL TaggedText.keywordGet
    let keywordTrue = wordL TaggedText.keywordTrue
    let keywordFalse = wordL TaggedText.keywordFalse
    let bar = wordL TaggedText.bar
    let keywordStruct = wordL TaggedText.keywordStruct
    let keywordInherit = wordL TaggedText.keywordInherit
    let keywordEnd = wordL TaggedText.keywordEnd
    let keywordNested = wordL TaggedText.keywordNested
    let keywordType = wordL TaggedText.keywordType
    let keywordDelegate = wordL TaggedText.keywordDelegate
    let keywordOf = wordL TaggedText.keywordOf
    let keywordInternal = wordL TaggedText.keywordInternal
    let keywordPrivate = wordL TaggedText.keywordPrivate
    let keywordAbstract = wordL TaggedText.keywordAbstract
    let keywordOverride = wordL TaggedText.keywordOverride
    let keywordEnum = wordL TaggedText.keywordEnum

module LeftL =
    let leftParen = leftL TaggedText.leftParen
    let questionMark = leftL TaggedText.questionMark
    let colon = leftL TaggedText.colon
    let leftBracketAngle = leftL TaggedText.leftBracketAngle
    let leftBracketBar = leftL TaggedText.leftBracketBar
    let keywordTypeof = leftL TaggedText.keywordTypeof
    let keywordTypedefof = leftL TaggedText.keywordTypedefof

module RightL =
    let comma = rightL TaggedText.comma
    let rightParen = rightL TaggedText.rightParen
    let colon = rightL TaggedText.colon
    let rightBracket = rightL TaggedText.rightBracket
    let rightAngle = rightL TaggedText.rightAngle
    let rightBracketAngle = rightL TaggedText.rightBracketAngle
    let rightBracketBar = rightL TaggedText.rightBracketBar

type LayoutRenderer<'a, 'b> =
    abstract Start    : unit -> 'b
    abstract AddText  : 'b -> TaggedText -> 'b
    abstract AddBreak : 'b -> int -> 'b
    abstract AddTag   : 'b -> string * (string * string) list * bool -> 'b
    abstract Finish   : 'b -> 'a
  
type NoState = NoState
type NoResult = NoResult

[<AutoOpen>]
module LayoutRender =
    let mkNav r t = NavigableTaggedText(t, r) :> TaggedText

    let spaces n = new String(' ', n)
      
    let renderL (rr: LayoutRenderer<_, _>) layout =
        let rec addL z pos i layout k = 
          match layout with
          | ObjLeaf _ -> failwith "ObjLeaf should never appear here"
            (* pos is tab level *)
          | Leaf (_, text, _)                 -> 
              k(rr.AddText z text, i + text.Text.Length)
          | Node (l, r, Broken indent) -> 
              addL z pos i l <|
                fun (z, _i) ->
                  let z, i = rr.AddBreak z (pos+indent), (pos+indent) 
                  addL z (pos+indent) i r k
          | Node (l, r, _)             -> 
              let jm = Layout.JuxtapositionMiddle (l, r)
              addL z pos i l <|
                fun (z, i) ->
                  let z, i = if jm then z, i else rr.AddText z TaggedText.space, i+1 
                  let pos = i 
                  addL z pos i r k
          | Attr (tag, attrs, l)                -> 
              let z   = rr.AddTag z (tag, attrs, true) 
              addL z pos i l <|
                fun (z, i) ->
                  let z   = rr.AddTag z (tag, attrs, false) 
                  k(z, i)
        let pos = 0 
        let z, i = rr.Start(), 0 
        let z, _i = addL z pos i layout id
        rr.Finish z

    /// string render 
    let stringR =
      { new LayoutRenderer<string, string list> with 
          member _.Start () = []
          member _.AddText rstrs taggedText = taggedText.Text :: rstrs
          member _.AddBreak rstrs n = (spaces n) :: "\n" ::  rstrs 
          member _.AddTag z (_, _, _) = z
          member _.Finish rstrs = String.Join("", Array.ofList (List.rev rstrs)) }

    /// string render 
    let taggedTextListR collector =
      { new LayoutRenderer<NoResult, NoState> with 
          member _.Start () = NoState
          member _.AddText z text = collector text; z
          member _.AddBreak rstrs n = collector TaggedText.lineBreak; collector (TaggedText.tagSpace(spaces n)); rstrs 
          member _.AddTag z (_, _, _) = z
          member _.Finish rstrs = NoResult }

    /// channel LayoutRenderer
    let channelR (chan:TextWriter) =
      { new LayoutRenderer<NoResult, NoState> with 
          member r.Start () = NoState
          member r.AddText z s = chan.Write s.Text; z
          member r.AddBreak z n = chan.WriteLine(); chan.Write (spaces n); z
          member r.AddTag z (tag, attrs, start) =  z
          member r.Finish z = NoResult }

    /// buffer render
    let bufferR os =
      { new LayoutRenderer<NoResult, NoState> with 
          member r.Start () = NoState
          member r.AddText z s = bprintf os "%s" s.Text; z
          member r.AddBreak z n = bprintf os "\n"; bprintf os "%s" (spaces n); z
          member r.AddTag z (tag, attrs, start) = z
          member r.Finish z = NoResult }

    let showL layout = renderL stringR layout

    let outL (chan:TextWriter)  layout = renderL (channelR chan) layout |> ignore

    let bufferL os layout = renderL (bufferR os) layout |> ignore

    let emitL f layout = renderL (taggedTextListR f) layout |> ignore

    let toArray layout = 
        let output = ResizeArray()
        renderL (taggedTextListR (fun tt -> output.Add(tt))) layout |> ignore
        output.ToArray()
