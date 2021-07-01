// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Core

open Microsoft.FSharp.Core.Operators

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Option = 

    [<CompiledName("GetValue")>]
    let get option = match option with None -> invalidArg "option" (SR.GetString(SR.optionValueWasNone)) | Some x -> x

    [<CompiledName("IsSome")>]
    let inline isSome option = match option with None -> false | Some _ -> true

    [<CompiledName("IsNone")>]
    let inline isNone option = match option with None -> true | Some _ -> false

    [<CompiledName("DefaultValue")>]
    let defaultValue value option = match option with None -> value | Some v -> v

    [<CompiledName("DefaultWith")>]
    let defaultWith defThunk option = match option with None -> defThunk () | Some v -> v

    [<CompiledName("OrElse")>]
    let orElse ifNone option = match option with None -> ifNone | Some _ -> option

    [<CompiledName("OrElseWith")>]
    let orElseWith ifNoneThunk option = match option with None -> ifNoneThunk () | Some _ -> option

    [<CompiledName("Count")>]
    let count option = match option with None -> 0 | Some _ -> 1

    [<CompiledName("Fold")>]
    let fold<'T,'State> folder (state:'State) (option: option<'T>) = match option with None -> state | Some x -> folder state x

    [<CompiledName("FoldBack")>]
    let foldBack<'T,'State> folder (option: option<'T>) (state:'State) =  match option with None -> state | Some x -> folder x state

    [<CompiledName("Exists")>]
    let exists predicate option = match option with None -> false | Some x -> predicate x

    [<CompiledName("ForAll")>]
    let forall predicate option = match option with None -> true | Some x -> predicate x

    [<CompiledName("Contains")>]
    let inline contains value option = match option with None -> false | Some v -> v = value

    [<CompiledName("Iterate")>]
    let iter action option = match option with None -> () | Some x -> action x

    [<CompiledName("Map")>]
    let map mapping option = match option with None -> None | Some x -> Some (mapping x)

    [<CompiledName("Map2")>]
    let map2 mapping option1 option2 = 
        match option1, option2 with
        | Some x, Some y -> Some (mapping x y)
        | _ -> None

    [<CompiledName("Map3")>]
    let map3 mapping option1 option2 option3 = 
        match option1, option2, option3 with
        | Some x, Some y, Some z -> Some (mapping x y z)
        | _ -> None

    [<CompiledName("Bind")>]
    let bind binder option = match option with None -> None | Some x -> binder x

    [<CompiledName("Flatten")>]
    let flatten option = match option with None -> None | Some x -> x

    [<CompiledName("Filter")>]
    let filter predicate option = match option with None -> None | Some x -> if predicate x then Some x else None

    [<CompiledName("ToArray")>]
    let toArray option = match option with  None -> [| |] | Some x -> [| x |]

    [<CompiledName("ToList")>]
    let toList option = match option with  None -> [ ] | Some x -> [ x ]

    [<CompiledName("ToNullable")>]
    let toNullable option = match option with None -> System.Nullable() | Some v -> System.Nullable(v)

    [<CompiledName("OfNullable")>]
    let ofNullable (value:System.Nullable<'T>) = if value.HasValue then Some value.Value else None

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
    [<CompiledName("OfObj")>]
    let ofObj value = match value with null -> None | _ -> Some value

    [<CompiledName("ToObj")>]
    let toObj value = match value with None -> null | Some x -> x
#else
    [<CompiledName("OfObj")>]
    let ofObj (value: 'T?) : 'T option when 'T: not struct and 'T : not null = 
        match value with null -> None | _ -> Some value

    [<CompiledName("ToObj")>]
    let toObj (value: 'T option) : 'T? when 'T: not struct (* and 'T : not null *)  =
        match value with None -> null | Some x -> x
#endif

module ValueOption =

    [<CompiledName("GetValue")>]
    let get voption = match voption with ValueNone -> invalidArg "option" (SR.GetString(SR.optionValueWasNone)) | ValueSome x -> x

    [<CompiledName("IsSome")>]
    let inline isSome voption = match voption with ValueNone -> false | ValueSome _ -> true

    [<CompiledName("IsNone")>]
    let inline isNone voption = match voption with ValueNone -> true | ValueSome _ -> false

    [<CompiledName("DefaultValue")>]
    let defaultValue value voption = match voption with ValueNone -> value | ValueSome v -> v

    [<CompiledName("DefaultWith")>]
    let defaultWith defThunk voption = match voption with ValueNone -> defThunk () | ValueSome v -> v

    [<CompiledName("OrElse")>]
    let orElse ifNone voption = match voption with ValueNone -> ifNone | ValueSome _ -> voption

    [<CompiledName("OrElseWith")>]
    let orElseWith ifNoneThunk voption = match voption with ValueNone -> ifNoneThunk () | ValueSome _ -> voption

    [<CompiledName("Count")>]
    let count voption = match voption with ValueNone -> 0 | ValueSome _ -> 1

    [<CompiledName("Fold")>]
    let fold<'T,'State> folder (state:'State) (voption: voption<'T>) = match voption with ValueNone -> state | ValueSome x -> folder state x

    [<CompiledName("FoldBack")>]
    let foldBack<'T,'State> folder (voption: voption<'T>) (state:'State) =  match voption with ValueNone -> state | ValueSome x -> folder x state

    [<CompiledName("Exists")>]
    let exists predicate voption = match voption with ValueNone -> false | ValueSome x -> predicate x

    [<CompiledName("ForAll")>]
    let forall predicate voption = match voption with ValueNone -> true | ValueSome x -> predicate x

    [<CompiledName("Contains")>]
    let inline contains value voption = match voption with ValueNone -> false | ValueSome v -> v = value

    [<CompiledName("Iterate")>]
    let iter action voption = match voption with ValueNone -> () | ValueSome x -> action x

    [<CompiledName("Map")>]
    let map mapping voption = match voption with ValueNone -> ValueNone | ValueSome x -> ValueSome (mapping x)

    [<CompiledName("Map2")>]
    let map2 mapping voption1 voption2 = 
        match voption1, voption2 with
        | ValueSome x, ValueSome y -> ValueSome (mapping x y)
        | _ -> ValueNone

    [<CompiledName("Map3")>]
    let map3 mapping voption1 voption2 voption3 = 
        match voption1, voption2, voption3 with
        | ValueSome x, ValueSome y, ValueSome z -> ValueSome (mapping x y z)
        | _ -> ValueNone

    [<CompiledName("Bind")>]
    let bind binder voption = match voption with ValueNone -> ValueNone | ValueSome x -> binder x

    [<CompiledName("Flatten")>]
    let flatten voption = match voption with ValueNone -> ValueNone | ValueSome x -> x

    [<CompiledName("Filter")>]
    let filter predicate voption = match voption with ValueNone -> ValueNone | ValueSome x -> if predicate x then ValueSome x else ValueNone

    [<CompiledName("ToArray")>]
    let toArray voption = match voption with  ValueNone -> [| |] | ValueSome x -> [| x |]

    [<CompiledName("ToList")>]
    let toList voption = match voption with  ValueNone -> [ ] | ValueSome x -> [ x ]

    [<CompiledName("ToNullable")>]
    let toNullable voption = match voption with ValueNone -> System.Nullable() | ValueSome v -> System.Nullable(v)

    [<CompiledName("OfNullable")>]
    let ofNullable (value:System.Nullable<'T>) = if value.HasValue then ValueSome value.Value else ValueNone

#if BUILDING_WITH_LKG || BUILD_FROM_SOURCE
    [<CompiledName("OfObj")>]
    let ofObj value = match value with null -> ValueNone | _ -> ValueSome value

    [<CompiledName("ToObj")>]
    let toObj value = match value with ValueNone -> null | ValueSome x -> x
#else
    [<CompiledName("OfObj")>]
    let ofObj (value: 'T?) : 'T voption when 'T: not struct and 'T : not null  = 
        match value with null -> ValueNone | _ -> ValueSome value

    [<CompiledName("ToObj")>]
    let toObj (value : 'T voption) : 'T? when 'T: not struct (* and 'T : not null *) = 
        match value with ValueNone -> null | ValueSome x -> x
#endif