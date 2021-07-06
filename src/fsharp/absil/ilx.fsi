// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// ILX extensions to Abstract IL types and instructions F# 
module internal FSharp.Compiler.AbstractIL.ILX.Types

open FSharp.Compiler.AbstractIL.IL 

/// Union case field
[<Sealed>]
type IlxUnionCaseField = 
    new: ILFieldDef -> IlxUnionCaseField
    member Type: ILType
    member Name: string
    /// The name used for the field in parameter or IL field position.
    member LowerName: string 
    member ILField: ILFieldDef
    
/// Union alternative
type IlxUnionCase = 
    { altName: string
      altFields: IlxUnionCaseField[]
      altCustomAttrs: ILAttributes }

    member FieldDefs: IlxUnionCaseField[]
    member FieldDef:  int -> IlxUnionCaseField
    member Name:  string
    member IsNullary :  bool
    member FieldTypes:  ILType[]


type IlxUnionHasHelpers = 
   | NoHelpers
   | AllHelpers
   | SpecialFSharpListHelpers 
   | SpecialFSharpOptionHelpers 
   
/// Union references 
type IlxUnionRef = 
    | IlxUnionRef of boxity: ILBoxity * ILTypeRef * IlxUnionCase[] * bool (* cudNullPermitted *)  * IlxUnionHasHelpers (* cudHasHelpers *)

type IlxUnionSpec = 
    | IlxUnionSpec of IlxUnionRef * ILGenericArgs

    member DeclaringType:  ILType

    member GenericArgs:  ILGenericArgs

    member Alternatives:  IlxUnionCase list

    member AlternativesArray:  IlxUnionCase[]

    member Boxity:  ILBoxity

    member TypeRef:  ILTypeRef 

    member IsNullPermitted:  bool

    member HasHelpers:  IlxUnionHasHelpers

    member Alternative:  int -> IlxUnionCase

    member FieldDef: int -> int -> IlxUnionCaseField

// -------------------------------------------------------------------- 
// Closure references 
// -------------------------------------------------------------------- 

type IlxClosureLambdas = 
    | Lambdas_forall of ILGenericParameterDef * IlxClosureLambdas
    | Lambdas_lambda of ILParameter * IlxClosureLambdas
    | Lambdas_return of ILType

type IlxClosureFreeVar = 
    { fvName: string  
      fvCompilerGenerated:bool 
      fvType: ILType }

type IlxClosureRef = 
    | IlxClosureRef of ILTypeRef * IlxClosureLambdas * IlxClosureFreeVar[]

/// Represents a usage of a closure 
type IlxClosureSpec = 
    | IlxClosureSpec of IlxClosureRef * ILGenericArgs * ILType * useStaticField: bool

    member TypeRef: ILTypeRef

    member ILType: ILType

    member ClosureRef: IlxClosureRef

    member FormalLambdas: IlxClosureLambdas

    member FormalFreeVars: IlxClosureFreeVar[]

    member GenericArgs: ILGenericArgs

    static member Create: IlxClosureRef * ILGenericArgs * useStaticField: bool -> IlxClosureSpec

    /// Get the constructor for the closure
    member Constructor: ILMethodSpec

    /// Get the static field used to store an instance of this closure, if useStaticField is true
    member GetStaticFieldSpec: unit -> ILFieldSpec

    /// Indicates if a static field being used to store an instance of this closure (because it has no free variables)
    member UseStaticField: bool


/// IlxClosureApps - i.e. types being applied at a callsite.
type IlxClosureApps = 
    | Apps_tyapp of ILType * IlxClosureApps 
    | Apps_app of ILType * IlxClosureApps 
    | Apps_done of ILType

/// Represents a closure prior to erasure
type IlxClosureInfo = 
    { 
      cloStructure: IlxClosureLambdas
      cloFreeVars: IlxClosureFreeVar[]  
      cloCode: Lazy<ILMethodBody>
      cloUseStaticField: bool 
    }

/// Represents a discriminated union type prior to erasure
type IlxUnionInfo = 
    { 
      /// Is the representation public? 
      cudReprAccess: ILMemberAccess 

      /// Are the representation helpers public? 
      cudHelpersAccess: ILMemberAccess 

      /// Generate the helpers? 
      cudHasHelpers: IlxUnionHasHelpers 

      cudDebugProxies: bool 

      cudDebugDisplayAttributes: ILAttribute list

      cudAlternatives: IlxUnionCase[]

      cudNullPermitted: bool

      /// Debug info for generated code for classunions.
      cudWhere: ILSourceMarker option  

      cudExplicitStructLayout: bool
    }

// -------------------------------------------------------------------- 
// MS-ILX constructs: Closures, thunks, classunions
// -------------------------------------------------------------------- 

val instAppsAux: int -> ILGenericArgs -> IlxClosureApps -> IlxClosureApps
val destTyFuncApp: IlxClosureApps -> ILType * IlxClosureApps

val mkILFormalCloRef: ILGenericParameterDefs -> IlxClosureRef -> useStaticField: bool -> IlxClosureSpec

// -------------------------------------------------------------------- 
// MS-ILX: Unions
// -------------------------------------------------------------------- 

val actualTypOfIlxUnionField: IlxUnionSpec -> int -> int -> ILType

val mkILFreeVar: string * bool * ILType -> IlxClosureFreeVar
