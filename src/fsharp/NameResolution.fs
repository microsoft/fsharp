// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.


/// Name environment and name resolution
module internal FSharp.Compiler.NameResolution

open System.Collections.Generic

open Internal.Utilities
open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Library.ResultOrException

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.Diagnostics
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.InfoReader
open FSharp.Compiler.Infos
open FSharp.Compiler.Features
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Position
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif


/// An object that captures the logical context for name resolution.
type NameResolver(g: TcGlobals,
                  amap: Import.ImportMap,
                  infoReader: InfoReader,
                  instantiationGenerator: range -> Typars -> ITraitContext option -> TypeInst) =

    /// Used to transform typars into new inference typars
    /// instantiationGenerator is a function to help us create the
    /// type parameters by copying them from type parameter specifications read
    /// from IL code.
    ///
    /// If these includes trait constraints then the process is context dependent.
    ///
    /// When looking up items in generic types we create a fresh instantiation
    /// of the type, i.e. instantiate the type with inference variables.
    /// This means the item is returned ready for use by the type inference engine
    /// without further freshening. However it does mean we end up plumbing 'instantiationGenerator'
    /// around a bit more than we would like to, which is a bit annoying.
    member nr.InstantiationGenerator = instantiationGenerator
    member nr.g = g
    member nr.amap = amap
    member nr.InfoReader = infoReader
    member nr.languageSupportsNameOf = g.langVersion.SupportsFeature LanguageFeature.NameOf

//-------------------------------------------------------------------------
// Helpers for unionconstrs and recdfields
//-------------------------------------------------------------------------

/// Get references to all the union cases in the type definition
let UnionCaseRefsInTycon (modref: ModuleOrNamespaceRef) (tycon: Tycon) =
    tycon.UnionCasesAsList |> List.map (mkModuleUnionCaseRef modref tycon)

/// Get references to all the union cases defined in the module
let UnionCaseRefsInModuleOrNamespace (modref: ModuleOrNamespaceRef) =
    [ for x in modref.ModuleOrNamespaceType.AllEntities do yield! UnionCaseRefsInTycon modref x ]

/// Try to find a type with a union case of the given name
let TryFindTypeWithUnionCase (modref: ModuleOrNamespaceRef) (id: Ident) =
    modref.ModuleOrNamespaceType.AllEntities
    |> QueueList.tryFind (fun tycon -> tycon.GetUnionCaseByName id.idText |> Option.isSome)

/// Try to find a type with a record field of the given name
let TryFindTypeWithRecdField (modref: ModuleOrNamespaceRef) (id: Ident) =
    modref.ModuleOrNamespaceType.AllEntities
    |> QueueList.tryFind (fun tycon -> tycon.GetFieldByName id.idText |> Option.isSome)

/// Get the active pattern elements defined by a given value, if any
let ActivePatternElemsOfValRef g (vref: ValRef) =
    match TryGetActivePatternInfo vref with
    | Some apinfo ->
        
        let isStructRetTy = 
            if apinfo.IsTotal then
                false
            else
                let _, apReturnTy = stripFunTy g vref.TauType
                isStructTy g apReturnTy
        apinfo.ActiveTags |> List.mapi (fun i _ -> APElemRef(apinfo, vref, i, isStructRetTy))
    | None -> []

/// Try to make a reference to a value in a module.
//
// mkNestedValRef may fail if the assembly load set is
// incomplete and the value is an extension member of a type that is not
// available. In some cases we can reasonably recover from this, e.g. by simply not adding
// an entry to a table. Callsites have to cope with the error (None) condition
// sensibly, e.g. in a way that won't change the way things are compiled as the
// assembly set is completed.
let TryMkValRefInModRef modref vspec =
    protectAssemblyExploration
        None
        (fun () -> Some (mkNestedValRef modref vspec))

/// Get the active pattern elements defined by a given value, if any
let ActivePatternElemsOfVal g modref vspec =
    // If the assembly load set is incomplete then don't add anything to the table
    match TryMkValRefInModRef modref vspec with
    | None -> []
    | Some vref -> ActivePatternElemsOfValRef g vref

/// Get the active pattern elements defined in a module, if any. Cache in the slot in the module type.
let ActivePatternElemsOfModuleOrNamespace g (modref: ModuleOrNamespaceRef) : NameMap<ActivePatternElemRef> =
    let mtyp = modref.ModuleOrNamespaceType
    cacheOptRef mtyp.ActivePatternElemRefLookupTable (fun () ->
        mtyp.AllValsAndMembers
        |> Seq.collect (ActivePatternElemsOfVal g modref)
        |> Seq.fold (fun acc apref -> NameMap.add apref.Name apref acc) Map.empty)

//---------------------------------------------------------------------------
// Name Resolution Items
//-------------------------------------------------------------------------

/// Detect a use of a nominal type, including type abbreviations.
///
/// When reporting symbols, we care about abbreviations, e.g. 'int' and 'int32' count as two separate symbols
let (|AbbrevOrAppTy|_|) (ty: TType) =
    match stripTyparEqns ty with
    | TType_app (tcref, _) -> Some tcref
    | _ -> None

[<NoEquality; NoComparison; RequireQualifiedAccess>]
/// Represents the item with which a named argument is associated.
type ArgumentContainer =
    /// The named argument is an argument of a method
    | Method of MethInfo
    /// The named argument is a static parameter to a provided type.
    | Type of TyconRef

// Note: Active patterns are encoded like this:
//   let (|A|B|) x = if x < 0 then A else B    // A and B are reported as results using 'Item.ActivePatternResult'
//   match () with | A | B -> ()               // A and B are reported using 'Item.ActivePatternCase'

let emptyTypeInst : TypeInst = []
type EnclosingTypeInst = TypeInst
let emptyEnclosingTypeInst : EnclosingTypeInst = emptyTypeInst

[<NoEquality; NoComparison; RequireQualifiedAccess>]
/// Represents an item that results from name resolution
type Item =

    /// Represents the resolution of a name to an F# value or function.
    | Value of  ValRef

    /// Represents the resolution of a name to an F# union case.
    | UnionCase of UnionCaseInfo * hasRequireQualifiedAccessAttr: bool

    /// Represents the resolution of a name to an F# active pattern result.
    | ActivePatternResult of apinfo: ActivePatternInfo * apOverallTy: TType * index: int * range: range

    /// Represents the resolution of a name to an F# active pattern case within the body of an active pattern.
    | ActivePatternCase of ActivePatternElemRef

    /// Represents the resolution of a name to an F# exception definition.
    | ExnCase of TyconRef

    /// Represents the resolution of a name to an F# record or exception field.
    | RecdField of RecdFieldInfo

    /// Represents the resolution of a name to a union case field.
    | UnionCaseField of UnionCaseInfo * fieldIndex: int

    /// Represents the resolution of a name to a field of an anonymous record type.
    | AnonRecdField of AnonRecdTypeInfo * TTypes * int * range

    // The following are never in the items table but are valid results of binding
    // an identifier in different circumstances.

    /// Represents the resolution of a name at the point of its own definition.
    | NewDef of Ident

    /// Represents the resolution of a name to a .NET field
    | ILField of ILFieldInfo

    /// Represents the resolution of a name to an event
    | Event of EventInfo

    /// Represents the resolution of a name to a property
    | Property of string * PropInfo list

    /// Represents the resolution of a name to a group of methods.
    | MethodGroup of displayName: string * methods: MethInfo list * uninstantiatedMethodOpt: MethInfo option

    /// Represents the resolution of a name to a constructor
    | CtorGroup of string * MethInfo list

    /// Represents the resolution of a name to the fake constructor simulated for an interface type.
    | FakeInterfaceCtor of TType

    /// Represents the resolution of a name to a delegate
    | DelegateCtor of TType

    /// Represents the resolution of a name to a group of types
    | Types of string * TType list

    /// CustomOperation(nm, helpText, methInfo)
    ///
    /// Used to indicate the availability or resolution of a custom query operation such as 'sortBy' or 'where' in computation expression syntax
    | CustomOperation of string * (unit -> string option) * MethInfo option

    /// Represents the resolution of a name to a custom builder in the F# computation expression syntax
    | CustomBuilder of string * ValRef

    /// Represents the resolution of a name to a type variable
    | TypeVar of string * Typar

    /// Represents the resolution of a name to a module or namespace
    | ModuleOrNamespaces of ModuleOrNamespaceRef list

    /// Represents the resolution of a name to an operator
    | ImplicitOp of Ident * TraitConstraintSln option ref

    /// Represents the resolution of a name to a named argument
    | ArgName of Ident * TType * ArgumentContainer option

    /// Represents the resolution of a name to a named property setter
    | SetterArg of Ident * Item

    /// Represents the potential resolution of an unqualified name to a type.
    | UnqualifiedType of TyconRef list

    static member MakeMethGroup (nm, minfos: MethInfo list) =
        let minfos = minfos |> List.sortBy (fun minfo -> minfo.NumArgs |> List.sum)
        Item.MethodGroup (nm, minfos, None)

    static member MakeCtorGroup (nm, minfos: MethInfo list) =
        let minfos = minfos |> List.sortBy (fun minfo -> minfo.NumArgs |> List.sum)
        Item.CtorGroup (nm, minfos)

    member d.DisplayName =
        match d with
        | Item.Value v -> v.DisplayName
        | Item.ActivePatternCase apref -> apref.Name
        | Item.UnionCase(uinfo, _) -> DecompileOpName uinfo.UnionCase.DisplayName
        | Item.ExnCase tcref -> tcref.LogicalName
        | Item.RecdField rfinfo -> DecompileOpName rfinfo.RecdField.Name
        | Item.UnionCaseField (uci, fieldIndex) -> uci.UnionCase.GetFieldByIndex(fieldIndex).Name
        | Item.AnonRecdField (anonInfo, _tys, i, _m) -> anonInfo.SortedNames.[i]
        | Item.NewDef id -> id.idText
        | Item.ILField finfo -> finfo.FieldName
        | Item.Event einfo -> einfo.EventName
        | Item.Property(_, FSProp(_, _, Some v, _) :: _)
        | Item.Property(_, FSProp(_, _, _, Some v) :: _) -> v.DisplayName
        | Item.Property(nm, _) -> DemangleOperatorName nm
        | Item.MethodGroup(_, (FSMeth(_, _, v, _) :: _), _) -> v.DisplayName
        | Item.MethodGroup(nm, _, _) -> DemangleOperatorName nm
        | Item.CtorGroup(nm, _) -> DemangleGenericTypeName nm
        | Item.FakeInterfaceCtor (AbbrevOrAppTy tcref)
        | Item.DelegateCtor (AbbrevOrAppTy tcref) -> DemangleGenericTypeName tcref.DisplayName
        | Item.Types(nm, _) -> DemangleGenericTypeName nm
        | Item.UnqualifiedType(tcref :: _) -> tcref.DisplayName
        | Item.TypeVar (nm, _) -> nm
        | Item.ModuleOrNamespaces(modref :: _) -> modref.DemangledModuleOrNamespaceName
        | Item.ArgName (id, _, _)  -> id.idText
        | Item.SetterArg (id, _) -> id.idText
        | Item.CustomOperation (customOpName, _, _) -> customOpName
        | Item.CustomBuilder (nm, _) -> nm
        | _ ->  ""

let valRefHash (vref: ValRef) =
    match vref.TryDeref with
    | ValueNone -> 0
    | ValueSome v -> LanguagePrimitives.PhysicalHash v

[<RequireQualifiedAccess>]
/// Pairs an Item with a TyparInst showing how generic type variables of the item are instantiated at
/// a particular usage point.
type ItemWithInst =
    { Item: Item
      TyparInst: TyparInst }

let ItemWithNoInst item = ({ Item = item; TyparInst = emptyTyparInst } : ItemWithInst)

let (|ItemWithInst|) (x: ItemWithInst) = (x.Item, x.TyparInst)

/// Represents a record field resolution and the information if the usage is deprecated.
type FieldResolution = FieldResolution of RecdFieldInfo * bool

/// Information about an extension member held in the name resolution environment
type ExtensionMember =

   /// F#-style Extrinsic extension member, defined in F# code
   | FSExtMem of ValRef * ExtensionMethodPriority

   /// ILExtMem(declaringTyconRef, ilMetadata, pri)
   ///
   /// IL-style extension member, backed by some kind of method with an [<Extension>] attribute
   | ILExtMem of TyconRef * MethInfo * ExtensionMethodPriority

   /// Check if two extension members refer to the same definition
   static member Equality g e1 e2 =
       match e1, e2 with
       | FSExtMem (vref1, _), FSExtMem (vref2, _) -> valRefEq g vref1 vref2
       | ILExtMem (_, md1, _), ILExtMem (_, md2, _) -> MethInfo.MethInfosUseIdenticalDefinitions md1 md2
       | _ -> false

   static member Hash e1 =
       match e1 with
       | FSExtMem(vref, _) -> valRefHash vref
       | ILExtMem(_, m, _) ->
           match m with
           | ILMeth(_, ilmeth, _) -> LanguagePrimitives.PhysicalHash ilmeth.RawMetadata
           | FSMeth(_, _, vref, _) -> valRefHash vref
           | _ -> 0

   static member Comparer g = HashIdentity.FromFunctions ExtensionMember.Hash (ExtensionMember.Equality g)

   /// Describes the sequence order of the introduction of an extension method. Extension methods that are introduced
   /// later through 'open' get priority in overload resolution.
   member x.Priority =
       match x with
       | FSExtMem (_, pri) -> pri
       | ILExtMem (_, _, pri) -> pri

   member x.LogicalName = 
       match x with 
       | FSExtMem (vref, _) -> vref.LogicalName
       | ILExtMem (_, minfo, _) -> minfo.LogicalName

type FullyQualifiedFlag = 
    /// Only resolve full paths
    | FullyQualified
    /// Resolve any paths accessible via 'open'
    | OpenQualified

type UnqualifiedItems = LayeredMap<string, Item>

let traitCtxtNone : ITraitContext option = None

[<NoEquality; NoComparison>]
/// The environment of information used to resolve names
type NameResolutionEnv =
    { /// Display environment information for output
      eDisplayEnv: DisplayEnv

      /// Values, functions, methods and other items available by unqualified name
      eUnqualifiedItems: UnqualifiedItems

      /// Enclosing type instantiations that are associated with an unqualified type item
      eUnqualifiedEnclosingTypeInsts: TyconRefMap<EnclosingTypeInst>

      /// Data Tags and Active Pattern Tags available by unqualified name
      ePatItems: NameMap<Item>

      /// Modules accessible via "." notation. Note this is a multi-map.
      /// Adding a module abbreviation adds it a local entry to this List.map.
      /// Likewise adding a ccu or opening a path adds entries to this List.map.


      /// REVIEW (old comment)
      /// "The boolean flag is means the namespace or module entry shouldn't 'really' be in the
      ///  map, and if it is ever used to resolve a name then we give a warning.
      ///  This is used to give warnings on unqualified namespace accesses, e.g.
      ///    open System
      ///    open Collections                            <--- give a warning
      ///    let v = new Collections.Generic.List<int>() <--- give a warning"

      eModulesAndNamespaces:  NameMultiMap<ModuleOrNamespaceRef>

      /// Fully qualified modules and namespaces. 'open' does not change this.
      eFullyQualifiedModulesAndNamespaces:  NameMultiMap<ModuleOrNamespaceRef>

      /// RecdField labels in scope.  RecdField labels are those where type are inferred
      /// by label rather than by known type annotation.
      /// Bools indicate if from a record, where no warning is given on indeterminate lookup
      eFieldLabels: NameMultiMap<RecdFieldRef>

      /// Record or unions that may have type instantiations associated with them
      /// when record labels or union cases are used in an unqualified context.
      eUnqualifiedRecordOrUnionTypeInsts: TyconRefMap<TypeInst>

      /// Tycons indexed by the various names that may be used to access them, e.g.
      ///     "List" --> multiple TyconRef's for the various tycons accessible by this name.
      ///     "List`1" --> TyconRef
      eTyconsByAccessNames: LayeredMultiMap<string, TyconRef>

      eFullyQualifiedTyconsByAccessNames: LayeredMultiMap<string, TyconRef>

      /// Tycons available by unqualified, demangled names (i.e. (List, 1) --> TyconRef)
      eTyconsByDemangledNameAndArity: LayeredMap<NameArityPair, TyconRef>

      /// Tycons available by unqualified, demangled names (i.e. (List, 1) --> TyconRef)
      eFullyQualifiedTyconsByDemangledNameAndArity: LayeredMap<NameArityPair, TyconRef>

      /// Extension members by type and name
      eIndexedExtensionMembers: TyconRefMultiMap<ExtensionMember>

      /// Extension members by name  
      eExtensionMembersByName: NameMultiMap<ExtensionMember>

      /// Other extension members unindexed by type
      eUnindexedExtensionMembers: ExtensionMember list

      /// Typars (always available by unqualified names). Further typars can be
      /// in the tpenv, a structure folded through each top-level definition.
      eTypars: NameMap<Typar>

    }

    /// The initial, empty name resolution environment. The mother of all things.
    static member Empty g =
        { eDisplayEnv = DisplayEnv.Empty g
          eModulesAndNamespaces = Map.empty
          eFullyQualifiedModulesAndNamespaces = Map.empty
          eFieldLabels = Map.empty
          eUnqualifiedRecordOrUnionTypeInsts = TyconRefMap.Empty
          eUnqualifiedItems = LayeredMap.Empty
          eUnqualifiedEnclosingTypeInsts = TyconRefMap.Empty
          ePatItems = Map.empty
          eTyconsByAccessNames = LayeredMultiMap.Empty
          eTyconsByDemangledNameAndArity = LayeredMap.Empty
          eFullyQualifiedTyconsByAccessNames = LayeredMultiMap.Empty
          eFullyQualifiedTyconsByDemangledNameAndArity = LayeredMap.Empty
          eIndexedExtensionMembers = TyconRefMultiMap<_>.Empty
          eExtensionMembersByName = NameMultiMap<_>.Empty
          eUnindexedExtensionMembers = []
          eTypars = Map.empty }

    member nenv.DisplayEnv = nenv.eDisplayEnv

    member nenv.FindUnqualifiedItem nm = nenv.eUnqualifiedItems.[nm]

    /// Get the table of types, indexed by name and arity
    member nenv.TyconsByDemangledNameAndArity fq =
        match fq with
        | FullyQualified -> nenv.eFullyQualifiedTyconsByDemangledNameAndArity
        | OpenQualified  -> nenv.eTyconsByDemangledNameAndArity

    /// Get the table of types, indexed by name
    member nenv.TyconsByAccessNames fq =
        match fq with
        | FullyQualified -> nenv.eFullyQualifiedTyconsByAccessNames
        | OpenQualified  -> nenv.eTyconsByAccessNames

    /// Get the table of modules and namespaces
    member nenv.ModulesAndNamespaces fq =
        match fq with
        | FullyQualified -> nenv.eFullyQualifiedModulesAndNamespaces
        | OpenQualified -> nenv.eModulesAndNamespaces

//-------------------------------------------------------------------------
// Helpers to do with extension members
//-------------------------------------------------------------------------

/// Indicates if we only need one result or all possible results from a resolution.
[<RequireQualifiedAccess>]
type ResultCollectionSettings =
    | AllResults
    | AtMostOneResult

/// Allocate the next extension method priority. This is an incrementing sequence of integers
/// during type checking.
let NextExtensionMethodPriority() = uint64 (newStamp())

/// Checks if the type is used for C# style extension members.
let IsTyconRefUsedForCSharpStyleExtensionMembers g m (tcref: TyconRef) =
    // Type must be non-generic and have 'Extension' attribute
    isNil(tcref.Typars m) && TyconRefHasAttribute g m g.attrib_ExtensionAttribute tcref

/// Checks if the type is used for C# style extension members.
let IsTypeUsedForCSharpStyleExtensionMembers g m ty =
    match tryTcrefOfAppTy g ty with
    | ValueSome tcref -> IsTyconRefUsedForCSharpStyleExtensionMembers g m tcref
    | _ -> false

/// A 'plain' method is an extension method not interpreted as an extension method.
let IsMethInfoPlainCSharpStyleExtensionMember g m isEnclExtTy (minfo: MethInfo) =
    // Method must be static, have 'Extension' attribute, must not be curried, must have at least one argument
    isEnclExtTy &&
    not minfo.IsInstance &&
    not minfo.IsExtensionMember &&
    (match minfo.NumArgs with [x] when x >= 1 -> true | _ -> false) &&
    MethInfoHasAttribute g m g.attrib_ExtensionAttribute minfo

/// Get the info for all the .NET-style extension members listed as static members in the type.
let private GetCSharpStyleIndexedExtensionMembersForTyconRef (amap: Import.ImportMap) m  (tcrefOfStaticClass: TyconRef) =
    let g = amap.g

    if IsTyconRefUsedForCSharpStyleExtensionMembers g m tcrefOfStaticClass then
        let pri = NextExtensionMethodPriority()
        let ty = generalizedTyconRef tcrefOfStaticClass

        let minfos = GetImmediateIntrinsicMethInfosOfType (None, AccessorDomain.AccessibleFromSomeFSharpCode) g amap m ty
        [ for minfo in minfos do
            if IsMethInfoPlainCSharpStyleExtensionMember g m true minfo then
                let ilExtMem = ILExtMem (tcrefOfStaticClass, minfo, pri)

                // The results are indexed by the TyconRef of the first 'this' argument, if any.
                // So we need to go and crack the type of the 'this' argument.
                //
                // This is convoluted because we only need the ILTypeRef of the first argument, and we don't
                // want to read any other metadata as it can trigger missing-assembly errors. It turns out ImportILTypeRef
                // is less eager in reading metadata than GetParamTypes.
                //
                // We don't use the index for the IL extension method for tuple of F# function types (e.g. if extension
                // methods for tuple occur in C# code)
                let thisTyconRef =
                 try
                    let rs =
                        match metadataOfTycon tcrefOfStaticClass.Deref, minfo with
                        | ILTypeMetadata (TILObjectReprData(scoref, _, _)), ILMeth(_, ILMethInfo(_, _, _, ilMethod, _), _) ->
                            match ilMethod.ParameterTypes with
                            | firstTy :: _ ->
                                match firstTy with
                                | ILType.Boxed  tspec | ILType.Value tspec ->
                                    let tref = (tspec |> rescopeILTypeSpec scoref).TypeRef
                                    if Import.CanImportILTypeRef amap m tref then
                                        let tcref = tref |> Import.ImportILTypeRef amap m
                                        if isCompiledTupleTyconRef g tcref || tyconRefEq g tcref g.fastFunc_tcr then None
                                        else Some tcref
                                    else None
                                | _ -> None
                            | _ -> None
                        | _ ->
                            // The results are indexed by the TyconRef of the first 'this' argument, if any.
                            // So we need to go and crack the type of the 'this' argument.
                            let thisTy = minfo.GetParamTypes(amap, m, generalizeTypars minfo.FormalMethodTypars).Head.Head
                            match thisTy with
                            | AppTy g (tcrefOfTypeExtended, _) when not (isByrefTy g thisTy) -> Some tcrefOfTypeExtended
                            | _ -> None

                    Some rs

                  with e -> // Import of the ILType may fail, if so report the error and skip on
                    errorRecovery e m
                    None

                match thisTyconRef with
                | None -> ()
                | Some (Some tcref) -> yield Choice1Of2(tcref, ilExtMem)
                | Some None -> yield Choice2Of2 ilExtMem ]
    else
        []


/// Query the declared properties of a type (including inherited properties)
let IntrinsicPropInfosOfTypeInScope (infoReader: InfoReader) optFilter ad findFlag m ty =
    let g = infoReader.g
    let amap = infoReader.amap
    let pinfos = GetIntrinsicPropInfoSetsOfType infoReader optFilter ad AllowMultiIntfInstantiations.Yes findFlag m ty
    let pinfos = pinfos |> ExcludeHiddenOfPropInfos g amap m
    pinfos

/// Select from a list of extension properties
let SelectPropInfosFromExtMembers (infoReader: InfoReader) ad optFilter declaringTy m extMemInfos =
    let g = infoReader.g
    let amap = infoReader.amap
    // NOTE: multiple "open"'s push multiple duplicate values into eIndexedExtensionMembers, hence use a set.
    let seen = HashSet(ExtensionMember.Comparer g)
    let propCollector = new PropertyCollector(g, amap, m, declaringTy, optFilter, ad)
    for emem in extMemInfos do
        if seen.Add emem then
            match emem with
            | FSExtMem (vref, _pri) ->
                match vref.MemberInfo with
                | None -> ()
                | Some membInfo -> propCollector.Collect(membInfo, vref)
            | ILExtMem _ ->
                // No extension properties coming from .NET
                ()
    propCollector.Close()

/// Query the available extension properties of a type (including extension properties for inherited types)
let ExtensionPropInfosOfTypeInScope collectionSettings (infoReader:InfoReader) (nenv: NameResolutionEnv) optFilter ad m ty =
    let g = infoReader.g

    let extMemsDangling = SelectPropInfosFromExtMembers infoReader ad optFilter ty m nenv.eUnindexedExtensionMembers 

    if collectionSettings = ResultCollectionSettings.AtMostOneResult && not (isNil extMemsDangling) then 
        extMemsDangling 
    else
        let extMemsFromHierarchy =
            infoReader.GetEntireTypeHierarchy(AllowMultiIntfInstantiations.Yes, m, ty)
            |> List.collect (fun ty ->
                 match tryTcrefOfAppTy g ty with
                 | ValueSome tcref ->
                    let extMemInfos = nenv.eIndexedExtensionMembers.Find tcref
                    SelectPropInfosFromExtMembers infoReader ad optFilter ty m extMemInfos
                 | _ -> [])

        extMemsDangling @ extMemsFromHierarchy

/// Get all the available properties of a type (both intrinsic and extension)
let AllPropInfosOfTypeInScope collectionSettings infoReader nenv optFilter ad findFlag m ty =
    IntrinsicPropInfosOfTypeInScope infoReader optFilter ad findFlag m ty
    @ ExtensionPropInfosOfTypeInScope collectionSettings infoReader nenv optFilter ad m ty

/// Get the available methods of a type (both declared and inherited)
let IntrinsicMethInfosOfType (infoReader: InfoReader) optFilter ad allowMultiIntfInst findFlag m ty =
    let g = infoReader.g
    let amap = infoReader.amap
    let minfos = GetIntrinsicMethInfoSetsOfType infoReader optFilter ad allowMultiIntfInst findFlag m ty
    let minfos = minfos |> ExcludeHiddenOfMethInfos g amap m
    minfos

let TrySelectExtensionMethInfoOfILExtMem m amap apparentTy (actualParent, minfo, pri) = 
    match minfo with 
    | ILMeth(_,ilminfo,_) -> 
        MethInfo.CreateILExtensionMeth (amap, m, apparentTy, actualParent, Some pri, ilminfo.RawMetadata) |> Some
    // F#-defined IL-style extension methods are not seen as extension methods in F# code
    | FSMeth(g,_,vref,_) -> 
        FSMeth(g, apparentTy, vref, Some pri) |> Some
#if !NO_EXTENSIONTYPING
    // // Provided extension methods are not yet supported
    | ProvidedMeth(amap,providedMeth,_,m) -> 
        ProvidedMeth(amap, providedMeth, Some pri,m) |> Some
#endif
    | DefaultStructCtor _ -> 
        None

/// Select from a list of extension methods
let SelectMethInfosFromExtMembers (infoReader: InfoReader) optFilter apparentTy m extMemInfos =
    let g = infoReader.g
    // NOTE: multiple "open"'s push multiple duplicate values into eIndexedExtensionMembers
    let seen = HashSet(ExtensionMember.Comparer g)
    [
        for emem in extMemInfos do
            if seen.Add emem then
                match emem with
                | FSExtMem (vref, pri) ->
                    match vref.MemberInfo with
                    | None -> ()
                    | Some membInfo ->
                        match TrySelectMemberVal g optFilter apparentTy (Some pri) membInfo vref with
                        | Some m -> yield m
                        | _ -> ()
                | ILExtMem (actualParent, minfo, pri) when (match optFilter with None -> true | Some nm -> nm = minfo.LogicalName) ->
                    // Make a reference to the type containing the extension members
                    match TrySelectExtensionMethInfoOfILExtMem m infoReader.amap apparentTy (actualParent, minfo, pri) with 
                    | Some minfo -> yield minfo
                    | None -> ()
                | _ -> ()
    ]

/// Query the available extension properties of a methods (including extension methods for inherited types)
let ExtensionMethInfosOfTypeInScope (collectionSettings: ResultCollectionSettings) (infoReader: InfoReader) (nenv: NameResolutionEnv) optFilter m ty =
    let extMemsDangling = SelectMethInfosFromExtMembers  infoReader optFilter ty  m nenv.eUnindexedExtensionMembers
    if collectionSettings = ResultCollectionSettings.AtMostOneResult && not (isNil extMemsDangling) then 
        extMemsDangling
    else
        let extMemsFromHierarchy =
            infoReader.GetEntireTypeHierarchy(AllowMultiIntfInstantiations.Yes, m, ty)
            |> List.collect (fun ty ->
                let g = infoReader.g
                match tryTcrefOfAppTy g ty with
                | ValueSome tcref ->
                    let extValRefs = nenv.eIndexedExtensionMembers.Find tcref
                    SelectMethInfosFromExtMembers infoReader optFilter ty  m extValRefs
                | _ -> [])
        extMemsDangling @ extMemsFromHierarchy

/// Get all the available methods of a type (both intrinsic and extension)
let AllMethInfosOfTypeInScope collectionSettings infoReader nenv optFilter ad findFlag m ty =
    let intrinsic = IntrinsicMethInfosOfType infoReader optFilter ad AllowMultiIntfInstantiations.Yes findFlag m ty
    if collectionSettings = ResultCollectionSettings.AtMostOneResult && not (isNil intrinsic) then 
        intrinsic
    else
        intrinsic @ ExtensionMethInfosOfTypeInScope collectionSettings infoReader nenv optFilter m ty

let SelectExtensionMethInfosForTrait(traitInfo: TraitConstraintInfo, m, nenv: NameResolutionEnv, infoReader: InfoReader) =
    let g = infoReader.g
    if g.langVersion.SupportsFeature LanguageFeature.ExtensionConstraintSolutions then
        [ for traitSupportTy in traitInfo.SupportTypes do
            if not (isTyparTy g traitSupportTy) then 
                let extMethInfos = ExtensionMethInfosOfTypeInScope ResultCollectionSettings.AllResults infoReader nenv (Some traitInfo.MemberName) m traitSupportTy
                for extMethInfo in extMethInfos do
                    yield extMethInfo ]
    else
        []
//-------------------------------------------------------------------------
// Helpers to do with building environments
//-------------------------------------------------------------------------

