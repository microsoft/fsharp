// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.


/// Select members from a type by name, searching the type hierarchy if needed
module internal FSharp.Compiler.InfoReader

open Internal.Utilities.Library
open FSharp.Compiler 
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.Import
open FSharp.Compiler.Infos
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree

/// Try to select an F# value when querying members, and if so return a MethInfo that wraps the F# value.
val TrySelectMemberVal: g:TcGlobals -> optFilter:string option -> ty:TType -> pri:ExtensionMethodPriority option -> _membInfo:'a -> vref:ValRef -> MethInfo option

/// Query the immediate methods of an F# type, not taking into account inherited methods. The optFilter
/// parameter is an optional name to restrict the set of properties returned.
val GetImmediateIntrinsicMethInfosOfType: optFilter:string option * ad:AccessorDomain -> g:TcGlobals -> amap:ImportMap -> m:range -> ty:TType -> MethInfo list

/// A helper type to help collect properties.
///
/// Join up getters and setters which are not associated in the F# data structure 
type PropertyCollector =
    new: g:TcGlobals * amap:ImportMap * m:range * ty:TType * optFilter:string option * ad:AccessorDomain -> PropertyCollector
    member Close: unit -> PropInfo list
    member Collect: membInfo:ValMemberInfo * vref:ValRef -> unit
  
/// Query the immediate properties of an F# type, not taking into account inherited properties. The optFilter
/// parameter is an optional name to restrict the set of properties returned.
val GetImmediateIntrinsicPropInfosOfType: optFilter:string option * ad:AccessorDomain -> g:TcGlobals -> amap:ImportMap -> m:range -> ty:TType -> PropInfo list

/// Checks whether the given type has an indexer property.
val IsIndexerType: g:TcGlobals -> amap:ImportMap -> ty:TType -> bool

