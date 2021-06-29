// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.
  
/// Defines the typed abstract syntax intermediate representation used throughout the F# compiler.
module internal rec FSharp.Compiler.TypedTree 

open System
open System.Collections.Generic 
open System.Diagnostics
open System.Reflection

open Internal.Utilities.Collections
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras
open Internal.Utilities.Rational

open FSharp.Compiler 
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.AbstractIL.ILX.Types
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Syntax
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.QuotationPickler
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
open FSharp.Core.CompilerServices
#endif

type Stamp = int64

type StampMap<'T> = Map<Stamp, 'T>

[<RequireQualifiedAccess>]
type ValInline =

    /// Indicates the value must always be inlined and no .NET IL code is generated for the value/function
    | PseudoVal

    /// Indicates the value is inlined but the .NET IL code for the function still exists, e.g. to satisfy interfaces on objects, but that it is also always inlined 
    | Always

    /// Indicates the value may optionally be inlined by the optimizer
    | Optional

    /// Indicates the value must never be inlined by the optimizer
    | Never

    /// Returns true if the implementation of a value must always be inlined
    member x.MustInline = 
        match x with 
        | ValInline.PseudoVal | ValInline.Always -> true 
        | ValInline.Optional | ValInline.Never -> false

/// A flag associated with values that indicates whether the recursive scope of the value is currently being processed, and 
/// if the value has been generalized or not as yet.
type ValRecursiveScopeInfo =

    /// Set while the value is within its recursive scope. The flag indicates if the value has been eagerly generalized and accepts generic-recursive calls 
    | ValInRecScope of bool

    /// The normal value for this flag when the value is not within its recursive scope 
    | ValNotInRecScope

type ValMutability = 
    | Immutable 
    | Mutable 

[<RequireQualifiedAccess>]
/// Indicates if a type parameter is needed at runtime and may not be eliminated
type TyparDynamicReq = 

    /// Indicates the type parameter is not needed at runtime and may be eliminated
    | No 

    /// Indicates the type parameter is needed at runtime and may not be eliminated
    | Yes

type ValBaseOrThisInfo = 

    /// Indicates a ref-cell holding 'this' or the implicit 'this' used throughout an 
    /// implicit constructor to access and set values
    | CtorThisVal 

    /// Indicates the value called 'base' available for calling base class members
    | BaseVal  

    /// Indicates a normal value
    | NormalVal  

    /// Indicates the 'this' value specified in a memberm e.g. 'x' in 'member x.M() = 1'
    | MemberThisVal 

[<Struct>]
/// Flags on values
type ValFlags(flags: int64) = 

    new (recValInfo, baseOrThis, isCompGen, inlineInfo, isMutable, isModuleOrMemberBinding, isExtensionMember, isIncrClassSpecialMember, isTyFunc, allowTypeInst, isGeneratedEventVal) =
        let flags = 
                     (match baseOrThis with
                                        | BaseVal ->                         0b00000000000000000000L
                                        | CtorThisVal ->                     0b00000000000000000010L
                                        | NormalVal ->                       0b00000000000000000100L
                                        | MemberThisVal ->                   0b00000000000000000110L) |||
                     (if isCompGen then                                      0b00000000000000001000L 
                      else                                                   0b000000000000000000000L) |||
                     (match inlineInfo with
                                        | ValInline.PseudoVal ->             0b00000000000000000000L
                                        | ValInline.Always ->                0b00000000000000010000L
                                        | ValInline.Optional ->              0b00000000000000100000L
                                        | ValInline.Never ->                 0b00000000000000110000L) |||
                     (match isMutable with
                                        | Immutable ->                       0b00000000000000000000L
                                        | Mutable   ->                       0b00000000000001000000L) |||

                     (match isModuleOrMemberBinding with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00000000000010000000L) |||
                     (match isExtensionMember with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00000000000100000000L) |||
                     (match isIncrClassSpecialMember with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00000000001000000000L) |||
                     (match isTyFunc with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00000000010000000000L) |||

                     (match recValInfo with
                                     | ValNotInRecScope     ->               0b00000000000000000000L
                                     | ValInRecScope true   ->               0b00000000100000000000L
                                     | ValInRecScope false  ->               0b00000001000000000000L) |||

                     (match allowTypeInst with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00000100000000000000L) |||

                     (match isGeneratedEventVal with
                                        | false     ->                       0b00000000000000000000L
                                        | true      ->                       0b00100000000000000000L)                                        

        ValFlags flags

    member x.BaseOrThisInfo = 
                                  match (flags       &&&                     0b00000000000000000110L) with 
                                                             |               0b00000000000000000000L -> BaseVal
                                                             |               0b00000000000000000010L -> CtorThisVal
                                                             |               0b00000000000000000100L -> NormalVal
                                                             |               0b00000000000000000110L -> MemberThisVal
                                                             | _          -> failwith "unreachable"



    member x.IsCompilerGenerated =      (flags       &&&                     0b00000000000000001000L) <> 0x0L

    member x.WithIsCompilerGenerated isCompGen = 
            let flags =                 (flags       &&&                  ~~~0b00000000000000001000L) |||
                                        (match isCompGen with
                                          | false           ->               0b00000000000000000000L
                                          | true            ->               0b00000000000000001000L)
            ValFlags flags

    member x.InlineInfo = 
                                  match (flags       &&&                     0b00000000000000110000L) with 
                                                             |               0b00000000000000000000L -> ValInline.PseudoVal
                                                             |               0b00000000000000010000L -> ValInline.Always
                                                             |               0b00000000000000100000L -> ValInline.Optional
                                                             |               0b00000000000000110000L -> ValInline.Never
                                                             | _          -> failwith "unreachable"

    member x.MutabilityInfo = 
                                  match (flags       &&&                     0b00000000000001000000L) with 
                                                             |               0b00000000000000000000L -> Immutable
                                                             |               0b00000000000001000000L -> Mutable
                                                             | _          -> failwith "unreachable"


    member x.IsMemberOrModuleBinding = 
                                  match (flags       &&&                     0b00000000000010000000L) with 
                                                             |               0b00000000000000000000L -> false
                                                             |               0b00000000000010000000L -> true
                                                             | _          -> failwith "unreachable"


    member x.WithIsMemberOrModuleBinding = ValFlags(flags |||                0b00000000000010000000L)


    member x.IsExtensionMember        = (flags       &&&                     0b00000000000100000000L) <> 0L

    member x.IsIncrClassSpecialMember = (flags       &&&                     0b00000000001000000000L) <> 0L

    member x.IsTypeFunction           = (flags       &&&                     0b00000000010000000000L) <> 0L

    member x.RecursiveValInfo =   match (flags       &&&                     0b00000001100000000000L) with 
                                                             |               0b00000000000000000000L -> ValNotInRecScope
                                                             |               0b00000000100000000000L -> ValInRecScope true
                                                             |               0b00000001000000000000L -> ValInRecScope false
                                                             | _                   -> failwith "unreachable"

    member x.WithRecursiveValInfo recValInfo = 
            let flags = 
                     (flags       &&&                                    ~~~0b00000001100000000000L) |||
                     (match recValInfo with
                                     | ValNotInRecScope     ->              0b00000000000000000000L
                                     | ValInRecScope true  ->               0b00000000100000000000L
                                     | ValInRecScope false ->               0b00000001000000000000L) 
            ValFlags flags

    member x.MakesNoCriticalTailcalls         =                   (flags &&& 0b00000010000000000000L) <> 0L

    member x.WithMakesNoCriticalTailcalls =               ValFlags(flags ||| 0b00000010000000000000L)

    member x.PermitsExplicitTypeInstantiation =                   (flags &&& 0b00000100000000000000L) <> 0L

    member x.HasBeenReferenced                =                   (flags &&& 0b00001000000000000000L) <> 0L

    member x.WithHasBeenReferenced                     =  ValFlags(flags ||| 0b00001000000000000000L)

    member x.IsCompiledAsStaticPropertyWithoutField =             (flags &&& 0b00010000000000000000L) <> 0L

    member x.WithIsCompiledAsStaticPropertyWithoutField = ValFlags(flags ||| 0b00010000000000000000L)

    member x.IsGeneratedEventVal =                                (flags &&& 0b00100000000000000000L) <> 0L

    member x.IsFixed                                =             (flags &&& 0b01000000000000000000L) <> 0L

    member x.WithIsFixed                               =  ValFlags(flags ||| 0b01000000000000000000L)

    member x.IgnoresByrefScope                         =          (flags &&& 0b10000000000000000000L) <> 0L

    member x.WithIgnoresByrefScope                     =  ValFlags(flags ||| 0b10000000000000000000L)

    /// Get the flags as included in the F# binary metadata
    member x.PickledBits = 
        // Clear the RecursiveValInfo, only used during inference and irrelevant across assembly boundaries
        // Clear the IsCompiledAsStaticPropertyWithoutField, only used to determine whether to use a true field for a value, and to eliminate the optimization info for observable bindings
        // Clear the HasBeenReferenced, only used to report "unreferenced variable" warnings and to help collect 'it' values in FSI.EXE
        // Clear the IsGeneratedEventVal, since there's no use in propagating specialname information for generated add/remove event vals
                                                      (flags       &&&    ~~~0b10011001100000000000L) 

/// Represents the kind of a type parameter
[<RequireQualifiedAccess (* ; StructuredFormatDisplay("{DebugText}") *) >]
type TyparKind = 

    | Type 

    | Measure

    member x.AttrName =
        match x with
        | TyparKind.Type -> ValueNone
        | TyparKind.Measure -> ValueSome "Measure"

    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText  =  x.ToString()

    override x.ToString() =
        match x with
        | TyparKind.Type -> "type"
        | TyparKind.Measure -> "measure"

/// Indicates if the type variable can be solved or given new constraints. The status of a type variable
/// evolves towards being either rigid or solved. 
[<RequireQualifiedAccess>]
type TyparRigidity = 

    /// Indicates the type parameter can't be solved
    | Rigid 

    /// Indicates the type parameter can't be solved, but the variable is not set to "rigid" until after inference is complete
    | WillBeRigid 

    /// Indicates we give a warning if the type parameter is ever solved
    | WarnIfNotRigid 

    /// Indicates the type parameter is an inference variable may be solved
    | Flexible

    /// Indicates the type parameter derives from an '_' anonymous type
    /// For units-of-measure, we give a warning if this gets solved to '1'
    | Anon

    member x.ErrorIfUnified = match x with TyparRigidity.Rigid -> true | _ -> false

    member x.WarnIfUnified = match x with TyparRigidity.WillBeRigid | TyparRigidity.WarnIfNotRigid -> true | _ -> false

    member x.WarnIfMissingConstraint = match x with TyparRigidity.WillBeRigid -> true | _ -> false


/// Encode typar flags into a bit field  
[<Struct>]
type TyparFlags(flags: int32) =

    new (kind: TyparKind, rigidity: TyparRigidity, isFromError: bool, isCompGen: bool, staticReq: TyparStaticReq, dynamicReq: TyparDynamicReq, equalityDependsOn: bool, comparisonDependsOn: bool) = 
        TyparFlags((if isFromError then                0b00000000000000010 else 0) |||
                   (if isCompGen   then                0b00000000000000100 else 0) |||
                   (match staticReq with
                     | TyparStaticReq.None                  -> 0b00000000000000000
                     | TyparStaticReq.HeadType            -> 0b00000000000001000) |||
                   (match rigidity with
                     | TyparRigidity.Rigid          -> 0b00000000000000000
                     | TyparRigidity.WillBeRigid    -> 0b00000000000100000
                     | TyparRigidity.WarnIfNotRigid -> 0b00000000001000000
                     | TyparRigidity.Flexible       -> 0b00000000001100000
                     | TyparRigidity.Anon           -> 0b00000000010000000) |||
                   (match kind with
                     | TyparKind.Type               -> 0b00000000000000000
                     | TyparKind.Measure            -> 0b00000000100000000) |||   
                   (if comparisonDependsOn then 
                                                       0b00000001000000000 else 0) |||
                   (match dynamicReq with
                     | TyparDynamicReq.No           -> 0b00000000000000000
                     | TyparDynamicReq.Yes          -> 0b00000010000000000) |||
                   (if equalityDependsOn then 
                                                       0b00000100000000000 else 0))

    /// Indicates if the type inference variable was generated after an error when type checking expressions or patterns
    member x.IsFromError         = (flags &&& 0b00000000000000010) <> 0x0

    /// Indicates if the type variable is compiler generated, i.e. is an implicit type inference variable 
    member x.IsCompilerGenerated = (flags &&& 0b00000000000000100) <> 0x0

    /// Indicates if the type variable has a static "head type" requirement, i.e. ^a variables used in FSharp.Core and member constraints.
    member x.StaticReq = 
                             match (flags &&& 0b00000000000001000) with 
                                            | 0b00000000000000000 -> TyparStaticReq.None
                                            | 0b00000000000001000 -> TyparStaticReq.HeadType
                                            | _             -> failwith "unreachable"

    /// Indicates if the type variable can be solved or given new constraints. The status of a type variable
    /// generally always evolves towards being either rigid or solved. 
    member x.Rigidity = 
                             match (flags &&& 0b00000000011100000) with 
                                            | 0b00000000000000000 -> TyparRigidity.Rigid
                                            | 0b00000000000100000 -> TyparRigidity.WillBeRigid
                                            | 0b00000000001000000 -> TyparRigidity.WarnIfNotRigid
                                            | 0b00000000001100000 -> TyparRigidity.Flexible
                                            | 0b00000000010000000 -> TyparRigidity.Anon
                                            | _          -> failwith "unreachable"

    /// Indicates whether a type variable can be instantiated by types or units-of-measure.
    member x.Kind = 
                             match (flags &&& 0b00001000100000000) with 
                                            | 0b00000000000000000 -> TyparKind.Type
                                            | 0b00000000100000000 -> TyparKind.Measure
                                            | _             -> failwith "unreachable"


    /// Indicates that whether or not a generic type definition satisfies the comparison constraint is dependent on whether this type variable satisfies the comparison constraint.
    member x.ComparisonConditionalOn =
                                   (flags &&& 0b00000001000000000) <> 0x0

    /// Indicates if a type parameter is needed at runtime and may not be eliminated
    member x.DynamicReq = 
                             match (flags &&& 0b00000010000000000) with 
                                            | 0b00000000000000000 -> TyparDynamicReq.No
                                            | 0b00000010000000000 -> TyparDynamicReq.Yes
                                            | _             -> failwith "unreachable"

    /// Indicates that whether or not a generic type definition satisfies the equality constraint is dependent on whether this type variable satisfies the equality constraint.
    member x.EqualityConditionalOn = 
                                   (flags &&& 0b00000100000000000) <> 0x0

    /// Indicates that whether this type parameter is a compat-flex type parameter (i.e. where "expr :> tp" only emits an optional warning)
    member x.IsCompatFlex = 
                                   (flags &&& 0b00010000000000000) <> 0x0

    member x.WithCompatFlex b =  
                  if b then 
                      TyparFlags(flags |||    0b00010000000000000)
                  else
                      TyparFlags(flags &&& ~~~0b00010000000000000)

    /// Get the flags as included in the F# binary metadata. We pickle this as int64 to allow for future expansion
    member x.PickledBits = flags       

/// Encode entity flags into a bit field. We leave lots of space to allow for future expansion.
[<Struct>]
type EntityFlags(flags: int64) =

    new (usesPrefixDisplay, isModuleOrNamespace, preEstablishedHasDefaultCtor, hasSelfReferentialCtor, isStructRecordOrUnionType) = 
        EntityFlags((if isModuleOrNamespace then                        0b000000000000001L else 0L) |||
                    (if usesPrefixDisplay   then                        0b000000000000010L else 0L) |||
                    (if preEstablishedHasDefaultCtor then               0b000000000000100L else 0L) |||
                    (if hasSelfReferentialCtor then                     0b000000000001000L else 0L) |||
                    (if isStructRecordOrUnionType then                  0b000000000100000L else 0L)) 

    /// Indicates the Entity is actually a module or namespace, not a type definition
    member x.IsModuleOrNamespace                 = (flags       &&&     0b000000000000001L) <> 0x0L

    /// Indicates the type prefers the "tycon<a, b>" syntax for display etc. 
    member x.IsPrefixDisplay                     = (flags       &&&     0b000000000000010L) <> 0x0L
    
    // This bit is not pickled, only used while establishing a type constructor. It is needed because the type constructor
    // is known to satisfy the default constructor constraint even before any of its members have been established.
    member x.PreEstablishedHasDefaultConstructor = (flags       &&&     0b000000000000100L) <> 0x0L

    // This bit represents an F# specific condition where a type has at least one constructor that may access
    // the 'this' pointer prior to successful initialization of the partial contents of the object. In this
    // case sub-classes must protect themselves against early access to their contents.
    member x.HasSelfReferentialConstructor       = (flags       &&&     0b000000000001000L) <> 0x0L

    /// This bit is reserved for us in the pickle format, see pickle.fs, it's being listed here to stop it ever being used for anything else
    static member ReservedBitForPickleFormatTyconReprFlag   =           0b000000000010000L

    /// This bit represents a F# record that is a value type, or a struct record.
    member x.IsStructRecordOrUnionType           = (flags       &&&     0b000000000100000L) <> 0x0L

    /// These two bits represents the on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.TryIsByRefLike                      = (flags       &&&     0b000000011000000L) 
                                                                |> function 
                                                                      | 0b000000011000000L -> ValueSome true
                                                                      | 0b000000010000000L -> ValueSome false
                                                                      | _                  -> ValueNone

    /// Adjust the on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.WithIsByRefLike flag = 
            let flags = 
                     (flags       &&&                                ~~~0b000000011000000L) |||
                     (match flag with
                      | true  ->                                        0b000000011000000L
                      | false ->                                        0b000000010000000L) 
            EntityFlags flags

    /// These two bits represents the on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.TryIsReadOnly                       = (flags       &&&     0b000001100000000L) 
                                                                |> function 
                                                                      | 0b000001100000000L -> ValueSome true
                                                                      | 0b000001000000000L -> ValueSome false
                                                                      | _                  -> ValueNone

    /// Adjust the on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.WithIsReadOnly flag = 
            let flags = 
                     (flags       &&&                                ~~~0b000001100000000L) |||
                     (match flag with
                      | true  ->                                        0b000001100000000L
                      | false ->                                        0b000001000000000L) 
            EntityFlags flags

    /// These two bits represents the on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.TryIsAssumedReadOnly                = (flags       &&&     0b000110000000000L) 
                                                                |> function 
                                                                      | 0b000110000000000L -> ValueSome true
                                                                      | 0b000100000000000L -> ValueSome false
                                                                      | _                  -> ValueNone

    /// Adjust the on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.WithIsAssumedReadOnly flag = 
            let flags = 
                     (flags       &&&                                ~~~0b000110000000000L) |||
                     (match flag with
                      | true  ->                                        0b000110000000000L
                      | false ->                                        0b000100000000000L) 
            EntityFlags flags

    /// Get the flags as included in the F# binary metadata
    member x.PickledBits =                         (flags       &&&  ~~~0b000111111000100L)



exception UndefinedName of 
    depth: int * 
    error: (string -> string) * 
    id: Ident * 
    suggestions: ErrorLogger.Suggestions

exception InternalUndefinedItemRef of (string * string * string -> int * string) * string * string * string

type ModuleOrNamespaceKind = 

    /// Indicates that a module is compiled to a class with the "Module" suffix added. 
    | FSharpModuleWithSuffix 

    /// Indicates that a module is compiled to a class with the same name as the original module 
    | ModuleOrType 

    /// Indicates that a 'module' is really a namespace 
    | Namespace

/// A public path records where a construct lives within the global namespace
/// of a CCU.
type PublicPath = 
    | PubPath of string[] 
    member x.EnclosingPath = 
        let (PubPath pp) = x 
        assert (pp.Length >= 1)
        pp.[0..pp.Length-2]


/// The information ILXGEN needs about the location of an item
type CompilationPath = 
    | CompPath of ILScopeRef * (string * ModuleOrNamespaceKind) list

    member x.ILScopeRef = (let (CompPath(scoref, _)) = x in scoref)

    member x.AccessPath = (let (CompPath(_, p)) = x in p)

    member x.MangledPath = List.map fst x.AccessPath

    member x.NestedPublicPath (id: Ident) = PubPath(Array.append (Array.ofList x.MangledPath) [| id.idText |])

    member x.ParentCompPath = 
        let a, _ = List.frontAndBack x.AccessPath
        CompPath(x.ILScopeRef, a)

    member x.NestedCompPath n modKind = CompPath(x.ILScopeRef, x.AccessPath@[(n, modKind)])

    member x.DemangledPath = 
        x.AccessPath |> List.map (fun (nm, k) -> CompilationPath.DemangleEntityName nm k)

    /// String 'Module' off an F# module name, if FSharpModuleWithSuffix is used
    static member DemangleEntityName nm k =  
        match k with 
        | FSharpModuleWithSuffix -> String.dropSuffix nm FSharpModuleSuffix
        | _ -> nm

[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type EntityOptionalData =
    {
      /// The name of the type, possibly with `n mangling 
      // MUTABILITY; used only when establishing tycons. 
      mutable entity_compiled_name: string option

      // MUTABILITY: the signature is adjusted when it is checked
      /// If this field is populated, this is the implementation range for an item in a signature, otherwise it is 
      /// the signature range for an item in an implementation
      mutable entity_other_range: (range * bool) option

      // MUTABILITY; used only when establishing tycons. 
      mutable entity_kind: TyparKind

      /// The declared documentation for the type or module 
      // MUTABILITY: only for unpickle linkage
      mutable entity_xmldoc: XmlDoc
      
      /// The XML document signature for this entity
      mutable entity_xmldocsig: string

      /// If non-None, indicates the type is an abbreviation for another type. 
      //
      // MUTABILITY; used only during creation and remapping of tycons 
      mutable entity_tycon_abbrev: TType option             

      /// The declared accessibility of the representation, not taking signatures into account 
      mutable entity_tycon_repr_accessibility: Accessibility

      /// Indicates how visible is the entity is.
      // MUTABILITY: only for unpickle linkage
      mutable entity_accessibility: Accessibility   

      /// Field used when the 'tycon' is really an exception definition
      // 
      // MUTABILITY; used only during creation and remapping of tycons 
      mutable entity_exn_info: ExceptionInfo     
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "EntityOptionalData(...)"

/// Represents a type definition, exception definition, module definition or namespace definition.
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>] 
type Entity = 
    {
      /// The declared type parameters of the type  
      // MUTABILITY; used only during creation and remapping of tycons 
      mutable entity_typars: LazyWithContext<Typars, range>        
      
      mutable entity_flags: EntityFlags
      
      /// The unique stamp of the "tycon blob". Note the same tycon in signature and implementation get different stamps 
      // MUTABILITY: only for unpickle linkage
      mutable entity_stamp: Stamp

      /// The name of the type, possibly with `n mangling 
      // MUTABILITY: only for unpickle linkage
      mutable entity_logical_name: string

      /// The declaration location for the type constructor 
      mutable entity_range: range
      
      /// The declared attributes for the type 
      // MUTABILITY; used during creation and remapping of tycons 
      // MUTABILITY; used when propagating signature attributes into the implementation.
      mutable entity_attribs: Attribs     
                
      /// The declared representation of the type, i.e. record, union, class etc. 
      //
      // MUTABILITY; used only during creation and remapping of tycons 
      mutable entity_tycon_repr: TyconRepresentation 
      
      /// The methods and properties of the type 
      //
      // MUTABILITY; used only during creation and remapping of tycons 
      mutable entity_tycon_tcaug: TyconAugmentation
      
      /// This field is used when the 'tycon' is really a module definition. It holds statically nested type definitions and nested modules 
      //
      // MUTABILITY: only used during creation and remapping of tycons and 
      // when compiling fslib to fixup compiler forward references to internal items 
      mutable entity_modul_contents: MaybeLazy<ModuleOrNamespaceType>     

      /// The stable path to the type, e.g. Microsoft.FSharp.Core.FSharpFunc`2 
      // REVIEW: it looks like entity_cpath subsumes this 
      // MUTABILITY: only for unpickle linkage
      mutable entity_pubpath: PublicPath option 
 
      /// The stable path to the type, e.g. Microsoft.FSharp.Core.FSharpFunc`2 
      // MUTABILITY: only for unpickle linkage
      mutable entity_cpath: CompilationPath option 

      /// Used during codegen to hold the ILX representation indicating how to access the type 
      // MUTABILITY: only for unpickle linkage and caching
      mutable entity_il_repr_cache: CompiledTypeRepr cache

      mutable entity_opt_data: EntityOptionalData option
    }

    static member NewEmptyEntityOptData() = 
        { entity_compiled_name = None 
          entity_other_range = None
          entity_kind = TyparKind.Type
          entity_xmldoc = XmlDoc.Empty 
          entity_xmldocsig = ""
          entity_tycon_abbrev = None
          entity_tycon_repr_accessibility = TAccess []
          entity_accessibility = TAccess []
          entity_exn_info = TExnNone }

    /// The name of the namespace, module or type, possibly with mangling, e.g. List`1, List or FailureException 
    member x.LogicalName = x.entity_logical_name

    /// The compiled name of the namespace, module or type, e.g. FSharpList`1, ListModule or FailureException 
    member x.CompiledName = 
        match x.entity_opt_data with 
        | Some { entity_compiled_name = Some s } -> s
        | _ -> x.LogicalName 

    member x.EntityCompiledName =
        match x.entity_opt_data with 
        | Some optData -> optData.entity_compiled_name
        | _ -> None 

    member x.SetCompiledName name =
        match x.entity_opt_data with
        | Some optData -> optData.entity_compiled_name <- name
        | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_compiled_name = name }

    /// The display name of the namespace, module or type, e.g. List instead of List`1, and no static parameters
    member x.DisplayName = x.GetDisplayName()

    /// The display name of the namespace, module or type with <'T, 'U, 'V> added for generic types, plus static parameters if any
    member x.DisplayNameWithStaticParametersAndTypars = x.GetDisplayName(withStaticParameters=true, withTypars=true, withUnderscoreTypars=false)

    /// The display name of the namespace, module or type with <_, _, _> added for generic types, plus static parameters if any
    member x.DisplayNameWithStaticParametersAndUnderscoreTypars = x.GetDisplayName(withStaticParameters=true, withTypars=false, withUnderscoreTypars=true)

    /// The display name of the namespace, module or type, e.g. List instead of List`1, including static parameters if any
    member x.DisplayNameWithStaticParameters = x.GetDisplayName(withStaticParameters=true, withTypars=false, withUnderscoreTypars=false)

#if !NO_EXTENSIONTYPING
    member x.IsStaticInstantiationTycon = 
        x.IsProvidedErasedTycon &&
            let _nm, args = PrettyNaming.demangleProvidedTypeName x.LogicalName
            args.Length > 0 
#endif

    member x.GetDisplayName(?withStaticParameters, ?withTypars, ?withUnderscoreTypars) =
        let withStaticParameters = defaultArg withStaticParameters false
        let withTypars = defaultArg withTypars false
        let withUnderscoreTypars = defaultArg withUnderscoreTypars false
        let nm = x.LogicalName

        let getName () =
            match x.TyparsNoRange with 
            | [] -> nm
            | tps -> 
                let nm = DemangleGenericTypeName nm
                if (withUnderscoreTypars || withTypars) && not (List.isEmpty tps) then
                    let typearNames = tps |> List.map (fun typar -> if withUnderscoreTypars then "_" else typar.Name)
                    nm + "<" + String.concat "," typearNames + ">"
                else
                    nm

#if !NO_EXTENSIONTYPING
        if x.IsProvidedErasedTycon then 
            let nm, args = PrettyNaming.demangleProvidedTypeName nm
            if withStaticParameters && args.Length > 0 then 
                nm + "<" + String.concat "," (Array.map snd args) + ">"
            else
                nm
        else
            getName ()
#else
        ignore withStaticParameters
        getName ()
#endif


    /// The code location where the module, namespace or type is defined.
    member x.Range = 
#if !NO_EXTENSIONTYPING    
        match x.TypeReprInfo with
        | TProvidedTypeExtensionPoint info ->
            match Construct.ComputeDefinitionLocationOfProvidedItem info.ProvidedType with
            | Some range -> range
            | None -> x.entity_range
        | _ -> 
