// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Core

open System
open System.IO
open System.Text

open System.Collections.Concurrent
open System.Globalization
open System.Reflection

open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.Operators
open Microsoft.FSharp.Collections

open LanguagePrimitives.IntrinsicOperators

type PrintfFormat<'Printer, 'State, 'Residue, 'Result>(value:string, captures: obj[], captureTys: Type[]) =
        
    new (value) = new PrintfFormat<'Printer, 'State, 'Residue, 'Result>(value, null, null) 

    member _.Value = value

    member _.Captures = captures

    member _.CaptureTypes = captureTys

    override _.ToString() = value
    
type PrintfFormat<'Printer, 'State, 'Residue, 'Result, 'Tuple>(value:string, captures, captureTys: Type[]) = 

    inherit PrintfFormat<'Printer, 'State, 'Residue, 'Result>(value, captures, captureTys)

    new (value) = new PrintfFormat<'Printer, 'State, 'Residue, 'Result, 'Tuple>(value, null, null)

type Format<'Printer, 'State, 'Residue, 'Result> = PrintfFormat<'Printer, 'State, 'Residue, 'Result>

type Format<'Printer, 'State, 'Residue, 'Result, 'Tuple> = PrintfFormat<'Printer, 'State, 'Residue, 'Result, 'Tuple>

