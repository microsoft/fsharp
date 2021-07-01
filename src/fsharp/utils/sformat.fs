// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// This file is compiled twice in the codebase
//    - as the internal implementation of printf '%A' formatting in FSharp.Core
//    - as the implementation of structured formatting in the compiler, F# Interactive and FSharp.Compiler.Service.
//
// The one implementation file is used because we keep the implementations of
// structured formatting the same for fsi.exe and '%A' printing. However F# Interactive has
// a richer feature set.

#nowarn "52" // The value has been copied to ensure the original is not mutated by this operation

#if COMPILER
namespace FSharp.Compiler.Text
#else
// FSharp.Core.dll:
namespace Microsoft.FSharp.Text.StructuredPrintfImpl
#endif

// Breakable block layout implementation.
// This is a fresh implementation of pre-existing ideas.

open System
open System.IO
open System.Reflection
open System.Globalization
open System.Collections.Generic
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Collections

[<StructuralEquality; NoComparison>]
type TextTag =
    | ActivePatternCase
    | ActivePatternResult
    | Alias
    | Class
    | Union
    | UnionCase
    | Delegate
    | Enum
    | Event
    | Field
    | Interface
    | Keyword
    | LineBreak
    | Local
    | Record
    | RecordField
    | Method
    | Member
    | ModuleBinding
    | Function
    | Module
    | Namespace
    | NumericLiteral
    | Operator
    | Parameter
    | Property
    | Space
    | StringLiteral
    | Struct
    | TypeParameter
    | Text
    | Punctuation
    | UnknownType
    | UnknownEntity

type TaggedText(tag: TextTag, text: string) =
    member x.Tag = tag
    member x.Text = text
    override x.ToString() = text + "(tag: " + tag.ToString() + ")"

type TaggedTextWriter =
    abstract Write: t: TaggedText -> unit
    abstract WriteLine: unit -> unit

/// A joint, between 2 layouts, is either:
///  - unbreakable, or
///  - breakable, and if broken the second block has a given indentation.
[<StructuralEquality; NoComparison>]
type Joint =
    | Unbreakable
    | Breakable of indentation: int
    | Broken of indentation: int

/// If either juxtaposition flag is true, then no space between words.
[<NoEquality; NoComparison>]
type Layout =
    | ObjLeaf of juxtLeft: bool * object: obj * juxtRight: bool
    | Leaf of juxtLeft: bool * text: TaggedText * justRight: bool
    | Node of leftLayout: Layout * rightLayout: Layout * joint: Joint
    | Attr of text: string * attributes: (string * string) list * layout: Layout

    member layout.JuxtapositionLeft =
        match layout with
        | ObjLeaf (jl, _, _) -> jl
        | Leaf (jl, _, _) -> jl
        | Node (left, _, _) -> left.JuxtapositionLeft
        | Attr (_, _, subLayout) -> subLayout.JuxtapositionLeft

    static member JuxtapositionMiddle (left: Layout, right: Layout) =
        left.JuxtapositionRight || right.JuxtapositionLeft

    member layout.JuxtapositionRight =
        match layout with
        | ObjLeaf (_, _, jr) -> jr
        | Leaf (_, _, jr) -> jr
        | Node (_, right, _) -> right.JuxtapositionRight
        | Attr (_, _, subLayout) -> subLayout.JuxtapositionRight