#endif
        x.entity_range

    /// The range in the implementation, adjusted for an item in a signature
    member x.DefinitionRange = 
        match x.entity_opt_data with 
        | Some { entity_other_range = Some (r, true) } -> r
        | _ -> x.Range

    member x.SigRange = 
        match x.entity_opt_data with 
        | Some { entity_other_range = Some (r, false) } -> r
        | _ -> x.Range

    member x.SetOtherRange m = 
        match x.entity_opt_data with 
        | Some optData -> optData.entity_other_range <- Some m
        | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_other_range = Some m }

    /// A unique stamp for this module, namespace or type definition within the context of this compilation. 
    /// Note that because of signatures, there are situations where in a single compilation the "same" 
    /// module, namespace or type may have two distinct Entity objects that have distinct stamps.
    member x.Stamp = x.entity_stamp

    /// The F#-defined custom attributes of the entity, if any. If the entity is backed by Abstract IL or provided metadata
    /// then this does not include any attributes from those sources.
    member x.Attribs = x.entity_attribs

    /// The XML documentation of the entity, if any. If the entity is backed by provided metadata
    /// then this _does_ include this documentation. If the entity is backed by Abstract IL metadata
    /// or comes from another F# assembly then it does not (because the documentation will get read from 
    /// an XML file).
    member x.XmlDoc = 
#if !NO_EXTENSIONTYPING
        match x.TypeReprInfo with
        | TProvidedTypeExtensionPoint info ->
            let lines = info.ProvidedType.PUntaintNoFailure(fun st -> (st :> IProvidedCustomAttributeProvider).GetXmlDocAttributes(info.ProvidedType.TypeProvider.PUntaintNoFailure id))
            XmlDoc (lines, x.DefinitionRange)
        | _ -> 
#endif
        match x.entity_opt_data with
        | Some optData -> optData.entity_xmldoc
        | _ -> XmlDoc.Empty

    /// The XML documentation sig-string of the entity, if any, to use to lookup an .xml doc file. This also acts
    /// as a cache for this sig-string computation.
    member x.XmlDocSig 
        with get() = 
            match x.entity_opt_data with
            | Some optData -> optData.entity_xmldocsig
            | _ -> ""
        and set v =
            match x.entity_opt_data with
            | Some optData -> optData.entity_xmldocsig <- v
            | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_xmldocsig = v }

    /// The logical contents of the entity when it is a module or namespace fragment.
    member x.ModuleOrNamespaceType = x.entity_modul_contents.Force()

    /// The logical contents of the entity when it is a type definition.
    member x.TypeContents = x.entity_tycon_tcaug

    /// The kind of the type definition - is it a measure definition or a type definition?
    member x.TypeOrMeasureKind =
        match x.entity_opt_data with
        | Some optData -> optData.entity_kind
        | _ -> TyparKind.Type

    member x.SetTypeOrMeasureKind kind =
        match x.entity_opt_data with
        | Some optData -> optData.entity_kind <- kind
        | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_kind = kind }

    /// The identifier at the point of declaration of the type definition.
    member x.Id = ident(x.LogicalName, x.Range)

    /// The information about the r.h.s. of a type definition, if any. For example, the r.h.s. of a union or record type.
    member x.TypeReprInfo = x.entity_tycon_repr

    /// The information about the r.h.s. of an F# exception definition, if any. 
    member x.ExceptionInfo =
        match x.entity_opt_data with
        | Some optData -> optData.entity_exn_info
        | _ -> TExnNone

    member x.SetExceptionInfo exn_info =
        match x.entity_opt_data with
        | Some optData -> optData.entity_exn_info <- exn_info
        | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_exn_info = exn_info }

    /// Indicates if the entity represents an F# exception declaration.
    member x.IsExceptionDecl = match x.ExceptionInfo with TExnNone -> false | _ -> true

    /// Demangle the module name, if FSharpModuleWithSuffix is used
    member x.DemangledModuleOrNamespaceName =  
          CompilationPath.DemangleEntityName x.LogicalName x.ModuleOrNamespaceType.ModuleOrNamespaceKind
    
    /// Get the type parameters for an entity that is a type declaration, otherwise return the empty list.
    /// 
    /// Lazy because it may read metadata, must provide a context "range" in case error occurs reading metadata.
    member x.Typars m = x.entity_typars.Force m

    /// Get the type parameters for an entity that is a type declaration, otherwise return the empty list.
    member x.TyparsNoRange: Typars = x.Typars x.Range

    /// Get the type abbreviated by this type definition, if it is an F# type abbreviation definition
    member x.TypeAbbrev = 
        match x.entity_opt_data with
        | Some optData -> optData.entity_tycon_abbrev
        | _ -> None

    member x.SetTypeAbbrev tycon_abbrev = 
        match x.entity_opt_data with
        | Some optData -> optData.entity_tycon_abbrev <- tycon_abbrev
        | _ -> x.entity_opt_data <- Some { Entity.NewEmptyEntityOptData() with entity_tycon_abbrev = tycon_abbrev }

    /// Indicates if this entity is an F# type abbreviation definition
    member x.IsTypeAbbrev = x.TypeAbbrev.IsSome

    /// Get the value representing the accessibility of the r.h.s. of an F# type definition.
    member x.TypeReprAccessibility =
        match x.entity_opt_data with
        | Some optData -> optData.entity_tycon_repr_accessibility
        | _ -> TAccess []

    /// Get the cache of the compiled ILTypeRef representation of this module or type.
    member x.CompiledReprCache = x.entity_il_repr_cache

    /// Get a blob of data indicating how this type is nested in other namespaces, modules or types.
    member x.PublicPath = x.entity_pubpath

    /// Get the value representing the accessibility of an F# type definition or module.
    member x.Accessibility =
        match x.entity_opt_data with
        | Some optData -> optData.entity_accessibility
        | _ -> TAccess []

    /// Indicates the type prefers the "tycon<a, b>" syntax for display etc. 
    member x.IsPrefixDisplay = x.entity_flags.IsPrefixDisplay

    /// Indicates the Entity is actually a module or namespace, not a type definition
    member x.IsModuleOrNamespace = x.entity_flags.IsModuleOrNamespace

    /// Indicates if the entity is a namespace
    member x.IsNamespace = x.IsModuleOrNamespace && (match x.ModuleOrNamespaceType.ModuleOrNamespaceKind with Namespace -> true | _ -> false)

    /// Indicates if the entity is an F# module definition
    member x.IsModule = x.IsModuleOrNamespace && (match x.ModuleOrNamespaceType.ModuleOrNamespaceKind with Namespace -> false | _ -> true)
#if !NO_EXTENSIONTYPING

    /// Indicates if the entity is a provided type or namespace definition
    member x.IsProvided = 
        match x.TypeReprInfo with 
        | TProvidedTypeExtensionPoint _ -> true
        | TProvidedNamespaceExtensionPoint _ -> true
        | _ -> false

    /// Indicates if the entity is a provided namespace fragment
    member x.IsProvidedNamespace = 
        match x.TypeReprInfo with 
        | TProvidedNamespaceExtensionPoint _ -> true
        | _ -> false

    /// Indicates if the entity is an erased provided type definition
    member x.IsProvidedErasedTycon = 
        match x.TypeReprInfo with 
        | TProvidedTypeExtensionPoint info -> info.IsErased
        | _ -> false

    /// Indicates if the entity is a generated provided type definition, i.e. not erased.
    member x.IsProvidedGeneratedTycon = 
        match x.TypeReprInfo with 
        | TProvidedTypeExtensionPoint info -> info.IsGenerated
        | _ -> false
#endif

    /// Indicates if the entity is erased, either a measure definition, or an erased provided type definition
    member x.IsErased = 
        x.IsMeasureableReprTycon 
#if !NO_EXTENSIONTYPING
        || x.IsProvidedErasedTycon
#endif

    /// Get a blob of data indicating how this type is nested inside other namespaces, modules and types.
    member x.CompilationPathOpt = x.entity_cpath 

    /// Get a blob of data indicating how this type is nested inside other namespaces, modules and types.
    member x.CompilationPath = 
        match x.CompilationPathOpt with 
        | Some cpath -> cpath 
        | None -> error(Error(FSComp.SR.tastTypeOrModuleNotConcrete(x.LogicalName), x.Range))
    
    /// Get a table of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldTable = 
        match x.TypeReprInfo with 
        | TRecdRepr x | TFSharpObjectRepr {fsobjmodel_rfields=x} -> x
        | _ -> 
        match x.ExceptionInfo with 
        | TExnFresh x -> x
        | _ -> 
        { FieldsByIndex = [| |] 
          FieldsByName = NameMap.empty }

    /// Get an array of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldsArray = x.AllFieldTable.FieldsByIndex

    /// Get a list of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldsAsList = x.AllFieldsArray |> Array.toList

    /// Get a list of all instance fields for F#-defined record, struct and class fields in this type definition.
    /// including hidden fields from the compilation of implicit class constructions.
    // NOTE: This method doesn't perform particularly well, and is over-used, but doesn't seem to appear on performance traces
    member x.AllInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic)

    /// Get a list of all fields for F#-defined record, struct and class fields in this type definition,
    /// including static fields, but excluding compiler-generate fields.
    member x.TrueFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsCompilerGenerated)

    /// Get a list of all instance fields for F#-defined record, struct and class fields in this type definition,
    /// excluding compiler-generate fields.
    member x.TrueInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic && not f.IsCompilerGenerated)

    /// Get a field by index in definition order
    member x.GetFieldByIndex n = x.AllFieldTable.FieldByIndex n

    /// Get a field by name.
    member x.GetFieldByName n = x.AllFieldTable.FieldByName n

    /// Indicate if this is a type whose r.h.s. is known to be a union type definition.
    member x.IsUnionTycon = match x.TypeReprInfo with | TUnionRepr _ -> true | _ -> false

    /// Get the union cases and other union-type information for a type, if any
    member x.UnionTypeInfo = 
        match x.TypeReprInfo with 
        | TUnionRepr x -> ValueSome x 
        | _ -> ValueNone

    /// Get the union cases for a type, if any
    member x.UnionCasesArray = 
        match x.UnionTypeInfo with 
        | ValueSome x -> x.CasesTable.CasesByIndex 
        | ValueNone -> [| |] 

    /// Get the union cases for a type, if any, as a list
    member x.UnionCasesAsList = x.UnionCasesArray |> Array.toList

    /// Get a union case of a type by name
    member x.GetUnionCaseByName n =
        match x.UnionTypeInfo with 
        | ValueSome x -> NameMap.tryFind n x.CasesTable.CasesByName
        | ValueNone -> None

    
    /// Create a new entity with empty, unlinked data. Only used during unpickling of F# metadata.
    static member NewUnlinked() : Entity = 
        { entity_typars = Unchecked.defaultof<_>
          entity_flags = Unchecked.defaultof<_>
          entity_stamp = Unchecked.defaultof<_>
          entity_logical_name = Unchecked.defaultof<_> 
          entity_range = Unchecked.defaultof<_> 
          entity_attribs = Unchecked.defaultof<_>
          entity_tycon_repr= Unchecked.defaultof<_>
          entity_tycon_tcaug= Unchecked.defaultof<_>
          entity_modul_contents= Unchecked.defaultof<_>
          entity_pubpath = Unchecked.defaultof<_>
          entity_cpath = Unchecked.defaultof<_>
          entity_il_repr_cache = Unchecked.defaultof<_>
          entity_opt_data = Unchecked.defaultof<_>}

    /// Create a new entity with the given backing data. Only used during unpickling of F# metadata.
    static member New _reason (data: Entity) : Entity = data

    /// Link an entity based on empty, unlinked data to the given data. Only used during unpickling of F# metadata.
    member x.Link (tg: EntityData) = 
        x.entity_typars <- tg.entity_typars 
        x.entity_flags <- tg.entity_flags 
        x.entity_stamp <- tg.entity_stamp 
        x.entity_logical_name <- tg.entity_logical_name  
        x.entity_range <- tg.entity_range  
        x.entity_attribs <- tg.entity_attribs 
        x.entity_tycon_repr <- tg.entity_tycon_repr
        x.entity_tycon_tcaug <- tg.entity_tycon_tcaug
        x.entity_modul_contents <- tg.entity_modul_contents
        x.entity_pubpath <- tg.entity_pubpath 
        x.entity_cpath <- tg.entity_cpath 
        x.entity_il_repr_cache <- tg.entity_il_repr_cache 
        match tg.entity_opt_data with
        | Some tg ->
            x.entity_opt_data <- 
                Some { entity_compiled_name = tg.entity_compiled_name
                       entity_other_range = tg.entity_other_range
                       entity_kind = tg.entity_kind
                       entity_xmldoc = tg.entity_xmldoc
                       entity_xmldocsig = tg.entity_xmldocsig
                       entity_tycon_abbrev = tg.entity_tycon_abbrev
                       entity_tycon_repr_accessibility = tg.entity_tycon_repr_accessibility
                       entity_accessibility = tg.entity_accessibility
                       entity_exn_info = tg.entity_exn_info }
        | None -> ()


    /// Indicates if the entity is linked to backing data. Only used during unpickling of F# metadata.
    member x.IsLinked = match box x.entity_attribs with null -> false | _ -> true 

    /// Get the blob of information associated with an F# object-model type definition, i.e. class, interface, struct etc.
    member x.FSharpObjectModelTypeInfo = 
         match x.TypeReprInfo with 
         | TFSharpObjectRepr x -> x 
         | _ -> failwith "not an F# object model type definition"

    /// Indicate if this is a type definition backed by Abstract IL metadata.
    member x.IsILTycon = match x.TypeReprInfo with | TILObjectRepr _ -> true | _ -> false

    /// Get the Abstract IL scope, nesting and metadata for this 
    /// type definition, assuming it is backed by Abstract IL metadata.
    member x.ILTyconInfo = match x.TypeReprInfo with | TILObjectRepr data -> data | _ -> failwith "not a .NET type definition"

    /// Get the Abstract IL metadata for this type definition, assuming it is backed by Abstract IL metadata.
    member x.ILTyconRawMetadata = let (TILObjectReprData(_, _, td)) = x.ILTyconInfo in td

    /// Indicates if this is an F# type definition whose r.h.s. is known to be a record type definition.
    member x.IsRecordTycon = match x.TypeReprInfo with | TRecdRepr _ -> true | _ -> false

    /// Indicates if this is an F# type definition whose r.h.s. is known to be a record type definition that is a value type.
    member x.IsStructRecordOrUnionTycon = match x.TypeReprInfo with TRecdRepr _ | TUnionRepr _ -> x.entity_flags.IsStructRecordOrUnionType | _ -> false

    /// The on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.TryIsByRefLike = x.entity_flags.TryIsByRefLike

    /// Set the on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.SetIsByRefLike b = x.entity_flags <- x.entity_flags.WithIsByRefLike b 

    /// These two bits represents the on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.TryIsReadOnly = x.entity_flags.TryIsReadOnly

    /// Set the on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.SetIsReadOnly b = x.entity_flags <- x.entity_flags.WithIsReadOnly b 

    /// These two bits represents the on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.TryIsAssumedReadOnly = x.entity_flags.TryIsAssumedReadOnly

    /// Set the on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.SetIsAssumedReadOnly b = x.entity_flags <- x.entity_flags.WithIsAssumedReadOnly b 

    /// Indicates if this is an F# type definition whose r.h.s. is known to be some kind of F# object model definition
    member x.IsFSharpObjectModelTycon = match x.TypeReprInfo with | TFSharpObjectRepr _ -> true | _ -> false

    /// Indicates if this is an F# type definition which is one of the special types in FSharp.Core.dll which uses 
    /// an assembly-code representation for the type, e.g. the primitive array type constructor.
    member x.IsAsmReprTycon = match x.TypeReprInfo with | TAsmRepr _ -> true | _ -> false

    /// Indicates if this is an F# type definition which is one of the special types in FSharp.Core.dll like 'float<_>' which
    /// defines a measure type with a relation to an existing non-measure type as a representation.
    member x.IsMeasureableReprTycon = match x.TypeReprInfo with | TMeasureableRepr _ -> true | _ -> false

    /// Indicates if this is an F# type definition whose r.h.s. definition is unknown (i.e. a traditional ML 'abstract' type in a signature,
    /// which in F# is called a 'unknown representation' type).
    member x.IsHiddenReprTycon = match x.TypeAbbrev, x.TypeReprInfo with | None, TNoRepr -> true | _ -> false

    /// Indicates if this is an F#-defined interface type definition 
    member x.IsFSharpInterfaceTycon = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconInterface -> true | _ -> false

    /// Indicates if this is an F#-defined delegate type definition 
    member x.IsFSharpDelegateTycon = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconDelegate _ -> true | _ -> false

    /// Indicates if this is an F#-defined enum type definition 
    member x.IsFSharpEnumTycon = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconEnum -> true | _ -> false

    /// Indicates if this is an F#-defined class type definition 
    member x.IsFSharpClassTycon = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconClass -> true | _ -> false

    /// Indicates if this is a .NET-defined enum type definition 
    member x.IsILEnumTycon = x.IsILTycon && x.ILTyconRawMetadata.IsEnum

    /// Indicates if this is an enum type definition 
    member x.IsEnumTycon = 
#if !NO_EXTENSIONTYPING
        match x.TypeReprInfo with 
        | TProvidedTypeExtensionPoint info -> info.IsEnum 
        | TProvidedNamespaceExtensionPoint _ -> false
        | _ ->
#endif
        x.IsILEnumTycon || x.IsFSharpEnumTycon


    /// Indicates if this is an F#-defined struct or enum type definition, i.e. a value type definition
    member x.IsFSharpStructOrEnumTycon =
        match x.TypeReprInfo with
        | TRecdRepr _ -> x.IsStructRecordOrUnionTycon
        | TUnionRepr _ -> x.IsStructRecordOrUnionTycon
        | TFSharpObjectRepr info ->
            match info.fsobjmodel_kind with
            | TTyconClass | TTyconInterface | TTyconDelegate _ -> false
            | TTyconStruct | TTyconEnum -> true
        | _ -> false

    /// Indicates if this is a .NET-defined struct or enum type definition, i.e. a value type definition
    member x.IsILStructOrEnumTycon =
        x.IsILTycon && 
        x.ILTyconRawMetadata.IsStructOrEnum

    /// Indicates if this is a struct or enum type definition, i.e. a value type definition
    member x.IsStructOrEnumTycon = 
#if !NO_EXTENSIONTYPING
        match x.TypeReprInfo with 
        | TProvidedTypeExtensionPoint info -> info.IsStructOrEnum 
        | TProvidedNamespaceExtensionPoint _ -> false
        | _ ->
#endif
        x.IsILStructOrEnumTycon || x.IsFSharpStructOrEnumTycon

    /// Gets the immediate interface definitions of an F# type definition. Further interfaces may be supported through class and interface inheritance.
    member x.ImmediateInterfacesOfFSharpTycon =
        x.TypeContents.tcaug_interfaces

    /// Gets the immediate interface types of an F# type definition. Further interfaces may be supported through class and interface inheritance.
    member x.ImmediateInterfaceTypesOfFSharpTycon =
        x.ImmediateInterfacesOfFSharpTycon |> List.map (fun (x, _, _) -> x)

    /// Gets the immediate members of an F# type definition, excluding compiler-generated ones.
    /// Note: result is alphabetically sorted, then for each name the results are in declaration order
    member x.MembersOfFSharpTyconSorted =
        x.TypeContents.tcaug_adhoc 
        |> NameMultiMap.rangeReversingEachBucket 
        |> List.filter (fun v -> not v.IsCompilerGenerated)

    /// Gets all immediate members of an F# type definition keyed by name, including compiler-generated ones.
    /// Note: result is a indexed table, and for each name the results are in reverse declaration order
    member x.MembersOfFSharpTyconByName =
        x.TypeContents.tcaug_adhoc 

    /// Gets any implicit hash/equals (with comparer argument) methods added to an F# record, union or struct type definition.
    member x.GeneratedHashAndEqualsWithComparerValues = x.TypeContents.tcaug_hash_and_equals_withc 

    /// Gets any implicit CompareTo (with comparer argument) methods added to an F# record, union or struct type definition.
    member x.GeneratedCompareToWithComparerValues = x.TypeContents.tcaug_compare_withc

    /// Gets any implicit CompareTo methods added to an F# record, union or struct type definition.
    member x.GeneratedCompareToValues = x.TypeContents.tcaug_compare

    /// Gets any implicit hash/equals methods added to an F# record, union or struct type definition.
    member x.GeneratedHashAndEqualsValues = x.TypeContents.tcaug_equals

    /// Gets all implicit hash/equals/compare methods added to an F# record, union or struct type definition.
    member x.AllGeneratedValues = 
        [ match x.GeneratedCompareToValues with 
          | None -> ()
          | Some (v1, v2) -> yield v1; yield v2
          match x.GeneratedCompareToWithComparerValues with
          | None -> ()
          | Some v -> yield v
          match x.GeneratedHashAndEqualsValues with
          | None -> ()
          | Some (v1, v2) -> yield v1; yield v2
          match x.GeneratedHashAndEqualsWithComparerValues with
          | None -> ()
          | Some (v1, v2, v3) -> yield v1; yield v2; yield v3 ]
    

    /// Gets the data indicating the compiled representation of a type or module in terms of Abstract IL data structures.
    member x.CompiledRepresentation =
#if !NO_EXTENSIONTYPING
        match x.TypeReprInfo with 
        // We should never be computing this property for erased types
        | TProvidedTypeExtensionPoint info when info.IsErased -> 
            failwith "No compiled representation for provided erased type"
        
        // Generated types that are not relocated just point straight to the generated backing assembly, computed from "st".
        // These are used when running F# Interactive, which does not use static linking of provider-generated assemblies,
        // and also for types with relocation suppressed.
        | TProvidedTypeExtensionPoint info when info.IsGenerated && info.IsSuppressRelocate -> 
            let st = info.ProvidedType
            let tref = ExtensionTyping.GetILTypeRefOfProvidedType (st, x.Range)
            let boxity = if x.IsStructOrEnumTycon then AsValue else AsObject
            CompiledTypeRepr.ILAsmNamed(tref, boxity, None)
        | TProvidedNamespaceExtensionPoint _ -> failwith "No compiled representation for provided namespace"
        | _ ->
#endif
            let ilTypeRefForCompilationPath (CompPath(sref, p)) item = 
                let rec top racc p = 
                    match p with 
                    | [] -> ILTypeRef.Create(sref, [], textOfPath (List.rev (item :: racc)))
                    | (h, isType) :: t -> 
                        match isType with 
                        | FSharpModuleWithSuffix | ModuleOrType -> 
                            let outerTypeName = (textOfPath (List.rev (h :: racc)))
                            ILTypeRef.Create(sref, (outerTypeName :: List.map (fun (nm, _) -> nm) t), item)
                        | _ -> 
                          top (h :: racc) t
                top [] p 


            cached x.CompiledReprCache (fun () -> 
                match x.ExceptionInfo with 
                | TExnAbbrevRepr ecref2 -> ecref2.CompiledRepresentation
                | TExnAsmRepr tref -> CompiledTypeRepr.ILAsmNamed(tref, AsObject, Some (mkILTy AsObject (mkILTySpec (tref, []))))
                | _ -> 
                match x.TypeReprInfo with 
                | TAsmRepr ty -> CompiledTypeRepr.ILAsmOpen ty
                | _ -> 
                    let boxity = if x.IsStructOrEnumTycon then AsValue else AsObject
                    let ilTypeRef = 
                        match x.TypeReprInfo with 
                        | TILObjectRepr (TILObjectReprData(ilScopeRef, ilEnclosingTypeDefs, ilTypeDef)) -> IL.mkRefForNestedILTypeDef ilScopeRef (ilEnclosingTypeDefs, ilTypeDef)
                        | _ -> ilTypeRefForCompilationPath x.CompilationPath x.CompiledName
                    // Pre-allocate a ILType for monomorphic types, to reduce memory usage from Abstract IL nodes
                    let ilTypeOpt = 
                        match x.TyparsNoRange with 
                        | [] -> Some (mkILTy boxity (mkILTySpec (ilTypeRef, []))) 
                        | _ -> None
                    CompiledTypeRepr.ILAsmNamed (ilTypeRef, boxity, ilTypeOpt))

    /// Gets the data indicating the compiled representation of a named type or module in terms of Abstract IL data structures.
    member x.CompiledRepresentationForNamedType =
        match x.CompiledRepresentation with 
        | CompiledTypeRepr.ILAsmNamed(tref, _, _) -> tref
        | CompiledTypeRepr.ILAsmOpen _ -> invalidOp (FSComp.SR.tastTypeHasAssemblyCodeRepresentation(x.DisplayNameWithStaticParametersAndUnderscoreTypars))


    /// Indicates if we have pre-determined that a type definition has a default constructor.
    member x.PreEstablishedHasDefaultConstructor = x.entity_flags.PreEstablishedHasDefaultConstructor

    /// Indicates if we have pre-determined that a type definition has a self-referential constructor using 'as x'
    member x.HasSelfReferentialConstructor = x.entity_flags.HasSelfReferentialConstructor

    /// Set the custom attributes on an F# type definition.
    member x.SetAttribs attribs = x.entity_attribs <- attribs

    /// Sets the structness of a record or union type definition
    member x.SetIsStructRecordOrUnion b = let flags = x.entity_flags in x.entity_flags <- EntityFlags(flags.IsPrefixDisplay, flags.IsModuleOrNamespace, flags.PreEstablishedHasDefaultConstructor, flags.HasSelfReferentialConstructor, b)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.LogicalName

type EntityData = Entity

/// Represents the parent entity of a type definition, if any
type ParentRef = 
    | Parent of parent: EntityRef
    | ParentNone
    
/// Specifies the compiled representations of type and exception definitions. Basically
/// just an ILTypeRef. Computed and cached by later phases. Stored in 
/// type and exception definitions. Not pickled. Store an optional ILType object for 
/// non-generic types.
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type CompiledTypeRepr = 

    /// An AbstractIL type representation that is just the name of a type.
    ///
    /// CompiledTypeRepr.ILAsmNamed (ilTypeRef, ilBoxity, ilTypeOpt)
    /// 
    /// The ilTypeOpt is present for non-generic types. It is an ILType corresponding to the first two elements of the case. This
    /// prevents reallocation of the ILType each time we need to generate it. For generic types, it is None.
    | ILAsmNamed of 
         ilTypeRef: ILTypeRef * 
         ilBoxity: ILBoxity * 
         ilTypeOpt: ILType option
         
    /// An AbstractIL type representation that may include type variables
    // This case is only used for types defined in the F# library by their translation to ILASM types, e.g.
    //   type ``[]``<'T> = (# "!0[]" #)
    //   type ``[, ]``<'T> = (# "!0[0 ..., 0 ...]" #)
    //   type ``[, , ]``<'T> = (# "!0[0 ..., 0 ..., 0 ...]" #)
    //   type byref<'T> = (# "!0&" #)
    //   type nativeptr<'T when 'T: unmanaged> = (# "native int" #)
    //   type ilsigptr<'T> = (# "!0*" #)
    | ILAsmOpen of ilType: ILType  

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "CompiledTypeRepr(...)"

