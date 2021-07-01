// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Interactive

open System
open System.Text
open System.Collections.Generic


type internal Style = Prompt | Out | Error

/// Class managing the command History.
type internal History() =
    let list  = new List<string>()
    let mutable current  = 0 

    member x.Count = list.Count
    member x.Current = 
        if current >= 0 && current < list.Count then list.[current] else String.Empty

    member x.Clear() = list.Clear(); current <- -1
    member x.Add line = 
        match line with 
        | Null | "" -> ()
        | _ -> list.Add(line)

    member x.AddLast line = 
        match line with 
        | Null | "" -> ()
        | _ -> list.Add(line); current <- list.Count

    // Dead code   
    // member x.First() = current <- 0; x.Current
    // member x.Last() = current <- list.Count - 1; x.Current;

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

    let guard(f) = f() 

    // Quick and dirty dirty method lookup for inlined IL
    // In some situations, we can't use ldtoken to obtain a RuntimeMethodHandle, since the method
    // in question's token may contain typars from an external type environment.  Such a token would
    // cause the PE file to be flagged as invalid.
    // In such a situation, we'll want to search out the MethodRef in a similar fashion to bindMethodBySearch
    // but since we can't use ldtoken to obtain System.Type objects, we'll need to do everything with strings.
    // This is the least fool-proof method for resolving the binding, but since the scenarios it's used in are
    // so constrained, (fsi 2.0, methods with generic multi-dimensional arrays in their signatures), it's 
    // acceptable
    let findMethod (parentT:Type,nm,marity,argtys : string [],rty : string) =
        let staticOrInstanceBindingFlags = BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.DeclaredOnly
        let methInfos = parentT.GetMethods(staticOrInstanceBindingFlags) |> Array.toList 

        let methInfos = methInfos |> List.filter (fun methInfo -> methInfo.Name = nm)
        match methInfos with 
        | [methInfo] -> 
            methInfo
        | _ -> 
            let select (methInfo:MethodInfo) =
                let mtyargTIs = if methInfo.IsGenericMethod then methInfo.GetGenericArguments() else [| |] 
                if mtyargTIs.Length  <> marity then false else

                let haveArgTs = 
                    let parameters = Array.toList (methInfo.GetParameters()) 
                    parameters |> List.map (fun param -> param.ParameterType) 
                let haveResT  = methInfo.ReturnType 

                if argtys.Length <> haveArgTs.Length then false else
                let res = rty :: (Array.toList argtys) = (List.map (fun (t : System.Type) -> t.Name) (haveResT::haveArgTs))
                res

            match List.tryFind select methInfos with
            | None          -> failwith "Internal Error: cannot bind to method"
            | Some methInfo -> methInfo        

[<Sealed>]
type internal Cursor =
    static member ResetTo(top,left) = 
        Utils.guard(fun () -> 
           Console.CursorTop <- min top (Console.BufferHeight - 1);
           Console.CursorLeft <- left)
    static member Move(inset, delta) =
        let position = Console.CursorTop * (Console.BufferWidth - inset) + (Console.CursorLeft - inset) + delta
        let top  = position / (Console.BufferWidth - inset)
        let left = inset + position % (Console.BufferWidth - inset)
        Cursor.ResetTo(top,left)
    
