// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.IlxGen

open System
open System.IO
open System.Reflection
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TcGlobals

/// Indicates how the generated IL code is ultimately emitted 
type IlxGenBackend =
    | IlWriteBackend
    | IlReflectBackend

[<NoEquality; NoComparison>]
type internal IlxGenOptions = 
    { fragName: string

      /// Indicates if we are generating filter blocks
      generateFilterBlocks: bool

      /// Indicates if we should workaround old reflection emit bugs
      workAroundReflectionEmitBugs: bool

      /// Indicates if static array data should be emitted using static blobs
      emitConstantArraysUsingStaticDataBlobs: bool

      /// If this is set, then the last module becomes the "main" module 
      mainMethodInfo: Attribs option

      /// Indicates if local optimizations are active
      localOptimizationsAreOn: bool

      /// Indicates if we are generating debug symbols or not
      generateDebugSymbols: bool

      /// A flag to help test emit of debug information
      testFlagEmitFeeFeeAs100001: bool

      /// Indicates which backend we are generating code for
      ilxBackend: IlxGenBackend

      /// Indicates the code is being generated in FSI.EXE and is executed immediately after code generation
      /// This includes all interactively compiled code, including #load, definitions, and expressions
      isInteractive: bool 

      /// Indicates the code generated is an interactive 'it' expression. We generate a setter to allow clearing of the underlying
      /// storage, even though 'it' is not logically mutable
      isInteractiveItExpr: bool

      /// Indicates that, whenever possible, use callvirt instead of call
      alwaysCallVirt: bool

      /// Indicates that we are not generating method bodies.
      metadataOnly: bool
    }

/// The results of the ILX compilation of one fragment of an assembly
type public IlxGenResults = 
    {
      /// The generated IL/ILX type definitions
      ilTypeDefs: ILTypeDef list

      /// The generated IL/ILX assembly attributes
      ilAssemAttrs: ILAttribute list

      /// The generated IL/ILX .NET module attributes
      ilNetModuleAttrs: ILAttribute list

      /// The attributes for the assembly in F# form
      topAssemblyAttrs: Attribs

      /// The security attributes to attach to the assembly
      permissionSets: ILSecurityDecl list

      /// The generated IL/ILX resources associated with F# quotations
      quotationResourceInfo: (ILTypeRef list * byte[])  list
    }
  
/// Used to support the compilation-inversion operations "ClearGeneratedValue" and "LookupGeneratedValue"
type ExecutionContext =
    {
      LookupFieldRef: (ILFieldRef -> FieldInfo)
      LookupMethodRef: (ILMethodRef -> MethodInfo)
      LookupTypeRef: (ILTypeRef -> Type)
      LookupType: (ILType -> Type)
    } 

/// An incremental ILX code generator for a single assembly
type public IlxAssemblyGenerator =
    /// Create an incremental ILX code generator for a single assembly
    new: Import.ImportMap * TcGlobals * ConstraintSolver.TcValF * CcuThunk -> IlxAssemblyGenerator 
    
    /// Register a set of referenced assemblies with the ILX code generator
    member AddExternalCcus: CcuThunk list -> unit

    /// Register a fragment of the current assembly with the ILX code generator. If 'isIncrementalFragment' is true then the input
    /// is assumed to be a fragment 'typed' into FSI.EXE, otherwise the input is assumed to be the result of a '#load'
    member AddIncrementalLocalAssemblyFragment: isIncrementalFragment: bool * fragName:string * typedImplFiles: TypedImplFile list -> unit

    /// Generate ILX code for an assembly fragment
    member GenerateCode: IlxGenOptions * TypedAssemblyAfterOptimization * Attribs * Attribs -> IlxGenResults

    /// Invert the compilation of the given value and clear the storage of the value
    member ClearGeneratedValue: ExecutionContext * Val -> unit

    /// Invert the compilation of the given value and set the storage of the value, even if it is immutable
    member ForceSetGeneratedValue: ExecutionContext * Val * obj -> unit

    /// Invert the compilation of the given value and return its current dynamic value and its compiled System.Type
    member LookupGeneratedValue: ExecutionContext * Val -> (obj * System.Type) option

val ReportStatistics: TextWriter -> unit

/// Determine if an F#-declared value, method or function is compiled as a method.
val IsFSharpValCompiledAsMethod: TcGlobals -> Val -> bool
