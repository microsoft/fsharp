// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.EditorServices

open FSharp.Compiler.Text
open FSharp.Compiler.AbstractIL.IL
    
/// Represents a type in an external (non F#) assembly.
[<RequireQualifiedAccess>]
type public FindDeclExternalType =
    /// Type defined in non-F# assembly.
    | Type of fullName: string * genericArgs: FindDeclExternalType list

    /// Array of type that is defined in non-F# assembly.
    | Array of inner: FindDeclExternalType

    /// Pointer defined in non-F# assembly.
    | Pointer of inner: FindDeclExternalType

    /// Type variable defined in non-F# assembly.
    | TypeVar of typeName: string

    override ToString : unit -> string
        
module internal FindDeclExternalType =
    val internal tryOfILType : string array -> ILType -> FindDeclExternalType option

/// Represents the type of a single method parameter
[<Sealed>]
type public FindDeclExternalParam =

    member IsByRef: bool

    member ParameterType: FindDeclExternalType

    static member Create: parameterType: FindDeclExternalType * isByRef: bool -> FindDeclExternalParam

    override ToString : unit -> string

module internal FindDeclExternalParam =

    val internal tryOfILType : string array -> ILType -> FindDeclExternalParam option

    val internal tryOfILTypes : string array -> ILType list -> FindDeclExternalParam list option

/// Represents a symbol in an external (non F#) assembly
[<RequireQualifiedAccess>]
type public FindDeclExternalSymbol =
    | Type of fullName: string

    | Constructor of typeName: string * args: FindDeclExternalParam list

    | Method of typeName: string * name: string * paramSyms: FindDeclExternalParam list * genericArity: int 

    | Field of typeName: string * name: string

    | Event of typeName: string * name: string

    | Property of typeName: string * name: string

    override ToString : unit -> string

    member internal ToDebuggerDisplay : unit -> string

/// Represents the reason why the GetDeclarationLocation operation failed.
[<RequireQualifiedAccess>]
type public FindDeclFailureReason = 

    /// Generic reason: no particular information about error apart from a message
    | Unknown of message: string

    /// Source code file is not available
    | NoSourceCode

    /// Trying to find declaration of ProvidedType without TypeProviderDefinitionLocationAttribute
    | ProvidedType of typeName: string

    /// Trying to find declaration of ProvidedMember without TypeProviderDefinitionLocationAttribute
    | ProvidedMember of memberName: string

/// Represents the result of the GetDeclarationLocation operation.
[<RequireQualifiedAccess>]
type public FindDeclResult = 

    /// Indicates a declaration location was not found, with an additional reason
    | DeclNotFound of FindDeclFailureReason

    /// Indicates a declaration location was found
    | DeclFound of location: range

    /// Indicates an external declaration was found
    | ExternalDecl of assembly: string * externalSym : FindDeclExternalSymbol
     