type internal Anchor = 
    {top:int; left:int}
    static member Current(inset) = {top=Console.CursorTop;left= max inset Console.CursorLeft}

    member p.PlaceAt(inset, index) =
        //printf "p.top = %d, p.left = %d, inset = %d, index = %d\n" p.top p.left inset index;
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

        let name = input.Substring(start, input.Length - start);
        if (name.Trim().Length > 0) then
            let lastDot = name.LastIndexOf('.');
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
                 // engine.Evaluate(String.Format("dir({0})", attr)) as IEnumerable;
                optionsCache.Root <-root;
            with e ->
                optionsCache.Clear();
            optionsCache,true;
        else 
            optionsCache,false;

    member x.MapCharacter(c) : string =
        match c with 
        | '\x1A'-> "^Z";
        | _ -> "^?"

    member x.GetCharacterSize(c) =
        if (Char.IsControl(c)) 
        then x.MapCharacter(c).Length
        else 1

    static member TabSize = 4;

    member x.ReadLine() =

        let checkLeftEdge(prompt) = 
            let currLeft = Console.CursorLeft 
            if currLeft < x.Inset then 
                if currLeft = 0 then Console.Write (if prompt then x.Prompt2 else String(' ',x.Inset))
                Utils.guard(fun () -> 
                    Console.CursorTop <- min Console.CursorTop (Console.BufferHeight - 1);
                    Console.CursorLeft <- x.Inset);

        // The caller writes the primary prompt.  If we are reading the 2nd and subsequent lines of the
        // input we're responsible for writing the secondary prompt.
        checkLeftEdge true

        /// Cursor anchor - position of !anchor when the routine was called
        let anchor = ref (Anchor.Current(x.Inset));
        /// Length of the output currently rendered on screen.
        let rendered = ref 0
        /// Input has changed, therefore options cache is invalidated.
        let changed = ref false
        /// Cache of optionsCache
        let optionsCache = ref (new Options())
        
        let writeBlank() = 
            Console.Write(' ');
            checkLeftEdge false
        let writeChar(c) = 
            if Console.CursorTop = Console.BufferHeight - 1 && Console.CursorLeft = Console.BufferWidth - 1 then 
                //printf "bottom right!\n";
                anchor := { !anchor with top = (!anchor).top - 1 };
            checkLeftEdge true
            if (Char.IsControl(c)) then
                let s = x.MapCharacter(c)
                Console.Write(s);
                rendered := !rendered + s.Length;
            else 
                Console.Write(c);
                rendered := !rendered + 1;
            checkLeftEdge true

        /// The console input buffer.
        let input = new StringBuilder() 
        /// Current position - index into the input buffer
        let current = ref 0;

        let render() = 
            //printf "render\n";
            let curr = !current
            (!anchor).PlaceAt(x.Inset,0);
            let output = new StringBuilder()
            let mutable position = -1
            for i = 0 to input.Length - 1 do 
                if (i = curr) then
                    position <- output.Length
                let c = input.Chars(i)
                if (Char.IsControl(c)) then
                    output.Append(x.MapCharacter(c)) |> ignore;
                else 
                    output.Append(c) |> ignore;

            if (curr = input.Length) then
                position <- output.Length;

            // render the current text, computing a new value for "rendered"
            let old_rendered = !rendered 
            rendered := 0;
            for i = 0 to input.Length - 1 do 
               writeChar(input.Chars(i));

            // blank out any dangling old text
            for i = !rendered to old_rendered - 1 do 
                writeBlank();

            (!anchor).PlaceAt(x.Inset,position);

        render();

        let insertChar(c:char) =
            if (!current = input.Length)  then 
                current := !current + 1;
                input.Append(c) |> ignore;
                writeChar(c)
            else
                input.Insert(!current, c) |> ignore;
                current := !current + 1;
                render();
        
        let insertTab() = 
            for i = ReadLineConsole.TabSize - (!current % ReadLineConsole.TabSize) downto 1 do
                insertChar(' ')
                
        let moveLeft() = 
            if (!current > 0 && (!current - 1 < input.Length)) then
                current := !current - 1
                let c = input.Chars(!current)
                Cursor.Move(x.Inset, - x.GetCharacterSize(c))
        
        let moveRight() = 
            if (!current < input.Length) then
                let c = input.Chars(!current);
                current := !current + 1;
                Cursor.Move(x.Inset, x.GetCharacterSize(c));

        let setInput(line:string) =
            input.Length <- 0;
            input.Append(line) |> ignore;
            current := input.Length;
            render()

        let tabPress(shift) = 
            let  opts,prefix = 
                if !changed then
                    changed := false;
                    x.GetOptions(input.ToString());
                else
                   !optionsCache,false
            optionsCache := opts;
            
            if (opts.Count > 0) then
                let part = 
                    if shift
                    then opts.Previous() 
                    else opts.Next();
                setInput(opts.Root + part);
            else 
                if (prefix) then
                    Console.Beep();
                else 
                    insertTab();

        let delete() = 
            if (input.Length > 0 && !current < input.Length) then
                input.Remove(!current, 1) |> ignore;
                render();
        
        let deleteToEndOfLine() =
            if (!current < input.Length) then
                input.Remove (!current, input.Length - !current) |> ignore;
                render();

        let insert(key: ConsoleKeyInfo) =
            // REVIEW: is this F6 rewrite required? 0x1A looks like Ctrl-Z.
            // REVIEW: the Ctrl-Z code is not recognised as EOF by the lexer.
            // REVIEW: looks like a relic of the port of readline, which is currently removable.
            let c = if (key.Key = ConsoleKey.F6) then '\x1A' else key.KeyChar
            insertChar(c);
            
        let backspace() =                 
            if (input.Length > 0 && !current > 0) then
                input.Remove(!current - 1, 1) |> ignore;
                current := !current - 1;
                render();
         
        let enter() = 
            Console.Write("\n");
            let line = input.ToString();
            if (line = "\x1A") then null
            else 
                if (line.Length > 0) then 
                    history.AddLast(line);
                line;
        
        let rec read() = 
            let key = Console.ReadKey true

            match (key.Key) with
            | ConsoleKey.Backspace ->  
                backspace();
                change() 
            | ConsoleKey.Delete -> 
                delete();
                change() 
            | ConsoleKey.Enter -> 
                enter()
            | ConsoleKey.Tab -> 
                tabPress(key.Modifiers &&& ConsoleModifiers.Shift <> enum 0);
                read()
            | ConsoleKey.UpArrow ->
                setInput(history.Previous());
                change()
            | ConsoleKey.DownArrow ->
                setInput(history.Next());
                change()
            | ConsoleKey.RightArrow ->
                moveRight()
                change()
            | ConsoleKey.LeftArrow ->
                moveLeft()
                change()
            | ConsoleKey.Escape ->
                setInput(String.Empty);
                change()
            | ConsoleKey.Home ->
                current := 0;
                (!anchor).PlaceAt(x.Inset,0)
                change()
            | ConsoleKey.End ->
                current := input.Length;
                (!anchor).PlaceAt(x.Inset,!rendered);
                change()
            | _ ->
            match (key.Modifiers, key.KeyChar) with
            // Control-A
            | (ConsoleModifiers.Control, '\001') ->
                current := 0;
                (!anchor).PlaceAt(x.Inset,0)
                change ()
            // Control-E
            | (ConsoleModifiers.Control, '\005') ->
                current := input.Length;
                (!anchor).PlaceAt(x.Inset,!rendered)
                change ()
            // Control-B
            | (ConsoleModifiers.Control, '\002') ->
                moveLeft()
                change ()
            // Control-f
            | (ConsoleModifiers.Control, '\006') ->
                moveRight()
                change ()
            // Control-k delete to end of line
            | (ConsoleModifiers.Control, '\011') ->
                deleteToEndOfLine()
                change()
            // Control-P
            | (ConsoleModifiers.Control, '\016') ->
                setInput(history.Previous());
                change()
            // Control-n
            | (ConsoleModifiers.Control, '\014') ->
                setInput(history.Next());
                change()
            // Control-d
            | (ConsoleModifiers.Control, '\004') ->
                if (input.Length = 0) then
                    exit 0 //quit
                else
                    delete()
                    change()
            | _ ->
                // Note: If KeyChar=0, the not a proper char, e.g. it could be part of a multi key-press character,
                //       e.g. e-acute is ' and e with the French (Belgium) IME and US Intl KB.
                // Here: skip KeyChar=0 (except for F6 which maps to 0x1A (ctrl-Z?)).
                if key.KeyChar <> '\000' || key.Key = ConsoleKey.F6 then
                  insert(key);
                  change()
                else
                  // Skip and read again.
                  read()

        and change() = 
           changed := true;
           read() 
        read()