[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type TyconAugmentation = 
    {
      /// This is the value implementing the auto-generated comparison 
      /// semantics if any. It is not present if the type defines its own implementation 
      /// of IComparable or if the type doesn't implement IComparable implicitly. 
      mutable tcaug_compare: (ValRef * ValRef) option
      
      /// This is the value implementing the auto-generated comparison
      /// semantics if any. It is not present if the type defines its own implementation
      /// of IStructuralComparable or if the type doesn't implement IComparable implicitly.
      mutable tcaug_compare_withc: ValRef option                      

      /// This is the value implementing the auto-generated equality 
      /// semantics if any. It is not present if the type defines its own implementation 
      /// of Object.Equals or if the type doesn't override Object.Equals implicitly. 
      mutable tcaug_equals: (ValRef * ValRef) option

      /// This is the value implementing the auto-generated comparison
      /// semantics if any. It is not present if the type defines its own implementation
      /// of IStructuralEquatable or if the type doesn't implement IComparable implicitly.
      mutable tcaug_hash_and_equals_withc: (ValRef * ValRef * ValRef) option                                    

      /// True if the type defined an Object.GetHashCode method. In this 
      /// case we give a warning if we auto-generate a hash method since the semantics may not match up
      mutable tcaug_hasObjectGetHashCode: bool             
      
      /// Properties, methods etc. in declaration order. The boolean flag for each indicates if the
      /// member is known to be an explicit interface implementation. This must be computed and
      /// saved prior to remapping assembly information.
      tcaug_adhoc_list: ResizeArray<bool * ValRef> 
      
      /// Properties, methods etc. as lookup table
      mutable tcaug_adhoc: NameMultiMap<ValRef>
      
      /// Interface implementations - boolean indicates compiler-generated 
      mutable tcaug_interfaces: (TType * bool * range) list  
      
      /// Super type, if any 
      mutable tcaug_super: TType option                 
      
      /// Set to true at the end of the scope where proper augmentations are allowed 
      mutable tcaug_closed: bool                       

      /// Set to true if the type is determined to be abstract 
      mutable tcaug_abstract: bool                       
    }

    member tcaug.SetCompare x = tcaug.tcaug_compare <- Some x

    member tcaug.SetCompareWith x = tcaug.tcaug_compare_withc <- Some x

    member tcaug.SetEquals x = tcaug.tcaug_equals <- Some x

    member tcaug.SetHashAndEqualsWith x = tcaug.tcaug_hash_and_equals_withc <- Some x

    member tcaug.SetHasObjectGetHashCode b = tcaug.tcaug_hasObjectGetHashCode <- b

    static member Create() =
        { tcaug_compare=None 
          tcaug_compare_withc=None 
          tcaug_equals=None 
          tcaug_hash_and_equals_withc=None 
          tcaug_hasObjectGetHashCode=false 
          tcaug_adhoc=NameMultiMap.empty 
          tcaug_adhoc_list=new ResizeArray<_>() 
          tcaug_super=None
          tcaug_interfaces=[] 
          tcaug_closed=false 
          tcaug_abstract=false }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "SynTypeDefnKind.Augmentation(...)"

/// The information for the contents of a type. Also used for a provided namespace.
[<NoEquality; NoComparison (*; StructuredFormatDisplay("{DebugText}") *) >]
type TyconRepresentation = 

    /// Indicates the type is a class, struct, enum, delegate or interface 
    | TFSharpObjectRepr of TyconObjModelData

    /// Indicates the type is a record 
    | TRecdRepr of TyconRecdFields

    /// Indicates the type is a discriminated union 
    | TUnionRepr of TyconUnionData 

    /// Indicates the type is a type from a .NET assembly without F# metadata.
    | TILObjectRepr of TILObjectReprData

    /// Indicates the type is implemented as IL assembly code using the given closed Abstract IL type 
    | TAsmRepr of ILType

    /// Indicates the type is parameterized on a measure (e.g. float<_>) but erases to some other type (e.g. float)
    | TMeasureableRepr of TType

#if !NO_EXTENSIONTYPING
    /// TProvidedTypeExtensionPoint
    ///
    /// Indicates the representation information for a provided type. 
    | TProvidedTypeExtensionPoint of TProvidedTypeInfo

    /// Indicates the representation information for a provided namespace.  
    //
    // Note, the list could probably be a list of IProvidedNamespace rather than ITypeProvider
    | TProvidedNamespaceExtensionPoint of ExtensionTyping.ResolutionEnvironment * Tainted<ITypeProvider> list
#endif

    /// The 'NoRepr' value here has four meanings: 
    ///     (1) it indicates 'not yet known' during the first 2 phases of establishing type definitions
    ///     (2) it indicates 'no representation', i.e. 'type X' in signatures
    ///     (3) it is the setting used for exception definitions (!)
    ///     (4) it is the setting used for modules and namespaces.
    /// 
    /// It would be better to separate the "not yet known" and other cases out.
    /// The information for exception definitions should be folded into here.
    | TNoRepr

    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TILObjectReprData = 
    | TILObjectReprData of scope: ILScopeRef * nesting: ILTypeDef list * definition: ILTypeDef 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TILObjectReprData(...)"


#if !NO_EXTENSIONTYPING

/// The information kept about a provided type
[<NoComparison; NoEquality; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type TProvidedTypeInfo = 
    { 
      /// The parameters given to the provider that provided to this type.
      ResolutionEnvironment: ExtensionTyping.ResolutionEnvironment

      /// The underlying System.Type (wrapped as a ProvidedType to make sure we don't call random things on
      /// System.Type, and wrapped as Tainted to make sure we track which provider this came from, for reporting
      /// error messages)
      ProvidedType: Tainted<ProvidedType>

      /// The base type of the type. We use it to compute the compiled representation of the type for erased types.
      /// Reading is delayed, since it does an import on the underlying type
      LazyBaseType: LazyWithContext<TType, range * TType> 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsClass: bool 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsSealed: bool 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsAbstract:  bool 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsInterface:  bool 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsStructOrEnum: bool 

      /// A flag read eagerly from the provided type and used to compute basic properties of the type definition.
      IsEnum: bool 

      /// A type read from the provided type and used to compute basic properties of the type definition.
      /// Reading is delayed, since it does an import on the underlying type
      UnderlyingTypeOfEnum: (unit -> TType) 

      /// A flag read from the provided type and used to compute basic properties of the type definition.
      /// Reading is delayed, since it looks at the .BaseType
      IsDelegate: (unit -> bool) 

      /// Indicates the type is erased
      IsErased: bool 

      /// Indicates the type is generated, but type-relocation is suppressed
      IsSuppressRelocate: bool
    }

    /// Indicates if the provided type is generated, i.e. not erased
    member info.IsGenerated = not info.IsErased

    /// Gets the base type of an erased provided type
    member info.BaseTypeForErased (m, objTy) = 
       if info.IsErased then info.LazyBaseType.Force (m, objTy) 
       else failwith "expect erased type"

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "TProvidedTypeInfo(...)"

#endif

type TyconObjModelKind = 
    /// Indicates the type is a class (also used for units-of-measure)
    | TTyconClass 

    /// Indicates the type is an interface 
    | TTyconInterface 

    /// Indicates the type is a struct 
    | TTyconStruct 

    /// Indicates the type is a delegate with the given Invoke signature 
    | TTyconDelegate of slotSig: SlotSig 

    /// Indicates the type is an enumeration 
    | TTyconEnum
    
    /// Indicates if the type definition is a value type
    member x.IsValueType =
        match x with 
        | TTyconClass | TTyconInterface | TTyconDelegate _ -> false
        | TTyconStruct | TTyconEnum -> true

/// Represents member values and class fields relating to the F# object model
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TyconObjModelData = 
    { 
      /// Indicates whether the type declaration is a class, interface, enum, delegate or struct 
      fsobjmodel_kind: TyconObjModelKind

      /// The declared abstract slots of the class, interface or struct 
      fsobjmodel_vslots: ValRef list

      /// The fields of the class, struct or enum 
      fsobjmodel_rfields: TyconRecdFields
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TyconObjModelData(...)"

/// Represents record fields in an F# type definition
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type TyconRecdFields = 
    { 
      /// The fields of the record, in declaration order. 
      FieldsByIndex: RecdField[]
      
      /// The fields of the record, indexed by name. 
      FieldsByName: NameMap<RecdField>
    }

    /// Get a field by index
    member x.FieldByIndex n = 
        if n >= 0 && n < x.FieldsByIndex.Length then x.FieldsByIndex.[n] 
        else failwith "FieldByIndex"

    /// Get a field by name
    member x.FieldByName nm = x.FieldsByName.TryFind nm

    /// Get all the fields as a list
    member x.AllFieldsAsList = x.FieldsByIndex |> Array.toList

    /// Get all non-compiler-generated fields as a list
    member x.TrueFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsCompilerGenerated)   

    /// Get all non-compiler-generated instance fields as a list
    member x.TrueInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic && not f.IsCompilerGenerated)   

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TyconRecdFields(...)"

/// Represents union cases in an F# type definition
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type TyconUnionCases = 
    { 
      /// The cases of the discriminated union, in declaration order. 
      CasesByIndex: UnionCase[]

      /// The cases of the discriminated union, indexed by name. 
      CasesByName: NameMap<UnionCase>
    }

    /// Get a union case by index
    member x.GetUnionCaseByIndex n = 
        if n >= 0 && n < x.CasesByIndex.Length then x.CasesByIndex.[n] 
        else invalidArg "n" "GetUnionCaseByIndex"

    /// Get the union cases as a list
    member x.UnionCasesAsList = x.CasesByIndex |> Array.toList

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TyconUnionCases(...)"

/// Represents the union cases and related information in an F# type definition
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type TyconUnionData =
    {
      /// The cases contained in the discriminated union. 
      CasesTable: TyconUnionCases

      /// The ILX data structure representing the discriminated union. 
      CompiledRepresentation: IlxUnionRef cache 
    }

    /// Get the union cases as a list
    member x.UnionCasesAsList = x.CasesTable.CasesByIndex |> Array.toList

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TyconUnionData(...)"

/// Represents a union case in an F# type definition
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type UnionCase =
    { 
      /// Data carried by the case. 
      FieldTable: TyconRecdFields

      /// Return type constructed by the case. Normally exactly the type of the enclosing type, sometimes an abbreviation of it 
      ReturnType: TType

      /// Documentation for the case 
      XmlDoc: XmlDoc

      /// XML documentation signature for the case
      mutable XmlDocSig: string

      /// Name/range of the case 
      Id: Ident 

      /// If this field is populated, this is the implementation range for an item in a signature, otherwise it is 
      /// the signature range for an item in an implementation
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable OtherRangeOpt: (range * bool) option

      ///  Indicates the declared visibility of the union constructor, not taking signatures into account 
      Accessibility: Accessibility 

      /// Attributes, attached to the generated static method to make instances of the case 
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable Attribs: Attribs
    }

    /// Get the declaration location of the union case
    member uc.Range = uc.Id.idRange

    /// Get the definition location of the union case
    member uc.DefinitionRange = 
        match uc.OtherRangeOpt with 
        | Some (m, true) -> m
        | _ -> uc.Range 

    /// Get the signature location of the union case
    member uc.SigRange = 
        match uc.OtherRangeOpt with 
        | Some (m, false) -> m
        | _ -> uc.Range 

    /// Get the display name of the union case
    member uc.DisplayName = uc.Id.idText

    /// Get the name of the case in generated IL code.
    member uc.CompiledName =
        let idText = uc.Id.idText
        if idText = opNameCons then "Cons" 
        elif idText = opNameNil then "Empty"
        else idText

    /// Get the full array of fields of the union case
    member uc.RecdFieldsArray = uc.FieldTable.FieldsByIndex 

    /// Get the full list of fields of the union case
    member uc.RecdFields = uc.FieldTable.FieldsByIndex |> Array.toList

    /// Get a field of the union case by name
    member uc.GetFieldByName nm = uc.FieldTable.FieldByName nm

    /// Get a field of the union case by position
    member uc.GetFieldByIndex n = uc.FieldTable.FieldByIndex n

    /// Indicates if the union case has no fields
    member uc.IsNullary = (uc.FieldTable.FieldsByIndex.Length = 0)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "UnionCase(" + x.DisplayName + ")"

/// Represents a class, struct or record field in an F# type definition.
/// This may represent a "field" in either a struct, class, record or union.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type RecdField =
    {
      /// Is the field declared mutable in F#? 
      rfield_mutable: bool

      /// Documentation for the field 
      rfield_xmldoc: XmlDoc

      /// XML Documentation signature for the field
      mutable rfield_xmldocsig: string

      /// The type of the field, w.r.t. the generic parameters of the enclosing type constructor 
      rfield_type: TType

      /// Indicates a static field 
      rfield_static: bool

      /// Indicates a volatile field 
      rfield_volatile: bool

      /// Indicates a compiler generated field, not visible to Intellisense or name resolution 
      rfield_secret: bool

      /// The default initialization info, for static literals 
      rfield_const: Const option 

      ///  Indicates the declared visibility of the field, not taking signatures into account 
      rfield_access: Accessibility 

      /// Attributes attached to generated property 
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable rfield_pattribs: Attribs 

      /// Attributes attached to generated field 
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable rfield_fattribs: Attribs 

      /// Name/declaration-location of the field 
      rfield_id: Ident

      rfield_name_generated: bool

      /// If this field is populated, this is the implementation range for an item in a signature, otherwise it is 
      /// the signature range for an item in an implementation
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable rfield_other_range: (range * bool) option }

    ///  Indicates the declared visibility of the field, not taking signatures into account 
    member v.Accessibility = v.rfield_access

    /// Attributes attached to generated property 
    member v.PropertyAttribs = v.rfield_pattribs

    /// Attributes attached to generated field 
    member v.FieldAttribs = v.rfield_fattribs

    /// Get the declaration location of the field 
    member v.Range = v.rfield_id.idRange

    /// Get the definition location of the field 
    member v.DefinitionRange = 
        match v.rfield_other_range with 
        | Some (m, true) -> m
        | _ -> v.Range 

    /// Get the signature location of the field 
    member v.SigRange = 
        match v.rfield_other_range with 
        | Some (m, false) -> m
        | _ -> v.Range 

    /// Name/declaration-location of the field 
    member v.Id = v.rfield_id

    /// Name of the field 
    member v.Name = v.rfield_id.idText

      /// Indicates a compiler generated field, not visible to Intellisense or name resolution 
    member v.IsCompilerGenerated = v.rfield_secret

    /// Is the field declared mutable in F#? 
    member v.IsMutable = v.rfield_mutable

    /// Indicates a static field 
    member v.IsStatic = v.rfield_static

    /// Indicates a volatile field 
    member v.IsVolatile = v.rfield_volatile

    /// The type of the field, w.r.t. the generic parameters of the enclosing type constructor 
    member v.FormalType = v.rfield_type

    /// XML Documentation signature for the field
    member v.XmlDoc = v.rfield_xmldoc

    /// Get or set the XML documentation signature for the field
    member v.XmlDocSig
        with get() = v.rfield_xmldocsig
        and set x = v.rfield_xmldocsig <- x

    /// The default initialization info, for static literals 
    member v.LiteralValue = 
        match v.rfield_const with 
        | None -> None
        | Some Const.Zero -> None
        | Some k -> Some k

    /// Indicates if the field is zero-initialized
    member v.IsZeroInit = 
        match v.rfield_const with 
        | None -> false 
        | Some Const.Zero -> true 
        | _ -> false

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.Name

/// Represents the implementation of an F# exception definition.
[<NoEquality; NoComparison (*; StructuredFormatDisplay("{DebugText}") *) >]
type ExceptionInfo =

    /// Indicates that an exception is an abbreviation for the given exception 
    | TExnAbbrevRepr of TyconRef 

    /// Indicates that an exception is shorthand for the given .NET exception type 
    | TExnAsmRepr of ILTypeRef

    /// Indicates that an exception carries the given record of values 
    | TExnFresh of TyconRecdFields

    /// Indicates that an exception is abstract, i.e. is in a signature file, and we do not know the representation 
    | TExnNone

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// Represents the contents of of a module of namespace
[<Sealed; StructuredFormatDisplay("{DebugText}")>]
type ModuleOrNamespaceType(kind: ModuleOrNamespaceKind, vals: QueueList<Val>, entities: QueueList<Entity>) = 

    /// Mutation used during compilation of FSharp.Core.dll
    let mutable entities = entities 
      
    // Lookup tables keyed the way various clients expect them to be keyed.
    // We attach them here so we don't need to store lookup tables via any other technique.
    //
    // The type option ref is used because there are a few functions that treat these as first class values.
    // We should probably change to 'mutable'.
    //
    // We do not need to lock this mutable state this it is only ever accessed from the compiler thread.
    let activePatternElemRefCache: NameMap<ActivePatternElemRef> option ref = ref None

    let mutable modulesByDemangledNameCache: NameMap<ModuleOrNamespace> option = None

    let mutable exconsByDemangledNameCache: NameMap<Tycon> option = None

    let mutable tyconsByDemangledNameAndArityCache: LayeredMap<NameArityPair, Tycon> option = None

    let mutable tyconsByAccessNamesCache: LayeredMultiMap<string, Tycon> option = None

    let mutable tyconsByMangledNameCache: NameMap<Tycon> option = None

    let mutable allEntitiesByMangledNameCache: NameMap<Entity> option = None

    let mutable allValsAndMembersByPartialLinkageKeyCache: MultiMap<ValLinkagePartialKey, Val> option = None

    let mutable allValsByLogicalNameCache: NameMap<Val> option = None
  
    /// Namespace or module-compiled-as-type? 
    member _.ModuleOrNamespaceKind = kind 
              
    /// Values, including members in F# types in this module-or-namespace-fragment. 
    member _.AllValsAndMembers = vals

    /// Type, mapping mangled name to Tycon, e.g. 
    ////     "Dictionary`2" --> Tycon 
    ////     "ListModule" --> Tycon with module info
    ////     "FooException" --> Tycon with exception info
    member _.AllEntities = entities

    /// Mutation used during compilation of FSharp.Core.dll
    member _.AddModuleOrNamespaceByMutation(modul: ModuleOrNamespace) =
        entities <- QueueList.appendOne entities modul
        modulesByDemangledNameCache <- None          
        allEntitiesByMangledNameCache <- None       

#if !NO_EXTENSIONTYPING
    /// Mutation used in hosting scenarios to hold the hosted types in this module or namespace
    member mtyp.AddProvidedTypeEntity(entity: Entity) = 
        entities <- QueueList.appendOne entities entity
        tyconsByMangledNameCache <- None          
        tyconsByDemangledNameAndArityCache <- None
        tyconsByAccessNamesCache <- None
        allEntitiesByMangledNameCache <- None             
#endif 
          
    /// Return a new module or namespace type with an entity added.
    member _.AddEntity(tycon: Tycon) = 
        ModuleOrNamespaceType(kind, vals, entities.AppendOne tycon)
          
    /// Return a new module or namespace type with a value added.
    member _.AddVal(vspec: Val) = 
        ModuleOrNamespaceType(kind, vals.AppendOne vspec, entities)
          
    /// Get a table of the active patterns defined in this module.
    member _.ActivePatternElemRefLookupTable = activePatternElemRefCache
  
    /// Get a list of types defined within this module, namespace or type. 
    member _.TypeDefinitions = entities |> Seq.filter (fun x -> not x.IsExceptionDecl && not x.IsModuleOrNamespace) |> Seq.toList

    /// Get a list of F# exception definitions defined within this module, namespace or type. 
    member _.ExceptionDefinitions = entities |> Seq.filter (fun x -> x.IsExceptionDecl) |> Seq.toList

    /// Get a list of module and namespace definitions defined within this module, namespace or type. 
    member _.ModuleAndNamespaceDefinitions = entities |> Seq.filter (fun x -> x.IsModuleOrNamespace) |> Seq.toList

    /// Get a list of type and exception definitions defined within this module, namespace or type. 
    member _.TypeAndExceptionDefinitions = entities |> Seq.filter (fun x -> not x.IsModuleOrNamespace) |> Seq.toList

    /// Get a table of types defined within this module, namespace or type. The 
    /// table is indexed by both name and generic arity. This means that for generic 
    /// types "List`1", the entry (List, 1) will be present.
    member mtyp.TypesByDemangledNameAndArity = 
        cacheOptByref &tyconsByDemangledNameAndArityCache (fun () -> 
           LayeredMap.Empty.AddAndMarkAsCollapsible( mtyp.TypeAndExceptionDefinitions |> List.map (fun (tc: Tycon) -> Construct.KeyTyconByDecodedName tc.LogicalName tc) |> List.toArray))

    /// Get a table of types defined within this module, namespace or type. The 
    /// table is indexed by both name and, for generic types, also by mangled name.
    member mtyp.TypesByAccessNames = 
        cacheOptByref &tyconsByAccessNamesCache (fun () -> 
             LayeredMultiMap.Empty.AddAndMarkAsCollapsible (mtyp.TypeAndExceptionDefinitions |> List.toArray |> Array.collect (fun (tc: Tycon) -> Construct.KeyTyconByAccessNames tc.LogicalName tc)))

    // REVIEW: we can remove this lookup and use AllEntitiesByMangledName instead?
    member mtyp.TypesByMangledName = 
        let addTyconByMangledName (x: Tycon) tab = NameMap.add x.LogicalName x tab 
        cacheOptByref &tyconsByMangledNameCache (fun () -> 
             List.foldBack addTyconByMangledName mtyp.TypeAndExceptionDefinitions Map.empty)

    /// Get a table of entities indexed by both logical and compiled names
    member _.AllEntitiesByCompiledAndLogicalMangledNames: NameMap<Entity> = 
        let addEntityByMangledName (x: Entity) tab = 
            let name1 = x.LogicalName
            let name2 = x.CompiledName
            let tab = NameMap.add name1 x tab 
            if name1 = name2 then tab
            else NameMap.add name2 x tab 
          
        cacheOptByref &allEntitiesByMangledNameCache (fun () -> 
             QueueList.foldBack addEntityByMangledName entities Map.empty)

    /// Get a table of entities indexed by both logical name
    member _.AllEntitiesByLogicalMangledName: NameMap<Entity> = 
        let addEntityByMangledName (x: Entity) tab = NameMap.add x.LogicalName x tab 
        QueueList.foldBack addEntityByMangledName entities Map.empty

    /// Get a table of values and members indexed by partial linkage key, which includes name, the mangled name of the parent type (if any),
    /// and the method argument count (if any).
    member _.AllValsAndMembersByPartialLinkageKey = 
        let addValByMangledName (x: Val) tab = 
           if x.IsCompiledAsTopLevel then
               let key = x.GetLinkagePartialKey()
               MultiMap.add key x tab 
           else
               tab
        cacheOptByref &allValsAndMembersByPartialLinkageKeyCache (fun () -> 
             QueueList.foldBack addValByMangledName vals MultiMap.empty)

    /// Try to find the member with the given linkage key in the given module.
    member mtyp.TryLinkVal(ccu: CcuThunk, key: ValLinkageFullKey) = 
        mtyp.AllValsAndMembersByPartialLinkageKey
          |> MultiMap.find key.PartialKey
          |> List.tryFind (fun v -> match key.TypeForLinkage with 
                                    | None -> true
                                    | Some keyTy -> ccu.MemberSignatureEquality(keyTy, v.Type))
          |> ValueOptionInternal.ofOption

    /// Get a table of values indexed by logical name
    member _.AllValsByLogicalName = 
        let addValByName (x: Val) tab = 
           // Note: names may occur twice prior to raising errors about this in PostTypeCheckSemanticChecks
           // Earlier ones take precedence since we report errors about the later ones
           if not x.IsMember && not x.IsCompilerGenerated then 
               NameMap.add x.LogicalName x tab 
           else
               tab
        cacheOptByref &allValsByLogicalNameCache (fun () -> 
           QueueList.foldBack addValByName vals Map.empty)

    /// Compute a table of values and members indexed by logical name.
    member _.AllValsAndMembersByLogicalNameUncached = 
        let addValByName (x: Val) tab = 
            if not x.IsCompilerGenerated then 
                MultiMap.add x.LogicalName x tab 
            else
                tab
        QueueList.foldBack addValByName vals MultiMap.empty

    /// Get a table of F# exception definitions indexed by demangled name, so 'FailureException' is indexed by 'Failure'
    member mtyp.ExceptionDefinitionsByDemangledName = 
        let add (tycon: Tycon) acc = NameMap.add tycon.LogicalName tycon acc
        cacheOptByref &exconsByDemangledNameCache (fun () -> 
            List.foldBack add mtyp.ExceptionDefinitions Map.empty)

    /// Get a table of nested module and namespace fragments indexed by demangled name (so 'ListModule' becomes 'List')
    member _.ModulesAndNamespacesByDemangledName = 
        let add (entity: Entity) acc = 
            if entity.IsModuleOrNamespace then 
                NameMap.add entity.DemangledModuleOrNamespaceName entity acc
            else acc
        cacheOptByref &modulesByDemangledNameCache (fun () -> 
            QueueList.foldBack add entities Map.empty)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member mtyp.DebugText = mtyp.ToString()

    override _.ToString() = "ModuleOrNamespaceType(...)"

/// Represents a module or namespace definition in the typed AST
type ModuleOrNamespace = Entity 

/// Represents a type or exception definition in the typed AST
type Tycon = Entity 

/// Represents the constraint on access for a construct
[<StructuralEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type Accessibility = 

    /// Indicates the construct can only be accessed from any code in the given type constructor, module or assembly. [] indicates global scope. 
    | TAccess of compilationPaths: CompilationPath list
    
    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "Accessibility(...)"

/// Represents less-frequently-required data about a type parameter of type inference variable
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TyparOptionalData =
    {
      /// MUTABILITY: we set the names of generalized inference type parameters to make the look nice for IL code generation 
      /// The storage for the IL name for the type parameter.
      mutable typar_il_name: string option 

      /// The documentation for the type parameter. Empty for inference variables.
      /// MUTABILITY: for linking when unpickling
      mutable typar_xmldoc: XmlDoc

      /// The inferred constraints for the type parameter or inference variable.
      mutable typar_constraints: TyparConstraint list 

      /// The declared attributes of the type parameter. Empty for type inference variables. 
      mutable typar_attribs: Attribs
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = sprintf "TyparOptionalData(...)"

type TyparData = Typar

/// A declared generic type/measure parameter, or a type/measure inference variable.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type Typar = 
    {
      /// MUTABILITY: we set the names of generalized inference type parameters to make the look nice for IL code generation 
      /// The identifier for the type parameter
      mutable typar_id: Ident 
       
      /// The flag data for the type parameter
      mutable typar_flags: TyparFlags
       
      /// The unique stamp of the type parameter
      /// MUTABILITY: for linking when unpickling
      mutable typar_stamp: Stamp       
       
       /// An inferred equivalence for a type inference variable. 
      mutable typar_solution: TType option

      /// A cached TAST type used when this type variable is used as type.
      mutable typar_astype: TType
      
      /// The optional data for the type parameter
      mutable typar_opt_data: TyparOptionalData option
    }

    /// The name of the type parameter 
    member x.Name = x.typar_id.idText

    /// The range of the identifier for the type parameter definition
    member x.Range = x.typar_id.idRange

    /// The identifier for a type parameter definition
    member x.Id = x.typar_id

    /// The unique stamp of the type parameter
    member x.Stamp = x.typar_stamp

    /// The inferred equivalence for the type inference variable, if any.
    member x.Solution = x.typar_solution

    /// The inferred constraints for the type inference variable, if any
    member x.Constraints = 
        match x.typar_opt_data with
        | Some optData -> optData.typar_constraints
        | _ -> []

    /// Indicates if the type variable is compiler generated, i.e. is an implicit type inference variable 
    member x.IsCompilerGenerated = x.typar_flags.IsCompilerGenerated

    /// Indicates if the type variable can be solved or given new constraints. The status of a type variable
    /// generally always evolves towards being either rigid or solved. 
    member x.Rigidity = x.typar_flags.Rigidity

    /// Indicates if a type parameter is needed at runtime and may not be eliminated
    member x.DynamicReq = x.typar_flags.DynamicReq

    /// Indicates that whether or not a generic type definition satisfies the equality constraint is dependent on whether this type variable satisfies the equality constraint.
    member x.EqualityConditionalOn = x.typar_flags.EqualityConditionalOn

    /// Indicates that whether or not a generic type definition satisfies the comparison constraint is dependent on whether this type variable satisfies the comparison constraint.
    member x.ComparisonConditionalOn = x.typar_flags.ComparisonConditionalOn

    /// Indicates if the type variable has a static "head type" requirement, i.e. ^a variables used in FSharp.Core and member constraints.
    member x.StaticReq = x.typar_flags.StaticReq

    /// Indicates if the type inference variable was generated after an error when type checking expressions or patterns
    member x.IsFromError = x.typar_flags.IsFromError

    /// Indicates that whether this type parameter is a compat-flex type parameter (i.e. where "expr :> tp" only emits an optional warning)
    member x.IsCompatFlex = x.typar_flags.IsCompatFlex

    /// Set whether this type parameter is a compat-flex type parameter (i.e. where "expr :> tp" only emits an optional warning)
    member x.SetIsCompatFlex b = x.typar_flags <- x.typar_flags.WithCompatFlex b

    /// Indicates whether a type variable can be instantiated by types or units-of-measure.
    member x.Kind = x.typar_flags.Kind

    /// Indicates whether a type variable is erased in compiled .NET IL code, i.e. whether it is a unit-of-measure variable
    member x.IsErased = match x.Kind with TyparKind.Type -> false | _ -> true

    /// The declared attributes of the type parameter. Empty for type inference variables and parameters from .NET.
    member x.Attribs = 
        match x.typar_opt_data with
        | Some optData -> optData.typar_attribs
        | _ -> []

    /// Set the attributes on the type parameter
    member x.SetAttribs attribs = 
        match attribs, x.typar_opt_data with
        | [], None -> ()
        | [], Some { typar_il_name = None; typar_xmldoc = doc; typar_constraints = [] } when doc.IsEmpty ->
            x.typar_opt_data <- None
        | _, Some optData -> optData.typar_attribs <- attribs
        | _ -> x.typar_opt_data <- Some { typar_il_name = None; typar_xmldoc = XmlDoc.Empty; typar_constraints = []; typar_attribs = attribs }

    /// Get the XML documetnation for the type parameter
    member x.XmlDoc =
        match x.typar_opt_data with
        | Some optData -> optData.typar_xmldoc
        | _ -> XmlDoc.Empty

    /// Get the IL name of the type parameter
    member x.ILName =
        match x.typar_opt_data with
        | Some optData -> optData.typar_il_name
        | _ -> None

    /// Set the IL name of the type parameter
    member x.SetILName il_name =
        match x.typar_opt_data with
        | Some optData -> optData.typar_il_name <- il_name
        | _ -> x.typar_opt_data <- Some { typar_il_name = il_name; typar_xmldoc = XmlDoc.Empty; typar_constraints = []; typar_attribs = [] }

    /// Indicates the display name of a type variable
    member x.DisplayName = if x.Name = "?" then "?"+string x.Stamp else x.Name

    /// Adjusts the constraints associated with a type variable
    member x.SetConstraints cs =
        match cs, x.typar_opt_data with
        | [], None -> ()
        | [], Some { typar_il_name = None; typar_xmldoc = doc; typar_attribs = [] } when doc.IsEmpty ->
            x.typar_opt_data <- None
        | _, Some optData -> optData.typar_constraints <- cs
        | _ -> x.typar_opt_data <- Some { typar_il_name = None; typar_xmldoc = XmlDoc.Empty; typar_constraints = cs; typar_attribs = [] }

    /// Creates a type variable that contains empty data, and is not yet linked. Only used during unpickling of F# metadata.
    static member NewUnlinked() : Typar = 
        { typar_id = Unchecked.defaultof<_>
          typar_flags = Unchecked.defaultof<_>
          typar_stamp = -1L
          typar_solution = Unchecked.defaultof<_>
          typar_astype = Unchecked.defaultof<_>
          typar_opt_data = Unchecked.defaultof<_> }

    /// Creates a type variable based on the given data. Only used during unpickling of F# metadata.
    static member New (data: TyparData) : Typar = data

    /// Links a previously unlinked type variable to the given data. Only used during unpickling of F# metadata.
    member x.Link (tg: TyparData) = 
        x.typar_id <- tg.typar_id
        x.typar_flags <- tg.typar_flags
        x.typar_stamp <- tg.typar_stamp
        x.typar_solution <- tg.typar_solution
        match tg.typar_opt_data with
        | Some tg -> 
            let optData = { typar_il_name = tg.typar_il_name; typar_xmldoc = tg.typar_xmldoc; typar_constraints = tg.typar_constraints; typar_attribs = tg.typar_attribs }
            x.typar_opt_data <- Some optData
        | None -> ()

    /// Links a previously unlinked type variable to the given data. Only used during unpickling of F# metadata.
    member x.AsType = 
        let ty = x.typar_astype
        match box ty with 
        | null -> 
            let ty2 = TType_var x
            x.typar_astype <- ty2
            ty2
        | _ -> ty

    /// Indicates if a type variable has been linked. Only used during unpickling of F# metadata.
    member x.IsLinked = x.typar_stamp <> -1L

    /// Indicates if a type variable has been solved.
    member x.IsSolved = 
        match x.Solution with 
        | None -> false
        | _ -> true

    /// Sets the identifier associated with a type variable
    member x.SetIdent id = x.typar_id <- id

    /// Sets the rigidity of a type variable
    member x.SetRigidity b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, b, flags.IsFromError, flags.IsCompilerGenerated, flags.StaticReq, flags.DynamicReq, flags.EqualityConditionalOn, flags.ComparisonConditionalOn) 

    /// Sets whether a type variable is compiler generated
    member x.SetCompilerGenerated b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, flags.Rigidity, flags.IsFromError, b, flags.StaticReq, flags.DynamicReq, flags.EqualityConditionalOn, flags.ComparisonConditionalOn) 

    /// Sets whether a type variable has a static requirement
    member x.SetStaticReq b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, flags.Rigidity, flags.IsFromError, flags.IsCompilerGenerated, b, flags.DynamicReq, flags.EqualityConditionalOn, flags.ComparisonConditionalOn) 

    /// Sets whether a type variable is required at runtime
    member x.SetDynamicReq b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, flags.Rigidity, flags.IsFromError, flags.IsCompilerGenerated, flags.StaticReq, b, flags.EqualityConditionalOn, flags.ComparisonConditionalOn) 

    /// Sets whether the equality constraint of a type definition depends on this type variable 
    member x.SetEqualityDependsOn b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, flags.Rigidity, flags.IsFromError, flags.IsCompilerGenerated, flags.StaticReq, flags.DynamicReq, b, flags.ComparisonConditionalOn) 

    /// Sets whether the comparison constraint of a type definition depends on this type variable 
    member x.SetComparisonDependsOn b = let flags = x.typar_flags in x.typar_flags <- TyparFlags(flags.Kind, flags.Rigidity, flags.IsFromError, flags.IsCompilerGenerated, flags.StaticReq, flags.DynamicReq, flags.EqualityConditionalOn, b) 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.Name