/// For the operations that build the overall name resolution
/// tables, BulkAdd.Yes is set to true when "opening" a
/// namespace. If BulkAdd is true then add-and-collapse
/// is used for the backing maps.Multiple "open" operations are
/// thus coalesced, and the first subsequent lookup after a sequence
/// of opens will collapse the maps and build the backing dictionary.
[<RequireQualifiedAccess>]
type BulkAdd = Yes | No


/// bulkAddMode: true when adding the values from the 'open' of a namespace
/// or module, when we collapse the value table down to a dictionary.
let AddValRefsToItems (bulkAddMode: BulkAdd) (eUnqualifiedItems: UnqualifiedItems) (vrefs: ValRef[]) =
    // Object model members are not added to the unqualified name resolution environment
    let vrefs = vrefs |> Array.filter (fun vref -> not vref.IsMember)

    if vrefs.Length = 0 then eUnqualifiedItems else

    match bulkAddMode with
    | BulkAdd.Yes ->
        eUnqualifiedItems.AddAndMarkAsCollapsible(vrefs |> Array.map (fun vref -> KeyValuePair(vref.LogicalName, Item.Value vref)))
    | BulkAdd.No ->
        assert (vrefs.Length = 1)
        let vref = vrefs.[0]
        eUnqualifiedItems.Add (vref.LogicalName, Item.Value vref)

/// Add an F# value to the table of available extension members, if necessary, as an FSharp-style extension member
let AddValRefToExtensionMembers pri (eIndexedExtensionMembers: TyconRefMultiMap<_>) (vref: ValRef) =
    if vref.IsMember && vref.IsExtensionMember then
        eIndexedExtensionMembers.Add (vref.MemberApparentEntity, FSExtMem (vref, pri))
    else
        eIndexedExtensionMembers

/// Add an F# value to the table of available extension members, if necessary, as an FSharp-style extension member
let AddValRefToExtensionMembersByNameTable logicalName (eExtensionMembersByName: NameMultiMap<_>) extMemInfo =
    NameMultiMap.add logicalName extMemInfo eExtensionMembersByName

/// Add an F# value to the table of available extension members, if necessary, as an FSharp-style extension member
let AddValRefToExtensionMembersByName pri (eExtensionMembersByName: NameMultiMap<_>) (vref:ValRef) =
    if vref.IsMember && vref.IsExtensionMember then
        AddValRefToExtensionMembersByNameTable vref.LogicalName eExtensionMembersByName (FSExtMem (vref,pri))
    else
        eExtensionMembersByName


/// This entry point is used to add some extra items to the environment for Visual Studio, e.g. static members
let AddFakeNamedValRefToNameEnv nm nenv vref =
    {nenv with eUnqualifiedItems = nenv.eUnqualifiedItems.Add (nm, Item.Value vref) }

/// This entry point is used to add some extra items to the environment for Visual Studio, e.g. record members
let AddFakeNameToNameEnv nm nenv item =
    {nenv with eUnqualifiedItems = nenv.eUnqualifiedItems.Add (nm, item) }

/// Add an F# value to the table of available active patterns
let AddValRefsToActivePatternsNameEnv g ePatItems (vref: ValRef) =
    let ePatItems =
        (ActivePatternElemsOfValRef g vref, ePatItems)
        ||> List.foldBack (fun apref tab ->
            NameMap.add apref.Name (Item.ActivePatternCase apref) tab)

    // Add literal constants to the environment available for resolving items in patterns
    let ePatItems =
        match vref.LiteralValue with
        | None -> ePatItems
        | Some _ -> NameMap.add vref.LogicalName (Item.Value vref) ePatItems

    ePatItems

/// Add a set of F# values to the environment.
let AddValRefsToNameEnvWithPriority g bulkAddMode pri nenv (vrefs: ValRef []) =
    if vrefs.Length = 0 then nenv else
    { nenv with
        eUnqualifiedItems = AddValRefsToItems bulkAddMode nenv.eUnqualifiedItems vrefs
        eIndexedExtensionMembers = (nenv.eIndexedExtensionMembers, vrefs) ||> Array.fold (AddValRefToExtensionMembers pri)
        eExtensionMembersByName = (nenv.eExtensionMembersByName, vrefs) ||> Array.fold (AddValRefToExtensionMembersByName pri)
        ePatItems = (nenv.ePatItems, vrefs) ||> Array.fold (AddValRefsToActivePatternsNameEnv g) }

/// Add a single F# value to the environment.
let AddValRefToNameEnv g nenv (vref: ValRef) =
    let pri = NextExtensionMethodPriority()
    { nenv with
        eUnqualifiedItems =
            if not vref.IsMember then
                nenv.eUnqualifiedItems.Add (vref.LogicalName, Item.Value vref)
            else
                nenv.eUnqualifiedItems
        eIndexedExtensionMembers = AddValRefToExtensionMembers pri nenv.eIndexedExtensionMembers vref
        eExtensionMembersByName = AddValRefToExtensionMembersByName pri nenv.eExtensionMembersByName vref
        ePatItems = AddValRefsToActivePatternsNameEnv g nenv.ePatItems vref }


/// Add a set of active pattern result tags to the environment.
let AddActivePatternResultTagsToNameEnv (apinfo: ActivePatternInfo) nenv apOverallTy m =
    if List.isEmpty apinfo.Names then nenv else
    let apResultNameList = List.indexed apinfo.Names
    { nenv with
        eUnqualifiedItems =
            (apResultNameList, nenv.eUnqualifiedItems)
            ||> List.foldBack (fun (j, nm) acc -> acc.Add(nm, Item.ActivePatternResult(apinfo, apOverallTy, j, m))) }

/// Generalize a union case, from Cons --> List<T>.Cons
let GeneralizeUnionCaseRef (ucref: UnionCaseRef) =
    UnionCaseInfo (fst (generalizeTyconRef ucref.TyconRef), ucref)


/// Add type definitions to the sub-table of the environment indexed by name and arity
let AddTyconsByDemangledNameAndArity (bulkAddMode: BulkAdd) (tcrefs: TyconRef[]) (tab: LayeredMap<NameArityPair, TyconRef>) =
    if tcrefs.Length = 0 then tab else
    let entries =
        tcrefs
        |> Array.map (fun tcref -> Construct.KeyTyconByDecodedName tcref.LogicalName tcref)

    match bulkAddMode with
    | BulkAdd.Yes -> tab.AddAndMarkAsCollapsible entries
    | BulkAdd.No -> (tab, entries) ||> Array.fold (fun tab (KeyValue(k, v)) -> tab.Add(k, v))

/// Add type definitions to the sub-table of the environment indexed by access name
let AddTyconByAccessNames bulkAddMode (tcrefs: TyconRef[]) (tab: LayeredMultiMap<string, _>) =
    if tcrefs.Length = 0 then tab else
    let entries =
        tcrefs
        |> Array.collect (fun tcref -> Construct.KeyTyconByAccessNames tcref.LogicalName tcref)

    match bulkAddMode with
    | BulkAdd.Yes -> tab.AddAndMarkAsCollapsible entries
    | BulkAdd.No -> (tab, entries) ||> Array.fold (fun tab (KeyValue(k, v)) -> tab.Add (k, v))

/// Add a record field to the corresponding sub-table of the name resolution environment
let AddRecdField (rfref: RecdFieldRef) tab = NameMultiMap.add rfref.FieldName rfref tab

/// Add a set of union cases to the corresponding sub-table of the environment
let AddUnionCases1 (tab: Map<_, _>) (ucrefs: UnionCaseRef list) =
    (tab, ucrefs) ||> List.fold (fun acc ucref ->
        let item = Item.UnionCase(GeneralizeUnionCaseRef ucref, false)
        acc.Add (ucref.CaseName, item))

/// Add a set of union cases to the corresponding sub-table of the environment
let AddUnionCases2 bulkAddMode (eUnqualifiedItems: UnqualifiedItems) (ucrefs: UnionCaseRef list) =
    match bulkAddMode with
    | BulkAdd.Yes ->
        let items =
            ucrefs |> Array.ofList |> Array.map (fun ucref ->
                let item = Item.UnionCase(GeneralizeUnionCaseRef ucref, false)
                KeyValuePair(ucref.CaseName, item))
        eUnqualifiedItems.AddAndMarkAsCollapsible items

    | BulkAdd.No ->
        (eUnqualifiedItems, ucrefs) ||> List.fold (fun acc ucref ->
            let item = Item.UnionCase(GeneralizeUnionCaseRef ucref, false)
            acc.Add (ucref.CaseName, item))

//-------------------------------------------------------------------------
// TypeNameResolutionInfo
//-------------------------------------------------------------------------

/// Indicates whether we are resolving type names to type definitions or to constructor methods.
type TypeNameResolutionFlag =
    | ResolveTypeNamesToCtors
    | ResolveTypeNamesToTypeRefs

[<RequireQualifiedAccess>]
[<NoEquality; NoComparison>]
/// Represents information about the generic argument count of a type name when resolving it.
///
/// In some situations we resolve "List" to any type definition with that name regardless of the number
/// of generic arguments. In others, we know precisely how many generic arguments are needed.
type TypeNameResolutionStaticArgsInfo =
    /// Indicates indefinite knowledge of type arguments
    | Indefinite
    /// Indicates definite knowledge of type arguments
    | Definite of int

    /// Indicates definite knowledge of empty type arguments
    static member DefiniteEmpty = TypeNameResolutionStaticArgsInfo.Definite 0

    static member FromTyArgs (numTyArgs: int) = TypeNameResolutionStaticArgsInfo.Definite numTyArgs

    member x.HasNoStaticArgsInfo = match x with TypeNameResolutionStaticArgsInfo.Indefinite -> true | _-> false

    member x.NumStaticArgs = match x with TypeNameResolutionStaticArgsInfo.Indefinite -> 0 | TypeNameResolutionStaticArgsInfo.Definite n -> n

    // Get the first possible mangled name of the type, assuming the args are generic args
    member x.MangledNameForType nm =
        if x.NumStaticArgs = 0 || TryDemangleGenericNameAndPos nm <> ValueNone then nm
        else nm + "`" + string x.NumStaticArgs

[<NoEquality; NoComparison>]
/// Represents information which guides name resolution of types.
type TypeNameResolutionInfo =
    | TypeNameResolutionInfo of TypeNameResolutionFlag * TypeNameResolutionStaticArgsInfo

    static member Default = TypeNameResolutionInfo (ResolveTypeNamesToCtors, TypeNameResolutionStaticArgsInfo.Indefinite)
    static member ResolveToTypeRefs statResInfo = TypeNameResolutionInfo (ResolveTypeNamesToTypeRefs, statResInfo)
    member x.StaticArgsInfo = match x with TypeNameResolutionInfo(_, staticResInfo) -> staticResInfo
    member x.ResolutionFlag = match x with TypeNameResolutionInfo(flag, _) -> flag
    member x.DropStaticArgsInfo = match x with TypeNameResolutionInfo(flag2, _) -> TypeNameResolutionInfo(flag2, TypeNameResolutionStaticArgsInfo.Indefinite)

/// A flag which indicates if direct references to generated provided types are allowed. Normally these
/// are disallowed.
[<RequireQualifiedAccess>]
type PermitDirectReferenceToGeneratedType =
    | Yes
    | No

#if !NO_EXTENSIONTYPING

/// Check for direct references to generated provided types.
let CheckForDirectReferenceToGeneratedType (tcref: TyconRef, genOk, m) =
  match genOk with
  | PermitDirectReferenceToGeneratedType.Yes -> ()
  | PermitDirectReferenceToGeneratedType.No ->
    match tcref.TypeReprInfo with
    | TProvidedTypeExtensionPoint info when not info.IsErased ->
        if ExtensionTyping.IsGeneratedTypeDirectReference (info.ProvidedType, m) then
            error (Error(FSComp.SR.etDirectReferenceToGeneratedTypeNotAllowed(tcref.DisplayName), m))
    |  _ -> ()

/// This adds a new entity for a lazily discovered provided type into the TAST structure.
let AddEntityForProvidedType (amap: Import.ImportMap, modref: ModuleOrNamespaceRef, resolutionEnvironment, st: Tainted<ProvidedType>, m) =
    let importProvidedType t = Import.ImportProvidedType amap m t
    let isSuppressRelocate = amap.g.isInteractive || st.PUntaint((fun st -> st.IsSuppressRelocate), m)
    let tycon = Construct.NewProvidedTycon(resolutionEnvironment, st, importProvidedType, isSuppressRelocate, m)
    modref.ModuleOrNamespaceType.AddProvidedTypeEntity tycon
    let tcref = modref.NestedTyconRef tycon
    System.Diagnostics.Debug.Assert(modref.TryDeref.IsSome)
    tcref


/// Given a provided type or provided namespace, resolve the type name using the type provider API.
/// If necessary, incorporate the provided type or namespace into the entity.
let ResolveProvidedTypeNameInEntity (amap, m, typeName, modref: ModuleOrNamespaceRef) =
    match modref.TypeReprInfo with
    | TProvidedNamespaceExtensionPoint(resolutionEnvironment, resolvers) ->
        match modref.Deref.PublicPath with
        | Some(PubPath path) ->
            resolvers
            |> List.choose (fun r-> ExtensionTyping.TryResolveProvidedType(r, m, path, typeName))
            |> List.map (fun st -> AddEntityForProvidedType (amap, modref, resolutionEnvironment, st, m))
        | None -> []

    // We have a provided type, look up its nested types (populating them on-demand if necessary)
    | TProvidedTypeExtensionPoint info ->
        let sty = info.ProvidedType
        let resolutionEnvironment = info.ResolutionEnvironment

#if DEBUG
        if resolutionEnvironment.showResolutionMessages then
            dprintfn "resolving name '%s' in TProvidedTypeExtensionPoint '%s'" typeName (sty.PUntaint((fun sty -> sty.FullName), m))
#endif

        match sty.PApply((fun sty -> sty.GetNestedType typeName), m) with
        | Tainted.Null ->
            //if staticResInfo.NumStaticArgs > 0 then
            //    error(Error(FSComp.SR.etNestedProvidedTypesDoNotTakeStaticArgumentsOrGenericParameters(), m))
            []
        | nestedSty ->
            [AddEntityForProvidedType (amap, modref, resolutionEnvironment, nestedSty, m) ]
    | _ -> []
#endif

//-------------------------------------------------------------------------
// Resolve (possibly mangled) type names in entity
//-------------------------------------------------------------------------

/// Qualified lookups of type names where the number of generic arguments is known
/// from context, e.g. Module.Type<args>.  The full names suh as ``List`1`` can
/// be used to qualify access if needed
let LookupTypeNameInEntityHaveArity nm (staticResInfo: TypeNameResolutionStaticArgsInfo) (mty: ModuleOrNamespaceType) =
    let attempt1 = mty.TypesByMangledName.TryFind (staticResInfo.MangledNameForType nm)
    match attempt1 with
    | None -> mty.TypesByMangledName.TryFind nm
    | _ -> attempt1

/// Implements unqualified lookups of type names where the number of generic arguments is NOT known
/// from context.
//
// This is used in five places:
//     -  static member lookups, e.g. MyType.StaticMember(3)
//     -                         e.g. MyModule.MyType.StaticMember(3)
//     -  type-qualified field names, e.g. { RecordType.field = 3 }
//     -  type-qualified constructor names, e.g. match x with UnionType.A -> 3
//     -  identifiers to constructors for better error messages, e.g. 'String(3)' after 'open System'
//     -  the special single-constructor rule in TcTyconCores
//
// Because of the potential ambiguity multiple results can be returned.
// Explicit type annotations can be added where needed to specify the generic arity.
//
// In theory the full names such as ``RecordType`1`` can
// also be used to qualify access if needed, though this is almost never needed.
let LookupTypeNameNoArity nm (byDemangledNameAndArity: LayeredMap<NameArityPair, _>) (byAccessNames: LayeredMultiMap<string, _>) =
    match TryDemangleGenericNameAndPos nm with
    | ValueSome pos ->
        let demangled = DecodeGenericTypeNameWithPos pos nm
        match byDemangledNameAndArity.TryGetValue demangled with
        | true, res -> [res]
        | _ ->
            match byAccessNames.TryGetValue nm with
            | true, res -> res
            | _ -> []
    | _ ->
        byAccessNames.[nm]

/// Qualified lookup of type names in an entity
let LookupTypeNameInEntityNoArity _m nm (mtyp: ModuleOrNamespaceType) =
    LookupTypeNameNoArity nm mtyp.TypesByDemangledNameAndArity mtyp.TypesByAccessNames

/// Lookup a type name in an entity.
let LookupTypeNameInEntityMaybeHaveArity (amap, m, ad, nm, staticResInfo: TypeNameResolutionStaticArgsInfo, modref: ModuleOrNamespaceRef) =
    let mtyp = modref.ModuleOrNamespaceType
    let tcrefs =
        match staticResInfo with
        | TypeNameResolutionStaticArgsInfo.Indefinite ->
            LookupTypeNameInEntityNoArity m nm mtyp
            |> List.map modref.NestedTyconRef
        | TypeNameResolutionStaticArgsInfo.Definite _ ->
            match LookupTypeNameInEntityHaveArity nm staticResInfo mtyp with
            | Some tycon -> [modref.NestedTyconRef tycon]
            | None -> []
#if !NO_EXTENSIONTYPING
    let tcrefs =
        match tcrefs with
        | [] -> ResolveProvidedTypeNameInEntity (amap, m, nm, modref)
        | _ -> tcrefs
#else
    amap |> ignore
#endif
    let tcrefs = tcrefs |> List.filter (IsEntityAccessible amap m ad)
    tcrefs

/// Get all the accessible nested types of an existing type.
let GetNestedTyconRefsOfType (infoReader: InfoReader) (amap: Import.ImportMap) (ad, optFilter, staticResInfo, checkForGenerated, m) ty =
    let g = amap.g
    argsOfAppTy g ty,
    infoReader.GetPrimaryTypeHierarchy(AllowMultiIntfInstantiations.Yes, m, ty) |> List.collect (fun ty ->
        match ty with
        | AppTy g (tcref, _) ->
            let tycon = tcref.Deref
            let mty = tycon.ModuleOrNamespaceType
            // No dotting through type generators to get to a nested type!
#if !NO_EXTENSIONTYPING
            if checkForGenerated then
                CheckForDirectReferenceToGeneratedType (tcref, PermitDirectReferenceToGeneratedType.No, m)
#else
            checkForGenerated |> ignore
#endif

            match optFilter with
            | Some nm ->
                LookupTypeNameInEntityMaybeHaveArity (amap, m, ad, nm, staticResInfo, tcref)
            | None ->
#if !NO_EXTENSIONTYPING
                match tycon.TypeReprInfo with
                | TProvidedTypeExtensionPoint info ->
                    [ for nestedType in info.ProvidedType.PApplyArray((fun sty -> sty.GetNestedTypes()), "GetNestedTypes", m) do
                        let nestedTypeName = nestedType.PUntaint((fun t -> t.Name), m)
                        yield! 
                            LookupTypeNameInEntityMaybeHaveArity (amap, m, ad, nestedTypeName, staticResInfo, tcref) ]

                | _ ->
#endif
                    mty.TypesByAccessNames.Values
                    |> List.choose (fun entity ->
                        let tcref = tcref.NestedTyconRef entity
                        if IsEntityAccessible amap m ad tcref then Some tcref else None)
        | _ -> [])

/// Make a type that refers to a nested type.
///
/// Handle the .NET/C# business where nested generic types implicitly accumulate the type parameters
/// from their enclosing types.
let MakeNestedType (ncenv: NameResolver) traitCtxt (tinst: TType list) m (tcrefNested: TyconRef) =
    let tps = List.skip tinst.Length (tcrefNested.Typars m)
    let tinstNested = ncenv.InstantiationGenerator m tps traitCtxt
    mkAppTy tcrefNested (tinst @ tinstNested)

/// Get all the accessible nested types of an existing type.
let GetNestedTypesOfType (ncenv: NameResolver, ad, traitCtxt, optFilter, staticResInfo, checkForGenerated, m) ty =
    let tinst, tcrefsNested = GetNestedTyconRefsOfType ncenv.InfoReader ncenv.amap (ad, optFilter, staticResInfo, checkForGenerated, m) ty
    tcrefsNested
    |> List.map (MakeNestedType ncenv traitCtxt tinst m)

let ChooseMethInfosForNameEnv g m ty (minfos: MethInfo list) =
    let isExtTy = IsTypeUsedForCSharpStyleExtensionMembers g m ty

    minfos
    |> List.filter (fun minfo ->
        not (minfo.IsInstance || minfo.IsClassConstructor || minfo.IsConstructor) && typeEquiv g minfo.ApparentEnclosingType ty &&
        not (IsMethInfoPlainCSharpStyleExtensionMember g m isExtTy minfo) &&
        not (IsMangledOpName minfo.LogicalName))
    |> List.groupBy (fun minfo -> minfo.LogicalName)
    |> List.filter (fun (_, methGroup) -> not methGroup.IsEmpty)
    |> List.map (fun (methName, methGroup) -> KeyValuePair(methName, Item.MethodGroup(methName, methGroup, None)))

let ChoosePropInfosForNameEnv g ty (pinfos: PropInfo list) =
    pinfos
    |> List.filter (fun pinfo ->
        pinfo.IsStatic && typeEquiv g pinfo.ApparentEnclosingType ty)
    |> List.groupBy (fun pinfo -> pinfo.PropertyName)
    |> List.filter (fun (_, propGroup) -> not propGroup.IsEmpty)
    |> List.map (fun (propName, propGroup) -> KeyValuePair(propName, Item.Property(propName, propGroup)))

let ChooseFSharpFieldInfosForNameEnv g ty (rfinfos: RecdFieldInfo list) =
    rfinfos
    |> List.filter (fun rfinfo -> rfinfo.IsStatic && typeEquiv g rfinfo.DeclaringType ty)
    |> List.map (fun rfinfo -> KeyValuePair(rfinfo.Name, Item.RecdField rfinfo))

let ChooseILFieldInfosForNameEnv g ty (finfos: ILFieldInfo list) =
    finfos
    |> List.filter (fun finfo -> finfo.IsStatic && typeEquiv g finfo.ApparentEnclosingType ty)
    |> List.map (fun finfo -> KeyValuePair(finfo.FieldName, Item.ILField finfo))

let ChooseEventInfosForNameEnv g ty (einfos: EventInfo list) =
    einfos
    |> List.filter (fun einfo -> einfo.IsStatic && typeEquiv g einfo.ApparentEnclosingType ty)
    |> List.map (fun einfo -> KeyValuePair(einfo.EventName, Item.Event einfo))

/// Add static content from a type.
/// Rules:
///     1. Add nested types - access to their constructors.
///     2. Add static parts of type - i.e. C# style extension members, record labels, and union cases.
///     3. Add static extention methods.
///     4. Add static extension properties.
///     5. Add static events.
///     6. Add static fields.
///     7. Add static properies.
///     8. Add static methods and combine extension methods of the same group.
let rec AddStaticContentOfTypeToNameEnv (g:TcGlobals) (amap: Import.ImportMap) ad m (nenv: NameResolutionEnv) (ty: TType) =
    let infoReader = InfoReader(g,amap)

    let nenv = AddNestedTypesOfTypeToNameEnv infoReader amap ad m nenv ty
    let nenv = AddStaticPartsOfTypeToNameEnv amap m nenv ty

    // The order of items matter such as intrinsic members will always be favored over extension members of the same name.
    // Extension property members will always be favored over extenion methods of the same name.
    let items =
        [| 
            // Extension methods
            yield! 
                ExtensionMethInfosOfTypeInScope ResultCollectionSettings.AllResults infoReader nenv None m ty
                |> ChooseMethInfosForNameEnv g m ty

            // Extension properties
            yield!
                ExtensionPropInfosOfTypeInScope ResultCollectionSettings.AllResults infoReader nenv None ad m ty
                |> ChoosePropInfosForNameEnv g ty

            // Events
            yield!
                infoReader.GetEventInfosOfType(None, ad, m, ty)
                |> ChooseEventInfosForNameEnv g ty

            // FSharp fields
            yield!
                infoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
                |> ChooseFSharpFieldInfosForNameEnv g ty

            // IL fields
            yield!
                infoReader.GetILFieldInfosOfType(None, ad, m, ty)
                |> ChooseILFieldInfosForNameEnv g ty

            // Properties
            yield!
                IntrinsicPropInfosOfTypeInScope infoReader None ad PreferOverrides m ty
                |> ChoosePropInfosForNameEnv g ty
        |]

    let nenv = { nenv with eUnqualifiedItems = nenv.eUnqualifiedItems.AddAndMarkAsCollapsible items }

    let methodGroupItems =
        // Methods
        IntrinsicMethInfosOfType infoReader None ad AllowMultiIntfInstantiations.Yes PreferOverrides m ty
        |> ChooseMethInfosForNameEnv g m ty
        // Combine methods and extension method groups of the same type
        |> List.map (fun pair ->
            match pair.Value with
            | Item.MethodGroup(name, methInfos, orig) ->              
                match nenv.eUnqualifiedItems.TryFind pair.Key with
                // First method of the found group must be an extension and have the same enclosing type as the type we are opening.
                // If the first method is an extension, we are assuming the rest of the methods in the group are also extensions.
                | Some(Item.MethodGroup(_, ((methInfo :: _) as methInfos2), _)) when methInfo.IsExtensionMember && typeEquiv g methInfo.ApparentEnclosingType ty ->
                    KeyValuePair (pair.Key, Item.MethodGroup(name, methInfos @ methInfos2, orig))
                | _ ->
                    pair
            | _ ->
                pair)
        |> Array.ofList

    { nenv with eUnqualifiedItems = nenv.eUnqualifiedItems.AddAndMarkAsCollapsible methodGroupItems }
    
and private AddNestedTypesOfTypeToNameEnv infoReader (amap: Import.ImportMap) ad m nenv ty =
    let tinst, tcrefs = GetNestedTyconRefsOfType infoReader amap (ad, None, TypeNameResolutionStaticArgsInfo.Indefinite, true, m) ty
    let tcrefGroup =
        tcrefs
        |> List.groupBy (fun tcref -> tcref.LogicalName)

    (nenv, tcrefGroup)
    ||> List.fold (fun nenv (_, tcrefs) ->
        AddTyconRefsWithEnclosingTypeInstToNameEnv BulkAdd.Yes false amap.g amap ad m false nenv (tinst, tcrefs))

and private AddTyconRefsWithEnclosingTypeInstToNameEnv bulkAddMode ownDefinition g amap ad m root nenv (tinstEnclosing: TypeInst, tcrefs: TyconRef list) =
    let nenv =
        (nenv, tcrefs)
        ||> List.fold (fun nenv tcref ->
            if tinstEnclosing.IsEmpty then nenv
            else { nenv with eUnqualifiedEnclosingTypeInsts = nenv.eUnqualifiedEnclosingTypeInsts.Add tcref tinstEnclosing })
    AddTyconRefsToNameEnv bulkAddMode ownDefinition g amap ad m root nenv tcrefs

and private AddStaticPartsOfTypeToNameEnv (amap: Import.ImportMap) m nenv ty =
    match tryAppTy amap.g ty with
    | ValueSome (tcref, tinst) ->
        AddStaticPartsOfTyconRefToNameEnv BulkAdd.Yes false amap.g amap m nenv (Some tinst) tcref
    | _ ->
        nenv

and private AddStaticPartsOfTyconRefToNameEnv bulkAddMode ownDefinition g amap m nenv tinstOpt (tcref: TyconRef) =
    let isIL = tcref.IsILTycon
    let ucrefs = if isIL then [] else tcref.UnionCasesAsList |> List.map tcref.MakeNestedUnionCaseRef
    let flds =  if isIL then [| |] else tcref.AllFieldsArray

    // C# style extension members
    let eIndexedExtensionMembers, eExtensionMembersByName, eUnindexedExtensionMembers =
        let ilStyleExtensionMeths = GetCSharpStyleIndexedExtensionMembersForTyconRef amap m tcref
        ((nenv.eIndexedExtensionMembers, nenv.eExtensionMembersByName, nenv.eUnindexedExtensionMembers), ilStyleExtensionMeths) ||> List.fold (fun (tab1, tab2, tab3) extMemInfo ->
            match extMemInfo with
            | Choice1Of2 (tcref, extMemInfo) -> tab1.Add (tcref, extMemInfo), AddValRefToExtensionMembersByNameTable extMemInfo.LogicalName tab2 extMemInfo, tab3
            | Choice2Of2 extMemInfo -> tab1, AddValRefToExtensionMembersByNameTable extMemInfo.LogicalName tab2 extMemInfo, extMemInfo :: tab3)  

    let isILOrRequiredQualifiedAccess = isIL || (not ownDefinition && HasFSharpAttribute g g.attrib_RequireQualifiedAccessAttribute tcref.Attribs)

    // Record labels
    let eFieldLabels =
        if isILOrRequiredQualifiedAccess || not tcref.IsRecordTycon || flds.Length = 0 then
            nenv.eFieldLabels
        else
            (nenv.eFieldLabels, flds) ||> Array.fold (fun acc f ->
                   if f.IsStatic || f.IsCompilerGenerated then acc
                   else AddRecdField (tcref.MakeNestedRecdFieldRef f) acc)

    let eUnqualifiedItems =
        let tab = nenv.eUnqualifiedItems 
        if isILOrRequiredQualifiedAccess || List.isEmpty ucrefs then
            tab
        else
            // Union cases for unqualfied
            AddUnionCases2 bulkAddMode tab ucrefs

    let ePatItems =
        if isILOrRequiredQualifiedAccess || List.isEmpty ucrefs then
            nenv.ePatItems
        else
            // Union cases for patterns
            AddUnionCases1 nenv.ePatItems ucrefs

    let eUnqualifiedRecordOrUnionTypeInsts =
        if isILOrRequiredQualifiedAccess || not (tcref.IsRecordTycon || tcref.IsUnionTycon) then
            nenv.eUnqualifiedRecordOrUnionTypeInsts
        else
            match tinstOpt with
            | None
            | Some [] -> nenv.eUnqualifiedEnclosingTypeInsts
            | Some tinst ->
                nenv.eUnqualifiedRecordOrUnionTypeInsts.Add tcref tinst

    { nenv with
        eFieldLabels = eFieldLabels
        eUnqualifiedRecordOrUnionTypeInsts = eUnqualifiedRecordOrUnionTypeInsts
        eUnqualifiedItems = eUnqualifiedItems
        ePatItems = ePatItems
        eIndexedExtensionMembers = eIndexedExtensionMembers
        eExtensionMembersByName = eExtensionMembersByName
        eUnindexedExtensionMembers = eUnindexedExtensionMembers }

