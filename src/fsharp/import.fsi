// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Functions to import .NET binary metadata as TAST objects
module internal FSharp.Compiler.Import

open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.TypedTree

#if !NO_EXTENSIONTYPING
open FSharp.Compiler.ExtensionTyping
#endif

/// Represents an interface to some of the functionality of TcImports, for loading assemblies 
/// and accessing information about generated provided assemblies.
type AssemblyLoader = 

    /// Resolve an Abstract IL assembly reference to a Ccu
    abstract FindCcuFromAssemblyRef : CompilationThreadToken * range * ILAssemblyRef -> CcuResolutionResult

    abstract TryFindXmlDocumentationInfo : assemblyName: string -> XmlDocumentationInfo option

#if !NO_EXTENSIONTYPING
    /// Get a flag indicating if an assembly is a provided assembly, plus the
    /// table of information recording remappings from type names in the provided assembly to type
    /// names in the statically linked, embedded assembly.
    abstract GetProvidedAssemblyInfo : CompilationThreadToken * range * Tainted<ProvidedAssembly> -> bool * ProvidedAssemblyStaticLinkingMap option

    /// Record a root for a [<Generate>] type to help guide static linking & type relocation
    abstract RecordGeneratedTypeRoot : ProviderGeneratedType -> unit
#endif

/// Represents a context used for converting AbstractIL .NET and provided types to F# internal compiler data structures.
/// Also cache the conversion of AbstractIL ILTypeRef nodes, based on hashes of these.
///
/// There is normally only one ImportMap for any assembly compilation, though additional instances can be created
/// using tcImports.GetImportMap() if needed, and it is not harmful if multiple instances are used. The object 
/// serves as an interface through to the tables stored in the primary TcImports structures defined in CompileOps.fs. 
[<SealedAttribute>]
type ImportMap =
    new : g:TcGlobals * assemblyLoader:AssemblyLoader -> ImportMap
    
    /// The AssemblyLoader for the import context
    member assemblyLoader : AssemblyLoader

    /// The TcGlobals for the import context
    member g : TcGlobals

/// Import a reference to a type definition, given an AbstractIL ILTypeRef, with caching
val internal ImportILTypeRef : ImportMap -> range -> ILTypeRef -> TyconRef

/// Pre-check for ability to import a reference to a type definition, given an AbstractIL ILTypeRef, with caching
val internal CanImportILTypeRef : ImportMap -> range -> ILTypeRef -> bool

/// Import an IL type as an F# type.
val internal ImportILType : ImportMap -> range -> TType list -> ILType -> TType

/// Pre-check for ability to import an IL type as an F# type.
val internal CanImportILType : ImportMap -> range -> ILType -> bool

#if !NO_EXTENSIONTYPING
/// Import a provided type as an F# type.
val internal ImportProvidedType : ImportMap -> range -> (* TType list -> *) Tainted<ProvidedType> -> TType

/// Import a provided type reference as an F# type TyconRef
val internal ImportProvidedNamedType : ImportMap -> range -> (* TType list -> *) Tainted<ProvidedType> -> TyconRef

/// Import a provided type as an AbstractIL type
val internal ImportProvidedTypeAsILType : ImportMap -> range -> Tainted<ProvidedType> -> ILType

/// Import a provided method reference as an Abstract IL method reference
val internal ImportProvidedMethodBaseAsILMethodRef : ImportMap -> range -> Tainted<ProvidedMethodBase> -> ILMethodRef
#endif

/// Import a set of Abstract IL generic parameter specifications as a list of new F# generic parameters.  
val internal ImportILGenericParameters : (unit -> ImportMap) -> range -> ILScopeRef -> TType list -> ILGenericParameterDef list -> Typar list

/// Import an IL assembly as a new TAST CCU
val internal ImportILAssembly : (unit -> ImportMap) * range * (ILScopeRef -> ILModuleDef) * IXmlDocumentationInfoLoader option * ILScopeRef * sourceDir:string * filename: string option * ILModuleDef * IEvent<string> -> CcuThunk

/// Import the type forwarder table for an IL assembly
val internal ImportILAssemblyTypeForwarders : (unit -> ImportMap) * range * ILExportedTypesAndForwarders -> Map<(string array * string), Lazy<EntityRef>>