/// Represents a constraint on a type parameter or type
[<NoEquality; NoComparison; RequireQualifiedAccess (*; StructuredFormatDisplay("{DebugText}") *) >]
type TyparConstraint = 

    /// A constraint that a type is a subtype of the given type 
    | CoercesTo of ty: TType * range: range

    /// A constraint for a default value for an inference type variable should it be neither generalized nor solved 
    | DefaultsTo of priority: int * ty: TType * range: range 
    
    /// A constraint that a type has a 'null' value 
    | SupportsNull of range: range 
    
    /// A constraint that a type has a member with the given signature 
    | MayResolveMember of constraintInfo: TraitConstraintInfo * range: range
    
    /// A constraint that a type is a non-Nullable value type 
    /// These are part of .NET's model of generic constraints, and in order to 
    /// generate verifiable code we must attach them to F# generalized type variables as well. 
    | IsNonNullableStruct of range: range 
    
    /// A constraint that a type is a reference type 
    | IsReferenceType of range: range 

    /// A constraint that a type is a simple choice between one of the given ground types. Only arises from 'printf' format strings. See format.fs 
    | SimpleChoice of tys: TTypes * range: range 

    /// A constraint that a type has a parameterless constructor 
    | RequiresDefaultConstructor of range: range 

    /// A constraint that a type is an enum with the given underlying 
    | IsEnum of ty: TType * range: range 
    
    /// A constraint that a type implements IComparable, with special rules for some known structural container types
    | SupportsComparison of range: range 
    
    /// A constraint that a type does not have the Equality(false) attribute, or is not a structural type with this attribute, with special rules for some known structural container types
    | SupportsEquality of range: range 
    
    /// A constraint that a type is a delegate from the given tuple of args to the given return type
    | IsDelegate of aty: TType * bty: TType * range: range 
    
    /// A constraint that a type is .NET unmanaged type
    | IsUnmanaged of range: range

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()
    
    override x.ToString() = sprintf "%+A" x 
    
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TraitWitnessInfo = 
    | TraitWitnessInfo of TTypes * string * SynMemberFlags * TTypes * TType option
    
    /// Get the member name associated with the member constraint.
    member x.MemberName = (let (TraitWitnessInfo(_, b, _, _, _)) = x in b)

    /// Get the return type recorded in the member constraint.
    member x.ReturnType = (let (TraitWitnessInfo(_, _, _, _, ty)) = x in ty)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TTrait(" + x.MemberName + ")"
    
/// The specification of a member constraint that must be solved 
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TraitConstraintInfo = 

    /// Indicates the signature of a member constraint. Contains a mutable solution cell
    /// to store the inferred solution of the constraint.
    | TTrait of tys: TTypes * memberName: string * _memFlags: SynMemberFlags * argTys: TTypes * returnTy: TType option * solution: TraitConstraintSln option ref 

    /// Get the key associated with the member constraint.
    member x.TraitKey = (let (TTrait(a, b, c, d, e, _)) = x in TraitWitnessInfo(a, b, c, d, e))

    /// Get the member name associated with the member constraint.
    member x.MemberName = (let (TTrait(_, nm, _, _, _, _)) = x in nm)

    /// Get the member flags associated with the member constraint.
    member x.MemberFlags = (let (TTrait(_, _, flags, _, _, _)) = x in flags)

    /// Get the argument types recorded in the member constraint. This includes the object instance type for
    /// instance members.
    member x.ArgumentTypes = (let (TTrait(_, _, _, argtys, _, _)) = x in argtys)

    /// Get the return type recorded in the member constraint.
    member x.ReturnType = (let (TTrait(_, _, _, _, ty, _)) = x in ty)

    /// Get or set the solution of the member constraint during inference
    member x.Solution 
        with get() = (let (TTrait(_, _, _, _, _, sln)) = x in sln.Value)
        and set v = (let (TTrait(_, _, _, _, _, sln)) = x in sln.Value <- v)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TTrait(" + x.MemberName + ")"
    
/// Represents the solution of a member constraint during inference.
[<NoEquality; NoComparison (* ; StructuredFormatDisplay("{DebugText}") *) >]
type TraitConstraintSln = 

    /// FSMethSln(ty, vref, minst)
    ///
    /// Indicates a trait is solved by an F# method.
    ///    ty -- the type and its instantiation
    ///    vref -- the method that solves the trait constraint
    ///    minst -- the generic method instantiation 
    | FSMethSln of ty: TType * vref: ValRef * minst: TypeInst 

    /// FSRecdFieldSln(tinst, rfref, isSetProp)
    ///
    /// Indicates a trait is solved by an F# record field.
    ///    tinst -- the instantiation of the declaring type
    ///    rfref -- the reference to the record field
    ///    isSetProp -- indicates if this is a set of a record field
    | FSRecdFieldSln of tinst: TypeInst * rfref: RecdFieldRef * isSetProp: bool

    /// Indicates a trait is solved by an F# anonymous record field.
    | FSAnonRecdFieldSln of anonInfo: AnonRecdTypeInfo * tinst: TypeInst * index: int

    /// ILMethSln(ty, extOpt, ilMethodRef, minst)
    ///
    /// Indicates a trait is solved by a .NET method.
    ///    ty -- the type and its instantiation
    ///    extOpt -- information about an extension member, if any
    ///    ilMethodRef -- the method that solves the trait constraint
    ///    minst -- the generic method instantiation 
    | ILMethSln of ty: TType * extOpt: ILTypeRef option * ilMethodRef: ILMethodRef * minst: TypeInst    

    /// ClosedExprSln expr
    ///
    /// Indicates a trait is solved by an erased provided expression
    | ClosedExprSln of expr: Expr 

    /// Indicates a trait is solved by a 'fake' instance of an operator, like '+' on integers
    | BuiltInSln

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// The partial information used to index the methods of all those in a ModuleOrNamespace.
[<RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type ValLinkagePartialKey = 
   {
     /// The name of the type with which the member is associated. None for non-member values.
     MemberParentMangledName: string option 

     /// Indicates if the member is an override. 
     MemberIsOverride: bool 

     /// Indicates the logical name of the member. 
     LogicalName: string 

     /// Indicates the total argument count of the member.
     TotalArgCount: int
    } 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "ValLinkagePartialKey(" + x.LogicalName + ")"

/// The full information used to identify a specific overloaded method
/// amongst all those in a ModuleOrNamespace.
[<StructuredFormatDisplay("{DebugText}")>]
type ValLinkageFullKey(partialKey: ValLinkagePartialKey, typeForLinkage: TType option) =

    /// The partial information used to index the value in a ModuleOrNamespace.
    member x.PartialKey = partialKey

    /// The full type of the value for the purposes of linking. May be None for non-members, since they can't be overloaded.
    member x.TypeForLinkage = typeForLinkage

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "ValLinkageFullKey(" + partialKey.LogicalName + ")"

[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ValOptionalData =
    {
      /// MUTABILITY: for unpickle linkage
      mutable val_compiled_name: string option

      /// If this field is populated, this is the implementation range for an item in a signature, otherwise it is 
      /// the signature range for an item in an implementation
      mutable val_other_range: (range * bool) option 

      mutable val_const: Const option
      
      /// What is the original, unoptimized, closed-term definition, if any? 
      /// Used to implement [<ReflectedDefinition>]
      mutable val_defn: Expr option 

      // MUTABILITY CLEANUP: mutability of this field is used by 
      //     -- adjustAllUsesOfRecValue 
      //     -- TLR optimizations
      //     -- LinearizeTopMatch
      //
      // For example, we use mutability to replace the empty arity initially assumed with an arity garnered from the 
      // type-checked expression.  
      mutable val_repr_info: ValReprInfo option

      /// How visible is this? 
      /// MUTABILITY: for unpickle linkage
      mutable val_access: Accessibility 

      /// XML documentation attached to a value.
      /// MUTABILITY: for unpickle linkage
      mutable val_xmldoc: XmlDoc 

      /// Is the value actually an instance method/property/event that augments 
      /// a type, and if so what name does it take in the IL?
      /// MUTABILITY: for unpickle linkage
      mutable val_member_info: ValMemberInfo option

      // MUTABILITY CLEANUP: mutability of this field is used by 
      //     -- LinearizeTopMatch
      //
      // The fresh temporary should just be created with the right parent
      mutable val_declaring_entity: ParentRef

      /// XML documentation signature for the value
      mutable val_xmldocsig: string

      /// Custom attributes attached to the value. These contain references to other values (i.e. constructors in types). Mutable to fixup  
      /// these value references after copying a collection of values. 
      mutable val_attribs: Attribs
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "ValOptionalData(...)"

type ValData = Val

[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type Val = 
    {
      /// Mutable for unpickle linkage
      mutable val_logical_name: string

      /// Mutable for unpickle linkage
      mutable val_range: range

      mutable val_type: TType

      /// Mutable for unpickle linkage
      mutable val_stamp: Stamp 

      /// See vflags section further below for encoding/decodings here
      mutable val_flags: ValFlags

      mutable val_opt_data: ValOptionalData option }

    static member NewEmptyValOptData() =
        { val_compiled_name = None
          val_other_range = None
          val_const = None
          val_defn = None
          val_repr_info = None
          val_access = TAccess []
          val_xmldoc = XmlDoc.Empty
          val_member_info = None
          val_declaring_entity = ParentNone
          val_xmldocsig = String.Empty
          val_attribs = [] }

    /// Range of the definition (implementation) of the value, used by Visual Studio 
    member x.DefinitionRange = 
        match x.val_opt_data with
        | Some { val_other_range = Some(m, true) } -> m
        | _ -> x.val_range

    /// Range of the definition (signature) of the value, used by Visual Studio 
    member x.SigRange = 
        match x.val_opt_data with
        | Some { val_other_range = Some(m, false) } -> m
        | _ -> x.val_range

    /// The place where the value was defined. 
    member x.Range = x.val_range

    /// A unique stamp within the context of this invocation of the compiler process 
    member x.Stamp = x.val_stamp

    /// The type of the value. 
    /// May be a TType_forall for a generic value. 
    /// May be a type variable or type containing type variables during type inference. 
    //
    // Note: this data is mutated during inference by adjustAllUsesOfRecValue when we replace the inferred type with a schema. 
    member x.Type = x.val_type

    /// How visible is this value, function or member?
    member x.Accessibility = 
        match x.val_opt_data with
        | Some optData -> optData.val_access
        | _ -> TAccess []

    /// The value of a value or member marked with [<LiteralAttribute>] 
    member x.LiteralValue = 
        match x.val_opt_data with
        | Some optData -> optData.val_const
        | _ -> None

    /// Records the "extra information" for a value compiled as a method.
    ///
    /// This indicates the number of arguments in each position for a curried 
    /// functions, and relates to the F# spec for arity analysis.
    /// For module-defined values, the currying is based 
    /// on the number of lambdas, and in each position the elements are 
    /// based on attempting to deconstruct the type of the argument as a 
    /// tuple-type.  
    ///
    /// The field is mutable because arities for recursive 
    /// values are only inferred after the r.h.s. is analyzed, but the 
    /// value itself is created before the r.h.s. is analyzed. 
    ///
    /// TLR also sets this for inner bindings that it wants to 
    /// represent as "top level" bindings.     
    member x.ValReprInfo: ValReprInfo option =
        match x.val_opt_data with
        | Some optData -> optData.val_repr_info
        | _ -> None

    member x.Id = ident(x.LogicalName, x.Range)

    /// Is this represented as a "top level" static binding (i.e. a static field, static member,
    /// instance member), rather than an "inner" binding that may result in a closure.
    ///
    /// This is implied by IsMemberOrModuleBinding, however not vice versa, for two reasons.
    /// Some optimizations mutate this value when they decide to change the representation of a 
    /// binding to be IsCompiledAsTopLevel. Second, even immediately after type checking we expect
    /// some non-module, non-member bindings to be marked IsCompiledAsTopLevel, e.g. 'y' in 
    /// 'let x = let y = 1 in y + y' (NOTE: check this, don't take it as gospel)
    member x.IsCompiledAsTopLevel = x.ValReprInfo.IsSome 

    /// The partial information used to index the methods of all those in a ModuleOrNamespace.
    member x.GetLinkagePartialKey() : ValLinkagePartialKey = 
        assert x.IsCompiledAsTopLevel
        { LogicalName = x.LogicalName 
          MemberParentMangledName = (if x.IsMember then Some x.MemberApparentEntity.LogicalName else None)
          MemberIsOverride = x.IsOverrideOrExplicitImpl
          TotalArgCount = if x.IsMember then x.ValReprInfo.Value.TotalArgCount else 0 }

    /// The full information used to identify a specific overloaded method amongst all those in a ModuleOrNamespace.
    member x.GetLinkageFullKey() : ValLinkageFullKey = 
        assert x.IsCompiledAsTopLevel
        let key = x.GetLinkagePartialKey()
        ValLinkageFullKey(key, (if x.IsMember then Some x.Type else None))

    /// Is this a member definition or module definition?
    member x.IsMemberOrModuleBinding = x.val_flags.IsMemberOrModuleBinding

    /// Indicates if this is an F#-defined extension member
    member x.IsExtensionMember = x.val_flags.IsExtensionMember

    /// The quotation expression associated with a value given the [<ReflectedDefinition>] tag
    member x.ReflectedDefinition =
        match x.val_opt_data with
        | Some optData -> optData.val_defn
        | _ -> None

    /// Is this a member, if so some more data about the member.
    ///
    /// Note, the value may still be (a) an extension member or (b) and abstract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.MemberInfo = 
        match x.val_opt_data with
        | Some optData -> optData.val_member_info
        | _ -> None

    /// Indicates if this is a member
    member x.IsMember = x.MemberInfo.IsSome

    /// Indicates if this is a member, excluding extension members
    member x.IsIntrinsicMember = x.IsMember && not x.IsExtensionMember

    /// Indicates if this is an F#-defined value in a module, or an extension member, but excluding compiler generated bindings from optimizations
    member x.IsModuleBinding = x.IsMemberOrModuleBinding && not x.IsMember 

    /// Indicates if this is something compiled into a module, i.e. a user-defined value, an extension member or a compiler-generated value
    member x.IsCompiledIntoModule = x.IsExtensionMember || x.IsModuleBinding

    /// Indicates if this is an F#-defined instance member. 
    ///
    /// Note, the value may still be (a) an extension member or (b) and abstract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.IsInstanceMember = x.IsMember && x.MemberInfo.Value.MemberFlags.IsInstance

    /// Indicates if this is an F#-defined 'new' constructor member
    member x.IsConstructor =
        match x.MemberInfo with 
        | Some memberInfo when not x.IsExtensionMember && (memberInfo.MemberFlags.MemberKind = SynMemberKind.Constructor) -> true
        | _ -> false

    /// Indicates if this is a compiler-generated class constructor member
    member x.IsClassConstructor =
        match x.MemberInfo with 
        | Some memberInfo when not x.IsExtensionMember && (memberInfo.MemberFlags.MemberKind = SynMemberKind.ClassConstructor) -> true
        | _ -> false

    /// Indicates if this value was a member declared 'override' or an implementation of an interface slot
    member x.IsOverrideOrExplicitImpl =
        match x.MemberInfo with 
        | Some memberInfo when memberInfo.MemberFlags.IsOverrideOrExplicitImpl -> true
        | _ -> false
            
    /// Indicates if this is declared 'mutable'
    member x.IsMutable = (match x.val_flags.MutabilityInfo with Immutable -> false | Mutable -> true)

    /// Indicates if this is inferred to be a method or function that definitely makes no critical tailcalls?
    member x.MakesNoCriticalTailcalls = x.val_flags.MakesNoCriticalTailcalls
    
    /// Indicates if this is ever referenced?
    member x.HasBeenReferenced = x.val_flags.HasBeenReferenced

    /// Indicates if the backing field for a static value is suppressed.
    member x.IsCompiledAsStaticPropertyWithoutField = 
            let hasValueAsStaticProperty = x.Attribs |> List.exists(fun (Attrib(tc, _, _, _, _, _, _)) -> tc.CompiledName = "ValueAsStaticPropertyAttribute")
            x.val_flags.IsCompiledAsStaticPropertyWithoutField || hasValueAsStaticProperty

    /// Indicates if the value is pinned/fixed
    member x.IsFixed = x.val_flags.IsFixed

    /// Indicates if the value will ignore byref scoping rules
    member x.IgnoresByrefScope = x.val_flags.IgnoresByrefScope

    /// Indicates if this value allows the use of an explicit type instantiation (i.e. does it itself have explicit type arguments,
    /// or does it have a signature?)
    member x.PermitsExplicitTypeInstantiation = x.val_flags.PermitsExplicitTypeInstantiation

    /// Indicates if this is a member generated from the de-sugaring of 'let' function bindings in the implicit class syntax?
    member x.IsIncrClassGeneratedMember = x.IsCompilerGenerated && x.val_flags.IsIncrClassSpecialMember

    /// Indicates if this is a constructor member generated from the de-sugaring of implicit constructor for a class type?
    member x.IsIncrClassConstructor = x.IsConstructor && x.val_flags.IsIncrClassSpecialMember

    /// Get the information about the value used during type inference
    member x.RecursiveValInfo = x.val_flags.RecursiveValInfo

    /// Indicates if this is a 'base' or 'this' value?
    member x.BaseOrThisInfo = x.val_flags.BaseOrThisInfo

    //  Indicates if this value was declared to be a type function, e.g. "let f<'a> = typeof<'a>"
    member x.IsTypeFunction = x.val_flags.IsTypeFunction

    /// Get the inline declaration on the value
    member x.InlineInfo = x.val_flags.InlineInfo

    /// Indicates whether the inline declaration for the value indicate that the value must be inlined?
    member x.MustInline = x.InlineInfo.MustInline

    /// Indicates whether this value was generated by the compiler.
    ///
    /// Note: this is true for the overrides generated by hash/compare augmentations
    member x.IsCompilerGenerated = x.val_flags.IsCompilerGenerated
    
    /// Get the declared attributes for the value
    member x.Attribs = 
        match x.val_opt_data with
        | Some optData -> optData.val_attribs
        | _ -> []

    /// Get the declared documentation for the value
    member x.XmlDoc =
        match x.val_opt_data with
        | Some optData -> optData.val_xmldoc
        | _ -> XmlDoc.Empty
    
    ///Get the signature for the value's XML documentation
    member x.XmlDocSig 
        with get() = 
            match x.val_opt_data with 
            | Some optData -> optData.val_xmldocsig 
            | _ -> String.Empty
        and set v = 
            match x.val_opt_data with 
            | Some optData -> optData.val_xmldocsig <- v 
            | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_xmldocsig = v }

    /// The parent type or module, if any (None for expression bindings and parameters)
    member x.DeclaringEntity = 
        match x.val_opt_data with
        | Some optData -> optData.val_declaring_entity
        | _ -> ParentNone

    /// Get the actual parent entity for the value (a module or a type), i.e. the entity under which the
    /// value will appear in compiled code. For extension members this is the module where the extension member
    /// is declared.
    member x.TopValDeclaringEntity = 
        match x.DeclaringEntity with 
        | Parent tcref -> tcref
        | ParentNone -> error(InternalError("TopValDeclaringEntity: does not have a parent", x.Range))

    member x.HasDeclaringEntity = 
        match x.DeclaringEntity with 
        | Parent _ -> true
        | ParentNone -> false
            
    /// Get the apparent parent entity for a member
    member x.MemberApparentEntity: TyconRef = 
        match x.MemberInfo with 
        | Some membInfo -> membInfo.ApparentEnclosingEntity
        | None -> error(InternalError("MemberApparentEntity", x.Range))

    /// Get the number of 'this'/'self' object arguments for the member. Instance extension members return '1'.
    member v.NumObjArgs =
        match v.MemberInfo with 
        | Some membInfo -> if membInfo.MemberFlags.IsInstance then 1 else 0
        | None -> 0

    /// Get the apparent parent entity for the value, i.e. the entity under with which the
    /// value is associated. For extension members this is the nominal type the member extends.
    /// For other values it is just the actual parent.
    member x.ApparentEnclosingEntity = 
        match x.MemberInfo with 
        | Some membInfo -> Parent(membInfo.ApparentEnclosingEntity)
        | None -> x.DeclaringEntity

    /// Get the public path to the value, if any? Should be set if and only if
    /// IsMemberOrModuleBinding is set.
    //
    // We use it here:
    //   - in opt.fs: when compiling fslib, we bind an entry for the value in a global table (see bind_escaping_local_vspec)
    //   - in ilxgen.fs: when compiling fslib, we bind an entry for the value in a global table (see bind_escaping_local_vspec)
    //   - in opt.fs: (fullDebugTextOfValRef) for error reporting of non-inlinable values
    //   - in service.fs (boutput_item_description): to display the full text of a value's binding location
    //   - in check.fs: as a boolean to detect public values for saving quotations 
    //   - in ilxgen.fs: as a boolean to detect public values for saving quotations 
    //   - in MakeExportRemapping, to build non-local references for values
    member x.PublicPath = 
        match x.DeclaringEntity with 
        | Parent eref -> 
            match eref.PublicPath with 
            | None -> None
            | Some p -> Some(ValPubPath(p, x.GetLinkageFullKey()))
        | ParentNone -> 
            None

    /// Indicates if this member is an F#-defined dispatch slot.
    member x.IsDispatchSlot = 
        match x.MemberInfo with 
        | Some membInfo -> membInfo.MemberFlags.IsDispatchSlot 
        | _ -> false

    /// Get the type of the value including any generic type parameters
    member x.TypeScheme = 
        match x.Type with 
        | TType_forall(tps, tau) -> tps, tau
        | ty -> [], ty

    /// Get the type of the value after removing any generic type parameters
    member x.TauType = 
        match x.Type with 
        | TType_forall(_, tau) -> tau
        | ty -> ty

    /// Get the generic type parameters for the value
    member x.Typars = 
        match x.Type with 
        | TType_forall(tps, _) -> tps
        | _ -> []

    /// The name of the method. 
    ///   - If this is a property then this is 'get_Foo' or 'set_Foo'
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an extension member then this will be the simple name
    member x.LogicalName = 
        match x.MemberInfo with 
        | None -> x.val_logical_name
        | Some membInfo -> 
            match membInfo.ImplementedSlotSigs with 
            | slotsig :: _ -> slotsig.Name
            | _ -> x.val_logical_name

    // Set the logical name of the value
    member x.SetLogicalName(nm) = 
        x.val_logical_name <- nm

    member x.ValCompiledName =
        match x.val_opt_data with
        | Some optData -> optData.val_compiled_name
        | _ -> None

    /// The name of the method in compiled code (with some exceptions where ilxgen.fs decides not to use a method impl)
    ///   - If this is a property then this is 'get_Foo' or 'set_Foo'
    ///   - If this is an implementation of an abstract slot then this may be a mangled name
    ///   - If this is an extension member then this will be a mangled name
    ///   - If this is an operator then this is 'op_Addition'
    member x.CompiledName (compilerGlobalState:CompilerGlobalState option) =
        let givenName = 
            match x.val_opt_data with 
            | Some { val_compiled_name = Some n } -> n
            | _ -> x.LogicalName 
        // These cases must get stable unique names for their static field & static property. This name
        // must be stable across quotation generation and IL code generation (quotations can refer to the 
        // properties implicit in these)
        //
        //    Variable 'x' here, which is compiled as a top level static:
        //         do let x = expr in ...    // IsMemberOrModuleBinding = false, IsCompiledAsTopLevel = true, IsMember = false, CompilerGenerated=false
        //
        //    The implicit 'patternInput' variable here:
        //         let [x] = expr in ...    // IsMemberOrModuleBinding = true, IsCompiledAsTopLevel = true, IsMember = false, CompilerGenerated=true
        //    
        //    The implicit 'copyOfStruct' variables here:
        //         let dt = System.DateTime.Now - System.DateTime.Now // IsMemberOrModuleBinding = false, IsCompiledAsTopLevel = true, IsMember = false, CompilerGenerated=true
        //    
        // However we don't need this for CompilerGenerated members such as the implementations of IComparable
        match compilerGlobalState with
        | Some state when x.IsCompiledAsTopLevel && not x.IsMember && (x.IsCompilerGenerated || not x.IsMemberOrModuleBinding) ->
            state.StableNameGenerator.GetUniqueCompilerGeneratedName(givenName, x.Range, x.Stamp) 
        | _ -> givenName

    /// The name of the property.
    /// - If this is a property then this is 'Foo' 
    ///  - If this is an implementation of an abstract slot then this is the name of the property implemented by the abstract slot
    member x.PropertyName = 
        let logicalName = x.LogicalName
        ChopPropertyName logicalName

    /// The name of the method. 
    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an operator then this is 'op_Addition'
    member x.CoreDisplayName = 
        match x.MemberInfo with 
        | Some membInfo -> 
            match membInfo.MemberFlags.MemberKind with 
            | SynMemberKind.ClassConstructor 
            | SynMemberKind.Constructor 
            | SynMemberKind.Member -> x.LogicalName
            | SynMemberKind.PropertyGetSet 
            | SynMemberKind.PropertySet
            | SynMemberKind.PropertyGet -> x.PropertyName
        | None -> x.LogicalName

    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an operator then this is '(+)'
    member x.DisplayName = 
        DemangleOperatorName x.CoreDisplayName

    member x.SetValRec b = x.val_flags <- x.val_flags.WithRecursiveValInfo b 

    member x.SetIsCompilerGenerated(v) = x.val_flags <- x.val_flags.WithIsCompilerGenerated(v) 

    member x.SetIsMemberOrModuleBinding() = x.val_flags <- x.val_flags.WithIsMemberOrModuleBinding 

    member x.SetMakesNoCriticalTailcalls() = x.val_flags <- x.val_flags.WithMakesNoCriticalTailcalls

    member x.SetHasBeenReferenced() = x.val_flags <- x.val_flags.WithHasBeenReferenced

    member x.SetIsCompiledAsStaticPropertyWithoutField() = x.val_flags <- x.val_flags.WithIsCompiledAsStaticPropertyWithoutField

    member x.SetIsFixed() = x.val_flags <- x.val_flags.WithIsFixed

    member x.SetIgnoresByrefScope() = x.val_flags <- x.val_flags.WithIgnoresByrefScope

    member x.SetValReprInfo info = 
        match x.val_opt_data with
        | Some optData -> optData.val_repr_info <- info
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_repr_info = info }

    member x.SetType ty = x.val_type <- ty

    member x.SetOtherRange m =
        match x.val_opt_data with
        | Some optData -> optData.val_other_range <- Some m
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_other_range = Some m }

    member x.SetDeclaringEntity parent = 
        match x.val_opt_data with
        | Some optData -> optData.val_declaring_entity <- parent
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_declaring_entity = parent }

    member x.SetAttribs attribs = 
        match x.val_opt_data with
        | Some optData -> optData.val_attribs <- attribs
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_attribs = attribs }

    member x.SetMemberInfo member_info = 
        match x.val_opt_data with
        | Some optData -> optData.val_member_info <- Some member_info
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_member_info = Some member_info }

    member x.SetValDefn val_defn = 
        match x.val_opt_data with
        | Some optData -> optData.val_defn <- Some val_defn
        | _ -> x.val_opt_data <- Some { Val.NewEmptyValOptData() with val_defn = Some val_defn }

    /// Create a new value with empty, unlinked data. Only used during unpickling of F# metadata.
    static member NewUnlinked() : Val = 
        { val_logical_name = Unchecked.defaultof<_>
          val_range = Unchecked.defaultof<_>
          val_type = Unchecked.defaultof<_>
          val_stamp = Unchecked.defaultof<_>
          val_flags = Unchecked.defaultof<_>
          val_opt_data = Unchecked.defaultof<_> }


    /// Create a new value with the given backing data. Only used during unpickling of F# metadata.
    static member New data: Val = data

    /// Link a value based on empty, unlinked data to the given data. Only used during unpickling of F# metadata.
    member x.Link (tg: ValData) = x.SetData tg

    /// Set all the data on a value
    member x.SetData (tg: ValData) = 
        x.val_logical_name <- tg.val_logical_name 
        x.val_range <- tg.val_range        
        x.val_type <- tg.val_type         
        x.val_stamp <- tg.val_stamp        
        x.val_flags <- tg.val_flags        
        match tg.val_opt_data with
        | Some tg -> 
            x.val_opt_data <- 
                Some { val_compiled_name = tg.val_compiled_name
                       val_other_range = tg.val_other_range
                       val_const = tg.val_const
                       val_defn = tg.val_defn
                       val_repr_info = tg.val_repr_info
                       val_access = tg.val_access
                       val_xmldoc = tg.val_xmldoc
                       val_member_info = tg.val_member_info
                       val_declaring_entity = tg.val_declaring_entity
                       val_xmldocsig = tg.val_xmldocsig
                       val_attribs = tg.val_attribs }
        | None -> ()

    /// Indicates if a value is linked to backing data yet. Only used during unpickling of F# metadata.
    member x.IsLinked = match box x.val_logical_name with null -> false | _ -> true 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.LogicalName
    
    