and private CanAutoOpenTyconRef (g: TcGlobals) m (tcref: TyconRef) =
    g.langVersion.SupportsFeature LanguageFeature.OpenTypeDeclaration &&
    not tcref.IsILTycon &&
    TryFindFSharpBoolAttribute g g.attrib_AutoOpenAttribute tcref.Attribs = Some true &&
    tcref.Typars(m).Length = 0

/// Add any implied contents of a type definition to the environment.
and private AddPartsOfTyconRefToNameEnv bulkAddMode ownDefinition (g: TcGlobals) amap ad m  nenv (tcref: TyconRef) =
    let nenv =
        let tab = nenv.eUnqualifiedItems
        // add the type name for potential use as a constructor
        // The rules are
        // - The unqualified lookup table in the environment can contain map names to a set of type names (the set of type names is a new kind of "item").
        // - When the contents of a type definition is added to the environment, an entry is added in this table for all class and struct types.
        // - When opening a module, types are added first to the environment, then values, then auto-opened sub-modules.
        // - When a value is added by an "open" previously available type names will become inaccessible by this table.
        let tab =
            // This may explore into an unreferenced assembly if the name
            // is a type abbreviation. If it does, assume the name does not
            // have a constructor.
            let mayHaveConstruction =
                protectAssemblyExploration
                    false
                    (fun () ->
                        let ty = generalizedTyconRef tcref
                        isClassTy g ty || isStructTy g ty)

            if mayHaveConstruction then
                tab.LinearTryModifyThenLaterFlatten (tcref.DisplayName, (fun prev ->
                    match prev with
                    | Some (Item.UnqualifiedType tcrefs) -> Item.UnqualifiedType (tcref :: tcrefs)
                    | _ -> Item.UnqualifiedType [tcref]))
            else
                tab

        { nenv with eUnqualifiedItems = tab }

    let nenv = AddStaticPartsOfTyconRefToNameEnv bulkAddMode ownDefinition g amap m nenv None tcref
    let nenv = 
        if CanAutoOpenTyconRef g m tcref then
            let ty = generalizedTyconRef tcref
            AddStaticContentOfTypeToNameEnv g amap ad m nenv ty
        else
            nenv

    nenv

/// Add a set of type definitions to the name resolution environment
and AddTyconRefsToNameEnv bulkAddMode ownDefinition g amap ad m root nenv tcrefs =
    if isNil tcrefs then nenv else
    let env = List.fold (AddPartsOfTyconRefToNameEnv bulkAddMode ownDefinition g amap ad m) nenv tcrefs
    // Add most of the contents of the tycons en-masse, then flatten the tables if we're opening a module or namespace
    let tcrefs = Array.ofList tcrefs
    { env with
        eFullyQualifiedTyconsByDemangledNameAndArity =
            if root then
                AddTyconsByDemangledNameAndArity bulkAddMode tcrefs nenv.eFullyQualifiedTyconsByDemangledNameAndArity
            else
                nenv.eFullyQualifiedTyconsByDemangledNameAndArity
        eFullyQualifiedTyconsByAccessNames =
            if root then
                AddTyconByAccessNames bulkAddMode tcrefs nenv.eFullyQualifiedTyconsByAccessNames
            else
                nenv.eFullyQualifiedTyconsByAccessNames
        eTyconsByDemangledNameAndArity =
            AddTyconsByDemangledNameAndArity bulkAddMode tcrefs nenv.eTyconsByDemangledNameAndArity
        eTyconsByAccessNames =
            AddTyconByAccessNames bulkAddMode tcrefs nenv.eTyconsByAccessNames }

/// Add an F# exception definition to the name resolution environment
let AddExceptionDeclsToNameEnv bulkAddMode nenv (ecref: TyconRef) =
    assert ecref.IsExceptionDecl
    let item = Item.ExnCase ecref
    {nenv with
       eUnqualifiedItems =
            match bulkAddMode with
            | BulkAdd.Yes ->
                nenv.eUnqualifiedItems.AddAndMarkAsCollapsible [| KeyValuePair(ecref.LogicalName, item) |]
            | BulkAdd.No ->
                nenv.eUnqualifiedItems.Add (ecref.LogicalName, item)

       ePatItems = nenv.ePatItems.Add (ecref.LogicalName, item) }

/// Add a module abbreviation to the name resolution environment
let AddModuleAbbrevToNameEnv (id: Ident) nenv modrefs =
    {nenv with
       eModulesAndNamespaces =
         let add old nw = nw @ old
         NameMap.layerAdditive add (Map.add id.idText modrefs Map.empty) nenv.eModulesAndNamespaces }


//-------------------------------------------------------------------------
// Open a structure or an IL namespace
//-------------------------------------------------------------------------

let MakeNestedModuleRefs (modref: ModuleOrNamespaceRef) =
  modref.ModuleOrNamespaceType.ModuleAndNamespaceDefinitions
     |> List.map modref.NestedTyconRef

/// Add a set of module or namespace to the name resolution environment, including any sub-modules marked 'AutoOpen'
//
// Recursive because of "AutoOpen", i.e. adding a module reference may automatically open further modules
let rec AddModuleOrNamespaceRefsToNameEnv g amap m root ad nenv (modrefs: ModuleOrNamespaceRef list) =
    if isNil modrefs then nenv else
    let modrefsMap = modrefs |> NameMap.ofKeyedList (fun modref -> modref.DemangledModuleOrNamespaceName)
    let addModrefs tab =
         let add old nw =
             if IsEntityAccessible amap m ad nw then
                 nw :: old
             else
                 old
         NameMap.layerAdditive add modrefsMap tab
    let nenv =
        {nenv with
           eModulesAndNamespaces = addModrefs nenv.eModulesAndNamespaces
           eFullyQualifiedModulesAndNamespaces =
              if root then
                  addModrefs nenv.eFullyQualifiedModulesAndNamespaces
              else
                  nenv.eFullyQualifiedModulesAndNamespaces }
    let nenv =
        (nenv, modrefs) ||> List.fold (fun nenv modref ->
            if modref.IsModule && TryFindFSharpBoolAttribute g g.attrib_AutoOpenAttribute modref.Attribs = Some true then
                AddModuleOrNamespaceContentsToNameEnv g amap ad m false nenv modref
            else
                nenv)
    nenv

/// Add the contents of a module or namespace to the name resolution environment
and AddModuleOrNamespaceContentsToNameEnv (g: TcGlobals) amap (ad: AccessorDomain) m root nenv (modref: ModuleOrNamespaceRef) =
    let pri = NextExtensionMethodPriority()
    let mty = modref.ModuleOrNamespaceType

    let nenv =
        let mutable state = { nenv with eDisplayEnv = nenv.eDisplayEnv.AddOpenModuleOrNamespace modref }

        for exnc in mty.ExceptionDefinitions do
           let tcref = modref.NestedTyconRef exnc
           if IsEntityAccessible amap m ad tcref then
               state <- AddExceptionDeclsToNameEnv BulkAdd.Yes state tcref

        state

    let tcrefs =
       mty.TypeAndExceptionDefinitions
       |> List.choose (fun tycon ->
           let tcref = modref.NestedTyconRef tycon
           if IsEntityAccessible amap m ad tcref then Some tcref else None)

    let nenv = (nenv, tcrefs) ||> AddTyconRefsToNameEnv BulkAdd.Yes false g amap ad m false
    let vrefs =
        mty.AllValsAndMembers.ToList()
        |> List.choose (fun x -> if IsAccessible ad x.Accessibility then TryMkValRefInModRef modref x else None)
        |> List.toArray
    let nenv = AddValRefsToNameEnvWithPriority g BulkAdd.Yes pri nenv vrefs
    let nestedModules = MakeNestedModuleRefs modref
    let nenv = (nenv, nestedModules) ||> AddModuleOrNamespaceRefsToNameEnv g amap m root ad
    nenv

/// Add a set of modules or namespaces to the name resolution environment
//
// Note this is a 'foldBack' - the most recently added modules come first in the list, e.g.
//    module M1 = ... // M1a
//    module M1 = ... // M1b
//    open M1
//
// The list contains [M1b; M1a]
and AddModuleOrNamespaceRefsContentsToNameEnv g amap ad m root nenv modrefs =
   (modrefs, nenv) ||> List.foldBack (fun modref acc -> AddModuleOrNamespaceRefContentsToNameEnv g amap ad m root acc modref)

and AddTypeContentsToNameEnv g amap ad m nenv (typ: TType) =
    assert (isAppTy g typ)
    assert not (tcrefOfAppTy g typ).IsModuleOrNamespace
    AddStaticContentOfTypeToNameEnv g amap ad m nenv typ

and AddModuleOrNamespaceRefContentsToNameEnv g amap ad m root nenv (modref: EntityRef) =
    assert modref.IsModuleOrNamespace 
    AddModuleOrNamespaceContentsToNameEnv g amap ad m root nenv modref

/// Add a single modules or namespace to the name resolution environment
let AddModuleOrNamespaceRefToNameEnv g amap m root ad nenv (modref: EntityRef) =
    AddModuleOrNamespaceRefsToNameEnv g amap m root ad nenv [modref]


/// A flag which indicates if it is an error to have two declared type parameters with identical names
/// in the name resolution environment.
type CheckForDuplicateTyparFlag =
    | CheckForDuplicateTypars
    | NoCheckForDuplicateTypars

/// Add some declared type parameters to the name resolution environment
let AddDeclaredTyparsToNameEnv check nenv typars =
    let typarmap =
      List.foldBack
        (fun (tp: Typar) sofar ->
          match check with
          | CheckForDuplicateTypars ->
              if Map.containsKey tp.Name sofar then
                errorR (Duplicate("type parameter", tp.DisplayName, tp.Range))
          | NoCheckForDuplicateTypars -> ()

          Map.add tp.Name tp sofar) typars Map.empty
    {nenv with eTypars = NameMap.layer typarmap nenv.eTypars }


//-------------------------------------------------------------------------
// Generating fresh instantiations for type inference.
//-------------------------------------------------------------------------

/// Convert a reference to a named type into a type that includes
/// a fresh set of inference type variables for the type parameters.
let FreshenTycon (ncenv: NameResolver) traitCtxt m (tcref: TyconRef) =
    let tinst = ncenv.InstantiationGenerator m (tcref.Typars m) traitCtxt
    let improvedTy = ncenv.g.decompileType tcref tinst
    improvedTy

/// Convert a reference to a named type into a type that includes
/// a set of enclosing type instantiations and a fresh set of inference type variables for the type parameters.
let FreshenTyconWithEnclosingTypeInst (ncenv: NameResolver) traitCtxt m (tinstEnclosing: TypeInst) (tcref: TyconRef) =
    let tps = ncenv.InstantiationGenerator m (tcref.Typars m) traitCtxt
    let tinst = List.skip tinstEnclosing.Length tps
    let improvedTy = ncenv.g.decompileType tcref (tinstEnclosing @ tinst)
    improvedTy

/// Convert a reference to a union case into a UnionCaseInfo that includes
/// a fresh set of inference type variables for the type parameters of the union type.

let FreshenUnionCaseRef (ncenv: NameResolver) traitCtxt m (ucref: UnionCaseRef) = 
    let tinst = ncenv.InstantiationGenerator m (ucref.TyconRef.Typars m) traitCtxt
    UnionCaseInfo(tinst, ucref)

/// Generate a new reference to a record field with a fresh type instantiation
let FreshenRecdFieldRef (ncenv: NameResolver) traitCtxt m (rfref: RecdFieldRef) =
    RecdFieldInfo(ncenv.InstantiationGenerator m (rfref.Tycon.Typars m) traitCtxt, rfref)

/// This must be called after fetching unqualified items that may need to be freshened 
/// or have type instantiations
let ResolveUnqualifiedItem (ncenv: NameResolver) traitCtxt nenv m res =
    match res with
    | Item.UnionCase(UnionCaseInfo(_, ucref), _) ->
        match nenv.eUnqualifiedRecordOrUnionTypeInsts.TryFind ucref.TyconRef with
        | Some tinst ->
            Item.UnionCase(UnionCaseInfo(tinst, ucref), false)
        | _ ->
            Item.UnionCase(FreshenUnionCaseRef ncenv traitCtxt m ucref, false)
    | _ -> res

//-------------------------------------------------------------------------
// Resolve module paths, value, field etc. lookups.  Doing this involves
// searching through many possibilities and disambiguating.  Hence first
// define some ways of combining multiple results and for carrying
// error information.  Errors are generally undefined names and are
// reported by returning the error that occurs at greatest depth in the
// sequence of Identifiers.
//-------------------------------------------------------------------------

// Accumulate a set of possible results.
// If neither operations succeed, return an approximate error.
// If one succeeds, return that one.
// Prefer the error associated with the first argument.
let OneResult res =
    match res with
    | Result x -> Result [x]
    | Exception e -> Exception e

let OneSuccess x = Result [x]

let AddResults res1 res2 =
    match res1, res2 with
    | Result [], _ -> res2
    | _, Result [] -> res1
    | Result x, Result l -> Result (x @ l)
    | Exception _, Result l -> Result l
    | Result x, Exception _ -> Result x
    // If we have error messages for the same symbol, then we can merge suggestions.
    | Exception (UndefinedName(n1, f, id1, suggestions1)), Exception (UndefinedName(n2, _, id2, suggestions2)) when n1 = n2 && id1.idText = id2.idText && Range.equals id1.idRange id2.idRange ->
        Exception(UndefinedName(n1, f, id1, fun addToBuffer -> suggestions1 addToBuffer; suggestions2 addToBuffer))
    // This prefers error messages coming from deeper failing long identifier paths
    | Exception (UndefinedName(n1, _, _, _) as e1), Exception (UndefinedName(n2, _, _, _) as e2) ->
        if n1 < n2 then Exception e2 else Exception e1
    // Prefer more concrete errors about things being undefined
    | Exception (UndefinedName _ as e1), Exception (Error _) -> Exception e1
    | Exception (Error _), Exception (UndefinedName _ as e2) -> Exception e2
    | Exception e1, Exception _ -> Exception e1

let NoResultsOrUsefulErrors = Result []

let rec CollectResults f = function
    | [] -> NoResultsOrUsefulErrors
    | [h] -> OneResult (f h)
    | h :: t -> AddResults (OneResult (f h)) (CollectResults f t)

let rec CollectAtMostOneResult f inputs = 
    match inputs with 
    | [] -> NoResultsOrUsefulErrors
    | [h] -> OneResult (f h)
    | h :: t ->
        match f h with
        | Result r -> Result [r]
        | Exception e -> AddResults (Exception e) (CollectAtMostOneResult f t)

let CollectResults2 resultCollectionSettings f =
    match resultCollectionSettings with
    | ResultCollectionSettings.AtMostOneResult -> CollectAtMostOneResult f
    | _ -> CollectResults f

let MapResults f = function
    | Result xs -> Result (List.map f xs)
    | Exception err -> Exception err

let AtMostOneResult m res =
    match res with
    | Exception err -> raze err
    | Result [] -> raze (Error(FSComp.SR.nrInvalidModuleExprType(), m))
    | Result (res :: _) -> success res

let AtMostOneResultQuery query2 res1 =
    match res1 with
    | Exception _ -> AddResults res1 (query2())
    | Result [] -> query2()
    | _ -> res1

let inline (+++) res1 query2 = AtMostOneResultQuery query2 res1

//-------------------------------------------------------------------------
// Resolve (possibly mangled) type names in environment
//-------------------------------------------------------------------------

/// Unqualified lookups of type names where the number of generic arguments is known
/// from context, e.g. List<arg>.  Rebindings due to 'open' may have rebound identifiers.
let LookupTypeNameInEnvHaveArity fq nm numTyArgs (nenv: NameResolutionEnv) =
    let key =
        match TryDemangleGenericNameAndPos nm with
        | ValueSome pos -> DecodeGenericTypeNameWithPos pos nm
        | _ -> NameArityPair(nm, numTyArgs)

    match nenv.TyconsByDemangledNameAndArity(fq).TryFind key with
    | None -> nenv.TyconsByAccessNames(fq).TryFind nm |> Option.map List.head
    | res -> res

/// Qualified lookup of type names in the environment
let LookupTypeNameInEnvNoArity fq nm (nenv: NameResolutionEnv) =
    LookupTypeNameNoArity nm (nenv.TyconsByDemangledNameAndArity fq) (nenv.TyconsByAccessNames fq)

/// Qualified lookup of type names in an entity where we may know a generic argument count
let LookupTypeNameInEnvMaybeHaveArity fq nm (typeNameResInfo: TypeNameResolutionInfo) nenv =
    if typeNameResInfo.StaticArgsInfo.HasNoStaticArgsInfo then
        LookupTypeNameInEnvNoArity fq nm nenv
    else
        LookupTypeNameInEnvHaveArity fq nm typeNameResInfo.StaticArgsInfo.NumStaticArgs nenv |> Option.toList

//-------------------------------------------------------------------------
// Report environments to visual studio. We stuff intermediary results
// into a global variable. A little unpleasant.
//-------------------------------------------------------------------------

/// Represents the kind of the occurrence when reporting a name in name resolution
[<RequireQualifiedAccess; Struct>]
type ItemOccurence =
    /// This is a binding / declaration of the item
    | Binding
    /// This is a usage of the item
    | Use
    /// This is a usage of a type name in a type
    | UseInType
    /// This is a usage of a type name in an attribute
    | UseInAttribute
    /// Inside pattern matching
    | Pattern
    /// Abstract slot gets implemented
    | Implemented
    /// Result gets suppressed over this text range
    | RelatedText
    /// This is a usage of a module or namespace name in open statement
    | Open

type OpenDeclaration =
    { Target: SynOpenDeclTarget
      Range: range option
      Modules: ModuleOrNamespaceRef list
      Types: TType list
      AppliedScope: range
      IsOwnNamespace: bool }

    static member Create(target: SynOpenDeclTarget, modules: ModuleOrNamespaceRef list, types: TType list, appliedScope: range, isOwnNamespace: bool) =
        { Target = target
          Range =
            match target with 
            | SynOpenDeclTarget.ModuleOrNamespace (range=m)
            | SynOpenDeclTarget.Type (range=m) -> Some m
          Types = types
          Modules = modules
          AppliedScope = appliedScope
          IsOwnNamespace = isOwnNamespace }

type FormatStringCheckContext =
    { SourceText: ISourceText
      LineStartPositions: int[] }

/// An abstract type for reporting the results of name resolution and type checking.
type ITypecheckResultsSink =
    abstract NotifyEnvWithScope: range * NameResolutionEnv * AccessorDomain -> unit
    abstract NotifyExprHasType: TType * NameResolutionEnv * AccessorDomain * range -> unit
    abstract NotifyNameResolution: pos * item: Item * TyparInst * ItemOccurence * NameResolutionEnv * AccessorDomain * range * replace: bool -> unit
    abstract NotifyMethodGroupNameResolution : pos * item: Item * itemMethodGroup: Item * TyparInst * ItemOccurence * NameResolutionEnv * AccessorDomain * range * replace: bool -> unit
    abstract NotifyFormatSpecifierLocation: range * int -> unit
    abstract NotifyOpenDeclaration: OpenDeclaration -> unit
    abstract CurrentSourceText: ISourceText option
    abstract FormatStringCheckContext: FormatStringCheckContext option

let (|ValRefOfProp|_|) (pi: PropInfo) = pi.ArbitraryValRef
let (|ValRefOfMeth|_|) (mi: MethInfo) = mi.ArbitraryValRef
let (|ValRefOfEvent|_|) (evt: EventInfo) = evt.ArbitraryValRef

let rec (|RecordFieldUse|_|) (item: Item) =
    match item with
    | Item.RecdField(RecdFieldInfo(_, RecdFieldRef(tcref, name))) -> Some (name, tcref)
    | Item.SetterArg(_, RecordFieldUse f) -> Some f
    | _ -> None

let (|UnionCaseFieldUse|_|) (item: Item) =
    match item with
    | Item.UnionCaseField (uci, fieldIndex) -> Some (fieldIndex, uci.UnionCaseRef)
    | _ -> None

let rec (|ILFieldUse|_|) (item: Item) =
    match item with
    | Item.ILField finfo -> Some finfo
    | Item.SetterArg(_, ILFieldUse f) -> Some f
    | _ -> None

let rec (|PropertyUse|_|) (item: Item) =
    match item with
    | Item.Property(_, pinfo :: _) -> Some pinfo
    | Item.SetterArg(_, PropertyUse pinfo) -> Some pinfo
    | _ -> None

let rec (|FSharpPropertyUse|_|) (item: Item) =
    match item with
    | Item.Property(_, [ValRefOfProp vref]) -> Some vref
    | Item.SetterArg(_, FSharpPropertyUse propDef) -> Some propDef
    | _ -> None

let (|MethodUse|_|) (item: Item) =
    match item with
    | Item.MethodGroup(_, [minfo], _) -> Some minfo
    | _ -> None

let (|FSharpMethodUse|_|) (item: Item) =
    match item with
    | Item.MethodGroup(_, [ValRefOfMeth vref], _) -> Some vref
    | Item.Value vref when vref.IsMember -> Some vref
    | _ -> None

let (|EntityUse|_|) (item: Item) =
    match item with
    | Item.UnqualifiedType (tcref :: _) -> Some tcref
    | Item.ExnCase tcref -> Some tcref
    | Item.Types(_, [AbbrevOrAppTy tcref])
    | Item.DelegateCtor(AbbrevOrAppTy tcref)
    | Item.FakeInterfaceCtor(AbbrevOrAppTy tcref) -> Some tcref
    | Item.CtorGroup(_, ctor :: _) ->
        match ctor.ApparentEnclosingType with
        | AbbrevOrAppTy tcref -> Some tcref
        | _ -> None
    | _ -> None

let (|EventUse|_|) (item: Item) =
    match item with
    | Item.Event einfo -> Some einfo
    | _ -> None

let (|FSharpEventUse|_|) (item: Item) =
    match item with
    | Item.Event(ValRefOfEvent vref) -> Some vref
    | _ -> None

let (|UnionCaseUse|_|) (item: Item) =
    match item with
    | Item.UnionCase(UnionCaseInfo(_, u1), _) -> Some u1
    | _ -> None

let (|ValUse|_|) (item: Item) =
    match item with
    | Item.Value vref
    | FSharpPropertyUse vref
    | FSharpMethodUse vref
    | FSharpEventUse vref
    | Item.CustomBuilder(_, vref) -> Some vref
    | _ -> None

let (|ActivePatternCaseUse|_|) (item: Item) =
    match item with
    | Item.ActivePatternCase(APElemRef(_, vref, idx, _)) -> Some (vref.SigRange, vref.DefinitionRange, idx)
    | Item.ActivePatternResult(ap, _, idx, _) -> Some (ap.Range, ap.Range, idx)
    | _ -> None

let tyconRefDefnHash (_g: TcGlobals) (eref1: EntityRef) =
    hash eref1.LogicalName

let tyconRefDefnEq g (eref1: EntityRef) (eref2: EntityRef) =
    tyconRefEq g eref1 eref2 ||

    // Signature items considered equal to implementation items
    not (Range.equals eref1.DefinitionRange Range.rangeStartup) &&
    not (Range.equals eref1.DefinitionRange Range.range0) &&
    not (Range.equals eref1.DefinitionRange Range.rangeCmdArgs) &&
    (Range.equals eref1.DefinitionRange eref2.DefinitionRange || Range.equals eref1.SigRange eref2.SigRange) &&
    eref1.LogicalName = eref2.LogicalName

let valRefDefnHash (_g: TcGlobals) (vref1: ValRef) =
    hash vref1.DisplayName

let valRefDefnEq g (vref1: ValRef) (vref2: ValRef) =
    valRefEq g vref1 vref2 ||

    // Signature items considered equal to implementation items
    not (Range.equals vref1.DefinitionRange Range.rangeStartup) &&
    not (Range.equals vref1.DefinitionRange Range.range0) &&
    not (Range.equals vref1.DefinitionRange Range.rangeCmdArgs) &&
    (Range.equals vref1.DefinitionRange vref2.DefinitionRange || Range.equals vref1.SigRange vref2.SigRange) &&
    vref1.LogicalName = vref2.LogicalName

let unionCaseRefDefnEq g (uc1: UnionCaseRef) (uc2: UnionCaseRef) =
    uc1.CaseName = uc2.CaseName && tyconRefDefnEq g uc1.TyconRef uc2.TyconRef

/// Given the Item 'orig' - returns function 'other: Item -> bool', that will yield true if other and orig represents the same item and false - otherwise
let ItemsAreEffectivelyEqual g orig other =
    match orig, other  with
    | EntityUse ty1, EntityUse ty2 ->
        tyconRefDefnEq g ty1 ty2

    | Item.TypeVar (nm1, tp1), Item.TypeVar (nm2, tp2) ->
        nm1 = nm2 &&
        (typeEquiv g (mkTyparTy tp1) (mkTyparTy tp2) ||
         match stripTyparEqns (mkTyparTy tp1), stripTyparEqns (mkTyparTy tp2) with
         | TType_var tp1, TType_var tp2 ->
            not tp1.IsCompilerGenerated && not tp1.IsFromError &&
            not tp2.IsCompilerGenerated && not tp2.IsFromError &&
            Range.equals tp1.Range tp2.Range
         | AbbrevOrAppTy tcref1, AbbrevOrAppTy tcref2 ->
            tyconRefDefnEq g tcref1 tcref2
         | _ -> false)

    | ValUse vref1, ValUse vref2 ->
        valRefDefnEq g vref1 vref2

    | ActivePatternCaseUse (range1, range1i, idx1), ActivePatternCaseUse (range2, range2i, idx2) ->
        (idx1 = idx2) && (Range.equals range1 range2 || Range.equals range1i range2i)

    | MethodUse minfo1, MethodUse minfo2 ->
        MethInfo.MethInfosUseIdenticalDefinitions minfo1 minfo2 ||
        // Allow for equality up to signature matching
        match minfo1.ArbitraryValRef, minfo2.ArbitraryValRef with
        | Some vref1, Some vref2 -> valRefDefnEq g vref1 vref2
        | _ -> false

    | PropertyUse pinfo1, PropertyUse pinfo2 ->
        PropInfo.PropInfosUseIdenticalDefinitions pinfo1 pinfo2 ||
        // Allow for equality up to signature matching
        match pinfo1.ArbitraryValRef, pinfo2.ArbitraryValRef with
        | Some vref1, Some vref2 -> valRefDefnEq g vref1 vref2
        | _ -> false

    | Item.ArgName (id1, _, _), Item.ArgName (id2, _, _) ->
        (id1.idText = id2.idText && Range.equals id1.idRange id2.idRange)

    | (Item.ArgName (id, _, _), ValUse vref) | (ValUse vref, Item.ArgName (id, _, _)) ->
        ((Range.equals id.idRange vref.DefinitionRange || Range.equals id.idRange vref.SigRange) && id.idText = vref.DisplayName)

    | Item.AnonRecdField(anon1, _, i1, _), Item.AnonRecdField(anon2, _, i2, _) -> anonInfoEquiv anon1 anon2 && i1 = i2

    | ILFieldUse f1, ILFieldUse f2 ->
        ILFieldInfo.ILFieldInfosUseIdenticalDefinitions f1 f2

    | UnionCaseUse u1, UnionCaseUse u2 ->
        unionCaseRefDefnEq g u1 u2

    | RecordFieldUse(name1, tcref1), RecordFieldUse(name2, tcref2) ->
        name1 = name2 && tyconRefDefnEq g tcref1 tcref2

    | UnionCaseFieldUse(fieldIndex1, ucref1), UnionCaseFieldUse(fieldIndex2, ucref2) ->
        unionCaseRefDefnEq g ucref1 ucref2 && fieldIndex1 = fieldIndex2 

    | EventUse evt1, EventUse evt2 ->
        EventInfo.EventInfosUseIdenticalDefinitions evt1 evt2  ||
        // Allow for equality up to signature matching
        match evt1.ArbitraryValRef, evt2.ArbitraryValRef with
        | Some vref1, Some vref2 -> valRefDefnEq g vref1 vref2
        | _ -> false

    | Item.ModuleOrNamespaces modrefs1, Item.ModuleOrNamespaces modrefs2 ->
        modrefs1 |> List.exists (fun modref1 -> modrefs2 |> List.exists (fun r -> tyconRefDefnEq g modref1 r || fullDisplayTextOfModRef modref1 = fullDisplayTextOfModRef r))

    | _ -> false

/// Given the Item 'orig' - returns function 'other: Item -> bool', that will yield true if other and orig represents the same item and false - otherwise
let ItemsAreEffectivelyEqualHash (g: TcGlobals) orig =
    match orig with
    | EntityUse tcref -> tyconRefDefnHash g tcref
    | Item.TypeVar (nm, _)-> hash nm
    | ValUse vref -> valRefDefnHash g vref
    | ActivePatternCaseUse (_, _, idx)-> hash idx
    | MethodUse minfo -> minfo.ComputeHashCode()
    | PropertyUse pinfo -> pinfo.ComputeHashCode()
    | Item.ArgName (id, _, _) -> hash id.idText
    | ILFieldUse ilfinfo -> ilfinfo.ComputeHashCode()
    | UnionCaseUse ucase ->  hash ucase.CaseName
    | RecordFieldUse (name, _) -> hash name
    | UnionCaseFieldUse (fieldIndex, case) -> hash (case.CaseName, fieldIndex)
    | EventUse einfo -> einfo.ComputeHashCode()
    | Item.ModuleOrNamespaces (mref :: _) -> hash mref.DefinitionRange
    | _ -> 389329

[<System.Diagnostics.DebuggerDisplay("{DebugToString()}")>]
type CapturedNameResolution(i: Item, tpinst, io: ItemOccurence, nre: NameResolutionEnv, ad: AccessorDomain, m: range) =
    member this.Pos = m.End
    member this.Item = i
    member this.ItemWithInst = ({ Item = i; TyparInst = tpinst } : ItemWithInst)
    member this.ItemOccurence = io
    member this.DisplayEnv = nre.DisplayEnv
    member this.NameResolutionEnv = nre
    member this.AccessorDomain = ad
    member this.Range = m
    member this.DebugToString() =
        sprintf "%A: %+A" (this.Pos.Line, this.Pos.Column) i

/// Represents container for all name resolutions that were met so far when typechecking some particular file
type TcResolutions
    (capturedEnvs: ResizeArray<range * NameResolutionEnv * AccessorDomain>,
     capturedExprTypes: ResizeArray<TType * NameResolutionEnv * AccessorDomain * range>,
     capturedNameResolutions: ResizeArray<CapturedNameResolution>,
     capturedMethodGroupResolutions: ResizeArray<CapturedNameResolution>) =

    static let empty = TcResolutions(ResizeArray 0, ResizeArray 0, ResizeArray 0, ResizeArray 0)

    member this.CapturedEnvs = capturedEnvs
    member this.CapturedExpressionTypings = capturedExprTypes
    member this.CapturedNameResolutions = capturedNameResolutions
    member this.CapturedMethodGroupResolutions = capturedMethodGroupResolutions

    static member Empty = empty

