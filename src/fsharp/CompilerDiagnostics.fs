// Copyright (c) Microsoft Corporation. All Rights Reserved. See License.txt in the project root for license information.

/// Contains logic to prepare, post-process, filter and emit compiler diagnsotics
module internal FSharp.Compiler.CompilerDiagnostics

open System
open System.Diagnostics
open System.IO
open System.Text

open Internal.Utilities
open Internal.Utilities.Filename
open Internal.Utilities.Text

open FSharp.Compiler
open FSharp.Compiler.AbstractIL.Internal.Library
open FSharp.Compiler.AttributeChecking
open FSharp.Compiler.CheckExpressions
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.ConstraintSolver
open FSharp.Compiler.DiagnosticMessage
open FSharp.Compiler.ErrorLogger
open FSharp.Compiler.Infos
open FSharp.Compiler.Lexhelp
open FSharp.Compiler.Lib
open FSharp.Compiler.MethodCalls
open FSharp.Compiler.MethodOverrides
open FSharp.Compiler.NameResolution
open FSharp.Compiler.ParseHelpers
open FSharp.Compiler.SourceCodeServices.PrettyNaming
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.SyntaxTree
open FSharp.Compiler.Text.Pos
open FSharp.Compiler.Text.Range
open FSharp.Compiler.SignatureConformance
open FSharp.Compiler.Text
open FSharp.Compiler.TextLayout
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps

#if DEBUG
[<AutoOpen>]
module internal CompilerService =
    let showAssertForUnexpectedException = ref true
#endif // DEBUG
        
/// This exception is an old-style way of reporting a diagnostic
exception HashIncludeNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception HashReferenceNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception HashLoadedSourceHasIssues of (*warnings*) exn list * (*errors*) exn list * range

/// This exception is an old-style way of reporting a diagnostic
exception HashLoadedScriptConsideredSource of range

/// This exception is an old-style way of reporting a diagnostic
exception HashDirectiveNotAllowedInNonScript of range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionFull of string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionForHtmlDoc of string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionSuggestAlternative of string * string * range

/// This exception is an old-style way of reporting a diagnostic
exception DeprecatedCommandLineOptionNoDescription of string * range

/// This exception is an old-style way of reporting a diagnostic
exception InternalCommandLineOption of string * range

let GetRangeOfDiagnostic(err: PhasedDiagnostic) = 
  let rec RangeFromException = function
      | ErrorFromAddingConstraint(_, err2, _) -> RangeFromException err2 
#if !NO_EXTENSIONTYPING
      | ExtensionTyping.ProvidedTypeResolutionNoRange e -> RangeFromException e
      | ExtensionTyping.ProvidedTypeResolution(m, _)
#endif
      | ReservedKeyword(_, m)
      | IndentationProblem(_, m)
      | ErrorFromAddingTypeEquation(_, _, _, _, _, m) 
      | ErrorFromApplyingDefault(_, _, _, _, _, m) 
      | ErrorsFromAddingSubsumptionConstraint(_, _, _, _, _, _, m)
      | FunctionExpected(_, _, m)
      | BakedInMemberConstraintName(_, m)
      | StandardOperatorRedefinitionWarning(_, m)
      | BadEventTransformation m
      | ParameterlessStructCtor m
      | FieldNotMutable (_, _, m) 
      | Recursion (_, _, _, _, m) 
      | InvalidRuntimeCoercion(_, _, _, m) 
      | IndeterminateRuntimeCoercion(_, _, _, m)
      | IndeterminateStaticCoercion (_, _, _, m)
      | StaticCoercionShouldUseBox (_, _, _, m)
      | CoercionTargetSealed(_, _, m)
      | UpcastUnnecessary m
      | QuotationTranslator.IgnoringPartOfQuotedTermWarning (_, m) 
      
      | TypeTestUnnecessary m
      | RuntimeCoercionSourceSealed(_, _, m)
      | OverrideDoesntOverride(_, _, _, _, _, m)
      | UnionPatternsBindDifferentNames m 
      | UnionCaseWrongArguments (_, _, _, m) 
      | TypeIsImplicitlyAbstract m 
      | RequiredButNotSpecified (_, _, _, _, m) 
      | FunctionValueUnexpected (_, _, m)
      | UnitTypeExpected (_, _, m)
      | UnitTypeExpectedWithEquality (_, _, m)
      | UnitTypeExpectedWithPossiblePropertySetter (_, _, _, _, m)
      | UnitTypeExpectedWithPossibleAssignment (_, _, _, _, m)
      | UseOfAddressOfOperator m 
      | DeprecatedThreadStaticBindingWarning m 
      | NonUniqueInferredAbstractSlot (_, _, _, _, _, m) 
      | DefensiveCopyWarning (_, m)
      | LetRecCheckedAtRuntime m 
      | UpperCaseIdentifierInPattern m
      | NotUpperCaseConstructor m
      | RecursiveUseCheckedAtRuntime (_, _, m) 
      | LetRecEvaluatedOutOfOrder (_, _, _, m) 
      | Error (_, m)
      | ErrorWithSuggestions (_, m, _, _)
      | NumberedError (_, m)
      | SyntaxError (_, m) 
      | InternalError (_, m)
      | InterfaceNotRevealed(_, _, m) 
      | WrappedError (_, m)
      | PatternMatchCompilation.MatchIncomplete (_, _, m)
      | PatternMatchCompilation.EnumMatchIncomplete (_, _, m)
      | PatternMatchCompilation.RuleNeverMatched m 
      | ValNotMutable(_, _, m)
      | ValNotLocal(_, _, m) 
      | MissingFields(_, m) 
      | OverrideInIntrinsicAugmentation m
      | IntfImplInIntrinsicAugmentation m 
      | OverrideInExtrinsicAugmentation m
      | IntfImplInExtrinsicAugmentation m 
      | ValueRestriction(_, _, _, _, m) 
      | LetRecUnsound (_, _, m) 
      | ObsoleteError (_, m) 
      | ObsoleteWarning (_, m) 
      | Experimental (_, m) 
      | PossibleUnverifiableCode m
      | UserCompilerMessage (_, _, m) 
      | Deprecated(_, m) 
      | LibraryUseOnly m 
      | FieldsFromDifferentTypes (_, _, _, m) 
      | IndeterminateType m
      | TyconBadArgs(_, _, _, m) -> 
          Some m

      | FieldNotContained(_, arf, _, _) -> Some arf.Range
      | ValueNotContained(_, _, aval, _, _) -> Some aval.Range
      | ConstrNotContained(_, aval, _, _) -> Some aval.Id.idRange
      | ExnconstrNotContained(_, aexnc, _, _) -> Some aexnc.Range

      | VarBoundTwice id 
      | UndefinedName(_, _, id, _) -> 
          Some id.idRange 

      | Duplicate(_, _, m) 
      | NameClash(_, _, _, m, _, _, _) 
      | UnresolvedOverloading(_, _, _, m) 
      | UnresolvedConversionOperator (_, _, _, m)
      | VirtualAugmentationOnNullValuedType m
      | NonVirtualAugmentationOnNullValuedType m
      | NonRigidTypar(_, _, _, _, _, m)
      | ConstraintSolverTupleDiffLengths(_, _, _, m, _) 
      | ConstraintSolverInfiniteTypes(_, _, _, _, m, _) 
      | ConstraintSolverMissingConstraint(_, _, _, m, _) 
      | ConstraintSolverTypesNotInEqualityRelation(_, _, _, m, _, _)
      | ConstraintSolverError(_, m, _) 
      | ConstraintSolverTypesNotInSubsumptionRelation(_, _, _, m, _) 
      | ConstraintSolverRelatedInformation(_, m, _) 
      | SelfRefObjCtor(_, m) -> 
          Some m

      | NotAFunction(_, _, mfun, _) -> 
          Some mfun
          
      | NotAFunctionButIndexer(_, _, _, mfun, _) -> 
          Some mfun

      | IllegalFileNameChar(_) -> Some rangeCmdArgs

      | UnresolvedReferenceError(_, m) 
      | UnresolvedPathReference(_, _, m) 
      | DeprecatedCommandLineOptionFull(_, m) 
      | DeprecatedCommandLineOptionForHtmlDoc(_, m) 
      | DeprecatedCommandLineOptionSuggestAlternative(_, _, m) 
      | DeprecatedCommandLineOptionNoDescription(_, m) 
      | InternalCommandLineOption(_, m)
      | HashIncludeNotAllowedInNonScript m
      | HashReferenceNotAllowedInNonScript m 
      | HashDirectiveNotAllowedInNonScript m  
      | FileNameNotResolved(_, _, m) 
      | LoadedSourceNotFoundIgnoring(_, m) 
      | MSBuildReferenceResolutionWarning(_, _, m) 
      | MSBuildReferenceResolutionError(_, _, m) 
      | AssemblyNotResolved(_, m) 
      | HashLoadedSourceHasIssues(_, _, m) 
      | HashLoadedScriptConsideredSource m -> 
          Some m
      // Strip TargetInvocationException wrappers
      | :? System.Reflection.TargetInvocationException as e -> 
          RangeFromException e.InnerException
#if !NO_EXTENSIONTYPING
      | :? TypeProviderError as e -> e.Range |> Some
#endif
      
      | _ -> None
  
  RangeFromException err.Exception

let GetDiagnosticNumber(err: PhasedDiagnostic) = 
    let rec GetFromException(e: exn) = 
      match e with
      (* DO NOT CHANGE THESE NUMBERS *)
      | ErrorFromAddingTypeEquation _ -> 1
      | FunctionExpected _ -> 2
      | NotAFunctionButIndexer _ -> 3217
      | NotAFunction _ -> 3
      | FieldNotMutable _ -> 5
      | Recursion _ -> 6
      | InvalidRuntimeCoercion _ -> 7
      | IndeterminateRuntimeCoercion _ -> 8
      | PossibleUnverifiableCode _ -> 9
      | SyntaxError _ -> 10
      // 11 cannot be reused
      // 12 cannot be reused
      | IndeterminateStaticCoercion _ -> 13
      | StaticCoercionShouldUseBox _ -> 14
      // 15 cannot be reused
      | RuntimeCoercionSourceSealed _ -> 16 
      | OverrideDoesntOverride _ -> 17
      | UnionPatternsBindDifferentNames _ -> 18
      | UnionCaseWrongArguments _ -> 19
      | UnitTypeExpected _ -> 20
      | UnitTypeExpectedWithEquality _ -> 20
      | UnitTypeExpectedWithPossiblePropertySetter _ -> 20
      | UnitTypeExpectedWithPossibleAssignment _ -> 20
      | RecursiveUseCheckedAtRuntime _ -> 21
      | LetRecEvaluatedOutOfOrder _ -> 22
      | NameClash _ -> 23
      // 24 cannot be reused
      | PatternMatchCompilation.MatchIncomplete _ -> 25
      | PatternMatchCompilation.RuleNeverMatched _ -> 26
      | ValNotMutable _ -> 27
      | ValNotLocal _ -> 28
      | MissingFields _ -> 29
      | ValueRestriction _ -> 30
      | LetRecUnsound _ -> 31
      | FieldsFromDifferentTypes _ -> 32
      | TyconBadArgs _ -> 33
      | ValueNotContained _ -> 34
      | Deprecated _ -> 35
      | ConstrNotContained _ -> 36
      | Duplicate _ -> 37
      | VarBoundTwice _ -> 38
      | UndefinedName _ -> 39
      | LetRecCheckedAtRuntime _ -> 40
      | UnresolvedOverloading _ -> 41
      | LibraryUseOnly _ -> 42
      | ErrorFromAddingConstraint _ -> 43
      | ObsoleteWarning _ -> 44
      | ReservedKeyword _ -> 46
      | SelfRefObjCtor _ -> 47
      | VirtualAugmentationOnNullValuedType _ -> 48
      | UpperCaseIdentifierInPattern _ -> 49
      | InterfaceNotRevealed _ -> 50
      | UseOfAddressOfOperator _ -> 51
      | DefensiveCopyWarning _ -> 52
      | NotUpperCaseConstructor _ -> 53
      | TypeIsImplicitlyAbstract _ -> 54
      // 55 cannot be reused
      | DeprecatedThreadStaticBindingWarning _ -> 56
      | Experimental _ -> 57
      | IndentationProblem _ -> 58
      | CoercionTargetSealed _ -> 59 
      | OverrideInIntrinsicAugmentation _ -> 60
      | NonVirtualAugmentationOnNullValuedType _ -> 61
      | UserCompilerMessage (_, n, _) -> n
      | ExnconstrNotContained _ -> 63
      | NonRigidTypar _ -> 64
      // 65 cannot be reused
      | UpcastUnnecessary _ -> 66
      | TypeTestUnnecessary _ -> 67
      | QuotationTranslator.IgnoringPartOfQuotedTermWarning _ -> 68
      | IntfImplInIntrinsicAugmentation _ -> 69
      | NonUniqueInferredAbstractSlot _ -> 70
      | ErrorFromApplyingDefault _ -> 71
      | IndeterminateType _ -> 72
      | InternalError _ -> 73
      | UnresolvedReferenceNoRange _
      | UnresolvedReferenceError _ 
      | UnresolvedPathReferenceNoRange _ 
      | UnresolvedPathReference _ -> 74
      | DeprecatedCommandLineOptionFull _
      | DeprecatedCommandLineOptionForHtmlDoc _
      | DeprecatedCommandLineOptionSuggestAlternative _
      | DeprecatedCommandLineOptionNoDescription _ 
      | InternalCommandLineOption _ -> 75
      | HashIncludeNotAllowedInNonScript _ 
      | HashReferenceNotAllowedInNonScript _ 
      | HashDirectiveNotAllowedInNonScript _ -> 76
      | BakedInMemberConstraintName _ -> 77
      | FileNameNotResolved _ -> 78  
      | LoadedSourceNotFoundIgnoring _ -> 79
      // 80 cannot be reused
      | ParameterlessStructCtor _ -> 81
      | MSBuildReferenceResolutionWarning _ -> 82
      | MSBuildReferenceResolutionError _ -> 83
      | AssemblyNotResolved _ -> 84
      | HashLoadedSourceHasIssues _ -> 85
      | StandardOperatorRedefinitionWarning _ -> 86
      | InvalidInternalsVisibleToAssemblyName _ -> 87
      // 88 cannot be reused
      | OverrideInExtrinsicAugmentation _ -> 89
      | IntfImplInExtrinsicAugmentation _ -> 90
      | BadEventTransformation _ -> 91
      | HashLoadedScriptConsideredSource _ -> 92
      | UnresolvedConversionOperator _ -> 93
      // avoid 94-100 for safety
      | ObsoleteError _ -> 101