/// Represents the extra information stored for a member
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type ValMemberInfo = 
    {
      /// The parent type. For an extension member this is the type being extended 
      ApparentEnclosingEntity: TyconRef  

      /// Updated with the full implemented slotsig after interface implementation relation is checked 
      mutable ImplementedSlotSigs: SlotSig list 

      /// Gets updated with 'true' if an abstract slot is implemented in the file being typechecked. Internal only. 
      mutable IsImplemented: bool                      

      MemberFlags: SynMemberFlags
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "ValMemberInfo(...)"

[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type NonLocalValOrMemberRef = 
    {
      /// A reference to the entity containing the value or member. This will always be a non-local reference
      EnclosingEntity: EntityRef 

      /// The name of the value, or the full signature of the member
      ItemKey: ValLinkageFullKey
    }

    /// Get the thunk for the assembly referred to
    member x.Ccu = x.EnclosingEntity.nlr.Ccu

    /// Get the name of the assembly referred to
    member x.AssemblyName = x.EnclosingEntity.nlr.AssemblyName

    /// For debugging
    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    /// For debugging
    override x.ToString() = x.EnclosingEntity.nlr.ToString() + "::" + x.ItemKey.PartialKey.LogicalName
      
/// Represents the path information for a reference to a value or member in another assembly, disassociated
/// from any particular reference.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ValPublicPath = 
    | ValPubPath of PublicPath * ValLinkageFullKey

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = sprintf "ValPubPath(...)"

/// Represents an index into the namespace/module structure of an assembly
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type NonLocalEntityRef = 
    | NonLocalEntityRef of CcuThunk * string[]

    /// Try to find the entity corresponding to the given path in the given CCU
    static member TryDerefEntityPath(ccu: CcuThunk, path: string[], i: int, entity: Entity) = 
        if i >= path.Length then ValueSome entity
        else  
            match entity.ModuleOrNamespaceType.AllEntitiesByCompiledAndLogicalMangledNames.TryGetValue path.[i] with 
            | true, res -> NonLocalEntityRef.TryDerefEntityPath(ccu, path, (i+1), res)
#if !NO_EXTENSIONTYPING
            | _ -> NonLocalEntityRef.TryDerefEntityPathViaProvidedType(ccu, path, i, entity)
#else
            | _ -> ValueNone
#endif

#if !NO_EXTENSIONTYPING
    /// Try to find the entity corresponding to the given path, using type-providers to link the data
    static member TryDerefEntityPathViaProvidedType(ccu: CcuThunk, path: string[], i: int, entity: Entity) = 
        // Errors during linking are not necessarily given good ranges. This has always been the case in F# 2.0, but also applies to
        // type provider type linking errors in F# 3.0.
        let m = range0
        match entity.TypeReprInfo with
        | TProvidedTypeExtensionPoint info -> 
            let resolutionEnvironment = info.ResolutionEnvironment
            let st = info.ProvidedType
                        
            // In this case, we're safely in the realm of types. Just iterate through the nested
            // types until i = path.Length-1. Create the Tycon's as needed
            let rec tryResolveNestedTypeOf(parentEntity: Entity, resolutionEnvironment, st: Tainted<ProvidedType>, i) = 
                match st.PApply((fun st -> st.GetNestedType path.[i]), m) with
                | Tainted.Null -> ValueNone
                | st -> 
                    let newEntity = Construct.NewProvidedTycon(resolutionEnvironment, st, ccu.ImportProvidedType, false, m)
                    parentEntity.ModuleOrNamespaceType.AddProvidedTypeEntity newEntity
                    if i = path.Length-1 then ValueSome newEntity
                    else tryResolveNestedTypeOf(newEntity, resolutionEnvironment, st, i+1)

            tryResolveNestedTypeOf(entity, resolutionEnvironment, st, i)

        | TProvidedNamespaceExtensionPoint(resolutionEnvironment, resolvers) -> 

            // In this case, we're still in the realm of extensible namespaces. 
            //     <----entity-->
            //     0 .........i-1..i .......... j ..... path.Length-1
            //
            //     <----entity-->  <---resolver---->
            //     0 .........i-1..i ............. j ..... path.Length-1
            //
            //     <----entity-->  <---resolver----> <--loop--->
            //     0 .........i-1..i ............. j ..... path.Length-1
            //
            // We now query the resolvers with 
            //      moduleOrNamespace = path.[0..j-1] 
            //      typeName = path.[j] 
            // starting with j = i and then progressively increasing j
                        
            // This function queries at 'j'
            let tryResolvePrefix j = 
                assert (j >= 0)
                assert (j <= path.Length - 1)
                let matched = 
                    [ for resolver in resolvers do
                        let moduleOrNamespace = if j = 0 then null else path.[0..j-1]
                        let typename = path.[j]
                        let resolution = ExtensionTyping.TryLinkProvidedType(resolver, moduleOrNamespace, typename, m)
                        match resolution with
                        | None | Some (Tainted.Null) -> ()
                        | Some st -> yield (resolver, st) ]
                match matched with
                | [(_, st)] ->
                    // 'entity' is at position i in the dereference chain. We resolved to position 'j'.
                    // Inject namespaces until we're an position j, and then inject the type.
                    // Note: this is similar to code in CompileOps.fs
                    let rec injectNamespacesFromIToJ (entity: Entity) k = 
                        if k = j then 
                            let newEntity = Construct.NewProvidedTycon(resolutionEnvironment, st, ccu.ImportProvidedType, false, m)
                            entity.ModuleOrNamespaceType.AddProvidedTypeEntity newEntity
                            newEntity
                        else
                            let cpath = entity.CompilationPath.NestedCompPath entity.LogicalName ModuleOrNamespaceKind.Namespace
                            let newEntity = 
                                Construct.NewModuleOrNamespace 
                                    (Some cpath) 
                                    (TAccess []) (ident(path.[k], m)) XmlDoc.Empty [] 
                                    (MaybeLazy.Strict (Construct.NewEmptyModuleOrNamespaceType Namespace)) 
                            entity.ModuleOrNamespaceType.AddModuleOrNamespaceByMutation newEntity
                            injectNamespacesFromIToJ newEntity (k+1)
                    let newEntity = injectNamespacesFromIToJ entity i
                                
                    // newEntity is at 'j'
                    NonLocalEntityRef.TryDerefEntityPath(ccu, path, (j+1), newEntity) 

                | [] -> ValueNone 
                | _ -> failwith "Unexpected"

            let rec tryResolvePrefixes j = 
                if j >= path.Length then ValueNone
                else match tryResolvePrefix j with 
                      | ValueNone -> tryResolvePrefixes (j+1)
                      | ValueSome res -> ValueSome res

            tryResolvePrefixes i

        | _ -> ValueNone
#endif
            
    /// Try to link a non-local entity reference to an actual entity
    member nleref.TryDeref canError = 
        let (NonLocalEntityRef(ccu, path)) = nleref 
        if canError then 
            ccu.EnsureDerefable path

        if ccu.IsUnresolvedReference then ValueNone else

        match NonLocalEntityRef.TryDerefEntityPath(ccu, path, 0, ccu.Contents) with
        | ValueSome _ as r -> r
        | ValueNone ->
            // OK, the lookup failed. Check if we can redirect through a type forwarder on this assembly.
            // Look for a forwarder for each prefix-path
            let rec tryForwardPrefixPath i = 
                if i < path.Length then 
                    match ccu.TryForward(path.[0..i-1], path.[i]) with
                       // OK, found a forwarder, now continue with the lookup to find the nested type
                    | Some tcref -> NonLocalEntityRef.TryDerefEntityPath(ccu, path, (i+1), tcref.Deref)  
                    | None -> tryForwardPrefixPath (i+1)
                else
                    ValueNone
            tryForwardPrefixPath 0
        
    /// Get the CCU referenced by the nonlocal reference.
    member nleref.Ccu =
        let (NonLocalEntityRef(ccu, _)) = nleref 
        ccu

    /// Get the path into the CCU referenced by the nonlocal reference.
    member nleref.Path =
        let (NonLocalEntityRef(_, p)) = nleref 
        p

    member nleref.DisplayName =
        String.concat "." nleref.Path

    /// Get the mangled name of the last item in the path of the nonlocal reference.
    member nleref.LastItemMangledName = 
        let p = nleref.Path
        p.[p.Length-1]

    /// Get the all-but-last names of the path of the nonlocal reference.
    member nleref.EnclosingMangledPath =  
        let p = nleref.Path
        p.[0..p.Length-2]
        
    /// Get the name of the assembly referenced by the nonlocal reference.
    member nleref.AssemblyName = nleref.Ccu.AssemblyName

    /// Dereference the nonlocal reference, and raise an error if this fails.
    member nleref.Deref = 
        match nleref.TryDeref(canError=true) with 
        | ValueSome res -> res
        | ValueNone -> 
              errorR (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefModuleNamespace, nleref.DisplayName, nleref.AssemblyName, "<some module on this path>")) 
              raise (KeyNotFoundException())
        
    /// Get the details of the module or namespace fragment for the entity referred to by this non-local reference.
    member nleref.ModuleOrNamespaceType = 
        nleref.Deref.ModuleOrNamespaceType

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.DisplayName
        
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type EntityRef = 
    {
      /// Indicates a reference to something bound in this CCU 
      mutable binding: NonNullSlot<Entity>

      /// Indicates a reference to something bound in another CCU 
      nlr: NonLocalEntityRef
    }

    /// Indicates if the reference is a local reference
    member x.IsLocalRef = match box x.nlr with null -> true | _ -> false

    /// Indicates if the reference has been resolved
    member x.IsResolved = match box x.binding with null -> false | _ -> true

    /// The resolved target of the reference
    member x.ResolvedTarget = x.binding

    /// Resolve the reference
    member private tcr.Resolve canError = 
        let res = tcr.nlr.TryDeref canError
        match res with 
        | ValueSome r -> 
             tcr.binding <- nullableSlotFull r 
        | ValueNone -> 
             ()

    /// Dereference the TyconRef to a Tycon. Amortize the cost of doing this.
    /// This path should not allocate in the amortized case
    member tcr.Deref = 
        match box tcr.binding with 
        | null ->
            tcr.Resolve(canError=true)
            match box tcr.binding with 
            | null -> error (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefModuleNamespaceType, String.concat "." tcr.nlr.EnclosingMangledPath, tcr.nlr.AssemblyName, tcr.nlr.LastItemMangledName))
            | _ -> tcr.binding
        | _ -> 
            tcr.binding

    /// Dereference the TyconRef to a Tycon option.
    member tcr.TryDeref = 
        match box tcr.binding with 
        | null -> 
            tcr.Resolve(canError=false)
            match box tcr.binding with 
            | null -> ValueNone
            | _ -> ValueSome tcr.binding

        | _ -> 
            ValueSome tcr.binding

    /// Is the destination assembly available?
    member tcr.CanDeref = tcr.TryDeref.IsSome

    /// Gets the data indicating the compiled representation of a type or module in terms of Abstract IL data structures.
    member x.CompiledRepresentation = x.Deref.CompiledRepresentation

    /// Gets the data indicating the compiled representation of a named type or module in terms of Abstract IL data structures.
    member x.CompiledRepresentationForNamedType = x.Deref.CompiledRepresentationForNamedType

    /// The implementation definition location of the namespace, module or type
    member x.DefinitionRange = x.Deref.DefinitionRange

    /// The signature definition location of the namespace, module or type
    member x.SigRange = x.Deref.SigRange

    /// The name of the namespace, module or type, possibly with mangling, e.g. List`1, List or FailureException 
    member x.LogicalName = x.Deref.LogicalName

    /// The compiled name of the namespace, module or type, e.g. FSharpList`1, ListModule or FailureException 
    member x.CompiledName = x.Deref.CompiledName

    /// The display name of the namespace, module or type, e.g. List instead of List`1, not including static parameters
    member x.DisplayName = x.Deref.DisplayName

    /// The display name of the namespace, module or type with <'T, 'U, 'V> added for generic types, including static parameters
    member x.DisplayNameWithStaticParametersAndTypars = x.Deref.DisplayNameWithStaticParametersAndTypars

    /// The display name of the namespace, module or type with <_, _, _> added for generic types, including static parameters
    member x.DisplayNameWithStaticParametersAndUnderscoreTypars = x.Deref.DisplayNameWithStaticParametersAndUnderscoreTypars

    /// The display name of the namespace, module or type, e.g. List instead of List`1, including static parameters
    member x.DisplayNameWithStaticParameters = x.Deref.DisplayNameWithStaticParameters

    /// The code location where the module, namespace or type is defined.
    member x.Range = x.Deref.Range

    /// A unique stamp for this module, namespace or type definition within the context of this compilation. 
    /// Note that because of signatures, there are situations where in a single compilation the "same" 
    /// module, namespace or type may have two distinct Entity objects that have distinct stamps.
    member x.Stamp = x.Deref.Stamp

    /// The F#-defined custom attributes of the entity, if any. If the entity is backed by Abstract IL or provided metadata
    /// then this does not include any attributes from those sources.
    member x.Attribs = x.Deref.Attribs

    /// The XML documentation of the entity, if any. If the entity is backed by provided metadata
    /// then this _does_ include this documentation. If the entity is backed by Abstract IL metadata
    /// or comes from another F# assembly then it does not (because the documentation will get read from 
    /// an XML file).
    member x.XmlDoc = x.Deref.XmlDoc

    /// The XML documentation sig-string of the entity, if any, to use to lookup an .xml doc file. This also acts
    /// as a cache for this sig-string computation.
    member x.XmlDocSig = x.Deref.XmlDocSig

    /// The logical contents of the entity when it is a module or namespace fragment.
    member x.ModuleOrNamespaceType = x.Deref.ModuleOrNamespaceType
    
    /// Demangle the module name, if FSharpModuleWithSuffix is used
    member x.DemangledModuleOrNamespaceName = x.Deref.DemangledModuleOrNamespaceName

    /// The logical contents of the entity when it is a type definition.
    member x.TypeContents = x.Deref.TypeContents

    /// The kind of the type definition - is it a measure definition or a type definition?
    member x.TypeOrMeasureKind = x.Deref.TypeOrMeasureKind

    /// The identifier at the point of declaration of the type definition.
    member x.Id = x.Deref.Id

    /// The information about the r.h.s. of a type definition, if any. For example, the r.h.s. of a union or record type.
    member x.TypeReprInfo = x.Deref.TypeReprInfo

    /// The information about the r.h.s. of an F# exception definition, if any. 
    member x.ExceptionInfo = x.Deref.ExceptionInfo

    /// Indicates if the entity represents an F# exception declaration.
    member x.IsExceptionDecl = x.Deref.IsExceptionDecl
    
    /// Get the type parameters for an entity that is a type declaration, otherwise return the empty list.
    /// 
    /// Lazy because it may read metadata, must provide a context "range" in case error occurs reading metadata.
    member x.Typars m = x.Deref.Typars m

    /// Get the type parameters for an entity that is a type declaration, otherwise return the empty list.
    member x.TyparsNoRange = x.Deref.TyparsNoRange

    /// Indicates if this entity is an F# type abbreviation definition
    member x.TypeAbbrev = x.Deref.TypeAbbrev

    /// Indicates if this entity is an F# type abbreviation definition
    member x.IsTypeAbbrev = x.Deref.IsTypeAbbrev

    /// Get the value representing the accessibility of the r.h.s. of an F# type definition.
    member x.TypeReprAccessibility = x.Deref.TypeReprAccessibility

    /// Get the cache of the compiled ILTypeRef representation of this module or type.
    member x.CompiledReprCache = x.Deref.CompiledReprCache

    /// Get a blob of data indicating how this type is nested in other namespaces, modules or types.
    member x.PublicPath: PublicPath option = x.Deref.PublicPath

    /// Get the value representing the accessibility of an F# type definition or module.
    member x.Accessibility = x.Deref.Accessibility

    /// Indicates the type prefers the "tycon<a, b>" syntax for display etc. 
    member x.IsPrefixDisplay = x.Deref.IsPrefixDisplay

    /// Indicates the "tycon blob" is actually a module 
    member x.IsModuleOrNamespace = x.Deref.IsModuleOrNamespace

    /// Indicates if the entity is a namespace
    member x.IsNamespace = x.Deref.IsNamespace

    /// Indicates if the entity is an F# module definition
    member x.IsModule = x.Deref.IsModule

    /// Get a blob of data indicating how this type is nested inside other namespaces, modules and types.
    member x.CompilationPathOpt = x.Deref.CompilationPathOpt

#if !NO_EXTENSIONTYPING
    /// Indicates if the entity is a provided namespace fragment
    member x.IsProvided = x.Deref.IsProvided

    /// Indicates if the entity is a provided namespace fragment
    member x.IsProvidedNamespace = x.Deref.IsProvidedNamespace

    /// Indicates if the entity is an erased provided type definition
    member x.IsProvidedErasedTycon = x.Deref.IsProvidedErasedTycon

    /// Indicates if the entity is an erased provided type definition that incorporates a static instantiation (and therefore in some sense compiler generated)
    member x.IsStaticInstantiationTycon = x.Deref.IsStaticInstantiationTycon

    /// Indicates if the entity is a generated provided type definition, i.e. not erased.
    member x.IsProvidedGeneratedTycon = x.Deref.IsProvidedGeneratedTycon
#endif

    /// Get a blob of data indicating how this type is nested inside other namespaces, modules and types.
    member x.CompilationPath = x.Deref.CompilationPath

    /// Get a table of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldTable = x.Deref.AllFieldTable

    /// Get an array of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldsArray = x.Deref.AllFieldsArray

    /// Get a list of fields for all the F#-defined record, struct and class fields in this type definition, including
    /// static fields, 'val' declarations and hidden fields from the compilation of implicit class constructions.
    member x.AllFieldsAsList = x.Deref.AllFieldsAsList

    /// Get a list of all fields for F#-defined record, struct and class fields in this type definition,
    /// including static fields, but excluding compiler-generate fields.
    member x.TrueFieldsAsList = x.Deref.TrueFieldsAsList

    /// Get a list of all instance fields for F#-defined record, struct and class fields in this type definition,
    /// excluding compiler-generate fields.
    member x.TrueInstanceFieldsAsList = x.Deref.TrueInstanceFieldsAsList

    /// Get a list of all instance fields for F#-defined record, struct and class fields in this type definition.
    /// including hidden fields from the compilation of implicit class constructions.
    member x.AllInstanceFieldsAsList = x.Deref.AllInstanceFieldsAsList

    /// Get a field by index in definition order
    member x.GetFieldByIndex n = x.Deref.GetFieldByIndex n

    /// Get a field by name.
    member x.GetFieldByName n = x.Deref.GetFieldByName n

    /// Get the union cases and other union-type information for a type, if any
    member x.UnionTypeInfo = x.Deref.UnionTypeInfo

    /// Get the union cases for a type, if any
    member x.UnionCasesArray = x.Deref.UnionCasesArray

    /// Get the union cases for a type, if any, as a list
    member x.UnionCasesAsList = x.Deref.UnionCasesAsList

    /// Get a union case of a type by name
    member x.GetUnionCaseByName n = x.Deref.GetUnionCaseByName n

    /// Get the blob of information associated with an F# object-model type definition, i.e. class, interface, struct etc.
    member x.FSharpObjectModelTypeInfo = x.Deref.FSharpObjectModelTypeInfo

    /// Gets the immediate interface definitions of an F# type definition. Further interfaces may be supported through class and interface inheritance.
    member x.ImmediateInterfacesOfFSharpTycon = x.Deref.ImmediateInterfacesOfFSharpTycon

    /// Gets the immediate interface types of an F# type definition. Further interfaces may be supported through class and interface inheritance.
    member x.ImmediateInterfaceTypesOfFSharpTycon = x.Deref.ImmediateInterfaceTypesOfFSharpTycon

    /// Gets the immediate members of an F# type definition, excluding compiler-generated ones.
    /// Note: result is alphabetically sorted, then for each name the results are in declaration order
    member x.MembersOfFSharpTyconSorted = x.Deref.MembersOfFSharpTyconSorted

    /// Gets all immediate members of an F# type definition keyed by name, including compiler-generated ones.
    /// Note: result is a indexed table, and for each name the results are in reverse declaration order
    member x.MembersOfFSharpTyconByName = x.Deref.MembersOfFSharpTyconByName

    /// Indicates if this is a struct or enum type definition, i.e. a value type definition
    member x.IsStructOrEnumTycon = x.Deref.IsStructOrEnumTycon

    /// Indicates if this is an F# type definition which is one of the special types in FSharp.Core.dll which uses 
    /// an assembly-code representation for the type, e.g. the primitive array type constructor.
    member x.IsAsmReprTycon = x.Deref.IsAsmReprTycon

    /// Indicates if this is an F# type definition which is one of the special types in FSharp.Core.dll like 'float<_>' which
    /// defines a measure type with a relation to an existing non-measure type as a representation.
    member x.IsMeasureableReprTycon = x.Deref.IsMeasureableReprTycon

    /// Indicates if the entity is erased, either a measure definition, or an erased provided type definition
    member x.IsErased = x.Deref.IsErased
    
    /// Gets any implicit hash/equals (with comparer argument) methods added to an F# record, union or struct type definition.
    member x.GeneratedHashAndEqualsWithComparerValues = x.Deref.GeneratedHashAndEqualsWithComparerValues

    /// Gets any implicit CompareTo (with comparer argument) methods added to an F# record, union or struct type definition.
    member x.GeneratedCompareToWithComparerValues = x.Deref.GeneratedCompareToWithComparerValues

    /// Gets any implicit CompareTo methods added to an F# record, union or struct type definition.
    member x.GeneratedCompareToValues = x.Deref.GeneratedCompareToValues

    /// Gets any implicit hash/equals methods added to an F# record, union or struct type definition.
    member x.GeneratedHashAndEqualsValues = x.Deref.GeneratedHashAndEqualsValues
    
    /// Indicate if this is a type definition backed by Abstract IL metadata.
    member x.IsILTycon = x.Deref.IsILTycon

    /// Get the Abstract IL scope, nesting and metadata for this 
    /// type definition, assuming it is backed by Abstract IL metadata.
    member x.ILTyconInfo = x.Deref.ILTyconInfo

    /// Get the Abstract IL metadata for this type definition, assuming it is backed by Abstract IL metadata.
    member x.ILTyconRawMetadata = x.Deref.ILTyconRawMetadata

    /// Indicate if this is a type whose r.h.s. is known to be a union type definition.
    member x.IsUnionTycon = x.Deref.IsUnionTycon

    /// Indicates if this is an F# type definition whose r.h.s. is known to be a record type definition.
    member x.IsRecordTycon = x.Deref.IsRecordTycon

    /// Indicates if this is an F# type definition whose r.h.s. is known to be some kind of F# object model definition
    member x.IsFSharpObjectModelTycon = x.Deref.IsFSharpObjectModelTycon

    /// The on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.TryIsByRefLike = x.Deref.TryIsByRefLike

    /// Set the on-demand analysis about whether the entity has the IsByRefLike attribute
    member x.SetIsByRefLike b = x.Deref.SetIsByRefLike b

    /// The on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.TryIsReadOnly = x.Deref.TryIsReadOnly

    /// Set the on-demand analysis about whether the entity has the IsReadOnly attribute
    member x.SetIsReadOnly b = x.Deref.SetIsReadOnly b

    /// The on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.TryIsAssumedReadOnly = x.Deref.TryIsAssumedReadOnly

    /// Set the on-demand analysis about whether the entity is assumed to be a readonly struct
    member x.SetIsAssumedReadOnly b = x.Deref.SetIsAssumedReadOnly b

    /// Indicates if this is an F# type definition whose r.h.s. definition is unknown (i.e. a traditional ML 'abstract' type in a signature,
    /// which in F# is called a 'unknown representation' type).
    member x.IsHiddenReprTycon = x.Deref.IsHiddenReprTycon

    /// Indicates if this is an F#-defined interface type definition 
    member x.IsFSharpInterfaceTycon = x.Deref.IsFSharpInterfaceTycon

    /// Indicates if this is an F#-defined delegate type definition 
    member x.IsFSharpDelegateTycon = x.Deref.IsFSharpDelegateTycon

    /// Indicates if this is an F#-defined enum type definition 
    member x.IsFSharpEnumTycon = x.Deref.IsFSharpEnumTycon

    /// Indicates if this is a .NET-defined enum type definition 
    member x.IsILEnumTycon = x.Deref.IsILEnumTycon

    /// Indicates if this is an enum type definition 
    member x.IsEnumTycon = x.Deref.IsEnumTycon

    /// Indicates if this is an F#-defined struct or enum type definition, i.e. a value type definition
    member x.IsFSharpStructOrEnumTycon = x.Deref.IsFSharpStructOrEnumTycon

    /// Indicates if this is a .NET-defined struct or enum type definition, i.e. a value type definition
    member x.IsILStructOrEnumTycon = x.Deref.IsILStructOrEnumTycon

    /// Indicates if we have pre-determined that a type definition has a default constructor.
    member x.PreEstablishedHasDefaultConstructor = x.Deref.PreEstablishedHasDefaultConstructor

    /// Indicates if we have pre-determined that a type definition has a self-referential constructor using 'as x'
    member x.HasSelfReferentialConstructor = x.Deref.HasSelfReferentialConstructor

    member x.UnionCasesAsRefList = x.UnionCasesAsList |> List.map x.MakeNestedUnionCaseRef

    member x.TrueInstanceFieldsAsRefList = x.TrueInstanceFieldsAsList |> List.map x.MakeNestedRecdFieldRef

    member x.AllFieldAsRefList = x.AllFieldsAsList |> List.map x.MakeNestedRecdFieldRef

    member x.MakeNestedRecdFieldRef (rf: RecdField) = RecdFieldRef (x, rf.Name)

    member x.MakeNestedUnionCaseRef (uc: UnionCase) = UnionCaseRef (x, uc.Id.idText)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = 
       if x.IsLocalRef then 
           x.ResolvedTarget.DisplayName 
       else 
           x.nlr.DisplayName 