[<Struct>]
type TcSymbolUseData =
   { Item: Item
     ItemOccurence: ItemOccurence
     DisplayEnv: DisplayEnv
     Range: range }

/// Represents container for all name resolutions that were met so far when typechecking some particular file
///
/// This is a memory-critical data structure - allocations of this data structure and its immediate contents
/// is one of the highest memory long-lived data structures in typical uses of IDEs. Not many of these objects
/// are allocated (one per file), but they are large because the allUsesOfAllSymbols array is large.
type TcSymbolUses(g, capturedNameResolutions: ResizeArray<CapturedNameResolution>, formatSpecifierLocations: (range * int)[]) =

    // Make sure we only capture the information we really need to report symbol uses
    let allUsesOfSymbols =
        capturedNameResolutions
        |> ResizeArray.mapToSmallArrayChunks (fun cnr -> { Item=cnr.Item; ItemOccurence=cnr.ItemOccurence; DisplayEnv=cnr.DisplayEnv; Range=cnr.Range })

    let capturedNameResolutions = ()
    do ignore capturedNameResolutions // don't capture this!

    member this.GetUsesOfSymbol item =
        // This member returns what is potentially a very large array, which may approach the size constraints of the Large Object Heap.
        // This is unlikely in practice, though, because we filter down the set of all symbol uses to those specifically for the given `item`.
        // Consequently we have a much lesser chance of ending up with an array large enough to be promoted to the LOH.
        [| for symbolUseChunk in allUsesOfSymbols do
            for symbolUse in symbolUseChunk do
                if protectAssemblyExploration false (fun () -> ItemsAreEffectivelyEqual g item symbolUse.Item) then
                    yield symbolUse |]

    member this.AllUsesOfSymbols = allUsesOfSymbols

    member this.GetFormatSpecifierLocationsAndArity() = formatSpecifierLocations

    static member Empty = TcSymbolUses(Unchecked.defaultof<_>, ResizeArray(), Array.empty)

/// An accumulator for the results being emitted into the tcSink.
type TcResultsSinkImpl(tcGlobals, ?sourceText: ISourceText) =
    let capturedEnvs = ResizeArray<_>()
    let capturedExprTypings = ResizeArray<_>()
    let capturedNameResolutions = ResizeArray<CapturedNameResolution>()
    let capturedMethodGroupResolutions = ResizeArray<CapturedNameResolution>()
    let capturedOpenDeclarations = ResizeArray<OpenDeclaration>()
    let capturedFormatSpecifierLocations = ResizeArray<_>()

    let capturedNameResolutionIdentifiers =
        new System.Collections.Generic.HashSet<pos * string>
            ( { new IEqualityComparer<_> with
                    member _.GetHashCode((p: pos, i)) = p.Line + 101 * p.Column + hash i
                    member _.Equals((p1, i1), (p2, i2)) = posEq p1 p2 && i1 =  i2 } )

    let capturedModulesAndNamespaces =
        new System.Collections.Generic.HashSet<range * Item>
            ( { new IEqualityComparer<range * Item> with
                    member _.GetHashCode ((m, _)) = hash m
                    member _.Equals ((m1, item1), (m2, item2)) = Range.equals m1 m2 && ItemsAreEffectivelyEqual tcGlobals item1 item2 } )

    let allowedRange (m: range) =
        not m.IsSynthetic

    let isAlreadyDone endPos item m =
        // Desugaring some F# constructs (notably computation expressions with custom operators)
        // results in duplication of textual variables. So we ensure we never record two name resolutions
        // for the same identifier at the same location.

        match item with
        | Item.ModuleOrNamespaces _ ->
            not (capturedModulesAndNamespaces.Add (m, item))
        | _ ->

        let keyOpt =
            match item with
            | Item.Value vref -> Some (endPos, vref.DisplayName)
            | Item.ArgName (id, _, _) -> Some (endPos, id.idText)
            | _ -> None

        match keyOpt with
        | Some key -> not (capturedNameResolutionIdentifiers.Add key)
        | _ -> false

    let remove m =
        capturedNameResolutions.RemoveAll(fun cnr -> Range.equals cnr.Range m) |> ignore
        capturedMethodGroupResolutions.RemoveAll(fun cnr -> Range.equals cnr.Range m) |> ignore

    let formatStringCheckContext =
        lazy
            sourceText |> Option.map (fun sourceText ->
                let positions =
                    [|
                        yield 0
                        for i in 1..sourceText.Length do
                            let c = sourceText.[i-1]
                            if c = '\r' && i < sourceText.Length && sourceText.[i] = '\n' then ()
                            elif c = '\r' then yield i
                            elif c = '\n' then yield i
                        yield sourceText.Length
                    |]
                { SourceText = sourceText
                  LineStartPositions = positions })

    member this.GetResolutions() =
        TcResolutions(capturedEnvs, capturedExprTypings, capturedNameResolutions, capturedMethodGroupResolutions)

    member this.GetSymbolUses() =
        TcSymbolUses(tcGlobals, capturedNameResolutions, capturedFormatSpecifierLocations.ToArray())

    member this.GetOpenDeclarations() =
        capturedOpenDeclarations |> Seq.distinctBy (fun x -> x.Range, x.AppliedScope, x.IsOwnNamespace) |> Seq.toArray

    member this.GetFormatSpecifierLocations() =
        capturedFormatSpecifierLocations.ToArray()

    interface ITypecheckResultsSink with
        member sink.NotifyEnvWithScope(m, nenv, ad) =
            if allowedRange m then
                capturedEnvs.Add((m, nenv, ad))

        member sink.NotifyExprHasType(ty, nenv, ad, m) =
            if allowedRange m then
                capturedExprTypings.Add((ty, nenv, ad, m))

        member sink.NotifyNameResolution(endPos, item, tpinst, occurenceType, nenv, ad, m, replace) =
            if allowedRange m then
                if replace then
                    remove m
                elif not (isAlreadyDone endPos item m) then
                    capturedNameResolutions.Add(CapturedNameResolution(item, tpinst, occurenceType, nenv, ad, m))

        member sink.NotifyMethodGroupNameResolution(endPos, item, itemMethodGroup, tpinst, occurenceType, nenv, ad, m, replace) =
            if allowedRange m then
                if replace then
                    remove m
                elif not (isAlreadyDone endPos item m) then
                    capturedNameResolutions.Add(CapturedNameResolution(item, tpinst, occurenceType, nenv, ad, m))
                    capturedMethodGroupResolutions.Add(CapturedNameResolution(itemMethodGroup, [], occurenceType, nenv, ad, m))

        member sink.NotifyFormatSpecifierLocation(m, numArgs) =
            capturedFormatSpecifierLocations.Add((m, numArgs))

        member sink.NotifyOpenDeclaration openDeclaration =
            capturedOpenDeclarations.Add openDeclaration

        member sink.CurrentSourceText = sourceText

        member sink.FormatStringCheckContext = formatStringCheckContext.Value

/// An abstract type for reporting the results of name resolution and type checking, and which allows
/// temporary suspension and/or redirection of reporting.
type TcResultsSink =
    { mutable CurrentSink: ITypecheckResultsSink option }
    static member NoSink =  { CurrentSink = None }
    static member WithSink sink = { CurrentSink = Some sink }

/// Temporarily redirect reporting of name resolution and type checking results
let WithNewTypecheckResultsSink (newSink: ITypecheckResultsSink, sink: TcResultsSink) =
    let old = sink.CurrentSink
    sink.CurrentSink <- Some newSink
    { new System.IDisposable with member x.Dispose() = sink.CurrentSink <- old }

/// Temporarily suspend reporting of name resolution and type checking results
let TemporarilySuspendReportingTypecheckResultsToSink (sink: TcResultsSink) =
    let old = sink.CurrentSink
    sink.CurrentSink <- None
    { new System.IDisposable with member x.Dispose() = sink.CurrentSink <- old }


/// Report the active name resolution environment for a specific source range
let CallEnvSink (sink: TcResultsSink) (scopem, nenv, ad) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyEnvWithScope(scopem, nenv, ad)

/// Report a specific name resolution at a source range
let CallNameResolutionSink (sink: TcResultsSink) (m: range, nenv, item, tpinst, occurenceType, ad) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyNameResolution(m.End, item, tpinst, occurenceType, nenv, ad, m, false)

let CallMethodGroupNameResolutionSink (sink: TcResultsSink) (m: range, nenv, item, itemMethodGroup, tpinst, occurenceType, ad) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyMethodGroupNameResolution(m.End, item, itemMethodGroup, tpinst, occurenceType, nenv, ad, m, false)

let CallNameResolutionSinkReplacing (sink: TcResultsSink) (m: range, nenv, item, tpinst, occurenceType, ad) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyNameResolution(m.End, item, tpinst, occurenceType, nenv, ad, m, true)

/// Report a specific expression typing at a source range
let CallExprHasTypeSink (sink: TcResultsSink) (m: range, nenv, ty, ad) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyExprHasType(ty, nenv, ad, m)

let CallOpenDeclarationSink (sink: TcResultsSink) (openDeclaration: OpenDeclaration) =
    match sink.CurrentSink with
    | None -> ()
    | Some sink -> sink.NotifyOpenDeclaration openDeclaration

//-------------------------------------------------------------------------
// Check inferability of type parameters in resolved items.
//-------------------------------------------------------------------------

/// Checks if the type variables associated with the result of a resolution are inferable,
/// i.e. occur in the arguments or return type of the resolution. If not give a warning
/// about a type instantiation being needed.
type ResultTyparChecker = ResultTyparChecker of (unit -> bool)

let CheckAllTyparsInferrable amap m item =
    match item with
    | Item.Property(_, pinfos) ->
        pinfos |> List.forall (fun pinfo ->
            pinfo.IsExtensionMember ||
            let freeInDeclaringType = freeInType CollectTyparsNoCaching pinfo.ApparentEnclosingType
            let freeInArgsAndRetType =
                accFreeInTypes CollectTyparsNoCaching (pinfo.GetParamTypes(amap, m))
                       (freeInType CollectTyparsNoCaching (pinfo.GetPropertyType(amap, m)))
            let free = Zset.diff freeInDeclaringType.FreeTypars  freeInArgsAndRetType.FreeTypars
            free.IsEmpty)

    | Item.MethodGroup(_, minfos, _) ->
        minfos |> List.forall (fun minfo ->
            minfo.IsExtensionMember ||
            let fminst = minfo.FormalMethodInst
            let freeInDeclaringType = freeInType CollectTyparsNoCaching minfo.ApparentEnclosingType
            let freeInArgsAndRetType =
                List.foldBack (accFreeInTypes CollectTyparsNoCaching) (minfo.GetParamTypes(amap, m, fminst))
                   (accFreeInTypes CollectTyparsNoCaching (minfo.GetObjArgTypes(amap, m, fminst))
                       (freeInType CollectTyparsNoCaching (minfo.GetFSharpReturnTy(amap, m, fminst))))
            let free = Zset.diff freeInDeclaringType.FreeTypars  freeInArgsAndRetType.FreeTypars
            free.IsEmpty)

    | Item.CtorGroup _
    | Item.FakeInterfaceCtor _
    | Item.DelegateCtor _
    | Item.Types _
    | Item.ModuleOrNamespaces _
    | Item.CustomOperation _
    | Item.CustomBuilder _
    | Item.TypeVar _
    | Item.ArgName _
    | Item.ActivePatternResult _
    | Item.Value _
    | Item.ActivePatternCase _
    | Item.UnionCase _
    | Item.ExnCase _
    | Item.RecdField _
    | Item.UnionCaseField _
    | Item.AnonRecdField _
    | Item.NewDef _
    | Item.ILField _
    | Item.Event _
    | Item.ImplicitOp _
    | Item.UnqualifiedType _
    | Item.SetterArg _ -> true

//-------------------------------------------------------------------------
// Check inferability of type parameters in resolved items.
//-------------------------------------------------------------------------

/// Keeps track of information relevant to the chosen resolution of a long identifier
///
/// When we resolve an item such as System.Console.In we
/// resolve it in one step to a property/val/method etc. item. However
/// Visual Studio needs to know about the exact resolutions of the names
/// System and Console, i.e. the 'entity path' of the resolution.
///
/// Each of the resolution routines keeps track of the entity path and
/// ultimately calls ResolutionInfo.Method to record it for
/// later use by Visual Studio.
type ResolutionInfo =
    | ResolutionInfo of (*entityPath, reversed*)(range * EntityRef) list * (*warnings/errors*)(ResultTyparChecker -> unit) * tinstEnclosing: EnclosingTypeInst

    static member SendEntityPathToSink(sink, ncenv: NameResolver, nenv, occ, ad, traitCtxt, ResolutionInfo(entityPath, warnings, _), typarChecker) =
        entityPath |> List.iter (fun (m, eref: EntityRef) ->
            CheckEntityAttributes ncenv.g eref m |> CommitOperationResult
            CheckTyconAccessible ncenv.amap m ad eref |> ignore
            let item =
                if eref.IsModuleOrNamespace then
                    Item.ModuleOrNamespaces [eref]
                else
                    Item.Types(eref.DisplayName, [FreshenTycon ncenv traitCtxt m eref])
            CallNameResolutionSink sink (m, nenv, item, emptyTyparInst, occ, ad))
        warnings typarChecker

    static member Empty =
        ResolutionInfo([], (fun _ -> ()), emptyEnclosingTypeInst)

    member x.AddEntity info =
        let (ResolutionInfo(entityPath, warnings, tinstEnclosing)) = x
        ResolutionInfo(info :: entityPath, warnings, tinstEnclosing)

    member x.AddWarning f =
        let (ResolutionInfo(entityPath, warnings, tinstEnclosing)) = x
        ResolutionInfo(entityPath, (fun typarChecker -> f typarChecker; warnings typarChecker), tinstEnclosing)

    member x.WithEnclosingTypeInst tinstEnclosing =
        let (ResolutionInfo(entityPath, warnings, _)) = x
        ResolutionInfo(entityPath, warnings, tinstEnclosing)

    member x.EnclosingTypeInst =
        match x with
        | ResolutionInfo(tinstEnclosing=tinstEnclosing) -> tinstEnclosing

/// Resolve ambiguities between types overloaded by generic arity, based on number of type arguments.
/// Also check that we're not returning direct references to generated provided types.
//
// Given ambiguous C<>, C<_>    we resolve the ambiguous 'C.M' to C<> without warning
// Given ambiguous C<_>, C<_, _> we resolve the ambiguous 'C.M' to C<_> with an ambiguity error
// Given C<_>                   we resolve the ambiguous 'C.M' to C<_> with a warning if the argument or return types can't be inferred

// Given ambiguous C<>, C<_>    we resolve the ambiguous 'C()' to C<> without warning
// Given ambiguous C<_>, C<_, _> we resolve the ambiguous 'C()' to C<_> with an ambiguity error
// Given C<_>                   we resolve the ambiguous 'C()' to C<_> with a warning if the argument or return types can't be inferred

let CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities
        (tcrefs:(ResolutionInfo * TyconRef) list,
         typeNameResInfo: TypeNameResolutionInfo,
         genOk: PermitDirectReferenceToGeneratedType,
         m) =

    let tcrefs =
        tcrefs
        // remove later duplicates (if we've opened the same module more than once)
        |> List.distinctBy (fun (_, tcref) -> tcref.Stamp)
        // List.sortBy is a STABLE sort (the order matters!)
        |> List.sortBy (fun (resInfo, tcref) -> tcref.Typars(m).Length - resInfo.EnclosingTypeInst.Length)

    let tcrefs =
        match tcrefs with
        | ((resInfo, tcref) :: _) when
                // multiple types
                tcrefs.Length > 1 &&
                // no explicit type instantiation
                typeNameResInfo.StaticArgsInfo.HasNoStaticArgsInfo &&
                // some type arguments required on all types (note sorted by typar count above)
                ((tcref.Typars m).Length - resInfo.EnclosingTypeInst.Length) > 0 &&
                // plausible types have different arities
                (tcrefs |> Seq.distinctBy (fun (_, tcref) -> tcref.Typars(m).Length) |> Seq.length > 1)  ->
            [ for (resInfo, tcref) in tcrefs do
                let resInfo = resInfo.AddWarning (fun _typarChecker -> errorR(Error(FSComp.SR.nrTypeInstantiationNeededToDisambiguateTypesWithSameName(tcref.DisplayName, tcref.DisplayNameWithStaticParametersAndUnderscoreTypars), m)))
                yield (resInfo, tcref) ]

        | [(resInfo, tcref)] when  typeNameResInfo.StaticArgsInfo.HasNoStaticArgsInfo && ((tcref.Typars m).Length - resInfo.EnclosingTypeInst.Length) > 0 && typeNameResInfo.ResolutionFlag = ResolveTypeNamesToTypeRefs ->
            let resInfo =
                resInfo.AddWarning (fun (ResultTyparChecker typarChecker) ->
                    if not (typarChecker()) then
                        warning(Error(FSComp.SR.nrTypeInstantiationIsMissingAndCouldNotBeInferred(tcref.DisplayName, tcref.DisplayNameWithStaticParametersAndUnderscoreTypars), m)))
            [(resInfo, tcref)]

        | _ ->
            tcrefs

#if !NO_EXTENSIONTYPING
    for (_, tcref) in tcrefs do
        // Type generators can't be returned by name resolution, unless PermitDirectReferenceToGeneratedType.Yes
        CheckForDirectReferenceToGeneratedType (tcref, genOk, m)
#else
    genOk |> ignore
#endif

    tcrefs


//-------------------------------------------------------------------------
// Consume ids that refer to a namespace, module, or type
//-------------------------------------------------------------------------

/// Perform name resolution for an identifier which must resolve to be a module or namespace.
let rec ResolveLongIdentAsModuleOrNamespace sink (atMostOne: ResultCollectionSettings) (amap: Import.ImportMap) m first fullyQualified (nenv: NameResolutionEnv) ad (id:Ident) (rest: Ident list) isOpenDecl =
    if first && id.idText = MangledGlobalName then
        match rest with
        | [] ->
            error (Error(FSComp.SR.nrGlobalUsedOnlyAsFirstName(), id.idRange))
        | id2 :: rest2 ->
            ResolveLongIdentAsModuleOrNamespace sink atMostOne amap m false FullyQualified nenv ad id2 rest2 isOpenDecl
    else
        let notFoundAux (id: Ident) depth error (tcrefs: TyconRef seq) =
            let suggestNames (addToBuffer: string -> unit) =
                for tcref in tcrefs do
                    if IsEntityAccessible amap m ad tcref then
                        addToBuffer tcref.DisplayName
                        addToBuffer tcref.DemangledModuleOrNamespaceName

            UndefinedName(depth, error, id, suggestNames)

        let moduleOrNamespaces = nenv.ModulesAndNamespaces fullyQualified
        let namespaceNotFound =
            lazy
                seq { for kv in moduleOrNamespaces do
                        for modref in kv.Value do 
                            modref }
                |> notFoundAux id 0 FSComp.SR.undefinedNameNamespaceOrModule

        // Avoid generating the same error and name suggestion thunk twice It's not clear this is necessary
        // since it's just saving an allocation.
        let mutable moduleNotFoundErrorCache = None
        let moduleNotFound (modref: ModuleOrNamespaceRef) (mty: ModuleOrNamespaceType) (id: Ident) depth =
            match moduleNotFoundErrorCache with
            | Some (oldId, error) when Range.equals oldId id.idRange -> error
            | _ ->
                let error =
                    seq { for kv in mty.ModulesAndNamespacesByDemangledName do
                            modref.NestedTyconRef kv.Value }
                    |> notFoundAux id depth FSComp.SR.undefinedNameNamespace
                let error = raze error
                moduleNotFoundErrorCache <- Some(id.idRange, error)
                error

        let notifyNameResolution (modref: ModuleOrNamespaceRef) m =
            let item = Item.ModuleOrNamespaces [modref]
            let occurence = if isOpenDecl then ItemOccurence.Open else ItemOccurence.Use
            CallNameResolutionSink sink (m, nenv, item, emptyTyparInst, occurence, ad)

        let modrefs = 
            match moduleOrNamespaces.TryGetValue id.idText with 
            | true, modrefs -> modrefs 
            | _ -> []

        if not modrefs.IsEmpty then 
            /// Look through the sub-namespaces and/or modules
            let rec look depth (modref: ModuleOrNamespaceRef) (lid: Ident list) =
                let mty = modref.ModuleOrNamespaceType
                match lid with
                | [] -> 
                    success  [ (depth, modref, mty) ]

                | id :: rest ->
                    let modrefs =
                        match mty.ModulesAndNamespacesByDemangledName.TryGetValue id.idText with 
                        | true, res -> [res]
                        | _ -> []
                    
                    if not modrefs.IsEmpty then 
                        modrefs 
                        |> List.map (fun espec ->
                            let subref = modref.NestedTyconRef espec
                            if IsEntityAccessible amap m ad subref then
                                notifyNameResolution subref id.idRange
                                look (depth+1) subref rest
                            else
                                moduleNotFound modref mty id depth) 
                        |> List.reduce AddResults
                    else
                        moduleNotFound modref mty id depth

            modrefs
            |> List.map (fun modref ->
                if IsEntityAccessible amap m ad modref then
                    notifyNameResolution modref id.idRange
                    look 1 modref rest
                else
                    raze (namespaceNotFound.Force()))
            |> List.reduce AddResults
        else
            raze (namespaceNotFound.Force())

// Note - 'rest' is annotated due to a bug currently in Unity (see: https://github.com/dotnet/fsharp/pull/7427)
let ResolveLongIdentAsModuleOrNamespaceThen sink atMostOne amap m fullyQualified (nenv: NameResolutionEnv) ad id (rest: Ident list) isOpenDecl f =
    match ResolveLongIdentAsModuleOrNamespace sink ResultCollectionSettings.AllResults amap m true fullyQualified nenv ad id [] isOpenDecl with
    | Result modrefs ->
        match rest with
        | [] -> error(Error(FSComp.SR.nrUnexpectedEmptyLongId(), id.idRange))
        | id2 :: rest2 ->
            modrefs
            |> CollectResults2 atMostOne (fun (depth, modref, mty) ->
                let resInfo = ResolutionInfo.Empty.AddEntity(id.idRange, modref)
                f resInfo (depth+1) id.idRange modref mty id2 rest2)
    | Exception err -> Exception err

//-------------------------------------------------------------------------
// Bind name used in "new Foo.Bar(...)" constructs
//-------------------------------------------------------------------------

let private ResolveObjectConstructorPrim (ncenv: NameResolver) edenv resInfo m ad ty =
    let g = ncenv.g
    let amap = ncenv.amap
    if isDelegateTy g ty then
        success (resInfo, Item.DelegateCtor ty)
    else
        let ctorInfos = GetIntrinsicConstructorInfosOfType ncenv.InfoReader m ty
        if isNil ctorInfos && isInterfaceTy g ty then
            success (resInfo, Item.FakeInterfaceCtor ty)
        else
            let defaultStructCtorInfo =
                if (not (ctorInfos |> List.exists (fun x -> x.IsNullary)) &&
                    isStructTy g ty &&
                    not (isRecdTy g ty) &&
                    not (isUnionTy g ty))
                then
                    [DefaultStructCtor(g, ty)]
                else []
            if (isNil defaultStructCtorInfo && isNil ctorInfos) || (not (isAppTy g ty) && not (isAnyTupleTy g ty)) then
                raze (Error(FSComp.SR.nrNoConstructorsAvailableForType(NicePrint.minimalStringOfType edenv ty), m))
            else
                let ctorInfos = ctorInfos |> List.filter (IsMethInfoAccessible amap m ad)
                let metadataTy = convertToTypeWithMetadataIfPossible g ty
                success (resInfo, Item.MakeCtorGroup ((tcrefOfAppTy g metadataTy).LogicalName, (defaultStructCtorInfo@ctorInfos)))

/// Perform name resolution for an identifier which must resolve to be an object constructor.
let ResolveObjectConstructor (ncenv: NameResolver) edenv m ad ty =
    ResolveObjectConstructorPrim (ncenv: NameResolver) edenv [] m ad ty  |?> (fun (_resInfo, item) -> item)

//-------------------------------------------------------------------------
// Bind the "." notation (member lookup or lookup in a type)
//-------------------------------------------------------------------------

/// Used to report an error condition where name resolution failed due to an indeterminate type
exception IndeterminateType of range

/// Indicates the kind of lookup being performed. Note, this type should be made private to nameres.fs.
[<RequireQualifiedAccess>]
type LookupKind =
   | RecdField
   | Pattern
   | Expr
   | Type
   | Ctor


/// Try to find a union case of a type, with the given name
let TryFindUnionCaseOfType g ty nm =
    match tryAppTy g ty with
    | ValueSome(tcref, tinst) ->
        match tcref.GetUnionCaseByName nm with
        | None -> ValueNone
        | Some ucase -> ValueSome(UnionCaseInfo(tinst, tcref.MakeNestedUnionCaseRef ucase))
    | _ ->
        ValueNone

/// Try to find a union case of a type, with the given name
let TryFindAnonRecdFieldOfType g typ nm =
    match tryDestAnonRecdTy g typ with
    | ValueSome (anonInfo, tys) ->
        match anonInfo.SortedIds |> Array.tryFindIndex (fun x -> x.idText = nm) with
        | Some i -> Some (Item.AnonRecdField(anonInfo, tys, i, anonInfo.SortedIds.[i].idRange))
        | None -> None
    | ValueNone -> None

let CoreDisplayName(pinfo: PropInfo) =
    match pinfo with
    | FSProp(_, _, _, Some set) -> set.CoreDisplayName
    | FSProp(_, _, Some get, _) -> get.CoreDisplayName
    | FSProp _ -> failwith "unexpected (property must have either getter or setter)"
    | ILProp(ILPropInfo(_, def))  -> def.Name
#if !NO_EXTENSIONTYPING
    | ProvidedProp(_, pi, m) -> pi.PUntaint((fun pi -> pi.Name), m)
#endif

let DecodeFSharpEvent (pinfos: PropInfo list) ad g (ncenv: NameResolver) m =
    match pinfos with
    | [pinfo] when pinfo.IsFSharpEventProperty ->
        let nm = CoreDisplayName pinfo
        let minfos1 = GetImmediateIntrinsicMethInfosOfType (Some("add_"+nm), ad) g ncenv.amap m pinfo.ApparentEnclosingType
        let minfos2 = GetImmediateIntrinsicMethInfosOfType (Some("remove_"+nm), ad) g ncenv.amap m pinfo.ApparentEnclosingType
        match  minfos1, minfos2 with
        | [FSMeth(_, _, addValRef, _)], [FSMeth(_, _, removeValRef, _)] ->
            // FOUND PROPERTY-AS-EVENT AND CORRESPONDING ADD/REMOVE METHODS
            Some(Item.Event(FSEvent(g, pinfo, addValRef, removeValRef)))
        | _ ->
            // FOUND PROPERTY-AS-EVENT BUT DIDN'T FIND CORRESPONDING ADD/REMOVE METHODS
            Some(Item.Property (nm, pinfos))
    | pinfo :: _ ->
        let nm = CoreDisplayName pinfo
        Some(Item.Property (nm, pinfos))
    | _ ->
        None

/// Returns all record label names for the given type.
let GetRecordLabelsForType g nenv ty =
    let result = HashSet()
    if isRecdTy g ty then
      let typeName = NicePrint.minimalStringOfType nenv.eDisplayEnv ty
      for KeyValue(k, v) in nenv.eFieldLabels do
        if v |> List.exists (fun r -> r.TyconRef.DisplayName = typeName) then
          result.Add k |> ignore
    result

/// Get the nested types of the given type and check the nested types based on the type name resolution info.
let CheckNestedTypesOfType (ncenv: NameResolver) (resInfo: ResolutionInfo) ad traitCtxt nm (typeNameResInfo: TypeNameResolutionInfo) m ty =
    let tinstEnclosing, tcrefsNested = GetNestedTyconRefsOfType ncenv.InfoReader ncenv.amap (ad, Some nm, typeNameResInfo.StaticArgsInfo, true, m) ty
    let tcrefsNested = tcrefsNested |> List.map (fun tcrefNested -> (resInfo, tcrefNested))
    let tcrefsNested = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefsNested, typeNameResInfo, PermitDirectReferenceToGeneratedType.No, m)
    tcrefsNested |> List.map (fun (_, tcrefNested) -> MakeNestedType ncenv traitCtxt tinstEnclosing m tcrefNested)