#if !NO_EXTENSIONTYPING
      | ExtensionTyping.ProvidedTypeResolutionNoRange _
      | ExtensionTyping.ProvidedTypeResolution _ -> 103
#endif
      | PatternMatchCompilation.EnumMatchIncomplete _ -> 104
       (* DO NOT CHANGE THE NUMBERS *)

      // Strip TargetInvocationException wrappers
      | :? System.Reflection.TargetInvocationException as e -> 
          GetFromException e.InnerException
      
      | WrappedError(e, _) -> GetFromException e   

      | Error ((n, _), _) -> n
      | ErrorWithSuggestions ((n, _), _, _, _) -> n
      | Failure _ -> 192
      | NumberedError((n, _), _) -> n
      | IllegalFileNameChar(fileName, invalidChar) -> fst (FSComp.SR.buildUnexpectedFileNameCharacter(fileName, string invalidChar))
#if !NO_EXTENSIONTYPING
      | :? TypeProviderError as e -> e.Number
#endif
      | ErrorsFromAddingSubsumptionConstraint (_, _, _, _, _, ContextInfo.DowncastUsedInsteadOfUpcast _, _) -> fst (FSComp.SR.considerUpcast("", ""))
      | _ -> 193
    GetFromException err.Exception
   
let GetWarningLevel err = 
    match err.Exception with 
    // Level 5 warnings
    | RecursiveUseCheckedAtRuntime _
    | LetRecEvaluatedOutOfOrder _
    | DefensiveCopyWarning _  -> 5

    | NumberedError((n, _), _)
    | ErrorWithSuggestions((n, _), _, _, _) 
    | Error((n, _), _) -> 
        // 1178, tcNoComparisonNeeded1, "The struct, record or union type '%s' is not structurally comparable because the type parameter %s does not satisfy the 'comparison' constraint..."
        // 1178, tcNoComparisonNeeded2, "The struct, record or union type '%s' is not structurally comparable because the type '%s' does not satisfy the 'comparison' constraint...."
        // 1178, tcNoEqualityNeeded1, "The struct, record or union type '%s' does not support structural equality because the type parameter %s does not satisfy the 'equality' constraint..."
        // 1178, tcNoEqualityNeeded2, "The struct, record or union type '%s' does not support structural equality because the type '%s' does not satisfy the 'equality' constraint...."
        if (n = 1178) then 5 else 2
    // Level 2 
    | _ -> 2

let warningOn err level specificWarnOn = 
    let n = GetDiagnosticNumber err
    List.contains n specificWarnOn ||
    // Some specific warnings are never on by default, i.e. unused variable warnings
    match n with 
    | 1182 -> false // chkUnusedValue - off by default
    | 3180 -> false // abImplicitHeapAllocation - off by default
    | _ -> level >= GetWarningLevel err 

let SplitRelatedDiagnostics(err: PhasedDiagnostic) : PhasedDiagnostic * PhasedDiagnostic list = 
    let ToPhased e = {Exception=e; Phase = err.Phase}
    let rec SplitRelatedException = function
      | ConstraintSolverRelatedInformation(fopt, m2, e) -> 
          let e, related = SplitRelatedException e
          ConstraintSolverRelatedInformation(fopt, m2, e.Exception)|>ToPhased, related
      | ErrorFromAddingTypeEquation(g, denv, t1, t2, e, m) ->
          let e, related = SplitRelatedException e
          ErrorFromAddingTypeEquation(g, denv, t1, t2, e.Exception, m)|>ToPhased, related
      | ErrorFromApplyingDefault(g, denv, tp, defaultType, e, m) ->  
          let e, related = SplitRelatedException e
          ErrorFromApplyingDefault(g, denv, tp, defaultType, e.Exception, m)|>ToPhased, related
      | ErrorsFromAddingSubsumptionConstraint(g, denv, t1, t2, e, contextInfo, m) ->  
          let e, related = SplitRelatedException e
          ErrorsFromAddingSubsumptionConstraint(g, denv, t1, t2, e.Exception, contextInfo, m)|>ToPhased, related
      | ErrorFromAddingConstraint(x, e, m) ->  
          let e, related = SplitRelatedException e
          ErrorFromAddingConstraint(x, e.Exception, m)|>ToPhased, related
      | WrappedError (e, m) -> 
          let e, related = SplitRelatedException e
          WrappedError(e.Exception, m)|>ToPhased, related
      // Strip TargetInvocationException wrappers
      | :? System.Reflection.TargetInvocationException as e -> 
          SplitRelatedException e.InnerException
      | e -> 
           ToPhased e, []
    SplitRelatedException err.Exception


let DeclareMessage = FSharp.Compiler.DiagnosticMessage.DeclareResourceString

do FSComp.SR.RunStartupValidation()
let SeeAlsoE() = DeclareResourceString("SeeAlso", "%s")
let ConstraintSolverTupleDiffLengthsE() = DeclareResourceString("ConstraintSolverTupleDiffLengths", "%d%d")
let ConstraintSolverInfiniteTypesE() = DeclareResourceString("ConstraintSolverInfiniteTypes", "%s%s")
let ConstraintSolverMissingConstraintE() = DeclareResourceString("ConstraintSolverMissingConstraint", "%s")
let ConstraintSolverTypesNotInEqualityRelation1E() = DeclareResourceString("ConstraintSolverTypesNotInEqualityRelation1", "%s%s")
let ConstraintSolverTypesNotInEqualityRelation2E() = DeclareResourceString("ConstraintSolverTypesNotInEqualityRelation2", "%s%s")
let ConstraintSolverTypesNotInSubsumptionRelationE() = DeclareResourceString("ConstraintSolverTypesNotInSubsumptionRelation", "%s%s%s")
let ErrorFromAddingTypeEquation1E() = DeclareResourceString("ErrorFromAddingTypeEquation1", "%s%s%s")
let ErrorFromAddingTypeEquation2E() = DeclareResourceString("ErrorFromAddingTypeEquation2", "%s%s%s")
let ErrorFromApplyingDefault1E() = DeclareResourceString("ErrorFromApplyingDefault1", "%s")
let ErrorFromApplyingDefault2E() = DeclareResourceString("ErrorFromApplyingDefault2", "")
let ErrorsFromAddingSubsumptionConstraintE() = DeclareResourceString("ErrorsFromAddingSubsumptionConstraint", "%s%s%s")
let UpperCaseIdentifierInPatternE() = DeclareResourceString("UpperCaseIdentifierInPattern", "")
let NotUpperCaseConstructorE() = DeclareResourceString("NotUpperCaseConstructor", "")
let FunctionExpectedE() = DeclareResourceString("FunctionExpected", "")
let BakedInMemberConstraintNameE() = DeclareResourceString("BakedInMemberConstraintName", "%s")
let BadEventTransformationE() = DeclareResourceString("BadEventTransformation", "")
let ParameterlessStructCtorE() = DeclareResourceString("ParameterlessStructCtor", "")
let InterfaceNotRevealedE() = DeclareResourceString("InterfaceNotRevealed", "%s")
let TyconBadArgsE() = DeclareResourceString("TyconBadArgs", "%s%d%d")
let IndeterminateTypeE() = DeclareResourceString("IndeterminateType", "")
let NameClash1E() = DeclareResourceString("NameClash1", "%s%s")
let NameClash2E() = DeclareResourceString("NameClash2", "%s%s%s%s%s")
let Duplicate1E() = DeclareResourceString("Duplicate1", "%s")
let Duplicate2E() = DeclareResourceString("Duplicate2", "%s%s")
let UndefinedName2E() = DeclareResourceString("UndefinedName2", "")
let FieldNotMutableE() = DeclareResourceString("FieldNotMutable", "")
let FieldsFromDifferentTypesE() = DeclareResourceString("FieldsFromDifferentTypes", "%s%s")
let VarBoundTwiceE() = DeclareResourceString("VarBoundTwice", "%s")
let RecursionE() = DeclareResourceString("Recursion", "%s%s%s%s")
let InvalidRuntimeCoercionE() = DeclareResourceString("InvalidRuntimeCoercion", "%s%s%s")
let IndeterminateRuntimeCoercionE() = DeclareResourceString("IndeterminateRuntimeCoercion", "%s%s")
let IndeterminateStaticCoercionE() = DeclareResourceString("IndeterminateStaticCoercion", "%s%s")
let StaticCoercionShouldUseBoxE() = DeclareResourceString("StaticCoercionShouldUseBox", "%s%s")
let TypeIsImplicitlyAbstractE() = DeclareResourceString("TypeIsImplicitlyAbstract", "")
let NonRigidTypar1E() = DeclareResourceString("NonRigidTypar1", "%s%s")
let NonRigidTypar2E() = DeclareResourceString("NonRigidTypar2", "%s%s")
let NonRigidTypar3E() = DeclareResourceString("NonRigidTypar3", "%s%s")
let OBlockEndSentenceE() = DeclareResourceString("BlockEndSentence", "")
let UnexpectedEndOfInputE() = DeclareResourceString("UnexpectedEndOfInput", "")
let UnexpectedE() = DeclareResourceString("Unexpected", "%s")
let NONTERM_interactionE() = DeclareResourceString("NONTERM.interaction", "")
let NONTERM_hashDirectiveE() = DeclareResourceString("NONTERM.hashDirective", "")
let NONTERM_fieldDeclE() = DeclareResourceString("NONTERM.fieldDecl", "")
let NONTERM_unionCaseReprE() = DeclareResourceString("NONTERM.unionCaseRepr", "")
let NONTERM_localBindingE() = DeclareResourceString("NONTERM.localBinding", "")
let NONTERM_hardwhiteLetBindingsE() = DeclareResourceString("NONTERM.hardwhiteLetBindings", "")
let NONTERM_classDefnMemberE() = DeclareResourceString("NONTERM.classDefnMember", "")
let NONTERM_defnBindingsE() = DeclareResourceString("NONTERM.defnBindings", "")
let NONTERM_classMemberSpfnE() = DeclareResourceString("NONTERM.classMemberSpfn", "")
let NONTERM_valSpfnE() = DeclareResourceString("NONTERM.valSpfn", "")
let NONTERM_tyconSpfnE() = DeclareResourceString("NONTERM.tyconSpfn", "")
let NONTERM_anonLambdaExprE() = DeclareResourceString("NONTERM.anonLambdaExpr", "")
let NONTERM_attrUnionCaseDeclE() = DeclareResourceString("NONTERM.attrUnionCaseDecl", "")
let NONTERM_cPrototypeE() = DeclareResourceString("NONTERM.cPrototype", "")
let NONTERM_objectImplementationMembersE() = DeclareResourceString("NONTERM.objectImplementationMembers", "")
let NONTERM_ifExprCasesE() = DeclareResourceString("NONTERM.ifExprCases", "")
let NONTERM_openDeclE() = DeclareResourceString("NONTERM.openDecl", "")
let NONTERM_fileModuleSpecE() = DeclareResourceString("NONTERM.fileModuleSpec", "")
let NONTERM_patternClausesE() = DeclareResourceString("NONTERM.patternClauses", "")
let NONTERM_beginEndExprE() = DeclareResourceString("NONTERM.beginEndExpr", "")
let NONTERM_recdExprE() = DeclareResourceString("NONTERM.recdExpr", "")
let NONTERM_tyconDefnE() = DeclareResourceString("NONTERM.tyconDefn", "")
let NONTERM_exconCoreE() = DeclareResourceString("NONTERM.exconCore", "")
let NONTERM_typeNameInfoE() = DeclareResourceString("NONTERM.typeNameInfo", "")
let NONTERM_attributeListE() = DeclareResourceString("NONTERM.attributeList", "")
let NONTERM_quoteExprE() = DeclareResourceString("NONTERM.quoteExpr", "")
let NONTERM_typeConstraintE() = DeclareResourceString("NONTERM.typeConstraint", "")
let NONTERM_Category_ImplementationFileE() = DeclareResourceString("NONTERM.Category.ImplementationFile", "")
let NONTERM_Category_DefinitionE() = DeclareResourceString("NONTERM.Category.Definition", "")
let NONTERM_Category_SignatureFileE() = DeclareResourceString("NONTERM.Category.SignatureFile", "")
let NONTERM_Category_PatternE() = DeclareResourceString("NONTERM.Category.Pattern", "")
let NONTERM_Category_ExprE() = DeclareResourceString("NONTERM.Category.Expr", "")
let NONTERM_Category_TypeE() = DeclareResourceString("NONTERM.Category.Type", "")
let NONTERM_typeArgsActualE() = DeclareResourceString("NONTERM.typeArgsActual", "")
let TokenName1E() = DeclareResourceString("TokenName1", "%s")
let TokenName1TokenName2E() = DeclareResourceString("TokenName1TokenName2", "%s%s")
let TokenName1TokenName2TokenName3E() = DeclareResourceString("TokenName1TokenName2TokenName3", "%s%s%s")
let RuntimeCoercionSourceSealed1E() = DeclareResourceString("RuntimeCoercionSourceSealed1", "%s")
let RuntimeCoercionSourceSealed2E() = DeclareResourceString("RuntimeCoercionSourceSealed2", "%s")
let CoercionTargetSealedE() = DeclareResourceString("CoercionTargetSealed", "%s")
let UpcastUnnecessaryE() = DeclareResourceString("UpcastUnnecessary", "")
let TypeTestUnnecessaryE() = DeclareResourceString("TypeTestUnnecessary", "")
let OverrideDoesntOverride1E() = DeclareResourceString("OverrideDoesntOverride1", "%s")
let OverrideDoesntOverride2E() = DeclareResourceString("OverrideDoesntOverride2", "%s")
let OverrideDoesntOverride3E() = DeclareResourceString("OverrideDoesntOverride3", "%s")
let OverrideDoesntOverride4E() = DeclareResourceString("OverrideDoesntOverride4", "%s")
let UnionCaseWrongArgumentsE() = DeclareResourceString("UnionCaseWrongArguments", "%d%d")
let UnionPatternsBindDifferentNamesE() = DeclareResourceString("UnionPatternsBindDifferentNames", "")
let RequiredButNotSpecifiedE() = DeclareResourceString("RequiredButNotSpecified", "%s%s%s")
let UseOfAddressOfOperatorE() = DeclareResourceString("UseOfAddressOfOperator", "")
let DefensiveCopyWarningE() = DeclareResourceString("DefensiveCopyWarning", "%s")
let DeprecatedThreadStaticBindingWarningE() = DeclareResourceString("DeprecatedThreadStaticBindingWarning", "")
let FunctionValueUnexpectedE() = DeclareResourceString("FunctionValueUnexpected", "%s")
let UnitTypeExpectedE() = DeclareResourceString("UnitTypeExpected", "%s")
let UnitTypeExpectedWithEqualityE() = DeclareResourceString("UnitTypeExpectedWithEquality", "%s")
let UnitTypeExpectedWithPossiblePropertySetterE() = DeclareResourceString("UnitTypeExpectedWithPossiblePropertySetter", "%s%s%s")
let UnitTypeExpectedWithPossibleAssignmentE() = DeclareResourceString("UnitTypeExpectedWithPossibleAssignment", "%s%s")
let UnitTypeExpectedWithPossibleAssignmentToMutableE() = DeclareResourceString("UnitTypeExpectedWithPossibleAssignmentToMutable", "%s%s")
let RecursiveUseCheckedAtRuntimeE() = DeclareResourceString("RecursiveUseCheckedAtRuntime", "")
let LetRecUnsound1E() = DeclareResourceString("LetRecUnsound1", "%s")
let LetRecUnsound2E() = DeclareResourceString("LetRecUnsound2", "%s%s")
let LetRecUnsoundInnerE() = DeclareResourceString("LetRecUnsoundInner", "%s")
let LetRecEvaluatedOutOfOrderE() = DeclareResourceString("LetRecEvaluatedOutOfOrder", "")
let LetRecCheckedAtRuntimeE() = DeclareResourceString("LetRecCheckedAtRuntime", "")
let SelfRefObjCtor1E() = DeclareResourceString("SelfRefObjCtor1", "")
let SelfRefObjCtor2E() = DeclareResourceString("SelfRefObjCtor2", "")
let VirtualAugmentationOnNullValuedTypeE() = DeclareResourceString("VirtualAugmentationOnNullValuedType", "")
let NonVirtualAugmentationOnNullValuedTypeE() = DeclareResourceString("NonVirtualAugmentationOnNullValuedType", "")
let NonUniqueInferredAbstractSlot1E() = DeclareResourceString("NonUniqueInferredAbstractSlot1", "%s")
let NonUniqueInferredAbstractSlot2E() = DeclareResourceString("NonUniqueInferredAbstractSlot2", "")
let NonUniqueInferredAbstractSlot3E() = DeclareResourceString("NonUniqueInferredAbstractSlot3", "%s%s")
let NonUniqueInferredAbstractSlot4E() = DeclareResourceString("NonUniqueInferredAbstractSlot4", "")
let Failure3E() = DeclareResourceString("Failure3", "%s")
let Failure4E() = DeclareResourceString("Failure4", "%s")
let MatchIncomplete1E() = DeclareResourceString("MatchIncomplete1", "")
let MatchIncomplete2E() = DeclareResourceString("MatchIncomplete2", "%s")
let MatchIncomplete3E() = DeclareResourceString("MatchIncomplete3", "%s")
let MatchIncomplete4E() = DeclareResourceString("MatchIncomplete4", "")
let RuleNeverMatchedE() = DeclareResourceString("RuleNeverMatched", "")
let EnumMatchIncomplete1E() = DeclareResourceString("EnumMatchIncomplete1", "")
let ValNotMutableE() = DeclareResourceString("ValNotMutable", "%s")
let ValNotLocalE() = DeclareResourceString("ValNotLocal", "")
let Obsolete1E() = DeclareResourceString("Obsolete1", "")
let Obsolete2E() = DeclareResourceString("Obsolete2", "%s")
let ExperimentalE() = DeclareResourceString("Experimental", "%s")
let PossibleUnverifiableCodeE() = DeclareResourceString("PossibleUnverifiableCode", "")
let DeprecatedE() = DeclareResourceString("Deprecated", "%s")
let LibraryUseOnlyE() = DeclareResourceString("LibraryUseOnly", "")
let MissingFieldsE() = DeclareResourceString("MissingFields", "%s")
let ValueRestriction1E() = DeclareResourceString("ValueRestriction1", "%s%s%s")
let ValueRestriction2E() = DeclareResourceString("ValueRestriction2", "%s%s%s")
let ValueRestriction3E() = DeclareResourceString("ValueRestriction3", "%s")
let ValueRestriction4E() = DeclareResourceString("ValueRestriction4", "%s%s%s")
let ValueRestriction5E() = DeclareResourceString("ValueRestriction5", "%s%s%s")
let RecoverableParseErrorE() = DeclareResourceString("RecoverableParseError", "")
let ReservedKeywordE() = DeclareResourceString("ReservedKeyword", "%s")
let IndentationProblemE() = DeclareResourceString("IndentationProblem", "%s")
let OverrideInIntrinsicAugmentationE() = DeclareResourceString("OverrideInIntrinsicAugmentation", "")
let OverrideInExtrinsicAugmentationE() = DeclareResourceString("OverrideInExtrinsicAugmentation", "")
let IntfImplInIntrinsicAugmentationE() = DeclareResourceString("IntfImplInIntrinsicAugmentation", "")
let IntfImplInExtrinsicAugmentationE() = DeclareResourceString("IntfImplInExtrinsicAugmentation", "")
let UnresolvedReferenceNoRangeE() = DeclareResourceString("UnresolvedReferenceNoRange", "%s")
let UnresolvedPathReferenceNoRangeE() = DeclareResourceString("UnresolvedPathReferenceNoRange", "%s%s")
let HashIncludeNotAllowedInNonScriptE() = DeclareResourceString("HashIncludeNotAllowedInNonScript", "")
let HashReferenceNotAllowedInNonScriptE() = DeclareResourceString("HashReferenceNotAllowedInNonScript", "")
let HashDirectiveNotAllowedInNonScriptE() = DeclareResourceString("HashDirectiveNotAllowedInNonScript", "")
let FileNameNotResolvedE() = DeclareResourceString("FileNameNotResolved", "%s%s")
let AssemblyNotResolvedE() = DeclareResourceString("AssemblyNotResolved", "%s")
let HashLoadedSourceHasIssues1E() = DeclareResourceString("HashLoadedSourceHasIssues1", "")
let HashLoadedSourceHasIssues2E() = DeclareResourceString("HashLoadedSourceHasIssues2", "")
let HashLoadedScriptConsideredSourceE() = DeclareResourceString("HashLoadedScriptConsideredSource", "")  
let InvalidInternalsVisibleToAssemblyName1E() = DeclareResourceString("InvalidInternalsVisibleToAssemblyName1", "%s%s")
let InvalidInternalsVisibleToAssemblyName2E() = DeclareResourceString("InvalidInternalsVisibleToAssemblyName2", "%s")
let LoadedSourceNotFoundIgnoringE() = DeclareResourceString("LoadedSourceNotFoundIgnoring", "%s")
let MSBuildReferenceResolutionErrorE() = DeclareResourceString("MSBuildReferenceResolutionError", "%s%s")
let TargetInvocationExceptionWrapperE() = DeclareResourceString("TargetInvocationExceptionWrapper", "%s")