/// Represents a module-or-namespace reference in the typed abstract syntax.
type ModuleOrNamespaceRef = EntityRef

/// Represents a type definition reference in the typed abstract syntax.
type TyconRef = EntityRef

/// References are either local or nonlocal
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ValRef = 
    {
      /// Indicates a reference to something bound in this CCU 
      mutable binding: NonNullSlot<Val>

      /// Indicates a reference to something bound in another CCU 
      nlr: NonLocalValOrMemberRef
    }

    member x.IsLocalRef = obj.ReferenceEquals(x.nlr, null)

    member x.IsResolved = not (obj.ReferenceEquals(x.binding, null))

    member x.ResolvedTarget = x.binding

    /// Dereference the ValRef to a Val.
    member vr.Deref = 
        if obj.ReferenceEquals(vr.binding, null) then
            let res = 
                let nlr = vr.nlr 
                let e = nlr.EnclosingEntity.Deref 
                let possible = e.ModuleOrNamespaceType.TryLinkVal(nlr.EnclosingEntity.nlr.Ccu, nlr.ItemKey)
                match possible with 
                | ValueNone -> error (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefVal, e.DisplayNameWithStaticParameters, nlr.AssemblyName, sprintf "%+A" nlr.ItemKey.PartialKey))
                | ValueSome h -> h
            vr.binding <- nullableSlotFull res 
            res 
        else vr.binding

    /// Dereference the ValRef to a Val option.
    member vr.TryDeref = 
        if obj.ReferenceEquals(vr.binding, null) then
            let resOpt = 
                match vr.nlr.EnclosingEntity.TryDeref with 
                | ValueNone -> ValueNone
                | ValueSome e -> e.ModuleOrNamespaceType.TryLinkVal(vr.nlr.EnclosingEntity.nlr.Ccu, vr.nlr.ItemKey)
            match resOpt with 
            | ValueNone -> ()
            | ValueSome res -> 
                vr.binding <- nullableSlotFull res 
            resOpt
        else ValueSome vr.binding

    /// The type of the value. May be a TType_forall for a generic value. 
    /// May be a type variable or type containing type variables during type inference. 
    member x.Type = x.Deref.Type

    /// Get the type of the value including any generic type parameters
    member x.TypeScheme = x.Deref.TypeScheme

    /// Get the type of the value after removing any generic type parameters
    member x.TauType = x.Deref.TauType

    member x.Typars = x.Deref.Typars

    member x.LogicalName = x.Deref.LogicalName

    member x.DisplayName = x.Deref.DisplayName

    member x.CoreDisplayName = x.Deref.CoreDisplayName

    member x.Range = x.Deref.Range

    /// Get the value representing the accessibility of an F# type definition or module.
    member x.Accessibility = x.Deref.Accessibility

    /// The parent type or module, if any (None for expression bindings and parameters)
    member x.DeclaringEntity = x.Deref.DeclaringEntity

    /// Get the apparent parent entity for the value, i.e. the entity under with which the
    /// value is associated. For extension members this is the nominal type the member extends.
    /// For other values it is just the actual parent.
    member x.ApparentEnclosingEntity = x.Deref.ApparentEnclosingEntity

    member x.DefinitionRange = x.Deref.DefinitionRange

    member x.SigRange = x.Deref.SigRange

    /// The value of a value or member marked with [<LiteralAttribute>] 
    member x.LiteralValue = x.Deref.LiteralValue

    member x.Id = x.Deref.Id

    /// Get the name of the value, assuming it is compiled as a property.
    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the property implemented by the abstract slot
    member x.PropertyName = x.Deref.PropertyName

    /// Indicates whether this value represents a property getter.
    member x.IsPropertyGetterMethod = 
        match x.MemberInfo with
        | None -> false
        | Some (memInfo: ValMemberInfo) -> memInfo.MemberFlags.MemberKind = SynMemberKind.PropertyGet || memInfo.MemberFlags.MemberKind = SynMemberKind.PropertyGetSet

    /// Indicates whether this value represents a property setter.
    member x.IsPropertySetterMethod = 
        match x.MemberInfo with
        | None -> false
        | Some (memInfo: ValMemberInfo) -> memInfo.MemberFlags.MemberKind = SynMemberKind.PropertySet || memInfo.MemberFlags.MemberKind = SynMemberKind.PropertyGetSet

    /// A unique stamp within the context of this invocation of the compiler process 
    member x.Stamp = x.Deref.Stamp

    /// Is this represented as a "top level" static binding (i.e. a static field, static member,
    /// instance member), rather than an "inner" binding that may result in a closure.
    member x.IsCompiledAsTopLevel = x.Deref.IsCompiledAsTopLevel

    /// Indicates if this member is an F#-defined dispatch slot.
    member x.IsDispatchSlot = x.Deref.IsDispatchSlot

    /// The name of the method in compiled code (with some exceptions where ilxgen.fs decides not to use a method impl)
    member x.CompiledName = x.Deref.CompiledName

    /// Get the public path to the value, if any? Should be set if and only if
    /// IsMemberOrModuleBinding is set.
    member x.PublicPath = x.Deref.PublicPath

    /// The quotation expression associated with a value given the [<ReflectedDefinition>] tag
    member x.ReflectedDefinition = x.Deref.ReflectedDefinition

    /// Indicates if this is an F#-defined 'new' constructor member
    member x.IsConstructor = x.Deref.IsConstructor

    /// Indicates if this value was a member declared 'override' or an implementation of an interface slot
    member x.IsOverrideOrExplicitImpl = x.Deref.IsOverrideOrExplicitImpl

    /// Is this a member, if so some more data about the member.
    member x.MemberInfo = x.Deref.MemberInfo

    /// Indicates if this is a member
    member x.IsMember = x.Deref.IsMember

    /// Indicates if this is an F#-defined value in a module, or an extension member, but excluding compiler generated bindings from optimizations
    member x.IsModuleBinding = x.Deref.IsModuleBinding

    /// Indicates if this is an F#-defined instance member. 
    ///
    /// Note, the value may still be (a) an extension member or (b) and abstract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.IsInstanceMember = x.Deref.IsInstanceMember

    /// Indicates if this value is declared 'mutable'
    member x.IsMutable = x.Deref.IsMutable

    /// Indicates if this value allows the use of an explicit type instantiation (i.e. does it itself have explicit type arguments,
    /// or does it have a signature?)
    member x.PermitsExplicitTypeInstantiation = x.Deref.PermitsExplicitTypeInstantiation

    /// Indicates if this is inferred to be a method or function that definitely makes no critical tailcalls?
    member x.MakesNoCriticalTailcalls = x.Deref.MakesNoCriticalTailcalls

    /// Is this a member definition or module definition?
    member x.IsMemberOrModuleBinding = x.Deref.IsMemberOrModuleBinding

    /// Indicates if this is an F#-defined extension member
    member x.IsExtensionMember = x.Deref.IsExtensionMember

    /// Indicates if this is a constructor member generated from the de-sugaring of implicit constructor for a class type?
    member x.IsIncrClassConstructor = x.Deref.IsIncrClassConstructor

    /// Indicates if this is a member generated from the de-sugaring of 'let' function bindings in the implicit class syntax?
    member x.IsIncrClassGeneratedMember = x.Deref.IsIncrClassGeneratedMember

    /// Get the information about a recursive value used during type inference
    member x.RecursiveValInfo = x.Deref.RecursiveValInfo

    /// Indicates if this is a 'base' or 'this' value?
    member x.BaseOrThisInfo = x.Deref.BaseOrThisInfo

    ///  Indicates if this value was declared to be a type function, e.g. "let f<'a> = typeof<'a>"
    member x.IsTypeFunction = x.Deref.IsTypeFunction

    /// Records the "extra information" for a value compiled as a method.
    ///
    /// This indicates the number of arguments in each position for a curried function.
    member x.ValReprInfo = x.Deref.ValReprInfo

    /// Get the inline declaration on the value
    member x.InlineInfo = x.Deref.InlineInfo

    /// Indicates whether the inline declaration for the value indicate that the value must be inlined?
    member x.MustInline = x.Deref.MustInline

    /// Indicates whether this value was generated by the compiler.
    ///
    /// Note: this is true for the overrides generated by hash/compare augmentations
    member x.IsCompilerGenerated = x.Deref.IsCompilerGenerated

    /// Get the declared attributes for the value
    member x.Attribs = x.Deref.Attribs

    /// Get the declared documentation for the value
    member x.XmlDoc = x.Deref.XmlDoc

    /// Get or set the signature for the value's XML documentation
    member x.XmlDocSig = x.Deref.XmlDocSig

    /// Get the actual parent entity for the value (a module or a type), i.e. the entity under which the
    /// value will appear in compiled code. For extension members this is the module where the extension member
    /// is declared.
    member x.TopValDeclaringEntity = x.Deref.TopValDeclaringEntity

    // Can be false for members after error recovery
    member x.HasDeclaringEntity = x.Deref.HasDeclaringEntity

    /// Get the apparent parent entity for a member
    member x.MemberApparentEntity = x.Deref.MemberApparentEntity

    /// Get the number of 'this'/'self' object arguments for the member. Instance extension members return '1'.
    member x.NumObjArgs = x.Deref.NumObjArgs

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = 
       if x.IsLocalRef then x.ResolvedTarget.DisplayName 
       else x.nlr.ToString()

/// Represents a reference to a case of a union type
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type UnionCaseRef = 
    | UnionCaseRef of TyconRef * string

    /// Get a reference to the type containing this union case
    member x.TyconRef = let (UnionCaseRef(tcref, _)) = x in tcref

    /// Get the name of this union case
    member x.CaseName = let (UnionCaseRef(_, nm)) = x in nm

    /// Get the Entity for the type containing this union case
    member x.Tycon = x.TyconRef.Deref

    /// Dereference the reference to the union case
    member x.UnionCase = 
        match x.TyconRef.GetUnionCaseByName x.CaseName with 
        | Some res -> res
        | None -> error(InternalError(sprintf "union case %s not found in type %s" x.CaseName x.TyconRef.LogicalName, x.TyconRef.Range))

    /// Try to dereference the reference 
    member x.TryUnionCase =
        x.TyconRef.TryDeref 
        |> ValueOptionInternal.bind (fun tcref -> tcref.GetUnionCaseByName x.CaseName |> ValueOptionInternal.ofOption)

    /// Get the attributes associated with the union case
    member x.Attribs = x.UnionCase.Attribs

    /// Get the range of the union case
    member x.Range = x.UnionCase.Range

    /// Get the definition range of the union case
    member x.DefinitionRange = x.UnionCase.DefinitionRange

    /// Get the signature range of the union case
    member x.SigRange = x.UnionCase.SigRange

    /// Get the index of the union case amongst the cases
    member x.Index = 
        try 
           // REVIEW: this could be faster, e.g. by storing the index in the NameMap 
            x.TyconRef.UnionCasesArray |> Array.findIndex (fun ucspec -> ucspec.DisplayName = x.CaseName) 
        with :? KeyNotFoundException -> 
            error(InternalError(sprintf "union case %s not found in type %s" x.CaseName x.TyconRef.LogicalName, x.TyconRef.Range))

    /// Get the fields of the union case
    member x.AllFieldsAsList = x.UnionCase.FieldTable.AllFieldsAsList

    /// Get the resulting type of the union case
    member x.ReturnType = x.UnionCase.ReturnType

    /// Get a field of the union case by index
    member x.FieldByIndex n = x.UnionCase.FieldTable.FieldByIndex n

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.CaseName

/// Represents a reference to a field in a record, class or struct
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type RecdFieldRef = 
    | RecdFieldRef of tcref: TyconRef * id: string

    /// Get a reference to the type containing this union case
    member x.TyconRef = let (RecdFieldRef(tcref, _)) = x in tcref

    /// Get the name off the field
    member x.FieldName = let (RecdFieldRef(_, id)) = x in id

    /// Get the Entity for the type containing this union case
    member x.Tycon = x.TyconRef.Deref

    /// Dereference the reference 
    member x.RecdField = 
        let (RecdFieldRef(tcref, id)) = x
        match tcref.GetFieldByName id with 
        | Some res -> res
        | None -> error(InternalError(sprintf "field %s not found in type %s" id tcref.LogicalName, tcref.Range))

    /// Try to dereference the reference 
    member x.TryRecdField = 
        x.TyconRef.TryDeref 
        |> ValueOptionInternal.bind (fun tcref -> tcref.GetFieldByName x.FieldName |> ValueOptionInternal.ofOption)

    /// Get the attributes associated with the compiled property of the record field 
    member x.PropertyAttribs = x.RecdField.PropertyAttribs

    /// Get the declaration range of the record field 
    member x.Range = x.RecdField.Range

    /// Get the definition range of the record field 
    member x.DefinitionRange = x.RecdField.DefinitionRange

    /// Get the signature range of the record field 
    member x.SigRange = x.RecdField.SigRange

    member x.Index =
        let (RecdFieldRef(tcref, id)) = x
        try 
            // REVIEW: this could be faster, e.g. by storing the index in the NameMap 
            tcref.AllFieldsArray |> Array.findIndex (fun rfspec -> rfspec.Name = id)  
        with :? KeyNotFoundException -> 
            error(InternalError(sprintf "field %s not found in type %s" id tcref.LogicalName, tcref.Range))

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = x.FieldName

/// Represents a type in the typed abstract syntax
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TType =

    /// TType_forall(typars, bodyTy).
    ///
    /// Indicates the type is a universal type, only used for types of values and members 
    | TType_forall of typars: Typars * bodyTy: TType

    /// TType_app(tyconRef, typeInstantiation).
    ///
    /// Indicates the type is built from a named type and a number of type arguments
    | TType_app of tyconRef: TyconRef * typeInstantiation: TypeInst

    /// TType_anon
    ///
    /// Indicates the type is an anonymous record type whose compiled representation is located in the given assembly
    | TType_anon of anonInfo: AnonRecdTypeInfo * tys: TType list

    /// TType_tuple(elementTypes).
    ///
    /// Indicates the type is a tuple type. elementTypes must be of length 2 or greater.
    | TType_tuple of tupInfo: TupInfo * elementTypes: TTypes

    /// TType_fun(domainType, rangeType).
    ///
    /// Indicates the type is a function type 
    | TType_fun of domainType: TType * rangeType: TType

    /// TType_ucase(unionCaseRef, typeInstantiation)
    ///
    /// Indicates the type is a non-F#-visible type representing a "proof" that a union value belongs to a particular union case
    /// These types are not user-visible and will never appear as an inferred type. They are the types given to
    /// the temporaries arising out of pattern matching on union values.
    | TType_ucase of unionCaseRef: UnionCaseRef * typeInstantiation: TypeInst

    /// Indicates the type is a variable type, whether declared, generalized or an inference type parameter  
    | TType_var of typar: Typar 

    /// Indicates the type is a unit-of-measure expression being used as an argument to a type or member
    | TType_measure of measure: Measure

    /// For now, used only as a discriminant in error message.
    /// See https://github.com/Microsoft/visualfsharp/issues/2561
    member x.GetAssemblyName() =
        match x with
        | TType_forall (_tps, ty) -> ty.GetAssemblyName()
        | TType_app (tcref, _tinst) -> tcref.CompilationPath.ILScopeRef.QualifiedName
        | TType_tuple (_tupInfo, _tinst) -> ""
        | TType_anon (anonInfo, _tinst) -> defaultArg anonInfo.Assembly.QualifiedName ""
        | TType_fun (_d, _r) -> ""
        | TType_measure _ms -> ""
        | TType_var tp -> tp.Solution |> function Some sln -> sln.GetAssemblyName() | None -> ""
        | TType_ucase (_uc, _tinst) ->
            let (TILObjectReprData(scope, _nesting, _definition)) = _uc.Tycon.ILTyconInfo
            scope.QualifiedName

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() =  
        match x with 
        | TType_forall (_tps, ty) -> "forall ... " + ty.ToString()
        | TType_app (tcref, tinst) -> tcref.DisplayName + (match tinst with [] -> "" | tys -> "<" + String.concat "," (List.map string tys) + ">")
        | TType_tuple (tupInfo, tinst) -> 
            (match tupInfo with 
             | TupInfo.Const false -> ""
             | TupInfo.Const true -> "struct ")
             + String.concat "," (List.map string tinst) 
        | TType_anon (anonInfo, tinst) -> 
            (match anonInfo.TupInfo with 
             | TupInfo.Const false -> ""
             | TupInfo.Const true -> "struct ")
             + "{|" + String.concat "," (Seq.map2 (fun nm ty -> nm + " " + string ty + ";") anonInfo.SortedNames tinst) + ")" + "|}"
        | TType_fun (d, r) -> "(" + string d + " -> " + string r + ")"
        | TType_ucase (uc, tinst) -> "ucase " + uc.CaseName + (match tinst with [] -> "" | tys -> "<" + String.concat "," (List.map string tys) + ">")
        | TType_var tp -> 
            match tp.Solution with 
            | None -> tp.DisplayName
            | Some _ -> tp.DisplayName + " (solved)"
        | TType_measure ms -> ms.ToString()

type TypeInst = TType list 

type TTypes = TType list 

/// Represents the information identifying an anonymous record 
[<RequireQualifiedAccess>] 
type AnonRecdTypeInfo = 
    {
      // Mutability for pickling/unpickling only
      mutable Assembly: CcuThunk 

      mutable TupInfo: TupInfo

      mutable SortedIds: Ident[]

      mutable Stamp: Stamp

      mutable SortedNames: string[]
    }

    /// Create an AnonRecdTypeInfo from the basic data
    static member Create(ccu: CcuThunk, tupInfo, ids: Ident[]) = 
        let sortedIds = ids |> Array.sortBy (fun id -> id.idText)
        // Hash all the data to form a unique stamp
        let stamp = 
            sha1HashInt64 
                [| for c in ccu.AssemblyName do yield byte c; yield byte (int32 c >>> 8)
                   match tupInfo with 
                   | TupInfo.Const b -> yield (if b then 0uy else 1uy)
                   for id in sortedIds do 
                       for c in id.idText do yield byte c; yield byte (int32 c >>> 8) |]
        let sortedNames = Array.map textOfId sortedIds
        { Assembly = ccu; TupInfo = tupInfo; SortedIds = sortedIds; Stamp = stamp; SortedNames = sortedNames }

    /// Get the ILTypeRef for the generated type implied by the anonymous type
    member x.ILTypeRef = 
        let ilTypeName = sprintf "<>f__AnonymousType%s%u`%d" (match x.TupInfo with TupInfo.Const b -> if b then "1000" else "") (uint32 x.Stamp) x.SortedIds.Length
        mkILTyRef(x.Assembly.ILScopeRef, ilTypeName)

    static member NewUnlinked() : AnonRecdTypeInfo = 
        { Assembly = Unchecked.defaultof<_>
          TupInfo = Unchecked.defaultof<_>
          SortedIds = Unchecked.defaultof<_>
          Stamp = Unchecked.defaultof<_> 
          SortedNames = Unchecked.defaultof<_> }

    member x.Link d = 
        let sortedNames = Array.map textOfId d.SortedIds
        x.Assembly <- d.Assembly
        x.TupInfo <- d.TupInfo
        x.SortedIds <- d.SortedIds
        x.Stamp <- d.Stamp
        x.SortedNames <- sortedNames

    member x.IsLinked = (match x.SortedIds with null -> true | _ -> false)
    
[<RequireQualifiedAccess>] 
type TupInfo = 
    /// Some constant, e.g. true or false for tupInfo
    | Const of bool

/// Represents a unit of measure in the typed AST
[<RequireQualifiedAccess (* ; StructuredFormatDisplay("{DebugText}") *) >]
type Measure = 

    /// A variable unit-of-measure
    | Var of typar: Typar

    /// A constant, leaf unit-of-measure such as 'kg' or 'm'
    | Con of tyconRef: TyconRef

    /// A product of two units of measure
    | Prod of measure1: Measure * measure2: Measure

    /// An inverse of a units of measure expression
    | Inv of measure: Measure

    /// The unit of measure '1', e.g. float = float<1>
    | One

    /// Raising a measure to a rational power 
    | RationalPower of measure: Measure * power: Rational

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()
    
    override x.ToString() = sprintf "%+A" x 

type Attribs = Attrib list 

[<NoEquality; NoComparison (* ; StructuredFormatDisplay("{DebugText}") *) >]
type AttribKind = 

    /// Indicates an attribute refers to a type defined in an imported .NET assembly 
    | ILAttrib of ilMethodRef: ILMethodRef 

    /// Indicates an attribute refers to a type defined in an imported F# assembly 
    | FSAttrib of valRef: ValRef

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// Attrib(tyconRef, kind, unnamedArgs, propVal, appliedToAGetterOrSetter, targetsOpt, range)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type Attrib = 

    | Attrib of
        tyconRef: TyconRef *
        kind: AttribKind *
        unnamedArgs: AttribExpr list *
        propVal: AttribNamedArg list *
        appliedToAGetterOrSetter: bool *
        targetsOpt: AttributeTargets option *
        range: range

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    member x.TyconRef = (let (Attrib(tcref, _, _, _, _, _, _)) = x in tcref)

    member x.Range = (let (Attrib(_, _, _, _, _, _, m)) = x in m)

    override x.ToString() = "attrib" + x.TyconRef.ToString()

/// We keep both source expression and evaluated expression around to help intellisense and signature printing
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type AttribExpr = 

    /// AttribExpr(source, evaluated)
    | AttribExpr of source: Expr * evaluated: Expr 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "AttribExpr(...)"

/// AttribNamedArg(name, type, isField, value)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type AttribNamedArg = 
    | AttribNamedArg of (string*TType*bool*AttribExpr)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "AttribNamedArg(...)"