// REVIEW: this shows up on performance logs. Consider for example endless resolutions of "List.map" to
// the empty set of results, or "x.Length" for a list or array type. This indicates it could be worth adding a cache here.
let rec ResolveLongIdentInTypePrim (ncenv: NameResolver) nenv lookupKind (resInfo: ResolutionInfo) depth m ad traitCtxt (id: Ident) (rest: Ident list) findFlag (typeNameResInfo: TypeNameResolutionInfo) ty =
    let g = ncenv.g
    let m = unionRanges m id.idRange
    let nm = id.idText // used to filter the searches of the tables
    let optFilter = Some nm // used to filter the searches of the tables
    let contentsSearchAccessible =
        let unionCaseSearch =
            match lookupKind with
            | LookupKind.Expr | LookupKind.Pattern -> TryFindUnionCaseOfType g ty nm
            | _ -> ValueNone

        // Lookup: datatype constructors take precedence
        match unionCaseSearch with
        | ValueSome ucase ->
            OneResult (success(resInfo, Item.UnionCase(ucase, false), rest))
        | ValueNone ->
            let anonRecdSearch =
                match lookupKind with
                | LookupKind.Expr -> TryFindAnonRecdFieldOfType g ty nm
                | _ -> None
            match anonRecdSearch with
            | Some item ->
                OneResult (success(resInfo, item, rest))
            | None ->
            let isLookUpExpr = (lookupKind = LookupKind.Expr)
            match TryFindIntrinsicNamedItemOfType ncenv.InfoReader (nm, ad) findFlag m ty with
            | Some (PropertyItem psets) when isLookUpExpr ->
                let pinfos = psets |> ExcludeHiddenOfPropInfos g ncenv.amap m

                // fold the available extension members into the overload resolution
                let extensionPropInfos = ExtensionPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv optFilter ad m ty

                // make sure to keep the intrinsic pinfos before the extension pinfos in the list,
                // since later on this logic is used when giving preference to intrinsic definitions
                match DecodeFSharpEvent (pinfos@extensionPropInfos) ad g ncenv m with
                | Some x -> success [resInfo, x, rest]
                | None -> raze (UndefinedName (depth, FSComp.SR.undefinedNameFieldConstructorOrMember, id, NoSuggestions))

            | Some(MethodItem msets) when isLookUpExpr ->
                let minfos = msets |> ExcludeHiddenOfMethInfos g ncenv.amap m

                // fold the available extension members into the overload resolution
                let extensionMethInfos = ExtensionMethInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv optFilter m ty

                success [resInfo, Item.MakeMethGroup (nm, minfos@extensionMethInfos), rest]
            | Some (ILFieldItem (finfo :: _))  when (match lookupKind with LookupKind.Expr | LookupKind.Pattern -> true | _ -> false) ->
                success [resInfo, Item.ILField finfo, rest]

            | Some (EventItem (einfo :: _)) when isLookUpExpr ->
                success [resInfo, Item.Event einfo, rest]

            | Some (RecdFieldItem rfinfo) when (match lookupKind with LookupKind.Expr | LookupKind.RecdField | LookupKind.Pattern -> true | _ -> false) ->
                success [resInfo, Item.RecdField rfinfo, rest]

            | _ ->

            let pinfos = ExtensionPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv optFilter ad m ty
            if not (isNil pinfos) && isLookUpExpr then OneResult(success (resInfo, Item.Property (nm, pinfos), rest)) else
            let minfos = ExtensionMethInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv optFilter m ty

            if not (isNil minfos) && isLookUpExpr then
                success [resInfo, Item.MakeMethGroup (nm, minfos), rest]
            elif isTyparTy g ty then raze (IndeterminateType(unionRanges m id.idRange))
            else NoResultsOrUsefulErrors

    match contentsSearchAccessible with
    | Result res when not (isNil res) -> contentsSearchAccessible
    | Exception _ -> contentsSearchAccessible
    | _ ->

    let nestedSearchAccessible =
        match rest with
        | [] ->
            let nestedTypes = CheckNestedTypesOfType ncenv resInfo ad traitCtxt nm typeNameResInfo m ty
            if isNil nestedTypes then
                NoResultsOrUsefulErrors
            else
                match typeNameResInfo.ResolutionFlag with
                | ResolveTypeNamesToCtors ->
                    nestedTypes
                    |> CollectAtMostOneResult (ResolveObjectConstructorPrim ncenv nenv.eDisplayEnv resInfo m ad)
                    |> MapResults (fun (resInfo, item) -> (resInfo, item, []))
                | ResolveTypeNamesToTypeRefs ->
                    OneSuccess (resInfo, Item.Types (nm, nestedTypes), rest)
        | id2 :: rest2 ->
            let nestedTypes = CheckNestedTypesOfType ncenv resInfo ad traitCtxt nm (TypeNameResolutionInfo.ResolveToTypeRefs TypeNameResolutionStaticArgsInfo.Indefinite) m ty
            ResolveLongIdentInNestedTypes ncenv nenv lookupKind resInfo (depth+1) id m ad traitCtxt id2 rest2 findFlag typeNameResInfo nestedTypes

    match nestedSearchAccessible with
    | Result res when not (isNil res) -> nestedSearchAccessible
    | _ ->
        let suggestMembers (addToBuffer: string -> unit) =
            for p in ExtensionPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None ad m ty do
                addToBuffer p.PropertyName

            for m in ExtensionMethInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None m ty do
                addToBuffer m.DisplayName

            for p in GetIntrinsicPropInfosOfType ncenv.InfoReader None ad AllowMultiIntfInstantiations.No findFlag m ty do
                addToBuffer p.PropertyName

            for m in GetIntrinsicMethInfosOfType ncenv.InfoReader None ad AllowMultiIntfInstantiations.No findFlag m ty do
                if not m.IsClassConstructor && not m.IsConstructor then
                    addToBuffer m.DisplayName

            for l in GetRecordLabelsForType g nenv ty do
                addToBuffer l

            match lookupKind with
            | LookupKind.Expr | LookupKind.Pattern ->
                match tryTcrefOfAppTy g ty with
                | ValueSome tcref ->
                    for uc in tcref.UnionCasesArray do
                        addToBuffer uc.DisplayName
                | _ -> ()
            | _ -> ()

        let errorTextF s =
            match tryTcrefOfAppTy g ty with
            | ValueSome tcref ->
                FSComp.SR.undefinedNameFieldConstructorOrMemberWhenTypeIsKnown(tcref.DisplayNameWithStaticParametersAndTypars, s)
            | _ ->
                FSComp.SR.undefinedNameFieldConstructorOrMember(s)

        raze (UndefinedName (depth, errorTextF, id, suggestMembers))

and ResolveLongIdentInNestedTypes (ncenv: NameResolver) nenv lookupKind resInfo depth id m ad traitCtxt (id2: Ident) (rest: Ident list) findFlag typeNameResInfo tys =
    tys
    |> CollectAtMostOneResult (fun ty ->
        let resInfo = 
             match tryTcrefOfAppTy ncenv.g ty with
             | ValueSome tcref ->
                resInfo.AddEntity(id.idRange, tcref) 
             | _ ->
                resInfo
        ResolveLongIdentInTypePrim ncenv nenv lookupKind resInfo depth m ad traitCtxt id2 rest findFlag typeNameResInfo ty
        |> AtMostOneResult m)

/// Resolve a long identifier using type-qualified name resolution.
let ResolveLongIdentInType sink ncenv nenv lookupKind m ad traitCtxt id findFlag typeNameResInfo ty =
    let resInfo, item, rest =
        ResolveLongIdentInTypePrim (ncenv: NameResolver) nenv lookupKind ResolutionInfo.Empty 0 m ad traitCtxt id [] findFlag typeNameResInfo ty
        |> AtMostOneResult m
        |> ForceRaise

    ResolutionInfo.SendEntityPathToSink (sink, ncenv, nenv, ItemOccurence.UseInType, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> CheckAllTyparsInferrable ncenv.amap m item))
    item, rest

let private ResolveLongIdentInTyconRef (ncenv: NameResolver) nenv lookupKind (resInfo: ResolutionInfo) depth m ad traitCtxt id rest typeNameResInfo tcref =
#if !NO_EXTENSIONTYPING
    // No dotting through type generators to get to a member!
    CheckForDirectReferenceToGeneratedType (tcref, PermitDirectReferenceToGeneratedType.No, m)
#endif
    let ty = 
        match resInfo.EnclosingTypeInst with
        | [] -> FreshenTycon ncenv traitCtxt m tcref
        | tinstEnclosing -> FreshenTyconWithEnclosingTypeInst ncenv traitCtxt m tinstEnclosing tcref
    ty |> ResolveLongIdentInTypePrim ncenv nenv lookupKind resInfo depth m ad traitCtxt id rest IgnoreOverrides typeNameResInfo

let private ResolveLongIdentInTyconRefs atMostOne (ncenv: NameResolver) nenv lookupKind depth m ad traitCtxt id rest typeNameResInfo idRange tcrefs =
    tcrefs |> CollectResults2 atMostOne (fun (resInfo: ResolutionInfo, tcref) ->
        let resInfo = resInfo.AddEntity(idRange, tcref)
        tcref |> ResolveLongIdentInTyconRef ncenv nenv lookupKind resInfo depth m ad traitCtxt id rest typeNameResInfo |> AtMostOneResult m)

//-------------------------------------------------------------------------
// ResolveExprLongIdentInModuleOrNamespace
//-------------------------------------------------------------------------

let (|AccessibleEntityRef|_|) amap m ad (modref: ModuleOrNamespaceRef) mspec =
    let eref = modref.NestedTyconRef mspec
    if IsEntityAccessible amap m ad eref then Some eref else None

let rec ResolveExprLongIdentInModuleOrNamespace (ncenv: NameResolver) nenv (typeNameResInfo: TypeNameResolutionInfo) ad traitCtxt resInfo depth m modref (mty: ModuleOrNamespaceType) (id: Ident) (rest: Ident list) =
    // resInfo records the modules or namespaces actually relevant to a resolution
    let m = unionRanges m id.idRange
    match mty.AllValsByLogicalName.TryGetValue id.idText with
    | true, vspec when IsValAccessible ad (mkNestedValRef modref vspec) ->
        success(resInfo, Item.Value (mkNestedValRef modref vspec), rest)
    | _->
    match mty.ExceptionDefinitionsByDemangledName.TryGetValue id.idText with
    | true, excon when IsTyconReprAccessible ncenv.amap m ad (modref.NestedTyconRef excon) ->
        success (resInfo, Item.ExnCase (modref.NestedTyconRef excon), rest)
    | _ ->
        // Something in a discriminated union without RequireQualifiedAccess attribute?
        let unionSearch, hasRequireQualifiedAccessAttribute =
            match TryFindTypeWithUnionCase modref id with
            | Some tycon when IsTyconReprAccessible ncenv.amap m ad (modref.NestedTyconRef tycon) ->
                let ucref = mkUnionCaseRef (modref.NestedTyconRef tycon) id.idText
                let ucinfo = FreshenUnionCaseRef ncenv traitCtxt m ucref
                let hasRequireQualifiedAccessAttribute = HasFSharpAttribute ncenv.g ncenv.g.attrib_RequireQualifiedAccessAttribute tycon.Attribs
                success [resInfo, Item.UnionCase(ucinfo, hasRequireQualifiedAccessAttribute), rest], hasRequireQualifiedAccessAttribute
            | _ -> NoResultsOrUsefulErrors, false

        match unionSearch with
        | Result (res :: _) when not hasRequireQualifiedAccessAttribute -> success res
        | _ ->

        // Something in a type?
        let tyconSearch =
            let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, (if isNil rest then typeNameResInfo.StaticArgsInfo else TypeNameResolutionStaticArgsInfo.Indefinite), modref)
            if isNil tcrefs then NoResultsOrUsefulErrors else
            let tcrefs = tcrefs |> List.map (fun tcref -> (resInfo, tcref))

            match rest with
            | id2 :: rest2 ->

                let tcrefs =
                    let typeNameResInfo = TypeNameResolutionInfo.ResolveToTypeRefs typeNameResInfo.StaticArgsInfo
                    CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, PermitDirectReferenceToGeneratedType.No, unionRanges m id.idRange)

                ResolveLongIdentInTyconRefs ResultCollectionSettings.AtMostOneResult ncenv nenv LookupKind.Expr (depth+1) m ad traitCtxt id2 rest2 typeNameResInfo id.idRange tcrefs

            // Check if we've got some explicit type arguments
            | _ ->
                let tcrefs = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, PermitDirectReferenceToGeneratedType.No, unionRanges m id.idRange)
                match typeNameResInfo.ResolutionFlag with
                | ResolveTypeNamesToTypeRefs ->
                    success [ for (resInfo, tcref) in tcrefs do
                                    let ty = FreshenTycon ncenv traitCtxt m tcref
                                    let item = (resInfo, Item.Types(id.idText, [ty]), [])
                                    yield item ]
                | ResolveTypeNamesToCtors ->
                    tcrefs
                    |> List.map (fun (resInfo, tcref) -> resInfo, FreshenTycon ncenv traitCtxt m tcref)
                    |> CollectAtMostOneResult (fun (resInfo, ty) -> ResolveObjectConstructorPrim ncenv nenv.eDisplayEnv resInfo id.idRange ad ty)
                    |> MapResults (fun (resInfo, item) -> (resInfo, item, []))


        // Something in a sub-namespace or sub-module
        let moduleSearch() =
            match rest with
            | id2 :: rest2 ->
                match mty.ModulesAndNamespacesByDemangledName.TryGetValue id.idText with
                | true, AccessibleEntityRef ncenv.amap m ad modref submodref ->
                    let resInfo = resInfo.AddEntity(id.idRange, submodref)

                    OneResult (ResolveExprLongIdentInModuleOrNamespace ncenv nenv typeNameResInfo ad traitCtxt resInfo (depth+1) m submodref submodref.ModuleOrNamespaceType id2 rest2)
                | _ ->
                    NoResultsOrUsefulErrors
            | _ ->
                NoResultsOrUsefulErrors

        match tyconSearch +++ moduleSearch +++ (fun _ -> unionSearch) with
        | Result [] ->
            let suggestPossibleTypesAndNames (addToBuffer: string -> unit) =
                for e in modref.ModuleOrNamespaceType.AllEntities do
                    if IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef e) then
                        addToBuffer e.DisplayName

                        if e.IsUnionTycon then
                            let hasRequireQualifiedAccessAttribute = HasFSharpAttribute ncenv.g ncenv.g.attrib_RequireQualifiedAccessAttribute e.Attribs
                            if not hasRequireQualifiedAccessAttribute then
                                for uc in e.UnionCasesArray do
                                    addToBuffer uc.DisplayName

                for kv in mty.ModulesAndNamespacesByDemangledName do
                    if IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef kv.Value) then
                        addToBuffer kv.Value.DisplayName

                for e in modref.ModuleOrNamespaceType.AllValsByLogicalName do
                    if IsValAccessible ad (mkNestedValRef modref e.Value) then
                        addToBuffer e.Value.DisplayName

                for e in modref.ModuleOrNamespaceType.ExceptionDefinitionsByDemangledName do
                    if IsTyconReprAccessible ncenv.amap m ad (modref.NestedTyconRef e.Value) then
                        addToBuffer e.Value.DisplayName

            raze (UndefinedName(depth, FSComp.SR.undefinedNameValueConstructorNamespaceOrType, id, suggestPossibleTypesAndNames))
        | results -> AtMostOneResult id.idRange results

/// An identifier has resolved to a type name in an expression (corresponding to one or more TyconRefs).
/// Return either a set of constructors (later refined by overload resolution), or a set of TyconRefs.
let ChooseTyconRefInExpr (ncenv: NameResolver, m, ad, traitCtxt, nenv, id: Ident, typeNameResInfo: TypeNameResolutionInfo, tcrefs) =
    let tcrefs = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, PermitDirectReferenceToGeneratedType.No, m)

    let tys = 
        tcrefs 
        |> List.map (fun (resInfo, tcref) -> 
            match resInfo.EnclosingTypeInst with
            | [] ->
                (resInfo, FreshenTycon ncenv traitCtxt m tcref)
            | tinstEnclosing ->
                (resInfo, FreshenTyconWithEnclosingTypeInst ncenv traitCtxt m tinstEnclosing tcref))

    match typeNameResInfo.ResolutionFlag with
    | ResolveTypeNamesToCtors ->
        tys
        |> CollectAtMostOneResult (fun (resInfo, ty) -> ResolveObjectConstructorPrim ncenv nenv.eDisplayEnv resInfo id.idRange ad ty)
        |> MapResults (fun (resInfo, item) -> (resInfo, item))
    | ResolveTypeNamesToTypeRefs ->
        success (tys |> List.map (fun (resInfo, ty) -> (resInfo, Item.Types(id.idText, [ty]))))

/// Resolves the given tycons.
/// For each tycon, return resolution info that could contain enclosing type instantations.
let ResolveUnqualifiedTyconRefs nenv tcrefs =
    let resInfo = ResolutionInfo.Empty

    tcrefs 
    |> List.map (fun tcref -> 
        match nenv.eUnqualifiedEnclosingTypeInsts.TryFind tcref with
        | None ->
            (resInfo, tcref)
        | Some tinst ->
            (resInfo.WithEnclosingTypeInst tinst, tcref))

/// Resolve F# "A.B.C" syntax in expressions
/// Not all of the sequence will necessarily be swallowed, i.e. we return some identifiers
/// that may represent further actions, e.g. further lookups.
let rec ResolveExprLongIdentPrim sink (ncenv: NameResolver) first fullyQualified m ad traitCtxt nenv (typeNameResInfo: TypeNameResolutionInfo) (id: Ident) (rest: Ident list) isOpenDecl =
    let canSuggestThisItem (item:Item) =
        // All items can be suggested except nameof when it comes from FSharp.Core.dll and the nameof feature is not enabled
        match item with
        | Item.Value v ->
            let isNameOfOperator = valRefEq ncenv.g ncenv.g.nameof_vref v
            if isNameOfOperator && not (ncenv.g.langVersion.SupportsFeature LanguageFeature.NameOf) then false
            else true
        | _ -> true

    if first && id.idText = MangledGlobalName then
        match rest with
        | [] ->
            raze (Error(FSComp.SR.nrGlobalUsedOnlyAsFirstName(), id.idRange))
        | [next] ->
            ResolveExprLongIdentPrim sink ncenv false fullyQualified m ad traitCtxt nenv typeNameResInfo next [] isOpenDecl
        | id2 :: rest2 ->
            ResolveExprLongIdentPrim sink ncenv false FullyQualified m ad traitCtxt nenv typeNameResInfo id2 rest2 isOpenDecl
    else
        if isNil rest && fullyQualified <> FullyQualified then
            let mutable typeError = None
            // Single identifier.  Lookup the unqualified names in the environment
            let envSearch =
                match nenv.eUnqualifiedItems.TryGetValue id.idText with

                // The name is a type name and it has not been clobbered by some other name
                | true, Item.UnqualifiedType tcrefs ->

                    // Do not use type names from the environment if an explicit type instantiation is
                    // given and the number of type parameters do not match
                    let tcrefs =
                        tcrefs 
                        |> ResolveUnqualifiedTyconRefs nenv
                        |> List.filter (fun (resInfo, tcref) ->
                            typeNameResInfo.StaticArgsInfo.HasNoStaticArgsInfo ||
                            typeNameResInfo.StaticArgsInfo.NumStaticArgs = tcref.Typars(m).Length - resInfo.EnclosingTypeInst.Length)

                    let search = ChooseTyconRefInExpr (ncenv, m, ad, traitCtxt, nenv, id, typeNameResInfo, tcrefs)
                    match AtMostOneResult m search with
                    | Result (resInfo, item) ->
                        ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> CheckAllTyparsInferrable ncenv.amap m item))
                        Some(resInfo.EnclosingTypeInst, item, rest)
                    | Exception e -> 
                        typeError <- Some e
                        None

                | true, res ->
                    let fresh = ResolveUnqualifiedItem ncenv traitCtxt nenv m res
                    match fresh with
                    | Item.Value value ->
                        let isNameOfOperator = valRefEq ncenv.g ncenv.g.nameof_vref value
                        if isNameOfOperator && not (ncenv.languageSupportsNameOf) then
                            // Do not resolve `nameof` if the feature is unsupported, even if it is FSharp.Core
                            None
                         else
                            Some (emptyEnclosingTypeInst, fresh, rest)
                    | _ -> Some (emptyEnclosingTypeInst, fresh, rest)
                | _ ->
                    None

            match envSearch with
            | Some res -> success res
            | None ->
                let innerSearch =
                    // Check if it's a type name, e.g. a constructor call or a type instantiation
                    let ctorSearch =
                        let tcrefs = 
                            LookupTypeNameInEnvMaybeHaveArity fullyQualified id.idText typeNameResInfo nenv
                            |> ResolveUnqualifiedTyconRefs nenv
                        ChooseTyconRefInExpr (ncenv, m, ad, traitCtxt, nenv, id, typeNameResInfo, tcrefs)

                    let implicitOpSearch() =
                        if IsMangledOpName id.idText then
                            success [(ResolutionInfo.Empty, Item.ImplicitOp(id, ref None))]
                        else
                            NoResultsOrUsefulErrors

                    ctorSearch +++ implicitOpSearch

                let res =
                    match AtMostOneResult m innerSearch with
                    | Result _ as res -> res
                    | _ ->
                        let failingCase =
                            match typeError with
                            | Some e -> raze e
                            | _ ->
                                let suggestNamesAndTypes (addToBuffer: string -> unit) =
                                    for e in nenv.eUnqualifiedItems do
                                        if canSuggestThisItem e.Value then
                                            addToBuffer e.Value.DisplayName

                                    for e in nenv.TyconsByDemangledNameAndArity fullyQualified do
                                        if IsEntityAccessible ncenv.amap m ad e.Value then
                                            addToBuffer e.Value.DisplayName

                                    for kv in nenv.ModulesAndNamespaces fullyQualified do
                                        for modref in kv.Value do
                                            if IsEntityAccessible ncenv.amap m ad modref then
                                                addToBuffer modref.DisplayName
                                                addToBuffer modref.DemangledModuleOrNamespaceName

                                    // check if the user forgot to use qualified access
                                    for e in nenv.eTyconsByDemangledNameAndArity do                                    
                                        let hasRequireQualifiedAccessAttribute = HasFSharpAttribute ncenv.g ncenv.g.attrib_RequireQualifiedAccessAttribute e.Value.Attribs
                                        if hasRequireQualifiedAccessAttribute then
                                            if e.Value.IsUnionTycon && e.Value.UnionCasesArray |> Array.exists (fun c -> c.DisplayName = id.idText) then
                                                addToBuffer (e.Value.DisplayName + "." + id.idText)

                                raze (UndefinedName(0, FSComp.SR.undefinedNameValueOfConstructor, id, suggestNamesAndTypes))
                        failingCase
                match res with 
                | Exception e -> raze e
                | Result (resInfo, item) -> 
                ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> CheckAllTyparsInferrable ncenv.amap m item))
                success (resInfo.EnclosingTypeInst, item, rest)

        // A compound identifier.
        // It still might be a value in the environment, or something in an F# module, namespace, type, or nested type
        else
            let m = unionRanges m id.idRange
            // Values in the environment take total priority, but constructors do NOT for compound lookups, e.g. if someone in some imported
            // module has defined a constructor "String" (common enough) then "String.foo" doesn't give an error saying 'constructors have no members'
            // Instead we go lookup the String module or type.
            let ValIsInEnv nm =
                match fullyQualified with
                | FullyQualified -> false
                | _ ->
                    match nenv.eUnqualifiedItems.TryGetValue nm with
                    | true, Item.Value _ -> true
                    | _ -> false

            if ValIsInEnv id.idText then
              success (emptyEnclosingTypeInst, nenv.eUnqualifiedItems.[id.idText], rest)
            else
              // Otherwise modules are searched first. REVIEW: modules and types should be searched together.
              // For each module referenced by 'id', search the module as if it were an F# module and/or a .NET namespace.
              let moduleSearch ad () =
                 ResolveLongIdentAsModuleOrNamespaceThen sink ResultCollectionSettings.AtMostOneResult ncenv.amap m fullyQualified nenv ad id rest isOpenDecl
                     (ResolveExprLongIdentInModuleOrNamespace ncenv nenv typeNameResInfo ad traitCtxt)

              // REVIEW: somewhat surprisingly, this shows up on performance traces, with tcrefs non-nil.
              // This seems strange since we would expect in the vast majority of cases tcrefs is empty here.
              let tyconSearch ad () =
                  let tcrefs = LookupTypeNameInEnvNoArity fullyQualified id.idText nenv

                  if isNil tcrefs then NoResultsOrUsefulErrors else
                  match rest with
                  | id2 :: rest2 ->
                    let tcrefs = ResolveUnqualifiedTyconRefs nenv tcrefs
                    let tcrefs =
                       let typeNameResInfo = TypeNameResolutionInfo.ResolveToTypeRefs typeNameResInfo.StaticArgsInfo
                       CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, PermitDirectReferenceToGeneratedType.No, unionRanges m id.idRange)
                    ResolveLongIdentInTyconRefs ResultCollectionSettings.AtMostOneResult ncenv nenv LookupKind.Expr 1 m ad traitCtxt id2 rest2 typeNameResInfo id.idRange tcrefs
                  | _ ->
                    NoResultsOrUsefulErrors

              let search =
                let envSearch () =
                    match fullyQualified with
                    | FullyQualified ->
                        NoResultsOrUsefulErrors
                    | OpenQualified ->
                        match nenv.eUnqualifiedItems.TryGetValue id.idText with
                        | true, Item.UnqualifiedType _
                        | false, _ -> NoResultsOrUsefulErrors
                        | true, res -> OneSuccess (ResolutionInfo.Empty, ResolveUnqualifiedItem ncenv traitCtxt nenv m res, rest)

                moduleSearch ad () +++ tyconSearch ad +++ envSearch

              let res =
                  match AtMostOneResult m search with
                  | Result _ as res -> res
                  | _ ->
                      let innerSearch = search +++ (moduleSearch AccessibleFromSomeFSharpCode) +++ (tyconSearch AccessibleFromSomeFSharpCode)

                      let suggestEverythingInScope (addToBuffer: string -> unit) =
                        for kv in nenv.ModulesAndNamespaces fullyQualified do
                            for modref in kv.Value do
                                if IsEntityAccessible ncenv.amap m ad modref then
                                    addToBuffer modref.DisplayName
                                    addToBuffer modref.DemangledModuleOrNamespaceName

                        for e in nenv.TyconsByDemangledNameAndArity fullyQualified do
                            if IsEntityAccessible ncenv.amap m ad e.Value then
                                addToBuffer e.Value.DisplayName

                        for e in nenv.eUnqualifiedItems do
                            if canSuggestThisItem e.Value then
                                addToBuffer e.Value.DisplayName

                      match innerSearch with
                      | Exception (UndefinedName(0, _, id1, suggestionsF)) when Range.equals id.idRange id1.idRange ->
                          let mergeSuggestions addToBuffer = 
                              suggestionsF addToBuffer 
                              suggestEverythingInScope addToBuffer
                          raze (UndefinedName(0, FSComp.SR.undefinedNameValueNamespaceTypeOrModule, id, mergeSuggestions))
                      | Exception err -> raze err
                      | Result (res :: _) -> success res
                      | Result [] ->
                            raze (UndefinedName(0, FSComp.SR.undefinedNameValueNamespaceTypeOrModule, id, suggestEverythingInScope))

              match res with 
              | Exception e -> raze e
              | Result (resInfo, item, rest) -> 
                  ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> CheckAllTyparsInferrable ncenv.amap m item))
                  success (resInfo.EnclosingTypeInst, item, rest)

let ResolveExprLongIdent sink (ncenv: NameResolver) m ad traitCtxt nenv typeNameResInfo lid =
    match lid with
    | [] -> raze (Error(FSComp.SR.nrInvalidExpression(textOfLid lid), m))
    | id :: rest -> ResolveExprLongIdentPrim sink ncenv true OpenQualified m ad traitCtxt nenv typeNameResInfo id rest false

//-------------------------------------------------------------------------
// Resolve F#/IL "." syntax in patterns
//-------------------------------------------------------------------------

let rec ResolvePatternLongIdentInModuleOrNamespace (ncenv: NameResolver) nenv numTyArgsOpt ad traitCtxt resInfo depth m modref (mty: ModuleOrNamespaceType) (id: Ident) (rest: Ident list) =
    let m = unionRanges m id.idRange
    match TryFindTypeWithUnionCase modref id with
    | Some tycon when IsTyconReprAccessible ncenv.amap m ad (modref.NestedTyconRef tycon) ->
        let tcref = modref.NestedTyconRef tycon
        let ucref = mkUnionCaseRef tcref id.idText
        let showDeprecated = HasFSharpAttribute ncenv.g ncenv.g.attrib_RequireQualifiedAccessAttribute tycon.Attribs
        let ucinfo = FreshenUnionCaseRef ncenv traitCtxt m ucref
        success (resInfo, Item.UnionCase(ucinfo, showDeprecated), rest)
    | _ ->
    match mty.ExceptionDefinitionsByDemangledName.TryGetValue id.idText with
    | true, exnc when IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef exnc) ->
        success (resInfo, Item.ExnCase (modref.NestedTyconRef exnc), rest)
    | _ ->
    // An active pattern constructor in a module
    match (ActivePatternElemsOfModuleOrNamespace ncenv.g modref).TryGetValue id.idText with
    | true, (APElemRef(_, vref, _, _) as apref) when IsValAccessible ad vref ->
        success (resInfo, Item.ActivePatternCase apref, rest)
    | _ ->
    match mty.AllValsByLogicalName.TryGetValue id.idText with
    | true, vspec when IsValAccessible ad (mkNestedValRef modref vspec) ->
        success(resInfo, Item.Value (mkNestedValRef modref vspec), rest)
    | _ ->
    let tcrefs = lazy (
        LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, TypeNameResolutionStaticArgsInfo.Indefinite, modref)
        |> List.map (fun tcref -> (resInfo, tcref)))

    // Something in a type? e.g. a literal field
    let tyconSearch =
        match rest with
        | id2 :: rest2 ->
            let tcrefs = tcrefs.Force()
            ResolveLongIdentInTyconRefs ResultCollectionSettings.AtMostOneResult (ncenv: NameResolver) nenv LookupKind.Pattern (depth+1) m ad traitCtxt id2 rest2 numTyArgsOpt id.idRange tcrefs
        | _ ->
            NoResultsOrUsefulErrors

    // Constructor of a type?
    let ctorSearch() =
        if isNil rest then
            tcrefs.Force()
            |> List.map (fun (resInfo, tcref) -> (resInfo, FreshenTycon ncenv traitCtxt m tcref))
            |> CollectAtMostOneResult (fun (resInfo, ty) -> ResolveObjectConstructorPrim ncenv nenv.eDisplayEnv resInfo id.idRange ad ty)
            |> MapResults (fun (resInfo, item) -> (resInfo, item, []))
        else
            NoResultsOrUsefulErrors

    // Something in a sub-namespace or sub-module or nested-type
    let moduleSearch() =
        match rest with
        | id2 :: rest2 ->
            match mty.ModulesAndNamespacesByDemangledName.TryGetValue id.idText with
            | true, AccessibleEntityRef ncenv.amap m ad modref submodref ->
                let resInfo = resInfo.AddEntity(id.idRange, submodref)
                OneResult (ResolvePatternLongIdentInModuleOrNamespace ncenv nenv numTyArgsOpt ad traitCtxt resInfo (depth+1) m submodref submodref.ModuleOrNamespaceType id2 rest2)
            | _ ->
                NoResultsOrUsefulErrors
        | [] -> NoResultsOrUsefulErrors

    match tyconSearch +++ ctorSearch +++ moduleSearch with
    | Result [] ->
        let suggestPossibleTypes (addToBuffer: string -> unit) =
            for kv in mty.ModulesAndNamespacesByDemangledName do
                if IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef kv.Value) then
                    addToBuffer kv.Value.DisplayName
                    addToBuffer kv.Value.DemangledModuleOrNamespaceName

            for e in nenv.TyconsByDemangledNameAndArity FullyQualifiedFlag.OpenQualified do
                if IsEntityAccessible ncenv.amap m ad e.Value then
                    addToBuffer e.Value.DisplayName

        raze (UndefinedName(depth, FSComp.SR.undefinedNameConstructorModuleOrNamespace, id, suggestPossibleTypes))
    | results -> AtMostOneResult id.idRange results

/// Used to report a warning condition for the use of upper-case identifiers in patterns
exception UpperCaseIdentifierInPattern of range

/// Indicates if a warning should be given for the use of upper-case identifiers in patterns
type WarnOnUpperFlag = WarnOnUpperCase | AllIdsOK

