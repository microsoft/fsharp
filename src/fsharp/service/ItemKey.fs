// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.CodeAnalysis

open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Reflection.Metadata
open System.Runtime.InteropServices
open FSharp.NativeInterop
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.Infos
open FSharp.Compiler.NameResolution
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics

#nowarn "9"
#nowarn "51"

/// These tags are used to create unique item key strings to decrease possible key string collisions when the Items are actually completely different.
[<RequireQualifiedAccess>]
module ItemKeyTags =

    [<Literal>]
    let entityRef = "#E#"

    [<Literal>]
    let typeTuple = "#T#"

    [<Literal>]
    let typeAnonymousRecord = "#N#"

    [<Literal>]
    let typeFunction = "#F#"

    [<Literal>]
    let typeMeasure = "#M#"

    [<Literal>]
    let typeUnionCase = "#U#"

    [<Literal>]
    let typeMeasureVar = "#p#"

    [<Literal>]
    let typeMeasureCon = "#c#"

    [<Literal>]
    let itemValueMember = "m$"

    [<Literal>]
    let itemValue = "v$"

    [<Literal>]
    let itemUnionCase = "u$"

    [<Literal>]
    let itemActivePattern = "r$"

    [<Literal>]
    let itemExnCase = "e$"

    [<Literal>]
    let itemRecordField = "d$"

    [<Literal>]
    let itemAnonymousRecordField = "a$"

    [<Literal>]
    let itemNewDef = "n$"

    [<Literal>]
    let itemILField = "l$"

    [<Literal>]
    let itemEvent = "t$"

    [<Literal>]
    let itemProperty = "p$"

    [<Literal>]
    let itemTypeVar = "y$"

    [<Literal>]
    let itemModuleOrNamespace = "o$"

    [<Literal>]
    let itemDelegateCtor = "g$"

    [<Literal>]
    let parameters = "p$p$"

[<Sealed>]
type ItemKeyStore(mmf: MemoryMappedFile, length) =

    let rangeBuffer = Array.zeroCreate<byte> sizeof<range>

    let mutable isDisposed = false
    let checkDispose() =
        if isDisposed then
            raise (ObjectDisposedException("ItemKeyStore"))

    member _.ReadRange(reader: byref<BlobReader>) =
        reader.ReadBytes(sizeof<range>, rangeBuffer, 0)
        MemoryMarshal.Cast<byte, range>(Span(rangeBuffer)).[0]

    member _.ReadKeyString(reader: byref<BlobReader>) =
        let size = reader.ReadInt32()
        let keyString = ReadOnlySpan<byte>(reader.CurrentPointer |> NativePtr.toVoidPtr, size)
        reader.Offset <- reader.Offset + size
        keyString

    member this.ReadFirstKeyString() =
        use view = mmf.CreateViewAccessor(0L, length)
        let mutable reader = BlobReader(view.SafeMemoryMappedViewHandle.DangerousGetHandle() |> NativePtr.ofNativeInt, int length)
        this.ReadRange &reader |> ignore
        let bytes = (this.ReadKeyString &reader).ToArray()
        ReadOnlySpan.op_Implicit bytes

    member this.FindAll(item: Item) =
        checkDispose ()

        let builder = ItemKeyStoreBuilder()
        builder.Write(Range.range0, item)
        match builder.TryBuildAndReset() with
        | None -> Seq.empty
        | Some(singleStore : ItemKeyStore) ->
            let keyString1 = singleStore.ReadFirstKeyString()
            (singleStore :> IDisposable).Dispose()

            let results = ResizeArray()

            use view = mmf.CreateViewAccessor(0L, length)
            let mutable reader = BlobReader(view.SafeMemoryMappedViewHandle.DangerousGetHandle() |> NativePtr.ofNativeInt, int length)

            reader.Offset <- 0
            while reader.Offset < reader.Length do
                let m = this.ReadRange &reader
                let keyString2 = this.ReadKeyString &reader
                if keyString1.SequenceEqual keyString2 then
                    results.Add m

            results :> range seq

    interface IDisposable with

        member _.Dispose() =
            isDisposed <- true
            mmf.Dispose()

