// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Interactive

open System
open System.Text
open System.Collections.Generic

/// System.Console.ReadKey appears to return an ANSI character (not the expected the unicode character).
/// When this fix flag is true, this byte is converted to a char using the System.Console.InputEncoding.
/// This is a code-around for bug://1345.
/// Fixes to System.Console.ReadKey may break this code around, hence the option here.
[<AutoOpen>]
module internal ConsoleOptions =

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
  // Shim to match nullness checking library support in preview
  let inline (|Null|NonNull|) (x: 'T) : Choice<unit,'T> = match x with null -> Null | v -> NonNull v
#endif

  let readKeyFixup (c:char) =
      // Assumes the c:char is actually a byte in the System.Console.InputEncoding.
      // Convert it to a Unicode char through the encoding.
      if 0 <= int c && int c <= 255 then
        let chars = System.Console.InputEncoding.GetChars [| byte c |]
        if chars.Length = 1 then
          chars.[0] // fixed up char
        else
          assert("readKeyFixHook: InputEncoding.GetChars(single-byte) returned multiple chars" = "")
          c // no fix up
      else
        assert("readKeyFixHook: given char is outside the 0..255 byte range" = "")
        c

type internal Style = Prompt | Out | Error

/// Class managing the command History.
type internal History() =
    let list  = new List<string>()
    let mutable current  = 0

    member x.Count = list.Count
    member x.Current =
        if current >= 0 && current < list.Count then list.[current] else String.Empty

    member x.Clear() = list.Clear(); current <- -1

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    member x.Add (line: string) =
#else
    member x.Add (line: string?) = 
#endif
        match line with 
        | Null | "" -> ()
        | NonNull line -> list.Add(line)

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
    member x.AddLast (line: string) =
#else
    member x.AddLast (line: string?) =  
#endif
        match line with 
        | Null | "" -> ()
        | NonNull line ->
            list.Add(line)
            current <- list.Count

    member x.Previous() =
        if (list.Count > 0)  then
            current <- ((current - 1) + list.Count) % list.Count
        x.Current

    member x.Next() =
        if (list.Count > 0) then
            current <- (current + 1) % list.Count
        x.Current

/// List of available optionsCache

type internal Options() =
    inherit History()
    let mutable root = ""
    member x.Root with get() = root and set(v) = (root <- v)

/// Cursor position management

module internal Utils =

    open System
    open System.Reflection
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections

    let guard(f) =
        try f()
        with e ->
             FSharp.Compiler.ErrorLogger.warning(Failure(sprintf "Note: an unexpected exception in fsi.exe readline console support. Consider starting fsi.exe with the --no-readline option and report the stack trace below to the .NET or Mono implementors\n%s\n%s\n" e.Message e.StackTrace))

    let rec previousWordFromIdx (line: string) (idx, isInWord) =
        if idx < 0 then 0 else
        match line.Chars(idx), isInWord with
        | ' ', true -> idx + 1
        | ' ', false -> previousWordFromIdx line (idx - 1, false)
        | _, _ -> previousWordFromIdx line (idx - 1, true)
        
    let rec nextWordFromIdx (line: string) (idx, isInWord) =
        if idx >= line.Length then line.Length - 1 else
        match line.Chars(idx), isInWord with
        | ' ', true -> idx 
        | ' ', false -> nextWordFromIdx line (idx + 1, false)
        | _, _ -> nextWordFromIdx line (idx + 1, true)
                    
[<Sealed>]
type internal Cursor =
    static member ResetTo(top,left) =
        Utils.guard(fun () ->
           Console.CursorTop <- min top (Console.BufferHeight - 1)
           Console.CursorLeft <- left)

    static member Move(inset, delta) =
        let position = Console.CursorTop * (Console.BufferWidth - inset) + (Console.CursorLeft - inset) + delta
        let top  = position / (Console.BufferWidth - inset)
        let left = inset + position % (Console.BufferWidth - inset)
        Cursor.ResetTo(top,left)

type internal Anchor =
    {top:int; left:int}
    static member Current(inset) = {top=Console.CursorTop;left= max inset Console.CursorLeft}
    static member Top(inset) = {top = 0; left = inset}
    member p.PlaceAt(inset, index) =
        //printf "p.top = %d, p.left = %d, inset = %d, index = %d\n" p.top p.left inset index
        let left = inset + (( (p.left - inset) + index) % (Console.BufferWidth - inset))
        let top = p.top + ( (p.left - inset) + index) / (Console.BufferWidth - inset)
        Cursor.ResetTo(top,left)

type internal ReadLineConsole() =
    let history = new History()
    let mutable complete : (string option * string -> seq<string>) = fun (_s1,_s2) -> Seq.empty
    member x.SetCompletionFunction f = complete <- f

    /// Inset all inputs by this amount
    member x.Prompt = "> "
    member x.Prompt2 = "- "
    member x.Inset = x.Prompt.Length

    member x.GetOptions(input:string) =
        /// Tab optionsCache available in current context
        let optionsCache = new Options()

        let rec look parenCount i =
            if i <= 0 then i else
            match input.Chars(i - 1) with
            | c when Char.IsLetterOrDigit(c) (* or Char.IsWhiteSpace(c) *) -> look parenCount (i-1)
            | '.' | '_' -> look parenCount (i-1)
            | '}' | ')' | ']' -> look (parenCount+1) (i-1)
            | '(' | '{' | '[' -> look (parenCount-1) (i-1)
            | _ when parenCount > 0 -> look parenCount (i-1)
            | _ -> i
        let start = look 0 input.Length

        let name = input.Substring(start, input.Length - start)
        if (name.Trim().Length > 0) then
            let lastDot = name.LastIndexOf('.')
            let attr, pref, root =
                if (lastDot < 0) then
                    None, name, input.Substring(0, start)
                else
                    Some(name.Substring(0, lastDot)),
                    name.Substring(lastDot + 1),
                    input.Substring(0, start + lastDot + 1)
            //printf "attr, pref, root = %s\n" (any_to_string (attr, pref, root))
            try
                complete(attr,pref)
                |> Seq.filter(fun option -> option.StartsWith(pref,StringComparison.Ordinal))
                |> Seq.iter (fun option -> optionsCache.Add(option))
                 // engine.Evaluate(String.Format("dir({0})", attr)) as IEnumerable
                optionsCache.Root <-root
            with e ->
                optionsCache.Clear()
            optionsCache,true
        else
            optionsCache,false

    member x.MapCharacter(c) : string =
        match c with
        | '\x1A'-> "^Z"
        | _ -> "^?"

    member x.GetCharacterSize(c) =
        if (Char.IsControl(c))
        then x.MapCharacter(c).Length
        else 1

    static member TabSize = 4

    member x.ReadLine() =

        let checkLeftEdge(prompt) =
            let currLeft = Console.CursorLeft
            if currLeft < x.Inset then
                if currLeft = 0 then Console.Write (if prompt then x.Prompt2 else String(' ',x.Inset))
                Utils.guard(fun () ->
                    Console.CursorTop <- min Console.CursorTop (Console.BufferHeight - 1)
                    Console.CursorLeft <- x.Inset)

        // The caller writes the primary prompt.  If we are reading the 2nd and subsequent lines of the
        // input we're responsible for writing the secondary prompt.
        checkLeftEdge true

        /// Cursor anchor - position of !anchor when the routine was called
        let mutable anchor = Anchor.Current x.Inset
        /// Length of the output currently rendered on screen.
        let mutable rendered = 0
        /// Input has changed, therefore options cache is invalidated.
        let mutable changed = false
        /// Cache of optionsCache
        let mutable optionsCache = Options()

        let writeBlank() =
            Console.Write(' ')
            checkLeftEdge false
        let writeChar(c) =
            if Console.CursorTop = Console.BufferHeight - 1 && Console.CursorLeft = Console.BufferWidth - 1 then
                //printf "bottom right!\n"
                anchor <- { anchor with top = (anchor).top - 1 }
            checkLeftEdge true
            if (Char.IsControl(c)) then
                let s = x.MapCharacter(c)
                Console.Write(s)
                rendered <- rendered + s.Length
            else
                Console.Write(c)
                rendered <- rendered + 1
            checkLeftEdge true

        /// The console input buffer.
        let input = new StringBuilder()
        /// Current position - index into the input buffer
        let mutable current = 0

        let render() =
            //printf "render\n"
            let curr = current
            anchor.PlaceAt(x.Inset,0)
            let output = new StringBuilder()
            let mutable position = -1
            for i = 0 to input.Length - 1 do
                if (i = curr) then
                    position <- output.Length
                let c = input.Chars(i)
                if (Char.IsControl(c)) then
                    output.Append(x.MapCharacter(c)) |> ignore
                else
                    output.Append(c) |> ignore

            if (curr = input.Length) then
                position <- output.Length

            // render the current text, computing a new value for "rendered"
            let old_rendered = rendered
            rendered <- 0
            for i = 0 to input.Length - 1 do
               writeChar(input.Chars(i))

            // blank out any dangling old text
            for i = rendered to old_rendered - 1 do
                writeBlank()

            anchor.PlaceAt(x.Inset,position)

        render()

        let insertChar(c:char) =
            if (current = input.Length)  then
                current <- current + 1
                input.Append(c) |> ignore
                writeChar(c)
            else
                input.Insert(current, c) |> ignore
                current <- current + 1
                render()

        let insertTab() =
            for i = ReadLineConsole.TabSize - (current % ReadLineConsole.TabSize) downto 1 do
                insertChar(' ')

        let moveLeft() =
            if (current > 0 && (current - 1 < input.Length)) then
                current <- current - 1
                let c = input.Chars(current)
                Cursor.Move(x.Inset, - x.GetCharacterSize(c))

        let moveRight() =
            if (current < input.Length) then
                let c = input.Chars(current)
                current <- current + 1
                Cursor.Move(x.Inset, x.GetCharacterSize(c))
                
        let moveWordLeft() =
            if (current > 0 && (current - 1 < input.Length)) then
                let line = input.ToString()
                current <- Utils.previousWordFromIdx line (current - 1, false)
                anchor.PlaceAt(x.Inset, current)
                
        let moveWordRight() =
            if (current < input.Length) then
                let line = input.ToString()
                let idxToMoveTo = Utils.nextWordFromIdx line (current + 1, false)
                
                // if has reached end of the last word
                if idxToMoveTo = current && current < line.Length
                then current <- line.Length
                else current <- idxToMoveTo
                
                anchor.PlaceAt(x.Inset, current)

        let setInput(line:string) =
            input.Length <- 0
            input.Append(line) |> ignore
            current <- input.Length
            render()

        let tabPress(shift) =
            let  opts,prefix =
                if changed then
                    changed <- false
                    x.GetOptions(input.ToString())
                else
                   optionsCache,false
            optionsCache <- opts

            if (opts.Count > 0) then
                let part =
                    if shift
                    then opts.Previous()
                    else opts.Next()
                setInput(opts.Root + part)
            else
                if (prefix) then
                    Console.Beep()
                else
                    insertTab()

        let delete() =
            if (input.Length > 0 && current < input.Length) then
                input.Remove(current, 1) |> ignore
                render()
                
        let deleteFromStartOfLineToCursor() =
            if (input.Length > 0 && current > 0) then
                input.Remove (0, current) |> ignore
                current <- 0
                render()
            
        let deleteWordLeadingToCursor() =
            if (input.Length > 0 && current > 0) then
                let line = input.ToString()
                let idx = Utils.previousWordFromIdx line (current - 1, false)
                input.Remove(idx, current - idx) |> ignore
                current <- idx
                render()

        let deleteToEndOfLine() =
            if (current < input.Length) then
                input.Remove (current, input.Length - current) |> ignore
                render()

        let insert(key: ConsoleKeyInfo) =
            // REVIEW: is this F6 rewrite required? 0x1A looks like Ctrl-Z.
            // REVIEW: the Ctrl-Z code is not recognised as EOF by the lexer.
            // REVIEW: looks like a relic of the port of readline, which is currently removable.
            let c = if (key.Key = ConsoleKey.F6) then '\x1A' else key.KeyChar
            let c = readKeyFixup c
            insertChar(c)
            
        let backspace() =
            if (input.Length > 0 && current > 0) then
                input.Remove(current - 1, 1) |> ignore
                current <- current - 1
                render()
        let enter() = 
            Console.Write("\n")
            let line = input.ToString()
            if (line = "\x1A") then null
            else 
                if (line.Length > 0) then 
                    history.AddLast(line)
                line
        let clear() = 
            current <- input.Length
            let setPrompt prompt = 
                if prompt then // We only allow clearing if prompt is ">"
                    Console.Clear()
                    Console.Write (x.Prompt)
                    Console.Write(input.ToString())
                    anchor <- Anchor.Top(x.Inset)   
            let previous = history.Previous()
            history.Next() |> ignore
            if previous = "" then
                setPrompt true
            else
                setPrompt (previous.EndsWith(";;"))

            
        let home() =
            current <- 0
            anchor.PlaceAt(x.Inset,0)
        let rec read() = 
            let key = Console.ReadKey true

            match key.Key with
            | ConsoleKey.Backspace ->
                backspace()
                change()
            | ConsoleKey.Delete ->
                delete()
                change()
            | ConsoleKey.Enter ->
                enter()
            | ConsoleKey.Tab ->
                tabPress(key.Modifiers &&& ConsoleModifiers.Shift <> enum 0)
                read()
            | ConsoleKey.UpArrow ->
                setInput(history.Previous())
                change()
            | ConsoleKey.DownArrow ->
                setInput(history.Next())
                change()
            | ConsoleKey.RightArrow when key.Modifiers &&& ConsoleModifiers.Control = enum 0 ->
                moveRight()
                change()
            | ConsoleKey.LeftArrow when key.Modifiers &&& ConsoleModifiers.Control = enum 0 ->
                moveLeft()
                change()
            | ConsoleKey.Escape ->
                setInput String.Empty
                change()
            | ConsoleKey.Home ->
                home()
                change()
            | ConsoleKey.End ->
                current <- input.Length
                anchor.PlaceAt(x.Inset,rendered)
                change()
            | _ ->
            match (key.Modifiers, key.Key) with
            | (ConsoleModifiers.Control, ConsoleKey.A) ->
                home()
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.E) ->
                current <- input.Length
                anchor.PlaceAt(x.Inset, rendered)
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.B) ->
                moveLeft()
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.F) ->
                moveRight()
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.LeftArrow) 
            | (ConsoleModifiers.Alt, ConsoleKey.B) ->
                moveWordLeft()
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.RightArrow) 
            | (ConsoleModifiers.Alt, ConsoleKey.F) ->
                moveWordRight()
                change ()
            | (ConsoleModifiers.Control, ConsoleKey.K) ->
                deleteToEndOfLine()
                change()
            | (ConsoleModifiers.Control,ConsoleKey.P) ->
                setInput(history.Previous())
                change()
            | (ConsoleModifiers.Control, ConsoleKey.N) ->
                setInput(history.Next())
                change()
            | (ConsoleModifiers.Control, ConsoleKey.D) ->
                if (input.Length = 0) then
                    exit 0 //quit
                else
                    delete()
                    change()
            | (ConsoleModifiers.Control, ConsoleKey.L) ->
                clear()
                change()
            | (ConsoleModifiers.Control, ConsoleKey.U) ->
                deleteFromStartOfLineToCursor()
                change()
            | (ConsoleModifiers.Control, ConsoleKey.W) ->
                deleteWordLeadingToCursor()
                change()
            | _ ->
                // Note: If KeyChar=0, the not a proper char, e.g. it could be part of a multi key-press character,
                //       e.g. e-acute is ' and e with the French (Belgium) IME and US Intl KB.
                // Here: skip KeyChar=0 (except for F6 which maps to 0x1A (ctrl-Z?)).
                if key.KeyChar <> '\000' || key.Key = ConsoleKey.F6 then
                  insert(key)
                  change()
                else
                  // Skip and read again.
                  read()

        and change() =
           changed <- true
           read()
        read()