// Long ID in a pattern
let rec ResolvePatternLongIdentPrim sink (ncenv: NameResolver) fullyQualified warnOnUpper newDef m ad traitCtxt nenv numTyArgsOpt (id: Ident) (rest: Ident list) =
    if id.idText = MangledGlobalName then
        match rest with
        | [] ->
            error (Error(FSComp.SR.nrGlobalUsedOnlyAsFirstName(), id.idRange))
        | id2 :: rest2 ->
            ResolvePatternLongIdentPrim sink ncenv FullyQualified warnOnUpper newDef m ad traitCtxt nenv numTyArgsOpt id2 rest2
    else
        // Single identifiers in patterns
        if isNil rest && fullyQualified <> FullyQualified then
            // Single identifiers in patterns - bind to constructors and active patterns
            // For the special case of
            //   let C = x
            match nenv.ePatItems.TryGetValue id.idText with
            | true, res when not newDef  -> ResolveUnqualifiedItem ncenv traitCtxt nenv m res
            | _ ->
            // Single identifiers in patterns - variable bindings
            if not newDef &&
               (warnOnUpper = WarnOnUpperCase) &&
               id.idText.Length >= 3 &&
               System.Char.ToLowerInvariant id.idText.[0] <> id.idText.[0] then
              warning(UpperCaseIdentifierInPattern m)
            Item.NewDef id

        // Long identifiers in patterns
        else
            let moduleSearch ad () =
                ResolveLongIdentAsModuleOrNamespaceThen sink ResultCollectionSettings.AtMostOneResult ncenv.amap m fullyQualified nenv ad id rest false
                    (ResolvePatternLongIdentInModuleOrNamespace ncenv nenv numTyArgsOpt ad traitCtxt)

            let tyconSearch ad =
                match rest with
                | id2 :: rest2 ->
                    let tcrefs = LookupTypeNameInEnvNoArity fullyQualified id.idText nenv
                    if isNil tcrefs then NoResultsOrUsefulErrors else
                    let tcrefs = tcrefs |> List.map (fun tcref -> (ResolutionInfo.Empty, tcref))
                    ResolveLongIdentInTyconRefs ResultCollectionSettings.AtMostOneResult ncenv nenv LookupKind.Pattern 1 id.idRange ad traitCtxt id2 rest2 numTyArgsOpt id.idRange tcrefs
                | _ ->
                    NoResultsOrUsefulErrors

            let resInfo, res, rest =
                match AtMostOneResult m (tyconSearch ad +++ (moduleSearch ad)) with
                | Result _ as res -> ForceRaise res
                | _ ->

                tyconSearch AccessibleFromSomeFSharpCode +++ (moduleSearch AccessibleFromSomeFSharpCode)
                |> AtMostOneResult m
                |> ForceRaise

            ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> true))

            match rest with
            | [] -> res
            | element :: _ -> error(Error(FSComp.SR.nrIsNotConstructorOrLiteral(), element.idRange))

/// Resolve a long identifier when used in a pattern.
let ResolvePatternLongIdent sink (ncenv: NameResolver) warnOnUpper newDef m ad traitCtxt nenv numTyArgsOpt (lid: Ident list) =
    match lid with
    | [] -> error(Error(FSComp.SR.nrUnexpectedEmptyLongId(), m))
    | id :: rest -> ResolvePatternLongIdentPrim sink ncenv OpenQualified warnOnUpper newDef m ad traitCtxt nenv numTyArgsOpt id rest

//-------------------------------------------------------------------------
// Resolve F#/IL "." syntax in types
//-------------------------------------------------------------------------

/// Resolve nested types referenced through a .NET abbreviation.
//
// Note the generic case is not supported by F#, so
//    type X = List<int>
//
// X.ListEnumerator // does not resolve
//
let ResolveNestedTypeThroughAbbreviation (ncenv: NameResolver) (tcref: TyconRef) m =
    if tcref.IsTypeAbbrev && tcref.Typars(m).IsEmpty then 
        match tryAppTy ncenv.g tcref.TypeAbbrev.Value with
        | ValueSome (abbrevTcref, []) -> abbrevTcref
        | _ -> tcref
    else
        tcref

/// Resolve a long identifier representing a type name
let rec ResolveTypeLongIdentInTyconRefPrim (ncenv: NameResolver) (typeNameResInfo: TypeNameResolutionInfo) ad resInfo genOk depth m (tcref: TyconRef) (id: Ident) (rest: Ident list) =
    let tcref = ResolveNestedTypeThroughAbbreviation ncenv tcref m
    match rest with
    | [] ->
#if !NO_EXTENSIONTYPING
        // No dotting through type generators to get to a nested type!
        CheckForDirectReferenceToGeneratedType (tcref, PermitDirectReferenceToGeneratedType.No, m)
#endif
        let m = unionRanges m id.idRange
        let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, typeNameResInfo.StaticArgsInfo, tcref)
        let tcrefs = tcrefs |> List.map (fun tcref -> (resInfo, tcref))
        let tcrefs = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, genOk, m)
        match tcrefs with
        | tcref :: _ -> success tcref
        | [] ->
            let suggestTypes (addToBuffer: string -> unit) =
                for e in tcref.ModuleOrNamespaceType.TypesByDemangledNameAndArity do
                    addToBuffer e.Value.DisplayName

            raze (UndefinedName(depth, FSComp.SR.undefinedNameType, id, suggestTypes))
    | id2 :: rest2 ->
#if !NO_EXTENSIONTYPING
        // No dotting through type generators to get to a nested type!
        CheckForDirectReferenceToGeneratedType (tcref, PermitDirectReferenceToGeneratedType.No, m)
#endif
        let m = unionRanges m id.idRange
        // Search nested types
        let tyconSearch =
            let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, TypeNameResolutionStaticArgsInfo.Indefinite, tcref)
            if isNil tcrefs then NoResultsOrUsefulErrors else
            let tcrefs = tcrefs |> List.map (fun tcref -> (resInfo, tcref))
            let tcrefs = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, genOk, m)
            match tcrefs with
            | _ :: _ -> tcrefs |> CollectAtMostOneResult (fun (resInfo, tcref) -> ResolveTypeLongIdentInTyconRefPrim ncenv typeNameResInfo ad resInfo genOk (depth+1) m tcref id2 rest2)
            | [] ->
                let suggestTypes (addToBuffer: string -> unit) =
                    for e in tcref.ModuleOrNamespaceType.TypesByDemangledNameAndArity do
                        addToBuffer e.Value.DisplayName

                raze (UndefinedName(depth, FSComp.SR.undefinedNameType, id, suggestTypes))

        AtMostOneResult m tyconSearch

/// Resolve a long identifier representing a type name and report the result
let ResolveTypeLongIdentInTyconRef sink (ncenv: NameResolver) nenv typeNameResInfo ad traitCtxt m tcref (lid: Ident list) =
    let resInfo, tcref =
        match lid with
        | [] ->
            error(Error(FSComp.SR.nrUnexpectedEmptyLongId(), m))
        | id :: rest ->
            ForceRaise (ResolveTypeLongIdentInTyconRefPrim ncenv typeNameResInfo ad ResolutionInfo.Empty PermitDirectReferenceToGeneratedType.No 0 m tcref id rest)
    ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> true))
    let item = Item.Types(tcref.DisplayName, [FreshenTycon ncenv traitCtxt m tcref])
    CallNameResolutionSink sink (rangeOfLid lid, nenv, item, emptyTyparInst, ItemOccurence.UseInType, ad)
    tcref

/// Create an UndefinedName error with details
let SuggestTypeLongIdentInModuleOrNamespace depth (modref: ModuleOrNamespaceRef) amap ad m (id: Ident) =
    let suggestPossibleTypes (addToBuffer: string -> unit) =
        for e in modref.ModuleOrNamespaceType.AllEntities do
            if IsEntityAccessible amap m ad (modref.NestedTyconRef e) then
                addToBuffer e.DisplayName
                addToBuffer e.DemangledModuleOrNamespaceName

    let errorTextF s = FSComp.SR.undefinedNameTypeIn(s, fullDisplayTextOfModRef modref)
    UndefinedName(depth, errorTextF, id, suggestPossibleTypes)

/// Resolve a long identifier representing a type in a module or namespace
let rec private ResolveTypeLongIdentInModuleOrNamespace sink nenv (ncenv: NameResolver) (typeNameResInfo: TypeNameResolutionInfo) ad genOk (resInfo: ResolutionInfo) depth m modref _mty (id: Ident) (rest: Ident list) =
    match rest with
    | [] ->
        // On all paths except error reporting we have isSome(staticResInfo), hence get at most one result back
        let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, typeNameResInfo.StaticArgsInfo, modref)
        match tcrefs with
        | _ :: _ -> tcrefs |> CollectResults (fun tcref -> success(resInfo, tcref))
        | [] -> raze (SuggestTypeLongIdentInModuleOrNamespace depth modref ncenv.amap ad m id)
    | id2 :: rest2 ->
        let m = unionRanges m id.idRange
        let modulSearch =
            match modref.ModuleOrNamespaceType.ModulesAndNamespacesByDemangledName.TryGetValue id.idText with
            | true, AccessibleEntityRef ncenv.amap m ad modref submodref ->
                let item = Item.ModuleOrNamespaces [submodref]
                CallNameResolutionSink sink (id.idRange, nenv, item, emptyTyparInst, ItemOccurence.Use, ad)
                let resInfo = resInfo.AddEntity(id.idRange, submodref)
                ResolveTypeLongIdentInModuleOrNamespace sink nenv ncenv typeNameResInfo ad genOk resInfo (depth+1) m submodref submodref.ModuleOrNamespaceType id2 rest2
            | _ ->
                let suggestPossibleModules (addToBuffer: string -> unit) =
                    for kv in modref.ModuleOrNamespaceType.ModulesAndNamespacesByDemangledName do
                        if IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef kv.Value) then
                            addToBuffer kv.Value.DisplayName
                            addToBuffer kv.Value.DemangledModuleOrNamespaceName

                raze (UndefinedName(depth, FSComp.SR.undefinedNameNamespaceOrModule, id, suggestPossibleModules))

        let tyconSearch =
            let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, TypeNameResolutionStaticArgsInfo.Indefinite, modref)
            match tcrefs with
            | _ :: _ -> tcrefs |> CollectResults (fun tcref -> ResolveTypeLongIdentInTyconRefPrim ncenv typeNameResInfo ad resInfo genOk (depth+1) m tcref id2 rest2)
            | [] ->
                let suggestTypes (addToBuffer: string -> unit) =
                    for e in modref.ModuleOrNamespaceType.TypesByDemangledNameAndArity do
                        addToBuffer e.Value.DisplayName

                raze (UndefinedName(depth, FSComp.SR.undefinedNameType, id, suggestTypes))

        AddResults tyconSearch modulSearch

/// Resolve a long identifier representing a type
let rec ResolveTypeLongIdentPrim sink (ncenv: NameResolver) occurence first fullyQualified m nenv ad (id: Ident) (rest: Ident list) (staticResInfo: TypeNameResolutionStaticArgsInfo) genOk =
    let typeNameResInfo = TypeNameResolutionInfo.ResolveToTypeRefs staticResInfo
    if first && id.idText = MangledGlobalName then
        match rest with
        | [] ->
            error (Error(FSComp.SR.nrGlobalUsedOnlyAsFirstName(), id.idRange))
        | id2 :: rest2 ->
            ResolveTypeLongIdentPrim sink ncenv occurence false FullyQualified m nenv ad id2 rest2 staticResInfo genOk
    else
        match rest with
        | [] ->
            match LookupTypeNameInEnvHaveArity fullyQualified id.idText staticResInfo.NumStaticArgs nenv with
            | Some res ->
                let resInfo =
                    match fullyQualified with
                    | OpenQualified ->
                        match nenv.eUnqualifiedEnclosingTypeInsts.TryFind res with
                        | Some tinst -> ResolutionInfo.Empty.WithEnclosingTypeInst tinst
                        | _ -> ResolutionInfo.Empty
                    | _ ->
                        ResolutionInfo.Empty
                let res = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities ([(resInfo, res)], typeNameResInfo, genOk, unionRanges m id.idRange)
                assert (res.Length = 1)
                success res.Head
            | None ->
                // For Good Error Reporting!
                let tcrefs = LookupTypeNameInEnvNoArity fullyQualified id.idText nenv
                match tcrefs with
                | tcref :: _tcrefs ->
                    // Note: This path is only for error reporting
                    //CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities tcref rest typeNameResInfo m
                    success(ResolutionInfo.Empty, tcref)
                | [] ->
                    let suggestPossibleTypes (addToBuffer: string -> unit) =
                        for kv in nenv.TyconsByDemangledNameAndArity fullyQualified do
                            if IsEntityAccessible ncenv.amap m ad kv.Value then
                                addToBuffer kv.Value.DisplayName
                                addToBuffer kv.Value.DemangledModuleOrNamespaceName
                                match occurence with
                                | ItemOccurence.UseInAttribute ->
                                    if kv.Value.DisplayName.EndsWithOrdinal("Attribute") then
                                        addToBuffer (kv.Value.DisplayName.Replace("Attribute", ""))
                                | _ -> ()

                    raze (UndefinedName(0, FSComp.SR.undefinedNameType, id, suggestPossibleTypes))
        | id2 :: rest2 ->
            let m2 = unionRanges m id.idRange
            let tyconSearch =
                match fullyQualified with
                | FullyQualified ->
                    NoResultsOrUsefulErrors
                | OpenQualified ->
                    match LookupTypeNameInEnvHaveArity fullyQualified id.idText 0 nenv with
                    | Some tcref when IsEntityAccessible ncenv.amap m2 ad tcref ->
                        let resInfo = ResolutionInfo.Empty.AddEntity(id.idRange, tcref)
                        let resInfo =
                            match nenv.eUnqualifiedEnclosingTypeInsts.TryFind tcref with
                            | Some tinstEnclosing -> resInfo.WithEnclosingTypeInst tinstEnclosing
                            | _ -> resInfo
                        OneResult (ResolveTypeLongIdentInTyconRefPrim ncenv typeNameResInfo ad resInfo genOk 1 m2 tcref id2 rest2)
                    | _ ->
                        NoResultsOrUsefulErrors

            let modulSearch =
                ResolveLongIdentAsModuleOrNamespaceThen sink ResultCollectionSettings.AllResults ncenv.amap m2 fullyQualified nenv ad id rest false
                    (ResolveTypeLongIdentInModuleOrNamespace sink nenv ncenv typeNameResInfo ad genOk)
                |?> List.concat

            let modulSearchFailed() =
                ResolveLongIdentAsModuleOrNamespaceThen sink ResultCollectionSettings.AllResults ncenv.amap m2 fullyQualified nenv AccessibleFromSomeFSharpCode id rest false
                    (ResolveTypeLongIdentInModuleOrNamespace sink nenv ncenv typeNameResInfo.DropStaticArgsInfo AccessibleFromSomeFSharpCode genOk)
                |?> List.concat

            let searchSoFar = AddResults tyconSearch modulSearch

            match searchSoFar with
            | Result results ->
                // NOTE: we delay checking the CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities condition until right at the end after we've
                // collected all possible resolutions of the type
                let tcrefs = CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (results, typeNameResInfo, genOk, m)
                match tcrefs with
                | (resInfo, tcref) :: _ ->
                    // We've already reported the ambiguity, possibly as an error. Now just take the first possible result.
                    success(resInfo, tcref)
                | [] ->
                    // failing case - report nice ambiguity errors even in this case
                    let r = AddResults searchSoFar (modulSearchFailed())
                    AtMostOneResult m2 (r |?> (fun tcrefs -> CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, genOk, m)))
            | _ ->
                // failing case - report nice ambiguity errors even in this case
                let r = AddResults searchSoFar (modulSearchFailed())
                AtMostOneResult m2 (r |?> (fun tcrefs -> CheckForTypeLegitimacyAndMultipleGenericTypeAmbiguities (tcrefs, typeNameResInfo, genOk, m)))


/// Resolve a long identifier representing a type and report it
let ResolveTypeLongIdentAux sink (ncenv: NameResolver) occurence fullyQualified nenv ad traitCtxt (lid: Ident list) staticResInfo genOk =
    let m = rangeOfLid lid
    let res =
        match lid with
        | [] ->
            error(Error(FSComp.SR.nrUnexpectedEmptyLongId(), m))
        | id :: rest ->
            ResolveTypeLongIdentPrim sink ncenv occurence true fullyQualified m nenv ad id rest staticResInfo genOk

    // Register the result as a name resolution
    match res with
    | Result (resInfo, tcref) ->
        ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.UseInType, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> true))
        let item = Item.Types(tcref.DisplayName, [FreshenTycon ncenv traitCtxt m tcref])
        CallNameResolutionSink sink (m, nenv, item, emptyTyparInst, occurence, ad)
    | _ -> ()
    res

/// Resolve a long identifier representing a type and report it
let ResolveTypeLongIdent sink ncenv occurence fullyQualified nenv ad traitCtxt lid staticResInfo genOk =
    let res = ResolveTypeLongIdentAux sink ncenv occurence fullyQualified nenv ad traitCtxt lid staticResInfo genOk
    (res |?> fun (resInfo, tcref) -> (resInfo.EnclosingTypeInst, tcref))

//-------------------------------------------------------------------------
// Resolve F#/IL "." syntax in records etc.
//-------------------------------------------------------------------------

/// Resolve a long identifier representing a record field in a module or namespace
let rec ResolveFieldInModuleOrNamespace (ncenv: NameResolver) nenv ad traitCtxt (resInfo: ResolutionInfo) depth m (modref: ModuleOrNamespaceRef) _mty (id: Ident) (rest: Ident list) =
    let typeNameResInfo = TypeNameResolutionInfo.Default
    let m = unionRanges m id.idRange
    // search for module-qualified names, e.g. { Microsoft.FSharp.Core.contents = 1 }
    let modulScopedFieldNames =
        match TryFindTypeWithRecdField modref id  with
        | Some tycon when IsEntityAccessible ncenv.amap m ad (modref.NestedTyconRef tycon) ->
            let showDeprecated = HasFSharpAttribute ncenv.g ncenv.g.attrib_RequireQualifiedAccessAttribute tycon.Attribs
            success [resInfo, FieldResolution(FreshenRecdFieldRef ncenv traitCtxt m (modref.RecdFieldRefInNestedTycon tycon id), showDeprecated), rest]
        | _ -> raze (UndefinedName(depth, FSComp.SR.undefinedNameRecordLabelOrNamespace, id, NoSuggestions))

    // search for type-qualified names, e.g. { Microsoft.FSharp.Core.Ref.contents = 1 }
    let tyconSearch() =
        match rest with
        | id2 :: rest2 ->
            let tcrefs = LookupTypeNameInEntityMaybeHaveArity (ncenv.amap, id.idRange, ad, id.idText, TypeNameResolutionStaticArgsInfo.Indefinite, modref)
            if isNil tcrefs then NoResultsOrUsefulErrors else
            let tcrefs = tcrefs |> List.map (fun tcref -> (ResolutionInfo.Empty, tcref))
            let tyconSearch = ResolveLongIdentInTyconRefs ResultCollectionSettings.AllResults ncenv nenv LookupKind.RecdField  (depth+1) m ad traitCtxt id2 rest2 typeNameResInfo id.idRange tcrefs
            // choose only fields
            let tyconSearch = tyconSearch |?> List.choose (function (resInfo, Item.RecdField(RecdFieldInfo(_, rfref)), rest) -> Some(resInfo, FieldResolution(FreshenRecdFieldRef ncenv traitCtxt m rfref, false), rest) | _ -> None)
            tyconSearch
        | _ ->
            NoResultsOrUsefulErrors

    // search for names in nested modules, e.g. { Microsoft.FSharp.Core.contents = 1 }
    let modulSearch() =
        match rest with
        | id2 :: rest2 ->
            match modref.ModuleOrNamespaceType.ModulesAndNamespacesByDemangledName.TryGetValue id.idText with
            | true, AccessibleEntityRef ncenv.amap m ad modref submodref ->
                let resInfo = resInfo.AddEntity(id.idRange, submodref)
                ResolveFieldInModuleOrNamespace ncenv nenv ad traitCtxt resInfo (depth+1) m submodref submodref.ModuleOrNamespaceType id2 rest2
                |> OneResult
            | _ -> raze (UndefinedName(depth, FSComp.SR.undefinedNameRecordLabelOrNamespace, id, NoSuggestions))
        | _ -> raze (UndefinedName(depth, FSComp.SR.undefinedNameRecordLabelOrNamespace, id, NoSuggestions))

    modulScopedFieldNames +++ tyconSearch +++ modulSearch
    |> AtMostOneResult m

/// Suggest other labels of the same record
let SuggestOtherLabelsOfSameRecordType g (nenv: NameResolutionEnv) ty (id: Ident) (allFields: Ident list) =
    let labelsOfPossibleRecord = GetRecordLabelsForType g nenv ty

    let givenFields =
        allFields
        |> List.map (fun fld -> fld.idText)
        |> List.filter ((<>) id.idText)

    labelsOfPossibleRecord.ExceptWith givenFields
    labelsOfPossibleRecord

let SuggestLabelsOfRelatedRecords g (nenv: NameResolutionEnv) (id: Ident) (allFields: Ident list) =
    let suggestLabels (addToBuffer: string -> unit) =
        let givenFields = allFields |> List.map (fun fld -> fld.idText) |> List.filter ((<>) id.idText) |> HashSet
        let fullyQualified =
            if givenFields.Count = 0 then
                // return labels from all records
                let result = NameMap.domainL nenv.eFieldLabels |> HashSet
                result.Remove "contents" |> ignore
                result
            else
                let possibleRecords =
                    [for fld in givenFields do
                        match nenv.eFieldLabels.TryGetValue fld with
                        | true, recordTypes -> yield! (recordTypes |> List.map (fun r -> r.TyconRef.DisplayName, fld))
                        | _ -> () ]
                    |> List.groupBy fst
                    |> List.map (fun (r, fields) -> r, fields |> List.map snd)
                    |> List.filter (fun (_, fields) -> givenFields.IsSubsetOf fields)
                    |> List.map fst
                    |> HashSet

                let labelsOfPossibleRecords =
                    nenv.eFieldLabels
                    |> Seq.filter (fun kv ->
                        kv.Value
                        |> List.map (fun r -> r.TyconRef.DisplayName)
                        |> List.exists possibleRecords.Contains)
                    |> Seq.map (fun kv -> kv.Key)
                    |> HashSet

                labelsOfPossibleRecords.ExceptWith givenFields
                labelsOfPossibleRecords

        if fullyQualified.Count > 0 then
            fullyQualified |> Seq.iter addToBuffer
        else
            // check if the user forgot to use qualified access
            for e in nenv.eTyconsByDemangledNameAndArity do
                let hasRequireQualifiedAccessAttribute = HasFSharpAttribute g g.attrib_RequireQualifiedAccessAttribute e.Value.Attribs
                if hasRequireQualifiedAccessAttribute then
                    if e.Value.IsRecordTycon && e.Value.AllFieldsArray |> Seq.exists (fun x -> x.Name = id.idText) then
                        addToBuffer (e.Value.DisplayName + "." + id.idText)

    UndefinedName(0, FSComp.SR.undefinedNameRecordLabel, id, suggestLabels)

/// Resolve a long identifier representing a record field
let ResolveFieldPrim sink (ncenv: NameResolver) nenv ad traitCtxt ty (mp, id: Ident) allFields =
    let typeNameResInfo = TypeNameResolutionInfo.Default
    let g = ncenv.g
    let m = id.idRange
    match mp with
    | [] ->
        let lookup() =
            let frefs =
                try Map.find id.idText nenv.eFieldLabels
                with :? KeyNotFoundException ->
                    // record label is unknown -> suggest related labels and give a hint to the user
                    error(SuggestLabelsOfRelatedRecords g nenv id allFields)

            // Eliminate duplicates arising from multiple 'open'
            frefs
            |> ListSet.setify (fun fref1 fref2 -> tyconRefEq g fref1.TyconRef fref2.TyconRef)
            |> List.map (fun x -> 
                let rfinfo =
                    match nenv.eUnqualifiedRecordOrUnionTypeInsts.TryFind x.TyconRef with
                    | Some tinst -> RecdFieldInfo(tinst, x)
                    | _ -> FreshenRecdFieldRef ncenv traitCtxt m x
                ResolutionInfo.Empty, FieldResolution(rfinfo, false))

        match tryTcrefOfAppTy g ty with
        | ValueSome tcref ->
            match ncenv.InfoReader.TryFindRecdOrClassFieldInfoOfType(id.idText, m, ty) with
            | ValueSome (RecdFieldInfo(_, rfref)) -> [ResolutionInfo.Empty, FieldResolution(FreshenRecdFieldRef ncenv traitCtxt m rfref, false)]
            | _ ->
                if tcref.IsRecordTycon then
                    // record label doesn't belong to record type -> suggest other labels of same record
                    let suggestLabels (addToBuffer: string -> unit) = 
                        for label in SuggestOtherLabelsOfSameRecordType g nenv ty id allFields do
                            addToBuffer label

                    let typeName = NicePrint.minimalStringOfType nenv.eDisplayEnv ty
                    let errorText = FSComp.SR.nrRecordDoesNotContainSuchLabel(typeName, id.idText)
                    error(ErrorWithSuggestions(errorText, m, id.idText, suggestLabels))
                else
                    lookup()
        | _ ->
            lookup()
    | _ ->
        let lid = (mp@[id])
        let tyconSearch ad () =
            match lid with
            | tn :: id2 :: rest2 ->
                let m = tn.idRange
                let tcrefs = LookupTypeNameInEnvNoArity OpenQualified tn.idText nenv
                if isNil tcrefs then NoResultsOrUsefulErrors else
                let tcrefs = tcrefs |> List.map (fun tcref -> (ResolutionInfo.Empty, tcref))
                let tyconSearch = ResolveLongIdentInTyconRefs ResultCollectionSettings.AllResults ncenv nenv LookupKind.RecdField 1 m ad traitCtxt id2 rest2 typeNameResInfo tn.idRange tcrefs
                // choose only fields
                let tyconSearch = tyconSearch |?> List.choose (function (resInfo, Item.RecdField(RecdFieldInfo(_, rfref)), rest) -> Some(resInfo, FieldResolution(FreshenRecdFieldRef ncenv traitCtxt m rfref, false), rest) | _ -> None)
                tyconSearch
            | _ -> NoResultsOrUsefulErrors

        let modulSearch ad () =
            match lid with
            | [] -> NoResultsOrUsefulErrors
            | id2 :: rest2 ->
                ResolveLongIdentAsModuleOrNamespaceThen sink ResultCollectionSettings.AtMostOneResult ncenv.amap m OpenQualified nenv ad id2 rest2 false
                    (ResolveFieldInModuleOrNamespace ncenv nenv ad traitCtxt)

        let resInfo, item, rest =
            modulSearch ad () +++ tyconSearch ad +++ modulSearch AccessibleFromSomeFSharpCode +++ tyconSearch AccessibleFromSomeFSharpCode
            |> AtMostOneResult m
            |> ForceRaise

        if not (isNil rest) then
            errorR(Error(FSComp.SR.nrInvalidFieldLabel(), (List.head rest).idRange))

        [(resInfo, item)]

let ResolveField sink ncenv nenv ad traitCtxt ty (mp, id) allFields =
    let res = ResolveFieldPrim sink ncenv nenv ad traitCtxt ty (mp, id) allFields
    // Register the results of any field paths "Module.Type" in "Module.Type.field" as a name resolution. (Note, the path resolution
    // info is only non-empty if there was a unique resolution of the field)
    let checker = ResultTyparChecker(fun () -> true)
    res
    |> List.map (fun (resInfo, rfref) ->
        ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.UseInType, ad, traitCtxt, resInfo, checker)
        rfref)

/// Resolve F#/IL "." syntax in expressions (2).
///
/// We have an expr. on the left, and we do an access, e.g.
/// (f obj).field or (f obj).meth.  The basic rule is that if l-r type
/// inference has determined the outer type then we can proceed in a simple fashion. The exception
/// to the rule is for field types, which applies if l-r was insufficient to
/// determine any valid members
//
// QUERY (instantiationGenerator cleanup): it would be really nice not to flow instantiationGenerator to here.
let private ResolveExprDotLongIdent (ncenv: NameResolver) m ad traitCtxt nenv ty (id: Ident) rest (typeNameResInfo: TypeNameResolutionInfo) findFlag =
    let adhocDotSearchAccessible = AtMostOneResult m (ResolveLongIdentInTypePrim ncenv nenv LookupKind.Expr ResolutionInfo.Empty 1 m ad traitCtxt id rest findFlag typeNameResInfo ty)
    match adhocDotSearchAccessible with
    | Exception _ ->
        // If the dot is not resolved by adhoc overloading then look for a record field
        // that can resolve the name.
        let dotFieldIdSearch =
            // If the type is already known, we should not try to lookup a record field
            if isAppTy ncenv.g ty then
                NoResultsOrUsefulErrors
            else
                match nenv.eFieldLabels.TryGetValue id.idText with
                | true, rfref :: _ ->
                    // NOTE (instantiationGenerator cleanup): we need to freshen here because we don't know the type.
                    // But perhaps the caller should freshen??
                    let item = Item.RecdField(FreshenRecdFieldRef ncenv traitCtxt m rfref)
                    OneSuccess (ResolutionInfo.Empty, item, rest)
                | _ -> NoResultsOrUsefulErrors

        let adhocDotSearchAll () = ResolveLongIdentInTypePrim ncenv nenv LookupKind.Expr ResolutionInfo.Empty 1 m AccessibleFromSomeFSharpCode traitCtxt id rest findFlag typeNameResInfo ty

        dotFieldIdSearch +++ adhocDotSearchAll
        |> AtMostOneResult m
        |> ForceRaise
    | _ ->
        ForceRaise adhocDotSearchAccessible

let ComputeItemRange wholem (lid: Ident list) rest =
    match rest with
    | [] -> wholem
    | _ ->
        let ids = List.truncate (max 0 (lid.Length - rest.Length)) lid
        match ids with
        | [] -> wholem
        | _ -> rangeOfLid ids

/// Filters method groups that will be sent to Visual Studio IntelliSense
/// to include only static/instance members

let FilterMethodGroups (ncenv: NameResolver) itemRange item staticOnly =
    match item with
    | Item.MethodGroup(nm, minfos, orig) ->
        let minfos = minfos |> List.filter  (fun minfo ->
           staticOnly = isNil (minfo.GetObjArgTypes(ncenv.amap, itemRange, minfo.FormalMethodInst)))
        Item.MethodGroup(nm, minfos, orig)
    | item -> item

let NeedsWorkAfterResolution namedItem =
    match namedItem with
    | Item.MethodGroup(_, minfos, _)
    | Item.CtorGroup(_, minfos) -> minfos.Length > 1 || minfos |> List.exists (fun minfo -> not (isNil minfo.FormalMethodInst))
    | Item.Property(_, pinfos) -> pinfos.Length > 1
    | Item.ImplicitOp(_, { contents = Some(TraitConstraintSln.FSMethSln(_, vref, _, _)) })
    | Item.Value vref | Item.CustomBuilder (_, vref) -> not (List.isEmpty vref.Typars)
    | Item.CustomOperation (_, _, Some minfo) -> not (isNil minfo.FormalMethodInst)
    | Item.ActivePatternCase apref -> not (List.isEmpty apref.ActivePatternVal.Typars)
    | _ -> false