#if DEBUG
let mutable showParserStackOnParseError = false
#endif

let getErrorString key = SR.GetString key

let (|InvalidArgument|_|) (exn: exn) = match exn with :? ArgumentException as e -> Some e.Message | _ -> None

let OutputPhasedErrorR (os: StringBuilder) (err: PhasedDiagnostic) (canSuggestNames: bool) =

    let suggestNames suggestionsF idText =
        if canSuggestNames then
            let buffer = ErrorResolutionHints.SuggestionBuffer idText
            if not buffer.Disabled then
              suggestionsF buffer.Add
              if not buffer.IsEmpty then
                  os.Append " " |> ignore
                  os.Append(FSComp.SR.undefinedNameSuggestionsIntro()) |> ignore
                  for value in buffer do
                      os.AppendLine() |> ignore
                      os.Append "   " |> ignore
                      os.Append(DecompileOpName value) |> ignore

    let rec OutputExceptionR (os: StringBuilder) error = 

      match error with
      | ConstraintSolverTupleDiffLengths(_, tl1, tl2, m, m2) -> 
          os.Append(ConstraintSolverTupleDiffLengthsE().Format tl1.Length tl2.Length) |> ignore
          if m.StartLine <> m2.StartLine then 
             os.Append(SeeAlsoE().Format (stringOfRange m)) |> ignore

      | ConstraintSolverInfiniteTypes(denv, contextInfo, t1, t2, m, m2) ->
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
          os.Append(ConstraintSolverInfiniteTypesE().Format t1 t2) |> ignore

          match contextInfo with
          | ContextInfo.ReturnInComputationExpression ->
            os.Append(" " + FSComp.SR.returnUsedInsteadOfReturnBang()) |> ignore
          | ContextInfo.YieldInComputationExpression ->
            os.Append(" " + FSComp.SR.yieldUsedInsteadOfYieldBang()) |> ignore
          | _ -> ()

          if m.StartLine <> m2.StartLine then 
             os.Append(SeeAlsoE().Format (stringOfRange m)) |> ignore

      | ConstraintSolverMissingConstraint(denv, tpr, tpc, m, m2) -> 
          os.Append(ConstraintSolverMissingConstraintE().Format (NicePrint.stringOfTyparConstraint denv (tpr, tpc))) |> ignore
          if m.StartLine <> m2.StartLine then 
             os.Append(SeeAlsoE().Format (stringOfRange m)) |> ignore

      | ConstraintSolverTypesNotInEqualityRelation(denv, (TType_measure _ as t1), (TType_measure _ as t2), m, m2, _) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
          
          os.Append(ConstraintSolverTypesNotInEqualityRelation1E().Format t1 t2 ) |> ignore
          
          if m.StartLine <> m2.StartLine then
             os.Append(SeeAlsoE().Format (stringOfRange m)) |> ignore

      | ConstraintSolverTypesNotInEqualityRelation(denv, t1, t2, m, m2, contextInfo) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
          
          match contextInfo with
          | ContextInfo.IfExpression range when Range.equals range m -> os.Append(FSComp.SR.ifExpression(t1, t2)) |> ignore
          | ContextInfo.CollectionElement (isArray, range) when Range.equals range m -> 
            if isArray then
                os.Append(FSComp.SR.arrayElementHasWrongType(t1, t2)) |> ignore
            else
                os.Append(FSComp.SR.listElementHasWrongType(t1, t2)) |> ignore
          | ContextInfo.OmittedElseBranch range when Range.equals range m -> os.Append(FSComp.SR.missingElseBranch(t2)) |> ignore
          | ContextInfo.ElseBranchResult range when Range.equals range m -> os.Append(FSComp.SR.elseBranchHasWrongType(t1, t2)) |> ignore
          | ContextInfo.FollowingPatternMatchClause range when Range.equals range m -> os.Append(FSComp.SR.followingPatternMatchClauseHasWrongType(t1, t2)) |> ignore
          | ContextInfo.PatternMatchGuard range when Range.equals range m -> os.Append(FSComp.SR.patternMatchGuardIsNotBool(t2)) |> ignore
          | _ -> os.Append(ConstraintSolverTypesNotInEqualityRelation2E().Format t1 t2) |> ignore
          if m.StartLine <> m2.StartLine then 
             os.Append(SeeAlsoE().Format (stringOfRange m)) |> ignore

      | ConstraintSolverTypesNotInSubsumptionRelation(denv, t1, t2, m, m2) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, cxs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
          os.Append(ConstraintSolverTypesNotInSubsumptionRelationE().Format t2 t1 cxs) |> ignore
          if m.StartLine <> m2.StartLine then 
             os.Append(SeeAlsoE().Format (stringOfRange m2)) |> ignore

      | ConstraintSolverError(msg, m, m2) -> 
         os.Append msg |> ignore
         if m.StartLine <> m2.StartLine then 
            os.Append(SeeAlsoE().Format (stringOfRange m2)) |> ignore

      | ConstraintSolverRelatedInformation(fopt, _, e) -> 
          match e with 
          | ConstraintSolverError _ -> OutputExceptionR os e
          | _ -> ()
          fopt |> Option.iter (Printf.bprintf os " %s")

      | ErrorFromAddingTypeEquation(g, denv, t1, t2, ConstraintSolverTypesNotInEqualityRelation(_, t1', t2', m, _, contextInfo), _) 
         when typeEquiv g t1 t1'
              && typeEquiv g t2 t2' ->
          let t1, t2, tpcs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
          match contextInfo with
          | ContextInfo.IfExpression range when Range.equals range m -> os.Append(FSComp.SR.ifExpression(t1, t2)) |> ignore
          | ContextInfo.CollectionElement (isArray, range) when Range.equals range m -> 
            if isArray then
                os.Append(FSComp.SR.arrayElementHasWrongType(t1, t2)) |> ignore
            else
                os.Append(FSComp.SR.listElementHasWrongType(t1, t2)) |> ignore
          | ContextInfo.OmittedElseBranch range when Range.equals range m -> os.Append(FSComp.SR.missingElseBranch(t2)) |> ignore
          | ContextInfo.ElseBranchResult range when Range.equals range m -> os.Append(FSComp.SR.elseBranchHasWrongType(t1, t2)) |> ignore
          | ContextInfo.FollowingPatternMatchClause range when Range.equals range m -> os.Append(FSComp.SR.followingPatternMatchClauseHasWrongType(t1, t2)) |> ignore
          | ContextInfo.PatternMatchGuard range when Range.equals range m -> os.Append(FSComp.SR.patternMatchGuardIsNotBool(t2)) |> ignore
          | ContextInfo.TupleInRecordFields ->
                os.Append(ErrorFromAddingTypeEquation1E().Format t2 t1 tpcs) |> ignore
                os.Append(System.Environment.NewLine + FSComp.SR.commaInsteadOfSemicolonInRecord()) |> ignore
          | _ when t2 = "bool" && t1.EndsWithOrdinal(" ref") ->
                os.Append(ErrorFromAddingTypeEquation1E().Format t2 t1 tpcs) |> ignore
                os.Append(System.Environment.NewLine + FSComp.SR.derefInsteadOfNot()) |> ignore
          | _ -> os.Append(ErrorFromAddingTypeEquation1E().Format t2 t1 tpcs) |> ignore

      | ErrorFromAddingTypeEquation(_, _, _, _, ((ConstraintSolverTypesNotInEqualityRelation (_, _, _, _, _, contextInfo) ) as e), _)
              when (match contextInfo with ContextInfo.NoContext -> false | _ -> true) ->  
          OutputExceptionR os e

      | ErrorFromAddingTypeEquation(_, _, _, _, ((ConstraintSolverTypesNotInSubsumptionRelation _ | ConstraintSolverError _ ) as e), _) ->  
          OutputExceptionR os e

      | ErrorFromAddingTypeEquation(g, denv, t1, t2, e, _) ->
          if not (typeEquiv g t1 t2) then
              let t1, t2, tpcs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
              if t1<>t2 + tpcs then os.Append(ErrorFromAddingTypeEquation2E().Format t1 t2 tpcs) |> ignore

          OutputExceptionR os e

      | ErrorFromApplyingDefault(_, denv, _, defaultType, e, _) ->  
          let defaultType = NicePrint.minimalStringOfType denv defaultType
          os.Append(ErrorFromApplyingDefault1E().Format defaultType) |> ignore
          OutputExceptionR os e
          os.Append(ErrorFromApplyingDefault2E().Format) |> ignore

      | ErrorsFromAddingSubsumptionConstraint(g, denv, t1, t2, e, contextInfo, _) ->
          match contextInfo with
          | ContextInfo.DowncastUsedInsteadOfUpcast isOperator -> 
              let t1, t2, _ = NicePrint.minimalStringsOfTwoTypes denv t1 t2
              if isOperator then
                  os.Append(FSComp.SR.considerUpcastOperator(t1, t2) |> snd) |> ignore
              else
                  os.Append(FSComp.SR.considerUpcast(t1, t2) |> snd) |> ignore
          | _ ->
              if not (typeEquiv g t1 t2) then
                  let t1, t2, tpcs = NicePrint.minimalStringsOfTwoTypes denv t1 t2
                  if t1 <> (t2 + tpcs) then 
                      os.Append(ErrorsFromAddingSubsumptionConstraintE().Format t2 t1 tpcs) |> ignore
                  else
                      OutputExceptionR os e
              else
                  OutputExceptionR os e

      | UpperCaseIdentifierInPattern(_) -> 
          os.Append(UpperCaseIdentifierInPatternE().Format) |> ignore

      | NotUpperCaseConstructor(_) -> 
          os.Append(NotUpperCaseConstructorE().Format) |> ignore

      | ErrorFromAddingConstraint(_, e, _) ->  
          OutputExceptionR os e

