// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.Optimizer

open FSharp.Compiler 
open FSharp.Compiler.TcGlobals 
open FSharp.Compiler.Text
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypedTreePickle

type OptimizationSettings = 
    { abstractBigTargets: bool
      jitOptUser: bool option
      localOptUser: bool option
      crossModuleOptUser: bool option
      bigTargetSize: int
      veryBigExprSize: int 
      lambdaInlineThreshold: int
      reportingPhase: bool
      reportNoNeedToTailcall: bool
      reportFunctionSizes: bool
      reportHasEffect: bool
      reportTotalSizes: bool
    }

    member jitOpt: unit -> bool 

    member localOpt: unit -> bool 

    static member Defaults: OptimizationSettings

/// Optimization information 
type ModuleInfo

type LazyModuleInfo = Lazy<ModuleInfo>

type ImplFileOptimizationInfo = LazyModuleInfo

type CcuOptimizationInfo = LazyModuleInfo

[<Sealed>]
type IncrementalOptimizationEnv =
    static member Empty: IncrementalOptimizationEnv

/// For building optimization environments incrementally 
val internal BindCcu: CcuThunk -> CcuOptimizationInfo -> IncrementalOptimizationEnv -> TcGlobals -> IncrementalOptimizationEnv

/// Optimize one implementation file in the given environment
val internal OptimizeImplFile:
    OptimizationSettings * 
    CcuThunk *
    TcGlobals *
    ConstraintSolver.TcValF *
    Import.ImportMap *
    IncrementalOptimizationEnv *
    isIncrementalFragment: bool *
    emitTailcalls: bool *
    SignatureHidingInfo * 
    TypedImplFile 
       -> (IncrementalOptimizationEnv * TypedImplFile * ImplFileOptimizationInfo * SignatureHidingInfo) * (bool -> Expr -> Expr)

#if DEBUG
/// Displaying optimization data
val internal moduleInfoL: TcGlobals -> LazyModuleInfo -> Layout
#endif

/// Saving and re-reading optimization information 
val p_CcuOptimizationInfo: CcuOptimizationInfo -> WriterState -> unit 

/// Rewrite the module info using the export remapping 
val RemapOptimizationInfo: TcGlobals -> Remap -> (CcuOptimizationInfo -> CcuOptimizationInfo)

/// Ensure that 'internal' items are not exported in the optimization info
val AbstractOptimizationInfoToEssentials: (CcuOptimizationInfo -> CcuOptimizationInfo)

/// Combine optimization infos
val UnionOptimizationInfos: seq<ImplFileOptimizationInfo> -> CcuOptimizationInfo

/// Check if an expression has an effect
val ExprHasEffect: TcGlobals -> Expr -> bool

val internal u_CcuOptimizationInfo: ReaderState -> CcuOptimizationInfo

/// Indicates the value is only mutable during its initialization and before any access or capture
val IsKnownOnlyMutableBeforeUse: ValRef -> bool