[<AutoOpen>]
module internal PrintfImpl =

    /// Basic idea of implementation:
    /// Every Printf.* family should returns curried function that collects arguments and then somehow prints them.
    /// Idea - instead of building functions on fly argument by argument we instead introduce some predefined parts and then construct functions from these parts
    /// Parts include:
    /// Plain ones:
    /// 1. Final pieces (1..5) - set of functions with arguments number 1..5. 
    /// Primary characteristic - these functions produce final result of the *printf* operation
    /// 2. Chained pieces (1..5) - set of functions with arguments number 1..5. 
    /// Primary characteristic - these functions doesn not produce final result by itself, instead they tailed with some another piece (chained or final).
    /// Plain parts correspond to simple format specifiers (that are projected to just one parameter of the function, say %d or %s). However we also have 
    /// format specifiers that can be projected to more than one argument (i.e %a, %t or any simple format specified with * width or precision). 
    /// For them we add special cases (both chained and final to denote that they can either return value themselves or continue with some other piece)
    /// These primitives allow us to construct curried functions with arbitrary signatures.
    /// For example: 
    /// - function that corresponds to %s%s%s%s%s (string -> string -> string -> string -> string -> T) will be represented by one piece final 5.
    /// - function that has more that 5 arguments will include chained parts: %s%s%s%s%s%d%s  => chained2 -> final 5
    /// Primary benefits: 
    /// 1. creating specialized version of any part requires only one reflection call. This means that we can handle up to 5 simple format specifiers
    /// with just one reflection call
    /// 2. we can make combinable parts independent from particular printf implementation. Thus final result can be cached and shared. 
    /// i.e when first call to printf "%s %s" will trigger creation of the specialization. Subsequent calls will pick existing specialization
    
    [<Flags>]
    type FormatFlags = 
        | None = 0
        | LeftJustify = 1
        | PadWithZeros = 2
        | PlusForPositives = 4
        | SpaceForPositives = 8

    let inline hasFlag flags (expected: FormatFlags) = (flags &&& expected) = expected
    let inline isLeftJustify flags = hasFlag flags FormatFlags.LeftJustify
    let inline isPadWithZeros flags = hasFlag flags FormatFlags.PadWithZeros
    let inline isPlusForPositives flags = hasFlag flags FormatFlags.PlusForPositives
    let inline isSpaceForPositives flags = hasFlag flags FormatFlags.SpaceForPositives

    /// Used for width and precision to denote that user has specified '*' flag
    [<Literal>]
    let StarValue = -1
    /// Used for width and precision to denote that corresponding value was omitted in format string
    [<Literal>]
    let NotSpecifiedValue = -2

    [<System.Diagnostics.DebuggerDisplayAttribute("{ToString()}")>]
    [<NoComparison; NoEquality>]
    type FormatSpecifier =
        {
            TypeChar: char
            Precision: int
            Width: int
            Flags: FormatFlags
            InteropHoleDotNetFormat: string voption
        }
        member spec.IsStarPrecision = (spec.Precision = StarValue)

        member spec.IsPrecisionSpecified = (spec.Precision <> NotSpecifiedValue)

        member spec.IsStarWidth = (spec.Width = StarValue)

        member spec.IsWidthSpecified = (spec.Width <> NotSpecifiedValue)

        member spec.ArgCount = 
            let n = 
                if spec.TypeChar = 'a' then 2 
                elif spec.IsStarWidth || spec.IsStarPrecision then
                    if spec.IsStarWidth = spec.IsStarPrecision then 3 
                    else 2
                else 1

            let n = if spec.TypeChar = '%' then n - 1 else n
                
            assert (n <> 0)

            n

        override spec.ToString() = 
            let valueOf n = match n with StarValue -> "*" | NotSpecifiedValue -> "-" | n -> n.ToString()
            System.String.Format
                (
                    "'{0}', Precision={1}, Width={2}, Flags={3}", 
                    spec.TypeChar, 
                    (valueOf spec.Precision),
                    (valueOf spec.Width), 
                    spec.Flags
                )

        member spec.IsDecimalFormat = 
            spec.TypeChar = 'M'

        member spec.GetPadAndPrefix allowZeroPadding = 
            let padChar = if allowZeroPadding && isPadWithZeros spec.Flags then '0' else ' ';
            let prefix = 
                if isPlusForPositives spec.Flags then "+" 
                elif isSpaceForPositives spec.Flags then " "
                else ""
            padChar, prefix    

        member spec.IsGFormat = 
            spec.IsDecimalFormat || System.Char.ToLower(spec.TypeChar) = 'g'

    
    /// Set of helpers to parse format string
    module private FormatString =

        let intFromString (s: string) (i: byref<int>) =
            let mutable res = 0
            while (Char.IsDigit s.[i]) do
                let n = int s.[i] - int '0'
                res <- res * 10 + n
                i <- i + 1
            res

        let parseFlags (s: string) (i: byref<int>) = 
            let mutable flags = FormatFlags.None
            let mutable fin = false
            while not fin do
                match s.[i] with
                | '0' -> 
                    flags <- flags ||| FormatFlags.PadWithZeros
                    i <- i + 1
                | '+' -> 
                    flags <- flags ||| FormatFlags.PlusForPositives
                    i <- i + 1
                | ' ' -> 
                    flags <- flags ||| FormatFlags.SpaceForPositives
                    i <- i + 1
                | '-' -> 
                    flags <- flags ||| FormatFlags.LeftJustify
                    i <- i + 1
                | _ ->
                    fin <- true
            flags

        let parseWidth (s: string) (i: byref<int>) = 
            if s.[i] = '*' then 
                i <- i + 1
                StarValue
            elif Char.IsDigit s.[i] then
                intFromString s (&i)
            else 
                NotSpecifiedValue

        let parsePrecision (s: string) (i: byref<int>) = 
            if s.[i] = '.' then
                if s.[i + 1] = '*' then 
                    i <- i + 2
                    StarValue
                elif Char.IsDigit s.[i + 1] then
                    i <- i + 1
                    intFromString s (&i)
                else raise (ArgumentException("invalid precision value"))
            else 
                NotSpecifiedValue
        
        let parseTypeChar (s: string) (i: byref<int>) = 
            let res = s.[i]
            i <- i + 1
            res

        let parseInterpolatedHoleDotNetFormat typeChar (s: string) (i: byref<int>) =
            if typeChar = 'P' then 
                if i < s.Length && s.[i] = '(' then  
                     let i2 = s.IndexOf(")", i)
                     if i2 = -1 then 
                         ValueNone
                     else 
                         let res = s.[i+1..i2-1]
                         i <- i2+1
                         ValueSome res
                else
                    ValueNone
            else
                ValueNone

        // Skip %P() added for hole in "...%d{x}..."
        let skipInterpolationHole typeChar (fmt: string) (i: byref<int>) =
            if typeChar <> 'P' then 
              if i+1 < fmt.Length && fmt.[i] = '%' && fmt.[i+1] = 'P'  then
                i <- i + 2
                if i+1 < fmt.Length && fmt.[i] = '('  && fmt.[i+1] = ')' then 
                    i <- i+2
    
        let findNextFormatSpecifier (s: string) (i: byref<int>) = 
            let buf = StringBuilder()
            let mutable fin = false
            while not fin do 
                if i >= s.Length then 
                    fin <- true
                else
                    let c = s.[i]
                    if c = '%' then
                        if i + 1 < s.Length then
                            let mutable i2 = i+1
                            let _ = parseFlags s &i2
                            let w = parseWidth s &i2
                            let p = parsePrecision s &i2
                            let typeChar = parseTypeChar s &i2

                            // shortcut for the simpliest case
                            // if typeChar is not % or it has star as width\precision - resort to long path
                            if typeChar = '%' && not (w = StarValue || p = StarValue) then 
                                buf.Append('%') |> ignore
                                i <- i2
                            else 
                                fin <- true
                        else
                            raise (ArgumentException("Missing format specifier"))
                    else 
                        buf.Append c |> ignore
                        i <- i + 1
            buf.ToString()

    [<NoComparison; NoEquality>]
    /// Represents one step in the execution of a format string
    type Step =
        | StepWithArg of prefix: string * conv: (obj -> string) 
        | StepWithTypedArg of prefix: string * conv: (obj -> Type -> string) 
        | StepString of prefix: string 
        | StepLittleT of prefix: string 
        | StepLittleA of prefix: string
        | StepStar1 of prefix: string * conv: (obj -> int -> string) 
        | StepPercentStar1 of prefix: string
        | StepStar2 of prefix: string * conv: (obj -> int -> int -> string)
        | StepPercentStar2 of prefix: string

        // Count the number of string fragments in a sequence of steps
        static member BlockCount(steps: Step[]) =
            let mutable count = 0
            for step in steps do 
                match step with 
                | StepWithArg (prefix, _conv) ->
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepWithTypedArg (prefix, _conv) ->
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepString prefix ->
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                | StepLittleT(prefix) -> 
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepLittleA(prefix) -> 
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepStar1(prefix, _conv) -> 
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepPercentStar1(prefix) ->
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepStar2(prefix, _conv) -> 
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
                | StepPercentStar2(prefix) -> 
                    if not (String.IsNullOrEmpty prefix) then count <- count + 1
                    count <- count + 1
            count

    /// Abstracts generated printer from the details of particular environment: how to write text, how to produce results etc...
    [<AbstractClass>]
    type PrintfEnv<'State, 'Residue, 'Result>(state: 'State) =
        member _.State = state

        abstract Finish: unit -> 'Result

        abstract Write: string -> unit
        
        /// Write the result of a '%t' format.  If this is a string it is written. If it is a 'unit' value
        /// the side effect has already happened
        abstract WriteT: 'Residue -> unit

        member env.WriteSkipEmpty(s: string) = 
            if not (String.IsNullOrEmpty s) then 
                env.Write s
    
        member env.RunSteps (args: obj[], argTys: Type[], steps: Step[]) =
            let mutable argIndex = 0
            let mutable tyIndex = 0

            for step in steps do 
                match step with 
                | StepWithArg (prefix, conv) ->
                    env.WriteSkipEmpty prefix
                    let arg = args.[argIndex]
                    argIndex <- argIndex + 1
                    env.Write(conv arg)

                | StepWithTypedArg (prefix, conv) ->
                    env.WriteSkipEmpty prefix
                    let arg = args.[argIndex]
                    let argTy = argTys.[tyIndex]
                    argIndex <- argIndex + 1
                    tyIndex <- tyIndex + 1
                    env.Write(conv arg argTy)

                | StepString prefix ->
                    env.WriteSkipEmpty prefix

                | StepLittleT(prefix) -> 
                    env.WriteSkipEmpty prefix
                    let farg = args.[argIndex]
                    argIndex <- argIndex + 1
                    let f = farg :?> ('State -> 'Residue)
                    env.WriteT(f env.State)

                | StepLittleA(prefix) -> 
                    env.WriteSkipEmpty prefix
                    let farg = args.[argIndex]
                    argIndex <- argIndex + 1
                    let arg = args.[argIndex]
                    argIndex <- argIndex + 1
                    let f = farg :?> ('State -> obj -> 'Residue)
                    env.WriteT(f env.State arg)

                | StepStar1(prefix, conv) -> 
                    env.WriteSkipEmpty prefix
                    let star1 = args.[argIndex] :?> int
                    argIndex <- argIndex + 1
                    let arg1 = args.[argIndex]
                    argIndex <- argIndex + 1
                    env.Write (conv arg1 star1)
       
                | StepPercentStar1(prefix) ->
                    argIndex <- argIndex + 1
                    env.WriteSkipEmpty prefix
                    env.Write("%")

                | StepStar2(prefix, conv) -> 
                    env.WriteSkipEmpty prefix
                    let star1 = args.[argIndex] :?> int
                    argIndex <- argIndex + 1
                    let star2 = args.[argIndex] :?> int
                    argIndex <- argIndex + 1
                    let arg1 = args.[argIndex]
                    argIndex <- argIndex + 1
                    env.Write (conv arg1 star1 star2)

                | StepPercentStar2(prefix) -> 
                    env.WriteSkipEmpty prefix
                    argIndex <- argIndex + 2
                    env.Write("%")
    
            env.Finish()

    /// Type of results produced by specialization.
    ///
    /// This is a function that accepts a thunk to create PrintfEnv on demand (at the very last
    /// appliction of an argument) and returns a concrete instance of an appriate curried printer.
    ///
    /// After all arguments are collected, specialization obtains concrete PrintfEnv from the thunk
    /// and uses it to output collected data.
    ///
    /// Note the arguments must be captured in an *immutable* collection.  For example consider
    ///    let f1 = printf "%d%d%d" 3 // activation captures '3'  (args --> [3])
    ///    let f2 = f1 4  // same activation captures 4 (args --> [3;4])
    ///    let f3 = f1 5  // same activation captures 5 (args --> [3;5])
    ///    f2 7           // same activation captures 7 (args --> [3;4;7])
    ///    f3 8           // same activation captures 8 (args --> [3;5;8])
    ///
    /// If we captured into an mutable array then these would interfere 
    type PrintfInitial<'State, 'Residue, 'Result> = (unit -> PrintfEnv<'State, 'Residue, 'Result>)
    type PrintfFuncFactory<'Printer, 'State, 'Residue, 'Result> = 
        delegate of obj list * PrintfInitial<'State, 'Residue, 'Result> -> 'Printer

    [<Literal>]
    let MaxArgumentsInSpecialization = 3

    let revToArray extra (args: 'T list) = 
        // We've reached the end, now fill in the array, reversing steps, avoiding reallocating
        let n = args.Length
        let res = Array.zeroCreate (n+extra)
        let mutable j = 0
        for arg in args do
            res.[n-j-1] <- arg
            j <- j + 1
        res

    type Specializations<'State, 'Residue, 'Result>() =
     
        static member Final0(allSteps) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                let env = initial()
                env.RunSteps(revToArray 0 args, null, allSteps)
            )

        static member CaptureFinal1<'A>(allSteps) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) ->
                    let env = initial()
                    let argArray = revToArray 1 args
                    argArray.[argArray.Length-1] <- box arg1
                    env.RunSteps(argArray, null, allSteps)
                )
            )

        static member CaptureFinal2<'A, 'B>(allSteps) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) (arg2: 'B) ->
                    let env = initial()
                    let argArray = revToArray 2 args
                    argArray.[argArray.Length-1] <- box arg2
                    argArray.[argArray.Length-2] <- box arg1
                    env.RunSteps(argArray, null, allSteps)
                )
            )

        static member CaptureFinal3<'A, 'B, 'C>(allSteps) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) (arg2: 'B) (arg3: 'C) ->
                    let env = initial()
                    let argArray = revToArray 3 args
                    argArray.[argArray.Length-1] <- box arg3
                    argArray.[argArray.Length-2] <- box arg2
                    argArray.[argArray.Length-3] <- box arg1
                    env.RunSteps(argArray, null, allSteps)
                )
            )

        static member Capture1<'A, 'Tail>(next: PrintfFuncFactory<_, 'State, 'Residue, 'Result>) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) ->
                    let args = (box arg1 :: args)
                    next.Invoke(args, initial) : 'Tail
                )
            )

        static member CaptureLittleA<'A, 'Tail>(next: PrintfFuncFactory<_, 'State, 'Residue, 'Result>) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (f: 'State -> 'A -> 'Residue) (arg1: 'A) ->
                    let args = box arg1 :: box (fun s (arg:obj) -> f s (unbox arg)) :: args
                    next.Invoke(args, initial) : 'Tail
                )
            )

        static member Capture2<'A, 'B, 'Tail>(next: PrintfFuncFactory<_, 'State, 'Residue, 'Result>) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) (arg2: 'B) ->
                    let args = box arg2 :: box arg1 :: args
                    next.Invoke(args, initial) : 'Tail
                )
            )

        static member Capture3<'A, 'B, 'C, 'Tail>(next: PrintfFuncFactory<_, 'State, 'Residue, 'Result>) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun args initial -> 
                (fun (arg1: 'A) (arg2: 'B) (arg3: 'C) ->
                    let args = box arg3 :: box arg2 :: box arg1 :: args
                    next.Invoke(args, initial) : 'Tail
                )
            )

        // Special case for format strings containing just one '%d' etc, i.e. StepWithArg then StepString.
        // This avoids allocating an argument array, and unfolds the single iteration of RunSteps.
        static member OneStepWithArg<'A>(prefix1, conv1, prefix2) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun _args initial -> 
                // Note this is the actual computed/stored closure for 
                //     sprintf "prefix1 %d prefix2"
                // for any simple format specifiers, where conv1 and conv2 will depend on the format specifiers etc.
                (fun (arg1: 'A) ->
                    let env = initial()
                    env.WriteSkipEmpty prefix1
                    env.Write(conv1 (box arg1))
                    env.WriteSkipEmpty prefix2
                    env.Finish())
            )

        // Special case for format strings containing two simple formats like '%d %s' etc, i.e. 
        ///StepWithArg then StepWithArg then StepString. This avoids allocating an argument array, 
        // and unfolds the two iteration of RunSteps.
        static member TwoStepWithArg<'A, 'B>(prefix1, conv1, prefix2, conv2, prefix3) =
            PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun _args initial -> 
                // Note this is the actual computed/stored closure for 
                //     sprintf "prefix1 %d prefix2 %s prefix3"
                // for any simple format specifiers, where conv1 and conv2 will depend on the format specifiers etc.
                (fun (arg1: 'A) (arg2: 'B) ->
                    let env = initial()
                    env.WriteSkipEmpty prefix1
                    env.Write(conv1 (box arg1))
                    env.WriteSkipEmpty prefix2
                    env.Write(conv2 (box arg2))
                    env.WriteSkipEmpty prefix3
                    env.Finish())
            )

    let inline (===) a b = Object.ReferenceEquals(a, b)

    let inline boolToString v = if v then "true" else "false"

    let inline stringToSafeString v = 
        match v with
        | null -> ""
        | _ -> v

    [<Literal>]
    let DefaultPrecision = 6

    /// A wrapper struct used to slightly strengthen the types of "ValueConverter" objects produced during composition of
    /// the dynamic implementation.  These are always functions but sometimes they take one argument, sometimes two.
    [<Struct; NoEquality; NoComparison>]
    type ValueConverter private (f: obj) =
        member x.FuncObj = f

        static member inline Make (f: obj -> string) = ValueConverter(box f)
        static member inline Make (f: obj -> int -> string) = ValueConverter(box f)
        static member inline Make (f: obj -> int-> int -> string) = ValueConverter(box f)

    let getFormatForFloat (ch: char) (prec: int) = ch.ToString() +  prec.ToString()

    let normalizePrecision prec = min (max prec 0) 99

    /// Contains helpers to convert printer functions to functions that prints value with respect to specified justification
    /// There are two kinds to printers: 
    /// 'T -> string - converts value to string - used for strings, basic integers etc..
    /// string -> 'T -> string - converts value to string with given format string - used by numbers with floating point, typically precision is set via format string 
    /// To support both categories there are two entry points:
    /// - withPadding - adapts first category
    /// - withPaddingFormatted - adapts second category
    module Padding = 

        /// pad here is function that converts T to string with respect of justification
        /// basic - function that converts T to string without applying justification rules
        /// adaptPaddedFormatted returns boxed function that has various number of arguments depending on if width\precision flags has '*' value 
        let adaptPaddedFormatted (spec: FormatSpecifier) getFormat (basic: string -> obj -> string) (pad: string -> int -> obj -> string) : ValueConverter =
            if spec.IsStarWidth then
                if spec.IsStarPrecision then
                    // width=*, prec=*
                    ValueConverter.Make (fun v width prec -> 
                        let fmt = getFormat (normalizePrecision prec)
                        pad fmt width v)
                else 
                    // width=*, prec=?
                    let prec = if spec.IsPrecisionSpecified then normalizePrecision spec.Precision else DefaultPrecision
                    let fmt = getFormat prec
                    ValueConverter.Make (fun v width -> 
                        pad fmt width v)

            elif spec.IsStarPrecision then
                if spec.IsWidthSpecified then
                    // width=val, prec=*
                    ValueConverter.Make (fun v prec -> 
                        let fmt = getFormat prec
                        pad fmt spec.Width v)
                else
                    // width=X, prec=*
                    ValueConverter.Make (fun v prec -> 
                        let fmt = getFormat prec
                        basic fmt v)                        
            else
                let prec = if spec.IsPrecisionSpecified then normalizePrecision spec.Precision else DefaultPrecision
                let fmt = getFormat prec
                if spec.IsWidthSpecified then
                    // width=val, prec=*
                    ValueConverter.Make (fun v -> 
                        pad fmt spec.Width v)
                else
                    // width=X, prec=*
                    ValueConverter.Make (fun v -> 
                        basic fmt v)

        /// pad here is function that converts T to string with respect of justification
        /// basic - function that converts T to string without applying justification rules
        /// adaptPadded returns boxed function that has various number of arguments depending on if width flags has '*' value 
        let adaptPadded (spec: FormatSpecifier) (basic: obj -> string) (pad: int -> obj -> string) : ValueConverter = 
            if spec.IsStarWidth then
                // width=*, prec=?
                ValueConverter.Make (fun v width -> 
                    pad width v)
            else
                if spec.IsWidthSpecified then
                    // width=val, prec=*
                    ValueConverter.Make (fun v -> 
                        pad spec.Width v)
                else
                    // width=X, prec=*
                    ValueConverter.Make (fun v -> 
                        basic v)

        let withPaddingFormatted (spec: FormatSpecifier) getFormat  (defaultFormat: string) (f: string ->  obj -> string) left right : ValueConverter =
            if not (spec.IsWidthSpecified || spec.IsPrecisionSpecified) then
                ValueConverter.Make (f defaultFormat)
            else
                if isLeftJustify spec.Flags then
                    adaptPaddedFormatted spec getFormat f left
                else
                    adaptPaddedFormatted spec getFormat f right

        let withPadding (spec: FormatSpecifier) (f: obj -> string) left right : ValueConverter =
            if not spec.IsWidthSpecified then
                ValueConverter.Make f
            else
                if isLeftJustify spec.Flags then
                    adaptPadded spec f left
                else
                    adaptPadded  spec f right

    /// Contains functions to handle left/right justifications for non-numeric types (strings/bools)
    module Basic =
        let leftJustify (f: obj -> string) padChar = 
            fun (w: int) v -> 
                (f v).PadRight(w, padChar)
    
        let rightJustify (f: obj -> string) padChar = 
            fun (w: int) v -> 
                (f v).PadLeft(w, padChar)
    
        let withPadding (spec: FormatSpecifier) f =
            let padChar, _ = spec.GetPadAndPrefix false 
            Padding.withPadding spec f (leftJustify f padChar) (rightJustify f padChar)
    
    /// Contains functions to handle left/right and no justification case for numbers
    module GenericNumber =

        let isPositive (n: obj) = 
            match n with 
            | :? int8 as n -> n >= 0y
            | :? uint8 -> true
            | :? int16 as n -> n >= 0s
            | :? uint16 -> true
            | :? int32 as n -> n >= 0
            | :? uint32 -> true
            | :? int64 as n -> n >= 0L
            | :? uint64 -> true
            | :? nativeint as n -> n >= 0n
            | :? unativeint -> true
            | :? single as n -> n >= 0.0f
            | :? double as n -> n >= 0.0
            | :? decimal as n -> n >= 0.0M
            | _ -> failwith "isPositive: unreachable"

        /// handles right justification when pad char = '0'
        /// this case can be tricky:
        /// - negative numbers, -7 should be printed as '-007', not '00-7'
        /// - positive numbers when prefix for positives is set: 7 should be '+007', not '00+7'
        let rightJustifyWithZeroAsPadChar (str: string) isNumber isPositive w (prefixForPositives: string) =
            System.Diagnostics.Debug.Assert(prefixForPositives.Length = 0 || prefixForPositives.Length = 1)
            if isNumber then
                if isPositive then
                    prefixForPositives + (if w = 0 then str else str.PadLeft(w - prefixForPositives.Length, '0')) // save space to 
                else
                    if str.[0] = '-' then
                        let str = str.Substring 1
                        "-" + (if w = 0 then str else str.PadLeft(w - 1, '0'))
                    else
                        str.PadLeft(w, '0')
            else
                str.PadLeft(w, ' ')
        
        /// handler right justification when pad char = ' '
        let rightJustifyWithSpaceAsPadChar (str: string) isNumber isPositive w (prefixForPositives: string) =
            System.Diagnostics.Debug.Assert(prefixForPositives.Length = 0 || prefixForPositives.Length = 1)
            (if isNumber && isPositive then prefixForPositives + str else str).PadLeft(w, ' ')
        
        /// handles left justification with formatting with 'G'\'g' - either for decimals or with 'g'\'G' is explicitly set 
        let leftJustifyWithGFormat (str: string) isNumber isInteger isPositive w (prefixForPositives: string) padChar  =
            if isNumber then
                let str = if isPositive then prefixForPositives + str else str
                // NOTE: difference - for 'g' format we use isInt check to detect situations when '5.0' is printed as '5'
                // in this case we need to override padding and always use ' ', otherwise we'll produce incorrect results
                if isInteger then
                    str.PadRight(w, ' ') // don't pad integer numbers with '0' when 'g' format is specified (may yield incorrect results)
                else
                    str.PadRight(w, padChar) // non-integer => string representation has point => can pad with any character
            else
                str.PadRight(w, ' ') // pad NaNs with ' '

        let leftJustifyWithNonGFormat (str: string) isNumber isPositive w (prefixForPositives: string) padChar  =
            if isNumber then
                let str = if isPositive then prefixForPositives + str else str
                str.PadRight(w, padChar)
            else
                str.PadRight(w, ' ') // pad NaNs with ' ' 
        
        /// processes given string based depending on values isNumber\isPositive
        let noJustificationCore (str: string) isNumber isPositive prefixForPositives = 
            if isNumber && isPositive then prefixForPositives + str
            else str
        
        /// noJustification handler for f: 'T -> string - basic integer types
        let noJustification (f: obj -> string) (prefix: string) isUnsigned =
            if isUnsigned then
                fun (v: obj) -> noJustificationCore (f v) true true prefix
            else 
                fun (v: obj) -> noJustificationCore (f v) true (isPositive v) prefix

    /// contains functions to handle left/right and no justification case for numbers
    module Integer =
    
        let eliminateNative (v: obj) = 
            match v with
            | :? nativeint as n ->
                if IntPtr.Size = 4 then box (n.ToInt32())
                else box (n.ToInt64())
            | :? unativeint as n ->
                if IntPtr.Size = 4 then box (uint32 (n.ToUInt32()))
                else box (uint64 (n.ToUInt64()))
            | _ -> v

        let rec toString (v: obj) =
            match v with
            | :? int32 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? int64 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? sbyte as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? byte as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? int16 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? uint16 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? uint32 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? uint64 as n -> n.ToString(CultureInfo.InvariantCulture)
            | :? nativeint | :? unativeint -> toString (eliminateNative v)
            | _ -> failwith "toString: unreachable"

        let rec toFormattedString fmt (v: obj) = 
            match v with
            | :? int32 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? int64 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? sbyte as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? byte as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? int16 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? uint16 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? uint32 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? uint64 as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? nativeint | :? unativeint -> toFormattedString fmt (eliminateNative v)
            | _ -> failwith "toFormattedString: unreachable"

        let rec toUnsigned (v: obj) = 
            match v with
            | :? int32 as n -> box (uint32 n)
            | :? int64 as n -> box (uint64 n)
            | :? sbyte as n -> box (byte n)
            | :? int16 as n -> box (uint16 n)
            | :? nativeint | :? unativeint -> toUnsigned (eliminateNative v)
            | _ -> v

        /// Left justification handler for f: 'T -> string - basic integer types
        let leftJustify isGFormat (f: obj -> string) (prefix: string) padChar isUnsigned = 
            if isUnsigned then
                if isGFormat then
                    fun (w: int) (v: obj) ->
                        GenericNumber.leftJustifyWithGFormat (f v) true true true w prefix padChar
                else
                    fun (w: int) (v: obj) ->
                        GenericNumber.leftJustifyWithNonGFormat (f v) true true w prefix padChar
            else
                if isGFormat then
                    fun (w: int) (v: obj) ->
                        GenericNumber.leftJustifyWithGFormat (f v) true true (GenericNumber.isPositive v) w prefix padChar
                else
                    fun (w: int) (v: obj) ->
                        GenericNumber.leftJustifyWithNonGFormat (f v) true (GenericNumber.isPositive v) w prefix padChar
        
        /// Right justification handler for f: 'T -> string - basic integer types
        let rightJustify f (prefixForPositives: string) padChar isUnsigned =
            if isUnsigned then
                if padChar = '0' then
                    fun (w: int) (v: obj) ->
                        GenericNumber.rightJustifyWithZeroAsPadChar (f v) true true w prefixForPositives
                else
                    System.Diagnostics.Debug.Assert((padChar = ' '))
                    fun (w: int) (v: obj) ->
                        GenericNumber.rightJustifyWithSpaceAsPadChar (f v) true true w prefixForPositives
            else
                if padChar = '0' then
                    fun (w: int) (v: obj) ->
                        GenericNumber.rightJustifyWithZeroAsPadChar (f v) true (GenericNumber.isPositive v) w prefixForPositives

                else
                    System.Diagnostics.Debug.Assert((padChar = ' '))
                    fun (w: int) v ->
                        GenericNumber.rightJustifyWithSpaceAsPadChar (f v) true (GenericNumber.isPositive v) w prefixForPositives

        /// Computes a new function from 'f' that wraps the basic conversion given
        /// by 'f' with padding for 0, spacing and justification, if the flags specify
        /// it.  If they don't, f is made into a value converter
        let withPadding (spec: FormatSpecifier) isUnsigned (f: obj -> string)  =
            let allowZeroPadding = not (isLeftJustify spec.Flags) || spec.IsDecimalFormat
            let padChar, prefix = spec.GetPadAndPrefix allowZeroPadding
            Padding.withPadding spec
                (GenericNumber.noJustification f prefix isUnsigned)
                (leftJustify spec.IsGFormat f prefix padChar isUnsigned)
                (rightJustify f prefix padChar isUnsigned)

        let getValueConverter (spec: FormatSpecifier) : ValueConverter =
            match spec.TypeChar with
            | 'd' | 'i' ->
                withPadding spec false toString
            | 'u' ->
                withPadding spec true  (toUnsigned >> toString) 
            | 'x' ->
                withPadding spec true (toFormattedString "x")
            | 'X' ->
                withPadding spec true (toFormattedString "X")
            | 'o' ->
                withPadding spec true (fun (v: obj) ->
                    // Convert.ToInt64 throws for uint64 with values above int64 range so cast directly
                    match toUnsigned v with 
                    | :? uint64 as u -> Convert.ToString(int64 u, 8)
                    | u -> Convert.ToString(Convert.ToInt64 u, 8))
            | 'B' ->
                withPadding spec true (fun (v: obj) ->
                    match toUnsigned v with 
                    | :? uint64 as u -> Convert.ToString(int64 u, 2)
                    | u -> Convert.ToString(Convert.ToInt64 u, 2))
            | _ -> invalidArg (nameof spec) "Invalid integer format"
    
    module FloatAndDecimal = 

        let rec toFormattedString fmt (v: obj) = 
            match v with
            | :? single as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? double as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | :? decimal as n -> n.ToString(fmt, CultureInfo.InvariantCulture)
            | _ -> failwith "toFormattedString: unreachable"

        let isNumber (x: obj) =
            match x with
            | :? single as x -> 
                not (Single.IsPositiveInfinity(x)) &&
                not (Single.IsNegativeInfinity(x)) &&
                not (Single.IsNaN(x))
            | :? double as x -> 
                not (Double.IsPositiveInfinity(x)) &&
                not (Double.IsNegativeInfinity(x)) &&
                not (Double.IsNaN(x))
            | :? decimal -> true
            | _ -> failwith "isNumber: unreachable"

        let isInteger (n: obj) = 
            match n with 
            | :? single as n -> n % 1.0f = 0.0f
            | :? double as n -> n % 1. = 0.
            | :? decimal as n -> n % 1.0M = 0.0M
            | _ -> failwith "isInteger: unreachable"

        let noJustification (prefixForPositives: string) = 
            fun (fmt: string) (v: obj) -> 
                GenericNumber.noJustificationCore (toFormattedString fmt v) (isNumber v) (GenericNumber.isPositive v) prefixForPositives
    
        let leftJustify isGFormat (prefix: string) padChar = 
            if isGFormat then
                fun (fmt: string) (w: int) (v: obj) ->
                    GenericNumber.leftJustifyWithGFormat (toFormattedString fmt v) (isNumber v) (isInteger v) (GenericNumber.isPositive v) w prefix padChar
            else
                fun (fmt: string) (w: int) (v: obj) ->
                    GenericNumber.leftJustifyWithNonGFormat (toFormattedString fmt v) (isNumber v) (GenericNumber.isPositive v) w prefix padChar  

        let rightJustify (prefixForPositives: string) padChar =
            if padChar = '0' then
                fun (fmt: string) (w: int) (v: obj) ->
                    GenericNumber.rightJustifyWithZeroAsPadChar (toFormattedString fmt v) (isNumber v) (GenericNumber.isPositive v) w prefixForPositives
            else
                System.Diagnostics.Debug.Assert((padChar = ' '))
                fun (fmt: string) (w: int) (v: obj) ->
                    GenericNumber.rightJustifyWithSpaceAsPadChar (toFormattedString fmt v) (isNumber v) (GenericNumber.isPositive v) w prefixForPositives

        let withPadding (spec: FormatSpecifier) getFormat defaultFormat =
            let padChar, prefix = spec.GetPadAndPrefix true 
            Padding.withPaddingFormatted spec getFormat defaultFormat
                (noJustification prefix)
                (leftJustify spec.IsGFormat prefix padChar)
                (rightJustify prefix padChar)

    type ObjectPrinter = 

        static member ObjectToString(spec: FormatSpecifier) : ValueConverter = 
            Basic.withPadding spec (fun (v: obj) ->
                match v with
                | null -> "<null>"
                | x -> x.ToString())
        
        /// Convert an interpoland to a string
        static member InterpolandToString(spec: FormatSpecifier) : ValueConverter = 
            let fmt = 
                match spec.InteropHoleDotNetFormat with 
                | ValueNone -> null
                | ValueSome fmt -> "{0:" + fmt + "}"
            Basic.withPadding spec (fun (vobj: obj) ->
                match vobj with
                | null -> ""
                | x -> 
                    match fmt with 
                    | null -> x.ToString()
                    | fmt -> String.Format(fmt, x))
        
        static member GenericToStringCore(v: 'T, opts: Microsoft.FSharp.Text.StructuredPrintfImpl.FormatOptions, bindingFlags) = 
            let vty = 
                match box v with
                | null -> typeof<'T>
                | _ -> v.GetType()
            Microsoft.FSharp.Text.StructuredPrintfImpl.Display.anyToStringForPrintf opts bindingFlags (v, vty)

        static member GenericToString<'T>(spec: FormatSpecifier) : ValueConverter = 
            let bindingFlags = 
                if isPlusForPositives spec.Flags then BindingFlags.Public ||| BindingFlags.NonPublic
                else BindingFlags.Public 

            let useZeroWidth = isPadWithZeros spec.Flags
            let opts = 
                let o = Microsoft.FSharp.Text.StructuredPrintfImpl.FormatOptions.Default
                let o =
                    if useZeroWidth then { o with PrintWidth = 0} 
                    elif spec.IsWidthSpecified then { o with PrintWidth = spec.Width}
                    else o
                if spec.IsPrecisionSpecified then { o with PrintSize = spec.Precision}
                else o

            match spec.IsStarWidth, spec.IsStarPrecision with
            | true, true ->
                ValueConverter.Make (fun (vobj: obj) (width: int) (prec: int) ->
                    let v = unbox<'T> vobj
                    let opts = { opts with PrintSize = prec }
                    let opts  = if not useZeroWidth then { opts with PrintWidth = width} else opts
                    ObjectPrinter.GenericToStringCore(v, opts, bindingFlags)
                    )

            | true, false ->
                ValueConverter.Make (fun (vobj: obj) (width: int) ->
                    let v = unbox<'T> vobj
                    let opts  = if not useZeroWidth then { opts with PrintWidth = width} else opts
                    ObjectPrinter.GenericToStringCore(v, opts, bindingFlags))

            | false, true ->
                ValueConverter.Make (fun (vobj: obj) (prec: int) ->
                    let v = unbox<'T> vobj
                    let opts = { opts with PrintSize = prec }
                    ObjectPrinter.GenericToStringCore(v, opts, bindingFlags) )

            | false, false ->
                ValueConverter.Make (fun (vobj: obj) ->
                    let v = unbox<'T> vobj
                    ObjectPrinter.GenericToStringCore(v, opts, bindingFlags))
        
    let basicFloatToString spec = 
        let defaultFormat = getFormatForFloat spec.TypeChar DefaultPrecision
        FloatAndDecimal.withPadding spec (getFormatForFloat spec.TypeChar) defaultFormat

    let private NonPublicStatics = BindingFlags.NonPublic ||| BindingFlags.Static

    let mi_GenericToString = typeof<ObjectPrinter>.GetMethod("GenericToString", NonPublicStatics)

    let private getValueConverter (ty: Type) (spec: FormatSpecifier) : ValueConverter = 
        match spec.TypeChar with
        | 'b' ->  
            Basic.withPadding spec (unbox >> boolToString)
        | 's' ->
            Basic.withPadding spec (unbox >> stringToSafeString)
        | 'c' ->
            Basic.withPadding spec (fun (c: obj) -> (unbox<char> c).ToString())
        | 'M'  ->
            FloatAndDecimal.withPadding spec (fun _ -> "G") "G" // %M ignores precision
        | 'd' | 'i' | 'u' | 'B' | 'o' | 'x' | 'X' -> 
            Integer.getValueConverter spec
        | 'e' | 'E' 
        | 'f' | 'F' 
        | 'g' | 'G' -> 
            basicFloatToString spec
        | 'A' ->
            let mi = mi_GenericToString.MakeGenericMethod ty
            mi.Invoke(null, [| box spec |]) |> unbox
        | 'O' -> 
            ObjectPrinter.ObjectToString(spec) 
        | 'P' -> 
            ObjectPrinter.InterpolandToString(spec) 
        | _ -> 
            raise (ArgumentException(SR.GetString(SR.printfBadFormatSpecifier)))
    
    let extractCurriedArguments (ty: Type) n = 
        System.Diagnostics.Debug.Assert(n = 1 || n = 2 || n = 3, "n = 1 || n = 2 || n = 3")
        let buf = Array.zeroCreate n
        let rec go (ty: Type) i = 
            if i < n then
                match ty.GetGenericArguments() with
                | [| argTy; retTy|] ->
                    buf.[i] <- argTy
                    go retTy (i + 1)
                | _ -> failwith (String.Format("Expected function with {0} arguments", n))
            else 
                System.Diagnostics.Debug.Assert((i = n), "i = n")
                (buf, ty)
        go ty 0    


    type LargeStringPrintfEnv<'Result>(continuation, blockSize) = 
        inherit PrintfEnv<unit, string, 'Result>(())
        let buf: string[] = Array.zeroCreate blockSize
        let mutable ptr = 0

        override _.Finish() : 'Result = continuation (String.Concat buf)

        override _.Write(s: string) = 
            buf.[ptr] <- s
            ptr <- ptr + 1

        override x.WriteT s = x.Write(s)

    type SmallStringPrintfEnv2() = 
        inherit PrintfEnv<unit, string, string>(())
        let mutable c = null

        override _.Finish() : string = if isNull c then "" else c
        override _.Write(s: string) = if isNull c then c <- s else c <- c + s
        override x.WriteT s = x.Write(s)

    type SmallStringPrintfEnv4() = 
        inherit PrintfEnv<unit, string, string>(())
        let mutable s1 : string = null
        let mutable s2 : string = null
        let mutable s3 : string = null
        let mutable s4 : string = null

        override _.Finish() : string = String.Concat(s1, s2, s3, s4)
        override _.Write(s: string) =
            if isNull s1 then s1 <- s 
            elif isNull s2 then s2 <- s 
            elif isNull s3 then s3 <- s 
            else s4 <- s
        override x.WriteT s = x.Write(s)

    let StringPrintfEnv blockSize = 
        if blockSize <= 2 then
            SmallStringPrintfEnv2() :> PrintfEnv<_,_,_>
        elif blockSize <= 4 then
            SmallStringPrintfEnv4() :> PrintfEnv<_,_,_>
        else
            LargeStringPrintfEnv(id, blockSize) :> PrintfEnv<_,_,_>

    let StringBuilderPrintfEnv<'Result>(k, buf) = 
        { new PrintfEnv<Text.StringBuilder, unit, 'Result>(buf) with
            override _.Finish() : 'Result = k ()
            override _.Write(s: string) = ignore(buf.Append s)
            override _.WriteT(()) = () }

    let TextWriterPrintfEnv<'Result>(k, tw: IO.TextWriter) =
        { new PrintfEnv<IO.TextWriter, unit, 'Result>(tw) with 
            override _.Finish() : 'Result = k()
            override _.Write(s: string) = tw.Write s
            override _.WriteT(()) = () }

    let MAX_CAPTURE = 3

    /// Parses format string and creates resulting step list and printer factory function.
    [<AllowNullLiteral>]
    type FormatParser<'Printer, 'State, 'Residue, 'Result>(fmt: string) =
    
        let buildCaptureFunc (spec: FormatSpecifier, allSteps, argTys: Type[], retTy, nextInfo) = 
            let (next:obj, nextCanCombine: bool, nextArgTys: Type[], nextRetTy, nextNextOpt) = nextInfo
            assert (argTys.Length > 0)

            // See if we can compress a capture to a multi-capture
            //     CaptureN + Final --> CaptureFinalN
            //     Capture1 + Capture1 --> Capture2
            //     Capture1 + Capture2 --> Capture3
            //     Capture2 + Capture1 --> Capture3
            match argTys.Length, nextArgTys.Length with 
            |  _ when spec.TypeChar = 'a' ->
                // %a has an existential type which must be converted to obj
                assert (argTys.Length = 2)
                let captureMethName = "CaptureLittleA" 
                let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                let mi = mi.MakeGenericMethod([| argTys.[1]; retTy |])
                let factoryObj = mi.Invoke(null, [| next  |])
                factoryObj, false, argTys, retTy, None

            | n1, n2 when nextCanCombine && n1 + n2 <= MAX_CAPTURE ->
                // 'next' is thrown away on this path and replaced by a combined Capture
                let captureCount = n1 + n2
                let combinedArgTys = Array.append argTys nextArgTys
                match nextNextOpt with 
                | None ->
                    let captureMethName = "CaptureFinal" + string captureCount
                    let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                    let mi = mi.MakeGenericMethod(combinedArgTys)
                    let factoryObj = mi.Invoke(null, [| allSteps |])
                    factoryObj, true, combinedArgTys, nextRetTy, None
                | Some nextNext ->
                    let captureMethName = "Capture" + string captureCount
                    let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                    let mi = mi.MakeGenericMethod(Array.append combinedArgTys [| nextRetTy |])
                    let factoryObj = mi.Invoke(null, [| nextNext |])
                    factoryObj, true, combinedArgTys, nextRetTy, nextNextOpt

            | captureCount, _ ->
                let captureMethName = "Capture" + string captureCount
                let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                let mi = mi.MakeGenericMethod(Array.append argTys [| retTy |])
                let factoryObj = mi.Invoke(null, [| next  |])
                factoryObj, true, argTys, retTy, Some next

        let buildStep (spec: FormatSpecifier) (argTys: Type[]) prefix = 
            if spec.TypeChar = 'a' then
                StepLittleA prefix
            elif spec.TypeChar = 't' then
                StepLittleT prefix
            elif spec.IsStarPrecision || spec.IsStarWidth then
                let isTwoStar = (spec.IsStarWidth = spec.IsStarPrecision)
                match isTwoStar, spec.TypeChar with 
                | false, '%' -> StepPercentStar1 prefix
                | true, '%' -> StepPercentStar2 prefix
                | _ ->
                    // For curried interpolated string format processing, the static types of the '%A' arguments 
                    // are provided via the argument typed extracted from the curried function. They are known on first phase.
                    let argTy = match argTys with null -> typeof<obj> | _ -> argTys.[argTys.Length - 1]
                    let conv = getValueConverter argTy spec 
                    if isTwoStar then 
                        let convFunc = conv.FuncObj :?> (obj -> int -> int -> string)
                        StepStar2 (prefix, convFunc)
                    else
                        let convFunc = conv.FuncObj :?> (obj -> int -> string)
                        StepStar1 (prefix, convFunc)
            else
                // For interpolated string format processing, the static types of the '%A' arguments 
                // are provided via CaptureTypes and are only known on second phase.
                match argTys with
                | null when spec.TypeChar = 'A' ->
                    let convFunc arg argTy = 
                        let mi = mi_GenericToString.MakeGenericMethod [| argTy |]
                        let f = mi.Invoke(null, [| box spec |]) :?> ValueConverter
                        let f2 = f.FuncObj :?> (obj -> string)
                        f2 arg

                    StepWithTypedArg (prefix, convFunc)

                | _ -> 
                    // For curried interpolated string format processing, the static types of the '%A' arguments 
                    // are provided via the argument typed extracted from the curried function. They are known on first phase.
                    let argTy = match argTys with null -> typeof<obj> | _ -> argTys.[0]
                    let conv = getValueConverter argTy spec
                    let convFunc = conv.FuncObj :?> (obj -> string)
                    StepWithArg (prefix, convFunc)
            
        let parseSpec (i: byref<int>) = 
            i <- i + 1
            let flags = FormatString.parseFlags fmt &i
            let width = FormatString.parseWidth fmt &i
            let precision = FormatString.parsePrecision fmt &i
            let typeChar = FormatString.parseTypeChar fmt &i
            let interpHoleDotnetFormat = FormatString.parseInterpolatedHoleDotNetFormat typeChar fmt &i

            // Skip %P insertion points added after %d{...} etc. in interpolated strings
            FormatString.skipInterpolationHole typeChar fmt &i

            let spec = 
                { TypeChar = typeChar
                  Precision = precision
                  Flags = flags
                  Width = width
                  InteropHoleDotNetFormat = interpHoleDotnetFormat }
            spec
            
        // The steps, populated on-demand. This is for the case where the string is being used
        // with interpolands captured in the Format object, including the %A capture types.
        //
        // We may initialize this twice, but the assignment is atomic and the computation will give functionally
        // identical results each time, so it is ok.
        let mutable stepsForCapturedFormat = Unchecked.defaultof<_>

        // The function factory, populated on-demand, for the case where the string is being used to make a curried function for printf.
        //
        // We may initialize this twice, but the assignment is atomic and the computation will give functionally
        // identical results each time, so it is ok.
        let mutable factory = Unchecked.defaultof<PrintfFuncFactory<'Printer, 'State, 'Residue, 'Result>>
        let mutable printer = Unchecked.defaultof<'Printer>

        // The function factory, populated on-demand.
        //
        // We may initialize this twice, but the assignment is atomic and the computation will give functionally
        // identical results each time, so it is ok.
        let mutable stringCount = 0

        // A simplified parser. For the case where the string is being used with interpolands captured in the Format object. 
        let rec parseAndCreateStepsForCapturedFormatAux steps (prefix: string) (i: byref<int>) = 
            if i >= fmt.Length then 
                let step = StepString(prefix)
                let allSteps = revToArray 1 steps
                allSteps.[allSteps.Length-1] <- step
                stringCount <- Step.BlockCount allSteps
                stepsForCapturedFormat <- allSteps
            else
                let spec = parseSpec &i
                let suffix = FormatString.findNextFormatSpecifier fmt &i
                let step = buildStep spec null prefix
                parseAndCreateStepsForCapturedFormatAux (step::steps) suffix &i

        let parseAndCreateStepsForCapturedFormat () =
            let mutable i = 0
            let prefix = FormatString.findNextFormatSpecifier fmt &i
            parseAndCreateStepsForCapturedFormatAux [] prefix &i

        /// The more advanced parser which both builds the steps (with %A types extracted from the funcTy),
        /// and produces a curried function value of the right type guided by funcTy
        let rec parseAndCreateFuncFactoryAux steps (prefix: string) (funcTy: Type) (i: byref<int>) = 
            
            if i >= fmt.Length then 
                let step = StepString(prefix)
                let allSteps = revToArray 1 steps
                allSteps.[allSteps.Length-1] <- step
                let last = Specializations<'State, 'Residue, 'Result>.Final0(allSteps)
                stringCount <- Step.BlockCount allSteps
                let nextInfo = (box last, true, [| |], funcTy, None)
                (allSteps, nextInfo)
            else
                assert (fmt.[i] = '%')
                let spec = parseSpec &i
                let suffix = FormatString.findNextFormatSpecifier fmt &i
                let n = spec.ArgCount
                let (argTys, retTy) =  extractCurriedArguments funcTy n
                let step = buildStep spec argTys prefix
                let (allSteps, nextInfo) = parseAndCreateFuncFactoryAux (step::steps) suffix retTy &i
                let nextInfoNew = buildCaptureFunc (spec, allSteps, argTys, retTy, nextInfo)
                (allSteps, nextInfoNew)

        let parseAndCreateFunctionFactory () =
            let funcTy = typeof<'Printer>

            // Find the first format specifier
            let mutable i = 0
            let prefix = FormatString.findNextFormatSpecifier fmt &i
            
            let (allSteps, (factoryObj, _, combinedArgTys, _, _)) = parseAndCreateFuncFactoryAux [] prefix funcTy &i
            
            // If there are no format specifiers then take a simple path
            match allSteps with 
            | [| StepString prefix |] ->
                PrintfFuncFactory<_, 'State, 'Residue, 'Result>(fun _args initial -> 
                    let env = initial()
                    env.WriteSkipEmpty prefix
                    env.Finish()
                ) |> box

            // If there is one simple format specifier then we can create an even better factory function
            | [| StepWithArg (prefix1, conv1); StepString prefix2 |] ->
                let captureMethName = "OneStepWithArg" 
                let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                let mi = mi.MakeGenericMethod(combinedArgTys)
                let factoryObj = mi.Invoke(null, [| box prefix1; box conv1; box prefix2  |])
                factoryObj

            // If there are two simple format specifiers then we can create an even better factory function
            | [| StepWithArg (prefix1, conv1); StepWithArg (prefix2, conv2); StepString prefix3 |] ->
                let captureMethName = "TwoStepWithArg" 
                let mi = typeof<Specializations<'State, 'Residue, 'Result>>.GetMethod(captureMethName, NonPublicStatics)
                let mi = mi.MakeGenericMethod(combinedArgTys)
                let factoryObj = mi.Invoke(null, [| box prefix1; box conv1; box prefix2; box conv2; box prefix3 |])
                factoryObj

            | _ -> 
                factoryObj

        /// The format string, used to help identify the cache entry (the cache index types are taken
        /// into account as well).
        member _.FormatString = fmt

        /// The steps involved in executing the format string when interpolands are captured
        ///
        /// If %A patterns are involved these steps are only accurate when the %A capture types
        /// are given in the format string through interpolation capture.
        member _.GetStepsForCapturedFormat() =
            match stepsForCapturedFormat with
            | null -> parseAndCreateStepsForCapturedFormat () 
            | _ -> ()
            stepsForCapturedFormat

        /// The number of strings produced for a sprintf
        member _.BlockCount = stringCount
            
        /// The factory function used to generate the result or the resulting function.  
        member _.GetCurriedPrinterFactory() =
            match box factory with
            | null -> 
                let factoryObj = parseAndCreateFunctionFactory () 
                let p = (factoryObj :?> PrintfFuncFactory<'Printer, 'State, 'Residue, 'Result>)
                // We may initialize this twice, but the assignment is atomic and the computation will give functionally
                // identical results each time it is ok
                factory <- p
                p
            | _ -> factory

        /// This avoids reallocation and application of 'initial' for sprintf printers
        member this.GetCurriedStringPrinter() =
            match box printer with
            | null -> 
                let f = this.GetCurriedPrinterFactory()
                let initial() = (StringPrintfEnv stringCount |> box :?> PrintfEnv<'State, 'Residue, 'Result>)
                let p = f.Invoke([], initial)
                // We may initialize this twice, but the assignment is atomic and the computation will give functionally
                // identical results each time it is ok
                printer <- p
                p
            | _ -> printer


    /// 2-level cache, keyed by format string and index types
    type Cache<'Printer, 'State, 'Residue, 'Result>() =

        /// 1st level cache (type-indexed). Stores last value that was consumed by the current thread in
        /// thread-static field thus providing shortcuts for scenarios when printf is called in tight loop.
        [<DefaultValue; ThreadStatic>]
        static val mutable private mostRecent: FormatParser<'Printer, 'State, 'Residue, 'Result>
    
        // 2nd level cache (type-indexed). Dictionary that maps format string to the corresponding cache entry
        static let mutable dict : ConcurrentDictionary<string, FormatParser<'Printer, 'State, 'Residue, 'Result>> = null

        static member GetParser(format: Format<'Printer, 'State, 'Residue, 'Result>) =
            let recent = Cache<'Printer, 'State, 'Residue, 'Result>.mostRecent
            let fmt = format.Value
            if isNull recent then 
                let parser = FormatParser(fmt)
                Cache<'Printer, 'State, 'Residue, 'Result>.mostRecent <- parser
                parser
            elif fmt.Equals recent.FormatString then 
                recent
            else
                // Initialize the 2nd level cache if necessary.  Note there's a race condition but it doesn't
                // matter if we initialize these values twice (and lose one entry)
                if isNull dict then 
                    dict <- ConcurrentDictionary<_,_>()

                let parser = 
                    match dict.TryGetValue(fmt) with 
                    | true, res -> res
                    | _ -> 
                        let parser = FormatParser(fmt)
                        // There's a race condition - but the computation is functional and it doesn't matter if we do it twice
                        dict.TryAdd(fmt, parser) |> ignore
                        parser
                Cache<'Printer, 'State, 'Residue, 'Result>.mostRecent <- parser
                parser

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Printf =

    type BuilderFormat<'T, 'Result> = Format<'T, StringBuilder, unit, 'Result>
    type StringFormat<'T, 'Result> = Format<'T, unit, string, 'Result>
    type TextWriterFormat<'T, 'Result> = Format<'T, TextWriter, unit, 'Result>
    type BuilderFormat<'T> = BuilderFormat<'T,unit>
    type StringFormat<'T> = StringFormat<'T,string>
    type TextWriterFormat<'T>  = TextWriterFormat<'T,unit>

    let gprintf envf (format: Format<'Printer, 'State, 'Residue, 'Result>) = 
        let cacheItem = Cache.GetParser format
        match format.Captures with 
        | null -> 
            // The ksprintf "...%d ...." arg path, producing a function
            let factory = cacheItem.GetCurriedPrinterFactory()
            let initial() = (envf cacheItem.BlockCount :> PrintfEnv<_,_,_>)
            factory.Invoke([], initial)
        | captures -> 
            // The ksprintf $"...%d{3}...." path, running the steps straight away to produce a string
            let steps = cacheItem.GetStepsForCapturedFormat()
            let env = envf cacheItem.BlockCount :> PrintfEnv<_,_,_>
            let res = env.RunSteps(captures, format.CaptureTypes, steps)
            unbox res // prove 'T = 'Result
            //continuation res
    
    [<CompiledName("PrintFormatToStringThen")>]
    let ksprintf continuation (format: StringFormat<'T, 'Result>) : 'T = 
        gprintf (fun stringCount -> LargeStringPrintfEnv(continuation, stringCount)) format

    [<CompiledName("PrintFormatToStringThen")>]
    let sprintf (format: StringFormat<'T>) =
        // We inline gprintf by hand here to be sure to remove a few allocations
        let cacheItem = Cache.GetParser format
        match format.Captures with 
        | null ->
            // The sprintf "...%d ...." arg path, producing a function
            cacheItem.GetCurriedStringPrinter()
        | captures -> 
            // The sprintf $"...%d{3}...." path, running the steps straight away to produce a string
            let steps = cacheItem.GetStepsForCapturedFormat()
            let env = StringPrintfEnv cacheItem.BlockCount
            let res = env.RunSteps(captures, format.CaptureTypes, steps)
            unbox res // proves 'T = string

    [<CompiledName("PrintFormatThen")>]
    let kprintf continuation format = ksprintf continuation format

    [<CompiledName("PrintFormatToStringBuilderThen")>]
    let kbprintf continuation (builder: StringBuilder) (format: BuilderFormat<'T, 'Result>) : 'T = 
        gprintf (fun _stringCount -> StringBuilderPrintfEnv(continuation, builder)) format
    
    [<CompiledName("PrintFormatToTextWriterThen")>]
    let kfprintf continuation textWriter (format: TextWriterFormat<'T, 'Result>) =
        gprintf (fun _stringCount -> TextWriterPrintfEnv(continuation, textWriter)) format

    [<CompiledName("PrintFormatToStringBuilder")>]
    let bprintf builder format =
        kbprintf ignore builder format 

    [<CompiledName("PrintFormatToTextWriter")>]
    let fprintf (textWriter: TextWriter) format =
        kfprintf ignore textWriter format 

    [<CompiledName("PrintFormatLineToTextWriter")>]
    let fprintfn (textWriter: TextWriter) format =
        kfprintf (fun _ -> textWriter.WriteLine()) textWriter format

    [<CompiledName("PrintFormatToStringThenFail")>]
    let failwithf format =
        ksprintf failwith format

    [<CompiledName("PrintFormat")>]
    let printf format =
        fprintf Console.Out format

    [<CompiledName("PrintFormatToError")>]
    let eprintf format =
        fprintf Console.Error format

    [<CompiledName("PrintFormatLine")>]
    let printfn format =
        fprintfn Console.Out format

    [<CompiledName("PrintFormatLineToError")>]
    let eprintfn format =
        fprintfn Console.Error format