#if !NO_EXTENSIONTYPING
      | ExtensionTyping.ProvidedTypeResolutionNoRange e

      | ExtensionTyping.ProvidedTypeResolution(_, e) -> 
          OutputExceptionR os e

      | :? TypeProviderError as e ->
          os.Append(e.ContextualErrorMessage) |> ignore
#endif

      | UnresolvedOverloading(denv, callerArgs, failure, m) ->
          
          // extract eventual information (return type and type parameters)
          // from ConstraintTraitInfo
          let knownReturnType, genericParameterTypes =
              match failure with
              | NoOverloadsFound (cx=Some cx)
              | PossibleCandidates (cx=Some cx) -> cx.ReturnType, cx.ArgumentTypes
              | _ -> None, []
         
          // prepare message parts (known arguments, known return type, known generic parameters)
          let argsMessage, returnType, genericParametersMessage =
              
              let retTy =
                  knownReturnType
                  |> Option.defaultValue (TType.TType_var (Typar.NewUnlinked()))
              
              let argRepr = 
                  callerArgs.ArgumentNamesAndTypes
                  |> List.map (fun (name,tTy) -> tTy, {ArgReprInfo.Name = name |> Option.map (fun name -> Ident(name, range.Zero)); ArgReprInfo.Attribs = []})
                  
              let argsL,retTyL,genParamTysL = NicePrint.prettyLayoutsOfUnresolvedOverloading denv argRepr retTy genericParameterTypes
              
              match callerArgs.ArgumentNamesAndTypes with
              | [] -> None, LayoutRender.showL retTyL, LayoutRender.showL genParamTysL
              | items ->
                  let args = LayoutRender.showL argsL  
                  let prefixMessage =
                      match items with
                      | [_] -> FSComp.SR.csNoOverloadsFoundArgumentsPrefixSingular
                      | _ -> FSComp.SR.csNoOverloadsFoundArgumentsPrefixPlural
                  Some (prefixMessage args)
                  , LayoutRender.showL retTyL
                  , LayoutRender.showL genParamTysL

          let knownReturnType =
              match knownReturnType with
              | None -> None
              | Some _ -> Some (FSComp.SR.csNoOverloadsFoundReturnType returnType)

          let genericParametersMessage =
              match genericParameterTypes with
              | [] -> None
              | [_] -> Some (FSComp.SR.csNoOverloadsFoundTypeParametersPrefixSingular genericParametersMessage)
              | _ -> Some (FSComp.SR.csNoOverloadsFoundTypeParametersPrefixPlural genericParametersMessage)

          let overloadMethodInfo displayEnv m (x: OverloadInformation) =
              let paramInfo =
                  match x.error with
                  | :? ArgDoesNotMatchError as x ->
                      let nameOrOneBasedIndexMessage =
                          x.calledArg.NameOpt
                          |> Option.map (fun n -> FSComp.SR.csOverloadCandidateNamedArgumentTypeMismatch n.idText)
                          |> Option.defaultValue (FSComp.SR.csOverloadCandidateIndexedArgumentTypeMismatch ((Lib.vsnd x.calledArg.Position) + 1)) //snd
                      sprintf " // %s" nameOrOneBasedIndexMessage
                  | _ -> ""
                  
              (NicePrint.stringOfMethInfo x.amap m displayEnv x.methodSlot.Method) + paramInfo
              
          let nl = System.Environment.NewLine
          let formatOverloads (overloads: OverloadInformation list) =
              overloads
              |> List.map (overloadMethodInfo denv m)
              |> List.sort
              |> List.map FSComp.SR.formatDashItem
              |> String.concat nl
         
          // assemble final message composing the parts
          let msg =
              let optionalParts =
                [knownReturnType; genericParametersMessage; argsMessage]
                |> List.choose id
                |> String.concat (nl + nl)
                |> function | "" -> nl
                            | result -> nl + nl + result + nl + nl
              
              match failure with
              | NoOverloadsFound (methodName, overloads, _) ->
                  FSComp.SR.csNoOverloadsFound methodName
                      + optionalParts                      
                      + (FSComp.SR.csAvailableOverloads (formatOverloads overloads))
              | PossibleCandidates (methodName, [], _) ->
                  FSComp.SR.csMethodIsOverloaded methodName
              | PossibleCandidates (methodName, overloads, _) ->
                  FSComp.SR.csMethodIsOverloaded methodName
                      + optionalParts
                      + FSComp.SR.csCandidates (formatOverloads overloads)
          
          os.Append msg |> ignore

      | UnresolvedConversionOperator(denv, fromTy, toTy, _) -> 
          let t1, t2, _tpcs = NicePrint.minimalStringsOfTwoTypes denv fromTy toTy
          os.Append(FSComp.SR.csTypeDoesNotSupportConversion(t1, t2)) |> ignore

      | FunctionExpected _ ->
          os.Append(FunctionExpectedE().Format) |> ignore

      | BakedInMemberConstraintName(nm, _) ->
          os.Append(BakedInMemberConstraintNameE().Format nm) |> ignore

      | StandardOperatorRedefinitionWarning(msg, _) -> 
          os.Append msg |> ignore

      | BadEventTransformation(_) ->
         os.Append(BadEventTransformationE().Format) |> ignore

      | ParameterlessStructCtor(_) ->
         os.Append(ParameterlessStructCtorE().Format) |> ignore

      | InterfaceNotRevealed(denv, ity, _) ->
          os.Append(InterfaceNotRevealedE().Format (NicePrint.minimalStringOfType denv ity)) |> ignore

      | NotAFunctionButIndexer(_, _, name, _, _) ->
          match name with
          | Some name -> os.Append(FSComp.SR.notAFunctionButMaybeIndexerWithName name) |> ignore
          | _ -> os.Append(FSComp.SR.notAFunctionButMaybeIndexer()) |> ignore

      | NotAFunction(_, _, _, marg) ->
          if marg.StartColumn = 0 then
              os.Append(FSComp.SR.notAFunctionButMaybeDeclaration()) |> ignore
          else
              os.Append(FSComp.SR.notAFunction()) |> ignore

      | TyconBadArgs(_, tcref, d, _) -> 
          let exp = tcref.TyparsNoRange.Length
          if exp = 0 then
              os.Append(FSComp.SR.buildUnexpectedTypeArgs(fullDisplayTextOfTyconRef tcref, d)) |> ignore
          else
              os.Append(TyconBadArgsE().Format (fullDisplayTextOfTyconRef tcref) exp d) |> ignore

      | IndeterminateType(_) -> 
          os.Append(IndeterminateTypeE().Format) |> ignore

      | NameClash(nm, k1, nm1, _, k2, nm2, _) -> 
          if nm = nm1 && nm1 = nm2 && k1 = k2 then 
              os.Append(NameClash1E().Format k1 nm1) |> ignore
          else
              os.Append(NameClash2E().Format k1 nm1 nm k2 nm2) |> ignore

      | Duplicate(k, s, _) -> 
          if k = "member" then 
              os.Append(Duplicate1E().Format (DecompileOpName s)) |> ignore
          else 
              os.Append(Duplicate2E().Format k (DecompileOpName s)) |> ignore

      | UndefinedName(_, k, id, suggestionsF) ->
          os.Append(k (DecompileOpName id.idText)) |> ignore
          suggestNames suggestionsF id.idText

      | InternalUndefinedItemRef(f, smr, ccuName, s) ->  
          let _, errs = f(smr, ccuName, s)
          os.Append errs |> ignore  

      | FieldNotMutable _ -> 
          os.Append(FieldNotMutableE().Format) |> ignore

      | FieldsFromDifferentTypes (_, fref1, fref2, _) -> 
          os.Append(FieldsFromDifferentTypesE().Format fref1.FieldName fref2.FieldName) |> ignore

      | VarBoundTwice id ->  
          os.Append(VarBoundTwiceE().Format (DecompileOpName id.idText)) |> ignore

      | Recursion (denv, id, ty1, ty2, _) -> 
          let t1, t2, tpcs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(RecursionE().Format (DecompileOpName id.idText) t1 t2 tpcs) |> ignore

      | InvalidRuntimeCoercion(denv, ty1, ty2, _) -> 
          let t1, t2, tpcs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(InvalidRuntimeCoercionE().Format t1 t2 tpcs) |> ignore

      | IndeterminateRuntimeCoercion(denv, ty1, ty2, _) -> 
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(IndeterminateRuntimeCoercionE().Format t1 t2) |> ignore

      | IndeterminateStaticCoercion(denv, ty1, ty2, _) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(IndeterminateStaticCoercionE().Format t1 t2) |> ignore

      | StaticCoercionShouldUseBox(denv, ty1, ty2, _) ->
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(StaticCoercionShouldUseBoxE().Format t1 t2) |> ignore

      | TypeIsImplicitlyAbstract(_) -> 
          os.Append(TypeIsImplicitlyAbstractE().Format) |> ignore

      | NonRigidTypar(denv, tpnmOpt, typarRange, ty1, ty, _) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let (ty1, ty), _cxs = PrettyTypes.PrettifyTypePair denv.g (ty1, ty)
          match tpnmOpt with 
          | None -> 
              os.Append(NonRigidTypar1E().Format (stringOfRange typarRange) (NicePrint.stringOfTy denv ty)) |> ignore
          | Some tpnm -> 
              match ty1 with 
              | TType_measure _ -> 
                os.Append(NonRigidTypar2E().Format tpnm (NicePrint.stringOfTy denv ty)) |> ignore
              | _ -> 
                os.Append(NonRigidTypar3E().Format tpnm (NicePrint.stringOfTy denv ty)) |> ignore

      | SyntaxError (ctxt, _) -> 
          let ctxt = unbox<Parsing.ParseErrorContext<Parser.token>>(ctxt)
          
          let (|EndOfStructuredConstructToken|_|) token = 
              match token with
              | Parser.TOKEN_ODECLEND 
              | Parser.TOKEN_OBLOCKSEP 
              | Parser.TOKEN_OEND 
              | Parser.TOKEN_ORIGHT_BLOCK_END 
              | Parser.TOKEN_OBLOCKEND | Parser.TOKEN_OBLOCKEND_COMING_SOON | Parser.TOKEN_OBLOCKEND_IS_HERE -> Some()
              | _ -> None

          let tokenIdToText tid = 
              match tid with 
              | Parser.TOKEN_IDENT -> getErrorString("Parser.TOKEN.IDENT")
              | Parser.TOKEN_BIGNUM 
              | Parser.TOKEN_INT8  
              | Parser.TOKEN_UINT8 
              | Parser.TOKEN_INT16  
              | Parser.TOKEN_UINT16 
              | Parser.TOKEN_INT32 
              | Parser.TOKEN_UINT32 
              | Parser.TOKEN_INT64 
              | Parser.TOKEN_UINT64 
              | Parser.TOKEN_UNATIVEINT 
              | Parser.TOKEN_NATIVEINT -> getErrorString("Parser.TOKEN.INT")
              | Parser.TOKEN_IEEE32 
              | Parser.TOKEN_IEEE64 -> getErrorString("Parser.TOKEN.FLOAT")
              | Parser.TOKEN_DECIMAL -> getErrorString("Parser.TOKEN.DECIMAL")
              | Parser.TOKEN_CHAR -> getErrorString("Parser.TOKEN.CHAR")
                
              | Parser.TOKEN_BASE -> getErrorString("Parser.TOKEN.BASE")
              | Parser.TOKEN_LPAREN_STAR_RPAREN -> getErrorString("Parser.TOKEN.LPAREN.STAR.RPAREN")
              | Parser.TOKEN_DOLLAR -> getErrorString("Parser.TOKEN.DOLLAR")
              | Parser.TOKEN_INFIX_STAR_STAR_OP -> getErrorString("Parser.TOKEN.INFIX.STAR.STAR.OP")
              | Parser.TOKEN_INFIX_COMPARE_OP -> getErrorString("Parser.TOKEN.INFIX.COMPARE.OP")
              | Parser.TOKEN_COLON_GREATER -> getErrorString("Parser.TOKEN.COLON.GREATER")  
              | Parser.TOKEN_COLON_COLON ->getErrorString("Parser.TOKEN.COLON.COLON")
              | Parser.TOKEN_PERCENT_OP -> getErrorString("Parser.TOKEN.PERCENT.OP")
              | Parser.TOKEN_INFIX_AT_HAT_OP -> getErrorString("Parser.TOKEN.INFIX.AT.HAT.OP")
              | Parser.TOKEN_INFIX_BAR_OP -> getErrorString("Parser.TOKEN.INFIX.BAR.OP")
              | Parser.TOKEN_PLUS_MINUS_OP -> getErrorString("Parser.TOKEN.PLUS.MINUS.OP")
              | Parser.TOKEN_PREFIX_OP -> getErrorString("Parser.TOKEN.PREFIX.OP")
              | Parser.TOKEN_COLON_QMARK_GREATER -> getErrorString("Parser.TOKEN.COLON.QMARK.GREATER")
              | Parser.TOKEN_INFIX_STAR_DIV_MOD_OP -> getErrorString("Parser.TOKEN.INFIX.STAR.DIV.MOD.OP")
              | Parser.TOKEN_INFIX_AMP_OP -> getErrorString("Parser.TOKEN.INFIX.AMP.OP")
              | Parser.TOKEN_AMP -> getErrorString("Parser.TOKEN.AMP")
              | Parser.TOKEN_AMP_AMP -> getErrorString("Parser.TOKEN.AMP.AMP")
              | Parser.TOKEN_BAR_BAR -> getErrorString("Parser.TOKEN.BAR.BAR")
              | Parser.TOKEN_LESS -> getErrorString("Parser.TOKEN.LESS")
              | Parser.TOKEN_GREATER -> getErrorString("Parser.TOKEN.GREATER")
              | Parser.TOKEN_QMARK -> getErrorString("Parser.TOKEN.QMARK")
              | Parser.TOKEN_QMARK_QMARK -> getErrorString("Parser.TOKEN.QMARK.QMARK")
              | Parser.TOKEN_COLON_QMARK-> getErrorString("Parser.TOKEN.COLON.QMARK")
              | Parser.TOKEN_INT32_DOT_DOT -> getErrorString("Parser.TOKEN.INT32.DOT.DOT")
              | Parser.TOKEN_DOT_DOT -> getErrorString("Parser.TOKEN.DOT.DOT")
              | Parser.TOKEN_DOT_DOT_HAT -> getErrorString("Parser.TOKEN.DOT.DOT")
              | Parser.TOKEN_QUOTE -> getErrorString("Parser.TOKEN.QUOTE")
              | Parser.TOKEN_STAR -> getErrorString("Parser.TOKEN.STAR")
              | Parser.TOKEN_HIGH_PRECEDENCE_TYAPP -> getErrorString("Parser.TOKEN.HIGH.PRECEDENCE.TYAPP")
              | Parser.TOKEN_COLON -> getErrorString("Parser.TOKEN.COLON")
              | Parser.TOKEN_COLON_EQUALS -> getErrorString("Parser.TOKEN.COLON.EQUALS")
              | Parser.TOKEN_LARROW -> getErrorString("Parser.TOKEN.LARROW")
              | Parser.TOKEN_EQUALS -> getErrorString("Parser.TOKEN.EQUALS")
              | Parser.TOKEN_GREATER_BAR_RBRACK -> getErrorString("Parser.TOKEN.GREATER.BAR.RBRACK")
              | Parser.TOKEN_MINUS -> getErrorString("Parser.TOKEN.MINUS")
              | Parser.TOKEN_ADJACENT_PREFIX_OP -> getErrorString("Parser.TOKEN.ADJACENT.PREFIX.OP")
              | Parser.TOKEN_FUNKY_OPERATOR_NAME -> getErrorString("Parser.TOKEN.FUNKY.OPERATOR.NAME") 
              | Parser.TOKEN_COMMA-> getErrorString("Parser.TOKEN.COMMA")
              | Parser.TOKEN_DOT -> getErrorString("Parser.TOKEN.DOT")
              | Parser.TOKEN_BAR-> getErrorString("Parser.TOKEN.BAR")
              | Parser.TOKEN_HASH -> getErrorString("Parser.TOKEN.HASH")
              | Parser.TOKEN_UNDERSCORE -> getErrorString("Parser.TOKEN.UNDERSCORE")
              | Parser.TOKEN_SEMICOLON -> getErrorString("Parser.TOKEN.SEMICOLON")
              | Parser.TOKEN_SEMICOLON_SEMICOLON-> getErrorString("Parser.TOKEN.SEMICOLON.SEMICOLON")
              | Parser.TOKEN_LPAREN-> getErrorString("Parser.TOKEN.LPAREN")
              | Parser.TOKEN_RPAREN | Parser.TOKEN_RPAREN_COMING_SOON | Parser.TOKEN_RPAREN_IS_HERE -> getErrorString("Parser.TOKEN.RPAREN")
              | Parser.TOKEN_LQUOTE -> getErrorString("Parser.TOKEN.LQUOTE")
              | Parser.TOKEN_LBRACK -> getErrorString("Parser.TOKEN.LBRACK")
              | Parser.TOKEN_LBRACE_BAR -> getErrorString("Parser.TOKEN.LBRACE.BAR")
              | Parser.TOKEN_LBRACK_BAR -> getErrorString("Parser.TOKEN.LBRACK.BAR")
              | Parser.TOKEN_LBRACK_LESS -> getErrorString("Parser.TOKEN.LBRACK.LESS")
              | Parser.TOKEN_LBRACE -> getErrorString("Parser.TOKEN.LBRACE")
              | Parser.TOKEN_BAR_RBRACK -> getErrorString("Parser.TOKEN.BAR.RBRACK")
              | Parser.TOKEN_BAR_RBRACE -> getErrorString("Parser.TOKEN.BAR.RBRACE")
              | Parser.TOKEN_GREATER_RBRACK -> getErrorString("Parser.TOKEN.GREATER.RBRACK")
              | Parser.TOKEN_RQUOTE_DOT _ 
              | Parser.TOKEN_RQUOTE -> getErrorString("Parser.TOKEN.RQUOTE")
              | Parser.TOKEN_RBRACK -> getErrorString("Parser.TOKEN.RBRACK")
              | Parser.TOKEN_RBRACE | Parser.TOKEN_RBRACE_COMING_SOON | Parser.TOKEN_RBRACE_IS_HERE -> getErrorString("Parser.TOKEN.RBRACE")
              | Parser.TOKEN_PUBLIC -> getErrorString("Parser.TOKEN.PUBLIC")
              | Parser.TOKEN_PRIVATE -> getErrorString("Parser.TOKEN.PRIVATE")
              | Parser.TOKEN_INTERNAL -> getErrorString("Parser.TOKEN.INTERNAL")
              | Parser.TOKEN_CONSTRAINT -> getErrorString("Parser.TOKEN.CONSTRAINT")
              | Parser.TOKEN_INSTANCE -> getErrorString("Parser.TOKEN.INSTANCE")
              | Parser.TOKEN_DELEGATE -> getErrorString("Parser.TOKEN.DELEGATE")
              | Parser.TOKEN_INHERIT -> getErrorString("Parser.TOKEN.INHERIT")
              | Parser.TOKEN_CONSTRUCTOR-> getErrorString("Parser.TOKEN.CONSTRUCTOR")
              | Parser.TOKEN_DEFAULT -> getErrorString("Parser.TOKEN.DEFAULT")
              | Parser.TOKEN_OVERRIDE-> getErrorString("Parser.TOKEN.OVERRIDE")
              | Parser.TOKEN_ABSTRACT-> getErrorString("Parser.TOKEN.ABSTRACT")
              | Parser.TOKEN_CLASS-> getErrorString("Parser.TOKEN.CLASS")
              | Parser.TOKEN_MEMBER -> getErrorString("Parser.TOKEN.MEMBER")
              | Parser.TOKEN_STATIC -> getErrorString("Parser.TOKEN.STATIC")
              | Parser.TOKEN_NAMESPACE-> getErrorString("Parser.TOKEN.NAMESPACE")
              | Parser.TOKEN_OBLOCKBEGIN -> getErrorString("Parser.TOKEN.OBLOCKBEGIN") 
              | EndOfStructuredConstructToken -> getErrorString("Parser.TOKEN.OBLOCKEND") 
              | Parser.TOKEN_THEN  
              | Parser.TOKEN_OTHEN -> getErrorString("Parser.TOKEN.OTHEN")
              | Parser.TOKEN_ELSE
              | Parser.TOKEN_OELSE -> getErrorString("Parser.TOKEN.OELSE")
              | Parser.TOKEN_LET(_) 
              | Parser.TOKEN_OLET(_) -> getErrorString("Parser.TOKEN.OLET")
              | Parser.TOKEN_OBINDER 
              | Parser.TOKEN_BINDER -> getErrorString("Parser.TOKEN.BINDER")
              | Parser.TOKEN_OAND_BANG 
              | Parser.TOKEN_AND_BANG -> getErrorString("Parser.TOKEN.AND.BANG")
              | Parser.TOKEN_ODO -> getErrorString("Parser.TOKEN.ODO")
              | Parser.TOKEN_OWITH -> getErrorString("Parser.TOKEN.OWITH")
              | Parser.TOKEN_OFUNCTION -> getErrorString("Parser.TOKEN.OFUNCTION")
              | Parser.TOKEN_OFUN -> getErrorString("Parser.TOKEN.OFUN")
              | Parser.TOKEN_ORESET -> getErrorString("Parser.TOKEN.ORESET")
              | Parser.TOKEN_ODUMMY -> getErrorString("Parser.TOKEN.ODUMMY")
              | Parser.TOKEN_DO_BANG 
              | Parser.TOKEN_ODO_BANG -> getErrorString("Parser.TOKEN.ODO.BANG")
              | Parser.TOKEN_YIELD -> getErrorString("Parser.TOKEN.YIELD")
              | Parser.TOKEN_YIELD_BANG -> getErrorString("Parser.TOKEN.YIELD.BANG")
              | Parser.TOKEN_OINTERFACE_MEMBER-> getErrorString("Parser.TOKEN.OINTERFACE.MEMBER")
              | Parser.TOKEN_ELIF -> getErrorString("Parser.TOKEN.ELIF")
              | Parser.TOKEN_RARROW -> getErrorString("Parser.TOKEN.RARROW")
              | Parser.TOKEN_SIG -> getErrorString("Parser.TOKEN.SIG")
              | Parser.TOKEN_STRUCT -> getErrorString("Parser.TOKEN.STRUCT")
              | Parser.TOKEN_UPCAST -> getErrorString("Parser.TOKEN.UPCAST")
              | Parser.TOKEN_DOWNCAST -> getErrorString("Parser.TOKEN.DOWNCAST")
              | Parser.TOKEN_NULL -> getErrorString("Parser.TOKEN.NULL")
              | Parser.TOKEN_RESERVED -> getErrorString("Parser.TOKEN.RESERVED")
              | Parser.TOKEN_MODULE | Parser.TOKEN_MODULE_COMING_SOON | Parser.TOKEN_MODULE_IS_HERE -> getErrorString("Parser.TOKEN.MODULE")
              | Parser.TOKEN_AND -> getErrorString("Parser.TOKEN.AND")
              | Parser.TOKEN_AS -> getErrorString("Parser.TOKEN.AS")
              | Parser.TOKEN_ASSERT -> getErrorString("Parser.TOKEN.ASSERT")
              | Parser.TOKEN_OASSERT -> getErrorString("Parser.TOKEN.ASSERT")
              | Parser.TOKEN_ASR-> getErrorString("Parser.TOKEN.ASR")
              | Parser.TOKEN_DOWNTO -> getErrorString("Parser.TOKEN.DOWNTO")
              | Parser.TOKEN_EXCEPTION -> getErrorString("Parser.TOKEN.EXCEPTION")
              | Parser.TOKEN_FALSE -> getErrorString("Parser.TOKEN.FALSE")
              | Parser.TOKEN_FOR -> getErrorString("Parser.TOKEN.FOR")
              | Parser.TOKEN_FUN -> getErrorString("Parser.TOKEN.FUN")
              | Parser.TOKEN_FUNCTION-> getErrorString("Parser.TOKEN.FUNCTION")
              | Parser.TOKEN_FINALLY -> getErrorString("Parser.TOKEN.FINALLY")
              | Parser.TOKEN_LAZY -> getErrorString("Parser.TOKEN.LAZY")
              | Parser.TOKEN_OLAZY -> getErrorString("Parser.TOKEN.LAZY")
              | Parser.TOKEN_MATCH -> getErrorString("Parser.TOKEN.MATCH")
              | Parser.TOKEN_MATCH_BANG -> getErrorString("Parser.TOKEN.MATCH.BANG")
              | Parser.TOKEN_MUTABLE -> getErrorString("Parser.TOKEN.MUTABLE")
              | Parser.TOKEN_NEW -> getErrorString("Parser.TOKEN.NEW")
              | Parser.TOKEN_OF -> getErrorString("Parser.TOKEN.OF")
              | Parser.TOKEN_OPEN -> getErrorString("Parser.TOKEN.OPEN")
              | Parser.TOKEN_OR -> getErrorString("Parser.TOKEN.OR")
              | Parser.TOKEN_VOID -> getErrorString("Parser.TOKEN.VOID")
              | Parser.TOKEN_EXTERN-> getErrorString("Parser.TOKEN.EXTERN")
              | Parser.TOKEN_INTERFACE -> getErrorString("Parser.TOKEN.INTERFACE")
              | Parser.TOKEN_REC -> getErrorString("Parser.TOKEN.REC")
              | Parser.TOKEN_TO -> getErrorString("Parser.TOKEN.TO")
              | Parser.TOKEN_TRUE -> getErrorString("Parser.TOKEN.TRUE")
              | Parser.TOKEN_TRY -> getErrorString("Parser.TOKEN.TRY")
              | Parser.TOKEN_TYPE | Parser.TOKEN_TYPE_COMING_SOON | Parser.TOKEN_TYPE_IS_HERE -> getErrorString("Parser.TOKEN.TYPE")
              | Parser.TOKEN_VAL -> getErrorString("Parser.TOKEN.VAL")
              | Parser.TOKEN_INLINE -> getErrorString("Parser.TOKEN.INLINE")
              | Parser.TOKEN_WHEN -> getErrorString("Parser.TOKEN.WHEN")
              | Parser.TOKEN_WHILE -> getErrorString("Parser.TOKEN.WHILE")
              | Parser.TOKEN_WITH-> getErrorString("Parser.TOKEN.WITH")
              | Parser.TOKEN_IF -> getErrorString("Parser.TOKEN.IF")
              | Parser.TOKEN_DO -> getErrorString("Parser.TOKEN.DO")
              | Parser.TOKEN_GLOBAL -> getErrorString("Parser.TOKEN.GLOBAL")
              | Parser.TOKEN_DONE -> getErrorString("Parser.TOKEN.DONE")
              | Parser.TOKEN_IN | Parser.TOKEN_JOIN_IN -> getErrorString("Parser.TOKEN.IN")
              | Parser.TOKEN_HIGH_PRECEDENCE_PAREN_APP-> getErrorString("Parser.TOKEN.HIGH.PRECEDENCE.PAREN.APP")
              | Parser.TOKEN_HIGH_PRECEDENCE_BRACK_APP-> getErrorString("Parser.TOKEN.HIGH.PRECEDENCE.BRACK.APP")
              | Parser.TOKEN_BEGIN -> getErrorString("Parser.TOKEN.BEGIN")
              | Parser.TOKEN_END -> getErrorString("Parser.TOKEN.END")
              | Parser.TOKEN_HASH_LIGHT
              | Parser.TOKEN_HASH_LINE 
              | Parser.TOKEN_HASH_IF 
              | Parser.TOKEN_HASH_ELSE 
              | Parser.TOKEN_HASH_ENDIF -> getErrorString("Parser.TOKEN.HASH.ENDIF")
              | Parser.TOKEN_INACTIVECODE -> getErrorString("Parser.TOKEN.INACTIVECODE")
              | Parser.TOKEN_LEX_FAILURE-> getErrorString("Parser.TOKEN.LEX.FAILURE")
              | Parser.TOKEN_WHITESPACE -> getErrorString("Parser.TOKEN.WHITESPACE")
              | Parser.TOKEN_COMMENT -> getErrorString("Parser.TOKEN.COMMENT")
              | Parser.TOKEN_LINE_COMMENT -> getErrorString("Parser.TOKEN.LINE.COMMENT")
              | Parser.TOKEN_STRING_TEXT -> getErrorString("Parser.TOKEN.STRING.TEXT")
              | Parser.TOKEN_BYTEARRAY -> getErrorString("Parser.TOKEN.BYTEARRAY")
              | Parser.TOKEN_STRING -> getErrorString("Parser.TOKEN.STRING")
              | Parser.TOKEN_KEYWORD_STRING -> getErrorString("Parser.TOKEN.KEYWORD_STRING")
              | Parser.TOKEN_EOF -> getErrorString("Parser.TOKEN.EOF")
              | Parser.TOKEN_CONST -> getErrorString("Parser.TOKEN.CONST")
              | Parser.TOKEN_FIXED -> getErrorString("Parser.TOKEN.FIXED")
              | Parser.TOKEN_INTERP_STRING_BEGIN_END -> getErrorString("Parser.TOKEN.INTERP.STRING.BEGIN.END")
              | Parser.TOKEN_INTERP_STRING_BEGIN_PART -> getErrorString("Parser.TOKEN.INTERP.STRING.BEGIN.PART")
              | Parser.TOKEN_INTERP_STRING_PART -> getErrorString("Parser.TOKEN.INTERP.STRING.PART")
              | Parser.TOKEN_INTERP_STRING_END -> getErrorString("Parser.TOKEN.INTERP.STRING.END")
              | unknown ->           
                  Debug.Assert(false, "unknown token tag")
                  let result = sprintf "%+A" unknown
                  Debug.Assert(false, result)
                  result