/// Specifies additional work to do after an item has been processed further in type checking.
[<RequireQualifiedAccess>]
type AfterResolution =

    /// Notification is not needed
    | DoNothing

    /// Notify the tcSink of a precise resolution. The 'Item' contains the candidate overrides.
    | RecordResolution of Item option * (TyparInst -> unit) * (MethInfo * PropInfo option * TyparInst -> unit) * (unit -> unit)

/// Resolve a long identifier occurring in an expression position.
///
/// Called for 'TypeName.Bar' - for VS IntelliSense, we can filter out instance members from method groups
let ResolveLongIdentAsExprAndComputeRange (sink: TcResultsSink) (ncenv: NameResolver) wholem ad traitCtxt nenv typeNameResInfo lid =
    match ResolveExprLongIdent sink ncenv wholem ad traitCtxt nenv typeNameResInfo lid with 
    | Exception e -> Exception e 
    | Result (tinstEnclosing, item1, rest) ->
    let itemRange = ComputeItemRange wholem lid rest

    let item = FilterMethodGroups ncenv itemRange item1 true

    match item1, item with
    | Item.MethodGroup(name, minfos1, _), Item.MethodGroup(_, [], _) when not (isNil minfos1) ->
        raze(Error(FSComp.SR.methodIsNotStatic name, wholem))
    | _ -> 

    // Fake idents e.g. 'Microsoft.FSharp.Core.None' have identical ranges for each part
    let isFakeIdents =
        match lid with
        | [] | [_] -> false
        | head :: ids ->
            ids |> List.forall (fun id -> Range.equals id.idRange head.idRange)

    let callSink (refinedItem, tpinst) =
        if not isFakeIdents then
            let occurence =
                match item with
                // It's r.h.s. `Case1` in `let (|Case1|Case1|) _ = if true then Case1 else Case2`
                // We return `Binding` for it because it's actually not usage, but definition. If we did not
                // it confuses detecting unused definitions.
                | Item.ActivePatternResult _ -> ItemOccurence.Binding
                | _ -> ItemOccurence.Use

            CallMethodGroupNameResolutionSink sink (itemRange, nenv, refinedItem, item, tpinst, occurence, ad)

    let callSinkWithSpecificOverload (minfo: MethInfo, pinfoOpt: PropInfo option, tpinst) =
        let refinedItem =
            match pinfoOpt with
            | None when minfo.IsConstructor -> Item.CtorGroup(minfo.LogicalName, [minfo])
            | None -> Item.MethodGroup(minfo.LogicalName, [minfo], None)
            | Some pinfo -> Item.Property(pinfo.PropertyName, [pinfo])

        callSink (refinedItem, tpinst)

    let afterResolution =
        match sink.CurrentSink with
        | None -> AfterResolution.DoNothing
        | Some _ ->
            if NeedsWorkAfterResolution item then
                AfterResolution.RecordResolution(None, (fun tpinst -> callSink(item, tpinst)), callSinkWithSpecificOverload, (fun () -> callSink (item, emptyTyparInst)))
            else
               callSink (item, emptyTyparInst)
               AfterResolution.DoNothing

    success (tinstEnclosing, item, itemRange, rest, afterResolution)

let (|NonOverridable|_|) namedItem =
    match namedItem with
    |   Item.MethodGroup(_, minfos, _) when minfos |> List.exists(fun minfo -> minfo.IsVirtual || minfo.IsAbstract) -> None
    |   Item.Property(_, pinfos) when pinfos |> List.exists(fun pinfo -> pinfo.IsVirtualProperty) -> None
    |   _ -> Some ()

/// Called for 'expression.Bar' - for VS IntelliSense, we can filter out static members from method groups
/// Also called for 'GenericType<Args>.Bar' - for VS IntelliSense, we can filter out non-static members from method groups
let ResolveExprDotLongIdentAndComputeRange (sink: TcResultsSink) (ncenv: NameResolver) wholem ad traitCtxt nenv ty lid (staticResInfo: TypeNameResolutionInfo) findFlag thisIsActuallyATyAppNotAnExpr =
    let resolveExpr findFlag =
        let resInfo, item, rest =
            match lid with
            | id :: rest ->
                ResolveExprDotLongIdent ncenv wholem ad traitCtxt nenv ty id rest staticResInfo findFlag
            | _ -> error(InternalError("ResolveExprDotLongIdentAndComputeRange", wholem))
        let itemRange = ComputeItemRange wholem lid rest
        resInfo, item, rest, itemRange
    // "true" resolution
    let resInfo, item, rest, itemRange = resolveExpr findFlag
    ResolutionInfo.SendEntityPathToSink(sink, ncenv, nenv, ItemOccurence.Use, ad, traitCtxt, resInfo, ResultTyparChecker(fun () -> CheckAllTyparsInferrable ncenv.amap itemRange item))

    // Record the precise resolution of the field for intellisense/goto definition
    let afterResolution =
        match sink.CurrentSink with
        | None -> AfterResolution.DoNothing // do not refine the resolution if nobody listens
        | Some _ ->
            // resolution for goto definition
            let unrefinedItem, itemRange, overrides =
                match findFlag, item with
                | FindMemberFlag.PreferOverrides, _
                | _, NonOverridable() -> item, itemRange, false
                | FindMemberFlag.IgnoreOverrides, _ ->
                    let _, item, _, itemRange = resolveExpr FindMemberFlag.PreferOverrides
                    item, itemRange, true

            let callSink (refinedItem, tpinst) =
                let staticOnly = thisIsActuallyATyAppNotAnExpr
                let refinedItem = FilterMethodGroups ncenv itemRange refinedItem staticOnly
                let unrefinedItem = FilterMethodGroups ncenv itemRange unrefinedItem staticOnly
                CallMethodGroupNameResolutionSink sink (itemRange, nenv, refinedItem, unrefinedItem, tpinst, ItemOccurence.Use, ad)

            let callSinkWithSpecificOverload (minfo: MethInfo, pinfoOpt: PropInfo option, tpinst) =
                let refinedItem =
                    match pinfoOpt with
                    | None when minfo.IsConstructor -> Item.CtorGroup(minfo.LogicalName, [minfo])
                    | None -> Item.MethodGroup(minfo.LogicalName, [minfo], None)
                    | Some pinfo -> Item.Property(pinfo.PropertyName, [pinfo])

                callSink (refinedItem, tpinst)

            match overrides, NeedsWorkAfterResolution unrefinedItem with
            | false, true ->
                AfterResolution.RecordResolution (None, (fun tpinst -> callSink(item, tpinst)), callSinkWithSpecificOverload, (fun () -> callSink (unrefinedItem, emptyTyparInst)))
            | true, true  ->
                AfterResolution.RecordResolution (Some unrefinedItem, (fun tpinst -> callSink(item, tpinst)), callSinkWithSpecificOverload, (fun () -> callSink (unrefinedItem, emptyTyparInst)))
            | _, false   ->
                callSink (unrefinedItem, emptyTyparInst)
                AfterResolution.DoNothing

    item, itemRange, rest, afterResolution


//-------------------------------------------------------------------------
// Given an nenv resolve partial paths to sets of names, used by interactive
// environments (Visual Studio)
//
// ptc = partial type check
// ptci = partial type check item
//
// There are some inefficiencies in this code - e.g. we often
// create potentially large lists of methods/fields/properties and then
// immediately List.filter them.  We also use lots of "map/concats".  Doesn't
// seem to hit the interactive experience too badly though.
//-------------------------------------------------------------------------

/// A generator of type instantiations used when no more specific type instantiation is known.
let FakeInstantiationGenerator = (fun (_m: range) gps (_traitCtxt: ITraitContext option) ->  List.map mkTyparTy gps)

// note: using local refs is ok since it is only used by VS
let ItemForModuleOrNamespaceRef v = Item.ModuleOrNamespaces [v]
let ItemForPropInfo (pinfo: PropInfo) = Item.Property (pinfo.PropertyName, [pinfo])

let IsTyconUnseenObsoleteSpec ad g amap m (x: TyconRef) allowObsolete =
    not (IsEntityAccessible amap m ad x) ||
    ((not allowObsolete) &&
      (if x.IsILTycon then
          CheckILAttributesForUnseen g x.ILTyconRawMetadata.CustomAttrs m
       else
          CheckFSharpAttributesForUnseen g x.Attribs m))

let IsTyconUnseen ad g amap m (x: TyconRef) = IsTyconUnseenObsoleteSpec ad g amap m x false

let IsValUnseen ad g m (v: ValRef) =
    v.IsCompilerGenerated ||
    v.Deref.IsClassConstructor ||
    not (IsValAccessible ad v) ||
    CheckFSharpAttributesForUnseen g v.Attribs m

let IsUnionCaseUnseen ad g amap m (ucref: UnionCaseRef) =
    not (IsUnionCaseAccessible amap m ad ucref) ||
    IsTyconUnseen ad g amap m ucref.TyconRef ||
    CheckFSharpAttributesForUnseen g ucref.Attribs m

let ItemIsUnseen ad g amap m item =
    match item with
    | Item.Value x -> 
        let isUnseenNameOfOperator = valRefEq g g.nameof_vref x && not (g.langVersion.SupportsFeature LanguageFeature.NameOf)
        isUnseenNameOfOperator || IsValUnseen ad  g m x
    | Item.UnionCase(x, _) -> IsUnionCaseUnseen ad g amap m x.UnionCaseRef
    | Item.ExnCase x -> IsTyconUnseen ad g amap m x
    | _ -> false

let ItemOfTyconRef ncenv m (x: TyconRef) =
    Item.Types (x.DisplayName, [FreshenTycon ncenv traitCtxtNone m x])

let ItemOfTy g x =
    let nm = 
        match tryTcrefOfAppTy g x with
        | ValueSome tcref -> tcref.DisplayName 
        | _ -> "?"
    Item.Types (nm, [x])

// Filter out 'PrivateImplementationDetail' classes
let IsInterestingModuleName nm = not (System.String.IsNullOrEmpty nm) && nm.[0] <> '<'

let rec PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThen f plid (modref: ModuleOrNamespaceRef) =
    let mty = modref.ModuleOrNamespaceType
    match plid with
    | [] -> f modref
    | id :: rest ->
        match mty.ModulesAndNamespacesByDemangledName.TryGetValue id with
        | true, mty -> PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThen f rest (modref.NestedTyconRef mty)
        | _ -> []

let PartialResolveLongIdentAsModuleOrNamespaceThen (nenv: NameResolutionEnv) plid f =
    match plid with
    | id :: rest ->
        match nenv.eModulesAndNamespaces.TryGetValue id with
        | true, modrefs ->
            List.collect (PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThen f rest) modrefs
        | _ ->
            []
    | [] -> []

/// Returns fields for the given class or record
let ResolveRecordOrClassFieldsOfType (ncenv: NameResolver) m ad ty statics =
    ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
    |> List.filter (fun rfref -> rfref.IsStatic = statics && IsFieldInfoAccessible ad rfref)
    |> List.map Item.RecdField

[<RequireQualifiedAccess>]
type ResolveCompletionTargets =
    | All of (MethInfo -> TType -> bool)
    | SettablePropertiesAndFields
    member this.ResolveAll =
        match this with
        | All _ -> true
        | SettablePropertiesAndFields -> false

/// Resolve a (possibly incomplete) long identifier to a set of possible resolutions, qualified by type.
let ResolveCompletionsInType (ncenv: NameResolver) nenv (completionTargets: ResolveCompletionTargets) m ad statics ty =
  let traitCtxt = traitCtxtNone
  protectAssemblyExploration [] <| fun () ->
    let g = ncenv.g
    let amap = ncenv.amap

    let rfinfos =
        ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
        |> List.filter (fun rfref -> rfref.IsStatic = statics && IsFieldInfoAccessible ad rfref)

    let ucinfos =
        if completionTargets.ResolveAll && statics then
            match tryAppTy g ty with
            | ValueSome (tc, tinst) ->
                tc.UnionCasesAsRefList
                |> List.filter (IsUnionCaseUnseen ad g ncenv.amap m >> not)
                |> List.map (fun ucref -> Item.UnionCase(UnionCaseInfo(tinst, ucref), false))
            | _ -> []
        else []

    let einfos =
        if completionTargets.ResolveAll then
            ncenv.InfoReader.GetEventInfosOfType(None, ad, m, ty)
            |> List.filter (fun x ->
                IsStandardEventInfo ncenv.InfoReader m ad x &&
                x.IsStatic = statics)
        else []

    let nestedTypes =
        if completionTargets.ResolveAll && statics then
            ty
            |> GetNestedTypesOfType (ncenv, ad, traitCtxt, None, TypeNameResolutionStaticArgsInfo.Indefinite, false, m)
        else
            []

    let finfos =
        ncenv.InfoReader.GetILFieldInfosOfType(None, ad, m, ty)
        |> List.filter (fun x ->
            not x.IsSpecialName &&
            x.IsStatic = statics &&
            IsILFieldInfoAccessible g amap m ad x)

    let pinfosIncludingUnseen =
        AllPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None ad PreferOverrides m ty
        |> List.filter (fun x ->
            x.IsStatic = statics &&
            IsPropInfoAccessible g amap m ad x)

    // Exclude get_ and set_ methods accessed by properties
    let pinfoMethNames =
      (pinfosIncludingUnseen
       |> List.filter (fun pinfo -> pinfo.HasGetter)
       |> List.map (fun pinfo -> pinfo.GetterMethod.LogicalName))
      @
      (pinfosIncludingUnseen
       |> List.filter (fun pinfo -> pinfo.HasSetter)
       |> List.map (fun pinfo -> pinfo.SetterMethod.LogicalName))

    let einfoMethNames =
        if completionTargets.ResolveAll then
            [ for einfo in einfos do
                let delegateType = einfo.GetDelegateType(amap, m)
                let (SigOfFunctionForDelegate(invokeMethInfo, _, _, _)) = GetSigOfFunctionForDelegate ncenv.InfoReader delegateType m ad
                // Only events with void return types are suppressed in intellisense.
                if slotSigHasVoidReturnTy (invokeMethInfo.GetSlotSig(amap, m, traitCtxtNone)) then
                  yield einfo.AddMethod.DisplayName
                  yield einfo.RemoveMethod.DisplayName ]
        else []

    let pinfos =
        pinfosIncludingUnseen
        |> List.filter (fun x -> not (PropInfoIsUnseen m x))

    let minfoFilter (suppressedMethNames: Zset<_>) (minfo: MethInfo) =
        let isApplicableMeth =
            match completionTargets with
            | ResolveCompletionTargets.All x -> x
            | _ -> failwith "internal error: expected completionTargets = ResolveCompletionTargets.All"

        // Only show the Finalize, MemberwiseClose etc. methods on System.Object for values whose static type really is
        // System.Object. Few of these are typically used from F#.
        //
        // Don't show GetHashCode or Equals for F# types that admit equality as an abnormal operation
        let isUnseenDueToBasicObjRules =
            not (isObjTy g ty) &&
            not minfo.IsExtensionMember &&
            match minfo.LogicalName with
            | "GetType"  -> false
            | "GetHashCode"  -> isObjTy g minfo.ApparentEnclosingType && not (AugmentWithHashCompare.TypeDefinitelyHasEquality g ty)
            | "ToString" -> false
            | "Equals" ->
                if not (isObjTy g minfo.ApparentEnclosingType) then
                    // declaring type is not System.Object - show it
                    false
                elif minfo.IsInstance then
                    // System.Object has only one instance Equals method and we want to suppress it unless Augment.TypeDefinitelyHasEquality is true
                    not (AugmentWithHashCompare.TypeDefinitelyHasEquality g ty)
                else
                    // System.Object has only one static Equals method and we always want to suppress it
                    true
            | _ ->
                // filter out self methods of obj type
                isObjTy g minfo.ApparentEnclosingType

        let result =
            not isUnseenDueToBasicObjRules &&
            not minfo.IsInstance = statics &&
            IsMethInfoAccessible amap m ad minfo &&
            not (MethInfoIsUnseen g m ty minfo) &&
            not minfo.IsConstructor &&
            not minfo.IsClassConstructor &&
            not (minfo.LogicalName = ".cctor") &&
            not (minfo.LogicalName = ".ctor") &&
            isApplicableMeth minfo ty &&
            not (suppressedMethNames.Contains minfo.LogicalName)

        result

    let pinfoItems =
        let pinfos =
            match completionTargets with
            | ResolveCompletionTargets.SettablePropertiesAndFields -> pinfos |> List.filter (fun p -> p.HasSetter)
            | _ -> pinfos

        pinfos
        |> List.choose (fun pinfo->
            let pinfoOpt = DecodeFSharpEvent [pinfo] ad g ncenv m
            match pinfoOpt, completionTargets with
            | Some(Item.Event einfo), ResolveCompletionTargets.All _ -> if IsStandardEventInfo ncenv.InfoReader m ad einfo then pinfoOpt else None
            | _ -> pinfoOpt)

    // REVIEW: add a name filter here in the common cases?
    let minfos =
        if completionTargets.ResolveAll then
            let minfos = AllMethInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None ad PreferOverrides m ty
            if isNil minfos then
                []
            else
                let suppressedMethNames = Zset.ofList String.order (pinfoMethNames @ einfoMethNames)

                let minfos =
                    minfos
                    |> List.filter (minfoFilter suppressedMethNames)

                if isNil minfos then
                    []
                else
                    let minfos =
                        let addersAndRemovers =
                            let hashSet = HashSet()
                            for item in pinfoItems do
                                match item with
                                | Item.Event(FSEvent(_, _, addValRef, removeValRef)) ->
                                    hashSet.Add addValRef.LogicalName |> ignore
                                    hashSet.Add removeValRef.LogicalName |> ignore
                                | _ -> ()
                            hashSet

                        if addersAndRemovers.Count = 0 then minfos
                        else minfos |> List.filter (fun minfo -> not (addersAndRemovers.Contains minfo.LogicalName))

#if !NO_EXTENSIONTYPING
                    // Filter out the ones with mangled names from applying static parameters
                    let minfos =
                        let methsWithStaticParams =
                            minfos
                            |> List.filter (fun minfo ->
                                match minfo.ProvidedStaticParameterInfo with
                                | Some (_methBeforeArguments, staticParams) -> staticParams.Length <> 0
                                | _ -> false)
                            |> List.map (fun minfo -> minfo.DisplayName)

                        if methsWithStaticParams.IsEmpty then minfos
                        else minfos |> List.filter (fun minfo ->
                                let nm = minfo.LogicalName
                                not (nm.Contains "," && methsWithStaticParams |> List.exists (fun m -> nm.StartsWithOrdinal m)))
#endif

                    minfos

        else
            []

    // Partition methods into overload sets
    let rec partitionl (l: MethInfo list) acc =
        match l with
        | [] -> acc
        | h :: t ->
            let nm = h.LogicalName
            partitionl t (NameMultiMap.add nm h acc)

    let anonFields =
        if statics then  []
        else
            match tryDestAnonRecdTy g ty with
            | ValueSome (anonInfo, tys) ->
                [ for (i, id) in Array.indexed anonInfo.SortedIds do
                    yield Item.AnonRecdField(anonInfo, tys, i, id.idRange) ]
            | _ -> []

    // Build the results
    ucinfos @
    List.map Item.RecdField rfinfos @
    pinfoItems @
    anonFields @
    List.map Item.ILField finfos @
    List.map Item.Event einfos @
    List.map (ItemOfTy g) nestedTypes @
    List.map Item.MakeMethGroup (NameMap.toList (partitionl minfos Map.empty))


let rec ResolvePartialLongIdentInType (ncenv: NameResolver) nenv isApplicableMeth m ad statics plid ty =
    let g = ncenv.g
    let amap = ncenv.amap
    let traitCtxt = traitCtxtNone
    match plid with
    | [] -> ResolveCompletionsInType ncenv nenv isApplicableMeth m ad statics ty
    | id :: rest ->

      let rfinfos =
          ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
          |> List.filter (fun fref -> fref.Name = id && IsRecdFieldAccessible ncenv.amap m ad fref.RecdFieldRef && fref.RecdField.IsStatic = statics)

      let nestedTypes =
          ty
          |> GetNestedTypesOfType (ncenv, ad, traitCtxt, Some id, TypeNameResolutionStaticArgsInfo.Indefinite, false, m)

      // e.g. <val-id>.<recdfield-id>.<more>
      (rfinfos |> List.collect (fun x -> x.FieldType |> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest)) @

      // e.g. <val-id>.<property-id>.<more>
      let FullTypeOfPinfo(pinfo: PropInfo) =
          let rty = pinfo.GetPropertyType(amap, m)
          let rty = if pinfo.IsIndexer then mkRefTupledTy g (pinfo.GetParamTypes(amap, m)) --> rty else rty
          rty

      (ty
         |> AllPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv (Some id) ad IgnoreOverrides m
         |> List.filter (fun pinfo -> pinfo.IsStatic = statics && IsPropInfoAccessible g amap m ad pinfo)
         |> List.collect (fun pinfo -> (FullTypeOfPinfo pinfo) |> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest)) @

      (if statics then []
       else
          match TryFindAnonRecdFieldOfType g ty id with
          | Some (Item.AnonRecdField(_anonInfo, tys, i, _)) -> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest tys.[i]
          | _ -> []) @

      // e.g. <val-id>.<event-id>.<more>
      (ncenv.InfoReader.GetEventInfosOfType(Some id, ad, m, ty)
         |> List.collect (PropTypOfEventInfo ncenv.InfoReader m ad >> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest)) @

      // nested types!
      (nestedTypes
         |> List.collect (ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad statics rest)) @

      // e.g. <val-id>.<il-field-id>.<more>
      (ncenv.InfoReader.GetILFieldInfosOfType(Some id, ad, m, ty)
         |> List.filter (fun x ->
             not x.IsSpecialName &&
             x.IsStatic = statics &&
             IsILFieldInfoAccessible g amap m ad x)
         |> List.collect (fun x -> x.FieldType(amap, m) |> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest))

let InfosForTyconConstructors (ncenv: NameResolver) m ad (tcref: TyconRef) =
    let g = ncenv.g
    let amap = ncenv.amap
    // Don't show constructors for type abbreviations. See FSharp 1.0 bug 2881
    if tcref.IsTypeAbbrev then
        []
    else
        let ty = FreshenTycon ncenv traitCtxtNone m tcref
        match ResolveObjectConstructor ncenv (DisplayEnv.Empty g) m ad ty with
        | Result item ->
            match item with
            | Item.FakeInterfaceCtor _ -> []
            | Item.CtorGroup(nm, ctorInfos) ->
                let ctors =
                    ctorInfos
                    |> List.filter (fun minfo ->
                        IsMethInfoAccessible amap m ad minfo &&
                        not (MethInfoIsUnseen g m ty minfo))
                match ctors with
                | [] -> []
                | _ -> [Item.MakeCtorGroup(nm, ctors)]
            | item ->
                [item]
        | Exception _ -> []

/// import.fs creates somewhat fake modules for nested members of types (so that
/// types never contain other types)
let inline notFakeContainerModule (tyconNames: HashSet<_>) nm =
    not (tyconNames.Contains nm)

let getFakeContainerModulesFromTycons (tycons:#seq<Tycon>) =
    let hashSet = HashSet()
    for tycon in tycons do
        if tycon.IsILTycon then
            hashSet.Add tycon.DisplayName |> ignore
    hashSet

let getFakeContainerModulesFromTyconRefs (tyconRefs:#seq<TyconRef>) =
    let hashSet = HashSet()
    for tyconRef in tyconRefs do
        if tyconRef.IsILTycon then
            hashSet.Add tyconRef.DisplayName |> ignore
    hashSet

/// Check is a namespace or module contains something accessible
let rec private EntityRefContainsSomethingAccessible (ncenv: NameResolver) m ad (modref: ModuleOrNamespaceRef) =
    let g = ncenv.g
    let mty = modref.ModuleOrNamespaceType

    // Search the values in the module for an accessible value
    (mty.AllValsAndMembers
     |> Seq.exists (fun v ->
         // This may explore assemblies that are not in the reference set,
         // e.g. for extension members that extend a type not in the reference set.
         // In this case assume it is accessible. The user may later explore this module
         // but will not see the extension members anyway.
         //
         // Note: this is the only use of protectAssemblyExplorationNoReraise.
         // REVIEW: consider changing this to protectAssemblyExploration. We shouldn't need
         // to catch arbitrary exceptions here.
         protectAssemblyExplorationNoReraise  true false
             (fun () ->
                 let vref = mkNestedValRef modref v
                 not vref.IsCompilerGenerated &&
                 not (IsValUnseen ad g m vref) &&
                 (vref.IsExtensionMember || not vref.IsMember)))) ||

    // Search the types in the namespace/module for an accessible tycon
    (mty.AllEntities
     |> QueueList.exists (fun tc ->
          not tc.IsModuleOrNamespace &&
          not (IsTyconUnseen ad g ncenv.amap m (modref.NestedTyconRef tc)))) ||

    // Search the sub-modules of the namespace/module for something accessible
    (mty.ModulesAndNamespacesByDemangledName
     |> NameMap.exists (fun _ submod ->
        let submodref = modref.NestedTyconRef submod
        EntityRefContainsSomethingAccessible ncenv m ad submodref))

let rec ResolvePartialLongIdentInModuleOrNamespace (ncenv: NameResolver) nenv isApplicableMeth m ad (modref: ModuleOrNamespaceRef) plid allowObsolete =
    let g = ncenv.g
    let mty = modref.ModuleOrNamespaceType
    
    match plid with
    | [] ->
         let tycons =
             mty.TypeDefinitions |> List.filter (fun tcref ->
                 not (tcref.LogicalName.Contains ",") &&
                 not (IsTyconUnseen ad g ncenv.amap m (modref.NestedTyconRef tcref)))

         let accessibleSubModules =
             let moduleOrNamespaces =
                 mty.ModulesAndNamespacesByDemangledName
                 |> NameMap.range

             if isNil moduleOrNamespaces then [] else

             let ilTyconNames = getFakeContainerModulesFromTycons mty.TypesByAccessNames.Values

             moduleOrNamespaces
             |> List.filter (fun x ->
                 let demangledName = x.DemangledModuleOrNamespaceName
                 notFakeContainerModule ilTyconNames demangledName && IsInterestingModuleName demangledName)
             |> List.map modref.NestedTyconRef
             |> List.filter (fun tyref ->
                 not (IsTyconUnseen ad g ncenv.amap m tyref) &&
                 EntityRefContainsSomethingAccessible ncenv m ad tyref)
             |> List.map ItemForModuleOrNamespaceRef

         // Collect up the accessible values in the module, excluding the members
         (mty.AllValsAndMembers
          |> Seq.toList
          |> List.choose (TryMkValRefInModRef modref) // if the assembly load set is incomplete and we get a None value here, then ignore the value
          |> List.filter (fun vref -> not vref.IsMember && not (IsValUnseen ad g m vref))
          |> List.map Item.Value)

         // Collect up the accessible discriminated union cases in the module
       @ (UnionCaseRefsInModuleOrNamespace modref
          |> List.filter (IsUnionCaseUnseen ad g ncenv.amap m >> not)
          |> List.filter (fun ucref -> not (HasFSharpAttribute g g.attrib_RequireQualifiedAccessAttribute ucref.TyconRef.Attribs))
          |> List.map (fun x -> Item.UnionCase(GeneralizeUnionCaseRef x, false)))

         // Collect up the accessible active patterns in the module
       @ (ActivePatternElemsOfModuleOrNamespace g modref
          |> NameMap.range
          |> List.filter (fun apref -> apref.ActivePatternVal |> IsValUnseen ad g m |> not)
          |> List.map Item.ActivePatternCase)

         // Collect up the accessible F# exception declarations in the module
       @ (mty.ExceptionDefinitionsByDemangledName
          |> NameMap.range
          |> List.map modref.NestedTyconRef
          |> List.filter (IsTyconUnseen ad g ncenv.amap m >> not)
          |> List.map Item.ExnCase)

       @ accessibleSubModules

    // Get all the types and .NET constructor groups accessible from here
       @ (tycons
          |> List.map (modref.NestedTyconRef >> ItemOfTyconRef ncenv m) )

       @ (tycons
          |> List.collect (modref.NestedTyconRef >> InfosForTyconConstructors ncenv m ad))

    | id :: rest  ->

        (match mty.ModulesAndNamespacesByDemangledName.TryGetValue id with
         | true, mspec ->
             let nested = modref.NestedTyconRef mspec
             if IsTyconUnseenObsoleteSpec ad g ncenv.amap m nested allowObsolete then [] else
             let allowObsolete = allowObsolete && not (isNil rest)
             ResolvePartialLongIdentInModuleOrNamespace ncenv nenv isApplicableMeth m ad nested rest allowObsolete

         | _ -> [])

      @ (LookupTypeNameInEntityNoArity m id modref.ModuleOrNamespaceType
         |> List.collect (fun tycon ->
             let tcref = modref.NestedTyconRef tycon
             if not (IsTyconUnseenObsoleteSpec ad g ncenv.amap m tcref allowObsolete) then
                 tcref |> generalizedTyconRef |> ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad true rest
             else
                 []))

/// Try to resolve a long identifier as type.
let TryToResolveLongIdentAsType (ncenv: NameResolver) (nenv: NameResolutionEnv) m (plid: string list) =
    let g = ncenv.g

    match List.tryLast plid with
    | Some id ->
        // Look for values called 'id' that accept the dot-notation
        let ty =
            match nenv.eUnqualifiedItems.TryGetValue id with
               // v.lookup: member of a value
            | true, v ->
                match v with
                | Item.Value x ->
                    let ty = x.Type
                    let ty = if x.BaseOrThisInfo = CtorThisVal && isRefCellTy g ty then destRefCellTy g ty else ty
                    Some ty
                | _ -> None
            | _ -> None

        match ty with
        | Some _ -> ty
        | _ ->
            // type.lookup: lookup a static something in a type
            LookupTypeNameInEnvNoArity OpenQualified id nenv
            |> List.tryHead
            |> Option.map (fun tcref ->
                let tcref = ResolveNestedTypeThroughAbbreviation ncenv tcref m
                FreshenTycon ncenv traitCtxtNone m tcref)
    | _ -> None

/// allowObsolete - specifies whether we should return obsolete types & modules
///   as (no other obsolete items are returned)
let rec ResolvePartialLongIdentPrim (ncenv: NameResolver) (nenv: NameResolutionEnv) isApplicableMeth fullyQualified m ad plid allowObsolete: Item list =
    let g = ncenv.g

    match plid with
    |  id :: plid when id = "global" -> // this is deliberately not the mangled name

       ResolvePartialLongIdentPrim ncenv nenv isApplicableMeth FullyQualified m ad plid allowObsolete

    |  [] ->


       /// Include all the entries in the eUnqualifiedItems table.
       let unqualifiedItems =
           match fullyQualified with
           | FullyQualified -> []
           | OpenQualified ->
               nenv.eUnqualifiedItems.Values
               |> List.filter (function
                   | Item.UnqualifiedType _ -> false
                   | Item.Value v -> not v.IsMember
                   | _ -> true)
               |> List.filter (ItemIsUnseen ad g ncenv.amap m >> not)

       let activePatternItems =
           match fullyQualified with
           | FullyQualified -> []
           | OpenQualified ->
               nenv.ePatItems
               |> NameMap.range
               |> List.filter (function Item.ActivePatternCase _v -> true | _ -> false)

       let moduleAndNamespaceItems =
           let moduleOrNamespaceRefs =
               nenv.ModulesAndNamespaces fullyQualified
               |> NameMultiMap.range

           if isNil moduleOrNamespaceRefs then [] else
           let ilTyconNames = getFakeContainerModulesFromTyconRefs (nenv.TyconsByAccessNames(fullyQualified).Values)

           moduleOrNamespaceRefs
           |> List.filter (fun modref ->
               let demangledName = modref.DemangledModuleOrNamespaceName
               IsInterestingModuleName demangledName && notFakeContainerModule ilTyconNames demangledName &&
               EntityRefContainsSomethingAccessible ncenv m ad modref &&
               not (IsTyconUnseen ad g ncenv.amap m modref))
           |> List.map ItemForModuleOrNamespaceRef

       let tycons =
           nenv.TyconsByDemangledNameAndArity(fullyQualified).Values
           |> List.filter (fun tcref ->
               not (tcref.LogicalName.Contains ",") &&
               not tcref.IsExceptionDecl &&
               not (IsTyconUnseen ad g ncenv.amap m tcref))
           |> List.map (ItemOfTyconRef ncenv m)

       // Get all the constructors accessible from here
       let constructors =
           nenv.TyconsByDemangledNameAndArity(fullyQualified).Values
           |> List.filter (IsTyconUnseen ad g ncenv.amap m >> not)
           |> List.collect (InfosForTyconConstructors ncenv m ad)

       unqualifiedItems @ activePatternItems @ moduleAndNamespaceItems @ tycons @ constructors

    | id :: rest ->

        // Look in the namespaces 'id'
        let namespaces =
            PartialResolveLongIdentAsModuleOrNamespaceThen nenv [id] (fun modref ->
              let allowObsolete = rest <> [] && allowObsolete
              if EntityRefContainsSomethingAccessible ncenv m ad modref then
                ResolvePartialLongIdentInModuleOrNamespace ncenv nenv isApplicableMeth m ad modref rest allowObsolete
              else
                [])

        // Look for values called 'id' that accept the dot-notation
        let values, isItemVal =
            (match nenv.eUnqualifiedItems.TryGetValue id with
               // v.lookup: member of a value
             | true, v ->
                 match v with
                 | Item.Value x ->
                     let ty = x.Type
                     let ty = if x.BaseOrThisInfo = CtorThisVal && isRefCellTy g ty then destRefCellTy g ty else ty
                     (ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad false rest ty), true
                 | _ -> [], false
             | _ -> [], false)

        let staticSomethingInType =
            [ if not isItemVal then
                // type.lookup: lookup a static something in a type
                for tcref in LookupTypeNameInEnvNoArity OpenQualified id nenv do
                    let tcref = ResolveNestedTypeThroughAbbreviation ncenv tcref m
                    let ty = FreshenTycon ncenv traitCtxtNone m tcref
                    yield! ResolvePartialLongIdentInType ncenv nenv isApplicableMeth m ad true rest ty ]

        namespaces @ values @ staticSomethingInType

/// Resolve a (possibly incomplete) long identifier to a set of possible resolutions.
let ResolvePartialLongIdent ncenv nenv isApplicableMeth m ad plid allowObsolete =
    ResolvePartialLongIdentPrim ncenv nenv (ResolveCompletionTargets.All isApplicableMeth) OpenQualified m ad plid allowObsolete

// REVIEW: has much in common with ResolvePartialLongIdentInModuleOrNamespace - probably they should be united
let rec ResolvePartialLongIdentInModuleOrNamespaceForRecordFields (ncenv: NameResolver) nenv m ad (modref: ModuleOrNamespaceRef) plid allowObsolete =
    let g = ncenv.g
    let mty = modref.ModuleOrNamespaceType

    match plid with
    | [] ->
       // get record type constructors
       let tycons =
           mty.TypeDefinitions
           |> List.filter (fun tcref ->
               not (tcref.LogicalName.Contains ",") &&
               tcref.IsRecordTycon &&
               not (IsTyconUnseen ad g ncenv.amap m (modref.NestedTyconRef tcref)))


       let accessibleSubModules =
           let moduleOrNamespaces =
               mty.ModulesAndNamespacesByDemangledName
               |> NameMap.range

           if isNil moduleOrNamespaces then [] else

           let ilTyconNames = getFakeContainerModulesFromTycons mty.TypesByAccessNames.Values

           moduleOrNamespaces
           |> List.filter (fun x ->
                 let demangledName = x.DemangledModuleOrNamespaceName
                 notFakeContainerModule ilTyconNames demangledName && IsInterestingModuleName demangledName)
           |> List.map modref.NestedTyconRef
           |> List.filter (fun tcref ->
               not (IsTyconUnseen ad g ncenv.amap m tcref) &&
               EntityRefContainsSomethingAccessible ncenv m ad tcref)
           |> List.map ItemForModuleOrNamespaceRef

       accessibleSubModules

       // Collect all accessible record types
       @ (tycons |> List.map (modref.NestedTyconRef >> ItemOfTyconRef ncenv m) )
       @ [ // accessible record fields
            for tycon in tycons do
                let nested = modref.NestedTyconRef tycon
                if IsEntityAccessible ncenv.amap m ad nested then
                    let ttype = FreshenTycon ncenv traitCtxtNone m nested
                    yield!
                        ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ttype)
                        |> List.map Item.RecdField
         ]

    | id :: rest  ->
        (match mty.ModulesAndNamespacesByDemangledName.TryGetValue id with
         | true, mspec ->
             let nested = modref.NestedTyconRef mspec
             if IsTyconUnseenObsoleteSpec ad g ncenv.amap m nested allowObsolete then [] else
             let allowObsolete = allowObsolete && not (isNil rest)
             ResolvePartialLongIdentInModuleOrNamespaceForRecordFields ncenv nenv m ad nested rest allowObsolete
         | _ -> [])
        @ (
            match rest with
            | [] ->
                // get all fields from the type named 'id' located in current modref
                let tycons = LookupTypeNameInEntityNoArity m id modref.ModuleOrNamespaceType
                tycons
                |> List.filter (fun tc -> tc.IsRecordTycon)
                |> List.collect (fun tycon ->
                    let tcref = modref.NestedTyconRef tycon
                    let ttype = FreshenTycon ncenv traitCtxtNone m tcref
                    ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ttype))
                |> List.map Item.RecdField
            | _ -> []
        )

