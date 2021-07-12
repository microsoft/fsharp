// Copyright (c) Microsoft Corporation, Tomas Petricek, Gustavo Guerra, and other contributors
// 
// Licensed under the MIT License see LICENSE.md in this project

namespace ProviderImplementation.ProvidedTypes

#nowarn "1182"

// This file contains a set of helper types and methods for providing types in an implementation
// of ITypeProvider.
//
// This code has been modified and is appropriate for use in conjunction with the F# 4.x releases

open System
open System.Reflection
open System.Collections.Generic
open System.Diagnostics

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Core.CompilerServices

[<AutoOpen>]
module Utils = 
    let K x = (fun () -> x)
    let isNull x = match x with null -> true | _ -> false
    let isNil x = match x with [] -> true | _ -> false
    let isEmpty x = match x with [| |] -> true | _ -> false

    module Option = 
        let toObj x = match x with None -> null | Some x -> x
        let ofObj x = match x with null -> None | _ -> Some x

    [<Struct>]
    type StructOption<'T> (hasValue: bool, value: 'T) =
        member __.IsNone = not hasValue
        member __.HasValue = hasValue
        member __.Value = value
        override __.ToString() = if hasValue then match box value with null -> "null" | x -> x.ToString() else "<none>"

    type uoption<'T> = StructOption<'T>

    let UNone<'T> = uoption<'T>(false, Unchecked.defaultof<'T>)
    let USome v = uoption<'T>(true, v)
    let (|UNone|USome|) (x:uoption<'T>) = if x.HasValue then USome x.Value else UNone

    module StructOption = 
        let toObj x = match x with UNone -> null | USome x -> x
        let ofObj x = match x with null -> UNone | x -> USome x


    let tryFindMulti k map = match Map.tryFind k map with Some res -> res | None -> [| |]

    let splitNameAt (nm:string) idx =
        if idx < 0 then failwith "splitNameAt: idx < 0";
        let last = nm.Length - 1
        if idx > last then failwith "splitNameAt: idx > last";
        (nm.Substring(0, idx)), 
        (if idx < last then nm.Substring (idx+1, last - idx) else "")

    let splitILTypeName (nm:string) =
        match nm.LastIndexOf '.' with
        | -1 -> UNone, nm
        | idx -> let a, b = splitNameAt nm idx in USome a, b

    let joinILTypeName (nspace: string uoption) (nm:string) =
        match nspace with
        | UNone -> nm
        | USome ns -> ns + "." + nm

    let lengthsEqAndForall2 (arr1: 'T1[]) (arr2: 'T2[]) f =
        (arr1.Length = arr2.Length) &&
        (arr1, arr2) ||> Array.forall2 f

    /// General implementation of .Equals(Type) logic for System.Type over symbol types. You can use this with other types too.
    let rec eqTypes (ty1: Type) (ty2: Type) =
        if Object.ReferenceEquals(ty1, ty2) then true
        elif ty1.IsGenericTypeDefinition then ty2.IsGenericTypeDefinition && ty1.Equals(ty2)
        elif ty1.IsGenericType then ty2.IsGenericType && not ty2.IsGenericTypeDefinition && eqTypes (ty1.GetGenericTypeDefinition()) (ty2.GetGenericTypeDefinition()) && lengthsEqAndForall2 (ty1.GetGenericArguments()) (ty2.GetGenericArguments()) eqTypes
        elif ty1.IsArray then ty2.IsArray && ty1.GetArrayRank() = ty2.GetArrayRank() && eqTypes (ty1.GetElementType()) (ty2.GetElementType())
        elif ty1.IsPointer then ty2.IsPointer && eqTypes (ty1.GetElementType()) (ty2.GetElementType())
        elif ty1.IsByRef then ty2.IsByRef && eqTypes (ty1.GetElementType()) (ty2.GetElementType())
        else ty1.Equals(box ty2)

    /// General implementation of .Equals(obj) logic for System.Type over symbol types. You can use this with other types too.
    let eqTypeObj (this: Type) (other: obj) =
        match other with
        | :? Type as otherTy -> eqTypes this otherTy
        | _ -> false

    /// General implementation of .IsAssignableFrom logic for System.Type, regardless of specific implementation
    let isAssignableFrom (ty: Type) (otherTy: Type) =
        eqTypes ty otherTy || (match otherTy.BaseType with null -> false | bt -> ty.IsAssignableFrom(bt))

    /// General implementation of .IsSubclassOf logic for System.Type, regardless of specific implementation, with 
    /// an added hack to make the types usable with the FSharp.Core quotations implementation
    let isSubclassOf (this: Type) (otherTy: Type) =
        (this.IsClass && otherTy.IsClass && this.IsAssignableFrom(otherTy) && not (eqTypes this otherTy))
        // The FSharp.Core implementation of FSharp.Quotations uses
        //      let isDelegateType (typ:Type) = 
        //          if typ.IsSubclassOf(typeof<Delegate>) then ...
        // This means even target type definitions must process the case where ``otherTy`` is typeof<Delegate> rather than
        // the System.Delegate type for the target assemblies.
        || (match this.BaseType with 
            | null -> false 
            | bt -> bt.FullName = "System.MulticastDelegate" && (let fn = otherTy.FullName in fn = "System.Delegate" || fn = "System.MulticastDelegate" ))


    /// General implementation of .GetAttributeFlags logic for System.Type over symbol types 
    let getAttributeFlagsImpl (ty: Type) = 
        if ty.IsGenericType then ty.GetGenericTypeDefinition().Attributes
        elif ty.IsArray then typeof<int[]>.Attributes
        elif ty.IsPointer then typeof<int>.MakePointerType().Attributes
        elif ty.IsByRef then typeof<int>.MakeByRefType().Attributes
        else Unchecked.defaultof<TypeAttributes>

    let bindAll = BindingFlags.DeclaredOnly ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Static ||| BindingFlags.Instance
    let bindCommon = BindingFlags.DeclaredOnly ||| BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.Public
    let bindSome isStatic = BindingFlags.DeclaredOnly ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| (if isStatic then BindingFlags.Static else BindingFlags.Instance)
    let inline hasFlag e flag = (e &&& flag) <> enum 0

    let memberBinds isType (bindingFlags: BindingFlags) isStatic isPublic = 
        (isType || hasFlag bindingFlags (if isStatic then BindingFlags.Static else BindingFlags.Instance)) &&
        ((hasFlag bindingFlags BindingFlags.Public && isPublic) || (hasFlag bindingFlags BindingFlags.NonPublic && not isPublic))

    [<Interface>]
    type ITypeBuilder =
        abstract MakeGenericType: Type * Type[] -> Type
        abstract MakeArrayType: Type -> Type
        abstract MakeRankedArrayType: Type*int -> Type
        abstract MakeByRefType: Type -> Type
        abstract MakePointerType: Type -> Type

    let defaultTypeBuilder =
        { new ITypeBuilder with
            member __.MakeGenericType(typeDef, args) = typeDef.MakeGenericType(args)
            member __.MakeArrayType(typ) = typ.MakeArrayType()
            member __.MakeRankedArrayType(typ, rank) = typ.MakeArrayType(rank)
            member __.MakeByRefType(typ) = typ.MakeByRefType()
            member __.MakePointerType(typ) = typ.MakePointerType() }

    let rec instType (typeBuilder: ITypeBuilder) inst (ty:Type) =
        if isNull ty then null
        elif ty.IsGenericType then
            let typeArgs = Array.map (instType typeBuilder inst) (ty.GetGenericArguments())
            typeBuilder.MakeGenericType(ty.GetGenericTypeDefinition(), typeArgs)
        elif ty.HasElementType then
            let ety : Type = instType typeBuilder inst (ty.GetElementType())
            if ty.IsArray then
                let rank = ty.GetArrayRank()
                if rank = 1 then typeBuilder.MakeArrayType(ety)
                else typeBuilder.MakeRankedArrayType(ety,rank)
            elif ty.IsPointer then typeBuilder.MakePointerType(ety)
            elif ty.IsByRef then typeBuilder.MakeByRefType(ety)
            else ty
        elif ty.IsGenericParameter then
            let pos = ty.GenericParameterPosition
            let (inst1: Type[], inst2: Type[]) = inst
            if pos < inst1.Length then inst1.[pos]
            elif pos < inst1.Length + inst2.Length then inst2.[pos - inst1.Length]
            else ty
        else ty


    let mutable token = 0 
    let genToken() =  token <- token + 1; token
    /// Internal code of .NET expects the obj[] returned by GetCustomAttributes to be an Attribute[] even in the case of empty arrays
    let emptyAttributes = (([| |]: Attribute[]) |> box |> unbox<obj[]>)
        
    type Attributes<'T when 'T :> Attribute>() = 
        static let empty = ([| |] : 'T []) |> box |> unbox<obj[]>
        static member Empty() = empty
 
    type Attributes = 
        static member CreateEmpty (typ : Type) =
            let gtype = typedefof<Attributes<_>>.MakeGenericType([| typ |])
            // the Empty member is private due to the presence of the fsi file
            // but when getting rid of the fsi for diagnostic purpose, it becomes public
            // this is the reason for having both Public and NonPublic flag bellow
            let gmethod = gtype.GetMethod("Empty", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            gmethod.Invoke(null, [||]) :?> obj array

    let nonNull str x = if isNull x then failwithf "Null in '%s', stacktrace = '%s'" str Environment.StackTrace else x
    let nonNone str x = match x with None -> failwithf "No value has been specified for '%s', stacktrace = '%s'" str Environment.StackTrace | Some v -> v
    let patchOption v f = match v with None -> f() | Some _ -> failwithf "Already patched, stacktrace = '%s'" Environment.StackTrace 

    let notRequired this opname item =
        let msg = sprintf "The operation '%s' on item '%s' should not be called on provided type, member or parameter of type '%O'. Stack trace:\n%s" opname item (this.GetType()) Environment.StackTrace
        Debug.Assert (false, msg)
        raise (NotSupportedException msg)


    let adjustTypeAttributes isNested attrs =
        let visibilityAttributes =
            match attrs &&& TypeAttributes.VisibilityMask with
            | TypeAttributes.Public when isNested -> TypeAttributes.NestedPublic
            | TypeAttributes.NotPublic when isNested -> TypeAttributes.NestedAssembly
            | TypeAttributes.NestedPublic when not isNested -> TypeAttributes.Public
            | TypeAttributes.NestedAssembly
            | TypeAttributes.NestedPrivate
            | TypeAttributes.NestedFamORAssem
            | TypeAttributes.NestedFamily
            | TypeAttributes.NestedFamANDAssem when not isNested -> TypeAttributes.NotPublic
            | a -> a
        (attrs &&& ~~~TypeAttributes.VisibilityMask) ||| visibilityAttributes


    type ConstructorInfo with
        member m.GetDefinition() = 
            let dty = m.DeclaringType
            if (dty.IsGenericType && not dty.IsGenericTypeDefinition) then 
                // Search through the original type definition looking for the one with a matching metadata token
                let gdty = dty.GetGenericTypeDefinition()
                gdty.GetConstructors(bindAll) 
                |> Array.tryFind (fun c -> c.MetadataToken = m.MetadataToken)
                |> function Some m2 -> m2 | None -> failwithf "couldn't rebind %O::%s back to generic constructor definition via metadata token, stacktrace = '%s'" m.DeclaringType m.Name Environment.StackTrace
            else
                m

    type PropertyInfo  with
        member m.GetDefinition() = 
            let dty = m.DeclaringType
            if (dty.IsGenericType && not dty.IsGenericTypeDefinition) then 
                // Search through the original type definition looking for the one with a matching metadata token
                let gdty = dty.GetGenericTypeDefinition()
                gdty.GetProperties(bindAll) 
                |> Array.tryFind (fun c -> c.MetadataToken = m.MetadataToken)
                |> function Some m2 -> m2 | None -> failwithf "couldn't rebind %O::%s back to generic property definition via metadata token" m.DeclaringType m.Name
            else
                m

        member p.IsStatic = p.CanRead && p.GetGetMethod(true).IsStatic || p.CanWrite && p.GetSetMethod(true).IsStatic
        member p.IsPublic = p.CanRead && p.GetGetMethod(true).IsPublic || p.CanWrite && p.GetSetMethod(true).IsPublic

    type EventInfo  with
        member m.GetDefinition() = 
            let dty = m.DeclaringType
            if (dty.IsGenericType && not dty.IsGenericTypeDefinition) then 
                // Search through the original type definition looking for the one with a matching metadata token
                let gdty = dty.GetGenericTypeDefinition()
                gdty.GetEvents(bindAll) 
                |> Array.tryFind (fun c -> c.MetadataToken = m.MetadataToken)
                |> function Some m2 -> m2 | None -> failwithf "couldn't rebind %O::%s back to generic event definition via metadata token" m.DeclaringType m.Name
            else
                m

        member p.IsStatic = p.GetAddMethod().IsStatic || p.GetRemoveMethod().IsStatic
        member p.IsPublic = p.GetAddMethod().IsPublic || p.GetRemoveMethod().IsPublic

    type FieldInfo  with
        member m.GetDefinition() = 
            let dty = m.DeclaringType
            if (dty.IsGenericType && not dty.IsGenericTypeDefinition) then 
                // Search through the original type definition looking for the one with a matching metadata token
                let gdty = dty.GetGenericTypeDefinition()
                gdty.GetFields(bindAll) 
                |> Array.tryFind (fun c -> c.MetadataToken = m.MetadataToken)
                |> function Some m2 -> m2 | None -> failwithf "couldn't rebind %O::%s back to generic event definition via metadata token" m.DeclaringType m.Name
            else
                m

    type MethodInfo with
        member m.GetDefinition() = 
            let dty = m.DeclaringType
            if (m.IsGenericMethod && not dty.IsGenericType) then m.GetGenericMethodDefinition()
            elif (m.IsGenericMethod && (not m.IsGenericMethodDefinition || not dty.IsGenericTypeDefinition)) || 
                    (dty.IsGenericType && not dty.IsGenericTypeDefinition) then 

                // Search through ALL the methods on the original type definition looking for the one
                // with a matching metadata token
                let gdty = if dty.IsGenericType then dty.GetGenericTypeDefinition() else dty
                gdty.GetMethods(bindSome m.IsStatic) 
                |> Array.tryFind (fun c -> c.MetadataToken = m.MetadataToken)
                |> function Some m2 -> m2 | None -> failwithf "couldn't rebind generic instantiation of %O::%s back to generic method definition via metadata token" m.DeclaringType m.Name

            else
                m 

    let canBindConstructor (bindingFlags: BindingFlags) (c: ConstructorInfo) =
            hasFlag bindingFlags BindingFlags.Public && c.IsPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsPublic

    let canBindMethod (bindingFlags: BindingFlags) (c: MethodInfo) =
            hasFlag bindingFlags BindingFlags.Public && c.IsPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsPublic

    let canBindProperty (bindingFlags: BindingFlags) (c: PropertyInfo) =
            hasFlag bindingFlags BindingFlags.Public && c.IsPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsPublic

    let canBindField (bindingFlags: BindingFlags) (c: FieldInfo) =
            hasFlag bindingFlags BindingFlags.Public && c.IsPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsPublic

    let canBindEvent (bindingFlags: BindingFlags) (c: EventInfo) =
            hasFlag bindingFlags BindingFlags.Public && c.IsPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsPublic

    let canBindNestedType (bindingFlags: BindingFlags) (c: Type) =
            hasFlag bindingFlags BindingFlags.Public && c.IsNestedPublic || hasFlag bindingFlags BindingFlags.NonPublic && not c.IsNestedPublic

    // We only want to return source types "typeof<Void>" values as _target_ types in one very specific location due to a limitation in the
    // F# compiler code for multi-targeting.
    let ImportProvidedMethodBaseAsILMethodRef_OnStack_HACK() = 
        let rec loop i = 
            if i > 9 then 
                false 
            else
                let frame = StackFrame(i, true)
                match frame.GetMethod() with
                | null -> loop (i+1)
                | m -> m.Name = "ImportProvidedMethodBaseAsILMethodRef" || loop (i+1)
        loop 1

//--------------------------------------------------------------------------------
// UncheckedQuotations

// The FSharp.Core 2.0 - 4.0 (4.0.0.0 - 4.4.0.0) quotations implementation is overly strict in that it doesn't allow
// generation of quotations for cross-targeted FSharp.Core.  Below we define a series of Unchecked methods
// implemented via reflection hacks to allow creation of various nodes when using a cross-targets FSharp.Core and
// mscorlib.dll.
//
//   - Most importantly, these cross-targeted quotations can be provided to the F# compiler by a type provider.
//     They are generally produced via the AssemblyReplacer.fs component through a process of rewriting design-time quotations that
//     are not cross-targeted.
//
//   - However, these quotation values are a bit fragile. Using existing FSharp.Core.Quotations.Patterns
//     active patterns on these quotation nodes will generally work correctly. But using ExprShape.RebuildShapeCombination
//     on these new nodes will not succed, nor will operations that build new quotations such as Expr.Call.
//     Instead, use the replacement provided in this module.
//
//   - Likewise, some operations in these quotation values like "expr.Type" may be a bit fragile, possibly returning non cross-targeted types in
//     the result. However those operations are not used by the F# compiler.
[<AutoOpen>]
module UncheckedQuotations =

    let qTy = typeof<Var>.Assembly.GetType("Microsoft.FSharp.Quotations.ExprConstInfo")
    assert (not (isNull qTy))

    let pTy = typeof<Var>.Assembly.GetType("Microsoft.FSharp.Quotations.PatternsModule")
    assert (not (isNull pTy))

    // These are handles to the internal functions that create quotation nodes of different sizes. Although internal, 
    // these function names have been stable since F# 2.0.
    let mkFE0 = pTy.GetMethod("mkFE0", bindAll)
    assert (not (isNull mkFE0))

    let mkFE1 = pTy.GetMethod("mkFE1", bindAll)
    assert (not (isNull mkFE1))

    let mkFE2 = pTy.GetMethod("mkFE2", bindAll)
    assert (mkFE2 |> isNull |> not)

    let mkFE3 = pTy.GetMethod("mkFE3", bindAll)
    assert (mkFE3 |> isNull |> not)

    let mkFEN = pTy.GetMethod("mkFEN", bindAll)
    assert (mkFEN |> isNull |> not)

    // These are handles to the internal tags attached to quotation nodes of different sizes. Although internal, 
    // these function names have been stable since F# 2.0.
    let newDelegateOp = qTy.GetMethod("NewNewDelegateOp", bindAll)
    assert (newDelegateOp |> isNull |> not)

    let instanceCallOp = qTy.GetMethod("NewInstanceMethodCallOp", bindAll)
    assert (instanceCallOp |> isNull |> not)

    let staticCallOp = qTy.GetMethod("NewStaticMethodCallOp", bindAll)
    assert (staticCallOp |> isNull |> not)

    let newObjectOp = qTy.GetMethod("NewNewObjectOp", bindAll)
    assert (newObjectOp |> isNull |> not)

    let newArrayOp = qTy.GetMethod("NewNewArrayOp", bindAll)
    assert (newArrayOp |> isNull |> not)

    let appOp = qTy.GetMethod("get_AppOp", bindAll)
    assert (appOp |> isNull |> not)

    let instancePropGetOp = qTy.GetMethod("NewInstancePropGetOp", bindAll)
    assert (instancePropGetOp |> isNull |> not)

    let staticPropGetOp = qTy.GetMethod("NewStaticPropGetOp", bindAll)
    assert (staticPropGetOp |> isNull |> not)

    let instancePropSetOp = qTy.GetMethod("NewInstancePropSetOp", bindAll)
    assert (instancePropSetOp |> isNull |> not)

    let staticPropSetOp = qTy.GetMethod("NewStaticPropSetOp", bindAll)
    assert (staticPropSetOp |> isNull |> not)

    let instanceFieldGetOp = qTy.GetMethod("NewInstanceFieldGetOp", bindAll)
    assert (instanceFieldGetOp |> isNull |> not)

    let staticFieldGetOp = qTy.GetMethod("NewStaticFieldGetOp", bindAll)
    assert (staticFieldGetOp |> isNull |> not)

    let instanceFieldSetOp = qTy.GetMethod("NewInstanceFieldSetOp", bindAll)
    assert (instanceFieldSetOp |> isNull |> not)

    let staticFieldSetOp = qTy.GetMethod("NewStaticFieldSetOp", bindAll)
    assert (staticFieldSetOp |> isNull |> not)

    let tupleGetOp = qTy.GetMethod("NewTupleGetOp", bindAll)
    assert (tupleGetOp |> isNull |> not)

    let letOp = qTy.GetMethod("get_LetOp", bindAll)
    assert (letOp |> isNull |> not)

    let forIntegerRangeLoopOp = qTy.GetMethod("get_ForIntegerRangeLoopOp", bindAll)
    assert (forIntegerRangeLoopOp |> isNull |> not)

    let whileLoopOp = qTy.GetMethod("get_WhileLoopOp", bindAll)
    assert (whileLoopOp |> isNull |> not)

    let ifThenElseOp = qTy.GetMethod("get_IfThenElseOp", bindAll)
    assert (ifThenElseOp |> isNull |> not)

    let newUnionCaseOp = qTy.GetMethod("NewNewUnionCaseOp", bindAll)
    assert (newUnionCaseOp |> isNull |> not)

    let newRecordOp = qTy.GetMethod("NewNewRecordOp", bindAll)
    assert (newRecordOp |> isNull |> not)

    type Microsoft.FSharp.Quotations.Expr with

        static member NewDelegateUnchecked (ty: Type, vs: Var list, body: Expr) =
            let e =  List.foldBack (fun v acc -> Expr.Lambda(v, acc)) vs body
            let op = newDelegateOp.Invoke(null, [| box ty |])
            mkFE1.Invoke(null, [| box op; box e |]) :?> Expr

        static member NewObjectUnchecked (cinfo: ConstructorInfo, args: Expr list) =
            let op = newObjectOp.Invoke(null, [| box cinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member NewArrayUnchecked (elementType: Type, elements: Expr list) =
            let op = newArrayOp.Invoke(null, [| box elementType |])
            mkFEN.Invoke(null, [| box op; box elements |]) :?> Expr

        static member CallUnchecked (minfo: MethodInfo, args: Expr list) =
            let op = staticCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member CallUnchecked (obj: Expr, minfo: MethodInfo, args: Expr list) =
            let op = instanceCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr

        static member ApplicationUnchecked (f: Expr, x: Expr) =
            let op = appOp.Invoke(null, [| |])
            mkFE2.Invoke(null, [| box op; box f; box x |]) :?> Expr

        static member PropertyGetUnchecked (pinfo: PropertyInfo, args: Expr list) =
            let op = staticPropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member PropertyGetUnchecked (obj: Expr, pinfo: PropertyInfo, ?args: Expr list) =
            let args = defaultArg args []
            let op = instancePropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr

        static member PropertySetUnchecked (pinfo: PropertyInfo, value: Expr, ?args: Expr list) =
            let args = defaultArg args []
            let op = staticPropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (args@[value]) |]) :?> Expr

        static member PropertySetUnchecked (obj: Expr, pinfo: PropertyInfo, value: Expr, ?args: Expr list) =
            let args = defaultArg args []
            let op = instancePropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::(args@[value])) |]) :?> Expr

        static member FieldGetUnchecked (pinfo: FieldInfo) =
            let op = staticFieldGetOp.Invoke(null, [| box pinfo |])
            mkFE0.Invoke(null, [| box op; |]) :?> Expr

        static member FieldGetUnchecked (obj: Expr, pinfo: FieldInfo) =
            let op = instanceFieldGetOp.Invoke(null, [| box pinfo |])
            mkFE1.Invoke(null, [| box op; box obj |]) :?> Expr

        static member FieldSetUnchecked (pinfo: FieldInfo, value: Expr) =
            let op = staticFieldSetOp.Invoke(null, [| box pinfo |])
            mkFE1.Invoke(null, [| box op; box value |]) :?> Expr

        static member FieldSetUnchecked (obj: Expr, pinfo: FieldInfo, value: Expr) =
            let op = instanceFieldSetOp.Invoke(null, [| box pinfo |])
            mkFE2.Invoke(null, [| box op; box obj; box value |]) :?> Expr

        static member TupleGetUnchecked (e: Expr, n:int) =
            let op = tupleGetOp.Invoke(null, [| box e.Type; box n |])
            mkFE1.Invoke(null, [| box op; box e |]) :?> Expr

        static member LetUnchecked (v:Var, e: Expr, body:Expr) =
            let lam = Expr.Lambda(v, body)
            let op = letOp.Invoke(null, [| |])
            mkFE2.Invoke(null, [| box op; box e; box lam |]) :?> Expr

        static member ForIntegerRangeLoopUnchecked (loopVariable, startExpr:Expr, endExpr:Expr, body:Expr) = 
            let lam = Expr.Lambda(loopVariable, body)
            let op = forIntegerRangeLoopOp.Invoke(null, [| |])
            mkFE3.Invoke(null, [| box op; box startExpr; box endExpr; box lam |] ) :?> Expr

        static member WhileLoopUnchecked (guard:Expr, body:Expr) = 
            let op = whileLoopOp.Invoke(null, [| |])
            mkFE2.Invoke(null, [| box op; box guard; box body |] ):?> Expr

        static member IfThenElseUnchecked (e:Expr, t:Expr, f:Expr) = 
            let op = ifThenElseOp.Invoke(null, [| |])
            mkFE3.Invoke(null, [| box op; box e; box t; box f |] ):?> Expr

        static member NewUnionCaseUnchecked (uci:Reflection.UnionCaseInfo, args:Expr list) = 
            let op = newUnionCaseOp.Invoke(null, [| box uci |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member NewRecordUnchecked (ty:Type, args:Expr list) =
            let op = newRecordOp.Invoke(null, [| box ty |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

    type Shape = Shape of (Expr list -> Expr)

    let (|ShapeCombinationUnchecked|ShapeVarUnchecked|ShapeLambdaUnchecked|) e =
        match e with
        | NewObject (cinfo, args) ->
            ShapeCombinationUnchecked (Shape (function args -> Expr.NewObjectUnchecked (cinfo, args)), args)
        | NewArray (ty, args) ->
            ShapeCombinationUnchecked (Shape (function args -> Expr.NewArrayUnchecked (ty, args)), args)
        | NewDelegate (t, vars, expr) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.NewDelegateUnchecked (t, vars, expr) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | TupleGet (expr, n) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, n) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | Application (f, x) ->
            ShapeCombinationUnchecked (Shape (function [f; x] -> Expr.ApplicationUnchecked (f, x) | _ -> invalidArg "expr" "invalid shape"), [f; x])
        | Call (objOpt, minfo, args) ->
            match objOpt with
            | None -> ShapeCombinationUnchecked (Shape (function args -> Expr.CallUnchecked (minfo, args)), args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::args) -> Expr.CallUnchecked (obj, minfo, args) | _ -> invalidArg "expr" "invalid shape"), obj::args)
        | PropertyGet (objOpt, pinfo, args) ->
            match objOpt with
            | None -> ShapeCombinationUnchecked (Shape (function args -> Expr.PropertyGetUnchecked (pinfo, args)), args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::args) -> Expr.PropertyGetUnchecked (obj, pinfo, args) | _ -> invalidArg "expr" "invalid shape"), obj::args)
        | PropertySet (objOpt, pinfo, args, value) ->
            match objOpt with
            | None -> ShapeCombinationUnchecked (Shape (function (value::args) -> Expr.PropertySetUnchecked (pinfo, value, args) | _ -> invalidArg "expr" "invalid shape"), value::args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::value::args) -> Expr.PropertySetUnchecked (obj, pinfo, value, args) | _ -> invalidArg "expr" "invalid shape"), obj::value::args)
        | FieldGet (objOpt, pinfo) ->
            match objOpt with
            | None -> ShapeCombinationUnchecked (Shape (function _ -> Expr.FieldGetUnchecked (pinfo)), [])
            | Some obj -> ShapeCombinationUnchecked (Shape (function [obj] -> Expr.FieldGetUnchecked (obj, pinfo) | _ -> invalidArg "expr" "invalid shape"), [obj])
        | FieldSet (objOpt, pinfo, value) ->
            match objOpt with
            | None -> ShapeCombinationUnchecked (Shape (function [value] -> Expr.FieldSetUnchecked (pinfo, value) | _ -> invalidArg "expr" "invalid shape"), [value])
            | Some obj -> ShapeCombinationUnchecked (Shape (function [obj;value] -> Expr.FieldSetUnchecked (obj, pinfo, value) | _ -> invalidArg "expr" "invalid shape"), [obj; value])
        | Let (var, value, body) ->
            ShapeCombinationUnchecked (Shape (function [value;Lambda(var, body)] -> Expr.LetUnchecked(var, value, body) | _ -> invalidArg "expr" "invalid shape"), [value; Expr.Lambda(var, body)])
        | ForIntegerRangeLoop (loopVar, first, last, body) ->
            ShapeCombinationUnchecked (Shape (function [first; last; Lambda(loopVar, body)] -> Expr.ForIntegerRangeLoopUnchecked (loopVar, first, last, body) | _ -> invalidArg "expr" "invalid shape"), [first; last; Expr.Lambda(loopVar, body)])
        | WhileLoop (cond, body) ->
            ShapeCombinationUnchecked (Shape (function [cond; body] -> Expr.WhileLoopUnchecked (cond, body) | _ -> invalidArg "expr" "invalid shape"), [cond; body])
        | IfThenElse (g, t, e) ->
            ShapeCombinationUnchecked (Shape (function [g; t; e] -> Expr.IfThenElseUnchecked (g, t, e) | _ -> invalidArg "expr" "invalid shape"), [g; t; e])
        | TupleGet (expr, i) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, i) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | ExprShape.ShapeCombination (comb, args) ->
            ShapeCombinationUnchecked (Shape (fun args -> ExprShape.RebuildShapeCombination(comb, args)), args)
        | ExprShape.ShapeVar v -> ShapeVarUnchecked v
        | ExprShape.ShapeLambda (v, e) -> ShapeLambdaUnchecked (v, e)

    let RebuildShapeCombinationUnchecked (Shape comb, args) = comb args

//--------------------------------------------------------------------------------
// Instantiated symbols
//

/// Represents the type constructor in a provided symbol type.
[<NoComparison>]
type ProvidedTypeSymbolKind =
    | SDArray
    | Array of int
    | Pointer
    | ByRef
    | Generic of Type
    | FSharpTypeAbbreviation of (Assembly * string * string[])


/// Represents an array or other symbolic type involving a provided type as the argument.
/// See the type provider spec for the methods that must be implemented.
/// Note that the type provider specification does not require us to implement pointer-equality for provided types.
type ProvidedTypeSymbol(kind: ProvidedTypeSymbolKind, typeArgs: Type list, typeBuilder: ITypeBuilder) as this =
    inherit TypeDelegator()
    let typeArgs = Array.ofList typeArgs

    do this.typeImpl <- this

    /// Substitute types for type variables.
    override __.FullName =
        match kind, typeArgs with
        | ProvidedTypeSymbolKind.SDArray, [| arg |] -> arg.FullName + "[]"
        | ProvidedTypeSymbolKind.Array _, [| arg |] -> arg.FullName + "[*]"
        | ProvidedTypeSymbolKind.Pointer, [| arg |] -> arg.FullName + "*"
        | ProvidedTypeSymbolKind.ByRef, [| arg |] -> arg.FullName + "&"
        | ProvidedTypeSymbolKind.Generic gty, typeArgs -> gty.FullName + "[" + (typeArgs |> Array.map (fun arg -> arg.ToString()) |> String.concat ",") + "]"
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation (_, nsp, path), typeArgs -> String.concat "." (Array.append [| nsp |] path) + (match typeArgs with [| |] -> "" | _ -> typeArgs.ToString())
        | _ -> failwith "unreachable"

    /// Although not strictly required by the type provider specification, this is required when doing basic operations like FullName on
    /// .NET symbolic types made from this type, e.g. when building Nullable<SomeProvidedType[]>.FullName
    override __.DeclaringType =
        match kind with
        | ProvidedTypeSymbolKind.SDArray -> null
        | ProvidedTypeSymbolKind.Array _ -> null
        | ProvidedTypeSymbolKind.Pointer -> null
        | ProvidedTypeSymbolKind.ByRef -> null
        | ProvidedTypeSymbolKind.Generic gty -> gty.DeclaringType
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation _ -> null

    override __.Name = 
        match kind, typeArgs with
        | ProvidedTypeSymbolKind.SDArray, [| arg |] -> arg.Name + "[]"
        | ProvidedTypeSymbolKind.Array _, [| arg |] -> arg.Name + "[*]"
        | ProvidedTypeSymbolKind.Pointer, [| arg |] -> arg.Name + "*"
        | ProvidedTypeSymbolKind.ByRef, [| arg |] -> arg.Name + "&"
        | ProvidedTypeSymbolKind.Generic gty, _typeArgs -> gty.Name
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation (_, _, path), _ -> path.[path.Length-1]
        | _ -> failwith "unreachable"

    override __.BaseType =
        match kind with
        | ProvidedTypeSymbolKind.SDArray -> typeof<Array>
        | ProvidedTypeSymbolKind.Array _ -> typeof<Array>
        | ProvidedTypeSymbolKind.Pointer -> typeof<ValueType>
        | ProvidedTypeSymbolKind.ByRef -> typeof<ValueType>
        | ProvidedTypeSymbolKind.Generic gty  ->
            if isNull gty.BaseType then null else
            instType typeBuilder (typeArgs, [| |]) gty.BaseType
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation _ -> typeof<obj>

    override __.GetArrayRank() = (match kind with ProvidedTypeSymbolKind.Array n -> n | ProvidedTypeSymbolKind.SDArray -> 1 | _ -> failwithf "non-array type '%O'" this)
    override __.IsValueTypeImpl() = (match kind with ProvidedTypeSymbolKind.Generic gtd -> gtd.IsValueType | _ -> false)
    override __.IsArrayImpl() = (match kind with ProvidedTypeSymbolKind.Array _ | ProvidedTypeSymbolKind.SDArray -> true | _ -> false)
    override __.IsByRefImpl() = (match kind with ProvidedTypeSymbolKind.ByRef _ -> true | _ -> false)
    override __.IsPointerImpl() = (match kind with ProvidedTypeSymbolKind.Pointer _ -> true | _ -> false)
    override __.IsPrimitiveImpl() = false
    override __.IsGenericType = (match kind with ProvidedTypeSymbolKind.Generic _ -> true | _ -> false)
    override this.GetGenericArguments() = (match kind with ProvidedTypeSymbolKind.Generic _ -> typeArgs |  _ -> failwithf "non-generic type '%O'" this)
    override this.GetGenericTypeDefinition() = (match kind with ProvidedTypeSymbolKind.Generic e -> e | _ -> failwithf "non-generic type '%O'" this)
    override __.IsCOMObjectImpl() = false
    override __.HasElementTypeImpl() = (match kind with ProvidedTypeSymbolKind.Generic _ -> false | _ -> true)
    override __.GetElementType() = (match kind, typeArgs with (ProvidedTypeSymbolKind.Array _  | ProvidedTypeSymbolKind.SDArray | ProvidedTypeSymbolKind.ByRef | ProvidedTypeSymbolKind.Pointer), [| e |] -> e | _ -> failwithf "not an array, pointer or byref type")

    override this.Assembly =
        match kind, typeArgs with
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation (assembly, _nsp, _path), _ -> assembly
        | ProvidedTypeSymbolKind.Generic gty, _ -> gty.Assembly
        | ProvidedTypeSymbolKind.SDArray, [| arg |] -> arg.Assembly
        | ProvidedTypeSymbolKind.Array _, [| arg |] -> arg.Assembly
        | ProvidedTypeSymbolKind.Pointer, [| arg |] -> arg.Assembly
        | ProvidedTypeSymbolKind.ByRef, [| arg |] -> arg.Assembly
        | _ -> notRequired this "Assembly" this.FullName

    override this.Namespace =
        match kind, typeArgs with
        | ProvidedTypeSymbolKind.SDArray, [| arg |] -> arg.Namespace
        | ProvidedTypeSymbolKind.Array _, [| arg |] -> arg.Namespace
        | ProvidedTypeSymbolKind.Pointer, [| arg |] -> arg.Namespace
        | ProvidedTypeSymbolKind.ByRef, [| arg |] -> arg.Namespace
        | ProvidedTypeSymbolKind.Generic gty, _ -> gty.Namespace
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation (_assembly, nsp, _path), _ -> nsp
        | _ -> notRequired this "Namespace" this.FullName

    override x.Module = x.Assembly.ManifestModule

    override __.GetHashCode()                                                                    =
        match kind, typeArgs with
        | ProvidedTypeSymbolKind.SDArray, [| arg |] -> 10 + hash arg
        | ProvidedTypeSymbolKind.Array _, [| arg |] -> 163 + hash arg
        | ProvidedTypeSymbolKind.Pointer, [| arg |] -> 283 + hash arg
        | ProvidedTypeSymbolKind.ByRef, [| arg |] -> 43904 + hash arg
        | ProvidedTypeSymbolKind.Generic gty, _ -> 9797 + hash gty + Array.sumBy hash typeArgs
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation _, _ -> 3092
        | _ -> failwith "unreachable"

    override this.Equals(other: obj) = eqTypeObj this other

    override this.Equals(otherTy: Type) = eqTypes this otherTy

    override this.IsAssignableFrom(otherTy: Type) = isAssignableFrom this otherTy

    override this.IsSubclassOf(otherTy: Type) = isSubclassOf this otherTy

    member __.Kind = kind

    member __.Args = typeArgs

    member __.IsFSharpTypeAbbreviation = match kind with FSharpTypeAbbreviation _ -> true | _ -> false

    // For example, int<kg>
    member __.IsFSharpUnitAnnotated = match kind with ProvidedTypeSymbolKind.Generic gtd -> not gtd.IsGenericTypeDefinition | _ -> false

    override __.GetConstructorImpl(_bindingFlags, _binder, _callConventions, _types, _modifiers) = null

    override this.GetMethodImpl(name, bindingFlags, _binderBinder, _callConvention, _types, _modifiers) =
        match kind with
        | Generic gtd ->
            let ty = gtd.GetGenericTypeDefinition().MakeGenericType(typeArgs)
            ty.GetMethod(name, bindingFlags)
        | _ -> notRequired this "GetMethodImpl" this.FullName


    override this.GetField(_name, _bindingFlags) = notRequired this "GetField" this.FullName

    override this.GetPropertyImpl(_name, _bindingFlags, _binder, _returnType, _types, _modifiers) = notRequired this "GetPropertyImpl" this.FullName

    override this.GetEvent(_name, _bindingFlags) = notRequired this "GetEvent" this.FullName

    override this.GetNestedType(_name, _bindingFlags) = notRequired this "GetNestedType" this.FullName

    override this.GetConstructors _bindingFlags = notRequired this "GetConstructors" this.FullName

    override this.GetMethods _bindingFlags = notRequired this "GetMethods" this.FullName

    override this.GetFields _bindingFlags = notRequired this "GetFields" this.FullName

    override this.GetProperties _bindingFlags = notRequired this "GetProperties" this.FullName

    override this.GetEvents _bindingFlags = notRequired this "GetEvents" this.FullName

    override this.GetNestedTypes _bindingFlags = notRequired this "GetNestedTypes" this.FullName

    override this.GetMembers _bindingFlags = notRequired this "GetMembers" this.FullName

    override this.GetInterface(_name, _ignoreCase) = notRequired this "GetInterface" this.FullName

    override this.GetInterfaces() = notRequired this "GetInterfaces" this.FullName

    override this.GetAttributeFlagsImpl() = getAttributeFlagsImpl this

    override this.UnderlyingSystemType =
        match kind with
        | ProvidedTypeSymbolKind.SDArray
        | ProvidedTypeSymbolKind.Array _
        | ProvidedTypeSymbolKind.Pointer
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation _
        | ProvidedTypeSymbolKind.ByRef -> upcast this
        | ProvidedTypeSymbolKind.Generic gty -> gty.UnderlyingSystemType

    override __.GetCustomAttributesData() =  ([| |] :> IList<_>)

    override this.MemberType = notRequired this "MemberType" this.FullName

    override this.GetMember(_name, _mt, _bindingFlags) = notRequired this "GetMember" this.FullName

    override this.GUID = notRequired this "GUID" this.FullName

    override this.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired this "InvokeMember" this.FullName

    override this.AssemblyQualifiedName = notRequired this "AssemblyQualifiedName" this.FullName

    override __.GetCustomAttributes(_inherit) = emptyAttributes

    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType

    override __.IsDefined(_attributeType, _inherit) = false

    override this.MakeArrayType() = ProvidedTypeSymbol(ProvidedTypeSymbolKind.SDArray, [this], typeBuilder) :> Type

    override this.MakeArrayType arg = ProvidedTypeSymbol(ProvidedTypeSymbolKind.Array arg, [this], typeBuilder) :> Type

#if NETCOREAPP
    // See bug https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/236
    override __.IsSZArray = 
        match kind with
        | ProvidedTypeSymbolKind.SDArray -> true
        | _ -> false
#endif

    override __.MetadataToken = 
        match kind with
        | ProvidedTypeSymbolKind.SDArray -> typeof<Array>.MetadataToken
        | ProvidedTypeSymbolKind.Array _ -> typeof<Array>.MetadataToken
        | ProvidedTypeSymbolKind.Pointer -> typeof<ValueType>.MetadataToken
        | ProvidedTypeSymbolKind.ByRef -> typeof<ValueType>.MetadataToken
        | ProvidedTypeSymbolKind.Generic gty  -> gty.MetadataToken
        | ProvidedTypeSymbolKind.FSharpTypeAbbreviation _ -> typeof<obj>.MetadataToken

    override this.GetEvents() = this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating

    override this.ToString() = this.FullName

type ProvidedSymbolMethod(genericMethodDefinition: MethodInfo, parameters: Type[], typeBuilder: ITypeBuilder) =
    inherit MethodInfo()

    let convParam (p:ParameterInfo) =
        { new ParameterInfo() with
                override __.Name = p.Name
                override __.ParameterType = instType typeBuilder (parameters, [| |]) p.ParameterType
                override __.Attributes = p.Attributes
                override __.RawDefaultValue = p.RawDefaultValue
                override __.GetCustomAttributesData() = p.GetCustomAttributesData()
        }

    override this.IsGenericMethod =
        (if this.DeclaringType.IsGenericType then this.DeclaringType.GetGenericArguments().Length else 0) < parameters.Length

    override this.GetGenericArguments() =
        Seq.skip (if this.DeclaringType.IsGenericType then this.DeclaringType.GetGenericArguments().Length else 0) parameters |> Seq.toArray

    override __.GetGenericMethodDefinition() = genericMethodDefinition

    override __.DeclaringType = instType typeBuilder (parameters, [| |]) genericMethodDefinition.DeclaringType
    override __.ToString() = "Method " + genericMethodDefinition.Name
    override __.Name = genericMethodDefinition.Name
    override __.MetadataToken = genericMethodDefinition.MetadataToken
    override __.Attributes = genericMethodDefinition.Attributes
    override __.CallingConvention = genericMethodDefinition.CallingConvention
    override __.MemberType = genericMethodDefinition.MemberType

    override this.IsDefined(_attributeType, _inherit): bool = notRequired this "IsDefined" genericMethodDefinition.Name
    override __.ReturnType = instType typeBuilder (parameters, [| |]) genericMethodDefinition.ReturnType
    override __.GetParameters() = genericMethodDefinition.GetParameters() |> Array.map convParam
    override __.ReturnParameter = genericMethodDefinition.ReturnParameter |> convParam
    override this.ReturnTypeCustomAttributes = notRequired this "ReturnTypeCustomAttributes" genericMethodDefinition.Name
    override this.GetBaseDefinition() = notRequired this "GetBaseDefinition" genericMethodDefinition.Name
    override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" genericMethodDefinition.Name
    override this.MethodHandle = notRequired this "MethodHandle" genericMethodDefinition.Name
    override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" genericMethodDefinition.Name
    override this.ReflectedType = notRequired this "ReflectedType" genericMethodDefinition.Name
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) =  Attributes.CreateEmpty attributeType

//--------------------------------------------------------------------------------
// ProvidedMethod, ProvidedConstructor, ProvidedTypeDefinition and other provided objects


[<AutoOpen>]
module Misc =


    let mkParamArrayCustomAttributeData() =
        { new CustomAttributeData() with
            member __.Constructor =  typeof<ParamArrayAttribute>.GetConstructors().[0]
            member __.ConstructorArguments = upcast [| |]
            member __.NamedArguments = upcast [| |] }

    let mkEditorHideMethodsCustomAttributeData() =
        { new CustomAttributeData() with
            member __.Constructor =  typeof<TypeProviderEditorHideMethodsAttribute>.GetConstructors().[0]
            member __.ConstructorArguments = upcast [| |]
            member __.NamedArguments = upcast [| |] }

    let mkAllowNullLiteralCustomAttributeData value =
        { new CustomAttributeData() with
            member __.Constructor = typeof<AllowNullLiteralAttribute>.GetConstructors().[0]
            member __.ConstructorArguments = upcast [| CustomAttributeTypedArgument(typeof<bool>, value) |]
            member __.NamedArguments = upcast [| |] }

    /// This makes an xml doc attribute w.r.t. an amortized computation of an xml doc string.
    /// It is important that the text of the xml doc only get forced when poking on the ConstructorArguments
    /// for the CustomAttributeData object.
    let mkXmlDocCustomAttributeDataLazy(lazyText: Lazy<string>) =
        { new CustomAttributeData() with
            member __.Constructor =  typeof<TypeProviderXmlDocAttribute>.GetConstructors().[0]
            member __.ConstructorArguments = upcast [| CustomAttributeTypedArgument(typeof<string>, lazyText.Force())  |]
            member __.NamedArguments = upcast [| |] }

    let mkXmlDocCustomAttributeData(s:string) =  mkXmlDocCustomAttributeDataLazy (lazy s)

    let mkDefinitionLocationAttributeCustomAttributeData(line:int, column:int, filePath:string) =
        { new CustomAttributeData() with
            member __.Constructor =  typeof<TypeProviderDefinitionLocationAttribute>.GetConstructors().[0]
            member __.ConstructorArguments = upcast [| |]
            member __.NamedArguments =
                upcast [| CustomAttributeNamedArgument(typeof<TypeProviderDefinitionLocationAttribute>.GetProperty("FilePath"), CustomAttributeTypedArgument(typeof<string>, filePath));
                            CustomAttributeNamedArgument(typeof<TypeProviderDefinitionLocationAttribute>.GetProperty("Line"), CustomAttributeTypedArgument(typeof<int>, line)) ;
                            CustomAttributeNamedArgument(typeof<TypeProviderDefinitionLocationAttribute>.GetProperty("Column"), CustomAttributeTypedArgument(typeof<int>, column))
                        |] }
    let mkObsoleteAttributeCustomAttributeData(message:string, isError: bool) =
        { new CustomAttributeData() with
                member __.Constructor =  typeof<ObsoleteAttribute>.GetConstructors() |> Array.find (fun x -> x.GetParameters().Length = 2)
                member __.ConstructorArguments = upcast [|CustomAttributeTypedArgument(typeof<string>, message) ; CustomAttributeTypedArgument(typeof<bool>, isError)  |]
                member __.NamedArguments = upcast [| |] }

    let mkReflectedDefinitionCustomAttributeData() =
        { new CustomAttributeData() with
                member __.Constructor =  typeof<ReflectedDefinitionAttribute>.GetConstructors().[0]
                member __.ConstructorArguments = upcast [| |]
                member __.NamedArguments = upcast [| |] }

    type CustomAttributesImpl(isTgt, customAttributesData) =
        let customAttributes = ResizeArray<CustomAttributeData>()
        let mutable hideObjectMethods = false
        let mutable nonNullable = false
        let mutable obsoleteMessage = None
        let mutable xmlDocDelayed = None
        let mutable xmlDocAlwaysRecomputed = None
        let mutable hasParamArray = false
        let mutable hasReflectedDefinition = false

        // XML doc text that we only compute once, if any. This must _not_ be forced until the ConstructorArguments
        // property of the custom attribute is foced.
        let xmlDocDelayedText =
            lazy
                (match xmlDocDelayed with None -> assert false; "" | Some f -> f())

        // Custom atttributes that we only compute once
        let customAttributesOnce =
            lazy
                [|
                    if not isTgt then
                        if hideObjectMethods then yield mkEditorHideMethodsCustomAttributeData()
                        if nonNullable then yield mkAllowNullLiteralCustomAttributeData false
                        match xmlDocDelayed with None -> () | Some _ -> customAttributes.Add(mkXmlDocCustomAttributeDataLazy xmlDocDelayedText)
                        match xmlDocAlwaysRecomputed with None -> () | Some f -> yield mkXmlDocCustomAttributeData (f())
                        match obsoleteMessage with None -> () | Some s -> customAttributes.Add(mkObsoleteAttributeCustomAttributeData s)
                        if hasParamArray then yield mkParamArrayCustomAttributeData()
                        if hasReflectedDefinition then yield mkReflectedDefinitionCustomAttributeData()
                        yield! customAttributes 
                    yield! customAttributesData()
                |]

        member __.AddDefinitionLocation(line:int, column:int, filePath:string) = customAttributes.Add(mkDefinitionLocationAttributeCustomAttributeData(line, column, filePath))
        member __.AddObsolete(message: string, isError) = obsoleteMessage <- Some (message, isError)
        member __.HasParamArray with get() = hasParamArray and set(v) = hasParamArray <- v
        member __.HasReflectedDefinition with get() = hasReflectedDefinition and set(v) = hasReflectedDefinition <- v
        member __.AddXmlDocComputed xmlDocFunction = xmlDocAlwaysRecomputed <- Some xmlDocFunction
        member __.AddXmlDocDelayed xmlDocFunction = xmlDocDelayed <- Some xmlDocFunction
        member __.AddXmlDoc xmlDoc =  xmlDocDelayed <- Some (K xmlDoc)
        member __.HideObjectMethods with get() = hideObjectMethods and set v = hideObjectMethods <- v
        member __.NonNullable with get () = nonNullable and set v = nonNullable <- v
        member __.AddCustomAttribute(attribute) = customAttributes.Add(attribute)
        member __.GetCustomAttributesData() =
            let attrs = customAttributesOnce.Force()
            let attrsWithDocHack = 
                match xmlDocAlwaysRecomputed with 
                | None -> 
                        attrs
                | Some f -> 
                    // Recomputed XML doc is evaluated on every call to GetCustomAttributesData() when in the IDE
                    [| for ca in attrs ->
                            if ca.Constructor.DeclaringType.Name = typeof<TypeProviderXmlDocAttribute>.Name then 
                                { new CustomAttributeData() with
                                    member __.Constructor =  ca.Constructor
                                    member __.ConstructorArguments = upcast [| CustomAttributeTypedArgument(typeof<string>, f())  |]
                                    member __.NamedArguments = upcast [| |] }
                            else ca |]
            attrsWithDocHack :> IList<_>


type ProvidedStaticParameter(isTgt: bool, parameterName:string, parameterType:Type, parameterDefaultValue:obj option, customAttributesData) =
    inherit ParameterInfo()

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    new (parameterName:string, parameterType:Type, ?parameterDefaultValue:obj) = 
        ProvidedStaticParameter(false, parameterName, parameterType, parameterDefaultValue, (K [| |]))

    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc

    member __.ParameterDefaultValue = parameterDefaultValue 
    member __.BelongsToTargetModel = isTgt

    override __.RawDefaultValue = defaultArg parameterDefaultValue null
    override __.Attributes = if parameterDefaultValue.IsNone then enum 0 else ParameterAttributes.Optional
    override __.Position = 0
    override __.ParameterType = parameterType
    override __.Name = parameterName
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

type ProvidedParameter(isTgt: bool, parameterName:string, attrs, parameterType:Type, optionalValue:obj option, customAttributesData) =
    
    inherit ParameterInfo()

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    new (parameterName:string, parameterType:Type, ?isOut:bool, ?optionalValue:obj) = 
        ProvidedParameter(false, parameterName, parameterType, isOut, optionalValue)

    new (_isTgt, parameterName:string, parameterType:Type, isOut:bool option, optionalValue:obj option) = 
        let isOut = defaultArg isOut false
        let attrs = (if isOut then ParameterAttributes.Out else enum 0) |||
                    (match optionalValue with None -> enum 0 | Some _ -> ParameterAttributes.Optional ||| ParameterAttributes.HasDefault)
        ProvidedParameter(false, parameterName, attrs, parameterType, optionalValue, K [| |])

    member __.IsParamArray with set(v) = customAttributesImpl.HasParamArray <- v
    member __.IsReflectedDefinition with set(v) = customAttributesImpl.HasReflectedDefinition <- v
    member __.OptionalValue = optionalValue 
    member __.HasDefaultParameterValue = Option.isSome optionalValue
    member __.BelongsToTargetModel = isTgt
    member __.AddCustomAttribute(attribute) = customAttributesImpl.AddCustomAttribute(attribute)

    override __.Name = parameterName
    override __.ParameterType = parameterType
    override __.Attributes = attrs
    override __.RawDefaultValue = defaultArg optionalValue null
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

and ProvidedConstructor(isTgt: bool, attrs: MethodAttributes, parameters: ProvidedParameter[], invokeCode: (Expr list -> Expr), baseCall, isImplicitCtor, customAttributesData) =
    
    inherit ConstructorInfo()
    let parameterInfos = parameters |> Array.map (fun p -> p :> ParameterInfo)
    let mutable baseCall = baseCall
    let mutable declaringType : ProvidedTypeDefinition option = None  
    let mutable isImplicitCtor = isImplicitCtor
    let mutable attrs = attrs
    let isStatic() = hasFlag attrs MethodAttributes.Static

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    new (parameters, invokeCode) =
        ProvidedConstructor(false, MethodAttributes.Public ||| MethodAttributes.RTSpecialName ||| MethodAttributes.HideBySig, Array.ofList parameters, invokeCode, None, false, K [| |])

    member __.IsTypeInitializer
        with get() = isStatic() && hasFlag attrs MethodAttributes.Private
        and set(v) =
            let typeInitializerAttributes = MethodAttributes.Static ||| MethodAttributes.Private
            attrs <- if v then attrs ||| typeInitializerAttributes else attrs &&& ~~~typeInitializerAttributes

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddObsoleteAttribute (message, ?isError) = customAttributesImpl.AddObsolete (message, defaultArg isError false)
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)

    member __.PatchDeclaringType x = patchOption declaringType (fun () -> declaringType <- Some x)
    member this.BaseConstructorCall
        with set (d:Expr list -> (ConstructorInfo * Expr list)) =
            match baseCall with
            | None -> baseCall <- Some d
            | Some _ -> failwithf "ProvidedConstructor: base call already given for '%s'" this.Name

    member __.IsImplicitConstructor with get() = isImplicitCtor and set v = isImplicitCtor <- v
    member __.BaseCall = baseCall
    member __.Parameters = parameters
    member __.GetInvokeCode args = invokeCode args
    member __.BelongsToTargetModel = isTgt
    member __.DeclaringProvidedType = declaringType
    member this.IsErased = (nonNone "DeclaringType" this.DeclaringProvidedType).IsErased

    // Implement overloads
    override __.GetParameters() = parameterInfos
    override __.Attributes = attrs
    override __.Name = if isStatic() then ".cctor" else ".ctor"
    override __.DeclaringType = declaringType |> nonNone "DeclaringType" :> Type
    override __.IsDefined(_attributeType, _inherit) = true

    override this.Invoke(_invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
    override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
    override this.ReflectedType = notRequired this "ReflectedType" this.Name
    override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
    override this.MethodHandle = notRequired this "MethodHandle" this.Name
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

and ProvidedMethod(isTgt: bool, methodName: string, attrs: MethodAttributes, parameters: ProvidedParameter[], returnType: Type, invokeCode: (Expr list -> Expr) option, staticParams, staticParamsApply, customAttributesData) =
    inherit MethodInfo()
    let parameterInfos = parameters |> Array.map (fun p -> p :> ParameterInfo)

    let mutable declaringType : ProvidedTypeDefinition option = None 
    let mutable attrs = attrs
    let mutable staticParams = staticParams
    let mutable staticParamsApply = staticParamsApply
    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)
    let mutable returnTypeFixCache = None

    /// The public constructor for the design-time/source model
    new (methodName, parameters, returnType, ?invokeCode, ?isStatic) =
        let isStatic = defaultArg isStatic false
        let attrs = if isStatic then MethodAttributes.Public ||| MethodAttributes.Static else MethodAttributes.Public
        ProvidedMethod(false, methodName, attrs, Array.ofList parameters, returnType, invokeCode, [], None, K [| |])

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddObsoleteAttribute (message, ?isError) = customAttributesImpl.AddObsolete (message, defaultArg isError false)
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)
    member __.AddCustomAttribute(attribute) = customAttributesImpl.AddCustomAttribute(attribute)

    member __.SetMethodAttrs attributes = attrs <- attributes
    member __.AddMethodAttrs attributes = attrs <- attrs ||| attributes
    member __.PatchDeclaringType x = patchOption declaringType (fun () -> declaringType <- Some x)

    /// Abstract a type to a parametric-type. Requires "formal parameters" and "instantiation function".
    member __.DefineStaticParameters(parameters: ProvidedStaticParameter list, instantiationFunction: (string -> obj[] -> ProvidedMethod)) =
        staticParams      <- parameters
        staticParamsApply <- Some instantiationFunction

    /// Get ParameterInfo[] for the parametric type parameters
    member __.GetStaticParametersInternal() = [| for p in staticParams -> p :> ParameterInfo |]

    /// Instantiate parametric method
    member this.ApplyStaticArguments(mangledName:string, args:obj[]) =
        if staticParams.Length <> args.Length then
            failwithf "ProvidedMethod: expecting %d static parameters but given %d for method %s" staticParams.Length args.Length methodName
        if staticParams.Length > 0 then
            match staticParamsApply with
            | None -> failwith "ProvidedMethod: DefineStaticParameters was not called"
            | Some f -> f mangledName args
        else
            this

    member __.Parameters = parameters
    member __.GetInvokeCode = invokeCode
    member __.StaticParams = staticParams
    member __.StaticParamsApply = staticParamsApply
    member __.BelongsToTargetModel = isTgt
    member __.DeclaringProvidedType = declaringType
    member this.IsErased = (nonNone "DeclaringType" this.DeclaringProvidedType).IsErased

    // Implement overloads
    override __.GetParameters() = parameterInfos 

    override this.Attributes = 
        match invokeCode, this.DeclaringProvidedType with
        | None, Some pt when pt.IsInterface || pt.IsAbstract -> 
                attrs ||| MethodAttributes.Abstract ||| MethodAttributes.Virtual ||| MethodAttributes.HideBySig ||| MethodAttributes.NewSlot
        | _ -> attrs


    override __.Name = methodName

    override __.DeclaringType = declaringType |> nonNone "DeclaringType" :> Type

    override __.IsDefined(_attributeType, _inherit): bool = true

    override __.MemberType = MemberTypes.Method

    override x.CallingConvention =
        let cc = CallingConventions.Standard
        let cc = if not x.IsStatic then cc ||| CallingConventions.HasThis else cc
        cc

    override __.ReturnType = 
        if isTgt then 
            match returnTypeFixCache with 
            | Some returnTypeFix -> returnTypeFix
            | None -> 
                let returnTypeFix = 
                    match returnType.Namespace, returnType.Name with 
                    | "System", "Void"->  
                        if ImportProvidedMethodBaseAsILMethodRef_OnStack_HACK() then 
                            typeof<Void>
                        else 
                            returnType
                    | _ -> returnType
                returnTypeFixCache <- Some returnTypeFix
                returnTypeFix
        else
            returnType

    override __.ReturnParameter = null // REVIEW: Give it a name and type?

    override __.ToString() = "Method " + methodName

    // These don't have to return fully accurate results - they are used
    // by the F# Quotations library function SpecificCall as a pre-optimization
    // when comparing methods
    override __.MetadataToken = genToken()
    override __.MethodHandle = RuntimeMethodHandle()

    override this.ReturnTypeCustomAttributes = notRequired this "ReturnTypeCustomAttributes" methodName
    override this.GetBaseDefinition() = notRequired this "GetBaseDefinition" methodName
    override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" methodName
    override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" methodName
    override this.ReflectedType = notRequired this "ReflectedType" methodName
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()


and ProvidedProperty(isTgt: bool, propertyName: string, attrs: PropertyAttributes, propertyType: Type, isStatic: bool, getter: (unit -> MethodInfo) option, setter: (unit -> MethodInfo) option, indexParameters: ProvidedParameter[], customAttributesData) =
    inherit PropertyInfo()

    let mutable declaringType : ProvidedTypeDefinition option = None  

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    /// The public constructor for the design-time/source model
    new (propertyName, propertyType, ?getterCode, ?setterCode, ?isStatic, ?indexParameters) =
        let isStatic = defaultArg isStatic false
        let indexParameters = defaultArg indexParameters []
        let pattrs = (if isStatic then MethodAttributes.Static else enum<MethodAttributes>(0)) ||| MethodAttributes.Public ||| MethodAttributes.SpecialName
        let getter = getterCode |> Option.map (fun _ -> ProvidedMethod(false, "get_" + propertyName, pattrs, Array.ofList indexParameters, propertyType, getterCode, [], None, K [| |]) :> MethodInfo)
        let setter = setterCode |> Option.map (fun _ -> ProvidedMethod(false, "set_" + propertyName, pattrs, [| yield! indexParameters; yield ProvidedParameter(false, "value", propertyType, isOut=Some false, optionalValue=None) |], typeof<Void>, setterCode, [], None, K [| |]) :> MethodInfo)
        ProvidedProperty(false, propertyName, PropertyAttributes.None, propertyType, isStatic, Option.map K getter, Option.map K setter, Array.ofList indexParameters, K [| |])

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddObsoleteAttribute (message, ?isError) = customAttributesImpl.AddObsolete (message, defaultArg isError false)
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)
    member __.AddCustomAttribute attribute = customAttributesImpl.AddCustomAttribute attribute
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

    member __.PatchDeclaringType x =
        if not isTgt then 
            match getter with Some f -> (match f() with (:? ProvidedMethod as g) -> g.PatchDeclaringType x | _ -> ()) | _ -> ()
            match setter with Some f -> (match f() with (:? ProvidedMethod as s) -> s.PatchDeclaringType x | _ -> ()) | _ -> ()
        patchOption declaringType (fun () -> declaringType <- Some x)
            
    member __.IsStatic = isStatic
    member __.IndexParameters = indexParameters
    member __.BelongsToTargetModel = isTgt
    member __.Getter = getter
    member __.Setter = setter

    override __.PropertyType = propertyType
    override this.SetValue(_obj, _value, _invokeAttr, _binder, _index, _culture) = notRequired this "SetValue" propertyName
    override this.GetAccessors _nonPublic = notRequired this "nonPublic" propertyName
    override __.GetGetMethod _nonPublic = match getter with None -> null | Some g -> g()
    override __.GetSetMethod _nonPublic = match setter with None -> null | Some s -> s() 
    override __.GetIndexParameters() = [| for p in indexParameters -> upcast p |]
    override __.Attributes = attrs
    override __.CanRead = getter.IsSome
    override __.CanWrite = setter.IsSome
    override this.GetValue(_obj, _invokeAttr, _binder, _index, _culture): obj = notRequired this "GetValue" propertyName
    override __.Name = propertyName
    override __.DeclaringType = declaringType |> nonNone "DeclaringType":> Type
    override __.MemberType: MemberTypes = MemberTypes.Property

    override this.ReflectedType = notRequired this "ReflectedType" propertyName
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override this.IsDefined(_attributeType, _inherit) = notRequired this "IsDefined" propertyName

and ProvidedEvent(isTgt: bool, eventName:string, attrs: EventAttributes, eventHandlerType:Type, isStatic: bool, adder: (unit -> MethodInfo), remover: (unit -> MethodInfo), customAttributesData) =
    inherit EventInfo()

    let mutable declaringType : ProvidedTypeDefinition option = None  

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    new (eventName, eventHandlerType, adderCode, removerCode, ?isStatic) = 
        let isStatic = defaultArg isStatic false
        let pattrs = (if isStatic then MethodAttributes.Static else enum<MethodAttributes>(0)) ||| MethodAttributes.Public ||| MethodAttributes.SpecialName
        let adder = ProvidedMethod(false, "add_" + eventName, pattrs, [| ProvidedParameter(false, "handler", eventHandlerType, isOut=Some false, optionalValue=None) |], typeof<Void>, Some adderCode, [], None, K [| |])  :> MethodInfo
        let remover = ProvidedMethod(false, "remove_" + eventName, pattrs, [| ProvidedParameter(false, "handler", eventHandlerType, isOut=Some false, optionalValue=None) |], typeof<Void>, Some removerCode, [], None, K [| |])  :> MethodInfo
        ProvidedEvent(false, eventName, EventAttributes.None, eventHandlerType, isStatic, K adder, K remover, K [| |])

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)

    member __.PatchDeclaringType x =
        if not isTgt then 
            match adder() with :? ProvidedMethod as a -> a.PatchDeclaringType x | _ -> ()
            match remover() with :? ProvidedMethod as r -> r.PatchDeclaringType x | _ -> ()
        patchOption declaringType (fun () -> declaringType <- Some x)

    member __.IsStatic = isStatic
    member __.Adder = adder()
    member __.Remover = remover()
    member __.BelongsToTargetModel = isTgt

    override __.EventHandlerType = eventHandlerType
    override __.GetAddMethod _nonPublic = adder() 
    override __.GetRemoveMethod _nonPublic = remover()
    override __.Attributes = attrs
    override __.Name = eventName
    override __.DeclaringType = declaringType |> nonNone "DeclaringType":> Type
    override __.MemberType: MemberTypes = MemberTypes.Event

    override this.GetRaiseMethod _nonPublic = notRequired this "GetRaiseMethod" eventName
    override this.ReflectedType = notRequired this "ReflectedType" eventName
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override this.IsDefined(_attributeType, _inherit) = notRequired this "IsDefined" eventName
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

and ProvidedField(isTgt: bool, fieldName:string, attrs, fieldType:Type, rawConstantValue: obj, customAttributesData) =
    inherit FieldInfo()

    let mutable declaringType : ProvidedTypeDefinition option = None  

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)
    let mutable attrs = attrs

    new (fieldName:string, fieldType:Type) = ProvidedField(false, fieldName, FieldAttributes.Private, fieldType, null, (K [| |]))

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddObsoleteAttribute (message, ?isError) = customAttributesImpl.AddObsolete (message, defaultArg isError false)
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)
    member __.SetFieldAttributes attributes = attrs <- attributes
    member __.BelongsToTargetModel = isTgt

    member __.PatchDeclaringType x = patchOption declaringType (fun () -> declaringType <- Some x)

    member __.AddCustomAttribute attribute = customAttributesImpl.AddCustomAttribute attribute
    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

    // Implement overloads
    override __.FieldType = fieldType
    override __.GetRawConstantValue() = rawConstantValue
    override __.Attributes = attrs
    override __.Name = fieldName
    override __.DeclaringType = declaringType |> nonNone "DeclaringType":> Type
    override __.MemberType: MemberTypes = MemberTypes.Field

    override this.ReflectedType = notRequired this "ReflectedType" fieldName
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override this.IsDefined(_attributeType, _inherit) = notRequired this "IsDefined" fieldName

    override this.SetValue(_obj, _value, _invokeAttr, _binder, _culture) = notRequired this "SetValue" fieldName
    override this.GetValue(_obj): obj = notRequired this "GetValue" fieldName
    override this.FieldHandle = notRequired this "FieldHandle" fieldName

    static member Literal(fieldName:string, fieldType:Type, literalValue: obj) = 
        ProvidedField(false, fieldName, (FieldAttributes.Static ||| FieldAttributes.Literal ||| FieldAttributes.Public), fieldType, literalValue, K [| |])

    
and ProvidedMeasureBuilder() =

    // TODO: this shouldn't be hardcoded, but without creating a dependency on FSharp.Compiler.Service
    // there seems to be no way to check if a type abbreviation exists
    static let unitNamesTypeAbbreviations =
        [
            "meter"; "hertz"; "newton"; "pascal"; "joule"; "watt"; "coulomb";
            "volt"; "farad"; "ohm"; "siemens"; "weber"; "tesla"; "henry"
            "lumen"; "lux"; "becquerel"; "gray"; "sievert"; "katal"
        ]
        |> Set.ofList

    static let unitSymbolsTypeAbbreviations =
        [
            "m"; "kg"; "s"; "A"; "K"; "mol"; "cd"; "Hz"; "N"; "Pa"; "J"; "W"; "C"
            "V"; "F"; "S"; "Wb"; "T"; "lm"; "lx"; "Bq"; "Gy"; "Sv"; "kat"; "H"
        ]
        |> Set.ofList 

    static member One = typeof<CompilerServices.MeasureOne>
    static member Product (measure1, measure2) = typedefof<CompilerServices.MeasureProduct<_, _>>.MakeGenericType [| measure1;measure2 |]
    static member Inverse denominator = typedefof<CompilerServices.MeasureInverse<_>>.MakeGenericType [| denominator |]
    static member Ratio (numerator, denominator) = ProvidedMeasureBuilder.Product(numerator, ProvidedMeasureBuilder.Inverse denominator)
    static member Square measure = ProvidedMeasureBuilder.Product(measure, measure)

    // If the unit is not a valid type, instead
    // of assuming it's a type abbreviation, which may not be the case and cause a
    // problem later on, check the list of valid abbreviations
    static member SI (unitName:string) =
        let mLowerCase = unitName.ToLowerInvariant()
        let abbreviation =
            if unitNamesTypeAbbreviations.Contains mLowerCase then
                Some ("Microsoft.FSharp.Data.UnitSystems.SI.UnitNames", mLowerCase)
            elif unitSymbolsTypeAbbreviations.Contains unitName then
                Some ("Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols", unitName)
            else
                None
        match abbreviation with
        | Some (ns, unitName) ->
            ProvidedTypeSymbol(ProvidedTypeSymbolKind.FSharpTypeAbbreviation(typeof<Core.CompilerServices.MeasureOne>.Assembly, ns, [| unitName |]), [], defaultTypeBuilder) :> Type
        | None ->
            typedefof<list<int>>.Assembly.GetType("Microsoft.FSharp.Data.UnitSystems.SI.UnitNames." + mLowerCase)

    static member AnnotateType (basic, argument) = ProvidedTypeSymbol(Generic basic, argument, defaultTypeBuilder) :> Type

and 
    [<RequireQualifiedAccess; NoComparison>] 
    TypeContainer =
    | Namespace of (unit -> Assembly) * string // namespace
    | Type of ProvidedTypeDefinition
    | TypeToBeDecided

/// backingDataSource is a set of functions to fetch backing data for the ProvidedTypeDefinition, 
/// and allows us to reuse this type for both target and source models, even when the
/// source model is being incrementally updates by further .AddMember calls
and ProvidedTypeDefinition(isTgt: bool, container:TypeContainer, className: string, getBaseType: (unit -> Type option), attrs: TypeAttributes, getEnumUnderlyingType, staticParams, staticParamsApply, backingDataSource, customAttributesData, nonNullable, hideObjectMethods, typeBuilder: ITypeBuilder) as this =
    inherit TypeDelegator()

    do match container, !ProvidedTypeDefinition.Logger with
        | TypeContainer.Namespace _, Some logger when not isTgt -> logger (sprintf "Creating ProvidedTypeDefinition %s [%d]" className (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode this))
        | _ -> ()

    static let defaultAttributes (isErased, isSealed, isInterface, isAbstract) =
        TypeAttributes.Public ||| 
        (if isInterface then TypeAttributes.Interface ||| TypeAttributes.Abstract
            elif isAbstract then TypeAttributes.Abstract
            else TypeAttributes.Class) |||
        (if isSealed && not isInterface && not isAbstract then TypeAttributes.Sealed else enum 0) |||
        enum (if isErased then int32 TypeProviderTypeAttributes.IsErased else 0)

    // state
    let mutable attrs   = attrs
    let mutable enumUnderlyingType = lazy getEnumUnderlyingType()
    let mutable baseType =  lazy getBaseType()
        
    /// Represents the evaluated members so far
    let members = ResizeArray<MemberInfo>()

    /// Represents delayed members, as yet uncomputed
    let membersQueue = ResizeArray<(unit -> MemberInfo[])>()

    let mutable staticParamsDefined = false
    let mutable staticParams = staticParams
    let mutable staticParamsApply = staticParamsApply
    let mutable container = container
    let interfaceImpls = ResizeArray<Type>()
    let interfacesQueue = ResizeArray<unit -> Type[]>()
    let methodOverrides = ResizeArray<ProvidedMethod * MethodInfo>()
    let methodOverridesQueue = ResizeArray<unit -> (ProvidedMethod * MethodInfo)[]>()

    do match backingDataSource with 
        | None -> () 
        | Some (_, getFreshMembers, getFreshInterfaces, getFreshMethodOverrides) ->
            membersQueue.Add getFreshMembers
            interfacesQueue.Add getFreshInterfaces
            methodOverridesQueue.Add getFreshMethodOverrides

    let checkFreshMembers() =
        match backingDataSource with 
        | None -> false
        | Some (checkFreshMembers, _getFreshMembers, _getFreshInterfaces, _getFreshMethodOverrides) -> checkFreshMembers()

    let moreMembers() =
        membersQueue.Count > 0 || checkFreshMembers() 

    let evalMembers() =
        if moreMembers() then
            // re-add the getFreshMembers call from the backingDataSource to make sure we fetch the latest translated members from the source model
            match backingDataSource with 
            | None -> () 
            | Some (_, getFreshMembers, _getFreshInterfaces, _getFreshMethodOverrides) ->
                membersQueue.Add getFreshMembers

            let elems = membersQueue |> Seq.toArray // take a copy in case more elements get added
            membersQueue.Clear()
            for  f in elems do
                for m in f() do
                    members.Add m
                        
                    // Implicitly add the property and event methods (only for the source model where they are not explicitly declared)
                    match m with
                    | :? ProvidedProperty    as p ->
                        if not p.BelongsToTargetModel then 
                            if p.CanRead then members.Add (p.GetGetMethod true)
                            if p.CanWrite then members.Add (p.GetSetMethod true)
                    | :? ProvidedEvent       as e ->
                        if not e.BelongsToTargetModel then 
                            members.Add (e.GetAddMethod true)
                            members.Add (e.GetRemoveMethod true)
                    | _ -> ()
                
    let getMembers() =
        evalMembers()
        members.ToArray()

    // Save some common lookups for provided types with lots of members
    let mutable bindings :  Dictionary<int32, obj> = null

    let save (key: BindingFlags) f : 'T = 
        let key = int key

        if bindings = null then 
            bindings <- Dictionary<_, _>(HashIdentity.Structural)

        if not (moreMembers()) && bindings.ContainsKey(key)  then 
            bindings.[key] :?> 'T
        else
            let res = f () // this will refresh the members
            bindings.[key] <- box res
            res

    let evalInterfaces() =
        if interfacesQueue.Count > 0 then
            let elems = interfacesQueue |> Seq.toArray // take a copy in case more elements get added
            interfacesQueue.Clear()
            for  f in elems do
                for i in f() do
                    interfaceImpls.Add i
            match backingDataSource with 
            | None -> () 
            | Some (_, _getFreshMembers, getInterfaces, _getFreshMethodOverrides) ->
                interfacesQueue.Add getInterfaces

    let getInterfaces() =
        evalInterfaces()
        interfaceImpls.ToArray()

    let evalMethodOverrides () =
        if methodOverridesQueue.Count > 0 then
            let elems = methodOverridesQueue |> Seq.toArray // take a copy in case more elements get added
            methodOverridesQueue.Clear()
            for  f in elems do
                for i in f() do
                    methodOverrides.Add i
            match backingDataSource with 
            | None -> () 
            | Some (_, _getFreshMembers, _getFreshInterfaces, getFreshMethodOverrides) ->
                methodOverridesQueue.Add getFreshMethodOverrides

    let getFreshMethodOverrides () =
        evalMethodOverrides ()
        methodOverrides.ToArray()

    let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)

    do if nonNullable then customAttributesImpl.NonNullable <- true
    do if hideObjectMethods then customAttributesImpl.HideObjectMethods <- true
    do this.typeImpl <- this

    override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

    new (assembly:Assembly, namespaceName, className, baseType, ?hideObjectMethods, ?nonNullable, ?isErased, ?isSealed, ?isInterface, ?isAbstract) = 
        let isErased = defaultArg isErased true
        let isSealed = defaultArg isSealed true
        let isInterface = defaultArg isInterface false
        let isAbstract = defaultArg isAbstract false
        let nonNullable = defaultArg nonNullable false
        let hideObjectMethods = defaultArg hideObjectMethods false
        let attrs = defaultAttributes (isErased, isSealed, isInterface, isAbstract)
        //if not isErased && assembly.GetType().Name <> "ProvidedAssembly" then failwithf "a non-erased (i.e. generative) ProvidedTypeDefinition '%s.%s' was placed in an assembly '%s' that is not a ProvidedAssembly" namespaceName className (assembly.GetName().Name)
        ProvidedTypeDefinition(false, TypeContainer.Namespace (K assembly, namespaceName), className, K baseType, attrs, K None, [], None, None, K [| |], nonNullable, hideObjectMethods, defaultTypeBuilder)

    new (className:string, baseType, ?hideObjectMethods, ?nonNullable, ?isErased, ?isSealed, ?isInterface, ?isAbstract) = 
        let isErased = defaultArg isErased true
        let isSealed = defaultArg isSealed true
        let isInterface = defaultArg isInterface false
        let isAbstract = defaultArg isAbstract false
        let nonNullable = defaultArg nonNullable false
        let hideObjectMethods = defaultArg hideObjectMethods false
        let attrs = defaultAttributes (isErased, isSealed, isInterface, isAbstract)
        ProvidedTypeDefinition(false, TypeContainer.TypeToBeDecided, className, K baseType, attrs, K None, [], None, None, K [| |], nonNullable, hideObjectMethods, defaultTypeBuilder)

    // state ops

    override __.UnderlyingSystemType = typeof<Type>

    // Implement overloads
    override __.Assembly = 
        match container with
        | TypeContainer.Namespace (theAssembly, _) -> theAssembly()
        | TypeContainer.Type t           -> t.Assembly
        | TypeContainer.TypeToBeDecided -> failwithf "type '%s' was not yet added as a member to a declaring type, stacktrace = %s" className Environment.StackTrace

    override __.FullName = 
        match container with
        | TypeContainer.Type declaringType -> declaringType.FullName + "+" + className
        | TypeContainer.Namespace (_, namespaceName) ->
            if namespaceName="" then failwith "use null for global namespace"
            match namespaceName with
            | null -> className
            | _    -> namespaceName + "." + className
        | TypeContainer.TypeToBeDecided -> failwithf "type '%s' was not added as a member to a declaring type" className

    override __.Namespace = 
        match container with
        | TypeContainer.Namespace (_, nsp) -> nsp
        | TypeContainer.Type t           -> t.Namespace
        | TypeContainer.TypeToBeDecided -> failwithf "type '%s' was not added as a member to a declaring type" className

    override __.BaseType = match baseType.Value with Some ty -> ty | None -> null

    override __.GetConstructors bindingFlags =
        (//save ("ctor", bindingFlags, None) (fun () -> 
            getMembers() 
            |> Array.choose (function :? ConstructorInfo as c when memberBinds false bindingFlags c.IsStatic c.IsPublic -> Some c | _ -> None))

    override this.GetMethods bindingFlags =
        (//save ("methods", bindingFlags, None) (fun () -> 
            getMembers() 
            |> Array.choose (function :? MethodInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> Some m | _ -> None)
            |> (if hasFlag bindingFlags BindingFlags.DeclaredOnly || this.BaseType = null then id else (fun mems -> Array.append mems (this.ErasedBaseType.GetMethods(bindingFlags)))))

    override this.GetFields bindingFlags =
        (//save ("fields", bindingFlags, None) (fun () -> 
            getMembers() 
            |> Array.choose (function :? FieldInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> Some m | _ -> None)
            |> (if hasFlag bindingFlags BindingFlags.DeclaredOnly || this.BaseType = null then id else (fun mems -> Array.append mems (this.ErasedBaseType.GetFields(bindingFlags)))))

    override this.GetProperties bindingFlags =
        (//save ("props", bindingFlags, None) (fun () -> 
            let staticOrPublic =
                getMembers() 
                |> Array.choose (function :? PropertyInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> Some m | _ -> None)
            staticOrPublic 
            |> (if hasFlag bindingFlags BindingFlags.DeclaredOnly || this.BaseType = null
                then id
                else (fun mems -> Array.append mems (this.ErasedBaseType.GetProperties(bindingFlags)))))

    override this.GetEvents bindingFlags =
        (//save ("events", bindingFlags, None) (fun () -> 
            getMembers() 
            |> Array.choose (function :? EventInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> Some m | _ -> None)
            |> (if hasFlag bindingFlags BindingFlags.DeclaredOnly || this.BaseType = null then id else (fun mems -> Array.append mems (this.ErasedBaseType.GetEvents(bindingFlags)))))

    override __.GetNestedTypes bindingFlags =
        (//save ("nested", bindingFlags, None) (fun () -> 
            getMembers() 
            |> Array.choose (function :? Type as m when memberBinds true bindingFlags false m.IsPublic || m.IsNestedPublic -> Some m | _ -> None)
            |> (if hasFlag bindingFlags BindingFlags.DeclaredOnly || this.BaseType = null then id else (fun mems -> Array.append mems (this.ErasedBaseType.GetNestedTypes(bindingFlags)))))

    override this.GetConstructorImpl(bindingFlags, _binder, _callConventions, _types, _modifiers) = 
        let xs = this.GetConstructors bindingFlags |> Array.filter (fun m -> m.Name = ".ctor")
        if xs.Length > 1 then failwith "GetConstructorImpl. not support overloads"
        if xs.Length > 0 then xs.[0] else null

    override __.GetMethodImpl(name, bindingFlags, _binderBinder, _callConvention, _types, _modifiers): MethodInfo =
        (//save ("methimpl", bindingFlags, Some name) (fun () -> 
            // This is performance critical for large spaces of provided methods and properties
            // Save a table of the methods grouped by name
            let table = 
                save (bindingFlags ||| BindingFlags.InvokeMethod) (fun () -> 
                    let methods = this.GetMethods bindingFlags
                    methods |> Seq.groupBy (fun m -> m.Name) |> Seq.map (fun (k, v) -> k, Seq.toArray v) |> dict)
                
            let xs = if table.ContainsKey name then table.[name] else [| |]
            //let xs = this.GetMethods bindingFlags |> Array.filter (fun m -> m.Name = name)
            if xs.Length > 1 then failwithf "GetMethodImpl. not support overloads, name = '%s', methods - '%A', callstack = '%A'" name xs Environment.StackTrace
            if xs.Length > 0 then xs.[0] else null)

    override this.GetField(name, bindingFlags) =
        (//save ("field1", bindingFlags, Some name) (fun () -> 
            let xs = this.GetFields bindingFlags |> Array.filter (fun m -> m.Name = name)
            if xs.Length > 0 then xs.[0] else null)

    override __.GetPropertyImpl(name, bindingFlags, _binder, _returnType, _types, _modifiers) =
        (//save ("prop1", bindingFlags, Some name) (fun () -> 
            let table = 
                save (bindingFlags ||| BindingFlags.GetProperty) (fun () -> 
                    let methods = this.GetProperties bindingFlags
                    methods |> Seq.groupBy (fun m -> m.Name) |> Seq.map (fun (k, v) -> k, Seq.toArray v) |> dict)
            let xs = if table.ContainsKey name then table.[name] else [| |]
            //let xs = this.GetProperties bindingFlags |> Array.filter (fun m -> m.Name = name)
            if xs.Length > 0 then xs.[0] else null)

    override __.GetEvent(name, bindingFlags) =
        (//save ("event1", bindingFlags, Some name) (fun () -> 
            let xs = this.GetEvents bindingFlags |> Array.filter (fun m -> m.Name = name)
            if xs.Length > 0 then xs.[0] else null)

    override __.GetNestedType(name, bindingFlags) =
        (//save ("nested1", bindingFlags, Some name) (fun () -> 
            let xs = this.GetNestedTypes bindingFlags |> Array.filter (fun m -> m.Name = name)
            if xs.Length > 0 then xs.[0] else null)

    override __.GetInterface(_name, _ignoreCase) = notRequired this "GetInterface" this.Name

    override __.GetInterfaces() = getInterfaces()  


    override __.MakeArrayType() = ProvidedTypeSymbol(ProvidedTypeSymbolKind.SDArray, [this], typeBuilder) :> Type

    override __.MakeArrayType arg = ProvidedTypeSymbol(ProvidedTypeSymbolKind.Array arg, [this], typeBuilder) :> Type

    override __.MakePointerType() = ProvidedTypeSymbol(ProvidedTypeSymbolKind.Pointer, [this], typeBuilder) :> Type

    override __.MakeByRefType() = ProvidedTypeSymbol(ProvidedTypeSymbolKind.ByRef, [this], typeBuilder) :> Type

    // The binding attributes are always set to DeclaredOnly ||| Static ||| Instance ||| Public when GetMembers is called directly by the F# compiler
    // However, it's possible for the framework to generate other sets of flags in some corner cases (e.g. via use of `enum` with a provided type as the target)
    override __.GetMembers bindingFlags =
        [| for m in getMembers()  do
                match m with 
                | :? ConstructorInfo as c when memberBinds false bindingFlags c.IsStatic c.IsPublic -> yield (c :> MemberInfo)
                | :? MethodInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> yield (m :> _)
                | :? FieldInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> yield (m :> _)
                | :? PropertyInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> yield (m :> _)
                | :? EventInfo as m when memberBinds false bindingFlags m.IsStatic m.IsPublic -> yield (m :> _)
                | :? Type as m when memberBinds true bindingFlags false m.IsPublic || m.IsNestedPublic -> yield (m :> _) 
                | _ -> () |]

    override this.GetMember(name, mt, _bindingFlags) =
        let mt = if hasFlag mt MemberTypes.NestedType then mt ||| MemberTypes.TypeInfo else mt
        this.GetMembers() |> Array.filter (fun m -> 0 <> int(m.MemberType &&& mt) && m.Name = name)

    // Attributes, etc..
    override __.GetAttributeFlagsImpl() = adjustTypeAttributes this.IsNested attrs 

    override this.IsValueTypeImpl() = 
        match this.BaseType with 
        | null -> false 
        | bt -> bt.FullName = "System.Enum" || bt.FullName = "System.ValueType" || bt.IsValueType 

    override __.IsEnum = 
        match this.BaseType with 
        | null -> false
        | bt -> bt.FullName = "System.Enum" || bt.IsEnum

    override __.GetEnumUnderlyingType() =
        if this.IsEnum then
            match enumUnderlyingType.Force() with
            | None -> typeof<int>
            | Some ty -> ty
        else failwithf "not enum type"

    override __.IsArrayImpl() = false
    override __.IsByRefImpl() = false
    override __.IsPointerImpl() = false
    override __.IsPrimitiveImpl() = false
    override __.IsCOMObjectImpl() = false
    override __.HasElementTypeImpl() = false
    override __.Name = className

    override __.DeclaringType = 
        match container with
        | TypeContainer.Namespace _ -> null
        | TypeContainer.Type enclosingTyp -> (enclosingTyp :> Type)
        | TypeContainer.TypeToBeDecided -> failwithf "type '%s' was not added as a member to a declaring type" className

    override __.MemberType = if this.IsNested then MemberTypes.NestedType else MemberTypes.TypeInfo

#if NETCOREAPP
    // See bug https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/236
    override __.IsSZArray = false
#endif

    override x.GetHashCode() = x.Namespace.GetHashCode() ^^^ className.GetHashCode()
    override this.Equals(that: obj) = Object.ReferenceEquals(this, that)
    override this.Equals(that: Type) = Object.ReferenceEquals(this, that)

    override this.IsAssignableFrom(otherTy: Type) = isAssignableFrom this otherTy

    override this.IsSubclassOf(otherTy: Type) = isSubclassOf this otherTy

    override __.GetGenericArguments() = [||]

    override __.ToString() = this.Name

    override x.Module = x.Assembly.ManifestModule

    override __.GUID = Guid.Empty
    override __.GetCustomAttributes(_inherit) = emptyAttributes
    override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
    override __.IsDefined(_attributeType: Type, _inherit) = false

    override __.GetElementType() = notRequired this "Module" this.Name
    override __.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired this "Module" this.Name
    override __.AssemblyQualifiedName = notRequired this "Module" this.Name
    // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating
    override this.GetEvents() = this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating

    // Get the model
    member __.BelongsToTargetModel = isTgt
    member __.AttributesRaw = attrs
    member __.EnumUnderlyingTypeRaw() = enumUnderlyingType.Force()
    member __.Container = container
    member __.BaseTypeRaw() = baseType.Force()
    member __.StaticParams = staticParams
    member __.StaticParamsApply = staticParamsApply
        
    // Count the members declared since the indicated position in the members list.  This allows the target model to observe 
    // incremental additions made to the source model
    member __.CountMembersFromCursor(idx: int) = evalMembers(); members.Count - idx

    // Fetch the members declared since the indicated position in the members list.  This allows the target model to observe 
    // incremental additions made to the source model
    member __.GetMembersFromCursor(idx: int) = evalMembers(); members.GetRange(idx, members.Count - idx).ToArray(), members.Count

    // Fetch the interfaces declared since the indicated position in the interfaces list
    member __.GetInterfaceImplsFromCursor(idx: int) = evalInterfaces(); interfaceImpls.GetRange(idx, interfaceImpls.Count - idx).ToArray(), interfaceImpls.Count

    // Fetch the method overrides declared since the indicated position in the list
    member __.GetMethodOverridesFromCursor(idx: int) = evalMethodOverrides(); methodOverrides.GetRange(idx, methodOverrides.Count - idx).ToArray(), methodOverrides.Count

    // Fetch the method overrides 
    member __.GetMethodOverrides() = getFreshMethodOverrides()

    member this.ErasedBaseType : Type = ProvidedTypeDefinition.EraseType(this.BaseType)

    member __.AddXmlDocComputed xmlDocFunction = customAttributesImpl.AddXmlDocComputed xmlDocFunction
    member __.AddXmlDocDelayed xmlDocFunction = customAttributesImpl.AddXmlDocDelayed xmlDocFunction
    member __.AddXmlDoc xmlDoc = customAttributesImpl.AddXmlDoc xmlDoc
    member __.AddObsoleteAttribute (message, ?isError) = customAttributesImpl.AddObsolete (message, defaultArg isError false)
    member __.AddDefinitionLocation(line, column, filePath) = customAttributesImpl.AddDefinitionLocation(line, column, filePath)
    member __.HideObjectMethods with get() = customAttributesImpl.HideObjectMethods and set v = customAttributesImpl.HideObjectMethods <- v
    member __.NonNullable with get() = customAttributesImpl.NonNullable and set v = customAttributesImpl.NonNullable <- v
    member __.AddCustomAttribute attribute = customAttributesImpl.AddCustomAttribute attribute

    member __.SetEnumUnderlyingType(ty) = enumUnderlyingType <- lazy Some ty
    member __.SetBaseType t = 
        if baseType.IsValueCreated then failwithf "The base type has already been evaluated for this type. Please call SetBaseType before any operations which traverse the type hierarchy. stacktrace = %A" Environment.StackTrace
        baseType <- lazy Some t
    member __.SetBaseTypeDelayed baseTypeFunction = 
        if baseType.IsValueCreated then failwithf "The base type has already been evaluated for this type. Please call SetBaseType before any operations which traverse the type hierarchy. stacktrace = %A" Environment.StackTrace
        baseType <- lazy (Some (baseTypeFunction()))
    member __.AddAttributes x = attrs <- attrs ||| x
    member __.SetAttributes x = attrs <- x

    member this.AddMembers(memberInfos:list<#MemberInfo>) = 
        memberInfos |> List.iter this.PatchDeclaringTypeOfMember
        membersQueue.Add (fun () -> memberInfos |> List.toArray |> Array.map (fun x -> x :> MemberInfo ))

    member __.AddMember(memberInfo:MemberInfo) =
        this.AddMembers [memberInfo]

    member __.AddMembersDelayed(membersFunction: unit -> list<#MemberInfo>) =
        membersQueue.Add (fun () -> membersFunction() |> List.toArray |> Array.map (fun x -> this.PatchDeclaringTypeOfMember x; x :> MemberInfo ))

    member __.AddMemberDelayed(memberFunction: unit -> #MemberInfo) =
        this.AddMembersDelayed(fun () -> [memberFunction()])

    member __.AddAssemblyTypesAsNestedTypesDelayed (assemblyFunction: unit -> Assembly)  =
        let bucketByPath nodef tipf (items: (string list * 'Value) list) =
            // Find all the items with an empty key list and call 'tipf'
            let tips =
                [ for (keylist, v) in items do
                        match keylist with
                        | [] -> yield tipf v
                        | _ -> () ]

            // Find all the items with a non-empty key list. Bucket them together by
            // the first key. For each bucket, call 'nodef' on that head key and the bucket.
            let nodes =
                let buckets = new Dictionary<_, _>(10)
                for (keylist, v) in items do
                    match keylist with
                    | [] -> ()
                    | key::rest ->
                        buckets.[key] <- (rest, v) :: (if buckets.ContainsKey key then buckets.[key] else []);

                [ for (KeyValue(key, items)) in buckets -> nodef key items ]

            tips @ nodes
        this.AddMembersDelayed (fun _ ->
            let topTypes = [ for ty in assemblyFunction().GetTypes() do
                                    if not ty.IsNested then
                                            let namespaceParts = match ty.Namespace with null -> [] | s -> s.Split '.' |> Array.toList
                                            yield namespaceParts, ty ]
            let rec loop types =
                types
                |> bucketByPath
                    (fun namespaceComponent typesUnderNamespaceComponent ->
                        let t = ProvidedTypeDefinition(namespaceComponent, baseType = Some typeof<obj>)
                        t.AddMembers (loop typesUnderNamespaceComponent)
                        (t :> Type))
                    id
            loop topTypes)

    /// Abstract a type to a parametric-type. Requires "formal parameters" and "instantiation function".
    member __.DefineStaticParameters(parameters: ProvidedStaticParameter list, instantiationFunction: (string -> obj[] -> ProvidedTypeDefinition)) =
        if staticParamsDefined then failwithf "Static parameters have already been defined for this type. stacktrace = %A" Environment.StackTrace
        staticParamsDefined <- true
        staticParams      <- parameters
        staticParamsApply <- Some instantiationFunction

    /// Get ParameterInfo[] for the parametric type parameters 
    member __.GetStaticParametersInternal() = [| for p in staticParams -> p :> ParameterInfo |]

    /// Instantiate parametric type
    member this.ApplyStaticArguments(name:string, args:obj[]) =
        if staticParams.Length <> args.Length then
            failwithf "ProvidedTypeDefinition: expecting %d static parameters but given %d for type %s" staticParams.Length args.Length this.FullName
        if staticParams.Length > 0 then
            match staticParamsApply with
            | None -> failwith "ProvidedTypeDefinition: DefineStaticParameters was not called"
            | Some f -> f name args
        else
            this

    member __.PatchDeclaringType x = container <- TypeContainer.Type x

    member __.IsErased
        with get() = (attrs &&& enum (int32 TypeProviderTypeAttributes.IsErased)) <> enum 0
        and set v =
            if v then attrs <- attrs ||| enum (int32 TypeProviderTypeAttributes.IsErased)
            else attrs <- attrs &&& ~~~(enum (int32 TypeProviderTypeAttributes.IsErased))

    member __.SuppressRelocation
        with get() = (attrs &&& enum (int32 TypeProviderTypeAttributes.SuppressRelocate)) <> enum 0
        and set v =
            if v then attrs <- attrs ||| enum (int32 TypeProviderTypeAttributes.SuppressRelocate)
            else attrs <- attrs &&& ~~~(enum (int32 TypeProviderTypeAttributes.SuppressRelocate))

    member __.AddInterfaceImplementation interfaceType = interfaceImpls.Add interfaceType

    member __.AddInterfaceImplementationsDelayed interfacesFunction = interfacesQueue.Add (interfacesFunction >> Array.ofList)

    member __.SetAssemblyInternal (assembly: unit -> Assembly)  = 
        match container with 
        | TypeContainer.Namespace (_, ns) -> container <- TypeContainer.Namespace (assembly, ns)
        | TypeContainer.Type _ -> failwithf "can't set assembly of nested type '%s'" className
        | TypeContainer.TypeToBeDecided -> failwithf "type '%s' was not added as a member to a declaring type" className

    member __.DefineMethodOverride (methodInfoBody, methodInfoDeclaration) = methodOverrides.Add (methodInfoBody, methodInfoDeclaration)
    member __.DefineMethodOverridesDelayed f = methodOverridesQueue.Add (f >> Array.ofList)

    // This method is used by Debug.fs and QuotationBuilder.fs.
    // Emulate the F# type provider type erasure mechanism to get the
    // actual (erased) type. We erase ProvidedTypes to their base type
    // and we erase array of provided type to array of base type. In the
    // case of generics all the generic type arguments are also recursively
    // replaced with the erased-to types
    static member EraseType(typ:Type): Type =
        match typ with
        | :? ProvidedTypeDefinition as ptd when ptd.IsErased -> ProvidedTypeDefinition.EraseType typ.BaseType
        | t when t.IsArray ->
            let rank = t.GetArrayRank()
            let et = ProvidedTypeDefinition.EraseType (t.GetElementType())
            if rank = 0 then et.MakeArrayType() else et.MakeArrayType(rank)
        | :? ProvidedTypeSymbol as sym when sym.IsFSharpUnitAnnotated ->
            typ.UnderlyingSystemType
        | t when t.IsGenericType && not t.IsGenericTypeDefinition ->
            let genericTypeDefinition = t.GetGenericTypeDefinition()
            let genericArguments = t.GetGenericArguments() |> Array.map ProvidedTypeDefinition.EraseType
            genericTypeDefinition.MakeGenericType(genericArguments)
        | t -> t


    member this.PatchDeclaringTypeOfMember (m:MemberInfo) =
        match m with
        | :? ProvidedConstructor as c -> c.PatchDeclaringType this 
        | :? ProvidedMethod      as m -> m.PatchDeclaringType this 
        | :? ProvidedProperty    as p -> p.PatchDeclaringType this 
        | :? ProvidedEvent       as e -> e.PatchDeclaringType this 
        | :? ProvidedTypeDefinition  as t -> t.PatchDeclaringType this
        | :? ProvidedField as l -> l.PatchDeclaringType this
        | _ -> ()

    static member Logger: (string -> unit) option ref = ref None


//====================================================================================================
// AssemblyReader for ProvidedTypesContext
//
// A lightweight .NET assembly reader that fits in a single F# file.  Based on the well-tested Abstract IL
// binary reader code.  Used by the type provider to read referenced asssemblies.

module internal AssemblyReader =

    open System
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Collections.ObjectModel
    open System.IO
    open System.Reflection
    open System.Text
    //open ProviderImplementation.ProvidedTypes

    [<AutoOpen>]
    module Utils =

        let singleOfBits (x:int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes(x), 0)
        let doubleOfBits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

        //---------------------------------------------------------------------
        // SHA1 hash-signing algorithm.  Used to get the public key token from
        // the public key.
        //---------------------------------------------------------------------

        // Little-endian encoding of int32 
        let b0 n = byte (n &&& 0xFF)
        let b1 n = byte ((n >>> 8) &&& 0xFF)
        let b2 n = byte ((n >>> 16) &&& 0xFF)
        let b3 n = byte ((n >>> 24) &&& 0xFF)

        // Little-endian encoding of int64 
        let dw7 n = byte ((n >>> 56) &&& 0xFFL)
        let dw6 n = byte ((n >>> 48) &&& 0xFFL)
        let dw5 n = byte ((n >>> 40) &&& 0xFFL)
        let dw4 n = byte ((n >>> 32) &&& 0xFFL)
        let dw3 n = byte ((n >>> 24) &&& 0xFFL)
        let dw2 n = byte ((n >>> 16) &&& 0xFFL)
        let dw1 n = byte ((n >>> 8)  &&& 0xFFL)
        let dw0 n = byte (n &&& 0xFFL)


        module SHA1 =
            let inline (>>>&)  (x:int) (y:int) = int32 (uint32 x >>> y)
            let f(t, b, c, d) =
                if t < 20 then (b &&& c) ||| ((~~~b) &&& d)
                elif t < 40 then b ^^^ c ^^^ d
                elif t < 60 then (b &&& c) ||| (b &&& d) ||| (c &&& d)
                else b ^^^ c ^^^ d

            let [<Literal>] k0to19 = 0x5A827999
            let [<Literal>] k20to39 = 0x6ED9EBA1
            let [<Literal>] k40to59 = 0x8F1BBCDC
            let [<Literal>] k60to79 = 0xCA62C1D6

            let k t =
                if t < 20 then k0to19
                elif t < 40 then k20to39
                elif t < 60 then k40to59
                else k60to79

            type SHAStream =
                { stream: byte[];
                  mutable pos: int;
                  mutable eof:  bool; }

            let rotLeft32 x n =  (x <<< n) ||| (x >>>& (32-n))

            // padding and length (in bits!) recorded at end
            let shaAfterEof sha  =
                let n = sha.pos
                let len = sha.stream.Length
                if n = len then 0x80
                else
                  let paddedLen = (((len + 9 + 63) / 64) * 64) - 8
                  if n < paddedLen - 8  then 0x0
                  elif (n &&& 63) = 56 then int32 ((int64 len * int64 8) >>> 56) &&& 0xff
                  elif (n &&& 63) = 57 then int32 ((int64 len * int64 8) >>> 48) &&& 0xff
                  elif (n &&& 63) = 58 then int32 ((int64 len * int64 8) >>> 40) &&& 0xff
                  elif (n &&& 63) = 59 then int32 ((int64 len * int64 8) >>> 32) &&& 0xff
                  elif (n &&& 63) = 60 then int32 ((int64 len * int64 8) >>> 24) &&& 0xff
                  elif (n &&& 63) = 61 then int32 ((int64 len * int64 8) >>> 16) &&& 0xff
                  elif (n &&& 63) = 62 then int32 ((int64 len * int64 8) >>> 8) &&& 0xff
                  elif (n &&& 63) = 63 then (sha.eof <- true; int32 (int64 len * int64 8) &&& 0xff)
                  else 0x0

            let shaRead8 sha =
                let s = sha.stream
                let b = if sha.pos >= s.Length then shaAfterEof sha else int32 s.[sha.pos]
                sha.pos <- sha.pos + 1
                b

            let shaRead32 sha  =
                let b0 = shaRead8 sha
                let b1 = shaRead8 sha
                let b2 = shaRead8 sha
                let b3 = shaRead8 sha
                let res = (b0 <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3
                res

            let sha1Hash sha =
                let mutable h0 = 0x67452301
                let mutable h1 = 0xEFCDAB89
                let mutable h2 = 0x98BADCFE
                let mutable h3 = 0x10325476
                let mutable h4 = 0xC3D2E1F0
                let mutable a = 0
                let mutable b = 0
                let mutable c = 0
                let mutable d = 0
                let mutable e = 0
                let w = Array.create 80 0x00
                while (not sha.eof) do
                    for i = 0 to 15 do
                        w.[i] <- shaRead32 sha
                    for t = 16 to 79 do
                        w.[t] <- rotLeft32 (w.[t-3] ^^^ w.[t-8] ^^^ w.[t-14] ^^^ w.[t-16]) 1
                    a <- h0
                    b <- h1
                    c <- h2
                    d <- h3
                    e <- h4
                    for t = 0 to 79 do
                        let temp = (rotLeft32 a 5) + f(t, b, c, d) + e + w.[t] + k(t)
                        e <- d
                        d <- c
                        c <- rotLeft32 b 30
                        b <- a
                        a <- temp
                    h0 <- h0 + a
                    h1 <- h1 + b
                    h2 <- h2 + c
                    h3 <- h3 + d
                    h4 <- h4 + e
                h0, h1, h2, h3, h4

            let sha1HashBytes s =
                let (_h0, _h1, _h2, h3, h4) = sha1Hash { stream = s; pos = 0; eof = false }   // the result of the SHA algorithm is stored in registers 3 and 4
                Array.map byte [|  b0 h4; b1 h4; b2 h4; b3 h4; b0 h3; b1 h3; b2 h3; b3 h3; |]


        let sha1HashBytes s = SHA1.sha1HashBytes s


    [<StructuralEquality; StructuralComparison>]
    type PublicKey =
        | PublicKey of byte[]
        | PublicKeyToken of byte[]
        member x.IsKey=match x with PublicKey _ -> true | _ -> false
        member x.IsKeyToken=match x with PublicKeyToken _ -> true | _ -> false
        member x.Key=match x with PublicKey b -> b | _ -> failwithf "not a key"
        member x.KeyToken=match x with PublicKeyToken b -> b | _ -> failwithf"not a key token"

        member x.ToToken() =
            match x with
            | PublicKey bytes -> SHA1.sha1HashBytes bytes
            | PublicKeyToken token -> token
        static member KeyAsToken(k) = PublicKeyToken(PublicKey(k).ToToken())

    [<Sealed>]
    type ILAssemblyRef(name: string, hash: byte[] uoption, publicKey: PublicKey uoption, retargetable: bool, version: Version uoption, locale: string uoption)  =
        member __.Name=name
        member __.Hash=hash
        member __.PublicKey=publicKey
        member __.Retargetable=retargetable
        member __.Version=version
        member __.Locale=locale

        member x.ToAssemblyName() =
            let asmName = AssemblyName(Name=x.Name)
            match x.PublicKey with 
            | USome bytes -> asmName.SetPublicKeyToken(bytes.ToToken())
            | UNone -> ()
            match x.Version with 
            | USome v -> asmName.Version <- v
            | UNone -> ()
            asmName.CultureName <- System.Globalization.CultureInfo.InvariantCulture.Name
            asmName

        static member FromAssemblyName (aname:AssemblyName) =
            let locale = UNone
            let publicKey =
               match aname.GetPublicKey()  with
               | null | [| |] ->
                   match aname.GetPublicKeyToken()  with
                   | null | [| |] -> UNone
                   | bytes -> USome (PublicKeyToken bytes)
               | bytes ->
                   USome (PublicKey.KeyAsToken(bytes))

            let version =
               match aname.Version with
               | null -> UNone
               | v -> USome (Version(v.Major, v.Minor, v.Build, v.Revision))

            let retargetable = aname.Flags = System.Reflection.AssemblyNameFlags.Retargetable

            ILAssemblyRef(aname.Name, UNone, publicKey, retargetable, version, locale)

        member aref.QualifiedName =
            let b = new StringBuilder(100)
            let add (s:string) = (b.Append(s) |> ignore)
            let addC (s:char) = (b.Append(s) |> ignore)
            add(aref.Name);
            match aref.Version with
            | UNone -> ()
            | USome v ->
                add ", Version=";
                add (string v.Major)
                add ".";
                add (string v.Minor)
                add ".";
                add (string v.Build)
                add ".";
                add (string v.Revision)
                add ", Culture="
                match aref.Locale with
                | UNone -> add "neutral"
                | USome b -> add b
                add ", PublicKeyToken="
                match aref.PublicKey with
                | UNone -> add "null"
                | USome pki ->
                      let pkt = pki.ToToken()
                      let convDigit(digit) =
                          let digitc =
                              if digit < 10
                              then  System.Convert.ToInt32 '0' + digit
                              else System.Convert.ToInt32 'a' + (digit - 10)
                          System.Convert.ToChar(digitc)
                      for i = 0 to pkt.Length-1 do
                          let v = pkt.[i]
                          addC (convDigit(System.Convert.ToInt32(v)/16))
                          addC (convDigit(System.Convert.ToInt32(v)%16))
                // retargetable can be true only for system assemblies that definitely have Version
                if aref.Retargetable then
                    add ", Retargetable=Yes"
            b.ToString()
        override x.ToString() = x.QualifiedName

        override __.GetHashCode() =
            
            name.GetHashCode() +
            137 * (hash.GetHashCode() +
                137 * (publicKey.GetHashCode() +
                    137 * ( retargetable.GetHashCode() +
                        137 * ( version.GetHashCode() +
                                137 * locale.GetHashCode()))))

            override __.Equals(obj: obj) =
                match obj with
                | :? ILAssemblyRef as y ->
                    name = y.Name
                    && hash = y.Hash 
                    && publicKey = y.PublicKey 
                    && retargetable = y.Retargetable
                    && version = y.Version
                    && locale = y.Locale
                | _ -> false

    type ILModuleRef(name:string, hasMetadata: bool, hash: byte[] uoption) =
        member __.Name=name
        member __.HasMetadata=hasMetadata
        member __.Hash=hash
        override __.ToString() = "module " + name

        override __.GetHashCode() =
            name.GetHashCode()
            + 137 * (hasMetadata.GetHashCode()
                + 137 * hash.GetHashCode())

        override __.Equals(obj: obj) =
            match obj with
            | :? ILModuleRef as y ->
                name = y.Name
                && hasMetadata = y.HasMetadata
                && hash = y.Hash
            | _ -> false


    [<RequireQualifiedAccess>]
    type ILScopeRef =
        | Local
        | Module of ILModuleRef
        | Assembly of ILAssemblyRef
        member x.IsLocalRef = match x with ILScopeRef.Local      -> true | _ -> false
        member x.IsModuleRef = match x with ILScopeRef.Module _   -> true | _ -> false
        member x.IsAssemblyRef= match x with ILScopeRef.Assembly _ -> true | _ -> false
        member x.ModuleRef = match x with ILScopeRef.Module x   -> x | _ -> failwith "not a module reference"
        member x.AssemblyRef = match x with ILScopeRef.Assembly x -> x | _ -> failwith "not an assembly reference"

        member x.QualifiedName =
            match x with
            | ILScopeRef.Local -> ""
            | ILScopeRef.Module mref -> "module "+mref.Name
            | ILScopeRef.Assembly aref -> aref.QualifiedName

        override x.ToString() = x.QualifiedName

    type ILArrayBound = int32 option
    type ILArrayBounds = ILArrayBound * ILArrayBound

    [<StructuralEquality; StructuralComparison>]
    type ILArrayShape =
        | ILArrayShape of ILArrayBounds[] (* lobound/size pairs *)
        member x.Rank = (let (ILArrayShape l) = x in l.Length)
        static member SingleDimensional = ILArrayShapeStatics.SingleDimensional
        static member FromRank n = if n = 1 then ILArrayShape.SingleDimensional else ILArrayShape(List.replicate n (Some 0, None) |> List.toArray)


    and ILArrayShapeStatics() =
        static let singleDimensional = ILArrayShape [| (Some 0, None) |]
        static member SingleDimensional = singleDimensional

    /// Calling conventions.  These are used in method pointer types.
    [<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
    type ILArgConvention =
        | Default
        | CDecl
        | StdCall
        | ThisCall
        | FastCall
        | VarArg

    [<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
    type ILThisConvention =
        | Instance
        | InstanceExplicit
        | Static

    [<StructuralEquality; StructuralComparison>]
    type ILCallingConv =
        | Callconv of ILThisConvention * ILArgConvention
        member x.ThisConv = let (Callconv(a, _b)) = x in a
        member x.BasicConv = let (Callconv(_a, b)) = x in b
        member x.IsInstance = match x.ThisConv with ILThisConvention.Instance -> true | _ -> false
        member x.IsInstanceExplicit = match x.ThisConv with ILThisConvention.InstanceExplicit -> true | _ -> false
        member x.IsStatic = match x.ThisConv with ILThisConvention.Static -> true | _ -> false

        static member Instance = ILCallingConvStatics.Instance
        static member Static = ILCallingConvStatics.Static

    /// Static storage to amortize the allocation of ILCallingConv.Instance and ILCallingConv.Static
    and ILCallingConvStatics() =
        static let instanceCallConv = Callconv(ILThisConvention.Instance, ILArgConvention.Default)
        static let staticCallConv =  Callconv(ILThisConvention.Static, ILArgConvention.Default)
        static member Instance = instanceCallConv
        static member Static = staticCallConv

    type ILBoxity =
        | AsObject
        | AsValue

    [<RequireQualifiedAccess>]
    type ILTypeRefScope =
        | Top of ILScopeRef
        | Nested of ILTypeRef
        member x.QualifiedNameExtension =
            match x with
            | Top scoref ->
                let sco = scoref.QualifiedName
                if sco = "" then "" else ", " + sco
            | Nested tref ->
                tref.QualifiedNameExtension


    // IL type references have a pre-computed hash code to enable quick lookup tables during binary generation.
    and ILTypeRef(enc: ILTypeRefScope, nsp: string uoption, name: string) =
        let hashCode = hash enc + 137 *( 137 *(hash name) + hash nsp)

        member __.Scope = enc
        member __.Name = name
        member __.Namespace = nsp

        member tref.FullName =
            match enc with
            | ILTypeRefScope.Top _ -> joinILTypeName tref.Namespace tref.Name
            | ILTypeRefScope.Nested enc -> enc.FullName + "." + tref.Name

        member tref.BasicQualifiedName =
            match enc with
            | ILTypeRefScope.Top _ -> joinILTypeName tref.Namespace tref.Name
            | ILTypeRefScope.Nested enc -> enc.BasicQualifiedName + "+" + tref.Name

        member __.QualifiedNameExtension = enc.QualifiedNameExtension

        member tref.QualifiedName = tref.BasicQualifiedName + enc.QualifiedNameExtension

        override x.ToString() = x.FullName

        override __.GetHashCode() = hashCode

        override __.Equals(obj: obj) =
            match obj with
            | :? ILTypeRef as y ->
                enc = y.Scope
                && name = y.Name
                && nsp = y.Namespace
            | _ -> false


    and ILTypeSpec(typeRef: ILTypeRef, inst: ILGenericArgs) =
        let hashCode = hash typeRef + 137 * (hash inst)

        member __.TypeRef = typeRef
        member x.Scope = x.TypeRef.Scope
        member x.Name = x.TypeRef.Name
        member x.Namespace = x.TypeRef.Namespace
        member __.GenericArgs = inst
        member x.BasicQualifiedName =
            let tc = x.TypeRef.BasicQualifiedName
            if x.GenericArgs.Length = 0 then
                tc
            else
                tc + "[" + String.concat ", " (x.GenericArgs |> Array.map (fun arg -> "[" + arg.QualifiedName + "]")) + "]"

        member x.QualifiedNameExtension =
            x.TypeRef.QualifiedNameExtension

        member x.FullName = x.TypeRef.FullName

        override x.ToString() = x.TypeRef.ToString() + (if x.GenericArgs.Length = 0 then "" else "<...>")

        override __.GetHashCode() = hashCode
        override __.Equals(obj: obj) =
            match obj with
            | :? ILTypeSpec as y ->
                typeRef = y.TypeRef
                && inst = y.GenericArgs
            | _ -> false  

    and [<RequireQualifiedAccess>]
        ILType =
        | Void
        | Array    of ILArrayShape * ILType
        | Value    of ILTypeSpec
        | Boxed    of ILTypeSpec
        | Ptr      of ILType
        | Byref    of ILType
        | FunctionPointer     of ILCallingSignature
        | Var    of int
        | Modified of bool * ILTypeRef * ILType

        member x.BasicQualifiedName =
            match x with
            | ILType.Var n -> "!" + string n
            | ILType.Modified(_, _ty1, ty2) -> ty2.BasicQualifiedName
            | ILType.Array (ILArrayShape(s), ty) -> ty.BasicQualifiedName + "[" + System.String(',', s.Length-1) + "]"
            | ILType.Value tr | ILType.Boxed tr -> tr.BasicQualifiedName
            | ILType.Void -> "void"
            | ILType.Ptr _ty -> failwith "unexpected pointer type"
            | ILType.Byref _ty -> failwith "unexpected byref type"
            | ILType.FunctionPointer _mref -> failwith "unexpected function pointer type"

        member x.QualifiedNameExtension =
            match x with
            | ILType.Var _n -> ""
            | ILType.Modified(_, _ty1, ty2) -> ty2.QualifiedNameExtension
            | ILType.Array (ILArrayShape(_s), ty) -> ty.QualifiedNameExtension
            | ILType.Value tr | ILType.Boxed tr -> tr.QualifiedNameExtension
            | ILType.Void -> failwith "void"
            | ILType.Ptr _ty -> failwith "unexpected pointer type"
            | ILType.Byref _ty -> failwith "unexpected byref type"
            | ILType.FunctionPointer _mref -> failwith "unexpected function pointer type"

        member x.QualifiedName =
            x.BasicQualifiedName + x.QualifiedNameExtension

        member x.TypeSpec =
          match x with
          | ILType.Boxed tr | ILType.Value tr -> tr
          | _ -> failwithf "not a nominal type"

        member x.Boxity =
          match x with
          | ILType.Boxed _ -> AsObject
          | ILType.Value _ -> AsValue
          | _ -> failwithf "not a nominal type"

        member x.TypeRef =
          match x with
          | ILType.Boxed tspec | ILType.Value tspec -> tspec.TypeRef
          | _ -> failwithf "not a nominal type"

        member x.IsNominal =
          match x with
          | ILType.Boxed _ | ILType.Value _ -> true
          | _ -> false

        member x.GenericArgs =
          match x with
          | ILType.Boxed tspec | ILType.Value tspec -> tspec.GenericArgs
          | _ -> [| |]

        member x.IsTyvar =
          match x with
          | ILType.Var _ -> true | _ -> false

        override x.ToString() = x.QualifiedName

    and ILCallingSignature(callingConv: ILCallingConv, argTypes: ILTypes, returnType: ILType) =
        member __.CallingConv = callingConv
        member __.ArgTypes = argTypes
        member __.ReturnType = returnType

    and ILGenericArgs = ILType[]
    and ILTypes = ILType[]


    type ILMethodRef(parent: ILTypeRef, callconv: ILCallingConv, genericArity: int, name: string, args: ILTypes, ret: ILType) =
        member __.EnclosingTypeRef = parent
        member __.CallingConv = callconv
        member __.Name = name
        member __.GenericArity = genericArity
        member __.ArgCount = args.Length
        member __.ArgTypes = args
        member __.ReturnType = ret

        member x.CallingSignature = ILCallingSignature (x.CallingConv, x.ArgTypes, x.ReturnType)
        override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name + "(...)"


    type ILFieldRef(enclosingTypeRef: ILTypeRef, name: string, typ: ILType) =
        member __.EnclosingTypeRef = enclosingTypeRef
        member __.Name = name
        member __.Type = typ
        override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name

    type ILMethodSpec(methodRef: ILMethodRef, enclosingType: ILType, methodInst: ILGenericArgs) =
        member __.MethodRef = methodRef
        member __.EnclosingType = enclosingType
        member __.GenericArgs = methodInst
        member x.Name = x.MethodRef.Name
        member x.CallingConv = x.MethodRef.CallingConv
        member x.GenericArity = x.MethodRef.GenericArity
        member x.FormalArgTypes = x.MethodRef.ArgTypes
        member x.FormalReturnType = x.MethodRef.ReturnType
        override x.ToString() = x.MethodRef.ToString() + "(...)"

    type ILFieldSpec(fieldRef: ILFieldRef, enclosingType: ILType) =
        member __.FieldRef = fieldRef
        member __.EnclosingType = enclosingType
        member __.FormalType = fieldRef.Type
        member __.Name = fieldRef.Name
        member __.EnclosingTypeRef = fieldRef.EnclosingTypeRef
        override x.ToString() = x.FieldRef.ToString()

    type ILCodeLabel = int

    // --------------------------------------------------------------------
    // Instruction set.                                                     
    // -------------------------------------------------------------------- 

    type ILBasicType =
      | DT_R
      | DT_I1
      | DT_U1
      | DT_I2
      | DT_U2
      | DT_I4
      | DT_U4
      | DT_I8
      | DT_U8
      | DT_R4
      | DT_R8
      | DT_I
      | DT_U
      | DT_REF

    [<RequireQualifiedAccess>]
    type ILToken = 
      | ILType of ILType 
      | ILMethod of ILMethodSpec 
      | ILField of ILFieldSpec

    [<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
    type ILConst = 
      | I4 of int32
      | I8 of int64
      | R4 of single
      | R8 of double

    type ILTailcall = 
      | Tailcall
      | Normalcall

    type ILAlignment =  
      | Aligned
      | Unaligned1
      | Unaligned2
      | Unaligned4

    type ILVolatility =  
      | Volatile
      | Nonvolatile

    type ILReadonly =  
      | ReadonlyAddress
      | NormalAddress

    type ILVarArgs = ILTypes option

    [<StructuralEquality; StructuralComparison>]
    type ILComparisonInstr = 
      | I_beq        
      | I_bge        
      | I_bge_un     
      | I_bgt        
      | I_bgt_un        
      | I_ble        
      | I_ble_un        
      | I_blt        
      | I_blt_un 
      | I_bne_un 
      | I_brfalse 
      | I_brtrue 


#if DEBUG_INFO
    type ILSourceMarker =
        { sourceDocument: ILSourceDocument;
          sourceLine: int;
          sourceColumn: int;
          sourceEndLine: int;
          sourceEndColumn: int }
        static member Create(document, line, column, endLine, endColumn) = 
            { sourceDocument=document;
              sourceLine=line;
              sourceColumn=column;
              sourceEndLine=endLine;
              sourceEndColumn=endColumn }
        member x.Document=x.sourceDocument
        member x.Line=x.sourceLine
        member x.Column=x.sourceColumn
        member x.EndLine=x.sourceEndLine
        member x.EndColumn=x.sourceEndColumn
        override x.ToString() = sprintf "(%d, %d)-(%d, %d)" x.Line x.Column x.EndLine x.EndColumn
#endif

    [<StructuralEquality; NoComparison>]
    type ILInstr = 
      | I_add    
      | I_add_ovf
      | I_add_ovf_un
      | I_and    
      | I_div   
      | I_div_un
      | I_ceq      
      | I_cgt      
      | I_cgt_un   
      | I_clt     
      | I_clt_un  
      | I_conv      of ILBasicType
      | I_conv_ovf  of ILBasicType
      | I_conv_ovf_un  of ILBasicType
      | I_mul       
      | I_mul_ovf   
      | I_mul_ovf_un
      | I_rem       
      | I_rem_un       
      | I_shl       
      | I_shr       
      | I_shr_un
      | I_sub       
      | I_sub_ovf   
      | I_sub_ovf_un   
      | I_xor       
      | I_or        
      | I_neg       
      | I_not       
      | I_ldnull    
      | I_dup       
      | I_pop
      | I_ckfinite 
      | I_nop
      | I_ldc of ILBasicType * ILConst 
      | I_ldarg     of int
      | I_ldarga    of int
      | I_ldind     of ILAlignment * ILVolatility * ILBasicType
      | I_ldloc     of int
      | I_ldloca    of int
      | I_starg     of int
      | I_stind     of  ILAlignment * ILVolatility * ILBasicType
      | I_stloc     of int

      | I_br    of  ILCodeLabel
      | I_jmp   of ILMethodSpec
      | I_brcmp of ILComparisonInstr * ILCodeLabel 
      | I_switch    of ILCodeLabel list  
      | I_ret 

      | I_call     of ILTailcall * ILMethodSpec * ILVarArgs
      | I_callvirt of ILTailcall * ILMethodSpec * ILVarArgs
      | I_callconstraint of ILTailcall * ILType * ILMethodSpec * ILVarArgs
      | I_calli    of ILTailcall * ILCallingSignature * ILVarArgs
      | I_ldftn    of ILMethodSpec
      | I_newobj  of ILMethodSpec  * ILVarArgs
  
      | I_throw
      | I_endfinally
      | I_endfilter
      | I_leave     of  ILCodeLabel
      | I_rethrow

      | I_ldsfld      of ILVolatility * ILFieldSpec
      | I_ldfld       of ILAlignment * ILVolatility * ILFieldSpec
      | I_ldsflda     of ILFieldSpec
      | I_ldflda      of ILFieldSpec 
      | I_stsfld      of ILVolatility  *  ILFieldSpec
      | I_stfld       of ILAlignment * ILVolatility * ILFieldSpec
      | I_ldstr       of string
      | I_isinst      of ILType
      | I_castclass   of ILType
      | I_ldtoken     of ILToken
      | I_ldvirtftn   of ILMethodSpec

      | I_cpobj       of ILType
      | I_initobj     of ILType
      | I_ldobj       of ILAlignment * ILVolatility * ILType
      | I_stobj       of ILAlignment * ILVolatility * ILType
      | I_box         of ILType
      | I_unbox       of ILType
      | I_unbox_any   of ILType
      | I_sizeof      of ILType

      | I_ldelem      of ILBasicType
      | I_stelem      of ILBasicType
      | I_ldelema     of ILReadonly * ILArrayShape * ILType
      | I_ldelem_any  of ILArrayShape * ILType
      | I_stelem_any  of ILArrayShape * ILType
      | I_newarr      of ILArrayShape * ILType 
      | I_ldlen

      | I_mkrefany    of ILType
      | I_refanytype  
      | I_refanyval   of ILType

      | I_break 
#if EMIT_DEBUG_INFO
      | I_seqpoint of ILSourceMarker
#endif
      | I_arglist  

      | I_localloc
      | I_cpblk of ILAlignment * ILVolatility
      | I_initblk of ILAlignment  * ILVolatility

      (* FOR EXTENSIONS, e.g. MS-ILX *)  
      | EI_ilzero of ILType
      | EI_ldlen_multi      of int32 * int32


    [<RequireQualifiedAccess>]
    type ILExceptionClause = 
        | Finally of (ILCodeLabel * ILCodeLabel)
        | Fault  of (ILCodeLabel * ILCodeLabel)
        | FilterCatch of (ILCodeLabel * ILCodeLabel) * (ILCodeLabel * ILCodeLabel)
        | TypeCatch of ILType * (ILCodeLabel * ILCodeLabel)

    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ILExceptionSpec = 
        { Range: (ILCodeLabel * ILCodeLabel);
          Clause: ILExceptionClause }

    /// Indicates that a particular local variable has a particular source 
    /// language name within a given set of ranges. This does not effect local 
    /// variable numbering, which is global over the whole method. 
    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ILLocalDebugMapping =
        { LocalIndex: int;
          LocalName: string; }

    [<NoEquality; NoComparison>]
    type ILLocalDebugInfo = 
        { Range: (ILCodeLabel * ILCodeLabel);
          DebugMappings: ILLocalDebugMapping[] }

    [<NoEquality; NoComparison>]
    type ILCode = 
        { Labels: Dictionary<ILCodeLabel, int> 
          Instrs:ILInstr[] 
          Exceptions: ILExceptionSpec[]
          Locals: ILLocalDebugInfo[] }

    [<NoComparison; NoEquality>]
    type ILLocal = 
        { Type: ILType;
          IsPinned: bool;
          DebugInfo: (string * int * int) option }
      
    type ILLocals = ILLocal[]

    [<NoEquality; NoComparison>]
    type ILMethodBody = 
        { IsZeroInit: bool
          MaxStack: int32
          Locals: ILLocals
          Code:  ILCode
#if EMIT_DEBUG_INFO
          SourceMarker: ILSourceMarker option 
#endif
         }

    type ILPlatform =
        | X86
        | AMD64
        | IA64

    type ILCustomAttrNamedArg =  ILCustomAttrNamedArg of (string * ILType * obj)

    type ILCustomAttribute = 
        { Method: ILMethodSpec
          Data: byte[] 
          Elements: obj list}

    type ILCustomAttrs =
       abstract Entries: ILCustomAttribute[]

    type ILCustomAttrsStatics() =
       static let empty = { new ILCustomAttrs with member __.Entries = [| |] }
       static member Empty = empty

    [<RequireQualifiedAccess>]
    type ILMemberAccess =
        | Assembly
        | CompilerControlled
        | FamilyAndAssembly
        | FamilyOrAssembly
        | Family
        | Private
        | Public
        static member OfFlags (flags: int) =
            let f = (flags &&& 0x00000007)
            if f = 0x00000001 then  ILMemberAccess.Private
            elif f = 0x00000006 then  ILMemberAccess.Public
            elif f = 0x00000004 then  ILMemberAccess.Family
            elif f = 0x00000002 then  ILMemberAccess.FamilyAndAssembly
            elif f = 0x00000005 then  ILMemberAccess.FamilyOrAssembly
            elif f = 0x00000003 then  ILMemberAccess.Assembly
            else ILMemberAccess.CompilerControlled

    [<RequireQualifiedAccess>]
    type ILFieldInit = obj

    type ILParameter =
        { Name: string uoption
          ParameterType: ILType
          Default: ILFieldInit uoption
          //Marshal: ILNativeType option
          Attributes: ParameterAttributes
          CustomAttrs: ILCustomAttrs }
        member x.IsIn = ((x.Attributes &&& ParameterAttributes.In) <> enum 0)
        member x.IsOut = ((x.Attributes &&& ParameterAttributes.Out) <> enum 0)
        member x.IsOptional = ((x.Attributes &&& ParameterAttributes.Optional) <> enum 0)

    type ILParameters = ILParameter[]

    type ILReturn =
        { //Marshal: ILNativeType option;
          Type: ILType;
          CustomAttrs: ILCustomAttrs }

    type ILOverridesSpec =
        | OverridesSpec of ILMethodRef * ILType
        member x.MethodRef = let (OverridesSpec(mr, _ty)) = x in mr
        member x.EnclosingType = let (OverridesSpec(_mr, ty)) = x in ty

    [<StructuralEquality; StructuralComparison>]
    type ILGenericVariance =
        | NonVariant
        | CoVariant
        | ContraVariant

    type ILGenericParameterDef =
        { Name: string
          Constraints: ILTypes
          Attributes: GenericParameterAttributes
          CustomAttrs: ILCustomAttrs 
          Token: int }

        member x.HasReferenceTypeConstraint= (x.Attributes &&& GenericParameterAttributes.ReferenceTypeConstraint) <> enum 0
        member x.HasNotNullableValueTypeConstraint= (x.Attributes &&& GenericParameterAttributes.NotNullableValueTypeConstraint) <> enum 0
        member x.HasDefaultConstructorConstraint= (x.Attributes &&& GenericParameterAttributes.DefaultConstructorConstraint) <> enum 0
        member x.IsCovariant = (x.Attributes &&& GenericParameterAttributes.Covariant) <> enum 0
        member x.IsContravariant = (x.Attributes &&& GenericParameterAttributes.Contravariant) <> enum 0
        override x.ToString() = x.Name

    type ILGenericParameterDefs = ILGenericParameterDef[]

    [<NoComparison; NoEquality>]
    type ILMethodDef =
        { Token: int32
          Name: string
          CallingConv: ILCallingConv
          Parameters: ILParameters
          Return: ILReturn
          Body: ILMethodBody option
          ImplAttributes: MethodImplAttributes
          //SecurityDecls: ILPermissions
          //HasSecurity: bool
          IsEntryPoint:bool
          Attributes: MethodAttributes
          GenericParams: ILGenericParameterDefs
          CustomAttrs: ILCustomAttrs }
        member x.ParameterTypes = x.Parameters |> Array.map (fun p -> p.ParameterType)
        static member ComputeIsStatic attrs = attrs &&& MethodAttributes.Static <> enum 0
        member x.IsStatic = ILMethodDef.ComputeIsStatic x.Attributes 
        member x.IsAbstract = x.Attributes &&& MethodAttributes.Abstract <> enum 0
        member x.IsVirtual = x.Attributes &&& MethodAttributes.Virtual <> enum 0
        member x.IsCheckAccessOnOverride = x.Attributes &&& MethodAttributes.CheckAccessOnOverride <> enum 0
        member x.IsNewSlot = x.Attributes &&& MethodAttributes.NewSlot <> enum 0
        member x.IsFinal = x.Attributes &&& MethodAttributes.Final <> enum 0
        member x.IsSpecialName = x.Attributes &&& MethodAttributes.SpecialName <> enum 0
        member x.IsRTSpecialName = x.Attributes &&& MethodAttributes.RTSpecialName <> enum 0
        member x.IsHideBySig = x.Attributes &&& MethodAttributes.HideBySig <> enum 0
        member x.IsClassInitializer = x.Name = ".cctor"
        member x.IsConstructor = x.Name = ".ctor"
        member x.IsInternalCall = (int x.ImplAttributes &&& 0x1000 <> 0)
        member x.IsManaged = (int x.ImplAttributes &&& 0x0004 = 0)
        member x.IsForwardRef = (int x.ImplAttributes &&& 0x0010 <> 0)
        member x.IsPreserveSig = (int x.ImplAttributes &&& 0x0080 <> 0)
        member x.IsMustRun = (int x.ImplAttributes &&& 0x0040 <> 0)
        member x.IsSynchronized = (int x.ImplAttributes &&& 0x0020 <> 0)
        member x.IsNoInline = (int x.ImplAttributes &&& 0x0008 <> 0)
        member x.Access = ILMemberAccess.OfFlags (int x.Attributes)

        member md.CallingSignature =  ILCallingSignature (md.CallingConv, md.ParameterTypes, md.Return.Type)
        override x.ToString() = "method " + x.Name

    type ILMethodDefs(larr: Lazy<ILMethodDef[]>) =

        let mutable lmap = null
        let getmap() =
            if lmap = null then
                lmap <- Dictionary()
                for y in larr.Force() do
                    let key = y.Name
                    if lmap.ContainsKey key then
                        lmap.[key] <- Array.append [| y |] lmap.[key]
                    else
                        lmap.[key] <- [| y |]
            lmap

        member __.Entries = larr.Force()
        member __.FindByName nm =  
            let scc, ys = getmap().TryGetValue(nm)
            if scc then ys else Array.empty
        member x.FindByNameAndArity (nm, arity) =  x.FindByName nm |> Array.filter (fun x -> x.Parameters.Length = arity)
        member x.TryFindUniqueByName name =  
            match x.FindByName(name) with
            | [| md |] -> Some md 
            | [| |] -> None
            | _ -> failwithf "multiple methods exist with name %s" name

    [<NoComparison; NoEquality>]
    type ILEventDef =
        { //EventHandlerType: ILType option
          Name: string
          Attributes: System.Reflection.EventAttributes
          AddMethod: ILMethodRef
          RemoveMethod: ILMethodRef
          //FireMethod: ILMethodRef option
          //OtherMethods: ILMethodRef[]
          CustomAttrs: ILCustomAttrs
          Token: int }
        member x.EventHandlerType = x.AddMethod.ArgTypes.[0]
        member x.IsStatic = x.AddMethod.CallingConv.IsStatic
        member x.IsSpecialName = (x.Attributes &&& EventAttributes.SpecialName) <> enum<_>(0)
        member x.IsRTSpecialName = (x.Attributes &&& EventAttributes.RTSpecialName) <> enum<_>(0)
        override x.ToString() = "event " + x.Name

    type ILEventDefs =
        abstract Entries: ILEventDef[]

    [<NoComparison; NoEquality>]
    type ILPropertyDef =
        { Name: string
          Attributes: System.Reflection.PropertyAttributes
          SetMethod: ILMethodRef option
          GetMethod: ILMethodRef option
          CallingConv: ILThisConvention
          PropertyType: ILType
          Init: ILFieldInit option
          IndexParameterTypes: ILTypes
          CustomAttrs: ILCustomAttrs
          Token: int }
        member x.IsStatic = (match x.CallingConv with ILThisConvention.Static -> true | _ -> false)
        member x.IndexParameters = 
            x.IndexParameterTypes |> Array.mapi (fun i ty ->
                {  Name = USome("arg"+string i)
                   ParameterType = ty
                   Default = UNone
                   Attributes = ParameterAttributes.None
                   CustomAttrs = ILCustomAttrsStatics.Empty })
        member x.IsSpecialName = x.Attributes &&& PropertyAttributes.SpecialName <> enum 0
        member x.IsRTSpecialName = x.Attributes &&& PropertyAttributes.RTSpecialName <> enum 0
        override x.ToString() = "property " + x.Name

    type ILPropertyDefs =
        abstract Entries: ILPropertyDef[]

    [<NoComparison; NoEquality>]
    type ILFieldDef =
        { Name: string
          FieldType: ILType
          Attributes: FieldAttributes
          //Data:  byte[] option
          LiteralValue:  ILFieldInit option
          Offset:  int32 option
          //Marshal: ILNativeType option
          CustomAttrs: ILCustomAttrs
          Token: int }
        member x.IsStatic = x.Attributes &&& FieldAttributes.Static <> enum 0
        member x.IsInitOnly = x.Attributes &&& FieldAttributes.InitOnly <> enum 0
        member x.IsLiteral = x.Attributes &&& FieldAttributes.Literal <> enum 0
        member x.NotSerialized = x.Attributes &&& FieldAttributes.NotSerialized <> enum 0
        member x.IsSpecialName = x.Attributes &&& FieldAttributes.SpecialName <> enum 0
                 //let isStatic = (flags &&& 0x0010) <> 0
                 //{ Name = nm
                 //  FieldType = readBlobHeapAsFieldSig numtypars typeIdx
                  // IsInitOnly = (flags &&& 0x0020) <> 0
                  // IsLiteral = (flags &&& 0x0040) <> 0
                  // NotSerialized = (flags &&& 0x0080) <> 0
                  // IsSpecialName = (flags &&& 0x0200) <> 0 || (flags &&& 0x0400) <> 0 (* REVIEW: RTSpecialName *)
        member x.Access = ILMemberAccess.OfFlags (int x.Attributes)
        override x.ToString() = "field " + x.Name


    type ILFieldDefs =
        abstract Entries: ILFieldDef[]

    type ILMethodImplDef =
        { Overrides: ILOverridesSpec
          OverrideBy: ILMethodSpec }

    // Index table by name and arity.
    type ILMethodImplDefs =
        abstract Entries: ILMethodImplDef[]

    [<RequireQualifiedAccess>]
    type ILTypeInit =
        | BeforeField
        | OnAny

    [<RequireQualifiedAccess>]
    type ILDefaultPInvokeEncoding =
        | Ansi
        | Auto
        | Unicode

    [<RequireQualifiedAccess>]
    type ILTypeDefLayout =
        | Auto
        | Sequential of ILTypeDefLayoutInfo
        | Explicit of ILTypeDefLayoutInfo

    and ILTypeDefLayoutInfo =
        { Size: int32 option
          Pack: uint16 option } 

    type ILTypeDefAccess =
        | Public
        | Private
        | Nested of ILMemberAccess
        static member OfFlags flags = 
            let f = (flags &&& 0x00000007)
            if f = 0x00000001 then ILTypeDefAccess.Public
            elif f = 0x00000002 then ILTypeDefAccess.Nested ILMemberAccess.Public
            elif f = 0x00000003 then ILTypeDefAccess.Nested ILMemberAccess.Private
            elif f = 0x00000004 then ILTypeDefAccess.Nested ILMemberAccess.Family
            elif f = 0x00000006 then ILTypeDefAccess.Nested ILMemberAccess.FamilyAndAssembly
            elif f = 0x00000007 then ILTypeDefAccess.Nested ILMemberAccess.FamilyOrAssembly
            elif f = 0x00000005 then ILTypeDefAccess.Nested ILMemberAccess.Assembly
            else ILTypeDefAccess.Private

    [<RequireQualifiedAccess>]
    type ILTypeDefKind =
        | Class
        | ValueType
        | Interface
        | Enum
        | Delegate

    [<NoComparison; NoEquality>]
    type ILTypeDef =
        { Namespace: string uoption
          Name: string
          GenericParams: ILGenericParameterDefs
          Attributes: TypeAttributes
          NestedTypes: ILTypeDefs
          Layout: ILTypeDefLayout
          Implements: ILTypes
          Extends: ILType option
          Methods: ILMethodDefs
          Fields: ILFieldDefs
          MethodImpls: ILMethodImplDefs
          Events: ILEventDefs
          Properties: ILPropertyDefs
          CustomAttrs: ILCustomAttrs
          Token: int }
        static member ComputeKind flags (super: ILType option) (nsp: string uoption) (nm: string) = 
            if (flags &&& 0x00000020) <> 0x0 then ILTypeDefKind.Interface else
            let isEnum = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = USome "System" && ty.TypeSpec.Name = "Enum")
            let isDelegate = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = USome "System" && ty.TypeSpec.Name = "Delegate")
            let isMulticastDelegate = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = USome "System" && ty.TypeSpec.Name = "MulticastDelegate")
            let selfIsMulticastDelegate = (nsp = USome "System" && nm = "MulticastDelegate")
            let isValueType = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = USome "System" && ty.TypeSpec.Name = "ValueType" && not (nsp = USome "System" && nm = "Enum"))
            if isEnum then ILTypeDefKind.Enum
            elif  (isDelegate && not selfIsMulticastDelegate) || isMulticastDelegate then ILTypeDefKind.Delegate
            elif isValueType then ILTypeDefKind.ValueType
            else ILTypeDefKind.Class

        member x.Kind = ILTypeDef.ComputeKind (int x.Attributes) x.Extends x.Namespace x.Name
        member x.IsClass =     (match x.Kind with ILTypeDefKind.Class -> true | _ -> false)
        member x.IsInterface = (match x.Kind with ILTypeDefKind.Interface -> true | _ -> false)
        member x.IsEnum =      (match x.Kind with ILTypeDefKind.Enum -> true | _ -> false)
        member x.IsDelegate =  (match x.Kind with ILTypeDefKind.Delegate -> true | _ -> false)
        member x.IsAbstract= (x.Attributes &&& TypeAttributes.Abstract) <> enum 0
        member x.IsSealed= (x.Attributes &&& TypeAttributes.Sealed) <> enum 0
        member x.IsSerializable= (x.Attributes &&& TypeAttributes.Serializable) <> enum 0
        member x.IsComInterop= (x.Attributes &&& TypeAttributes.Import) <> enum 0
        member x.IsSpecialName= (x.Attributes &&& TypeAttributes.SpecialName) <> enum 0
        member x.Access = ILTypeDefAccess.OfFlags (int x.Attributes)

        member x.IsNested =
            match x.Access with 
            | ILTypeDefAccess.Nested _ -> true
            | _ -> false

        member tdef.IsStructOrEnum =
            match tdef.Kind with
            | ILTypeDefKind.ValueType | ILTypeDefKind.Enum -> true
            | _ -> false

        member x.Encoding = 
            let f = (int x.Attributes &&& 0x00030000)
            if f = 0x00020000 then ILDefaultPInvokeEncoding.Auto
            elif f = 0x00010000 then ILDefaultPInvokeEncoding.Unicode
            else ILDefaultPInvokeEncoding.Ansi

        member x.InitSemantics = 
            if x.Kind = ILTypeDefKind.Interface then ILTypeInit.OnAny
            elif (int x.Attributes &&& 0x00100000) <> 0x0 then ILTypeInit.BeforeField
            else ILTypeInit.OnAny

        override x.ToString() = "type " + x.Name

    and ILTypeDefs(larr: Lazy<(string uoption * string * Lazy<ILTypeDef>)[]>) =

        let mutable lmap = null
        let getmap() =
            if lmap = null then
                lmap <- Dictionary()
                for (nsp, nm, ltd) in larr.Force() do
                    let key = nsp, nm
                    lmap.[key] <- ltd
            lmap

        member __.Entries =
            [| for (_, _, td) in larr.Force() -> td.Force() |]

        member __.TryFindByName (nsp, nm)  =
            let tdefs = getmap()
            let key = (nsp, nm)
            if tdefs.ContainsKey key then
                Some (tdefs.[key].Force())
            else
                None

    type ILNestedExportedType =
        { Name: string
          Access: ILMemberAccess
          Nested: ILNestedExportedTypesAndForwarders
          CustomAttrs: ILCustomAttrs }
        override x.ToString() = "nested fwd " + x.Name

    and ILNestedExportedTypesAndForwarders(larr:Lazy<ILNestedExportedType[]>) =
        let lmap = lazy ((Map.empty, larr.Force()) ||> Array.fold (fun m x -> m.Add(x.Name, x)))
        member __.Entries = larr.Force()
        member __.TryFindByName nm = lmap.Force().TryFind nm

    and [<NoComparison; NoEquality>]
        ILExportedTypeOrForwarder =
        { ScopeRef: ILScopeRef
          Namespace: string uoption
          Name: string
          IsForwarder: bool 
          Access: ILTypeDefAccess; 
          Nested: ILNestedExportedTypesAndForwarders;
          CustomAttrs: ILCustomAttrs } 
        override x.ToString() = "fwd " + x.Name

    and ILExportedTypesAndForwarders(larr:Lazy<ILExportedTypeOrForwarder[]>) =
        let mutable lmap = null
        let getmap() =
            if lmap = null then
                lmap <- Dictionary()
                for ltd in larr.Force() do
                    let key = ltd.Namespace, ltd.Name
                    lmap.[key] <- ltd
            lmap
        member __.Entries = larr.Force()
        member __.TryFindByName (nsp, nm) = match getmap().TryGetValue ((nsp, nm)) with true, v -> Some v | false, _ -> None

    [<RequireQualifiedAccess>]
    type ILResourceAccess =
        | Public
        | Private

    [<RequireQualifiedAccess>]
    type ILResourceLocation =
        | Local of (unit -> byte[])
        | File of ILModuleRef * int32
        | Assembly of ILAssemblyRef

    type ILResource =
        { Name: string
          Location: ILResourceLocation
          Access: ILResourceAccess
          CustomAttrs: ILCustomAttrs }
        override x.ToString() = "resource " + x.Name

    type ILResources(larr: Lazy<ILResource[]>) =
        member __.Entries = larr.Force()

    type ILAssemblyManifest =
        { Name: string
          AuxModuleHashAlgorithm: int32
          PublicKey: byte[] uoption
          Version: Version uoption
          Locale: string uoption
          CustomAttrs: ILCustomAttrs
          //AssemblyLongevity: ILAssemblyLongevity
          DisableJitOptimizations: bool
          JitTracking: bool
          IgnoreSymbolStoreSequencePoints: bool
          Retargetable: bool
          ExportedTypes: ILExportedTypesAndForwarders
          EntrypointElsewhere: ILModuleRef option }

        member x.GetName() =
            let asmName = AssemblyName(Name=x.Name)
            match x.PublicKey with 
            | USome bytes -> asmName.SetPublicKey(bytes)
            | UNone -> ()
            match x.Version with 
            | USome v -> asmName.Version <- v
            | UNone -> ()
            asmName.CultureName <- System.Globalization.CultureInfo.InvariantCulture.Name
            asmName

        override x.ToString() = "manifest " + x.Name

    type ILModuleDef =
        { Manifest: ILAssemblyManifest option
          CustomAttrs: ILCustomAttrs
          Name: string
          TypeDefs: ILTypeDefs
          SubsystemVersion: int * int
          UseHighEntropyVA: bool
          (* Random bits of relatively uninteresting data *)
          SubSystemFlags: int32
          IsDLL: bool
          IsILOnly: bool
          Platform: ILPlatform option 
          StackReserveSize: int32 option
          Is32Bit: bool
          Is32BitPreferred: bool
          Is64Bit: bool
          VirtualAlignment: int32
          PhysicalAlignment: int32
          ImageBase: int32
          MetadataVersion: string
          Resources: ILResources  }

        member x.ManifestOfAssembly =
            match x.Manifest with
            | Some m -> m
            | None -> failwith "no manifest"

        member m.HasManifest = m.Manifest.IsSome

        override x.ToString() = "module " + x.Name


    [<NoEquality; NoComparison>]
    type ILGlobals =
        { typ_Object: ILType
          typ_String: ILType
          typ_Void: ILType
          typ_Type: ILType
          typ_TypedReference: ILType option
          typ_SByte: ILType
          typ_Int16: ILType
          typ_Int32: ILType
          typ_Array: ILType
          typ_Int64: ILType
          typ_Byte: ILType
          typ_UInt16: ILType
          typ_UInt32: ILType
          typ_UInt64: ILType
          typ_Single: ILType
          typ_Double: ILType
          typ_Boolean: ILType
          typ_Char: ILType
          typ_IntPtr: ILType
          typ_UIntPtr: ILType
          systemRuntimeScopeRef: ILScopeRef }
        override __.ToString() = "<ILGlobals>"

    [<AutoOpen>]

    [<Struct>]
    type ILTableName(idx: int) =
        member __.Index = idx
        static member FromIndex n = ILTableName n

    module ILTableNames =
        let Module = ILTableName 0
        let TypeRef = ILTableName 1
        let TypeDef = ILTableName 2
        let FieldPtr = ILTableName 3
        let Field = ILTableName 4
        let MethodPtr = ILTableName 5
        let Method = ILTableName 6
        let ParamPtr = ILTableName 7
        let Param = ILTableName 8
        let InterfaceImpl = ILTableName 9
        let MemberRef = ILTableName 10
        let Constant = ILTableName 11
        let CustomAttribute = ILTableName 12
        let FieldMarshal = ILTableName 13
        let Permission = ILTableName 14
        let ClassLayout = ILTableName 15
        let FieldLayout = ILTableName 16
        let StandAloneSig = ILTableName 17
        let EventMap = ILTableName 18
        let EventPtr = ILTableName 19
        let Event = ILTableName 20
        let PropertyMap = ILTableName 21
        let PropertyPtr = ILTableName 22
        let Property = ILTableName 23
        let MethodSemantics = ILTableName 24
        let MethodImpl = ILTableName 25
        let ModuleRef = ILTableName 26
        let TypeSpec = ILTableName 27
        let ImplMap = ILTableName 28
        let FieldRVA = ILTableName 29
        let ENCLog = ILTableName 30
        let ENCMap = ILTableName 31
        let Assembly = ILTableName 32
        let AssemblyProcessor = ILTableName 33
        let AssemblyOS = ILTableName 34
        let AssemblyRef = ILTableName 35
        let AssemblyRefProcessor = ILTableName 36
        let AssemblyRefOS = ILTableName 37
        let File = ILTableName 38
        let ExportedType = ILTableName 39
        let ManifestResource = ILTableName 40
        let Nested = ILTableName 41
        let GenericParam = ILTableName 42
        let MethodSpec = ILTableName 43
        let GenericParamConstraint = ILTableName 44
        let UserStrings = ILTableName 0x70 (* Special encoding of embedded UserString tokens - See 1.9 Partition III *)

        /// Which tables are sorted and by which column. 
        //
        // Sorted bit-vector as stored by CLR V1: 00fa 0133 0002 0000 
        // But what does this mean?  The ECMA spec does not say! 
        // Metainfo -schema reports sorting as shown below. 
        // But some sorting, e.g. EventMap does not seem to show 
        let sortedTableInfo = 
          [ (InterfaceImpl, 0) 
            (Constant, 1)
            (CustomAttribute, 0)
            (FieldMarshal, 0)
            (Permission, 1)
            (ClassLayout, 2)
            (FieldLayout, 1)
            (MethodSemantics, 2)
            (MethodImpl, 0)
            (ImplMap, 1)
            (FieldRVA, 1)
            (Nested, 0)
            (GenericParam, 2) 
            (GenericParamConstraint, 0) ]

    [<Struct>]
    type TypeDefOrRefOrSpecTag(tag: int32) =
        member __.Tag = tag
        static member TypeDef = TypeDefOrRefOrSpecTag 0x00
        static member TypeRef = TypeDefOrRefOrSpecTag 0x01
        static member TypeSpec = TypeDefOrRefOrSpecTag 0x2

    [<Struct>]
    type HasConstantTag(tag: int32) =
        member __.Tag = tag
        static member FieldDef = HasConstantTag 0x0
        static member ParamDef = HasConstantTag 0x1
        static member Property = HasConstantTag 0x2

    [<Struct>]
    type HasCustomAttributeTag(tag: int32) =
        member __.Tag = tag
        static member MethodDef = HasCustomAttributeTag 0x0
        static member FieldDef = HasCustomAttributeTag 0x1
        static member TypeRef = HasCustomAttributeTag 0x2
        static member TypeDef = HasCustomAttributeTag 0x3
        static member ParamDef = HasCustomAttributeTag 0x4
        static member InterfaceImpl = HasCustomAttributeTag 0x5
        static member MemberRef = HasCustomAttributeTag 0x6
        static member Module = HasCustomAttributeTag 0x7
        static member Permission = HasCustomAttributeTag 0x8
        static member Property = HasCustomAttributeTag 0x9
        static member Event = HasCustomAttributeTag 0xa
        static member StandAloneSig = HasCustomAttributeTag 0xb
        static member ModuleRef = HasCustomAttributeTag 0xc
        static member TypeSpec = HasCustomAttributeTag 0xd
        static member Assembly = HasCustomAttributeTag 0xe
        static member AssemblyRef = HasCustomAttributeTag 0xf
        static member File = HasCustomAttributeTag 0x10
        static member ExportedType = HasCustomAttributeTag 0x11
        static member ManifestResource = HasCustomAttributeTag 0x12
        static member GenericParam = HasCustomAttributeTag 0x13
        static member GenericParamConstraint = HasCustomAttributeTag 0x14
        static member MethodSpec = HasCustomAttributeTag 0x15

    [<Struct>]
    type HasFieldMarshalTag(tag: int32) =
        member __.Tag = tag
        static member FieldDef =  HasFieldMarshalTag 0x00
        static member ParamDef =  HasFieldMarshalTag 0x01

    [<Struct>]
    type HasDeclSecurityTag(tag: int32) =
        member __.Tag = tag
        static member TypeDef =  HasDeclSecurityTag 0x00
        static member MethodDef =  HasDeclSecurityTag 0x01
        static member Assembly =  HasDeclSecurityTag 0x02

    [<Struct>]
    type MemberRefParentTag(tag: int32) =
        member __.Tag = tag
        static member TypeRef = MemberRefParentTag 0x01
        static member ModuleRef = MemberRefParentTag 0x02
        static member MethodDef = MemberRefParentTag 0x03
        static member TypeSpec = MemberRefParentTag 0x04

    [<Struct>]
    type HasSemanticsTag(tag: int32) =
        member __.Tag = tag
        static member Event =  HasSemanticsTag 0x00
        static member Property =  HasSemanticsTag 0x01

    [<Struct>]
    type MethodDefOrRefTag(tag: int32) =
        member __.Tag = tag
        static member MethodDef =  MethodDefOrRefTag 0x00
        static member MemberRef =  MethodDefOrRefTag 0x01
        static member MethodSpec =  MethodDefOrRefTag 0x02

    [<Struct>]
    type MemberForwardedTag(tag: int32) =
        member __.Tag = tag
        static member FieldDef =  MemberForwardedTag 0x00
        static member MethodDef =  MemberForwardedTag 0x01

    [<Struct>]
    type ImplementationTag(tag: int32) =
        member __.Tag = tag
        static member File =  ImplementationTag 0x00
        static member AssemblyRef =  ImplementationTag 0x01
        static member ExportedType =  ImplementationTag 0x02

    [<Struct>]
    type CustomAttributeTypeTag(tag: int32) =
        member __.Tag = tag
        static member MethodDef =  CustomAttributeTypeTag 0x02
        static member MemberRef =  CustomAttributeTypeTag 0x03

    [<Struct>]
    type ResolutionScopeTag(tag: int32) =
        member __.Tag = tag
        static member Module =  ResolutionScopeTag 0x00
        static member ModuleRef =  ResolutionScopeTag 0x01
        static member AssemblyRef =  ResolutionScopeTag 0x02
        static member TypeRef =  ResolutionScopeTag 0x03

    [<Struct>]
    type TypeOrMethodDefTag(tag: int32) =
        member __.Tag = tag
        static member TypeDef = TypeOrMethodDefTag 0x00
        static member MethodDef = TypeOrMethodDefTag 0x01

    [<Struct>]
    type TaggedIndex<'T> =
        val tag: 'T
        val index: int32
        new(tag, index) = { tag=tag; index=index }


    type ILImageChunk = { size: int32; addr: int32 }

    type ILRowElementKind =
        | UShort
        | ULong
        | Byte
        | Data
        | GGuid
        | Blob
        | SString
        | SimpleIndex of ILTableName
        | TypeDefOrRefOrSpec
        | TypeOrMethodDef
        | HasConstant
        | HasCustomAttribute
        | HasFieldMarshal
        | HasDeclSecurity
        | MemberRefParent
        | HasSemantics
        | MethodDefOrRef
        | MemberForwarded
        | Implementation
        | CustomAttributeType
        | ResolutionScope

    type ILRowKind = ILRowKind of ILRowElementKind list

    type TypeDefAsTypIdx = TypeDefAsTypIdx of ILBoxity * ILGenericArgs * int
    type TypeRefAsTypIdx = TypeRefAsTypIdx of ILBoxity * ILGenericArgs * int
    type BlobAsMethodSigIdx = BlobAsMethodSigIdx of int * int32
    type BlobAsFieldSigIdx = BlobAsFieldSigIdx of int * int32
    type BlobAsPropSigIdx = BlobAsPropSigIdx of int * int32
    type BlobAsLocalSigIdx = BlobAsLocalSigIdx of int * int32
    type MemberRefAsMspecIdx =  MemberRefAsMspecIdx of int * int
    type MethodSpecAsMspecIdx =  MethodSpecAsMspecIdx of int * int
    type MemberRefAsFspecIdx = MemberRefAsFspecIdx of int * int
    type CustomAttrIdx = CustomAttrIdx of CustomAttributeTypeTag * int * int32
    type SecurityDeclIdx = SecurityDeclIdx of uint16 * int32
    type GenericParamsIdx = GenericParamsIdx of int * TypeOrMethodDefTag * int

    type MethodData = MethodData of ILType * ILCallingConv * string * ILTypes * ILType * ILTypes
    type VarArgMethodData = VarArgMethodData of ILType * ILCallingConv * string * ILTypes * ILVarArgs * ILType * ILTypes

    [<AutoOpen>]
    module Constants = 
        let et_END = 0x00uy
        let et_VOID = 0x01uy
        let et_BOOLEAN = 0x02uy
        let et_CHAR = 0x03uy
        let et_I1 = 0x04uy
        let et_U1 = 0x05uy
        let et_I2 = 0x06uy
        let et_U2 = 0x07uy
        let et_I4 = 0x08uy
        let et_U4 = 0x09uy
        let et_I8 = 0x0Auy
        let et_U8 = 0x0Buy
        let et_R4 = 0x0Cuy
        let et_R8 = 0x0Duy
        let et_STRING = 0x0Euy
        let et_PTR = 0x0Fuy
        let et_BYREF = 0x10uy
        let et_VALUETYPE = 0x11uy
        let et_CLASS = 0x12uy
        let et_VAR = 0x13uy
        let et_ARRAY = 0x14uy
        let et_WITH = 0x15uy
        let et_TYPEDBYREF = 0x16uy
        let et_I = 0x18uy
        let et_U = 0x19uy
        let et_FNPTR = 0x1Buy
        let et_OBJECT = 0x1Cuy
        let et_SZARRAY = 0x1Duy
        let et_MVAR = 0x1euy
        let et_CMOD_REQD = 0x1Fuy
        let et_CMOD_OPT = 0x20uy

        let et_SENTINEL = 0x41uy // sentinel for varargs
        let et_PINNED = 0x45uy

        let e_IMAGE_CEE_CS_CALLCONV_FASTCALL = 0x04uy
        let e_IMAGE_CEE_CS_CALLCONV_STDCALL = 0x02uy
        let e_IMAGE_CEE_CS_CALLCONV_THISCALL = 0x03uy
        let e_IMAGE_CEE_CS_CALLCONV_CDECL = 0x01uy
        let e_IMAGE_CEE_CS_CALLCONV_VARARG = 0x05uy
        let e_IMAGE_CEE_CS_CALLCONV_FIELD = 0x06uy
        let e_IMAGE_CEE_CS_CALLCONV_LOCAL_SIG = 0x07uy
        let e_IMAGE_CEE_CS_CALLCONV_PROPERTY = 0x08uy

        let e_IMAGE_CEE_CS_CALLCONV_GENERICINST = 0x0auy
        let e_IMAGE_CEE_CS_CALLCONV_GENERIC = 0x10uy
        let e_IMAGE_CEE_CS_CALLCONV_INSTANCE = 0x20uy
        let e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT = 0x40uy


        // Logical shift right treating int32 as unsigned integer.
        // Code that uses this should probably be adjusted to use unsigned integer types.
        let (>>>&) (x:int32) (n:int32) = int32 (uint32 x >>> n)

        let align alignment n = ((n + alignment - 0x1) / alignment) * alignment

        let uncodedToken (tab:ILTableName) idx = ((tab.Index <<< 24) ||| idx)

        let i32ToUncodedToken tok  =
            let idx = tok &&& 0xffffff
            let tab = tok >>>& 24
            (ILTableName.FromIndex tab, idx)


        let uncodedTokenToTypeDefOrRefOrSpec (tab, tok) =
            let tag =
                if tab = ILTableNames.TypeDef then TypeDefOrRefOrSpecTag.TypeDef
                elif tab = ILTableNames.TypeRef then TypeDefOrRefOrSpecTag.TypeRef
                elif tab = ILTableNames.TypeSpec then TypeDefOrRefOrSpecTag.TypeSpec
                else failwith "bad table in uncodedTokenToTypeDefOrRefOrSpec"
            TaggedIndex(tag, tok)

        let uncodedTokenToMethodDefOrRef (tab, tok) =
            let tag =
                if tab = ILTableNames.Method then MethodDefOrRefTag.MethodDef
                elif tab = ILTableNames.MemberRef then MethodDefOrRefTag.MemberRef
                else failwith "bad table in uncodedTokenToMethodDefOrRef"
            TaggedIndex(tag, tok)

        let (|TaggedIndex|) (x:TaggedIndex<'T>) = x.tag, x.index
        let tokToTaggedIdx f nbits tok =
            let tagmask =
                if nbits = 1 then 1
                elif nbits = 2 then 3
                elif nbits = 3 then 7
                elif nbits = 4 then 15
                   elif nbits = 5 then 31
                   else failwith "too many nbits"
            let tag = tok &&& tagmask
            let idx = tok >>>& nbits
            TaggedIndex(f tag, idx)

        let i_nop           = 0x00 
        let i_break         = 0x01 
        let i_ldarg_0       = 0x02 
        let i_ldarg_1       = 0x03 
        let i_ldarg_2       = 0x04 
        let i_ldarg_3       = 0x05 
        let i_ldloc_0       = 0x06 
        let i_ldloc_1       = 0x07 
        let i_ldloc_2       = 0x08 
        let i_ldloc_3       = 0x09 
        let i_stloc_0       = 0x0a 
        let i_stloc_1       = 0x0b 
        let i_stloc_2       = 0x0c 
        let i_stloc_3       = 0x0d 
        let i_ldarg_s       = 0x0e 
        let i_ldarga_s      = 0x0f 
        let i_starg_s       = 0x10 
        let i_ldloc_s       = 0x11 
        let i_ldloca_s      = 0x12 
        let i_stloc_s       = 0x13 
        let i_ldnull        = 0x14 
        let i_ldc_i4_m1     = 0x15 
        let i_ldc_i4_0      = 0x16 
        let i_ldc_i4_1      = 0x17 
        let i_ldc_i4_2      = 0x18 
        let i_ldc_i4_3      = 0x19 
        let i_ldc_i4_4      = 0x1a 
        let i_ldc_i4_5      = 0x1b 
        let i_ldc_i4_6      = 0x1c 
        let i_ldc_i4_7      = 0x1d 
        let i_ldc_i4_8      = 0x1e 
        let i_ldc_i4_s      = 0x1f 
        let i_ldc_i4        = 0x20 
        let i_ldc_i8        = 0x21 
        let i_ldc_r4        = 0x22 
        let i_ldc_r8        = 0x23 
        let i_dup           = 0x25 
        let i_pop           = 0x26 
        let i_jmp           = 0x27 
        let i_call          = 0x28 
        let i_calli         = 0x29 
        let i_ret           = 0x2a 
        let i_br_s          = 0x2b 
        let i_brfalse_s     = 0x2c 
        let i_brtrue_s      = 0x2d 
        let i_beq_s         = 0x2e 
        let i_bge_s         = 0x2f 
        let i_bgt_s         = 0x30 
        let i_ble_s         = 0x31 
        let i_blt_s         = 0x32 
        let i_bne_un_s      = 0x33 
        let i_bge_un_s      = 0x34 
        let i_bgt_un_s      = 0x35 
        let i_ble_un_s      = 0x36 
        let i_blt_un_s      = 0x37 
        let i_br            = 0x38 
        let i_brfalse       = 0x39 
        let i_brtrue        = 0x3a 
        let i_beq           = 0x3b 
        let i_bge           = 0x3c 
        let i_bgt           = 0x3d 
        let i_ble           = 0x3e 
        let i_blt           = 0x3f 
        let i_bne_un        = 0x40 
        let i_bge_un        = 0x41 
        let i_bgt_un        = 0x42 
        let i_ble_un        = 0x43 
        let i_blt_un        = 0x44 
        let i_switch        = 0x45 
        let i_ldind_i1      = 0x46 
        let i_ldind_u1      = 0x47 
        let i_ldind_i2      = 0x48 
        let i_ldind_u2      = 0x49 
        let i_ldind_i4      = 0x4a 
        let i_ldind_u4      = 0x4b 
        let i_ldind_i8      = 0x4c 
        let i_ldind_i       = 0x4d 
        let i_ldind_r4      = 0x4e 
        let i_ldind_r8      = 0x4f 
        let i_ldind_ref     = 0x50 
        let i_stind_ref     = 0x51 
        let i_stind_i1      = 0x52 
        let i_stind_i2      = 0x53 
        let i_stind_i4      = 0x54 
        let i_stind_i8      = 0x55 
        let i_stind_r4      = 0x56 
        let i_stind_r8      = 0x57 
        let i_add           = 0x58 
        let i_sub           = 0x59 
        let i_mul           = 0x5a 
        let i_div           = 0x5b 
        let i_div_un        = 0x5c 
        let i_rem           = 0x5d 
        let i_rem_un        = 0x5e 
        let i_and           = 0x5f 
        let i_or            = 0x60 
        let i_xor           = 0x61 
        let i_shl           = 0x62 
        let i_shr           = 0x63 
        let i_shr_un        = 0x64 
        let i_neg           = 0x65 
        let i_not           = 0x66 
        let i_conv_i1       = 0x67 
        let i_conv_i2       = 0x68 
        let i_conv_i4       = 0x69 
        let i_conv_i8       = 0x6a 
        let i_conv_r4       = 0x6b 
        let i_conv_r8       = 0x6c 
        let i_conv_u4       = 0x6d 
        let i_conv_u8       = 0x6e 
        let i_callvirt      = 0x6f 
        let i_cpobj         = 0x70 
        let i_ldobj         = 0x71 
        let i_ldstr         = 0x72 
        let i_newobj        = 0x73 
        let i_castclass     = 0x74 
        let i_isinst        = 0x75 
        let i_conv_r_un     = 0x76 
        let i_unbox         = 0x79 
        let i_throw         = 0x7a 
        let i_ldfld         = 0x7b 
        let i_ldflda        = 0x7c 
        let i_stfld         = 0x7d 
        let i_ldsfld        = 0x7e 
        let i_ldsflda       = 0x7f 
        let i_stsfld        = 0x80 
        let i_stobj         = 0x81 
        let i_conv_ovf_i1_un= 0x82 
        let i_conv_ovf_i2_un= 0x83 
        let i_conv_ovf_i4_un= 0x84 
        let i_conv_ovf_i8_un= 0x85 
        let i_conv_ovf_u1_un= 0x86 
        let i_conv_ovf_u2_un= 0x87 
        let i_conv_ovf_u4_un= 0x88 
        let i_conv_ovf_u8_un= 0x89 
        let i_conv_ovf_i_un = 0x8a 
        let i_conv_ovf_u_un = 0x8b 
        let i_box           = 0x8c 
        let i_newarr        = 0x8d 
        let i_ldlen         = 0x8e 
        let i_ldelema       = 0x8f 
        let i_ldelem_i1     = 0x90 
        let i_ldelem_u1     = 0x91 
        let i_ldelem_i2     = 0x92 
        let i_ldelem_u2     = 0x93 
        let i_ldelem_i4     = 0x94 
        let i_ldelem_u4     = 0x95 
        let i_ldelem_i8     = 0x96 
        let i_ldelem_i      = 0x97 
        let i_ldelem_r4     = 0x98 
        let i_ldelem_r8     = 0x99 
        let i_ldelem_ref    = 0x9a 
        let i_stelem_i      = 0x9b 
        let i_stelem_i1     = 0x9c 
        let i_stelem_i2     = 0x9d 
        let i_stelem_i4     = 0x9e 
        let i_stelem_i8     = 0x9f 
        let i_stelem_r4     = 0xa0 
        let i_stelem_r8     = 0xa1 
        let i_stelem_ref    = 0xa2 
        let i_conv_ovf_i1   = 0xb3 
        let i_conv_ovf_u1   = 0xb4 
        let i_conv_ovf_i2   = 0xb5 
        let i_conv_ovf_u2   = 0xb6 
        let i_conv_ovf_i4   = 0xb7 
        let i_conv_ovf_u4   = 0xb8 
        let i_conv_ovf_i8   = 0xb9 
        let i_conv_ovf_u8   = 0xba 
        let i_refanyval     = 0xc2 
        let i_ckfinite      = 0xc3 
        let i_mkrefany      = 0xc6 
        let i_ldtoken       = 0xd0 
        let i_conv_u2       = 0xd1 
        let i_conv_u1       = 0xd2 
        let i_conv_i        = 0xd3 
        let i_conv_ovf_i    = 0xd4 
        let i_conv_ovf_u    = 0xd5 
        let i_add_ovf       = 0xd6 
        let i_add_ovf_un    = 0xd7 
        let i_mul_ovf       = 0xd8 
        let i_mul_ovf_un    = 0xd9 
        let i_sub_ovf       = 0xda 
        let i_sub_ovf_un    = 0xdb 
        let i_endfinally    = 0xdc 
        let i_leave         = 0xdd 
        let i_leave_s       = 0xde 
        let i_stind_i       = 0xdf 
        let i_conv_u        = 0xe0 
        let i_arglist        = 0xfe00
        let i_ceq        = 0xfe01
        let i_cgt        = 0xfe02
        let i_cgt_un        = 0xfe03
        let i_clt        = 0xfe04
        let i_clt_un        = 0xfe05
        let i_ldftn        = 0xfe06 
        let i_ldvirtftn    = 0xfe07 
        let i_ldarg          = 0xfe09 
        let i_ldarga         = 0xfe0a 
        let i_starg          = 0xfe0b 
        let i_ldloc          = 0xfe0c 
        let i_ldloca         = 0xfe0d 
        let i_stloc          = 0xfe0e 
        let i_localloc     = 0xfe0f 
        let i_endfilter    = 0xfe11 
        let i_unaligned   = 0xfe12 
        let i_volatile    = 0xfe13 
        let i_constrained    = 0xfe16
        let i_readonly    = 0xfe1e
        let i_tail           = 0xfe14 
        let i_initobj        = 0xfe15 
        let i_cpblk          = 0xfe17 
        let i_initblk        = 0xfe18 
        let i_rethrow        = 0xfe1a 
        let i_sizeof         = 0xfe1c 
        let i_refanytype   = 0xfe1d 

        let i_ldelem_any = 0xa3
        let i_stelem_any = 0xa4
        let i_unbox_any = 0xa5

        let mk_ldc i = I_ldc (DT_I4, ILConst.I4 i)
        let mk_ldc_i8 i = I_ldc (DT_I8, ILConst.I8 i)
        let mkNormalCall mspec = I_call (Normalcall, mspec, None)
        let mkILFormalGenericArgs numtypars (n:int) =
            Array.init n (fun i -> ILType.Var (numtypars + i))


        let noArgInstrs  = 
           lazy [ i_ldc_i4_0, mk_ldc 0
                  i_ldc_i4_1, mk_ldc 1
                  i_ldc_i4_2, mk_ldc 2
                  i_ldc_i4_3, mk_ldc 3
                  i_ldc_i4_4, mk_ldc 4
                  i_ldc_i4_5, mk_ldc 5
                  i_ldc_i4_6, mk_ldc 6
                  i_ldc_i4_7, mk_ldc 7
                  i_ldc_i4_8, mk_ldc 8
                  i_ldc_i4_m1, mk_ldc -1
                  0x0a, I_stloc 0
                  0x0b, I_stloc 1
                  0x0c, I_stloc 2
                  0x0d, I_stloc 3
                  0x06, I_ldloc 0
                  0x07, I_ldloc 1
                  0x08, I_ldloc 2
                  0x09, I_ldloc 3
                  0x02, I_ldarg 0
                  0x03, I_ldarg 1
                  0x04, I_ldarg 2
                  0x05, I_ldarg 3
                  0x2a, I_ret
                  0x58, I_add
                  0xd6, I_add_ovf
                  0xd7, I_add_ovf_un
                  0x5f, I_and
                  0x5b, I_div
                  0x5c, I_div_un
                  0xfe01, I_ceq
                  0xfe02, I_cgt
                  0xfe03, I_cgt_un
                  0xfe04, I_clt
                  0xfe05, I_clt_un
                  0x67, I_conv DT_I1
                  0x68, I_conv DT_I2 
                  0x69, I_conv DT_I4
                  0x6a, I_conv DT_I8  
                  0xd3, I_conv DT_I  
                  0x6b, I_conv DT_R4  
                  0x6c, I_conv DT_R8  
                  0xd2, I_conv DT_U1  
                  0xd1, I_conv DT_U2  
                  0x6d, I_conv DT_U4  
                  0x6e, I_conv DT_U8  
                  0xe0, I_conv DT_U  
                  0x76, I_conv DT_R  
                  0xb3, I_conv_ovf DT_I1  
                  0xb5, I_conv_ovf DT_I2  
                  0xb7, I_conv_ovf DT_I4  
                  0xb9, I_conv_ovf DT_I8  
                  0xd4, I_conv_ovf DT_I  
                  0xb4, I_conv_ovf DT_U1  
                  0xb6, I_conv_ovf DT_U2  
                  0xb8, I_conv_ovf DT_U4  
                  0xba, I_conv_ovf DT_U8  
                  0xd5, I_conv_ovf DT_U  
                  0x82, I_conv_ovf_un DT_I1  
                  0x83, I_conv_ovf_un DT_I2  
                  0x84, I_conv_ovf_un DT_I4  
                  0x85, I_conv_ovf_un DT_I8  
                  0x8a, I_conv_ovf_un DT_I  
                  0x86, I_conv_ovf_un DT_U1  
                  0x87, I_conv_ovf_un DT_U2  
                  0x88, I_conv_ovf_un DT_U4  
                  0x89, I_conv_ovf_un DT_U8  
                  0x8b, I_conv_ovf_un DT_U  
                  0x9c, I_stelem DT_I1  
                  0x9d, I_stelem DT_I2
                  0x9e, I_stelem DT_I4  
                  0x9f, I_stelem DT_I8  
                  0xa0, I_stelem DT_R4  
                  0xa1, I_stelem DT_R8  
                  0x9b, I_stelem DT_I  
                  0xa2, I_stelem DT_REF  
                  0x90, I_ldelem DT_I1
                  0x92, I_ldelem DT_I2  
                  0x94, I_ldelem DT_I4  
                  0x96, I_ldelem DT_I8  
                  0x91, I_ldelem DT_U1  
                  0x93, I_ldelem DT_U2  
                  0x95, I_ldelem DT_U4 
                  0x98, I_ldelem DT_R4  
                  0x99, I_ldelem DT_R8  
                  0x97, I_ldelem DT_I  
                  0x9a, I_ldelem DT_REF  
                  0x5a, I_mul
                  0xd8, I_mul_ovf
                  0xd9, I_mul_ovf_un
                  0x5d, I_rem
                  0x5e, I_rem_un
                  0x62, I_shl 
                  0x63, I_shr
                  0x64, I_shr_un
                  0x59, I_sub
                  0xda, I_sub_ovf
                  0xdb, I_sub_ovf_un
                  0x61, I_xor
                  0x60, I_or
                  0x65, I_neg
                  0x66, I_not
                  i_ldnull, I_ldnull
                  i_dup, I_dup
                  i_pop, I_pop
                  i_ckfinite, I_ckfinite
                  i_nop, I_nop
                  i_break, I_break
                  i_arglist, I_arglist
                  i_endfilter, I_endfilter
                  i_endfinally, I_endfinally
                  i_refanytype, I_refanytype
                  i_localloc, I_localloc
                  i_throw, I_throw
                  i_ldlen, I_ldlen
                  i_rethrow, I_rethrow ]

        let isNoArgInstr i = 
          match i with 
          | I_ldc (DT_I4, ILConst.I4 n) when -1 <= n && n <= 8 -> true
          | I_stloc n | I_ldloc n | I_ldarg n when n <= 3 -> true
          | I_ret
          | I_add
          | I_add_ovf
          | I_add_ovf_un
          | I_and  
          | I_div 
          | I_div_un
          | I_ceq  
          | I_cgt 
          | I_cgt_un
          | I_clt
          | I_clt_un
          | I_conv DT_I1  
          | I_conv DT_I2  
          | I_conv DT_I4  
          | I_conv DT_I8  
          | I_conv DT_I  
          | I_conv DT_R4  
          | I_conv DT_R8  
          | I_conv DT_U1  
          | I_conv DT_U2  
          | I_conv DT_U4  
          | I_conv DT_U8  
          | I_conv DT_U  
          | I_conv DT_R  
          | I_conv_ovf DT_I1  
          | I_conv_ovf DT_I2  
          | I_conv_ovf DT_I4  
          | I_conv_ovf DT_I8  
          | I_conv_ovf DT_I  
          | I_conv_ovf DT_U1  
          | I_conv_ovf DT_U2  
          | I_conv_ovf DT_U4  
          | I_conv_ovf DT_U8  
          | I_conv_ovf DT_U  
          | I_conv_ovf_un DT_I1  
          | I_conv_ovf_un DT_I2  
          | I_conv_ovf_un DT_I4  
          | I_conv_ovf_un DT_I8  
          | I_conv_ovf_un DT_I  
          | I_conv_ovf_un DT_U1  
          | I_conv_ovf_un DT_U2  
          | I_conv_ovf_un DT_U4  
          | I_conv_ovf_un DT_U8  
          | I_conv_ovf_un DT_U  
          | I_stelem DT_I1  
          | I_stelem DT_I2  
          | I_stelem DT_I4  
          | I_stelem DT_I8  
          | I_stelem DT_R4  
          | I_stelem DT_R8  
          | I_stelem DT_I  
          | I_stelem DT_REF  
          | I_ldelem DT_I1  
          | I_ldelem DT_I2  
          | I_ldelem DT_I4  
          | I_ldelem DT_I8  
          | I_ldelem DT_U1  
          | I_ldelem DT_U2  
          | I_ldelem DT_U4  
          | I_ldelem DT_R4  
          | I_ldelem DT_R8  
          | I_ldelem DT_I  
          | I_ldelem DT_REF  
          | I_mul  
          | I_mul_ovf
          | I_mul_ovf_un
          | I_rem  
          | I_rem_un  
          | I_shl  
          | I_shr  
          | I_shr_un
          | I_sub  
          | I_sub_ovf
          | I_sub_ovf_un 
          | I_xor  
          | I_or     
          | I_neg     
          | I_not     
          | I_ldnull   
          | I_dup   
          | I_pop
          | I_ckfinite
          | I_nop
          | I_break
          | I_arglist
          | I_endfilter
          | I_endfinally
          | I_refanytype
          | I_localloc
          | I_throw
          | I_ldlen
          | I_rethrow -> true
          | _ -> false

        let ILCmpInstrMap = 
            lazy (
                let dict = Dictionary 12
                dict.Add (I_beq     , i_beq     )
                dict.Add (I_bgt     , i_bgt     )
                dict.Add (I_bgt_un  , i_bgt_un  )
                dict.Add (I_bge     , i_bge     )
                dict.Add (I_bge_un  , i_bge_un  )
                dict.Add (I_ble     , i_ble     )
                dict.Add (I_ble_un  , i_ble_un  )
                dict.Add (I_blt     , i_blt     )
                dict.Add (I_blt_un  , i_blt_un  )
                dict.Add (I_bne_un  , i_bne_un  )
                dict.Add (I_brfalse , i_brfalse )
                dict.Add (I_brtrue  , i_brtrue  )
                dict
            )

        let ILCmpInstrRevMap = 
          lazy (
              let dict = Dictionary 12
              dict.Add ( I_beq     , i_beq_s     )
              dict.Add ( I_bgt     , i_bgt_s     )
              dict.Add ( I_bgt_un  , i_bgt_un_s  )
              dict.Add ( I_bge     , i_bge_s     )
              dict.Add ( I_bge_un  , i_bge_un_s  )
              dict.Add ( I_ble     , i_ble_s     )
              dict.Add ( I_ble_un  , i_ble_un_s  )
              dict.Add ( I_blt     , i_blt_s     )
              dict.Add ( I_blt_un  , i_blt_un_s  )
              dict.Add ( I_bne_un  , i_bne_un_s  )
              dict.Add ( I_brfalse , i_brfalse_s )
              dict.Add ( I_brtrue  , i_brtrue_s  )
              dict
          )

        // From corhdr.h 

        let nt_VOID        = 0x1uy
        let nt_BOOLEAN     = 0x2uy
        let nt_I1          = 0x3uy
        let nt_U1          = 0x4uy
        let nt_I2          = 0x5uy
        let nt_U2          = 0x6uy
        let nt_I4          = 0x7uy
        let nt_U4          = 0x8uy
        let nt_I8          = 0x9uy
        let nt_U8          = 0xAuy
        let nt_R4          = 0xBuy
        let nt_R8          = 0xCuy
        let nt_SYSCHAR     = 0xDuy
        let nt_VARIANT     = 0xEuy
        let nt_CURRENCY    = 0xFuy
        let nt_PTR         = 0x10uy
        let nt_DECIMAL     = 0x11uy
        let nt_DATE        = 0x12uy
        let nt_BSTR        = 0x13uy
        let nt_LPSTR       = 0x14uy
        let nt_LPWSTR      = 0x15uy
        let nt_LPTSTR      = 0x16uy
        let nt_FIXEDSYSSTRING  = 0x17uy
        let nt_OBJECTREF   = 0x18uy
        let nt_IUNKNOWN    = 0x19uy
        let nt_IDISPATCH   = 0x1Auy
        let nt_STRUCT      = 0x1Buy
        let nt_INTF        = 0x1Cuy
        let nt_SAFEARRAY   = 0x1Duy
        let nt_FIXEDARRAY  = 0x1Euy
        let nt_INT         = 0x1Fuy
        let nt_UINT        = 0x20uy
        let nt_NESTEDSTRUCT  = 0x21uy
        let nt_BYVALSTR    = 0x22uy
        let nt_ANSIBSTR    = 0x23uy
        let nt_TBSTR       = 0x24uy
        let nt_VARIANTBOOL = 0x25uy
        let nt_FUNC        = 0x26uy
        let nt_ASANY       = 0x28uy
        let nt_ARRAY       = 0x2Auy
        let nt_LPSTRUCT    = 0x2Buy
        let nt_CUSTOMMARSHALER = 0x2Cuy
        let nt_ERROR       = 0x2Duy
        let nt_MAX = 0x50uy

        // From c:/clrenv.i386/Crt/Inc/i386/hs.h

        let vt_EMPTY = 0
        let vt_NULL = 1
        let vt_I2 = 2
        let vt_I4 = 3
        let vt_R4 = 4
        let vt_R8 = 5
        let vt_CY = 6
        let vt_DATE = 7
        let vt_BSTR = 8
        let vt_DISPATCH = 9
        let vt_ERROR = 10
        let vt_BOOL = 11
        let vt_VARIANT = 12
        let vt_UNKNOWN = 13
        let vt_DECIMAL = 14
        let vt_I1 = 16
        let vt_UI1 = 17
        let vt_UI2 = 18
        let vt_UI4 = 19
        let vt_I8 = 20
        let vt_UI8 = 21
        let vt_INT = 22
        let vt_UINT = 23
        let vt_VOID = 24
        let vt_HRESULT  = 25
        let vt_PTR = 26
        let vt_SAFEARRAY = 27
        let vt_CARRAY = 28
        let vt_USERDEFINED = 29
        let vt_LPSTR = 30
        let vt_LPWSTR = 31
        let vt_RECORD = 36
        let vt_FILETIME = 64
        let vt_BLOB = 65
        let vt_STREAM = 66
        let vt_STORAGE = 67
        let vt_STREAMED_OBJECT = 68
        let vt_STORED_OBJECT = 69
        let vt_BLOB_OBJECT = 70
        let vt_CF = 71
        let vt_CLSID = 72
        let vt_VECTOR = 0x1000
        let vt_ARRAY = 0x2000
        let vt_BYREF = 0x4000

 

        let e_CorILMethod_TinyFormat = 0x02uy
        let e_CorILMethod_FatFormat = 0x03uy
        let e_CorILMethod_FormatMask = 0x03uy
        let e_CorILMethod_MoreSects = 0x08uy
        let e_CorILMethod_InitLocals = 0x10uy


        let e_CorILMethod_Sect_EHTable = 0x1uy
        let e_CorILMethod_Sect_FatFormat = 0x40uy
        let e_CorILMethod_Sect_MoreSects = 0x80uy

        let e_COR_ILEXCEPTION_CLAUSE_EXCEPTION = 0x0
        let e_COR_ILEXCEPTION_CLAUSE_FILTER = 0x1
        let e_COR_ILEXCEPTION_CLAUSE_FINALLY = 0x2
        let e_COR_ILEXCEPTION_CLAUSE_FAULT = 0x4


        module Bytes = 

            let dWw1 n = int32 ((n >>> 32) &&& 0xFFFFFFFFL)
            let dWw0 n = int32 (n          &&& 0xFFFFFFFFL)

            let get (b:byte[]) n = int32 (Array.get b n)  
            let zeroCreate n: byte[] = Array.zeroCreate n      

            let sub ( b:byte[]) s l = Array.sub b s l   
            let blit (a:byte[]) b c d e = Array.blit a b c d e 

            let ofInt32Array (arr:int[]) = Array.init arr.Length (fun i -> byte arr.[i]) 

            let stringAsUtf8NullTerminated (s:string) = 
                Array.append (Encoding.UTF8.GetBytes s) (ofInt32Array [| 0x0 |]) 

            let stringAsUnicodeNullTerminated (s:string) = 
                Array.append (Encoding.Unicode.GetBytes s) (ofInt32Array [| 0x0;0x0 |]) 

        type ByteStream = 
            { bytes: byte[] 
              mutable pos: int 
              max: int }
            member b.ReadByte() = 
                if b.pos >= b.max then failwith "end of stream"
                let res = b.bytes.[b.pos]
                b.pos <- b.pos + 1
                res 
            member b.ReadUtf8String n = 
                let res = Encoding.UTF8.GetString(b.bytes, b.pos, n)  
                b.pos <- b.pos + n; res 
      
            static member FromBytes (b:byte[], n, len) = 
                if n < 0 || (n+len) > b.Length then failwith "FromBytes"
                { bytes = b; pos = n; max = n+len }

            member b.ReadBytes n  = 
                if b.pos + n > b.max then failwith "ReadBytes: end of stream"
                let res = Bytes.sub b.bytes b.pos n
                b.pos <- b.pos + n
                res 

            member b.Position = b.pos 


        type ByteBuffer = 
            { mutable bbArray: byte[] 
              mutable bbCurrent: int }

            member buf.Ensure newSize = 
                let oldBufSize = buf.bbArray.Length 
                if newSize > oldBufSize then 
                    let old = buf.bbArray 
                    buf.bbArray <- Bytes.zeroCreate (max newSize (oldBufSize * 2))
                    Bytes.blit old 0 buf.bbArray 0 buf.bbCurrent

            member buf.Close () = Bytes.sub buf.bbArray 0 buf.bbCurrent

            member buf.EmitIntAsByte (i:int) = 
                let newSize = buf.bbCurrent + 1 
                buf.Ensure newSize
                buf.bbArray.[buf.bbCurrent] <- byte i
                buf.bbCurrent <- newSize 

            member buf.EmitByte (b:byte) = buf.EmitIntAsByte (int b)

            member buf.EmitIntsAsBytes (arr:int[]) = 
                let n = arr.Length
                let newSize = buf.bbCurrent + n 
                buf.Ensure newSize
                let bbarr = buf.bbArray
                let bbbase = buf.bbCurrent
                for i = 0 to n - 1 do 
                    bbarr.[bbbase + i] <- byte arr.[i] 
                buf.bbCurrent <- newSize 

            member bb.FixupInt32 pos n = 
                bb.bbArray.[pos] <- (b0 n |> byte)
                bb.bbArray.[pos + 1] <- (b1 n |> byte)
                bb.bbArray.[pos + 2] <- (b2 n |> byte)
                bb.bbArray.[pos + 3] <- (b3 n |> byte)

            member buf.EmitInt32 n = 
                let newSize = buf.bbCurrent + 4 
                buf.Ensure newSize
                buf.FixupInt32 buf.bbCurrent n
                buf.bbCurrent <- newSize 

            member buf.EmitBytes (i:byte[]) = 
                let n = i.Length 
                let newSize = buf.bbCurrent + n 
                buf.Ensure newSize
                Bytes.blit i 0 buf.bbArray buf.bbCurrent n
                buf.bbCurrent <- newSize 

            member buf.EmitInt32AsUInt16 n = 
                let newSize = buf.bbCurrent + 2 
                buf.Ensure newSize
                buf.bbArray.[buf.bbCurrent] <- (b0 n |> byte)
                buf.bbArray.[buf.bbCurrent + 1] <- (b1 n |> byte)
                buf.bbCurrent <- newSize 
    
            member buf.EmitBoolAsByte (b:bool) = buf.EmitIntAsByte (if b then 1 else 0)

            member buf.EmitUInt16 (x:uint16) = buf.EmitInt32AsUInt16 (int32 x)

            member buf.EmitInt64 x = 
                buf.EmitInt32 (Bytes.dWw0 x)
                buf.EmitInt32 (Bytes.dWw1 x)

            member buf.Position = buf.bbCurrent

            static member Create sz = 
                { bbArray=Bytes.zeroCreate sz 
                  bbCurrent = 0 }

            /// Z32 = compressed unsigned integer 
            static member Z32Size n = 
              if n <= 0x7F then 1
              elif n <= 0x3FFF then 2
              else 4

            /// Emit int32 as compressed unsigned integer
            member buf.EmitZ32 n = 
                if n >= 0 &&  n <= 0x7F then 
                    buf.EmitIntAsByte n  
                elif n >= 0x80 && n <= 0x3FFF then 
                    buf.EmitIntAsByte (0x80 ||| (n >>> 8))
                    buf.EmitIntAsByte (n &&& 0xFF) 
                else 
                    buf.EmitIntAsByte (0xc0l ||| ((n >>> 24) &&& 0xFF))
                    buf.EmitIntAsByte (           (n >>> 16) &&& 0xFF)
                    buf.EmitIntAsByte (           (n >>> 8)  &&& 0xFF)
                    buf.EmitIntAsByte (            n         &&& 0xFF)

            static member Z32 n = let bb = ByteBuffer.Create (ByteBuffer.Z32Size n) in bb.EmitZ32 n; bb.Close()

            member buf.EmitPadding n = 
                for i = 0 to n-1 do
                    buf.EmitByte 0x0uy

            // Emit compressed untagged integer
            member buf.EmitZUntaggedIndex big idx = 
                if big then buf.EmitInt32 idx
                elif idx > 0xffff then failwith "EmitZUntaggedIndex: too big for small address or simple index"
                else buf.EmitInt32AsUInt16 idx

            // Emit compressed tagged integer
            member buf.EmitZTaggedIndex tag nbits big idx =
                let idx2 = (idx <<< nbits) ||| tag
                if big then buf.EmitInt32 idx2
                else buf.EmitInt32AsUInt16 idx2

        //---------------------------------------------------------------------
        // Byte, byte array fragments and other concrete representations
        // manipulations.
        //---------------------------------------------------------------------

        let bitsOfSingle (x:float32) = System.BitConverter.ToInt32(System.BitConverter.GetBytes(x), 0)
        let bitsOfDouble (x:float) = System.BitConverter.DoubleToInt64Bits(x)

    type ByteFile(bytes:byte[]) =

        member __.ReadByte addr = bytes.[addr]

        member __.ReadBytes addr len = Array.sub bytes addr len

        member __.CountUtf8String addr =
            let mutable p = addr
            while bytes.[p] <> 0uy do
                p <- p + 1
            p - addr

        member m.ReadUTF8String addr =
            let n = m.CountUtf8String addr
            Encoding.UTF8.GetString (bytes, addr, n)

        member is.ReadInt32 addr =
            let b0 = is.ReadByte addr
            let b1 = is.ReadByte (addr+1)
            let b2 = is.ReadByte (addr+2)
            let b3 = is.ReadByte (addr+3)
            int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)

        member is.ReadUInt16 addr =
            let b0 = is.ReadByte addr
            let b1 = is.ReadByte (addr+1)
            uint16 b0 ||| (uint16 b1 <<< 8)

    [<AutoOpen>]
    module Reader =
        let seekReadByte (is:ByteFile) addr = is.ReadByte addr
        let seekReadBytes (is:ByteFile) addr len = is.ReadBytes addr len
        let seekReadInt32 (is:ByteFile) addr = is.ReadInt32 addr
        let seekReadUInt16 (is:ByteFile) addr = is.ReadUInt16 addr

        let seekReadByteAsInt32 is addr = int32 (seekReadByte is addr)

        let seekReadInt64 is addr =
            let b0 = seekReadByte is addr
            let b1 = seekReadByte is (addr+1)
            let b2 = seekReadByte is (addr+2)
            let b3 = seekReadByte is (addr+3)
            let b4 = seekReadByte is (addr+4)
            let b5 = seekReadByte is (addr+5)
            let b6 = seekReadByte is (addr+6)
            let b7 = seekReadByte is (addr+7)
            int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) |||
            (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56)

        let seekReadUInt16AsInt32 is addr = int32 (seekReadUInt16 is addr)

        let seekReadCompressedUInt32 is addr =
            let b0 = seekReadByte is addr
            if b0 <= 0x7Fuy then int b0, addr+1
            elif b0 <= 0xBFuy then
                let b0 = b0 &&& 0x7Fuy
                let b1 = seekReadByteAsInt32 is (addr+1)
                (int b0 <<< 8) ||| int b1, addr+2
            else
                let b0 = b0 &&& 0x3Fuy
                let b1 = seekReadByteAsInt32 is (addr+1)
                let b2 = seekReadByteAsInt32 is (addr+2)
                let b3 = seekReadByteAsInt32 is (addr+3)
                (int b0 <<< 24) ||| (int b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, addr+4

        let seekReadSByte         is addr = sbyte (seekReadByte is addr)

        let rec seekCountUtf8String is addr n =
            let c = seekReadByteAsInt32 is addr
            if c = 0 then n
            else seekCountUtf8String is (addr+1) (n+1)

        let seekReadUTF8String is addr =
            let n = seekCountUtf8String is addr 0
            let bytes = seekReadBytes is addr n
            Encoding.UTF8.GetString (bytes, 0, bytes.Length)

        let seekReadBlob is addr =
            let len, addr = seekReadCompressedUInt32 is addr
            seekReadBytes is addr len

        let seekReadUserString is addr =
            let len, addr = seekReadCompressedUInt32 is addr
            let bytes = seekReadBytes is addr (len - 1)
            Encoding.Unicode.GetString(bytes, 0, bytes.Length)

        let seekReadGuid is addr =  seekReadBytes is addr 0x10

        let seekReadUncodedToken is addr  =
            i32ToUncodedToken (seekReadInt32 is addr)

        let sigptrGetByte (bytes:byte[]) sigptr =
            bytes.[sigptr], sigptr + 1

        let sigptrGetBool bytes sigptr =
            let b0, sigptr = sigptrGetByte bytes sigptr
            (b0 = 0x01uy) , sigptr

        let sigptrGetSByte bytes sigptr =
            let i, sigptr = sigptrGetByte bytes sigptr
            sbyte i, sigptr

        let sigptrGetUInt16 bytes sigptr =
            let b0, sigptr = sigptrGetByte bytes sigptr
            let b1, sigptr = sigptrGetByte bytes sigptr
            uint16 (int b0 ||| (int b1 <<< 8)), sigptr

        let sigptrGetInt16 bytes sigptr =
            let u, sigptr = sigptrGetUInt16 bytes sigptr
            int16 u, sigptr

        let sigptrGetInt32 (bytes: byte[]) sigptr =
            let b0 = bytes.[sigptr]
            let b1 = bytes.[sigptr+1]
            let b2 = bytes.[sigptr+2]
            let b3 = bytes.[sigptr+3]
            let res = int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)
            res, sigptr + 4

        let sigptrGetUInt32 bytes sigptr =
            let u, sigptr = sigptrGetInt32 bytes sigptr
            uint32 u, sigptr

        let sigptrGetUInt64 bytes sigptr =
            let u0, sigptr = sigptrGetUInt32 bytes sigptr
            let u1, sigptr = sigptrGetUInt32 bytes sigptr
            (uint64 u0 ||| (uint64 u1 <<< 32)), sigptr

        let sigptrGetInt64 bytes sigptr =
            let u, sigptr = sigptrGetUInt64 bytes sigptr
            int64 u, sigptr

        let sigptrGetSingle bytes sigptr =
            let u, sigptr = sigptrGetInt32 bytes sigptr
            singleOfBits u, sigptr

        let sigptrGetDouble bytes sigptr =
            let u, sigptr = sigptrGetInt64 bytes sigptr
            doubleOfBits u, sigptr

        let sigptrGetZInt32 bytes sigptr =
            let b0, sigptr = sigptrGetByte bytes sigptr
            if b0 <= 0x7Fuy then int b0, sigptr
            elif b0 <= 0xBFuy then
                let b0 = b0 &&& 0x7Fuy
                let b1, sigptr = sigptrGetByte bytes sigptr
                (int b0 <<< 8) ||| int b1, sigptr
            else
                let b0 = b0 &&& 0x3Fuy
                let b1, sigptr = sigptrGetByte bytes sigptr
                let b2, sigptr = sigptrGetByte bytes sigptr
                let b3, sigptr = sigptrGetByte bytes sigptr
                (int b0 <<< 24) ||| (int  b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, sigptr

        let rec sigptrFoldAcc f n (bytes:byte[]) (sigptr:int) i acc =
            if i < n then
                let x, sp = f bytes sigptr
                sigptrFoldAcc f n bytes sp (i+1) (x::acc)
            else
                Array.ofList (List.rev acc), sigptr

        let sigptrFold f n (bytes:byte[]) (sigptr:int) =
            sigptrFoldAcc f n bytes sigptr 0 []

        let sigptrGetBytes n (bytes:byte[]) sigptr =
                let res = Array.zeroCreate n
                for i = 0 to (n - 1) do
                    res.[i] <- bytes.[sigptr + i]
                res, sigptr + n

        let sigptrGetString n bytes sigptr =
            let bytearray, sigptr = sigptrGetBytes n bytes sigptr
            (Encoding.UTF8.GetString(bytearray, 0, bytearray.Length)), sigptr

        let chunk sz next = ({addr=next; size=sz}, next + sz)
        let nochunk next = ({addr= 0x0;size= 0x0; } , next)


        let kindAssemblyRef = ILRowKind [ UShort; UShort; UShort; UShort; ULong; Blob; SString; SString; Blob; ]
        let kindModuleRef = ILRowKind [ SString ]
        let kindFileRef = ILRowKind [ ULong; SString; Blob ]
        let kindTypeRef = ILRowKind [ ResolutionScope; SString; SString ]
        let kindTypeSpec = ILRowKind [ Blob ]
        let kindTypeDef = ILRowKind [ ULong; SString; SString; TypeDefOrRefOrSpec; SimpleIndex ILTableNames.Field; SimpleIndex ILTableNames.Method ]
        let kindPropertyMap = ILRowKind [ SimpleIndex ILTableNames.TypeDef; SimpleIndex ILTableNames.Property ]
        let kindEventMap = ILRowKind [ SimpleIndex ILTableNames.TypeDef; SimpleIndex ILTableNames.Event ]
        let kindInterfaceImpl = ILRowKind [ SimpleIndex ILTableNames.TypeDef; TypeDefOrRefOrSpec ]
        let kindNested = ILRowKind [ SimpleIndex ILTableNames.TypeDef; SimpleIndex ILTableNames.TypeDef ]
        let kindCustomAttribute = ILRowKind [ HasCustomAttribute; CustomAttributeType; Blob ]
        let kindDeclSecurity = ILRowKind [ UShort; HasDeclSecurity; Blob ]
        let kindMemberRef = ILRowKind [ MemberRefParent; SString; Blob ]
        let kindStandAloneSig = ILRowKind [ Blob ]
        let kindFieldDef = ILRowKind [ UShort; SString; Blob ]
        let kindFieldRVA = ILRowKind [ Data; SimpleIndex ILTableNames.Field ]
        let kindFieldMarshal = ILRowKind [ HasFieldMarshal; Blob ]
        let kindConstant = ILRowKind [ UShort;HasConstant; Blob ]
        let kindFieldLayout = ILRowKind [ ULong; SimpleIndex ILTableNames.Field ]
        let kindParam = ILRowKind [ UShort; UShort; SString ]
        let kindMethodDef = ILRowKind [ ULong;  UShort; UShort; SString; Blob; SimpleIndex ILTableNames.Param ]
        let kindMethodImpl = ILRowKind [ SimpleIndex ILTableNames.TypeDef; MethodDefOrRef; MethodDefOrRef ]
        let kindImplMap = ILRowKind [ UShort; MemberForwarded; SString; SimpleIndex ILTableNames.ModuleRef ]
        let kindMethodSemantics = ILRowKind [ UShort; SimpleIndex ILTableNames.Method; HasSemantics ]
        let kindProperty = ILRowKind [ UShort; SString; Blob ]
        let kindEvent = ILRowKind [ UShort; SString; TypeDefOrRefOrSpec ]
        let kindManifestResource = ILRowKind [ ULong; ULong; SString; Implementation ]
        let kindClassLayout = ILRowKind [ UShort; ULong; SimpleIndex ILTableNames.TypeDef ]
        let kindExportedType = ILRowKind [ ULong; ULong; SString; SString; Implementation ]
        let kindAssembly = ILRowKind [ ULong; UShort; UShort; UShort; UShort; ULong; Blob; SString; SString ]
        let kindGenericParam_v1_1 = ILRowKind [ UShort; UShort; TypeOrMethodDef; SString; TypeDefOrRefOrSpec ]
        let kindGenericParam_v2_0 = ILRowKind [ UShort; UShort; TypeOrMethodDef; SString ]
        let kindMethodSpec = ILRowKind [ MethodDefOrRef; Blob ]
        let kindGenericParamConstraint = ILRowKind [ SimpleIndex ILTableNames.GenericParam; TypeDefOrRefOrSpec ]
        let kindModule = ILRowKind [ UShort; SString; GGuid; GGuid; GGuid ]
        let kindIllegal = ILRowKind [ ]

        let hcCompare (TaggedIndex((t1: HasConstantTag), (idx1:int))) (TaggedIndex((t2: HasConstantTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let hsCompare (TaggedIndex((t1:HasSemanticsTag), (idx1:int))) (TaggedIndex((t2:HasSemanticsTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let hcaCompare (TaggedIndex((t1:HasCustomAttributeTag), (idx1:int))) (TaggedIndex((t2:HasCustomAttributeTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let mfCompare (TaggedIndex((t1:MemberForwardedTag), (idx1:int))) (TaggedIndex((t2:MemberForwardedTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let hdsCompare (TaggedIndex((t1:HasDeclSecurityTag), (idx1:int))) (TaggedIndex((t2:HasDeclSecurityTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let hfmCompare (TaggedIndex((t1:HasFieldMarshalTag), idx1)) (TaggedIndex((t2:HasFieldMarshalTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let tomdCompare (TaggedIndex((t1:TypeOrMethodDefTag), idx1)) (TaggedIndex((t2:TypeOrMethodDefTag), idx2)) =
            if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

        let simpleIndexCompare (idx1:int) (idx2:int) =
            compare idx1 idx2

        let mkCacheInt32 lowMem _infile _nm _sz  =
            if lowMem then (fun f x -> f x) else
            let cache = ref null
            fun f (idx:int32) ->
                let cache =
                    match !cache with
                    | null -> cache :=  new Dictionary<int32, _>(11)
                    | _ -> ()
                    !cache
                let mutable res = Unchecked.defaultof<_>
                let ok = cache.TryGetValue(idx, &res)
                if ok then
                    res
                else
                    let res = f idx
                    cache.[idx] <- res;
                    res

        let mkCacheGeneric lowMem _inbase _nm _sz  =
            if lowMem then (fun f x -> f x) else
            let cache = ref null
            fun f (idx :'T) ->
                let cache =
                    match !cache with
                    | null -> cache := new Dictionary<_, _>(11 (* sz:int *) )
                    | _ -> ()
                    !cache
                if cache.ContainsKey idx then cache.[idx]
                else let res = f idx in cache.[idx] <- res; res

        let seekFindRow numRows rowChooser =
            let mutable i = 1
            while (i <= numRows &&  not (rowChooser i)) do
                i <- i + 1;
            i

        // search for rows satisfying predicate
        let seekReadIndexedRows (numRows, rowReader, keyFunc, keyComparer, binaryChop, rowConverter) =
            if binaryChop then
                let mutable low = 0
                let mutable high = numRows + 1
                begin
                  let mutable fin = false
                  while not fin do
                      if high - low <= 1  then
                          fin <- true
                      else
                          let mid = (low + high) / 2
                          let midrow = rowReader mid
                          let c = keyComparer (keyFunc midrow)
                          if c > 0 then
                              low <- mid
                          elif c < 0 then
                              high <- mid
                          else
                              fin <- true
                end;
                let mutable res = []
                if high - low > 1 then
                    // now read off rows, forward and backwards
                    let mid = (low + high) / 2
                    // read forward
                    begin
                        let mutable fin = false
                        let mutable curr = mid
                        while not fin do
                          if curr > numRows then
                              fin <- true;
                          else
                              let currrow = rowReader curr
                              if keyComparer (keyFunc currrow) = 0 then
                                  res <- rowConverter currrow :: res;
                              else
                                  fin <- true;
                              curr <- curr + 1;
                        done;
                    end;
                    res <- List.rev res;
                    // read backwards
                    begin
                        let mutable fin = false
                        let mutable curr = mid - 1
                        while not fin do
                          if curr = 0 then
                            fin <- true
                          else
                            let currrow = rowReader curr
                            if keyComparer (keyFunc currrow) = 0 then
                                res <- rowConverter currrow :: res;
                            else
                                fin <- true;
                            curr <- curr - 1;
                    end;
                res |> List.toArray
            else
                let res = ref []
                for i = 1 to numRows do
                    let rowinfo = rowReader i
                    if keyComparer (keyFunc rowinfo) = 0 then
                      res := rowConverter rowinfo :: !res;
                List.rev !res  |> List.toArray


        let seekReadOptionalIndexedRow (info) =
            match seekReadIndexedRows info with
            | [| |] -> None
            | xs -> Some xs.[0]

        let seekReadIndexedRow (info) =
            match seekReadOptionalIndexedRow info with
            | Some row -> row
            | None -> failwith ("no row found for key when indexing table")

        let getName (ltd: Lazy<ILTypeDef>) =
            let td = ltd.Force()
            (td.Name, ltd)

        let emptyILEvents = { new ILEventDefs with member __.Entries = [| |] }
        let emptyILProperties = { new ILPropertyDefs with member __.Entries = [| |] }
        let emptyILTypeDefs = ILTypeDefs (lazy [| |])
        let emptyILCustomAttrs =  { new ILCustomAttrs with member __.Entries = [| |] }
        let mkILCustomAttrs x = { new ILCustomAttrs with member __.Entries = x }
        let emptyILMethodImpls = { new ILMethodImplDefs with member __.Entries = [| |] }
        let emptyILMethods = ILMethodDefs (lazy [| |])
        let emptyILFields = { new ILFieldDefs with member __.Entries = [| |] }

        let mkILTy boxed tspec =
            match boxed with
            | AsObject -> ILType.Boxed tspec
            | _ -> ILType.Value tspec

        let mkILArr1DTy ty = ILType.Array (ILArrayShape.SingleDimensional, ty)

        let typeNameForGlobalFunctions = "<Module>"

        let mkILNonGenericTySpec tref =  ILTypeSpec (tref, [| |])
        let mkILTypeForGlobalFunctions scoref = ILType.Boxed (mkILNonGenericTySpec (ILTypeRef(ILTypeRefScope.Top scoref, UNone, typeNameForGlobalFunctions)))
        let mkILArrTy (ty, shape) = ILType.Array(shape, ty)

        let mkILMethSpecInTyRaw (typ:ILType, cc, nm, args, rty, minst:ILGenericArgs) =
            ILMethodSpec (ILMethodRef (typ.TypeRef, cc, minst.Length, nm, args, rty), typ, minst)

        let mkILFieldSpecInTy (typ:ILType, nm, fty) =
            ILFieldSpec (ILFieldRef (typ.TypeRef, nm, fty), typ)

        let mkILGlobals systemRuntimeScopeRef =
              let mkILTyspec nsp nm =  mkILNonGenericTySpec(ILTypeRef(ILTypeRefScope.Top(systemRuntimeScopeRef), USome nsp, nm))
              { typ_Object = ILType.Boxed (mkILTyspec "System" "Object")
                typ_String = ILType.Boxed (mkILTyspec "System" "String")
                typ_Void = ILType.Value (mkILTyspec "System" "Void")
                typ_Type = ILType.Boxed (mkILTyspec "System" "Type")
                typ_Int64 = ILType.Value (mkILTyspec "System" "Int64")
                typ_UInt64 = ILType.Value (mkILTyspec "System" "UInt64")
                typ_Int32 = ILType.Value (mkILTyspec "System" "Int32")
                typ_Array = ILType.Boxed (mkILTyspec "System" "Array")
                typ_UInt32 = ILType.Value (mkILTyspec "System" "UInt32")
                typ_Int16 = ILType.Value (mkILTyspec "System" "Int16")
                typ_UInt16 = ILType.Value (mkILTyspec "System" "UInt16")
                typ_SByte = ILType.Value (mkILTyspec "System" "SByte")
                typ_Byte = ILType.Value (mkILTyspec "System" "Byte")
                typ_Single = ILType.Value (mkILTyspec "System" "Single")
                typ_Double = ILType.Value (mkILTyspec "System" "Double")
                typ_Boolean = ILType.Value (mkILTyspec "System" "Boolean")
                typ_Char = ILType.Value (mkILTyspec "System" "Char")
                typ_IntPtr = ILType.Value (mkILTyspec "System" "IntPtr")
                typ_TypedReference = Some (ILType.Value (mkILTyspec "System" "TypedReference"))
                typ_UIntPtr = ILType.Value (mkILTyspec "System" "UIntPtr")
                systemRuntimeScopeRef = systemRuntimeScopeRef }

        type PEReader(fileName: string, is: ByteFile) =

            //-----------------------------------------------------------------------
            // Crack the binary headers, build a reader context and return the lazy
            // read of the AbsIL module.
            // ----------------------------------------------------------------------

            (* MSDOS HEADER *)
            let peSignaturePhysLoc = seekReadInt32 is 0x3c

            (* PE HEADER *)
            let peFileHeaderPhysLoc = peSignaturePhysLoc + 0x04
            let peOptionalHeaderPhysLoc = peFileHeaderPhysLoc + 0x14
            let peSignature = seekReadInt32 is (peSignaturePhysLoc + 0)
            do if peSignature <>  0x4550 then failwithf "not a PE file - bad magic PE number 0x%08x, is = %A" peSignature is;


            (* PE SIGNATURE *)
            let machine = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 0)
            let numSections = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 2)
            let optHeaderSize = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 16)
            do if optHeaderSize <>  0xe0 &&
                 optHeaderSize <> 0xf0 then failwith "not a PE file - bad optional header size";
            let x64adjust = optHeaderSize - 0xe0
            let only64 = (optHeaderSize = 0xf0)    (* May want to read in the optional header Magic number and check that as well... *)
            let platform = match machine with | 0x8664 -> Some(AMD64) | 0x200 -> Some(IA64) | _ -> Some(X86)
            let sectionHeadersStartPhysLoc = peOptionalHeaderPhysLoc + optHeaderSize

            let flags = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 18)
            let isDll = (flags &&& 0x2000) <> 0x0

            (* OPTIONAL PE HEADER *)
            (* x86: 000000a0 *)
            (* x86: 000000b0 *)
            let dataSegmentAddr = seekReadInt32 is (peOptionalHeaderPhysLoc + 24) (* e.g. 0x0000c000 *)
            let imageBaseReal = if only64 then dataSegmentAddr else seekReadInt32 is (peOptionalHeaderPhysLoc + 28)  (* Image Base Always 0x400000 (see Section 23.1). - QUERY: no it's not always 0x400000, e.g. 0x034f0000 *)
            let alignVirt = seekReadInt32 is (peOptionalHeaderPhysLoc + 32)   (*  Section Alignment Always 0x2000 (see Section 23.1). *)
            let alignPhys = seekReadInt32 is (peOptionalHeaderPhysLoc + 36)  (* File Alignment Either 0x200 or 0x1000. *)
            (* x86: 000000c0 *)
            let subsysMajor = seekReadUInt16AsInt32 is (peOptionalHeaderPhysLoc + 48)   (* SubSys Major Always 4 (see Section 23.1). *)
            let subsysMinor = seekReadUInt16AsInt32 is (peOptionalHeaderPhysLoc + 50)   (* SubSys Minor Always 0 (see Section 23.1). *)
            (* x86: 000000d0 *)
            let subsys = seekReadUInt16 is (peOptionalHeaderPhysLoc + 68)   (* SubSystem Subsystem required to run this image. Shall be either IMAGE_SUBSYSTEM_WINDOWS_CE_GUI (!0x3) or IMAGE_SUBSYSTEM_WINDOWS_GUI (!0x2). QUERY: Why is this 3 on the images ILASM produces??? *)
            let useHighEntropyVA =
                let n = seekReadUInt16 is (peOptionalHeaderPhysLoc + 70)
                let highEnthropyVA = 0x20us
                (n &&& highEnthropyVA) = highEnthropyVA

             (* x86: 000000e0 *)
             (* x86: 000000f0, x64: 00000100 *)
             (* x86: 00000100 - these addresses are for x86 - for the x64 location, add x64adjust (0x10) *)
             (* x86: 00000110 *)
             (* x86: 00000120 *)
             (* x86: 00000130 *)
             (* x86: 00000140 *)
             (* x86: 00000150 *)
             (* x86: 00000160 *)
            let cliHeaderAddr = seekReadInt32 is (peOptionalHeaderPhysLoc + 208 + x64adjust)

            let anyV2P (n, v) =
              let rec look i pos =
                if i >= numSections then (failwith (fileName + ": bad "+n+", rva "+string v); 0x0)
                else
                  let virtSize = seekReadInt32 is (pos + 8)
                  let virtAddr = seekReadInt32 is (pos + 12)
                  let physLoc = seekReadInt32 is (pos + 20)
                  if (v >= virtAddr && (v < virtAddr + virtSize)) then (v - virtAddr) + physLoc
                  else look (i+1) (pos + 0x28)
              look 0 sectionHeadersStartPhysLoc

            let cliHeaderPhysLoc = anyV2P ("cli header", cliHeaderAddr)

            let metadataAddr = seekReadInt32 is (cliHeaderPhysLoc + 8)
            let metadataSize = seekReadInt32 is (cliHeaderPhysLoc + 12)
            let cliFlags = seekReadInt32 is (cliHeaderPhysLoc + 16)
            let ilOnly = (cliFlags &&& 0x01) <> 0x00
            let only32 = (cliFlags &&& 0x02) <> 0x00
            let is32bitpreferred = (cliFlags &&& 0x00020003) <> 0x00

            let entryPointToken = seekReadUncodedToken is (cliHeaderPhysLoc + 20)
            let resourcesAddr = seekReadInt32 is (cliHeaderPhysLoc + 24)

            let metadataPhysLoc = anyV2P ("metadata", metadataAddr)
            let resourcePhysLoc offset = anyV2P ("resource", offset + resourcesAddr)

            member __.MetadataPhysLoc = metadataPhysLoc
            member __.MetadataSize = metadataSize
            member __.ResourcePhysLoc offset = resourcePhysLoc offset
            
        type ILModuleReader(fileName: string, is: ByteFile, ilg: ILGlobals, lowMem: bool) =

            let metadataPhysLoc = 0
            let magic = seekReadUInt16AsInt32 is metadataPhysLoc
            do if magic <> 0x5342 then failwith (fileName + ": bad metadata magic number: " + string magic);
            let magic2 = seekReadUInt16AsInt32 is (metadataPhysLoc + 2)
            do if magic2 <> 0x424a then failwith "bad metadata magic number";

            let versionLength = seekReadInt32 is (metadataPhysLoc + 12)
            let ilMetadataVersion = seekReadBytes is (metadataPhysLoc + 16) versionLength |> Array.filter (fun b -> b <> 0uy)
            let x = align 0x04 (16 + versionLength)
            let numStreams = seekReadUInt16AsInt32 is (metadataPhysLoc + x + 2)
            let streamHeadersStart = (metadataPhysLoc + x + 4)

            (* Crack stream headers *)

            let tryFindStream name =
              let rec look i pos =
                if i >= numStreams then None
                else
                  let offset = seekReadInt32 is (pos + 0)
                  let length = seekReadInt32 is (pos + 4)
                  let res = ref true
                  let fin = ref false
                  let n = ref 0
                  // read and compare the stream name byte by byte
                  while (not !fin) do
                      let c= seekReadByteAsInt32 is (pos + 8 + (!n))
                      if c = 0 then
                          fin := true
                      elif !n >= Array.length name || c <> name.[!n] then
                          res := false;
                      incr n
                  if !res then Some(offset + metadataPhysLoc, length)
                  else look (i+1) (align 0x04 (pos + 8 + (!n)))
              look 0 streamHeadersStart

            let findStream name =
                match tryFindStream name with
                | None -> (0x0, 0x0)
                | Some positions ->  positions

            let (tablesStreamPhysLoc, _tablesStreamSize) =
              match tryFindStream [| 0x23; 0x7e |] (* #~ *) with
              | Some res -> res
              | None ->
                match tryFindStream [| 0x23; 0x2d |] (* #-: at least one DLL I've seen uses this! *)   with
                | Some res -> res
                | None ->
                 let firstStreamOffset = seekReadInt32 is (streamHeadersStart + 0)
                 let firstStreamLength = seekReadInt32 is (streamHeadersStart + 4)
                 firstStreamOffset, firstStreamLength

            let (stringsStreamPhysicalLoc, stringsStreamSize) = findStream [| 0x23; 0x53; 0x74; 0x72; 0x69; 0x6e; 0x67; 0x73; |] (* #Strings *)
            let (blobsStreamPhysicalLoc, blobsStreamSize) = findStream [| 0x23; 0x42; 0x6c; 0x6f; 0x62; |] (* #Blob *)

            let tablesStreamMajorVersion = seekReadByteAsInt32 is (tablesStreamPhysLoc + 4)
            let tablesStreamMinorVersion = seekReadByteAsInt32 is (tablesStreamPhysLoc + 5)

            let usingWhidbeyBeta1TableSchemeForGenericParam = (tablesStreamMajorVersion = 1) && (tablesStreamMinorVersion = 1)

            let tableKinds =
                [|kindModule               (* Table 0  *);
                  kindTypeRef              (* Table 1  *);
                  kindTypeDef              (* Table 2  *);
                  kindIllegal (* kindFieldPtr *)             (* Table 3  *);
                  kindFieldDef                (* Table 4  *);
                  kindIllegal (* kindMethodPtr *)            (* Table 5  *);
                  kindMethodDef               (* Table 6  *);
                  kindIllegal (* kindParamPtr *)             (* Table 7  *);
                  kindParam                (* Table 8  *);
                  kindInterfaceImpl        (* Table 9  *);
                  kindMemberRef            (* Table 10 *);
                  kindConstant             (* Table 11 *);
                  kindCustomAttribute      (* Table 12 *);
                  kindFieldMarshal         (* Table 13 *);
                  kindDeclSecurity         (* Table 14 *);
                  kindClassLayout          (* Table 15 *);
                  kindFieldLayout          (* Table 16 *);
                  kindStandAloneSig        (* Table 17 *);
                  kindEventMap             (* Table 18 *);
                  kindIllegal (* kindEventPtr *)             (* Table 19 *);
                  kindEvent                (* Table 20 *);
                  kindPropertyMap          (* Table 21 *);
                  kindIllegal (* kindPropertyPtr *)          (* Table 22 *);
                  kindProperty             (* Table 23 *);
                  kindMethodSemantics      (* Table 24 *);
                  kindMethodImpl           (* Table 25 *);
                  kindModuleRef            (* Table 26 *);
                  kindTypeSpec             (* Table 27 *);
                  kindImplMap              (* Table 28 *);
                  kindFieldRVA             (* Table 29 *);
                  kindIllegal (* kindENCLog *)               (* Table 30 *);
                  kindIllegal (* kindENCMap *)               (* Table 31 *);
                  kindAssembly             (* Table 32 *);
                  kindIllegal (* kindAssemblyProcessor *)    (* Table 33 *);
                  kindIllegal (* kindAssemblyOS *)           (* Table 34 *);
                  kindAssemblyRef          (* Table 35 *);
                  kindIllegal (* kindAssemblyRefProcessor *) (* Table 36 *);
                  kindIllegal (* kindAssemblyRefOS *)        (* Table 37 *);
                  kindFileRef                 (* Table 38 *);
                  kindExportedType         (* Table 39 *);
                  kindManifestResource     (* Table 40 *);
                  kindNested               (* Table 41 *);
                 (if usingWhidbeyBeta1TableSchemeForGenericParam then kindGenericParam_v1_1 else  kindGenericParam_v2_0);        (* Table 42 *)
                  kindMethodSpec         (* Table 43 *);
                  kindGenericParamConstraint         (* Table 44 *);
                  kindIllegal         (* Table 45 *);
                  kindIllegal         (* Table 46 *);
                  kindIllegal         (* Table 47 *);
                  kindIllegal         (* Table 48 *);
                  kindIllegal         (* Table 49 *);
                  kindIllegal         (* Table 50 *);
                  kindIllegal         (* Table 51 *);
                  kindIllegal         (* Table 52 *);
                  kindIllegal         (* Table 53 *);
                  kindIllegal         (* Table 54 *);
                  kindIllegal         (* Table 55 *);
                  kindIllegal         (* Table 56 *);
                  kindIllegal         (* Table 57 *);
                  kindIllegal         (* Table 58 *);
                  kindIllegal         (* Table 59 *);
                  kindIllegal         (* Table 60 *);
                  kindIllegal         (* Table 61 *);
                  kindIllegal         (* Table 62 *);
                  kindIllegal         (* Table 63 *);
                |]

            let heapSizes = seekReadByteAsInt32 is (tablesStreamPhysLoc + 6)
            let valid = seekReadInt64 is (tablesStreamPhysLoc + 8)
            let sorted = seekReadInt64 is (tablesStreamPhysLoc + 16)
            let tableRowCount, startOfTables =
                let numRows = Array.create 64 0
                let prevNumRowIdx = ref (tablesStreamPhysLoc + 24)
                for i = 0 to 63 do
                    if (valid &&& (int64 1 <<< i)) <> int64  0 then
                        numRows.[i] <-  (seekReadInt32 is !prevNumRowIdx);
                        prevNumRowIdx := !prevNumRowIdx + 4
                numRows, !prevNumRowIdx

            let getNumRows (tab:ILTableName) = tableRowCount.[tab.Index]
            let stringsBigness = (heapSizes &&& 1) <> 0
            let guidsBigness = (heapSizes &&& 2) <> 0
            let blobsBigness = (heapSizes &&& 4) <> 0

            let tableBigness = Array.map (fun n -> n >= 0x10000) tableRowCount

            let codedBigness nbits tab =
              let rows = getNumRows tab
              rows >= (0x10000 >>>& nbits)

            let tdorBigness =
              codedBigness 2 ILTableNames.TypeDef ||
              codedBigness 2 ILTableNames.TypeRef ||
              codedBigness 2 ILTableNames.TypeSpec

            let tomdBigness =
              codedBigness 1 ILTableNames.TypeDef ||
              codedBigness 1 ILTableNames.Method

            let hcBigness =
              codedBigness 2 ILTableNames.Field ||
              codedBigness 2 ILTableNames.Param ||
              codedBigness 2 ILTableNames.Property

            let hcaBigness =
              codedBigness 5 ILTableNames.Method ||
              codedBigness 5 ILTableNames.Field ||
              codedBigness 5 ILTableNames.TypeRef  ||
              codedBigness 5 ILTableNames.TypeDef ||
              codedBigness 5 ILTableNames.Param ||
              codedBigness 5 ILTableNames.InterfaceImpl ||
              codedBigness 5 ILTableNames.MemberRef ||
              codedBigness 5 ILTableNames.Module ||
              codedBigness 5 ILTableNames.Permission ||
              codedBigness 5 ILTableNames.Property ||
              codedBigness 5 ILTableNames.Event ||
              codedBigness 5 ILTableNames.StandAloneSig ||
              codedBigness 5 ILTableNames.ModuleRef ||
              codedBigness 5 ILTableNames.TypeSpec ||
              codedBigness 5 ILTableNames.Assembly ||
              codedBigness 5 ILTableNames.AssemblyRef ||
              codedBigness 5 ILTableNames.File ||
              codedBigness 5 ILTableNames.ExportedType ||
              codedBigness 5 ILTableNames.ManifestResource ||
              codedBigness 5 ILTableNames.GenericParam ||
              codedBigness 5 ILTableNames.GenericParamConstraint ||
              codedBigness 5 ILTableNames.MethodSpec


            let hfmBigness =
              codedBigness 1 ILTableNames.Field ||
              codedBigness 1 ILTableNames.Param

            let hdsBigness =
              codedBigness 2 ILTableNames.TypeDef ||
              codedBigness 2 ILTableNames.Method ||
              codedBigness 2 ILTableNames.Assembly

            let mrpBigness =
              codedBigness 3 ILTableNames.TypeRef ||
              codedBigness 3 ILTableNames.ModuleRef ||
              codedBigness 3 ILTableNames.Method ||
              codedBigness 3 ILTableNames.TypeSpec

            let hsBigness =
              codedBigness 1 ILTableNames.Event ||
              codedBigness 1 ILTableNames.Property

            let mdorBigness =
              codedBigness 1 ILTableNames.Method ||
              codedBigness 1 ILTableNames.MemberRef

            let mfBigness =
              codedBigness 1 ILTableNames.Field ||
              codedBigness 1 ILTableNames.Method

            let iBigness =
              codedBigness 2 ILTableNames.File ||
              codedBigness 2 ILTableNames.AssemblyRef ||
              codedBigness 2 ILTableNames.ExportedType

            let catBigness =
              codedBigness 3 ILTableNames.Method ||
              codedBigness 3 ILTableNames.MemberRef

            let rsBigness =
              codedBigness 2 ILTableNames.Module ||
              codedBigness 2 ILTableNames.ModuleRef ||
              codedBigness 2 ILTableNames.AssemblyRef  ||
              codedBigness 2 ILTableNames.TypeRef

            let rowKindSize (ILRowKind kinds) =
              kinds |> List.sumBy (fun x ->
                    match x with
                    | UShort -> 2
                    | ULong -> 4
                    | Byte -> 1
                    | Data -> 4
                    | GGuid -> (if guidsBigness then 4 else 2)
                    | Blob  -> (if blobsBigness then 4 else 2)
                    | SString  -> (if stringsBigness then 4 else 2)
                    | SimpleIndex tab -> (if tableBigness.[tab.Index] then 4 else 2)
                    | TypeDefOrRefOrSpec -> (if tdorBigness then 4 else 2)
                    | TypeOrMethodDef -> (if tomdBigness then 4 else 2)
                    | HasConstant  -> (if hcBigness then 4 else 2)
                    | HasCustomAttribute -> (if hcaBigness then 4 else 2)
                    | HasFieldMarshal  -> (if hfmBigness then 4 else 2)
                    | HasDeclSecurity  -> (if hdsBigness then 4 else 2)
                    | MemberRefParent  -> (if mrpBigness then 4 else 2)
                    | HasSemantics  -> (if hsBigness then 4 else 2)
                    | MethodDefOrRef -> (if mdorBigness then 4 else 2)
                    | MemberForwarded -> (if mfBigness then 4 else 2)
                    | Implementation  -> (if iBigness then 4 else 2)
                    | CustomAttributeType -> (if catBigness then 4 else 2)
                    | ResolutionScope -> (if rsBigness then 4 else 2))

            let tableRowSizes = tableKinds |> Array.map rowKindSize

            let tablePhysLocations =
                 let res = Array.create 64 0x0
                 let prevTablePhysLoc = ref startOfTables
                 for i = 0 to 63 do
                     res.[i] <- !prevTablePhysLoc;
                     prevTablePhysLoc := !prevTablePhysLoc + (tableRowCount.[i] * tableRowSizes.[i]);
                 res

            // All the caches.  The sizes are guesstimates for the rough sharing-density of the assembly
            let cacheAssemblyRef = mkCacheInt32 lowMem fileName "ILAssemblyRef"  (getNumRows ILTableNames.AssemblyRef)
            let cacheMemberRefAsMemberData = mkCacheGeneric lowMem fileName "MemberRefAsMemberData" (getNumRows ILTableNames.MemberRef / 20 + 1)
            let cacheTypeRef = mkCacheInt32 lowMem fileName "ILTypeRef" (getNumRows ILTableNames.TypeRef / 20 + 1)
            let cacheTypeRefAsType = mkCacheGeneric lowMem fileName "TypeRefAsType" (getNumRows ILTableNames.TypeRef / 20 + 1)
            let cacheBlobHeapAsPropertySig = mkCacheGeneric lowMem fileName "BlobHeapAsPropertySig" (getNumRows ILTableNames.Property / 20 + 1)
            let cacheBlobHeapAsFieldSig = mkCacheGeneric lowMem fileName "BlobHeapAsFieldSig" (getNumRows ILTableNames.Field / 20 + 1)
            let cacheBlobHeapAsMethodSig = mkCacheGeneric lowMem fileName "BlobHeapAsMethodSig" (getNumRows ILTableNames.Method / 20 + 1)
            let cacheTypeDefAsType = mkCacheGeneric lowMem fileName "TypeDefAsType" (getNumRows ILTableNames.TypeDef / 20 + 1)
            let cacheMethodDefAsMethodData = mkCacheInt32 lowMem fileName "MethodDefAsMethodData" (getNumRows ILTableNames.Method / 20 + 1)
            // nb. Lots and lots of cache hits on this cache, hence never optimize cache away
            let cacheStringHeap = mkCacheInt32 false fileName "string heap" ( stringsStreamSize / 50 + 1)
            let cacheBlobHeap = mkCacheInt32 lowMem fileName "blob heap" ( blobsStreamSize / 50 + 1)

           //-----------------------------------------------------------------------

            let rowAddr (tab:ILTableName) idx = tablePhysLocations.[tab.Index] + (idx - 1) * tableRowSizes.[tab.Index]

            let seekReadUInt16Adv (addr: byref<int>) =
                let res = seekReadUInt16 is addr
                addr <- addr + 2
                res

            let seekReadInt32Adv (addr: byref<int>) =
                let res = seekReadInt32 is addr
                addr <- addr+4
                res

            let seekReadUInt16AsInt32Adv (addr: byref<int>) =
                let res = seekReadUInt16AsInt32 is addr
                addr <- addr+2
                res

            let seekReadTaggedIdx f nbits big (addr: byref<int>) =
                let tok = if big then seekReadInt32Adv &addr else seekReadUInt16AsInt32Adv &addr
                tokToTaggedIdx f nbits tok


            let seekReadIdx big (addr: byref<int>) =
                if big then seekReadInt32Adv &addr else seekReadUInt16AsInt32Adv &addr

            let seekReadUntaggedIdx (tab:ILTableName) (addr: byref<int>) =
                seekReadIdx tableBigness.[tab.Index] &addr


            let seekReadResolutionScopeIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> ResolutionScopeTag idx)    2 rsBigness   &addr
            let seekReadTypeDefOrRefOrSpecIdx  (addr: byref<int>) = seekReadTaggedIdx (fun idx -> TypeDefOrRefOrSpecTag idx)  2 tdorBigness &addr
            let seekReadTypeOrMethodDefIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> TypeOrMethodDefTag idx)    1 tomdBigness &addr
            let seekReadHasConstantIdx         (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasConstantTag idx)        2 hcBigness   &addr
            let seekReadHasCustomAttributeIdx  (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasCustomAttributeTag idx)  5 hcaBigness  &addr
            //let seekReadHasFieldMarshalIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasFieldMarshalTag idx)    1 hfmBigness &addr
            //let seekReadHasDeclSecurityIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasDeclSecurityTag idx)    2 hdsBigness &addr
            let seekReadMemberRefParentIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> MemberRefParentTag idx)    3 mrpBigness &addr
            let seekReadHasSemanticsIdx        (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasSemanticsTag idx)       1 hsBigness &addr
            let seekReadMethodDefOrRefIdx      (addr: byref<int>) = seekReadTaggedIdx (fun idx -> MethodDefOrRefTag idx)      1 mdorBigness &addr
            let seekReadImplementationIdx      (addr: byref<int>) = seekReadTaggedIdx (fun idx -> ImplementationTag idx)     2 iBigness &addr
            let seekReadCustomAttributeTypeIdx (addr: byref<int>) = seekReadTaggedIdx (fun idx -> CustomAttributeTypeTag idx) 3 catBigness &addr
            let seekReadStringIdx (addr: byref<int>) = seekReadIdx stringsBigness &addr
            let seekReadGuidIdx (addr: byref<int>) = seekReadIdx guidsBigness &addr
            let seekReadBlobIdx (addr: byref<int>) = seekReadIdx blobsBigness &addr

            let seekReadModuleRow idx =
                if idx = 0 then failwith "cannot read Module table row 0";
                let mutable addr = rowAddr ILTableNames.Module idx
                let generation = seekReadUInt16Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let mvidIdx = seekReadGuidIdx &addr
                let encidIdx = seekReadGuidIdx &addr
                let encbaseidIdx = seekReadGuidIdx &addr
                (generation, nameIdx, mvidIdx, encidIdx, encbaseidIdx)

            /// Read Table ILTypeRef
            let seekReadTypeRefRow idx =
                let mutable addr = rowAddr ILTableNames.TypeRef idx
                let scopeIdx = seekReadResolutionScopeIdx &addr
                let nameIdx = seekReadStringIdx &addr
                let namespaceIdx = seekReadStringIdx &addr
                (scopeIdx, nameIdx, namespaceIdx)

            /// Read Table ILTypeDef
            let seekReadTypeDefRow idx =
                let mutable addr = rowAddr ILTableNames.TypeDef idx
                let flags = seekReadInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let namespaceIdx = seekReadStringIdx &addr
                let extendsIdx = seekReadTypeDefOrRefOrSpecIdx &addr
                let fieldsIdx = seekReadUntaggedIdx ILTableNames.Field &addr
                let methodsIdx = seekReadUntaggedIdx ILTableNames.Method &addr
                (flags, nameIdx, namespaceIdx, extendsIdx, fieldsIdx, methodsIdx)

            /// Read Table Field
            let seekReadFieldRow idx =
                let mutable addr = rowAddr ILTableNames.Field idx
                let flags = seekReadUInt16AsInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let typeIdx = seekReadBlobIdx &addr
                (flags, nameIdx, typeIdx)

            /// Read Table Method
            let seekReadMethodRow idx =
                let mutable addr = rowAddr ILTableNames.Method idx
                let codeRVA = seekReadInt32Adv &addr
                let implflags = seekReadUInt16AsInt32Adv &addr
                let flags = seekReadUInt16AsInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let typeIdx = seekReadBlobIdx &addr
                let paramIdx = seekReadUntaggedIdx ILTableNames.Param &addr
                (codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx)

            /// Read Table Param
            let seekReadParamRow idx =
                let mutable addr = rowAddr ILTableNames.Param idx
                let flags = seekReadUInt16AsInt32Adv &addr
                let seq =  seekReadUInt16AsInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                (flags, seq, nameIdx)

            let seekReadInterfaceImplRow idx =
                let mutable addr = rowAddr ILTableNames.InterfaceImpl idx
                let tidx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                let intfIdx = seekReadTypeDefOrRefOrSpecIdx &addr
                (tidx, intfIdx)

            /// Read Table MemberRef
            let seekReadMemberRefRow idx =
                let mutable addr = rowAddr ILTableNames.MemberRef idx
                let mrpIdx = seekReadMemberRefParentIdx &addr
                let nameIdx = seekReadStringIdx &addr
                let typeIdx = seekReadBlobIdx &addr
                (mrpIdx, nameIdx, typeIdx)

            /// Read Table Constant
            let seekReadConstantRow idx =
                let mutable addr = rowAddr ILTableNames.Constant idx
                let kind = seekReadUInt16Adv &addr
                let parentIdx = seekReadHasConstantIdx &addr
                let valIdx = seekReadBlobIdx &addr
                (kind, parentIdx, valIdx)

            /// Read Table CustomAttribute
            let seekReadCustomAttributeRow idx =
                let mutable addr = rowAddr ILTableNames.CustomAttribute idx
                let parentIdx = seekReadHasCustomAttributeIdx &addr
                let typeIdx = seekReadCustomAttributeTypeIdx &addr
                let valIdx = seekReadBlobIdx &addr
                (parentIdx, typeIdx, valIdx)

            //let seekReadFieldMarshalRow idx = 
            //    let mutable addr = rowAddr TableNames.FieldMarshal idx
            //    let parentIdx = seekReadHasFieldMarshalIdx &addr
            //    let typeIdx = seekReadBlobIdx &addr
            //    (parentIdx, typeIdx)

            /// Read Table ClassLayout. 
            let seekReadClassLayoutRow idx =
                let mutable addr = rowAddr ILTableNames.ClassLayout idx
                let pack = seekReadUInt16Adv &addr
                let size = seekReadInt32Adv &addr
                let tidx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                (pack, size, tidx)  

            /// Read Table FieldLayout. 
            let seekReadFieldLayoutRow idx =
                let mutable addr = rowAddr ILTableNames.FieldLayout idx
                let offset = seekReadInt32Adv &addr
                let fidx = seekReadUntaggedIdx ILTableNames.Field &addr
                (offset, fidx)  

            /// Read Table EventMap
            let seekReadEventMapRow idx =
                let mutable addr = rowAddr ILTableNames.EventMap idx
                let tidx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                let eventsIdx = seekReadUntaggedIdx ILTableNames.Event &addr
                (tidx, eventsIdx)

            /// Read Table Event
            let seekReadEventRow idx =
                let mutable addr = rowAddr ILTableNames.Event idx
                let flags = seekReadUInt16AsInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let typIdx = seekReadTypeDefOrRefOrSpecIdx &addr
                (flags, nameIdx, typIdx)

            /// Read Table PropertyMap
            let seekReadPropertyMapRow idx =
                let mutable addr = rowAddr ILTableNames.PropertyMap idx
                let tidx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                let propsIdx = seekReadUntaggedIdx ILTableNames.Property &addr
                (tidx, propsIdx)

            /// Read Table Property
            let seekReadPropertyRow idx =
                let mutable addr = rowAddr ILTableNames.Property idx
                let flags = seekReadUInt16AsInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let typIdx = seekReadBlobIdx &addr
                (flags, nameIdx, typIdx)

            /// Read Table MethodSemantics
            let seekReadMethodSemanticsRow idx =
                let mutable addr = rowAddr ILTableNames.MethodSemantics idx
                let flags = seekReadUInt16AsInt32Adv &addr
                let midx = seekReadUntaggedIdx ILTableNames.Method &addr
                let assocIdx = seekReadHasSemanticsIdx &addr
                (flags, midx, assocIdx)

            let seekReadMethodImplRow idx =
                let mutable addr = rowAddr ILTableNames.MethodImpl idx
                let tidx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                let mbodyIdx = seekReadMethodDefOrRefIdx &addr
                let mdeclIdx = seekReadMethodDefOrRefIdx &addr
                (tidx, mbodyIdx, mdeclIdx) 

            /// Read Table ILModuleRef
            let seekReadModuleRefRow idx =
                let mutable addr = rowAddr ILTableNames.ModuleRef idx
                let nameIdx = seekReadStringIdx &addr
                nameIdx

            /// Read Table ILTypeSpec
            let seekReadTypeSpecRow idx =
                let mutable addr = rowAddr ILTableNames.TypeSpec idx
                let blobIdx = seekReadBlobIdx &addr
                blobIdx

            /// Read Table Assembly
            let seekReadAssemblyRow idx =
                let mutable addr = rowAddr ILTableNames.Assembly idx
                let hash = seekReadInt32Adv &addr
                let v1 = seekReadUInt16Adv &addr
                let v2 = seekReadUInt16Adv &addr
                let v3 = seekReadUInt16Adv &addr
                let v4 = seekReadUInt16Adv &addr
                let flags = seekReadInt32Adv &addr
                let publicKeyIdx = seekReadBlobIdx &addr
                let nameIdx = seekReadStringIdx &addr
                let localeIdx = seekReadStringIdx &addr
                (hash, v1, v2, v3, v4, flags, publicKeyIdx, nameIdx, localeIdx)

            /// Read Table ILAssemblyRef
            let seekReadAssemblyRefRow idx =
                let mutable addr = rowAddr ILTableNames.AssemblyRef idx
                let v1 = seekReadUInt16Adv &addr
                let v2 = seekReadUInt16Adv &addr
                let v3 = seekReadUInt16Adv &addr
                let v4 = seekReadUInt16Adv &addr
                let flags = seekReadInt32Adv &addr
                let publicKeyOrTokenIdx = seekReadBlobIdx &addr
                let nameIdx = seekReadStringIdx &addr
                let localeIdx = seekReadStringIdx &addr
                let hashValueIdx = seekReadBlobIdx &addr
                (v1, v2, v3, v4, flags, publicKeyOrTokenIdx, nameIdx, localeIdx, hashValueIdx)

            /// Read Table File
            let seekReadFileRow idx =
                let mutable addr = rowAddr ILTableNames.File idx
                let flags = seekReadInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let hashValueIdx = seekReadBlobIdx &addr
                (flags, nameIdx, hashValueIdx)

            /// Read Table ILExportedTypeOrForwarder
            let seekReadExportedTypeRow idx =
                let mutable addr = rowAddr ILTableNames.ExportedType idx
                let flags = seekReadInt32Adv &addr
                let tok = seekReadInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let namespaceIdx = seekReadStringIdx &addr
                let implIdx = seekReadImplementationIdx &addr
                (flags, tok, nameIdx, namespaceIdx, implIdx)

            /// Read Table ManifestResource
            let seekReadManifestResourceRow idx =
                let mutable addr = rowAddr ILTableNames.ManifestResource idx
                let offset = seekReadInt32Adv &addr
                let flags = seekReadInt32Adv &addr
                let nameIdx = seekReadStringIdx &addr
                let implIdx = seekReadImplementationIdx &addr
                (offset, flags, nameIdx, implIdx)

            /// Read Table Nested
            let seekReadNestedRow idx =
                let mutable addr = rowAddr ILTableNames.Nested idx
                let nestedIdx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                let enclIdx = seekReadUntaggedIdx ILTableNames.TypeDef &addr
                (nestedIdx, enclIdx)

            /// Read Table GenericParam
            let seekReadGenericParamRow idx =
                let mutable addr = rowAddr ILTableNames.GenericParam idx
                let seq = seekReadUInt16Adv &addr
                let flags = seekReadUInt16Adv &addr
                let ownerIdx = seekReadTypeOrMethodDefIdx &addr
                let nameIdx = seekReadStringIdx &addr
                (idx, seq, flags, ownerIdx, nameIdx)

            // Read Table GenericParamConstraint
            let seekReadGenericParamConstraintRow idx =
                let mutable addr = rowAddr ILTableNames.GenericParamConstraint idx
                let pidx = seekReadUntaggedIdx ILTableNames.GenericParam &addr
                let constraintIdx = seekReadTypeDefOrRefOrSpecIdx &addr
                (pidx, constraintIdx)

            //let readUserStringHeapUncached idx = seekReadUserString is (userStringsStreamPhysicalLoc + idx)
            //let readUserStringHeap = cacheUserStringHeap readUserStringHeapUncached

            let readStringHeapUncached idx =  seekReadUTF8String is (stringsStreamPhysicalLoc + idx)
            let readStringHeap = cacheStringHeap readStringHeapUncached
            let readStringHeapOption idx = if idx = 0 then UNone else USome (readStringHeap idx)

            let emptyByteArray: byte[] = [||]
            let readBlobHeapUncached idx =
                // valid index lies in range [1..streamSize)
                // NOTE: idx cannot be 0 - Blob\String heap has first empty element that is one byte 0
                if idx <= 0 || idx >= blobsStreamSize then emptyByteArray
                else seekReadBlob is (blobsStreamPhysicalLoc + idx)
            let readBlobHeap = cacheBlobHeap readBlobHeapUncached
            let readBlobHeapOption idx = if idx = 0 then UNone else USome (readBlobHeap idx)

            //let readGuidHeap idx = seekReadGuid is (guidsStreamPhysicalLoc + idx)

            // read a single value out of a blob heap using the given function
            let readBlobHeapAsBool   vidx = fst (sigptrGetBool   (readBlobHeap vidx) 0)
            let readBlobHeapAsSByte  vidx = fst (sigptrGetSByte  (readBlobHeap vidx) 0)
            let readBlobHeapAsInt16  vidx = fst (sigptrGetInt16  (readBlobHeap vidx) 0)
            let readBlobHeapAsInt32  vidx = fst (sigptrGetInt32  (readBlobHeap vidx) 0)
            let readBlobHeapAsInt64  vidx = fst (sigptrGetInt64  (readBlobHeap vidx) 0)
            let readBlobHeapAsByte   vidx = fst (sigptrGetByte   (readBlobHeap vidx) 0)
            let readBlobHeapAsUInt16 vidx = fst (sigptrGetUInt16 (readBlobHeap vidx) 0)
            let readBlobHeapAsUInt32 vidx = fst (sigptrGetUInt32 (readBlobHeap vidx) 0)
            let readBlobHeapAsUInt64 vidx = fst (sigptrGetUInt64 (readBlobHeap vidx) 0)
            let readBlobHeapAsSingle vidx = fst (sigptrGetSingle (readBlobHeap vidx) 0)
            let readBlobHeapAsDouble vidx = fst (sigptrGetDouble (readBlobHeap vidx) 0)

            //-----------------------------------------------------------------------
            // Read the AbsIL structure (lazily) by reading off the relevant rows.
            // ----------------------------------------------------------------------

            let isSorted (tab:ILTableName) = ((sorted &&& (int64 1 <<< tab.Index)) <> int64 0x0)

            //let subsysversion = (subsysMajor, subsysMinor)
            let ilMetadataVersion = Encoding.UTF8.GetString (ilMetadataVersion, 0, ilMetadataVersion.Length)

            let rec seekReadModule (ilMetadataVersion) idx =
                let (_generation, nameIdx, _mvidIdx, _encidIdx, _encbaseidIdx) = seekReadModuleRow idx
                let ilModuleName = readStringHeap nameIdx
                //let nativeResources = readNativeResources tgt

                { Manifest =
                     if getNumRows (ILTableNames.Assembly) > 0 then Some (seekReadAssemblyManifest 1)
                     else None;
                  CustomAttrs = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Module, idx));
                  Name = ilModuleName;
                  //NativeResources=nativeResources;
                  TypeDefs = ILTypeDefs (lazy (seekReadTopTypeDefs ()));
                  SubsystemVersion = (4, 0)
                  UseHighEntropyVA = false
                  SubSystemFlags=3
                  IsDLL=true
                  IsILOnly=true
                  Platform=None
                  StackReserveSize=None
                  Is32Bit=false
                  Is32BitPreferred=false
                  Is64Bit=false
                  PhysicalAlignment=512
                  VirtualAlignment=0x2000
                  ImageBase=0x034f0000
                  MetadataVersion=""
                  Resources = seekReadManifestResources ()
                  }

            and seekReadAssemblyManifest idx =
                let (hash, v1, v2, v3, v4, flags, publicKeyIdx, nameIdx, localeIdx) = seekReadAssemblyRow idx
                let name = readStringHeap nameIdx
                let pubkey = readBlobHeapOption publicKeyIdx
                { Name= name;
                  AuxModuleHashAlgorithm=hash
                  PublicKey= pubkey
                  Version= USome (Version(int v1, int v2, int v3, int v4))
                  Locale= readStringHeapOption localeIdx
                  CustomAttrs = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Assembly, idx))
                  ExportedTypes= seekReadTopExportedTypes ()
                  EntrypointElsewhere=None
                  Retargetable = 0 <> (flags &&& 0x100);
                  DisableJitOptimizations = 0 <> (flags &&& 0x4000);
                  JitTracking = 0 <> (flags &&& 0x8000)
                  IgnoreSymbolStoreSequencePoints = 0 <> (flags &&& 0x2000) 
                  }

            and seekReadAssemblyRef idx = cacheAssemblyRef  seekReadAssemblyRefUncached idx
            and seekReadAssemblyRefUncached idx =
                let (v1, v2, v3, v4, flags, publicKeyOrTokenIdx, nameIdx, localeIdx, hashValueIdx) = seekReadAssemblyRefRow idx
                let nm = readStringHeap nameIdx
                let publicKey =
                    match readBlobHeapOption publicKeyOrTokenIdx with
                      | UNone -> UNone
                      | USome blob -> USome (if (flags &&& 0x0001) <> 0x0 then PublicKey blob else PublicKeyToken blob)

                ILAssemblyRef
                    (name=nm, 
                     hash=readBlobHeapOption hashValueIdx, 
                     publicKey=publicKey, 
                     retargetable=((flags &&& 0x0100) <> 0x0), 
                     version=USome(Version(int v1, int v2, int v3, int v4)), 
                     locale=readStringHeapOption localeIdx;)

            and seekReadModuleRef idx =
                let nameIdx = seekReadModuleRefRow idx
                ILModuleRef(name=readStringHeap nameIdx, hasMetadata=true, hash=UNone)

            and seekReadFile idx =
                let (flags, nameIdx, hashValueIdx) = seekReadFileRow idx
                ILModuleRef(name =  readStringHeap nameIdx, 
                            hasMetadata= ((flags &&& 0x0001) = 0x0), 
                            hash= readBlobHeapOption hashValueIdx)

            and seekReadClassLayout idx =
                match seekReadOptionalIndexedRow (getNumRows ILTableNames.ClassLayout, seekReadClassLayoutRow, (fun (_, _, tidx) -> tidx), simpleIndexCompare idx, isSorted ILTableNames.ClassLayout, (fun (pack, size, _) -> pack, size)) with
                | None -> { Size = None; Pack = None }
                | Some (pack, size) -> { Size = Some size; Pack = Some pack; }


            and typeLayoutOfFlags flags tidx =
                let f = (flags &&& 0x00000018)
                if f = 0x00000008 then ILTypeDefLayout.Sequential (seekReadClassLayout tidx)
                elif f = 0x00000010 then  ILTypeDefLayout.Explicit (seekReadClassLayout tidx)
                else ILTypeDefLayout.Auto

            and isTopTypeDef flags =
                (ILTypeDefAccess.OfFlags flags =  ILTypeDefAccess.Private) ||
                 ILTypeDefAccess.OfFlags flags =  ILTypeDefAccess.Public

            and seekIsTopTypeDefOfIdx idx =
                let (flags, _, _, _, _, _) = seekReadTypeDefRow idx
                isTopTypeDef flags

            and readStringHeapAsTypeName (nameIdx, namespaceIdx) =
                let name = readStringHeap nameIdx
                let nspace = readStringHeapOption namespaceIdx
                nspace, name

            and seekReadTypeDefRowExtents _info (idx:int) =
                if idx >= getNumRows ILTableNames.TypeDef then
                    getNumRows ILTableNames.Field + 1, 
                    getNumRows ILTableNames.Method + 1
                else
                    let (_, _, _, _, fieldsIdx, methodsIdx) = seekReadTypeDefRow (idx + 1)
                    fieldsIdx, methodsIdx

            and seekReadTypeDefRowWithExtents (idx:int) =
                let info= seekReadTypeDefRow idx
                info, seekReadTypeDefRowExtents info idx

            and seekReadTypeDef toponly (idx:int) =
                let (flags, nameIdx, namespaceIdx, _, _, _) = seekReadTypeDefRow idx
                if toponly && not (isTopTypeDef flags) then None
                else

                 let name = readStringHeap nameIdx
                 let nspace = readStringHeapOption namespaceIdx
                 let rest =
                    lazy
                       let ((flags, nameIdx, namespaceIdx, extendsIdx, fieldsIdx, methodsIdx) as info) = seekReadTypeDefRow idx
                       let name = readStringHeap nameIdx
                       let nspace = readStringHeapOption namespaceIdx
                       let (endFieldsIdx, endMethodsIdx) = seekReadTypeDefRowExtents info idx
                       let typars = seekReadGenericParams 0 (TypeOrMethodDefTag.TypeDef, idx)
                       let numtypars = typars.Length
                       let super = seekReadOptionalTypeDefOrRef numtypars AsObject extendsIdx
                       let layout = typeLayoutOfFlags flags idx
                       //let hasLayout = (match layout with ILTypeDefLayout.Explicit _ -> true | _ -> false)
                       let hasLayout = false
                       let mdefs = seekReadMethods numtypars methodsIdx endMethodsIdx
                       let fdefs = seekReadFields (numtypars, hasLayout) fieldsIdx endFieldsIdx
                       let nested = seekReadNestedTypeDefs idx
                       let intfs = seekReadInterfaceImpls numtypars idx
                       //let sdecls =  seekReadSecurityDecls (TaggedIndex(hds_TypeDef, idx))
                       let mimpls = seekReadMethodImpls numtypars idx
                       let props = seekReadProperties numtypars idx
                       let events = seekReadEvents numtypars idx
                       let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.TypeDef, idx))
                       { Namespace=nspace
                         Name=name
                         GenericParams=typars
                         Attributes = enum<TypeAttributes> flags
                         Layout = layout
                         NestedTypes= nested
                         Implements =  intfs
                         Extends = super
                         Methods = mdefs

                         //SecurityDecls = sdecls
                         //HasSecurity=(flags &&& 0x00040000) <> 0x0
                         Fields=fdefs
                         MethodImpls=mimpls
                         Events= events
                         Properties=props
                         CustomAttrs=cas
                         Token = idx }
                 Some (nspace, name, rest)

            and seekReadTopTypeDefs () =
                [| for i = 1 to getNumRows ILTableNames.TypeDef do
                      match seekReadTypeDef true i  with
                      | None -> ()
                      | Some td -> yield td |]

            and seekReadNestedTypeDefs tidx =
                ILTypeDefs
                  (lazy
                       let nestedIdxs = seekReadIndexedRows (getNumRows ILTableNames.Nested, seekReadNestedRow, snd, simpleIndexCompare tidx, false, fst)
                       [| for i in nestedIdxs do
                             match seekReadTypeDef false i with
                             | None -> ()
                             | Some td -> yield td |])

            and seekReadInterfaceImpls numtypars tidx =
                seekReadIndexedRows (getNumRows ILTableNames.InterfaceImpl, seekReadInterfaceImplRow , fst, simpleIndexCompare tidx, isSorted ILTableNames.InterfaceImpl, (snd >> seekReadTypeDefOrRef numtypars AsObject [| |]))

            and seekReadGenericParams numtypars (a, b): ILGenericParameterDefs =
                let pars =
                    seekReadIndexedRows
                        (getNumRows ILTableNames.GenericParam, seekReadGenericParamRow, 
                         (fun (_, _, _, tomd, _) -> tomd), 
                         tomdCompare (TaggedIndex(a, b)), 
                         isSorted ILTableNames.GenericParam, 
                         (fun (gpidx, seq, flags, _, nameIdx) ->
                             let constraints = seekReadGenericParamConstraintsUncached numtypars gpidx
                             let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.GenericParam, gpidx))
                             seq, {Name=readStringHeap nameIdx
                                   Constraints= constraints
                                   CustomAttrs=cas
                                   Attributes = enum (int32 flags)
                                   Token=gpidx }))
                pars |> Array.sortBy fst |> Array.map snd

            and seekReadGenericParamConstraintsUncached numtypars gpidx =
                seekReadIndexedRows
                    (getNumRows ILTableNames.GenericParamConstraint, 
                     seekReadGenericParamConstraintRow, 
                     fst, 
                     simpleIndexCompare gpidx, 
                     isSorted ILTableNames.GenericParamConstraint, 
                     (snd >>  seekReadTypeDefOrRef numtypars AsObject (*ok*) [| |]))

            and seekReadTypeDefAsType boxity (ginst:ILTypes) idx = cacheTypeDefAsType seekReadTypeDefAsTypeUncached (TypeDefAsTypIdx (boxity, ginst, idx))

            and seekReadTypeDefAsTypeUncached (TypeDefAsTypIdx (boxity, ginst, idx)) =
                mkILTy boxity (ILTypeSpec(seekReadTypeDefAsTypeRef idx, ginst))

            and seekReadTypeDefAsTypeRef idx =
                 let enc =
                   if seekIsTopTypeDefOfIdx idx then ILTypeRefScope.Top ILScopeRef.Local
                   else
                     let enclIdx = seekReadIndexedRow (getNumRows ILTableNames.Nested, seekReadNestedRow, fst, simpleIndexCompare idx, isSorted ILTableNames.Nested, snd)
                     let tref = seekReadTypeDefAsTypeRef enclIdx
                     ILTypeRefScope.Nested tref
                 let (_, nameIdx, namespaceIdx, _, _, _) = seekReadTypeDefRow idx
                 let nsp, nm = readStringHeapAsTypeName (nameIdx, namespaceIdx)
                 ILTypeRef(enc=enc, nsp = nsp, name = nm )

            and seekReadTypeRef idx = cacheTypeRef seekReadTypeRefUncached idx
            and seekReadTypeRefUncached idx =
                 let scopeIdx, nameIdx, namespaceIdx = seekReadTypeRefRow idx
                 let enc = seekReadTypeRefScope scopeIdx
                 let nsp, nm = readStringHeapAsTypeName (nameIdx, namespaceIdx)
                 ILTypeRef(enc, nsp, nm)

            and seekReadTypeRefAsType boxity ginst idx = cacheTypeRefAsType seekReadTypeRefAsTypeUncached (TypeRefAsTypIdx (boxity, ginst, idx))
            and seekReadTypeRefAsTypeUncached (TypeRefAsTypIdx (boxity, ginst, idx)) =
                 mkILTy boxity (ILTypeSpec(seekReadTypeRef idx, ginst))

            and seekReadTypeDefOrRef numtypars boxity (ginst:ILTypes) (TaggedIndex(tag, idx) ) =
                match tag with
                | tag when tag = TypeDefOrRefOrSpecTag.TypeDef -> seekReadTypeDefAsType boxity ginst idx
                | tag when tag = TypeDefOrRefOrSpecTag.TypeRef -> seekReadTypeRefAsType boxity ginst idx
                | tag when tag = TypeDefOrRefOrSpecTag.TypeSpec -> readBlobHeapAsType numtypars (seekReadTypeSpecRow idx)
                | _ -> failwith "seekReadTypeDefOrRef"

            and seekReadTypeDefOrRefAsTypeRef (TaggedIndex(tag, idx) ) =
                match tag with
                | tag when tag = TypeDefOrRefOrSpecTag.TypeDef -> seekReadTypeDefAsTypeRef idx
                | tag when tag = TypeDefOrRefOrSpecTag.TypeRef -> seekReadTypeRef idx
                | tag when tag = TypeDefOrRefOrSpecTag.TypeSpec -> ilg.typ_Object.TypeRef
                | _ -> failwith "seekReadTypeDefOrRefAsTypeRef_readTypeDefOrRefOrSpec"

            and seekReadMethodRefParent numtypars (TaggedIndex(tag, idx)) =
                match tag with
                | tag when tag = MemberRefParentTag.TypeRef -> seekReadTypeRefAsType AsObject (* not ok - no way to tell if a member ref parent is a value type or not *) [| |] idx
                | tag when tag = MemberRefParentTag.ModuleRef -> mkILTypeForGlobalFunctions (ILScopeRef.Module (seekReadModuleRef idx))
                | tag when tag = MemberRefParentTag.MethodDef ->
                    let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData idx
                    let mspec = mkILMethSpecInTyRaw(enclTyp, cc, nm, argtys, retty, minst)
                    mspec.EnclosingType
                | tag when tag = MemberRefParentTag.TypeSpec -> readBlobHeapAsType numtypars (seekReadTypeSpecRow idx)
                | _ -> failwith "seekReadMethodRefParent"


            and seekReadMethodDefOrRef numtypars (TaggedIndex(tag, idx)) =
                match tag with 
                | tag when tag = MethodDefOrRefTag.MethodDef -> 
                    let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData idx
                    VarArgMethodData(enclTyp, cc, nm, argtys, None, retty, minst)
                | tag when tag = MethodDefOrRefTag.MemberRef -> 
                    seekReadMemberRefAsMethodData numtypars idx
                | _ -> failwith "seekReadMethodDefOrRef ctxt"

            and seekReadMethodDefOrRefNoVarargs numtypars x =
                let (VarArgMethodData(enclTyp, cc, nm, argtys, varargs, retty, minst)) =     seekReadMethodDefOrRef numtypars x 
                MethodData(enclTyp, cc, nm, argtys, retty, minst)

            and seekReadCustomAttrType (TaggedIndex(tag, idx) ) =
                match tag with
                | tag when tag = CustomAttributeTypeTag.MethodDef ->
                    let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData idx
                    mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)
                | tag when tag = CustomAttributeTypeTag.MemberRef ->
                    let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMemberRefAsMethDataNoVarArgs 0 idx
                    mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)
                | _ -> failwith "seekReadCustomAttrType"

            and seekReadImplAsScopeRef (TaggedIndex(tag, idx) ) =
                 if idx = 0 then ILScopeRef.Local
                 else
                   match tag with
                   | tag when tag = ImplementationTag.File -> ILScopeRef.Module (seekReadFile idx)
                   | tag when tag = ImplementationTag.AssemblyRef -> ILScopeRef.Assembly (seekReadAssemblyRef idx)
                   | tag when tag = ImplementationTag.ExportedType -> failwith "seekReadImplAsScopeRef"
                   | _ -> failwith "seekReadImplAsScopeRef"

            and seekReadTypeRefScope (TaggedIndex(tag, idx) ): ILTypeRefScope =
                match tag with
                | tag when tag = ResolutionScopeTag.Module -> ILTypeRefScope.Top(ILScopeRef.Local)
                | tag when tag = ResolutionScopeTag.ModuleRef -> ILTypeRefScope.Top(ILScopeRef.Module (seekReadModuleRef idx))
                | tag when tag = ResolutionScopeTag.AssemblyRef -> ILTypeRefScope.Top(ILScopeRef.Assembly (seekReadAssemblyRef idx))
                | tag when tag = ResolutionScopeTag.TypeRef -> ILTypeRefScope.Nested (seekReadTypeRef idx)
                | _ -> failwith "seekReadTypeRefScope"

            and seekReadOptionalTypeDefOrRef numtypars boxity idx =
                if idx = TaggedIndex(TypeDefOrRefOrSpecTag.TypeDef, 0) then None
                else Some (seekReadTypeDefOrRef numtypars boxity [| |] idx)

            and seekReadField (numtypars, hasLayout) (idx:int) =
                 let (flags, nameIdx, typeIdx) = seekReadFieldRow idx
                 let nm = readStringHeap nameIdx
                 let isStatic = (flags &&& 0x0010) <> 0
                 { Name = nm
                   FieldType = readBlobHeapAsFieldSig numtypars typeIdx
                   LiteralValue = if (flags &&& 0x8000) = 0 then None else Some (seekReadConstant (TaggedIndex(HasConstantTag.FieldDef, idx)))
                   //Marshal =
                   //      if (flags &&& 0x1000) = 0 then None else
                   //      Some (seekReadIndexedRow (getNumRows ILTableNames.FieldMarshal, seekReadFieldMarshalRow, 
                   //                                fst, hfmCompare (TaggedIndex(hfm_FieldDef, idx)), 
                   //                                isSorted ILTableNames.FieldMarshal, 
                   //                                (snd >> readBlobHeapAsNativeType ctxt)))
                   //Data =
                   //      if (flags &&& 0x0100) = 0 then None
                   //      else
                   //        let rva = seekReadIndexedRow (getNumRows ILTableNames.FieldRVA, seekReadFieldRVARow, 
                   //                                      snd, simpleIndexCompare idx, isSorted ILTableNames.FieldRVA, fst)
                   //        Some (rvaToData "field" rva)
                   Attributes = enum<FieldAttributes>(flags)
                   Offset =
                         if hasLayout && not isStatic then
                             Some (seekReadIndexedRow (getNumRows ILTableNames.FieldLayout, seekReadFieldLayoutRow, 
                                                       snd, simpleIndexCompare idx, isSorted ILTableNames.FieldLayout, fst)) else None
                   CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.FieldDef, idx)) 
                   Token = idx }

            and seekReadFields (numtypars, hasLayout) fidx1 fidx2 =
                { new ILFieldDefs with
                   member __.Entries =
                       [| for i = fidx1 to fidx2 - 1 do
                           yield seekReadField (numtypars, hasLayout) i |] }

            and seekReadMethods numtypars midx1 midx2 =
                ILMethodDefs
                   (lazy
                       [| for i = midx1 to midx2 - 1 do
                             yield seekReadMethod numtypars i |])

            and sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr =
                let n, sigptr = sigptrGetZInt32 bytes sigptr
                if (n &&& 0x01) = 0x0 then (* Type Def *)
                    TaggedIndex(TypeDefOrRefOrSpecTag.TypeDef, (n >>>& 2)), sigptr
                else (* Type Ref *)
                    TaggedIndex(TypeDefOrRefOrSpecTag.TypeRef, (n >>>& 2)), sigptr

            and sigptrGetTy numtypars bytes sigptr =
                let b0, sigptr = sigptrGetByte bytes sigptr
                if b0 = et_OBJECT then ilg.typ_Object , sigptr
                elif b0 = et_STRING then ilg.typ_String, sigptr
                elif b0 = et_I1 then ilg.typ_SByte, sigptr
                elif b0 = et_I2 then ilg.typ_Int16, sigptr
                elif b0 = et_I4 then ilg.typ_Int32, sigptr
                elif b0 = et_I8 then ilg.typ_Int64, sigptr
                elif b0 = et_I then ilg.typ_IntPtr, sigptr
                elif b0 = et_U1 then ilg.typ_Byte, sigptr
                elif b0 = et_U2 then ilg.typ_UInt16, sigptr
                elif b0 = et_U4 then ilg.typ_UInt32, sigptr
                elif b0 = et_U8 then ilg.typ_UInt64, sigptr
                elif b0 = et_U then ilg.typ_UIntPtr, sigptr
                elif b0 = et_R4 then ilg.typ_Single, sigptr
                elif b0 = et_R8 then ilg.typ_Double, sigptr
                elif b0 = et_CHAR then ilg.typ_Char, sigptr
                elif b0 = et_BOOLEAN then ilg.typ_Boolean, sigptr
                elif b0 = et_WITH then
                    let b0, sigptr = sigptrGetByte bytes sigptr
                    let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
                    let n, sigptr = sigptrGetZInt32 bytes sigptr
                    let argtys, sigptr = sigptrFold (sigptrGetTy numtypars) n bytes sigptr
                    seekReadTypeDefOrRef numtypars (if b0 = et_CLASS then AsObject else AsValue) argtys tdorIdx, 
                    sigptr

                elif b0 = et_CLASS then
                    let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
                    seekReadTypeDefOrRef numtypars AsObject [| |] tdorIdx, sigptr
                elif b0 = et_VALUETYPE then
                    let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
                    seekReadTypeDefOrRef numtypars AsValue [| |] tdorIdx, sigptr
                elif b0 = et_VAR then
                    let n, sigptr = sigptrGetZInt32 bytes sigptr
                    ILType.Var n, sigptr
                elif b0 = et_MVAR then
                    let n, sigptr = sigptrGetZInt32 bytes sigptr
                    ILType.Var (n + numtypars), sigptr
                elif b0 = et_BYREF then
                    let typ, sigptr = sigptrGetTy numtypars bytes sigptr
                    ILType.Byref typ, sigptr
                elif b0 = et_PTR then
                    let typ, sigptr = sigptrGetTy numtypars bytes sigptr
                    ILType.Ptr typ, sigptr
                elif b0 = et_SZARRAY then
                    let typ, sigptr = sigptrGetTy numtypars bytes sigptr
                    mkILArr1DTy typ, sigptr
                elif b0 = et_ARRAY then
                    let typ, sigptr = sigptrGetTy numtypars bytes sigptr
                    let rank, sigptr = sigptrGetZInt32 bytes sigptr
                    let numSized, sigptr = sigptrGetZInt32 bytes sigptr
                    let sizes, sigptr = sigptrFold sigptrGetZInt32 numSized bytes sigptr
                    let numLoBounded, sigptr = sigptrGetZInt32 bytes sigptr
                    let lobounds, sigptr = sigptrFold sigptrGetZInt32 numLoBounded bytes sigptr
                    let shape =
                        let dim i =
                          (if i <  numLoBounded then Some lobounds.[i] else None), 
                          (if i <  numSized then Some sizes.[i] else None)
                        ILArrayShape (Array.init rank dim)
                    ILType.Array (shape, typ), sigptr

                elif b0 = et_VOID then ILType.Void, sigptr
                elif b0 = et_TYPEDBYREF then
                    match ilg.typ_TypedReference with
                    | Some t -> t, sigptr
                    | _ -> failwith "system runtime doesn't contain System.TypedReference"
                elif b0 = et_CMOD_REQD || b0 = et_CMOD_OPT  then
                    let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
                    let typ, sigptr = sigptrGetTy numtypars bytes sigptr
                    ILType.Modified((b0 = et_CMOD_REQD), seekReadTypeDefOrRefAsTypeRef tdorIdx, typ), sigptr
                elif b0 = et_FNPTR then
                    let ccByte, sigptr = sigptrGetByte bytes sigptr
                    let generic, cc = byteAsCallConv ccByte
                    if generic then failwith "fptr sig may not be generic"
                    let numparams, sigptr = sigptrGetZInt32 bytes sigptr
                    let retty, sigptr = sigptrGetTy numtypars bytes sigptr
                    let argtys, sigptr = sigptrFold (sigptrGetTy numtypars) ( numparams) bytes sigptr
                    ILType.FunctionPointer (ILCallingSignature(cc, argtys, retty)), sigptr
                elif b0 = et_SENTINEL then failwith "varargs NYI"
                else ILType.Void , sigptr

            and sigptrGetVarArgTys n numtypars bytes sigptr =
                sigptrFold (sigptrGetTy numtypars) n bytes sigptr

            and sigptrGetArgTys n numtypars bytes sigptr acc =
                if n <= 0 then (Array.ofList (List.rev acc), None), sigptr
                else
                  let b0, sigptr2 = sigptrGetByte bytes sigptr
                  if b0 = et_SENTINEL then
                    let varargs, sigptr = sigptrGetVarArgTys n numtypars bytes sigptr2
                    (Array.ofList (List.rev acc), Some( varargs)), sigptr
                  else
                    let x, sigptr = sigptrGetTy numtypars bytes sigptr
                    sigptrGetArgTys (n-1) numtypars bytes sigptr (x::acc)

            and readBlobHeapAsMethodSig numtypars blobIdx = cacheBlobHeapAsMethodSig readBlobHeapAsMethodSigUncached (BlobAsMethodSigIdx (numtypars, blobIdx))

            and readBlobHeapAsMethodSigUncached (BlobAsMethodSigIdx (numtypars, blobIdx)) =
                let bytes = readBlobHeap blobIdx
                let sigptr = 0
                let ccByte, sigptr = sigptrGetByte bytes sigptr
                let generic, cc = byteAsCallConv ccByte
                let genarity, sigptr = if generic then sigptrGetZInt32 bytes sigptr else 0x0, sigptr
                let numparams, sigptr = sigptrGetZInt32 bytes sigptr
                let retty, sigptr = sigptrGetTy numtypars bytes sigptr
                let (argtys, varargs), _sigptr = sigptrGetArgTys  ( numparams) numtypars bytes sigptr []
                generic, genarity, cc, retty, argtys, varargs

            and readBlobHeapAsType numtypars blobIdx =
                let bytes = readBlobHeap blobIdx
                let ty, _sigptr = sigptrGetTy numtypars bytes 0
                ty

            and readBlobHeapAsFieldSig numtypars blobIdx = cacheBlobHeapAsFieldSig readBlobHeapAsFieldSigUncached (BlobAsFieldSigIdx (numtypars, blobIdx))

            and readBlobHeapAsFieldSigUncached (BlobAsFieldSigIdx (numtypars, blobIdx)) =
                let bytes = readBlobHeap blobIdx
                let sigptr = 0
                let _ccByte, sigptr = sigptrGetByte bytes sigptr
                let retty, _sigptr = sigptrGetTy numtypars bytes sigptr
                retty


            and readBlobHeapAsPropertySig numtypars blobIdx = cacheBlobHeapAsPropertySig readBlobHeapAsPropertySigUncached (BlobAsPropSigIdx (numtypars, blobIdx))
            and readBlobHeapAsPropertySigUncached (BlobAsPropSigIdx (numtypars, blobIdx))  =
                let bytes = readBlobHeap blobIdx
                let sigptr = 0
                let ccByte, sigptr = sigptrGetByte bytes sigptr
                let hasthis = byteAsHasThis ccByte
                let numparams, sigptr = sigptrGetZInt32 bytes sigptr
                let retty, sigptr = sigptrGetTy numtypars bytes sigptr
                let argtys, _sigptr = sigptrFold (sigptrGetTy numtypars) ( numparams) bytes sigptr
                hasthis, retty, argtys

            and byteAsHasThis b =
                let hasthis_masked = b &&& 0x60uy
                if hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE then ILThisConvention.Instance
                elif hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT then ILThisConvention.InstanceExplicit
                else ILThisConvention.Static

            and byteAsCallConv b =
                let cc =
                    let ccMaxked = b &&& 0x0Fuy
                    if ccMaxked =  e_IMAGE_CEE_CS_CALLCONV_FASTCALL then ILArgConvention.FastCall
                    elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_STDCALL then ILArgConvention.StdCall
                    elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_THISCALL then ILArgConvention.ThisCall
                    elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_CDECL then ILArgConvention.CDecl
                    elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_VARARG then ILArgConvention.VarArg
                    else  ILArgConvention.Default
                let generic = (b &&& e_IMAGE_CEE_CS_CALLCONV_GENERIC) <> 0x0uy
                generic, Callconv (byteAsHasThis b, cc)

            and seekReadMemberRefAsMethodData numtypars idx: VarArgMethodData =  cacheMemberRefAsMemberData  seekReadMemberRefAsMethodDataUncached (MemberRefAsMspecIdx (numtypars, idx))

            and seekReadMemberRefAsMethodDataUncached (MemberRefAsMspecIdx (numtypars, idx)) =
                let (mrpIdx, nameIdx, typeIdx) = seekReadMemberRefRow idx
                let nm = readStringHeap nameIdx
                let enclTyp = seekReadMethodRefParent numtypars mrpIdx
                let _generic, genarity, cc, retty, argtys, varargs = readBlobHeapAsMethodSig enclTyp.GenericArgs.Length typeIdx
                let minst =  Array.init genarity (fun n -> ILType.Var (numtypars+n))
                (VarArgMethodData(enclTyp, cc, nm, argtys, varargs, retty, minst))

            and seekReadMemberRefAsMethDataNoVarArgs numtypars idx: MethodData =
               let (VarArgMethodData(enclTyp, cc, nm, argtys, _varargs, retty, minst)) =  seekReadMemberRefAsMethodData numtypars idx
               (MethodData(enclTyp, cc, nm, argtys, retty, minst))

            // One extremely annoying aspect of the MD format is that given a
            // ILMethodDef token it is non-trivial to find which ILTypeDef it belongs
            // to.  So we do a binary chop through the ILTypeDef table
            // looking for which ILTypeDef has the ILMethodDef within its range.
            // Although the ILTypeDef table is not "sorted", it is effectively sorted by
            // method-range and field-range start/finish indexes
            and seekReadMethodDefAsMethodData idx = cacheMethodDefAsMethodData seekReadMethodDefAsMethodDataUncached idx
            and seekReadMethodDefAsMethodDataUncached idx =
               let (_code_rva, _implflags, _flags, nameIdx, typeIdx, _paramIdx) = seekReadMethodRow idx
               let nm = readStringHeap nameIdx
               // Look for the method def parent.
               let tidx =
                 seekReadIndexedRow (getNumRows ILTableNames.TypeDef, 
                                        (fun i -> i, seekReadTypeDefRowWithExtents i), 
                                        (fun r -> r), 
                                        (fun (_, ((_, _, _, _, _, methodsIdx), 
                                                  (_, endMethodsIdx)))  ->
                                                    if endMethodsIdx <= idx then 1
                                                    elif methodsIdx <= idx && idx < endMethodsIdx then 0
                                                    else -1), 
                                        true, fst)
               let _generic, _genarity, cc, retty, argtys, _varargs = readBlobHeapAsMethodSig 0 typeIdx
               let ctps = seekReadGenericParams 0 (TypeOrMethodDefTag.TypeDef, tidx)
               let mtps = seekReadGenericParams ctps.Length (TypeOrMethodDefTag.MethodDef, idx)
               let finst = mkILFormalGenericArgs 0 ctps.Length
               let minst = mkILFormalGenericArgs ctps.Length mtps.Length
               let enclTyp = seekReadTypeDefAsType AsObject finst tidx
               MethodData(enclTyp, cc, nm, argtys, retty, minst)

            and seekReadMethod numtypars (idx:int) =
                 let (_codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx) = seekReadMethodRow idx
                 let nm = readStringHeap nameIdx
                 let _generic, _genarity, cc, retty, argtys, _varargs = readBlobHeapAsMethodSig numtypars typeIdx

                 let endParamIdx =
                   if idx >= getNumRows ILTableNames.Method then
                     getNumRows ILTableNames.Param + 1
                   else
                     let (_, _, _, _, _, paramIdx) = seekReadMethodRow (idx + 1)
                     paramIdx

                 let ret, ilParams = seekReadParams (retty, argtys) paramIdx endParamIdx

                 { Token=idx // This value is not a strict metadata token but it's good enough (if needed we could get the real one pretty easily)
                   Name=nm
                   Attributes = enum<System.Reflection.MethodAttributes>(flags)
                   //SecurityDecls=seekReadSecurityDecls (TaggedIndex(hds_MethodDef, idx))
                   //IsEntryPoint= (fst entryPointToken = ILTableNames.Method && snd entryPointToken = idx)
                   ImplAttributes= enum<MethodImplAttributes> implflags
                   GenericParams=seekReadGenericParams numtypars (TypeOrMethodDefTag.MethodDef, idx)
                   CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.MethodDef, idx))
                   Parameters= ilParams
                   CallingConv=cc
                   Return=ret
                   Body= None
                   //SecurityDecls
                   //HasSecurity= false
                   IsEntryPoint= false (* unused by reader *)
                 }


            and seekReadParams (retty, argtys) pidx1 pidx2 =
                let retRes: ILReturn ref =  ref { (* Marshal=None *) Type=retty; CustomAttrs=ILCustomAttrsStatics.Empty }
                let paramsRes =
                    argtys
                    |> Array.map (fun ty ->
                        { Name=UNone
                          Default=UNone
                          //Marshal=None
                          Attributes= ParameterAttributes.None
                          ParameterType=ty
                          CustomAttrs=ILCustomAttrsStatics.Empty })
                for i = pidx1 to pidx2 - 1 do
                    seekReadParamExtras (retRes, paramsRes) i
                !retRes, paramsRes

            and seekReadParamExtras (retRes, paramsRes) (idx:int) =
               let (flags, seq, nameIdx) = seekReadParamRow idx
               //let _hasMarshal = (flags &&& 0x2000) <> 0x0
               let hasDefault = (flags &&& 0x1000) <> 0x0
               //let fmReader idx = seekReadIndexedRow (getNumRows ILTableNames.FieldMarshal, seekReadFieldMarshalRow, fst, hfmCompare idx, isSorted ILTableNames.FieldMarshal, (snd >> readBlobHeapAsNativeType ctxt))
               let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ParamDef, idx))
               if seq = 0 then
                   retRes := { !retRes with
                                    //Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef, idx))) else None);
                                    CustomAttrs = cas }
               else
                   paramsRes.[seq - 1] <-
                      { paramsRes.[seq - 1] with
                           //Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef, idx))) else None)
                           Default = (if hasDefault then USome (seekReadConstant (TaggedIndex(HasConstantTag.ParamDef, idx))) else UNone)
                           Name = readStringHeapOption nameIdx
                           Attributes = enum<ParameterAttributes> flags
                           CustomAttrs = cas }

            and seekReadMethodImpls numtypars tidx =
               { new ILMethodImplDefs with
                  member __.Entries =
                      let mimpls = seekReadIndexedRows (getNumRows ILTableNames.MethodImpl, seekReadMethodImplRow, (fun (a, _, _) -> a), simpleIndexCompare tidx, isSorted ILTableNames.MethodImpl, (fun (_, b, c) -> b, c))
                      mimpls |> Array.map (fun (b, c) ->
                          { OverrideBy=
                              let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefOrRefNoVarargs numtypars b
                              mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst);
                            Overrides=
                              let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefOrRefNoVarargs numtypars c
                              let mspec = mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)
                              OverridesSpec(mspec.MethodRef, mspec.EnclosingType) }) }

            and seekReadMultipleMethodSemantics (flags, id) =
                seekReadIndexedRows
                  (getNumRows ILTableNames.MethodSemantics , 
                   seekReadMethodSemanticsRow, 
                   (fun (_flags, _, c) -> c), 
                   hsCompare id, 
                   isSorted ILTableNames.MethodSemantics, 
                   (fun (a, b, _c) ->
                       let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData b
                       a, (mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)).MethodRef))
                |> Array.filter (fun (flags2, _) -> flags = flags2)
                |> Array.map snd


            and seekReadOptionalMethodSemantics id =
                match seekReadMultipleMethodSemantics id with
                | [| |] -> None
                | xs -> Some xs.[0]

            and seekReadMethodSemantics id =
                match seekReadOptionalMethodSemantics id with
                | None -> failwith "seekReadMethodSemantics ctxt: no method found"
                | Some x -> x

            and seekReadEvent _numtypars idx =
               let (flags, nameIdx, _typIdx) = seekReadEventRow idx
               { Name = readStringHeap nameIdx
                 //EventHandlerType = seekReadOptionalTypeDefOrRef numtypars AsObject typIdx
                 Attributes = enum<System.Reflection.EventAttributes>(flags)
                 AddMethod= seekReadMethodSemantics (0x0008, TaggedIndex(HasSemanticsTag.Event, idx))
                 RemoveMethod=seekReadMethodSemantics (0x0010, TaggedIndex(HasSemanticsTag.Event, idx))
                 //FireMethod=seekReadOptionalMethodSemantics (0x0020, TaggedIndex(HasSemanticsTag.Event, idx))
                 //OtherMethods = seekReadMultipleMethodSemantics (0x0004, TaggedIndex(HasSemanticsTag.Event, idx))
                 CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Event, idx)) 
                 Token = idx}

            and seekReadEvents numtypars tidx =
               { new ILEventDefs with
                    member __.Entries =
                       match seekReadOptionalIndexedRow (getNumRows ILTableNames.EventMap, (fun i -> i, seekReadEventMapRow i), (fun (_, row) -> fst row), compare tidx, false, (fun (i, row) -> (i, snd row))) with
                       | None -> [| |]
                       | Some (rowNum, beginEventIdx) ->
                           let endEventIdx =
                               if rowNum >= getNumRows ILTableNames.EventMap then
                                   getNumRows ILTableNames.Event + 1
                               else
                                   let (_, endEventIdx) = seekReadEventMapRow (rowNum + 1)
                                   endEventIdx

                           [| for i in beginEventIdx .. endEventIdx - 1 do
                               yield seekReadEvent numtypars i |] }

            and seekReadProperty numtypars idx =
               let (flags, nameIdx, typIdx) = seekReadPropertyRow idx
               let cc, retty, argtys = readBlobHeapAsPropertySig numtypars typIdx
               let setter= seekReadOptionalMethodSemantics (0x0001, TaggedIndex(HasSemanticsTag.Property, idx))
               let getter = seekReadOptionalMethodSemantics (0x0002, TaggedIndex(HasSemanticsTag.Property, idx))
               let cc2 =
                   match getter with
                   | Some mref -> mref.CallingConv.ThisConv
                   | None ->
                       match setter with
                       | Some mref ->  mref.CallingConv .ThisConv
                       | None -> cc
               { Name=readStringHeap nameIdx
                 CallingConv = cc2
                 Attributes = enum<System.Reflection.PropertyAttributes>(flags)
                 SetMethod=setter;
                 GetMethod=getter;
                 PropertyType=retty;
                 Init= if (flags &&& 0x1000) = 0 then None else Some (seekReadConstant (TaggedIndex(HasConstantTag.Property, idx)));
                 IndexParameterTypes=argtys;
                 CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Property, idx))
                 Token = idx }

            and seekReadProperties numtypars tidx =
               { new ILPropertyDefs with
                  member __.Entries =
                       match seekReadOptionalIndexedRow (getNumRows ILTableNames.PropertyMap, (fun i -> i, seekReadPropertyMapRow i), (fun (_, row) -> fst row), compare tidx, false, (fun (i, row) -> (i, snd row))) with
                       | None -> [| |]
                       | Some (rowNum, beginPropIdx) ->
                           let endPropIdx =
                               if rowNum >= getNumRows ILTableNames.PropertyMap then
                                   getNumRows ILTableNames.Property + 1
                               else
                                   let (_, endPropIdx) = seekReadPropertyMapRow (rowNum + 1)
                                   endPropIdx
                           [| for i in beginPropIdx .. endPropIdx - 1 do
                                 yield seekReadProperty numtypars i |] }


            and seekReadCustomAttrs idx =
                { new ILCustomAttrs with
                   member __.Entries =
                       seekReadIndexedRows (getNumRows ILTableNames.CustomAttribute, 
                                              seekReadCustomAttributeRow, (fun (a, _, _) -> a), 
                                              hcaCompare idx, 
                                              isSorted ILTableNames.CustomAttribute, 
                                              (fun (_, b, c) -> seekReadCustomAttr (b, c))) }

            and seekReadCustomAttr (catIdx, valIdx) =
                let data =
                    match readBlobHeapOption valIdx with
                    | USome bytes -> bytes
                    | UNone -> [| |] 
                { Method=seekReadCustomAttrType catIdx;
                  Data= data
                  Elements = [] }

            (*
            and seekReadSecurityDecls idx =
               mkILLazySecurityDecls
                (lazy
                     seekReadIndexedRows (getNumRows ILTableNames.Permission, 
                                             seekReadPermissionRow, 
                                             (fun (_, par, _) -> par), 
                                             hdsCompare idx, 
                                             isSorted ILTableNames.Permission, 
                                             (fun (act, _, ty) -> seekReadSecurityDecl (act, ty))))

            and seekReadSecurityDecl (a, b) =
                ctxt.seekReadSecurityDecl (SecurityDeclIdx (a, b))

            and seekReadSecurityDeclUncached ctxtH (SecurityDeclIdx (act, ty)) =
                PermissionSet ((if List.memAssoc (int act) (Lazy.force ILSecurityActionRevMap) then List.assoc (int act) (Lazy.force ILSecurityActionRevMap) else failwith "unknown security action"), 
                               readBlobHeap ty)

            *)

            and seekReadConstant idx =
              let kind, vidx = seekReadIndexedRow (getNumRows ILTableNames.Constant, 
                                                  seekReadConstantRow, 
                                                  (fun (_, key, _) -> key), 
                                                  hcCompare idx, isSorted ILTableNames.Constant, (fun (kind, _, v) -> kind, v))
              match kind with
              | x when x = uint16 et_STRING ->
                let blobHeap = readBlobHeap vidx
                let s = Encoding.Unicode.GetString(blobHeap, 0, blobHeap.Length)
                box s
              | x when x = uint16 et_BOOLEAN -> box (readBlobHeapAsBool vidx)
              | x when x = uint16 et_CHAR -> box (readBlobHeapAsUInt16 vidx)
              | x when x = uint16 et_I1 -> box (readBlobHeapAsSByte vidx)
              | x when x = uint16 et_I2 -> box (readBlobHeapAsInt16 vidx)
              | x when x = uint16 et_I4 -> box (readBlobHeapAsInt32 vidx)
              | x when x = uint16 et_I8 -> box (readBlobHeapAsInt64 vidx)
              | x when x = uint16 et_U1 -> box (readBlobHeapAsByte vidx)
              | x when x = uint16 et_U2 -> box (readBlobHeapAsUInt16 vidx)
              | x when x = uint16 et_U4 -> box (readBlobHeapAsUInt32 vidx)
              | x when x = uint16 et_U8 -> box (readBlobHeapAsUInt64 vidx)
              | x when x = uint16 et_R4 -> box (readBlobHeapAsSingle vidx)
              | x when x = uint16 et_R8 -> box (readBlobHeapAsDouble vidx)
              | x when x = uint16 et_CLASS || x = uint16 et_OBJECT ->  null
              | _ -> null

            and seekReadManifestResources () =
                ILResources
                  (lazy
                     [| for i = 1 to getNumRows ILTableNames.ManifestResource do
                         let (offset, flags, nameIdx, implIdx) = seekReadManifestResourceRow i
                         let scoref = seekReadImplAsScopeRef implIdx
                         let datalab =
                           match scoref with
                           | ILScopeRef.Local ->
                                ILResourceLocation.Local (fun () ->
                                    // We re-crack the PE file on each resource read, which is a bit dodgy
                                    let bytes = File.ReadAllBytes fileName
                                    let is = ByteFile(bytes)
                                    let pe = PEReader(fileName, is)
                                    let start = pe.ResourcePhysLoc offset
                                    let len = seekReadInt32 is start
                                    seekReadBytes is (start + 4) len)
                           | ILScopeRef.Module mref -> ILResourceLocation.File (mref, offset)
                           | ILScopeRef.Assembly aref -> ILResourceLocation.Assembly aref

                         let r =
                           { Name= readStringHeap nameIdx;
                             Location = datalab;
                             Access = (if (flags &&& 0x01) <> 0x0 then ILResourceAccess.Public else ILResourceAccess.Private);
                             CustomAttrs =  seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ManifestResource, i)) }
                         yield r |])

            and seekReadNestedExportedTypes parentIdx =
                ILNestedExportedTypesAndForwarders
                  (lazy
                     [| for i = 1 to getNumRows ILTableNames.ExportedType do
                           let (flags, _tok, nameIdx, namespaceIdx, implIdx) = seekReadExportedTypeRow i
                           if not (isTopTypeDef flags) then
                               let (TaggedIndex(tag, idx) ) = implIdx
                               match tag with
                               | tag when tag = ImplementationTag.ExportedType && idx = parentIdx  ->
                                   let _nsp, nm = readStringHeapAsTypeName (nameIdx, namespaceIdx)
                                   yield
                                     { Name=nm
                                       Access=(match ILTypeDefAccess.OfFlags flags with ILTypeDefAccess.Nested n -> n | _ -> failwith "non-nested access for a nested type described as being in an auxiliary module")
                                       Nested=seekReadNestedExportedTypes i
                                       CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ExportedType, i)) }
                               | _ -> () |])

            and seekReadTopExportedTypes () =
                ILExportedTypesAndForwarders
                  (lazy
                     [| for i = 1 to getNumRows ILTableNames.ExportedType do
                         let (flags, _tok, nameIdx, namespaceIdx, implIdx) = seekReadExportedTypeRow i
                         if isTopTypeDef flags then
                           let (TaggedIndex(tag, _idx) ) = implIdx

                           // the nested types will be picked up by their enclosing types
                           if tag <> ImplementationTag.ExportedType then
                               let nsp, nm = readStringHeapAsTypeName (nameIdx, namespaceIdx)

                               let scoref = seekReadImplAsScopeRef implIdx

                               let entry =
                                 { ScopeRef=scoref
                                   Namespace=nsp
                                   Name=nm
                                   IsForwarder =   ((flags &&& 0x00200000) <> 0)
                                   Access=ILTypeDefAccess.OfFlags flags
                                   Nested=seekReadNestedExportedTypes i
                                   CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ExportedType, i)) }
                               yield entry |])


            let ilModule = seekReadModule (ilMetadataVersion) 1
            let ilAssemblyRefs = [ for i in 1 .. getNumRows ILTableNames.AssemblyRef do yield seekReadAssemblyRef i ]

            member __.ILGlobals = ilg
            member __.ILModuleDef = ilModule
            member __.ILAssemblyRefs = ilAssemblyRefs

        let sigptr_get_byte (bytes: byte[]) sigptr =
            int bytes.[sigptr], sigptr + 1

        let sigptr_get_u8 bytes sigptr =
            let b0, sigptr = sigptr_get_byte bytes sigptr
            byte b0, sigptr

        let sigptr_get_bool bytes sigptr =
            let b0, sigptr = sigptr_get_byte bytes sigptr
            (b0 = 0x01) , sigptr

        let sigptr_get_i8 bytes sigptr =
            let i, sigptr = sigptr_get_u8 bytes sigptr
            sbyte i, sigptr

        let sigptr_get_u16 bytes sigptr =
            let b0, sigptr = sigptr_get_byte bytes sigptr
            let b1, sigptr = sigptr_get_byte bytes sigptr
            uint16 (b0 ||| (b1 <<< 8)), sigptr

        let sigptr_get_i16 bytes sigptr =
            let u, sigptr = sigptr_get_u16 bytes sigptr
            int16 u, sigptr

        let sigptr_get_i32 bytes sigptr =
            let b0, sigptr = sigptr_get_byte bytes sigptr
            let b1, sigptr = sigptr_get_byte bytes sigptr
            let b2, sigptr = sigptr_get_byte bytes sigptr
            let b3, sigptr = sigptr_get_byte bytes sigptr
            b0 ||| (b1 <<< 8) ||| (b2 <<< 16) ||| (b3 <<< 24), sigptr

        let sigptr_get_u32 bytes sigptr =
            let u, sigptr = sigptr_get_i32 bytes sigptr
            uint32 u, sigptr

        let sigptr_get_i64 bytes sigptr =
            let b0, sigptr = sigptr_get_byte bytes sigptr
            let b1, sigptr = sigptr_get_byte bytes sigptr
            let b2, sigptr = sigptr_get_byte bytes sigptr
            let b3, sigptr = sigptr_get_byte bytes sigptr
            let b4, sigptr = sigptr_get_byte bytes sigptr
            let b5, sigptr = sigptr_get_byte bytes sigptr
            let b6, sigptr = sigptr_get_byte bytes sigptr
            let b7, sigptr = sigptr_get_byte bytes sigptr
            int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) |||
            (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56), 
            sigptr

        let sigptr_get_u64 bytes sigptr =
            let u, sigptr = sigptr_get_i64 bytes sigptr
            uint64 u, sigptr


        let ieee32_of_bits (x:int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes(x), 0)
        let ieee64_of_bits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

        let sigptr_get_ieee32 bytes sigptr =
            let u, sigptr = sigptr_get_i32 bytes sigptr
            ieee32_of_bits u, sigptr

        let sigptr_get_ieee64 bytes sigptr =
            let u, sigptr = sigptr_get_i64 bytes sigptr
            ieee64_of_bits u, sigptr

        let u8AsBytes (i:byte) = [| i |]
        let u16AsBytes x =  let n = (int x) in [| b0 n; b1 n |]
        let i32AsBytes i = [| b0 i; b1 i; b2 i; b3 i |]
        let i64AsBytes i = [| dw0 i; dw1 i; dw2 i; dw3 i; dw4 i; dw5 i; dw6 i; dw7 i |]

        let i8AsBytes (i:sbyte) = u8AsBytes (byte i)
        let i16AsBytes (i:int16) = u16AsBytes (uint16 i)
        let u32AsBytes (i:uint32) = i32AsBytes (int32 i)
        let u64AsBytes (i:uint64) = i64AsBytes (int64 i)
        let bits_of_float32 (x:float32) = BitConverter.ToInt32(BitConverter.GetBytes(x), 0)
        let bits_of_float (x:float) = BitConverter.DoubleToInt64Bits(x)

        let ieee32AsBytes i = i32AsBytes (bits_of_float32 i)
        let ieee64AsBytes i = i64AsBytes (bits_of_float i)


        let (|ElementType|_|) (ty: ILType) =
            match ty with 
            | ILType.Boxed tspec -> 
                match tspec.Namespace, tspec.Name with 
                | USome "System", "String"->  Some  et_STRING
                | USome "System", "Object"->  Some  et_OBJECT
                | _ -> None
            | ILType.Value tspec -> 
                match tspec.Namespace, tspec.Name with
                | USome "System", "Int32" ->  Some  et_I4
                | USome "System", "SByte" ->  Some et_I1
                | USome "System", "Int16"->  Some et_I2 
                | USome "System", "Int64" ->  Some  et_I8
                | USome "System", "IntPtr" ->  Some  et_I
                | USome "System", "Byte" ->  Some et_U1
                | USome "System", "UInt16"->  Some et_U2
                | USome "System", "UInt32" ->  Some  et_U4
                | USome "System", "UInt64" ->  Some  et_U8
                | USome "System", "UIntPtr" ->  Some  et_U
                | USome "System", "Double" ->  Some  et_R8
                | USome "System", "Single" ->  Some  et_R4
                | USome "System", "Char" ->  Some  et_CHAR
                | USome "System", "Boolean" ->  Some  et_BOOLEAN
                | USome "System", "TypedReference" ->  Some  et_TYPEDBYREF
                | _ -> None
            | _ -> None

        let encodeCustomAttrString (s: string) = 
            let arr =  Encoding.UTF8.GetBytes s
            Array.concat [ ByteBuffer.Z32 arr.Length; arr ]      

        let rec encodeCustomAttrElemType x = 
            match x with
            | ILType.Boxed tspec when tspec.Namespace = USome "System" && tspec.Name = "Object" ->  [| 0x51uy |] 
            | ILType.Boxed tspec when tspec.Namespace = USome "System" && tspec.Name = "Type" ->  [| 0x50uy |]
            | ElementType et ->  [| et |]
            | ILType.Value tspec ->  Array.append [| 0x55uy |] (encodeCustomAttrString tspec.TypeRef.QualifiedName)
            | ILType.Array (shape, elemType) when shape = ILArrayShape.SingleDimensional -> 
                  Array.append [| et_SZARRAY |] (encodeCustomAttrElemType elemType)
            | _ ->  failwith "encodeCustomAttrElemType: unrecognized custom element type"

        /// Given a custom attribute element, work out the type of the .NET argument for that element.
        let rec encodeCustomAttrElemTypeForObject (x: obj) = 
            match x with
            | :? string   -> [| et_STRING |]
            | :? bool -> [| et_BOOLEAN |]
            | :? char     -> [| et_CHAR |]
            | :? sbyte    -> [| et_I1 |]
            | :? int16    -> [| et_I2 |]
            | :? int32    -> [| et_I4 |]
            | :? int64    -> [| et_I8 |]
            | :? byte     -> [| et_U1 |]
            | :? uint16   -> [| et_U2 |]
            | :? uint32   -> [| et_U4 |]
            | :? uint64   -> [| et_U8 |]
            | :? ILType     -> [| 0x50uy |]
            | :? Type     -> [| 0x50uy |]
            | null     -> [| et_STRING  |]// yes, the 0xe prefix is used when passing a "null" to a property or argument of type "object" here
            | :? single   -> [| et_R4 |]
            | :? double   -> [| et_R8 |]
            | :? (obj[])   -> failwith "TODO: can't yet emit arrays in attrs" // [| yield et_SZARRAY; yield! encodeCustomAttrElemType elemTy |]
            | _   -> failwith "unexpected value in custom attribute" 

        /// Given a custom attribute element, encode it to a binary representation according to the rules in Ecma 335 Partition II.
        let rec encodeCustomAttrPrimValue (c: obj) = 
            match c with 
            | :? bool as b -> [| (if b then 0x01uy else 0x00uy) |]
            | null -> [| 0xFFuy |]
            | :? string as s -> encodeCustomAttrString s
            | :? char as x ->  u16AsBytes (uint16 x)
            | :? SByte as x -> i8AsBytes x
            | :? Int16 as x -> i16AsBytes x
            | :? Int32 as x -> i32AsBytes x
            | :? Int64 as x -> i64AsBytes x
            | :? Byte as x -> u8AsBytes x
            | :? UInt16 as x -> u16AsBytes x
            | :? UInt32 as x -> u32AsBytes x
            | :? UInt64 as x -> u64AsBytes x
            | :? Single as x -> ieee32AsBytes x
            | :? Double as x -> ieee64AsBytes x
            | :? ILType as ty -> encodeCustomAttrString ty.QualifiedName 
            | :? Type as ty -> encodeCustomAttrString ty.FullName 
            | :? (obj[]) as elems ->  
                 [| yield! i32AsBytes elems.Length; for elem in elems do yield! encodeCustomAttrPrimValue elem |]
            | _ -> failwith "unexpected value in custom attribute"

        and encodeCustomAttrValue ty (c: obj) = 
            match ty, c with 
            | ILType.Boxed tspec, _ when tspec.Namespace = USome "System" && tspec.Name = "Object" ->  
               [| yield! encodeCustomAttrElemTypeForObject c; yield! encodeCustomAttrPrimValue c |]
            | ILType.Array (shape, _), null when shape = ILArrayShape.SingleDimensional ->  
               [| yield! i32AsBytes 0xFFFFFFFF |]
            | ILType.Array (shape, elemType), (:? (obj[]) as elems) when shape = ILArrayShape.SingleDimensional ->  
               [| yield! i32AsBytes elems.Length; for elem in elems do yield! encodeCustomAttrValue elemType elem |]
            | _ -> 
               encodeCustomAttrPrimValue c

        let encodeCustomAttrNamedArg prop (ILCustomAttrNamedArg (nm, ty, elem)) = 
           [| yield (if prop then 0x54uy else 0x53uy) 
              yield! encodeCustomAttrElemType ty
              yield! encodeCustomAttrString nm
              yield! encodeCustomAttrValue ty elem |]

        let mkILCustomAttribMethRef (mspec:ILMethodSpec, fixedArgs: obj list, propArgs: ILCustomAttrNamedArg list, fieldArgs: ILCustomAttrNamedArg list) = 
            let argtys = mspec.MethodRef.ArgTypes
            let nnamed = propArgs.Length + fieldArgs.Length
            let data = 
              [| yield! [| 0x01uy; 0x00uy; |]
                 for (argty, fixedArg) in Seq.zip argtys fixedArgs do
                    yield! encodeCustomAttrValue argty fixedArg
                 yield! u16AsBytes (uint16 nnamed )
                 for arg in propArgs do 
                     yield! encodeCustomAttrNamedArg true arg
                 for arg in fieldArgs do 
                     yield! encodeCustomAttrNamedArg false arg |]
            //printfn "mkILCustomAttribMethRef, nnamed = %d, data.Length = %d, data = %A" nnamed data.Length data
            { Method = mspec;
              Data = data;
              Elements = fixedArgs @ (propArgs |> List.map(fun (ILCustomAttrNamedArg(_, _, e)) -> e)) @ (fieldArgs |> List.map(fun (ILCustomAttrNamedArg(_, _, e)) -> e)) }

        let rec decodeCustomAttrElemType ilg bytes sigptr x =
            match x with
            | x when x = et_I1 -> ilg.typ_SByte, sigptr
            | x when x = et_U1 -> ilg.typ_Byte, sigptr
            | x when x = et_I2 -> ilg.typ_Int16, sigptr
            | x when x = et_U2 -> ilg.typ_UInt16, sigptr
            | x when x = et_I4 -> ilg.typ_Int32, sigptr
            | x when x = et_U4 -> ilg.typ_UInt32, sigptr
            | x when x = et_I8 -> ilg.typ_Int64, sigptr
            | x when x = et_U8 -> ilg.typ_UInt64, sigptr
            | x when x = et_R8 -> ilg.typ_Double, sigptr
            | x when x = et_R4 -> ilg.typ_Single, sigptr
            | x when x = et_CHAR -> ilg.typ_Char, sigptr
            | x when x = et_BOOLEAN -> ilg.typ_Boolean, sigptr
            | x when x = et_STRING -> ilg.typ_String, sigptr
            | x when x = et_OBJECT -> ilg.typ_Object, sigptr
            | x when x = et_SZARRAY ->
                 let et, sigptr = sigptr_get_u8 bytes sigptr
                 let elemTy, sigptr = decodeCustomAttrElemType ilg bytes sigptr et
                 mkILArr1DTy elemTy, sigptr
            | x when x = 0x50uy -> ilg.typ_Type, sigptr
            | _ ->  failwithf "decodeCustomAttrElemType ilg: sigptr = %d, unrecognized custom element type: %A, bytes = %A" sigptr x bytes

        // Parse an IL type signature argument within a custom attribute blob
        type ILTypeSigParser(tstring: string) =

            let mutable startPos = 0
            let mutable currentPos = 0

            //let reset() = startPos <- 0 ; currentPos <- 0
            let nil = '\r' // cannot appear in a type sig

            // take a look at the next value, but don't advance
            let peek() = if currentPos < (tstring.Length-1) then tstring.[currentPos+1] else nil
            let peekN(skip) = if currentPos < (tstring.Length - skip) then tstring.[currentPos+skip] else nil
            // take a look at the current value, but don't advance
            let here() = if currentPos < tstring.Length then tstring.[currentPos] else nil
            // move on to the next character
            let step() = currentPos <- currentPos+1
            // ignore the current lexeme
            let skip() = startPos <- currentPos
            // ignore the current lexeme, advance
            let drop() = skip() ; step() ; skip()
            // return the current lexeme, advance
            let take() =
                let s = if currentPos < tstring.Length then tstring.[startPos..currentPos] else ""
                drop()
                s

            // The format we accept is
            // "<type name>{`<arity>[<type>, +]}{<array rank>}{<scope>}"  E.g., 
            //
            // System.Collections.Generic.Dictionary
            //     `2[
            //         [System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089], 
            //         dev.virtualearth.net.webservices.v1.search.CategorySpecificPropertySet], 
            // mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
            //
            // Note that
            //   • Since we're only reading valid IL, we assume that the signature is properly formed
            //   • For type parameters, if the type is non-local, it will be wrapped in brackets ([])
            member x.ParseType() =

                // Does the type name start with a leading '['?  If so, ignore it
                // (if the specialization type is in another module, it will be wrapped in bracket)
                if here() = '[' then drop()

                // 1. Iterate over beginning of type, grabbing the type name and determining if it's generic or an array
                let typeName =
                    while (peek() <> '`') && (peek() <> '[') && (peek() <> ']') && (peek() <> ',') && (peek() <> nil) do step()
                    take()

                // 2. Classify the type

                // Is the type generic?
                let typeName, specializations =
                    if here() = '`' then
                        drop() // step to the number
                        // fetch the arity
                        let arity =
                            while (int(here()) >= (int('0'))) && (int(here()) <= ((int('9')))) && (int(peek()) >= (int('0'))) && (int(peek()) <= ((int('9')))) do step()
                            System.Int32.Parse(take())

                        // typically types are saturated, i.e. if generic they have arguments. However, assembly metadata for reflectedDefinitions they occur free.
                        // this code takes care of exactly this case.
                        if here () = '[' then
                            // skip the '['
                            drop()
                            // get the specializations
                            typeName+"`"+(arity.ToString()), Some(([| for _i in 0..arity-1 do yield x.ParseType() |]))
                        else
                            typeName+"`"+(arity.ToString()), None
                    else
                        typeName, None

                // Is the type an array?
                let rank =
                    if here() = '[' then
                        let mutable rank = 0

                        while here() <> ']' do
                            rank <- rank + 1
                            step()
                        drop()

                        Some(ILArrayShape(Array.create rank (Some 0, None)))
                    else
                        None

                // Is there a scope?
                let scope =
                    if (here() = ',' || here() = ' ') && (peek() <> '[' && peekN(2) <> '[') then
                        let grabScopeComponent() =
                            if here() = ',' then drop() // ditch the ','
                            if here() = ' ' then drop() // ditch the ' '

                            while (peek() <> ',' && peek() <> ']' && peek() <> nil) do step()
                            take()

                        let scope =
                            [ yield grabScopeComponent() // assembly
                              yield grabScopeComponent() // version
                              yield grabScopeComponent() // culture
                              yield grabScopeComponent() // public key token
                            ] |> String.concat ","
                        ILScopeRef.Assembly(ILAssemblyRef.FromAssemblyName(System.Reflection.AssemblyName(scope)))
                    else
                        ILScopeRef.Local

                // strip any extraneous trailing brackets or commas
                if (here() = ']')  then drop()
                if (here() = ',') then drop()

                // build the IL type
                let tref =
                    let nsp, nm = splitILTypeName typeName
                    ILTypeRef(ILTypeRefScope.Top scope, nsp, nm)

                let genericArgs =
                    match specializations with
                    | None -> [| |]
                    | Some(genericArgs) -> genericArgs
                let tspec = ILTypeSpec(tref, genericArgs)
                let ilty =
                    match tspec.Name with
                    | "System.SByte"
                    | "System.Byte"
                    | "System.Int16"
                    | "System.UInt16"
                    | "System.Int32"
                    | "System.UInt32"
                    | "System.Int64"
                    | "System.UInt64"
                    | "System.Char"
                    | "System.Double"
                    | "System.Single"
                    | "System.Boolean" -> ILType.Value(tspec)
                    | _ -> ILType.Boxed(tspec)

                // if it's an array, wrap it - otherwise, just return the IL type
                match rank with
                | Some(r) -> ILType.Array(r, ilty)
                | _ -> ilty


        let sigptr_get_bytes n (bytes:byte[]) sigptr =
            let res = Array.zeroCreate n
            for i = 0 to n - 1 do
                res.[i] <- bytes.[sigptr + i]
            res, sigptr + n

        let sigptr_get_string n bytes sigptr =
            let intarray, sigptr = sigptr_get_bytes n bytes sigptr
            Encoding.UTF8.GetString(intarray , 0, intarray.Length), sigptr

        let sigptr_get_serstring  bytes sigptr =
            let len, sigptr = sigptrGetZInt32 bytes sigptr
            sigptr_get_string len bytes sigptr

        let sigptr_get_serstring_possibly_null  bytes sigptr =
            let b0, new_sigptr = sigptr_get_byte bytes sigptr
            if b0 = 0xFF then // null case
                None, new_sigptr
            else  // throw away  new_sigptr, getting length & text advance
                let len, sigptr = sigptrGetZInt32 bytes sigptr
                let s, sigptr = sigptr_get_string len bytes sigptr
                Some(s), sigptr

        let decodeILCustomAttribData ilg (ca: ILCustomAttribute) =
            let bytes = ca.Data
            let sigptr = 0
            let bb0, sigptr = sigptr_get_byte bytes sigptr
            let bb1, sigptr = sigptr_get_byte bytes sigptr
            if not (bb0 = 0x01 && bb1 = 0x00) then failwith "decodeILCustomAttribData: invalid data";

            let rec parseVal argty sigptr =
                match argty with
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "SByte" ->
                    let n, sigptr = sigptr_get_i8 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Byte" ->
                    let n, sigptr = sigptr_get_u8 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Int16" ->
                    let n, sigptr = sigptr_get_i16 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "UInt16" ->
                    let n, sigptr = sigptr_get_u16 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Int32" ->
                    let n, sigptr = sigptr_get_i32 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "UInt32" ->
                    let n, sigptr = sigptr_get_u32 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Int64" ->
                    let n, sigptr = sigptr_get_i64 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "UInt64" ->
                    let n, sigptr = sigptr_get_u64 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Double" ->
                    let n, sigptr = sigptr_get_ieee64 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Single" ->
                    let n, sigptr = sigptr_get_ieee32 bytes sigptr
                    (argty, box n), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Char" ->
                    let n, sigptr = sigptr_get_u16 bytes sigptr
                    (argty, box (char n)), sigptr
                | ILType.Value tspec when tspec.Namespace = USome "System" && tspec.Name = "Boolean" ->
                    let n, sigptr = sigptr_get_byte bytes sigptr
                    (argty, box (not (n = 0))), sigptr
                | ILType.Boxed tspec when tspec.Namespace = USome "System" && tspec.Name = "String" ->
                    //printfn "parsing string, sigptr = %d" sigptr
                    let n, sigptr = sigptr_get_serstring_possibly_null bytes sigptr
                    //printfn "got string, sigptr = %d" sigptr
                    (argty, box (match n with None -> null | Some s -> s)), sigptr
                | ILType.Boxed tspec when tspec.Namespace = USome "System" && tspec.Name = "Type" ->
                    let nOpt, sigptr = sigptr_get_serstring_possibly_null bytes sigptr
                    match nOpt with
                    | None -> (argty, box null) , sigptr // TODO: read System.Type attrs
                    | Some n ->
                    try
                        let parser = ILTypeSigParser(n)
                        parser.ParseType() |> ignore
                        (argty, box null) , sigptr // TODO: read System.Type attributes
                    with e ->
                        failwithf "decodeILCustomAttribData: error parsing type in custom attribute blob: %s" e.Message
                | ILType.Boxed tspec when tspec.Namespace = USome "System" && tspec.Name = "Object" ->
                    let et, sigptr = sigptr_get_u8 bytes sigptr
                    if et = 0xFFuy then
                        (argty, null), sigptr
                    else
                        let ty, sigptr = decodeCustomAttrElemType ilg bytes sigptr et
                        parseVal ty sigptr
                | ILType.Array(shape, elemTy) when shape = ILArrayShape.SingleDimensional ->
                    let n, sigptr = sigptr_get_i32 bytes sigptr
                    if n = 0xFFFFFFFF then (argty, null), sigptr else
                    let rec parseElems acc n sigptr =
                        if n = 0 then List.rev acc, sigptr else
                        let v, sigptr = parseVal elemTy sigptr
                        parseElems (v ::acc) (n-1) sigptr
                    let elems, sigptr = parseElems [] n sigptr 
                    let elems = elems |> List.map snd |> List.toArray
                    (argty, box elems), sigptr
                | ILType.Value _ ->  (* assume it is an enumeration *)
                    let n, sigptr = sigptr_get_i32 bytes sigptr
                    (argty, box n), sigptr
                | _ ->  failwith "decodeILCustomAttribData: attribute data involves an enum or System.Type value"

            let rec parseFixed argtys sigptr =
                match argtys with
                | [] -> [], sigptr
                | h::t ->
                    let nh, sigptr = parseVal h sigptr
                    let nt, sigptr = parseFixed t sigptr
                    nh ::nt, sigptr

            let fixedArgs, sigptr = parseFixed (List.ofArray ca.Method.FormalArgTypes) sigptr
            let nnamed, sigptr = sigptr_get_u16 bytes sigptr
            //printfn "nnamed = %d" nnamed

            try
            let rec parseNamed acc n sigptr =
                if n = 0 then List.rev acc else
                let isPropByte, sigptr = sigptr_get_u8 bytes sigptr
                let isProp = (int isPropByte = 0x54)
                let et, sigptr = sigptr_get_u8 bytes sigptr
                // We have a named value
                let ty, sigptr =
                    if ((* 0x50 = (int et) || *) 0x55 = (int et)) then
                        let qualified_tname, sigptr = sigptr_get_serstring bytes sigptr
                        let unqualified_tname, rest =
                            let pieces = qualified_tname.Split(',')
                            if pieces.Length > 1 then
                                pieces.[0], Some (String.concat "," pieces.[1..])
                            else
                                pieces.[0], None
                        let scoref =
                            match rest with
                            | Some aname -> ILTypeRefScope.Top(ILScopeRef.Assembly(ILAssemblyRef.FromAssemblyName(System.Reflection.AssemblyName(aname))))
                            | None -> ilg.typ_Boolean.TypeSpec.Scope

                        let nsp, nm = splitILTypeName unqualified_tname
                        let tref = ILTypeRef (scoref, nsp, nm)
                        let tspec = mkILNonGenericTySpec tref
                        ILType.Value(tspec), sigptr
                    else
                        decodeCustomAttrElemType ilg bytes sigptr et
                let nm, sigptr = sigptr_get_serstring bytes sigptr
                let (_, v), sigptr = parseVal ty sigptr
                parseNamed ((nm, ty, isProp, v) :: acc) (n-1) sigptr
            let named = parseNamed [] (int nnamed) sigptr
            fixedArgs, named

            with err -> 
              failwithf  "FAILED decodeILCustomAttribData, data.Length = %d, data = %A, meth = %A, argtypes = %A, fixedArgs=%A, nnamed = %A, sigptr before named = %A, innerError = %A" bytes.Length bytes ca.Method.EnclosingType ca.Method.FormalArgTypes fixedArgs nnamed sigptr (err.ToString())

        // Share DLLs within a provider by weak-caching them. 
        let readerWeakCache = ConcurrentDictionary<(string * string), DateTime * WeakReference<ILModuleReader>>(HashIdentity.Structural)

        // Share DLLs across providers by strong-caching them, but flushing regularly
        let readerStrongCache = ConcurrentDictionary<(string * string), DateTime * int * ILModuleReader>(HashIdentity.Structural)

        type File with 
            static member ReadBinaryChunk (fileName: string, start, len) = 
                use stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                stream.Seek(int64 start, SeekOrigin.Begin) |> ignore
                let buffer = Array.zeroCreate len 
                let mutable n = 0
                while n < len do 
                    n <- n + stream.Read(buffer, n, len-n)
                buffer

        let createReader ilGlobals (fileName: string) =
            let bytes = File.ReadAllBytes fileName
            let is = ByteFile(bytes)
            let pe = PEReader(fileName, is)
            let mdchunk = File.ReadBinaryChunk (fileName, pe.MetadataPhysLoc, pe.MetadataSize)
            let mdfile = ByteFile(mdchunk)
            let reader = ILModuleReader(fileName, mdfile, ilGlobals, true)
            reader

        let GetWeakReaderCache () = readerWeakCache
        let GetStrongReaderCache () = readerStrongCache

        // Auto-clear the cache every 30.0 seconds.
        // We would use System.Runtime.Caching but some version constraints make this difficult.
        let enableAutoClear = try Environment.GetEnvironmentVariable("FSHARP_TPREADER_AUTOCLEAR_OFF") = null with _ -> true
        let clearSpanDefault = 30000
        let clearSpan = try (match Environment.GetEnvironmentVariable("FSHARP_TPREADER_AUTOCLEAR_SPAN") with null -> clearSpanDefault | s -> int32 s) with _ -> clearSpanDefault
        let lastAccessLock = obj()
        let mutable lastAccess = DateTime.Now

        let StartClearReaderCache() = 
            if enableAutoClear then 
                async {
                    while true do
                        do! Async.Sleep clearSpan
                        let timeSinceLastAccess = DateTime.Now - lock lastAccessLock (fun () -> lastAccess)
                        if timeSinceLastAccess > TimeSpan.FromMilliseconds(float clearSpan) then
                            readerStrongCache.Clear()
                    }
                |> Async.Start

        do StartClearReaderCache()

        let (|WeakReference|_|) (x: WeakReference<'T>) = 
            match x.TryGetTarget() with 
            | true, v -> Some v
            | _ -> None

        let ILModuleReaderAfterReadingAllBytes (file:string, ilGlobals: ILGlobals) =
            let key = (file, ilGlobals.systemRuntimeScopeRef.QualifiedName)
            lock lastAccessLock (fun () -> lastAccess <- DateTime.Now)
            
            // Check the weak cache, to enable sharing within a provider, even if the strong cache is flushed.
            match readerWeakCache.TryGetValue(key) with 
            | true, (currentLastWriteTime, WeakReference(reader)) when 
                    let lastWriteTime = File.GetLastWriteTime(file)
                    currentLastWriteTime = lastWriteTime ->

                reader

            | _ -> 
                let add _ = 
                    let lastWriteTime = File.GetLastWriteTime(file)
                    let reader = createReader ilGlobals file
                    // record in the weak cache, to enable sharing within a provider, even if the strong cache is flushed.
                    readerWeakCache.[key] <-  (lastWriteTime, WeakReference<_>(reader))
                    (lastWriteTime, 1, reader)

                let update _ (currentLastWriteTime, count, reader) =
                    let lastWriteTime = File.GetLastWriteTime(file)
                    if currentLastWriteTime <> lastWriteTime then
                        let reader = createReader ilGlobals file
                        // record in the weak cache, to enable sharing within a provider, even if the strong cache is flushed.
                        readerWeakCache.[key] <-  (lastWriteTime, WeakReference<_>(reader))
                        (lastWriteTime, count + 1, reader)
                    else
                        (lastWriteTime, count, reader)
                let _, _, reader = readerStrongCache.AddOrUpdate(key, add, update)
                reader

        (* NOTE: ecma_ prefix refers to the standard "mscorlib" *)
        let EcmaPublicKey = PublicKeyToken ([|0xdeuy; 0xaduy; 0xbeuy; 0xefuy; 0xcauy; 0xfeuy; 0xfauy; 0xceuy |])
        let EcmaMscorlibScopeRef = ILScopeRef.Assembly (ILAssemblyRef("mscorlib", UNone, USome EcmaPublicKey, true, UNone, UNone))

//====================================================================================================
// TargetAssembly 
//
// An implementation of reflection objects over on-disk assemblies, sufficient to give
// System.Type, System.MethodInfo, System.ConstructorInfo etc. objects
// that can be referred to in quotations and used as backing information for cross-
// targeting F# type providers.


namespace ProviderImplementation.ProvidedTypes

    #nowarn "1182"

    //
    // The on-disk assemblies are read by AssemblyReader.
    //
    // Background
    // ----------
    //
    // Provided type/member definitions need to refer to non-provided definitions like "System.Object" and "System.String".
    //
    // For cross-targeting F# type providers, these can be references to assemblies that can't easily be loaded by .NET
    // reflection. For this reason, an implementation of the .NET reflection objects is needed. At minimum this
    // implementation must support the operations used by the F# compiler to interrogate the reflection objects.
    //
    //     For a System.Assembly, the information must be sufficient to allow the Assembly --> ILScopeRef conversion
    //     in ExtensionTyping.fs of the F# compiler. This requires:
    //         Assembly.GetName()
    //
    //     For a System.Type representing a reference to a named type definition, the information must be sufficient
    //     to allow the Type --> ILTypeRef conversion in the F# compiler. This requires:
    //         typ.DeclaringType
    //         typ.Name
    //         typ.Namespace
    //
    //     For a System.Type representing a type expression, the information must be sufficient to allow the Type --> ILType.Var conversion in the F# compiler.
    //        typeof<System.Void>.Equals(typ)
    //        typ.IsGenericParameter
    //           typ.GenericParameterPosition
    //        typ.IsArray
    //           typ.GetElementType()
    //           typ.GetArrayRank()
    //        typ.IsByRef
    //           typ.GetElementType()
    //        typ.IsPointer
    //           typ.GetElementType()
    //        typ.IsGenericType
    //           typ.GetGenericArguments()
    //           typ.GetGenericTypeDefinition()
    //
    //     For a System.MethodBase --> ILType.ILMethodRef conversion:
    //
    //       :?> MethodInfo as minfo
    //
    //          minfo.IsGenericMethod || minfo.DeclaringType.IsGenericType
    //             minfo.DeclaringType.GetGenericTypeDefinition
    //             minfo.DeclaringType.GetMethods().MetadataToken
    //             minfo.MetadataToken
    //          minfo.IsGenericMethod
    //             minfo.GetGenericArguments().Length
    //          minfo.ReturnType
    //          minfo.GetParameters | .ParameterType
    //          minfo.Name
    //
    //       :?> ConstructorInfo as cinfo
    //
    //          cinfo.DeclaringType.IsGenericType
    //             cinfo.DeclaringType.GetGenericTypeDefinition
    //             cinfo.DeclaringType.GetConstructors() GetParameters | .ParameterType
    //

    #nowarn "40"

    open System
    open System.IO
    open System.Collections.Generic
    open System.Reflection
    open ProviderImplementation.ProvidedTypes.AssemblyReader


    [<AutoOpen>]
    module Utils2 =

        // A table tracking how wrapped type definition objects are translated to cloned objects.
        // Unique wrapped type definition objects must be translated to unique wrapper objects, based
        // on object identity.
        type TxTable<'T2>() =
            let tab = Dictionary<int, 'T2>()
            member __.Get inp f =
                if tab.ContainsKey inp then
                    tab.[inp]
                else
                    let res = f()
                    tab.[inp] <- res
                    res

            member __.ContainsKey inp = tab.ContainsKey inp


        let instParameterInfo typeBuilder inst (inp: ParameterInfo) =
            { new ParameterInfo() with
                override __.Name = inp.Name
                override __.ParameterType = inp.ParameterType |> instType typeBuilder inst
                override __.Attributes = inp.Attributes
                override __.RawDefaultValue = inp.RawDefaultValue
                override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
                override __.ToString() = inp.ToString() + "@inst" }

        let hashILParameterTypes (ps: ILParameters) =
           // This hash code doesn't need to be very good as hashing by name is sufficient to give decent hash granularity
           ps.Length

        let eqILScopeRef (_sco1: ILScopeRef) (_sco2: ILScopeRef) =
            true // TODO (though omitting this is not a problem in practice since type equivalence by name is sufficient to bind methods)

        let eqAssemblyAndILScopeRef (_ass1: Assembly) (_sco2: ILScopeRef) =
            true // TODO (though omitting this is not a problem in practice since type equivalence by name is sufficient to bind methods)


        let rec eqILTypeRef (ty1: ILTypeRef) (ty2: ILTypeRef) =
            ty1.Name = ty2.Name && eqILTypeRefScope ty1.Scope ty2.Scope

        and eqILTypeRefScope (ty1: ILTypeRefScope) (ty2: ILTypeRefScope) =
            match ty1, ty2 with
            | ILTypeRefScope.Top scoref1, ILTypeRefScope.Top scoref2 -> eqILScopeRef scoref1 scoref2
            | ILTypeRefScope.Nested tref1, ILTypeRefScope.Nested tref2 -> eqILTypeRef tref1 tref2
            | _ -> false

        and eqILTypes (tys1: ILType[]) (tys2: ILType[]) =
            lengthsEqAndForall2 tys1 tys2 eqILType

        and eqILType (ty1: ILType) (ty2: ILType) =
            match ty1, ty2 with
            | (ILType.Value(tspec1) | ILType.Boxed(tspec1)), (ILType.Value(tspec2) | ILType.Boxed(tspec2))->
                eqILTypeRef tspec1.TypeRef tspec2.TypeRef && eqILTypes tspec1.GenericArgs tspec2.GenericArgs
            | ILType.Array(rank1, arg1), ILType.Array(rank2, arg2) ->
                rank1 = rank2 && eqILType arg1 arg2
            | ILType.Ptr(arg1), ILType.Ptr(arg2) ->
                eqILType arg1 arg2
            | ILType.Byref(arg1), ILType.Byref(arg2) ->
                eqILType arg1 arg2
            | ILType.Var(arg1), ILType.Var(arg2) ->
                arg1 = arg2
            | _ -> false

        let rec eqTypeAndILTypeRef (ty1: Type) (ty2: ILTypeRef) =
            ty1.Name = ty2.Name &&
            ty1.Namespace = (StructOption.toObj ty2.Namespace) &&
            match ty2.Scope with
            | ILTypeRefScope.Top scoref2 -> eqAssemblyAndILScopeRef ty1.Assembly scoref2
            | ILTypeRefScope.Nested tref2 -> ty1.IsNested && eqTypeAndILTypeRef ty1.DeclaringType tref2

        let rec eqTypesAndILTypes (tys1: Type[]) (tys2: ILType[]) =
            eqTypesAndILTypesWithInst [| |] tys1 tys2

        and eqTypesAndILTypesWithInst inst2 (tys1: Type[]) (tys2: ILType[]) =
            lengthsEqAndForall2 tys1 tys2 (eqTypeAndILTypeWithInst inst2)

        and eqTypeAndILTypeWithInst inst2 (ty1: Type) (ty2: ILType) =
            match ty2 with
            | (ILType.Value(tspec2) | ILType.Boxed(tspec2))->
                if tspec2.GenericArgs.Length > 0 then
                    ty1.IsGenericType && eqTypeAndILTypeRef (ty1.GetGenericTypeDefinition()) tspec2.TypeRef && eqTypesAndILTypesWithInst inst2 (ty1.GetGenericArguments()) tspec2.GenericArgs
                else
                    not ty1.IsGenericType && eqTypeAndILTypeRef ty1 tspec2.TypeRef
            | ILType.Array(rank2, arg2) ->
                ty1.IsArray && ty1.GetArrayRank() = rank2.Rank && eqTypeAndILTypeWithInst inst2 (ty1.GetElementType()) arg2
            | ILType.Ptr(arg2) ->
                ty1.IsPointer && eqTypeAndILTypeWithInst inst2 (ty1.GetElementType()) arg2
            | ILType.Byref(arg2) ->
                ty1.IsByRef && eqTypeAndILTypeWithInst inst2 (ty1.GetElementType()) arg2
            | ILType.Var(arg2) ->
                if int arg2 < inst2.Length then
                     eqTypes ty1 inst2.[int arg2]
                else
                     ty1.IsGenericParameter && ty1.GenericParameterPosition = int arg2

            | _ -> false

        let eqParametersAndILParameterTypesWithInst inst2 (ps1: ParameterInfo[])  (ps2: ILParameters) =
            lengthsEqAndForall2 ps1 ps2 (fun p1 p2 -> eqTypeAndILTypeWithInst inst2 p1.ParameterType p2.ParameterType)


    type MethodSymbol2(gmd: MethodInfo, gargs: Type[], typeBuilder: ITypeBuilder) =
        inherit MethodInfo()
        let dty = gmd.DeclaringType
        let dinst = (if dty.IsGenericType then dty.GetGenericArguments() else [| |])

        override __.Attributes = gmd.Attributes
        override __.Name = gmd.Name
        override __.DeclaringType = dty
        override __.MemberType = gmd.MemberType

        override __.GetParameters() = gmd.GetParameters() |> Array.map (instParameterInfo typeBuilder (dinst, gargs))
        override __.CallingConvention = gmd.CallingConvention
        override __.ReturnType = gmd.ReturnType |> instType typeBuilder (dinst, gargs)
        override __.GetGenericMethodDefinition() = gmd
        override __.IsGenericMethod = gmd.IsGenericMethod
        override __.GetGenericArguments() = gargs
        override __.MetadataToken = gmd.MetadataToken

        override __.GetCustomAttributesData() = gmd.GetCustomAttributesData()
        override __.MakeGenericMethod(typeArgs) = MethodSymbol2(gmd, typeArgs, typeBuilder) :> MethodInfo
        override __.GetHashCode() = gmd.MetadataToken
        override this.Equals(that:obj) =
            match that with
            | :? MethodInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes dty that.DeclaringType && lengthsEqAndForall2 (gmd.GetGenericArguments()) (that.GetGenericArguments()) (=)
            | _ -> false

        
        override this.MethodHandle = notRequired this "MethodHandle" this.Name
        override this.ReturnParameter = notRequired this "ReturnParameter" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name
        override this.ReturnTypeCustomAttributes = notRequired this "ReturnTypeCustomAttributes" this.Name
        override this.GetBaseDefinition() = notRequired this "GetBaseDefinition" this.Name
        override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
        override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override __.GetCustomAttributes(inherited) =
            gmd.GetCustomAttributes(inherited)
        override __.GetCustomAttributes(attributeType, inherited) =
            gmd.GetCustomAttributes(attributeType, inherited)

        override __.ToString() = gmd.ToString() + "@inst"


     /// Represents a constructor in an instantiated type
    type ConstructorSymbol (declTy: Type, inp: ConstructorInfo, typeBuilder: ITypeBuilder) =
        inherit ConstructorInfo() 
        let gps = ((if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]), [| |])

        override __.Name = inp.Name
        override __.Attributes = inp.Attributes
        override __.MemberType = MemberTypes.Constructor
        override __.DeclaringType = declTy

        override __.GetParameters() = inp.GetParameters() |> Array.map (instParameterInfo typeBuilder gps)
        override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
        override __.MetadataToken = inp.MetadataToken

        override __.GetHashCode() = inp.GetHashCode()
        override this.Equals(that:obj) =
            match that with
            | :? ConstructorInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes declTy that.DeclaringType
            | _ -> false

        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined"  this.Name
        override this.Invoke(_invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke"  this.Name
        override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
        override this.MethodHandle = notRequired this "MethodHandle" this.Name
        override this.GetCustomAttributes(inherited) = inp.GetCustomAttributes(inherited)
        override this.GetCustomAttributes(attributeType, inherited) = inp.GetCustomAttributes(attributeType, inherited)

        override __.ToString() = sprintf "tgt constructor(...) in type %s" declTy.FullName 
        static member Make (typeBuilder: ITypeBuilder) (declTy: Type) md = ConstructorSymbol (declTy, md, typeBuilder) :> ConstructorInfo

     /// Represents a method in an instantiated type
    type MethodSymbol (declTy: Type, inp: MethodInfo, typeBuilder: ITypeBuilder) =
        inherit MethodInfo() 
        let gps1 = (if declTy.IsGenericType then declTy.GetGenericArguments() else [| |])
        let gps2 = inp.GetGenericArguments()
        let gps = (gps1, gps2)

        override __.Name = inp.Name
        override __.DeclaringType = declTy
        override __.MemberType = inp.MemberType
        override __.Attributes = inp.Attributes
        override __.GetParameters() = inp.GetParameters() |> Array.map (instParameterInfo typeBuilder gps)
        override __.CallingConvention = inp.CallingConvention
        override __.ReturnType = inp.ReturnType |> instType typeBuilder gps
        override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
        override __.GetGenericArguments() = gps2
        override __.IsGenericMethod = (gps2.Length <> 0)
        override __.IsGenericMethodDefinition = __.IsGenericMethod

        override __.GetHashCode() = inp.GetHashCode()
        override this.Equals(that:obj) =
            match that with
            | :? MethodInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType 
            | _ -> false

        override this.MakeGenericMethod(args) = MethodSymbol2(this, args, typeBuilder) :> MethodInfo

        override __.MetadataToken = inp.MetadataToken

        override this.MethodHandle = notRequired this "MethodHandle" this.Name
        override this.ReturnParameter = notRequired this "ReturnParameter" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name
        override this.ReturnTypeCustomAttributes = notRequired this "ReturnTypeCustomAttributes" this.Name
        override this.GetBaseDefinition() = notRequired this "GetBaseDefinition" this.Name
        override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
        override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override this.GetCustomAttributes(inherited) = inp.GetCustomAttributes(inherited)
        override this.GetCustomAttributes(attributeType, inherited) = inp.GetCustomAttributes(attributeType, inherited)

        override __.ToString() = sprintf "tgt method %s(...) in type %s" inp.Name declTy.FullName  

        static member Make (typeBuilder: ITypeBuilder) (declTy: Type) md = MethodSymbol (declTy, md, typeBuilder) :> MethodInfo

     /// Represents a property in an instantiated type
    type PropertySymbol (declTy: Type, inp: PropertyInfo, typeBuilder: ITypeBuilder) =
        inherit PropertyInfo() 
        let gps = ((if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]), [| |])

        override __.Name = inp.Name
        override __.Attributes = inp.Attributes
        override __.MemberType = MemberTypes.Property
        override __.DeclaringType = declTy

        override __.PropertyType = inp.PropertyType |> instType typeBuilder gps
        override __.GetGetMethod(nonPublic) = inp.GetGetMethod(nonPublic) |> Option.ofObj |> Option.map (MethodSymbol.Make typeBuilder declTy) |> Option.toObj
        override __.GetSetMethod(nonPublic) = inp.GetSetMethod(nonPublic) |> Option.ofObj |> Option.map (MethodSymbol.Make typeBuilder declTy) |> Option.toObj
        override __.GetIndexParameters() = inp.GetIndexParameters() |> Array.map (instParameterInfo typeBuilder gps)
        override __.CanRead = inp.GetGetMethod(true) |> isNull |> not
        override __.CanWrite = inp.GetSetMethod(true) |> isNull |> not
        override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
        override __.MetadataToken = inp.MetadataToken

        override __.GetHashCode() = inp.GetHashCode()
        override this.Equals(that:obj) =
            match that with
            | :? PropertyInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
            | _ -> false

        override this.GetValue(_obj, _invokeAttr, _binder, _index, _culture) = notRequired this "GetValue" this.Name
        override this.SetValue(_obj, _value, _invokeAttr, _binder, _index, _culture) = notRequired this "SetValue" this.Name
        override this.GetAccessors(_nonPublic) = notRequired this "GetAccessors" this.Name
        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name

        override __.ToString() = sprintf "tgt property %s(...) in type %s" inp.Name declTy.Name 

        static member Make (typeBuilder: ITypeBuilder) (declTy: Type) md = PropertySymbol (declTy, md, typeBuilder) :> PropertyInfo

     /// Represents an event in an instantiated type
    type EventSymbol (declTy: Type, inp: EventInfo, typeBuilder: ITypeBuilder) =
        inherit EventInfo()
        let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]

        override __.Name = inp.Name
        override __.Attributes = inp.Attributes
        override __.MemberType = MemberTypes.Event
        override __.DeclaringType = declTy

        override __.EventHandlerType = inp.EventHandlerType |> instType typeBuilder (gps, [| |])
        override __.GetAddMethod(nonPublic) = inp.GetAddMethod(nonPublic) |> Option.ofObj |> Option.map (MethodSymbol.Make typeBuilder declTy) |> Option.toObj
        override __.GetRemoveMethod(nonPublic) = inp.GetRemoveMethod(nonPublic) |> Option.ofObj |> Option.map (MethodSymbol.Make typeBuilder declTy) |> Option.toObj
        override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
        override __.MetadataToken = inp.MetadataToken

        override __.GetHashCode() = inp.GetHashCode()
        override this.Equals(that:obj) =
            match that with
            | :? EventInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
            | _ -> false

        override this.GetRaiseMethod(_nonPublic) = notRequired this "GetRaiseMethod" this.Name
        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name

        override __.ToString() = sprintf "tgt event %s(...) in type %s" inp.Name declTy.FullName 

        static member Make (typeBuilder: ITypeBuilder) (declTy: Type) md = EventSymbol (declTy, md, typeBuilder) :> EventInfo

     /// Represents a field in an instantiated type
    type FieldSymbol (declTy: Type, inp: FieldInfo, typeBuilder: ITypeBuilder) =
        inherit FieldInfo() 
        let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]

        override __.Name = inp.Name
        override __.Attributes = inp.Attributes
        override __.MemberType = MemberTypes.Field
        override __.DeclaringType = declTy

        override __.FieldType = inp.FieldType |> instType typeBuilder (gps, [| |])
        override __.GetRawConstantValue() = inp.GetRawConstantValue()
        override __.GetCustomAttributesData() = inp.GetCustomAttributesData()
        override __.MetadataToken = inp.MetadataToken

        override __.GetHashCode() = inp.GetHashCode()
        override this.Equals(that:obj) =
            match that with
            | :? FieldInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
            | _ -> false

        override this.ReflectedType = notRequired this "ReflectedType" this.Name
        override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name
        override this.SetValue(_obj, _value, _invokeAttr, _binder, _culture) = notRequired this "SetValue" this.Name
        override this.GetValue(_obj) = notRequired this "GetValue" this.Name
        override this.FieldHandle = notRequired this "FieldHandle" this.Name

        override __.ToString() = sprintf "tgt literal field %s(...) in type %s" inp.Name declTy.FullName 

        static member Make (typeBuilder: ITypeBuilder) (declTy: Type) md = FieldSymbol (declTy, md, typeBuilder) :> FieldInfo

    /// Represents the type constructor in a provided symbol type.
    [<RequireQualifiedAccess>]
    type TypeSymbolKind =
        | SDArray
        | Array of int
        | Pointer
        | ByRef
        | TargetGeneric of TargetTypeDefinition
        | OtherGeneric of Type


    /// Represents an array or other symbolic type involving a provided type as the argument.
    /// See the type provider spec for the methods that must be implemented.
    /// Note that the type provider specification does not require us to implement pointer-equality for provided types.
    and TypeSymbol(kind: TypeSymbolKind, typeArgs: Type[], typeBuilder: ITypeBuilder) as this =
        inherit TypeDelegator()
        do this.typeImpl <- this

        override this.FullName =
            if this.IsArray then this.GetElementType().FullName + "[]"
            elif this.IsPointer  then this.GetElementType().FullName + "*"
            elif this.IsByRef   then this.GetElementType().FullName + "&"
            elif this.IsGenericType then this.GetGenericTypeDefinition().FullName + "[" + (this.GetGenericArguments() |> Array.map (fun arg -> arg.FullName) |> String.concat ",") + "]"
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.DeclaringType =
            if this.IsArray || this.IsPointer || this.IsByRef then this.GetElementType().DeclaringType
            elif this.IsGenericType then this.GetGenericTypeDefinition().DeclaringType
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.Name =
            if this.IsArray then this.GetElementType().Name + "[]"
            elif this.IsPointer  then this.GetElementType().Name + "*"
            elif this.IsByRef   then this.GetElementType().Name + "&"
            elif this.IsGenericType then this.GetGenericTypeDefinition().Name
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.BaseType =
            if this.IsArray then typeof<System.Array>
            elif this.IsPointer  then typeof<System.ValueType>
            elif this.IsByRef   then typeof<System.ValueType>
            elif this.IsGenericType then instType typeBuilder (this.GetGenericArguments(), [| |])  (this.GetGenericTypeDefinition().BaseType)
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.MetadataToken =
            if this.IsArray then typeof<System.Array>.MetadataToken
            elif this.IsPointer  then typeof<System.ValueType>.MetadataToken
            elif this.IsByRef   then typeof<System.ValueType>.MetadataToken
            elif this.IsGenericType then this.GetGenericTypeDefinition().MetadataToken
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.Assembly =
            if this.IsArray || this.IsPointer || this.IsByRef then this.GetElementType().Assembly
            elif this.IsGenericType then this.GetGenericTypeDefinition().Assembly
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override this.Namespace =
            if this.IsArray || this.IsPointer || this.IsByRef then this.GetElementType().Namespace
            elif this.IsGenericType then this.GetGenericTypeDefinition().Namespace
            else failwithf "unreachable, stack trace = %A" Environment.StackTrace

        override __.GetArrayRank() = (match kind with TypeSymbolKind.Array n -> n | TypeSymbolKind.SDArray -> 1 | _ -> failwithf "non-array type")
        override __.IsValueTypeImpl() = this.IsGenericType && this.GetGenericTypeDefinition().IsValueType
        override __.IsArrayImpl() = (match kind with TypeSymbolKind.Array _ | TypeSymbolKind.SDArray -> true | _ -> false)
        override __.IsByRefImpl() = (match kind with TypeSymbolKind.ByRef _ -> true | _ -> false)
        override __.IsPointerImpl() = (match kind with TypeSymbolKind.Pointer _ -> true | _ -> false)
        override __.IsPrimitiveImpl() = false
        override __.IsGenericType = (match kind with TypeSymbolKind.TargetGeneric _ | TypeSymbolKind.OtherGeneric _ -> true | _ -> false)
        override __.GetGenericArguments() = (match kind with TypeSymbolKind.TargetGeneric _ |  TypeSymbolKind.OtherGeneric _ -> typeArgs | _ -> [| |])
        override __.GetGenericTypeDefinition() = (match kind with TypeSymbolKind.TargetGeneric e -> (e :> Type) | TypeSymbolKind.OtherGeneric gtd -> gtd | _ -> failwithf "non-generic type")
        override __.IsCOMObjectImpl() = false
        override __.HasElementTypeImpl() = (match kind with TypeSymbolKind.TargetGeneric _ | TypeSymbolKind.OtherGeneric _ -> false | _ -> true)
        override __.GetElementType() = (match kind, typeArgs with (TypeSymbolKind.Array _  | TypeSymbolKind.SDArray | TypeSymbolKind.ByRef | TypeSymbolKind.Pointer), [| e |] -> e | _ -> failwithf "%A, %A: not an array, pointer or byref type" kind typeArgs)

        override x.Module = x.Assembly.ManifestModule

        override this.GetHashCode()                                                                    =
            if this.IsArray then 10 + hash (this.GetElementType())
            elif this.IsPointer then 163 + hash (this.GetElementType())
            elif this.IsByRef then 283 + hash (this.GetElementType())
            else this.GetGenericTypeDefinition().MetadataToken

        override this.Equals(other: obj) = eqTypeObj this other

        override this.Equals(otherTy: Type) = eqTypes this otherTy

        override this.IsAssignableFrom(otherTy: Type) = isAssignableFrom this otherTy

        override this.IsSubclassOf(otherTy: Type) = isSubclassOf this otherTy

        member __.Kind = kind
        member __.Args = typeArgs

        override this.GetConstructors bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.Methods.Entries 
                |> Array.filter (fun md -> md.Name = ".ctor" || md.Name = ".cctor")  
                |> Array.map (gtd.MakeConstructorInfo this) 
                |> Array.filter (canBindConstructor bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetConstructors(bindingFlags) 
                |> Array.map (ConstructorSymbol.Make typeBuilder this) 
            | _ -> notRequired this "GetConstructors" this.Name

        override this.GetMethods bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.Methods.Entries 
                |> Array.filter (fun md -> md.Name <> ".ctor" && md.Name <> ".cctor")  
                |> Array.map (gtd.MakeMethodInfo this) 
                |> Array.filter (canBindMethod bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetMethods(bindingFlags) 
                |> Array.map (MethodSymbol.Make typeBuilder this) 
            | _ -> notRequired this "GetMethods" this.Name

        override this.GetFields bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.Fields.Entries 
                |> Array.map (gtd.MakeFieldInfo this) 
                |> Array.filter (canBindField bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetFields(bindingFlags) 
                |> Array.map (FieldSymbol.Make typeBuilder this) 
            | _ -> notRequired this "GetFields" this.Name

        override this.GetProperties bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.Properties.Entries 
                |> Array.map (gtd.MakePropertyInfo this) 
                |> Array.filter (canBindProperty bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetProperties(bindingFlags) 
                |> Array.map (PropertySymbol.Make typeBuilder this) 
            | _ -> notRequired this "GetProperties" this.Name

        override this.GetEvents bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.Events.Entries 
                |> Array.map (gtd.MakeEventInfo this) 
                |> Array.filter (canBindEvent bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetEvents(bindingFlags) 
                |> Array.map (EventSymbol.Make typeBuilder this) 
            | _ -> notRequired this "GetEvents" this.Name

        override this.GetNestedTypes bindingFlags = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd -> 
                gtd.Metadata.NestedTypes.Entries 
                |> Array.map (gtd.MakeNestedTypeInfo this) 
                |> Array.filter (canBindNestedType bindingFlags)
            | TypeSymbolKind.OtherGeneric gtd -> 
                gtd.GetNestedTypes(bindingFlags) 
            | _ -> notRequired this "GetNestedTypes" this.Name

        override this.GetConstructorImpl(bindingFlags, _binderBinder, _callConvention, types, _modifiers) =
            let ctors = this.GetConstructors(bindingFlags) |> Array.filter (fun c -> match types with null -> true | t -> let ps = c.GetParameters() in ps.Length = t.Length && (ps, t) ||> Seq.forall2 (fun p ty -> p.ParameterType = ty ) )
            match ctors with
            | [| |] -> null
            | [| ci |] -> ci
            | _ -> failwithf "multiple constructors exist" 

        override this.GetMethodImpl(name, bindingFlags, _binderBinder, _callConvention, types, _modifiers) =
            match kind with
            | TypeSymbolKind.TargetGeneric gtd ->
                let md =
                    match types with
                    | null -> gtd.Metadata.Methods.TryFindUniqueByName(name) 
                    | _ ->
                        let mds = gtd.Metadata.Methods.FindByNameAndArity(name, types.Length) 
                        match mds |> Array.filter (fun md -> eqTypesAndILTypesWithInst typeArgs types md.ParameterTypes) with
                        | [| |] -> None
                        | [| md |] -> Some md
                        | _ -> failwithf "multiple methods exist with name %s" name
                md |> Option.map (gtd.MakeMethodInfo this) |> Option.toObj
            | TypeSymbolKind.OtherGeneric _ -> 
                match this.GetMethods(bindingFlags) |> Array.filter (fun c -> name = c.Name && match types with null -> true | t -> c.GetParameters().Length = t.Length) with
                | [| |] -> null
                | [| mi |] -> mi
                | _ -> failwithf "multiple methods exist with name %s" name
            | _ -> notRequired this "GetMethodImpl" this.Name

        override this.GetField(name, bindingFlags) = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd ->
                gtd.Metadata.Fields.Entries |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (gtd.MakeFieldInfo this) 
                |> Option.toObj
            | TypeSymbolKind.OtherGeneric gtd ->
                gtd.GetFields(bindingFlags) 
                |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (FieldSymbol.Make typeBuilder this) 
                |> Option.toObj

            | _ -> notRequired this "GetField" this.Name

        override this.GetPropertyImpl(name, bindingFlags, _binder, _returnType, _types, _modifiers) = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd ->
                gtd.Metadata.Properties.Entries
                |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (gtd.MakePropertyInfo this) 
                |> Option.toObj
            | TypeSymbolKind.OtherGeneric gtd ->
                gtd.GetProperties(bindingFlags) 
                |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (PropertySymbol.Make typeBuilder this) 
                |> Option.toObj

            | _ -> notRequired this "GetPropertyImpl" this.Name

        override this.GetEvent(name, bindingFlags) = 
            match kind with
            | TypeSymbolKind.TargetGeneric gtd ->
                gtd.Metadata.Events.Entries
                |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (gtd.MakeEventInfo this) 
                |> Option.toObj
            | TypeSymbolKind.OtherGeneric gtd ->
                gtd.GetEvents(bindingFlags) 
                |> Array.tryFind (fun md -> md.Name = name)
                |> Option.map (EventSymbol.Make typeBuilder this) 
                |> Option.toObj
            | _ -> notRequired this "GetEvent" this.Name

        override this.GetNestedType(_name, _bindingFlags) = notRequired this "GetNestedType" this.Name

        override this.AssemblyQualifiedName = "[" + this.Assembly.FullName + "]" + this.FullName

        override this.GetAttributeFlagsImpl() = getAttributeFlagsImpl this

        override this.UnderlyingSystemType = (this :> Type)

        override __.GetCustomAttributesData() =  ([| |] :> IList<_>)

        override this.GetMembers _bindingFlags = notRequired this "GetMembers" this.Name
        override this.GetInterface(_name, _ignoreCase) = notRequired this "GetInterface" this.Name
        override this.GetInterfaces() = notRequired this "GetInterfaces" this.Name
        override __.GetCustomAttributes(_inherit) = emptyAttributes
        override __.GetCustomAttributes(attributeType, _inherit) = Attributes.CreateEmpty attributeType
        override __.IsDefined(_attributeType, _inherit) = false

        override this.MemberType =
            match kind with
            | TypeSymbolKind.OtherGeneric gtd -> gtd.MemberType
            | _ -> notRequired this "MemberType" this.FullName
            
#if NETCOREAPP
        // See bug https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/236
        override __.IsSZArray =
            match kind with
            | TypeSymbolKind.SDArray _ -> true
            | _ -> false
#endif
        override this.GetMember(_name, _mt, _bindingFlags) = notRequired this "GetMember" this.Name
        override this.GUID = notRequired this "GUID" this.Name
        override this.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired this "InvokeMember" this.Name
        override this.MakeArrayType() = TypeSymbol(TypeSymbolKind.SDArray, [| this |], typeBuilder) :> Type
        override this.MakeArrayType arg = TypeSymbol(TypeSymbolKind.Array arg, [| this |], typeBuilder) :> Type
        override this.MakePointerType() = TypeSymbol(TypeSymbolKind.Pointer, [| this |], typeBuilder) :> Type
        override this.MakeByRefType() = TypeSymbol(TypeSymbolKind.ByRef, [| this |], typeBuilder) :> Type

        override this.GetEvents() = this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating
        override this.ToString() = this.FullName


        /// Convert an ILGenericParameterDef read from a binary to a System.Type.
    and TargetGenericParam (asm, gpsf, pos, inp: ILGenericParameterDef, txILType, txCustomAttributesData, typeBuilder: ITypeBuilder) as this =
        inherit TypeDelegator() 
        do this.typeImpl <- this
        override __.Name = inp.Name
        override __.Assembly = (asm :> Assembly)
        override __.FullName = inp.Name
        override __.IsGenericParameter = true
        override __.GenericParameterPosition = pos
        override __.GetGenericParameterConstraints() = inp.Constraints |> Array.map (txILType (gpsf()))

        override __.MemberType = enum 0
        override __.MetadataToken = inp.Token

        override __.Namespace = null //notRequired this "Namespace"
        override this.DeclaringType = notRequired this "DeclaringType" this.Name
        override __.BaseType = null //notRequired this "BaseType" this.Name
        override this.GetInterfaces() = notRequired this "GetInterfaces" this.Name

        override this.GetConstructors(_bindingFlags) = notRequired this "GetConstructors" this.Name
        override this.GetMethods(_bindingFlags) = notRequired this "GetMethods" this.Name
        override this.GetFields(_bindingFlags) = notRequired this "GetFields" this.Name
        override this.GetProperties(_bindingFlags) = notRequired this "GetProperties" this.Name
        override this.GetEvents(_bindingFlags) = notRequired this "GetEvents" this.Name
        override this.GetNestedTypes(_bindingFlags) = notRequired this "GetNestedTypes" this.Name

        override this.GetConstructorImpl(_bindingFlags, _binder, _callConvention, _types, _modifiers) = notRequired this "GetConstructorImpl" this.Name
        override this.GetMethodImpl(_name, _bindingFlags, _binder, _callConvention, _types, _modifiers) = notRequired this "GetMethodImpl" this.Name
        override this.GetField(_name, _bindingFlags) = notRequired this "GetField" this.Name
        override this.GetPropertyImpl(_name, _bindingFlags, _binder, _returnType, _types, _modifiers) = notRequired this "GetPropertyImpl" this.Name
        override this.GetNestedType(_name, _bindingFlags) = notRequired this "GetNestedType" this.Name
        override this.GetEvent(_name, _bindingFlags) = notRequired this "GetEvent" this.Name
        override this.GetMembers(_bindingFlags) = notRequired this "GetMembers" this.Name
        override this.MakeGenericType(_args) = notRequired this "MakeGenericType" this.Name

        override this.MakeArrayType() = TypeSymbol(TypeSymbolKind.SDArray, [| this |], typeBuilder) :> Type
        override this.MakeArrayType arg = TypeSymbol(TypeSymbolKind.Array arg, [| this |], typeBuilder) :> Type
        override this.MakePointerType() = TypeSymbol(TypeSymbolKind.Pointer, [| this |], typeBuilder) :> Type
        override this.MakeByRefType() = TypeSymbol(TypeSymbolKind.ByRef, [| this |], typeBuilder) :> Type

        override __.GetAttributeFlagsImpl() = TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Sealed

        override __.IsArrayImpl() = false
        override __.IsByRefImpl() = false
        override __.IsPointerImpl() = false
        override __.IsPrimitiveImpl() = false
        override __.IsCOMObjectImpl() = false
        override __.IsGenericType = false
        override __.IsGenericTypeDefinition = false

        override __.HasElementTypeImpl() = false

        override this.UnderlyingSystemType = (this :> Type)
        override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData

        override this.Equals(that:obj) = System.Object.ReferenceEquals (this, that)
        override this.GetHashCode() = hash inp.Name

        override __.ToString() = sprintf "tgt generic param %s" inp.Name

        override this.AssemblyQualifiedName = "[" + this.Assembly.FullName + "]" + this.FullName

        override this.GetGenericArguments() = notRequired this "GetGenericArguments" this.Name
        override this.GetGenericTypeDefinition() = notRequired this "GetGenericTypeDefinition" this.Name
        override this.GetMember(_name, _mt, _bindingFlags) = notRequired this "txILGenericParam: GetMember" this.Name
        override this.GUID = notRequired this "txILGenericParam: GUID" this.Name
        override this.GetCustomAttributes(_inherited) = notRequired this "txILGenericParam: GetCustomAttributes" this.Name
        override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "txILGenericParam: GetCustomAttributes" this.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "txILGenericParam: IsDefined" this.Name
        override this.GetInterface(_name, _ignoreCase) = notRequired this "txILGenericParam: GetInterface" this.Name
        override this.Module = notRequired this "txILGenericParam: Module" this.Name: Module 
        override this.GetElementType() = notRequired this "txILGenericParam: GetElementType" this.Name
        override this.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired this "txILGenericParam: InvokeMember" this.Name
        override this.GetEvents() = this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating

    /// Clones namespaces, type providers, types and members provided by tp, renaming namespace nsp1 into namespace nsp2.

    /// Makes a type definition read from a binary available as a System.Type. Not all methods are implemented.
    and TargetTypeDefinition(ilGlobals: ILGlobals, tryBindAssembly: ILAssemblyRef -> Choice<Assembly, exn>, asm: TargetAssembly, declTyOpt: Type option, inp: ILTypeDef, typeBuilder: ITypeBuilder) as this =
        inherit TypeDelegator()

        // Note: For F# type providers we never need to view the custom attributes
        let rec txCustomAttributesArg ((ty:ILType, v:obj)) =
            CustomAttributeTypedArgument(txILType ([| |], [| |]) ty, v)

        and txCustomAttributesDatum (inp: ILCustomAttribute) =
             let args, namedArgs = decodeILCustomAttribData ilGlobals inp
             { new CustomAttributeData () with
                member __.Constructor =  txILConstructorRef inp.Method.MethodRef
                member __.ConstructorArguments = [| for arg in args -> txCustomAttributesArg arg |] :> IList<_>
                // Note, named arguments of custom attributes are not required by F# compiler on binding context elements.
                member __.NamedArguments = [| |] :> IList<_>
             }

        and txCustomAttributesData (inp: ILCustomAttrs) =
             [| for a in inp.Entries do
                  yield txCustomAttributesDatum a |]
             :> IList<CustomAttributeData>

        /// Makes a parameter definition read from a binary available as a ParameterInfo. Not all methods are implemented.
        and txILParameter gps (inp: ILParameter) =
            { new ParameterInfo() with

                override __.Name = StructOption.toObj inp.Name
                override __.ParameterType = inp.ParameterType |> txILType gps
                override __.RawDefaultValue = (match inp.Default with UNone -> null | USome v -> v)
                override __.Attributes = inp.Attributes
                override __.GetCustomAttributesData() = inp.CustomAttrs  |> txCustomAttributesData

                override x.ToString() = sprintf "tgt parameter %s" x.Name }

        /// Makes a method definition read from a binary available as a ConstructorInfo. Not all methods are implemented.
        and txILConstructorDef (declTy: Type) (inp: ILMethodDef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            { new ConstructorInfo() with

                override __.Name = inp.Name
                override __.Attributes = inp.Attributes
                override __.MemberType = MemberTypes.Constructor
                override __.DeclaringType = declTy

                override __.GetParameters() = inp.Parameters |> Array.map (txILParameter (gps, [| |]))
                override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData
                override __.MetadataToken = inp.Token

                override __.GetHashCode() = inp.Token

                override this.Equals(that:obj) =
                    match that with
                    | :? ConstructorInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes declTy that.DeclaringType
                    | _ -> false

                override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined"  this.Name
                override this.Invoke(_invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke"  this.Name
                override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
                override this.ReflectedType = notRequired this "ReflectedType" this.Name
                override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
                override this.MethodHandle = notRequired this "MethodHandle" this.Name
                override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name

                override __.ToString() = sprintf "tgt constructor(...) in type %s" declTy.FullName }

        /// Makes a method definition read from a binary available as a MethodInfo. Not all methods are implemented.
        and txILMethodDef (declTy: Type) (inp: ILMethodDef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            let rec gps2 = inp.GenericParams |> Array.mapi (fun i gp -> txILGenericParam (fun () -> gps, gps2) (i + gps.Length) gp)
            let mutable returnTypeFixCache = None
            { new MethodInfo() with

                override __.Name = inp.Name
                override __.DeclaringType = declTy
                override __.MemberType = MemberTypes.Method
                override __.Attributes = inp.Attributes
                override __.GetParameters() = inp.Parameters |> Array.map (txILParameter (gps, gps2))
                override __.CallingConvention = if inp.IsStatic then CallingConventions.Standard else CallingConventions.HasThis ||| CallingConventions.Standard

                override __.ReturnType = 
                    match returnTypeFixCache with 
                    | None -> 
                        let returnType = inp.Return.Type |> txILType (gps, gps2)
                        let returnTypeFix =
                            match returnType.Namespace, returnType.Name with 
                            | "System", "Void"->  
                                if ImportProvidedMethodBaseAsILMethodRef_OnStack_HACK() then 
                                    typeof<Void>
                                else 
                                    returnType
                            | t -> returnType
                        returnTypeFixCache <- Some returnTypeFix
                        returnTypeFix
                    | Some returnTypeFix ->
                        returnTypeFix

                override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData
                override __.GetGenericArguments() = gps2 
                override __.IsGenericMethod = (gps2.Length <> 0)
                override __.IsGenericMethodDefinition = __.IsGenericMethod

                override __.GetHashCode() = inp.Token

                override this.Equals(that:obj) =
                    match that with
                    | :? MethodInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType 
                    | _ -> false

                override this.MakeGenericMethod(args) = MethodSymbol2(this, args, typeBuilder) :> MethodInfo

                override __.MetadataToken = inp.Token

                // unused
                override this.MethodHandle = notRequired this "MethodHandle" this.Name
                override this.ReturnParameter = notRequired this "ReturnParameter" this.Name
                override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name
                override this.ReturnTypeCustomAttributes = notRequired this "ReturnTypeCustomAttributes" this.Name
                override this.GetBaseDefinition() = notRequired this "GetBaseDefinition" this.Name
                override this.GetMethodImplementationFlags() = notRequired this "GetMethodImplementationFlags" this.Name
                override this.Invoke(_obj, _invokeAttr, _binder, _parameters, _culture) = notRequired this "Invoke" this.Name
                override this.ReflectedType = notRequired this "ReflectedType" this.Name
                override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name

                override __.ToString() = sprintf "tgt method %s(...) in type %s" inp.Name declTy.FullName  }

        /// Makes a property definition read from a binary available as a PropertyInfo. Not all methods are implemented.
        and txILPropertyDef (declTy: Type) (inp: ILPropertyDef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            { new PropertyInfo() with

                override __.Name = inp.Name
                override __.Attributes = inp.Attributes
                override __.MemberType = MemberTypes.Property
                override __.DeclaringType = declTy

                override __.PropertyType = inp.PropertyType |> txILType (gps, [| |])
                override __.GetGetMethod(_nonPublic) = inp.GetMethod |> Option.map (txILMethodRef declTy) |> Option.toObj
                override __.GetSetMethod(_nonPublic) = inp.SetMethod |> Option.map (txILMethodRef declTy) |> Option.toObj
                override __.GetIndexParameters() = inp.IndexParameters |> Array.map (txILParameter (gps, [| |]))
                override __.CanRead = inp.GetMethod.IsSome
                override __.CanWrite = inp.SetMethod.IsSome
                override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData
                override __.MetadataToken = inp.Token

                override __.GetHashCode() = inp.Token

                override this.Equals(that:obj) =
                    match that with
                    | :? PropertyInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
                    | _ -> false

                override this.GetValue(_obj, _invokeAttr, _binder, _index, _culture) = notRequired this "GetValue" this.Name
                override this.SetValue(_obj, _value, _invokeAttr, _binder, _index, _culture) = notRequired this "SetValue" this.Name
                override this.GetAccessors(nonPublic) = notRequired this "GetAccessors" this.Name
                override this.ReflectedType = notRequired this "ReflectedType" this.Name
                override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name

                override __.ToString() = sprintf "tgt property %s(...) in type %s" inp.Name declTy.Name }

        /// Make an event definition read from a binary available as an EventInfo. Not all methods are implemented.
        and txILEventDef (declTy: Type) (inp: ILEventDef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            { new EventInfo() with

                override __.Name = inp.Name
                override __.Attributes = inp.Attributes
                override __.MemberType = MemberTypes.Event
                override __.DeclaringType = declTy

                override __.EventHandlerType = inp.EventHandlerType |> txILType (gps, [| |])
                override __.GetAddMethod(_nonPublic) = inp.AddMethod |> txILMethodRef declTy
                override __.GetRemoveMethod(_nonPublic) = inp.RemoveMethod |> txILMethodRef declTy
                override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData
                override __.MetadataToken = inp.Token

                override __.GetHashCode() = inp.Token

                override this.Equals(that:obj) =
                    match that with
                    | :? EventInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
                    | _ -> false

                override this.GetRaiseMethod(_nonPublic) = notRequired this "GetRaiseMethod" this.Name
                override this.ReflectedType = notRequired this "ReflectedType" this.Name
                override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name

                override __.ToString() = sprintf "tgt event %s(...) in type %s" inp.Name declTy.FullName }

        /// Makes a field definition read from a binary available as a FieldInfo. Not all methods are implemented.
        and txILFieldDef (declTy: Type) (inp: ILFieldDef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            { new FieldInfo() with

                override __.Name = inp.Name
                override __.Attributes = inp.Attributes
                override __.MemberType = MemberTypes.Field
                override __.DeclaringType = declTy

                override __.FieldType = inp.FieldType |> txILType (gps, [| |])
                override __.GetRawConstantValue() = match inp.LiteralValue with None -> null | Some v -> v
                override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData
                override __.MetadataToken = inp.Token

                override __.GetHashCode() = inp.Token

                override this.Equals(that:obj) =
                    match that with
                    | :? FieldInfo as that -> this.MetadataToken = that.MetadataToken && eqTypes this.DeclaringType that.DeclaringType
                    | _ -> false

                override this.ReflectedType = notRequired this "ReflectedType" this.Name
                override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" this.Name
                override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" this.Name
                override this.SetValue(_obj, _value, _invokeAttr, _binder, _culture) = notRequired this "SetValue" this.Name
                override this.GetValue(_obj) = notRequired this "GetValue" this.Name
                override this.FieldHandle = notRequired this "FieldHandle" this.Name

                override __.ToString() = sprintf "tgt literal field %s(...) in type %s" inp.Name declTy.FullName }

        /// Bind a reference to an assembly
        and txScopeRef(sref: ILScopeRef) =
            match sref with
            | ILScopeRef.Assembly aref -> match tryBindAssembly aref with Choice1Of2 asm -> asm | Choice2Of2 exn -> raise exn
            | ILScopeRef.Local -> (asm :> Assembly)
            | ILScopeRef.Module _ -> (asm :> Assembly)

        /// Bind a reference to a type
        and txILTypeRef(tref: ILTypeRef): Type =
            match tref.Scope with
            | ILTypeRefScope.Top scoref -> txScopeRef(scoref).GetType(joinILTypeName tref.Namespace tref.Name)
            | ILTypeRefScope.Nested encl -> txILTypeRef(encl).GetNestedType(tref.Name, bindAll)

        /// Bind a reference to a constructor
        and txILConstructorRef (mref: ILMethodRef) =
            let declTy = txILTypeRef(mref.EnclosingTypeRef)
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            let argTypes = Array.map (txILType (gps, [| |])) mref.ArgTypes
            let cons = declTy.GetConstructor(bindAll, null, argTypes, null)
            if isNull cons then failwithf "constructor reference '%A' not resolved" mref
            cons

        /// Bind a reference to a method
        and txILMethodRef (declTy: Type) (mref: ILMethodRef) =
            let gps = if declTy.IsGenericType then declTy.GetGenericArguments() else [| |]
            let argTypes = mref.ArgTypes |> Array.map (txILType (gps, [| |]))
            let meth = declTy.GetMethod(mref.Name, bindAll, null, argTypes, null)
            if isNull meth then failwithf "method reference '%A' not resolved" mref
            meth

        /// Convert an ILType read from a binary to a System.Type backed by TargetTypeDefinitions
        and txILType gps (ty: ILType) =

            match ty with
            | ILType.Void -> txILType gps ilGlobals.typ_Void
            | ILType.Value tspec
            | ILType.Boxed tspec ->
                let tdefR = txILTypeRef tspec.TypeRef
                match tspec.GenericArgs with
                | [| |] -> tdefR
                | args -> tdefR.MakeGenericType(Array.map (txILType gps) args)
            | ILType.Array(rank, arg) ->
                let argR = txILType gps arg
                if rank.Rank = 1 then argR.MakeArrayType()
                else argR.MakeArrayType(rank.Rank)
            | ILType.FunctionPointer _  -> failwith "unexpected function type"
            | ILType.Ptr(arg) -> (txILType gps arg).MakePointerType()
            | ILType.Byref(arg) -> (txILType gps arg).MakeByRefType()
            | ILType.Modified(_, _mod, arg) -> txILType gps arg
            | ILType.Var(n) ->
                let (gps1:Type[]), (gps2:Type[]) = gps
                if n < gps1.Length then gps1.[n]
                elif n < gps1.Length + gps2.Length then gps2.[n - gps1.Length]
                else failwithf "generic parameter index out of range: %d" n

        /// Convert an ILGenericParameterDef read from a binary to a System.Type.
        and txILGenericParam gpsf pos (inp: ILGenericParameterDef) =
            TargetGenericParam (asm, gpsf, pos, inp, txILType, txCustomAttributesData, typeBuilder) :> Type

        let rec gps = inp.GenericParams |> Array.mapi (fun i gp -> txILGenericParam (fun () -> gps, [| |]) i gp)

        let isNested = declTyOpt.IsSome

        do this.typeImpl <- this
        override __.Name = inp.Name
        override __.Assembly = (asm :> Assembly)
        override __.DeclaringType = declTyOpt |> Option.toObj
        override __.MemberType = if isNested then MemberTypes.NestedType else MemberTypes.TypeInfo
        override __.MetadataToken = inp.Token

        override __.FullName =
            match declTyOpt with
            | None ->
                match inp.Namespace with
                | UNone -> inp.Name
                | USome nsp -> nsp + "." + inp.Name
            | Some declTy ->
                declTy.FullName + "+" + inp.Name

        override __.Namespace = inp.Namespace |> StructOption.toObj
        override __.BaseType = inp.Extends |> Option.map (txILType (gps, [| |])) |> Option.toObj
        override __.GetInterfaces() = inp.Implements |> Array.map (txILType (gps, [| |]))

        override this.GetConstructors(bindingFlags) =
            inp.Methods.Entries
            |> Array.filter (fun x -> x.Name = ".ctor" || x.Name = ".cctor")
            |> Array.map (txILConstructorDef this)
            |> Array.filter (canBindConstructor bindingFlags)

        override this.GetMethods(bindingFlags) =
            inp.Methods.Entries
            |> Array.filter (fun x -> x.Name <> ".ctor" && x.Name <> ".cctor")
            |> Array.map (txILMethodDef this)
            |> Array.filter (canBindMethod bindingFlags)

        override this.GetFields(bindingFlags) =
            inp.Fields.Entries
            |> Array.map (txILFieldDef this)
            |> Array.filter (canBindField bindingFlags)

        override this.GetEvents(bindingFlags) =
            inp.Events.Entries
            |> Array.map (txILEventDef this)
            |> Array.filter (canBindEvent bindingFlags)

        override this.GetProperties(bindingFlags) =
            inp.Properties.Entries
            |> Array.map (txILPropertyDef this)
            |> Array.filter (canBindProperty bindingFlags)

        override this.GetNestedTypes(bindingFlags) =
            inp.NestedTypes.Entries
            |> Array.map (asm.TxILTypeDef (Some (this :> Type)))
            |> Array.filter (canBindNestedType bindingFlags)

        override this.GetConstructorImpl(_bindingFlags, _binder, _callConvention, types, _modifiers)          =
            let md = 
                match types with
                | null -> inp.Methods.TryFindUniqueByName(".ctor")
                | _ -> 
                    inp.Methods.FindByNameAndArity(".ctor", types.Length)
                    |> Array.tryFind (fun md -> eqTypesAndILTypes types md.ParameterTypes)
            md 
            |> Option.map (txILConstructorDef this) 
            |> Option.toObj

        override this.GetMethodImpl(name, _bindingFlags, _binder, _callConvention, types, _modifiers)          =
            let md = 
                match types with
                | null -> inp.Methods.TryFindUniqueByName(name)
                | _ -> 
                    inp.Methods.FindByNameAndArity(name, types.Length)
                    |> Array.tryFind (fun md -> eqTypesAndILTypes types md.ParameterTypes)
            md |> Option.map (txILMethodDef this) |> Option.toObj

        override this.GetField(name, _bindingFlags) =
            inp.Fields.Entries
            |> Array.tryPick (fun p -> if p.Name = name then Some (txILFieldDef this p) else None)
            |> Option.toObj

        override this.GetPropertyImpl(name, _bindingFlags, _binder, _returnType, _types, _modifiers) =
            inp.Properties.Entries
            |> Array.tryPick (fun p -> if p.Name = name then Some (txILPropertyDef this p) else None)
            |> Option.toObj

        override this.GetEvent(name, _bindingFlags) =
            inp.Events.Entries
            |> Array.tryPick (fun ev -> if ev.Name = name then Some (txILEventDef this ev) else None)
            |> Option.toObj

        override this.GetNestedType(name, _bindingFlags) =
            inp.NestedTypes.TryFindByName(UNone, name) |> Option.map (asm.TxILTypeDef (Some (this :> Type))) |> Option.toObj


        override this.GetMembers(bindingFlags) =
            [| for x in this.GetConstructors(bindingFlags) do yield (x :> MemberInfo)
               for x in this.GetMethods(bindingFlags) do yield (x :> MemberInfo)
               for x in this.GetFields(bindingFlags) do yield (x :> MemberInfo)
               for x in this.GetProperties(bindingFlags) do yield (x :> MemberInfo)
               for x in this.GetEvents(bindingFlags) do yield (x :> MemberInfo)
               for x in this.GetNestedTypes(bindingFlags) do yield (x :> MemberInfo) |]

        override this.MakeGenericType(args) = TypeSymbol(TypeSymbolKind.TargetGeneric this, args, typeBuilder) :> Type
        override this.MakeArrayType() = TypeSymbol(TypeSymbolKind.SDArray, [| this |], typeBuilder) :> Type
        override this.MakeArrayType arg = TypeSymbol(TypeSymbolKind.Array arg, [| this |], typeBuilder) :> Type
        override this.MakePointerType() = TypeSymbol(TypeSymbolKind.Pointer, [| this |], typeBuilder) :> Type
        override this.MakeByRefType() = TypeSymbol(TypeSymbolKind.ByRef, [| this |], typeBuilder) :> Type

        override __.GetAttributeFlagsImpl() =
            let attr = TypeAttributes.Public ||| TypeAttributes.Class
            let attr = if inp.IsSealed then attr ||| TypeAttributes.Sealed else attr
            let attr = if inp.IsInterface then attr ||| TypeAttributes.Interface else attr
            let attr = if inp.IsSerializable then attr ||| TypeAttributes.Serializable else attr
            if isNested then adjustTypeAttributes isNested attr else attr

        override __.IsValueTypeImpl() = inp.IsStructOrEnum

        override __.IsEnum = 
            match this.BaseType with 
            | null -> false
            | bt -> bt.FullName = "System.Enum" || bt.IsEnum

        override __.GetEnumUnderlyingType() =
            if this.IsEnum then
                txILType ([| |], [| |]) ilGlobals.typ_Int32 // TODO: in theory the assumption of "Int32" is not accurate for all enums, howver in practice .NET only uses enums with backing field Int32
            else failwithf "not enum type"

        override __.IsArrayImpl() = false
        override __.IsByRefImpl() = false
        override __.IsPointerImpl() = false
        override __.IsPrimitiveImpl() = false
        override __.IsCOMObjectImpl() = false
        override __.IsGenericType = (gps.Length <> 0)
        override __.IsGenericTypeDefinition = (gps.Length <> 0)
        override __.HasElementTypeImpl() = false

        override this.UnderlyingSystemType = (this :> Type)
        override __.GetCustomAttributesData() = inp.CustomAttrs |> txCustomAttributesData

        override this.Equals(that:obj) = System.Object.ReferenceEquals (this, that)
        override this.Equals(that:Type) = System.Object.ReferenceEquals (this, that)
        override __.GetHashCode() =  inp.Token

        override this.IsAssignableFrom(otherTy: Type) = isAssignableFrom this otherTy

        override this.IsSubclassOf(otherTy: Type) = isSubclassOf this otherTy

        override this.AssemblyQualifiedName = "[" + this.Assembly.FullName + "]" + this.FullName

        override this.ToString() = sprintf "tgt type %s" this.FullName

        override __.GetGenericArguments() = gps
        override this.GetGenericTypeDefinition() = (this :> Type)

        override x.Module = x.Assembly.ManifestModule

        override this.GetMember(_name, _memberType, _bindingFlags) = notRequired this "GetMember" inp.Name
        override this.GUID = notRequired this "GUID" inp.Name
        override this.GetCustomAttributes(_inherited) = notRequired this "GetCustomAttributes" inp.Name
        override this.GetCustomAttributes(_attributeType, _inherited) = notRequired this "GetCustomAttributes" inp.Name
        override this.IsDefined(_attributeType, _inherited) = notRequired this "IsDefined" inp.Name
        override this.GetInterface(_name, _ignoreCase) = notRequired this "GetInterface" inp.Name
        override this.GetElementType() = notRequired this "GetElementType" inp.Name
        override this.InvokeMember(_name, _invokeAttr, _binder, _target, _args, _modifiers, _culture, _namedParameters) = notRequired this "InvokeMember" inp.Name

        member __.Metadata: ILTypeDef = inp
        member __.MakeMethodInfo (declTy: Type) md = txILMethodDef declTy md
        member __.MakeConstructorInfo (declTy: Type) md = txILConstructorDef declTy md
        member __.MakePropertyInfo (declTy: Type) md = txILPropertyDef declTy md
        member __.MakeEventInfo (declTy: Type) md = txILEventDef declTy md
        member __.MakeFieldInfo (declTy: Type) md = txILFieldDef declTy md
        member __.MakeNestedTypeInfo (declTy: Type) md =  asm.TxILTypeDef (Some declTy) md
        override this.GetEvents() = this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) // Needed because TypeDelegator.cs provides a delegting implementation of this, and we are self-delegating
#if NETCOREAPP
        // See bug https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/236
        override __.IsSZArray = false
#endif

    and TargetModule(location: string) =
        inherit Module()
        override __.MetadataToken = hash location

    /// Implements System.Reflection.Assembly backed by .NET metadata provided by an ILModuleReader
    and TargetAssembly(ilGlobals, tryBindAssembly: ILAssemblyRef -> Choice<Assembly, exn>, reader: ILModuleReader option, location: string, typeBuilder: ITypeBuilder) as asm =
        inherit Assembly()

        // A table tracking how type definition objects are translated.
        let txTable = TxTable<Type>()
        let mutable reader = reader
        let manifestModule = TargetModule(location)
        let getReader() = match reader with None -> failwith "the reader on the TargetAssembly has not been set" | Some r -> r

        let txILTypeDef (declTyOpt: Type option) (inp: ILTypeDef) =
            txTable.Get inp.Token (fun () -> 
                // We never create target types for the types of primitive values that are accepted by the F# compiler as Expr.Value nodes, 
                // which fortunately also correspond to element types. We just use the design-time types instead.
                // See convertConstExpr in the compiler, e.g. 
                //     https://github.com/Microsoft/visualfsharp/blob/44fa027b308681a1b78a089e44fa1ab35ff77b41/src/fsharp/MethodCalls.fs#L842
                // for the accepted types.
                match inp.Namespace, inp.Name with 
                //| USome "System", "Void"->  typeof<Void>
                (*
                | USome "System", "Boolean" -> typeof<bool>
                | USome "System", "String"->  typeof<string>
                | USome "System", "Object"->  typeof<obj>
                | USome "System", "Int32" ->  typeof<int32>
                | USome "System", "SByte" ->  typeof<sbyte>
                | USome "System", "Int16"->  typeof<int16>
                | USome "System", "Int64" ->  typeof<int64>
                | USome "System", "IntPtr" ->  typeof<IntPtr>
                | USome "System", "Byte" ->  typeof<byte>
                | USome "System", "UInt16"->  typeof<uint16>
                | USome "System", "UInt32" ->  typeof<uint32>
                | USome "System", "UInt64" ->  typeof<uint64>
                | USome "System", "UIntPtr" ->  typeof<UIntPtr>
                | USome "System", "Double" ->  typeof<double>
                | USome "System", "Single" ->  typeof<single>
                | USome "System", "Char" ->  typeof<char> 
                *)
                | _ -> 
                TargetTypeDefinition(ilGlobals, tryBindAssembly, asm, declTyOpt, inp, typeBuilder) :> System.Type)

        let types = lazy [| for td in getReader().ILModuleDef.TypeDefs.Entries -> txILTypeDef None td  |]


        override __.GetReferencedAssemblies() = [| for aref in getReader().ILAssemblyRefs -> aref.ToAssemblyName() |]

        override __.GetTypes () = types.Force()

        override x.GetType (nm:string) =
            if nm.Contains("+") then
                let i = nm.LastIndexOf("+")
                let enc, nm2 = nm.[0..i-1], nm.[i+1..]
                match x.GetType(enc) with
                | null -> null
                | t -> t.GetNestedType(nm2, bindAll)
            elif nm.Contains(".") then
                let i = nm.LastIndexOf(".")
                let nsp, nm2 = nm.[0..i-1], nm.[i+1..]
                x.TryBindType(USome nsp, nm2) |> Option.toObj
            else
                x.TryBindType(UNone, nm) |> Option.toObj

        override __.GetName() = getReader().ILModuleDef.ManifestOfAssembly.GetName()

        override x.FullName = x.GetName().ToString()

        override __.Location = location
        override __.ManifestModule = (manifestModule :> Module)

        override __.ReflectionOnly = true

        override x.GetManifestResourceStream(resourceName:string) =
            let r = getReader().ILModuleDef.Resources.Entries |> Seq.find (fun r -> r.Name = resourceName)
            match r.Location with
            | ILResourceLocation.Local f -> new MemoryStream(f()) :> Stream
            | _ -> 
            notRequired x "reading manifest resource %s" resourceName

        member __.TxILTypeDef declTyOpt inp = txILTypeDef declTyOpt inp

        member __.Reader with get() = reader  and set v = (if reader.IsSome then failwith "reader on TargetAssembly already set"); reader <- v

        member __.TryBindType(nsp:string uoption, nm:string): Type option =
            match getReader().ILModuleDef.TypeDefs.TryFindByName(nsp, nm) with
            | Some td -> asm.TxILTypeDef None td |> Some
            | None ->
            match getReader().ILModuleDef.ManifestOfAssembly.ExportedTypes.TryFindByName(nsp, nm) with
            | Some tref ->
                match tref.ScopeRef with
                | ILScopeRef.Assembly aref2 ->
                    let ass2opt = tryBindAssembly(aref2)
                    match ass2opt with
                    | Choice1Of2 ass2 -> 
                        match ass2.GetType(joinILTypeName nsp nm)  with 
                        | null -> None
                        | ty -> Some ty
                    | Choice2Of2 _err -> None
                | _ ->
                    printfn "unexpected non-forwarder during binding"
                    None
            | None -> None

        member x.BindType(nsp:string uoption, nm:string) =
            match x.TryBindType(nsp, nm) with
            | None -> failwithf "failed to bind type %s in assembly %s" nm asm.FullName
            | Some res -> res

        override x.ToString() = "tgt assembly " + x.FullName

    type ProvidedAssembly(isTgt: bool, assemblyName:AssemblyName, assemblyFileName: string, customAttributesData) =
      
        inherit Assembly()
        
        let customAttributesImpl = CustomAttributesImpl(isTgt, customAttributesData)
        
        let theTypes = ResizeArray<ProvidedTypeDefinition[] * string list option>()
        
        let addTypes (ptds:ProvidedTypeDefinition[], enclosingTypeNames: string list option) =
            for pt in ptds do
                if pt.IsErased then failwith ("The provided type "+pt.Name+"is marked as erased and cannot be converted to a generated type. Set 'IsErased=false' on the ProvidedTypeDefinition")
                if not isTgt && pt.BelongsToTargetModel then failwithf "Expected '%O' to be a source ProvidedTypeDefinition. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" pt
                if isTgt && not pt.BelongsToTargetModel then failwithf "Expected '%O' to be a target ProvidedTypeDefinition. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" pt
            theTypes.Add (ptds, enclosingTypeNames)

        let theTypesArray = lazy (theTypes.ToArray() |> Array.collect (function (ptds, None) -> Array.map (fun ptd -> (ptd :> Type)) ptds | _ -> [| |]))
        
        member __.AddCustomAttribute(attribute) = customAttributesImpl.AddCustomAttribute(attribute)

        override __.GetReferencedAssemblies() = [| |] //notRequired x "GetReferencedAssemblies" (assemblyName.ToString())

        override __.GetName() = assemblyName

        override __.FullName = assemblyName.ToString()

        override __.Location = assemblyFileName

        override __.ReflectionOnly = true

        override __.GetTypes () = theTypesArray.Force()

        override __.ToString () = assemblyName.ToString()

        override x.GetType (nm: string) = 
            if nm.Contains("+") then
                let i = nm.LastIndexOf("+")
                let enc, nm2 = nm.[0..i-1], nm.[i+1..]
                match x.GetType(enc) with
                | null -> null
                | t -> t.GetNestedType(nm2, bindAll)
            else
                theTypesArray.Force() 
                |> Array.tryPick (fun ty -> if ty.FullName = nm then Some ty else None) 
                |> Option.toObj
        
        new () = 
            let tmpFile = Path.GetTempFileName()
            let assemblyFileName = Path.ChangeExtension(tmpFile, "dll")
            File.Delete(tmpFile)
            let simpleName = Path.GetFileNameWithoutExtension(assemblyFileName)
            ProvidedAssembly(AssemblyName(simpleName), assemblyFileName)

        new (assemblyName, assemblyFileName) = 
            ProvidedAssembly(false, assemblyName, assemblyFileName, K [||])

        member __.BelongsToTargetModel = isTgt

        member __.AddNestedTypes (types, enclosingGeneratedTypeNames) = 
            addTypes (Array.ofList types, Some enclosingGeneratedTypeNames)

        member __.AddTypes (types) = 
            addTypes (Array.ofList types, None)

        member __.AddTheTypes (types, enclosingGeneratedTypeNames) = 
            addTypes (types, enclosingGeneratedTypeNames)

        member __.GetTheTypes () = theTypes.ToArray()
        
        override __.GetCustomAttributesData() = customAttributesImpl.GetCustomAttributesData()

//====================================================================================================
// ProvidedTypesContext
//
// A binding context for cross-targeting type providers

namespace ProviderImplementation.ProvidedTypes


    #nowarn "8796"
    #nowarn "1182"

    open System
    open System.Diagnostics
    open System.IO
    open System.Collections.Concurrent
    open System.Collections.Generic
    open System.Reflection

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.DerivedPatterns
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Reflection

    open ProviderImplementation.ProvidedTypes
    open ProviderImplementation.ProvidedTypes.AssemblyReader

    [<AutoOpen>]
    module private ImplementationUtils =
        type System.Object with
           member x.GetProperty(nm) =
               let ty = x.GetType()
               let prop = ty.GetProperty(nm, bindAll)
               let v = prop.GetValue(x, null)
               v

           member x.GetField(nm) =
               let ty = x.GetType()
               let fld = ty.GetField(nm, bindAll)
               let v = fld.GetValue(x)
               v

           member x.HasProperty(nm) =
               let ty = x.GetType()
               let p = ty.GetProperty(nm, bindAll)
               p |> isNull |> not

           member x.HasField(nm) =
               let ty = x.GetType()
               let fld = ty.GetField(nm, bindAll)
               fld |> isNull |> not

           member x.GetElements() = [ for v in (x :?> System.Collections.IEnumerable) do yield v ]



    type ProvidedTypeBuilder() =
        static let tupleNames = 
            [| "System.Tuple`1";      "System.Tuple`2";      "System.Tuple`3";
               "System.Tuple`4";      "System.Tuple`5";      "System.Tuple`6";
               "System.Tuple`7";      "System.Tuple`8";      "System.Tuple"
               "System.ValueTuple`1"; "System.ValueTuple`2"; "System.ValueTuple`3";
               "System.ValueTuple`4"; "System.ValueTuple`5"; "System.ValueTuple`6";
               "System.ValueTuple`7"; "System.ValueTuple`8"; "System.ValueTuple" |]


        static member MakeGenericType(genericTypeDefinition: Type, genericArguments: Type list) = 
            if genericArguments.Length = 0 then genericTypeDefinition else
            match genericTypeDefinition with 
            | :? TargetTypeDefinition -> failwithf "unexpected target model in ProvidedTypeBuilder.MakeGenericType, stacktrace = %s " Environment.StackTrace
            | :? ProvidedTypeDefinition as ptd when ptd.BelongsToTargetModel -> failwithf "unexpected target model ptd in MakeGenericType, stacktrace = %s " Environment.StackTrace
            | :? ProvidedTypeDefinition -> ProvidedTypeSymbol(ProvidedTypeSymbolKind.Generic genericTypeDefinition, genericArguments, ProvidedTypeBuilder.typeBuilder) :> Type
            | _ -> TypeSymbol(TypeSymbolKind.OtherGeneric genericTypeDefinition, List.toArray genericArguments, ProvidedTypeBuilder.typeBuilder) :> Type

        static member MakeGenericMethod(genericMethodDefinition, genericArguments: Type list) = 
            if genericArguments.Length = 0 then genericMethodDefinition else
            MethodSymbol2(genericMethodDefinition, Array.ofList genericArguments, ProvidedTypeBuilder.typeBuilder) :> MethodInfo

        static member MakeTupleType(types, isStruct) =
            let rec mkTupleType isStruct (asm:Assembly) (tys:Type list) =
                let maxTuple = 8

                let n = min tys.Length maxTuple
                let tupleFullName = tupleNames.[n - 1 + (if isStruct then 9 else 0)]
                let ty = asm.GetType(tupleFullName)
                if tys.Length >= maxTuple then 
                    let tysA = (Array.ofList tys).[0..maxTuple-2] |> List.ofArray
                    let tysB = (Array.ofList tys).[maxTuple-1..] |> List.ofArray
                    let tyB = mkTupleType isStruct asm tysB
                    ProvidedTypeBuilder.MakeGenericType(ty, List.append  tysA [ tyB ])
                else
                    ProvidedTypeBuilder.MakeGenericType(ty, tys)
            mkTupleType isStruct (typeof<System.Tuple>.Assembly) types

        static member MakeTupleType(types) = ProvidedTypeBuilder.MakeTupleType(types, false)

        static member typeBuilder = 
            { new ITypeBuilder with
                member this.MakeGenericType(typeDef: Type, args)= 
                    match typeDef with
                    | :? ProvidedTypeDefinition -> ProvidedTypeSymbol(ProvidedTypeSymbolKind.Generic typeDef, Array.toList args, this) :> Type
                    | _ -> 
                        if args |> Array.exists (function :? ProvidedTypeDefinition -> true | _ -> false) then
                            TypeSymbol(TypeSymbolKind.OtherGeneric typeDef, args, this) :> Type
                        else
                            typeDef.MakeGenericType(args)
                member this.MakeArrayType(typ) = 
                    match typ with
                    | :? ProvidedTypeDefinition ->
                        TypeSymbol(TypeSymbolKind.SDArray, [| typ |], this) :> Type
                    | _ -> typ.MakeArrayType()
                member this.MakeRankedArrayType(typ,rank) = 
                    match typ with
                    | :? ProvidedTypeDefinition ->
                        TypeSymbol(TypeSymbolKind.Array rank, [| typ |], this) :> Type
                    | _ -> typ.MakeArrayType(rank)
                member this.MakePointerType(typ) = 
                    match typ with
                    | :? ProvidedTypeDefinition ->
                        TypeSymbol(TypeSymbolKind.Pointer, [| typ |], this) :> Type
                    | _ -> typ.MakePointerType()
                member this.MakeByRefType(typ) = 
                    match typ with
                    | :? ProvidedTypeDefinition ->
                        TypeSymbol(TypeSymbolKind.ByRef, [| typ |], this) :> Type
                    | _ -> typ.MakeByRefType()                }

    //--------------------------------------------------------------------------------
    // The quotation simplifier
    //
    // This is invoked for each quotation specified by the type provider, as part of the translation to 
    /// the target model, i.e. before it is handed to the F# compiler (for erasing type providers) or 
    // the TPSDK IL code generator (for generative type providers). This allows a broader range of quotations 
    // to be used when authoring type providers than are strictly allowed by those tools. 
    //
    // Specifically we accept:
    //
    //     - NewTuple nodes (for generative type providers)
    //     - TupleGet nodes (for generative type providers)
    //     - array and list values as constants
    //     - PropertyGet and PropertySet nodes
    //     - Application, NewUnionCase, NewRecord, UnionCaseTest nodes
    //     - Let nodes (defining "byref" values)
    //     - LetRecursive nodes
    //
    // Additionally, a set of code optimizations are applied for generative type providers:
    //    - inlineRightPipe
    //    - optimizeCurriedApplications
    //    - inlineValueBindings

    // Note, the QuotationSimplifier works over source quotations, not target quotations
    type QuotationSimplifier(isGenerated: bool) =

        let rec simplifyExpr q =
            match q with

#if !NO_GENERATIVE
            // Convert NewTuple to the call to the constructor of the Tuple type (only for generated types, 
            // the F# compile does the job for erased types when it receives the quotation)
            | NewTuple(items) when isGenerated ->
                let rec mkCtor args ty =
                    let ctor, restTyOpt = Reflection.FSharpValue.PreComputeTupleConstructorInfo ty
                    match restTyOpt with
                    | None -> Expr.NewObjectUnchecked(ctor, List.map simplifyExpr args)
                    | Some restTy ->
                        let curr = [for a in Seq.take 7 args -> simplifyExpr a]
                        let rest = List.ofSeq (Seq.skip 7 args)
                        Expr.NewObjectUnchecked(ctor, curr @ [mkCtor rest restTy])
                let tys = [ for e in items -> e.Type ]
                let tupleTy = ProvidedTypeBuilder.MakeTupleType(tys, q.Type.IsValueType)
                simplifyExpr (mkCtor items tupleTy)

            // convert TupleGet to the chain of PropertyGet calls (only for generated types)
            | TupleGet(e, i) when isGenerated ->
                let rec mkGet (ty : Type) i (e: Expr)  =
                    if ty.IsValueType then 
                        let get index =
                                let fields = ty.GetFields() |> Array.sortBy (fun fi -> fi.Name) 
                                if index >= fields.Length then
                                    invalidArg "index" (sprintf "The tuple index '%d' was out of range for tuple type %s" index ty.Name)
                                fields.[index]
                        let tupleEncField = 7
                        let fget = Expr.FieldGetUnchecked(e, get i)
                        if i < tupleEncField then
                            fget
                        else
                            let etys = ty.GetGenericArguments()
                            mkGet etys.[tupleEncField] (i - tupleEncField) fget
                    else
                        let pi, restOpt = Reflection.FSharpValue.PreComputeTuplePropertyInfo(ty, i)
                        let propGet = Expr.PropertyGetUnchecked(e, pi)
                        match restOpt with
                        | None -> propGet
                        | Some (restTy, restI) -> mkGet restTy restI propGet
                simplifyExpr (mkGet e.Type i (simplifyExpr e))
#endif

            | Value(value, ty) ->
                if value |> isNull |> not then
                    let tyOfValue = value.GetType()
                    transValue(value, tyOfValue, ty)
                else q

            // Eliminate F# property gets to method calls
            | PropertyGet(obj, propInfo, args) ->
                match obj with
                | None -> simplifyExpr (Expr.CallUnchecked(propInfo.GetGetMethod(true), args))
                | Some o -> simplifyExpr (Expr.CallUnchecked(simplifyExpr o, propInfo.GetGetMethod(true), args))

            // Eliminate F# property sets to method calls
            | PropertySet(obj, propInfo, args, v) ->
                    match obj with
                    | None -> simplifyExpr (Expr.CallUnchecked(propInfo.GetSetMethod(true), args@[v]))
                    | Some o -> simplifyExpr (Expr.CallUnchecked(simplifyExpr o, propInfo.GetSetMethod(true), args@[v]))

            // Eliminate F# function applications to FSharpFunc<_, _>.Invoke calls
            | Application(f, e) ->
                simplifyExpr (Expr.CallUnchecked(simplifyExpr f, f.Type.GetMethod "Invoke", [ e ]) )

            // Eliminate F# union operations
            | NewUnionCase(ci, es) ->
                simplifyExpr (Expr.CallUnchecked(Reflection.FSharpValue.PreComputeUnionConstructorInfo ci, es) )

            // Eliminate F# union operations
            | UnionCaseTest(e, uc) ->
                let tagInfo = Reflection.FSharpValue.PreComputeUnionTagMemberInfo uc.DeclaringType
                let tagExpr =
                    match tagInfo with
                    | :? PropertyInfo as tagProp ->
                            simplifyExpr (Expr.PropertyGet(e, tagProp) )
                    | :? MethodInfo as tagMeth ->
                            if tagMeth.IsStatic then simplifyExpr (Expr.Call(tagMeth, [e]))
                            else simplifyExpr (Expr.Call(e, tagMeth, []))
                    | _ -> failwith "unreachable: unexpected result from PreComputeUnionTagMemberInfo. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues"
                let tagNumber = uc.Tag
                simplifyExpr <@@ (%%(tagExpr): int) = tagNumber @@>

            // Eliminate F# record operations
            | NewRecord(ci, es) ->
                simplifyExpr (Expr.NewObjectUnchecked(Reflection.FSharpValue.PreComputeRecordConstructorInfo ci, es) )

            // Explicitly handle weird byref variables in lets (used to populate out parameters), since the generic handlers can't deal with byrefs.
            //
            // The binding must have leaves that are themselves variables (due to the limited support for byrefs in expressions)
            // therefore, we can perform inlining to translate this to a form that can be compiled
            | Let(v, vexpr, bexpr) when v.Type.IsByRef -> transLetOfByref v vexpr bexpr

            // Eliminate recursive let bindings (which are unsupported by the type provider API) to regular let bindings
            | LetRecursive(bindings, expr) -> simplifyLetRec bindings expr

            // Handle the generic cases
            | ShapeLambdaUnchecked(v, body) -> Expr.Lambda(v, simplifyExpr body)
            | ShapeCombinationUnchecked(comb, args) -> RebuildShapeCombinationUnchecked(comb, List.map simplifyExpr args)
            | ShapeVarUnchecked _ -> q

        and simplifyLetRec bindings expr =
            // This uses a "lets and sets" approach, converting something like
            //    let rec even = function
            //    | 0 -> true
            //    | n -> odd (n-1)
            //    and odd = function
            //    | 0 -> false
            //    | n -> even (n-1)
            //    X
            // to something like
            //    let even = ref Unchecked.defaultof<_>
            //    let odd = ref Unchecked.defaultof<_>
            //    even := function
            //            | 0 -> true
            //            | n -> !odd (n-1)
            //    odd  := function
            //            | 0 -> false
            //            | n -> !even (n-1)
            //    X'
            // where X' is X but with occurrences of even/odd substituted by !even and !odd (since now even and odd are references)
            // Translation relies on typedefof<_ ref> - does this affect ability to target different runtime and design time environments?
            let vars = List.map fst bindings
            let refVars = vars |> List.map (fun v -> Var(v.Name, ProvidedTypeBuilder.MakeGenericType(typedefof<_ ref>, [v.Type])))

            // "init t" generates the equivalent of <@ ref Unchecked.defaultof<t> @>
            let init (t:Type) =
                let r = match <@ ref 1 @> with Call(None, r, [_]) -> r | _ -> failwith "Extracting MethodInfo from <@ 1 @> failed"
                let d = match <@ Unchecked.defaultof<_> @> with Call(None, d, []) -> d | _ -> failwith "Extracting MethodInfo from <@ Unchecked.defaultof<_> @> failed"
                let ir = ProvidedTypeBuilder.MakeGenericMethod(r.GetGenericMethodDefinition(), [ t ])
                let id = ProvidedTypeBuilder.MakeGenericMethod(d.GetGenericMethodDefinition(), [ t ])
                Expr.CallUnchecked(ir, [Expr.CallUnchecked(id, [])])

            // deref v generates the equivalent of <@ !v @>
            // (so v's type must be ref<something>)
            let deref (v:Var) =
                let m = match <@ !(ref 1) @> with Call(None, m, [_]) -> m | _ -> failwith "Extracting MethodInfo from <@ !(ref 1) @> failed"
                let tyArgs = v.Type.GetGenericArguments()
                let im = ProvidedTypeBuilder.MakeGenericMethod(m.GetGenericMethodDefinition(), Array.toList tyArgs)
                Expr.CallUnchecked(im, [Expr.Var v])

            // substitution mapping a variable v to the expression <@ !v' @> using the corresponding new variable v' of ref type
            let subst =
                let map = [ for v in refVars -> v.Name, deref v ] |> Map.ofList
                fun (v:Var) -> Map.tryFind v.Name map

            let refExpr = expr.Substitute(subst)

            // maps variables to new variables
            let varDict = [ for (v, rv) in List.zip vars refVars -> v.Name, rv ] |> dict

            // given an old variable v and an expression e, returns a quotation like <@ v' := e @> using the corresponding new variable v' of ref type
            let setRef (v:Var) e =
                let m = match <@ (ref 1) := 2 @> with Call(None, m, [_;_]) -> m | _ -> failwith "Extracting MethodInfo from <@ (ref 1) := 2 @> failed"
                let im = ProvidedTypeBuilder.MakeGenericMethod(m.GetGenericMethodDefinition(), [ v.Type ])
                Expr.CallUnchecked(im, [Expr.Var varDict.[v.Name]; e])

            // Something like
            //  <@
            //      v1 := e1'
            //      v2 := e2'
            //      ...
            //      refExpr
            //  @>
            // Note that we must substitute our new variable dereferences into the bound expressions
            let body =
                bindings
                |> List.fold (fun b (v, e) -> Expr.Sequential(setRef v (e.Substitute subst), b)) refExpr

            // Something like
            //   let v1 = ref Unchecked.defaultof<t1>
            //   let v2 = ref Unchecked.defaultof<t2>
            //   ...
            //   body
            (body, vars)
            ||> List.fold (fun b v -> Expr.LetUnchecked(varDict.[v.Name], init v.Type, b)) 
            |> simplifyExpr


        and transLetOfByref v vexpr bexpr =
            match vexpr with
            | Sequential(e', vexpr') ->
                (* let v = (e'; vexpr') in bexpr => e'; let v = vexpr' in bexpr *)
                Expr.Sequential(e', transLetOfByref v vexpr' bexpr)
                |> simplifyExpr
            | IfThenElse(c, b1, b2) ->
                (* let v = if c then b1 else b2 in bexpr => if c then let v = b1 in bexpr else let v = b2 in bexpr *)
                //
                // Note, this duplicates "bexpr"
                Expr.IfThenElseUnchecked(c, transLetOfByref v b1 bexpr, transLetOfByref v b2 bexpr)
                |> simplifyExpr
            | Var _ ->
                (* let v = v1 in bexpr => bexpr[v/v1] *)
                bexpr.Substitute(fun v' -> if v = v' then Some vexpr else None)
                |> simplifyExpr
            | _ ->
                failwithf "Unexpected byref binding: %A = %A. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" v vexpr

        and transValueArray (o: Array, ty: Type) =
            let elemTy = ty.GetElementType()
            let converter = getValueConverterForType elemTy
            let elements = [ for el in o -> converter el ]
            Expr.NewArrayUnchecked(elemTy, elements)

        and transValueList(o, ty: Type, nil, cons) =
            let converter = getValueConverterForType (ty.GetGenericArguments().[0])
            o
            |> Seq.cast
            |> List.ofSeq
            |> fun l -> List.foldBack(fun o s -> Expr.NewUnionCase(cons, [ converter(o); s ])) l (Expr.NewUnionCase(nil, []))
            |> simplifyExpr

        and getValueConverterForType (ty: Type) =
            if ty.IsArray then
                fun (v: obj) -> transValueArray(v :?> Array, ty)
            elif ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<_ list> then
                let nil, cons =
                    let cases = Reflection.FSharpType.GetUnionCases(ty)
                    let a = cases.[0]
                    let b = cases.[1]
                    if a.Name = "Empty" then a, b
                    else b, a

                fun v -> transValueList (v :?> System.Collections.IEnumerable, ty, nil, cons)
            else
                fun v -> Expr.Value(v, ty)

        and transValue (v: obj, tyOfValue: Type, expectedTy: Type) =
            let converter = getValueConverterForType tyOfValue
            let r = converter v
            if tyOfValue <> expectedTy then Expr.Coerce(r, expectedTy)
            else r

#if !NO_GENERATIVE
        let getFastFuncType (args: list<Expr>) resultType =
            let types =
                [|  for arg in args -> arg.Type
                    yield resultType |]
            let fastFuncTy =
                match List.length args with
                | 2 -> typedefof<OptimizedClosures.FSharpFunc<_, _, _>>.MakeGenericType(types) 
                | 3 -> typedefof<OptimizedClosures.FSharpFunc<_, _, _, _>>.MakeGenericType(types) 
                | 4 -> typedefof<OptimizedClosures.FSharpFunc<_, _, _, _, _>>.MakeGenericType(types) 
                | 5 -> typedefof<OptimizedClosures.FSharpFunc<_, _, _, _, _, _>>.MakeGenericType(types) 
                | _ -> invalidArg "args" "incorrect number of arguments"
            fastFuncTy.GetMethod("Adapt")

        let (===) a b = LanguagePrimitives.PhysicalEquality a b

        let traverse f =
            let rec fallback e =
                match e with
                | Let(v, value, body) ->
                    let fixedValue = f fallback value
                    let fixedBody = f fallback body
                    if fixedValue === value && fixedBody === body then
                        e
                    else
                        Expr.LetUnchecked(v, fixedValue, fixedBody)
                | ShapeVarUnchecked _ -> e
                | ShapeLambdaUnchecked(v, body) ->
                    let fixedBody = f fallback body
                    if fixedBody === body then
                        e
                    else
                        Expr.Lambda(v, fixedBody)
                | ShapeCombinationUnchecked(shape, exprs) ->
                    let exprs1 = List.map (f fallback) exprs
                    if List.forall2 (===) exprs exprs1 then
                        e
                    else
                        RebuildShapeCombinationUnchecked(shape, exprs1)
            fun e -> f fallback e

        let rightPipe = <@@ (|>) @@>
        let inlineRightPipe expr =
            let rec loop expr = traverse loopCore expr
            and loopCore fallback orig =
                match orig with
                | SpecificCall rightPipe (None, _, [operand; applicable]) ->
                    let fixedOperand = loop operand
                    match loop applicable with
                    | Lambda(arg, body) ->
                        let v = Var("__temp", operand.Type)
                        let ev = Expr.Var v

                        let fixedBody = loop body
                        Expr.Let(v, fixedOperand, fixedBody.Substitute(fun v1 -> if v1 = arg then Some ev else None))
                    | fixedApplicable -> Expr.Application(fixedApplicable, fixedOperand)
                | x -> fallback x
            loop expr


        let inlineValueBindings e =
            let map = Dictionary(HashIdentity.Reference)
            let rec loop expr = traverse loopCore expr
            and loopCore fallback orig =
                match orig with
                | Let(id, (Value(_) as v), body) when not id.IsMutable ->
                    map.[id] <- v
                    let fixedBody = loop body
                    map.Remove(id) |> ignore
                    fixedBody
                | ShapeVarUnchecked v ->
                    match map.TryGetValue v with
                    | true, e -> e
                    | _ -> orig
                | x -> fallback x
            loop e


        let optimizeCurriedApplications expr =
            let rec loop expr = traverse loopCore expr
            and loopCore fallback orig =
                match orig with
                | Application(e, arg) ->
                    let e1 = tryPeelApplications e [loop arg]
                    if e1 === e then
                        orig
                    else
                        e1
                | x -> fallback x
            and tryPeelApplications orig args =
                let n = List.length args
                match orig with
                | Application(e, arg) ->
                    let e1 = tryPeelApplications e ((loop arg)::args)
                    if e1 === e then
                        orig
                    else
                        e1
                | Let(id, applicable, (Lambda(_) as body)) when n > 0 ->
                    let numberOfApplication = countPeelableApplications body id 0
                    if numberOfApplication = 0 then orig
                    elif n = 1 then Expr.Application(applicable, List.head args)
                    elif n <= 5 then
                        let resultType =
                            applicable.Type
                            |> Seq.unfold (fun t ->
                                if not t.IsGenericType then None else
                                let args = t.GetGenericArguments()
                                if args.Length <> 2 then None else
                                Some (args.[1], args.[1])
                            )
                            |> Seq.toArray
                            |> (fun arr -> arr.[n - 1])

                        let adaptMethod = getFastFuncType args resultType
                        let adapted = Expr.Call(adaptMethod, [loop applicable])
                        let invoke = adapted.Type.GetMethod("Invoke", [| for arg in args -> arg.Type |])
                        Expr.Call(adapted, invoke, args)
                    else
                        (applicable, args) ||> List.fold (fun e a -> Expr.Application(e, a))
                | _ ->
                    orig
            and countPeelableApplications expr v n =
                match expr with
                // v - applicable entity obtained on the prev step
                // \arg -> let v1 = (f arg) in rest ==> f
                | Lambda(arg, Let(v1, Application(Var f, Var arg1), rest)) when v = f && arg = arg1 -> countPeelableApplications rest v1 (n + 1)
                // \arg -> (f arg) ==> f
                | Lambda(arg, Application(Var f, Var arg1)) when v = f && arg = arg1 -> n
                | _ -> n
            loop expr
#endif

        member __.TranslateExpression q = simplifyExpr q

        member __.TranslateQuotationToCode qexprf (paramNames: string[]) (argExprs: Expr[]) =
            // Use the real variable names instead of indices, to improve output of Debug.fs
            // Add let bindings for arguments to ensure that arguments will be evaluated
            let varDecisions = argExprs |> Array.mapi (fun i e -> match e with Var v when v.Name = paramNames.[i] -> false, v | _ -> true, Var(paramNames.[i], e.Type))
            let vars = varDecisions |> Array.map snd
            let expr = qexprf ([for v in vars -> Expr.Var v])

            let pairs = Array.zip argExprs varDecisions
            let expr = Array.foldBack (fun (arg, (replace, var)) e -> if replace then Expr.LetUnchecked(var, arg, e) else e) pairs expr
#if !NO_GENERATIVE
            let expr =
                if isGenerated then
                    let e1 = inlineRightPipe expr
                    let e2 = optimizeCurriedApplications e1
                    let e3 = inlineValueBindings e2
                    e3
                else
                    expr
#endif

            simplifyExpr expr

    /// A cross-targeting type provider must ultimately provide quotations and reflection objects w.r.t.
    /// the type binding context for the target assembly reference set.
    ///
    /// To make building a cross-targeting type provider palatable, the type provider is written w.r.t. to
    /// homogeneous quotations and reflection objects referring to a copy of the target runtime constructs held
    /// in the design-time assembly itself.   These are then systematically remapped (replaced/translated) to the
    /// corresponding reflection objects in the target assembly reference set.
    ///
    /// The ProvidedTypesContext acts as a way of creating provided objects where the replacement is automatically and
    /// systematically applied.
 

    /// Represents the type binding context for the type provider based on the set of assemblies
    /// referenced by the compilation.
    type ProvidedTypesContext(referencedAssemblyPaths: string list, assemblyReplacementMap: (string*string) list, sourceAssemblies: Assembly list) as this =

        // A duplicate 'mscorlib' appears in the paths reported by the F# compiler
        let referencedAssemblyPaths = referencedAssemblyPaths |> Seq.distinctBy Path.GetFileNameWithoutExtension |> Seq.toList
        //do System.Diagnostics.Debugger.Break()

        /// Find which assembly defines System.Object etc.
        let systemRuntimeScopeRef =
          lazy
            referencedAssemblyPaths |> List.tryPick (fun path ->
              try
                let simpleName = Path.GetFileNameWithoutExtension path
                if simpleName = "mscorlib" || simpleName = "System.Runtime" || simpleName = "netstandard" || simpleName = "System.Private.CoreLib" then
                    let reader = ILModuleReaderAfterReadingAllBytes (path, mkILGlobals EcmaMscorlibScopeRef)
                    let mdef = reader.ILModuleDef
                    match mdef.TypeDefs.TryFindByName(USome "System", "Object") with
                    | None -> None
                    | Some _ ->
                        let m = mdef.ManifestOfAssembly
                        let assRef = ILAssemblyRef(m.Name, UNone, (match m.PublicKey with USome k -> USome (PublicKey.KeyAsToken(k)) | UNone -> UNone), m.Retargetable, m.Version, m.Locale)
                        Some (ILScopeRef.Assembly assRef)
                else
                    None
              with _ -> None )
            |> function
               | None -> EcmaMscorlibScopeRef // failwith "no reference to mscorlib.dll or System.Runtime.dll or netstandard.dll found"
               | Some r -> r

        let fsharpCoreRefVersion =
          lazy
            referencedAssemblyPaths |> List.tryPick (fun path ->
              try
                let simpleName = Path.GetFileNameWithoutExtension path
                if simpleName = "FSharp.Core" then
                    let reader = ILModuleReaderAfterReadingAllBytes (path, mkILGlobals (systemRuntimeScopeRef.Force()))
                    match reader.ILModuleDef.Manifest with
                    | Some m -> match m.Version with USome v -> Some v | UNone -> None
                    | None -> None
                else
                    None
              with _ -> None )
            |> function
               | None -> typeof<int list>.Assembly.GetName().Version // failwith "no reference to FSharp.Core found"
               | Some r -> r

        let ilGlobals = 
            lazy mkILGlobals (systemRuntimeScopeRef.Force())

        let mkReader ref =
            try let reader = ILModuleReaderAfterReadingAllBytes(ref, ilGlobals.Force())
                Choice1Of2(TargetAssembly(ilGlobals.Force(), this.TryBindILAssemblyRefToTgt, Some reader, ref, ProvidedTypeBuilder.typeBuilder) :> Assembly)
            with err -> Choice2Of2 err

        let targetAssembliesTable_ =  ConcurrentDictionary<string, Choice<Assembly, _>>()
        let targetAssemblies_ = ResizeArray<Assembly>()
        let targetAssembliesQueue = ResizeArray<_>()
        do targetAssembliesQueue.Add (fun () -> 
              for ref in referencedAssemblyPaths do
                  let reader = mkReader ref 
                  let simpleName = Path.GetFileNameWithoutExtension ref 
                  targetAssembliesTable_.[simpleName] <- reader
                  match reader with 
                  | Choice2Of2 _ -> () 
                  | Choice1Of2 asm -> targetAssemblies_.Add asm)
        let flush() = 
            let qs = targetAssembliesQueue.ToArray()
            targetAssembliesQueue.Clear()
            for q in qs do q() 
        let getTargetAssemblies() =  flush(); targetAssemblies_
        let getTargetAssembliesTable() = flush(); targetAssembliesTable_

        let tryBindTargetAssemblySimple(simpleName:string): Choice<Assembly, exn> =
            let table = getTargetAssembliesTable()
            if table.ContainsKey(simpleName) then table.[simpleName]
            else Choice2Of2 (Exception(sprintf "assembly %s not found" simpleName))

        let sourceAssembliesTable_ =  ConcurrentDictionary<string, Assembly>()
        let sourceAssemblies_ = ResizeArray<_>()
        let sourceAssembliesQueue = ResizeArray<_>()

        let enqueueReferencedAssemblies(asm: Assembly) = 
            do sourceAssembliesQueue.Add (fun () -> 
                [| for referencedAssemblyName  in asm.GetReferencedAssemblies() do
                      let referencedAssembly = try Assembly.Load(referencedAssemblyName) with _ -> null
                      if not (isNull referencedAssembly) then
                          yield referencedAssembly |])

        do sourceAssembliesQueue.Add (fun () -> List.toArray sourceAssemblies)

        let getSourceAssemblies() = 
            while sourceAssembliesQueue.Count > 0 do
                let qs = sourceAssembliesQueue.ToArray()
                sourceAssembliesQueue.Clear()
                for q in qs do 
                    for asm in q() do 
                        let simpleName = asm.GetName().Name
                        if not (sourceAssembliesTable_.ContainsKey(simpleName)) then 
                            sourceAssembliesTable_.[simpleName] <- asm
                            sourceAssemblies_.Add asm
                            // Find the transitive closure of all referenced assemblies
                            enqueueReferencedAssemblies asm

            sourceAssemblies_

        /// When translating quotations, Expr.Var's are translated to new variable respecting reference equality.
        let varTableFwd = Dictionary<Var, Var>()
        let varTableBwd = Dictionary<Var, Var>()
        let assemblyTableFwd = Dictionary<Assembly, Assembly>()
        let typeTableFwd = Dictionary<Type, Type>()
        let typeTableBwd = Dictionary<Type, Type>()

        let fixName (fullName:string) =
          if fullName.StartsWith("FSI_") then
              // when F# Interactive is the host of the design time assembly, 
              // all namespaces are prefixed with FSI_, in the runtime assembly
              // the name won't have that prefix
              fullName.Substring(fullName.IndexOf('.') + 1)
          else
              fullName

        let tryGetTypeFromAssembly toTgt (originalAssemblyName:string) fullName (asm:Assembly) =
            // if the original assembly of the type being replaced is in `assemblyReplacementMap`, 
            // then we only map it to assemblies with a name specified in `assemblyReplacementMap`
            let restrictedAndMatching =
                assemblyReplacementMap
                |> Seq.exists (fun (originalName:string, newName:string) ->
                    originalAssemblyName.StartsWith originalName && not (asm.FullName.StartsWith(newName)))

            // Check if the assembly can be queried for types yet.  Cross-assembly recursive linking back to generated assemblies
            // is not supported in some cases where recursive linking is needed during the process of generation itself.
            let canQuery = (match asm with :? TargetAssembly as t  -> t.Reader.IsSome | _ -> true)

            if not canQuery then None
            elif restrictedAndMatching then None
            elif asm.FullName.StartsWith "FSI-ASSEMBLY" then
                // when F# Interactive is the host of the design time assembly, 
                // for each type in the runtime assembly there might be multiple
                // versions (FSI_0001.FullTypeName, FSI_0002.FullTypeName, etc).
                // Get the last one.
                asm.GetTypes()
                |> Seq.filter (fun t -> fixName t.FullName = fullName)
                |> Seq.sortBy (fun t -> t.FullName)
                |> Seq.toList
                |> function [] -> None | xs -> Some (Seq.last xs, false)
            else
                asm.GetType fullName |> function null -> None | x -> Some (x, true)

        let typeBuilder = ProvidedTypeBuilder.typeBuilder
        let rec convTypeRef toTgt (t:Type) =
            let table = (if toTgt then typeTableFwd else typeTableBwd)
            match table.TryGetValue(t) with
            | true, newT -> newT
            | false, _ ->
                match t with 
                | :? ProvidedTypeDefinition as ptd when toTgt -> 
                    if ptd.IsErased && ptd.BelongsToTargetModel then failwithf "unexpected erased target ProvidedTypeDefinition '%O'" ptd
                    // recursively get the provided type.
                    convTypeDefToTgt t
                    
                | _ -> 
                let asms = (if toTgt then getTargetAssemblies() else getSourceAssemblies())
                let fullName = fixName t.FullName

                // TODO: this linear search through all available source/target assemblies feels as if it must be too slow in some cases.
                // However, we store type translations in various tables (typeTableFwd and typeTableBwd) so perhaps it is not a problem
                let rec loop i = 
                    if i < 0 then 
                        let msg =
                            if toTgt then sprintf "The design-time type '%O' utilized by a type provider was not found in the target reference assembly set '%A'. You may be referencing a profile which contains fewer types than those needed by the type provider you are using." t (getTargetAssemblies() |> Seq.toList)
                            elif getSourceAssemblies() |> Seq.length = 0 then sprintf "A failure occured while determining compilation references"
                            else sprintf "The target type '%O' utilized by a type provider was not found in the design-time assembly set '%A'. Please report this problem to the project site for the type provider." t (getSourceAssemblies() |> Seq.toList)
                        failwith msg
                    else
                        match tryGetTypeFromAssembly toTgt t.Assembly.FullName fullName asms.[i] with
                        | Some (newT, canSave) ->
                            if canSave then table.[t] <- newT
                            newT
                        | None -> loop (i - 1)
                loop (asms.Count - 1)

        and convType toTgt (t:Type) =
            let table = (if toTgt then typeTableFwd else typeTableBwd)
            match table.TryGetValue(t) with
            | true, newT -> newT
            | false, _ ->
                if t :? ProvidedTypeSymbol && (t :?> ProvidedTypeSymbol).IsFSharpTypeAbbreviation then t
                // Types annotated with units-of-measure
                elif t :? ProvidedTypeSymbol && (t :?> ProvidedTypeSymbol).IsFSharpUnitAnnotated then
                    let genericType = t.GetGenericTypeDefinition()
                    let newT = convTypeRef toTgt genericType
                    let typeArguments = t.GetGenericArguments() |> Array.map (convType toTgt) |> Array.toList
                    ProvidedMeasureBuilder.AnnotateType (newT, typeArguments)
                elif t.IsGenericType && not t.IsGenericTypeDefinition then
                    let genericType = t.GetGenericTypeDefinition()
                    let newT = convTypeRef toTgt genericType
                    let typeArguments = t.GetGenericArguments() |> Array.map (convType toTgt)
                    typeBuilder.MakeGenericType(newT,typeArguments)
                elif t.IsGenericParameter then t
                elif t.IsArray || t.IsByRef || t.IsPointer then
                    let elemType = t.GetElementType()
                    let elemTypeT = convType toTgt elemType
                    if t.IsArray then
                        let rank = t.GetArrayRank()
                        if rank = 1 then typeBuilder.MakeArrayType(elemTypeT) else typeBuilder.MakeRankedArrayType(elemTypeT,t.GetArrayRank())
                    elif t.IsByRef then typeBuilder.MakeByRefType(elemTypeT)
                    else typeBuilder.MakePointerType(elemTypeT)

                else
                    convTypeRef toTgt t

        and convTypeToTgt ty = convType true ty
        and convTypeToSrc ty = convType false ty

        and convPropertyRefToTgt (p: PropertyInfo) =
            Debug.Assert((match p with :? ProvidedProperty as x -> not x.BelongsToTargetModel | _ -> true), "unexpected target ProvidedProperty")
            let t = convTypeToTgt p.DeclaringType
            let bindingFlags = bindSome p.IsStatic 
            let pT = t.GetProperty(p.Name, bindingFlags)
            if isNull pT then failwithf "Property '%O' of type '%O' not found. This property may be missing in the types available in the target assemblies." p t
            Debug.Assert((match pT with :? ProvidedProperty as x -> x.BelongsToTargetModel | _ -> true), "expected a target ProvidedProperty")
            pT

        and convFieldRefToTgt (f: FieldInfo) =
            Debug.Assert((match f with :? ProvidedField as x -> not x.BelongsToTargetModel | _ -> true), "unexpected target ProvidedField")
            let t = convTypeToTgt f.DeclaringType
            let fT = t.GetField(f.Name, bindSome f.IsStatic)
            if isNull fT then failwithf "Field '%O' of type '%O' not found. This field may be missing in the types available in the target assemblies." f t
            Debug.Assert((match fT with :? ProvidedField as x -> x.BelongsToTargetModel | _ -> true), "expected a target ProvidedField")
            fT

        and convMethodRefToTgt (m: MethodInfo) =
            Debug.Assert((match m with :? ProvidedMethod as x -> not x.BelongsToTargetModel | _ -> true), "unexpected target ProvidedMethod")
            //Debug.Assert (m.Name <> "get_Item1" || m.DeclaringType.Name <> "Tuple`2")
            let declTyT = convTypeToTgt m.DeclaringType
            let mT =
                if m.IsGenericMethod then
                  let genericMethod = m.GetGenericMethodDefinition()
                  let parameterTypesT = genericMethod.GetParameters() |> Array.map (fun p -> convTypeToTgt p.ParameterType)
                  let genericMethodT = declTyT.GetMethod(genericMethod.Name, bindSome m.IsStatic, null, parameterTypesT, null)
                  if isNull genericMethodT then null else
                  let typeArgumentsT =  m.GetGenericArguments() |> Array.map convTypeToTgt
                  genericMethodT.MakeGenericMethod(typeArgumentsT)
                else
                  let parameterTypesT = m.GetParameters() |> Array.map (fun p -> convTypeToTgt p.ParameterType)
                  declTyT.GetMethod(m.Name, bindSome m.IsStatic, null, parameterTypesT, null)
            match mT with
            | null -> failwithf "Method '%O' not found in type '%O'. This method may be missing in the types available in the target assemblies." m mT
            | _ -> 
                Debug.Assert((match mT with :? ProvidedMethod as x -> x.BelongsToTargetModel | _ -> true), "expected a target ProvidedMethod")
                mT

        and tryConvConstructorRefToTgt (cons: ConstructorInfo) =
            Debug.Assert((match cons with :? ProvidedConstructor as x -> not x.BelongsToTargetModel | _ -> true), "unexpected target ProvidedConstructor")
            let declTyT = convTypeToTgt cons.DeclaringType
            let parameterTypesT = cons.GetParameters() |> Array.map (fun p -> convTypeToTgt p.ParameterType)
            let flags = 
                (if cons.IsStatic then BindingFlags.Static else BindingFlags.Instance) 
                ||| (if cons.IsPublic then BindingFlags.Public else BindingFlags.NonPublic )
            let consT = declTyT.GetConstructor(flags, null,parameterTypesT, null )
            match consT with
            | null -> Choice1Of2 (sprintf "Constructor '%O' not found in type '%O'. This constructor may be missing in the types available in the target assemblies." cons declTyT)
            | _ -> 
                Debug.Assert((match consT with :? ProvidedConstructor as x -> x.BelongsToTargetModel | _ -> true), "expected a target ProvidedConstructor")
                Choice2Of2 consT

        and convConstructorRefToTgt (cons: ConstructorInfo) =
            match tryConvConstructorRefToTgt cons with 
            | Choice1Of2 err -> failwith err
            | Choice2Of2 res -> res

        and convVarToSrc (v: Var) =
            match varTableBwd.TryGetValue v with
            | true, v -> v
            | false, _ ->
                let newVar = Var (v.Name, convTypeToSrc v.Type, v.IsMutable)
                // store the original var as we'll have to revert to it later
                varTableBwd.Add(v, newVar)
                varTableFwd.Add(newVar, v)
                newVar

        and convVarExprToSrc quotation =
            match quotation with
            | ShapeVarUnchecked v ->
                Expr.Var (convVarToSrc v)
            | _ -> failwithf "Unexpected non-variable argument: %A" quotation

        and convVarToTgt (v: Var) =
            match varTableFwd.TryGetValue v with
            | true, v -> v
            | false, _ ->
                // It's a variable local to the quotation
                let newVar = Var (v.Name, convTypeToTgt v.Type, v.IsMutable)
                // store it so we reuse it from now on
                varTableFwd.Add(v, newVar)
                varTableBwd.Add(newVar, v)
                newVar


        and convExprToTgt quotation =
            match quotation with
            | Call (obj, m, args) ->
                let mR = convMethodRefToTgt m
                let argsR = List.map convExprToTgt args
                match obj with
                | Some obj -> Expr.CallUnchecked (convExprToTgt obj, mR, argsR)
                | None -> Expr.CallUnchecked (mR, argsR)
            | PropertyGet (obj, p, indexArgs) ->
                let pR = convPropertyRefToTgt p
                let indexArgsR = List.map convExprToTgt indexArgs
                match obj with
                | Some obj -> Expr.PropertyGetUnchecked (convExprToTgt obj, pR, indexArgsR)
                | None -> Expr.PropertyGetUnchecked (pR, indexArgsR)
            | PropertySet (obj, p, indexArgs, value) ->
                let pR = convPropertyRefToTgt p
                let indexArgsR = List.map convExprToTgt indexArgs
                match obj with
                | Some obj -> Expr.PropertySetUnchecked (convExprToTgt obj, pR, convExprToTgt value, indexArgsR)
                | None -> Expr.PropertySetUnchecked (pR, convExprToTgt value, indexArgsR)
            | NewObject (c, exprs) ->
                let exprsR = List.map convExprToTgt exprs
                Expr.NewObjectUnchecked (convConstructorRefToTgt c, exprsR)
            | DefaultValue (t) -> 
                Expr.DefaultValue (convTypeToTgt t)
            | Coerce (expr, t) ->
                Expr.Coerce (convExprToTgt expr, convTypeToTgt t)
            | TypeTest (expr, t) ->
                Expr.TypeTest (convExprToTgt expr, convTypeToTgt t)
            | TryWith (body, filterVar, filterBody, catchVar, catchBody) ->
                Expr.TryWith (convExprToTgt body, convVarToTgt filterVar, convExprToTgt filterBody, convVarToTgt catchVar, convExprToTgt catchBody)
            | TryFinally (body, compensation) ->
                Expr.TryFinally (convExprToTgt body, convExprToTgt compensation)
            | NewArray (t, exprs) ->
                Expr.NewArrayUnchecked (convTypeToTgt t, List.map convExprToTgt exprs)
            | NewTuple (exprs) ->
                Expr.NewTuple (List.map convExprToTgt exprs)
            | Lambda (v, expr) ->
                Expr.Lambda (convVarToTgt v, convExprToTgt expr)
            | TupleGet (expr, i) ->
                Expr.TupleGetUnchecked (convExprToTgt expr, i)
            | NewDelegate (t, vars, expr) ->
                Expr.NewDelegateUnchecked (convTypeToTgt t, List.map convVarToTgt vars, convExprToTgt expr)
            | FieldGet (obj, f) ->
                match obj with
                | Some obj -> Expr.FieldGetUnchecked (convExprToTgt obj, convFieldRefToTgt f)
                | None -> Expr.FieldGetUnchecked (convFieldRefToTgt f)
            | FieldSet (obj, f, value) ->
                match obj with
                | Some obj -> Expr.FieldSetUnchecked (convExprToTgt obj, convFieldRefToTgt f, convExprToTgt value)
                | None -> Expr.FieldSetUnchecked (convFieldRefToTgt f, convExprToTgt value)
            | Let (var, value, body) ->
                Expr.LetUnchecked(convVarToTgt var, convExprToTgt value, convExprToTgt body)

            // Eliminate some F# constructs which do not cross-target well
            | Application(f, e) ->
                convExprToTgt (Expr.CallUnchecked(f, f.Type.GetMethod "Invoke", [ e ]) )
            | NewUnionCase(ci, es) ->
                convExprToTgt (Expr.CallUnchecked(Reflection.FSharpValue.PreComputeUnionConstructorInfo ci, es) )
            | NewRecord(ci, es) ->
                convExprToTgt (Expr.NewObjectUnchecked(FSharpValue.PreComputeRecordConstructorInfo ci, es) )
            | UnionCaseTest(e, uc) ->
                let tagInfo = FSharpValue.PreComputeUnionTagMemberInfo uc.DeclaringType
                let tagExpr =
                    match tagInfo with
                    | :? PropertyInfo as tagProp -> Expr.PropertyGetUnchecked(e, tagProp)
                    | :? MethodInfo as tagMeth ->
                            if tagMeth.IsStatic then Expr.CallUnchecked(tagMeth, [e])
                            else Expr.CallUnchecked(e, tagMeth, [])
                    | _ -> failwith "unreachable: unexpected result from PreComputeUnionTagMemberInfo"
                let tagNumber = uc.Tag
                convExprToTgt <@@ (%%(tagExpr): int) = tagNumber @@>

            | Value (obj, ty) ->
                match obj with 
                | :? Type as vty -> Expr.Value(convTypeToTgt vty, ty)
                | _ -> Expr.Value(obj, convTypeToTgt ty)

            // Traverse remaining constructs
            | ShapeVarUnchecked v ->
                Expr.Var (convVarToTgt v)
            | ShapeLambdaUnchecked _ as d ->
                failwithf "It's not possible to use construct %O when cross targetting to a different FSharp.Core. Make sure you're not calling a function with signature A->(B->C) instead of A->B->C (using |> causes this)." d
            | ShapeCombinationUnchecked (o, exprs) ->
                RebuildShapeCombinationUnchecked (o, List.map convExprToTgt exprs)

        and convCodeToTgt (codeFun: Expr list -> Expr, isStatic, isCtor, parameters: ProvidedParameter[], isGenerated) = 
            (fun argsT -> 
                // argsT: the target arg expressions coming from host tooling.  Includes "this" for instance methods and generative constructors.
                // parameters: the (source) parameters specific by the TPDTC. Does not include "this"
                // paramNames: equal in length to argsT. The preferred named for the parameters. Seems to include "this" for instance methods and generative constructors.
                let args = List.map convVarExprToSrc argsT
                let paramNames = 
                    // https://github.com/fsprojects/SwaggerProvider/blob/cfb7a665fada77fd0200591f62faba0ba44e172c/src/SwaggerProvider.DesignTime/SwaggerProviderConfig.fs#L79
                    // "Erased constructors should not pass the instance as the first argument when calling invokeCode!"
                    // "Generated constructors should always pass the instance as the first argument when calling invokeCode!"
                    [| if not isStatic && (not isCtor || isGenerated) then yield "this"
                       for p in parameters do yield p.Name |]
                let code2 = QuotationSimplifier(isGenerated).TranslateQuotationToCode codeFun paramNames (Array.ofList args)
                let codeT = convExprToTgt code2
                codeT)

        and convBaseCallToTgt (codeFun: Expr list -> ConstructorInfo * Expr list, isGenerated) = 
            (fun argsT -> 
                let args = List.map convVarExprToSrc argsT
                let ctor, argExprs = codeFun args
                let argExprs2 = List.map (QuotationSimplifier(isGenerated).TranslateExpression) argExprs
                //let code2 = QuotationSimplifier(false).TranslateQuotationToCode code paramNames
                let ctorT = convConstructorRefToTgt ctor
                let codeT = List.map convExprToTgt argExprs2
                ctorT, codeT)

        and convMemberRefToTgt (x: MemberInfo) =
            match x with 
            | :? PropertyInfo as p -> convPropertyRefToTgt p :> MemberInfo
            | :? FieldInfo as p -> convFieldRefToTgt p :> MemberInfo
            | :? MethodInfo as p -> convMethodRefToTgt p :> MemberInfo
            | :? ConstructorInfo as p -> convConstructorRefToTgt p :> MemberInfo
            | :? Type as p -> convTypeToTgt p :> MemberInfo
            | _ -> failwith "unknown member info"

        and convCustomAttributesTypedArg (x: CustomAttributeTypedArgument) =
            CustomAttributeTypedArgument(convTypeToTgt x.ArgumentType, x.Value)

        and convCustomAttributesNamedArg (x: CustomAttributeNamedArgument) =
            CustomAttributeNamedArgument(convMemberRefToTgt x.MemberInfo, convCustomAttributesTypedArg x.TypedValue)

        and tryConvCustomAttributeDataToTgt (x: CustomAttributeData) = 
             // Allow a fail on AllowNullLiteralAttribute. Some downlevel FSharp.Core don't have this. 
             // In this case just skip the attribute which means null is allowed when targeting downlevel FSharp.Core.
             match tryConvConstructorRefToTgt x.Constructor  with 
             | Choice1Of2 _ when x.Constructor.DeclaringType.Name = typeof<AllowNullLiteralAttribute>.Name -> None
             | Choice1Of2 msg -> failwith msg
             | Choice2Of2 res -> 
                 Some
                     { new CustomAttributeData () with
                        member __.Constructor =  res
                        member __.ConstructorArguments = [| for arg in x.ConstructorArguments -> convCustomAttributesTypedArg arg |] :> IList<_>
                        member __.NamedArguments = [| for arg in x.NamedArguments -> convCustomAttributesNamedArg arg |] :> IList<_> }

        and convCustomAttributesDataToTgt (cattrs: IList<CustomAttributeData>) = 
            cattrs |> Array.ofSeq |> Array.choose tryConvCustomAttributeDataToTgt 
 
        and convProvidedTypeDefToTgt (x: ProvidedTypeDefinition) =
          if x.IsErased && x.BelongsToTargetModel then failwithf "unexpected target type definition '%O'" x
          match typeTableFwd.TryGetValue(x) with
          | true, newT -> (newT :?> ProvidedTypeDefinition)
          | false, _ ->
            let container = 
                match x.Container with 
                | TypeContainer.Namespace(assemf, nm) ->
                    TypeContainer.Namespace((fun () -> 
                        match assemf() with 
                        | :? ProvidedAssembly as assembly -> convProvidedAssembly assembly
                        | assembly -> 
                            assemblyReplacementMap 
                            |> Seq.tryPick (fun (originalName, newName) -> 
                               if assembly.GetName().Name = originalName then
                                   match this.TryBindSimpleAssemblyNameToTarget(newName) with 
                                   | Choice1Of2 replacementAssembly -> Some replacementAssembly
                                   | Choice2Of2 _ -> None
                               else
                                   None)
                            |> function None -> assembly | Some replacementAssembly -> replacementAssembly
                          ), nm)
                | c -> c // nested types patched below

            // Create the type definition with contents mapped to the target
            // Use a 'let rec' to allow access to the target as the declaring
            // type of the contents in a delayed way.
            let rec xT : ProvidedTypeDefinition = 
                let mutable methodsIdx = 0 
                let checkFreshMethods() = 
                    x.CountMembersFromCursor(methodsIdx) > 0
                    
                let getFreshMethods() = 
                    let vs, idx2 = x.GetMembersFromCursor(methodsIdx) 
                    methodsIdx <- idx2
                    vs |> Array.map (convMemberDefToTgt xT)

                let mutable interfacesIdx = 0 
                let getFreshInterfaces() = 
                    let vs, idx2 = x.GetInterfaceImplsFromCursor(interfacesIdx) 
                    interfacesIdx <- idx2
                    vs |> Array.map convTypeToTgt

                let mutable overridesIdx = 0 
                let getFreshMethodOverrides() = 
                    let vs, idx2 = x.GetMethodOverridesFromCursor(overridesIdx) 
                    overridesIdx <- idx2
                    vs |> Array.map (fun (a, b) -> (convMethodRefToTgt a :?> ProvidedMethod), convMethodRefToTgt b)

                let backingDataSource = Some (checkFreshMethods, getFreshMethods, getFreshInterfaces, getFreshMethodOverrides)

                ProvidedTypeDefinition(true, container, x.Name, 
                                        (x.BaseTypeRaw >> Option.map convTypeToTgt), 
                                        x.AttributesRaw, 
                                        (x.EnumUnderlyingTypeRaw >> Option.map convTypeToTgt), 
                                        x.StaticParams |> List.map convStaticParameterDefToTgt, 
                                        x.StaticParamsApply |> Option.map (fun f s p ->  
                                            let t = f s p 
                                            let tT = convProvidedTypeDefToTgt t
                                            tT), 
                                        backingDataSource, 
                                        (x.GetCustomAttributesData >> convCustomAttributesDataToTgt), 
                                        x.NonNullable, 
                                        x.HideObjectMethods, ProvidedTypeBuilder.typeBuilder) 

            Debug.Assert(not (typeTableFwd.ContainsKey(x)))
            typeTableFwd.[x] <- xT
            if x.IsNested then
                let parentT = (convTypeToTgt x.DeclaringType :?> ProvidedTypeDefinition)
                parentT.PatchDeclaringTypeOfMember xT
            xT

        and convTypeDefToTgt (x: Type) =
            match x with 
            | :? ProvidedTypeDefinition as x -> convProvidedTypeDefToTgt x :> Type  
            | _ -> x

        and convParameterDefToTgt (x: ProvidedParameter) = 
            Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedParameter")
            ProvidedParameter(true, x.Name, x.Attributes, 
                              x.ParameterType |> convTypeToTgt, 
                              x.OptionalValue, 
                              (x.GetCustomAttributesData >> convCustomAttributesDataToTgt))

        and convStaticParameterDefToTgt (x: ProvidedStaticParameter) = 
            Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedStaticParameter")
            ProvidedStaticParameter(x.Name, convTypeToTgt x.ParameterType, ?parameterDefaultValue=x.ParameterDefaultValue) 
            
        and convMemberDefToTgt declTyT (x: MemberInfo) = 
            let xT : MemberInfo = 
                match x with 
                | :? ProvidedField as x ->  
                    Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedField")
                    ProvidedField(true, x.Name, x.Attributes, 
                                  x.FieldType |> convTypeToTgt, 
                                  x.GetRawConstantValue(), 
                                  (x.GetCustomAttributesData >> convCustomAttributesDataToTgt)) :> _
                | :? ProvidedProperty as x -> 
                    Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedProperty")
                    ProvidedProperty(true, x.Name, x.Attributes, 
                                     x.PropertyType |> convTypeToTgt, 
                                     x.IsStatic, 
                                     x.Getter |> Option.map (fun f -> f >> convMethodRefToTgt), 
                                     x.Setter |> Option.map (fun f -> f >> convMethodRefToTgt), 
                                     x.IndexParameters |> Array.map convParameterDefToTgt, 
                                     (x.GetCustomAttributesData >> convCustomAttributesDataToTgt)) :> _
                | :? ProvidedEvent as x ->  
                    Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedEvent")
                    ProvidedEvent(true, x.Name, x.Attributes, 
                                  x.EventHandlerType |> convTypeToTgt, 
                                  x.IsStatic, 
                                  (fun () -> convMethodRefToTgt x.Adder), 
                                  (fun () -> convMethodRefToTgt x.Remover), 
                                  (x.GetCustomAttributesData >> convCustomAttributesDataToTgt)) :> _
                | :? ProvidedConstructor as x -> 
                    Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedConstructor")
                    ProvidedConstructor(true, x.Attributes, 
                                        x.Parameters |> Array.map convParameterDefToTgt, 
                                        convCodeToTgt (x.GetInvokeCode, x.IsStatic, true, x.Parameters, not x.IsErased), 
                                        (match x.BaseCall with None -> None | Some f -> Some (convBaseCallToTgt(f, not x.IsErased))), 
                                        x.IsImplicitConstructor, 
                                        (x.GetCustomAttributesData >> convCustomAttributesDataToTgt)) :> _
                | :? ProvidedMethod as x -> 
                    Debug.Assert (not x.BelongsToTargetModel, "unexpected target ProvidedMethod")
                    ProvidedMethod(true, x.Name, x.Attributes, 
                                    x.Parameters |> Array.map convParameterDefToTgt, 
                                    x.ReturnType |> convTypeToTgt, 
                                    x.GetInvokeCode |> Option.map (fun invokeCode -> convCodeToTgt (invokeCode, x.IsStatic, false, x.Parameters, not x.IsErased)), 
                                    x.StaticParams |> List.map convStaticParameterDefToTgt, 
                                    x.StaticParamsApply |> Option.map (fun f s p -> f s p |> convProvidedMethodDefToTgt declTyT), 
                                    (x.GetCustomAttributesData >> convCustomAttributesDataToTgt)) :> _
                | :? ProvidedTypeDefinition as x -> convTypeDefToTgt x :> _
                | _ -> failwith "unknown member type"
            Debug.Assert(declTyT.BelongsToTargetModel)
            declTyT.PatchDeclaringTypeOfMember xT
            Debug.Assert(xT.DeclaringType :? ProvidedTypeDefinition)
            Debug.Assert((xT.DeclaringType :?> ProvidedTypeDefinition).BelongsToTargetModel)
            xT

        and convProvidedMethodDefToTgt declTyT (x: ProvidedMethod) = 
            convMemberDefToTgt declTyT x :?> ProvidedMethod

        and convProvidedAssembly (assembly: ProvidedAssembly) = 
          match assemblyTableFwd.TryGetValue(assembly) with
          | true, newT -> newT
          | false, _ ->
            let tgtAssembly = ProvidedAssembly(true, assembly.GetName(), assembly.Location, K(convCustomAttributesDataToTgt(assembly.GetCustomAttributesData())))

            for (types, enclosingGeneratedTypeNames) in assembly.GetTheTypes() do
                let typesT = Array.map convProvidedTypeDefToTgt types
                tgtAssembly.AddTheTypes (typesT, enclosingGeneratedTypeNames) 

            assemblyTableFwd.Add(assembly, tgtAssembly)
            this.AddSourceAssembly(assembly)
            this.AddTargetAssembly(assembly.GetName(), tgtAssembly)
            (tgtAssembly :> Assembly)

        let rec convNamespaceToTgt (x: IProvidedNamespace) =
            { new IProvidedNamespace with
                  member __.GetNestedNamespaces() = Array.map convNamespaceToTgt (x.GetNestedNamespaces())
                  member __.NamespaceName = x.NamespaceName
                  member __.GetTypes() = Array.map convTypeDefToTgt (x.GetTypes())
                  member __.ResolveTypeName typeName = convTypeDefToTgt (x.ResolveTypeName typeName) }

        /// Gets the equivalent target method
        member __.ConvertSourceMethodRefToTarget t = convMethodRefToTgt t

        /// Gets the equivalent target constructor
        member __.ConvertSourceConstructorRefToTarget t = convConstructorRefToTgt t

        /// Gets the equivalent target type
        member __.ConvertSourceTypeToTarget t = convTypeToTgt t

        member __.ConvertTargetTypeToSource t = convTypeToSrc t

        member __.ConvertSourceExprToTarget e = convExprToTgt e

        member __.ConvertSourceNamespaceToTarget ns = convNamespaceToTgt ns

        member __.ConvertSourceProvidedTypeDefinitionToTarget ptd = convProvidedTypeDefToTgt ptd

        member __.TryBindILAssemblyRefToTgt(aref: ILAssemblyRef): Choice<Assembly, exn> = tryBindTargetAssemblySimple(aref.Name)

        member __.TryBindAssemblyNameToTarget(aref: AssemblyName): Choice<Assembly, exn> = tryBindTargetAssemblySimple(aref.Name)

        member __.TryBindSimpleAssemblyNameToTarget(assemblyName: string) = tryBindTargetAssemblySimple(assemblyName) 

        member __.ILGlobals = ilGlobals.Value

        member __.ReferencedAssemblyPaths = referencedAssemblyPaths

        member __.GetTargetAssemblies() =  getTargetAssemblies().ToArray()

        member __.GetSourceAssemblies() =  getSourceAssemblies().ToArray()

        member __.FSharpCoreAssemblyVersion = fsharpCoreRefVersion.Force()

        member this.ReadRelatedAssembly(fileName) = 
            let ilg = ilGlobals.Force()
            let reader = ILModuleReaderAfterReadingAllBytes(fileName, ilg) 
            TargetAssembly(ilg, this.TryBindILAssemblyRefToTgt, Some reader, fileName, ProvidedTypeBuilder.typeBuilder) :> Assembly

        member this.ReadRelatedAssembly(bytes:byte[]) = 
            let fileName = "file.dll"
            let ilg = ilGlobals.Force()
            let is = ByteFile(bytes)
            let pe = PEReader(fileName, is)
            let mdchunk = bytes.[pe.MetadataPhysLoc .. pe.MetadataPhysLoc + pe.MetadataSize - 1]
            let mdfile = ByteFile(mdchunk)
            let reader = ILModuleReader(fileName, mdfile, ilg, true)
            TargetAssembly(ilg, this.TryBindILAssemblyRefToTgt, Some reader, fileName, ProvidedTypeBuilder.typeBuilder) :> Assembly

        member __.AddSourceAssembly(asm: Assembly) = 
            sourceAssembliesQueue.Add (fun () -> [| asm |])

        member __.AddTargetAssembly(asmName: AssemblyName, asm: Assembly) = 
            targetAssembliesQueue.Add (fun () -> 
                targetAssembliesTable_.[asmName.Name] <- Choice1Of2 asm
                targetAssemblies_.Add asm)

        static member Create (config: TypeProviderConfig, assemblyReplacementMap, sourceAssemblies) =

            // Use the reflection hack to determine the set of referenced assemblies by reflecting over the SystemRuntimeContainsType
            // closure in the TypeProviderConfig object.
            let referencedAssemblyPaths = config.ReferencedAssemblies |> Array.toList
            ProvidedTypesContext(referencedAssemblyPaths, assemblyReplacementMap, sourceAssemblies)



#if !NO_GENERATIVE

namespace ProviderImplementation.ProvidedTypes

    #nowarn "1182"
    module BinaryWriter =

        open System
        open System.Diagnostics
        open System.IO
        open System.Collections.Concurrent
        open System.Collections.Generic
        open System.Reflection
        open System.Text

        open Microsoft.FSharp.Quotations
        open Microsoft.FSharp.Quotations.DerivedPatterns
        open Microsoft.FSharp.Quotations.Patterns
        open Microsoft.FSharp.Quotations.ExprShape
        open Microsoft.FSharp.Core.CompilerServices
        open Microsoft.FSharp.Reflection

        open ProviderImplementation.ProvidedTypes
        open ProviderImplementation.ProvidedTypes.AssemblyReader
        open ProviderImplementation.ProvidedTypes.UncheckedQuotations

        let formatCodeLabel (x:int) = "L"+string x

        let emitBytesViaBuffer f = let bb = ByteBuffer.Create 10 in f bb; bb.Close()

        /// Alignment and padding
        let align alignment n = ((n + alignment - 1) / alignment) * alignment

        //---------------------------------------------------------------------
        // Concrete token representations etc. used in PE files
        //---------------------------------------------------------------------


        let getUncodedToken (tab:ILTableName) idx = ((tab.Index <<< 24) ||| idx)

        let markerForUnicodeBytes (b:byte[]) = 
            let len = b.Length
            let rec scan i = 
                i < len/2 && 
                (let b1 = Bytes.get b (i*2)
                 let b2 = Bytes.get b (i*2+1)
                 (b2 <> 0)
                 || (b1 >= 0x01 && b1 <= 0x08)   // as per ECMA and C#
                 || (b1 >= 0xE && b1 <= 0x1F)    // as per ECMA and C#
                 || (b1 = 0x27)                  // as per ECMA and C#
                 || (b1 = 0x2D)                  // as per ECMA and C#
                 || (b1 > 0x7F)                  // as per C# (but ECMA omits this)
                 || scan (i+1))
            let marker = if scan 0 then 0x01 else 0x00
            marker


        // -------------------------------------------------------------------- 
        // Fixups
        // -------------------------------------------------------------------- 

        /// Check that the data held at a fixup is some special magic value, as a sanity check
        /// to ensure the fixup is being placed at a ood location.
        let checkFixup32 (data: byte[]) offset exp = 
            if data.[offset + 3] <> b3 exp then failwith "fixup sanity check failed"
            if data.[offset + 2] <> b2 exp then failwith "fixup sanity check failed"
            if data.[offset + 1] <> b1 exp then failwith "fixup sanity check failed"
            if data.[offset] <> b0 exp then failwith "fixup sanity check failed"

        let applyFixup32 (data:byte[]) offset v = 
            data.[offset] <-   b0 v
            data.[offset+1] <- b1 v
            data.[offset+2] <- b2 v
            data.[offset+3] <- b3 v

        //---------------------------------------------------------------------
        // TYPES FOR TABLES
        //---------------------------------------------------------------------

        module RowElementTags = 
            let [<Literal>] UShort = 0
            let [<Literal>] ULong = 1
            let [<Literal>] Data = 2
            let [<Literal>] DataResources = 3
            let [<Literal>] Guid = 4
            let [<Literal>] Blob = 5
            let [<Literal>] String = 6
            let [<Literal>] SimpleIndexMin = 7
            let SimpleIndex         (t: ILTableName)             = assert (t.Index <= 112); SimpleIndexMin + t.Index
            let [<Literal>] SimpleIndexMax = 119

            let [<Literal>] TypeDefOrRefOrSpecMin = 120
            let TypeDefOrRefOrSpec  (t: TypeDefOrRefOrSpecTag)        = assert (t.Tag <= 2);  TypeDefOrRefOrSpecMin + t.Tag (* + 111 + 1 = 0x70 + 1 = max ILTableName.Tndex  + 1 *)
            let [<Literal>] TypeDefOrRefOrSpecMax = 122

            let [<Literal>] TypeOrMethodDefMin = 123
            let TypeOrMethodDef     (t: TypeOrMethodDefTag)     = assert (t.Tag <= 1);  TypeOrMethodDefMin + t.Tag  (* + 2 + 1 = max TypeDefOrRefOrSpec.Tag  + 1 *)
            let [<Literal>] TypeOrMethodDefMax = 124

            let [<Literal>] HasConstantMin = 125
            let HasConstant         (t: HasConstantTag)         = assert (t.Tag <= 2);  HasConstantMin + t.Tag (* + 1 + 1 = max TypeOrMethodDef.Tag  + 1 *)
            let [<Literal>] HasConstantMax = 127

            let [<Literal>] HasCustomAttributeMin = 128
            let HasCustomAttribute  (t: HasCustomAttributeTag)  = assert (t.Tag <= 21); HasCustomAttributeMin + t.Tag  (* + 2 + 1 = max HasConstant.Tag  + 1 *)
            let [<Literal>] HasCustomAttributeMax = 149

            let [<Literal>] HasFieldMarshalMin = 150
            let HasFieldMarshal     (t: HasFieldMarshalTag)     = assert (t.Tag <= 1);  HasFieldMarshalMin + t.Tag  (* + 21 + 1 = max HasCustomAttribute.Tag  + 1 *)
            let [<Literal>] HasFieldMarshalMax = 151

            let [<Literal>] HasDeclSecurityMin = 152
            let HasDeclSecurity     (t: HasDeclSecurityTag)     = assert (t.Tag <= 2);  HasDeclSecurityMin + t.Tag  (* + 1 + 1 = max HasFieldMarshal.Tag  + 1 *)
            let [<Literal>] HasDeclSecurityMax = 154

            let [<Literal>] MemberRefParentMin = 155
            let MemberRefParent     (t: MemberRefParentTag)     = assert (t.Tag <= 4);  MemberRefParentMin + t.Tag  (* + 2 + 1 = max HasDeclSecurity.Tag  + 1 *)
            let [<Literal>] MemberRefParentMax = 159

            let [<Literal>] HasSemanticsMin = 160
            let HasSemantics        (t: HasSemanticsTag)        = assert (t.Tag <= 1);  HasSemanticsMin + t.Tag  (* + 4 + 1 = max MemberRefParent.Tag  + 1 *)
            let [<Literal>] HasSemanticsMax = 161

            let [<Literal>] MethodDefOrRefMin = 162
            let MethodDefOrRef      (t: MethodDefOrRefTag)      = assert (t.Tag <= 2);  MethodDefOrRefMin + t.Tag  (* + 1 + 1 = max HasSemantics.Tag  + 1 *)
            let [<Literal>] MethodDefOrRefMax = 164

            let [<Literal>] MemberForwardedMin = 165
            let MemberForwarded     (t: MemberForwardedTag)     = assert (t.Tag <= 1);  MemberForwardedMin + t.Tag  (* + 2 + 1 = max MethodDefOrRef.Tag  + 1 *)
            let [<Literal>] MemberForwardedMax = 166

            let [<Literal>] ImplementationMin = 167
            let Implementation      (t: ImplementationTag)      = assert (t.Tag <= 2);  ImplementationMin + t.Tag  (* + 1 + 1 = max MemberForwarded.Tag  + 1 *)
            let [<Literal>] ImplementationMax = 169

            let [<Literal>] CustomAttributeTypeMin = 170
            let CustomAttributeType (t: CustomAttributeTypeTag) = assert (t.Tag <= 3);  CustomAttributeTypeMin + t.Tag  (* + 2 + 1 = max Implementation.Tag + 1 *)
            let [<Literal>] CustomAttributeTypeMax = 173

            let [<Literal>] ResolutionScopeMin = 174
            let ResolutionScope     (t: ResolutionScopeTag)     = assert (t.Tag <= 4);  ResolutionScopeMin + t.Tag  (* + 3 + 1 = max CustomAttributeType.Tag  + 1 *)
            let [<Literal>] ResolutionScopeMax = 178

        [<Struct>]
        type RowElement(tag:int32, idx: int32) = 

            member __.Tag = tag
            member __.Val = idx

        // These create RowElements
        let UShort (x:uint16)    = RowElement(RowElementTags.UShort, int32 x)
        let ULong (x:int32)      = RowElement(RowElementTags.ULong, x)
        /// Index into cenv.data or cenv.resources.  Gets fixed up later once we known an overall
        /// location for the data section.  flag indicates if offset is relative to cenv.resources. 
        let Data (x:int, k:bool) = RowElement((if k then RowElementTags.DataResources else RowElementTags.Data ), x)
        /// pos. in guid array 
        let Guid (x:int)         = RowElement(RowElementTags.Guid, x)
        /// pos. in blob array 
        let Blob (x:int)         = RowElement(RowElementTags.Blob, x)
        /// pos. in string array 
        let StringE (x:int)      = RowElement(RowElementTags.String, x)
        /// pos. in some table 
        let SimpleIndex         (t, x:int) = RowElement(RowElementTags.SimpleIndex t, x)
        let TypeDefOrRefOrSpec  (t, x:int) = RowElement(RowElementTags.TypeDefOrRefOrSpec t, x)
        let TypeOrMethodDef     (t, x:int) = RowElement(RowElementTags.TypeOrMethodDef t, x)
        let HasConstant         (t, x:int) = RowElement(RowElementTags.HasConstant t, x)
        let HasCustomAttribute  (t, x:int) = RowElement(RowElementTags.HasCustomAttribute t, x)
        let HasFieldMarshal     (t, x:int) = RowElement(RowElementTags.HasFieldMarshal t, x)
        let HasDeclSecurity     (t, x:int) = RowElement(RowElementTags.HasDeclSecurity t, x)
        let MemberRefParent     (t, x:int) = RowElement(RowElementTags.MemberRefParent t, x)
        let HasSemantics        (t, x:int) = RowElement(RowElementTags.HasSemantics t, x)
        let MethodDefOrRef      (t, x:int) = RowElement(RowElementTags.MethodDefOrRef t, x)
        let MemberForwarded     (t, x:int) = RowElement(RowElementTags.MemberForwarded t, x)
        let Implementation      (t, x:int) = RowElement(RowElementTags.Implementation t, x)
        let CustomAttributeType (t, x:int) = RowElement(RowElementTags.CustomAttributeType t, x)
        let ResolutionScope     (t, x:int) = RowElement(RowElementTags.ResolutionScope t, x)

        type BlobIndex = int
        type StringIndex = int

        let BlobIndex (x:BlobIndex): int = x
        let StringIndex (x:StringIndex): int = x

        let inline combineHash x2 acc = 37 * acc + x2 // (acc <<< 6 + acc >>> 2 + x2 + 0x9e3779b9)

        let hashRow (elems:RowElement[]) = 
            let mutable acc = 0
            for i in 0 .. elems.Length - 1 do 
                acc <- (acc <<< 1) + elems.[i].Tag + elems.[i].Val + 631 
            acc

        let equalRows (elems:RowElement[]) (elems2:RowElement[]) = 
            if elems.Length <> elems2.Length  then false else
            let mutable ok = true
            let n = elems.Length
            let mutable i = 0 
            while ok && i < n do 
                if elems.[i].Tag <> elems2.[i].Tag || elems.[i].Val <> elems2.[i].Val then ok <- false
                i <- i + 1
            ok


        type GenericRow = RowElement[]

        /// This is the representation of shared rows is used for most shared row types.
        /// Rows ILAssemblyRef and ILMethodRef are very common and are given their own
        /// representations.
        [<Struct; CustomEquality; NoComparison>]
        type SharedRow(elems: RowElement[], hashCode: int) =
            member __.GenericRow = elems
            override __.GetHashCode() = hashCode
            override __.Equals(obj:obj) = 
                match obj with 
                | :? SharedRow as y -> equalRows elems y.GenericRow
                | _ -> false

        let SharedRow(elems: RowElement[]) = new SharedRow(elems, hashRow elems)

        /// Special representation: Note, only hashing by name
        let AssemblyRefRow(s1, s2, s3, s4, l1, b1, nameIdx, str2, b2) = 
            let hashCode = hash nameIdx
            let genericRow = [| UShort s1; UShort s2; UShort s3; UShort s4; ULong l1; Blob b1; StringE nameIdx; StringE str2; Blob b2 |]
            new SharedRow(genericRow, hashCode)

        /// Special representation the computes the hash more efficiently
        let MemberRefRow(mrp:RowElement, nmIdx:StringIndex, blobIdx:BlobIndex) = 
            let hashCode =   combineHash (hash blobIdx) (combineHash (hash nmIdx) (hash mrp))
            let genericRow = [| mrp; StringE nmIdx; Blob blobIdx |]
            new SharedRow(genericRow, hashCode)

        /// Unshared rows are used for definitional tables where elements do not need to be made unique
        /// e.g. ILMethodDef and ILTypeDef. Most tables are like this. We don't precompute a 
        /// hash code for these rows, and indeed the GetHashCode and Equals should not be needed.
        [<Struct; CustomEquality; NoComparison>]
        type UnsharedRow(elems: RowElement[]) =
            member __.GenericRow = elems
            override __.GetHashCode() = hashRow elems
            override __.Equals(obj:obj) = 
                match obj with 
                | :? UnsharedRow as y -> equalRows elems y.GenericRow
                | _ -> false
                     

        //=====================================================================
        //=====================================================================
        // IL --> TABLES+CODE
        //=====================================================================
        //=====================================================================

        // This environment keeps track of how many generic parameters are in scope. 
        // This lets us translate AbsIL type variable number to IL type variable numbering 
        type ILTypeWriterEnv = { EnclosingTyparCount: int }
        let envForTypeDef (td:ILTypeDef)               = { EnclosingTyparCount=td.GenericParams.Length }
        let envForMethodRef env (typ:ILType)           = { EnclosingTyparCount=(match typ with ILType.Array _ -> env.EnclosingTyparCount | _ -> typ.GenericArgs.Length) }
        let envForNonGenericMethodRef _mref            = { EnclosingTyparCount=System.Int32.MaxValue }
        let envForFieldSpec (fspec:ILFieldSpec)        = { EnclosingTyparCount=fspec.EnclosingType.GenericArgs.Length }
        let envForOverrideSpec (ospec:ILOverridesSpec) = { EnclosingTyparCount=ospec.EnclosingType.GenericArgs.Length }

        //---------------------------------------------------------------------
        // TABLES
        //---------------------------------------------------------------------

        [<NoEquality; NoComparison>]
        type MetadataTable<'T> = 
            { name: string
              dict: Dictionary<'T, int> // given a row, find its entry number
              mutable rows: ResizeArray<'T>  }
            member x.Count = x.rows.Count

            static member New(nm, hashEq) = 
                { name=nm
                  dict = new Dictionary<_, _>(100, hashEq)
                  rows= new ResizeArray<_>() }

            member tbl.EntriesAsArray = 
                tbl.rows.ToArray()

            member tbl.Entries = 
                tbl.rows.ToArray() |> Array.toList

            member tbl.AddSharedEntry x =
                let n = tbl.rows.Count + 1
                tbl.dict.[x] <- n
                tbl.rows.Add(x)
                n

            member tbl.AddUnsharedEntry x =
                let n = tbl.rows.Count + 1
                tbl.rows.Add(x)
                n

            member tbl.FindOrAddSharedEntry x =
                let mutable res = Unchecked.defaultof<_>
                let ok = tbl.dict.TryGetValue(x, &res)
                if ok then res
                else tbl.AddSharedEntry x


            /// This is only used in one special place - see further below. 
            member tbl.SetRowsOfTable (t: _[]) = 
                tbl.rows <- ResizeArray(t)  
                let h = tbl.dict
                h.Clear()
                t |> Array.iteri (fun i x -> h.[x] <- (i+1))

            member tbl.AddUniqueEntry nm geterr x =
                if tbl.dict.ContainsKey x then failwith ("duplicate entry '"+geterr x+"' in "+nm+" table")
                else tbl.AddSharedEntry x

            member tbl.GetTableEntry x = tbl.dict.[x] 
            member tbl.GetTableKeys() = tbl.dict.Keys |> Seq.toArray

        //---------------------------------------------------------------------
        // Keys into some of the tables
        //---------------------------------------------------------------------

        /// We use this key type to help find ILMethodDefs for MethodRefs 
        type MethodDefKey(tidx:int, garity:int, nm:string, rty:ILType, argtys:ILTypes, isStatic:bool) =
            // Precompute the hash. The hash doesn't include the return type or 
            // argument types (only argument type count). This is very important, since
            // hashing these is way too expensive
            let hashCode = 
               hash tidx 
               |> combineHash (hash garity) 
               |> combineHash (hash nm) 
               |> combineHash (hash argtys.Length)
               |> combineHash (hash isStatic)
            member __.TypeIdx = tidx
            member __.GenericArity = garity
            member __.Name = nm
            member __.ReturnType = rty
            member __.ArgTypes = argtys
            member __.IsStatic = isStatic
            override __.ToString() = sprintf "%A" (tidx, garity, nm, rty, argtys, isStatic)
            override __.GetHashCode() = hashCode
            override __.Equals(obj:obj) = 
                match obj with 
                | :? MethodDefKey as y -> 
                    tidx = y.TypeIdx && 
                    garity = y.GenericArity && 
                    nm = y.Name && 
                    // note: these next two use structural equality on AbstractIL ILType values
                    rty = y.ReturnType && 
                    argtys = y.ArgTypes &&
                    isStatic = y.IsStatic
                | _ -> false

        /// We use this key type to help find ILFieldDefs for FieldRefs
        type FieldDefKey(tidx:int, nm:string, ty:ILType) = 
            // precompute the hash. hash doesn't include the type 
            let hashCode = hash tidx |> combineHash (hash nm) 
            member __.TypeIdx = tidx
            member __.Name = nm
            member __.Type = ty
            override __.GetHashCode() = hashCode
            override __.Equals(obj:obj) = 
                match obj with 
                | :? FieldDefKey as y -> 
                    tidx = y.TypeIdx && 
                    nm = y.Name && 
                    ty = y.Type 
                | _ -> false

        type PropertyTableKey = PropKey of int (* type. def. idx. *) * string * ILType * ILTypes
        type EventTableKey = EventKey of int (* type. def. idx. *) * string
        type TypeDefTableKey = TdKey of string list * string uoption * string 

        //---------------------------------------------------------------------
        // The Writer Target
        //---------------------------------------------------------------------

        [<NoComparison; NoEquality; RequireQualifiedAccess>]
        type MetadataTable =
            | Shared of MetadataTable<SharedRow>
            | Unshared of MetadataTable<UnsharedRow>
            member t.FindOrAddSharedEntry(x) = match t with Shared u -> u.FindOrAddSharedEntry(x) | Unshared u -> failwithf "FindOrAddSharedEntry: incorrect table kind, u.name = %s" u.name
            member t.AddSharedEntry(x) = match t with | Shared u -> u.AddSharedEntry(x) | Unshared u -> failwithf "AddSharedEntry: incorrect table kind, u.name = %s" u.name
            member t.AddUnsharedEntry(x) = match t with Unshared u -> u.AddUnsharedEntry(x) | Shared u -> failwithf "AddUnsharedEntry: incorrect table kind, u.name = %s" u.name
            member t.GenericRowsOfTable = match t with Unshared u -> u.EntriesAsArray |> Array.map (fun x -> x.GenericRow) | Shared u -> u.EntriesAsArray |> Array.map (fun x -> x.GenericRow) 
            member t.SetRowsOfSharedTable rows = match t with Shared u -> u.SetRowsOfTable (Array.map SharedRow rows) | Unshared u -> failwithf "SetRowsOfSharedTable: incorrect table kind, u.name = %s" u.name
            member t.Count = match t with Unshared u -> u.Count | Shared u -> u.Count 


        [<NoEquality; NoComparison>]
        type cenv = 
            { ilg: ILGlobals
              emitTailcalls: bool
              deterministic: bool
              showTimes: bool
              desiredMetadataVersion: Version
              requiredDataFixups: ResizeArray<(int32 * (int * bool))>
              /// References to strings in codestreams: offset of code and a (fixup-location , string token) list) 
              mutable requiredStringFixups: ResizeArray<(int32 * (int * int)[])>
              codeChunks: ByteBuffer 
              mutable nextCodeAddr: int32
              
              // Collected debug information
              mutable moduleGuid: byte[]
              generatePdb: bool
              /// Raw data, to go into the data section 
              data: ByteBuffer 
              /// Raw resource data, to go into the data section 
              resources: ByteBuffer 
              mutable entrypoint: (bool * int) option 

              /// Caches
              trefCache: Dictionary<ILTypeRef, int>

              /// The following are all used to generate unique items in the output 
              tables: MetadataTable[]
              AssemblyRefs: MetadataTable<SharedRow>
              fieldDefs: MetadataTable<FieldDefKey>
              methodDefIdxsByKey:  MetadataTable<MethodDefKey>
              methodDefIdxs:  Dictionary<ILMethodDef, int>
              propertyDefs: MetadataTable<PropertyTableKey>
              eventDefs: MetadataTable<EventTableKey>
              typeDefs: MetadataTable<TypeDefTableKey> 
              guids: MetadataTable<byte[]> 
              blobs: MetadataTable<byte[]> 
              strings: MetadataTable<string> 
              userStrings: MetadataTable<string>
            }
            member cenv.GetTable (tab:ILTableName) = cenv.tables.[tab.Index]


            member cenv.AddCode ((reqdStringFixupsOffset, requiredStringFixups), code) = 
                cenv.requiredStringFixups.Add ((cenv.nextCodeAddr + reqdStringFixupsOffset, requiredStringFixups))
                cenv.codeChunks.EmitBytes code
                cenv.nextCodeAddr <- cenv.nextCodeAddr + code.Length

            member cenv.GetCode() = cenv.codeChunks.Close()


        let FindOrAddSharedRow (cenv:cenv) tbl x = cenv.GetTable(tbl).FindOrAddSharedEntry x

        // Shared rows must be hash-cons'd to be made unique (no duplicates according to contents)
        let AddSharedRow (cenv:cenv) tbl x = cenv.GetTable(tbl).AddSharedEntry x

        // Unshared rows correspond to definition elements (e.g. a ILTypeDef or a ILMethodDef)
        let AddUnsharedRow (cenv:cenv) tbl (x:UnsharedRow) = cenv.GetTable(tbl).AddUnsharedEntry x

        let metadataSchemaVersionSupportedByCLRVersion v = 2, 0
            
        let headerVersionSupportedByCLRVersion v = 
           // The COM20HEADER version number 
           // Whidbey version numbers are 2.5 
           // Earlier are 2.0 
           // From an email from jeffschw: "Be built with a compiler that marks the COM20HEADER with Major >=2 and Minor >= 5.  The V2.0 compilers produce images with 2.5, V1.x produces images with 2.0." 
            2, 5

        let peOptionalHeaderByteByCLRVersion v = 
           //  A flag in the PE file optional header seems to depend on CLI version 
           // Whidbey version numbers are 8 
           // Earlier are 6 
           // Tools are meant to ignore this, but the VS Profiler wants it to have the right value 
            8
            
        // returned by writeBinaryAndReportMappings 
        [<NoEquality; NoComparison>]
        type ILTokenMappings =  
            { TypeDefTokenMap: ILTypeDef list * ILTypeDef -> int32
              FieldDefTokenMap: ILTypeDef list * ILTypeDef -> ILFieldDef -> int32
              MethodDefTokenMap: ILTypeDef list * ILTypeDef -> ILMethodDef -> int32
              PropertyTokenMap: ILTypeDef list * ILTypeDef -> ILPropertyDef -> int32
              EventTokenMap: ILTypeDef list * ILTypeDef -> ILEventDef -> int32 }

        let recordRequiredDataFixup (requiredDataFixups: ResizeArray<_>) (buf: ByteBuffer) pos lab =
            requiredDataFixups.Add((pos, lab)) 
            // Write a special value in that we check later when applying the fixup 
            buf.EmitInt32 0xdeaddddd

        //---------------------------------------------------------------------
        // The UserString, BlobHeap, GuidHeap tables
        //---------------------------------------------------------------------

        let GetUserStringHeapIdx cenv s = 
            cenv.userStrings.FindOrAddSharedEntry s

        let GetBytesAsBlobIdx cenv (bytes:byte[]) = 
            if bytes.Length = 0 then 0 
            else cenv.blobs.FindOrAddSharedEntry bytes

        let GetStringHeapIdx cenv s = 
            if s = "" then 0 
            else cenv.strings.FindOrAddSharedEntry s

        let GetGuidIdx cenv info = cenv.guids.FindOrAddSharedEntry info

        let GetStringHeapIdxOption cenv sopt =
            match sopt with 
            | USome ns -> GetStringHeapIdx cenv ns
            | UNone -> 0


        let splitNameAt (nm:string) idx = 
            if idx < 0 then failwith "splitNameAt: idx < 0";
            let last = nm.Length - 1 
            if idx > last then failwith "splitNameAt: idx > last";
            (nm.Substring(0, idx)), 
            (if idx < last then nm.Substring (idx+1, last - idx) else "")


        module String = 
            let indexNotFound() = raise (new System.Collections.Generic.KeyNotFoundException("An index for the character was not found in the string"))

            let index (s:string) (c:char) =  
                let r = s.IndexOf(c) 
                if r = -1 then indexNotFound() else r

            let rindex (s:string) (c:char) =
                let r =  s.LastIndexOf(c) 
                if r = -1 then indexNotFound() else r

            let contains (s:string) (c:char) = 
                s.IndexOf(c, 0, String.length s) <> -1

        let splitTypeNameRightAux nm = 
            if String.contains nm '.' then 
              let idx = String.rindex nm '.'
              let s1, s2 = splitNameAt nm idx
              Some s1, s2 
            else None, nm

        let splitTypeNameRight nm =
            splitTypeNameRightAux nm

        let GetTypeNameAsElemPair cenv (n1, n2) =
            StringE (GetStringHeapIdxOption cenv n1), 
            StringE (GetStringHeapIdx cenv n2)

        //=====================================================================
        // Pass 1 - allocate indexes for types 
        //=====================================================================

        let addILTypeName enc (td: ILTypeDef) = enc@[(if td.IsNested then td.Name else joinILTypeName td.Namespace td.Name)]

        let rec GenTypeDefPass1 enc cenv (td:ILTypeDef) = 
          ignore (cenv.typeDefs.AddUniqueEntry "type index" (fun (TdKey (_, _, n)) -> n) (TdKey (enc, td.Namespace, td.Name)))
          GenTypeDefsPass1 (addILTypeName enc td) cenv td.NestedTypes.Entries

        and GenTypeDefsPass1 enc cenv tds = Array.iter (GenTypeDefPass1 enc cenv) tds

        //=====================================================================
        // Pass 2 - allocate indexes for methods and fields and write rows for types 
        //=====================================================================

        let rec GetIdxForTypeDef cenv key  = 
            try cenv.typeDefs.GetTableEntry key
            with 
              :? KeyNotFoundException -> 
                let (TdKey (enc, nsp, n) ) = key
                failwithf "One of your modules expects the type '%s' to be defined within the module being emitted. keys = %A" (String.concat "." (enc@[joinILTypeName nsp n])) (cenv.typeDefs.GetTableKeys())
                0
            
        // -------------------------------------------------------------------- 
        // Assembly and module references
        // -------------------------------------------------------------------- 

        let rec GetAssemblyRefAsRow cenv (aref:ILAssemblyRef) =
            AssemblyRefRow 
                ((match aref.Version with UNone -> 0us | USome v -> uint16 v.Major), 
                 (match aref.Version with UNone -> 0us | USome v -> uint16 v.Minor), 
                 (match aref.Version with UNone -> 0us | USome v -> uint16 v.Build), 
                 (match aref.Version with UNone -> 0us | USome v -> uint16 v.Revision), 
                 ((match aref.PublicKey with USome (PublicKey _) -> 0x0001 | _ -> 0x0000)
                  ||| (if aref.Retargetable then 0x0100 else 0x0000)), 
                 BlobIndex (match aref.PublicKey with 
                            | UNone ->  0 
                            | USome (PublicKey b | PublicKeyToken b) -> GetBytesAsBlobIdx cenv b), 
                 StringIndex (GetStringHeapIdx cenv aref.Name), 
                 StringIndex (match aref.Locale with UNone -> 0 | USome s -> GetStringHeapIdx cenv s), 
                 BlobIndex (match aref.Hash with UNone -> 0 | USome s -> GetBytesAsBlobIdx cenv s))
          
        and GetAssemblyRefAsIdx cenv aref = 
            FindOrAddSharedRow cenv ILTableNames.AssemblyRef (GetAssemblyRefAsRow cenv aref)

        and GetModuleRefAsRow cenv (mref:ILModuleRef) =
            SharedRow 
                [| StringE (GetStringHeapIdx cenv mref.Name) |]

        and GetModuleRefAsFileRow cenv (mref:ILModuleRef) =
            SharedRow 
                [|  ULong (if mref.HasMetadata then 0x0000 else 0x0001)
                    StringE (GetStringHeapIdx cenv mref.Name)
                    (match mref.Hash with UNone -> Blob 0 | USome s -> Blob (GetBytesAsBlobIdx cenv s)) |]

        and GetModuleRefAsIdx cenv mref = 
            FindOrAddSharedRow cenv ILTableNames.ModuleRef (GetModuleRefAsRow cenv mref)

        and GetModuleRefAsFileIdx cenv mref = 
            FindOrAddSharedRow cenv ILTableNames.File (GetModuleRefAsFileRow cenv mref)

        // -------------------------------------------------------------------- 
        // Does a ILScopeRef point to this module?
        // -------------------------------------------------------------------- 

        let isScopeRefLocal scoref = (scoref = ILScopeRef.Local) 
        let rec isTypeRefLocal (tref:ILTypeRef) =
            isILTypeScopeRefLocal tref.Scope 
        and isILTypeScopeRefLocal (scoref:ILTypeRefScope) =
            match scoref with 
            | ILTypeRefScope.Top t -> isScopeRefLocal t
            | ILTypeRefScope.Nested tref -> isTypeRefLocal tref
        let rec enclosing (scoref:ILTypeRefScope) =
            match scoref with 
            | ILTypeRefScope.Top _ -> []
            | ILTypeRefScope.Nested tref -> enclosing tref.Scope @ [joinILTypeName tref.Namespace tref.Name]

        let isTypeLocal (typ:ILType) = typ.IsNominal && isEmpty typ.GenericArgs && isTypeRefLocal typ.TypeRef

        // -------------------------------------------------------------------- 
        // Scopes to Implementation elements.
        // -------------------------------------------------------------------- 

        let GetScopeRefAsImplementationElem cenv scoref = 
            match scoref with 
            | ILScopeRef.Local ->  (ImplementationTag.AssemblyRef, 0)
            | ILScopeRef.Assembly aref -> (ImplementationTag.AssemblyRef, GetAssemblyRefAsIdx cenv aref)
            | ILScopeRef.Module mref -> (ImplementationTag.File, GetModuleRefAsFileIdx cenv mref)
         
        // -------------------------------------------------------------------- 
        // Type references, types etc.
        // -------------------------------------------------------------------- 

        let rec GetTypeRefAsTypeRefRow cenv (tref:ILTypeRef) = 
            let nselem, nelem = GetTypeNameAsElemPair cenv (tref.Namespace, tref.Name)
            let rs1, rs2 = GetResolutionScopeAsElem cenv tref.Scope
            SharedRow [| ResolutionScope (rs1, rs2); nelem; nselem |]

        and GetTypeRefAsTypeRefIdx cenv tref = 
            let mutable res = 0
            if cenv.trefCache.TryGetValue(tref, &res) then res else 
            let res = FindOrAddSharedRow cenv ILTableNames.TypeRef (GetTypeRefAsTypeRefRow cenv tref)
            cenv.trefCache.[tref] <- res
            res

        and GetTypeDescAsTypeRefIdx cenv (enc, nsp, n) =  
            GetTypeRefAsTypeRefIdx cenv (ILTypeRef (enc, nsp, n))

        and GetResolutionScopeAsElem cenv scoref = 
            match scoref with 
            | ILTypeRefScope.Top s -> 
                match s with 
                | ILScopeRef.Local -> (ResolutionScopeTag.Module, 1) 
                | ILScopeRef.Assembly aref -> (ResolutionScopeTag.AssemblyRef, GetAssemblyRefAsIdx cenv aref)
                | ILScopeRef.Module mref -> (ResolutionScopeTag.ModuleRef, GetModuleRefAsIdx cenv mref)
            
            | ILTypeRefScope.Nested tref -> 
                (ResolutionScopeTag.TypeRef, GetTypeRefAsTypeRefIdx cenv tref)
         

        let emitTypeInfoAsTypeDefOrRefEncoded cenv (bb: ByteBuffer) (scoref, nsp, nm) = 
            if isILTypeScopeRefLocal scoref then 
                let idx = GetIdxForTypeDef cenv (TdKey(enclosing scoref, nsp, nm))
                bb.EmitZ32 (idx <<< 2) // ECMA 22.2.8 TypeDefOrRefEncoded - ILTypeDef 
            else 
                let idx = GetTypeDescAsTypeRefIdx cenv (scoref, nsp, nm)
                bb.EmitZ32 ((idx <<< 2) ||| 0x01) // ECMA 22.2.8 TypeDefOrRefEncoded - ILTypeRef 

        let getTypeDefOrRefAsUncodedToken (tag, idx) =
            let tab = 
                if tag = TypeDefOrRefOrSpecTag.TypeDef then ILTableNames.TypeDef 
                elif tag = TypeDefOrRefOrSpecTag.TypeRef then ILTableNames.TypeRef  
                elif tag = TypeDefOrRefOrSpecTag.TypeSpec then ILTableNames.TypeSpec
                else failwith "getTypeDefOrRefAsUncodedToken"
            getUncodedToken tab idx

        // REVIEW: write into an accumuating buffer
        let EmitArrayShape (bb: ByteBuffer) (ILArrayShape shape) = 
            let sized = Array.filter (function (_, Some _) -> true | _ -> false) shape
            let lobounded = Array.filter (function (Some _, _) -> true | _ -> false) shape
            bb.EmitZ32 shape.Length
            bb.EmitZ32 sized.Length
            sized |> Array.iter (function (_, Some sz) -> bb.EmitZ32 sz | _ -> failwith "?")
            bb.EmitZ32 lobounded.Length
            lobounded |> Array.iter (function (Some low, _) -> bb.EmitZ32 low | _ -> failwith "?") 
                
        let hasthisToByte hasthis =
             match hasthis with 
             | ILThisConvention.Instance -> e_IMAGE_CEE_CS_CALLCONV_INSTANCE
             | ILThisConvention.InstanceExplicit -> e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT
             | ILThisConvention.Static -> 0x00uy

        let callconvToByte ntypars (Callconv (hasthis, bcc)) = 
            hasthisToByte hasthis |||
            (if ntypars > 0 then e_IMAGE_CEE_CS_CALLCONV_GENERIC else 0x00uy) |||
            (match bcc with 
            | ILArgConvention.FastCall -> e_IMAGE_CEE_CS_CALLCONV_FASTCALL
            | ILArgConvention.StdCall -> e_IMAGE_CEE_CS_CALLCONV_STDCALL
            | ILArgConvention.ThisCall -> e_IMAGE_CEE_CS_CALLCONV_THISCALL
            | ILArgConvention.CDecl -> e_IMAGE_CEE_CS_CALLCONV_CDECL
            | ILArgConvention.Default -> 0x00uy
            | ILArgConvention.VarArg -> e_IMAGE_CEE_CS_CALLCONV_VARARG)
          

        // REVIEW: write into an accumuating buffer
        let rec EmitTypeSpec cenv env (bb: ByteBuffer) (et, tspec:ILTypeSpec) = 
            if isEmpty tspec.GenericArgs then 
                bb.EmitByte et
                emitTypeInfoAsTypeDefOrRefEncoded cenv bb (tspec.Scope, tspec.Namespace, tspec.Name)
            else  
                bb.EmitByte et_WITH
                bb.EmitByte et
                emitTypeInfoAsTypeDefOrRefEncoded cenv bb (tspec.Scope, tspec.Namespace, tspec.Name)
                bb.EmitZ32 tspec.GenericArgs.Length
                EmitTypes cenv env bb tspec.GenericArgs

        and GetTypeAsTypeDefOrRef cenv env (ty:ILType) = 
            if isTypeLocal ty then 
                let tref = ty.TypeRef
                (TypeDefOrRefOrSpecTag.TypeDef, GetIdxForTypeDef cenv (TdKey(enclosing tref.Scope, tref.Namespace, tref.Name)))
            elif ty.IsNominal && isEmpty ty.GenericArgs then
                (TypeDefOrRefOrSpecTag.TypeRef, GetTypeRefAsTypeRefIdx cenv ty.TypeRef)
            else 
                (TypeDefOrRefOrSpecTag.TypeSpec, GetTypeAsTypeSpecIdx cenv env ty)

        and GetTypeAsBytes cenv env ty = emitBytesViaBuffer (fun bb -> EmitType cenv env bb ty)

        and GetTypeOfLocalAsBytes cenv env (l: ILLocal) = 
            emitBytesViaBuffer (fun bb ->  EmitLocalInfo cenv env bb l)

        and GetTypeAsBlobIdx cenv env (ty:ILType) = 
            GetBytesAsBlobIdx cenv (GetTypeAsBytes cenv env ty)

        and GetTypeAsTypeSpecRow cenv env (ty:ILType) = 
            SharedRow [| Blob (GetTypeAsBlobIdx cenv env ty) |]

        and GetTypeAsTypeSpecIdx cenv env ty = 
            FindOrAddSharedRow cenv ILTableNames.TypeSpec (GetTypeAsTypeSpecRow cenv env ty)


        and EmitType cenv env bb ty =
            match ty with 
            | ElementType et ->   bb.EmitByte et
            | ILType.Boxed tspec ->  EmitTypeSpec cenv env bb (et_CLASS, tspec)
            | ILType.Value tspec ->  EmitTypeSpec cenv env bb (et_VALUETYPE, tspec)
            | ILType.Array (shape, ty) ->  
                if shape = ILArrayShape.SingleDimensional then (bb.EmitByte et_SZARRAY ; EmitType cenv env bb ty)
                else (bb.EmitByte et_ARRAY; EmitType cenv env bb ty; EmitArrayShape bb shape)
            | ILType.Var tv ->  
                let cgparams = env.EnclosingTyparCount
                if int32 tv <  cgparams then 
                    bb.EmitByte et_VAR
                    bb.EmitZ32 (int32 tv)
                else
                    bb.EmitByte et_MVAR
                    bb.EmitZ32 (int32 tv -  cgparams)

            | ILType.Byref typ -> 
                bb.EmitByte et_BYREF
                EmitType cenv env bb typ
            | ILType.Ptr typ ->  
                bb.EmitByte et_PTR
                EmitType cenv env bb typ
            | ILType.Void ->   
                bb.EmitByte et_VOID 
            | ILType.FunctionPointer x ->
                bb.EmitByte et_FNPTR
                EmitCallsig cenv env bb (x.CallingConv, x.ArgTypes, x.ReturnType, None, 0)
            | ILType.Modified (req, tref, ty) ->
                bb.EmitByte (if req then et_CMOD_REQD else et_CMOD_OPT)
                emitTypeInfoAsTypeDefOrRefEncoded cenv bb (tref.Scope, tref.Namespace, tref.Name)
                EmitType cenv env bb ty
             | _ -> failwith "EmitType"

        and EmitLocalInfo cenv env (bb:ByteBuffer) (l:ILLocal) =
            if l.IsPinned then 
                bb.EmitByte et_PINNED
            EmitType cenv env bb l.Type

        and EmitCallsig cenv env (bb:ByteBuffer) (callconv, args:ILTypes, ret, varargs:ILVarArgs, genarity) = 
            bb.EmitByte (callconvToByte genarity callconv)
            if genarity > 0 then bb.EmitZ32 genarity
            bb.EmitZ32 ((args.Length + (match varargs with None -> 0 | Some l -> l.Length)))
            EmitType cenv env bb ret
            args |> Array.iter (EmitType cenv env bb)
            match varargs with 
             | None -> ()// no extra arg = no sentinel 
             | Some tys -> 
                 if isEmpty tys then () // no extra arg = no sentinel 
                 else 
                    bb.EmitByte et_SENTINEL
                    Array.iter (EmitType cenv env bb) tys

        and GetCallsigAsBytes cenv env x = emitBytesViaBuffer (fun bb -> EmitCallsig cenv env bb x)

        and EmitTypes cenv env bb (inst: ILTypes) = 
            inst |> Array.iter (EmitType cenv env bb) 

        let GetTypeAsMemberRefParent cenv env ty =
            match GetTypeAsTypeDefOrRef cenv env ty with 
            | (tag, _) when tag = TypeDefOrRefOrSpecTag.TypeDef -> printfn "GetTypeAsMemberRefParent: mspec should have been encoded as mdtMethodDef?"; MemberRefParent (MemberRefParentTag.TypeRef, 1)
            | (tag, tok) when tag = TypeDefOrRefOrSpecTag.TypeRef -> MemberRefParent (MemberRefParentTag.TypeRef, tok)
            | (tag, tok) when tag = TypeDefOrRefOrSpecTag.TypeSpec -> MemberRefParent (MemberRefParentTag.TypeSpec, tok)
            | _ -> failwith "GetTypeAsMemberRefParent"




        // -------------------------------------------------------------------- 
        // Native types
        // -------------------------------------------------------------------- 

        let rec GetFieldInitAsBlobIdx cenv (x:ILFieldInit) = 
            GetBytesAsBlobIdx cenv (emitBytesViaBuffer (fun bb -> GetFieldInit bb x))

        // REVIEW: write into an accumuating buffer
        and GetFieldInit (bb: ByteBuffer) x = 
            match x with 
            | :? string as b -> bb.EmitBytes (Encoding.Unicode.GetBytes b)
            | :? bool as b ->  bb.EmitByte (if b then 0x01uy else 0x00uy)
            | :? char as x -> bb.EmitUInt16 (uint16 x)
            | :? int8 as x -> bb.EmitByte (byte  x)
            | :? int16 as x -> bb.EmitUInt16 (uint16 x)
            | :? int32 as x -> bb.EmitInt32 x
            | :? int64 as x -> bb.EmitInt64 x
            | :? uint8 as x -> bb.EmitByte x
            | :? uint16 as x -> bb.EmitUInt16 x
            | :? uint32 as x -> bb.EmitInt32 (int32 x)
            | :? uint64 as x -> bb.EmitInt64 (int64 x)
            | :? single as x -> bb.EmitInt32 (bitsOfSingle x)
            | :? double as x -> bb.EmitInt64 (bitsOfDouble x)
            | _ -> bb.EmitInt32 0

        and GetFieldInitFlags (i: ILFieldInit) = 
            UShort 
              (uint16
                (match i with 
                 | :? string -> et_STRING
                 | :? bool -> et_BOOLEAN
                 | :? char -> et_CHAR
                 | :? int8 -> et_I1
                 | :? int16 -> et_I2
                 | :? int32 -> et_I4
                 | :? int64 -> et_I8
                 | :? uint8 -> et_U1
                 | :? uint16 -> et_U2
                 | :? uint32 -> et_U4
                 | :? uint64 -> et_U8
                 | :? single -> et_R4
                 | :? double -> et_R8
                 | _ -> et_CLASS))
                          
        // -------------------------------------------------------------------- 
        // Type definitions
        // -------------------------------------------------------------------- 

        let GetMemberAccessFlags access = 
            match access with 
            | ILMemberAccess.CompilerControlled -> 0x00000000
            | ILMemberAccess.Public -> 0x00000006
            | ILMemberAccess.Private  -> 0x00000001
            | ILMemberAccess.Family  -> 0x00000004
            | ILMemberAccess.FamilyAndAssembly -> 0x00000002
            | ILMemberAccess.FamilyOrAssembly -> 0x00000005
            | ILMemberAccess.Assembly -> 0x00000003

        let GetTypeAccessFlags  access = 
            match access with 
            | ILTypeDefAccess.Public -> 0x00000001
            | ILTypeDefAccess.Private  -> 0x00000000
            | ILTypeDefAccess.Nested ILMemberAccess.Public -> 0x00000002
            | ILTypeDefAccess.Nested ILMemberAccess.Private  -> 0x00000003
            | ILTypeDefAccess.Nested ILMemberAccess.Family  -> 0x00000004
            | ILTypeDefAccess.Nested ILMemberAccess.FamilyAndAssembly -> 0x00000006
            | ILTypeDefAccess.Nested ILMemberAccess.FamilyOrAssembly -> 0x00000007
            | ILTypeDefAccess.Nested ILMemberAccess.Assembly -> 0x00000005
            | ILTypeDefAccess.Nested ILMemberAccess.CompilerControlled -> failwith "bad type acccess"

        let rec GetTypeDefAsRow cenv env _enc (td:ILTypeDef) = 
            let nselem, nelem = GetTypeNameAsElemPair cenv (td.Namespace, td.Name)
            let flags = 
              if td.Name = "<Module>" then 0x00000000
              else
                
                int td.Attributes |||
                begin 
                  match td.Layout with 
                  | ILTypeDefLayout.Auto ->  0x00000000
                  | ILTypeDefLayout.Sequential _  -> 0x00000008
                  | ILTypeDefLayout.Explicit _ -> 0x00000010
                end |||
                begin 
                  match td.Kind with
                  | ILTypeDefKind.Interface -> 0x00000020
                  | _ -> 0x00000000
                end |||
#if EMIT_SECURITY_DECLS
// @REVIEW    (if rtspecialname_of_tdef td then 0x00000800 else 0x00000000) ||| 
                (if td.HasSecurity || not td.SecurityDecls.Entries.IsEmpty then 0x00040000 else 0x00000000)
#endif
                0x0

            let tdorTag, tdorRow = GetTypeOptionAsTypeDefOrRef cenv env td.Extends
            UnsharedRow 
               [| ULong flags  
                  nelem 
                  nselem 
                  TypeDefOrRefOrSpec (tdorTag, tdorRow) 
                  SimpleIndex (ILTableNames.Field, cenv.fieldDefs.Count + 1) 
                  SimpleIndex (ILTableNames.Method, cenv.methodDefIdxsByKey.Count + 1) |]  

        and GetTypeOptionAsTypeDefOrRef cenv env tyOpt = 
            match tyOpt with
            | None -> (TypeDefOrRefOrSpecTag.TypeDef, 0)
            | Some ty -> (GetTypeAsTypeDefOrRef cenv env ty)

        and GetTypeDefAsPropertyMapRow cenv tidx = 
            UnsharedRow
                [| SimpleIndex (ILTableNames.TypeDef, tidx)
                   SimpleIndex (ILTableNames.Property, cenv.propertyDefs.Count + 1) |]  

        and GetTypeDefAsEventMapRow cenv tidx = 
            UnsharedRow
                [| SimpleIndex (ILTableNames.TypeDef, tidx)
                   SimpleIndex (ILTableNames.Event, cenv.eventDefs.Count + 1) |]  
            
        and GetKeyForFieldDef tidx (fd: ILFieldDef) = 
            FieldDefKey (tidx, fd.Name, fd.FieldType)

        and GenFieldDefPass2 cenv tidx fd = 
            ignore (cenv.fieldDefs.AddUniqueEntry "field" (fun (fdkey:FieldDefKey) -> fdkey.Name) (GetKeyForFieldDef tidx fd))

        and GetKeyForMethodDef tidx (md: ILMethodDef) = 
            MethodDefKey (tidx, md.GenericParams.Length, md.Name, md.Return.Type, md.ParameterTypes, md.CallingConv.IsStatic)

        and GenMethodDefPass2 cenv tidx md = 
            let idx = 
              cenv.methodDefIdxsByKey.AddUniqueEntry
                 "method" 
                 (fun (key:MethodDefKey) -> 
                   printfn "Duplicate in method table is:"
                   printfn "%s" ("  Type index: "+string key.TypeIdx)
                   printfn "%s" ("  Method name: "+key.Name)
                   printfn "%s" ("  Method arity (num generic params): "+string key.GenericArity)
                   key.Name
                 )
                 (GetKeyForMethodDef tidx md) 
            
            cenv.methodDefIdxs.[md] <- idx

        and GetKeyForPropertyDef tidx (x: ILPropertyDef)  = 
            PropKey (tidx, x.Name, x.PropertyType, x.IndexParameterTypes)

        and GenPropertyDefPass2 cenv tidx x = 
            ignore (cenv.propertyDefs.AddUniqueEntry "property" (fun (PropKey (_, n, _, _)) -> n) (GetKeyForPropertyDef tidx x))

        and GetTypeAsImplementsRow cenv env tidx ty =
            let tdorTag, tdorRow = GetTypeAsTypeDefOrRef cenv env ty
            UnsharedRow 
                [| SimpleIndex (ILTableNames.TypeDef, tidx) 
                   TypeDefOrRefOrSpec (tdorTag, tdorRow) |]

        and GenImplementsPass2 cenv env tidx ty =
            AddUnsharedRow cenv ILTableNames.InterfaceImpl (GetTypeAsImplementsRow cenv env tidx ty) |> ignore
              
        and GetKeyForEvent tidx (x: ILEventDef) = 
            EventKey (tidx, x.Name)

        and GenEventDefPass2 cenv tidx x = 
            ignore (cenv.eventDefs.AddUniqueEntry "event" (fun (EventKey(_, b)) -> b) (GetKeyForEvent tidx x))

        and GenTypeDefPass2 pidx enc cenv (td:ILTypeDef) =
           try 
              let env = envForTypeDef td
              let tidx = GetIdxForTypeDef cenv (TdKey(enc, td.Namespace, td.Name))
              let tidx2 = AddUnsharedRow cenv ILTableNames.TypeDef (GetTypeDefAsRow cenv env enc td)
              if tidx <> tidx2 then failwith "index of typedef on second pass does not match index on first pass"

              // Add entries to auxiliary mapping tables, e.g. Nested, PropertyMap etc. 
              // Note Nested is organised differently to the others... 
              if not (isNil enc) then
                  AddUnsharedRow cenv ILTableNames.Nested 
                      (UnsharedRow 
                          [| SimpleIndex (ILTableNames.TypeDef, tidx) 
                             SimpleIndex (ILTableNames.TypeDef, pidx) |]) |> ignore
              let props = td.Properties.Entries
              if not (isEmpty props) then 
                  AddUnsharedRow cenv ILTableNames.PropertyMap (GetTypeDefAsPropertyMapRow cenv tidx) |> ignore 
              let events = td.Events.Entries
              if not (isEmpty events) then 
                  AddUnsharedRow cenv ILTableNames.EventMap (GetTypeDefAsEventMapRow cenv tidx) |> ignore

              // Now generate or assign index numbers for tables referenced by the maps. 
              // Don't yet generate contents of these tables - leave that to pass3, as 
              // code may need to embed these entries. 
              td.Implements |> Array.iter (GenImplementsPass2 cenv env tidx)
              props |> Array.iter (GenPropertyDefPass2 cenv tidx)
              events |> Array.iter (GenEventDefPass2 cenv tidx)
              td.Fields.Entries |> Array.iter (GenFieldDefPass2 cenv tidx)
              td.Methods.Entries |> Array.iter (GenMethodDefPass2 cenv tidx)
              td.NestedTypes.Entries |> GenTypeDefsPass2 tidx (addILTypeName enc td) cenv
           with e ->
             failwith ("Error in pass2 for type "+td.Name+", error: "+e.Message)

        and GenTypeDefsPass2 pidx enc cenv tds =
            Array.iter (GenTypeDefPass2 pidx enc cenv) tds

        //=====================================================================
        // Pass 3 - write details of methods, fields, IL code, custom attrs etc.
        //=====================================================================

        exception MethodDefNotFound
        let FindMethodDefIdx cenv mdkey = 
            try cenv.methodDefIdxsByKey.GetTableEntry mdkey
            with :? KeyNotFoundException -> 
              let typeNameOfIdx i = 
                match 
                   (cenv.typeDefs.dict 
                     |> Seq.fold (fun  sofar kvp -> 
                        let tkey2 = kvp.Key 
                        let tidx2 = kvp.Value 
                        if i = tidx2 then 
                            if sofar = None then 
                                Some tkey2 
                            else failwith "multiple type names map to index" 
                        else sofar)  None) with 
                  | Some x -> x
                  | None -> raise MethodDefNotFound 
              let (TdKey (tenc, tnsp, tname)) = typeNameOfIdx mdkey.TypeIdx
              printfn "%s" ("The local method '"+(String.concat "." (tenc@[tname]))+"'::'"+mdkey.Name+"' was referenced but not declared")
              printfn "generic arity: %s " (string mdkey.GenericArity)
              cenv.methodDefIdxsByKey.dict |> Seq.iter (fun (KeyValue(mdkey2, _)) -> 
                  if mdkey2.TypeIdx = mdkey.TypeIdx && mdkey.Name = mdkey2.Name then 
                      let (TdKey (tenc2, tnsp2, tname2)) = typeNameOfIdx mdkey2.TypeIdx
                      printfn "%s" ("A method in '"+(String.concat "." (tenc2@[tname2]))+"' had the right name but the wrong signature:")
                      printfn "%s" ("generic arity: "+string mdkey2.GenericArity) 
                      printfn "mdkey2 = %s" (mdkey2.ToString()))
              raise MethodDefNotFound


        let rec GetMethodDefIdx cenv md = 
            cenv.methodDefIdxs.[md]

        and FindFieldDefIdx cenv fdkey = 
            try cenv.fieldDefs.GetTableEntry fdkey 
            with :? KeyNotFoundException -> 
              failwith ("The local field "+fdkey.Name+" was referenced but not declared")
              1

        and GetFieldDefAsFieldDefIdx cenv tidx fd = 
            FindFieldDefIdx cenv (GetKeyForFieldDef tidx fd) 

        // -------------------------------------------------------------------- 
        // ILMethodRef --> ILMethodDef.  
        // 
        // Only successfuly converts ILMethodRef's referring to 
        // methods in the module being emitted.
        // -------------------------------------------------------------------- 

        let GetMethodRefAsMethodDefIdx cenv (mref:ILMethodRef) =
            let tref = mref.EnclosingTypeRef
            try 
                if not (isTypeRefLocal tref) then
                     failwithf "method referred to by method impl, event or property is not in a type defined in this module, method ref is %A" mref
                let tidx = GetIdxForTypeDef cenv (TdKey(enclosing tref.Scope, tref.Namespace, tref.Name))
                let mdkey = MethodDefKey (tidx, mref.GenericArity, mref.Name, mref.ReturnType, mref.ArgTypes, mref.CallingConv.IsStatic)
                FindMethodDefIdx cenv mdkey
            with e ->
                failwithf "Error in GetMethodRefAsMethodDefIdx for mref = %A, error: %s" (mref.Name, tref.Name)  e.Message

        let rec MethodRefInfoAsMemberRefRow cenv env fenv (nm, typ, callconv, args, ret, varargs, genarity) =
            MemberRefRow(GetTypeAsMemberRefParent cenv env typ, 
                         GetStringHeapIdx cenv nm, 
                         GetMethodRefInfoAsBlobIdx cenv fenv (callconv, args, ret, varargs, genarity))

        and GetMethodRefInfoAsBlobIdx cenv env info = 
            GetBytesAsBlobIdx cenv (GetCallsigAsBytes cenv env info)

        let GetMethodRefInfoAsMemberRefIdx cenv env  ((_, typ, _, _, _, _, _) as minfo) = 
            let fenv = envForMethodRef env typ
            FindOrAddSharedRow cenv ILTableNames.MemberRef (MethodRefInfoAsMemberRefRow cenv env fenv  minfo)

        let GetMethodRefInfoAsMethodRefOrDef isAlwaysMethodDef cenv env ((nm, typ:ILType, cc, args, ret, varargs, genarity) as minfo) =
            if Option.isNone varargs && (isAlwaysMethodDef || isTypeLocal typ) then
                if not typ.IsNominal then failwith "GetMethodRefInfoAsMethodRefOrDef: unexpected local tref-typ"
                try (MethodDefOrRefTag.MethodDef, GetMethodRefAsMethodDefIdx cenv (ILMethodRef (typ.TypeRef, cc, genarity, nm, args, ret)))
                with MethodDefNotFound -> (MethodDefOrRefTag.MemberRef, GetMethodRefInfoAsMemberRefIdx cenv env minfo)
            else (MethodDefOrRefTag.MemberRef, GetMethodRefInfoAsMemberRefIdx cenv env minfo)


        // -------------------------------------------------------------------- 
        // ILMethodSpec --> ILMethodRef/ILMethodDef/ILMethodSpec
        // -------------------------------------------------------------------- 

        let rec GetMethodSpecInfoAsMethodSpecIdx cenv env (nm, typ, cc, args, ret, varargs, minst:ILGenericArgs) = 
            let mdorTag, mdorRow = GetMethodRefInfoAsMethodRefOrDef false cenv env (nm, typ, cc, args, ret, varargs, minst.Length)
            let blob = 
                emitBytesViaBuffer (fun bb -> 
                    bb.EmitByte e_IMAGE_CEE_CS_CALLCONV_GENERICINST
                    bb.EmitZ32 minst.Length
                    minst |> Array.iter (EmitType cenv env bb))
            FindOrAddSharedRow cenv ILTableNames.MethodSpec 
              (SharedRow 
                  [| MethodDefOrRef (mdorTag, mdorRow)
                     Blob (GetBytesAsBlobIdx cenv blob) |])

        and GetMethodDefOrRefAsUncodedToken (tag, idx) =
            let tab = 
                if tag = MethodDefOrRefTag.MethodDef then ILTableNames.Method
                elif tag = MethodDefOrRefTag.MemberRef then ILTableNames.MemberRef  
                else failwith "GetMethodDefOrRefAsUncodedToken"
            getUncodedToken tab idx

        and GetMethodSpecInfoAsUncodedToken cenv env ((_, _, _, _, _, _, minst:ILGenericArgs) as minfo) =
            if minst.Length > 0 then 
              getUncodedToken ILTableNames.MethodSpec (GetMethodSpecInfoAsMethodSpecIdx cenv env minfo)
            else 
              GetMethodDefOrRefAsUncodedToken (GetMethodRefInfoAsMethodRefOrDef false cenv env (GetMethodRefInfoOfMethodSpecInfo minfo))

        and GetMethodSpecAsUncodedToken cenv env mspec = 
            GetMethodSpecInfoAsUncodedToken cenv env (InfoOfMethodSpec mspec)

        and GetMethodRefInfoOfMethodSpecInfo (nm, typ, cc, args, ret, varargs, minst:ILGenericArgs) = 
            (nm, typ, cc, args, ret, varargs, minst.Length)

        and GetMethodSpecAsMethodDefOrRef cenv env (mspec, varargs) =
            GetMethodRefInfoAsMethodRefOrDef false cenv env (GetMethodRefInfoOfMethodSpecInfo (InfoOfMethodSpec (mspec, varargs)))

        and GetMethodSpecAsMethodDef cenv env (mspec, varargs) =
            GetMethodRefInfoAsMethodRefOrDef true cenv env (GetMethodRefInfoOfMethodSpecInfo (InfoOfMethodSpec (mspec, varargs)))

        and InfoOfMethodSpec (mspec:ILMethodSpec, varargs) = 
              (mspec.Name, 
               mspec.EnclosingType, 
               mspec.CallingConv, 
               mspec.MethodRef.ArgTypes, 
               mspec.FormalReturnType, 
               varargs, 
               mspec.GenericArgs)

        // -------------------------------------------------------------------- 
        // method_in_parent --> ILMethodRef/ILMethodDef
        // 
        // Used for MethodImpls.
        // --------------------------------------------------------------------

        let rec GetOverridesSpecAsMemberRefIdx cenv env ospec = 
            let fenv = envForOverrideSpec ospec
            let row = MethodRefInfoAsMemberRefRow cenv env fenv  (ospec.MethodRef.Name, ospec.EnclosingType, ospec.MethodRef.CallingConv, ospec.MethodRef.ArgTypes, ospec.MethodRef.ReturnType, None, ospec.MethodRef.GenericArity)
            FindOrAddSharedRow cenv ILTableNames.MemberRef  row
             
        and GetOverridesSpecAsMethodDefOrRef cenv env (ospec:ILOverridesSpec) =
            let typ = ospec.EnclosingType
            if isTypeLocal typ then 
                if not typ.IsNominal then failwith "GetOverridesSpecAsMethodDefOrRef: unexpected local tref-typ" 
                try (MethodDefOrRefTag.MethodDef, GetMethodRefAsMethodDefIdx cenv ospec.MethodRef)
                with MethodDefNotFound ->  (MethodDefOrRefTag.MemberRef, GetOverridesSpecAsMemberRefIdx cenv env ospec) 
            else 
                (MethodDefOrRefTag.MemberRef, GetOverridesSpecAsMemberRefIdx cenv env ospec) 

        // -------------------------------------------------------------------- 
        // ILMethodRef --> ILMethodRef/ILMethodDef
        // 
        // Used for Custom Attrs.
        // -------------------------------------------------------------------- 

        let rec GetMethodRefAsMemberRefIdx cenv env fenv (mref:ILMethodRef) = 
            let row = MethodRefInfoAsMemberRefRow cenv env fenv (mref.Name, ILType.Boxed (ILTypeSpec (mref.EnclosingTypeRef, [| |])), mref.CallingConv, mref.ArgTypes, mref.ReturnType, None, mref.GenericArity)
            FindOrAddSharedRow cenv ILTableNames.MemberRef row

        and GetMethodRefAsCustomAttribType cenv (mref:ILMethodRef) =
            let fenv = envForNonGenericMethodRef mref
            let tref = mref.EnclosingTypeRef
            if isTypeRefLocal tref then
                try (CustomAttributeTypeTag.MethodDef, GetMethodRefAsMethodDefIdx cenv mref)
                with MethodDefNotFound -> (CustomAttributeTypeTag.MemberRef, GetMethodRefAsMemberRefIdx cenv fenv fenv mref)
            else
                (CustomAttributeTypeTag.MemberRef, GetMethodRefAsMemberRefIdx cenv fenv fenv mref)

        // -------------------------------------------------------------------- 
        // ILCustomAttrs --> CustomAttribute rows
        // -------------------------------------------------------------------- 

        let rec GetCustomAttrDataAsBlobIdx cenv (data:byte[]) = 
            if data.Length = 0 then 0 else GetBytesAsBlobIdx cenv data

        and GetCustomAttrRow cenv hca attr = 
            let cat = GetMethodRefAsCustomAttribType cenv attr.Method.MethodRef
            for element in attr.Elements do
                match element with
                | :? ILType as ty when ty.IsNominal -> GetTypeRefAsTypeRefIdx cenv ty.TypeRef |> ignore
                | _ -> ()

            UnsharedRow
                    [| HasCustomAttribute (fst hca, snd hca)
                       CustomAttributeType (fst cat, snd cat)
                       Blob (GetCustomAttrDataAsBlobIdx cenv attr.Data)
                    |]

        and GenCustomAttrPass3Or4 cenv hca attr = 
            AddUnsharedRow cenv ILTableNames.CustomAttribute (GetCustomAttrRow cenv hca attr) |> ignore

        and GenCustomAttrsPass3Or4 cenv hca (attrs: ILCustomAttrs) = 
            attrs.Entries |> Array.iter (GenCustomAttrPass3Or4 cenv hca) 

        // -------------------------------------------------------------------- 
        // ILPermissionSet --> DeclSecurity rows
        // -------------------------------------------------------------------- *)

#if EMIT_SECURITY_DECLS
        let rec GetSecurityDeclRow cenv hds (PermissionSet (action, s)) = 
            UnsharedRow 
                [| UShort (uint16 (List.assoc action (Lazy.force ILSecurityActionMap)))
                   HasDeclSecurity (fst hds, snd hds)
                   Blob (GetBytesAsBlobIdx cenv s) |]  

        and GenSecurityDeclPass3 cenv hds attr = 
            AddUnsharedRow cenv ILTableNames.Permission (GetSecurityDeclRow cenv hds attr) |> ignore

        and GenSecurityDeclsPass3 cenv hds attrs = 
            List.iter (GenSecurityDeclPass3 cenv hds) attrs 
#endif

        // -------------------------------------------------------------------- 
        // ILFieldSpec --> FieldRef  or ILFieldDef row
        // -------------------------------------------------------------------- 

        let rec GetFieldSpecAsMemberRefRow cenv env fenv (fspec:ILFieldSpec) = 
            MemberRefRow (GetTypeAsMemberRefParent cenv env fspec.EnclosingType, 
                          GetStringHeapIdx cenv fspec.Name, 
                          GetFieldSpecSigAsBlobIdx cenv fenv fspec)

        and GetFieldSpecAsMemberRefIdx cenv env fspec = 
            let fenv = envForFieldSpec fspec
            FindOrAddSharedRow cenv ILTableNames.MemberRef (GetFieldSpecAsMemberRefRow cenv env fenv fspec)

        // REVIEW: write into an accumuating buffer
        and EmitFieldSpecSig cenv env (bb: ByteBuffer) (fspec:ILFieldSpec) = 
            bb.EmitByte e_IMAGE_CEE_CS_CALLCONV_FIELD
            EmitType cenv env bb fspec.FormalType

        and GetFieldSpecSigAsBytes cenv env x = 
            emitBytesViaBuffer (fun bb -> EmitFieldSpecSig cenv env bb x) 

        and GetFieldSpecSigAsBlobIdx cenv env x = 
            GetBytesAsBlobIdx cenv (GetFieldSpecSigAsBytes cenv env x)

        and GetFieldSpecAsFieldDefOrRef cenv env (fspec:ILFieldSpec) =
            let typ = fspec.EnclosingType
            if isTypeLocal typ then
                if not typ.IsNominal then failwith "GetFieldSpecAsFieldDefOrRef: unexpected local tref-typ"
                let tref = typ.TypeRef
                let tidx = GetIdxForTypeDef cenv (TdKey(enclosing tref.Scope, tref.Namespace, tref.Name))
                let fdkey = FieldDefKey (tidx, fspec.Name, fspec.FormalType)
                (true, FindFieldDefIdx cenv fdkey)
            else 
                (false, GetFieldSpecAsMemberRefIdx cenv env fspec)

        and GetFieldDefOrRefAsUncodedToken (tag, idx) =
            let tab = if tag then ILTableNames.Field else ILTableNames.MemberRef
            getUncodedToken tab idx

        // -------------------------------------------------------------------- 
        // callsig --> StandAloneSig
        // -------------------------------------------------------------------- 

        let GetCallsigAsBlobIdx cenv env (callsig:ILCallingSignature, varargs) = 
            GetBytesAsBlobIdx cenv 
              (GetCallsigAsBytes cenv env (callsig.CallingConv, 
                                              callsig.ArgTypes, 
                                              callsig.ReturnType, varargs, 0))
            
        let GetCallsigAsStandAloneSigRow cenv env x = 
            SharedRow [| Blob (GetCallsigAsBlobIdx cenv env x) |]

        let GetCallsigAsStandAloneSigIdx cenv env info = 
            FindOrAddSharedRow cenv ILTableNames.StandAloneSig (GetCallsigAsStandAloneSigRow cenv env info)

        // -------------------------------------------------------------------- 
        // local signatures --> BlobHeap idx
        // -------------------------------------------------------------------- 

        let EmitLocalSig cenv env (bb: ByteBuffer) (locals: ILLocals) = 
            bb.EmitByte e_IMAGE_CEE_CS_CALLCONV_LOCAL_SIG
            bb.EmitZ32 locals.Length
            locals |> Array.iter (EmitLocalInfo cenv env bb)

        let GetLocalSigAsBlobHeapIdx cenv env locals = 
            GetBytesAsBlobIdx cenv (emitBytesViaBuffer (fun bb -> EmitLocalSig cenv env bb locals))

        let GetLocalSigAsStandAloneSigIdx cenv env locals = 
            SharedRow [| Blob (GetLocalSigAsBlobHeapIdx cenv env locals) |]



        type ExceptionClauseKind = 
          | FinallyClause 
          | FaultClause 
          | TypeFilterClause of int32 
          | FilterClause of int

        type ExceptionClauseSpec = (int * int * int * int * ExceptionClauseKind)

        type CodeBuffer = 

            // -------------------------------------------------------------------- 
            // Buffer to write results of emitting code into.  Also record:
            //   - branch sources (where fixups will occur)
            //   - possible branch destinations
            //   - locations of embedded handles into the string table
            //   - the exception table
            // -------------------------------------------------------------------- 
            { code: ByteBuffer 
              /// (instruction; optional short form); start of instr in code buffer; code loc for the end of the instruction the fixup resides in ; where is the destination of the fixup 
              mutable reqdBrFixups: ResizeArray<((int * int option) * int * ILCodeLabel list)>
              availBrFixups: Dictionary<ILCodeLabel, int> 
              /// code loc to fixup in code buffer 
              mutable reqdStringFixupsInMethod: ResizeArray<(int * int)>
              /// data for exception handling clauses 
              mutable seh: ResizeArray<ExceptionClauseSpec>
#if DEBUG_INFO
              seqpoints: ResizeArray<PdbSequencePoint> 
#endif
             }

            static member Create _nm = 
                { seh = ResizeArray()
                  code= ByteBuffer.Create 200
                  reqdBrFixups= ResizeArray()
                  reqdStringFixupsInMethod=ResizeArray()
                  availBrFixups = Dictionary<_, _>(10, HashIdentity.Structural) 
#if DEBUG_INFO
                  seqpoints = new ResizeArray<_>(10)
#endif
                }

            member codebuf.EmitExceptionClause seh = codebuf.seh.Add(seh)

#if DEBUG_INFO
            member codebuf.EmitSeqPoint cenv (m:ILSourceMarker)  = ()
                if cenv.generatePdb then 
                  // table indexes are 1-based, document array indexes are 0-based 
                  let doc = (cenv.documents.FindOrAddSharedEntry m.Document) - 1  
                  codebuf.seqpoints.Add 
                    { Document=doc
                      Offset= codebuf.code.Position
                      Line=m.Line
                      Column=m.Column
                      EndLine=m.EndLine
                      EndColumn=m.EndColumn }
#endif

            member codebuf.EmitByte x = codebuf.code.EmitIntAsByte x
            member codebuf.EmitUInt16 x = codebuf.code.EmitUInt16 x
            member codebuf.EmitInt32 x = codebuf.code.EmitInt32 x
            member codebuf.EmitInt64 x = codebuf.code.EmitInt64 x

            member codebuf.EmitUncodedToken u = codebuf.EmitInt32 u

            member codebuf.RecordReqdStringFixup stringidx = 
                codebuf.reqdStringFixupsInMethod.Add((codebuf.code.Position, stringidx))
                // Write a special value in that we check later when applying the fixup 
                codebuf.EmitInt32 0xdeadbeef

            member codebuf.RecordReqdBrFixups i tgs = 
                codebuf.reqdBrFixups.Add ((i, codebuf.code.Position, tgs))
                // Write a special value in that we check later when applying the fixup 
                // Value is 0x11 {deadbbbb}* where 11 is for the instruction and deadbbbb is for each target 
                codebuf.EmitByte 0x11 // for the instruction 
                (if fst i = i_switch then 
                  codebuf.EmitInt32 tgs.Length)
                List.iter (fun _ -> codebuf.EmitInt32 0xdeadbbbb) tgs

            member codebuf.RecordReqdBrFixup i tg = codebuf.RecordReqdBrFixups i [tg]
            member codebuf.RecordAvailBrFixup tg = 
                codebuf.availBrFixups.[tg] <- codebuf.code.Position

        module Codebuf = 
             // -------------------------------------------------------------------- 
             // Applying branch fixups.  Use short versions of instructions
             // wherever possible.  Sadly we can only determine if we can use a short
             // version after we've layed out the code for all other instructions.  
             // This in turn means that using a short version may change 
             // the various offsets into the code.
             // -------------------------------------------------------------------- 

            let binaryChop p (arr: 'T[]) = 
                let rec go n m =
                    if n > m then raise (KeyNotFoundException("binary chop did not find element"))
                    else 
                        let i = (n+m)/2
                        let c = p arr.[i] 
                        if c = 0 then i elif c < 0 then go n (i-1) else go (i+1) m
                go 0 (Array.length arr)

            let applyBrFixups (origCode :byte[]) origExnClauses origReqdStringFixups (origAvailBrFixups: Dictionary<ILCodeLabel, int>) origReqdBrFixups = 
              let orderedOrigReqdBrFixups = origReqdBrFixups |> Array.sortBy (fun (_, fixuploc, _) -> fixuploc)

              let newCode = ByteBuffer.Create origCode.Length

              // Copy over all the code, working out whether the branches will be short 
              // or long and adjusting the branch destinations.  Record an adjust function to adjust all the other 
              // gumpf that refers to fixed offsets in the code stream. 
              let newCode, newReqdBrFixups, adjuster = 
                  let remainingReqdFixups = ref (Array.toList orderedOrigReqdBrFixups)
                  let origWhere = ref 0
                  let newWhere = ref 0
                  let doneLast = ref false
                  let newReqdBrFixups = ref []

                  let adjustments = ref []

                  while (not (isNil !remainingReqdFixups) || not !doneLast) do
                      let doingLast = isNil !remainingReqdFixups  
                      let origStartOfNoBranchBlock = !origWhere
                      let newStartOfNoBranchBlock = !newWhere

                      let origEndOfNoBranchBlock = 
                        if doingLast then origCode.Length
                        else 
                          let (_, origStartOfInstr, _) = List.head !remainingReqdFixups
                          origStartOfInstr

                      // Copy over a chunk of non-branching code 
                      let nobranch_len = origEndOfNoBranchBlock - origStartOfNoBranchBlock
                      newCode.EmitBytes origCode.[origStartOfNoBranchBlock..origStartOfNoBranchBlock+nobranch_len-1]
                        
                      // Record how to adjust addresses in this range, including the branch instruction 
                      // we write below, or the end of the method if we're doing the last bblock 
                      adjustments := (origStartOfNoBranchBlock, origEndOfNoBranchBlock, newStartOfNoBranchBlock) :: !adjustments
                     
                      // Increment locations to the branch instruction we're really interested in  
                      origWhere := origEndOfNoBranchBlock
                      newWhere := !newWhere + nobranch_len
                        
                      // Now do the branch instruction.  Decide whether the fixup will be short or long in the new code 
                      if doingLast then 
                          doneLast := true
                      else 
                          let (i, origStartOfInstr, tgs:ILCodeLabel list) = List.head !remainingReqdFixups
                          remainingReqdFixups := List.tail !remainingReqdFixups
                          if origCode.[origStartOfInstr] <> 0x11uy then failwith "br fixup sanity check failed (1)"
                          let i_length = if fst i = i_switch then 5 else 1
                          origWhere := !origWhere + i_length

                          let origEndOfInstr = origStartOfInstr + i_length + 4 * tgs.Length
                          let newEndOfInstrIfSmall = !newWhere + i_length + 1
                          let newEndOfInstrIfBig = !newWhere + i_length + 4 * tgs.Length
                          
                          let short = 
                            match i, tgs with 
                            | (_, Some i_short), [tg] 
                                when
                                  begin 
                                    // Use the original offsets to compute if the branch is small or large.  This is 
                                    // a safe approximation because code only gets smaller. 
                                    if not (origAvailBrFixups.ContainsKey tg) then 
                                        printfn "%s" ("branch target " + formatCodeLabel tg + " not found in code")
                                    let origDest = 
                                        if origAvailBrFixups.ContainsKey tg then origAvailBrFixups.[tg]
                                        else 666666
                                    let origRelOffset = origDest - origEndOfInstr
                                    -128 <= origRelOffset && origRelOffset <= 127
                                  end 
                              ->
                                newCode.EmitIntAsByte i_short
                                true
                            | (i_long, _), _ ->
                                newCode.EmitIntAsByte i_long
                                (if i_long = i_switch then 
                                  newCode.EmitInt32 tgs.Length)
                                false
                          
                          newWhere := !newWhere + i_length
                          if !newWhere <> newCode.Position then printfn "mismatch between newWhere and newCode"

                          tgs |> List.iter (fun tg ->
                                let origFixupLoc = !origWhere
                                checkFixup32 origCode origFixupLoc 0xdeadbbbb
                                
                                if short then 
                                    newReqdBrFixups := (!newWhere, newEndOfInstrIfSmall, tg, true) :: !newReqdBrFixups
                                    newCode.EmitIntAsByte 0x98 (* sanity check *)
                                    newWhere := !newWhere + 1
                                else 
                                    newReqdBrFixups := (!newWhere, newEndOfInstrIfBig, tg, false) :: !newReqdBrFixups
                                    newCode.EmitInt32 0xf00dd00f (* sanity check *)
                                    newWhere := !newWhere + 4
                                if !newWhere <> newCode.Position then printfn "mismatch between newWhere and newCode"
                                origWhere := !origWhere + 4)
                          
                          if !origWhere <> origEndOfInstr then printfn "mismatch between origWhere and origEndOfInstr"

                  let adjuster  = 
                    let arr = Array.ofList (List.rev !adjustments)
                    fun addr -> 
                      let i = 
                          try binaryChop (fun (a1, a2, _) -> if addr < a1 then -1 elif addr > a2 then 1 else 0) arr 
                          with 
                             :? KeyNotFoundException -> 
                                 failwith ("adjuster: address "+string addr+" is out of range")
                      let (origStartOfNoBranchBlock, _, newStartOfNoBranchBlock) = arr.[i]
                      addr - (origStartOfNoBranchBlock - newStartOfNoBranchBlock) 

                  newCode.Close(), 
                  List.toArray !newReqdBrFixups, 
                  adjuster

              // Now adjust everything 
              let newAvailBrFixups = 
                  let tab = Dictionary<_, _>(10, HashIdentity.Structural) 
                  for (KeyValue(tglab, origBrDest)) in origAvailBrFixups do 
                      tab.[tglab]  <- adjuster origBrDest
                  tab
              let newReqdStringFixups = Array.map (fun (origFixupLoc, stok) -> adjuster origFixupLoc, stok) origReqdStringFixups
#if EMIT_DEBUG_INFO
              let newSeqPoints = Array.map (fun (sp:PdbSequencePoint) -> {sp with Offset=adjuster sp.Offset}) origSeqPoints
#endif
              let newExnClauses = 
                  origExnClauses |> Array.map (fun (st1, sz1, st2, sz2, kind) ->
                      (adjuster st1, (adjuster (st1 + sz1) - adjuster st1), 
                       adjuster st2, (adjuster (st2 + sz2) - adjuster st2), 
                       (match kind with 
                       | FinallyClause | FaultClause | TypeFilterClause _ -> kind
                       | FilterClause n -> FilterClause (adjuster n))))
                    
#if EMIT_DEBUG_INFO
              let newScopes =
                let rec remap scope =
                  {scope with StartOffset = adjuster scope.StartOffset
                              EndOffset = adjuster scope.EndOffset
                              Children = Array.map remap scope.Children }
                Array.map remap origScopes
#endif

              // Now apply the adjusted fixups in the new code 
              newReqdBrFixups |> Array.iter (fun (newFixupLoc, endOfInstr, tg, small) ->
                    if not (newAvailBrFixups.ContainsKey tg) then 
                      failwith ("target "+formatCodeLabel tg+" not found in new fixups")
                    try 
                        let n = newAvailBrFixups.[tg]
                        let relOffset = (n - endOfInstr)
                        if small then 
                            if Bytes.get newCode newFixupLoc <> 0x98 then failwith "br fixupsanity check failed"
                            newCode.[newFixupLoc] <- b0 relOffset
                        else 
                            checkFixup32 newCode newFixupLoc 0xf00dd00fl
                            applyFixup32 newCode newFixupLoc relOffset
                    with :? KeyNotFoundException -> ())

              newCode, newReqdStringFixups, newExnClauses


            // -------------------------------------------------------------------- 
            // Structured residue of emitting instructions: SEH exception handling
            // and scopes for local variables.
            // -------------------------------------------------------------------- 

            // Emitting instructions generates a tree of seh specifications 
            // We then emit the exception handling specs separately. 
            // nb. ECMA spec says the SEH blocks must be returned inside-out 
            type SEHTree = 
              | Node of ExceptionClauseSpec option * SEHTree[]
                

            // -------------------------------------------------------------------- 
            // Table of encodings for instructions without arguments, also indexes
            // for all instructions.
            // -------------------------------------------------------------------- 

            let encodingsForNoArgInstrs = Dictionary<_, _>(300, HashIdentity.Structural)
            let _ = 
              List.iter 
                (fun (x, mk) -> encodingsForNoArgInstrs.[mk] <- x)
                (noArgInstrs.Force())
            let encodingsOfNoArgInstr si = encodingsForNoArgInstrs.[si]

            // -------------------------------------------------------------------- 
            // Emit instructions
            // -------------------------------------------------------------------- 

            /// Emit the code for an instruction
            let emitInstrCode (codebuf: CodeBuffer) i = 
                if i > 0xFF then 
                    assert (i >>> 8 = 0xFE) 
                    codebuf.EmitByte ((i >>> 8)  &&& 0xFF) 
                    codebuf.EmitByte (i &&& 0xFF) 
                else 
                    codebuf.EmitByte i

            let emitTypeInstr cenv codebuf env i ty = 
                emitInstrCode codebuf i 
                codebuf.EmitUncodedToken (getTypeDefOrRefAsUncodedToken (GetTypeAsTypeDefOrRef cenv env ty))

            let emitMethodSpecInfoInstr cenv codebuf env i mspecinfo = 
                emitInstrCode codebuf i 
                codebuf.EmitUncodedToken (GetMethodSpecInfoAsUncodedToken cenv env mspecinfo)

            let emitMethodSpecInstr cenv codebuf env i mspec = 
                emitInstrCode codebuf i 
                codebuf.EmitUncodedToken (GetMethodSpecAsUncodedToken cenv env mspec)

            let emitFieldSpecInstr cenv codebuf env i fspec = 
                emitInstrCode codebuf i 
                codebuf.EmitUncodedToken (GetFieldDefOrRefAsUncodedToken (GetFieldSpecAsFieldDefOrRef cenv env fspec))

            let emitShortUInt16Instr codebuf (i_short, i) x = 
                let n = int32 x
                if n <= 255 then 
                    emitInstrCode codebuf i_short 
                    codebuf.EmitByte n
                else 
                    emitInstrCode codebuf i 
                    codebuf.EmitUInt16 x

            let emitShortInt32Instr codebuf (i_short, i) x = 
                if x >= (-128) && x <= 127 then 
                    emitInstrCode codebuf i_short 
                    codebuf.EmitByte (if x < 0x0 then x + 256 else x)
                else 
                    emitInstrCode codebuf i 
                    codebuf.EmitInt32 x

            let emitTailness (cenv: cenv) codebuf tl = 
                if tl = Tailcall && cenv.emitTailcalls then emitInstrCode codebuf i_tail

            //let emitAfterTailcall codebuf tl =
            //    if tl = Tailcall then emitInstrCode codebuf i_ret

            let emitVolatility codebuf tl = 
                if tl = Volatile then emitInstrCode codebuf i_volatile

            let emitConstrained cenv codebuf env ty = 
                emitInstrCode codebuf i_constrained
                codebuf.EmitUncodedToken (getTypeDefOrRefAsUncodedToken (GetTypeAsTypeDefOrRef cenv env ty))

            let emitAlignment codebuf tl = 
                match tl with 
                | Aligned -> ()
                | Unaligned1 -> emitInstrCode codebuf i_unaligned; codebuf.EmitByte 0x1
                | Unaligned2 -> emitInstrCode codebuf i_unaligned; codebuf.EmitByte 0x2
                | Unaligned4 -> emitInstrCode codebuf i_unaligned; codebuf.EmitByte 0x4

            let rec emitInstr cenv codebuf env instr =
                match instr with
                | si when isNoArgInstr si ->
                     emitInstrCode codebuf (encodingsOfNoArgInstr si)
                | I_brcmp (cmp, tg1)  -> 
                    codebuf.RecordReqdBrFixup (ILCmpInstrMap.Value.[cmp], Some ILCmpInstrRevMap.Value.[cmp]) tg1
                | I_br tg -> codebuf.RecordReqdBrFixup (i_br, Some i_br_s) tg
#if EMIT_DEBUG_INFO
                | I_seqpoint s ->   codebuf.EmitSeqPoint cenv s
#endif
                | I_leave tg -> codebuf.RecordReqdBrFixup (i_leave, Some i_leave_s) tg
                | I_call  (tl, mspec, varargs)      -> 
                    emitTailness cenv codebuf tl
                    emitMethodSpecInstr cenv codebuf env i_call (mspec, varargs)
                    //emitAfterTailcall codebuf tl
                | I_callvirt      (tl, mspec, varargs)      -> 
                    emitTailness cenv codebuf tl
                    emitMethodSpecInstr cenv codebuf env i_callvirt (mspec, varargs)
                    //emitAfterTailcall codebuf tl
                | I_callconstraint        (tl, ty, mspec, varargs)   -> 
                    emitTailness cenv codebuf tl
                    emitConstrained cenv codebuf env ty
                    emitMethodSpecInstr cenv codebuf env i_callvirt (mspec, varargs)
                    //emitAfterTailcall codebuf tl
                | I_newobj        (mspec, varargs) -> 
                    emitMethodSpecInstr cenv codebuf env i_newobj (mspec, varargs)
                | I_ldftn mspec   -> 
                    emitMethodSpecInstr cenv codebuf env i_ldftn (mspec, None)
                | I_ldvirtftn     mspec   -> 
                    emitMethodSpecInstr cenv codebuf env i_ldvirtftn (mspec, None)

                | I_calli (tl, callsig, varargs)    -> 
                    emitTailness cenv codebuf tl
                    emitInstrCode codebuf i_calli 
                    codebuf.EmitUncodedToken (getUncodedToken ILTableNames.StandAloneSig (GetCallsigAsStandAloneSigIdx cenv env (callsig, varargs)))
                    //emitAfterTailcall codebuf tl

                | I_ldarg x ->  emitShortUInt16Instr codebuf (i_ldarg_s, i_ldarg) (uint16 x)
                | I_starg x ->  emitShortUInt16Instr codebuf (i_starg_s, i_starg) (uint16 x)
                | I_ldarga x ->  emitShortUInt16Instr codebuf (i_ldarga_s, i_ldarga) (uint16 x)
                | I_ldloc x ->  emitShortUInt16Instr codebuf (i_ldloc_s, i_ldloc) (uint16 x)
                | I_stloc x ->  emitShortUInt16Instr codebuf (i_stloc_s, i_stloc) (uint16 x)
                | I_ldloca x ->  emitShortUInt16Instr codebuf (i_ldloca_s, i_ldloca) (uint16 x)

                | I_cpblk (al, vol)        -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitInstrCode codebuf i_cpblk
                | I_initblk       (al, vol)        -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitInstrCode codebuf i_initblk

                | (I_ldc (DT_I4, ILConst.I4 x)) -> 
                    emitShortInt32Instr codebuf (i_ldc_i4_s, i_ldc_i4) x
                | (I_ldc (DT_I8, ILConst.I8 x)) -> 
                    emitInstrCode codebuf i_ldc_i8 
                    codebuf.EmitInt64 x
                | (I_ldc (_, ILConst.R4 x)) -> 
                    emitInstrCode codebuf i_ldc_r4 
                    codebuf.EmitInt32 (bitsOfSingle x)
                | (I_ldc (_, ILConst.R8 x)) -> 
                    emitInstrCode codebuf i_ldc_r8 
                    codebuf.EmitInt64 (bitsOfDouble x)

                | I_ldind (al, vol, dt)     -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitInstrCode codebuf 
                      (match dt with 
                      | DT_I -> i_ldind_i
                      | DT_I1  -> i_ldind_i1     
                      | DT_I2  -> i_ldind_i2     
                      | DT_I4  -> i_ldind_i4     
                      | DT_U1  -> i_ldind_u1     
                      | DT_U2  -> i_ldind_u2     
                      | DT_U4  -> i_ldind_u4     
                      | DT_I8  -> i_ldind_i8     
                      | DT_R4  -> i_ldind_r4     
                      | DT_R8  -> i_ldind_r8     
                      | DT_REF  -> i_ldind_ref
                      | _ -> failwith "ldind")

                | I_stelem dt     -> 
                    emitInstrCode codebuf 
                      (match dt with 
                      | DT_I | DT_U -> i_stelem_i
                      | DT_U1 | DT_I1  -> i_stelem_i1     
                      | DT_I2 | DT_U2  -> i_stelem_i2     
                      | DT_I4 | DT_U4  -> i_stelem_i4     
                      | DT_I8 | DT_U8  -> i_stelem_i8     
                      | DT_R4  -> i_stelem_r4     
                      | DT_R8  -> i_stelem_r8     
                      | DT_REF  -> i_stelem_ref
                      | _ -> failwith "stelem")

                | I_ldelem dt     -> 
                    emitInstrCode codebuf 
                      (match dt with 
                      | DT_I -> i_ldelem_i
                      | DT_I1  -> i_ldelem_i1     
                      | DT_I2  -> i_ldelem_i2     
                      | DT_I4  -> i_ldelem_i4     
                      | DT_I8  -> i_ldelem_i8     
                      | DT_U1  -> i_ldelem_u1     
                      | DT_U2  -> i_ldelem_u2     
                      | DT_U4  -> i_ldelem_u4     
                      | DT_R4  -> i_ldelem_r4     
                      | DT_R8  -> i_ldelem_r8     
                      | DT_REF  -> i_ldelem_ref
                      | _ -> failwith "ldelem")

                | I_stind (al, vol, dt)     -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitInstrCode codebuf 
                      (match dt with 
                      | DT_U | DT_I -> i_stind_i
                      | DT_U1 | DT_I1  -> i_stind_i1     
                      | DT_U2 | DT_I2  -> i_stind_i2     
                      | DT_U4 | DT_I4  -> i_stind_i4     
                      | DT_U8 | DT_I8  -> i_stind_i8     
                      | DT_R4  -> i_stind_r4     
                      | DT_R8  -> i_stind_r8     
                      | DT_REF  -> i_stind_ref
                      | _ -> failwith "stelem")

                | I_switch labs    ->  codebuf.RecordReqdBrFixups (i_switch, None) labs

                | I_ldfld (al, vol, fspec)  -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitFieldSpecInstr cenv codebuf env i_ldfld fspec
                | I_ldflda        fspec   -> 
                    emitFieldSpecInstr cenv codebuf env i_ldflda fspec
                | I_ldsfld        (vol, fspec)     -> 
                    emitVolatility codebuf vol
                    emitFieldSpecInstr cenv codebuf env i_ldsfld fspec
                | I_ldsflda       fspec   -> 
                    emitFieldSpecInstr cenv codebuf env i_ldsflda fspec
                | I_stfld (al, vol, fspec)  -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitFieldSpecInstr cenv codebuf env i_stfld fspec
                | I_stsfld        (vol, fspec)     -> 
                    emitVolatility codebuf vol
                    emitFieldSpecInstr cenv codebuf env i_stsfld fspec

                | I_ldtoken  tok  -> 
                    emitInstrCode codebuf i_ldtoken
                    codebuf.EmitUncodedToken 
                      (match tok with 
                      | ILToken.ILType typ -> 
                          match GetTypeAsTypeDefOrRef cenv env typ with 
                          | (tag, idx) when tag = TypeDefOrRefOrSpecTag.TypeDef -> getUncodedToken ILTableNames.TypeDef idx
                          | (tag, idx) when tag = TypeDefOrRefOrSpecTag.TypeRef -> getUncodedToken ILTableNames.TypeRef idx
                          | (tag, idx) when tag = TypeDefOrRefOrSpecTag.TypeSpec -> getUncodedToken ILTableNames.TypeSpec idx
                          | _ -> failwith "?"
                      | ILToken.ILMethod mspec ->
                          match GetMethodSpecAsMethodDefOrRef cenv env (mspec, None) with 
                          | (tag, idx) when tag = MethodDefOrRefTag.MethodDef -> getUncodedToken ILTableNames.Method idx
                          | (tag, idx) when tag = MethodDefOrRefTag.MemberRef -> getUncodedToken ILTableNames.MemberRef idx
                          | _ -> failwith "?"

                      | ILToken.ILField fspec ->
                          match GetFieldSpecAsFieldDefOrRef cenv env fspec with 
                          | (true, idx) -> getUncodedToken ILTableNames.Field idx
                          | (false, idx)  -> getUncodedToken ILTableNames.MemberRef idx)
                | I_ldstr s       -> 
                    emitInstrCode codebuf i_ldstr
                    codebuf.RecordReqdStringFixup (GetUserStringHeapIdx cenv s)

                | I_box  ty       -> emitTypeInstr cenv codebuf env i_box ty
                | I_unbox  ty     -> emitTypeInstr cenv codebuf env i_unbox ty
                | I_unbox_any  ty -> emitTypeInstr cenv codebuf env i_unbox_any ty 

                | I_newarr (shape, ty) -> 
                    if (shape = ILArrayShape.SingleDimensional) then   
                        emitTypeInstr cenv codebuf env i_newarr ty
                    else
                        let args = Array.init shape.Rank (fun _ -> cenv.ilg.typ_Int32)
                        emitMethodSpecInfoInstr cenv codebuf env i_newobj (".ctor", mkILArrTy(ty, shape), ILCallingConv.Instance, args, ILType.Void, None, [| |])

                | I_stelem_any (shape, ty) -> 
                    if (shape = ILArrayShape.SingleDimensional) then   
                        emitTypeInstr cenv codebuf env i_stelem_any ty  
                    else 
                        let args = Array.init (shape.Rank+1) (fun i -> if i < shape.Rank then  cenv.ilg.typ_Int32 else ty)
                        emitMethodSpecInfoInstr cenv codebuf env i_call ("Set", mkILArrTy(ty, shape), ILCallingConv.Instance, args, ILType.Void, None, [| |])

                | I_ldelem_any (shape, ty) -> 
                    if (shape = ILArrayShape.SingleDimensional) then   
                        emitTypeInstr cenv codebuf env i_ldelem_any ty  
                    else 
                        let args = Array.init shape.Rank (fun _ -> cenv.ilg.typ_Int32)
                        emitMethodSpecInfoInstr cenv codebuf env i_call ("Get", mkILArrTy(ty, shape), ILCallingConv.Instance, args, ty, None, [| |])

                | I_ldelema  (ro, shape, ty) -> 
                    if (ro = ReadonlyAddress) then
                        emitInstrCode codebuf i_readonly
                    if (shape = ILArrayShape.SingleDimensional) then   
                        emitTypeInstr cenv codebuf env i_ldelema ty
                    else 
                        let args = Array.init shape.Rank (fun _ -> cenv.ilg.typ_Int32)
                        emitMethodSpecInfoInstr cenv codebuf env i_call ("Address", mkILArrTy(ty, shape), ILCallingConv.Instance, args, ILType.Byref ty, None, [| |])

                | I_castclass  ty -> emitTypeInstr cenv codebuf env i_castclass ty
                | I_isinst  ty -> emitTypeInstr cenv codebuf env i_isinst ty
                | I_refanyval  ty -> emitTypeInstr cenv codebuf env i_refanyval ty
                | I_mkrefany  ty -> emitTypeInstr cenv codebuf env i_mkrefany ty
                | I_initobj  ty -> emitTypeInstr cenv codebuf env i_initobj ty
                | I_ldobj  (al, vol, ty) -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitTypeInstr cenv codebuf env i_ldobj ty
                | I_stobj  (al, vol, ty) -> 
                    emitAlignment codebuf al 
                    emitVolatility codebuf vol
                    emitTypeInstr cenv codebuf env i_stobj ty
                | I_cpobj  ty -> emitTypeInstr cenv codebuf env i_cpobj ty
                | I_sizeof  ty -> emitTypeInstr cenv codebuf env i_sizeof ty
                | EI_ldlen_multi (_, m)    -> 
                    emitShortInt32Instr codebuf (i_ldc_i4_s, i_ldc_i4) m
                    let mspec = mkILMethSpecInTyRaw(cenv.ilg.typ_Array, ILCallingConv.Instance, "GetLength", [|cenv.ilg.typ_Int32|], cenv.ilg.typ_Int32, [| |])
                    emitInstr cenv codebuf env (mkNormalCall mspec)

                |  _ -> failwith "an IL instruction cannot be emitted"


            // Used to put local debug scopes and exception handlers into a tree form
            let rangeInsideRange (start_pc1, end_pc1) (start_pc2, end_pc2)  =
              (start_pc1:int) >= start_pc2 && start_pc1 < end_pc2 &&
              (end_pc1:int) > start_pc2 && end_pc1 <= end_pc2 

            let lranges_of_clause cl = 
              match cl with 
              | ILExceptionClause.Finally r1 -> [r1]
              | ILExceptionClause.Fault r1 -> [r1]
              | ILExceptionClause.FilterCatch (r1, r2) -> [r1;r2]
              | ILExceptionClause.TypeCatch (_ty, r1) -> [r1]  


            let labelsToRange (lab2pc: Dictionary<ILCodeLabel, int>) p = let (l1, l2) = p in lab2pc.[l1], lab2pc.[l2]

            let labelRangeInsideLabelRange lab2pc ls1 ls2 = 
                rangeInsideRange (labelsToRange lab2pc ls1) (labelsToRange lab2pc ls2) 

// This file still gets used when targeting FSharp.Core 3.1.0.0, e.g. in FSharp.Data
#if !ABOVE_FSCORE_4_0_0_0
            let mapFold f acc (array: _[]) =
                match array.Length with
                | 0 -> [| |], acc
                | len ->
                    let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(f)
                    let mutable acc = acc
                    let res = Array.zeroCreate len
                    for i = 0 to array.Length-1 do
                        let h', s' = f.Invoke(acc, array.[i])
                        res.[i] <- h'
                        acc <- s'
                    res, acc
#endif
            let findRoots contains vs = 
                // For each item, either make it a root or make it a child of an existing root
                let addToRoot roots x = 
                    // Look to see if 'x' is inside one of the roots
                    let roots, found = 
                        (false, roots) ||> mapFold (fun found (r, children) -> 
                            if found then ((r, children), true)
                            elif contains x r then ((r, Array.append [| x |] children), true) 
                            else ((r, children), false))

                    if found then roots 
                    else 
                        // Find the ones that 'x' encompasses and collapse them
                        let yes, others = roots |> Array.partition (fun (r, _) -> contains r x)
                        let yesChild = yes |> Array.collect (fun (r, ch) -> Array.append [| r |] ch)
                        Array.append [| (x, yesChild) |] others
            
                ([| |], vs) ||> Array.fold addToRoot

            let rec makeSEHTree cenv env (pc2pos: int[]) (lab2pc: Dictionary<ILCodeLabel, int>) (exs: ILExceptionSpec[]) = 

                let clause_inside_lrange cl lr =
                  List.forall (fun lr1 -> labelRangeInsideLabelRange lab2pc lr1 lr) (lranges_of_clause cl) 

                let tryspec_inside_lrange (tryspec1: ILExceptionSpec) lr =
                  (labelRangeInsideLabelRange lab2pc tryspec1.Range lr && clause_inside_lrange tryspec1.Clause lr) 

                let tryspec_inside_clause tryspec1 cl =
                  List.exists (fun lr -> tryspec_inside_lrange tryspec1 lr) (lranges_of_clause cl) 

                let tryspec_inside_tryspec tryspec1 (tryspec2: ILExceptionSpec) =
                  tryspec_inside_lrange tryspec1 tryspec2.Range ||
                  tryspec_inside_clause tryspec1 tryspec2.Clause

                let roots = findRoots tryspec_inside_tryspec exs
                let trees = 
                    roots |> Array.map (fun (cl, ch) -> 
                        let r1 = labelsToRange lab2pc cl.Range
                        let conv ((s1, e1), (s2, e2)) x = pc2pos.[s1], pc2pos.[e1] - pc2pos.[s1], pc2pos.[s2], pc2pos.[e2] - pc2pos.[s2], x
                        let children = makeSEHTree cenv env pc2pos lab2pc ch
                        let n = 
                            match cl.Clause with 
                            | ILExceptionClause.Finally r2 -> 
                                conv (r1, labelsToRange lab2pc r2) ExceptionClauseKind.FinallyClause
                            | ILExceptionClause.Fault r2 -> 
                                conv (r1, labelsToRange lab2pc r2) ExceptionClauseKind.FaultClause
                            | ILExceptionClause.FilterCatch ((filterStart, _), r3) -> 
                                conv (r1, labelsToRange lab2pc r3) (ExceptionClauseKind.FilterClause (pc2pos.[lab2pc.[filterStart]]))
                            | ILExceptionClause.TypeCatch (typ, r2) -> 
                                conv (r1, labelsToRange lab2pc r2) (TypeFilterClause (getTypeDefOrRefAsUncodedToken (GetTypeAsTypeDefOrRef cenv env typ)))
                        SEHTree.Node (Some n, children) )

                trees 

#if EMIT_DEBUG_INFO
            let rec makeLocalsTree cenv localSigs (pc2pos: int[]) (lab2pc: Dictionary<ILCodeLabel, int>) (exs: ILLocalDebugInfo[]) = 
                let localInsideLocal (locspec1: ILLocalDebugInfo) (locspec2: ILLocalDebugInfo) =
                  labelRangeInsideLabelRange lab2pc locspec1.Range locspec2.Range 

                let roots = findRoots localInsideLocal exs

                let trees = 
                    roots |> Array.collect (fun (cl, ch) -> 
                        let (s1, e1) = labelsToRange lab2pc cl.Range
                        let (s1, e1) = pc2pos.[s1], pc2pos.[e1]
                        let children = makeLocalsTree cenv localSigs pc2pos lab2pc ch
                        mkScopeNode cenv localSigs (s1, e1, cl.DebugMappings, children))
                trees 
#endif


            // Emit the SEH tree 
            let rec emitExceptionHandlerTree (codebuf: CodeBuffer) (Node (x, childSEH)) = 
                childSEH |> Array.iter (emitExceptionHandlerTree codebuf)  // internal first 
                x |> Option.iter codebuf.EmitExceptionClause 

            let emitCode cenv localSigs (codebuf: CodeBuffer) env (code: ILCode) = 
                let instrs = code.Instrs
                
                // Build a table mapping Abstract IL pcs to positions in the generated code buffer
                let pc2pos = Array.zeroCreate (instrs.Length+1)
                let pc2labs = Dictionary()
                for (KeyValue(lab, pc)) in code.Labels do
                    if pc2labs.ContainsKey pc then pc2labs.[pc] <- lab :: pc2labs.[pc] else pc2labs.[pc] <- [lab]

                // Emit the instructions
                for pc = 0 to instrs.Length do
                    if pc2labs.ContainsKey pc then  
                        for lab in pc2labs.[pc] do
                            codebuf.RecordAvailBrFixup lab
                    pc2pos.[pc] <- codebuf.code.Position
                    if pc < instrs.Length then 
                        match instrs.[pc] with 
                        | I_br l when code.Labels.[l] = pc + 1 -> () // compress I_br to next instruction
                        | i -> emitInstr cenv codebuf env i

                // Build the exceptions and locals information, ready to emit
                let SEHTree = makeSEHTree cenv env pc2pos code.Labels code.Exceptions
                Array.iter (emitExceptionHandlerTree codebuf) SEHTree

#if EMIT_DEBUG_INFO
                // Build the locals information, ready to emit
                let localsTree = makeLocalsTree cenv localSigs pc2pos code.Labels code.Locals
                localsTree
#endif

            let EmitTopCode cenv localSigs env nm code = 
                let codebuf = CodeBuffer.Create nm
                let origScopes =  emitCode cenv localSigs codebuf env code
                let origCode = codebuf.code.Close()
                let origExnClauses = codebuf.seh.ToArray()
                let origReqdStringFixups = codebuf.reqdStringFixupsInMethod.ToArray()
                let origAvailBrFixups = codebuf.availBrFixups
                let origReqdBrFixups = codebuf.reqdBrFixups.ToArray()
#if EMIT_DEBUG_INFO
                let origSeqPoints = codebuf.seqpoints.ToArray()
#endif

                let newCode, newReqdStringFixups, newExnClauses  = 
                    applyBrFixups origCode origExnClauses origReqdStringFixups origAvailBrFixups origReqdBrFixups 

#if EMIT_DEBUG_INFO
                let rootScope = 
                    { Children= newScopes
                      StartOffset=0
                      EndOffset=newCode.Length
                      Locals=[| |] }
#endif

                (newReqdStringFixups, newExnClauses, newCode)

        // -------------------------------------------------------------------- 
        // ILMethodBody --> bytes
        // -------------------------------------------------------------------- 
        let GetFieldDefTypeAsBlobIdx cenv env ty = 
            let bytes = emitBytesViaBuffer (fun bb -> bb.EmitByte e_IMAGE_CEE_CS_CALLCONV_FIELD 
                                                      EmitType cenv env bb ty)
            GetBytesAsBlobIdx cenv bytes

        let GenILMethodBody mname cenv env (il: ILMethodBody) =
            let localSigs = 
              if cenv.generatePdb then 
                il.Locals |> Array.map (fun l -> 
                    // Write a fake entry for the local signature headed by e_IMAGE_CEE_CS_CALLCONV_FIELD. This is referenced by the PDB file
                    ignore (FindOrAddSharedRow cenv ILTableNames.StandAloneSig (SharedRow [| Blob (GetFieldDefTypeAsBlobIdx cenv env l.Type) |]))
                    // Now write the type
                    GetTypeOfLocalAsBytes cenv env l) 
              else 
                [| |]

            let requiredStringFixups, seh, code (* , seqpoints, scopes *) = Codebuf.EmitTopCode cenv localSigs env mname il.Code
            let codeSize = code.Length
            let methbuf = ByteBuffer.Create (codeSize * 3)
            // Do we use the tiny format? 
            if isEmpty il.Locals && il.MaxStack <= 8 && isEmpty seh && codeSize < 64 then
                // Use Tiny format 
                let alignedCodeSize = align 4 (codeSize + 1)
                let codePadding =  (alignedCodeSize - (codeSize + 1))
                let requiredStringFixups' = (1, requiredStringFixups)
                methbuf.EmitByte (byte codeSize <<< 2 ||| e_CorILMethod_TinyFormat)
                methbuf.EmitBytes code
                methbuf.EmitPadding codePadding
                0x0, (requiredStringFixups', methbuf.Close()) // , seqpoints, scopes
            else
                // Use Fat format 
                let flags = 
                    e_CorILMethod_FatFormat |||
                    (if not (isEmpty seh) then e_CorILMethod_MoreSects else 0x0uy) ||| 
                    (if il.IsZeroInit then e_CorILMethod_InitLocals else 0x0uy)

                let localToken = 
                    if isEmpty il.Locals then 0x0 else 
                    getUncodedToken ILTableNames.StandAloneSig
                      (FindOrAddSharedRow cenv ILTableNames.StandAloneSig (GetLocalSigAsStandAloneSigIdx cenv env il.Locals))

                let alignedCodeSize = align 0x4 codeSize
                let codePadding =  (alignedCodeSize - codeSize)
                
                methbuf.EmitByte flags 
                methbuf.EmitByte 0x30uy // last four bits record size of fat header in 4 byte chunks - this is always 12 bytes = 3 four word chunks 
                methbuf.EmitUInt16 (uint16 il.MaxStack)
                methbuf.EmitInt32 codeSize
                methbuf.EmitInt32 localToken
                methbuf.EmitBytes code
                methbuf.EmitPadding codePadding

                if not (isEmpty seh) then 
                    // Can we use the small exception handling table format? 
                    let smallSize = (seh.Length * 12 + 4)
                    let canUseSmall = 
                      smallSize <= 0xFF &&
                      seh |> Array.forall (fun (st1, sz1, st2, sz2, _) -> 
                          st1 <= 0xFFFF && st2 <= 0xFFFF && sz1 <= 0xFF && sz2 <= 0xFF) 
                    
                    let kindAsInt32 k = 
                      match k with 
                      | FinallyClause -> e_COR_ILEXCEPTION_CLAUSE_FINALLY
                      | FaultClause -> e_COR_ILEXCEPTION_CLAUSE_FAULT
                      | FilterClause _ -> e_COR_ILEXCEPTION_CLAUSE_FILTER
                      | TypeFilterClause _ -> e_COR_ILEXCEPTION_CLAUSE_EXCEPTION
                    let kindAsExtraInt32 k = 
                      match k with 
                      | FinallyClause | FaultClause -> 0x0
                      | FilterClause i -> i
                      | TypeFilterClause uncoded -> uncoded
                    
                    if canUseSmall then     
                        methbuf.EmitByte e_CorILMethod_Sect_EHTable
                        methbuf.EmitByte (b0 smallSize |> byte) 
                        methbuf.EmitByte 0x00uy 
                        methbuf.EmitByte 0x00uy
                        seh |> Array.iter (fun (st1, sz1, st2, sz2, kind) -> 
                            let k32 = kindAsInt32 kind
                            methbuf.EmitInt32AsUInt16 k32 
                            methbuf.EmitInt32AsUInt16 st1 
                            methbuf.EmitByte (b0 sz1 |> byte) 
                            methbuf.EmitInt32AsUInt16 st2 
                            methbuf.EmitByte (b0 sz2 |> byte)
                            methbuf.EmitInt32 (kindAsExtraInt32 kind))
                    else 
                        let bigSize = (seh.Length * 24 + 4)
                        methbuf.EmitByte (e_CorILMethod_Sect_EHTable ||| e_CorILMethod_Sect_FatFormat)
                        methbuf.EmitByte (b0 bigSize |> byte)
                        methbuf.EmitByte (b1 bigSize |> byte)
                        methbuf.EmitByte (b2 bigSize |> byte)
                        seh |> Array.iter (fun (st1, sz1, st2, sz2, kind) -> 
                            let k32 = kindAsInt32 kind
                            methbuf.EmitInt32 k32
                            methbuf.EmitInt32 st1
                            methbuf.EmitInt32 sz1
                            methbuf.EmitInt32 st2
                            methbuf.EmitInt32 sz2
                            methbuf.EmitInt32 (kindAsExtraInt32 kind))
                
                let requiredStringFixups' = (12, requiredStringFixups)

                localToken, (requiredStringFixups', methbuf.Close()) //, seqpoints, scopes

        // -------------------------------------------------------------------- 
        // ILFieldDef --> FieldDef Row
        // -------------------------------------------------------------------- 

        let rec GetFieldDefAsFieldDefRow cenv env (fd: ILFieldDef) = 
            let flags = int fd.Attributes ||| (if (fd.LiteralValue <> None) then 0x8000 else 0x0) //|||
                //(if (fd.Marshal <> None) then 0x1000 else 0x0) |||
                //(if (fd.Data <> None) then 0x0100 else 0x0)
            UnsharedRow 
                [| UShort (uint16 flags) 
                   StringE (GetStringHeapIdx cenv fd.Name)
                   Blob (GetFieldDefSigAsBlobIdx cenv env fd ) |]

        and GetFieldDefSigAsBlobIdx cenv env fd = GetFieldDefTypeAsBlobIdx cenv env fd.FieldType

        and GenFieldDefPass3 cenv env fd = 
            let fidx = AddUnsharedRow cenv ILTableNames.Field (GetFieldDefAsFieldDefRow cenv env fd)
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.FieldDef, fidx) fd.CustomAttrs
#if EMIT_FIELD_DATA
            // Write FieldRVA table - fixups into data section done later 
            match fd.Data with 
            | None -> () 
            | Some b -> 
                let offs = cenv.data.Position
                cenv.data.EmitBytes b
                AddUnsharedRow cenv ILTableNames.FieldRVA 
                    (UnsharedRow [| Data (offs, false); SimpleIndex (ILTableNames.Field, fidx) |]) |> ignore
            // Write FieldMarshal table 
            match fd.Marshal with 
            | None -> ()
            | Some ntyp -> 
                AddUnsharedRow cenv ILTableNames.FieldMarshal 
                      (UnsharedRow [| HasFieldMarshal (hfm_FieldDef, fidx)
                                      Blob (GetNativeTypeAsBlobIdx cenv ntyp) |]) |> ignore
#endif
            // Write Content table 
            match fd.LiteralValue with 
            | None -> ()
            | Some i -> 
                AddUnsharedRow cenv ILTableNames.Constant 
                      (UnsharedRow 
                          [| GetFieldInitFlags i
                             HasConstant (HasConstantTag.FieldDef, fidx)
                             Blob (GetFieldInitAsBlobIdx cenv i) |]) |> ignore
            // Write FieldLayout table 
            match fd.Offset with 
            | None -> ()
            | Some offset -> 
                AddUnsharedRow cenv ILTableNames.FieldLayout 
                      (UnsharedRow [| ULong offset; SimpleIndex (ILTableNames.Field, fidx) |]) |> ignore

                        
        // -------------------------------------------------------------------- 
        // ILGenericParameterDef --> GenericParam Row
        // -------------------------------------------------------------------- 

        let rec GetGenericParamAsGenericParamRow cenv _env idx owner (gp: ILGenericParameterDef) = 
            let flags = 
                (if gp.IsCovariant then 0x0001 else 0x0000) |||
                (if gp.IsContravariant then 0x0002 else 0x0000) |||
                (if gp.HasReferenceTypeConstraint then 0x0004 else 0x0000) |||
                (if gp.HasNotNullableValueTypeConstraint then 0x0008 else 0x0000) |||
                (if gp.HasDefaultConstructorConstraint then 0x0010 else 0x0000)

            let mdVersionMajor, _ = metadataSchemaVersionSupportedByCLRVersion cenv.desiredMetadataVersion
            if (mdVersionMajor = 1) then 
                SharedRow 
                    [| UShort (uint16 idx) 
                       UShort (uint16 flags)   
                       TypeOrMethodDef (fst owner, snd owner)
                       StringE (GetStringHeapIdx cenv gp.Name)
                       TypeDefOrRefOrSpec (TypeDefOrRefOrSpecTag.TypeDef, 0) (* empty kind field in deprecated metadata *) |]
            else
                SharedRow 
                    [| UShort (uint16 idx) 
                       UShort (uint16 flags)   
                       TypeOrMethodDef (fst owner, snd owner)
                       StringE (GetStringHeapIdx cenv gp.Name) |]

        and GenTypeAsGenericParamConstraintRow cenv env gpidx ty = 
            let tdorTag, tdorRow = GetTypeAsTypeDefOrRef cenv env ty
            UnsharedRow 
                [| SimpleIndex (ILTableNames.GenericParam, gpidx)
                   TypeDefOrRefOrSpec (tdorTag, tdorRow) |]

        and GenGenericParamConstraintPass4 cenv env gpidx ty =
            AddUnsharedRow cenv ILTableNames.GenericParamConstraint (GenTypeAsGenericParamConstraintRow cenv env gpidx ty) |> ignore

        and GenGenericParamPass3 cenv env idx owner gp = 
            // here we just collect generic params, its constraints\custom attributes will be processed on pass4
            // shared since we look it up again below in GenGenericParamPass4
            AddSharedRow cenv ILTableNames.GenericParam (GetGenericParamAsGenericParamRow cenv env idx owner gp) 
            |> ignore


        and GenGenericParamPass4 cenv env idx owner (gp: ILGenericParameterDef) = 
            let gpidx = FindOrAddSharedRow cenv ILTableNames.GenericParam (GetGenericParamAsGenericParamRow cenv env idx owner gp)
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.GenericParam, gpidx) gp.CustomAttrs
            gp.Constraints |> Array.iter (GenGenericParamConstraintPass4 cenv env gpidx) 

        // -------------------------------------------------------------------- 
        // param and return --> Param Row
        // -------------------------------------------------------------------- 

        let rec GetParamAsParamRow cenv _env seq (param: ILParameter)  = 
            let flags = 
                (if param.IsIn then 0x0001 else 0x0000) |||
                (if param.IsOut then 0x0002 else 0x0000) |||
                (if param.IsOptional then 0x0010 else 0x0000) |||
                (if param.Default.HasValue then 0x1000 else 0x0000) //|||
                //(if param.Marshal <> None then 0x2000 else 0x0000)
            
            UnsharedRow 
                [| UShort (uint16 flags) 
                   UShort (uint16 seq) 
                   StringE (GetStringHeapIdxOption cenv param.Name) |]  

        and GenParamPass3 cenv env seq (param: ILParameter) = 
            if not param.IsIn && not param.IsOut && not param.IsOptional && param.Default.IsNone && param.Name.IsNone // && Option.isNone param.Marshal 
            then ()
            else    
              let pidx = AddUnsharedRow cenv ILTableNames.Param (GetParamAsParamRow cenv env seq param)
              GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.ParamDef, pidx) param.CustomAttrs
#if EMIT_FIELD_MARSHAL
              // Write FieldRVA table - fixups into data section done later 
              match param.Marshal with 
              | None -> ()
              | Some ntyp -> 
                  AddUnsharedRow cenv ILTableNames.FieldMarshal 
                        (UnsharedRow [| HasFieldMarshal (hfm_ParamDef, pidx); Blob (GetNativeTypeAsBlobIdx cenv ntyp) |]) |> ignore
              // Write Content table for DefaultParameterValue attr
#endif
              match param.Default with
              | UNone -> ()
              | USome i -> 
                AddUnsharedRow cenv ILTableNames.Constant 
                      (UnsharedRow 
                          [| GetFieldInitFlags i
                             HasConstant (HasConstantTag.ParamDef, pidx)
                             Blob (GetFieldInitAsBlobIdx cenv i) |]) |> ignore

        let GenReturnAsParamRow (returnv: ILReturn) = 
            let flags = 0x0000 // || (if returnv.Marshal <> None then 0x2000 else 0x0000)
            UnsharedRow 
                [| UShort (uint16 flags) 
                   UShort 0us (* sequence num. *)
                   StringE 0 |]  

        let GenReturnPass3 cenv (returnv: ILReturn) = 
            if (* Option.isSome returnv.Marshal || *) not (isEmpty returnv.CustomAttrs.Entries) then
                let pidx = AddUnsharedRow cenv ILTableNames.Param (GenReturnAsParamRow returnv)
                GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.ParamDef, pidx) returnv.CustomAttrs
#if EMIT_MARSHAL
                match returnv.Marshal with 
                | None -> ()
                | Some ntyp -> 
                    AddUnsharedRow cenv ILTableNames.FieldMarshal   
                        (UnsharedRow 
                            [| HasFieldMarshal (hfm_ParamDef, pidx)
                               Blob (GetNativeTypeAsBlobIdx cenv ntyp) |]) |> ignore
#endif

        // -------------------------------------------------------------------- 
        // ILMethodDef --> ILMethodDef Row
        // -------------------------------------------------------------------- 

        let GetMethodDefSigAsBytes cenv env (mdef: ILMethodDef) = 
            emitBytesViaBuffer (fun bb -> 
              bb.EmitByte (callconvToByte mdef.GenericParams.Length mdef.CallingConv)
              if mdef.GenericParams.Length > 0 then bb.EmitZ32 mdef.GenericParams.Length
              bb.EmitZ32 mdef.Parameters.Length
              EmitType cenv env bb mdef.Return.Type
              mdef.ParameterTypes |> Array.iter (EmitType cenv env bb))

        let GenMethodDefSigAsBlobIdx cenv env mdef = 
            GetBytesAsBlobIdx cenv (GetMethodDefSigAsBytes cenv env mdef)

        let GenMethodDefAsRow cenv env midx (md: ILMethodDef) = 
            let flags = int md.Attributes
            let implflags = int md.ImplAttributes

            if md.IsEntryPoint then 
                if cenv.entrypoint <> None then failwith "duplicate entrypoint"
                else cenv.entrypoint <- Some (true, midx)
            let codeAddr = 
              (match md.Body with 
              | Some ilmbody -> 
                  let addr = cenv.nextCodeAddr
                  let (localToken, code (* , seqpoints, rootScope *) ) = GenILMethodBody md.Name cenv env ilmbody

#if EMIT_DEBUG_INFO
                  // Now record the PDB record for this method - we write this out later. 
                  if cenv.generatePdb then 
                    cenv.pdbinfo.Add  
                      { MethToken=getUncodedToken ILTableNames.Method midx
                        MethName=md.Name
                        LocalSignatureToken=localToken
                        Params= [| |] (* REVIEW *)
                        RootScope = Some rootScope
                        Range=  
                          match ilmbody.SourceMarker with 
                          | Some m  when cenv.generatePdb -> 
                              // table indexes are 1-based, document array indexes are 0-based 
                              let doc = (cenv.documents.FindOrAddSharedEntry m.Document) - 1 

                              Some ({ Document=doc
                                      Line=m.Line
                                      Column=m.Column }, 
                                    { Document=doc
                                      Line=m.EndLine
                                      Column=m.EndColumn })
                          | _ -> None
                        SequencePoints=seqpoints }
#endif

                  cenv.AddCode code
                  addr
#if EMIT_DEBUG_INFO
              | MethodBody.Abstract ->
                  // Now record the PDB record for this method - we write this out later. 
                  if cenv.generatePdb then 
                    cenv.pdbinfo.Add  
                      { MethToken = getUncodedToken ILTableNames.Method midx
                        MethName = md.Name
                        LocalSignatureToken = 0x0                   // No locals it's abstract
                        Params = [| |]
                        RootScope = None
                        Range = None
                        SequencePoints = [| |] }
                  0x0000
              | MethodBody.Native -> 
                  failwith "cannot write body of native method - Abstract IL cannot roundtrip mixed native/managed binaries"
#endif
              | _  -> 0x0000)

            UnsharedRow 
               [| ULong  codeAddr  
                  UShort (uint16 implflags) 
                  UShort (uint16 flags) 
                  StringE (GetStringHeapIdx cenv md.Name) 
                  Blob (GenMethodDefSigAsBlobIdx cenv env md) 
                  SimpleIndex(ILTableNames.Param, cenv.GetTable(ILTableNames.Param).Count + 1) |]  

        let GenMethodImplPass3 cenv env _tgparams tidx mimpl =
            let midxTag, midxRow = GetMethodSpecAsMethodDef cenv env (mimpl.OverrideBy, None)
            let midx2Tag, midx2Row = GetOverridesSpecAsMethodDefOrRef cenv env mimpl.Overrides
            AddUnsharedRow cenv ILTableNames.MethodImpl
                (UnsharedRow 
                     [| SimpleIndex (ILTableNames.TypeDef, tidx)
                        MethodDefOrRef (midxTag, midxRow)
                        MethodDefOrRef (midx2Tag, midx2Row) |]) |> ignore
            
        let GenMethodDefPass3 cenv env (md:ILMethodDef) = 
            let midx = GetMethodDefIdx cenv md
            let idx2 = AddUnsharedRow cenv ILTableNames.Method (GenMethodDefAsRow cenv env midx md)
            if midx <> idx2 then failwith "index of method def on pass 3 does not match index on pass 2"
            GenReturnPass3 cenv md.Return  
            md.Parameters |> Array.iteri (fun n param -> GenParamPass3 cenv env (n+1) param) 
            md.CustomAttrs |> GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.MethodDef, midx) 
#if EMIT_SECURITY_DECLS
            md.SecurityDecls.Entries |> GenSecurityDeclsPass3 cenv (hds_MethodDef, midx)
#endif
            md.GenericParams |> Array.iteri (fun n gp -> GenGenericParamPass3 cenv env n (TypeOrMethodDefTag.MethodDef, midx) gp) 
#if EMIT_PINVOKE
            match md.Body.Contents with 
            | MethodBody.PInvoke attr ->
                let flags = 
                  begin match attr.CallingConv with 
                  | PInvokeCallingConvention.None ->     0x0000
                  | PInvokeCallingConvention.Cdecl ->    0x0200
                  | PInvokeCallingConvention.Stdcall ->  0x0300
                  | PInvokeCallingConvention.Thiscall -> 0x0400
                  | PInvokeCallingConvention.Fastcall -> 0x0500
                  | PInvokeCallingConvention.WinApi ->   0x0100
                  end |||
                  begin match attr.CharEncoding with 
                  | PInvokeCharEncoding.None ->    0x0000
                  | PInvokeCharEncoding.Ansi ->    0x0002
                  | PInvokeCharEncoding.Unicode -> 0x0004
                  | PInvokeCharEncoding.Auto ->    0x0006
                  end |||
                  begin match attr.CharBestFit with 
                  | PInvokeCharBestFit.UseAssembly -> 0x0000
                  | PInvokeCharBestFit.Enabled ->  0x0010
                  | PInvokeCharBestFit.Disabled -> 0x0020
                  end |||
                  begin match attr.ThrowOnUnmappableChar with 
                  | PInvokeThrowOnUnmappableChar.UseAssembly -> 0x0000
                  | PInvokeThrowOnUnmappableChar.Enabled ->  0x1000
                  | PInvokeThrowOnUnmappableChar.Disabled -> 0x2000
                  end |||
                  (if attr.NoMangle then 0x0001 else 0x0000) |||
                  (if attr.LastError then 0x0040 else 0x0000)
                AddUnsharedRow cenv ILTableNames.ImplMap
                    (UnsharedRow 
                       [| UShort (uint16 flags) 
                          MemberForwarded (mf_MethodDef, midx)
                          StringE (GetStringHeapIdx cenv attr.Name) 
                          SimpleIndex (ILTableNames.ModuleRef, GetModuleRefAsIdx cenv attr.Where) |]) |> ignore
            | _ -> ()
#endif

        let GenMethodDefPass4 cenv env  md = 
            let midx = GetMethodDefIdx cenv md
            md.GenericParams |> Array.iteri (fun n gp -> GenGenericParamPass4 cenv env n (TypeOrMethodDefTag.MethodDef, midx) gp) 

        let GenPropertyMethodSemanticsPass3 cenv pidx kind mref =
            // REVIEW: why are we catching exceptions here?
            let midx = try GetMethodRefAsMethodDefIdx cenv mref with MethodDefNotFound -> 1
            AddUnsharedRow cenv ILTableNames.MethodSemantics
                (UnsharedRow 
                   [| UShort (uint16 kind)
                      SimpleIndex (ILTableNames.Method, midx)
                      HasSemantics (HasSemanticsTag.Property, pidx) |]) |> ignore
            
        let rec GetPropertySigAsBlobIdx cenv env prop = 
            GetBytesAsBlobIdx cenv (GetPropertySigAsBytes cenv env prop)

        and GetPropertySigAsBytes cenv env prop = 
            emitBytesViaBuffer (fun bb -> 
                let b =  ((hasthisToByte prop.CallingConv) ||| e_IMAGE_CEE_CS_CALLCONV_PROPERTY)
                bb.EmitByte b
                bb.EmitZ32 prop.IndexParameterTypes.Length
                EmitType cenv env bb prop.PropertyType
                prop.IndexParameterTypes |> Array.iter (EmitType cenv env bb))

        and GetPropertyAsPropertyRow cenv env (prop:ILPropertyDef) = 
            let flags = 
              (if prop.IsSpecialName then 0x0200 else 0x0) ||| 
              (if prop.IsRTSpecialName then 0x0400 else 0x0) ||| 
              (if prop.Init <> None then 0x1000 else 0x0)
            UnsharedRow 
               [| UShort (uint16 flags) 
                  StringE (GetStringHeapIdx cenv prop.Name) 
                  Blob (GetPropertySigAsBlobIdx cenv env prop) |]  

        /// ILPropertyDef --> Property Row + MethodSemantics entries
        and GenPropertyPass3 cenv env prop = 
            let pidx = AddUnsharedRow cenv ILTableNames.Property (GetPropertyAsPropertyRow cenv env prop)
            prop.SetMethod |> Option.iter (GenPropertyMethodSemanticsPass3 cenv pidx 0x0001) 
            prop.GetMethod |> Option.iter (GenPropertyMethodSemanticsPass3 cenv pidx 0x0002) 
            // Write Constant table 
            match prop.Init with 
            | None -> ()
            | Some i -> 
                AddUnsharedRow cenv ILTableNames.Constant 
                    (UnsharedRow 
                        [| GetFieldInitFlags i
                           HasConstant (HasConstantTag.Property, pidx)
                           Blob (GetFieldInitAsBlobIdx cenv i) |]) |> ignore
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.Property, pidx) prop.CustomAttrs

        let rec GenEventMethodSemanticsPass3 cenv eidx kind mref =
            let addIdx = try GetMethodRefAsMethodDefIdx cenv mref with MethodDefNotFound -> 1
            AddUnsharedRow cenv ILTableNames.MethodSemantics
                (UnsharedRow 
                    [| UShort (uint16 kind)
                       SimpleIndex (ILTableNames.Method, addIdx)
                       HasSemantics (HasSemanticsTag.Event, eidx) |]) |> ignore

        /// ILEventDef --> Event Row + MethodSemantics entries
        and GenEventAsEventRow cenv env (md: ILEventDef) = 
            let flags = 
              (if md.IsSpecialName then 0x0200 else 0x0) ||| 
              (if md.IsRTSpecialName then 0x0400 else 0x0)
            let tdorTag, tdorRow = GetTypeAsTypeDefOrRef cenv env md.EventHandlerType
            UnsharedRow 
               [| UShort (uint16 flags) 
                  StringE (GetStringHeapIdx cenv md.Name) 
                  TypeDefOrRefOrSpec (tdorTag, tdorRow) |]

        and GenEventPass3 cenv env (md: ILEventDef) = 
            let eidx = AddUnsharedRow cenv ILTableNames.Event (GenEventAsEventRow cenv env md)
            md.AddMethod |> GenEventMethodSemanticsPass3 cenv eidx 0x0008  
            md.RemoveMethod |> GenEventMethodSemanticsPass3 cenv eidx 0x0010 
            //Option.iter (GenEventMethodSemanticsPass3 cenv eidx 0x0020) md.FireMethod  
            //List.iter (GenEventMethodSemanticsPass3 cenv eidx 0x0004) md.OtherMethods
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.Event, eidx) md.CustomAttrs


        // -------------------------------------------------------------------- 
        // resource --> generate ...
        // -------------------------------------------------------------------- 

        let rec GetResourceAsManifestResourceRow cenv r = 
            let data, impl = 
              match r.Location with
              | ILResourceLocation.Local bf ->
                  let b = bf()
                  // Embedded managed resources must be word-aligned.  However resource format is 
                  // not specified in ECMA.  Some mscorlib resources appear to be non-aligned - it seems it doesn't matter.. 
                  let offset = cenv.resources.Position
                  let alignedOffset =  (align 0x8 offset)
                  let pad = alignedOffset - offset
                  let resourceSize = b.Length
                  cenv.resources.EmitPadding pad
                  cenv.resources.EmitInt32 resourceSize
                  cenv.resources.EmitBytes b
                  Data (alignedOffset, true), (ImplementationTag.File, 0) 
              | ILResourceLocation.File (mref, offset) -> ULong offset, (ImplementationTag.File, GetModuleRefAsFileIdx cenv mref)
              | ILResourceLocation.Assembly aref -> ULong 0x0, (ImplementationTag.AssemblyRef, GetAssemblyRefAsIdx cenv aref)
            UnsharedRow 
               [| data 
                  ULong (match r.Access with ILResourceAccess.Public -> 0x01 | ILResourceAccess.Private -> 0x02)
                  StringE (GetStringHeapIdx cenv r.Name)    
                  Implementation (fst impl, snd impl) |]

        and GenResourcePass3 cenv r = 
          let idx = AddUnsharedRow cenv ILTableNames.ManifestResource (GetResourceAsManifestResourceRow cenv r)
          GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.ManifestResource, idx) r.CustomAttrs

        // -------------------------------------------------------------------- 
        // ILTypeDef --> generate ILFieldDef, ILMethodDef, ILPropertyDef etc. rows
        // -------------------------------------------------------------------- 

        let rec GenTypeDefPass3 enc cenv (td:ILTypeDef) = 
           try
              let env = envForTypeDef td
              let tidx = GetIdxForTypeDef cenv (TdKey(enc, td.Namespace, td.Name))
              td.Properties.Entries |> Array.iter (GenPropertyPass3 cenv env)
              td.Events.Entries |> Array.iter (GenEventPass3 cenv env)
              td.Fields.Entries |> Array.iter (GenFieldDefPass3 cenv env)
              td.Methods.Entries |> Array.iter (GenMethodDefPass3 cenv env)
              td.MethodImpls.Entries |> Array.iter (GenMethodImplPass3 cenv env  td.GenericParams.Length tidx)
            // ClassLayout entry if needed 
              match td.Layout with 
              | ILTypeDefLayout.Auto -> ()
              | ILTypeDefLayout.Sequential layout | ILTypeDefLayout.Explicit layout ->  
                  if Option.isSome layout.Pack || Option.isSome layout.Size then 
                    AddUnsharedRow cenv ILTableNames.ClassLayout
                        (UnsharedRow 
                            [| UShort (defaultArg layout.Pack (uint16 0x0))
                               ULong (defaultArg layout.Size 0x0)
                               SimpleIndex (ILTableNames.TypeDef, tidx) |]) |> ignore
                               
#if EMIT_SECURITY_DECLS
              td.SecurityDecls.Entries |> GenSecurityDeclsPass3 cenv (hds_TypeDef, tidx)
#endif
              td.CustomAttrs |> GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.TypeDef, tidx)
              td.GenericParams |> Array.iteri (fun n gp -> GenGenericParamPass3 cenv env n (TypeOrMethodDefTag.TypeDef, tidx) gp)  
              td.NestedTypes.Entries |> GenTypeDefsPass3 (addILTypeName enc td) cenv
           with e ->
              failwith  ("Error in pass3 for type "+td.Name+", error: "+e.ToString())
              reraise()
              raise e

        and GenTypeDefsPass3 enc cenv tds =
          Array.iter (GenTypeDefPass3 enc cenv) tds

        /// ILTypeDef --> generate generic params on ILMethodDef: ensures
        /// GenericParam table is built sorted by owner.

        let rec GenTypeDefPass4 enc cenv (td:ILTypeDef) = 
           try
               let env = envForTypeDef td
               let tidx = GetIdxForTypeDef cenv (TdKey(enc, td.Namespace, td.Name))
               td.Methods.Entries |> Array.iter (GenMethodDefPass4 cenv env) 
               td.GenericParams  |> Array.iteri (fun n gp -> GenGenericParamPass4 cenv env n (TypeOrMethodDefTag.TypeDef, tidx) gp) 
               GenTypeDefsPass4 (addILTypeName enc td) cenv td.NestedTypes.Entries
           with e ->
               failwith ("Error in pass4 for type "+td.Name+", error: "+e.Message)
               reraise()
               raise e

        and GenTypeDefsPass4 enc cenv tds =
            Array.iter (GenTypeDefPass4 enc cenv) tds


        let DateTime1970Jan01 = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc) (* ECMA Spec (Oct2002), Part II, 24.2.2 PE File Header. *)
        let timestamp = (System.DateTime.UtcNow - DateTime1970Jan01).TotalSeconds |> int

        // -------------------------------------------------------------------- 
        // ILExportedTypesAndForwarders --> ILExportedTypeOrForwarder table 
        // -------------------------------------------------------------------- 

        let rec GenNestedExportedTypePass3 cenv cidx (ce: ILNestedExportedType) = 
            let flags =  GetMemberAccessFlags ce.Access
            let nidx = 
              AddUnsharedRow cenv ILTableNames.ExportedType 
                (UnsharedRow 
                    [| ULong flags  
                       ULong 0x0
                       StringE (GetStringHeapIdx cenv ce.Name) 
                       StringE 0 
                       Implementation (ImplementationTag.ExportedType, cidx) |])
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.ExportedType, nidx) ce.CustomAttrs
            GenNestedExportedTypesPass3 cenv nidx ce.Nested

        and GenNestedExportedTypesPass3 cenv nidx (nce: ILNestedExportedTypesAndForwarders) =
            nce.Entries |> Array.iter (GenNestedExportedTypePass3 cenv nidx)

        and GenExportedTypePass3 cenv (ce: ILExportedTypeOrForwarder) = 
            let nselem, nelem = GetTypeNameAsElemPair cenv (ce.Namespace, ce.Name)
            let flags =  GetTypeAccessFlags ce.Access
            let flags = if ce.IsForwarder then 0x00200000 ||| flags else flags
            let impl = GetScopeRefAsImplementationElem cenv ce.ScopeRef
            let cidx = 
              AddUnsharedRow cenv ILTableNames.ExportedType 
                (UnsharedRow 
                    [| ULong flags  
                       ULong 0x0
                       nelem 
                       nselem 
                       Implementation (fst impl, snd impl) |])
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.ExportedType, cidx) ce.CustomAttrs
            GenNestedExportedTypesPass3 cenv cidx ce.Nested

            

        // -------------------------------------------------------------------- 
        // manifest --> generate Assembly row
        // -------------------------------------------------------------------- 

        and GetManifsetAsAssemblyRow cenv m = 
            UnsharedRow 
                [|ULong m.AuxModuleHashAlgorithm
                  UShort (match m.Version with UNone -> 0us | USome v -> uint16 v.Major)
                  UShort (match m.Version with UNone -> 0us | USome v -> uint16 v.Minor)
                  UShort (match m.Version with UNone -> 0us | USome v -> uint16 v.Build)
                  UShort (match m.Version with UNone -> 0us | USome v -> uint16 v.Revision)
                  ULong 
                    ( //(match m.AssemblyLongevity with 
                      //| ILAssemblyLongevity.Unspecified ->       0x0000
                      //| ILAssemblyLongevity.Library ->           0x0002 
                      //| ILAssemblyLongevity.PlatformAppDomain -> 0x0004
                     // | ILAssemblyLongevity.PlatformProcess ->   0x0006
                     // | ILAssemblyLongevity.PlatformSystem ->    0x0008) |||
                      (if m.Retargetable then 0x100 else 0x0) |||
                      // Setting these causes peverify errors. Hence both ilread and ilwrite ignore them and refuse to set them.
                      // Any debugging customattributes will automatically propagate
                      // REVIEW: No longer appears to be the case
                      (if m.JitTracking then                     0x8000 else 0x0) ||| 
                      (match m.PublicKey with UNone -> 0x0000 | USome _ -> 0x0001) ||| 0x0000)
                  (match m.PublicKey with UNone -> Blob 0 | USome x -> Blob (GetBytesAsBlobIdx cenv x))
                  StringE (GetStringHeapIdx cenv m.Name)
                  (match m.Locale with UNone -> StringE 0 | USome x -> StringE (GetStringHeapIdx cenv x)) |]

        and GenManifestPass3 cenv m = 
            let aidx = AddUnsharedRow cenv ILTableNames.Assembly (GetManifsetAsAssemblyRow cenv m)
#if EMIT_SECURITY_DECLS
            GenSecurityDeclsPass3 cenv (hds_Assembly, aidx) m.SecurityDecls.Entries
#endif
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.Assembly, aidx) m.CustomAttrs
            m.ExportedTypes.Entries |> Array.iter (GenExportedTypePass3 cenv) 
            // Record the entrypoint decl if needed. 
            match m.EntrypointElsewhere with
            | Some mref -> 
                if cenv.entrypoint <> None then failwith "duplicate entrypoint"
                else cenv.entrypoint <- Some (false, GetModuleRefAsIdx cenv mref)
            | None -> ()

        and newGuid (modul: ILModuleDef) = 
            let n = timestamp
            let m = hash n
            let m2 = hash modul.Name
            [| b0 m; b1 m; b2 m; b3 m; b0 m2; b1 m2; b2 m2; b3 m2; 0xa7uy; 0x45uy; 0x03uy; 0x83uy; b0 n; b1 n; b2 n; b3 n |]

        and deterministicGuid (modul: ILModuleDef) =
            let n = 16909060
            let m = hash n
            let m2 = hash modul.Name
            [| b0 m; b1 m; b2 m; b3 m; b0 m2; b1 m2; b2 m2; b3 m2; 0xa7uy; 0x45uy; 0x03uy; 0x83uy; b0 n; b1 n; b2 n; b3 n |]

        and GetModuleAsRow (cenv:cenv) (modul: ILModuleDef) = 
            // Store the generated MVID in the environment (needed for generating debug information)
            let modulGuid = if cenv.deterministic then deterministicGuid modul else newGuid modul
            cenv.moduleGuid <- modulGuid
            UnsharedRow 
                [| UShort (uint16 0x0) 
                   StringE (GetStringHeapIdx cenv modul.Name) 
                   Guid (GetGuidIdx cenv modulGuid) 
                   Guid 0 
                   Guid 0 |]


        let rowElemCompare (e1: RowElement) (e2: RowElement) = 
            let c = compare e1.Val e2.Val 
            if c <> 0 then c else 
            compare e1.Tag e2.Tag

        module List = 
            let rec assoc x l = 
                match l with 
                | [] -> failwith "index not found"
                | ((h, r)::t) -> if x = h then r else assoc x t

            let rec memAssoc x l = 
                match l with 
                | [] -> false
                | ((h, _)::t) -> x = h || memAssoc x t

        let TableRequiresSorting tab = 
            List.memAssoc tab ILTableNames.sortedTableInfo 

        let SortTableRows tab (rows:GenericRow[]) = 
            assert (TableRequiresSorting tab)
            let col = List.assoc tab ILTableNames.sortedTableInfo
            rows 
                // This needs to be a stable sort, so we use List.sortWith
                |> Array.toList
                |> List.sortWith (fun r1 r2 -> rowElemCompare r1.[col] r2.[col]) 
                |> Array.ofList
                //|> Array.map SharedRow


        let mkILSimpleClass (ilg: ILGlobals) (nsp, nm, methods, fields, nestedTypes, props, events, attrs) =
          { Namespace=nsp
            Name=nm
            GenericParams=  [| |]
            Implements = [| |]
            Attributes = TypeAttributes.Class ||| TypeAttributes.BeforeFieldInit ||| TypeAttributes.Public
            Layout=ILTypeDefLayout.Auto
            Extends = Some ilg.typ_Object
            Methods= methods
            Fields= fields
            NestedTypes=nestedTypes
            CustomAttrs=attrs
            MethodImpls=emptyILMethodImpls
            Properties=props
            Events=events
            Token=0
            //SecurityDecls=emptyILSecurityDecls; 
            //HasSecurity=false;
        } 
        let mkILTypeDefForGlobalFunctions ilg (methods, fields) = 
            mkILSimpleClass ilg (UNone, typeNameForGlobalFunctions, methods, fields, emptyILTypeDefs, emptyILProperties, emptyILEvents, emptyILCustomAttrs)

        let destTypeDefsWithGlobalFunctionsFirst ilg (tdefs: ILTypeDefs) = 
          let l = tdefs.Entries
          let top, nontop = l |> Array.partition (fun td -> td.Name = typeNameForGlobalFunctions)
          let top2 = if isEmpty top then [| mkILTypeDefForGlobalFunctions ilg (emptyILMethods, emptyILFields) |] else top
          Array.append top2 nontop

        let GenModule (cenv: cenv) (modul: ILModuleDef) = 
            let midx = AddUnsharedRow cenv ILTableNames.Module (GetModuleAsRow cenv modul)
            Array.iter (GenResourcePass3 cenv) modul.Resources.Entries 
            let tds = destTypeDefsWithGlobalFunctionsFirst cenv.ilg modul.TypeDefs
            GenTypeDefsPass1 [] cenv tds
            GenTypeDefsPass2 0 [] cenv tds
            (match modul.Manifest with None -> () | Some m -> GenManifestPass3 cenv m)
            GenTypeDefsPass3 [] cenv tds
            GenCustomAttrsPass3Or4 cenv (HasCustomAttributeTag.Module, midx) modul.CustomAttrs
            // GenericParam is the only sorted table indexed by Columns in other tables (GenericParamConstraint\CustomAttributes). 
            // Hence we need to sort it before we emit any entries in GenericParamConstraint\CustomAttributes that are attached to generic params. 
            // Note this mutates the rows in a table.  'SetRowsOfTable' clears 
            // the key --> index map since it is no longer valid 
            cenv.GetTable(ILTableNames.GenericParam).SetRowsOfSharedTable (SortTableRows ILTableNames.GenericParam (cenv.GetTable(ILTableNames.GenericParam).GenericRowsOfTable))
            GenTypeDefsPass4 [] cenv tds

        let generateIL requiredDataFixups (desiredMetadataVersion, generatePdb, ilg: ILGlobals, emitTailcalls, deterministic, showTimes)  (m: ILModuleDef) cilStartAddress =
            let isDll = m.IsDLL

            let cenv = 
                { emitTailcalls=emitTailcalls
                  deterministic = deterministic
                  showTimes=showTimes
                  ilg = ilg
                  desiredMetadataVersion=desiredMetadataVersion
                  requiredDataFixups= requiredDataFixups
                  requiredStringFixups = ResizeArray()
                  codeChunks=ByteBuffer.Create 40000
                  nextCodeAddr = cilStartAddress
                  data = ByteBuffer.Create 200
                  resources = ByteBuffer.Create 200
                  tables= 
                      Array.init 64 (fun i -> 
                          if (i = ILTableNames.AssemblyRef.Index ||
                              i = ILTableNames.MemberRef.Index ||
                              i = ILTableNames.ModuleRef.Index ||
                              i = ILTableNames.File.Index ||
                              i = ILTableNames.TypeRef.Index ||
                              i = ILTableNames.TypeSpec.Index ||
                              i = ILTableNames.MethodSpec.Index ||
                              i = ILTableNames.StandAloneSig.Index ||
                              i = ILTableNames.GenericParam.Index) then 
                              MetadataTable.Shared (MetadataTable<SharedRow>.New ("row table "+string i, EqualityComparer.Default))
                            else
                              MetadataTable.Unshared (MetadataTable<UnsharedRow>.New ("row table "+string i, EqualityComparer.Default)))

                  AssemblyRefs = MetadataTable<_>.New("ILAssemblyRef", EqualityComparer.Default)
                  //documents=MetadataTable<_>.New("pdbdocs", EqualityComparer.Default)
                  trefCache=new Dictionary<_, _>(100)
#if EMIT_DEBUG_INFO
                  pdbinfo= new ResizeArray<_>(200)
#endif
                  moduleGuid= Array.zeroCreate 16
                  fieldDefs= MetadataTable<_>.New("field defs", EqualityComparer.Default)
                  methodDefIdxsByKey = MetadataTable<_>.New("method defs", EqualityComparer.Default)
                  // This uses reference identity on ILMethodDef objects
                  methodDefIdxs = new Dictionary<_, _>(100, HashIdentity.Reference)
                  propertyDefs = MetadataTable<_>.New("property defs", EqualityComparer.Default)
                  eventDefs = MetadataTable<_>.New("event defs", EqualityComparer.Default)
                  typeDefs = MetadataTable<_>.New("type defs", EqualityComparer.Default)
                  entrypoint=None
                  generatePdb=generatePdb
                  // These must use structural comparison since they are keyed by arrays
                  guids=MetadataTable<_>.New("guids", HashIdentity.Structural)
                  blobs= MetadataTable<_>.New("blobs", HashIdentity.Structural)
                  strings= MetadataTable<_>.New("strings", EqualityComparer.Default) 
                  userStrings= MetadataTable<_>.New("user strings", EqualityComparer.Default) }

            // Now the main compilation step 
            GenModule cenv  m

            // .exe files have a .entrypoint instruction.  Do not write it to the entrypoint when writing  dll.
            let entryPointToken = 
                match cenv.entrypoint with 
                | Some (epHere, tok) -> 
                    if isDll then 0x0
                    else getUncodedToken (if epHere then ILTableNames.Method else ILTableNames.File) tok 
                | None -> 
                    if not isDll then printfn "warning: no entrypoint specified in executable binary"
                    0x0

#if EMIT_DEBUG_INFO
            let pdbData = 
                { EntryPoint= (if isDll then None else Some entryPointToken)
                  Timestamp = timestamp
                  ModuleID = cenv.moduleGuid
                  Documents = cenv.documents.EntriesAsArray
                  Methods = cenv.pdbinfo.ToArray() 
                  TableRowCounts = cenv.tables |> Seq.map(fun t -> t.Count) |> Seq.toArray }
#else
            let pdbData = ()
#endif

            let idxForNextedTypeDef (tds:ILTypeDef list, td:ILTypeDef) =
                let enc = tds |> List.map (fun td -> td.Name)
                GetIdxForTypeDef cenv (TdKey(enc, td.Namespace, td.Name))

            let strings =     Array.map Bytes.stringAsUtf8NullTerminated cenv.strings.EntriesAsArray
            let userStrings = cenv.userStrings.EntriesAsArray |> Array.map Encoding.Unicode.GetBytes
            let blobs =       cenv.blobs.EntriesAsArray
            let guids =       cenv.guids.EntriesAsArray
            let tables =      cenv.tables 
            let code =        cenv.GetCode() 
            // turn idx tbls into token maps 
            let mappings =
             { TypeDefTokenMap = (fun t ->
                getUncodedToken ILTableNames.TypeDef (idxForNextedTypeDef t))
               FieldDefTokenMap = (fun t fd ->
                let tidx = idxForNextedTypeDef t
                getUncodedToken ILTableNames.Field (GetFieldDefAsFieldDefIdx cenv tidx fd))
               MethodDefTokenMap = (fun t md ->
                let tidx = idxForNextedTypeDef t
                getUncodedToken ILTableNames.Method (FindMethodDefIdx cenv (GetKeyForMethodDef tidx md)))
               PropertyTokenMap = (fun t pd ->
                let tidx = idxForNextedTypeDef t
                getUncodedToken ILTableNames.Property (cenv.propertyDefs.GetTableEntry (GetKeyForPropertyDef tidx pd)))
               EventTokenMap = (fun t ed ->
                let tidx = idxForNextedTypeDef t
                getUncodedToken ILTableNames.Event (cenv.eventDefs.GetTableEntry (EventKey (tidx, ed.Name)))) }
            // New return the results 
            let data = cenv.data.Close()
            let resources = cenv.resources.Close()
            (strings, userStrings, blobs, guids, tables, entryPointToken, code, cenv.requiredStringFixups, data, resources, pdbData, mappings)


        //=====================================================================
        // TABLES+BLOBS --> PHYSICAL METADATA+BLOBS
        //=====================================================================
        let chunk sz next = ({addr=next; size=sz}, next + sz) 
        let nochunk next = ({addr= 0x0;size= 0x0; } , next)

        let count f arr = 
            Array.fold (fun x y -> x + f y) 0x0 arr 

        let writeILMetadataAndCode (generatePdb, desiredMetadataVersion, ilg, emitTailcalls, deterministic, showTimes) modul cilStartAddress =

            // When we know the real RVAs of the data section we fixup the references for the FieldRVA table. 
            // These references are stored as offsets into the metadata we return from this function 
            let requiredDataFixups = ResizeArray()

            let next = cilStartAddress

            let strings, userStrings, blobs, guids, tables, entryPointToken, code, requiredStringFixups, data, resources, pdbData, mappings = 
              generateIL requiredDataFixups (desiredMetadataVersion, generatePdb, ilg, emitTailcalls, deterministic, showTimes) modul cilStartAddress

            let tableSize (tab: ILTableName) = tables.[tab.Index].Count

           // Now place the code 
            let codeSize = code.Length
            let alignedCodeSize = align 0x4 codeSize
            let codep, next = chunk codeSize next
            let codePadding = Array.create (alignedCodeSize - codeSize) 0x0uy
            let _codePaddingChunk, next = chunk codePadding.Length next

           // Now layout the chunks of metadata and IL 
            let metadataHeaderStartChunk, _next = chunk 0x10 next

            let numStreams = 0x05

            let (mdtableVersionMajor, mdtableVersionMinor) = metadataSchemaVersionSupportedByCLRVersion desiredMetadataVersion

            let version = 
              Encoding.UTF8.GetBytes (sprintf "v%d.%d.%d" desiredMetadataVersion.Major desiredMetadataVersion.Minor desiredMetadataVersion.Build)


            let paddedVersionLength = align 0x4 (Array.length version)

            // Most addresses after this point are measured from the MD root 
            // Switch to md-rooted addresses 
            let next = metadataHeaderStartChunk.size
            let _metadataHeaderVersionChunk, next   = chunk paddedVersionLength next
            let _metadataHeaderEndChunk, next       = chunk 0x04 next
            let _tablesStreamHeaderChunk, next      = chunk (0x08 + (align 4 ("#~".Length + 0x01))) next
            let _stringsStreamHeaderChunk, next     = chunk (0x08 + (align 4 ("#Strings".Length + 0x01))) next
            let _userStringsStreamHeaderChunk, next = chunk (0x08 + (align 4 ("#US".Length + 0x01))) next
            let _guidsStreamHeaderChunk, next       = chunk (0x08 + (align 4 ("#GUID".Length + 0x01))) next
            let _blobsStreamHeaderChunk, next       = chunk (0x08 + (align 4 ("#Blob".Length + 0x01))) next

            let tablesStreamStart = next

            let stringsStreamUnpaddedSize = count (fun (s:byte[]) -> s.Length) strings + 1
            let stringsStreamPaddedSize = align 4 stringsStreamUnpaddedSize
            
            let userStringsStreamUnpaddedSize = count (fun (s:byte[]) -> let n = s.Length + 1 in n + ByteBuffer.Z32Size n) userStrings + 1
            let userStringsStreamPaddedSize = align 4 userStringsStreamUnpaddedSize
            
            let guidsStreamUnpaddedSize = (Array.length guids) * 0x10
            let guidsStreamPaddedSize = align 4 guidsStreamUnpaddedSize
            
            let blobsStreamUnpaddedSize = count (fun (blob:byte[]) -> let n = blob.Length in n + ByteBuffer.Z32Size n) blobs + 1
            let blobsStreamPaddedSize = align 4 blobsStreamUnpaddedSize

            let guidsBig = guidsStreamPaddedSize >= 0x10000
            let stringsBig = stringsStreamPaddedSize >= 0x10000
            let blobsBig = blobsStreamPaddedSize >= 0x10000

            // 64bit bitvector indicating which tables are in the metadata. 
            let (valid1, valid2), _ = 
               (((0, 0), 0), tables) ||> Array.fold (fun ((valid1, valid2) as valid, n) rows -> 
                  let valid = 
                      if  rows.Count = 0 then valid else
                      ( (if n < 32 then  valid1 ||| (1 <<< n     ) else valid1), 
                        (if n >= 32 then valid2 ||| (1 <<< (n-32)) else valid2) )
                  (valid, n+1))

            // 64bit bitvector indicating which tables are sorted. 
            // Constant - REVIEW: make symbolic! compute from sorted table info! 
            let sorted1 = 0x3301fa00
            let sorted2 = 
              // If there are any generic parameters in the binary we're emitting then mark that 
              // table as sorted, otherwise don't.  This maximizes the number of assemblies we emit 
              // which have an ECMA-v.1. compliant set of sorted tables. 
              (if tableSize (ILTableNames.GenericParam) > 0 then 0x00000400 else 0x00000000) ||| 
              (if tableSize (ILTableNames.GenericParamConstraint) > 0 then 0x00001000 else 0x00000000) ||| 
              0x00000200
            

            let guidAddress n =   (if n = 0 then 0 else (n - 1) * 0x10 + 0x01)

            let stringAddressTable = 
                let tab = Array.create (strings.Length + 1) 0
                let pos = ref 1
                for i = 1 to strings.Length do
                    tab.[i] <- !pos
                    let s = strings.[i - 1]
                    pos := !pos + s.Length
                tab

            let stringAddress n = 
                if n >= Array.length stringAddressTable then failwith ("string index "+string n+" out of range")
                stringAddressTable.[n]
            
            let userStringAddressTable = 
                let tab = Array.create (Array.length userStrings + 1) 0
                let pos = ref 1
                for i = 1 to Array.length userStrings do
                    tab.[i] <- !pos
                    let s = userStrings.[i - 1]
                    let n = s.Length + 1
                    pos := !pos + n + ByteBuffer.Z32Size n
                tab

            let userStringAddress n = 
                if n >= Array.length userStringAddressTable then failwith "userString index out of range"
                userStringAddressTable.[n]
            
            let blobAddressTable = 
                let tab = Array.create (blobs.Length + 1) 0
                let pos = ref 1
                for i = 1 to blobs.Length do
                    tab.[i] <- !pos
                    let blob = blobs.[i - 1]
                    pos := !pos + blob.Length + ByteBuffer.Z32Size blob.Length
                tab

            let blobAddress n = 
                if n >= blobAddressTable.Length then failwith "blob index out of range"
                blobAddressTable.[n]
            

            let sortedTables = 
              Array.init 64 (fun i -> 
                  let tab = tables.[i]
                  let tabName = ILTableName.FromIndex i
                  let rows = tab.GenericRowsOfTable
                  if TableRequiresSorting tabName then SortTableRows tabName rows else rows)
              

            let codedTables = 
                  
                let bignessTable = Array.map (fun rows -> Array.length rows >= 0x10000) sortedTables
                let bigness (tab:int32) = bignessTable.[tab]
                
                let codedBigness nbits tab =
                  (tableSize tab) >= (0x10000 >>> nbits)
                
                let tdorBigness = 
                    codedBigness 2 ILTableNames.TypeDef || 
                    codedBigness 2 ILTableNames.TypeRef || 
                    codedBigness 2 ILTableNames.TypeSpec
                
                let tomdBigness = 
                    codedBigness 1 ILTableNames.TypeDef || 
                    codedBigness 1 ILTableNames.Method
                
                let hcBigness = 
                    codedBigness 2 ILTableNames.Field ||
                    codedBigness 2 ILTableNames.Param ||
                    codedBigness 2 ILTableNames.Property
                
                let hcaBigness = 
                    codedBigness 5 ILTableNames.Method ||
                    codedBigness 5 ILTableNames.Field ||
                    codedBigness 5 ILTableNames.TypeRef  ||
                    codedBigness 5 ILTableNames.TypeDef ||
                    codedBigness 5 ILTableNames.Param ||
                    codedBigness 5 ILTableNames.InterfaceImpl ||
                    codedBigness 5 ILTableNames.MemberRef ||
                    codedBigness 5 ILTableNames.Module ||
                    codedBigness 5 ILTableNames.Permission ||
                    codedBigness 5 ILTableNames.Property ||
                    codedBigness 5 ILTableNames.Event ||
                    codedBigness 5 ILTableNames.StandAloneSig ||
                    codedBigness 5 ILTableNames.ModuleRef ||
                    codedBigness 5 ILTableNames.TypeSpec ||
                    codedBigness 5 ILTableNames.Assembly ||
                    codedBigness 5 ILTableNames.AssemblyRef ||
                    codedBigness 5 ILTableNames.File ||
                    codedBigness 5 ILTableNames.ExportedType ||
                    codedBigness 5 ILTableNames.ManifestResource  ||
                    codedBigness 5 ILTableNames.GenericParam ||
                    codedBigness 5 ILTableNames.GenericParamConstraint ||
                    codedBigness 5 ILTableNames.MethodSpec

                
                let hfmBigness = 
                    codedBigness 1 ILTableNames.Field || 
                    codedBigness 1 ILTableNames.Param
                
                let hdsBigness = 
                    codedBigness 2 ILTableNames.TypeDef || 
                    codedBigness 2 ILTableNames.Method ||
                    codedBigness 2 ILTableNames.Assembly
                
                let mrpBigness = 
                    codedBigness 3 ILTableNames.TypeRef ||
                    codedBigness 3 ILTableNames.ModuleRef ||
                    codedBigness 3 ILTableNames.Method ||
                    codedBigness 3 ILTableNames.TypeSpec
                
                let hsBigness = 
                    codedBigness 1 ILTableNames.Event || 
                    codedBigness 1 ILTableNames.Property 
                
                let mdorBigness =
                    codedBigness 1 ILTableNames.Method ||    
                    codedBigness 1 ILTableNames.MemberRef 
                
                let mfBigness =
                    codedBigness 1 ILTableNames.Field ||
                    codedBigness 1 ILTableNames.Method 
                
                let iBigness =
                    codedBigness 2 ILTableNames.File || 
                    codedBigness 2 ILTableNames.AssemblyRef ||    
                    codedBigness 2 ILTableNames.ExportedType 
                
                let catBigness =  
                    codedBigness 3 ILTableNames.Method ||    
                    codedBigness 3 ILTableNames.MemberRef 
                
                let rsBigness = 
                    codedBigness 2 ILTableNames.Module ||    
                    codedBigness 2 ILTableNames.ModuleRef || 
                    codedBigness 2 ILTableNames.AssemblyRef  ||
                    codedBigness 2 ILTableNames.TypeRef

                let tablesBuf =  ByteBuffer.Create 20000

                // Now the coded tables themselves  - first the schemata header 
                tablesBuf.EmitIntsAsBytes    
                    [| 0x00; 0x00; 0x00; 0x00; 
                       mdtableVersionMajor // major version of table schemata 
                       mdtableVersionMinor // minor version of table schemata 
                       
                       ((if stringsBig then 0x01 else 0x00) |||  // bit vector for heap size 
                        (if guidsBig then 0x02 else 0x00)   |||  
                        (if blobsBig then 0x04 else 0x00))
                       0x01 (* reserved, always 1 *) |]
         
                tablesBuf.EmitInt32 valid1
                tablesBuf.EmitInt32 valid2
                tablesBuf.EmitInt32 sorted1
                tablesBuf.EmitInt32 sorted2
                
                // Numbers of rows in various tables 
                for rows in sortedTables do 
                    if rows.Length <> 0 then 
                        tablesBuf.EmitInt32 rows.Length 
                
                

              // The tables themselves 
                for rows in sortedTables do
                    for row in rows do 
                        for x in row do 
                            // Emit the coded token for the array element 
                            let t = x.Tag
                            let n = x.Val
                            match t with 
                            | _ when t = RowElementTags.UShort        -> tablesBuf.EmitUInt16 (uint16 n)
                            | _ when t = RowElementTags.ULong         -> tablesBuf.EmitInt32 n
                            | _ when t = RowElementTags.Data          -> recordRequiredDataFixup requiredDataFixups tablesBuf (tablesStreamStart + tablesBuf.Position) (n, false)
                            | _ when t = RowElementTags.DataResources -> recordRequiredDataFixup requiredDataFixups tablesBuf (tablesStreamStart + tablesBuf.Position) (n, true)
                            | _ when t = RowElementTags.Guid          -> tablesBuf.EmitZUntaggedIndex guidsBig (guidAddress n)
                            | _ when t = RowElementTags.Blob          -> tablesBuf.EmitZUntaggedIndex blobsBig  (blobAddress n)
                            | _ when t = RowElementTags.String        -> tablesBuf.EmitZUntaggedIndex stringsBig (stringAddress n)
                            | _ when t <= RowElementTags.SimpleIndexMax         -> tablesBuf.EmitZUntaggedIndex (bigness (t - RowElementTags.SimpleIndexMin)) n
                            | _ when t <= RowElementTags.TypeDefOrRefOrSpecMax  -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.TypeDefOrRefOrSpecMin)  2 tdorBigness n
                            | _ when t <= RowElementTags.TypeOrMethodDefMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.TypeOrMethodDefMin)     1 tomdBigness n
                            | _ when t <= RowElementTags.HasConstantMax         -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.HasConstantMin)         2 hcBigness   n
                            | _ when t <= RowElementTags.HasCustomAttributeMax  -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.HasCustomAttributeMin)  5 hcaBigness  n
                            | _ when t <= RowElementTags.HasFieldMarshalMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.HasFieldMarshalMin)     1 hfmBigness  n
                            | _ when t <= RowElementTags.HasDeclSecurityMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.HasDeclSecurityMin)     2 hdsBigness  n
                            | _ when t <= RowElementTags.MemberRefParentMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.MemberRefParentMin)     3 mrpBigness  n 
                            | _ when t <= RowElementTags.HasSemanticsMax        -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.HasSemanticsMin)        1 hsBigness   n 
                            | _ when t <= RowElementTags.MethodDefOrRefMax      -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.MethodDefOrRefMin)      1 mdorBigness n
                            | _ when t <= RowElementTags.MemberForwardedMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.MemberForwardedMin)     1 mfBigness   n
                            | _ when t <= RowElementTags.ImplementationMax      -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.ImplementationMin)      2 iBigness    n
                            | _ when t <= RowElementTags.CustomAttributeTypeMax -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.CustomAttributeTypeMin) 3 catBigness  n
                            | _ when t <= RowElementTags.ResolutionScopeMax     -> tablesBuf.EmitZTaggedIndex (t - RowElementTags.ResolutionScopeMin)     2 rsBigness   n
                            | _ -> failwith "invalid tag in row element"

                tablesBuf.Close()


            let tablesStreamUnpaddedSize = codedTables.Length
            // QUERY: extra 4 empty bytes in array.exe - why? Include some extra padding after 
            // the tables just in case there is a mistake in the ECMA spec. 
            let tablesStreamPaddedSize = align 4 (tablesStreamUnpaddedSize + 4)
            let tablesChunk, next = chunk tablesStreamPaddedSize next
            let tablesStreamPadding = tablesChunk.size - tablesStreamUnpaddedSize

            let stringsChunk, next = chunk stringsStreamPaddedSize next
            let stringsStreamPadding = stringsChunk.size - stringsStreamUnpaddedSize
            let userStringsChunk, next = chunk userStringsStreamPaddedSize next
            let userStringsStreamPadding = userStringsChunk.size - userStringsStreamUnpaddedSize
            let guidsChunk, next = chunk (0x10 * guids.Length) next
            let blobsChunk, _next = chunk blobsStreamPaddedSize next
            let blobsStreamPadding = blobsChunk.size - blobsStreamUnpaddedSize
            

            let metadata, guidStart =
              let mdbuf =  ByteBuffer.Create 500000 
              mdbuf.EmitIntsAsBytes 
                [| 0x42; 0x53; 0x4a; 0x42; // Magic signature 
                   0x01; 0x00; // Major version 
                   0x01; 0x00; // Minor version 
                |];
              mdbuf.EmitInt32 0x0; // Reserved 

              mdbuf.EmitInt32 paddedVersionLength;
              mdbuf.EmitBytes version;
              for i = 1 to (paddedVersionLength - Array.length version) do 
                  mdbuf.EmitIntAsByte 0x00;

              mdbuf.EmitBytes 
                [| 0x00uy; 0x00uy; // flags, reserved 
                  b0 numStreams; b1 numStreams; |];
              mdbuf.EmitInt32 tablesChunk.addr;
              mdbuf.EmitInt32 tablesChunk.size;
              mdbuf.EmitIntsAsBytes [| 0x23; 0x7e; 0x00; 0x00; (* #~00 *)|];
              mdbuf.EmitInt32 stringsChunk.addr;
              mdbuf.EmitInt32 stringsChunk.size;
              mdbuf.EmitIntsAsBytes  [| 0x23; 0x53; 0x74; 0x72; 0x69; 0x6e; 0x67; 0x73; 0x00; 0x00; 0x00; 0x00 (* "#Strings0000" *)|];
              mdbuf.EmitInt32 userStringsChunk.addr;
              mdbuf.EmitInt32 userStringsChunk.size;
              mdbuf.EmitIntsAsBytes [| 0x23; 0x55; 0x53; 0x00; (* #US0*) |];
              mdbuf.EmitInt32 guidsChunk.addr;
              mdbuf.EmitInt32 guidsChunk.size;
              mdbuf.EmitIntsAsBytes [| 0x23; 0x47; 0x55; 0x49; 0x44; 0x00; 0x00; 0x00; (* #GUID000 *)|];
              mdbuf.EmitInt32 blobsChunk.addr;
              mdbuf.EmitInt32 blobsChunk.size;
              mdbuf.EmitIntsAsBytes [| 0x23; 0x42; 0x6c; 0x6f; 0x62; 0x00; 0x00; 0x00; (* #Blob000 *)|];
              
             // Now the coded tables themselves 
              mdbuf.EmitBytes codedTables;    
              for i = 1 to tablesStreamPadding do 
                  mdbuf.EmitIntAsByte 0x00;

             // The string stream 
              mdbuf.EmitByte 0x00uy;
              for s in strings do
                  mdbuf.EmitBytes s;
              for i = 1 to stringsStreamPadding do 
                  mdbuf.EmitIntAsByte 0x00;
             // The user string stream 
              mdbuf.EmitByte  0x00uy;
              for s in userStrings do
                  mdbuf.EmitZ32 (s.Length + 1);
                  mdbuf.EmitBytes s;
                  mdbuf.EmitIntAsByte (markerForUnicodeBytes s)
              for i = 1 to userStringsStreamPadding do 
                  mdbuf.EmitIntAsByte 0x00;

            // The GUID stream 
              let guidStart = mdbuf.Position
              Array.iter mdbuf.EmitBytes guids;
              
            // The blob stream 
              mdbuf.EmitByte 0x00uy;
              for s in blobs do 
                  mdbuf.EmitZ32 s.Length;
                  mdbuf.EmitBytes s
              for i = 1 to blobsStreamPadding do 
                  mdbuf.EmitIntAsByte 0x00;
             // Done - close the buffer and return the result. 
              mdbuf.Close(), guidStart
            

           // Now we know the user string tables etc. we can fixup the 
           // uses of strings in the code 
            for (codeStartAddr, l) in requiredStringFixups do
                for (codeOffset, userStringIndex) in l do 
                      if codeStartAddr < codep.addr || codeStartAddr >= codep.addr + codep.size  then failwith "strings-in-code fixup: a group of fixups is located outside the code array";
                      let locInCode =  ((codeStartAddr + codeOffset) - codep.addr)
                      checkFixup32 code locInCode 0xdeadbeef;
                      let token = getUncodedToken ILTableNames.UserStrings (userStringAddress userStringIndex)
                      if (Bytes.get code (locInCode-1) <> i_ldstr) then failwith "strings-in-code fixup: not at ldstr instruction!";
                      applyFixup32 code locInCode token

            entryPointToken, code, codePadding, metadata, data, resources, requiredDataFixups.ToArray(), pdbData, mappings, guidStart

        //---------------------------------------------------------------------
        // PHYSICAL METADATA+BLOBS --> PHYSICAL PE FORMAT
        //---------------------------------------------------------------------

        // THIS LAYS OUT A 2-SECTION .NET PE BINARY 
        // SECTIONS 
        // TEXT: physical 0x0200 --> RVA 0x00020000
        //         e.g. raw size 0x9600, 
        //         e.g. virt size 0x9584
        // RELOC: physical 0x9800 --> RVA 0x0000c000
        //    i.e. physbase --> rvabase
        //    where physbase = textbase + text raw size
        //         phsrva = roundup(0x2000, 0x0002000 + text virt size)

        let msdosHeader: byte[] = 
             [| 0x4duy; 0x5auy; 0x90uy; 0x00uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy
                0x04uy; 0x00uy; 0x00uy; 0x00uy; 0xFFuy; 0xFFuy; 0x00uy; 0x00uy
                0xb8uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
                0x40uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
                0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
                0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
                0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
                0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x80uy; 0x00uy; 0x00uy; 0x00uy
                0x0euy; 0x1fuy; 0xbauy; 0x0euy; 0x00uy; 0xb4uy; 0x09uy; 0xcduy
                0x21uy; 0xb8uy; 0x01uy; 0x4cuy; 0xcduy; 0x21uy; 0x54uy; 0x68uy
                0x69uy; 0x73uy; 0x20uy; 0x70uy; 0x72uy; 0x6fuy; 0x67uy; 0x72uy
                0x61uy; 0x6duy; 0x20uy; 0x63uy; 0x61uy; 0x6euy; 0x6euy; 0x6fuy
                0x74uy; 0x20uy; 0x62uy; 0x65uy; 0x20uy; 0x72uy; 0x75uy; 0x6euy
                0x20uy; 0x69uy; 0x6euy; 0x20uy; 0x44uy; 0x4fuy; 0x53uy; 0x20uy
                0x6duy; 0x6fuy; 0x64uy; 0x65uy; 0x2euy; 0x0duy; 0x0duy; 0x0auy
                0x24uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]

        let writeInt64 (os: BinaryWriter) x =
            os.Write (dw0 x);
            os.Write (dw1 x);
            os.Write (dw2 x);
            os.Write (dw3 x);
            os.Write (dw4 x);
            os.Write (dw5 x);
            os.Write (dw6 x);
            os.Write (dw7 x)

        let writeInt32 (os: BinaryWriter) x = 
            os.Write  (b0 x)
            os.Write  (b1 x)
            os.Write  (b2 x)
            os.Write  (b3 x)  

        let writeInt32AsUInt16 (os: BinaryWriter) x = 
            os.Write  (b0 x)
            os.Write  (b1 x)
              
        let writeDirectory os dict =
            writeInt32 os (if dict.size = 0x0 then 0x0 else dict.addr);
            writeInt32 os dict.size

        let writeBytes (os: BinaryWriter) (chunk:byte[]) = os.Write(chunk, 0, chunk.Length)  

        let writeBinaryAndReportMappings (outfile: string, 
                                          ilg: ILGlobals, pdbfile: string option, (* signer: ILStrongNameSigner option, *) portablePDB, embeddedPDB, 
                                          embedAllSource, embedSourceList, sourceLink, emitTailcalls, deterministic, showTimes, dumpDebugInfo ) modul =
            let isDll = modul.IsDLL
            
            let os = 
                try
                    // Ensure the output directory exists otherwise it will fail
                    let dir = Path.GetDirectoryName(outfile)
                    if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |>ignore
                    new BinaryWriter(System.IO.File.OpenWrite(outfile))
                with e -> 
                    failwith ("Could not open file for writing (binary mode): " + outfile + "\n" + e.ToString())    

            let pdbData, pdbOpt, debugDirectoryChunk, debugDataChunk, debugEmbeddedPdbChunk, textV2P, mappings =
                try 

                  let imageBaseReal = modul.ImageBase // FIXED CHOICE
                  let alignVirt = modul.VirtualAlignment // FIXED CHOICE
                  let alignPhys = modul.PhysicalAlignment // FIXED CHOICE
                  
                  let isItanium = modul.Platform = Some(IA64)
                  
                  let numSections = 3 // .text, .sdata, .reloc 


                  // HEADERS 
                  let next = 0x0
                  let headerSectionPhysLoc = 0x0
                  let headerAddr = next
                  let next = headerAddr
                  
                  let msdosHeaderSize = 0x80
                  let msdosHeaderChunk, next = chunk msdosHeaderSize next
                  
                  let peSignatureSize = 0x04
                  let peSignatureChunk, next = chunk peSignatureSize next
                  
                  let peFileHeaderSize = 0x14
                  let peFileHeaderChunk, next = chunk peFileHeaderSize next
                  
                  let peOptionalHeaderSize = if modul.Is64Bit then 0xf0 else 0xe0
                  let peOptionalHeaderChunk, next = chunk peOptionalHeaderSize next
                  
                  let textSectionHeaderSize = 0x28
                  let textSectionHeaderChunk, next = chunk textSectionHeaderSize next
                  
                  let dataSectionHeaderSize = 0x28
                  let dataSectionHeaderChunk, next = chunk dataSectionHeaderSize next
                  
                  let relocSectionHeaderSize = 0x28
                  let relocSectionHeaderChunk, next = chunk relocSectionHeaderSize next
                  
                  let headerSize = next - headerAddr
                  let nextPhys = align alignPhys (headerSectionPhysLoc + headerSize)
                  let headerSectionPhysSize = nextPhys - headerSectionPhysLoc
                  let next = align alignVirt (headerAddr + headerSize)
                  
                  // TEXT SECTION:  8 bytes IAT table 72 bytes CLI header 

                  let textSectionPhysLoc = nextPhys
                  let textSectionAddr = next
                  let next = textSectionAddr
                  
                  let importAddrTableChunk, next = chunk 0x08 next
                  let cliHeaderPadding = (if isItanium then (align 16 next) else next) - next
                  let next = next + cliHeaderPadding
                  let cliHeaderChunk, next = chunk 0x48 next
                  
                  let desiredMetadataVersion = 
                    if modul.MetadataVersion <> "" then
                        Version.Parse modul.MetadataVersion
                    else
                        match ilg.systemRuntimeScopeRef with 
                        | ILScopeRef.Local -> failwith "Expected mscorlib to be ILScopeRef.Assembly was ILScopeRef.Local" 
                        | ILScopeRef.Module(_) -> failwith "Expected mscorlib to be ILScopeRef.Assembly was ILScopeRef.Module"
                        | ILScopeRef.Assembly(aref) ->
                            match aref.Version with
                            | USome v -> v
                            | UNone -> failwith "Expected msorlib to have a version number"

                  let entryPointToken, code, codePadding, metadata, data, resources, requiredDataFixups, pdbData, mappings, guidStart =
                    writeILMetadataAndCode ((pdbfile <> None), desiredMetadataVersion, ilg, emitTailcalls, deterministic, showTimes) modul next

                  let _codeChunk, next = chunk code.Length next
                  let _codePaddingChunk, next = chunk codePadding.Length next
                  
                  let metadataChunk, next = chunk metadata.Length next
                  
#if EMIT_STRONG_NAME
                  let strongnameChunk, next = 
                    match signer with 
                    | None -> nochunk next
                    | Some s -> chunk s.SignatureSize next
#else
                  let strongnameChunk, next = nochunk next
#endif

                  let resourcesChunk, next = chunk resources.Length next
                 
                  let rawdataChunk, next = chunk data.Length next

                  let vtfixupsChunk, next = nochunk next   // Note: only needed for mixed mode assemblies
                  let importTableChunkPrePadding = (if isItanium then (align 16 next) else next) - next
                  let next = next + importTableChunkPrePadding
                  let importTableChunk, next = chunk 0x28 next
                  let importLookupTableChunk, next = chunk 0x14 next
                  let importNameHintTableChunk, next = chunk 0x0e next
                  let mscoreeStringChunk, next = chunk 0x0c next

                  let next = align 0x10 (next + 0x05) - 0x05
                  let importTableChunk = { addr=importTableChunk.addr; size = next - importTableChunk.addr}
                  let importTableChunkPadding = importTableChunk.size - (0x28 + 0x14 + 0x0e + 0x0c)
                  
                  let next = next + 0x03
                  let entrypointCodeChunk, next = chunk 0x06 next
                  let globalpointerCodeChunk, next = chunk (if isItanium then 0x8 else 0x0) next

#if EMIT_DEBUG_INFO
                  let pdbOpt =
                    match portablePDB with
                    | true  -> 
                        let (uncompressedLength, contentId, stream) as pdbStream = generatePortablePdb embedAllSource embedSourceList sourceLink showTimes pdbData deterministic
                        if embeddedPDB then Some (compressPortablePdbStream uncompressedLength contentId stream)
                        else Some (pdbStream)
                    | _ -> None

                  let debugDirectoryChunk, next = 
                    chunk (if pdbfile = None then 
                               0x0
                           else if embeddedPDB && portablePDB then
                               sizeof_IMAGE_DEBUG_DIRECTORY * 2
                           else
                               sizeof_IMAGE_DEBUG_DIRECTORY
                          ) next

                  // The debug data is given to us by the PDB writer and appears to 
                  // typically be the type of the data plus the PDB file name.  We fill 
                  // this in after we've written the binary. We approximate the size according 
                  // to what PDB writers seem to require and leave extra space just in case... 
                  let debugDataJustInCase = 40
                  let debugDataChunk, next = 
                      chunk (align 0x4 (match pdbfile with 
                                        | None -> 0
                                        | Some f -> (24 
                                                    + Encoding.Unicode.GetByteCount(f) // See bug 748444
                                                    + debugDataJustInCase))) next

                  let debugEmbeddedPdbChunk, next = 
                      let streamLength = 
                            match pdbOpt with
                            | Some (_, _, stream) -> int(stream.Length)
                            | None -> 0
                      chunk (align 0x4 (match embeddedPDB with 
                                        | true -> 8 + streamLength
                                        | _ -> 0 )) next

#else
                  let pdbOpt = None
                  let debugDirectoryChunk, next = chunk 0x0 next
                  let debugDataChunk, next = chunk (align 0x4 0) next
                  let debugEmbeddedPdbChunk, next = chunk (align 0x4 0) next
#endif

                  let textSectionSize = next - textSectionAddr
                  let nextPhys = align alignPhys (textSectionPhysLoc + textSectionSize)
                  let textSectionPhysSize = nextPhys - textSectionPhysLoc
                  let next = align alignVirt (textSectionAddr + textSectionSize)
                  
                  // .RSRC SECTION (DATA) 
                  let dataSectionPhysLoc =  nextPhys
                  let dataSectionAddr = next
                  let dataSectionVirtToPhys v = v - dataSectionAddr + dataSectionPhysLoc
                  
                  
#if EMIT_NATIVE_RESOURCES
                  let resourceFormat = if modul.Is64Bit then Support.X64 else Support.X86
                  let nativeResources = 
                    match modul.NativeResources with
                    | [] -> [||]
                    | resources ->
                        if runningOnMono then
                          [||]
                        else
                          let unlinkedResources = List.map Lazy.force resources
                          begin
                            try linkNativeResources unlinkedResources next resourceFormat (Path.GetDirectoryName(outfile))
                            with e -> failwith ("Linking a native resource failed: "+e.Message+"")
                          end
                  let nativeResourcesSize = nativeResources.Length
                  let nativeResourcesChunk, next = chunk nativeResourcesSize next
#else
                  let nativeResourcesChunk, next = chunk 0x0 next
#endif

                
                  let dummydatap, next = chunk (if next = dataSectionAddr then 0x01 else 0x0) next
                  
                  let dataSectionSize = next - dataSectionAddr
                  let nextPhys = align alignPhys (dataSectionPhysLoc + dataSectionSize)
                  let dataSectionPhysSize = nextPhys - dataSectionPhysLoc
                  let next = align alignVirt (dataSectionAddr + dataSectionSize)
                  
                  // .RELOC SECTION  base reloc table: 0x0c size 
                  let relocSectionPhysLoc =  nextPhys
                  let relocSectionAddr = next
                  let baseRelocTableChunk, next = chunk 0x0c next

                  let relocSectionSize = next - relocSectionAddr
                  let nextPhys = align alignPhys (relocSectionPhysLoc + relocSectionSize)
                  let relocSectionPhysSize = nextPhys - relocSectionPhysLoc
                  let next = align alignVirt (relocSectionAddr + relocSectionSize)

                 // Now we know where the data section lies we can fix up the  
                 // references into the data section from the metadata tables. 
                  begin 
                    requiredDataFixups |> Array.iter
                      (fun (metadataOffset32, (dataOffset, kind)) -> 
                        let metadataOffset =  metadataOffset32
                        if metadataOffset < 0 || metadataOffset >= metadata.Length - 4  then failwith "data RVA fixup: fixup located outside metadata";
                        checkFixup32 metadata metadataOffset 0xdeaddddd;
                        let dataRva = 
                          if kind then
                              let res = dataOffset
                              if res >= resourcesChunk.size then printfn ("resource offset bigger than resource data section");
                              res
                          else 
                              let res = rawdataChunk.addr + dataOffset
                              if res < rawdataChunk.addr then printfn ("data rva before data section");
                              if res >= rawdataChunk.addr + rawdataChunk.size then printfn "%s" ("data rva after end of data section, dataRva = "+string res+", rawdataChunk.addr = "+string rawdataChunk.addr+", rawdataChunk.size = "+string rawdataChunk.size);
                              res
                        applyFixup32 metadata metadataOffset dataRva);
                  end;
                  
                 // IMAGE TOTAL SIZE 
                  let imageEndSectionPhysLoc =  nextPhys
                  let imageEndAddr = next


                  let write p (os: BinaryWriter) chunkName chunk = 
                      match p with 
                      | None -> () 
                      | Some pExpected -> 
                          os.Flush(); 
                          let pCurrent =  int32 os.BaseStream.Position
                          if pCurrent <> pExpected then 
                            failwith ("warning: "+chunkName+" not where expected, pCurrent = "+string pCurrent+", p.addr = "+string pExpected) 
                      writeBytes os chunk 
                  
                  let writePadding (os: BinaryWriter) _comment sz =
                      if sz < 0 then failwith "writePadding: size < 0";
                      for i = 0 to sz - 1 do 
                          os.Write 0uy
                  
                  // Now we've computed all the offsets, write the image 
                  
                  write (Some msdosHeaderChunk.addr) os "msdos header" msdosHeader;
                  
                  write (Some peSignatureChunk.addr) os "pe signature" [| |];
                  
                  writeInt32 os 0x4550;
                  
                  write (Some peFileHeaderChunk.addr) os "pe file header" [| |];
                  
                  if (modul.Platform = Some(AMD64)) then
                    writeInt32AsUInt16 os 0x8664    // Machine - IMAGE_FILE_MACHINE_AMD64 
                  elif isItanium then
                    writeInt32AsUInt16 os 0x200
                  else
                    writeInt32AsUInt16 os 0x014c;   // Machine - IMAGE_FILE_MACHINE_I386 
                    
                  writeInt32AsUInt16 os numSections;

#if EMIT_DEBUG_INFO
                  let pdbData = 
                    if deterministic then
                      // Hash code, data and metadata
                      use sha = System.Security.Cryptography.SHA1.Create()    // IncrementalHash is core only
                      let hCode = sha.ComputeHash code
                      let hData = sha.ComputeHash data
                      let hMeta = sha.ComputeHash metadata
                      let final = [| hCode; hData; hMeta |] |> Array.collect id |> sha.ComputeHash

                      // Confirm we have found the correct data and aren't corrupting the metadata
                      if metadata.[ guidStart..guidStart+3]     <> [| 4uy; 3uy; 2uy; 1uy |] then failwith "Failed to find MVID"
                      if metadata.[ guidStart+12..guidStart+15] <> [| 4uy; 3uy; 2uy; 1uy |] then failwith "Failed to find MVID"

                      // Update MVID guid in metadata
                      Array.blit final 0 metadata guidStart 16

                      // Use last 4 bytes for timestamp - High bit set, to stop tool chains becoming confused
                      let timestamp = int final.[16] ||| (int final.[17] <<< 8) ||| (int final.[18] <<< 16) ||| (int (final.[19] ||| 128uy) <<< 24) 
                      writeInt32 os timestamp
                      // Update pdbData with new guid and timestamp.  Portable and embedded PDBs don't need the ModuleID
                      // Full and PdbOnly aren't supported under deterministic builds currently, they rely on non-determinsitic Windows native code
                      { pdbData with ModuleID = final.[0..15] ; Timestamp = timestamp }
                    else
                      writeInt32 os timestamp   // date since 1970
                      pdbData
#else
                  writeInt32 os timestamp   // date since 1970
#endif

                  writeInt32 os 0x00; // Pointer to Symbol Table Always 0 
               // 00000090 
                  writeInt32 os 0x00; // Number of Symbols Always 0 
                  writeInt32AsUInt16 os peOptionalHeaderSize; // Size of the optional header, the format is described below. 
                  
                  // 64bit: IMAGE_FILE_32BIT_MACHINE ||| IMAGE_FILE_LARGE_ADDRESS_AWARE
                  // 32bit: IMAGE_FILE_32BIT_MACHINE
                  // Yes, 32BIT_MACHINE is set for AMD64...
                  let iMachineCharacteristic = match modul.Platform with | Some IA64 -> 0x20 | Some AMD64 -> 0x0120 | _ -> 0x0100
                  
                  writeInt32AsUInt16 os ((if isDll then 0x2000 else 0x0000) ||| 0x0002 ||| 0x0004 ||| 0x0008 ||| iMachineCharacteristic);
                  
               // Now comes optional header 

                  let peOptionalHeaderByte = peOptionalHeaderByteByCLRVersion desiredMetadataVersion

                  write (Some peOptionalHeaderChunk.addr) os "pe optional header" [| |];
                  if modul.Is64Bit then
                    writeInt32AsUInt16 os 0x020B // Magic number is 0x020B for 64-bit 
                  else
                    writeInt32AsUInt16 os 0x010b; // Always 0x10B (see Section 23.1). 
                  writeInt32AsUInt16 os peOptionalHeaderByte; // ECMA spec says 6, some binaries, e.g. fscmanaged.exe say 7, Whidbey binaries say 8 
                  writeInt32 os textSectionPhysSize;          // Size of the code (text) section, or the sum of all code sections if there are multiple sections. 
                // 000000a0 
                  writeInt32 os dataSectionPhysSize;          // Size of the initialized data section, or the sum of all such sections if there are multiple data sections. 
                  writeInt32 os 0x00;                         // Size of the uninitialized data section, or the sum of all such sections if there are multiple uninitialized data sections. 
                  writeInt32 os entrypointCodeChunk.addr;     // RVA of entry point , needs to point to bytes 0xFF 0x25 followed by the RVA+!0x4000000 in a section marked execute/read for EXEs or 0 for DLLs e.g. 0x0000b57e 
                  writeInt32 os textSectionAddr;              // e.g. 0x0002000 
               // 000000b0 
                  if modul.Is64Bit then
                    writeInt64 os ((int64)imageBaseReal)    // REVIEW: For 64-bit, we should use a 64-bit image base 
                  else             
                    writeInt32 os dataSectionAddr; // e.g. 0x0000c000           
                    writeInt32 os imageBaseReal; // Image Base Always 0x400000 (see Section 23.1). - QUERY: no it's not always 0x400000, e.g. 0x034f0000 
                    
                  writeInt32 os alignVirt;  //  Section Alignment Always 0x2000 (see Section 23.1). 
                  writeInt32 os alignPhys; // File Alignment Either 0x200 or 0x1000. 
               // 000000c0  
                  writeInt32AsUInt16 os 0x04; //  OS Major Always 4 (see Section 23.1). 
                  writeInt32AsUInt16 os 0x00; // OS Minor Always 0 (see Section 23.1). 
                  writeInt32AsUInt16 os 0x00; // User Major Always 0 (see Section 23.1). 
                  writeInt32AsUInt16 os 0x00; // User Minor Always 0 (see Section 23.1). 
                  do
                    let (major, minor) = modul.SubsystemVersion
                    writeInt32AsUInt16 os major;
                    writeInt32AsUInt16 os minor;
                  writeInt32 os 0x00; // Reserved Always 0 (see Section 23.1). 
               // 000000d0  
                  writeInt32 os imageEndAddr; // Image Size: Size, in bytes, of image, including all headers and padding; shall be a multiple of Section Alignment. e.g. 0x0000e000 
                  writeInt32 os headerSectionPhysSize; // Header Size Combined size of MS-DOS Header, PE Header, PE Optional Header and padding; shall be a multiple of the file alignment. 
                  writeInt32 os 0x00; // File Checksum Always 0 (see Section 23.1). QUERY: NOT ALWAYS ZERO 
                  writeInt32AsUInt16 os modul.SubSystemFlags; // SubSystem Subsystem required to run this image. Shall be either IMAGE_SUBSYSTEM_WINDOWS_CE_GUI (0x3) or IMAGE_SUBSYSTEM_WINDOWS_GUI (0x2). QUERY: Why is this 3 on the images ILASM produces 
                  // DLL Flags Always 0x400 (no unmanaged windows exception handling - see Section 23.1).
                  //  Itanium: see notes at end of file 
                  //  IMAGE_DLLCHARACTERISTICS_NX_COMPAT: See FSharp 1.0 bug 5019 and http://blogs.msdn.com/ed_maurer/archive/2007/12/14/nxcompat-and-the-c-compiler.aspx 
                  // Itanium: IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE | IMAGE_DLLCHARACTERISTICS_ NO_SEH | IMAGE_DLL_CHARACTERISTICS_DYNAMIC_BASE | IMAGE_DLLCHARACTERISTICS_NX_COMPAT
                  // x86: IMAGE_DLLCHARACTERISTICS_ NO_SEH | IMAGE_DLL_CHARACTERISTICS_DYNAMIC_BASE | IMAGE_DLLCHARACTERISTICS_NX_COMPAT
                  // x64: IMAGE_DLLCHARACTERISTICS_ NO_SEH | IMAGE_DLL_CHARACTERISTICS_DYNAMIC_BASE | IMAGE_DLLCHARACTERISTICS_NX_COMPAT
                  let dllCharacteristics = 
                    let flags  = 
                        if modul.Is64Bit then (if isItanium then 0x8540 else 0x540)
                        else 0x540
                    if modul.UseHighEntropyVA then flags ||| 0x20 // IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA
                    else flags
                  writeInt32AsUInt16 os dllCharacteristics
               // 000000e0 
                  // Note that the defaults differ between x86 and x64
                  if modul.Is64Bit then
                    let size = defaultArg modul.StackReserveSize 0x400000 |> int64
                    writeInt64 os size // Stack Reserve Size Always 0x400000 (4Mb) (see Section 23.1). 
                    writeInt64 os 0x4000L // Stack Commit Size Always 0x4000 (16Kb) (see Section 23.1). 
                    writeInt64 os 0x100000L // Heap Reserve Size Always 0x100000 (1Mb) (see Section 23.1). 
                    writeInt64 os 0x2000L // Heap Commit Size Always 0x800 (8Kb) (see Section 23.1). 
                  else
                    let size = defaultArg modul.StackReserveSize 0x100000
                    writeInt32 os size // Stack Reserve Size Always 0x100000 (1Mb) (see Section 23.1). 
                    writeInt32 os 0x1000 // Stack Commit Size Always 0x1000 (4Kb) (see Section 23.1). 
                    writeInt32 os 0x100000 // Heap Reserve Size Always 0x100000 (1Mb) (see Section 23.1). 
                    writeInt32 os 0x1000 // Heap Commit Size Always 0x1000 (4Kb) (see Section 23.1).             
               // 000000f0 - x86 location, moving on, for x64, add 0x10  
                  writeInt32 os 0x00 // Loader Flags Always 0 (see Section 23.1) 
                  writeInt32 os 0x10 // Number of Data Directories: Always 0x10 (see Section 23.1). 
                  writeInt32 os 0x00 
                  writeInt32 os 0x00 // Export Table Always 0 (see Section 23.1). 
               // 00000100  
                  writeDirectory os importTableChunk // Import Table RVA of Import Table, (see clause 24.3.1). e.g. 0000b530  
                  // Native Resource Table: ECMA says Always 0 (see Section 23.1), but mscorlib and other files with resources bound into executable do not.  For the moment assume the resources table is always the first resource in the file. 
                  writeDirectory os nativeResourcesChunk

               // 00000110  
                  writeInt32 os 0x00 // Exception Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Exception Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Certificate Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Certificate Table Always 0 (see Section 23.1). 
               // 00000120  
                  writeDirectory os baseRelocTableChunk 
                  writeDirectory os debugDirectoryChunk // Debug Directory 
               // 00000130  
                  writeInt32 os 0x00 //  Copyright Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 //  Copyright Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Global Ptr Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Global Ptr Always 0 (see Section 23.1). 
               // 00000140  
                  writeInt32 os 0x00 // Load Config Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Load Config Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // TLS Table Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // TLS Table Always 0 (see Section 23.1). 
               // 00000150   
                  writeInt32 os 0x00 // Bound Import Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Bound Import Always 0 (see Section 23.1). 
                  writeDirectory os importAddrTableChunk // Import Addr Table, (see clause 24.3.1). e.g. 0x00002000  
               // 00000160   
                  writeInt32 os 0x00 // Delay Import Descriptor Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Delay Import Descriptor Always 0 (see Section 23.1). 
                  writeDirectory os cliHeaderChunk
               // 00000170  
                  writeInt32 os 0x00 // Reserved Always 0 (see Section 23.1). 
                  writeInt32 os 0x00 // Reserved Always 0 (see Section 23.1). 
                  
                  write (Some textSectionHeaderChunk.addr) os "text section header" [| |]
                  
               // 00000178  
                  writeBytes os  [| 0x2euy; 0x74uy; 0x65uy; 0x78uy; 0x74uy; 0x00uy; 0x00uy; 0x00uy; |] // ".text\000\000\000" 
               // 00000180  
                  writeInt32 os textSectionSize // VirtualSize: Total size of the section when loaded into memory in bytes rounded to Section Alignment. If this value is greater than Size of Raw Data, the section is zero-padded. e.g. 0x00009584 
                  writeInt32 os textSectionAddr //  VirtualAddress For executable images this is the address of the first byte of the section, when loaded into memory, relative to the image base. e.g. 0x00020000 
                  writeInt32 os textSectionPhysSize //  SizeOfRawData Size of the initialized data on disk in bytes, shall be a multiple of FileAlignment from the PE header. If this is less than VirtualSize the remainder of the section is zero filled. Because this field is rounded while the VirtualSize field is not it is possible for this to be greater than VirtualSize as well. When a section contains only uninitialized data, this field should be 0. 0x00009600 
                  writeInt32 os textSectionPhysLoc // PointerToRawData RVA to section's first page within the PE file. This shall be a multiple of FileAlignment from the optional header. When a section contains only uninitialized data, this field should be 0. e.g. 00000200 
               // 00000190  
                  writeInt32 os 0x00 // PointerToRelocations RVA of Relocation section. 
                  writeInt32 os 0x00 // PointerToLineNumbers Always 0 (see Section 23.1). 
               // 00000198  
                  writeInt32AsUInt16 os 0x00// NumberOfRelocations Number of relocations, set to 0 if unused. 
                  writeInt32AsUInt16 os 0x00  //  NumberOfLinenumbers Always 0 (see Section 23.1). 
                  writeBytes os [| 0x20uy; 0x00uy; 0x00uy; 0x60uy |] //  Characteristics Flags describing section's characteristics, see below. IMAGE_SCN_CNT_CODE || IMAGE_SCN_MEM_EXECUTE || IMAGE_SCN_MEM_READ 
                  
                  write (Some dataSectionHeaderChunk.addr) os "data section header" [| |]
                  
               // 000001a0  
                  writeBytes os [| 0x2euy; 0x72uy; 0x73uy; 0x72uy; 0x63uy; 0x00uy; 0x00uy; 0x00uy; |] // ".rsrc\000\000\000" 
            //  writeBytes os [| 0x2e; 0x73; 0x64; 0x61; 0x74; 0x61; 0x00; 0x00; |] // ".sdata\000\000"  
                  writeInt32 os dataSectionSize // VirtualSize: Total size of the section when loaded into memory in bytes rounded to Section Alignment. If this value is greater than Size of Raw Data, the section is zero-padded. e.g. 0x0000000c 
                  writeInt32 os dataSectionAddr //  VirtualAddress For executable images this is the address of the first byte of the section, when loaded into memory, relative to the image base. e.g. 0x0000c000
               // 000001b0  
                  writeInt32 os dataSectionPhysSize //  SizeOfRawData Size of the initialized data on disk in bytes, shall be a multiple of FileAlignment from the PE header. If this is less than VirtualSize the remainder of the section is zero filled. Because this field is rounded while the VirtualSize field is not it is possible for this to be greater than VirtualSize as well. When a section contains only uninitialized data, this field should be 0. e.g. 0x00000200 
                  writeInt32 os dataSectionPhysLoc // PointerToRawData QUERY: Why does ECMA say "RVA" here? Offset to section's first page within the PE file. This shall be a multiple of FileAlignment from the optional header. When a section contains only uninitialized data, this field should be 0. e.g. 0x00009800 
               // 000001b8  
                  writeInt32 os 0x00 // PointerToRelocations RVA of Relocation section. 
                  writeInt32 os 0x00 // PointerToLineNumbers Always 0 (see Section 23.1). 
               // 000001c0  
                  writeInt32AsUInt16 os 0x00 // NumberOfRelocations Number of relocations, set to 0 if unused. 
                  writeInt32AsUInt16 os 0x00  //  NumberOfLinenumbers Always 0 (see Section 23.1). 
                  writeBytes os [| 0x40uy; 0x00uy; 0x00uy; 0x40uy |] //  Characteristics Flags: IMAGE_SCN_MEM_READ |  IMAGE_SCN_CNT_INITIALIZED_DATA 
                  
                  write (Some relocSectionHeaderChunk.addr) os "reloc section header" [| |]
               // 000001a0  
                  writeBytes os [| 0x2euy; 0x72uy; 0x65uy; 0x6cuy; 0x6fuy; 0x63uy; 0x00uy; 0x00uy; |] // ".reloc\000\000" 
                  writeInt32 os relocSectionSize // VirtualSize: Total size of the section when loaded into memory in bytes rounded to Section Alignment. If this value is greater than Size of Raw Data, the section is zero-padded. e.g. 0x0000000c 
                  writeInt32 os relocSectionAddr //  VirtualAddress For executable images this is the address of the first byte of the section, when loaded into memory, relative to the image base. e.g. 0x0000c000
               // 000001b0  
                  writeInt32 os relocSectionPhysSize //  SizeOfRawData Size of the initialized reloc on disk in bytes, shall be a multiple of FileAlignment from the PE header. If this is less than VirtualSize the remainder of the section is zero filled. Because this field is rounded while the VirtualSize field is not it is possible for this to be greater than VirtualSize as well. When a section contains only uninitialized reloc, this field should be 0. e.g. 0x00000200 
                  writeInt32 os relocSectionPhysLoc // PointerToRawData QUERY: Why does ECMA say "RVA" here? Offset to section's first page within the PE file. This shall be a multiple of FileAlignment from the optional header. When a section contains only uninitialized reloc, this field should be 0. e.g. 0x00009800 
               // 000001b8  
                  writeInt32 os 0x00 // PointerToRelocations RVA of Relocation section. 
                  writeInt32 os 0x00 // PointerToLineNumbers Always 0 (see Section 23.1). 
               // 000001c0  
                  writeInt32AsUInt16 os 0x00 // NumberOfRelocations Number of relocations, set to 0 if unused. 
                  writeInt32AsUInt16 os 0x00  //  NumberOfLinenumbers Always 0 (see Section 23.1). 
                  writeBytes os [| 0x40uy; 0x00uy; 0x00uy; 0x42uy |] //  Characteristics Flags: IMAGE_SCN_CNT_INITIALIZED_DATA | IMAGE_SCN_MEM_READ |  
                  
                  writePadding os "pad to text begin" (textSectionPhysLoc - headerSize)
                  
                  // TEXT SECTION: e.g. 0x200 
                  
                  let textV2P v = v - textSectionAddr + textSectionPhysLoc
                  
                  // e.g. 0x0200 
                  write (Some (textV2P importAddrTableChunk.addr)) os "import addr table" [| |]
                  writeInt32 os importNameHintTableChunk.addr 
                  writeInt32 os 0x00  // QUERY 4 bytes of zeros not 2 like ECMA  24.3.1 says 
                  
                  // e.g. 0x0208 

                  let flags = 
                    (if modul.IsILOnly then 0x01 else 0x00) ||| 
                    (if modul.Is32Bit then 0x02 else 0x00) ||| 
                    (if modul.Is32BitPreferred then 0x00020003 else 0x00) ||| 
#if EMIT_STRONG_NAME
                    (if (match signer with None -> false | Some s -> s.IsFullySigned) then 0x08 else 0x00) |||
#endif
                    0x0000

                  let headerVersionMajor, headerVersionMinor = headerVersionSupportedByCLRVersion desiredMetadataVersion

                  writePadding os "pad to cli header" cliHeaderPadding 
                  write (Some (textV2P cliHeaderChunk.addr)) os "cli header"  [| |]
                  writeInt32 os 0x48 // size of header 
                  writeInt32AsUInt16 os headerVersionMajor // Major part of minimum version of CLR reqd. 
                  writeInt32AsUInt16 os headerVersionMinor // Minor part of minimum version of CLR reqd. ... 
                  // e.g. 0x0210 
                  writeDirectory os metadataChunk
                  writeInt32 os flags
                  
                  writeInt32 os entryPointToken 
                  write None os "rest of cli header" [| |]
                  
                  // e.g. 0x0220 
                  writeDirectory os resourcesChunk
                  writeDirectory os strongnameChunk
                  // e.g. 0x0230 
                  writeInt32 os 0x00 // code manager table, always 0 
                  writeInt32 os 0x00 // code manager table, always 0 
                  writeDirectory os vtfixupsChunk 
                  // e.g. 0x0240 
                  writeInt32 os 0x00  // export addr table jumps, always 0 
                  writeInt32 os 0x00  // export addr table jumps, always 0 
                  writeInt32 os 0x00  // managed native header, always 0 
                  writeInt32 os 0x00  // managed native header, always 0 
                  
                  writeBytes os code
                  write None os "code padding" codePadding
                  
                  writeBytes os metadata
                  
#if EMIT_STRONG_NAME
                  // write 0x80 bytes of empty space for encrypted SHA1 hash, written by SN.EXE or call to signing API 
                  if signer <> None then 
                    write (Some (textV2P strongnameChunk.addr)) os "strongname" (Array.create strongnameChunk.size 0x0uy)
#endif

                  write (Some (textV2P resourcesChunk.addr)) os "raw resources" [| |]
                  writeBytes os resources
                  write (Some (textV2P rawdataChunk.addr)) os "raw data" [| |]
                  writeBytes os data

                  writePadding os "start of import table" importTableChunkPrePadding

                  // vtfixups would go here 
                  write (Some (textV2P importTableChunk.addr)) os "import table" [| |]
                  
                  writeInt32 os importLookupTableChunk.addr
                  writeInt32 os 0x00
                  writeInt32 os 0x00
                  writeInt32 os mscoreeStringChunk.addr
                  writeInt32 os importAddrTableChunk.addr
                  writeInt32 os 0x00
                  writeInt32 os 0x00
                  writeInt32 os 0x00
                  writeInt32 os 0x00
                  writeInt32 os 0x00 
                
                  write (Some (textV2P importLookupTableChunk.addr)) os "import lookup table" [| |]
                  writeInt32 os importNameHintTableChunk.addr 
                  writeInt32 os 0x00 
                  writeInt32 os 0x00 
                  writeInt32 os 0x00 
                  writeInt32 os 0x00 
                  

                  write (Some (textV2P importNameHintTableChunk.addr)) os "import name hint table" [| |]
                  // Two zero bytes of hint, then Case sensitive, null-terminated ASCII string containing name to import. 
                  // Shall _CorExeMain a .exe file _CorDllMain for a .dll file.
                  if isDll then 
                      writeBytes os [| 0x00uy;  0x00uy;  0x5fuy;  0x43uy ;  0x6fuy;  0x72uy;  0x44uy;  0x6cuy;  0x6cuy;  0x4duy;  0x61uy;  0x69uy;  0x6euy;  0x00uy |]
                  else 
                      writeBytes os [| 0x00uy;  0x00uy;  0x5fuy;  0x43uy;  0x6fuy;  0x72uy;  0x45uy;  0x78uy;  0x65uy;  0x4duy;  0x61uy;  0x69uy;  0x6euy;  0x00uy |]
                  
                  write (Some (textV2P mscoreeStringChunk.addr)) os "mscoree string"
                    [| 0x6duy;  0x73uy;  0x63uy;  0x6fuy ;  0x72uy;  0x65uy ;  0x65uy;  0x2euy ;  0x64uy;  0x6cuy ;  0x6cuy;  0x00uy ; |]
                  
                  writePadding os "end of import tab" importTableChunkPadding
                  
                  writePadding os "head of entrypoint" 0x03
                  let ep = (imageBaseReal + textSectionAddr)
                  write (Some (textV2P entrypointCodeChunk.addr)) os " entrypoint code"
                         [| 0xFFuy; 0x25uy; (* x86 Instructions for entry *) b0 ep; b1 ep; b2 ep; b3 ep |]
                  if isItanium then 
                      write (Some (textV2P globalpointerCodeChunk.addr)) os " itanium global pointer"
                           [| 0x0uy; 0x0uy; 0x0uy; 0x0uy; 0x0uy; 0x0uy; 0x0uy; 0x0uy |]

                  if pdbfile.IsSome then 
                      write (Some (textV2P debugDirectoryChunk.addr)) os "debug directory" (Array.create debugDirectoryChunk.size 0x0uy)
                      write (Some (textV2P debugDataChunk.addr)) os "debug data" (Array.create debugDataChunk.size 0x0uy)

                  if embeddedPDB then
                      write (Some (textV2P debugEmbeddedPdbChunk.addr)) os "debug data" (Array.create debugEmbeddedPdbChunk.size 0x0uy)

                  writePadding os "end of .text" (dataSectionPhysLoc - textSectionPhysLoc - textSectionSize)
                  
                  // DATA SECTION 
#if EMIT_NATIVE_RESOURCES
                  match nativeResources with
                  | [||] -> ()
                  | resources ->
                        write (Some (dataSectionVirtToPhys nativeResourcesChunk.addr)) os "raw native resources" [| |]
                        writeBytes os resources
#endif

                  if dummydatap.size <> 0x0 then
                      write (Some (dataSectionVirtToPhys dummydatap.addr)) os "dummy data" [| 0x0uy |]

                  writePadding os "end of .rsrc" (relocSectionPhysLoc - dataSectionPhysLoc - dataSectionSize)            
                  
                  // RELOC SECTION 

                  // See ECMA 24.3.2 
                  let relocV2P v = v - relocSectionAddr + relocSectionPhysLoc
                  
                  let entrypointFixupAddr = entrypointCodeChunk.addr + 0x02
                  let entrypointFixupBlock = (entrypointFixupAddr / 4096) * 4096
                  let entrypointFixupOffset = entrypointFixupAddr - entrypointFixupBlock
                  let reloc = (if modul.Is64Bit then 0xA000 (* IMAGE_REL_BASED_DIR64 *) else 0x3000 (* IMAGE_REL_BASED_HIGHLOW *)) ||| entrypointFixupOffset
                  // For the itanium, you need to set a relocation entry for the global pointer
                  let reloc2 = 
                      if not isItanium then 
                          0x0
                      else
                          0xA000 ||| (globalpointerCodeChunk.addr - ((globalpointerCodeChunk.addr / 4096) * 4096))
                       
                  write (Some (relocV2P baseRelocTableChunk.addr)) os "base reloc table" 
                      [| b0 entrypointFixupBlock; b1 entrypointFixupBlock; b2 entrypointFixupBlock; b3 entrypointFixupBlock;
                         0x0cuy; 0x00uy; 0x00uy; 0x00uy;
                         b0 reloc; b1 reloc; 
                         b0 reloc2; b1 reloc2; |]
                  writePadding os "end of .reloc" (imageEndSectionPhysLoc - relocSectionPhysLoc - relocSectionSize)

                  os.Dispose()
                  
                  pdbData, pdbOpt, debugDirectoryChunk, debugDataChunk, debugEmbeddedPdbChunk, textV2P, mappings

                // Looks like a finally
                with e ->   
                    (try 
                        os.Dispose()
                        File.Delete outfile 
                     with _ -> ()) 
                    reraise()

            //Finished writing and signing the binary and debug info...
            mappings

        type options =
           { ilg: ILGlobals
             pdbfile: string option
             portablePDB: bool
             embeddedPDB: bool
             embedAllSource: bool
             embedSourceList: string list
             sourceLink: string
#if EMIT_STRONG_NAME
             signer: ILStrongNameSigner option
#endif
             emitTailcalls: bool
             deterministic: bool
             showTimes: bool
             dumpDebugInfo:bool }

        let WriteILBinary (outfile, (args: options), modul) =
            writeBinaryAndReportMappings (outfile, 
                                          args.ilg, args.pdbfile, (* args.signer, *) args.portablePDB, args.embeddedPDB, args.embedAllSource, 
                                          args.embedSourceList, args.sourceLink, args.emitTailcalls, args.deterministic, args.showTimes, args.dumpDebugInfo) modul
            |> ignore

//====================================================================================================
// ProvidedAssembly - model for generated assembly fragments

namespace ProviderImplementation.ProvidedTypes

    #nowarn "1182"
    open System
    open System.Diagnostics
    open System.IO
    open System.Collections.Concurrent
    open System.Collections.Generic
    open System.Reflection

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.DerivedPatterns
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.ExprShape
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Reflection

    open ProviderImplementation.ProvidedTypes
    open ProviderImplementation.ProvidedTypes.AssemblyReader
    open ProviderImplementation.ProvidedTypes.UncheckedQuotations
    

    type ILLocalBuilder(i: int) =
        member __.LocalIndex = i
    
    [<RequireQualifiedAccess>]
    type ILExceptionClauseBuilder =
        | Finally of ILCodeLabel
        | Fault of ILCodeLabel
        | FilterCatch of ILCodeLabel * (ILCodeLabel * ILCodeLabel)
        | TypeCatch of ILCodeLabel * ILType

    type ILExceptionBlockBuilder(i: ILCodeLabel, leave: ILCodeLabel) =
        member __.StartIndex = i
        member __.Leave = leave
        member val EndIndex : int = 0 with get, set
        member val Clause : ILExceptionClauseBuilder option = None with get, set

    type ILGenerator(methodName) =
        let mutable locals =  ResizeArray<ILLocal>()
        let mutable instrs =  ResizeArray<ILInstr>()
        let mutable exceptions = ResizeArray<ILExceptionSpec>()
        let mutable labelCount =  0
        let mutable labels =  Dictionary<ILCodeLabel, int>()
        let mutable exceptionBlocks = Stack<ILExceptionBlockBuilder>()

        member __.Content = 
            { IsZeroInit = true
              MaxStack = instrs.Count
              Locals = locals.ToArray()
              Code = 
                { Labels = labels
                  Instrs = instrs.ToArray()
                  Exceptions = exceptions.ToArray()
                  Locals = [| |] (* TODO ILLocalDebugInfo *) }
             }

        member __.DeclareLocal(ty: ILType) = 
            let idx = locals.Count
            let local = { Type = ty; IsPinned = false; DebugInfo = None }
            locals.Add(local)
            ILLocalBuilder(idx)
        
        member ilg.BeginExceptionBlock() =
            exceptionBlocks.Push(ILExceptionBlockBuilder(ilg.DefineLabelHere(), ilg.DefineLabel()))
        
        member ilg.EndGuardedBlock() =
            let block = exceptionBlocks.Peek()
            ilg.Emit(I_leave block.Leave)
            block.EndIndex <- ilg.DefineLabelHere()
        
        member ilg.BeginCatchBlock(typ: ILType) =
            exceptionBlocks.Peek().Clause <- Some <|
                ILExceptionClauseBuilder.TypeCatch(ilg.DefineLabelHere(), typ)
    
        member ilg.BeginCatchFilterBlock(range: ILCodeLabel * ILCodeLabel) =
            exceptionBlocks.Peek().Clause <- Some <|
                ILExceptionClauseBuilder.FilterCatch(ilg.DefineLabelHere(), range)
        
        member ilg.BeginFinallyBlock() =
            exceptionBlocks.Peek().Clause <- Some <|
                ILExceptionClauseBuilder.Finally (ilg.DefineLabelHere())
    
        member ilg.BeginFaultBlock() =
            exceptionBlocks.Peek().Clause <- Some <|
                ILExceptionClauseBuilder.Fault (ilg.DefineLabelHere())
        
        member ilg.EndExceptionBlock() =
            let exnBlock = exceptionBlocks.Pop()
            match exnBlock.Clause.Value with
            | ILExceptionClauseBuilder.Finally(start) ->
               ilg.Emit(I_endfinally)
            | ILExceptionClauseBuilder.Fault(start) ->
               ilg.Emit(I_endfinally)
            | ILExceptionClauseBuilder.FilterCatch _ -> 
               ilg.Emit(I_leave exnBlock.Leave)
            | ILExceptionClauseBuilder.TypeCatch _ -> 
               ilg.Emit(I_leave exnBlock.Leave)
            let endIndex = ilg.DefineLabelHere()
            ilg.MarkLabel(exnBlock.Leave)
            let clause = 
                match exnBlock.Clause.Value with
                | ILExceptionClauseBuilder.Finally(start) ->
                   ILExceptionClause.Finally (start, endIndex)
                | ILExceptionClauseBuilder.Fault(start) ->
                    ILExceptionClause.Fault (start, endIndex)
                | ILExceptionClauseBuilder.FilterCatch(start, range) ->
                   ILExceptionClause.FilterCatch (range, (start, endIndex))
                | ILExceptionClauseBuilder.TypeCatch(start, typ) ->
                   ILExceptionClause.TypeCatch(typ, (start, endIndex))
        
            exceptions.Add { Range  = (exnBlock.StartIndex, exnBlock.EndIndex)
                           ; Clause = clause
                           }

        member __.DefineLabel() = labelCount <- labelCount + 1; labelCount
        member __.MarkLabel(label) = labels.[label] <- instrs.Count
        member this.DefineLabelHere() = let label = this.DefineLabel() in this.MarkLabel(label); label
        member __.Emit(opcode) = instrs.Add(opcode)
        override __.ToString() = "generator for " + methodName


    type ILFieldBuilder(enclosing: ILType, nm: string, fty: ILType, attrs: FieldAttributes) =

        let mutable lit = None
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.SetConstant(lit2) = (lit <- Some lit2)
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)
        member __.FormalFieldRef = ILFieldRef(enclosing.TypeRef, nm, fty)
        member this.FormalFieldSpec = ILFieldSpec(this.FormalFieldRef, enclosing)
        member __.Name = nm

        member __.Content =
            { Name = nm
              FieldType = fty
              LiteralValue = lit
              Attributes = attrs
              Offset = None
              CustomAttrs  = mkILCustomAttrs (cattrs.ToArray()) 
              Token = genToken() }
        override __.ToString() = "builder for " + nm

    type ILGenericParameterBuilder(nm, attrs: GenericParameterAttributes) =

        let mutable constraints = ResizeArray<ILType>()
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.AddConstraint(ty) = constraints.Add(ty)
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)

        member __.Content =
            { Name=nm
              Constraints= constraints.ToArray()
              Attributes = attrs 
              CustomAttrs  = mkILCustomAttrs (cattrs.ToArray())
              Token = genToken() }
        override __.ToString() = "builder for " + nm

    type ILParameterBuilder(ty: ILType) =

        let mutable attrs = ParameterAttributes.None
        let mutable nm = UNone
        let mutable dflt = UNone
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.SetData(attrs2, nm2) = attrs <- attrs2; nm <- USome nm2
        member __.SetConstant(obj) = dflt <- USome obj
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)

        member __.Content = 
            { Name=nm
              ParameterType=ty
              Default=dflt
              Attributes = attrs
              CustomAttrs  = mkILCustomAttrs (cattrs.ToArray()) }

    type ILMethodBuilder(enclosing: ILType, methodName: string, attrs: MethodAttributes, retty: ILType, argtys:ILType[]) =

        let ilParams = [| yield ILParameterBuilder(retty); for argty in argtys do yield ILParameterBuilder(argty) |]
        let mutable implflags = MethodImplAttributes.IL
        let gparams = ResizeArray<ILGenericParameterBuilder>()
        let cattrs = ResizeArray<ILCustomAttribute>()
        let mutable body = None

        member __.DefineGenericParameter(name, attrs) =  let eb = ILGenericParameterBuilder(name, attrs) in gparams.Add eb; eb
        member __.DefineParameter(i, attrs, parameterName) =  ilParams.[i].SetData(attrs, parameterName) ; ilParams.[i]
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)
        member __.GetILGenerator() = let ilg = ILGenerator(methodName) in body <- Some ilg; ilg
        member __.FormalMethodRef = 
            let cc = (if ILMethodDef.ComputeIsStatic attrs then ILCallingConv.Static else ILCallingConv.Instance)
            ILMethodRef (enclosing.TypeRef, cc, gparams.Count, methodName, argtys, retty)
        member this.FormalMethodSpec = 
            ILMethodSpec(this.FormalMethodRef, enclosing, mkILFormalGenericArgs enclosing.TypeSpec.GenericArgs.Length gparams.Count)

        member __.Content = 
            { Token = genToken()
              Name = methodName
              Attributes = attrs
              ImplAttributes = implflags
              GenericParams = [| for x in gparams -> x.Content |]  
              CustomAttrs = mkILCustomAttrs (cattrs.ToArray()) 
              Parameters = [| for p in ilParams.[1..] -> p.Content |]
              CallingConv = (if attrs &&& MethodAttributes.Static <> enum<_>(0) then ILCallingConv.Static else ILCallingConv.Instance)
              Return = (let p = ilParams.[0].Content in { Type = p.ParameterType; CustomAttrs = p.CustomAttrs })
              Body = body |> Option.map (fun b -> b.Content)
              IsEntryPoint = false }
        override __.ToString() = "builder for " + methodName

    type ILPropertyBuilder(nm, attrs: PropertyAttributes, retty: ILType, argtys: ILType[]) =

        let mutable setMethod = None
        let mutable getMethod = None
        let cattrs = ResizeArray<ILCustomAttribute>()
        
        member __.SetGetMethod(mb: ILMethodBuilder) = getMethod <- Some mb
        member __.SetSetMethod(mb: ILMethodBuilder) = setMethod <- Some mb
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)

        member __.Content = 
            { Name=nm
              CallingConv = 
                  (if (getMethod.IsSome && getMethod.Value.Content.IsStatic) || 
                      (setMethod.IsSome && setMethod.Value.Content.IsStatic) then 
                      ILThisConvention.Static 
                   else ILThisConvention.Instance)
              Attributes = attrs
              GetMethod = (getMethod |> Option.map (fun mb -> mb.FormalMethodRef))
              SetMethod = (setMethod |> Option.map (fun mb -> mb.FormalMethodRef))
              CustomAttrs = mkILCustomAttrs (cattrs.ToArray()) 
              PropertyType=retty
              Init= None // TODO if (attrs &&& PropertyAttributes.HasDefault) = 0 then None else 
              IndexParameterTypes=argtys
              Token = genToken() }
        override __.ToString() = "builder for " + nm

    type ILEventBuilder(nm, attrs: EventAttributes) =

        let mutable addMethod = None
        let mutable removeMethod = None
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.SetAddOnMethod(mb: ILMethodBuilder) = addMethod <- Some mb
        member __.SetRemoveOnMethod(mb: ILMethodBuilder) = removeMethod <- Some mb
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)
        member __.Content = 
            { Name = nm
              Attributes = attrs
              AddMethod = addMethod.Value.FormalMethodRef
              RemoveMethod = removeMethod.Value.FormalMethodRef
              CustomAttrs = mkILCustomAttrs (cattrs.ToArray())  
              Token = genToken()}
        override __.ToString() = "builder for " + nm

    type ILTypeBuilder(scoref, nsp: string uoption, nm: string, attrs: TypeAttributes) =

        let mutable extends = None
        let implements = ResizeArray<ILType>()
        let nestedTypes = ResizeArray<ILTypeBuilder>()
        let methods = ResizeArray<ILMethodBuilder>()
        let fields = ResizeArray<ILFieldBuilder>()
        let props = ResizeArray<ILPropertyBuilder>()
        let events = ResizeArray<ILEventBuilder>()
        let gparams = ResizeArray<ILGenericParameterBuilder>()
        let methodImpls = ResizeArray<ILMethodImplDef>()
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.ILTypeRef = ILTypeRef(scoref, nsp, nm)
        member this.ILTypeSpec = ILTypeSpec(this.ILTypeRef, mkILFormalGenericArgs 0 gparams.Count)
        member this.ILType = 
            match ILTypeDef.ComputeKind (int attrs) extends nsp nm with
            | ILTypeDefKind.ValueType | ILTypeDefKind.Enum -> ILType.Value this.ILTypeSpec
            | _ -> ILType.Boxed this.ILTypeSpec

        member this.DefineNestedType(name, attrs) = let tb = ILTypeBuilder(ILTypeRefScope.Nested this.ILTypeRef, UNone, name, attrs) in nestedTypes.Add tb; tb

        member this.DefineField(name, retty, attrs) = let fb = ILFieldBuilder(this.ILType, name, retty, attrs) in fields.Add fb; fb
        member this.DefineMethod(name, attrs, retty, argtys) = let mb = ILMethodBuilder(this.ILType, name, attrs, retty, argtys) in methods.Add mb; mb
        member this.DefineConstructor(attrs, argtys) = let mb = ILMethodBuilder(this.ILType, ".ctor", attrs  ||| MethodAttributes.SpecialName ||| MethodAttributes.RTSpecialName, ILType.Void, argtys) in methods.Add mb; mb
        member __.DefineProperty(name, attrs, propty, argtys) = let pb = ILPropertyBuilder(name, attrs, propty, argtys) in props.Add pb; pb
        member __.DefineEvent(name, attrs) = let eb = ILEventBuilder(name, attrs) in events.Add eb; eb
        member __.DefineMethodOverride(mimpl) = methodImpls.Add (mimpl)
        member __.DefineGenericParameter(name, attrs) =  let eb = ILGenericParameterBuilder(name, attrs) in gparams.Add eb; eb
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)
        member __.AddInterfaceImplementation(ty) = implements.Add(ty)
        member this.DefineTypeInitializer () = let mb = ILMethodBuilder(this.ILType, ".cctor", MethodAttributes.Static ||| MethodAttributes.SpecialName  ||| MethodAttributes.RTSpecialName  ||| MethodAttributes.Private, ILType.Void, [| |]) in methods.Add mb; mb
        member __.SetParent ty = (extends <- Some ty)
        member this.DefineDefaultConstructor(attrs, baseCtor: ILMethodSpec) = 
            let ctor = this.DefineConstructor(attrs, [| |]) 
            let ilg = ctor.GetILGenerator()
            ilg.Emit(I_ldarg 0)
            ilg.Emit(I_call (Normalcall, baseCtor, None))
            ilg.Emit(I_ret)
            ctor


        member __.Content = 
            { Namespace=nsp
              Name=nm
              GenericParams=  [| for x in gparams -> x.Content |]  
              Implements = implements.ToArray()
              Attributes = attrs
              Layout=ILTypeDefLayout.Auto
              Extends=extends
              Token=genToken()
            //SecurityDecls=emptyILSecurityDecls; 
            //HasSecurity=false;
              NestedTypes = ILTypeDefs( lazy [| for x in nestedTypes -> let td = x.Content in td.Namespace, td.Name, lazy td |] ) 
              Fields = { new ILFieldDefs with member __.Entries = [| for x in fields -> x.Content |] } 
              Properties = { new ILPropertyDefs with member __.Entries = [| for x in props -> x.Content |] } 
              Events = { new ILEventDefs with member __.Entries = [| for x in events -> x.Content |] } 
              Methods = ILMethodDefs (lazy [| for x in methods -> x.Content |])
              MethodImpls = { new ILMethodImplDefs  with member __.Entries = methodImpls.ToArray() } 
              CustomAttrs = mkILCustomAttrs (cattrs.ToArray()) 
            }
        override __.ToString() = "builder for " + joinILTypeName nsp nm

    type ILModuleBuilder(scoref, moduleName, manifest) =
        let typeDefs = ResizeArray<ILTypeBuilder>()
        let cattrs = ResizeArray<ILCustomAttribute>()

        member __.DefineType(nsp, name, attrs) = let tb = ILTypeBuilder(ILTypeRefScope.Top scoref, nsp, name, attrs) in typeDefs.Add tb; tb
        member __.SetCustomAttribute(ca) = cattrs.Add(ca)

        member __.Content = 
            { Manifest=manifest
              Name=moduleName
              SubsystemVersion = (4, 0)
              UseHighEntropyVA = false
              SubSystemFlags=3
              IsDLL=true
              IsILOnly=true
              Platform=None
              StackReserveSize=None
              Is32Bit=false
              Is32BitPreferred=false
              Is64Bit=false
              PhysicalAlignment=512
              VirtualAlignment=0x2000
              ImageBase=0x034f0000
              MetadataVersion=""
              Resources=ILResources  (lazy [| |])
              TypeDefs = ILTypeDefs( lazy [| for x in typeDefs-> let td = x.Content in td.Namespace, td.Name, lazy td |] ) 
              CustomAttrs = { new ILCustomAttrs with member __.Entries = cattrs.ToArray() }
            } 
        override __.ToString() = "builder for " + moduleName

    type ILAssemblyBuilder(assemblyName: AssemblyName, fileName, ilg, attrs : ILCustomAttribute seq) =
        let cattrs = ResizeArray<ILCustomAttribute>(attrs)
        let manifest = 
            { Name = assemblyName.Name
              AuxModuleHashAlgorithm = 0x8004 // SHA1
              PublicKey = UNone
              Version = UNone
              Locale = UNone
              CustomAttrs = { new ILCustomAttrs with member __.Entries = cattrs.ToArray() }
              //AssemblyLongevity=ILAssemblyLongevity.Unspecified
              DisableJitOptimizations = false
              JitTracking = true
              IgnoreSymbolStoreSequencePoints = false
              Retargetable =  false
              ExportedTypes = ILExportedTypesAndForwarders (lazy [| |])
              EntrypointElsewhere=None }
        let mb = ILModuleBuilder(ILScopeRef.Local, "MainModule", Some manifest)
        member __.MainModule = mb
        member __.Save() = 
            let il = mb.Content
            let options: BinaryWriter.options = { ilg = ilg; pdbfile = None; portablePDB = false; embeddedPDB = false; embedAllSource = false; embedSourceList = []; sourceLink = ""; emitTailcalls = true; deterministic = false; showTimes = false; dumpDebugInfo = false }
            BinaryWriter.WriteILBinary (fileName, options, il)
        override __.ToString() = "builder for " + (assemblyName.ToString())
        

    type ExpectedStackState =
        | Empty = 1
        | Address = 2
        | Value = 3

    type CodeGenerator(assemblyMainModule: ILModuleBuilder, 
                       genUniqueTypeName: (unit -> string), 
                       implicitCtorArgsAsFields: ILFieldBuilder list, 
                       convTypeToTgt: Type -> Type, 
                       transType: Type -> ILType, 
                       transFieldSpec: FieldInfo -> ILFieldSpec, 
                       transMeth: MethodInfo -> ILMethodSpec, 
                       transMethRef: MethodInfo -> ILMethodRef, 
                       transCtorSpec: ConstructorInfo -> ILMethodSpec, 
                       ilg: ILGenerator, 
                       localsMap:Dictionary<Var, ILLocalBuilder>, 
                       parameterVars) =

        // TODO: this works over FSharp.Core 4.4.0.0 types and methods. These types need to be retargeted to the target runtime.

        let getTypeFromHandleMethod() = (convTypeToTgt typeof<Type>).GetMethod("GetTypeFromHandle")
        let languagePrimitivesType() = (convTypeToTgt (typedefof<list<_>>.Assembly.GetType("Microsoft.FSharp.Core.LanguagePrimitives")))
        let parseInt32Method() = (convTypeToTgt (languagePrimitivesType())).GetMethod "ParseInt32"
        let decimalConstructor() = (convTypeToTgt typeof<decimal>).GetConstructor([| typeof<int>; typeof<int>; typeof<int>; typeof<bool>; typeof<byte> |])
        let dateTimeConstructor() = (convTypeToTgt typeof<DateTime>).GetConstructor([| typeof<int64>; typeof<DateTimeKind> |])
        let dateTimeOffsetConstructor() = (convTypeToTgt typeof<DateTimeOffset>).GetConstructor([| typeof<int64>; typeof<TimeSpan> |])
        let timeSpanConstructor() = (convTypeToTgt typeof<TimeSpan>).GetConstructor([|typeof<int64>|])
        
        let decimalTypeTgt = convTypeToTgt typeof<decimal>
        let convertTypeTgt = convTypeToTgt typeof<System.Convert>
        let stringTypeTgt = convTypeToTgt typeof<string>
        let mathTypeTgt = convTypeToTgt typeof<System.Math>

        let makeTypePattern tp = 
            let tt = convTypeToTgt tp
            fun (t : Type) -> if t = tt then Some() else None

        let (|Bool|_|) = makeTypePattern(typeof<bool>)
        let (|SByte|_|) = makeTypePattern(typeof<sbyte>)
        let (|Int16|_|) = makeTypePattern(typeof<int16>)
        let (|Int32|_|) = makeTypePattern(typeof<int32>)
        let (|Int64|_|) = makeTypePattern(typeof<int64>)
        let (|Byte|_|) = makeTypePattern(typeof<byte>)
        let (|UInt16|_|) = makeTypePattern(typeof<uint16>)
        let (|UInt32|_|) = makeTypePattern(typeof<uint32>)
        let (|UInt64|_|) = makeTypePattern(typeof<uint64>)
        let (|Single|_|) = makeTypePattern(typeof<single>)
        let (|Double|_|) = makeTypePattern(typeof<double>)
        let (|Char|_|) = makeTypePattern(typeof<char>)
        let (|Decimal|_|) = makeTypePattern(typeof<decimal>)
        let (|String|_|) = makeTypePattern(typeof<string>)

        let (|StaticMethod|_|) name tps (t : Type) =
            match t.GetMethod(name, BindingFlags.Static ||| BindingFlags.Public, null, tps, null) with 
            | null -> None
            | m -> Some m
            
        let (|StaticMethodWithReturnType|_|) name tps returnType (t : Type) =
            t.GetMethods(BindingFlags.Static ||| BindingFlags.Public) 
            |> Array.tryFind 
                (fun x -> 
                    x.Name = name
                        && x.ReturnType = returnType 
                        && (x.GetParameters() |> Array.map (fun i -> i.ParameterType)) = tps)
            

        let (|SpecificCall|_|) templateParameter = 
            // Note: precomputation
            match templateParameter with
            | (Lambdas(_, Call(_, minfo1, _)) | Call(_, minfo1, _)) ->
                let targetType = convTypeToTgt minfo1.DeclaringType
                let minfo1 = targetType.GetMethod(minfo1.Name, bindAll)
                let isg1 = minfo1.IsGenericMethod
                let gmd = 
                    if minfo1.IsGenericMethodDefinition then 
                        minfo1
                    elif isg1 then 
                        minfo1.GetGenericMethodDefinition() 
                    else null

                // end-of-precomputation

                (fun tm ->
                   match tm with
                   | Call(obj, minfo2, args)
        #if FX_NO_REFLECTION_METADATA_TOKENS
                      when ( // if metadata tokens are not available we'll rely only on equality of method references
        #else
                      when (minfo1.MetadataToken = minfo2.MetadataToken &&
        #endif
                            if isg1 then
                              minfo2.IsGenericMethod && gmd = minfo2.GetGenericMethodDefinition()
                            else
                              minfo1 = minfo2) ->
                       Some(obj, (minfo2.GetGenericArguments() |> Array.toList), args)
                   | _ -> None)
            | _ ->
                 invalidArg "templateParameter" "The parameter is not a recognized method name"

        let (|MakeDecimal|_|) = 
            let minfo1 = languagePrimitivesType().GetNestedType("IntrinsicFunctions").GetMethod("MakeDecimal")
            (fun tm ->
               match tm with
               | Call(None, minfo2, args)
        #if FX_NO_REFLECTION_METADATA_TOKENS
                  when ( // if metadata tokens are not available we'll rely only on equality of method references
        #else
                  when (minfo1.MetadataToken = minfo2.MetadataToken &&
        #endif
                        minfo1 = minfo2) ->
                   Some(args)
               | _ -> None)
 
        let (|NaN|_|) =
            let operatorsType = convTypeToTgt (typedefof<list<_>>.Assembly.GetType("Microsoft.FSharp.Core.Operators"))
            let minfo1 = operatorsType.GetProperty("NaN").GetGetMethod()
            (fun e -> 
                match e with
                | Call(None, minfo2, [])
        #if FX_NO_REFLECTION_METADATA_TOKENS
                  when ( // if metadata tokens are not available we'll rely only on equality of method references
        #else
                  when (minfo1.MetadataToken = minfo2.MetadataToken &&
        #endif
                        minfo1 = minfo2) ->
                    Some()
                | _ -> None)
            
        let (|NaNSingle|_|) =
            let operatorsType = convTypeToTgt (typedefof<list<_>>.Assembly.GetType("Microsoft.FSharp.Core.Operators"))
            let minfo1 = operatorsType.GetProperty("NaNSingle").GetGetMethod()
            (fun e -> 
                match e with
                | Call(None, minfo2, [])
        #if FX_NO_REFLECTION_METADATA_TOKENS
                  when ( // if metadata tokens are not available we'll rely only on equality of method references
        #else
                  when (minfo1.MetadataToken = minfo2.MetadataToken &&
        #endif
                        minfo1 = minfo2) ->
                    Some()
                | _ -> None)
            
        let (|TypeOf|_|) = (|SpecificCall|_|) <@ typeof<obj> @>

        let (|LessThan|_|) = (|SpecificCall|_|) <@ (<) @>
        let (|GreaterThan|_|) = (|SpecificCall|_|) <@ (>) @>
        let (|LessThanOrEqual|_|) = (|SpecificCall|_|) <@ (<=) @>
        let (|GreaterThanOrEqual|_|) = (|SpecificCall|_|) <@ (>=) @>
        let (|Equals|_|) = (|SpecificCall|_|) <@ (=) @>
        let (|NotEquals|_|) = (|SpecificCall|_|) <@ (<>) @>
        let (|Multiply|_|) = (|SpecificCall|_|) <@ (*) @>
        let (|Addition|_|) = (|SpecificCall|_|) <@ (+) @>
        let (|Subtraction|_|) = (|SpecificCall|_|) <@ (-) @>
        let (|UnaryNegation|_|) = (|SpecificCall|_|) <@ (~-) @>
        let (|Division|_|) = (|SpecificCall|_|) <@ (/) @>
        let (|UnaryPlus|_|) = (|SpecificCall|_|) <@ (~+) @>
        let (|Modulus|_|) = (|SpecificCall|_|) <@ (%) @>
        let (|LeftShift|_|) = (|SpecificCall|_|) <@ (<<<) @>
        let (|RightShift|_|) = (|SpecificCall|_|) <@ (>>>) @>
        let (|And|_|) = (|SpecificCall|_|) <@ (&&&) @>
        let (|Or|_|) = (|SpecificCall|_|) <@ (|||) @>
        let (|Xor|_|) = (|SpecificCall|_|) <@ (^^^) @>
        let (|Not|_|) = (|SpecificCall|_|) <@ (~~~) @>
        //let (|Compare|_|) = (|SpecificCall|_|) <@ compare @>
        let (|Max|_|) = (|SpecificCall|_|) <@ max @>
        let (|Min|_|) = (|SpecificCall|_|) <@ min @>
        //let (|Hash|_|) = (|SpecificCall|_|) <@ hash @>
        let (|CallByte|_|) = (|SpecificCall|_|) <@ byte @>
        let (|CallSByte|_|) = (|SpecificCall|_|) <@ sbyte @>
        let (|CallUInt16|_|) = (|SpecificCall|_|) <@ uint16 @>
        let (|CallInt16|_|) = (|SpecificCall|_|) <@ int16 @>
        let (|CallUInt32|_|) = (|SpecificCall|_|) <@ uint32 @>
        let (|CallInt|_|) = (|SpecificCall|_|) <@ int @>
        let (|CallInt32|_|) = (|SpecificCall|_|) <@ int32 @>
        let (|CallUInt64|_|) = (|SpecificCall|_|) <@ uint64 @>
        let (|CallInt64|_|) = (|SpecificCall|_|) <@ int64 @>
        let (|CallSingle|_|) = (|SpecificCall|_|) <@ single @>
        let (|CallFloat32|_|) = (|SpecificCall|_|) <@ float32 @>
        let (|CallDouble|_|) = (|SpecificCall|_|) <@ double @>
        let (|CallFloat|_|) = (|SpecificCall|_|) <@ float @>
        let (|CallDecimal|_|) = (|SpecificCall|_|) <@ decimal @>
        let (|CallChar|_|) = (|SpecificCall|_|) <@ char @>
        let (|Ignore|_|) = (|SpecificCall|_|) <@ ignore @>
        let (|GetArray|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.GetArray @>
        let (|GetArray2D|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.GetArray2D @>
        let (|GetArray3D|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.GetArray3D @>
        let (|GetArray4D|_|) = (|SpecificCall|_|) <@ LanguagePrimitives.IntrinsicFunctions.GetArray4D @>
        
        let (|Abs|_|) = (|SpecificCall|_|) <@ abs @>
        let (|Acos|_|) = (|SpecificCall|_|) <@ acos @>
        let (|Asin|_|) = (|SpecificCall|_|) <@ asin @>
        let (|Atan|_|) = (|SpecificCall|_|) <@ atan @>
        let (|Atan2|_|) = (|SpecificCall|_|) <@ atan2 @>
        let (|Ceil|_|) = (|SpecificCall|_|) <@ ceil @>
        let (|Exp|_|) = (|SpecificCall|_|) <@ exp @>
        let (|Floor|_|) = (|SpecificCall|_|) <@ floor @>
        let (|Truncate|_|) = (|SpecificCall|_|) <@ truncate @>
        let (|Round|_|) = (|SpecificCall|_|) <@ round @>
        let (|Sign|_|) = (|SpecificCall|_|) <@ sign @>
        let (|Log|_|) = (|SpecificCall|_|) <@ log @>
        let (|Log10|_|) = (|SpecificCall|_|) <@ log10 @>
        let (|Sqrt|_|) = (|SpecificCall|_|) <@ sqrt @>
        let (|Cos|_|) = (|SpecificCall|_|) <@ cos @>
        let (|Cosh|_|) = (|SpecificCall|_|) <@ cosh @>
        let (|Sin|_|) = (|SpecificCall|_|) <@ sin @>
        let (|Sinh|_|) = (|SpecificCall|_|) <@ sinh @>
        let (|Tan|_|) = (|SpecificCall|_|) <@ tan @>
        let (|Tanh|_|) = (|SpecificCall|_|) <@ tanh @>
        //let (|Range|_|) = (|SpecificCall|_|) <@ (..) @>
        //let (|RangeStep|_|) = (|SpecificCall|_|) <@ (.. ..) @>
        let (|Pow|_|) = (|SpecificCall|_|) <@ ( ** ) @>
        //let (|Pown|_|) = (|SpecificCall|_|) <@ pown @>
    
        let mathOp t1 name = 
            match t1 with 
            | Double ->
                let m = mathTypeTgt.GetMethod(name, [|t1|])
                ilg.Emit(I_call(Normalcall, transMeth m, None))
            | Single ->
                ilg.Emit(I_conv DT_R8)
                let m = mathTypeTgt.GetMethod(name, [|convTypeToTgt typeof<double>|])
                ilg.Emit(I_call(Normalcall, transMeth m, None))
                ilg.Emit(I_conv DT_R4)
            | StaticMethod name [|t1|] m -> 
                ilg.Emit(I_call(Normalcall, transMeth m, None))
            | _ -> failwithf "%s not supported for type %s" name t1.Name

        let lessThan (a1 : Expr) (a2 : Expr) = 
            match <@@ (<) @@> with 
            | DerivedPatterns.Lambdas(vars, Call(None, meth, _)) -> 
                let targetType = convTypeToTgt meth.DeclaringType
                let m = targetType.GetMethod(meth.Name, bindAll).MakeGenericMethod(a1.Type)
                Expr.Call(m, [a1; a2])
            | _ -> failwith "Unreachable"
            
        let isEmpty s = (s = ExpectedStackState.Empty)
        let isAddress s = (s = ExpectedStackState.Address)
        let rec emitLambda(callSiteIlg: ILGenerator, v: Var, body: Expr, freeVars: seq<Var>, lambdaLocals: Dictionary<_, ILLocalBuilder>, parameters) =
            let lambda: ILTypeBuilder = assemblyMainModule.DefineType(UNone, genUniqueTypeName(), TypeAttributes.Class)

            let fsharpFuncType = convTypeToTgt (typedefof<FSharpFunc<_, _>>)
            let voidType = convTypeToTgt typeof<System.Void>
            let rec lambdaType (t : Type) = 
                if t.IsGenericType then 
                    let args = t.GetGenericArguments()
                    let gdef = t.GetGenericTypeDefinition()
                    if args.Length = 2 && gdef.FullName = fsharpFuncType.FullName && args.[1] = voidType then 
                        gdef.MakeGenericType(lambdaType args.[0], typeof<unit>)
                    else
                        gdef.MakeGenericType(args |> Array.map lambdaType)
                else
                    t

            let baseType = convTypeToTgt (lambdaType (typedefof<FSharpFunc<_, _>>.MakeGenericType(v.Type, body.Type)))
            lambda.SetParent(transType baseType)
            let baseCtor = baseType.GetConstructor(bindAll, null, [| |], null)
            if isNull baseCtor then failwithf "Couldn't find default constructor on %O" baseType
            let ctor = lambda.DefineDefaultConstructor(MethodAttributes.Public, transCtorSpec baseCtor)
            let decl = baseType.GetMethod "Invoke"
            let paramTypes = [| for p in decl.GetParameters() -> transType p.ParameterType |]
            let retType = transType decl.ReturnType
            let invoke = lambda.DefineMethod("Invoke", MethodAttributes.Virtual ||| MethodAttributes.Final ||| MethodAttributes.Public, retType, paramTypes)
            lambda.DefineMethodOverride
                { Overrides = OverridesSpec(transMethRef decl, transType decl.DeclaringType)
                  OverrideBy = invoke.FormalMethodSpec }

            // promote free vars to fields
            let fields = ResizeArray()
            for v in freeVars do
                let f = lambda.DefineField(v.Name, transType v.Type, FieldAttributes.Assembly)
                //Debug.Assert (v.Name <> "formatValue")
                fields.Add(v, f)

            let lambdaLocals = Dictionary()

            let ilg = invoke.GetILGenerator()
            for (v, f) in fields do
                let l = ilg.DeclareLocal(transType  v.Type)
                ilg.Emit(I_ldarg 0)
                ilg.Emit(I_ldfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, f.FormalFieldSpec))
                ilg.Emit(I_stloc l.LocalIndex)
                lambdaLocals.[v] <- l

            let unitType = transType (convTypeToTgt (typeof<unit>))
            let expectedState = if (retType = ILType.Void || retType.QualifiedName = unitType.QualifiedName) then ExpectedStackState.Empty else ExpectedStackState.Value
            let lambadParamVars = [| Var("this", typeof<obj>); v|]
            let codeGen = CodeGenerator(assemblyMainModule, genUniqueTypeName, implicitCtorArgsAsFields, convTypeToTgt, transType, transFieldSpec, transMeth, transMethRef, transCtorSpec, ilg, lambdaLocals, lambadParamVars)
            codeGen.EmitExpr (expectedState, body)
            if retType.QualifiedName = unitType.QualifiedName then 
                ilg.Emit(I_ldnull)
            ilg.Emit(I_ret)

            callSiteIlg.Emit(I_newobj (ctor.FormalMethodSpec, None))
            for (v, f) in fields do
                callSiteIlg.Emit(I_dup)
                match localsMap.TryGetValue v with
                | true, loc ->
                    callSiteIlg.Emit(I_ldloc loc.LocalIndex)
                | false, _ ->
                    let index = parameters |> Array.findIndex ((=) v)
                    callSiteIlg.Emit(I_ldarg index)
                callSiteIlg.Emit(I_stfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, f.FormalFieldSpec))

        and emitExpr expectedState expr =
            let pop () = ilg.Emit(I_pop)
            let popIfEmptyExpected s = if isEmpty s then pop()
            let emitConvIfNecessary t1 =
                if t1 = typeof<int16> then
                    ilg.Emit(I_conv DT_I2)
                elif t1 = typeof<uint16> then
                    ilg.Emit(I_conv DT_U2)
                elif t1 = typeof<sbyte> then
                    ilg.Emit(I_conv DT_I1)
                elif t1 = typeof<byte> then
                    ilg.Emit(I_conv DT_U1)
            /// emits given expression to corresponding IL
            match expr with
            | ForIntegerRangeLoop(loopVar, first, last, body) ->
                // for(loopVar = first..last) body
                let lb =
                    match localsMap.TryGetValue loopVar with
                    | true, lb -> lb
                    | false, _ ->
                        let lb = ilg.DeclareLocal(transType loopVar.Type)
                        localsMap.Add(loopVar, lb)
                        lb

                // loopVar = first
                emitExpr ExpectedStackState.Value first
                ilg.Emit(I_stloc lb.LocalIndex)

                let before = ilg.DefineLabel()
                let after = ilg.DefineLabel()

                ilg.MarkLabel before
                ilg.Emit(I_ldloc lb.LocalIndex)

                emitExpr ExpectedStackState.Value last
                ilg.Emit(I_brcmp (I_bgt, after))

                emitExpr ExpectedStackState.Empty body

                // loopVar++
                ilg.Emit(I_ldloc lb.LocalIndex)
                ilg.Emit(mk_ldc 1)
                ilg.Emit(I_add)
                ilg.Emit(I_stloc lb.LocalIndex)

                ilg.Emit(I_br before)
                ilg.MarkLabel(after)

            | NewArray(elementTy, elements) ->
                ilg.Emit(mk_ldc  (List.length elements))
                ilg.Emit(I_newarr (ILArrayShape.SingleDimensional, transType elementTy))

                elements
                |> List.iteri (fun i el ->
                    ilg.Emit(I_dup)
                    ilg.Emit(mk_ldc i)
                    emitExpr ExpectedStackState.Value el
                    ilg.Emit(I_stelem_any (ILArrayShape.SingleDimensional, transType elementTy)))

                popIfEmptyExpected expectedState

            | WhileLoop(cond, body) ->
                let before = ilg.DefineLabel()
                let after = ilg.DefineLabel()

                ilg.MarkLabel before
                emitExpr ExpectedStackState.Value cond
                ilg.Emit(I_brcmp (I_brfalse, after))
                emitExpr ExpectedStackState.Empty body
                ilg.Emit(I_br before)

                ilg.MarkLabel after

            | Var v ->
                if isEmpty expectedState then () else

                // Try to interpret this as a method parameter
                let methIdx = parameterVars |> Array.tryFindIndex (fun p -> p = v)
                match methIdx with
                | Some idx ->
                    ilg.Emit((if isAddress expectedState then I_ldarga idx else I_ldarg idx) )
                | None ->

                // Try to interpret this as an implicit field in a class
                let implicitCtorArgFieldOpt = implicitCtorArgsAsFields |> List.tryFind (fun f -> f.Name = v.Name)
                match implicitCtorArgFieldOpt with
                | Some ctorArgField ->
                    ilg.Emit(I_ldarg 0)
                    ilg.Emit(I_ldfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, ctorArgField.FormalFieldSpec))
                | None ->

                // Try to interpret this as a local
                match localsMap.TryGetValue v with
                | true, localBuilder ->
                    let idx = localBuilder.LocalIndex
                    ilg.Emit(if isAddress expectedState  then I_ldloca idx else I_ldloc idx)
                | false, _ ->
                    failwith "unknown parameter/field"

            | Coerce (arg, ty) ->
                // castClass may lead to observable side-effects - InvalidCastException
                emitExpr ExpectedStackState.Value arg
                let argTy = arg.Type
                let targetTy = ty
                if argTy.IsValueType && not targetTy.IsValueType then
                    ilg.Emit(I_box (transType argTy))
                elif not argTy.IsValueType && targetTy.IsValueType then
                    ilg.Emit(I_unbox_any (transType targetTy))
                else
                    ilg.Emit(I_castclass (transType  targetTy))

                popIfEmptyExpected expectedState
               
            | TypeOf(None, [t1], []) -> emitExpr expectedState (Expr.Value(t1)) 

            | NaN -> emitExpr ExpectedStackState.Value <@@ Double.NaN @@>

            | NaNSingle -> emitExpr ExpectedStackState.Value <@@ Single.NaN @@>

            | MakeDecimal(args) -> 
                emitExpr ExpectedStackState.Value (Expr.NewObjectUnchecked(decimalConstructor(), args))

            | LessThan(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Double | Single
                | Int16 | Int32 | Int64 -> ilg.Emit(I_clt)
                | Byte
                | UInt16 | UInt32 | UInt64 -> ilg.Emit(I_clt_un)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("CompareOrdinal", [|t1; t1|]) |> transMeth, None))
                    emitExpr ExpectedStackState.Value <@@ 0 @@>
                    ilg.Emit(I_clt)
                | StaticMethod "op_LessThan" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (<) not supported for type %s" t1.Name

           
            | GreaterThan(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Double | Single
                | Int16 | Int32 | Int64 -> ilg.Emit(I_cgt)
                | Byte
                | UInt16 | UInt32 | UInt64 -> ilg.Emit(I_cgt_un)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("CompareOrdinal", [|t1; t1|]) |> transMeth, None))
                    emitExpr ExpectedStackState.Value <@@ 0 @@>
                    ilg.Emit(I_cgt)
                | StaticMethod "op_GreaterThan" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (>) not supported for type %s" t1.Name
           
           
            | LessThanOrEqual(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Int16 | Int32 | Int64 -> 
                    ilg.Emit(I_cgt)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | Byte
                | Double | Single
                | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_cgt_un)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("CompareOrdinal", [|t1; t1|]) |> transMeth, None))
                    emitExpr ExpectedStackState.Value <@@ 0 @@>
                    ilg.Emit(I_cgt)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | StaticMethod "op_LessThanOrEqual" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (<=) not supported for type %s" t1.Name
           

            | GreaterThanOrEqual(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Int16 | Int32 | Int64 -> 
                    ilg.Emit(I_clt)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | Byte
                | Double | Single
                | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_clt_un)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("CompareOrdinal", [|t1; t1|]) |> transMeth, None))
                    emitExpr ExpectedStackState.Value <@@ 0 @@>
                    ilg.Emit(I_clt)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | StaticMethod "op_GreaterThanOrEqual" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (>=) not supported for type %s" t1.Name
           
            | Equals(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Double | Single
                | Int16 | Int32 | Int64 
                | Byte
                | UInt16 | UInt32 | UInt64 -> ilg.Emit(I_ceq)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("Equals", [|t1; t1|]) |> transMeth, None))
                | StaticMethod "op_Equality" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (=) not supported for type %s" t1.Name
           
            | NotEquals(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Bool | SByte | Char
                | Double | Single
                | Int16 | Int32 | Int64 
                | Byte
                | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_ceq)
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | String ->
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("Equals", [|t1; t1|]) |> transMeth, None))
                    emitExpr ExpectedStackState.Value <@@ false @@>
                    ilg.Emit(I_ceq)
                | StaticMethod "op_Inequality" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (<>) not supported for type %s" t1.Name

            | Multiply(None, [t1; t2; _], [a1; a2]) ->
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | SByte | Byte
                | Int16 | Int32 | Int64
                | UInt16 | UInt32 | UInt64
                | Double | Single ->
                    ilg.Emit(I_mul)
                | StaticMethod "op_Multiply" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (*) not supported for type %s" t1.Name
                emitConvIfNecessary t1
           
            | Addition(None, [t1; t2; _], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | SByte | Byte
                | Int16 | Int32 | Int64
                | UInt16 | UInt32 | UInt64
                | Double | Single 
                | Char ->
                    ilg.Emit(I_add)
                | String -> 
                    ilg.Emit(I_call(Normalcall, (convTypeToTgt typeof<System.String>).GetMethod("Concat", [|t1; t1|]) |> transMeth, None))
                | StaticMethod "op_Addition" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (+) not supported for type %s" t1.Name
                emitConvIfNecessary t1
            
            | Subtraction(None, [t1; t2; _], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | SByte | Byte
                | Int16 | Int32 | Int64
                | UInt16 | UInt32 | UInt64
                | Double | Single  ->
                    ilg.Emit(I_sub)
                | StaticMethod "op_Subtraction" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (-) not supported for type %s" t1.Name
                emitConvIfNecessary t1
            
            | UnaryNegation(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | SByte 
                | Int16 | Int32 | Int64
                | Double | Single  ->
                    ilg.Emit(I_neg)
                | StaticMethod "op_UnaryNegation" [|t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (~-) not supported for type %s" t1.Name
                
            | Division(None, [t1; t2; _], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Byte | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_div_un)
                | SByte | Int16 | Int32 | Int64
                | Double | Single  ->
                    ilg.Emit(I_div)
                | StaticMethod "op_Division" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (/) not supported for type %s" t1.Name
                emitConvIfNecessary t1

            | UnaryPlus(None, [t1], [a1]) -> 
                match t1.GetMethod("op_UnaryPlus", [|t1|]) with 
                | null ->
                    emitExpr expectedState a1
                | m -> 
                    emitExpr ExpectedStackState.Value a1
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
            
            | Modulus(None, [t1; t2; _], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Byte | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_rem_un)
                | SByte | Int16 | Int32 | Int64
                | Double | Single  ->
                    ilg.Emit(I_rem)
                | StaticMethod "op_Modulus" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (%%) not supported for type %s" t1.Name
                emitConvIfNecessary t1
                
            | LeftShift(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                let maskShift (x : int) =
                    match a2 with 
                    | Patterns.Value(:? int as v , _) -> 
                        emitExpr ExpectedStackState.Value (Expr.Value (v &&& x))
                    | _ -> 
                        emitExpr ExpectedStackState.Value a2
                        emitExpr ExpectedStackState.Value (Expr.Value x)
                        ilg.Emit(I_and)
                    ilg.Emit(I_shl) 
                match t1 with 
                | Int32 | UInt32 -> maskShift 31
                | Int64 | UInt64 -> maskShift 63
                | Int16 | UInt16 -> maskShift 15
                | SByte | Byte -> maskShift 7
                | StaticMethod "op_LeftShift" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (<<<) not supported for type %s" t1.Name
                emitConvIfNecessary t1

            | RightShift(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                let maskShift (x : int) =
                    match a2 with 
                    | Patterns.Value(:? int as v , _) -> 
                        emitExpr ExpectedStackState.Value (Expr.Value (v &&& x))
                    | _ -> 
                        emitExpr ExpectedStackState.Value a2
                        emitExpr ExpectedStackState.Value (Expr.Value x)
                        ilg.Emit(I_and)
                    ilg.Emit(I_shr) 
                match t1 with 
                | Int32 | UInt32 -> maskShift 31
                | Int64 | UInt64 -> maskShift 63
                | Int16 | UInt16 -> maskShift 15
                | SByte | Byte -> maskShift 7
                | StaticMethod "op_RightShift" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (>>>) not supported for type %s" t1.Name
                emitConvIfNecessary t1

            | And(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_and)
                | StaticMethod "op_And" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (&&&) not supported for type %s" t1.Name
                        
            | Or(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_or)
                | StaticMethod "op_Or" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (|||) not supported for type %s" t1.Name
                        
            | Xor(None, [t1], [a1; a2]) -> 
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_xor)
                | StaticMethod "op_Xor" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (^^^) not supported for type %s" t1.Name
                
            | Not(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_not)
                | StaticMethod "op_Not" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator (~~~) not supported for type %s" t1.Name

            | Max(None, [t1], [a1; a2]) -> 
                match t1 with 
                | Double ->
                    emitExpr ExpectedStackState.Value a1
                    emitExpr ExpectedStackState.Value a2
                    let m = mathTypeTgt.GetMethod("Max", [|t1; t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | Single ->
                    emitExpr ExpectedStackState.Value a1
                    emitExpr ExpectedStackState.Value a2
                    ilg.Emit(I_conv DT_R8)
                    let t = convTypeToTgt typeof<double>
                    let m = mathTypeTgt.GetMethod("Max", [|t;t|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv DT_R4)
                | _ -> 
                    match a1, a2 with 
                    | (Var _ | Value _), (Var _ | Value _) -> 
                        Expr.IfThenElseUnchecked(lessThan a1 a2, a2, a1)
                        |> emitExpr ExpectedStackState.Value
                    | (Var _ | Value _), _ -> 
                        let e2 = Var("e2", a2.Type)
                        Expr.Let(e2, a2, 
                            Expr.IfThenElseUnchecked(lessThan a1 (Expr.Var e2), Expr.Var e2, a1))
                        |> emitExpr ExpectedStackState.Value
                    | _, (Var _ | Value _) -> 
                        let e1 = Var("e1", a1.Type)
                        Expr.Let(e1, a1, 
                            Expr.IfThenElseUnchecked((lessThan (Expr.Var e1) a2, a2, (Expr.Var e1))))
                        |> emitExpr ExpectedStackState.Value
                    | _ -> 
                        let e1 = Var("e1", a1.Type)
                        let e2 = Var("e2", a2.Type)
                        Expr.Let(e1, a1, 
                            Expr.Let(e2, a2, 
                                Expr.IfThenElseUnchecked(lessThan (Expr.Var e1) (Expr.Var e2), Expr.Var e2, Expr.Var e1)))
                        |> emitExpr ExpectedStackState.Value
            
            | Min(None, [t1], [a1; a2]) -> 
                match t1 with 
                | Double ->
                    emitExpr ExpectedStackState.Value a1
                    emitExpr ExpectedStackState.Value a2
                    let m = mathTypeTgt.GetMethod("Min", [|t1; t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | Single ->
                    emitExpr ExpectedStackState.Value a1
                    emitExpr ExpectedStackState.Value a2
                    ilg.Emit(I_conv DT_R8)
                    let t = convTypeToTgt typeof<double>
                    let m = mathTypeTgt.GetMethod("Min", [|t;t|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv DT_R4)
                | _ -> 
                    match a1, a2 with 
                    | (Var _ | Value _), (Var _ | Value _) -> 
                        Expr.IfThenElseUnchecked(lessThan a1 a2, a1, a2)
                        |> emitExpr ExpectedStackState.Value
                    | (Var _ | Value _), _ -> 
                        let e2 = Var("e2", a2.Type)
                        Expr.Let(e2, a2, 
                            Expr.IfThenElseUnchecked(lessThan a1 (Expr.Var e2), a1, Expr.Var e2))
                        |> emitExpr ExpectedStackState.Value
                    | _, (Var _ | Value _) -> 
                        let e1 = Var("e1", a1.Type)
                        Expr.Let(e1, a1, 
                            Expr.IfThenElseUnchecked((lessThan (Expr.Var e1) a2, Expr.Var e1, a2)))
                        |> emitExpr ExpectedStackState.Value
                    | _ -> 
                        let e1 = Var("e1", a1.Type)
                        let e2 = Var("e2", a2.Type)
                        Expr.Let(e1, a1, 
                            Expr.Let(e2, a2, 
                                Expr.IfThenElseUnchecked(lessThan (Expr.Var e1) (Expr.Var e2), Expr.Var e1, Expr.Var e2)))
                        |> emitExpr ExpectedStackState.Value

            | CallByte(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_conv DT_U1) 
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseUInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv_ovf DT_U1) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'byte' not supported for type %s" t1.Name

            | CallSByte(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_conv DT_I1) 
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv_ovf DT_I1) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'sbyte' not supported for type %s" t1.Name

            | CallUInt16(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_conv DT_U2) 
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseUInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv_ovf DT_U2) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'uint16' not supported for type %s" t1.Name

            | CallInt16(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | Int32 | UInt32 
                | Int64 | UInt64 
                | Int16 | UInt16 
                | SByte | Byte -> ilg.Emit(I_conv DT_I2) 
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv_ovf DT_I2) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'int16' not supported for type %s" t1.Name

            | CallUInt32(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | UInt16 | UInt32 
                | Int64 | UInt64 
                | Byte -> ilg.Emit(I_conv DT_U4) 
                | Int32 | Int16 | SByte -> ()
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseUInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'uint32' not supported for type %s" t1.Name

            | CallInt(None, [t1], [a1])
            | CallInt32(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | UInt16 
                | Int64 | UInt64 
                | Byte -> ilg.Emit(I_conv DT_I4) 
                | UInt32 | Int32 | Int16 | SByte -> ()
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseInt32")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'int32' not supported for type %s" t1.Name

            | CallUInt64(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Char
                | Double | Single
                | UInt16 | UInt32 
                | Byte -> ilg.Emit(I_conv DT_U8) 
                | SByte | Int32 | Int16 -> ilg.Emit(I_conv DT_I8) 
                | Int64 | UInt64 -> ()
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseUInt64")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'uint64' not supported for type %s" t1.Name

            | CallInt64(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Double | Single
                | Int64 | Int32 | Int16 
                | SByte -> ilg.Emit(I_conv DT_I8) 
                | Char | Byte | UInt16 | UInt32 -> 
                    ilg.Emit(I_conv DT_U8) 
                | UInt64 -> ()
                | String -> 
                    let m = languagePrimitivesType().GetMethod("ParseInt64")
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Operator 'int64' not supported for type %s" t1.Name

            | CallSingle(None, [t1], [a1])
            | CallFloat32(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Double | Single
                | Int64 | Int32 | Int16 
                | SByte -> ilg.Emit(I_conv DT_R4) 
                | Char | Byte | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_conv DT_R) 
                    ilg.Emit(I_conv DT_R4) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> 
                    match expr.Type with 
                    | StaticMethodWithReturnType "Parse" [|t1|] expr.Type m -> 
                        ilg.Emit(I_call(Normalcall, transMeth m, None))
                    | _ -> failwithf "Operator 'float32' not supported for type %s" t1.Name

            | CallDouble(None, [t1], [a1])
            | CallFloat(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Double | Single
                | Int64 | Int32 | Int16 
                | SByte -> ilg.Emit(I_conv DT_R8) 
                | Char | Byte | UInt16 | UInt32 | UInt64 -> 
                    ilg.Emit(I_conv DT_R) 
                    ilg.Emit(I_conv DT_R8) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> 
                    match expr.Type with 
                    | StaticMethodWithReturnType "Parse" [|t1|] expr.Type m -> 
                        ilg.Emit(I_call(Normalcall, transMeth m, None))
                    | _ -> failwithf "Operator 'float' not supported for type %s" t1.Name
            
            | CallDecimal(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                let rtTgt = decimalTypeTgt
                if t1 = stringTypeTgt then 
                    let m = rtTgt.GetMethod("Parse", [|stringTypeTgt|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None)) 
                else
                    match convertTypeTgt.GetMethod("ToDecimal", [|t1|]) with 
                    | null -> 
                        let m = 
                            t1.GetMethods(BindingFlags.Static ||| BindingFlags.Public) 
                            |> Array.tryFind 
                                (fun x -> 
                                    x.Name = "op_Explicit"  
                                        && x.ReturnType = rtTgt 
                                        && (x.GetParameters() |> Array.map (fun i -> i.ParameterType)) = [|t1|])
                        match m with 
                        | None -> 
                            failwithf "decimal operator on %s not supported" (t1.Name)
                        | Some m -> 
                            ilg.Emit(I_call(Normalcall, transMeth m, None))
                    | toDecimal -> ilg.Emit(I_call(Normalcall, transMeth toDecimal, None)) 
                        
            | CallChar(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Double | Single
                | Int64 | Int32 | Int16 
                | Char | Byte | UInt16 | UInt32 | UInt64
                | SByte -> ilg.Emit(I_conv DT_U2) 
                | StaticMethodWithReturnType "op_Explicit" [|t1|] expr.Type m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> 
                    match expr.Type with 
                    | StaticMethodWithReturnType "Parse" [|t1|] expr.Type m -> 
                        ilg.Emit(I_call(Normalcall, transMeth m, None))
                    | _ -> failwithf "Operator 'char' not supported for type %s" t1.Name
            
            | Ignore(None, [t1], [a1]) -> emitExpr expectedState a1

            | GetArray(None, [ty], [arr; index]) ->
                // observable side-effect - IndexOutOfRangeException
                emitExpr ExpectedStackState.Value arr
                emitExpr ExpectedStackState.Value index
                if isAddress expectedState then
                    ilg.Emit(I_ldelema (ILReadonly.ReadonlyAddress, ILArrayShape.SingleDimensional, transType ty))
                else
                    ilg.Emit(I_ldelem_any (ILArrayShape.SingleDimensional, transType ty))

                popIfEmptyExpected expectedState

            | GetArray2D(None, _ty, arr::indices)
            | GetArray3D(None, _ty, arr::indices)
            | GetArray4D(None, _ty, arr::indices) ->

                let meth =
                    let name = if isAddress expectedState then "Address" else "Get"
                    arr.Type.GetMethod(name)

                // observable side-effect - IndexOutOfRangeException
                emitExpr ExpectedStackState.Value arr
                for index in indices do
                    emitExpr ExpectedStackState.Value index

                //if isAddress expectedState then
                //    ilg.Emit(I_readonly)

                ilg.Emit(mkNormalCall (transMeth meth))

                popIfEmptyExpected expectedState

            | Abs(None, [t1], [a1]) -> 
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Int32 | Double | Single | Int64 | Int16 | SByte | Decimal ->
                    let m = mathTypeTgt.GetMethod("Abs", [|t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | StaticMethod "Abs" [|t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Abs not supported for type %s" t1.Name

            | Acos(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Acos"

            | Asin(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Asin"

            | Atan(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Atan"

            | Atan2(None, [t1;t2], [a1; a2]) ->
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Double ->
                    let m = mathTypeTgt.GetMethod("Atan2", [|t1; t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | Single ->
                    ilg.Emit(I_conv DT_R8)
                    let t = convTypeToTgt typeof<double>
                    let m = mathTypeTgt.GetMethod("Atan2", [|t;t|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv DT_R4)
                | StaticMethod "Atan2" [|t1; t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Atan2 not supported for type %s" t1.Name

            | Ceil(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Ceiling"

            | Exp(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Exp"

            | Floor(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Floor"

            | Truncate(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Truncate"

            | Round(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Round"

            | Sign(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                match t1 with 
                | Int32 | Double | Single | Int64 | Int16 | SByte | Decimal ->
                    let m = mathTypeTgt.GetMethod("Sign", [|t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | Single ->
                    ilg.Emit(I_conv DT_R8)
                    let m = mathTypeTgt.GetMethod("Sign", [|convTypeToTgt typeof<double>|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv DT_R4)
                | StaticMethod "Sign" [|t1|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Sign not supported for type %s" t1.Name

            | Log(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Log"

            | Log10(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Log10"

            | Sqrt(None, [t1; t2], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Sqrt"

            | Cos(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Cos"

            | Cosh(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Cosh"

            | Sin(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Sin"

            | Sinh(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Sinh"

            | Tan(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Tan"

            | Tanh(None, [t1], [a1]) ->
                emitExpr ExpectedStackState.Value a1
                mathOp t1 "Tanh"

            | Pow(None, [t1; t2], [a1; a2]) ->
                emitExpr ExpectedStackState.Value a1
                emitExpr ExpectedStackState.Value a2
                match t1 with 
                | Double ->
                    let m = mathTypeTgt.GetMethod("Pow", [|t1; t1|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | Single ->
                    ilg.Emit(I_conv DT_R8)
                    let t = convTypeToTgt typeof<double>
                    let m = mathTypeTgt.GetMethod("Pow", [|t;t|])
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                    ilg.Emit(I_conv DT_R4)
                | StaticMethod "Pow" [|t1; t2|] m -> 
                    ilg.Emit(I_call(Normalcall, transMeth m, None))
                | _ -> failwithf "Pow not supported for type %s" t1.Name

            | FieldGet (None, field) when field.DeclaringType.IsEnum ->
                if expectedState <> ExpectedStackState.Empty then
                    emitExpr expectedState (Expr.Value(field.GetRawConstantValue(), field.FieldType.GetEnumUnderlyingType()))

            | FieldGet (objOpt, field) ->
                objOpt |> Option.iter (fun e ->
                    let s = if e.Type.IsValueType then ExpectedStackState.Address else ExpectedStackState.Value
                    emitExpr s e)
                if field.IsStatic then
                    ilg.Emit(I_ldsfld (ILVolatility.Nonvolatile, transFieldSpec field))
                else
                    ilg.Emit(I_ldfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, transFieldSpec field))

            | FieldSet (objOpt, field, v) ->
                objOpt |> Option.iter (fun e ->
                    let s = if e.Type.IsValueType then ExpectedStackState.Address else ExpectedStackState.Value
                    emitExpr s e)
                emitExpr ExpectedStackState.Value v
                if field.IsStatic then
                    ilg.Emit(I_stsfld (ILVolatility.Nonvolatile, transFieldSpec field))
                else
                    ilg.Emit(I_stfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, transFieldSpec field))

            | Call (objOpt, meth, args) ->
                objOpt |> Option.iter (fun e ->
                    let s = if e.Type.IsValueType then ExpectedStackState.Address else ExpectedStackState.Value
                    emitExpr s e)

                for pe in args do
                    emitExpr ExpectedStackState.Value pe

                // Handle the case where this is a generic method instantiated at a type being compiled
                let mappedMeth = transMeth meth

                match objOpt with
                | Some obj when meth.IsAbstract || meth.IsVirtual  ->
                    if obj.Type.IsValueType then 
                        ilg.Emit(I_callconstraint (Normalcall, transType obj.Type, mappedMeth, None))
                    else 
                        ilg.Emit(I_callvirt (Normalcall, mappedMeth, None))
                | _ ->
                    ilg.Emit(mkNormalCall mappedMeth)

                let returnTypeIsVoid = (mappedMeth.FormalReturnType = ILType.Void)
                match returnTypeIsVoid, (isEmpty expectedState) with
                | false, true ->
                        // method produced something, but we don't need it
                        pop()
                | true, false when expr.Type = typeof<unit> ->
                        // if we need result and method produce void and result should be unit - push null as unit value on stack
                        ilg.Emit(I_ldnull)
                | _ -> ()

            | NewObject (ctor, args) ->
                for pe in args do
                    emitExpr ExpectedStackState.Value pe
                ilg.Emit(I_newobj (transCtorSpec ctor, None))

                popIfEmptyExpected expectedState
                
            | DefaultValue (t) ->
                let ilt = transType t
                let lb = ilg.DeclareLocal ilt
                ilg.Emit(I_ldloca lb.LocalIndex)
                ilg.Emit(I_initobj ilt)
                ilg.Emit(I_ldloc lb.LocalIndex)

            | Value (obj, _ty) ->
                let rec emitC (v:obj) =
                    match v with
                    | :? string as x -> ilg.Emit(I_ldstr x)
                    | :? int8 as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? uint8 as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? int16 as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? uint16 as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? int32 as x -> ilg.Emit(mk_ldc x)
                    | :? uint32 as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? int64 as x -> ilg.Emit(mk_ldc_i8 x)
                    | :? uint64 as x -> ilg.Emit(mk_ldc_i8 (int64 x))
                    | :? char as x -> ilg.Emit(mk_ldc (int32 x))
                    | :? bool as x -> ilg.Emit(mk_ldc (if x then 1 else 0))
                    | :? float32 as x -> ilg.Emit(I_ldc (DT_R4, ILConst.R4 x))
                    | :? float as x -> ilg.Emit(I_ldc(DT_R8, ILConst.R8 x))
    #if !FX_NO_GET_ENUM_UNDERLYING_TYPE
                    | :? Enum as x when x.GetType().GetEnumUnderlyingType() = typeof<int32> -> ilg.Emit(mk_ldc (unbox<int32> v))
    #endif
                    | :? Type as ty ->
                        ilg.Emit(I_ldtoken (ILToken.ILType (transType ty)))
                        ilg.Emit(mkNormalCall (transMeth (getTypeFromHandleMethod())))
                    | :? decimal as x ->
                        let bits = Decimal.GetBits x
                        ilg.Emit(mk_ldc bits.[0])
                        ilg.Emit(mk_ldc bits.[1])
                        ilg.Emit(mk_ldc bits.[2])
                        do
                            let sign = (bits.[3] &&& 0x80000000) <> 0
                            ilg.Emit(if sign then mk_ldc 1 else mk_ldc 0)
                        do
                            let scale = (bits.[3] >>> 16) &&& 0x7F
                            ilg.Emit(mk_ldc scale)
                        ilg.Emit(I_newobj (transCtorSpec (decimalConstructor()), None))
                    | :? DateTime as x ->
                        ilg.Emit(mk_ldc_i8 x.Ticks)
                        ilg.Emit(mk_ldc (int x.Kind))
                        ilg.Emit(I_newobj (transCtorSpec (dateTimeConstructor()), None))
                    | :? DateTimeOffset as x ->
                        ilg.Emit(mk_ldc_i8 x.Ticks)
                        ilg.Emit(mk_ldc_i8 x.Offset.Ticks)
                        ilg.Emit(I_newobj (transCtorSpec (timeSpanConstructor()), None))
                        ilg.Emit(I_newobj (transCtorSpec (dateTimeOffsetConstructor()), None))
                    | null -> ilg.Emit(I_ldnull)
                    | _ -> failwithf "unknown constant '%A' of type '%O' in generated method. You may need to avoid variable capture in a quotation specifying a type provider." v (v.GetType())
                if isEmpty expectedState then ()
                else emitC obj

            | Let(v, e, b) ->
                let ty = transType v.Type
                let lb = ilg.DeclareLocal ty
                //printfn "declared local %d of original type %O and target type %O for variable %O" lb.LocalIndex v.Type ty  v
                localsMap.Add (v, lb)
                emitExpr ExpectedStackState.Value e
                ilg.Emit(I_stloc lb.LocalIndex)
                emitExpr expectedState b

            | TypeTest(e, tgtTy) ->
                let tgtTyT = transType tgtTy
                emitExpr ExpectedStackState.Value e
                ilg.Emit(I_isinst tgtTyT)

            | Sequential(e1, e2) ->
                emitExpr ExpectedStackState.Empty e1
                emitExpr expectedState e2

            | IfThenElse(cond, ifTrue, ifFalse) ->
                let ifFalseLabel = ilg.DefineLabel()
                let endLabel = ilg.DefineLabel()

                emitExpr ExpectedStackState.Value cond

                ilg.Emit(I_brcmp (I_brfalse, ifFalseLabel))

                emitExpr expectedState ifTrue
                ilg.Emit(I_br endLabel)

                ilg.MarkLabel(ifFalseLabel)
                emitExpr expectedState ifFalse

                ilg.Emit(I_nop)
                ilg.MarkLabel(endLabel)

            | TryWith(body, _filterVar, _filterBody, catchVar, catchBody) ->

                let stres, ldres =
                    if isEmpty expectedState then ignore, ignore
                    else
                        let local = ilg.DeclareLocal (transType body.Type)
                        let stres = fun () -> ilg.Emit(I_stloc local.LocalIndex)
                        let ldres = fun () -> ilg.Emit(I_ldloc local.LocalIndex)
                        stres, ldres

                let exceptionVar = ilg.DeclareLocal(transType catchVar.Type)
                localsMap.Add(catchVar, exceptionVar)

                ilg.BeginExceptionBlock()

                emitExpr expectedState body
                stres()
                ilg.EndGuardedBlock()

                ilg.BeginCatchBlock(transType  catchVar.Type)
                ilg.Emit(I_stloc exceptionVar.LocalIndex)
                emitExpr expectedState catchBody
                stres()
                ilg.EndExceptionBlock()

                ldres()

            | TryFinally(body, finallyBody) ->

                let stres, ldres =
                    if isEmpty expectedState then ignore, ignore
                    else
                        let local = ilg.DeclareLocal (transType body.Type)
                        let stres = fun () -> ilg.Emit(I_stloc local.LocalIndex)
                        let ldres = fun () -> ilg.Emit(I_ldloc local.LocalIndex)
                        stres, ldres
                
                ilg.BeginExceptionBlock() |> ignore

                emitExpr expectedState body
                stres()
                ilg.EndGuardedBlock()

                ilg.BeginFinallyBlock() |> ignore

                emitExpr expectedState finallyBody

                ilg.EndExceptionBlock()

                ldres()

            | VarSet(v, e) ->
                emitExpr ExpectedStackState.Value e
                match localsMap.TryGetValue v with
                | true, localBuilder ->
                    ilg.Emit(I_stloc localBuilder.LocalIndex)
                | false, _ ->
                    failwith "unknown parameter/field in assignment. Only assignments to locals are currently supported by TypeProviderEmit"
            | Lambda(v, body) ->
                let lambdaLocals = Dictionary()
                emitLambda(ilg, v, body, expr.GetFreeVars(), lambdaLocals, parameterVars)
                popIfEmptyExpected expectedState

            | n ->
                failwithf "unknown expression '%A' in generated method" n
        
        member __.EmitExpr (expectedState, expr) = emitExpr expectedState expr

    //-------------------------------------------------------------------------------------------------
    // AssemblyCompiler: the assembly compiler for generative type providers.

    /// Implements System.Reflection.Assembly backed by ILModuleReader over generated bytes 
    type AssemblyCompiler(targetAssembly: ProvidedAssembly, context: ProvidedTypesContext) =


        let typeMap = Dictionary<ProvidedTypeDefinition, ILTypeBuilder>(HashIdentity.Reference)
        let typeMapExtra = Dictionary<string, ILTypeBuilder>(HashIdentity.Structural)
        let ctorMap = Dictionary<ProvidedConstructor, ILMethodBuilder>(HashIdentity.Reference)
        let methMap = Dictionary<ProvidedMethod, ILMethodBuilder>(HashIdentity.Reference)
        let fieldMap = Dictionary<FieldInfo, ILFieldBuilder>(HashIdentity.Reference)
        let genUniqueTypeName() =
            // lambda name should be unique across all types that all type provider might contribute in result assembly
            sprintf "Lambda%O" (Guid.NewGuid())

        let convTypeToTgt ty = context.ConvertSourceTypeToTarget ty
        let rec defineNestedTypes (tb:ILTypeBuilder)  (td: ProvidedTypeDefinition) =
            Debug.Assert(td.BelongsToTargetModel, "expected a target ProvidedTypeDefinition in nested type")
            for ntd in td.GetNestedTypes(bindAll) do
                defineNestedType tb ntd

        and defineNestedType (tb:ILTypeBuilder)  (ntd: Type) =
            match ntd with
            | :? ProvidedTypeDefinition as pntd ->
                if pntd.IsErased then failwith ("The nested provided type "+pntd.Name+" is marked as erased and cannot be converted to a generated type. Set 'IsErased=false' on the ProvidedTypeDefinition")
                Debug.Assert(pntd.BelongsToTargetModel, "expected a target ProvidedTypeDefinition in nested type")
                // Adjust the attributes - we're codegen'ing this type as nested
                let attributes = adjustTypeAttributes true ntd.Attributes 
                let ntb = tb.DefineNestedType(pntd.Name, attributes)
                typeMap.[pntd] <- ntb
                defineNestedTypes ntb pntd
            | _ -> ()

        let rec transType (ty:Type) =
            if (match ty with :? ProvidedTypeDefinition as ty -> not ty.BelongsToTargetModel | _ -> false) then failwithf "expected '%O' to belong to the target model" ty
            if ty.IsGenericParameter then ILType.Var ty.GenericParameterPosition
            elif ty.HasElementType then
                let ety = transType (ty.GetElementType())
                if ty.IsArray then
                    let rank = ty.GetArrayRank()
                    if rank = 1 then ILType.Array(ILArrayShape.SingleDimensional, ety)
                    else ILType.Array(ILArrayShape.FromRank rank, ety)
                elif ty.IsPointer then ILType.Ptr ety
                elif ty.IsByRef then ILType.Byref ety
                else failwith "unexpected type with element type"
            elif ty.Namespace = "System" && ty.Name = "Void" then ILType.Void
            elif ty.IsValueType then ILType.Value (transTypeSpec ty)
            else ILType.Boxed (transTypeSpec ty)

        and transTypeSpec (ty: Type) =
            if ty.IsGenericType then 
                 ILTypeSpec(transTypeRef (ty.GetGenericTypeDefinition()), Array.map transType (ty.GetGenericArguments()))
            else 
                 ILTypeSpec(transTypeRef ty, [| |])

        and transTypeRef (ty: Type) = 
            let ty = if ty.IsGenericType then ty.GetGenericTypeDefinition() else ty
            ILTypeRef(transTypeRefScope ty, StructOption.ofObj (if ty.IsNested then null else ty.Namespace), ty.Name)

        and transTypeRefScope (ty: Type): ILTypeRefScope = 
            match ty.DeclaringType with 
            | null -> 
                if ty.Assembly = null then failwithf "null assembly for type %s" ty.FullName
                ILTypeRefScope.Top (transScopeRef ty.Assembly)
            | dt -> ILTypeRefScope.Nested (transTypeRef dt)

        and transScopeRef (assem: Assembly): ILScopeRef = 
            // Note: this simple equality check on assembly objects doesn't work on Mono, there must be some small difference in the 
            // implementation of equality on System.Assembly objects
            // if assem  = (targetAssembly :> Assembly) then ILScopeRef.Local
            if assem.GetName().Name = targetAssembly.GetName().Name then ILScopeRef.Local
            else ILScopeRef.Assembly (ILAssemblyRef.FromAssemblyName (assem.GetName()))

        let transCtorRef (m:ConstructorInfo) = 
            // Remove the generic instantiations to get the uninstantiated identity of the method
            let m2 = m.GetDefinition()
            let cc = (if m2.IsStatic then ILCallingConv.Static else ILCallingConv.Instance)
            let ptys = [| for p in m2.GetParameters() -> transType p.ParameterType |]
            ILMethodRef (transTypeRef m2.DeclaringType, cc, 0, m2.Name, ptys, ILType.Void)

        let transCtorSpec (f:ConstructorInfo) = 
            if (match f with :? ProvidedConstructor as f -> not f.BelongsToTargetModel | _ -> false) then failwithf "expected '%O' to belong to the target model" f
            match f with 
            | :? ProvidedConstructor as pc when ctorMap.ContainsKey pc -> ctorMap.[pc].FormalMethodSpec
            | m -> ILMethodSpec(transCtorRef f, transType m.DeclaringType, [| |])

        let transFieldSpec (f:FieldInfo) = 
            if (match f with :? ProvidedField as f -> not f.BelongsToTargetModel | _ -> false) then failwithf "expected '%O' to belong to the target model" f
            match f with 
            | :? ProvidedField as pf when fieldMap.ContainsKey pf -> fieldMap.[pf].FormalFieldSpec
            | f -> 
                let f2 = f.GetDefinition()
                ILFieldSpec(ILFieldRef (transTypeRef f2.DeclaringType, f2.Name, transType f2.FieldType), transType f.DeclaringType)

        let transMethRef (m:MethodInfo) = 
            if (match m with :? ProvidedMethod as m -> not m.BelongsToTargetModel | _ -> false) then failwithf "expected '%O' to belong to the target model" m
            // Remove the generic instantiations to get the uninstantiated identity of the method
            let m2 = m.GetDefinition()
            let ptys = [| for p in m2.GetParameters() -> transType p.ParameterType |]
            let genarity = (if m2.IsGenericMethod then m2.GetGenericArguments().Length else 0)
            let cc = (if m2.IsStatic then ILCallingConv.Static else ILCallingConv.Instance)
            ILMethodRef (transTypeRef m2.DeclaringType, cc, genarity, m2.Name, ptys, transType m2.ReturnType)

        let transMeth (m:MethodInfo): ILMethodSpec = 
            match m with 
            | :? ProvidedMethod as pm when methMap.ContainsKey pm -> methMap.[pm].FormalMethodSpec
            | m -> 
                //Debug.Assert (m.Name <> "get_Item1" || m.DeclaringType.Name <> "Tuple`2")
                let mref = transMethRef m
                let minst = (if m.IsGenericMethod then Array.map transType (m.GetGenericArguments()) else [| |])
                ILMethodSpec(mref, transType m.DeclaringType, minst)

        let iterateTypes f providedTypeDefinitions =
            let rec typeMembers (ptd: ProvidedTypeDefinition) =
                let tb = typeMap.[ptd]
                f tb (Some ptd)
                for ntd in ptd.GetNestedTypes(bindAll) do
                    nestedType ntd

            and nestedType (ntd: Type) =
                match ntd with
                | :? ProvidedTypeDefinition as pntd -> typeMembers pntd
                | _ -> ()

            for (pt, enclosingGeneratedTypeNames) in providedTypeDefinitions do
                match enclosingGeneratedTypeNames with
                | None ->
                    typeMembers pt
                | Some ns ->
                    let _fullName  =
                        ("", ns) ||> List.fold (fun fullName n ->
                            let fullName = if fullName = "" then n else fullName + "." + n
                            f typeMapExtra.[fullName] None
                            fullName)
                    nestedType pt

        let defineCustomAttrs f (cattrs: IList<CustomAttributeData>) =
            for attr in cattrs do
                let constructorArgs = [ for x in attr.ConstructorArguments -> x.Value ]
                let transValue (o:obj) = 
                    match o with 
                    | :? Type as t -> box (transType t)
                    | v -> v
                let namedProps = [ for x in attr.NamedArguments do match x.MemberInfo with :? PropertyInfo as pi -> yield ILCustomAttrNamedArg(pi.Name, transType x.TypedValue.ArgumentType, x.TypedValue.Value) | _ -> () ] 
                let namedFields = [ for x in attr.NamedArguments do match x.MemberInfo with :? FieldInfo as pi -> yield ILCustomAttrNamedArg(pi.Name, transType x.TypedValue.ArgumentType, x.TypedValue.Value) | _ -> () ] 
                let ca = mkILCustomAttribMethRef (transCtorSpec attr.Constructor, constructorArgs, namedProps, namedFields)
                f ca

        member __.Compile(isHostedExecution) =
            let providedTypeDefinitionsT = targetAssembly.GetTheTypes() |> Array.collect (fun (tds, nsps) -> Array.map (fun td -> (td, nsps)) tds)
            let ilg = context.ILGlobals
            let assemblyName = targetAssembly.GetName()
            let assemblyFileName = targetAssembly.Location
            let assemblyBuilder = 
                let attrs = targetAssembly.GetCustomAttributesData()
                let cattrs = ResizeArray()
                defineCustomAttrs cattrs.Add attrs
                ILAssemblyBuilder(assemblyName, assemblyFileName, ilg, cattrs)
            let assemblyMainModule = assemblyBuilder.MainModule

            // Set the Assembly on the type definitions
            for (ptdT, _) in providedTypeDefinitionsT do
                if not ptdT.BelongsToTargetModel then failwithf "expected '%O' to belong to the target model" ptdT
                ptdT.SetAssemblyInternal (K (targetAssembly :> Assembly))

            // phase 1 - define types
            for (pt, enclosingGeneratedTypeNames) in providedTypeDefinitionsT do
                match enclosingGeneratedTypeNames with
                | None ->
                    // Filter out the additional TypeProviderTypeAttributes flags
                    let attributes = pt.Attributes &&& ~~~(enum (int32 TypeProviderTypeAttributes.SuppressRelocate))
                                                    &&& ~~~(enum (int32 TypeProviderTypeAttributes.IsErased))
                    // Adjust the attributes - we're codegen'ing as non-nested
                    let attributes = adjustTypeAttributes false attributes
                    let tb = assemblyMainModule.DefineType(StructOption.ofObj pt.Namespace, pt.Name, attributes)
                    typeMap.[pt] <- tb
                    defineNestedTypes tb pt

                | Some ns ->
                    let otb, _ =
                        ((None, ""), ns) ||> List.fold (fun (otb:ILTypeBuilder option, fullName) n ->
                            let fullName = if fullName = "" then n else fullName + "." + n
                            let priorType = if typeMapExtra.ContainsKey(fullName) then Some typeMapExtra.[fullName]  else None
                            let tb =
                                match priorType with
                                | Some tbb -> tbb
                                | None ->
                                // OK, the implied nested type is not defined, define it now
                                let attributes = TypeAttributes.Public ||| TypeAttributes.Class ||| TypeAttributes.Sealed
                                let attributes = adjustTypeAttributes otb.IsSome attributes
                                let tb =
                                    match otb with
                                    | None -> 
                                        let nsp, n = splitILTypeName n
                                        assemblyMainModule.DefineType(nsp, n, attributes)
                                    | Some (otb:ILTypeBuilder) -> 
                                        otb.DefineNestedType(n, attributes)
                                typeMapExtra.[fullName] <- tb
                                tb
                            (Some tb, fullName))
                    defineNestedType otb.Value pt


            // phase 1b - emit base types
            providedTypeDefinitionsT |> iterateTypes (fun tb ptdT ->
                match ptdT with
                | None -> ()
                | Some ptdT ->
                match ptdT.BaseType with 
                | null -> () 
                | bt -> tb.SetParent(transType bt))

            // phase 2 - emit member definitions
            providedTypeDefinitionsT |> iterateTypes (fun tb ptdT ->
                match ptdT with
                | None -> ()
                | Some ptdT ->
                    for cinfo in ptdT.GetConstructors(bindAll) do
                        match cinfo with
                        | :? ProvidedConstructor as pcinfo when not (ctorMap.ContainsKey pcinfo)  ->
                            let cb =
                                if pcinfo.IsTypeInitializer then
                                    if (cinfo.GetParameters()).Length <> 0 then failwith "Type initializer should not have parameters"
                                    tb.DefineTypeInitializer()
                                else
                                    let cb = tb.DefineConstructor(cinfo.Attributes, [| for p in cinfo.GetParameters() -> transType p.ParameterType |])
                                    for (i, p) in cinfo.GetParameters() |> Seq.mapi (fun i x -> (i, x)) do
                                        cb.DefineParameter(i+1, ParameterAttributes.None, p.Name) |> ignore
                                    cb
                            ctorMap.[pcinfo] <- cb
                        | _ -> ()

                    if ptdT.IsEnum then
                        tb.DefineField("value__", transType (ptdT.GetEnumUnderlyingType()), FieldAttributes.Public ||| FieldAttributes.SpecialName ||| FieldAttributes.RTSpecialName)
                        |> ignore

                    for finfo in ptdT.GetFields(bindAll) do
                        match finfo with
                        | :? ProvidedField as pinfo ->
                            let fb = tb.DefineField(finfo.Name, transType finfo.FieldType, finfo.Attributes)

                            if finfo.IsLiteral then 
                                fb.SetConstant (pinfo.GetRawConstantValue())

                            defineCustomAttrs fb.SetCustomAttribute (pinfo.GetCustomAttributesData())

                            fieldMap.[finfo] <- fb

                        | _ -> ()

                    for minfo in ptdT.GetMethods(bindAll) do
                        match minfo with
                        | :? ProvidedMethod as pminfo when not (methMap.ContainsKey pminfo)  ->
                            let mb = tb.DefineMethod(minfo.Name, minfo.Attributes, transType minfo.ReturnType, [| for p in minfo.GetParameters() -> transType p.ParameterType |])

                            for (i, p) in minfo.GetParameters() |> Seq.mapi (fun i x -> (i, x :?> ProvidedParameter)) do

                                let pb = mb.DefineParameter(i+1, p.Attributes, p.Name)
                                if p.HasDefaultParameterValue then
                                    let ctorTy = typeof<System.Runtime.InteropServices.DefaultParameterValueAttribute>
                                    let ctor = ctorTy.GetConstructor([|typeof<obj>|])
                                    let ctorTgt = context.ConvertSourceConstructorRefToTarget ctor

                                    let ca = mkILCustomAttribMethRef (transCtorSpec ctorTgt, [p.RawDefaultValue], [], [])
                                    pb.SetCustomAttribute ca

                                    let ctorTy = typeof<System.Runtime.InteropServices.OptionalAttribute>
                                    let ctor = ctorTy.GetConstructor([||])
                                    let ctorTgt = context.ConvertSourceConstructorRefToTarget ctor
                                    let ca = mkILCustomAttribMethRef (transCtorSpec ctorTgt, [], [], [])
                                    pb.SetCustomAttribute ca

                                    pb.SetConstant p.RawDefaultValue

                            methMap.[pminfo] <- mb

                        | _ -> ()

                    for ityp in ptdT.GetInterfaces() do
                        tb.AddInterfaceImplementation (transType ityp))

            // phase 3 - emit member code
            providedTypeDefinitionsT |> iterateTypes (fun tb ptdT ->
                match ptdT with
                | None -> ()
                | Some ptdT ->

                    defineCustomAttrs tb.SetCustomAttribute (ptdT.GetCustomAttributesData())

                    // Allow at most one constructor, and use its arguments as the fields of the type
                    let ctors =
                        ptdT.GetConstructors(bindAll) // exclude type initializer
                        |> Seq.choose (function :? ProvidedConstructor as pcinfo when not pcinfo.IsTypeInitializer -> Some pcinfo | _ -> None)
                        |> Seq.toList

                    let implictCtorArgs =
                        match ctors  |> List.filter (fun x -> x.IsImplicitConstructor)  with
                        | [] -> []
                        | [ pcinfo ] -> [ for p in pcinfo.GetParameters() -> p ]
                        | _ -> failwith "at most one implicit constructor allowed"

                    let implicitCtorArgsAsFields =
                        [ for ctorArg in implictCtorArgs ->
                              tb.DefineField(ctorArg.Name, transType ctorArg.ParameterType, FieldAttributes.Private) ]


                    // Emit the constructor (if any)
                    for pcinfo in ctors do
                        assert ctorMap.ContainsKey pcinfo
                        if not pcinfo.BelongsToTargetModel then failwithf "expected '%O' to be a target ProvidedConstructor. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" pcinfo
                        let cb = ctorMap.[pcinfo]

                        defineCustomAttrs cb.SetCustomAttribute (pcinfo.GetCustomAttributesData())

                        let ilg = cb.GetILGenerator()
                        let ctorLocals = Dictionary<Var, ILLocalBuilder>()
                        let parameterVars =
                            [| yield Var("this", pcinfo.DeclaringType)
                               for p in pcinfo.GetParameters() do
                                    yield Var(p.Name, p.ParameterType) |]

                        let codeGen = CodeGenerator(assemblyMainModule, genUniqueTypeName, implicitCtorArgsAsFields, convTypeToTgt, transType, transFieldSpec, transMeth, transMethRef, transCtorSpec, ilg, ctorLocals, parameterVars)

                        let parameters = [ for v in parameterVars -> Expr.Var v ]

                        match pcinfo.BaseCall with
                        | None ->
                            ilg.Emit(I_ldarg 0)
                            let cinfo = ptdT.BaseType.GetConstructor(bindAll, null, [| |], null)
                            ilg.Emit(mkNormalCall (transCtorSpec cinfo))
                        | Some f ->
                            // argExprs should always include 'this'
                            let (cinfo, argExprs) = f parameters
                            for argExpr in argExprs do
                                codeGen.EmitExpr (ExpectedStackState.Value, argExpr)
                            ilg.Emit(mkNormalCall (transCtorSpec cinfo))

                        if pcinfo.IsImplicitConstructor then
                            for ctorArgsAsFieldIdx, ctorArgsAsField in List.mapi (fun i x -> (i, x)) implicitCtorArgsAsFields do
                                ilg.Emit(I_ldarg 0)
                                ilg.Emit(I_ldarg (ctorArgsAsFieldIdx+1))
                                ilg.Emit(I_stfld (ILAlignment.Aligned, ILVolatility.Nonvolatile, ctorArgsAsField.FormalFieldSpec))
                        else
                            let code = pcinfo.GetInvokeCode parameters
                            codeGen.EmitExpr (ExpectedStackState.Empty, code)
                        ilg.Emit(I_ret)

                    match ptdT.GetConstructors(bindAll) |> Seq.tryPick (function :? ProvidedConstructor as pc when pc.IsTypeInitializer -> Some pc | _ -> None) with
                    | None -> ()
                    | Some _ when ptdT.IsInterface ->
                        failwith "The provided type definition is an interface; therefore, it may not provide constructors."
                    | Some pc ->
                        let cb = ctorMap.[pc]
                        let ilg = cb.GetILGenerator()

                        defineCustomAttrs cb.SetCustomAttribute (pc.GetCustomAttributesData())

                        let expr = pc.GetInvokeCode [ ]
                        let ctorLocals = new Dictionary<_, _>()
                        let codeGen = CodeGenerator(assemblyMainModule, genUniqueTypeName, implicitCtorArgsAsFields, convTypeToTgt, transType, transFieldSpec, transMeth, transMethRef, transCtorSpec, ilg, ctorLocals, [| |])
                        codeGen.EmitExpr (ExpectedStackState.Empty, expr)
                        ilg.Emit I_ret

                    // Emit the methods
                    for minfo in ptdT.GetMethods(bindAll) do
                      match minfo with
                      | :? ProvidedMethod as pminfo   ->
                        if not pminfo.BelongsToTargetModel then failwithf "expected '%O' to be a target ProvidedMethod. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" pminfo
                        let mb = methMap.[pminfo]
                        defineCustomAttrs mb.SetCustomAttribute (pminfo.GetCustomAttributesData())

                        let parameterVars =
                            [| if not pminfo.IsStatic then
                                    yield Var("this", pminfo.DeclaringType)
                               for p in pminfo.GetParameters() do
                                    yield Var(p.Name, p.ParameterType) |]
                        let parameters =
                            [ for v in parameterVars -> Expr.Var v ]

                        match pminfo.GetInvokeCode with
                        | Some _ when ptdT.IsInterface ->
                            failwith "The provided type definition is an interface; therefore, it should not define an implementation for its members."
                        | Some _ when pminfo.IsAbstract ->
                            failwith "The provided method is marked as an abstract method; therefore, it should not define an implementation."
                        | None when not (pminfo.IsAbstract || ptdT.IsAbstract ||ptdT.IsInterface)  ->
                            failwith "The provided method is not marked as an abstract method; therefore, it should define an implementation."
                        | None when pminfo.IsAbstract || ptdT.IsInterface ->
                            // abstract and interface methods have no body at all
                            ()
                        | None -> 
                            let ilg = mb.GetILGenerator()
                            ilg.Emit I_ret
                        | Some invokeCode ->
                            let ilg = mb.GetILGenerator()
                            let expr = invokeCode parameters

                            let methLocals = Dictionary<Var, ILLocalBuilder>()

                            let expectedState = if (transType minfo.ReturnType = ILType.Void) then ExpectedStackState.Empty else ExpectedStackState.Value
                            let codeGen = CodeGenerator(assemblyMainModule, genUniqueTypeName, implicitCtorArgsAsFields, convTypeToTgt, transType, transFieldSpec, transMeth, transMethRef, transCtorSpec, ilg, methLocals, parameterVars)
                            codeGen.EmitExpr (expectedState, expr)
                            ilg.Emit I_ret
                      | _ -> ()

                    for (bodyMethInfo, declMethInfo) in ptdT.GetMethodOverrides() do
                        let bodyMethBuilder = methMap.[bodyMethInfo]
                        tb.DefineMethodOverride
                            { Overrides = OverridesSpec(transMethRef declMethInfo, transType declMethInfo.DeclaringType)
                              OverrideBy = bodyMethBuilder.FormalMethodSpec }

                    for evt in ptdT.GetEvents(bindAll) |> Seq.choose (function :? ProvidedEvent as pe -> Some pe | _ -> None) do
                        if not evt.BelongsToTargetModel then failwithf "expected '%O' to be a target ProvidedEvent. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" evt
                        let eb = tb.DefineEvent(evt.Name, evt.Attributes)
                        defineCustomAttrs eb.SetCustomAttribute (evt.GetCustomAttributesData())
                        eb.SetAddOnMethod(methMap.[evt.GetAddMethod(true) :?> _])
                        eb.SetRemoveOnMethod(methMap.[evt.GetRemoveMethod(true) :?> _])

                    for pinfo in ptdT.GetProperties(bindAll) |> Seq.choose (function :? ProvidedProperty as pe -> Some pe | _ -> None) do

                        let pb = tb.DefineProperty(pinfo.Name, pinfo.Attributes, transType pinfo.PropertyType, [| for p in pinfo.GetIndexParameters() -> transType p.ParameterType |])

                        defineCustomAttrs pb.SetCustomAttribute (pinfo.GetCustomAttributesData())

                        if  pinfo.CanRead then
                            let minfo = pinfo.GetGetMethod(true)
                            pb.SetGetMethod (methMap.[minfo :?> ProvidedMethod ])

                        if  pinfo.CanWrite then
                            let minfo = pinfo.GetSetMethod(true)
                            pb.SetSetMethod (methMap.[minfo :?> ProvidedMethod ]))

            //printfn "saving generated binary to '%s'" assemblyFileName
            assemblyBuilder.Save ()
            //printfn "re-reading generated binary from '%s'" assemblyFileName
            let reader = ILModuleReaderAfterReadingAllBytes(assemblyFileName, ilg)
            let bytes = File.ReadAllBytes(assemblyFileName)
#if DEBUG
            printfn "generated binary is at '%s'" assemblyFileName
#else
            File.Delete assemblyFileName
#endif

            // Use a real Reflection Load when running in F# Interactive
            if isHostedExecution then 
                let realTargetAssembly = Assembly.Load(bytes)
                for (ptdT, _) in providedTypeDefinitionsT do
                    ptdT.SetAssemblyInternal (K realTargetAssembly)

            bytes


#endif // NO_GENERATIVE

//-------------------------------------------------------------------------------------------------
// TypeProviderForNamespaces

namespace ProviderImplementation.ProvidedTypes

    #nowarn "1182"
    open System
    open System.Diagnostics
    open System.IO
    open System.Collections.Concurrent
    open System.Collections.Generic
    open System.Reflection

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.DerivedPatterns
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.ExprShape
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Reflection

    open ProviderImplementation.ProvidedTypes
    open ProviderImplementation.ProvidedTypes.AssemblyReader
    open ProviderImplementation.ProvidedTypes.UncheckedQuotations

    type TypeProviderForNamespaces(config: TypeProviderConfig, namespacesAndTypes: list<(string * list<ProvidedTypeDefinition>)>, assemblyReplacementMap: (string*string) list, sourceAssemblies: Assembly list, addDefaultProbingLocation: bool) as this =

        let ctxt = ProvidedTypesContext.Create (config, assemblyReplacementMap, sourceAssemblies)

#if !NO_GENERATIVE
        let theTable = ConcurrentDictionary<string, byte[]>()

        // When using hosted execution (i.e. in F# Interactive), ensure the generated assembly for a generated type is 
        // actually fully compiled and loaded as a reflection-load assembly before handing the type back to the API.
        let ensureCompiled (t: Type) =
            match t with 
            | :? ProvidedTypeDefinition as pt when pt.IsErased || pt.GetStaticParametersInternal().Length > 0 || not config.IsHostedExecution -> t
            | _ -> 
                let origAssembly = t.Assembly

                // We expect the results reported by t.Assembly to actually change after this call, because the act of compilation
                // when isHostedExecution=true replaces the Assembly object reported.
                (this :> ITypeProvider).GetGeneratedAssemblyContents(origAssembly) |> ignore

                //printfn "t.Assembly = %O" t.Assembly
                //printfn "t.Assembly.Location = %O" t.Assembly.Location
                //printfn "t.FullName = %O" t.FullName
                //printfn "t.Assembly.GetTypes() = %A" (t.Assembly.GetTypes())
                let tyName = t.FullName.Replace(",", "\\,")
                let newAssembly = t.Assembly
                let newAssemblyName = newAssembly.GetName().Name
                let origAssemblyName = origAssembly.GetName().Name
                // check the assembly was generated with the correct name
                if newAssemblyName <> origAssemblyName  then 
                    failwithf "expected identical assembly name keys '%s' and '%s'" origAssemblyName newAssemblyName

                // check the type really exists
                if t.Assembly.GetType(tyName) = null then 
                    failwithf "couldn't find type '%s' in assembly '%O'" tyName t.Assembly

                t

#else
        let ensureCompiled (t: Type) = t
#endif

        let makeProvidedNamespace (namespaceName:string) (typesSrc:ProvidedTypeDefinition list) =
            let typesSrc = [| for ty in typesSrc -> ty :> Type |]
            let nsSrc = 
                { new IProvidedNamespace with
                    member __.GetNestedNamespaces() = [| |]
                    member __.NamespaceName = namespaceName
                    member __.GetTypes() = typesSrc |> Array.map ensureCompiled
                    member __.ResolveTypeName typeName = typesSrc |> Array.tryFind (fun ty -> ty.Name = typeName) |> Option.map ensureCompiled |> Option.toObj }
            let nsT = ctxt.ConvertSourceNamespaceToTarget nsSrc
            nsT

        let namespacesT = ResizeArray<IProvidedNamespace>()

        do for (namespaceName, types)  in namespacesAndTypes do 
               namespacesT.Add (makeProvidedNamespace namespaceName types)

        let invalidateE = new Event<EventHandler, EventArgs>()

        let disposing = Event<EventHandler, EventArgs>()


#if !FX_NO_LOCAL_FILESYSTEM
        let probingFolders = ResizeArray()
        let handler = ResolveEventHandler(fun _ args -> this.ResolveAssembly(args))
        do AppDomain.CurrentDomain.add_AssemblyResolve handler
#endif

        // By default add the location of the TPDTC assembly (which is assumed to contain this file)
        // as a probing location.
        do if addDefaultProbingLocation  then
            let thisAssembly = Assembly.GetExecutingAssembly() 
            let folder = thisAssembly.Location |> Path.GetDirectoryName
            probingFolders.Add folder 

        new (config, namespaceName, types, ?sourceAssemblies, ?assemblyReplacementMap, ?addDefaultProbingLocation) = 
            let sourceAssemblies = defaultArg sourceAssemblies [ Assembly.GetCallingAssembly() ]
            let assemblyReplacementMap = defaultArg assemblyReplacementMap []
            let addDefaultProbingLocation = defaultArg addDefaultProbingLocation false
            new TypeProviderForNamespaces(config, [(namespaceName, types)], assemblyReplacementMap=assemblyReplacementMap, sourceAssemblies=sourceAssemblies, addDefaultProbingLocation=addDefaultProbingLocation)

        new (config, ?sourceAssemblies, ?assemblyReplacementMap, ?addDefaultProbingLocation) = 
            let sourceAssemblies = defaultArg sourceAssemblies [ Assembly.GetCallingAssembly() ]
            let assemblyReplacementMap = defaultArg assemblyReplacementMap []
            let addDefaultProbingLocation = defaultArg addDefaultProbingLocation false
            new TypeProviderForNamespaces(config, [], assemblyReplacementMap=assemblyReplacementMap, sourceAssemblies=sourceAssemblies, addDefaultProbingLocation=addDefaultProbingLocation)

        member __.TargetContext = ctxt

        [<CLIEvent>]
        member __.Disposing = disposing.Publish

#if FX_NO_LOCAL_FILESYSTEM

        interface IDisposable with
            member x.Dispose() =
                disposing.Trigger(x, EventArgs.Empty)

#else

        abstract member ResolveAssembly: args: ResolveEventArgs -> Assembly

        default __.ResolveAssembly(args) =
            let expectedName = (AssemblyName(args.Name)).Name + ".dll"
            let expectedLocationOpt =
                probingFolders
                |> Seq.map (fun f -> Path.Combine(f, expectedName))
                |> Seq.tryFind File.Exists
            match expectedLocationOpt with
            | Some f -> Assembly.LoadFrom f
            | None -> null

        member __.RegisterProbingFolder (folder) =
            // use GetFullPath to ensure that folder is valid
            ignore(Path.GetFullPath folder)
            probingFolders.Add folder

        member __.RegisterRuntimeAssemblyLocationAsProbingFolder (config: TypeProviderConfig) =
            config.RuntimeAssembly
            |> Path.GetDirectoryName
            |> this.RegisterProbingFolder

        interface IDisposable with
            member x.Dispose() =
                disposing.Trigger(x, EventArgs.Empty)
                AppDomain.CurrentDomain.remove_AssemblyResolve handler
#endif

        member __.AddNamespace (namespaceName, types) = 
            namespacesT.Add (makeProvidedNamespace namespaceName types)

        member __.Namespaces = 
            namespacesT.ToArray()

        member this.Invalidate() = 
            invalidateE.Trigger(this, EventArgs())

        member __.GetStaticParametersForMethod(mb: MethodBase) =
            match mb with
            | :? ProvidedMethod as t -> t.GetStaticParametersInternal()
            | _ -> [| |]

        member __.ApplyStaticArgumentsForMethod(mb: MethodBase, mangledName, objs) =
            match mb with
            | :? ProvidedMethod as t -> t.ApplyStaticArguments(mangledName, objs) :> MethodBase
            | _ -> failwithf "ApplyStaticArguments: static parameters for method %s are unexpected. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" mb.Name

        interface ITypeProvider with

            [<CLIEvent>]
            member __.Invalidate = invalidateE.Publish

            member __.GetNamespaces() = namespacesT.ToArray()

            member __.GetInvokerExpression(methodBaseT, parametersT) =

                /// This checks that the GetInvokeCodeInternal doesn't return things containing calls to other provided methods or constructors.
                let rec check expr =
                    match expr with
                    | NewObject((:? ProvidedConstructor), _) 
                    | Call(_, :? ProvidedMethod, _) -> failwithf "The invokeCode for a ProvidedConstructor or ProvidedMethod included a use or another ProvidedConstructor or ProvidedMethod '%A'.  This is not allowed.  Instead, the invokeCode should be the compiled representation without invoking other provided objects" expr
                    | ShapeCombinationUnchecked(shape, args) -> RebuildShapeCombinationUnchecked(shape, List.map check args)
                    | ShapeVarUnchecked v -> Expr.Var v
                    | ShapeLambdaUnchecked(v, body) -> Expr.Lambda(v, check body)

                match methodBaseT with
                | :? ProvidedMethod as mT when (match methodBaseT.DeclaringType with :? ProvidedTypeDefinition as pt -> pt.IsErased | _ -> true) ->
                    match mT.GetInvokeCode with
                    | Some _ when methodBaseT.DeclaringType.IsInterface ->
                        failwith "The provided type definition is an interface; therefore, it should not define an implementation for its members."
                    (* NOTE: These checks appear to fail for generative abstract and virtual methods.
                    | Some _ when mT.IsAbstract ->
                        failwith "The provided method is defined as abstract; therefore, it should not define an implementation."
                    | None when not mT.IsAbstract ->
                        failwith "The provided method is not defined as abstract; therefore it should define an implementation."
                    *)
                    | Some invokeCode ->
                        let exprT = invokeCode(Array.toList parametersT)
                        check exprT
                    | None -> <@@ () @@>

                | :? ProvidedConstructor as mT when (match methodBaseT.DeclaringType with :? ProvidedTypeDefinition as pt -> pt.IsErased | _ -> true) ->
                    if methodBaseT.DeclaringType.IsInterface then
                        failwith "The provided type definition is an interface; therefore, it should not define any constructors."
                    else
                        let exprT = mT.GetInvokeCode(Array.toList parametersT)
                        check exprT

                // Otherwise, assume this is a generative assembly and just emit a call to the constructor or method
                | :?  ConstructorInfo as cinfoT ->
                    Expr.NewObjectUnchecked(cinfoT, Array.toList parametersT)

                | :? MethodInfo as minfoT ->
                    if minfoT.IsStatic then
                        Expr.CallUnchecked(minfoT, Array.toList parametersT)
                    else
                        Expr.CallUnchecked(parametersT.[0], minfoT, Array.toList parametersT.[1..])

                | _ -> failwith ("TypeProviderForNamespaces.GetInvokerExpression: not a ProvidedMethod/ProvidedConstructor/ConstructorInfo/MethodInfo, name=" + methodBaseT.Name + " class=" + methodBaseT.GetType().FullName)

            member __.GetStaticParameters(ty) =
                match ty with
                | :? ProvidedTypeDefinition as t ->
                    if ty.Name = t.Name then
                        t.GetStaticParametersInternal()
                    else
                        [| |]
                | _ -> [| |]

            member __.ApplyStaticArguments(ty, typePathAfterArguments:string[], objs) =
                let typePathAfterArguments = typePathAfterArguments.[typePathAfterArguments.Length-1]
                match ty with
                | :? ProvidedTypeDefinition as t -> 
                    let ty = (t.ApplyStaticArguments(typePathAfterArguments, objs) :> Type)
                    ensureCompiled ty

                | _ -> failwithf "ApplyStaticArguments: static params for type %s are unexpected, it is not a provided type definition. Please report this bug to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues" ty.FullName

            member __.GetGeneratedAssemblyContents(assembly:Assembly) =
#if NO_GENERATIVE
                ignore assembly; failwith "no generative assemblies"
#else
                //printfn "looking up assembly '%s'" assembly.FullName
                let key = assembly.GetName().Name
                match theTable.TryGetValue key with
                | true, bytes -> bytes
                | _ ->
                    let bytes = 
                        match assembly with 
                        | :? ProvidedAssembly as targetAssembly -> AssemblyCompiler(targetAssembly, ctxt).Compile(config.IsHostedExecution)
                        | _ -> File.ReadAllBytes assembly.ManifestModule.FullyQualifiedName
                    theTable.[key] <- bytes
                    bytes

#if !NO_GENERATIVE
        member __.RegisterGeneratedTargetAssembly (fileName:string) =
            let assemblyBytes = File.ReadAllBytes fileName
            //printfn "registering assembly in '%s'" fileName
            let assembly = 
                if config.IsHostedExecution then 
                    Assembly.Load(assemblyBytes) // we need a real on-disk assembly
                else
                    ctxt.ReadRelatedAssembly(fileName)
            ctxt.AddTargetAssembly(assembly.GetName(), assembly)
            let key = assembly.GetName().Name
            theTable.[key] <- assemblyBytes
            assembly

#endif 
#endif