and [<Sealed>] ItemKeyStoreBuilder() =

    let b = BlobBuilder()

    let writeChar (c: char) =
        b.WriteUInt16(uint16 c)

    let writeUInt16 (i: uint16) =
        b.WriteUInt16 i

    let writeInt32 (i: int) =
        b.WriteInt32 i

    let writeInt64 (i: int64) =
        b.WriteInt64 i

    let writeString (str: string) =
        b.WriteUTF16 str

    let writeRange (m: range) =
        let mutable m = m
        let ptr = &&m |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<byte>
        b.WriteBytes(ptr, sizeof<range>)

    let writeEntityRef (eref: EntityRef) =
        writeString ItemKeyTags.entityRef
        writeString eref.CompiledName
        eref.CompilationPath.MangledPath
        |> List.iter (fun str -> writeString str)

    let rec writeILType (ilty: ILType) =
        match ilty with
        | ILType.TypeVar n -> writeString "!"; writeUInt16 n
        | ILType.Modified (_, _, ty2) -> writeILType ty2
        | ILType.Array (ILArrayShape s, ty) ->
            writeILType ty
            writeString "["
            writeInt32 (s.Length-1)
            writeString "]"
        | ILType.Value tr
        | ILType.Boxed tr ->
            tr.TypeRef.Enclosing
            |> List.iter (fun x ->
                writeString x
                writeChar '.')
            writeChar '.'
            writeString tr.TypeRef.Name
        | ILType.Void ->
            writeString "void"
        | ILType.Ptr ty ->
            writeString "ptr<"
            writeILType ty
            writeChar '>'
        | ILType.Byref ty ->
            writeString "byref<"
            writeILType ty
            writeChar '>'
        | ILType.FunctionPointer mref ->
            mref.ArgTypes
            |> List.iter (fun x ->
                writeILType x)
            writeILType mref.ReturnType

    let rec writeType isStandalone (ty: TType) =
        match stripTyparEqns ty with
        | TType_forall (_, ty) ->
            writeType false ty
        | TType_app (tcref, _) ->
            writeEntityRef tcref
        | TType_tuple (_, tinst) ->
            writeString ItemKeyTags.typeTuple
            tinst |> List.iter (writeType false)
        | TType_anon (anonInfo, tinst) ->
            writeString ItemKeyTags.typeAnonymousRecord
            writeString anonInfo.ILTypeRef.BasicQualifiedName
            tinst |> List.iter (writeType false)
        | TType_fun (d, r) ->
            writeString ItemKeyTags.typeFunction
            writeType false d
            writeType false r
        | TType_measure ms ->
            if isStandalone then
                writeString ItemKeyTags.typeMeasure
                writeMeasure isStandalone ms
        | TType_var tp ->
            writeTypar isStandalone tp
        | TType_ucase (uc, _) ->
            match uc with
            | UnionCaseRef.UnionCaseRef(tcref, nm) ->
                writeString ItemKeyTags.typeUnionCase
                writeEntityRef tcref
                writeString nm

    and writeMeasure isStandalone (ms: Measure) =
        match ms with
        | Measure.Var typar ->
            writeString ItemKeyTags.typeMeasureVar
            writeTypar isStandalone typar
        | Measure.Con tcref ->
            writeString ItemKeyTags.typeMeasureCon
            writeEntityRef tcref
        | _ ->
            ()

    and writeTypar (isStandalone: bool) (typar: Typar) =
        match typar.Solution with
        | Some ty -> writeType isStandalone ty
        | _ ->
            if isStandalone then
                writeInt64 typar.Stamp

    let writeValRef (vref: ValRef) =
        match vref.MemberInfo with
        | Some memberInfo ->
            writeString ItemKeyTags.itemValueMember
            writeEntityRef memberInfo.ApparentEnclosingEntity
            writeString vref.LogicalName
            writeString ItemKeyTags.parameters
            writeType false vref.Type
        | _ ->
            writeString ItemKeyTags.itemValue
            writeString vref.LogicalName
            writeString ItemKeyTags.parameters
            writeType false vref.Type
            match vref.DeclaringEntity with
            | ParentNone -> writeChar '%'
            | Parent eref -> writeEntityRef eref

    member _.Write (m: range, item: Item) =
        writeRange m

        let fixup = b.ReserveBytes 4 |> BlobWriter

        let preCount = b.Count

        match item with
        | Item.Value vref ->
            if vref.IsPropertyGetterMethod || vref.IsPropertySetterMethod then
                writeString ItemKeyTags.itemProperty
                writeString vref.PropertyName
                match vref.DeclaringEntity with
                | ParentRef.Parent parent ->
                    writeEntityRef parent
                | _ ->
                    ()
            else
                writeValRef vref

        | Item.UnionCase(info, _) ->
            writeString ItemKeyTags.typeUnionCase
            writeEntityRef info.TyconRef
            writeString info.Name

        | Item.ActivePatternResult(info, _, _, _) ->
            writeString ItemKeyTags.itemActivePattern
            info.ActiveTagsWithRanges
            |> List.iter (fun (nm, _) ->
                writeString nm)

        | Item.ActivePatternCase elemRef ->
            writeString ItemKeyTags.itemActivePattern
            elemRef.ActivePatternInfo.ActiveTagsWithRanges
            |> List.iter (fun (nm, _) -> writeString nm)

        | Item.ExnCase tcref ->
            writeString ItemKeyTags.itemExnCase
            writeEntityRef tcref

        | Item.RecdField info ->
            writeString ItemKeyTags.itemRecordField
            writeEntityRef info.TyconRef
            writeString info.Name
            writeType false info.FieldType

        | Item.UnionCaseField(info, fieldIndex) ->
            writeString ItemKeyTags.typeUnionCase
            writeEntityRef info.TyconRef
            writeString info.Name
            writeInt32 fieldIndex

        | Item.AnonRecdField(info, tys, i, _) ->
            writeString ItemKeyTags.itemAnonymousRecordField
            writeString info.ILTypeRef.BasicQualifiedName
            tys |> List.iter (writeType false)
            writeInt32 i

        | Item.NewDef ident ->
            writeString ItemKeyTags.itemNewDef
            writeString ident.idText

        | Item.ILField info ->
            writeString ItemKeyTags.itemILField
            writeString info.ILTypeRef.BasicQualifiedName
            writeString info.FieldName

        | Item.Event info ->
            writeString ItemKeyTags.itemEvent
            writeString info.EventName
            writeEntityRef info.DeclaringTyconRef

        | Item.Property(nm, infos) ->
            writeString ItemKeyTags.itemProperty
            writeString nm
            match infos |> List.tryHead with
            | Some info ->
                writeEntityRef info.DeclaringTyconRef
            | _ ->
                ()

        | Item.TypeVar(_, typar) ->
            writeTypar true typar

        | Item.Types(_, [ty]) ->
            writeType true ty

        | Item.UnqualifiedType [tcref] ->
            writeEntityRef tcref

        | Item.MethodGroup(_, [info], _)
        | Item.CtorGroup(_, [info]) ->
            match info with
            | FSMeth(_, _, vref, _) ->
                writeValRef vref
            | ILMeth(_, info, _) ->
                info.ILMethodRef.ArgTypes
                |> List.iter writeILType
                writeILType info.ILMethodRef.ReturnType
                writeString info.ILName
                writeType false info.ApparentEnclosingType
            | _ ->
                writeString ItemKeyTags.itemValueMember
                writeEntityRef info.DeclaringTyconRef
                writeString info.LogicalName

        | Item.ModuleOrNamespaces [x] ->
            writeString ItemKeyTags.itemModuleOrNamespace
            x.CompilationPath.DemangledPath
            |> List.iter (fun x ->
                writeString x
                writeString ".")
            writeString x.LogicalName

        | Item.DelegateCtor ty ->
            writeString ItemKeyTags.itemDelegateCtor
            writeType false ty

        | Item.MethodGroup _ -> ()
        | Item.CtorGroup _ -> ()
        | Item.FakeInterfaceCtor _ -> ()
        | Item.Types _ -> ()
        | Item.CustomOperation _ -> ()
        | Item.CustomBuilder _ -> ()
        | Item.ModuleOrNamespaces _ -> ()
        | Item.ImplicitOp _ -> ()
        | Item.ArgName _ -> ()
        | Item.SetterArg _ -> ()
        | Item.UnqualifiedType _ -> ()

        let postCount = b.Count

        fixup.WriteInt32(postCount - preCount)

    member _.TryBuildAndReset() =
        if b.Count > 0 then
            let length = int64 b.Count
            let mmf =
                let mmf =
                    MemoryMappedFile.CreateNew(
                        null,
                        length,
                        MemoryMappedFileAccess.ReadWrite,
                        MemoryMappedFileOptions.None,
                        HandleInheritability.None)
                use stream = mmf.CreateViewStream(0L, length, MemoryMappedFileAccess.ReadWrite)
                b.WriteContentTo stream
                mmf

            b.Clear()

            Some(new ItemKeyStore(mmf, length))
        else
            b.Clear()
            None