[<NoEquality; NoComparison>]
type IEnvironment = 
    abstract GetLayout: obj -> Layout
    abstract MaxColumns: int
    abstract MaxRows: int

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE || NO_CHECKNULLS
[<AutoOpen>]
module NullShim =
    // Shim to match nullness checking library support in preview
    let inline (|Null|NonNull|) (x: 'T) : Choice<unit,'T> = match x with null -> Null | v -> NonNull v
#endif

[<AutoOpen>]
module TaggedText =
    let mkTag tag text = TaggedText(tag, text)

    let length (tt: TaggedText) = tt.Text.Length
    let toText (tt: TaggedText) = tt.Text
    let tagClass name = mkTag TextTag.Class name
    let tagUnionCase t = mkTag TextTag.UnionCase t
    let tagField t = mkTag TextTag.Field t
    let tagNumericLiteral t = mkTag TextTag.NumericLiteral t
    let tagKeyword t = mkTag TextTag.Keyword t
    let tagStringLiteral t = mkTag TextTag.StringLiteral t
    let tagLocal t = mkTag TextTag.Local t
    let tagText t = mkTag TextTag.Text t
    let tagRecordField t = mkTag TextTag.RecordField t
    let tagProperty t = mkTag TextTag.Property t
    let tagMethod t = mkTag TextTag.Method t
    let tagPunctuation t = mkTag TextTag.Punctuation t
    let tagOperator t = mkTag TextTag.Operator t
    let tagSpace t = mkTag TextTag.Space t

    let leftParen = tagPunctuation "("
    let rightParen = tagPunctuation ")"
    let comma = tagPunctuation ","
    let semicolon = tagPunctuation ";"
    let questionMark = tagPunctuation "?"
    let leftBracket = tagPunctuation "["
    let rightBracket = tagPunctuation "]"
    let leftBrace= tagPunctuation "{"
    let rightBrace = tagPunctuation "}"
    let space = tagSpace " "
    let equals = tagOperator "="

#if COMPILER
    let tagAlias t = mkTag TextTag.Alias t
    let keywordFunctions =
        [
            "raise"
            "reraise"
            "typeof"
            "typedefof"
            "sizeof"
            "nameof"
            "char"
            "decimal"
            "double"
            "float"
            "float32"
            "int"
            "int8"
            "int16"
            "int32"
            "int64"
            "sbyte"
            "seq" // 'seq x' when 'x' is a string works, strangely enough
            "single"
            "string"
            "unit"
            "uint"
            "uint8"
            "uint16"
            "uint32"
            "uint64"
            "unativeint"
        ]
        |> Set.ofList
    let tagDelegate t = mkTag TextTag.Delegate t
    let tagEnum t = mkTag TextTag.Enum t
    let tagEvent t = mkTag TextTag.Event t
    let tagInterface t = mkTag TextTag.Interface t
    let tagLineBreak t = mkTag TextTag.LineBreak t
    let tagRecord t = mkTag TextTag.Record t
    let tagModule t = mkTag TextTag.Module t
    let tagModuleBinding name = if keywordFunctions.Contains name then mkTag TextTag.Keyword name else mkTag TextTag.ModuleBinding name
    let tagFunction t = mkTag TextTag.Function t
    let tagNamespace t = mkTag TextTag.Namespace t
    let tagParameter t = mkTag TextTag.Parameter t
    let tagStruct t = mkTag TextTag.Struct t
    let tagTypeParameter t = mkTag TextTag.TypeParameter t
    let tagActivePatternCase t = mkTag TextTag.ActivePatternCase t
    let tagActivePatternResult t = mkTag TextTag.ActivePatternResult t
    let tagUnion t = mkTag TextTag.Union t
    let tagMember t = mkTag TextTag.Member t
    let tagUnknownEntity t = mkTag TextTag.UnknownEntity t
    let tagUnknownType t = mkTag TextTag.UnknownType t

    // common tagged literals
    let lineBreak = tagLineBreak "\n"
    let leftBraceBar = tagPunctuation "{|"
    let rightBraceBar = tagPunctuation "|}"
    let arrow = tagPunctuation "->"
    let dot = tagPunctuation "."
    let leftAngle = tagPunctuation "<"
    let rightAngle = tagPunctuation ">"
    let colon = tagPunctuation ":"
    let minus = tagPunctuation "-"
    let keywordTrue = tagKeyword "true"
    let keywordFalse = tagKeyword "false"
    let structUnit = tagStruct "unit"
    let keywordStatic = tagKeyword "static"
    let keywordMember = tagKeyword "member"
    let keywordVal = tagKeyword "val"
    let keywordEvent = tagKeyword "event"
    let keywordWith = tagKeyword "with"
    let keywordSet = tagKeyword "set"
    let keywordGet = tagKeyword "get"
    let bar = tagPunctuation "|"
    let keywordStruct = tagKeyword "struct"
    let keywordInherit = tagKeyword "inherit"
    let keywordEnd = tagKeyword "end"
    let keywordNested = tagKeyword "nested"
    let keywordType = tagKeyword "type"
    let keywordDelegate = tagKeyword "delegate"
    let keywordOf = tagKeyword "of"
    let keywordInternal = tagKeyword "internal"
    let keywordPrivate = tagKeyword "private"
    let keywordAbstract = tagKeyword "abstract"
    let keywordOverride = tagKeyword "override"
    let keywordEnum = tagKeyword "enum"
    let leftBracketBar = tagPunctuation  "[|"
    let rightBracketBar = tagPunctuation "|]"
    let keywordTypeof = tagKeyword "typeof"
    let keywordTypedefof = tagKeyword "typedefof"
    let leftBracketAngle = tagPunctuation "[<"
    let rightBracketAngle = tagPunctuation ">]"
    let star = tagOperator "*"
    let keywordNew = tagKeyword "new"
#endif     

[<AutoOpen>]
module Layout = 

    // constructors
    let objL (value:obj) = 
        match value with 
        | :? string as s -> Leaf (false, mkTag TextTag.Text s, false)
        | o -> ObjLeaf (false, o, false)

    let wordL text = Leaf (false, text, false)

    let sepL text = Leaf (true , text, true)   

    let rightL text = Leaf (true , text, false)   

    let leftL text = Leaf (false, text, true)

    let emptyL = Leaf (true, mkTag TextTag.Text "", true)

    let isEmptyL layout = 
        match layout with 
        | Leaf(true, s, true) -> s.Text = ""
        | _ -> false

    let mkNode l r joint =
        if isEmptyL l then r else
        if isEmptyL r then l else
        Node(l, r, joint)

    let aboveL layout1 layout2 = mkNode layout1 layout2 (Broken 0)

    let tagAttrL text maps layout = Attr(text, maps, layout)

    let apply2 f l r =
        if isEmptyL l then r
        elif isEmptyL r then l 
        else f l r

    let (^^)  layout1 layout2 = mkNode layout1 layout2 (Unbreakable)

    let (++)  layout1 layout2 = mkNode layout1 layout2 (Breakable 0)

    let (--)  layout1 layout2 = mkNode layout1 layout2 (Breakable 1)

    let (---) layout1 layout2 = mkNode layout1 layout2 (Breakable 2)

    let (----)  layout1 layout2 = mkNode layout1 layout2 (Breakable 3)

    let (-----) layout1 layout2 = mkNode layout1 layout2 (Breakable 4)    

    let (@@) layout1 layout2 = apply2 (fun l r -> mkNode l r (Broken 0)) layout1 layout2

    let (@@-) layout1 layout2 = apply2 (fun l r -> mkNode l r (Broken 1)) layout1 layout2

    let (@@--) layout1 layout2 = apply2 (fun l r -> mkNode l r (Broken 2)) layout1 layout2
    
    let (@@---) layout1 layout2 = apply2 (fun l r -> mkNode l r (Broken 3)) layout1 layout2
        
    let (@@----) layout1 layout2 = apply2 (fun l r -> mkNode l r (Broken 4)) layout1 layout2

    let tagListL tagger els =
        match els with 
        | [] -> emptyL
        | [x] -> x
        | x :: xs ->
            let rec process' prefixL yl =
                match yl with
                | [] -> prefixL
                | y :: ys -> process' (tagger prefixL ++ y) ys
            process' x xs
            
    let commaListL layouts = tagListL (fun prefixL -> prefixL ^^ rightL comma) layouts

    let semiListL layouts = tagListL (fun prefixL -> prefixL ^^ rightL semicolon) layouts

    let spaceListL layouts = tagListL (fun prefixL -> prefixL) layouts

    let sepListL layout1 layouts = tagListL (fun prefixL -> prefixL ^^ layout1) layouts

    let bracketL layout = leftL leftParen ^^ layout ^^ rightL rightParen

    let tupleL layouts = bracketL (sepListL (sepL comma) layouts)

    let aboveListL layouts = 
        match layouts with
        | [] -> emptyL
        | [x] -> x
        | x :: ys -> List.fold (fun pre y -> pre @@ y) x ys

    let optionL selector value = 
        match value with 
        | None -> wordL (tagUnionCase "None")
        | Some x -> wordL (tagUnionCase "Some") -- (selector x)

    let listL selector value =
        leftL leftBracket ^^ sepListL (sepL semicolon) (List.map selector value) ^^ rightL rightBracket

    let squareBracketL layout =
        leftL leftBracket ^^ layout ^^ rightL rightBracket    

    let braceL layout =
        leftL leftBrace ^^ layout ^^ rightL rightBrace

    let boundedUnfoldL
        (itemL: 'a -> Layout)
        (project: 'z -> ('a * 'z) option)
        (stopShort: 'z -> bool)
        (z: 'z)
        maxLength =

        let rec consume n z =
            if stopShort z then [wordL (tagPunctuation "...")] else
            match project z with
            | None -> []  // exhausted input 
            | Some (x, z) -> if n<=0 then [wordL (tagPunctuation "...")]               // hit print_length limit 
                                    else itemL x :: consume (n-1) z  // cons recursive... 
        consume maxLength z  

    let unfoldL selector folder state count =
        boundedUnfoldL selector folder (fun _ -> false) state count
          
/// These are a typical set of options used to control structured formatting.
[<NoEquality; NoComparison>]
type FormatOptions =
    { FloatingPointFormat: string
      AttributeProcessor: (string -> (string * string) list -> bool -> unit)
#if COMPILER // This is the PrintIntercepts extensibility point currently revealed by fsi.exe's AddPrinter
      PrintIntercepts: (IEnvironment -> obj -> Layout option) list
      StringLimit: int
#endif
      FormatProvider: IFormatProvider
      BindingFlags: BindingFlags
      PrintWidth: int
      PrintDepth: int
      PrintLength: int
      PrintSize: int       
      ShowProperties: bool
      ShowIEnumerable: bool
    }

    static member Default =
        { FormatProvider = (CultureInfo.InvariantCulture :> IFormatProvider)
#if COMPILER    // This is the PrintIntercepts extensibility point currently revealed by fsi.exe's AddPrinter
          PrintIntercepts = []
          StringLimit = Int32.MaxValue
#endif
          AttributeProcessor= (fun _ _ _ -> ())
          BindingFlags = BindingFlags.Public
          FloatingPointFormat = "g10"
          PrintWidth = 80
          PrintDepth = 100
          PrintLength = 100
          PrintSize = 10000
          ShowProperties = false
          ShowIEnumerable = true
        }

module ReflectUtils = 

    [<NoEquality; NoComparison>]
    type TypeInfo =
        | TupleType of Type list
        | FunctionType of Type * Type
        | RecordType of (string * Type) list
        | SumType of (string * (string * Type) list) list
        | UnitType
        | ObjectType of Type

    let isNamedType (ty:Type) = not (ty.IsArray || ty.IsByRef || ty.IsPointer)

    let equivHeadTypes (ty1:Type) (ty2:Type) = 
        isNamedType(ty1) &&
        if ty1.IsGenericType then 
            ty2.IsGenericType && (ty1.GetGenericTypeDefinition()).Equals(ty2.GetGenericTypeDefinition())
        else 
            ty1.Equals(ty2)

    let option = typedefof<obj option>

    let func = typedefof<(obj -> obj)>

    let isOptionTy ty = equivHeadTypes ty (typeof<int option>)

    let isUnitType ty = equivHeadTypes ty (typeof<unit>)

    let isListType ty = 
        FSharpType.IsUnion ty && 
        (let cases = FSharpType.GetUnionCases ty 
         cases.Length > 0 && equivHeadTypes (typedefof<list<_>>) cases.[0].DeclaringType)

    [<RequireQualifiedAccess; StructuralComparison; StructuralEquality>]
    type TupleType =
        | Value
        | Reference

    [<NoEquality; NoComparison>]
    type ValueInfo =
        | TupleValue of TupleType * (obj * Type)[]
        | FunctionClosureValue of Type 
        | RecordValue of (string * obj * Type)[]
        | UnionCaseValue of string * (string * (obj * Type))[]
        | ExceptionValue of Type * (string * (obj * Type))[]
        | NullValue
        | UnitValue
        | ObjectValue of obj

    module Value =
        // Analyze an object to see if it the representation
        // of an F# value.
        let GetValueInfoOfObject (bindingFlags: BindingFlags) (obj: obj) =
            match obj with 
            | Null -> NullValue
            | _ -> 
            let reprty = obj.GetType() 

            // First a bunch of special rules for tuples
            // Because of the way F# currently compiles tuple values 
            // of size > 7 we can only reliably reflect on sizes up
            // to 7.

            if FSharpType.IsTuple reprty then 
                let tyArgs = FSharpType.GetTupleElements(reprty)
                let fields = FSharpValue.GetTupleFields obj |> Array.mapi (fun i v -> (v, tyArgs.[i]))
                let tupleType =
                    if reprty.Name.StartsWith "ValueTuple" then TupleType.Value
                    else TupleType.Reference
                TupleValue (tupleType, fields)

            elif FSharpType.IsFunction reprty then 
                FunctionClosureValue reprty
                    
            // It must be exception, abstract, record or union.
            // Either way we assume the only properties defined on
            // the type are the actual fields of the type.  Again,
            // we should be reading attributes here that indicate the
            // true structure of the type, e.g. the order of the fields.   
            elif FSharpType.IsUnion(reprty, bindingFlags) then 
                let tag, vals = FSharpValue.GetUnionFields (obj, reprty, bindingFlags) 
                let props = tag.GetFields()
                let pvals = (props, vals) ||> Array.map2 (fun prop v -> prop.Name, (v, prop.PropertyType))
                UnionCaseValue(tag.Name, pvals)

            elif FSharpType.IsExceptionRepresentation(reprty, bindingFlags) then 
                let props = FSharpType.GetExceptionFields(reprty, bindingFlags) 
                let vals = FSharpValue.GetExceptionFields(obj, bindingFlags) 
                let pvals = (props, vals) ||> Array.map2 (fun prop v -> prop.Name, (v, prop.PropertyType))
                ExceptionValue(reprty, pvals)

            elif FSharpType.IsRecord(reprty, bindingFlags) then 
                let props = FSharpType.GetRecordFields(reprty, bindingFlags) 
                RecordValue(props |> Array.map (fun prop -> prop.Name, prop.GetValue (obj, null), prop.PropertyType))
            else
                ObjectValue(obj)

        // This one is like the above but can make use of additional
        // statically-known type information to aid in the
        // analysis of null values. 

        let GetValueInfo bindingFlags (x: 'a, ty: Type)  (* x could be null *) = 
            let obj = (box x)
            match obj with 
            | Null ->
                let isNullaryUnion =
                    match ty.GetCustomAttributes(typeof<CompilationRepresentationAttribute>, false) with
                    | [|:? CompilationRepresentationAttribute as attr|] -> 
                        (attr.Flags &&& CompilationRepresentationFlags.UseNullAsTrueValue) = CompilationRepresentationFlags.UseNullAsTrueValue
                    | _ -> false
                if isNullaryUnion then
                    let nullaryCase = FSharpType.GetUnionCases ty |> Array.filter (fun uc -> uc.GetFields().Length = 0) |> Array.item 0
                    UnionCaseValue(nullaryCase.Name, [| |])
                elif isUnitType ty then UnitValue
                else NullValue
            | NonNull obj -> 
                GetValueInfoOfObject bindingFlags (obj) 

module Display = 
    open ReflectUtils
    
    let string_of_int (i:int) = i.ToString()

    let typeUsesSystemObjectToString (ty:System.Type) =
        try
            let methInfo = ty.GetMethod("ToString", BindingFlags.Public ||| BindingFlags.Instance, null, [| |], null)
            methInfo.DeclaringType = typeof<System.Object>
        with _e -> false

    let catchExn f = try Choice1Of2 (f ()) with e -> Choice2Of2 e

    // An implementation of break stack.
    // Uses mutable state, relying on linear threading of the state.

    [<NoEquality; NoComparison>]
    type Breaks = 
        Breaks of
            /// pos of next free slot 
            nextFreeSlot: int *     
            /// pos of next possible "outer" break - OR - outer=next if none possible 
            nextOuterBreak: int *     
            /// stack of savings, -ve means it has been broken 
            savingsStack: int[]

    // next  is next slot to push into - aka size of current occupied stack.  
    // outer counts up from 0, and is next slot to break if break forced.
    // - if all breaks forced, then outer=next.
    // - popping under these conditions needs to reduce outer and next.
        
    let chunkN = 400      
    let breaks0 () = Breaks(0, 0, Array.create chunkN 0)

    let pushBreak saving (Breaks(next, outer, stack)) =
        let stack = 
            if next = stack.Length then
                Array.init (next + chunkN) (fun i -> if i < next then stack.[i] else 0) // expand if full 
            else
                stack
           
        stack.[next] <- saving;
        Breaks(next+1, outer, stack)

    let popBreak (Breaks(next, outer, stack)) =
        if next=0 then raise (Failure "popBreak: underflow");
        let topBroke = stack.[next-1] < 0
        let outer = if outer=next then outer-1 else outer  // if all broken, unwind 
        let next = next - 1
        Breaks(next, outer, stack), topBroke

    let forceBreak (Breaks(next, outer, stack)) =
        if outer=next then
            // all broken 
            None
        else
            let saving = stack.[outer]
            stack.[outer] <- -stack.[outer];    
            let outer = outer+1
            Some (Breaks(next, outer, stack), saving)

    /// fitting
    let squashToAux (maxWidth, leafFormatter: _ -> TaggedText) layout =
        let (|ObjToTaggedText|) = leafFormatter
        if maxWidth <= 0 then layout else 
        let rec fit breaks (pos, layout) =
            // breaks = break context, can force to get indentation savings.
            // pos = current position in line
            // layout = to fit
            //------
            // returns:
            // breaks
            // layout - with breaks put in to fit it.
            // pos    - current pos in line = rightmost position of last line of block.
            // offset - width of last line of block
            // NOTE: offset <= pos -- depending on tabbing of last block
               
            let breaks, layout, pos, offset =
                match layout with
                | Attr (tag, attrs, l) ->
                    let breaks, layout, pos, offset = fit breaks (pos, l) 
                    let layout = Attr (tag, attrs, layout) 
                    breaks, layout, pos, offset

                | Leaf (jl, text, jr)
                | ObjLeaf (jl, ObjToTaggedText text, jr) ->
                    // save the formatted text from the squash
                    let layout = Leaf(jl, text, jr) 
                    let textWidth = length text
                    let rec fitLeaf breaks pos =
                        if pos + textWidth <= maxWidth then
                            breaks, layout, pos + textWidth, textWidth // great, it fits 
                        else
                            match forceBreak breaks with
                            | None -> 
                                breaks, layout, pos + textWidth, textWidth // tough, no more breaks 
                            | Some (breaks, saving) -> 
                                let pos = pos - saving 
                                fitLeaf breaks pos
                       
                    fitLeaf breaks pos

                | Node (l, r, joint) ->
                    let jm = Layout.JuxtapositionMiddle (l, r)
                    let mid = if jm then 0 else 1
                    match joint with
                    | Unbreakable ->
                        let breaks, l, pos, offsetl = fit breaks (pos, l)    // fit left 
                        let pos = pos + mid                              // fit space if juxt says so 
                        let breaks, r, pos, offsetr = fit breaks (pos, r)    // fit right 
                        breaks, Node (l, r, Unbreakable), pos, offsetl + mid + offsetr

                    | Broken indent ->
                        let breaks, l, pos, offsetl = fit breaks (pos, l)    // fit left 
                        let pos = pos - offsetl + indent                 // broken so - offset left + ident 
                        let breaks, r, pos, offsetr = fit breaks (pos, r)    // fit right 
                        breaks, Node (l, r, Broken indent), pos, indent + offsetr

                    | Breakable indent ->
                        let breaks, l, pos, offsetl = fit breaks (pos, l)    // fit left 
                        // have a break possibility, with saving 
                        let saving = offsetl + mid - indent
                        let pos = pos + mid
                        if saving>0 then
                            let breaks = pushBreak saving breaks
                            let breaks, r, pos, offsetr = fit breaks (pos, r)
                            let breaks, broken = popBreak breaks
                            if broken then
                                breaks, Node (l, r, Broken indent)   , pos, indent + offsetr
                            else
                                breaks, Node (l, r, Breakable indent), pos, offsetl + mid + offsetr
                        else
                            // actually no saving so no break 
                            let breaks, r, pos, offsetr = fit breaks (pos, r)
                            breaks, Node (l, r, Breakable indent)  , pos, offsetl + mid + offsetr
               
            //printf "\nDone:     pos=%d offset=%d" pos offset;
            breaks, layout, pos, offset
           
        let breaks = breaks0 ()
        let pos = 0
        let _, layout, _, _ = fit breaks (pos, layout)
        layout

    let combine (strs: string list) = String.Concat strs

    let showL opts leafFormatter layout =
        let push x rstrs = x :: rstrs
        let z0 = [], 0
        let addText (rstrs, i) (text:string) = push text rstrs, i + text.Length
        let index   (_, i)          = i
        let extract rstrs = combine(List.rev rstrs) 
        let newLine (rstrs, _) n = // \n then spaces... 
            let indent = new String(' ', n)
            let rstrs = push "\n"   rstrs
            let rstrs = push indent rstrs
            rstrs, n

        // addL: pos is tab level 
        let rec addL z pos layout = 
            match layout with 
            | ObjLeaf (_, obj, _) -> 
                let text = leafFormatter obj
                addText z text

            | Leaf (_, obj, _) ->
                addText z obj.Text

            | Node (l, r, Broken indent)
                    // Print width = 0 implies 1D layout, no squash
                    when not (opts.PrintWidth = 0) ->
                let z = addL z pos l
                let z = newLine z (pos+indent)
                let z = addL z (pos+indent) r
                z

            | Node (l, r, _) ->
                let jm = Layout.JuxtapositionMiddle (l, r)
                let z = addL z pos l
                let z = if jm then z else addText z " "
                let pos = index z
                let z = addL z pos r
                z

            | Attr (_, _, l) ->
                addL z pos l
           
        let rstrs, _ = addL z0 0 layout
        extract rstrs

    let outL outAttribute leafFormatter (chan: TaggedTextWriter) layout =
        // write layout to output chan directly 
        let write s = chan.Write(s)
        // z is just current indent 
        let z0 = 0
        let index i = i
        let addText z text = write text;  (z + length text)
        let newLine _ n = // \n then spaces... 
            let indent = new String(' ', n)
            chan.WriteLine();
            write (tagText indent);
            n
                
        // addL: pos is tab level 
        let rec addL z pos layout = 
            match layout with 
            | ObjLeaf (_, obj, _) -> 
                let text = leafFormatter obj 
                addText z text
            | Leaf (_, obj, _) -> 
                addText z obj
            | Node (l, r, Broken indent) -> 
                let z = addL z pos l
                let z = newLine z (pos+indent)
                let z = addL z (pos+indent) r
                z
            | Node (l, r, _) -> 
                let jm = Layout.JuxtapositionMiddle (l, r)
                let z = addL z pos l
                let z = if jm then z else addText z space
                let pos = index z
                let z = addL z pos r
                z 
            | Attr (tag, attrs, l) ->
                let _ = outAttribute tag attrs true
                let z = addL z pos l
                let _ = outAttribute tag attrs false
                z
           
        let _ = addL z0 0 layout
        ()

    let unpackCons recd =
        match recd with 
        | [|(_, h);(_, t)|] -> (h, t)
        | _ -> failwith "unpackCons"

    let getListValueInfo bindingFlags (x:obj, ty:Type) =
        match x with 
        | Null -> None 
        | NonNull x -> 
            match Value.GetValueInfo bindingFlags (x, ty) with
            | UnionCaseValue ("Cons", recd) -> Some (unpackCons recd)
            | UnionCaseValue ("Empty", [| |]) -> None
            | _ -> failwith "List value had unexpected ValueInfo"

    let structL = wordL (tagKeyword "struct")

    let nullL = wordL (tagKeyword "null")

    let unitL = wordL (tagPunctuation "()")
          
    let makeRecordL nameXs =
        let itemL (name, xL) = (wordL name ^^ wordL equals) -- xL
        let braceL xs = (wordL leftBrace) ^^ xs ^^ (wordL rightBrace)
            
        nameXs
        |> List.map itemL
        |> aboveListL
        |> braceL

    let makePropertiesL nameXs =
        let itemL (name, v) = 
            let labelL = wordL name 
            (labelL ^^ wordL equals)
            ^^ (match v with 
                | None -> wordL questionMark
                | Some xL -> xL)
            ^^ (rightL semicolon)
        let braceL xs = (leftL leftBrace) ^^ xs ^^ (rightL rightBrace)
        braceL (aboveListL (List.map itemL nameXs))

    let makeListL itemLs =
        (leftL leftBracket) 
        ^^ sepListL (rightL semicolon) itemLs 
        ^^ (rightL rightBracket)

    let makeArrayL xs =
        (leftL (tagPunctuation "[|")) 
        ^^ sepListL (rightL semicolon) xs 
        ^^ (rightL (tagPunctuation "|]"))

    let makeArray2L xs = leftL leftBracket ^^ aboveListL xs ^^ rightL rightBracket  

    let getProperty (ty: Type) (obj: obj) name =
        ty.InvokeMember(name, (BindingFlags.GetProperty ||| BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic), null, obj, [| |],CultureInfo.InvariantCulture)

    let getField obj (fieldInfo: FieldInfo) =
        fieldInfo.GetValue(obj)

    let formatChar isChar c = 
        match c with 
        | '\'' when isChar -> "\\\'"
        | '\"' when not isChar -> "\\\""
        | '\\' -> "\\\\"
        | '\b' -> "\\b"
        | _ when System.Char.IsControl(c) -> 
                let d1 = (int c / 100) % 10 
                let d2 = (int c / 10) % 10 
                let d3 = int c % 10 
                "\\" + d1.ToString() + d2.ToString() + d3.ToString()
        | _ -> c.ToString()
            
    let formatString (s:string) =
        let rec check i = i < s.Length && not (System.Char.IsControl(s,i)) && s.[i] <> '\"' && check (i+1) 
        let rec conv i acc = if i = s.Length then combine (List.rev acc) else conv (i+1) (formatChar false s.[i] :: acc)  
        "\"" + s + "\""

    // Return a truncated version of the string, e.g.
    //   "This is the initial text, which has been truncated"+[12 chars]
    //
    // Note: The layout code forces breaks based on leaf size and possible break points.
    //       It does not force leaf size based on width.
    //       So long leaf-string width can not depend on their printing context...
    //
    // The suffix like "+[dd chars]" is 11 chars.
    //                  12345678901
    let formatStringInWidth (width:int) (str:string) =
        let suffixLength = 11 // turning point suffix length
        let prefixMinLength = 12 // arbitrary. If print width is reduced, want to print a minimum of information on strings...
        let prefixLength = max (width - 2 (*quotes*) - suffixLength) prefixMinLength
        "\"" + (str.Substring(0,prefixLength)) + "\"" + "+[" + (str.Length - prefixLength).ToString() + " chars]"
                           
    type Precedence = 
        | BracketIfTupleOrNotAtomic = 2
        | BracketIfTuple = 3
        | NeverBracket = 4

    // In fsi.exe, certain objects are not printed for top-level bindings.
    [<StructuralEquality; NoComparison>]
    type ShowMode = 
        | ShowAll 
        | ShowTopLevelBinding

    let isSetOrMapType (ty:Type) =
        ty.IsGenericType && 
        (ty.GetGenericTypeDefinition() = typedefof<Map<_,_>> 
         || ty.GetGenericTypeDefinition() = typedefof<Set<_>>)

    // showMode = ShowTopLevelBinding on the outermost expression when called from fsi.exe,
    // This allows certain outputs, e.g. objects that would print as <seq> to be suppressed, etc. See 4343.
    // Calls to layout proper sub-objects should pass showMode = ShowAll.
    //
    // Precedences to ensure we add brackets in the right places   
    type ObjectGraphFormatter(opts: FormatOptions, bindingFlags) =
            
        // Keep a record of objects encountered along the way
        let path = Dictionary<obj,int>(10,HashIdentity.Reference)

        // Roughly count the "nodes" printed, e.g. leaf items and inner nodes, but not every bracket and comma.
        let mutable  size = opts.PrintSize
        let exceededPrintSize() = size<=0
        let countNodes n = if size > 0 then size <- size - n else () // no need to keep decrementing (and avoid wrap around) 
        let stopShort _ = exceededPrintSize() // for unfoldL

        // Recursive descent
        let rec nestedObjL depthLim prec (x:obj, ty:Type) =
            objL ShowAll depthLim prec (x, ty)

        and objL showMode depthLim prec (x:obj, ty:Type) =
            let info = Value.GetValueInfo bindingFlags (x, ty)
            try
                if depthLim<=0 || exceededPrintSize() then wordL (tagPunctuation "...") else
                match x with 
                | Null -> 
                    reprL showMode (depthLim-1) prec info x
                | NonNull x ->
                    if (path.ContainsKey(x)) then 
                        wordL (tagPunctuation "...")
                    else 
                        path.Add(x,0)

                        let res = 
                            // Lazy<T> values. VS2008 used StructuredFormatDisplayAttribute to show via ToString. Dev10 (no attr) needs a special case.
                            let ty = x.GetType()
                            if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<Lazy<_>> then
                                Some (wordL (tagText(x.ToString())))
                            else
                                // Try the StructuredFormatDisplayAttribute extensibility attribute
                                match ty.GetCustomAttributes (typeof<StructuredFormatDisplayAttribute>, true) with
                                | Null | [| |] -> None
                                | NonNull res -> 
                                structuredFormatObjectL showMode ty depthLim (res.[0] :?> StructuredFormatDisplayAttribute) x

#if COMPILER
                        // This is the PrintIntercepts extensibility point currently revealed by fsi.exe's AddPrinter
                        let res = 
                            match res with 
                            | Some _ -> res
                            | None -> 
                                let env = 
                                    { new IEnvironment with
                                        member _.GetLayout(y) = nestedObjL (depthLim-1) Precedence.BracketIfTuple (y, y.GetType()) 
                                        member _.MaxColumns = opts.PrintLength
                                        member _.MaxRows = opts.PrintLength }
                                opts.PrintIntercepts |> List.tryPick (fun intercept -> intercept env x)
#endif
                        let res = 
                            match res with 
                            | Some res -> res
                            | None -> reprL showMode (depthLim-1) prec info x

                        path.Remove(x) |> ignore
                        res
            with
                e ->
                countNodes 1
                wordL (tagText("Error: " + e.Message))

        // Format an object that has a layout specified by StructuredFormatAttribute
        and structuredFormatObjectL showMode ty depthLim (attr: StructuredFormatDisplayAttribute) (obj: obj) =
            let txt = attr.Value
            if isNull (box txt) || txt.Length <= 1 then  
                None
            else
            let messageRegexPattern = @"^(?<pre>.*?)(?<!\\){(?<prop>.*?)(?<!\\)}(?<post>.*)$"
            let illFormedBracketPattern = @"(?<!\\){|(?<!\\)}"

            let rec buildObjMessageL (txt:string) (layouts:Layout list) =
                                    
                let replaceEscapedBrackets (txt:string) =
                    txt.Replace("\{", "{").Replace("\}", "}")
                                      
                // to simplify support for escaped brackets, switch to using a Regex to simply parse the text as the following regex groups:
                //  1) Everything up to the first opening bracket not preceded by a "\", lazily
                //  2) Everything between that opening bracket and a closing bracket not preceded by a "\", lazily
                //  3) Everything after that closing bracket
                let m = System.Text.RegularExpressions.Regex.Match(txt, messageRegexPattern)
                if not m.Success then 
                    // there isn't a match on the regex looking for a property, so now let's make sure we don't have an ill-formed format string (i.e. mismatched/stray brackets)
                    let illFormedMatch = System.Text.RegularExpressions.Regex.IsMatch(txt, illFormedBracketPattern)
                    if illFormedMatch then 
                        None // there are mismatched brackets, bail out
                    elif layouts.Length > 1 then Some (spaceListL (List.rev ((wordL (tagText(replaceEscapedBrackets(txt))) :: layouts))))
                    else Some (wordL (tagText(replaceEscapedBrackets(txt))))
                else
                    // we have a hit on a property reference
                    let preText = replaceEscapedBrackets(m.Groups.["pre"].Value) // everything before the first opening bracket
                    let postText = m.Groups.["post"].Value // Everything after the closing bracket
                    let prop = replaceEscapedBrackets(m.Groups.["prop"].Value) // Unescape everything between the opening and closing brackets

                    match catchExn (fun () -> getProperty ty obj prop) with
                    | Choice2Of2 e -> Some (wordL (tagText("<StructuredFormatDisplay exception: " + e.Message + ">")))
                    | Choice1Of2 alternativeObj ->
                        try 
                            let alternativeObjL = 
                                match alternativeObj with 
                                // A particular rule is that if the alternative property
                                // returns a string, we turn off auto-quoting and escaping of
                                // the string, i.e. just treat the string as display text.
                                // This allows simple implementations of 
                                // such as
                                //
                                //    [<StructuredFormatDisplay("{StructuredDisplayString}I")>]
                                //    type BigInt(signInt:int, v: BigNat) =
                                //        member x.StructuredDisplayString = x.ToString()
                                //
                                | :? string as s -> sepL (tagText s)
                                | _ -> 
                                    // recursing like this can be expensive, so let's throttle it severely
                                    objL showMode (depthLim/10) Precedence.BracketIfTuple (alternativeObj, alternativeObj.GetType())
                            countNodes 0 // 0 means we do not count the preText and postText 

                            let postTextMatch = System.Text.RegularExpressions.Regex.Match(postText, messageRegexPattern)
                            // the postText for this node will be everything up to the next occurrence of an opening brace, if one exists
                            let currentPostText =
                                match postTextMatch.Success with
                                | false -> postText 
                                | true -> postTextMatch.Groups.["pre"].Value

                            let newLayouts = (sepL (tagText preText) ^^ alternativeObjL ^^ sepL (tagText currentPostText)) :: layouts
                            match postText with
                            | "" ->
                                //We are done, build a space-delimited layout from the collection of layouts we've accumulated
                                Some (spaceListL (List.rev newLayouts))

                            | remainingPropertyText when postTextMatch.Success ->
                                                      
                                // look for stray brackets in the text before the next opening bracket
                                let strayClosingMatch = System.Text.RegularExpressions.Regex.IsMatch(postTextMatch.Groups.["pre"].Value, illFormedBracketPattern)
                                if strayClosingMatch then
                                    None
                                else 
                                    // More to process, keep going, using the postText starting at the next instance of a '{'
                                    let openingBracketIndex = postTextMatch.Groups.["prop"].Index-1
                                    buildObjMessageL remainingPropertyText.[openingBracketIndex..] newLayouts

                            | remaingPropertyText ->
                                // make sure we don't have any stray brackets
                                let strayClosingMatch = System.Text.RegularExpressions.Regex.IsMatch(remaingPropertyText, illFormedBracketPattern)
                                if strayClosingMatch then
                                    None
                                else
                                    // We are done, there's more text but it doesn't contain any more properties, we need to remove escaped brackets now though
                                    // since that wasn't done when creating currentPostText
                                    Some (spaceListL (List.rev ((sepL (tagText preText) ^^ alternativeObjL ^^ sepL (tagText(replaceEscapedBrackets(remaingPropertyText)))) :: layouts)))
                        with _ -> 
                            None

            // Seed with an empty layout with a space to the left for formatting purposes
            buildObjMessageL txt [leftL (tagText "")] 

        and recdAtomicTupleL depthLim recd =
            // tuples up args to UnionConstruction or ExceptionConstructor. no node count.
            match recd with 
            | [(_,x)] -> nestedObjL depthLim Precedence.BracketIfTupleOrNotAtomic x 
            | txs -> leftL leftParen ^^ commaListL (List.map (snd >> nestedObjL depthLim Precedence.BracketIfTuple) txs) ^^ rightL rightParen

        and bracketIfL flag basicL =
            if flag then (leftL leftParen) ^^ basicL ^^ (rightL rightParen) else basicL

        and tupleValueL depthLim prec vals tupleType =
            let basicL = sepListL (rightL comma) (List.map (nestedObjL depthLim Precedence.BracketIfTuple ) (Array.toList vals))
            let fields = bracketIfL (prec <= Precedence.BracketIfTuple) basicL
            match tupleType with
            | TupleType.Value -> structL ^^ fields
            | TupleType.Reference -> fields

        and recordValueL depthLim items =
            let itemL (name, x, ty) =
                countNodes 1
                tagRecordField name,nestedObjL depthLim Precedence.BracketIfTuple (x, ty)
            makeRecordL (List.map itemL items)

        and listValueL depthLim constr recd =
            match constr with 
            | "Cons" -> 
                let (x,xs) = unpackCons recd
                let project xs = getListValueInfo bindingFlags xs
                let itemLs = nestedObjL depthLim Precedence.BracketIfTuple x :: boundedUnfoldL (nestedObjL depthLim Precedence.BracketIfTuple) project stopShort xs (opts.PrintLength - 1)
                makeListL itemLs
            | _ ->
                countNodes 1
                wordL (tagPunctuation "[]")

        and unionCaseValueL depthLim prec unionCaseName recd =
            countNodes 1
            let caseName = wordL (tagMethod unionCaseName)
            match recd with
            | [] -> caseName
            | recd -> (caseName --- recdAtomicTupleL depthLim recd) |> bracketIfL (prec <= Precedence.BracketIfTupleOrNotAtomic)

        and fsharpExceptionL depthLim prec (exceptionType: Type) recd =
            countNodes 1
            let name = exceptionType.Name 
            match recd with
            | [] -> (wordL (tagClass name))
            | recd -> (wordL (tagClass name) --- recdAtomicTupleL depthLim recd) |> bracketIfL (prec <= Precedence.BracketIfTupleOrNotAtomic)

        and showModeFilter showMode layout =
            match showMode with
            | ShowAll -> layout
            | ShowTopLevelBinding -> emptyL                                                             

        and functionClosureL showMode (closureType: Type) =
            // Q: should function printing include the ty.Name? It does not convey much useful info to most users, e.g. "clo@0_123".    
            countNodes 1
            wordL (tagText("<fun:"+closureType.Name+">")) |> showModeFilter showMode

        and stringValueL (s: string) =
            countNodes 1
#if COMPILER  
            if s.Length + 2(*quotes*) <= opts.StringLimit then
                // With the quotes, it fits within the limit.
                wordL (tagStringLiteral(formatString s))
            else
                // When a string is considered too long to print, there is a choice: what to print?
                // a) <string>            -- follows <fun:typename>
                // b) <string:length>     -- follows <fun:typename> and gives just the length
                // c) "abcdefg"+[n chars] -- gives a prefix and the remaining chars
                wordL (tagStringLiteral(formatStringInWidth opts.StringLimit s))
#else
            wordL (tagStringLiteral (formatString s))  
#endif                   

        and arrayValueL depthLim (arr: Array) =
            let ty = arr.GetType().GetElementType()
            match arr.Rank with
            | 1 -> 
                let n = arr.Length
                let b1 = arr.GetLowerBound(0) 
                let project depthLim = if depthLim=(b1+n) then None else Some ((box (arr.GetValue(depthLim)), ty),depthLim+1)
                let itemLs = boundedUnfoldL (nestedObjL depthLim Precedence.BracketIfTuple) project stopShort b1 opts.PrintLength
                makeArrayL (if b1 = 0 then itemLs else wordL (tagText("bound1="+string_of_int b1)) :: itemLs)
            | 2 -> 
                let n1 = arr.GetLength(0)
                let n2 = arr.GetLength(1)
                let b1 = arr.GetLowerBound(0) 
                let b2 = arr.GetLowerBound(1) 
                let project2 x y =
                    if x>=(b1+n1) || y>=(b2+n2) then None
                    else Some ((box (arr.GetValue(x,y)), ty),y+1)
                let rowL x = boundedUnfoldL (nestedObjL depthLim Precedence.BracketIfTuple) (project2 x) stopShort b2 opts.PrintLength |> makeListL
                let project1 x = if x>=(b1+n1) then None else Some (x,x+1)
                let rowsL = boundedUnfoldL rowL project1 stopShort b1 opts.PrintLength
                makeArray2L (if b1=0 && b2 = 0 then rowsL else wordL (tagText("bound1=" + string_of_int b1)) :: wordL(tagText("bound2=" + string_of_int b2)) :: rowsL)
            | n -> 
                makeArrayL [wordL (tagText("rank=" + string_of_int n))]
                        
        and mapSetValueL depthLim prec (ty: Type) (obj: obj) =
            let word = if ty.GetGenericTypeDefinition() = typedefof<Map<int,int>> then "map" else "set"
            let possibleKeyValueL v = 
                let tyv = v.GetType()
                if word = "map" &&
                    (match v with null -> false | _ -> true) && 
                    tyv.IsGenericType && 
                    tyv.GetGenericTypeDefinition() = typedefof<KeyValuePair<int,int>> then
                    nestedObjL depthLim Precedence.BracketIfTuple ((tyv.GetProperty("Key").GetValue(v, [| |]), 
                                                                    tyv.GetProperty("Value").GetValue(v, [| |])), tyv)
                else
                    nestedObjL depthLim Precedence.BracketIfTuple (v, tyv)
            let it = (obj :?>  System.Collections.IEnumerable).GetEnumerator() 
            try 
                let itemLs = boundedUnfoldL possibleKeyValueL (fun () -> if it.MoveNext() then Some(it.Current,()) else None) stopShort () (1+opts.PrintLength/12)
                (wordL (tagClass word) --- makeListL itemLs) |> bracketIfL (prec <= Precedence.BracketIfTupleOrNotAtomic)
            finally 
                match it with 
                | :? System.IDisposable as e -> e.Dispose()
                | _ -> ()

        and sequenceValueL showMode depthLim prec (ie: System.Collections.IEnumerable) =
            let showContent = 
                // do not display content of IQueryable since its execution may take significant time
                opts.ShowIEnumerable && (ie.GetType().GetInterfaces() |> Array.exists(fun ty -> ty.FullName = "System.Linq.IQueryable") |> not)

            if showContent then
                let word = "seq"
                let it = ie.GetEnumerator() 
                let ty = ie.GetType().GetInterfaces() |> Array.filter (fun ty -> ty.IsGenericType && ty.Name = "IEnumerable`1") |> Array.tryItem 0
                let ty = Option.map (fun (ty:Type) -> ty.GetGenericArguments().[0]) ty
                try 
                    let itemLs = boundedUnfoldL (nestedObjL depthLim Precedence.BracketIfTuple) (fun () -> if it.MoveNext() then Some((it.Current, match ty with | None -> it.Current.GetType() | Some ty -> ty),()) else None) stopShort () (1+opts.PrintLength/30)
                    (wordL (tagClass word) --- makeListL itemLs) |> bracketIfL (prec <= Precedence.BracketIfTupleOrNotAtomic)
                finally 
                    match it with 
                    | :? System.IDisposable as e -> e.Dispose()
                    | _ -> ()
                             
            else
                // Sequence printing is turned off for declared-values, and maybe be disabled to users.
                // There is choice here, what to print? <seq> or ... or ?
                // Also, in the declared values case, if the sequence is actually a known non-lazy type (list, array etc etc) we could print it.  
                wordL (tagText "<seq>") |> showModeFilter showMode

        and objectValueWithPropertiesL depthLim (ty: Type) (obj: obj) =

            // This buries an obj in the layout, rendered at squash time via a leafFormatter.
            let basicL = Layout.objL obj
            let props = ty.GetProperties(BindingFlags.GetField ||| BindingFlags.Instance ||| BindingFlags.Public)
            let fields = ty.GetFields(BindingFlags.Instance ||| BindingFlags.Public) |> Array.map (fun i -> i :> MemberInfo)
            let propsAndFields = 
                props |> Array.map (fun i -> i :> MemberInfo)
                        |> Array.append fields
                        |> Array.filter (fun pi ->
                    // check if property is annotated with System.Diagnostics.DebuggerBrowsable(Never). 
                    // Its evaluation may have unexpected side effects and\or block printing.
                    match Seq.toArray (pi.GetCustomAttributes(typeof<System.Diagnostics.DebuggerBrowsableAttribute>, false)) with
                    | [|:? System.Diagnostics.DebuggerBrowsableAttribute as attr |] -> attr.State <> System.Diagnostics.DebuggerBrowsableState.Never
                    | _ -> true
                )

            // massively reign in deep printing of properties 
            let nDepth = depthLim/10
#if NETSTANDARD
            Array.Sort((propsAndFields),{ new IComparer<MemberInfo> with member this.Compare(p1,p2) = compare (p1.Name) (p2.Name) } )
#else
            Array.Sort((propsAndFields :> Array),{ new System.Collections.IComparer with member this.Compare(p1,p2) = compare ((p1 :?> MemberInfo).Name) ((p2 :?> MemberInfo).Name) } )
#endif

            if propsAndFields.Length = 0 || (nDepth <= 0) then basicL 
            else basicL --- 
                    (propsAndFields 
                    |> Array.map 
                    (fun m -> 
                        ((if m :? FieldInfo then tagField m.Name else tagProperty m.Name),
                            (try Some (nestedObjL nDepth Precedence.BracketIfTuple ((getProperty ty obj m.Name), ty)) 
                                with _ -> 
                                try Some (nestedObjL nDepth Precedence.BracketIfTuple ((getField obj (m :?> FieldInfo)), ty)) 
                                with _ -> None)))
                    |> Array.toList 
                    |> makePropertiesL)

        and reprL showMode depthLim prec repr x (* x could be null *) =
            match repr with
            | TupleValue (tupleType, vals) ->
                tupleValueL depthLim prec vals tupleType

            | RecordValue items -> 
                recordValueL depthLim (Array.toList items)

            | UnionCaseValue (constr,recd) when // x is List<T>. Note: "null" is never a valid list value. 
                                                    x<>null && isListType (x.GetType()) ->
                listValueL depthLim constr recd

            | UnionCaseValue(unionCaseName, recd) ->
                unionCaseValueL depthLim prec unionCaseName (Array.toList recd)

            | ExceptionValue(exceptionType, recd) ->
                fsharpExceptionL depthLim prec exceptionType (Array.toList recd)

            | FunctionClosureValue closureType ->
                functionClosureL showMode closureType

            | UnitValue ->
                countNodes 1
                unitL

            | NullValue ->
                countNodes 1
                // If this is the root element, wrap the null with angle brackets
                if depthLim = opts.PrintDepth - 1 then
                    wordL (tagText "<null>")
                else nullL

            | ObjectValue obj ->
                let ty = obj.GetType()
                match obj with 
                | :? string as s ->
                    stringValueL s

                | :? Array as arr ->
                    arrayValueL depthLim arr

                | _ when isSetOrMapType ty ->
                    mapSetValueL depthLim prec ty obj

                | :? System.Collections.IEnumerable as ie ->
                    sequenceValueL showMode depthLim prec ie

                | _ when showMode = ShowTopLevelBinding && typeUsesSystemObjectToString ty ->
                    emptyL 

                | _ when opts.ShowProperties -> 
                    countNodes 1
                    objectValueWithPropertiesL depthLim (ty: Type) (obj: obj)

                | _ ->
                    countNodes 1
                    // This buries an obj in the layout, rendered at squash time via a leafFormatter.
                    Layout.objL obj

        member _.Format(showMode, x:'a, xty:Type) =
            objL showMode opts.PrintDepth  Precedence.BracketIfTuple (x, xty)

    let leafFormatter (opts:FormatOptions) (obj :obj) =
        match obj with 
        | Null -> tagKeyword "null"
        | :? double as d -> 
            let s = d.ToString(opts.FloatingPointFormat,opts.FormatProvider)
            let t = 
                if System.Double.IsNaN(d) then "nan"
                elif System.Double.IsNegativeInfinity(d) then "-infinity"
                elif System.Double.IsPositiveInfinity(d) then "infinity"
                elif opts.FloatingPointFormat.[0] = 'g'  && String.forall(fun c -> System.Char.IsDigit(c) || c = '-')  s
                then s + ".0" 
                else s
            tagNumericLiteral t

        | :? single as d -> 
            let t =
                (if System.Single.IsNaN(d) then "nan"
                    elif System.Single.IsNegativeInfinity(d) then "-infinity"
                    elif System.Single.IsPositiveInfinity(d) then "infinity"
                    elif opts.FloatingPointFormat.Length >= 1 && opts.FloatingPointFormat.[0] = 'g' 
                    && float32(System.Int32.MinValue) < d && d < float32(System.Int32.MaxValue) 
                    && float32(int32(d)) = d 
                    then (System.Convert.ToInt32 d).ToString(opts.FormatProvider) + ".0"
                    else d.ToString(opts.FloatingPointFormat,opts.FormatProvider)) 
                + "f"
            tagNumericLiteral t

        | :? decimal as d -> d.ToString("g",opts.FormatProvider) + "M" |> tagNumericLiteral
        | :? uint64 as d -> d.ToString(opts.FormatProvider) + "UL" |> tagNumericLiteral
        | :? int64  as d -> d.ToString(opts.FormatProvider) + "L" |> tagNumericLiteral
        | :? int32  as d -> d.ToString(opts.FormatProvider) |> tagNumericLiteral
        | :? uint32 as d -> d.ToString(opts.FormatProvider) + "u" |> tagNumericLiteral
        | :? int16  as d -> d.ToString(opts.FormatProvider) + "s" |> tagNumericLiteral
        | :? uint16 as d -> d.ToString(opts.FormatProvider) + "us" |> tagNumericLiteral
        | :? sbyte  as d -> d.ToString(opts.FormatProvider) + "y" |> tagNumericLiteral
        | :? byte   as d -> d.ToString(opts.FormatProvider) + "uy" |> tagNumericLiteral
        | :? nativeint as d -> d.ToString() + "n" |> tagNumericLiteral
        | :? unativeint  as d -> d.ToString() + "un" |> tagNumericLiteral
        | :? bool   as b -> (if b then "true" else "false") |> tagKeyword
        | :? char   as c -> "\'" + formatChar true c + "\'" |> tagStringLiteral

        | _ -> 
            let t = 
                try 
                    let text = obj.ToString()
                    match text with
                    | Null -> ""
                    | _ -> text
                with e ->
                    // If a .ToString() call throws an exception, catch it and use the message as the result.
                    // This may be informative, e.g. division by zero etc...
                    "<ToString exception: " + e.Message + ">" 
            tagText t

    let any_to_layout options (value, typValue) =
        let formatter = ObjectGraphFormatter(options, BindingFlags.Public) 
        formatter.Format(ShowAll, value, typValue)

    let squashTo width layout = 
       layout |> squashToAux (width, leafFormatter FormatOptions.Default)

    let squash_layout options layout = 
        // Print width = 0 implies 1D layout, no squash
        if options.PrintWidth = 0 then 
            layout 
        else 
            layout |> squashToAux (options.PrintWidth,leafFormatter options)

    let asTaggedTextWriter (writer: TextWriter) =
        { new TaggedTextWriter with
            member _.Write(t) = writer.Write t.Text
            member _.WriteLine() = writer.WriteLine() }

    let output_layout_tagged options writer layout = 
        layout |> squash_layout options 
            |> outL options.AttributeProcessor (leafFormatter options) writer

    let output_layout options writer layout = 
        output_layout_tagged options (asTaggedTextWriter writer) layout

    let layout_to_string options layout = 
        layout |> squash_layout options 
            |> showL options ((leafFormatter options) >> toText)

    let output_any_ex opts oc x = x |> any_to_layout opts |> output_layout opts oc

    let output_any writer x = output_any_ex FormatOptions.Default writer x

    let layout_as_string options x = x |> any_to_layout options |> layout_to_string options

    let any_to_string x = layout_as_string FormatOptions.Default x

#if COMPILER
    let fsi_any_to_layout options (value, typValue) =
        let formatter = ObjectGraphFormatter(options, BindingFlags.Public) 
        formatter.Format (ShowTopLevelBinding, value, typValue)
#else
    let internal anyToStringForPrintf options (bindingFlags:BindingFlags) (value, typValue) = 
        let formatter = ObjectGraphFormatter(options, bindingFlags) 
        formatter.Format (ShowAll, value, typValue) |> layout_to_string options
#endif

