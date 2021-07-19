// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Some general F# utilities for mangling / unmangling / manipulating names.
/// Anything to do with special names of identifiers and other lexical rules 
module public FSharp.Compiler.Syntax.PrettyNaming

open FSharp.Compiler.Text
open FSharp.Compiler.Text

[<LiteralAttribute>]
val internal parenGet: string = ".()"

[<LiteralAttribute>]
val internal parenSet: string = ".()<-"

[<LiteralAttribute>]
val internal qmark: string = "?"

[<LiteralAttribute>]
val internal qmarkSet: string = "?<-"

/// Prefix for compiled (mangled) operator names.
[<LiteralAttribute>]
val internal opNamePrefix: string = "op_"

/// Returns `true` if given string is an operator or double backticked name, e.g. ( |>> ) or ( long identifier ).
/// (where ( long identifier ) is the display name for ``long identifier``).
val IsOperatorOrBacktickedName: name:string -> bool

/// Returns `true` if given string is an operator display name, e.g. ( |>> )
val IsOperatorName: name:string -> bool

val IsMangledOpName: n:string -> bool

/// Compiles an operator into a mangled operator name.
/// For example, "!%" becomes "op_DereferencePercent".
/// This function accepts both built-in and custom operators.
val CompileOpName: string -> string

/// Decompiles a mangled operator name back into an operator.
/// For example, "op_DereferencePercent" becomes "!%".
/// This function accepts mangled names for both built-in and custom operators.
val DecompileOpName: string -> string

val DemangleOperatorName: nm:string -> string

val internal DemangleOperatorNameAsLayout: nonOpTagged:(string -> #TaggedText) -> nm:string -> Layout

val internal opNameCons: string

val internal opNameNil: string

val internal opNameEquals: string

val internal opNameEqualsNullable: string

val internal opNameNullableEquals: string

val internal opNameNullableEqualsNullable: string

/// The characters that are allowed to be the first character of an identifier.
val IsIdentifierFirstCharacter: c:char -> bool

/// The characters that are allowed to be in an identifier.
val IsIdentifierPartCharacter: c:char -> bool

/// Is this character a part of a long identifier?
val IsLongIdentifierPartCharacter: c:char -> bool

val internal isTildeOnlyString: s:string -> bool

val internal IsValidPrefixOperatorUse: s:string -> bool

val internal IsValidPrefixOperatorDefinitionName: s:string -> bool

val IsPrefixOperator: s:string -> bool

val IsPunctuation: s:string -> bool

val IsTernaryOperator: s:string -> bool

val IsInfixOperator: string -> bool

val internal ( |Control|Equality|Relational|Indexer|FixedTypes|Other| ):
    opName:string -> Choice<unit,unit,unit,unit,unit,unit>

val IsCompilerGeneratedName: nm:string -> bool

val internal CompilerGeneratedName: nm:string -> string

val internal GetBasicNameOfPossibleCompilerGeneratedName: name:string -> string

val internal CompilerGeneratedNameSuffix: basicName:string -> suffix:string -> string

val internal TryDemangleGenericNameAndPos: n:string -> int voption

type internal NameArityPair = | NameArityPair of string * int

val internal DemangleGenericTypeNameWithPos: pos:int -> mangledName:string -> string

val internal DecodeGenericTypeNameWithPos: pos:int -> mangledName:string -> NameArityPair

val internal DemangleGenericTypeName: mangledName:string -> string

val internal DecodeGenericTypeName: mangledName:string -> NameArityPair

/// Try to chop "get_" or "set_" from a string
val TryChopPropertyName: s:string -> string option

/// Try to chop "get_" or "set_" from a string.
/// If the string does not start with "get_" or "set_", this function raises an exception.
val internal ChopPropertyName: s:string -> string

val internal SplitNamesForILPath: s:string -> string list

[<LiteralAttribute>]
val internal FSharpModuleSuffix: string = "Module"

[<LiteralAttribute>]
val internal MangledGlobalName: string = "`global`"

val internal IllegalCharactersInTypeAndNamespaceNames: char []

/// Determines if the specified name is a valid name for an active pattern.
val IsActivePatternName: name:string -> bool

type internal ActivePatternInfo =
    | APInfo of bool * (string * range) list * range
    member ActiveTags: string list
    member ActiveTagsWithRanges: (string * range) list
    member IsTotal: bool
    member Range: range
  
val internal ActivePatternInfoOfValName: nm:string -> m:range -> ActivePatternInfo option

exception internal InvalidMangledStaticArg of string

val internal demangleProvidedTypeName: typeLogicalName:string -> string * (string * string) []

/// Mangle the static parameters for a provided type or method
val internal mangleProvidedTypeName: typeLogicalName:string * nonDefaultArgs:(string * string) [] -> string

/// Mangle the static parameters for a provided type or method
val internal computeMangledNameWithoutDefaultArgValues: nm:string * staticArgs:'a [] * defaultArgValues:(string * string option) [] -> string

val internal outArgCompilerGeneratedName: string

val internal ExtraWitnessMethodName: nm:string -> string

/// Reuses generated union case field name objects for common field numbers
val internal mkUnionCaseFieldName: (int -> int -> string)

/// Reuses generated exception field name objects for common field numbers
val internal mkExceptionFieldName: (int -> string)

/// The prefix of the names used for the fake namespace path added to all dynamic code entries in FSI.EXE
val FsiDynamicModulePrefix: string

module internal FSharpLib =
    val Root: string
    val RootPath: string list
    val Core: string
    val CorePath: string list

module internal CustomOperations =
    [<LiteralAttribute>]
    val Into: string = "into"

val internal unassignedTyparName: string

val internal FSharpOptimizationDataResourceName: string

val internal FSharpSignatureDataResourceName: string

val internal FSharpOptimizationDataResourceName2: string

val internal FSharpSignatureDataResourceName2: string

val GetLongNameFromString: string -> string list

val FormatAndOtherOverloadsString: int -> string

val FSharpSignatureDataResourceName2: string

/// Mark some variables (the ones we introduce via abstractBigTargets) as don't-eliminate 
[<Literal>] 
val internal suffixForVariablesThatMayNotBeEliminated : string = "$cont"

/// Indicates a ValRef generated to facilitate tuple eliminations
[<Literal>] 
val internal suffixForTupleElementAssignmentTarget : string = "$tupleElem"

[<Literal>] 
val internal stackVarPrefix : string = "__stack_"

