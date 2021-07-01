// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

// This implementation does not rely on Roslyn internals.

open System
open System.Diagnostics
open System.Threading
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text.BraceCompletion
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Utilities
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.CodeAnalysis.Classification

[<AutoOpen>]
module BraceCompletionSessionProviderHelpers =

    let tryGetCaretPoint (buffer: ITextBuffer) (session: IBraceCompletionSession) =
        let point = session.TextView.Caret.Position.Point.GetPoint(buffer, PositionAffinity.Predecessor)
        if point.HasValue then Some point.Value
        else None

    let tryGetCaretPosition session =
        session |> tryGetCaretPoint session.SubjectBuffer

    let tryInsertAdditionalBracePair (session: IBraceCompletionSession) openingChar closingChar =
        let sourceCode = session.TextView.TextSnapshot
        let position = session.TextView.Caret.Position.BufferPosition.Position
        let start = position - 2
        let end' = position

        start >= 0 && end' < sourceCode.Length &&
        sourceCode.GetText(start, 1).[0] = openingChar && sourceCode.GetText(end', 1).[0] = closingChar

    [<Literal>] 
    let BraceCompletion = "Brace_Completion"

type IEditorBraceCompletionSession =
    inherit ILanguageService

    abstract CheckOpeningPoint : IBraceCompletionSession * CancellationToken -> bool

    abstract AfterStart : IBraceCompletionSession * CancellationToken -> unit

    abstract AllowOverType : IBraceCompletionSession * CancellationToken -> bool

    abstract AfterReturn : IBraceCompletionSession * CancellationToken -> unit

[<AllowNullLiteral>]
type IEditorBraceCompletionSessionFactory =
    inherit ILanguageService

    abstract TryCreateSession : Document * openingPosition: int * openingBrace: char * CancellationToken -> IEditorBraceCompletionSession?

type BraceCompletionSession 
    (
        textView: ITextView,
        subjectBuffer: ITextBuffer,
        openingPoint: SnapshotPoint,
        openingBrace: char,
        closingBrace: char,
        undoHistory: ITextUndoHistory,
        editorOperationsFactoryService: IEditorOperationsFactoryService,
        session: IEditorBraceCompletionSession
    ) =

    let mutable closingPoint = subjectBuffer.CurrentSnapshot.CreateTrackingPoint(openingPoint.Position, PointTrackingMode.Positive)
    let mutable openingPoint : ITrackingPoint? = null
    let editorOperations = editorOperationsFactoryService.GetEditorOperations(textView)

    member _.EndSession() =
        closingPoint <- null
        openingPoint <- null

    member _.CreateUndoTransaction() =
        undoHistory.CreateTransaction(BraceCompletion)

    member this.Start (cancellationToken: CancellationToken) =
        // Sanity check.
        if closingPoint = null then this.EndSession()
        else

        // this is where the caret should go after the change
        let pos = textView.Caret.Position.BufferPosition
        let beforeTrackingPoint = pos.Snapshot.CreateTrackingPoint(pos.Position, PointTrackingMode.Negative)

        let mutable snapshot = subjectBuffer.CurrentSnapshot
        let closingSnapshotPoint = closingPoint.GetPoint(snapshot)

        if closingSnapshotPoint.Position < 1 then
            Debug.Fail("The closing point was not found at the expected position.")
            this.EndSession()
        else
            let openingSnapshotPoint = closingSnapshotPoint.Subtract(1)

            if openingSnapshotPoint.GetChar() <> openingBrace then
                // there is a bug in editor brace completion engine on projection buffer that already fixed in vs_pro. until that is FIed to use
                // I will make this not to assert
                // Debug.Fail("The opening brace was not found at the expected position.");
                this.EndSession()
            else
                openingPoint <- snapshot.CreateTrackingPoint(openingSnapshotPoint.Position, PointTrackingMode.Positive)
                let _document = snapshot.GetOpenDocumentInCurrentContextWithChanges()

                if not (session.CheckOpeningPoint(this, cancellationToken)) then
                    this.EndSession()
                else
                    use undo = this.CreateUndoTransaction()
                    let nextSnapshot =
                        use edit = subjectBuffer.CreateEdit()

                        edit.Insert(closingSnapshotPoint.Position, closingBrace.ToString()) |> ignore
                    
                        if edit.HasFailedChanges then
                            Debug.Fail("Unable to insert closing brace")

                            // exit without setting the closing point which will take us off the stack
                            edit.Cancel()
                            undo.Cancel()
                            None
                        else
                            Some(edit.Apply()) // FIXME: perhaps, it should be ApplyAndLogExceptions()

                    match nextSnapshot with
                    | None -> ()
                    | Some(nextSnapshot) ->

                        let beforePoint = beforeTrackingPoint.GetPoint(textView.TextSnapshot)

                        // switch from positive to negative tracking so it stays against the closing brace
                        closingPoint <- subjectBuffer.CurrentSnapshot.CreateTrackingPoint(closingPoint.GetPoint(nextSnapshot).Position, PointTrackingMode.Negative)

                        Debug.Assert(closingPoint.GetPoint(nextSnapshot).Position > 0 && (SnapshotSpan(closingPoint.GetPoint(nextSnapshot).Subtract(1), 1)).GetText().Equals(closingBrace.ToString()),
                            "The closing point does not match the closing brace character")

                        // move the caret back between the braces
                        textView.Caret.MoveTo(beforePoint) |> ignore

                        session.AfterStart(this, cancellationToken)

                        undo.Complete()

    member _.HasNoForwardTyping(caretPoint: SnapshotPoint, endPoint: SnapshotPoint) =
        Debug.Assert(caretPoint.Snapshot = endPoint.Snapshot, "snapshots do not match")

        if caretPoint.Snapshot = endPoint.Snapshot then
            if caretPoint = endPoint then
                true
            else
                if caretPoint.Position < endPoint.Position then
                    let span = SnapshotSpan(caretPoint, endPoint)
                    String.IsNullOrWhiteSpace(span.GetText())
                else
                    false
        else
            false

    member this.HasForwardTyping =
        let closingSnapshotPoint = closingPoint.GetPoint(subjectBuffer.CurrentSnapshot)

        if closingSnapshotPoint.Position > 0 then
            match tryGetCaretPosition this with
            | Some caretPos when not (this.HasNoForwardTyping(caretPos, closingSnapshotPoint.Subtract(1))) -> true
            | _ -> false
        else
            false

    member _.MoveCaretToClosingPoint() =
        let closingSnapshotPoint = closingPoint.GetPoint(subjectBuffer.CurrentSnapshot)

        // find the position just after the closing brace in the view's text buffer
        let afterBrace = 
            textView.BufferGraph.MapUpToBuffer(closingSnapshotPoint, PointTrackingMode.Negative, PositionAffinity.Predecessor, textView.TextBuffer)

        Debug.Assert(afterBrace.HasValue, "Unable to move caret to closing point")

        if afterBrace.HasValue then
            textView.Caret.MoveTo(afterBrace.Value) |> ignore

    interface IBraceCompletionSession with

        member this.Start() =
            // Brace completion is not cancellable.
            this.Start(CancellationToken.None)
                        
        member this.PreBackspace handledCommand =
            handledCommand <- false
            match openingPoint with 
            | Null -> ()
            | NonNull openingPoint -> 

            let caretPos = tryGetCaretPosition this
            let snapshot = subjectBuffer.CurrentSnapshot

            if caretPos.IsSome && caretPos.Value.Position > 0 && (caretPos.Value.Position - 1) = openingPoint.GetPoint(snapshot).Position && not this.HasForwardTyping then    
                use undo = this.CreateUndoTransaction()
                use edit = subjectBuffer.CreateEdit()

                let span = SnapshotSpan(openingPoint.GetPoint(snapshot), closingPoint.GetPoint(snapshot))

                edit.Delete(span.Span) |> ignore

                if edit.HasFailedChanges then
                    edit.Cancel()
                    undo.Cancel()
                    Debug.Fail("Unable to clear braces")
                else
                    // handle the command so the backspace does 
                    // not go through since we've already cleared the braces
                    handledCommand <- true
                    edit.Apply() |> ignore // FIXME: ApplyAndLogExceptions()
                    undo.Complete()
                    this.EndSession()
           
        member _.PostBackspace() = ()

        member this.PreOverType handledCommand =
            handledCommand <- false

            if closingPoint <> null then
                // Brace completion is not cancellable.
                let cancellationToken = CancellationToken.None
                let snapshot = subjectBuffer.CurrentSnapshot
                let _document = snapshot.GetOpenDocumentInCurrentContextWithChanges()

                let closingSnapshotPoint = closingPoint.GetPoint(snapshot)
                if not this.HasForwardTyping && session.AllowOverType(this, cancellationToken) then
                    let caretPos = tryGetCaretPosition this

                    Debug.Assert(caretPos.IsSome && caretPos.Value.Position < closingSnapshotPoint.Position)

                    match caretPos with
                    // ensure that we are within the session before clearing
                    | Some caretPos when caretPos.Position < closingSnapshotPoint.Position && closingSnapshotPoint.Position > 0 ->
                        use undo = this.CreateUndoTransaction()

                        editorOperations.AddBeforeTextBufferChangePrimitive()

                        let span = SnapshotSpan(caretPos, closingSnapshotPoint.Subtract(1))

                        use edit = subjectBuffer.CreateEdit()

                        edit.Delete(span.Span) |> ignore

                        if edit.HasFailedChanges then
                            Debug.Fail("Unable to clear closing brace")
                            edit.Cancel()
                            undo.Cancel()
                        else
                            handledCommand <- true
                            edit.Apply() |> ignore // FIXME: ApplyAndLogExceptions()
                            this.MoveCaretToClosingPoint()
                            editorOperations.AddAfterTextBufferChangePrimitive()
                            undo.Complete()
                    | _ -> ()
                
        member _.PostOverType() = ()

        member this.PreTab handledCommand =
            handledCommand <- false

            if not this.HasForwardTyping then
                handledCommand <- true

                use undo = this.CreateUndoTransaction()

                editorOperations.AddBeforeTextBufferChangePrimitive()
                this.MoveCaretToClosingPoint()
                editorOperations.AddAfterTextBufferChangePrimitive()
                undo.Complete()

        member _.PreReturn handledCommand =
            handledCommand <- false

        member this.PostReturn() =
            match tryGetCaretPosition this with
            | Some caretPos ->
                let closingSnapshotPoint = closingPoint.GetPoint(subjectBuffer.CurrentSnapshot)

                if closingSnapshotPoint.Position > 0 && this.HasNoForwardTyping(caretPos, closingSnapshotPoint.Subtract(1)) then
                    session.AfterReturn(this, CancellationToken.None)
            | _ -> ()
                
        member _.Finish() = ()

        member _.PostTab() = ()

        member _.PreDelete handledCommand =
            handledCommand <- false
        
        member _.PostDelete() = ()

        member _.OpeningBrace = openingBrace

        member _.ClosingBrace = closingBrace

        member _.OpeningPoint = openingPoint

        member _.ClosingPoint = closingPoint

        member _.SubjectBuffer = subjectBuffer

        member _.TextView = textView

module Parenthesis =

    [<Literal>]
    let OpenCharacter = '('

    [<Literal>]
    let CloseCharacter = ')'

module CurlyBrackets =

    [<Literal>]
    let OpenCharacter = '{'

    [<Literal>]
    let CloseCharacter = '}'

module SquareBrackets =

    [<Literal>]
    let OpenCharacter = '['

    [<Literal>]
    let CloseCharacter = ']'

module DoubleQuote =

    [<Literal>]
    let OpenCharacter = '"'

    [<Literal>]
    let CloseCharacter = '"'

(* This is for [| |] and {| |} , since the implementation deals with chars only. 
   We have to test if there is a { or [ before the cursor position and insert the closing '|'. *)
module VerticalBar =

    [<Literal>]
    let OpenCharacter = '|'

    [<Literal>]
    let CloseCharacter = '|'

(* This is for attributes [< >] , since the implementation deals with chars only. 
   We have to test if there is a [ before the cursor position and insert the closing '>'. *)
module AngleBrackets =

    [<Literal>]
    let OpenCharacter = '<'

    [<Literal>]
    let CloseCharacter = '>'

(* For multiline comments like this, since the implementation deals with chars only *)
module Asterisk =

    [<Literal>]
    let OpenCharacter = '*'

    [<Literal>]
    let CloseCharacter = '*'

type ParenthesisCompletionSession() =
    
    interface IEditorBraceCompletionSession with

        member _.AfterReturn(_session, _cancellationToken) = 
            ()

        member _.AfterStart(_session, _cancellationToken) = 
            ()

        member _.AllowOverType(_session, _cancellationToken) = 
            true

        member _.CheckOpeningPoint(_session, _cancellationToken) = 
            true 

type DoubleQuoteCompletionSession() =
    
    interface IEditorBraceCompletionSession with

        member _.AfterReturn(_session, _cancellationToken) = 
            ()

        member _.AfterStart(_session, _cancellationToken) = 
            ()

        member _.AllowOverType(_session, _cancellationToken) = 
            true

        member _.CheckOpeningPoint(_session, _cancellationToken) = 
            true 

type VerticalBarCompletionSession() =
    
    interface IEditorBraceCompletionSession with

        member _.AfterReturn(_session, _cancellationToken) = 
            ()

        member _.AfterStart(_session, _cancellationToken) = 
            ()

        member _.AllowOverType(_session, _cancellationToken) = 
            true
        
        (* This is for [| |] and {| |} , since the implementation deals with chars only. 
           We have to test if there is a { or [ before the cursor position and insert the closing '|'. *)
        member _.CheckOpeningPoint(session, _cancellationToken) = 
            tryInsertAdditionalBracePair session CurlyBrackets.OpenCharacter CurlyBrackets.CloseCharacter ||
            tryInsertAdditionalBracePair session SquareBrackets.OpenCharacter SquareBrackets.CloseCharacter

type AngleBracketCompletionSession() =
    
    interface IEditorBraceCompletionSession with

        member _.AfterReturn(_session, _cancellationToken) = 
            ()

        member _.AfterStart(_session, _cancellationToken) = 
            ()

        member _.AllowOverType(_session, _cancellationToken) = 
            true
        
        (* This is for attributes [< >] , since the implementation deals with chars only. 
           We have to test if there is a [ before the cursor position and insert the closing '>'. *)
        member _.CheckOpeningPoint(session, _cancellationToken) = 
            tryInsertAdditionalBracePair session SquareBrackets.OpenCharacter SquareBrackets.CloseCharacter          

(* For multi-line comments, test if it is between "()" *)
type AsteriskCompletionSession() =
    
    interface IEditorBraceCompletionSession with

        member _.AfterReturn(_session, _cancellationToken) = 
            ()

        member _.AfterStart(_session, _cancellationToken) = 
            ()

        member _.AllowOverType(_session, _cancellationToken) = 
            true
        
        (* This is for attributes [< >] , since the implementation deals with chars only. 
           We have to test if there is a [ before the cursor position and insert the closing '>'. *)
        member _.CheckOpeningPoint(session, _cancellationToken) = 
            tryInsertAdditionalBracePair session Parenthesis.OpenCharacter Parenthesis.CloseCharacter          

[<ExportLanguageService(typeof<IEditorBraceCompletionSessionFactory>, FSharpConstants.FSharpLanguageName)>]
type EditorBraceCompletionSessionFactory() =

    let spanIsNotCommentOrString (span: ClassifiedSpan) =
        match span.ClassificationType with
        | ClassificationTypeNames.Comment
        | ClassificationTypeNames.StringLiteral -> false
        | _ -> true 

    member _.IsSupportedOpeningBrace openingBrace =
        match openingBrace with
        | Parenthesis.OpenCharacter | CurlyBrackets.OpenCharacter | SquareBrackets.OpenCharacter
        | DoubleQuote.OpenCharacter | VerticalBar.OpenCharacter | AngleBrackets.OpenCharacter 
        | Asterisk.OpenCharacter -> true
        | _ -> false

    member _.CheckCodeContext(document: Document, position: int, _openingBrace:char, cancellationToken) =
        // We need to know if we are inside a F# string or comment. If we are, then don't do automatic completion.
        let sourceCodeTask = document.GetTextAsync(cancellationToken)
        sourceCodeTask.Wait(cancellationToken)
        let sourceCode = sourceCodeTask.Result
        
        position = 0 
        || (let colorizationData = Tokenizer.getClassifiedSpans(document.Id, sourceCode, TextSpan(position - 1, 1), Some (document.FilePath), [ ], cancellationToken)
            colorizationData.Count = 0
            || 
            colorizationData.Exists(fun classifiedSpan -> 
                classifiedSpan.TextSpan.IntersectsWith position &&
                spanIsNotCommentOrString classifiedSpan)) 

            // This would be the case where '{' has been pressed in a string and the next position
            // is known not to be a string.  This corresponds to the end of an interpolated string part.
            //
            // However, Roslyn doesn't activate BraceCompletionSessionProvider for string text at all (and at the time '{
            // is pressed the text is classified as a string). So this code doesn't get called at all and so
            // no brace completion is available inside interpolated strings.
            //
            // || (openingBrace = '{' &&
            // colorizationData.Exists(fun classifiedSpan -> 
            //    classifiedSpan.TextSpan.IntersectsWith (position-1) &&
            //    spanIsString classifiedSpan) && 
            // let colorizationData2 = Tokenizer.getClassifiedSpans(document.Id, sourceCode, TextSpan(position, 1), Some (document.FilePath), [ ], cancellationToken)
            // (colorizationData2.Count = 0 
            //  || 
            //  colorizationData2.Exists(fun classifiedSpan -> 
            //    classifiedSpan.TextSpan.IntersectsWith position &&
            //    not (spanIsString classifiedSpan)))))

    member _.CreateEditorSession(_document, _openingPosition, openingBrace, _cancellationToken) : IEditorBraceCompletionSession? =
        match openingBrace with
        | Parenthesis.OpenCharacter -> ParenthesisCompletionSession() :> _
        | CurlyBrackets.OpenCharacter -> ParenthesisCompletionSession() :> _
        | SquareBrackets.OpenCharacter -> ParenthesisCompletionSession() :> _
        | VerticalBar.OpenCharacter -> VerticalBarCompletionSession() :> _
        | AngleBrackets.OpenCharacter -> AngleBracketCompletionSession() :> _
        | DoubleQuote.OpenCharacter -> DoubleQuoteCompletionSession() :> _
        | Asterisk.OpenCharacter -> AsteriskCompletionSession() :> _
        | _ -> null

    interface IEditorBraceCompletionSessionFactory with

        member this.TryCreateSession(document, openingPosition, openingBrace, cancellationToken) : IEditorBraceCompletionSession? = 
            if this.IsSupportedOpeningBrace(openingBrace) && this.CheckCodeContext(document, openingPosition, openingBrace, cancellationToken) then
                this.CreateEditorSession(document, openingPosition, openingBrace, cancellationToken)
            else
                null

[<Export(typeof<IBraceCompletionSessionProvider>)>]
[<ContentType(FSharpConstants.FSharpContentTypeName)>]
[<BracePair(Parenthesis.OpenCharacter, Parenthesis.CloseCharacter)>]
[<BracePair(CurlyBrackets.OpenCharacter, CurlyBrackets.CloseCharacter)>]
[<BracePair(SquareBrackets.OpenCharacter, SquareBrackets.CloseCharacter)>]
[<BracePair(DoubleQuote.OpenCharacter, DoubleQuote.CloseCharacter)>]
[<BracePair(VerticalBar.OpenCharacter, VerticalBar.CloseCharacter)>]
[<BracePair(AngleBrackets.OpenCharacter, AngleBrackets.CloseCharacter)>]
[<BracePair(Asterisk.OpenCharacter, Asterisk.CloseCharacter)>]
type BraceCompletionSessionProvider
    [<ImportingConstructor>] 
    (
        undoManager: ITextBufferUndoManagerProvider,
        editorOperationsFactoryService: IEditorOperationsFactoryService
    ) =

    interface IBraceCompletionSessionProvider with

        member _.TryCreateSession(textView, openingPoint, openingBrace, closingBrace, session) =
            session <-
                maybe {
                    let! document =       openingPoint.Snapshot.GetOpenDocumentInCurrentContextWithChanges() |> Option.ofObj
                    let! sessionFactory = document.TryGetLanguageService<IEditorBraceCompletionSessionFactory>()
                    let! session =
                        sessionFactory.TryCreateSession(document, openingPoint.Position, openingBrace, CancellationToken.None)
                        |> Option.ofObj

                    let undoHistory = undoManager.GetTextBufferUndoManager(textView.TextBuffer).TextBufferUndoHistory
                    return BraceCompletionSession(
                        textView,
                        openingPoint.Snapshot.TextBuffer,
                        openingPoint, 
                        openingBrace, 
                        closingBrace, 
                        undoHistory, 
                        editorOperationsFactoryService, 
                        session) :> IBraceCompletionSession
                }
                |> Option.toObj

            session <> null