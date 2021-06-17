// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Reflection on F# values. Analyze an object to see if it the representation
// of an F# value.

namespace Microsoft.FSharp.Reflection

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.Operators
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Primitives.Basics
open System.Linq.Expressions

module internal ReflectionUtils =

    type BindingFlags = System.Reflection.BindingFlags

    let toBindingFlags allowAccessToNonPublicMembers =
        if allowAccessToNonPublicMembers then
            BindingFlags.NonPublic ||| BindingFlags.Public
        else
            BindingFlags.Public

[<AutoOpen>]
module internal Impl =

    let getBindingFlags allowAccess = ReflectionUtils.toBindingFlags (defaultArg allowAccess false)

    let inline checkNonNull argName (v: 'T) =
        match box v with
        | null -> nullArg argName
        | _ -> ()

    let isNamedType(typ: Type) = not (typ.IsArray || typ.IsByRef || typ.IsPointer)

    let equivHeadTypes (ty1: Type) (ty2: Type) =
        isNamedType ty1 &&
        if ty1.IsGenericType then
          ty2.IsGenericType && (ty1.GetGenericTypeDefinition()).Equals(ty2.GetGenericTypeDefinition())
        else
          ty1.Equals ty2

    let func = typedefof<(obj -> obj)>

    let isOptionType typ = equivHeadTypes typ (typeof<int option>)
    let isFunctionType typ = equivHeadTypes typ (typeof<(int -> int)>)
    let isListType typ = equivHeadTypes typ (typeof<int list>)

    //-----------------------------------------------------------------
    // GENERAL UTILITIES
    let instanceFieldFlags = BindingFlags.GetField ||| BindingFlags.Instance
    let instancePropertyFlags = BindingFlags.GetProperty ||| BindingFlags.Instance
    let staticPropertyFlags = BindingFlags.GetProperty ||| BindingFlags.Static
    let staticFieldFlags = BindingFlags.GetField ||| BindingFlags.Static
    let staticMethodFlags = BindingFlags.Static
    let getInstancePropertyInfo (typ: Type, propName, bindingFlags) = typ.GetProperty(propName, instancePropertyFlags ||| bindingFlags)
    let getInstancePropertyInfos (typ, names, bindingFlags) = names |> Array.map (fun nm -> getInstancePropertyInfo (typ, nm, bindingFlags))
    let getInstancePropertyReader (typ: Type, propName, bindingFlags) =
        match getInstancePropertyInfo(typ, propName, bindingFlags) with
        | null -> None
        | prop -> Some(fun (obj: obj) -> prop.GetValue (obj, instancePropertyFlags ||| bindingFlags, null, null, null))

    //-----------------------------------------------------------------
    // EXPRESSION TREE COMPILATION

    let compilePropGetterFunc (prop: PropertyInfo) =
        let param = Expression.Parameter (typeof<obj>, "param")
        
        let expr =
            Expression.Lambda<Func<obj, obj>> (
                Expression.Convert (
                    Expression.Property (
                        Expression.Convert (param, prop.DeclaringType),
                        prop),
                    typeof<obj>),
                param)
        expr.Compile ()

    let compileRecordOrUnionCaseReaderFunc (typ, props: PropertyInfo[]) =
        let param = Expression.Parameter (typeof<obj>, "param")
        let typedParam = Expression.Variable typ
    
        let expr =
            Expression.Lambda<Func<obj, obj[]>> (
                Expression.Block (
                    [ typedParam ],
                    Expression.Assign (typedParam, Expression.Convert (param, typ)),
                    Expression.NewArrayInit (typeof<obj>, [
                        for prop in props ->
                            Expression.Convert (Expression.Property (typedParam, prop), typeof<obj>) :> Expression
                    ])
                ),
                param)
        expr.Compile ()

    let compileRecordConstructorFunc (ctorInfo: ConstructorInfo) =
        let ctorParams = ctorInfo.GetParameters ()
        let paramArray = Expression.Parameter (typeof<obj[]>, "paramArray")

        let expr =
            Expression.Lambda<Func<obj[], obj>> (
                Expression.Convert (
                    Expression.New (
                        ctorInfo,
                        [
                            for paramIndex in 0 .. ctorParams.Length - 1 do
                                let p = ctorParams.[paramIndex]

                                Expression.Convert (
                                    Expression.ArrayAccess (paramArray, Expression.Constant paramIndex),
                                    p.ParameterType
                                ) :> Expression
                        ]
                    ),
                    typeof<obj>),
                paramArray
            )
        expr.Compile ()

    let compileUnionCaseConstructorFunc (methodInfo: MethodInfo) =
        let methodParams = methodInfo.GetParameters ()
        let paramArray = Expression.Parameter (typeof<obj[]>, "param")
        
        let expr =
            Expression.Lambda<Func<obj[], obj>> (
                Expression.Convert (
                    Expression.Call (
                        methodInfo,
                        [
                            for paramIndex in 0 .. methodParams.Length - 1 do
                                let p = methodParams.[paramIndex]

                                Expression.Convert (
                                    Expression.ArrayAccess (paramArray, Expression.Constant paramIndex),
                                    p.ParameterType
                                ) :> Expression
                        ]
                    ),
                    typeof<obj>),
                paramArray
            )
        expr.Compile ()

    let compileUnionTagReaderFunc (info: Choice<MethodInfo, PropertyInfo>) =
        let param = Expression.Parameter (typeof<obj>, "param")
        let tag =
            match info with
            | Choice1Of2 info -> Expression.Call (info, Expression.Convert (param, info.DeclaringType)) :> Expression
            | Choice2Of2 info -> Expression.Property (Expression.Convert (param, info.DeclaringType), info) :> _
        
        let expr =
            Expression.Lambda<Func<obj, int>> (
                tag,
                param)
        expr.Compile ()

    let compileTupleConstructor tupleEncField getTupleConstructorMethod typ =
        let rec constituentTuple (typ: Type) elements startIndex =
            Expression.New (
                getTupleConstructorMethod typ,
                [
                    let genericArgs = typ.GetGenericArguments ()

                    for paramIndex in 0 .. genericArgs.Length - 1 do
                        let genericArg = genericArgs.[paramIndex]
                        
                        if paramIndex = tupleEncField then
                            constituentTuple genericArg elements (startIndex + paramIndex) :> Expression
                        else
                            Expression.Convert (Expression.ArrayAccess (elements, Expression.Constant (startIndex + paramIndex)), genericArg)
                ])

        let elements = Expression.Parameter (typeof<obj[]>, "elements")

        let expr =
            Expression.Lambda<Func<obj[], obj>> (
                Expression.Convert (
                    constituentTuple typ elements 0,
                    typeof<obj>
                ),
                elements
            )

        expr.Compile ()

    let compileTupleReader tupleEncField getTupleElementAccessors typ =
        let rec writeTupleIntoArray (typ: Type) (tuple: Expression) outputArray startIndex = seq {
            let elements =
                match getTupleElementAccessors typ with
                // typ is a struct tuple and its elements are accessed via fields 
                | Choice1Of2 (fi: FieldInfo[]) -> fi |> Array.map (fun fi -> Expression.Field (tuple, fi), fi.FieldType)
                // typ is a class tuple and its elements are accessed via properties 
                | Choice2Of2 (pi: PropertyInfo[]) -> pi |> Array.map (fun pi -> Expression.Property (tuple, pi), pi.PropertyType)

            for index, (element, elementType) in elements |> Array.indexed do
                if index = tupleEncField then
                    let innerTupleParam = Expression.Parameter (elementType, "innerTuple")
                    Expression.Block (
                        [ innerTupleParam ],
                        [
                            yield Expression.Assign (innerTupleParam, element) :> Expression
                            yield! writeTupleIntoArray elementType innerTupleParam outputArray (startIndex + index)
                        ]
                    ) :> Expression
                else
                    Expression.Assign (
                        Expression.ArrayAccess (outputArray, Expression.Constant (index + startIndex)),
                        Expression.Convert (element, typeof<obj>)
                    ) :> Expression }

        let param = Expression.Parameter (typeof<obj>, "outerTuple")
        let outputArray = Expression.Variable (typeof<obj[]>, "output")
        let rec outputLength tupleEncField (typ: Type) =
            let genericArgs = typ.GetGenericArguments ()

            if genericArgs.Length > tupleEncField then
                tupleEncField + outputLength tupleEncField genericArgs.[genericArgs.Length - 1]
            else
                genericArgs.Length

        let expr =
            Expression.Lambda<Func<obj, obj[]>> (
                Expression.Block (
                    [ outputArray ],
                    [
                        yield Expression.Assign (
                            outputArray,
                            Expression.NewArrayBounds (typeof<obj>, Expression.Constant (outputLength tupleEncField typ))
                        ) :> Expression
                        yield! writeTupleIntoArray typ (Expression.Convert (param, typ)) outputArray 0
                        yield outputArray :> Expression
                    ]
                ),
                param)

        expr.Compile ()

    //-----------------------------------------------------------------
    // ATTRIBUTE DECOMPILATION

    let tryFindCompilationMappingAttribute (attrs: obj[]) =
      match attrs with
      | null | [| |] -> None
      | [| res |] -> let a = (res :?> CompilationMappingAttribute) in Some (a.SourceConstructFlags, a.SequenceNumber, a.VariantNumber)
      | _ -> invalidOp (SR.GetString (SR.multipleCompilationMappings))

    let findCompilationMappingAttribute (attrs: obj[]) =
      match tryFindCompilationMappingAttribute attrs with
      | None -> failwith "no compilation mapping attribute"
      | Some a -> a

    let cmaName = typeof<CompilationMappingAttribute>.FullName
    let assemblyName = typeof<CompilationMappingAttribute>.Assembly.GetName().Name
    let _ = assert (assemblyName = "FSharp.Core")

    let tryFindCompilationMappingAttributeFromData (attrs: IList<CustomAttributeData>) =
        match attrs with
        | null -> None
        | _ ->
            let mutable res = None
            for a in attrs do
                if a.Constructor.DeclaringType.FullName = cmaName then
                    let args = a.ConstructorArguments
                    let flags =
                         match args.Count  with
                         | 1 -> ((let x = args.[0] in x.Value :?> SourceConstructFlags), 0, 0)
                         | 2 -> ((let x = args.[0] in x.Value :?> SourceConstructFlags), (let x = args.[1] in x.Value :?> int), 0)
                         | 3 -> ((let x = args.[0] in x.Value :?> SourceConstructFlags), (let x = args.[1] in x.Value :?> int), (let x = args.[2] in x.Value :?> int))
                         | _ -> (enum 0, 0, 0)
                    res <- Some flags
            res

    let findCompilationMappingAttributeFromData attrs =
      match tryFindCompilationMappingAttributeFromData attrs with
      | None -> failwith "no compilation mapping attribute"
      | Some a -> a

    let tryFindCompilationMappingAttributeFromType       (typ: Type)        =
        let assem = typ.Assembly
        if (not (isNull assem)) && assem.ReflectionOnly then
           tryFindCompilationMappingAttributeFromData ( typ.GetCustomAttributesData())
        else
        tryFindCompilationMappingAttribute ( typ.GetCustomAttributes (typeof<CompilationMappingAttribute>, false))

    let tryFindCompilationMappingAttributeFromMemberInfo (info: MemberInfo) =
        let assem = info.DeclaringType.Assembly
        if (not (isNull assem)) && assem.ReflectionOnly then
           tryFindCompilationMappingAttributeFromData (info.GetCustomAttributesData())
        else
        tryFindCompilationMappingAttribute (info.GetCustomAttributes (typeof<CompilationMappingAttribute>, false))

    let    findCompilationMappingAttributeFromMemberInfo (info: MemberInfo) =
        let assem = info.DeclaringType.Assembly
        if (not (isNull assem)) && assem.ReflectionOnly then
            findCompilationMappingAttributeFromData (info.GetCustomAttributesData())
        else
        findCompilationMappingAttribute (info.GetCustomAttributes (typeof<CompilationMappingAttribute>, false))

    let sequenceNumberOfMember          (x: MemberInfo) = let (_, n, _) = findCompilationMappingAttributeFromMemberInfo x in n
    let variantNumberOfMember           (x: MemberInfo) = let (_, _, vn) = findCompilationMappingAttributeFromMemberInfo x in vn

    let sortFreshArray f arr = Array.sortInPlaceWith f arr; arr

    let isFieldProperty (prop : PropertyInfo) =
        match tryFindCompilationMappingAttributeFromMemberInfo prop with
        | None -> false
        | Some (flags, _n, _vn) -> (flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.Field

    let tryFindSourceConstructFlagsOfType (typ: Type) =
      match tryFindCompilationMappingAttributeFromType typ with
      | None -> None
      | Some (flags, _n, _vn) -> Some flags

    //-----------------------------------------------------------------
    // UNION DECOMPILATION

    // Get the type where the type definitions are stored
    let getUnionCasesTyp (typ: Type, _bindingFlags) =
#if CASES_IN_NESTED_CLASS
       let casesTyp = typ.GetNestedType("Cases", bindingFlags)
       if casesTyp.IsGenericTypeDefinition then casesTyp.MakeGenericType(typ.GetGenericArguments())
       else casesTyp
#else
       typ
#endif

    let getUnionTypeTagNameMap (typ: Type, bindingFlags) =
        let enumTyp = typ.GetNestedType("Tags", bindingFlags)
        // Unions with a singleton case do not get a Tags type (since there is only one tag), hence enumTyp may be null in this case
        match enumTyp with
        | null ->
            typ.GetMethods(staticMethodFlags ||| bindingFlags)
            |> Array.choose (fun minfo ->
                match tryFindCompilationMappingAttributeFromMemberInfo minfo with
                | None -> None
                | Some (flags, n, _vn) ->
                    if (flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.UnionCase then
                        let nm = minfo.Name
                        // chop "get_" or  "New" off the front
                        let nm =
                            if not (isListType typ) && not (isOptionType typ) && nm.Length > 3 then
                                if   nm.StartsWith ("get_", StringComparison.Ordinal) then nm.[4..]
                                elif nm.StartsWith ("New", StringComparison.Ordinal) then nm.[3..]
                                else nm
                            else nm
                        Some (n, nm)
                    else
                        None)
        | _ ->
            enumTyp.GetFields(staticFieldFlags ||| bindingFlags)
            |> Array.filter (fun (f: FieldInfo) -> f.IsStatic && f.IsLiteral)
            |> sortFreshArray (fun f1 f2 -> compare (f1.GetValue null :?> int) (f2.GetValue null :?> int))
            |> Array.map (fun tagfield -> (tagfield.GetValue null :?> int), tagfield.Name)

    let getUnionCaseTyp (typ: Type, tag: int, bindingFlags) =
        let tagFields = getUnionTypeTagNameMap(typ, bindingFlags)
        let tagField = tagFields |> Array.pick (fun (i, f) -> if i = tag then Some f else None)
        if tagFields.Length = 1 then
            typ
        else
            // special case: two-cased DU annotated with CompilationRepresentation(UseNullAsTrueValue)
            // in this case it will be compiled as one class: return self type for non-nullary case and null for nullary
            let isTwoCasedDU =
                if tagFields.Length = 2 then
                    match typ.GetCustomAttributes(typeof<CompilationRepresentationAttribute>, false) with
                    | [|:? CompilationRepresentationAttribute as attr|] ->
                        (attr.Flags &&& CompilationRepresentationFlags.UseNullAsTrueValue) = CompilationRepresentationFlags.UseNullAsTrueValue
                    | _ -> false
                else
                    false
            if isTwoCasedDU then
                typ
            else
            let casesTyp = getUnionCasesTyp (typ, bindingFlags)
            let caseTyp = casesTyp.GetNestedType(tagField, bindingFlags) // if this is null then the union is nullary
            match caseTyp with
            | null -> null
            | _ when caseTyp.IsGenericTypeDefinition -> caseTyp.MakeGenericType(casesTyp.GetGenericArguments())
            | _ -> caseTyp

    let getUnionTagConverter (typ: Type, bindingFlags) =
        if isOptionType typ then (fun tag -> match tag with 0 -> "None" | 1 -> "Some" | _ -> invalidArg "tag" (SR.GetString (SR.outOfRange)))
        elif isListType typ then (fun tag -> match tag with  0 -> "Empty" | 1 -> "Cons" | _ -> invalidArg "tag" (SR.GetString (SR.outOfRange)))
        else
          let tagfieldmap = getUnionTypeTagNameMap (typ, bindingFlags) |> Map.ofSeq
          (fun tag -> tagfieldmap.[tag])

    let isUnionType (typ: Type, bindingFlags: BindingFlags) =
        isOptionType typ ||
        isListType typ ||
        match tryFindSourceConstructFlagsOfType typ with
        | None -> false
        | Some flags ->
          (flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.SumType &&
          // We see private representations only if BindingFlags.NonPublic is set
          (if (flags &&& SourceConstructFlags.NonPublicRepresentation) <> enum 0 then
              (bindingFlags &&& BindingFlags.NonPublic) <> enum 0
           else
              true)

    // Check the base type - if it is also an F# type then
    // for the moment we know it is a Discriminated Union
    let isConstructorRepr (typ, bindingFlags) =
        let rec get typ = isUnionType (typ, bindingFlags) || match typ.BaseType with null -> false | b -> get b
        get typ

    let unionTypeOfUnionCaseType (typ, bindingFlags) =
        let rec get typ = if isUnionType (typ, bindingFlags) then typ else match typ.BaseType with null -> typ | b -> get b
        get typ

    let fieldsPropsOfUnionCase (typ, tag, bindingFlags) =
        if isOptionType typ then
            match tag with
            | 0 (* None *) -> getInstancePropertyInfos (typ, [| |], bindingFlags)
            | 1 (* Some *) -> getInstancePropertyInfos (typ, [| "Value" |], bindingFlags)
            | _ -> failwith "fieldsPropsOfUnionCase"
        elif isListType typ then
            match tag with
            | 0 (* Nil *)  -> getInstancePropertyInfos (typ, [| |], bindingFlags)
            | 1 (* Cons *) -> getInstancePropertyInfos (typ, [| "Head"; "Tail" |], bindingFlags)
            | _ -> failwith "fieldsPropsOfUnionCase"
        else
            // Lookup the type holding the fields for the union case
            let caseTyp = getUnionCaseTyp (typ, tag, bindingFlags)
            let caseTyp = match caseTyp with null ->  typ | _ -> caseTyp
            caseTyp.GetProperties(instancePropertyFlags ||| bindingFlags)
            |> Array.filter isFieldProperty
            |> Array.filter (fun prop -> variantNumberOfMember prop = tag)
            |> sortFreshArray (fun p1 p2 -> compare (sequenceNumberOfMember p1) (sequenceNumberOfMember p2))


    let getUnionCaseRecordReader (typ: Type, tag: int, bindingFlags) =
        let props = fieldsPropsOfUnionCase (typ, tag, bindingFlags)
        (fun (obj: obj) -> props |> Array.map (fun prop -> prop.GetValue (obj, bindingFlags, null, null, null)))

    let getUnionCaseRecordReaderCompiled (typ: Type, tag: int, bindingFlags) =
        let props = fieldsPropsOfUnionCase (typ, tag, bindingFlags)
        let caseTyp = getUnionCaseTyp (typ, tag, bindingFlags)
        let caseTyp = if isNull caseTyp then typ else caseTyp
        compileRecordOrUnionCaseReaderFunc(caseTyp, props).Invoke

    let getUnionTagReader (typ: Type, bindingFlags) : (obj -> int) =
        if isOptionType typ then
            (fun (obj: obj) -> match obj with null -> 0 | _ -> 1)
        else
            let tagMap = getUnionTypeTagNameMap (typ, bindingFlags)
            if tagMap.Length <= 1 then
                (fun (_obj: obj) -> 0)
            else
                match getInstancePropertyReader (typ, "Tag", bindingFlags) with
                | Some reader -> (fun (obj: obj) -> reader obj :?> int)
                | None ->
                    let m2b = typ.GetMethod("GetTag", BindingFlags.Static ||| bindingFlags, null, [| typ |], null)
                    (fun (obj: obj) -> m2b.Invoke(null, [|obj|]) :?> int)

    let getUnionTagReaderCompiled (typ: Type, bindingFlags) : (obj -> int) =
        if isOptionType typ then
            (fun (obj: obj) -> match obj with null -> 0 | _ -> 1)
        else
            let tagMap = getUnionTypeTagNameMap (typ, bindingFlags)
            if tagMap.Length <= 1 then
                (fun (_obj: obj) -> 0)
            else
                match getInstancePropertyInfo (typ, "Tag", bindingFlags) with
                | null ->
                    let m2b = typ.GetMethod("GetTag", BindingFlags.Static ||| bindingFlags, null, [| typ |], null)
                    compileUnionTagReaderFunc(Choice1Of2 m2b).Invoke
                | info -> compileUnionTagReaderFunc(Choice2Of2 info).Invoke

    let getUnionTagMemberInfo (typ: Type, bindingFlags) =
        match getInstancePropertyInfo (typ, "Tag", bindingFlags) with
        | null -> (typ.GetMethod("GetTag", BindingFlags.Static ||| bindingFlags) :> MemberInfo)
        | info -> (info :> MemberInfo)

    let isUnionCaseNullary (typ: Type, tag: int, bindingFlags) =
        fieldsPropsOfUnionCase(typ, tag, bindingFlags).Length = 0

    let getUnionCaseConstructorMethod (typ: Type, tag: int, bindingFlags) =
        let constrname = getUnionTagConverter (typ, bindingFlags) tag
        let methname =
            if isUnionCaseNullary (typ, tag, bindingFlags) then "get_" + constrname
            elif isListType typ || isOptionType typ then constrname
            else "New" + constrname

        match typ.GetMethod(methname, BindingFlags.Static  ||| bindingFlags) with
        | null -> invalidOp (String.Format (SR.GetString (SR.constructorForUnionCaseNotFound), methname))
        | meth -> meth

    let getUnionCaseConstructor (typ: Type, tag: int, bindingFlags) =
        let meth = getUnionCaseConstructorMethod (typ, tag, bindingFlags)
        (fun args ->
            meth.Invoke(null, BindingFlags.Static ||| BindingFlags.InvokeMethod ||| bindingFlags, null, args, null))

    let getUnionCaseConstructorCompiled (typ: Type, tag: int, bindingFlags) =
        let meth = getUnionCaseConstructorMethod (typ, tag, bindingFlags)
        compileUnionCaseConstructorFunc(meth).Invoke

    let checkUnionType (unionType, bindingFlags) =
        checkNonNull "unionType" unionType
        if not (isUnionType (unionType, bindingFlags)) then
            if isUnionType (unionType, bindingFlags ||| BindingFlags.NonPublic) then
                invalidArg "unionType" (String.Format (SR.GetString (SR.privateUnionType), unionType.FullName))
            else
                invalidArg "unionType" (String.Format (SR.GetString (SR.notAUnionType), unionType.FullName))

    //-----------------------------------------------------------------
    // TUPLE DECOMPILATION
    let tupleNames =
        [| "System.Tuple`1"
           "System.Tuple`2"
           "System.Tuple`3"
           "System.Tuple`4"
           "System.Tuple`5"
           "System.Tuple`6"
           "System.Tuple`7"
           "System.Tuple`8"
           "System.Tuple"
           "System.ValueTuple`1"
           "System.ValueTuple`2"
           "System.ValueTuple`3"
           "System.ValueTuple`4"
           "System.ValueTuple`5"
           "System.ValueTuple`6"
           "System.ValueTuple`7"
           "System.ValueTuple`8"
           "System.ValueTuple" |]

    let simpleTupleNames = 
        [| "Tuple`1"
           "Tuple`2"
           "Tuple`3"
           "Tuple`4"
           "Tuple`5"
           "Tuple`6"
           "Tuple`7"
           "Tuple`8"
           "ValueTuple`1"
           "ValueTuple`2"
           "ValueTuple`3"
           "ValueTuple`4"
           "ValueTuple`5"
           "ValueTuple`6"
           "ValueTuple`7"
           "ValueTuple`8" |]

    let isTupleType (typ: Type) =
        // We need to be careful that we only rely typ.IsGenericType, typ.Namespace and typ.Name here.
        //
        // Historically the FSharp.Core reflection utilities get used on implementations of
        // System.Type that don't have functionality such as .IsEnum and .FullName fully implemented.
        // This happens particularly over TypeBuilderInstantiation types in the ProvideTypes implementation of System.Type
        // used in F# type providers.
        typ.IsGenericType &&
        typ.Namespace = "System" &&
        simpleTupleNames |> Seq.exists typ.Name.StartsWith

    let maxTuple = 8
    // Which field holds the nested tuple?
    let tupleEncField = maxTuple - 1

    let dictionaryLock = obj()
    let refTupleTypes = Dictionary<Assembly, Type[]>()
    let valueTupleTypes = Dictionary<Assembly, Type[]>()

    let rec mkTupleType isStruct (asm: Assembly) (tys: Type[]) =
        let table =
            let makeIt n =
                let tupleFullName n =
                    let structOffset = if isStruct then 9 else 0
                    let index = n - 1 + structOffset
                    tupleNames.[index]

                match n with
                | 1 -> asm.GetType(tupleFullName 1)
                | 2 -> asm.GetType(tupleFullName 2)
                | 3 -> asm.GetType(tupleFullName 3)
                | 4 -> asm.GetType(tupleFullName 4)
                | 5 -> asm.GetType(tupleFullName 5)
                | 6 -> asm.GetType(tupleFullName 6)
                | 7 -> asm.GetType(tupleFullName 7)
                | 8 -> asm.GetType(tupleFullName 8)
                | _ -> invalidArg "tys" (SR.GetString (SR.invalidTupleTypes))

            let tables = if isStruct then valueTupleTypes else refTupleTypes
            match lock dictionaryLock (fun () -> tables.TryGetValue asm) with
            | false, _ ->
                // the Dictionary<>s here could be ConcurrentDictionary<>'s, but then
                // that would lock while initializing the Type array (maybe not an issue)
                let mutable a = Array.init<Type> 8 (fun i -> makeIt (i + 1))
                lock dictionaryLock (fun () ->
                    match tables.TryGetValue asm with
                    | true, t -> a <- t
                    | false, _ -> tables.Add(asm, a))
                a
            | true, t -> t

        match tys.Length with
        | 1 -> table.[0].MakeGenericType tys
        | 2 -> table.[1].MakeGenericType tys
        | 3 -> table.[2].MakeGenericType tys
        | 4 -> table.[3].MakeGenericType tys
        | 5 -> table.[4].MakeGenericType tys
        | 6 -> table.[5].MakeGenericType tys
        | 7 -> table.[6].MakeGenericType tys
        | n when n >= maxTuple ->
            let tysA = tys.[0..tupleEncField-1]
            let tysB = tys.[maxTuple-1..]
            let tyB = mkTupleType isStruct asm tysB
            table.[7].MakeGenericType(Array.append tysA [| tyB |])
        | _ -> invalidArg "tys" (SR.GetString (SR.invalidTupleTypes))

    let rec getTupleTypeInfo (typ: Type) =
        if not (isTupleType typ) then
            invalidArg "typ" (String.Format (SR.GetString (SR.notATupleType), typ.FullName))
        let tyargs = typ.GetGenericArguments()
        if tyargs.Length = maxTuple then
            let tysA = tyargs.[0..tupleEncField-1]
            let tyB = tyargs.[tupleEncField]
            Array.append tysA (getTupleTypeInfo tyB)
        else
            tyargs

    let orderTupleProperties (props: PropertyInfo[]) =
        // The tuple properties are of the form:
        //   Item1
        //   ..
        //   Item1, Item2, ..., Item<maxTuple-1>
        //   Item1, Item2, ..., Item<maxTuple-1>, Rest
        // The PropertyInfo may not come back in order, so ensure ordering here.
#if !NETSTANDARD
        assert(maxTuple < 10) // Alphasort will only works for upto 9 items: Item1, Item10, Item2, Item3, ..., Item9, Rest
#endif
        let props = props |> Array.sortBy (fun p -> p.Name) // they are not always in alphabetic order
#if !NETSTANDARD
        assert(props.Length <= maxTuple)
        assert(let haveNames   = props |> Array.map (fun p -> p.Name)
               let expectNames = Array.init props.Length (fun i -> let j = i+1 // index j = 1, 2, .., props.Length <= maxTuple
                                                                   if   j<maxTuple then "Item" + string j
                                                                   elif j=maxTuple then "Rest"
                                                                   else (assert false; "")) // dead code under prior assert, props.Length <= maxTuple
               haveNames = expectNames)
#endif
        props

    let orderTupleFields (fields: FieldInfo[]) =
        // The tuple fields are of the form:
        //   Item1
        //   ..
        //   Item1, Item2, ..., Item<maxTuple-1>
        //   Item1, Item2, ..., Item<maxTuple-1>, Rest
        // The PropertyInfo may not come back in order, so ensure ordering here.
#if !NETSTANDARD
        assert(maxTuple < 10) // Alphasort will only works for upto 9 items: Item1, Item10, Item2, Item3, ..., Item9, Rest
#endif
        let fields = fields |> Array.sortBy (fun fi -> fi.Name) // they are not always in alphabetic order
#if !NETSTANDARD
        assert(fields.Length <= maxTuple)
        assert(let haveNames   = fields |> Array.map (fun fi -> fi.Name)
               let expectNames = Array.init fields.Length (fun i -> let j = i+1 // index j = 1, 2, .., fields.Length <= maxTuple
                                                                    if   j<maxTuple then "Item" + string j
                                                                    elif j=maxTuple then "Rest"
                                                                    else (assert false; "")) // dead code under prior assert, props.Length <= maxTuple
               haveNames = expectNames)
#endif
        fields

    let getTupleConstructorMethod (typ: Type) =
        let ctor =
            if typ.IsValueType then
                let fields = typ.GetFields (instanceFieldFlags ||| BindingFlags.Public) |> orderTupleFields
                typ.GetConstructor(BindingFlags.Public ||| BindingFlags.Instance, null, fields |> Array.map (fun fi -> fi.FieldType), null)
            else
                let props = typ.GetProperties() |> orderTupleProperties
                typ.GetConstructor(BindingFlags.Public ||| BindingFlags.Instance, null, props |> Array.map (fun p -> p.PropertyType), null)
        match ctor with
        | null -> raise (ArgumentException (String.Format (SR.GetString (SR.invalidTupleTypeConstructorNotDefined), typ.FullName)))
        | _ -> ()
        ctor

    let getTupleCtor(typ: Type) =
          let ctor = getTupleConstructorMethod typ
          (fun (args: obj[]) ->
              ctor.Invoke(BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public, null, args, null))

    let getTupleElementAccessors (typ: Type) =
        if typ.IsValueType then
            Choice1Of2 (typ.GetFields (instanceFieldFlags ||| BindingFlags.Public) |> orderTupleFields)
        else
            Choice2Of2 (typ.GetProperties (instancePropertyFlags ||| BindingFlags.Public) |> orderTupleProperties)

    let rec getTupleReader (typ: Type) =
        let etys = typ.GetGenericArguments()
        // Get the reader for the outer tuple record
        let reader =
            match getTupleElementAccessors typ with
            | Choice1Of2 fi -> fun obj -> fi |> Array.map (fun f -> f.GetValue obj)
            | Choice2Of2 pi -> fun obj -> pi |> Array.map (fun p -> p.GetValue (obj, null))
        if etys.Length < maxTuple
        then reader
        else
            let tyBenc = etys.[tupleEncField]
            let reader2 = getTupleReader tyBenc
            (fun obj ->
                let directVals = reader obj
                let encVals = reader2 directVals.[tupleEncField]
                Array.append directVals.[0..tupleEncField-1] encVals)

    let rec getTupleConstructor (typ: Type) =
        let etys = typ.GetGenericArguments()
        let maker1 =  getTupleCtor typ
        if etys.Length < maxTuple
        then maker1
        else
            let tyBenc = etys.[tupleEncField]
            let maker2 = getTupleConstructor tyBenc
            (fun (args: obj[]) ->
                let encVal = maker2 args.[tupleEncField..]
                maker1 (Array.append args.[0..tupleEncField-1] [| encVal |]))

    let getTupleConstructorInfo (typ: Type) =
        let etys = typ.GetGenericArguments()
        let maker1 =  getTupleConstructorMethod typ
        if etys.Length < maxTuple then
            maker1, None
        else
            maker1, Some(etys.[tupleEncField])

    let getTupleReaderInfo (typ: Type, index: int) =
        if index < 0 then
            invalidArg "index" (String.Format (SR.GetString (SR.tupleIndexOutOfRange), typ.FullName, index.ToString()))

        let get index =
            if typ.IsValueType then
                let props = typ.GetProperties(instancePropertyFlags ||| BindingFlags.Public) |> orderTupleProperties
                if index >= props.Length then
                    invalidArg "index" (String.Format (SR.GetString (SR.tupleIndexOutOfRange), typ.FullName, index.ToString()))
                props.[index]
            else
                let props = typ.GetProperties(instancePropertyFlags ||| BindingFlags.Public) |> orderTupleProperties
                if index >= props.Length then
                    invalidArg "index" (String.Format (SR.GetString (SR.tupleIndexOutOfRange), typ.FullName, index.ToString()))
                props.[index]

        if index < tupleEncField then
            get index, None
        else
            let etys = typ.GetGenericArguments()
            get tupleEncField, Some(etys.[tupleEncField], index-(maxTuple-1))

    let getFunctionTypeInfo (typ: Type) =
      if not (isFunctionType typ) then
          invalidArg "typ" (String.Format (SR.GetString (SR.notAFunctionType), typ.FullName))
      let tyargs = typ.GetGenericArguments()
      tyargs.[0], tyargs.[1]

    let isModuleType (typ: Type) =
      match tryFindSourceConstructFlagsOfType typ with
      | None -> false
      | Some flags ->
        (flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.Module

    let rec isClosureRepr typ =
        isFunctionType typ ||
        (match typ.BaseType with null -> false | bty -> isClosureRepr bty)

    let isRecordType (typ: Type, bindingFlags: BindingFlags) =
      match tryFindSourceConstructFlagsOfType typ with
      | None -> false
      | Some flags ->
        (flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.RecordType &&
        // We see private representations only if BindingFlags.NonPublic is set
        (if (flags &&& SourceConstructFlags.NonPublicRepresentation) <> enum 0 then
            (bindingFlags &&& BindingFlags.NonPublic) <> enum 0
         else
            true)

    let fieldPropsOfRecordType(typ: Type, bindingFlags) =
      typ.GetProperties(instancePropertyFlags ||| bindingFlags)
      |> Array.filter isFieldProperty
      |> sortFreshArray (fun p1 p2 -> compare (sequenceNumberOfMember p1) (sequenceNumberOfMember p2))

    let getRecordReader(typ: Type, bindingFlags) =
        let props = fieldPropsOfRecordType(typ, bindingFlags)
        (fun (obj: obj) -> props |> Array.map (fun prop -> prop.GetValue (obj, null)))

    let getRecordReaderCompiled(typ: Type, bindingFlags) =
        let props = fieldPropsOfRecordType(typ, bindingFlags)
        compileRecordOrUnionCaseReaderFunc(typ, props).Invoke

    let getRecordConstructorMethod(typ: Type, bindingFlags) =
        let props = fieldPropsOfRecordType(typ, bindingFlags)
        let ctor = typ.GetConstructor(BindingFlags.Instance ||| bindingFlags, null, props |> Array.map (fun p -> p.PropertyType), null)
        match ctor with
        | null -> raise <| ArgumentException (String.Format (SR.GetString (SR.invalidRecordTypeConstructorNotDefined), typ.FullName))
        | _ -> ()
        ctor

    let getRecordConstructor(typ: Type, bindingFlags) =
        let ctor = getRecordConstructorMethod(typ, bindingFlags)
        (fun (args: obj[]) ->
            ctor.Invoke(BindingFlags.InvokeMethod  ||| BindingFlags.Instance ||| bindingFlags, null, args, null))

    let getRecordConstructorCompiled(typ: Type, bindingFlags) =
        let ctor = getRecordConstructorMethod(typ, bindingFlags)
        compileRecordConstructorFunc(ctor).Invoke

    /// EXCEPTION DECOMPILATION
    // Check the base type - if it is also an F# type then
    // for the moment we know it is a Discriminated Union
    let isExceptionRepr (typ: Type, bindingFlags) =
        match tryFindSourceConstructFlagsOfType typ with
        | None -> false
        | Some flags ->
          ((flags &&& SourceConstructFlags.KindMask) = SourceConstructFlags.Exception) &&
          // We see private representations only if BindingFlags.NonPublic is set
          (if (flags &&& SourceConstructFlags.NonPublicRepresentation) <> enum 0 then
              (bindingFlags &&& BindingFlags.NonPublic) <> enum 0
           else
              true)

    let getTypeOfReprType (typ: Type, bindingFlags) =
        if isExceptionRepr (typ, bindingFlags) then typ.BaseType
        elif isConstructorRepr (typ, bindingFlags) then unionTypeOfUnionCaseType(typ, bindingFlags)
        elif isClosureRepr typ then
          let rec get (typ: Type) = if isFunctionType typ then typ else match typ.BaseType with null -> typ | b -> get b
          get typ
        else typ

    //-----------------------------------------------------------------
    // CHECKING ROUTINES

    let checkExnType (exceptionType, bindingFlags) =
        if not (isExceptionRepr (exceptionType, bindingFlags)) then
            if isExceptionRepr (exceptionType, bindingFlags ||| BindingFlags.NonPublic) then
                invalidArg "exceptionType" (String.Format (SR.GetString (SR.privateExceptionType), exceptionType.FullName))
            else
                invalidArg "exceptionType" (String.Format (SR.GetString (SR.notAnExceptionType), exceptionType.FullName))

    let checkRecordType (argName, recordType, bindingFlags) =
        checkNonNull argName recordType
        if not (isRecordType (recordType, bindingFlags) ) then
            if isRecordType (recordType, bindingFlags ||| BindingFlags.NonPublic) then
                invalidArg argName (String.Format (SR.GetString (SR.privateRecordType), recordType.FullName))
            else
                invalidArg argName (String.Format (SR.GetString (SR.notARecordType), recordType.FullName))

    let checkTupleType(argName, (tupleType: Type)) =
        checkNonNull argName tupleType
        if not (isTupleType tupleType) then
            invalidArg argName (String.Format (SR.GetString (SR.notATupleType), tupleType.FullName))

[<Sealed>]
type UnionCaseInfo(typ: System.Type, tag: int) =

    // Cache the tag -> name map
    let mutable names = None

    let getMethInfo() = getUnionCaseConstructorMethod (typ, tag, BindingFlags.Public ||| BindingFlags.NonPublic)

    member _.Name =
        match names with
        | None ->
            let conv = getUnionTagConverter (typ, BindingFlags.Public ||| BindingFlags.NonPublic)
            names <- Some conv
            conv tag
        | Some conv ->
            conv tag

    member _.DeclaringType = typ

    member _.GetFields() =
        fieldsPropsOfUnionCase (typ, tag, BindingFlags.Public ||| BindingFlags.NonPublic)

    member _.GetCustomAttributes() =
        getMethInfo().GetCustomAttributes false

    member _.GetCustomAttributes attributeType =
        getMethInfo().GetCustomAttributes(attributeType, false)

    member _.GetCustomAttributesData() =
        getMethInfo().CustomAttributes |> Seq.toArray :> IList<_>

    member _.Tag = tag

    override x.ToString() = typ.Name + "." + x.Name

    override x.GetHashCode() = typ.GetHashCode() + tag

    override _.Equals(obj: obj) =
        match obj with
        | :? UnionCaseInfo as uci -> uci.DeclaringType = typ && uci.Tag = tag
        | _ -> false

[<AbstractClass; Sealed>]
type FSharpType =

    static member IsTuple(typ: Type) =
        checkNonNull "typ" typ
        isTupleType typ

    static member IsRecord(typ: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "typ" typ
        isRecordType (typ, bindingFlags)

    static member IsUnion (typ: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "typ" typ
        let typ = getTypeOfReprType (typ, BindingFlags.Public ||| BindingFlags.NonPublic)
        isUnionType (typ, bindingFlags)

    static member IsFunction (typ: Type) =
        checkNonNull "typ" typ
        let typ = getTypeOfReprType (typ, BindingFlags.Public ||| BindingFlags.NonPublic)
        isFunctionType typ

    static member IsModule (typ: Type) =
        checkNonNull "typ" typ
        isModuleType typ

    static member MakeFunctionType (domain: Type, range: Type) =
        checkNonNull "domain" domain
        checkNonNull "range" range
        func.MakeGenericType [| domain; range |]

    static member MakeTupleType (types: Type[]) =
        checkNonNull "types" types

        // No assembly passed therefore just get framework local version of Tuple
        let asm = typeof<System.Tuple>.Assembly
        if types |> Array.exists (function null -> true | _ -> false) then
            invalidArg "types" (SR.GetString (SR.nullsNotAllowedInArray))
        mkTupleType false asm types

    static member MakeTupleType (asm: Assembly, types: Type[])  =
        checkNonNull "types" types
        if types |> Array.exists (function null -> true | _ -> false) then
             invalidArg "types" (SR.GetString (SR.nullsNotAllowedInArray))
        mkTupleType false asm types

    static member MakeStructTupleType (asm: Assembly, types: Type[]) =
        checkNonNull "types" types
        if types |> Array.exists (function null -> true | _ -> false) then
             invalidArg "types" (SR.GetString (SR.nullsNotAllowedInArray))
        mkTupleType true asm types

    static member GetTupleElements (tupleType: Type) =
        checkTupleType("tupleType", tupleType)
        getTupleTypeInfo tupleType

    static member GetFunctionElements (functionType: Type) =
        checkNonNull "functionType" functionType
        let functionType = getTypeOfReprType (functionType, BindingFlags.Public ||| BindingFlags.NonPublic)
        getFunctionTypeInfo functionType

    static member GetRecordFields (recordType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkRecordType ("recordType", recordType, bindingFlags)
        fieldPropsOfRecordType(recordType, bindingFlags)

    static member GetUnionCases (unionType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionType" unionType
        let unionType = getTypeOfReprType (unionType, bindingFlags)
        checkUnionType (unionType, bindingFlags)
        getUnionTypeTagNameMap(unionType, bindingFlags) |> Array.mapi (fun i _ -> UnionCaseInfo(unionType, i))

    static member IsExceptionRepresentation (exceptionType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "exceptionType" exceptionType
        isExceptionRepr (exceptionType, bindingFlags)

    static member GetExceptionFields (exceptionType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "exceptionType" exceptionType
        checkExnType (exceptionType, bindingFlags)
        fieldPropsOfRecordType (exceptionType, bindingFlags)

type DynamicFunction<'T1, 'T2>() =
    inherit FSharpFunc<obj -> obj, obj>()
    override _.Invoke(impl: obj -> obj) : obj =
        box<('T1 -> 'T2)> (fun inp -> unbox<'T2>(impl (box<'T1>(inp))))

[<AbstractClass; Sealed>]
type FSharpValue =

    static member MakeRecord (recordType: Type, values, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkRecordType ("recordType", recordType, bindingFlags)
        getRecordConstructor (recordType, bindingFlags) values

    static member GetRecordField(record: obj, info: PropertyInfo) =
        checkNonNull "info" info
        checkNonNull "record" record
        let reprty = record.GetType()
        if not (isRecordType (reprty, BindingFlags.Public ||| BindingFlags.NonPublic)) then
            invalidArg "record" (SR.GetString (SR.objIsNotARecord))
        info.GetValue (record, null)

    static member GetRecordFields (record: obj, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "record" record
        let typ = record.GetType()
        if not (isRecordType (typ, bindingFlags)) then
            invalidArg "record" (SR.GetString (SR.objIsNotARecord))
        getRecordReader (typ, bindingFlags) record

    static member PreComputeRecordFieldReader(info: PropertyInfo): obj -> obj =
        checkNonNull "info" info
        compilePropGetterFunc(info).Invoke

    static member PreComputeRecordReader(recordType: Type, ?bindingFlags) : (obj -> obj[]) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkRecordType ("recordType", recordType, bindingFlags)
        getRecordReaderCompiled (recordType, bindingFlags)

    static member PreComputeRecordConstructor(recordType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkRecordType ("recordType", recordType, bindingFlags)
        getRecordConstructorCompiled (recordType, bindingFlags)

    static member PreComputeRecordConstructorInfo(recordType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkRecordType ("recordType", recordType, bindingFlags)
        getRecordConstructorMethod(recordType, bindingFlags)

    static member MakeFunction(functionType: Type, implementation:(obj->obj)) =
        checkNonNull "functionType" functionType
        if not (isFunctionType functionType) then
            invalidArg "functionType" (String.Format (SR.GetString (SR.notAFunctionType), functionType.FullName))
        checkNonNull "implementation" implementation
        let domain, range = getFunctionTypeInfo functionType
        let dynCloMakerTy = typedefof<DynamicFunction<obj, obj>>
        let saverTy = dynCloMakerTy.MakeGenericType [| domain; range |]
        let o = Activator.CreateInstance saverTy
        let (f : (obj -> obj) -> obj) = downcast o
        f implementation

    static member MakeTuple(tupleElements: obj[], tupleType: Type) =
        checkNonNull "tupleElements" tupleElements
        checkTupleType("tupleType", tupleType)
        getTupleConstructor tupleType tupleElements

    static member GetTupleFields(tuple: obj) = // argument name(s) used in error message
        checkNonNull "tuple" tuple
        let typ = tuple.GetType()
        if not (isTupleType typ ) then
            invalidArg "tuple" (String.Format (SR.GetString (SR.notATupleType), tuple.GetType().FullName))
        getTupleReader typ tuple

    static member GetTupleField(tuple: obj, index: int) = // argument name(s) used in error message
        checkNonNull "tuple" tuple
        let typ = tuple.GetType()
        if not (isTupleType typ ) then
            invalidArg "tuple" (String.Format (SR.GetString (SR.notATupleType), tuple.GetType().FullName))
        let fields = getTupleReader typ tuple
        if index < 0 || index >= fields.Length then
            invalidArg "index" (String.Format (SR.GetString (SR.tupleIndexOutOfRange), tuple.GetType().FullName, index.ToString()))
        fields.[index]

    static member PreComputeTupleReader(tupleType: Type) : (obj -> obj[])  =
        checkTupleType("tupleType", tupleType)
        (compileTupleReader tupleEncField getTupleElementAccessors tupleType).Invoke

    static member PreComputeTuplePropertyInfo(tupleType: Type, index: int) =
        checkTupleType("tupleType", tupleType)
        getTupleReaderInfo (tupleType, index)

    static member PreComputeTupleConstructor(tupleType: Type) =
        checkTupleType("tupleType", tupleType)
        (compileTupleConstructor tupleEncField getTupleConstructorMethod tupleType).Invoke

    static member PreComputeTupleConstructorInfo(tupleType: Type) =
        checkTupleType("tupleType", tupleType)
        getTupleConstructorInfo tupleType

    static member MakeUnion(unionCase: UnionCaseInfo, args: obj [], ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionCase" unionCase
        getUnionCaseConstructor (unionCase.DeclaringType, unionCase.Tag, bindingFlags) args

    static member PreComputeUnionConstructor (unionCase: UnionCaseInfo, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionCase" unionCase
        getUnionCaseConstructorCompiled (unionCase.DeclaringType, unionCase.Tag, bindingFlags)

    static member PreComputeUnionConstructorInfo(unionCase: UnionCaseInfo, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionCase" unionCase
        getUnionCaseConstructorMethod (unionCase.DeclaringType, unionCase.Tag, bindingFlags)

    static member GetUnionFields(value: obj, unionType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        let ensureType (typ: Type, obj: obj) =
                match typ with
                | null ->
                    match obj with
                    | null -> invalidArg "obj" (SR.GetString (SR.objIsNullAndNoType))
                    | _ -> obj.GetType()
                | _ -> typ

        let unionType = ensureType(unionType, value)

        checkNonNull "unionType" unionType
        let unionType = getTypeOfReprType (unionType, bindingFlags)

        checkUnionType (unionType, bindingFlags)
        let tag = getUnionTagReader (unionType, bindingFlags) value
        let flds = getUnionCaseRecordReader (unionType, tag, bindingFlags) value
        UnionCaseInfo (unionType, tag), flds

    static member PreComputeUnionTagReader(unionType: Type, ?bindingFlags) : (obj -> int) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionType" unionType
        let unionType = getTypeOfReprType (unionType, bindingFlags)
        checkUnionType (unionType, bindingFlags)
        getUnionTagReaderCompiled (unionType, bindingFlags)

    static member PreComputeUnionTagMemberInfo(unionType: Type, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionType" unionType
        let unionType = getTypeOfReprType (unionType, bindingFlags)
        checkUnionType (unionType, bindingFlags)
        getUnionTagMemberInfo (unionType, bindingFlags)

    static member PreComputeUnionReader(unionCase: UnionCaseInfo, ?bindingFlags) : (obj -> obj[])  =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "unionCase" unionCase
        let typ = unionCase.DeclaringType
        getUnionCaseRecordReaderCompiled (typ, unionCase.Tag, bindingFlags)

    static member GetExceptionFields (exn: obj, ?bindingFlags) =
        let bindingFlags = defaultArg bindingFlags BindingFlags.Public
        checkNonNull "exn" exn
        let typ = exn.GetType()
        checkExnType (typ, bindingFlags)
        getRecordReader (typ, bindingFlags) exn

module FSharpReflectionExtensions =

    type FSharpType with

        static member GetExceptionFields (exceptionType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.GetExceptionFields (exceptionType, bindingFlags)

        static member IsExceptionRepresentation(exceptionType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.IsExceptionRepresentation (exceptionType, bindingFlags)

        static member GetUnionCases (unionType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.GetUnionCases (unionType, bindingFlags)

        static member GetRecordFields (recordType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.GetRecordFields (recordType, bindingFlags)

        static member IsUnion (typ: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.IsUnion (typ, bindingFlags)

        static member IsRecord(typ: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpType.IsRecord (typ, bindingFlags)

    type FSharpValue with
        static member MakeRecord(recordType: Type, values, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.MakeRecord (recordType, values, bindingFlags)

        static member GetRecordFields (record: obj, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.GetRecordFields (record, bindingFlags)

        static member PreComputeRecordReader(recordType: Type, ?allowAccessToPrivateRepresentation) : (obj -> obj[]) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeRecordReader (recordType, bindingFlags)

        static member PreComputeRecordConstructor(recordType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeRecordConstructor (recordType, bindingFlags)

        static member PreComputeRecordConstructorInfo(recordType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeRecordConstructorInfo (recordType, bindingFlags)

        static member MakeUnion(unionCase: UnionCaseInfo, args: obj [], ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.MakeUnion (unionCase, args, bindingFlags)

        static member PreComputeUnionConstructor (unionCase: UnionCaseInfo, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeUnionConstructor (unionCase, bindingFlags)

        static member PreComputeUnionConstructorInfo(unionCase: UnionCaseInfo, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeUnionConstructorInfo (unionCase, bindingFlags)

        static member PreComputeUnionTagMemberInfo(unionType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeUnionTagMemberInfo (unionType, bindingFlags)

        static member GetUnionFields(value: obj, unionType: Type, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.GetUnionFields (value, unionType, bindingFlags)

        static member PreComputeUnionTagReader(unionType: Type, ?allowAccessToPrivateRepresentation) : (obj -> int) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeUnionTagReader (unionType, bindingFlags)

        static member PreComputeUnionReader(unionCase: UnionCaseInfo, ?allowAccessToPrivateRepresentation) : (obj -> obj[])  =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.PreComputeUnionReader (unionCase, bindingFlags)

        static member GetExceptionFields (exn: obj, ?allowAccessToPrivateRepresentation) =
            let bindingFlags = getBindingFlags allowAccessToPrivateRepresentation
            FSharpValue.GetExceptionFields (exn, bindingFlags)