/// Constants in expressions
[<RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type Const = 
    | Bool of bool
    | SByte of sbyte
    | Byte of byte
    | Int16 of int16
    | UInt16 of uint16
    | Int32 of int32
    | UInt32 of uint32
    | Int64 of int64
    | UInt64 of uint64
    | IntPtr of int64
    | UIntPtr of uint64
    | Single of single
    | Double of double
    | Char of char
    | String of string
    | Decimal of Decimal 
    | Unit
    | Zero // null/zero-bit-pattern 

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override c.ToString() =
        match c with
        | Bool b -> (if b then "true" else "false")
        | SByte x -> string x + "y"
        | Byte x -> string x + "uy"
        | Int16 x -> string x + "s"
        | UInt16 x -> string x + "us"
        | Int32 x -> string x
        | UInt32 x -> string x + "u"
        | Int64 x -> string x + "L"
        | UInt64 x -> string x + "UL"
        | IntPtr x -> string x + "n"
        | UIntPtr x -> string x + "un"
        | Single x -> string x + "f"
        | Double x -> string x
        | Char x -> "'" + string x + "'"
        | String  x -> "\"" + x + "\""
        | Decimal  x -> string x + "M"
        | Unit  -> "()"
        | Zero -> "Const.Zero"

/// Decision trees. Pattern matching has been compiled down to
/// a decision tree by this point. The right-hand-sides (actions) of
/// a decision tree by this point. The right-hand-sides (actions) of
/// the decision tree are labelled by integers that are unique for that
/// particular tree.
[<NoEquality; NoComparison (* ; StructuredFormatDisplay("{DebugText}") *) >]
type DecisionTree = 

    /// TDSwitch(input, cases, default, range)
    ///
    /// Indicates a decision point in a decision tree. 
    ///    input -- The expression being tested. If switching over a struct union this 
    ///             must be the address of the expression being tested.
    ///    cases -- The list of tests and their subsequent decision trees
    ///    default -- The default decision tree, if any
    ///    range -- (precise documentation needed)
    | TDSwitch of input: Expr * cases: DecisionTreeCase list * defaultOpt: DecisionTree option * range: range

    /// TDSuccess(results, targets)
    ///
    /// Indicates the decision tree has terminated with success, transferring control to the given target with the given parameters.
    ///    results -- the expressions to be bound to the variables at the target
    ///    target -- the target number for the continuation
    | TDSuccess of results: Exprs * targetNum: int  

    /// TDBind(binding, body)
    ///
    /// Bind the given value through the remaining cases of the dtree. 
    /// These arise from active patterns and some optimizations to prevent
    /// repeated computations in decision trees.
    ///    binding -- the value and the expression it is bound to
    ///    body -- the rest of the decision tree
    | TDBind of binding: Binding * body: DecisionTree

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// Represents a test and a subsequent decision tree
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type DecisionTreeCase = 
    | TCase of discriminator: DecisionTreeTest * caseTree: DecisionTree

    /// Get the discriminator associated with the case
    member x.Discriminator = let (TCase(d, _)) = x in d

    /// Get the decision tree or a successful test
    member x.CaseTree = let (TCase(_, d)) = x in d

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "DecisionTreeCase(...)"

[<NoEquality; NoComparison; RequireQualifiedAccess (*; StructuredFormatDisplay("{DebugText}") *) >]
type DecisionTreeTest = 
    /// Test if the input to a decision tree matches the given union case
    | UnionCase of caseRef: UnionCaseRef * tinst: TypeInst

    /// Test if the input to a decision tree is an array of the given length 
    | ArrayLength of length: int * ty: TType  

    /// Test if the input to a decision tree is the given constant value 
    | Const of value: Const

    /// Test if the input to a decision tree is null 
    | IsNull 

    /// IsInst(source, target)
    ///
    /// Test if the input to a decision tree is an instance of the given type 
    | IsInst of source: TType * target: TType

    /// Test.ActivePatternCase(activePatExpr, activePatResTys, isStructRetTy, activePatIdentity, idx, activePatInfo)
    ///
    /// Run the active pattern and bind a successful result to a 
    /// variable in the remaining tree. 
    ///     activePatExpr -- The active pattern function being called, perhaps applied to some active pattern parameters.
    ///     activePatResTys -- The result types (case types) of the active pattern.
    ///     isStructRetTy -- Is the active pattern a struct return
    ///     activePatIdentity -- The value and the types it is applied to. If there are any active pattern parameters then this is empty. 
    ///     idx -- The case number of the active pattern which the test relates to.
    ///     activePatternInfo -- The extracted info for the active pattern.
    | ActivePatternCase of
        activePatExpr: Expr *        
        activePatResTys: TTypes *
        isStructRetTy: bool *
        activePatIdentity: (ValRef * TypeInst) option *
        idx: int *
        activePatternInfo: ActivePatternInfo

    /// Used in error recovery
    | Error of range: range

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    //member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// A target of a decision tree. Can be thought of as a little function, though is compiled as a local block. 
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type DecisionTreeTarget = 
    | TTarget of Val list * Expr * DebugPointForTarget

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "DecisionTreeTarget(...)"

/// A collection of simultaneous bindings
type Bindings = Binding list

/// A binding of a variable to an expression, as in a `let` binding or similar
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type Binding = 
    | TBind of var: Val * expr: Expr * debugPoint: DebugPointAtBinding

    /// The value being bound
    member x.Var = (let (TBind(v, _, _)) = x in v)

    /// The expression the value is being bound to
    member x.Expr = (let (TBind(_, e, _)) = x in e)

    /// The information about whether to emit a sequence point for the binding
    member x.DebugPoint = (let (TBind(_, _, sp)) = x in sp)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() =  sprintf "TBind(%s, ...)" (x.Var.CompiledName None)

/// Represents a reference to an active pattern element. The 
/// integer indicates which choice in the target set is being selected by this item. 
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ActivePatternElemRef = 
    | APElemRef of activePatternInfo: ActivePatternInfo * activePatternVal: ValRef * caseIndex: int * isStructRetTy: bool

    /// Get the full information about the active pattern being referred to
    member x.ActivePatternInfo = (let (APElemRef(info, _, _, _)) = x in info)

    /// Get a reference to the value for the active pattern being referred to
    member x.ActivePatternVal = (let (APElemRef(_, vref, _, _)) = x in vref)

    /// Get a reference to the value for the active pattern being referred to
    member x.IsStructReturn = (let (APElemRef(_, _, _, isStructRetTy)) = x in isStructRetTy)

    /// Get the index of the active pattern element within the overall active pattern 
    member x.CaseIndex = (let (APElemRef(_, _, n, _)) = x in n)

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "ActivePatternElemRef(...)"

/// Records the "extra information" for a value compiled as a method (rather
/// than a closure or a local), including argument names, attributes etc.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ValReprInfo = 
    /// ValReprInfo (typars, args, result)
    | ValReprInfo of typars: TyparReprInfo list * args: ArgReprInfo list list * result: ArgReprInfo 

    /// Get the extra information about the arguments for the value
    member x.ArgInfos = (let (ValReprInfo(_, args, _)) = x in args)

    /// Get the number of curried arguments of the value
    member x.NumCurriedArgs = (let (ValReprInfo(_, args, _)) = x in args.Length)

    /// Get the number of type parameters of the value
    member x.NumTypars = (let (ValReprInfo(n, _, _)) = x in n.Length)

    /// Indicates if the value has no arguments - neither type parameters nor value arguments
    member x.HasNoArgs = (let (ValReprInfo(n, args, _)) = x in n.IsEmpty && args.IsEmpty)

    /// Get the number of tupled arguments in each curried argument position
    member x.AritiesOfArgs = (let (ValReprInfo(_, args, _)) = x in List.map List.length args)

    /// Get the kind of each type parameter
    member x.KindsOfTypars = (let (ValReprInfo(n, _, _)) = x in n |> List.map (fun (TyparReprInfo(_, k)) -> k))

    /// Get the total number of arguments 
    member x.TotalArgCount = 
        let (ValReprInfo(_, args, _)) = x
        // This is List.sumBy List.length args
        // We write this by hand as it can be a performance bottleneck in LinkagePartialKey
        let rec loop (args: ArgReprInfo list list) acc = 
            match args with 
            | [] -> acc 
            | [] :: t -> loop t acc 
            | [_] :: t -> loop t (acc+1) 
            | (_ :: _ :: h) :: t -> loop t (acc + h.Length + 2) 
        loop args 0

    member x.ArgNames =
        Some [ for argtys in x.ArgInfos do for arginfo in argtys do match arginfo.Name with None -> () | Some nm -> nm.idText ]

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "ValReprInfo(...)"

/// Records the "extra information" for an argument compiled as a real
/// method argument, specifically the argument name and attributes.
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type ArgReprInfo = 
    { 
      /// The attributes for the argument
      // MUTABILITY: used when propagating signature attributes into the implementation.
      mutable Attribs: Attribs 

      /// The name for the argument at this position, if any
      // MUTABILITY: used when propagating names of parameters from signature into the implementation.
      mutable Name: Ident option
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "ArgReprInfo(...)"

/// Records the extra metadata stored about typars for type parameters
/// compiled as "real" IL type parameters, specifically for values with 
/// ValReprInfo. Any information here is propagated from signature through
/// to the compiled code.
type TyparReprInfo = TyparReprInfo of Ident * TyparKind

type Typars = Typar list
 
type Exprs = Expr list

type Vals = Val list

/// Represents an expression in the typed abstract syntax
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type Expr =
    /// A constant expression. 
    | Const of
        value: Const *
        range: range *
        constType: TType

    /// Reference a value. The flag is only relevant if the value is an object model member 
    /// and indicates base calls and special uses of object constructors. 
    | Val of
        valRef: ValRef *
        flags: ValUseFlag *
        range: range

    /// Sequence expressions, used for "a;b", "let a = e in b;a" and "a then b" (the last an OO constructor). 
    | Sequential of
        expr1: Expr *
        expr2: Expr *
        kind: SequentialOpKind *
        debugPoint: DebugPointAtSequential *
        range: range

    /// Lambda expressions. 
    
    /// Why multiple vspecs? A Expr.Lambda taking multiple arguments really accepts a tuple. 
    /// But it is in a convenient form to be compile accepting multiple 
    /// arguments, e.g. if compiled as a toplevel static method. 
    | Lambda of
        unique: Unique *
        ctorThisValOpt: Val option *
        baseValOpt: Val option *
        valParams: Val list *
        bodyExpr: Expr * 
        range: range *
        overallType: TType

    /// Type lambdas. These are used for the r.h.s. of polymorphic 'let' bindings and 
    /// for expressions that implement first-class polymorphic values. 
    | TyLambda of
        unique: Unique *
        typeParams: Typars *
        bodyExpr: Expr *
        range: range *
        overallType: TType

    /// Applications.
    /// Applications combine type and term applications, and are normalized so 
    /// that sequential applications are combined, so "(f x y)" becomes "f [[x];[y]]". 
    /// The type attached to the function is the formal function type, used to ensure we don't build application 
    /// nodes that over-apply when instantiating at function types. 
    | App of
        funcExpr: Expr *
        formalType: TType *
        typeArgs: TypeInst *
        args: Exprs *
        range: range 

    /// Bind a recursive set of values. 
    | LetRec of
        bindings: Bindings *
        bodyExpr: Expr *
        range: range *
        frees: FreeVarsCache

    /// Bind a value. 
    | Let of
        binding: Binding *
        bodyExpr: Expr *
        range: range *
        frees: FreeVarsCache

    // Object expressions: A closure that implements an interface or a base type. 
    // The base object type might be a delegate type. 
    | Obj of 
        unique: Unique * 
        objTy: TType * (* <-- NOTE: specifies type parameters for base type *)
        baseVal: Val option * 
        ctorCall:  Expr * 
        overrides: ObjExprMethod list * 
        interfaceImpls: (TType * ObjExprMethod list) list *                   
        range: range

    /// Matches are a more complicated form of "let" with multiple possible destinations 
    /// and possibly multiple ways to get to each destination.  
    /// The first range is that of the expression being matched, which is used 
    /// as the range for all the decision making and binding that happens during the decision tree
    /// execution.
    | Match of
        debugPoint: DebugPointAtBinding *
        inputRange: range *
        decision: DecisionTree *
        targets: DecisionTreeTarget array *
        fullRange: range *
        exprType: TType

    /// If we statically know some information then in many cases we can use a more optimized expression 
    /// This is primarily used by terms in the standard library, particularly those implementing overloaded 
    /// operators. 
    | StaticOptimization of
        conditions: StaticOptimization list *
        expr: Expr *
        alternativeExpr: Expr *
        range: range

    /// An intrinsic applied to some (strictly evaluated) arguments 
    /// A few of intrinsics (TOp_try, TOp.While, TOp.For) expect arguments kept in a normal form involving lambdas 
    | Op of
        op: TOp *
        typeArgs: TypeInst *
        args: Exprs *
        range: range

    /// Indicates the expression is a quoted expression tree. 
    ///
    // MUTABILITY: this use of mutability is awkward and perhaps should be removed
    | Quote of
        quotedExpr: Expr *
        quotationInfo: ((ILTypeRef list * TTypes * Exprs * ExprData) * (ILTypeRef list * TTypes * Exprs * ExprData)) option ref *
        isFromQueryExpression: bool *
        range: range *
        quotedType: TType  
    
    /// Used in quotation generation to indicate a witness argument, spliced into a quotation literal.
    ///
    /// For example:
    ///
    ///     let inline f x = <@ sin x @>
    ///
    /// needs to pass a witness argument to `sin x`, captured from the surrounding context, for the witness-passing
    /// version of the code.  Thus the QuotationTranslation and IlxGen makes the generated code as follows:
    ///
    ///  f(x) { return Deserialize(<@ sin _spliceHole @>, [| x |]) }
    ///
    ///  f$W(witnessForSin, x) { return Deserialize(<@ sin$W _spliceHole1 _spliceHole2 @>, [| WitnessArg(witnessForSin), x |]) }
    ///
    /// where _spliceHole1 will be the location of the witness argument in the quotation data, and 
    /// witnessArg is the lambda for the witness
    /// 
    | WitnessArg of
        traitInfo: TraitConstraintInfo *
        range: range

    /// Indicates a free choice of typars that arises due to 
    /// minimization of polymorphism at let-rec bindings. These are 
    /// resolved to a concrete instantiation on subsequent rewrites. 
    | TyChoose of
        typeParams: Typars *
        bodyExpr: Expr *
        range: range

    /// An instance of a link node occurs for every use of a recursively bound variable. When type-checking 
    /// the recursive bindings a dummy expression is stored in the mutable reference cell. 
    /// After type checking the bindings this is replaced by a use of the variable, perhaps at an 
    /// appropriate type instantiation. These are immediately eliminated on subsequent rewrites. 
    | Link of Expr ref

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member expr.DebugText = expr.ToDebugString(3)

    override expr.ToString() = expr.ToDebugString(3)

    member expr.ToDebugString(depth: int) = 
        if depth = 0 then ".." else
        let depth = depth - 1
        match expr with 
        | Const (c, _, _) -> c.ToString()
        | Val (v, _, _) -> v.LogicalName
        | Sequential (e1, e2, _, _, _) -> "Sequential(" + e1.ToDebugString(depth) + ", " + e2.ToDebugString(depth) + ")"
        | Lambda (_, _, _, vs, body, _, _) -> sprintf "Lambda(%+A, " vs + body.ToDebugString(depth) + ")" 
        | TyLambda (_, tps, body, _, _) -> sprintf "TyLambda(%+A, " tps + body.ToDebugString(depth) + ")"
        | App (f, _, _, args, _) -> "App(" + f.ToDebugString(depth) + ", [" + String.concat ", " (args |> List.map (fun e -> e.ToDebugString(depth))) + "])"
        | LetRec _ -> "LetRec(..)"
        | Let (bind, body, _, _) -> "Let(" + bind.Var.DisplayName + ", " + bind.Expr.ToDebugString(depth) + ", " + body.ToDebugString(depth) + ")"
        | Obj (_, _objTy, _, _, _, _, _) -> "Obj(..)"
        | Match (_, _, _dt, _tgs, _, _) -> "Match(..)"
        | StaticOptimization (_, _, _, _) -> "StaticOptimization(..)"
        | Op (op, _, args, _) -> "Op(" + op.ToString() + ", " + String.concat ", " (args |> List.map (fun e -> e.ToDebugString(depth))) + ")"
        | Quote _ -> "Quote(..)"
        | WitnessArg _  -> "WitnessArg(..)"
        | TyChoose _ -> "TyChoose(..)"
        | Link e -> "Link(" + e.Value.ToDebugString(depth) + ")"

[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}") >]
type TOp =

    /// An operation representing the creation of a union value of the particular union case
    | UnionCase of UnionCaseRef 

    /// An operation representing the creation of an exception value using an F# exception declaration
    | ExnConstr of TyconRef

    /// An operation representing the creation of a tuple value
    | Tuple of TupInfo 

    /// An operation representing the creation of an anonymous record
    | AnonRecd of AnonRecdTypeInfo

    /// An operation representing the get of a property from an anonymous record
    | AnonRecdGet of AnonRecdTypeInfo * int

    /// An operation representing the creation of an array value
    | Array

    /// Constant byte arrays (used for parser tables and other embedded data)
    | Bytes of byte[]

    /// Constant uint16 arrays (used for parser tables)
    | UInt16s of uint16[] 

    /// An operation representing a lambda-encoded while loop. The special while loop marker is used to mark compilations of 'foreach' expressions
    | While of DebugPointAtWhile * SpecialWhileLoopMarker

    /// An operation representing a lambda-encoded for loop
    | For of DebugPointAtFor * ForLoopStyle (* count up or down? *)

    /// An operation representing a lambda-encoded try/with
    | TryWith of DebugPointAtTry * DebugPointAtWith

    /// An operation representing a lambda-encoded try/finally
    | TryFinally of DebugPointAtTry * DebugPointAtFinally

    /// Construct a record or object-model value. The ValRef is for self-referential class constructors, otherwise 
    /// it indicates that we're in a constructor and the purpose of the expression is to 
    /// fill in the fields of a pre-created but uninitialized object, and to assign the initialized 
    /// version of the object into the optional mutable cell pointed to be the given value. 
    | Recd of RecordConstructionInfo * TyconRef
    
    /// An operation representing setting a record or class field
    | ValFieldSet of RecdFieldRef 

    /// An operation representing getting a record or class field
    | ValFieldGet of RecdFieldRef 

    /// An operation representing getting the address of a record field
    | ValFieldGetAddr of RecdFieldRef * readonly: bool      

    /// An operation representing getting an integer tag for a union value representing the union case number
    | UnionCaseTagGet of TyconRef

    /// An operation representing a coercion that proves a union value is of a particular union case. This is not a test, its
    /// simply added proof to enable us to generate verifiable code for field access on union types
    | UnionCaseProof of UnionCaseRef

    /// An operation representing a field-get from a union value, where that value has been proven to be of the corresponding union case.
    | UnionCaseFieldGet of UnionCaseRef * int 

    /// An operation representing a field-get from a union value, where that value has been proven to be of the corresponding union case.
    | UnionCaseFieldGetAddr of UnionCaseRef * int * readonly: bool

    /// An operation representing a field-get from a union value. The value is not assumed to have been proven to be of the corresponding union case.
    | UnionCaseFieldSet of UnionCaseRef * int

    /// An operation representing a field-get from an F# exception value.
    | ExnFieldGet of TyconRef * int 

    /// An operation representing a field-set on an F# exception value.
    | ExnFieldSet of TyconRef * int 

    /// An operation representing a field-get from an F# tuple value.
    | TupleFieldGet of TupInfo * int 

    /// IL assembly code - type list are the types pushed on the stack 
    | ILAsm of 
        instrs: ILInstr list * 
        retTypes: TTypes 

    /// Generate a ldflda on an 'a ref. 
    | RefAddrGet of bool

    /// Conversion node, compiled via type-directed translation or to box/unbox 
    | Coerce 

    /// Represents a "rethrow" operation. May not be rebound, or used outside of try-finally, expecting a unit argument 
    | Reraise 

    /// Used for state machine compilation
    | Return

    /// Used for state machine compilation
    | Goto of ILCodeLabel

    /// Used for state machine compilation
    | Label of ILCodeLabel

    /// Pseudo method calls. This is used for overloaded operations like op_Addition. 
    | TraitCall of TraitConstraintInfo 

    /// Operation nodes representing C-style operations on byrefs and mutable vals (l-values) 
    | LValueOp of LValueOperation * ValRef 

    /// IL method calls.
    ///     isProperty -- used for quotation reflection, property getters & setters  
    ///     noTailCall - DllImport? if so don't tailcall  
    ///     retTypes -- the types of pushed values, if any
    | ILCall of 
        isVirtual: bool * 
        isProtected: bool * 
        isStruct: bool * 
        isCtor: bool * 
        valUseFlag: ValUseFlag * 
        isProperty: bool * 
        noTailCall: bool * 
        ilMethRef: ILMethodRef * 
        enclTypeInst: TypeInst * 
        methInst: TypeInst * 
        retTypes: TTypes   

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override op.ToString() = 
        match op with 
        | UnionCase ucref -> "UnionCase(" + ucref.CaseName + ")"
        | ExnConstr ecref -> "ExnConstr(" + ecref.LogicalName + ")"
        | Tuple _tupinfo -> "Tuple"
        | AnonRecd _anonInfo -> "AnonRecd(..)"
        | AnonRecdGet _ -> "AnonRecdGet(..)"
        | Array -> "NewArray"
        | Bytes _ -> "Bytes(..)"
        | UInt16s _ -> "UInt16s(..)"
        | While _ -> "While"
        | For _ -> "For"
        | TryWith _ -> "TryWith"
        | TryFinally _ -> "TryFinally"
        | Recd (_, tcref) -> "Recd(" + tcref.LogicalName + ")"
        | ValFieldSet rfref -> "ValFieldSet(" + rfref.FieldName + ")"
        | ValFieldGet rfref -> "ValFieldGet(" + rfref.FieldName + ")"
        | ValFieldGetAddr (rfref, _) -> "ValFieldGetAddr(" + rfref.FieldName + ",..)"
        | UnionCaseTagGet tcref -> "UnionCaseTagGet(" + tcref.LogicalName + ")"
        | UnionCaseProof ucref -> "UnionCaseProof(" + ucref.CaseName + ")"
        | UnionCaseFieldGet (ucref, _) -> "UnionCaseFieldGet(" + ucref.CaseName + ",..)"
        | UnionCaseFieldGetAddr (ucref, _, _) -> "UnionCaseFieldGetAddr(" + ucref.CaseName + ",..)"
        | UnionCaseFieldSet (ucref, _) -> "UnionCaseFieldSet(" + ucref.CaseName + ",..)"
        | ExnFieldGet (tcref, _) -> "ExnFieldGet(" + tcref.LogicalName + ",..)"
        | ExnFieldSet (tcref, _) -> "ExnFieldSet(" + tcref.LogicalName + ",..)"
        | TupleFieldGet _ -> "TupleFieldGet(..)"
        | ILAsm _ -> "ILAsm(..)"
        | RefAddrGet _ -> "RefAddrGet(..)"
        | Coerce -> "Coerce"
        | Reraise -> "Reraise"
        | Return -> "Return"
        | Goto n -> "Goto(" + string n + ")"
        | Label n -> "Label(" + string n + ")"
        | TraitCall info -> "TraitCall(" + info.MemberName + ")"
        | LValueOp (op, vref) -> sprintf "%+A(%s)" op vref.LogicalName
        | ILCall (_,_,_,_,_,_,_,ilMethRef,_,_,_) -> "ILCall(" + ilMethRef.ToString() + ",..)"

/// Represents the kind of record construction operation.
type RecordConstructionInfo = 

    /// We're in an explicit constructor. The purpose of the record expression is to 
    /// fill in the fields of a pre-created but uninitialized object 
    | RecdExprIsObjInit

    /// Normal record construction 
    | RecdExpr

/// If this is Some ty then it indicates that a .NET 2.0 constrained call is required, with the given type as the
/// static type of the object argument.
type ConstrainedCallInfo = TType option

/// Represents the kind of looping operation.
type SpecialWhileLoopMarker = 

    | NoSpecialWhileLoopMarker

    /// Marks the compiled form of a 'for ... in ... do ' expression
    | WhileLoopForCompiledForEachExprMarker
    
/// Represents the kind of looping operation.
type ForLoopStyle = 
    /// Evaluate start and end once, loop up
    | FSharpForLoopUp 

    /// Evaluate start and end once, loop down
    | FSharpForLoopDown 

    /// Evaluate start once and end multiple times, loop up
    | CSharpForLoopUp

/// Indicates what kind of pointer operation this is.
type LValueOperation = 

    /// In C syntax this is: &localv            
    | LAddrOf of readonly: bool

    /// In C syntax this is: *localv_ptr        
    | LByrefGet     

    /// In C syntax this is: localv = e, note == *(&localv) = e == LAddrOf; LByrefSet
    | LSet          

    /// In C syntax this is: *localv_ptr = e   
    | LByrefSet     

/// Represents the kind of sequential operation, i.e. "normal" or "to a before returning b"
type SequentialOpKind = 
    /// a ; b 
    | NormalSeq 

    /// let res = a in b;res 
    | ThenDoSeq     

/// Indicates how a value, function or member is being used at a particular usage point.
type ValUseFlag =

    /// Indicates a use of a value represents a call to a method that may require
    /// a .NET 2.0 constrained call. A constrained call is only used for calls where 
    // the object argument is a value type or generic type, and the call is to a method
    //  on System.Object, System.ValueType, System.Enum or an interface methods.
    | PossibleConstrainedCall of ty: TType

    /// A normal use of a value
    | NormalValUse

    /// A call to a constructor, e.g. 'inherit C()'
    | CtorValUsedAsSuperInit

    /// A call to a constructor, e.g. 'new C() = new C(3)'
    | CtorValUsedAsSelfInit

    /// A call to a base method, e.g. 'base.OnPaint(args)'
    | VSlotDirectCall
  
/// Represents the kind of an F# core library static optimization construct
type StaticOptimization = 

    /// Indicates the static optimization applies when a type equality holds
    | TTyconEqualsTycon of ty1: TType * ty2: TType

    /// Indicates the static optimization applies when a type is a struct
    | TTyconIsStruct of ty: TType 
  
/// A representation of a method in an object expression. 
///
/// TObjExprMethod(slotsig, attribs, methTyparsOfOverridingMethod, methodParams, methodBodyExpr, m)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ObjExprMethod = 

    | TObjExprMethod of
        slotSig: SlotSig *
        attribs: Attribs *
        methTyparsOfOverridingMethod: Typars *
        methodParams: Val list list *
        methodBodyExpr: Expr *
        range: range

    member x.Id = let (TObjExprMethod(slotsig, _, _, _, _, m)) = x in mkSynId m slotsig.Name

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "TObjExprMethod(%s, ...)" x.Id.idText

/// Represents an abstract method slot, or delegate signature.
///
/// TSlotSig(methodName, declaringType, declaringTypeParameters, methodTypeParameters, slotParameters, returnTy)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type SlotSig = 
    | TSlotSig of
        methodName: string *
        implementedType: TType *
        classTypars: Typars *
        methodTypars: Typars *
        formalParams: SlotParam list list *
        formalReturn: TType option

    /// The name of the method
    member ss.Name = let (TSlotSig(nm, _, _, _, _, _)) = ss in nm

    /// The (instantiated) type which the slot is logically a part of 
    member ss.ImplementedType = let (TSlotSig(_, ty, _, _, _, _)) = ss in ty

    /// The class type parameters of the slot
    member ss.ClassTypars = let (TSlotSig(_, _, ctps, _, _, _)) = ss in ctps

    /// The method type parameters of the slot
    member ss.MethodTypars = let (TSlotSig(_, _, _, mtps, _, _)) = ss in mtps

    /// The formal parameters of the slot (regardless of the type or method instantiation)
    member ss.FormalParams = let (TSlotSig(_, _, _, _, ps, _)) = ss in ps

    /// The formal return type of the slot (regardless of the type or method instantiation)
    member ss.FormalReturnType = let (TSlotSig(_, _, _, _, _, rt)) = ss in rt

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override ss.ToString() = sprintf "TSlotSig(%s, ...)" ss.Name

/// Represents a parameter to an abstract method slot. 
///
/// TSlotParam(nm, ty, inFlag, outFlag, optionalFlag, attribs)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type SlotParam = 
    | TSlotParam of
        paramName: string option *
        paramType: TType *
        isIn: bool *
        isOut: bool *
        isOptional: bool *
        attributes: Attribs

    member x.Type = let (TSlotParam(_, ty, _, _, _, _)) = x in ty

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TSlotParam(...)"

/// A type for a module-or-namespace-fragment and the actual definition of the module-or-namespace-fragment
/// The first ModuleOrNamespaceType is the signature and is a binder. However the bindings are not used in the ModuleOrNamespaceExpr: it is only referenced from the 'outside' 
/// is for use by FCS only to report the "hidden" contents of the assembly prior to applying the signature.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type ModuleOrNamespaceExprWithSig = 
    | ModuleOrNamespaceExprWithSig of 
         moduleType: ModuleOrNamespaceType *
         contents: ModuleOrNamespaceExpr *
         range: range

    member x.Type = let (ModuleOrNamespaceExprWithSig(mtyp, _, _)) = x in mtyp

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "ModuleOrNamespaceExprWithSig(...)"

/// The contents of a module-or-namespace-fragment definition 
[<NoEquality; NoComparison (* ; StructuredFormatDisplay("{DebugText}") *) >]
type ModuleOrNamespaceExpr = 
    /// Indicates the module is a module with a signature 
    | TMAbstract of moduleOrNamespaceExprWithSig: ModuleOrNamespaceExprWithSig

    /// Indicates the module fragment is made of several module fragments in succession 
    | TMDefs of moduleOrNamespaceExprs: ModuleOrNamespaceExpr list  

    /// Indicates the module fragment is a 'let' definition 
    | TMDefLet of binding: Binding * range: range

    /// Indicates the module fragment is an evaluation of expression for side-effects
    | TMDefDo of expr: Expr * range: range

    /// Indicates the module fragment is a 'rec' or 'non-rec' definition of types and modules
    | TMDefRec of isRec: bool * tycons: Tycon list * moduleOrNamespaceBindings: ModuleOrNamespaceBinding list * range: range

    // %+A formatting is used, so this is not needed
    //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "%+A" x 

/// A named module-or-namespace-fragment definition 
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type ModuleOrNamespaceBinding = 

    | Binding of binding: Binding 

    | Module of 
         /// This ModuleOrNamespace that represents the compilation of a module as a class. 
         /// The same set of tycons etc. are bound in the ModuleOrNamespace as in the ModuleOrNamespaceExpr
         moduleOrNamespace: ModuleOrNamespace * 
         /// This is the body of the module/namespace 
         moduleOrNamespaceExpr: ModuleOrNamespaceExpr

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "ModuleOrNamespaceBinding(...)"

/// Represents a complete typechecked implementation file, including its typechecked signature if any.
///
/// TImplFile (qualifiedNameOfFile, pragmas, implementationExpressionWithSignature, hasExplicitEntryPoint, isScript, anonRecdTypeInfo)
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TypedImplFile = 
    | TImplFile of 
        qualifiedNameOfFile: QualifiedNameOfFile *
        pragmas: ScopedPragma list *
        implementationExpressionWithSignature: ModuleOrNamespaceExprWithSig *
        hasExplicitEntryPoint: bool *
        isScript: bool *
        anonRecdTypeInfo: StampMap<AnonRecdTypeInfo>

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TImplFile (...)"

/// Represents a complete typechecked assembly, made up of multiple implementation files.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TypedImplFileAfterOptimization = 
    { ImplFile: TypedImplFile 
      OptimizeDuringCodeGen: (bool -> Expr -> Expr) }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TypedImplFileAfterOptimization(...)"

/// Represents a complete typechecked assembly, made up of multiple implementation files.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type TypedAssemblyAfterOptimization = 
    | TypedAssemblyAfterOptimization of TypedImplFileAfterOptimization list

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "TypedAssemblyAfterOptimization(...)"

[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type CcuData = 
    {
      /// Holds the filename for the DLL, if any 
      FileName: string option 
      
      /// Holds the data indicating how this assembly/module is referenced from the code being compiled. 
      ILScopeRef: ILScopeRef
      
      /// A unique stamp for this DLL 
      Stamp: Stamp
      
      /// The fully qualified assembly reference string to refer to this assembly. This is persisted in quotations 
      QualifiedName: string option 
      
      /// A hint as to where does the code for the CCU live (e.g what was the tcConfig.implicitIncludeDir at compilation time for this DLL?) 
      SourceCodeDirectory: string 
      
      /// Indicates that this DLL was compiled using the F# compiler and has F# metadata
      IsFSharp: bool 
      
#if !NO_EXTENSIONTYPING
      /// Is the CCu an assembly injected by a type provider
      IsProviderGenerated: bool 

      /// Triggered when the contents of the CCU are invalidated
      InvalidateEvent: IEvent<string> 

      /// A helper function used to link method signatures using type equality. This is effectively a forward call to the type equality 
      /// logic in tastops.fs
      ImportProvidedType: Tainted<ProvidedType> -> TType 
      
#endif
      /// Indicates that this DLL uses pre-F#-4.0 quotation literals somewhere. This is used to implement a restriction on static linking
      mutable UsesFSharp20PlusQuotations: bool
      
      /// A handle to the full specification of the contents of the module contained in this ccu
      // NOTE: may contain transient state during typechecking 
      mutable Contents: ModuleOrNamespace
      
      /// A helper function used to link method signatures using type equality. This is effectively a forward call to the type equality 
      /// logic in tastops.fs
      TryGetILModuleDef: (unit -> ILModuleDef option) 
      
      /// A helper function used to link method signatures using type equality. This is effectively a forward call to the type equality 
      /// logic in tastops.fs
      MemberSignatureEquality: (TType -> TType -> bool) 
      
      /// The table of .NET CLI type forwarders for this assembly
      TypeForwarders: CcuTypeForwarderTable
      
      XmlDocumentationInfo: XmlDocumentationInfo option }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = sprintf "CcuData(%A)" x.FileName

/// Represents a table of .NET CLI type forwarders for an assembly
type CcuTypeForwarderTable = Map<string[] * string, Lazy<EntityRef>>

type CcuReference = string // ILAssemblyRef

/// A relinkable handle to the contents of a compilation unit. Relinking is performed by mutation.
//
/// A compilation unit is, more or less, the new material created in one
/// invocation of the compiler. Due to static linking assemblies may hold more 
/// than one compilation unit (i.e. when two assemblies are merged into a compilation
/// the resulting assembly will contain 3 CUs). Compilation units are also created for referenced
/// .NET assemblies. 
/// 
/// References to items such as type constructors are via 
/// cross-compilation-unit thunks, which directly reference the data structures that define
/// these modules. Thus, when saving out values to disk we only wish 
/// to save out the "current" part of the term graph. When reading values
/// back in we "fixup" the links to previously referenced modules.
///
/// All non-local accesses to the data structures are mediated
/// by ccu-thunks. Ultimately, a ccu-thunk is either a (named) element of
/// the data structure, or it is a delayed fixup, i.e. an invalid dangling
/// reference that has not had an appropriate fixup applied.  
[<NoEquality; NoComparison; RequireQualifiedAccess; StructuredFormatDisplay("{DebugText}")>]
type CcuThunk = 
    {
      /// ccu.target is null when a reference is missing in the transitive closure of static references that
      /// may potentially be required for the metadata of referenced DLLs.
      mutable target: CcuData
      name: CcuReference
    }

    /// Dereference the asssembly reference 
    member ccu.Deref = 
        if isNull (ccu.target :> obj) then 
            raise(UnresolvedReferenceNoRange ccu.name)
        ccu.target
   
    /// Indicates if this assembly reference is unresolved
    member ccu.IsUnresolvedReference = isNull (ccu.target :> obj)

    /// Ensure the ccu is derefable in advance. Supply a path to attach to any resulting error message.
    member ccu.EnsureDerefable(requiringPath: string[]) = 
        if ccu.IsUnresolvedReference then 
            let path = System.String.Join(".", requiringPath)
            raise(UnresolvedPathReferenceNoRange(ccu.name, path))
            
    /// Indicates that this DLL uses F# 2.0+ quotation literals somewhere. This is used to implement a restriction on static linking.
    member ccu.UsesFSharp20PlusQuotations 
        with get() = ccu.Deref.UsesFSharp20PlusQuotations 
        and set v = ccu.Deref.UsesFSharp20PlusQuotations <- v

    /// The short name of the asssembly being referenced
    member ccu.AssemblyName = ccu.name

    /// Holds the data indicating how this assembly/module is referenced from the code being compiled. 
    member ccu.ILScopeRef = ccu.Deref.ILScopeRef

    /// A unique stamp for this assembly
    member ccu.Stamp = ccu.Deref.Stamp

    /// Holds the filename for the assembly, if any 
    member ccu.FileName = ccu.Deref.FileName

    /// Try to get the .NET Assembly, if known. May not be present for `IsFSharp` for
    /// in-memory cross-project references
    member ccu.TryGetILModuleDef() = ccu.Deref.TryGetILModuleDef()

#if !NO_EXTENSIONTYPING
    /// Is this a provider-injected assembly
    member ccu.IsProviderGenerated = ccu.Deref.IsProviderGenerated

    /// Used to make 'forward' calls into the loader during linking
    member ccu.ImportProvidedType ty: TType = ccu.Deref.ImportProvidedType ty
#endif

    /// The fully qualified assembly reference string to refer to this assembly. This is persisted in quotations 
    member ccu.QualifiedName = ccu.Deref.QualifiedName

    /// A hint as to where does the code for the CCU live (e.g what was the tcConfig.implicitIncludeDir at compilation time for this DLL?) 
    member ccu.SourceCodeDirectory = ccu.Deref.SourceCodeDirectory

    /// Indicates that this DLL was compiled using the F# compiler and has F# metadata
    member ccu.IsFSharp = ccu.Deref.IsFSharp

    /// A handle to the full specification of the contents of the module contained in this ccu
    // NOTE: may contain transient state during typechecking 
    member ccu.Contents = ccu.Deref.Contents

    /// The table of type forwarders for this assembly
    member ccu.TypeForwarders: Map<string[] * string, Lazy<EntityRef>> = ccu.Deref.TypeForwarders

    /// The table of modules and namespaces at the "root" of the assembly
    member ccu.RootModulesAndNamespaces = ccu.Contents.ModuleOrNamespaceType.ModuleAndNamespaceDefinitions

    /// The table of type definitions at the "root" of the assembly
    member ccu.RootTypeAndExceptionDefinitions = ccu.Contents.ModuleOrNamespaceType.TypeAndExceptionDefinitions

    /// Create a CCU with the given name and contents
    static member Create(nm, x) = 
        { target = x 
          name = nm }

    /// Create a CCU with the given name but where the contents have not yet been specified
    static member CreateDelayed nm = 
        { target = Unchecked.defaultof<_> 
          name = nm }

    /// Fixup a CCU to have the given contents
    member x.Fixup(avail: CcuThunk) = 

        match box x.target with
        | null -> ()
        | _ -> 
            // In the IDE we tolerate a double-fixup of FSHarp.Core when editing the FSharp.Core project itself
            if x.AssemblyName <> "FSharp.Core" then 
                errorR(Failure("internal error: Fixup: the ccu thunk for assembly "+x.AssemblyName+" not delayed!"))

        assert (avail.AssemblyName = x.AssemblyName)
        x.target <- 
            match box avail.target with
            | null -> error(Failure("internal error: ccu thunk '"+avail.name+"' not fixed up!"))
            | _ -> avail.target

    /// Try to resolve a path into the CCU by referencing the .NET/CLI type forwarder table of the CCU
    member ccu.TryForward(nlpath: string[], item: string) : EntityRef option = 
        ccu.EnsureDerefable nlpath
        let key = nlpath, item
        match ccu.TypeForwarders.TryGetValue key with
        | true, entity -> Some(entity.Force())
        | _ -> None

    /// Used to make forward calls into the type/assembly loader when comparing member signatures during linking
    member ccu.MemberSignatureEquality(ty1: TType, ty2: TType) = 
        ccu.Deref.MemberSignatureEquality ty1 ty2
    
    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    /// Used at the end of comppiling an assembly to get a frozen, final stable CCU
    /// for the compilation which we no longer mutate.
    member x.CloneWithFinalizedContents(ccuContents) =
        { x with target = { x.target with Contents = ccuContents } }

    override ccu.ToString() = ccu.AssemblyName

/// The result of attempting to resolve an assembly name to a full ccu.
/// UnresolvedCcu will contain the name of the assembly that could not be resolved.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type CcuResolutionResult =

    | ResolvedCcu of CcuThunk

    | UnresolvedCcu of string

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = match x with ResolvedCcu ccu -> ccu.ToString() | UnresolvedCcu s -> "unresolved " + s

/// Represents the information saved in the assembly signature data resource for an F# assembly
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type PickledCcuInfo =
    {
      mspec: ModuleOrNamespace

      compileTimeWorkingDir: string

      usesQuotations: bool
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override _.ToString() = "PickledCcuInfo(...)"


/// Represents a set of free local values. Computed and cached by later phases
/// (never cached type checking). Cached in expressions. Not pickled.
type FreeLocals = Zset<Val>

/// Represents a set of free type parameters. Computed and cached by later phases
/// (never cached type checking). Cached in expressions. Not pickled.
type FreeTypars = Zset<Typar>

/// Represents a set of 'free' named type definitions. Used to collect the named type definitions referred to 
/// from a type or expression. Computed and cached by later phases (never cached type checking). Cached
/// in expressions. Not pickled.
type FreeTycons = Zset<Tycon>

/// Represents a set of 'free' record field definitions. Used to collect the record field definitions referred to 
/// from an expression.
type FreeRecdFields = Zset<RecdFieldRef>

/// Represents a set of 'free' union cases. Used to collect the union cases referred to from an expression.
type FreeUnionCases = Zset<UnionCaseRef>

/// Represents a set of 'free' type-related elements, including named types, trait solutions, union cases and
/// record fields.
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type FreeTyvars = 
    {
      /// The summary of locally defined type definitions used in the expression. These may be made private by a signature 
      /// and we have to check various conditions associated with that. 
      FreeTycons: FreeTycons

      /// The summary of values used as trait solutions
      FreeTraitSolutions: FreeLocals
      
      /// The summary of type parameters used in the expression. These may not escape the enclosing generic construct 
      /// and we have to check various conditions associated with that. 
      FreeTypars: FreeTypars
    }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "FreeTyvars(...)"

/// Represents an amortized computation of the free variables in an expression
type FreeVarsCache = FreeVars cache

/// Represents the set of free variables in an expression
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type FreeVars = 
    {
      /// The summary of locally defined variables used in the expression. These may be hidden at let bindings etc. 
      /// or made private by a signature or marked 'internal' or 'private', and we have to check various conditions associated with that. 
      FreeLocals: FreeLocals
      
      /// Indicates if the expression contains a call to a protected member or a base call. 
      /// Calls to protected members and direct calls to super classes can't escape, also code can't be inlined 
      UsesMethodLocalConstructs: bool 

      /// Indicates if the expression contains a call to rethrow that is not bound under a (try-)with branch. 
      /// Rethrow may only occur in such locations. 
      UsesUnboundRethrow: bool 

      /// The summary of locally defined tycon representations used in the expression. These may be made private by a signature 
      /// or marked 'internal' or 'private' and we have to check various conditions associated with that. 
      FreeLocalTyconReprs: FreeTycons 

      /// The summary of fields used in the expression. These may be made private by a signature 
      /// or marked 'internal' or 'private' and we have to check various conditions associated with that. 
      FreeRecdFields: FreeRecdFields
      
      /// The summary of union constructors used in the expression. These may be
      /// marked 'internal' or 'private' and we have to check various conditions associated with that.
      FreeUnionCases: FreeUnionCases
      
      /// See FreeTyvars above.
      FreeTyvars: FreeTyvars }

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    override x.ToString() = "FreeVars(...)"

/// A set of static methods for constructing types.
type Construct() = 

    static let taccessPublic = TAccess [] 
    
    /// Key a Tycon or TyconRef by decoded name
    static member KeyTyconByDecodedName<'T> (nm: string) (x: 'T) : KeyValuePair<NameArityPair, 'T> = 
        KeyValuePair(DecodeGenericTypeName nm, x)

    /// Key a Tycon or TyconRef by both mangled and demangled name.
    /// Generic types can be accessed either by 'List' or 'List`1'.
    /// This lists both keys.
    static member KeyTyconByAccessNames<'T> (nm: string) (x: 'T) : KeyValuePair<string, 'T>[] = 
        match TryDemangleGenericNameAndPos nm with
        | ValueSome pos ->
            let dnm = DemangleGenericTypeNameWithPos pos nm 
            [| KeyValuePair(nm, x); KeyValuePair(dnm, x) |]
        | _ ->
            [| KeyValuePair(nm, x) |]

    /// Create a new node for the contents of a module or namespace
    static member NewModuleOrNamespaceType mkind tycons vals = 
        ModuleOrNamespaceType(mkind, QueueList.ofList vals, QueueList.ofList tycons)

    /// Create a new node for an empty module or namespace contents
    static member NewEmptyModuleOrNamespaceType mkind = 
        Construct.NewModuleOrNamespaceType mkind [] []

#if !NO_EXTENSIONTYPING

    /// Create a new node for the representation information for a provided type definition
    static member NewProvidedTyconRepr(resolutionEnvironment, st: Tainted<ProvidedType>, importProvidedType, isSuppressRelocate, m) = 

        let isErased = st.PUntaint((fun st -> st.IsErased), m)

        let lazyBaseTy = 
            LazyWithContext.Create 
                ((fun (m, objTy) -> 
                      let baseSystemTy = st.PApplyOption((fun st -> match st.BaseType with null -> None | ty -> Some ty), m)
                      match baseSystemTy with 
                      | None -> objTy 
                      | Some t -> importProvidedType t),
                  ErrorLogger.findOriginalException)

        TProvidedTypeExtensionPoint 
            { ResolutionEnvironment=resolutionEnvironment
              ProvidedType=st
              LazyBaseType=lazyBaseTy
              UnderlyingTypeOfEnum = (fun () -> importProvidedType (st.PApply((fun st -> st.GetEnumUnderlyingType()), m)))
              IsDelegate = (fun () -> st.PUntaint((fun st -> 
                                   let baseType = st.BaseType 
                                   match baseType with 
                                   | null -> false
                                   | x when x.IsGenericType -> false
                                   | x when x.DeclaringType <> null -> false
                                   | x -> x.FullName = "System.Delegate" || x.FullName = "System.MulticastDelegate"), m))
              IsEnum = st.PUntaint((fun st -> st.IsEnum), m)
              IsStructOrEnum = st.PUntaint((fun st -> st.IsValueType || st.IsEnum), m)
              IsInterface = st.PUntaint((fun st -> st.IsInterface), m)
              IsSealed = st.PUntaint((fun st -> st.IsSealed), m)
              IsAbstract = st.PUntaint((fun st -> st.IsAbstract), m)
              IsClass = st.PUntaint((fun st -> st.IsClass), m)
              IsErased = isErased
              IsSuppressRelocate = isSuppressRelocate }

    /// Create a new entity node for a provided type definition
    static member NewProvidedTycon(resolutionEnvironment, st: Tainted<ProvidedType>, importProvidedType, isSuppressRelocate, m, ?access, ?cpath) = 
        let stamp = newStamp() 
        let name = st.PUntaint((fun st -> st.Name), m)
        let id = ident (name, m)
        let kind = 
            let isMeasure = 
                st.PApplyWithProvider((fun (st, provider) ->
                    ignore provider
                    st.IsMeasure), m)
                  .PUntaintNoFailure(fun x -> x)
            if isMeasure then TyparKind.Measure else TyparKind.Type

        let access = 
            match access with 
            | Some a -> a 
            | None -> TAccess []
        let cpath =  
            match cpath with 
            | None -> 
                let ilScopeRef = st.TypeProviderAssemblyRef
                let enclosingName = ExtensionTyping.GetFSharpPathToProvidedType(st, m)
                CompPath(ilScopeRef, enclosingName |> List.map(fun id->id, ModuleOrNamespaceKind.Namespace))
            | Some p -> p
        let pubpath = cpath.NestedPublicPath id

        let repr = Construct.NewProvidedTyconRepr(resolutionEnvironment, st, importProvidedType, isSuppressRelocate, m)

        Tycon.New "tycon"
          { entity_stamp=stamp
            entity_logical_name=name
            entity_range=m
            entity_flags=EntityFlags(usesPrefixDisplay=false, isModuleOrNamespace=false, preEstablishedHasDefaultCtor=false, hasSelfReferentialCtor=false, isStructRecordOrUnionType=false)
            entity_attribs=[] // fetched on demand via est.fs API
            entity_typars= LazyWithContext.NotLazy []
            entity_tycon_repr = repr
            entity_tycon_tcaug=TyconAugmentation.Create()
            entity_modul_contents = MaybeLazy.Lazy (lazy new ModuleOrNamespaceType(Namespace, QueueList.ofList [], QueueList.ofList []))
            // Generated types get internal accessibility
            entity_pubpath = Some pubpath
            entity_cpath = Some cpath
            entity_il_repr_cache = newCache()
            entity_opt_data =
                match kind, access with
                | TyparKind.Type, TAccess [] -> None
                | _ -> Some { Entity.NewEmptyEntityOptData() with
                                 entity_kind = kind
                                 entity_accessibility = access } } 
#endif

    /// Create a new entity node for a module or namespace
    static member NewModuleOrNamespace cpath access (id: Ident) (xml: XmlDoc) attribs mtype = 
        let stamp = newStamp() 
        // Put the module suffix on if needed 
        Tycon.New "mspec"
          { entity_logical_name=id.idText
            entity_range = id.idRange
            entity_stamp=stamp
            entity_modul_contents = mtype
            entity_flags=EntityFlags(usesPrefixDisplay=false, isModuleOrNamespace=true, preEstablishedHasDefaultCtor=false, hasSelfReferentialCtor=false, isStructRecordOrUnionType=false)
            entity_typars=LazyWithContext.NotLazy []
            entity_tycon_repr = TNoRepr
            entity_tycon_tcaug=TyconAugmentation.Create()
            entity_pubpath=cpath |> Option.map (fun (cp: CompilationPath) -> cp.NestedPublicPath id)
            entity_cpath=cpath
            entity_attribs=attribs
            entity_il_repr_cache = newCache()
            entity_opt_data =
                match xml, access with
                | doc, TAccess [] when doc.IsEmpty -> None
                | _ -> Some { Entity.NewEmptyEntityOptData() with
                                 entity_xmldoc = xml
                                 entity_tycon_repr_accessibility = access
                                 entity_accessibility = access } } 

    /// Create a new unfilled cache for free variable calculations
    static member NewFreeVarsCache() = newCache ()

    /// Create the field tables for a record or class type
    static member MakeRecdFieldsTable ucs: TyconRecdFields = 
        { FieldsByIndex = Array.ofList ucs 
          FieldsByName = ucs |> NameMap.ofKeyedList (fun rfld -> rfld.Name) }

    /// Create the union case tables for a union type
    static member MakeUnionCases ucs: TyconUnionData = 
        { CasesTable = 
            { CasesByIndex = Array.ofList ucs 
              CasesByName = NameMap.ofKeyedList (fun uc -> uc.DisplayName) ucs }
          CompiledRepresentation=newCache() }

    /// Create a node for a union type
    static member MakeUnionRepr ucs = TUnionRepr (Construct.MakeUnionCases ucs)

    /// Create a new type parameter node
    static member NewTypar (kind, rigid, SynTypar(id, staticReq, isCompGen), isFromError, dynamicReq, attribs, eqDep, compDep) = 
        Typar.New
          { typar_id = id 
            typar_stamp = newStamp() 
            typar_flags= TyparFlags(kind, rigid, isFromError, isCompGen, staticReq, dynamicReq, eqDep, compDep) 
            typar_solution = None
            typar_astype = Unchecked.defaultof<_>
            typar_opt_data =
                match attribs with
                | [] -> None
                | _ -> Some { typar_il_name = None; typar_xmldoc = XmlDoc.Empty; typar_constraints = []; typar_attribs = attribs } } 

    /// Create a new type parameter node for a declared type parameter
    static member NewRigidTypar nm m =
        Construct.NewTypar (TyparKind.Type, TyparRigidity.Rigid, SynTypar(mkSynId m nm, TyparStaticReq.None, true), false, TyparDynamicReq.Yes, [], false, false)

    /// Create a new union case node
    static member NewUnionCase id tys rty attribs docOption access: UnionCase = 
        { Id=id
          XmlDoc=docOption
          XmlDocSig=""
          Accessibility=access
          FieldTable = Construct.MakeRecdFieldsTable tys
          ReturnType = rty
          Attribs=attribs 
          OtherRangeOpt = None } 

    /// Create a new TAST Entity node for an F# exception definition
    static member NewExn cpath (id: Ident) access repr attribs (doc: XmlDoc) = 
        Tycon.New "exnc"
          { entity_stamp=newStamp()
            entity_attribs=attribs
            entity_logical_name=id.idText
            entity_range=id.idRange
            entity_tycon_tcaug=TyconAugmentation.Create()
            entity_pubpath=cpath |> Option.map (fun (cp: CompilationPath) -> cp.NestedPublicPath id)
            entity_modul_contents = MaybeLazy.Strict (Construct.NewEmptyModuleOrNamespaceType ModuleOrType)
            entity_cpath= cpath
            entity_typars=LazyWithContext.NotLazy []
            entity_tycon_repr = TNoRepr
            entity_flags=EntityFlags(usesPrefixDisplay=false, isModuleOrNamespace=false, preEstablishedHasDefaultCtor=false, hasSelfReferentialCtor=false, isStructRecordOrUnionType=false)
            entity_il_repr_cache= newCache()
            entity_opt_data =
                match doc, access, repr with
                | doc, TAccess [], TExnNone when doc.IsEmpty -> None
                | _ -> Some { Entity.NewEmptyEntityOptData() with entity_xmldoc = doc; entity_accessibility = access; entity_tycon_repr_accessibility = access; entity_exn_info = repr } } 

    /// Create a new TAST RecdField node for an F# class, struct or record field
    static member NewRecdField stat konst id nameGenerated ty isMutable isVolatile pattribs fattribs docOption access secret =
        { rfield_mutable=isMutable
          rfield_pattribs=pattribs
          rfield_fattribs=fattribs
          rfield_type=ty
          rfield_static=stat
          rfield_volatile=isVolatile
          rfield_const=konst
          rfield_access = access
          rfield_secret = secret
          rfield_xmldoc = docOption 
          rfield_xmldocsig = ""
          rfield_id=id
          rfield_name_generated = nameGenerated
          rfield_other_range = None }

    
    /// Create a new type definition node
    static member NewTycon (cpath, nm, m, access, reprAccess, kind, typars, doc: XmlDoc, usesPrefixDisplay, preEstablishedHasDefaultCtor, hasSelfReferentialCtor, mtyp) =
        let stamp = newStamp() 
        Tycon.New "tycon"
          { entity_stamp=stamp
            entity_logical_name=nm
            entity_range=m
            entity_flags=EntityFlags(usesPrefixDisplay=usesPrefixDisplay, isModuleOrNamespace=false, preEstablishedHasDefaultCtor=preEstablishedHasDefaultCtor, hasSelfReferentialCtor=hasSelfReferentialCtor, isStructRecordOrUnionType=false)
            entity_attribs=[] // fixed up after
            entity_typars=typars
            entity_tycon_repr = TNoRepr
            entity_tycon_tcaug=TyconAugmentation.Create()
            entity_modul_contents = mtyp
            entity_pubpath=cpath |> Option.map (fun (cp: CompilationPath) -> cp.NestedPublicPath (mkSynId m nm))
            entity_cpath = cpath
            entity_il_repr_cache = newCache()
            entity_opt_data =
                match kind, doc, reprAccess, access with
                | TyparKind.Type, doc, TAccess [], TAccess [] when doc.IsEmpty -> None
                | _ -> Some { Entity.NewEmptyEntityOptData() with entity_kind = kind; entity_xmldoc = doc; entity_tycon_repr_accessibility = reprAccess; entity_accessibility=access } } 

    /// Create a new type definition node for a .NET type definition
    static member NewILTycon nlpath (nm, m) tps (scoref: ILScopeRef, enc, tdef: ILTypeDef) mtyp =
        let tycon = Construct.NewTycon(nlpath, nm, m, taccessPublic, taccessPublic, TyparKind.Type, tps, XmlDoc.Empty, true, false, false, mtyp)

        tycon.entity_tycon_repr <- TILObjectRepr (TILObjectReprData (scoref, enc, tdef))
        tycon.TypeContents.tcaug_closed <- true
        tycon

    /// Create a new Val node
    static member NewVal 
           (logicalName: string, m: range, compiledName, ty, isMutable, isCompGen, arity, access,
            recValInfo, specialRepr, baseOrThis, attribs, inlineInfo, doc: XmlDoc, isModuleOrMemberBinding,
            isExtensionMember, isIncrClassSpecialMember, isTyFunc, allowTypeInst, isGeneratedEventVal,
            konst, actualParent) : Val =

        let stamp = newStamp()
        Val.New {
            val_stamp = stamp
            val_logical_name = logicalName
            val_range = m
            val_flags = ValFlags(recValInfo, baseOrThis, isCompGen, inlineInfo, isMutable, isModuleOrMemberBinding, isExtensionMember, isIncrClassSpecialMember, isTyFunc, allowTypeInst, isGeneratedEventVal)
            val_type = ty
            val_opt_data =
                match compiledName, arity, konst, access, doc, specialRepr, actualParent, attribs with
                | None, None, None, TAccess [], doc, None, ParentNone, [] when doc.IsEmpty -> None
                | _ -> 
                    Some { Val.NewEmptyValOptData() with
                             val_compiled_name = (match compiledName with Some v when v <> logicalName -> compiledName | _ -> None)
                             val_repr_info = arity
                             val_const = konst
                             val_access = access
                             val_xmldoc = doc
                             val_member_info = specialRepr
                             val_declaring_entity = actualParent
                             val_attribs = attribs }
            }

    /// Create the new contents of an overall assembly
    static member NewCcuContents sref m nm mty =
        Construct.NewModuleOrNamespace (Some(CompPath(sref, []))) taccessPublic (ident(nm, m)) XmlDoc.Empty [] (MaybeLazy.Strict mty)

    /// Create a tycon based on an existing one using the function 'f'. 
    /// We require that we be given the new parent for the new tycon. 
    /// We pass the new tycon to 'f' in case it needs to reparent the 
    /// contents of the tycon. 
    static member NewModifiedTycon f (orig: Tycon) = 
        let data = { orig with entity_stamp = newStamp() }
        Tycon.New "NewModifiedTycon" (f data) 
    
    /// Create a module Tycon based on an existing one using the function 'f'. 
    /// We require that we be given the parent for the new module. 
    /// We pass the new module to 'f' in case it needs to reparent the 
    /// contents of the module. 
    static member NewModifiedModuleOrNamespace f orig = 
        orig |> Construct.NewModifiedTycon (fun d -> 
            { d with entity_modul_contents = MaybeLazy.Strict (f (d.entity_modul_contents.Force())) }) 

    /// Create a Val based on an existing one using the function 'f'. 
    /// We require that we be given the parent for the new Val. 
    static member NewModifiedVal f (orig: Val) = 
        let stamp = newStamp() 
        let data' = f { orig with val_stamp=stamp }
        Val.New data'

    /// Create a new module or namespace node by cloning an existing one
    static member NewClonedModuleOrNamespace orig =
        Construct.NewModifiedModuleOrNamespace (fun mty -> mty) orig

    /// Create a new type definition node by cloning an existing one
    static member NewClonedTycon orig =
        Construct.NewModifiedTycon (fun d -> d) orig

#if !NO_EXTENSIONTYPING
    /// Compute the definition location of a provided item
    static member ComputeDefinitionLocationOfProvidedItem<'T when 'T :> IProvidedCustomAttributeProvider> (p: Tainted<'T>) : range option =
        let attrs = p.PUntaintNoFailure(fun x -> x.GetDefinitionLocationAttribute(p.TypeProvider.PUntaintNoFailure id))
        match attrs with
        | None | Some (null, _, _) -> None
        | Some (filePath, line, column) -> 
            // Coordinates from type provider are 1-based for lines and columns
            // Coordinates internally in the F# compiler are 1-based for lines and 0-based for columns
            let pos = Position.mkPos line (max 0 (column - 1)) 
            Range.mkRange filePath pos pos |> Some
#endif