/// Get the items that are considered the most specific in the hierarchy out of the given items by type.
val GetMostSpecificItemsByType: g:TcGlobals -> amap:ImportMap -> f:('a -> (TType * range) option) -> xs:'a list -> 'a list

/// From the given method sets, filter each set down to the most specific ones. 
val FilterMostSpecificMethInfoSets: g:TcGlobals -> amap:ImportMap -> m:range -> minfoSets:NameMultiMap<TType *MethInfo> -> NameMultiMap<TType * MethInfo>

/// Sets of methods up the hierarchy, ignoring duplicates by name and sig.
/// Used to collect sets of virtual methods, protected methods, protected
/// properties etc. 
type HierarchyItem =
    | MethodItem of MethInfo list list
    | PropertyItem of PropInfo list list
    | RecdFieldItem of RecdFieldInfo
    | EventItem of EventInfo list
    | ILFieldItem of ILFieldInfo list

/// Indicates if we prefer overrides or abstract slots. 
type FindMemberFlag = 
    /// Prefer items toward the top of the hierarchy, which we do if the items are virtual 
    /// but not when resolving base calls. 
    | IgnoreOverrides 

    /// Get overrides instead of abstract slots when measuring whether a class/interface implements all its required slots. 
    | PreferOverrides

/// An InfoReader is an object to help us read and cache infos. 
/// We create one of these for each file we typecheck. 
type InfoReader =

    /// Get the declared IL fields of a type, not including inherited fields
    new: g:TcGlobals * amap:ImportMap -> InfoReader

    /// Get the super-types of a type, including interface types.
    member GetEntireTypeHierarchy: allowMultiIntfInst:AllowMultiIntfInstantiations * m:range * ty:TType -> TType list

    /// Read the events of a type, including inherited ones. Cache the result for monomorphic types.
    member GetEventInfosOfType: optFilter:string option * ad:AccessorDomain * m:range * ty:TType -> EventInfo list

    /// Read the IL fields of a type, including inherited ones. Cache the result for monomorphic types.
    member GetILFieldInfosOfType: optFilter:string option * ad:AccessorDomain * m:range * ty:TType -> ILFieldInfo list
    member GetImmediateIntrinsicEventsOfType: optFilter:string option * ad:AccessorDomain * m:range * ty:TType -> EventInfo list

    /// Get the super-types of a type, excluding interface types.
    member GetPrimaryTypeHierarchy: allowMultiIntfInst:AllowMultiIntfInstantiations * m:range * ty:TType -> TType list

    /// Read the raw method sets of a type, including inherited ones. Cache the result for monomorphic types
    member GetRawIntrinsicMethodSetsOfType: optFilter:string option * ad:AccessorDomain * allowMultiIntfInst:AllowMultiIntfInstantiations * m:range * ty:TType -> MethInfo list list

    /// Read the record or class fields of a type, including inherited ones. Cache the result for monomorphic types.
    member GetRecordOrClassFieldsOfType: optFilter:string option * ad:AccessorDomain * m:range * ty:TType -> RecdFieldInfo list

    /// Check if the given language feature is supported by the runtime.
    member IsLanguageFeatureRuntimeSupported: langFeature:Features.LanguageFeature -> bool

    /// Try and find a record or class field for a type.
    member TryFindRecdOrClassFieldInfoOfType: nm:string * m:range * ty:TType -> RecdFieldInfo voption
    member amap: ImportMap
    member g: TcGlobals
  
    /// Exclude methods from super types which have the same signature as a method in a more specific type.
    static member ExcludeHiddenOfMethInfos: g:TcGlobals -> amap:ImportMap -> m:range -> minfos:MethInfo list list -> MethInfo list

    /// Exclude properties from super types which have the same name as a property in a more specific type.
    static member ExcludeHiddenOfPropInfos: g:TcGlobals -> amap:ImportMap -> m:range -> pinfos:PropInfo list list -> PropInfo list

    /// Get the sets of intrinsic methods in the hierarchy (not including extension methods)
    member GetIntrinsicMethInfoSetsOfType: optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> MethInfo list list

    /// Get the sets intrinsic properties in the hierarchy (not including extension properties)
    member GetIntrinsicPropInfoSetsOfType: optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> PropInfo list list

    /// Get the flattened list of intrinsic methods in the hierarchy
    member GetIntrinsicMethInfosOfType: optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> MethInfo list

    /// Get the flattened list of intrinsic properties in the hierarchy
    member GetIntrinsicPropInfosOfType: optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> PropInfo list

    /// Perform type-directed name resolution of a particular named member in an F# type
    member TryFindIntrinsicNamedItemOfType: nm:string * ad:AccessorDomain -> findFlag:FindMemberFlag -> m:range -> ty:TType -> HierarchyItem option

    /// Find the op_Implicit for a type
    member FindImplicitConversions: m: range -> ad: AccessorDomain -> ty: TType -> MethInfo list

val checkLanguageFeatureRuntimeError: infoReader:InfoReader -> langFeature:Features.LanguageFeature -> m:range -> unit

val checkLanguageFeatureRuntimeErrorRecover: infoReader:InfoReader -> langFeature:Features.LanguageFeature -> m:range -> unit

val tryLanguageFeatureRuntimeErrorRecover: infoReader:InfoReader -> langFeature:Features.LanguageFeature -> m:range -> bool

/// Get the declared constructors of any F# type
val GetIntrinsicConstructorInfosOfType: infoReader:InfoReader -> m:range -> ty:TType -> MethInfo list

/// Exclude methods from super types which have the same signature as a method in a more specific type.
val ExcludeHiddenOfMethInfos: g:TcGlobals -> amap:ImportMap -> m:range -> minfos:MethInfo list list -> MethInfo list

/// Exclude properties from super types which have the same name as a property in a more specific type.
val ExcludeHiddenOfPropInfos: g:TcGlobals -> amap:ImportMap -> m:range -> pinfos:PropInfo list list -> PropInfo list

/// Get the sets of intrinsic methods in the hierarchy (not including extension methods)
val GetIntrinsicMethInfoSetsOfType: infoReader:InfoReader -> optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> MethInfo list list

/// Get the sets intrinsic properties in the hierarchy (not including extension properties)
val GetIntrinsicPropInfoSetsOfType: infoReader:InfoReader -> optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> PropInfo list list

/// Get the flattened list of intrinsic methods in the hierarchy
val GetIntrinsicMethInfosOfType: infoReader:InfoReader -> optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> MethInfo list

/// Get the flattened list of intrinsic properties in the hierarchy
val GetIntrinsicPropInfosOfType: infoReader:InfoReader -> optFilter:string option -> ad:AccessorDomain -> allowMultiIntfInst:AllowMultiIntfInstantiations -> findFlag:FindMemberFlag -> m:range -> ty:TType -> PropInfo list

/// Perform type-directed name resolution of a particular named member in an F# type
val TryFindIntrinsicNamedItemOfType: infoReader:InfoReader -> nm:string * ad:AccessorDomain -> findFlag:FindMemberFlag -> m:range -> ty:TType -> HierarchyItem option

/// Try to detect the existence of a method on a type.
val TryFindIntrinsicMethInfo: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> nm:string -> ty:TType -> MethInfo list

/// Try to find a particular named property on a type. Only used to ensure that local 'let' definitions and property names
/// are distinct, a somewhat adhoc check in tc.fs.
val TryFindIntrinsicPropInfo: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> nm:string -> ty:TType -> PropInfo list

/// Get a set of most specific override methods.
val GetIntrinisicMostSpecificOverrideMethInfoSetsOfType: infoReader:InfoReader -> m:range -> ty:TType -> NameMultiMap<TType * MethInfo>

/// The Invoke MethInfo, the function argument types, the function return type 
/// and the overall F# function type for the function type associated with a .NET delegate type
[<NoEquality; NoComparison>]
type SigOfFunctionForDelegate = | SigOfFunctionForDelegate of MethInfo * TType list * TType * TType

/// Given a delegate type work out the minfo, argument types, return type 
/// and F# function type by looking at the Invoke signature of the delegate. 
val GetSigOfFunctionForDelegate: infoReader:InfoReader -> delty:TType -> m:range -> ad:AccessorDomain -> SigOfFunctionForDelegate

/// Try and interpret a delegate type as a "standard" .NET delegate type associated with an event, with a "sender" parameter.
val TryDestStandardDelegateType: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> delTy:TType -> (TType * TType) option

/// Indicates if an event info is associated with a delegate type that is a "standard" .NET delegate type
/// with a sender parameter.
val IsStandardEventInfo: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> einfo:EventInfo -> bool

val ArgsTypOfEventInfo: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> einfo:EventInfo -> TType

val PropTypOfEventInfo: infoReader:InfoReader -> m:range -> ad:AccessorDomain -> einfo:EventInfo -> TType

/// Try to find the name of the metadata file for this external definition 
val TryFindMetadataInfoOfExternalEntityRef: infoReader:InfoReader -> m:range -> eref:EntityRef -> (string option * Typars * ILTypeInfo) option

/// Try to find the xml doc associated with the assembly name and metadata key
val TryFindXmlDocByAssemblyNameAndSig: infoReader:InfoReader -> assemblyName: string -> xmlDocSig: string -> XmlDoc option

val GetXmlDocSigOfEntityRef: infoReader:InfoReader -> m:range -> eref:EntityRef -> (string option * string) option

val GetXmlDocSigOfScopedValRef: TcGlobals -> tcref:TyconRef -> vref:ValRef -> (string option * string) option

val GetXmlDocSigOfRecdFieldRef: rfref:RecdFieldRef -> (string option * string) option

val GetXmlDocSigOfUnionCaseRef: ucref:UnionCaseRef -> (string option * string) option

val GetXmlDocSigOfMethInfo: infoReader: InfoReader -> m:range -> minfo:MethInfo -> (string option * string) option

val GetXmlDocSigOfValRef: TcGlobals -> vref: ValRef -> (string option * string) option

val GetXmlDocSigOfProp: infoReader:InfoReader -> m:range -> pinfo:PropInfo -> (string option * string) option

val GetXmlDocSigOfEvent: infoReader:InfoReader -> m:range -> einfo:EventInfo -> (string option * string) option

val GetXmlDocSigOfILFieldInfo: infoReader:InfoReader -> m:range -> finfo:ILFieldInfo -> (string option * string) option