#if DEBUG
          if showParserStackOnParseError then
              printfn "parser stack:" 
              for rps in ctxt.ReducibleProductions do 
                  printfn "   ----" 
                  //printfn "   state %d" state
                  for rp in rps do
                      printfn "       non-terminal %+A (idx %d): ... " (Parser.prodIdxToNonTerminal rp) rp
#endif

          match ctxt.CurrentToken with 
          | None -> os.Append(UnexpectedEndOfInputE().Format) |> ignore
          | Some token -> 
              match (token |> Parser.tagOfToken |> Parser.tokenTagToTokenId), token with 
              | EndOfStructuredConstructToken, _ -> os.Append(OBlockEndSentenceE().Format) |> ignore
              | Parser.TOKEN_LEX_FAILURE, Parser.LEX_FAILURE str -> Printf.bprintf os "%s" str (* Fix bug://2431 *)
              | token, _ -> os.Append(UnexpectedE().Format (token |> tokenIdToText)) |> ignore

              (* Search for a state producing a single recognized non-terminal in the states on the stack *)
              let foundInContext =
              
                  (* Merge a bunch of expression non terminals *)
                  let (|NONTERM_Category_Expr|_|) = function
                        | Parser.NONTERM_argExpr|Parser.NONTERM_minusExpr|Parser.NONTERM_parenExpr|Parser.NONTERM_atomicExpr
                        | Parser.NONTERM_appExpr|Parser.NONTERM_tupleExpr|Parser.NONTERM_declExpr|Parser.NONTERM_braceExpr|Parser.NONTERM_braceBarExpr
                        | Parser.NONTERM_typedSeqExprBlock
                        | Parser.NONTERM_interactiveExpr -> Some()
                        | _ -> None
                        
                  (* Merge a bunch of pattern non terminals *)
                  let (|NONTERM_Category_Pattern|_|) = function 
                        | Parser.NONTERM_constrPattern|Parser.NONTERM_parenPattern|Parser.NONTERM_atomicPattern -> Some() 
                        | _ -> None
                  
                  (* Merge a bunch of if/then/else non terminals *)
                  let (|NONTERM_Category_IfThenElse|_|) = function
                        | Parser.NONTERM_ifExprThen|Parser.NONTERM_ifExprElifs|Parser.NONTERM_ifExprCases -> Some()
                        | _ -> None
                        
                  (* Merge a bunch of non terminals *)
                  let (|NONTERM_Category_SignatureFile|_|) = function
                        | Parser.NONTERM_signatureFile|Parser.NONTERM_moduleSpfn|Parser.NONTERM_moduleSpfns -> Some()
                        | _ -> None
                  let (|NONTERM_Category_ImplementationFile|_|) = function
                        | Parser.NONTERM_implementationFile|Parser.NONTERM_fileNamespaceImpl|Parser.NONTERM_fileNamespaceImpls -> Some()
                        | _ -> None
                  let (|NONTERM_Category_Definition|_|) = function
                        | Parser.NONTERM_fileModuleImpl|Parser.NONTERM_moduleDefn|Parser.NONTERM_interactiveDefns
                        |Parser.NONTERM_moduleDefns|Parser.NONTERM_moduleDefnsOrExpr -> Some()
                        | _ -> None
                  
                  let (|NONTERM_Category_Type|_|) = function
                        | Parser.NONTERM_typ|Parser.NONTERM_tupleType -> Some()
                        | _ -> None

                  let (|NONTERM_Category_Interaction|_|) = function
                        | Parser.NONTERM_interactiveItemsTerminator|Parser.NONTERM_interaction|Parser.NONTERM__startinteraction -> Some()
                        | _ -> None
         
                  
                  // Canonicalize the categories and check for a unique category
                  ctxt.ReducibleProductions |> List.exists (fun prods -> 
                      match prods 
                            |> List.map Parser.prodIdxToNonTerminal 
                            |> List.map (function 
                                         | NONTERM_Category_Type -> Parser.NONTERM_typ
                                         | NONTERM_Category_Expr -> Parser.NONTERM_declExpr 
                                         | NONTERM_Category_Pattern -> Parser.NONTERM_atomicPattern 
                                         | NONTERM_Category_IfThenElse -> Parser.NONTERM_ifExprThen
                                         | NONTERM_Category_SignatureFile -> Parser.NONTERM_signatureFile
                                         | NONTERM_Category_ImplementationFile -> Parser.NONTERM_implementationFile
                                         | NONTERM_Category_Definition -> Parser.NONTERM_moduleDefn
                                         | NONTERM_Category_Interaction -> Parser.NONTERM_interaction
                                         | nt -> nt)
                            |> Set.ofList 
                            |> Set.toList with 
                      | [Parser.NONTERM_interaction] -> os.Append(NONTERM_interactionE().Format) |> ignore; true
                      | [Parser.NONTERM_hashDirective] -> os.Append(NONTERM_hashDirectiveE().Format) |> ignore; true
                      | [Parser.NONTERM_fieldDecl] -> os.Append(NONTERM_fieldDeclE().Format) |> ignore; true
                      | [Parser.NONTERM_unionCaseRepr] -> os.Append(NONTERM_unionCaseReprE().Format) |> ignore; true
                      | [Parser.NONTERM_localBinding] -> os.Append(NONTERM_localBindingE().Format) |> ignore; true
                      | [Parser.NONTERM_hardwhiteLetBindings] -> os.Append(NONTERM_hardwhiteLetBindingsE().Format) |> ignore; true
                      | [Parser.NONTERM_classDefnMember] -> os.Append(NONTERM_classDefnMemberE().Format) |> ignore; true
                      | [Parser.NONTERM_defnBindings] -> os.Append(NONTERM_defnBindingsE().Format) |> ignore; true
                      | [Parser.NONTERM_classMemberSpfn] -> os.Append(NONTERM_classMemberSpfnE().Format) |> ignore; true
                      | [Parser.NONTERM_valSpfn] -> os.Append(NONTERM_valSpfnE().Format) |> ignore; true
                      | [Parser.NONTERM_tyconSpfn] -> os.Append(NONTERM_tyconSpfnE().Format) |> ignore; true
                      | [Parser.NONTERM_anonLambdaExpr] -> os.Append(NONTERM_anonLambdaExprE().Format) |> ignore; true
                      | [Parser.NONTERM_attrUnionCaseDecl] -> os.Append(NONTERM_attrUnionCaseDeclE().Format) |> ignore; true
                      | [Parser.NONTERM_cPrototype] -> os.Append(NONTERM_cPrototypeE().Format) |> ignore; true
                      | [Parser.NONTERM_objExpr|Parser.NONTERM_objectImplementationMembers] -> os.Append(NONTERM_objectImplementationMembersE().Format) |> ignore; true
                      | [Parser.NONTERM_ifExprThen|Parser.NONTERM_ifExprElifs|Parser.NONTERM_ifExprCases] -> os.Append(NONTERM_ifExprCasesE().Format) |> ignore; true
                      | [Parser.NONTERM_openDecl] -> os.Append(NONTERM_openDeclE().Format) |> ignore; true
                      | [Parser.NONTERM_fileModuleSpec] -> os.Append(NONTERM_fileModuleSpecE().Format) |> ignore; true
                      | [Parser.NONTERM_patternClauses] -> os.Append(NONTERM_patternClausesE().Format) |> ignore; true
                      | [Parser.NONTERM_beginEndExpr] -> os.Append(NONTERM_beginEndExprE().Format) |> ignore; true
                      | [Parser.NONTERM_recdExpr] -> os.Append(NONTERM_recdExprE().Format) |> ignore; true
                      | [Parser.NONTERM_tyconDefn] -> os.Append(NONTERM_tyconDefnE().Format) |> ignore; true
                      | [Parser.NONTERM_exconCore] -> os.Append(NONTERM_exconCoreE().Format) |> ignore; true
                      | [Parser.NONTERM_typeNameInfo] -> os.Append(NONTERM_typeNameInfoE().Format) |> ignore; true
                      | [Parser.NONTERM_attributeList] -> os.Append(NONTERM_attributeListE().Format) |> ignore; true
                      | [Parser.NONTERM_quoteExpr] -> os.Append(NONTERM_quoteExprE().Format) |> ignore; true
                      | [Parser.NONTERM_typeConstraint] -> os.Append(NONTERM_typeConstraintE().Format) |> ignore; true
                      | [NONTERM_Category_ImplementationFile] -> os.Append(NONTERM_Category_ImplementationFileE().Format) |> ignore; true
                      | [NONTERM_Category_Definition] -> os.Append(NONTERM_Category_DefinitionE().Format) |> ignore; true
                      | [NONTERM_Category_SignatureFile] -> os.Append(NONTERM_Category_SignatureFileE().Format) |> ignore; true
                      | [NONTERM_Category_Pattern] -> os.Append(NONTERM_Category_PatternE().Format) |> ignore; true
                      | [NONTERM_Category_Expr] -> os.Append(NONTERM_Category_ExprE().Format) |> ignore; true
                      | [NONTERM_Category_Type] -> os.Append(NONTERM_Category_TypeE().Format) |> ignore; true
                      | [Parser.NONTERM_typeArgsActual] -> os.Append(NONTERM_typeArgsActualE().Format) |> ignore; true
                      | _ -> 
                          false)
                          
#if DEBUG
              if not foundInContext then
                  Printf.bprintf os ". (no 'in' context found: %+A)" (List.map (List.map Parser.prodIdxToNonTerminal) ctxt.ReducibleProductions)
#else
              foundInContext |> ignore // suppress unused variable warning in RELEASE
#endif
              let fix (s: string) = s.Replace(SR.GetString("FixKeyword"), "").Replace(SR.GetString("FixSymbol"), "").Replace(SR.GetString("FixReplace"), "")
              match (ctxt.ShiftTokens 
                           |> List.map Parser.tokenTagToTokenId 
                           |> List.filter (function Parser.TOKEN_error | Parser.TOKEN_EOF -> false | _ -> true) 
                           |> List.map tokenIdToText 
                           |> Set.ofList 
                           |> Set.toList) with 
              | [tokenName1] -> os.Append(TokenName1E().Format (fix tokenName1)) |> ignore
              | [tokenName1;tokenName2] -> os.Append(TokenName1TokenName2E().Format (fix tokenName1) (fix tokenName2)) |> ignore
              | [tokenName1;tokenName2;tokenName3] -> os.Append(TokenName1TokenName2TokenName3E().Format (fix tokenName1) (fix tokenName2) (fix tokenName3)) |> ignore
              | _ -> ()
        (*
              Printf.bprintf os ".\n\n    state = %A\n    token = %A\n    expect (shift) %A\n    expect (reduce) %A\n   prods=%A\n     non terminals: %A" 
                  ctxt.StateStack
                  ctxt.CurrentToken
                  (List.map Parser.tokenTagToTokenId ctxt.ShiftTokens)
                  (List.map Parser.tokenTagToTokenId ctxt.ReduceTokens)
                  ctxt.ReducibleProductions
                  (List.mapSquared Parser.prodIdxToNonTerminal ctxt.ReducibleProductions)
        *)

      | RuntimeCoercionSourceSealed(denv, ty, _) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          if isTyparTy denv.g ty 
          then os.Append(RuntimeCoercionSourceSealed1E().Format (NicePrint.stringOfTy denv ty)) |> ignore
          else os.Append(RuntimeCoercionSourceSealed2E().Format (NicePrint.stringOfTy denv ty)) |> ignore

      | CoercionTargetSealed(denv, ty, _) -> 
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let ty, _cxs= PrettyTypes.PrettifyType denv.g ty
          os.Append(CoercionTargetSealedE().Format (NicePrint.stringOfTy denv ty)) |> ignore

      | UpcastUnnecessary(_) -> 
          os.Append(UpcastUnnecessaryE().Format) |> ignore

      | TypeTestUnnecessary(_) -> 
          os.Append(TypeTestUnnecessaryE().Format) |> ignore

      | QuotationTranslator.IgnoringPartOfQuotedTermWarning (msg, _) -> 
          Printf.bprintf os "%s" msg

      | OverrideDoesntOverride(denv, impl, minfoVirtOpt, g, amap, m) ->
          let sig1 = DispatchSlotChecking.FormatOverride denv impl
          match minfoVirtOpt with 
          | None -> 
              os.Append(OverrideDoesntOverride1E().Format sig1) |> ignore
          | Some minfoVirt ->
              // https://github.com/Microsoft/visualfsharp/issues/35 
              // Improve error message when attempting to override generic return type with unit:
              // we need to check if unit was used as a type argument
              let rec hasUnitTType_app (types: TType list) =
                  match types with
                  | TType_app (maybeUnit, []) :: ts -> 
                      match maybeUnit.TypeAbbrev with
                      | Some ttype when isUnitTy g ttype -> true
                      | _ -> hasUnitTType_app ts
                  | _ :: ts -> hasUnitTType_app ts
                  | [] -> false

              match minfoVirt.ApparentEnclosingType with
              | TType_app (t, types) when t.IsFSharpInterfaceTycon && hasUnitTType_app types ->
                  // match abstract member with 'unit' passed as generic argument
                  os.Append(OverrideDoesntOverride4E().Format sig1) |> ignore
              | _ -> 
                  os.Append(OverrideDoesntOverride2E().Format sig1) |> ignore
                  let sig2 = DispatchSlotChecking.FormatMethInfoSig g amap m denv minfoVirt
                  if sig1 <> sig2 then 
                      os.Append(OverrideDoesntOverride3E().Format sig2) |> ignore

      | UnionCaseWrongArguments (_, n1, n2, _) ->
          os.Append(UnionCaseWrongArgumentsE().Format n2 n1) |> ignore

      | UnionPatternsBindDifferentNames _ -> 
          os.Append(UnionPatternsBindDifferentNamesE().Format) |> ignore

      | ValueNotContained (denv, mref, implVal, sigVal, f) ->
          let text1, text2 = NicePrint.minimalStringsOfTwoValues denv implVal sigVal
          os.Append(f((fullDisplayTextOfModRef mref), text1, text2)) |> ignore

      | ConstrNotContained (denv, v1, v2, f) ->
          os.Append(f((NicePrint.stringOfUnionCase denv v1), (NicePrint.stringOfUnionCase denv v2))) |> ignore

      | ExnconstrNotContained (denv, v1, v2, f) ->
          os.Append(f((NicePrint.stringOfExnDef denv v1), (NicePrint.stringOfExnDef denv v2))) |> ignore

      | FieldNotContained (denv, v1, v2, f) ->
          os.Append(f((NicePrint.stringOfRecdField denv v1), (NicePrint.stringOfRecdField denv v2))) |> ignore

      | RequiredButNotSpecified (_, mref, k, name, _) ->
          let nsb = new System.Text.StringBuilder()
          name nsb;
          os.Append(RequiredButNotSpecifiedE().Format (fullDisplayTextOfModRef mref) k (nsb.ToString())) |> ignore

      | UseOfAddressOfOperator _ -> 
          os.Append(UseOfAddressOfOperatorE().Format) |> ignore

      | DefensiveCopyWarning(s, _) -> os.Append(DefensiveCopyWarningE().Format s) |> ignore

      | DeprecatedThreadStaticBindingWarning(_) -> 
          os.Append(DeprecatedThreadStaticBindingWarningE().Format) |> ignore

      | FunctionValueUnexpected (denv, ty, _) ->
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          let errorText = FunctionValueUnexpectedE().Format (NicePrint.stringOfTy denv ty)
          os.Append errorText |> ignore

      | UnitTypeExpected (denv, ty, _) ->
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          let warningText = UnitTypeExpectedE().Format (NicePrint.stringOfTy denv ty)
          os.Append warningText |> ignore

      | UnitTypeExpectedWithEquality (denv, ty, _) ->
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          let warningText = UnitTypeExpectedWithEqualityE().Format (NicePrint.stringOfTy denv ty)
          os.Append warningText |> ignore

      | UnitTypeExpectedWithPossiblePropertySetter (denv, ty, bindingName, propertyName, _) ->
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          let warningText = UnitTypeExpectedWithPossiblePropertySetterE().Format (NicePrint.stringOfTy denv ty) bindingName propertyName
          os.Append warningText |> ignore

      | UnitTypeExpectedWithPossibleAssignment (denv, ty, isAlreadyMutable, bindingName, _) ->
          let ty, _cxs = PrettyTypes.PrettifyType denv.g ty
          let warningText = 
            if isAlreadyMutable then
                UnitTypeExpectedWithPossibleAssignmentToMutableE().Format (NicePrint.stringOfTy denv ty) bindingName
            else
                UnitTypeExpectedWithPossibleAssignmentE().Format (NicePrint.stringOfTy denv ty) bindingName
          os.Append warningText |> ignore

      | RecursiveUseCheckedAtRuntime _ -> 
          os.Append(RecursiveUseCheckedAtRuntimeE().Format) |> ignore

      | LetRecUnsound (_, [v], _) ->  
          os.Append(LetRecUnsound1E().Format v.DisplayName) |> ignore

      | LetRecUnsound (_, path, _) -> 
          let bos = new System.Text.StringBuilder()
          (path.Tail @ [path.Head]) |> List.iter (fun (v: ValRef) -> bos.Append(LetRecUnsoundInnerE().Format v.DisplayName) |> ignore) 
          os.Append(LetRecUnsound2E().Format (List.head path).DisplayName (bos.ToString())) |> ignore

      | LetRecEvaluatedOutOfOrder (_, _, _, _) -> 
          os.Append(LetRecEvaluatedOutOfOrderE().Format) |> ignore

      | LetRecCheckedAtRuntime _ -> 
          os.Append(LetRecCheckedAtRuntimeE().Format) |> ignore

      | SelfRefObjCtor(false, _) -> 
          os.Append(SelfRefObjCtor1E().Format) |> ignore

      | SelfRefObjCtor(true, _) -> 
          os.Append(SelfRefObjCtor2E().Format) |> ignore

      | VirtualAugmentationOnNullValuedType(_) ->
          os.Append(VirtualAugmentationOnNullValuedTypeE().Format) |> ignore

      | NonVirtualAugmentationOnNullValuedType(_) ->
          os.Append(NonVirtualAugmentationOnNullValuedTypeE().Format) |> ignore

      | NonUniqueInferredAbstractSlot(_, denv, bindnm, bvirt1, bvirt2, _) ->
          os.Append(NonUniqueInferredAbstractSlot1E().Format bindnm) |> ignore
          let ty1 = bvirt1.ApparentEnclosingType
          let ty2 = bvirt2.ApparentEnclosingType
          // REVIEW: consider if we need to show _cxs (the type parameter constraints)
          let t1, t2, _cxs = NicePrint.minimalStringsOfTwoTypes denv ty1 ty2
          os.Append(NonUniqueInferredAbstractSlot2E().Format) |> ignore
          if t1 <> t2 then 
              os.Append(NonUniqueInferredAbstractSlot3E().Format t1 t2) |> ignore
          os.Append(NonUniqueInferredAbstractSlot4E().Format) |> ignore

      | Error ((_, s), _) -> os.Append s |> ignore

      | ErrorWithSuggestions ((_, s), _, idText, suggestionF) -> 
          os.Append(DecompileOpName s) |> ignore
          suggestNames suggestionF idText

      | NumberedError ((_, s), _) -> os.Append s |> ignore

      | InternalError (s, _) 

      | InvalidArgument s 

      | Failure s as exn ->
          ignore exn // use the argument, even in non DEBUG
          let f1 = SR.GetString("Failure1")
          let f2 = SR.GetString("Failure2") 
          match s with 
          | f when f = f1 -> os.Append(Failure3E().Format s) |> ignore
          | f when f = f2 -> os.Append(Failure3E().Format s) |> ignore
          | _ -> os.Append(Failure4E().Format s) |> ignore
#if DEBUG
          Printf.bprintf os "\nStack Trace\n%s\n" (exn.ToString())
          System.Diagnostics.Debug.Assert(false, sprintf "Unexpected exception seen in compiler: %s\n%s" s (exn.ToString()))
#endif

      | WrappedError (exn, _) -> OutputExceptionR os exn

      | PatternMatchCompilation.MatchIncomplete (isComp, cexOpt, _) -> 
          os.Append(MatchIncomplete1E().Format) |> ignore
          match cexOpt with 
          | None -> ()
          | Some (cex, false) -> os.Append(MatchIncomplete2E().Format cex) |> ignore
          | Some (cex, true) -> os.Append(MatchIncomplete3E().Format cex) |> ignore
          if isComp then 
              os.Append(MatchIncomplete4E().Format) |> ignore

      | PatternMatchCompilation.EnumMatchIncomplete (isComp, cexOpt, _) ->
          os.Append(EnumMatchIncomplete1E().Format) |> ignore
          match cexOpt with
          | None -> ()
          | Some (cex, false) -> os.Append(MatchIncomplete2E().Format cex) |> ignore
          | Some (cex, true) -> os.Append(MatchIncomplete3E().Format cex) |> ignore
          if isComp then
              os.Append(MatchIncomplete4E().Format) |> ignore

      | PatternMatchCompilation.RuleNeverMatched _ -> os.Append(RuleNeverMatchedE().Format) |> ignore

      | ValNotMutable(_, valRef, _) -> os.Append(ValNotMutableE().Format(valRef.DisplayName)) |> ignore

      | ValNotLocal _ -> os.Append(ValNotLocalE().Format) |> ignore

      | ObsoleteError (s, _) 

      | ObsoleteWarning (s, _) -> 
            os.Append(Obsolete1E().Format) |> ignore
            if s <> "" then os.Append(Obsolete2E().Format s) |> ignore

      | Experimental (s, _) -> os.Append(ExperimentalE().Format s) |> ignore

      | PossibleUnverifiableCode _ -> os.Append(PossibleUnverifiableCodeE().Format) |> ignore

      | UserCompilerMessage (msg, _, _) -> os.Append msg |> ignore

      | Deprecated(s, _) -> os.Append(DeprecatedE().Format s) |> ignore

      | LibraryUseOnly(_) -> os.Append(LibraryUseOnlyE().Format) |> ignore

      | MissingFields(sl, _) -> os.Append(MissingFieldsE().Format (String.concat "," sl + ".")) |> ignore

      | ValueRestriction(denv, hassig, v, _, _) -> 
          let denv = { denv with showImperativeTyparAnnotations=true }
          let tau = v.TauType
          if hassig then 
              if isFunTy denv.g tau && (arityOfVal v).HasNoArgs then 
                os.Append(ValueRestriction1E().Format
                  v.DisplayName 
                  (NicePrint.stringOfQualifiedValOrMember denv v)
                  v.DisplayName) |> ignore
              else
                os.Append(ValueRestriction2E().Format
                  v.DisplayName 
                  (NicePrint.stringOfQualifiedValOrMember denv v)
                  v.DisplayName) |> ignore
          else
              match v.MemberInfo with 
              | Some membInfo when 
                  begin match membInfo.MemberFlags.MemberKind with 
                  | MemberKind.PropertyGet 
                  | MemberKind.PropertySet 
                  | MemberKind.Constructor -> true (* can't infer extra polymorphism *)
                  | _ -> false (* can infer extra polymorphism *)
                  end -> 
                      os.Append(ValueRestriction3E().Format (NicePrint.stringOfQualifiedValOrMember denv v)) |> ignore
              | _ -> 
                if isFunTy denv.g tau && (arityOfVal v).HasNoArgs then 
                    os.Append(ValueRestriction4E().Format
                      v.DisplayName
                      (NicePrint.stringOfQualifiedValOrMember denv v)
                      v.DisplayName) |> ignore
                else
                    os.Append(ValueRestriction5E().Format
                      v.DisplayName
                      (NicePrint.stringOfQualifiedValOrMember denv v)
                      v.DisplayName) |> ignore
                

      | Parsing.RecoverableParseError -> os.Append(RecoverableParseErrorE().Format) |> ignore

      | ReservedKeyword (s, _) -> os.Append(ReservedKeywordE().Format s) |> ignore

      | IndentationProblem (s, _) -> os.Append(IndentationProblemE().Format s) |> ignore

      | OverrideInIntrinsicAugmentation(_) -> os.Append(OverrideInIntrinsicAugmentationE().Format) |> ignore

      | OverrideInExtrinsicAugmentation(_) -> os.Append(OverrideInExtrinsicAugmentationE().Format) |> ignore

      | IntfImplInIntrinsicAugmentation(_) -> os.Append(IntfImplInIntrinsicAugmentationE().Format) |> ignore

      | IntfImplInExtrinsicAugmentation(_) -> os.Append(IntfImplInExtrinsicAugmentationE().Format) |> ignore

      | UnresolvedReferenceError(assemblyName, _)

      | UnresolvedReferenceNoRange assemblyName ->
          os.Append(UnresolvedReferenceNoRangeE().Format assemblyName) |> ignore

      | UnresolvedPathReference(assemblyName, pathname, _) 

      | UnresolvedPathReferenceNoRange(assemblyName, pathname) ->
          os.Append(UnresolvedPathReferenceNoRangeE().Format pathname assemblyName) |> ignore

      | DeprecatedCommandLineOptionFull(fullText, _) ->
          os.Append fullText |> ignore

      | DeprecatedCommandLineOptionForHtmlDoc(optionName, _) ->
          os.Append(FSComp.SR.optsDCLOHtmlDoc optionName) |> ignore

      | DeprecatedCommandLineOptionSuggestAlternative(optionName, altOption, _) ->
          os.Append(FSComp.SR.optsDCLODeprecatedSuggestAlternative(optionName, altOption)) |> ignore

      | InternalCommandLineOption(optionName, _) ->
          os.Append(FSComp.SR.optsInternalNoDescription optionName) |> ignore

      | DeprecatedCommandLineOptionNoDescription(optionName, _) ->
          os.Append(FSComp.SR.optsDCLONoDescription optionName) |> ignore

      | HashIncludeNotAllowedInNonScript(_) ->
          os.Append(HashIncludeNotAllowedInNonScriptE().Format) |> ignore

      | HashReferenceNotAllowedInNonScript(_) ->
          os.Append(HashReferenceNotAllowedInNonScriptE().Format) |> ignore

      | HashDirectiveNotAllowedInNonScript(_) ->
          os.Append(HashDirectiveNotAllowedInNonScriptE().Format) |> ignore

      | FileNameNotResolved(filename, locations, _) -> 
          os.Append(FileNameNotResolvedE().Format filename locations) |> ignore

      | AssemblyNotResolved(originalName, _) ->
          os.Append(AssemblyNotResolvedE().Format originalName) |> ignore

      | IllegalFileNameChar(fileName, invalidChar) ->
          os.Append(FSComp.SR.buildUnexpectedFileNameCharacter(fileName, string invalidChar)|>snd) |> ignore

      | HashLoadedSourceHasIssues(warnings, errors, _) -> 
        let Emit(l: exn list) =
            OutputExceptionR os (List.head l)
        if errors=[] then 
            os.Append(HashLoadedSourceHasIssues1E().Format) |> ignore
            Emit warnings
        else
            os.Append(HashLoadedSourceHasIssues2E().Format) |> ignore
            Emit errors

      | HashLoadedScriptConsideredSource(_) ->
          os.Append(HashLoadedScriptConsideredSourceE().Format) |> ignore

      | InvalidInternalsVisibleToAssemblyName(badName, fileNameOption) ->      
          match fileNameOption with      
          | Some file -> os.Append(InvalidInternalsVisibleToAssemblyName1E().Format badName file) |> ignore
          | None -> os.Append(InvalidInternalsVisibleToAssemblyName2E().Format badName) |> ignore

      | LoadedSourceNotFoundIgnoring(filename, _) ->
          os.Append(LoadedSourceNotFoundIgnoringE().Format filename) |> ignore

      | MSBuildReferenceResolutionWarning(code, message, _) 

      | MSBuildReferenceResolutionError(code, message, _) -> 
          os.Append(MSBuildReferenceResolutionErrorE().Format message code) |> ignore

      // Strip TargetInvocationException wrappers
      | :? System.Reflection.TargetInvocationException as e -> 
          OutputExceptionR os e.InnerException

      | :? FileNotFoundException as e -> Printf.bprintf os "%s" e.Message

      | :? DirectoryNotFoundException as e -> Printf.bprintf os "%s" e.Message

      | :? System.ArgumentException as e -> Printf.bprintf os "%s" e.Message

      | :? System.NotSupportedException as e -> Printf.bprintf os "%s" e.Message

      | :? IOException as e -> Printf.bprintf os "%s" e.Message

      | :? System.UnauthorizedAccessException as e -> Printf.bprintf os "%s" e.Message

      | e -> 
          os.Append(TargetInvocationExceptionWrapperE().Format e.Message) |> ignore
#if DEBUG
          Printf.bprintf os "\nStack Trace\n%s\n" (e.ToString())
          if !showAssertForUnexpectedException then 
              System.Diagnostics.Debug.Assert(false, sprintf "Unknown exception seen in compiler: %s" (e.ToString()))
#endif

    OutputExceptionR os err.Exception


// remove any newlines and tabs
let OutputPhasedDiagnostic (os: System.Text.StringBuilder) (err: PhasedDiagnostic) (flattenErrors: bool) (suggestNames: bool) =
    let buf = new System.Text.StringBuilder()

    OutputPhasedErrorR buf err suggestNames
    let s = if flattenErrors then ErrorLogger.NormalizeErrorString (buf.ToString()) else buf.ToString()
    
    os.Append s |> ignore

let SanitizeFileName fileName implicitIncludeDir =
    // The assert below is almost ok, but it fires in two cases:
    //  - fsi.exe sometimes passes "stdin" as a dummy filename
    //  - if you have a #line directive, e.g. 
    //        # 1000 "Line01.fs"
    //    then it also asserts. But these are edge cases that can be fixed later, e.g. in bug 4651.
    //System.Diagnostics.Debug.Assert(FileSystem.IsPathRootedShim fileName, sprintf "filename should be absolute: '%s'" fileName)
    try
        let fullPath = FileSystem.GetFullPathShim fileName
        let currentDir = implicitIncludeDir
        
        // if the file name is not rooted in the current directory, return the full path
        if not(fullPath.StartsWithOrdinal currentDir) then
            fullPath
        // if the file name is rooted in the current directory, return the relative path
        else
            fullPath.Replace(currentDir+"\\", "")
    with _ ->
        fileName

[<RequireQualifiedAccess>]
type DiagnosticLocation =
    { Range: range
      File: string
      TextRepresentation: string
      IsEmpty: bool }

[<RequireQualifiedAccess>]
type DiagnosticCanonicalInformation = 
    { ErrorNumber: int
      Subcategory: string
      TextRepresentation: string }

[<RequireQualifiedAccess>]
type DiagnosticDetailedInfo = 
    { Location: DiagnosticLocation option
      Canonical: DiagnosticCanonicalInformation
      Message: string }

[<RequireQualifiedAccess>]
type Diagnostic = 
    | Short of bool * string
    | Long of bool * DiagnosticDetailedInfo

/// returns sequence that contains Diagnostic for the given error + Diagnostic for all related errors
let CollectDiagnostic (implicitIncludeDir, showFullPaths, flattenErrors, errorStyle, isError, err: PhasedDiagnostic, suggestNames: bool) =
    let outputWhere (showFullPaths, errorStyle) m: DiagnosticLocation =
        if Range.equals m rangeStartup || Range.equals m rangeCmdArgs then
            { Range = m; TextRepresentation = ""; IsEmpty = true; File = "" }
        else
            let file = m.FileName
            let file = if showFullPaths then 
                            Filename.fullpath implicitIncludeDir file
                       else 
                            SanitizeFileName file implicitIncludeDir
            let text, m, file = 
                match errorStyle with
                  | ErrorStyle.EmacsErrors -> 
                    let file = file.Replace("\\", "/")
                    (sprintf "File \"%s\", line %d, characters %d-%d: " file m.StartLine m.StartColumn m.EndColumn), m, file

                  // We're adjusting the columns here to be 1-based - both for parity with C# and for MSBuild, which assumes 1-based columns for error output
                  | ErrorStyle.DefaultErrors -> 
                    let file = file.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    let m = mkRange m.FileName (mkPos m.StartLine (m.StartColumn + 1)) m.End
                    (sprintf "%s(%d,%d): " file m.StartLine m.StartColumn), m, file

                  // We may also want to change TestErrors to be 1-based
                  | ErrorStyle.TestErrors -> 
                    let file = file.Replace("/", "\\")
                    let m = mkRange m.FileName (mkPos m.StartLine (m.StartColumn + 1)) (mkPos m.EndLine (m.EndColumn + 1) )
                    sprintf "%s(%d,%d-%d,%d): " file m.StartLine m.StartColumn m.EndLine m.EndColumn, m, file

                  | ErrorStyle.GccErrors -> 
                    let file = file.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    let m = mkRange m.FileName (mkPos m.StartLine (m.StartColumn + 1)) (mkPos m.EndLine (m.EndColumn + 1) )
                    sprintf "%s:%d:%d: " file m.StartLine m.StartColumn, m, file

                  // Here, we want the complete range information so Project Systems can generate proper squiggles
                  | ErrorStyle.VSErrors -> 
                        // Show prefix only for real files. Otherwise, we just want a truncated error like:
                        //      parse error FS0031: blah blah
                        if not (Range.equals m range0) && not (Range.equals m rangeStartup) && not (Range.equals m rangeCmdArgs) then
                            let file = file.Replace("/", "\\")
                            let m = mkRange m.FileName (mkPos m.StartLine (m.StartColumn + 1)) (mkPos m.EndLine (m.EndColumn + 1) )
                            sprintf "%s(%d,%d,%d,%d): " file m.StartLine m.StartColumn m.EndLine m.EndColumn, m, file
                        else
                            "", m, file
            { Range = m; TextRepresentation = text; IsEmpty = false; File = file }

    match err.Exception with 
    | ReportedError _ -> 
        assert ("" = "Unexpected ReportedError") //  this should never happen 
        Seq.empty
    | StopProcessing -> 
        assert ("" = "Unexpected StopProcessing") // this should never happen 
        Seq.empty
    | _ -> 
        let errors = ResizeArray()
        let report err =
            let OutputWhere err = 
                match GetRangeOfDiagnostic err with 
                | Some m -> Some(outputWhere (showFullPaths, errorStyle) m)
                | None -> None

            let OutputCanonicalInformation(subcategory, errorNumber) : DiagnosticCanonicalInformation = 
                let text = 
                    match errorStyle with
                    // Show the subcategory for --vserrors so that we can fish it out in Visual Studio and use it to determine error stickiness.
                    | ErrorStyle.VSErrors -> sprintf "%s %s FS%04d: " subcategory (if isError then "error" else "warning") errorNumber
                    | _ -> sprintf "%s FS%04d: " (if isError then "error" else "warning") errorNumber
                { ErrorNumber = errorNumber; Subcategory = subcategory; TextRepresentation = text}
        
            let mainError, relatedErrors = SplitRelatedDiagnostics err
            let where = OutputWhere mainError
            let canonical = OutputCanonicalInformation(err.Subcategory(), GetDiagnosticNumber mainError)
            let message = 
                let os = System.Text.StringBuilder()
                OutputPhasedDiagnostic os mainError flattenErrors suggestNames
                os.ToString()
            
            let entry: DiagnosticDetailedInfo = { Location = where; Canonical = canonical; Message = message }
            
            errors.Add ( Diagnostic.Long(isError, entry ) )

            let OutputRelatedError(err: PhasedDiagnostic) =
                match errorStyle with
                // Give a canonical string when --vserror.
                | ErrorStyle.VSErrors -> 
                    let relWhere = OutputWhere mainError // mainError?
                    let relCanonical = OutputCanonicalInformation(err.Subcategory(), GetDiagnosticNumber mainError) // Use main error for code
                    let relMessage = 
                        let os = System.Text.StringBuilder()
                        OutputPhasedDiagnostic os err flattenErrors suggestNames
                        os.ToString()

                    let entry: DiagnosticDetailedInfo = { Location = relWhere; Canonical = relCanonical; Message = relMessage}
                    errors.Add( Diagnostic.Long (isError, entry) )

                | _ -> 
                    let os = System.Text.StringBuilder()
                    OutputPhasedDiagnostic os err flattenErrors suggestNames
                    errors.Add( Diagnostic.Short(isError, os.ToString()) )

            relatedErrors |> List.iter OutputRelatedError

        match err with
#if !NO_EXTENSIONTYPING
        | {Exception = (:? TypeProviderError as tpe)} ->
            tpe.Iter (fun e ->
                let newErr = {err with Exception = e}
                report newErr
            )
#endif
        | x -> report x

        errors:> seq<_>

/// used by fsc.exe and fsi.exe, but not by VS
/// prints error and related errors to the specified StringBuilder
let rec OutputDiagnostic (implicitIncludeDir, showFullPaths, flattenErrors, errorStyle, isError) os (err: PhasedDiagnostic) = 
    
    // 'true' for "canSuggestNames" is passed last here because we want to report suggestions in fsc.exe and fsi.exe, just not in regular IDE usage.
    let errors = CollectDiagnostic (implicitIncludeDir, showFullPaths, flattenErrors, errorStyle, isError, err, true)
    for e in errors do
        Printf.bprintf os "\n"
        match e with
        | Diagnostic.Short(_, txt) -> 
            os.Append txt |> ignore
        | Diagnostic.Long(_, details) ->
            match details.Location with
            | Some l when not l.IsEmpty -> os.Append l.TextRepresentation |> ignore
            | _ -> ()
            os.Append( details.Canonical.TextRepresentation ) |> ignore
            os.Append( details.Message ) |> ignore
      
let OutputDiagnosticContext prefix fileLineFunction os err =
    match GetRangeOfDiagnostic err with
    | None -> ()      
    | Some m -> 
        let filename = m.FileName
        let lineA = m.StartLine
        let lineB = m.EndLine
        let line = fileLineFunction filename lineA
        if line<>"" then 
            let iA = m.StartColumn
            let iB = m.EndColumn
            let iLen = if lineA = lineB then max (iB - iA) 1 else 1
            Printf.bprintf os "%s%s\n" prefix line
            Printf.bprintf os "%s%s%s\n" prefix (String.make iA '-') (String.make iLen '^')

let ReportWarning options err = 
    warningOn err (options.WarnLevel) (options.WarnOn) && not (List.contains (GetDiagnosticNumber err) (options.WarnOff))

let ReportWarningAsError options err =
    warningOn err (options.WarnLevel) (options.WarnOn) &&
    not (List.contains (GetDiagnosticNumber err) (options.WarnAsWarn)) &&
    ((options.GlobalWarnAsError && not (List.contains (GetDiagnosticNumber err) options.WarnOff)) ||
     List.contains (GetDiagnosticNumber err) (options.WarnAsError))

//----------------------------------------------------------------------------
// Scoped #nowarn pragmas


/// Build an ErrorLogger that delegates to another ErrorLogger but filters warnings turned off by the given pragma declarations
//
// NOTE: we allow a flag to turn of strict file checking. This is because file names sometimes don't match due to use of 
// #line directives, e.g. for pars.fs/pars.fsy. In this case we just test by line number - in most cases this is sufficient
// because we install a filtering error handler on a file-by-file basis for parsing and type-checking.
// However this is indicative of a more systematic problem where source-line 
// sensitive operations (lexfilter and warning filtering) do not always
// interact well with #line directives.
type ErrorLoggerFilteringByScopedPragmas (checkFile, scopedPragmas, errorLogger: ErrorLogger) =
    inherit ErrorLogger("ErrorLoggerFilteringByScopedPragmas")

    override x.DiagnosticSink (phasedError, isError) = 
        if isError then 
            errorLogger.DiagnosticSink (phasedError, isError)
        else 
          let report = 
            let warningNum = GetDiagnosticNumber phasedError
            match GetRangeOfDiagnostic phasedError with 
            | Some m -> 
                not (scopedPragmas |> List.exists (fun pragma ->
                    match pragma with 
                    | ScopedPragma.WarningOff(pragmaRange, warningNumFromPragma) -> 
                        warningNum = warningNumFromPragma && 
                        (not checkFile || m.FileIndex = pragmaRange.FileIndex) &&
                        Pos.posGeq m.Start pragmaRange.Start))  
            | None -> true
          if report then errorLogger.DiagnosticSink(phasedError, false)

    override x.ErrorCount = errorLogger.ErrorCount

let GetErrorLoggerFilteringByScopedPragmas(checkFile, scopedPragmas, errorLogger) = 
    (ErrorLoggerFilteringByScopedPragmas(checkFile, scopedPragmas, errorLogger) :> ErrorLogger)