/// allowObsolete - specifies whether we should return obsolete types & modules
///   as (no other obsolete items are returned)
let rec ResolvePartialLongIdentToClassOrRecdFields (ncenv: NameResolver) (nenv: NameResolutionEnv) m ad plid (allowObsolete: bool) =
    ResolvePartialLongIdentToClassOrRecdFieldsImpl ncenv nenv OpenQualified m ad plid allowObsolete

and ResolvePartialLongIdentToClassOrRecdFieldsImpl (ncenv: NameResolver) (nenv: NameResolutionEnv) fullyQualified m ad plid allowObsolete =
    let g = ncenv.g

    match  plid with
    |  id :: plid when id = "global" -> // this is deliberately not the mangled name
       // dive deeper
       ResolvePartialLongIdentToClassOrRecdFieldsImpl ncenv nenv FullyQualified m ad plid allowObsolete
    |  [] ->

        // empty plid - return namespaces\modules\record types\accessible fields


       let mods =
           let moduleOrNamespaceRefs =
               nenv.ModulesAndNamespaces fullyQualified
               |> NameMultiMap.range

           if isNil moduleOrNamespaceRefs then [] else

           let ilTyconNames = getFakeContainerModulesFromTyconRefs (nenv.TyconsByAccessNames(fullyQualified).Values)

           moduleOrNamespaceRefs
           |> List.filter (fun modref ->
               let demangledName = modref.DemangledModuleOrNamespaceName
               IsInterestingModuleName demangledName && notFakeContainerModule ilTyconNames demangledName &&
               EntityRefContainsSomethingAccessible ncenv m ad modref &&
               not (IsTyconUnseen ad g ncenv.amap m modref))
           |> List.map ItemForModuleOrNamespaceRef

       let recdTyCons =
           nenv.TyconsByDemangledNameAndArity(fullyQualified).Values
           |> List.filter (fun tcref ->
               not (tcref.LogicalName.Contains ",") &&
               tcref.IsRecordTycon &&
               not (IsTyconUnseen ad g ncenv.amap m tcref))
           |> List.map (ItemOfTyconRef ncenv m)

       let recdFields =
           nenv.eFieldLabels
           |> Seq.collect (fun (KeyValue(_, v)) -> v)
           |> Seq.map (fun fref ->
                let typeInsts = fref.TyconRef.TyparsNoRange |> List.map (fun tyar -> tyar.AsType)
                Item.RecdField(RecdFieldInfo(typeInsts, fref)))
           |> List.ofSeq

       mods @ recdTyCons @ recdFields

    | id :: rest ->
        // Get results
        let modsOrNs =
            PartialResolveLongIdentAsModuleOrNamespaceThen nenv [id] (fun modref ->
              let allowObsolete = rest <> [] && allowObsolete
              if EntityRefContainsSomethingAccessible ncenv m ad modref then
                ResolvePartialLongIdentInModuleOrNamespaceForRecordFields ncenv nenv m ad modref rest allowObsolete
              else
                [])

        let qualifiedFields =
            match rest with
            | [] ->
                // get record types accessible in given nenv
                let tycons = LookupTypeNameInEnvNoArity OpenQualified id nenv
                tycons
                |> List.collect (fun tcref ->
                    let ttype = FreshenTycon ncenv traitCtxtNone m tcref
                    ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ttype))
                |> List.map Item.RecdField
            | _-> []
        modsOrNs @ qualifiedFields

let ResolveCompletionsInTypeForItem (ncenv: NameResolver) nenv m ad statics ty (item: Item) : seq<Item> =
    seq {
        let g = ncenv.g
        let amap = ncenv.amap

        match item with
        | Item.RecdField _ ->
            yield!
                ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
                |> List.filter (fun rfref -> rfref.IsStatic = statics  &&  IsFieldInfoAccessible ad rfref)
                |> List.map Item.RecdField
        | Item.UnionCase _ ->
            if statics then
                match tryAppTy g ty with
                | ValueSome(tc, tinst) ->
                    yield!
                        tc.UnionCasesAsRefList
                        |> List.filter (IsUnionCaseUnseen ad g ncenv.amap m >> not)
                        |> List.map (fun ucref -> Item.UnionCase(UnionCaseInfo(tinst, ucref), false))
                | _ -> ()
        | Item.Event _ ->
            yield!
                ncenv.InfoReader.GetEventInfosOfType(None, ad, m, ty)
                |> List.filter (fun x ->
                    IsStandardEventInfo ncenv.InfoReader m ad x &&
                    x.IsStatic = statics)
                |> List.map Item.Event
        | Item.ILField _ ->
            yield!
                ncenv.InfoReader.GetILFieldInfosOfType(None, ad, m, ty)
                |> List.filter (fun x ->
                    not x.IsSpecialName &&
                    x.IsStatic = statics &&
                    IsILFieldInfoAccessible g amap m ad x)
                |> List.map Item.ILField
        | Item.Types _ ->
            if statics then
                yield! ty |> GetNestedTypesOfType (ncenv, ad, traitCtxtNone, None, TypeNameResolutionStaticArgsInfo.Indefinite, false, m) |> List.map (ItemOfTy g)
        | _ ->
            if not statics then
                match tryDestAnonRecdTy g ty with
                | ValueSome (anonInfo, tys) ->
                    for (i, id) in Array.indexed anonInfo.SortedIds do
                        yield Item.AnonRecdField(anonInfo, tys, i, id.idRange)
                | _ -> ()

            let pinfosIncludingUnseen =
                AllPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None ad PreferOverrides m ty
                |> List.filter (fun x ->
                    x.IsStatic = statics &&
                    IsPropInfoAccessible g amap m ad x)

            // Exclude get_ and set_ methods accessed by properties
            let pinfoMethNames =
              (pinfosIncludingUnseen
               |> List.filter (fun pinfo -> pinfo.HasGetter)
               |> List.map (fun pinfo -> pinfo.GetterMethod.LogicalName))
              @
              (pinfosIncludingUnseen
               |> List.filter (fun pinfo -> pinfo.HasSetter)
               |> List.map (fun pinfo -> pinfo.SetterMethod.LogicalName))

            let einfoMethNames =
                let einfos =
                    ncenv.InfoReader.GetEventInfosOfType(None, ad, m, ty)
                    |> List.filter (fun x ->
                        IsStandardEventInfo ncenv.InfoReader m ad x &&
                        x.IsStatic = statics)

                [ for einfo in einfos do
                    let delegateType = einfo.GetDelegateType(amap, m)
                    let (SigOfFunctionForDelegate(invokeMethInfo, _, _, _)) = GetSigOfFunctionForDelegate ncenv.InfoReader delegateType m ad
                    // Only events with void return types are suppressed in intellisense.
                    if slotSigHasVoidReturnTy (invokeMethInfo.GetSlotSig(amap, m, traitCtxtNone)) then
                      yield einfo.AddMethod.DisplayName
                      yield einfo.RemoveMethod.DisplayName ]

            let pinfos =
                pinfosIncludingUnseen
                |> List.filter (fun x -> not (PropInfoIsUnseen m x))

            let minfoFilter (suppressedMethNames: Zset<_>) (minfo: MethInfo) =
                // Only show the Finalize, MemberwiseClose etc. methods on System.Object for values whose static type really is
                // System.Object. Few of these are typically used from F#.
                //
                // Don't show GetHashCode or Equals for F# types that admit equality as an abnormal operation
                let isUnseenDueToBasicObjRules =
                    not (isObjTy g ty) &&
                    not minfo.IsExtensionMember &&
                    match minfo.LogicalName with
                    | "GetType"  -> false
                    | "GetHashCode"  -> isObjTy g minfo.ApparentEnclosingType && not (AugmentWithHashCompare.TypeDefinitelyHasEquality g ty)
                    | "ToString" -> false
                    | "Equals" ->
                        if not (isObjTy g minfo.ApparentEnclosingType) then
                            // declaring type is not System.Object - show it
                            false
                        elif minfo.IsInstance then
                            // System.Object has only one instance Equals method and we want to suppress it unless Augment.TypeDefinitelyHasEquality is true
                            not (AugmentWithHashCompare.TypeDefinitelyHasEquality g ty)
                        else
                            // System.Object has only one static Equals method and we always want to suppress it
                            true
                    | _ ->
                        // filter out self methods of obj type
                        isObjTy g minfo.ApparentEnclosingType
                let result =
                    not isUnseenDueToBasicObjRules &&
                    not minfo.IsInstance = statics &&
                    IsMethInfoAccessible amap m ad minfo &&
                    not (MethInfoIsUnseen g m ty minfo) &&
                    not minfo.IsConstructor &&
                    not minfo.IsClassConstructor &&
                    not (minfo.LogicalName = ".cctor") &&
                    not (minfo.LogicalName = ".ctor") &&
                    not (suppressedMethNames.Contains minfo.LogicalName)
                result

            let pinfoItems =
                pinfos
                |> List.choose (fun pinfo->
                    let pinfoOpt = DecodeFSharpEvent [pinfo] ad g ncenv m
                    match pinfoOpt with
                    | Some(Item.Event einfo) -> if IsStandardEventInfo ncenv.InfoReader m ad einfo then pinfoOpt else None
                    | _ -> pinfoOpt)

            yield! pinfoItems

            match item with
            | Item.MethodGroup _ ->
                // REVIEW: add a name filter here in the common cases?
                let minfos =
                    let minfos = AllMethInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv None ad PreferOverrides m ty
                    if isNil minfos then [] else

                    let suppressedMethNames = Zset.ofList String.order (pinfoMethNames @ einfoMethNames)
                    let minfos =
                        minfos
                        |> List.filter (minfoFilter suppressedMethNames)

                    if isNil minfos then
                        []
                    else
                        let minfos =
                            let addersAndRemovers =
                                let hashSet = HashSet()
                                for item in pinfoItems do
                                    match item with
                                    | Item.Event(FSEvent(_, _, addValRef, removeValRef)) ->
                                        hashSet.Add addValRef.LogicalName |> ignore
                                        hashSet.Add removeValRef.LogicalName |> ignore
                                    | _ -> ()
                                hashSet

                            if addersAndRemovers.Count = 0 then minfos
                            else minfos |> List.filter (fun minfo -> not (addersAndRemovers.Contains minfo.LogicalName))

        #if !NO_EXTENSIONTYPING
                        // Filter out the ones with mangled names from applying static parameters
                        let minfos =
                            let methsWithStaticParams =
                                minfos
                                |> List.filter (fun minfo ->
                                    match minfo.ProvidedStaticParameterInfo with
                                    | Some (_methBeforeArguments, staticParams) -> staticParams.Length <> 0
                                    | _ -> false)
                                |> List.map (fun minfo -> minfo.DisplayName)

                            if methsWithStaticParams.IsEmpty then minfos
                            else minfos |> List.filter (fun minfo ->
                                    let nm = minfo.LogicalName
                                    not (nm.Contains "," && methsWithStaticParams |> List.exists (fun m -> nm.StartsWithOrdinal m)))
        #endif

                        minfos

                // Partition methods into overload sets
                let rec partitionl (l: MethInfo list) acc =
                    match l with
                    | [] -> acc
                    | h :: t ->
                        let nm = h.LogicalName
                        partitionl t (NameMultiMap.add nm h acc)

                yield! List.map Item.MakeMethGroup (NameMap.toList (partitionl minfos Map.empty))
            | _ -> ()
    }

let rec ResolvePartialLongIdentInTypeForItem (ncenv: NameResolver) nenv m ad statics plid (item: Item) ty =
    seq {
        let g = ncenv.g
        let amap = ncenv.amap

        match plid with
        | [] -> yield! ResolveCompletionsInTypeForItem ncenv nenv m ad statics ty item
        | id :: rest ->

          let rfinfos =
              ncenv.InfoReader.GetRecordOrClassFieldsOfType(None, ad, m, ty)
              |> List.filter (fun fref -> fref.Name = id && IsRecdFieldAccessible ncenv.amap m ad fref.RecdFieldRef && fref.RecdField.IsStatic = statics)

          let nestedTypes = ty |> GetNestedTypesOfType (ncenv, ad, traitCtxtNone, Some id, TypeNameResolutionStaticArgsInfo.Indefinite, false, m)

          // e.g. <val-id>.<recdfield-id>.<more>
          for rfinfo in rfinfos do
              yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item rfinfo.FieldType

          // e.g. <val-id>.<property-id>.<more>
          let fullTypeOfPinfo (pinfo: PropInfo) =
              let rty = pinfo.GetPropertyType(amap, m)
              let rty = if pinfo.IsIndexer then mkRefTupledTy g (pinfo.GetParamTypes(amap, m)) --> rty else  rty
              rty

          let pinfos =
              ty
              |> AllPropInfosOfTypeInScope ResultCollectionSettings.AllResults ncenv.InfoReader nenv (Some id) ad IgnoreOverrides m
              |> List.filter (fun pinfo -> pinfo.IsStatic = statics && IsPropInfoAccessible g amap m ad pinfo)

          for pinfo in pinfos do
              yield! (fullTypeOfPinfo pinfo) |> ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item

          match TryFindAnonRecdFieldOfType g ty id with
          | Some (Item.AnonRecdField(_anonInfo, tys, i, _)) ->
              let tyinfo = tys.[i]
              yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item tyinfo
          | _ -> ()

          // e.g. <val-id>.<event-id>.<more>
          for einfo in ncenv.InfoReader.GetEventInfosOfType(Some id, ad, m, ty) do
              let tyinfo = PropTypOfEventInfo ncenv.InfoReader m ad einfo
              yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item tyinfo

          // nested types!
          for ty in nestedTypes do
              yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad statics rest item ty

          // e.g. <val-id>.<il-field-id>.<more>
          for finfo in ncenv.InfoReader.GetILFieldInfosOfType(Some id, ad, m, ty) do
              if not finfo.IsSpecialName && finfo.IsStatic = statics && IsILFieldInfoAccessible g amap m ad finfo then
                  yield! finfo.FieldType(amap, m) |> ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item
    }

let rec ResolvePartialLongIdentInModuleOrNamespaceForItem (ncenv: NameResolver) nenv m ad (modref: ModuleOrNamespaceRef) plid (item: Item) =
    let g = ncenv.g
    let mty = modref.ModuleOrNamespaceType

    seq {
        match plid with
        | [] ->
             match item with
             | Item.Value _ ->
                  // Collect up the accessible values in the module, excluding the members
                  yield!
                      mty.AllValsAndMembers
                      |> Seq.toList
                      |> List.choose (TryMkValRefInModRef modref) // if the assembly load set is incomplete and we get a None value here, then ignore the value
                      |> List.filter (fun vref -> not vref.IsMember && not (IsValUnseen ad g m vref))
                      |> List.map Item.Value
             | Item.UnionCase _ ->
             // Collect up the accessible discriminated union cases in the module
                  yield!
                      UnionCaseRefsInModuleOrNamespace modref
                      |> List.filter (IsUnionCaseUnseen ad g ncenv.amap m >> not)
                      |> List.filter (fun ucref -> not (HasFSharpAttribute g g.attrib_RequireQualifiedAccessAttribute ucref.TyconRef.Attribs))
                      |> List.map (fun x -> Item.UnionCase(GeneralizeUnionCaseRef x,  false))
             | Item.ActivePatternCase _ ->
             // Collect up the accessible active patterns in the module
                 yield!
                      ActivePatternElemsOfModuleOrNamespace g modref
                      |> NameMap.range
                      |> List.filter (fun apref -> apref.ActivePatternVal |> IsValUnseen ad g m |> not)
                      |> List.map Item.ActivePatternCase
             | Item.ExnCase _ ->
             // Collect up the accessible F# exception declarations in the module
                 yield!
                     mty.ExceptionDefinitionsByDemangledName
                     |> NameMap.range
                     |> List.map modref.NestedTyconRef
                     |> List.filter (IsTyconUnseen ad g ncenv.amap m >> not)
                     |> List.map Item.ExnCase
             | _ ->
                 // Collect up the accessible sub-modules. We must yield them even though `item` is not a module or namespace,
                 // otherwise we would not resolve long idents which have modules and namespaces in the middle (i.e. all long idents)

                 let moduleOrNamespaces =
                     mty.ModulesAndNamespacesByDemangledName
                     |> NameMap.range

                 if not (isNil moduleOrNamespaces) then
                     let ilTyconNames = getFakeContainerModulesFromTycons mty.TypesByAccessNames.Values

                     yield!
                         moduleOrNamespaces
                         |> List.filter (fun x ->
                               let demangledName = x.DemangledModuleOrNamespaceName
                               notFakeContainerModule ilTyconNames demangledName && IsInterestingModuleName demangledName)
                         |> List.map modref.NestedTyconRef
                         |> List.filter (fun tcref ->
                             not (IsTyconUnseen ad g ncenv.amap m tcref) &&
                             EntityRefContainsSomethingAccessible ncenv m ad tcref)
                         |> List.map ItemForModuleOrNamespaceRef

                 let tycons =
                     mty.TypeDefinitions
                     |> List.filter (fun tcref ->
                         not (tcref.LogicalName.Contains ",") &&
                         not (IsTyconUnseen ad g ncenv.amap m (modref.NestedTyconRef tcref)))

                 // Get all the types and .NET constructor groups accessible from here
                 let nestedTycons = tycons |> List.map modref.NestedTyconRef
                 yield! nestedTycons |> List.map (ItemOfTyconRef ncenv m)
                 yield! nestedTycons |> List.collect (InfosForTyconConstructors ncenv m ad)

        | id :: rest  ->

            match mty.ModulesAndNamespacesByDemangledName.TryGetValue id with
            | true, mspec ->
                let nested = modref.NestedTyconRef mspec
                if not (IsTyconUnseenObsoleteSpec ad g ncenv.amap m nested true) then
                    yield! ResolvePartialLongIdentInModuleOrNamespaceForItem ncenv nenv m ad nested rest item
            | _ -> ()

            for tycon in LookupTypeNameInEntityNoArity m id modref.ModuleOrNamespaceType do
                 let tcref = modref.NestedTyconRef tycon
                 if not (IsTyconUnseenObsoleteSpec ad g ncenv.amap m tcref true) then
                     yield! tcref |> generalizedTyconRef |> ResolvePartialLongIdentInTypeForItem ncenv nenv m ad true rest item
    }

let rec PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThenLazy f plid (modref: ModuleOrNamespaceRef) =
    let mty = modref.ModuleOrNamespaceType
    match plid with
    | [] -> f modref
    | id :: rest ->
        match mty.ModulesAndNamespacesByDemangledName.TryGetValue id with
        | true, mty ->
            PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThenLazy f rest (modref.NestedTyconRef mty)
        | _ -> Seq.empty

let PartialResolveLongIdentAsModuleOrNamespaceThenLazy (nenv: NameResolutionEnv) plid f =
    seq {
        match plid with
        | id :: rest ->
            match nenv.eModulesAndNamespaces.TryGetValue id with
            | true, modrefs ->
                for modref in modrefs do
                    yield! PartialResolveLookupInModuleOrNamespaceAsModuleOrNamespaceThenLazy f rest modref
            | _ -> ()
        | [] -> ()
    }

let rec GetCompletionForItem (ncenv: NameResolver) (nenv: NameResolutionEnv) m ad plid (item: Item) : seq<Item> =
    seq {
        let g = ncenv.g

        match plid with
        |  "global" :: plid -> // this is deliberately not the mangled name

           yield! GetCompletionForItem ncenv nenv m ad plid item

        |  [] ->

           /// Include all the entries in the eUnqualifiedItems table.
           for uitem in nenv.eUnqualifiedItems.Values do
               match uitem with
               | Item.UnqualifiedType _ -> ()
               | _ when not (ItemIsUnseen ad g ncenv.amap m uitem) ->
                   yield uitem
               | _ -> ()

           match item with
           | Item.ModuleOrNamespaces _ ->
               let moduleOrNamespaceRefs =
                   nenv.ModulesAndNamespaces OpenQualified
                   |> NameMultiMap.range

               if not (isNil moduleOrNamespaceRefs) then
                   let ilTyconNames = getFakeContainerModulesFromTyconRefs (nenv.TyconsByAccessNames(OpenQualified).Values)

                   for ns in moduleOrNamespaceRefs do
                       let demangledName = ns.DemangledModuleOrNamespaceName
                       if IsInterestingModuleName demangledName && notFakeContainerModule ilTyconNames demangledName
                          && EntityRefContainsSomethingAccessible ncenv m ad ns
                          && not (IsTyconUnseen ad g ncenv.amap m ns)
                       then yield ItemForModuleOrNamespaceRef ns

           | Item.Types _ ->
               for tcref in nenv.TyconsByDemangledNameAndArity(OpenQualified).Values do
                   if not tcref.IsExceptionDecl
                      && not (tcref.LogicalName.Contains ",")
                      && not (IsTyconUnseen ad g ncenv.amap m tcref)
                   then yield ItemOfTyconRef ncenv m tcref

           | Item.ActivePatternCase _ ->
               for pitem in NameMap.range nenv.ePatItems do
                   match pitem with
                   | Item.ActivePatternCase _ ->
                       yield pitem
                   | _ -> ()

           | Item.DelegateCtor _
           | Item.FakeInterfaceCtor _
           | Item.CtorGroup _
           | Item.UnqualifiedType _ ->
               for tcref in nenv.TyconsByDemangledNameAndArity(OpenQualified).Values do
                   if not (IsTyconUnseen ad g ncenv.amap m tcref)
                   then yield! InfosForTyconConstructors ncenv m ad tcref

           | _ -> ()

        | id :: rest ->

            // Look in the namespaces 'id'
            yield!
                PartialResolveLongIdentAsModuleOrNamespaceThenLazy nenv [id] (fun modref ->
                    if EntityRefContainsSomethingAccessible ncenv m ad modref then
                        ResolvePartialLongIdentInModuleOrNamespaceForItem ncenv nenv m ad modref rest item
                    else Seq.empty)

            // Look for values called 'id' that accept the dot-notation
            match nenv.eUnqualifiedItems.TryGetValue id with
            | true, Item.Value x ->
                let ty = x.Type
                let ty = if x.BaseOrThisInfo = CtorThisVal && isRefCellTy g ty then destRefCellTy g ty else ty
                yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad false rest item ty
            | _ ->
                // type.lookup: lookup a static something in a type
                for tcref in LookupTypeNameInEnvNoArity OpenQualified id nenv do
                    let tcref = ResolveNestedTypeThroughAbbreviation ncenv tcref m
                    let ty = FreshenTycon ncenv traitCtxtNone m tcref
                    yield! ResolvePartialLongIdentInTypeForItem ncenv nenv m ad true rest item ty
    }

let IsItemResolvable (ncenv: NameResolver) (nenv: NameResolutionEnv) m ad plid (item: Item) : bool =
    protectAssemblyExploration false (fun () ->
        GetCompletionForItem ncenv nenv m ad plid item
        |> Seq.exists (ItemsAreEffectivelyEqual ncenv.g item)
    )

let GetVisibleNamespacesAndModulesAtPoint (ncenv: NameResolver) (nenv: NameResolutionEnv) m ad =
    protectAssemblyExploration [] (fun () ->
        let items =
            nenv.ModulesAndNamespaces FullyQualifiedFlag.OpenQualified
            |> NameMultiMap.range

        if isNil items then [] else

        let ilTyconNames = getFakeContainerModulesFromTyconRefs (nenv.TyconsByAccessNames(FullyQualifiedFlag.OpenQualified).Values)

        items
        |> List.filter (fun x ->
             let demangledName = x.DemangledModuleOrNamespaceName
             IsInterestingModuleName demangledName && notFakeContainerModule ilTyconNames demangledName
             && EntityRefContainsSomethingAccessible ncenv m ad  x
             && not (IsTyconUnseen ad ncenv.g ncenv.amap m x))
    )

