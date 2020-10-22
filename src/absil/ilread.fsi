// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Binary reader.  Read a .NET binary and concert it to Abstract IL data
/// structures.
/// 
/// NOTE:
///   - The metadata in the loaded modules will be relative to 
///     those modules, e.g. ILScopeRef.Local will mean "local to 
///     that module".  You must use [rescopeILType] etc. if you want to include 
///     (i.e. copy) the metadata into your own module. 
///
///   - PDB (debug info) reading/folding:
///     The PDB reader is invoked if you give a PDB path 
///     This indicates if you want to search for PDB files and have the 
///     reader fold them in.  You cannot currently name the pdb file 
///     directly - you can only name the path.  Giving "None" says 
///     "do not read the PDB file even if one exists". 
/// 
///     The debug info appears primarily as I_seqpoint annotations in 
///     the instruction streams.  Unfortunately the PDB information does
///     not, for example, tell you how to map back from a class definition
///     to a source code line number - you will need to explicitly search
///     for a sequence point in the code for one of the methods of the 
///     class.  That is not particularly satisfactory, and it may be
///     a good idea to build a small library which extracts the information
///     you need.  
module FSharp.Compiler.AbstractIL.ILBinaryReader 

open Internal.Utilities
open FSharp.Compiler.AbstractIL 
open FSharp.Compiler.AbstractIL.IL 
open FSharp.Compiler.AbstractIL.Internal 
open FSharp.Compiler.ErrorLogger
open System.IO

/// Used to implement a Binary file over native memory, used by Roslyn integration
type ILReaderMetadataSnapshot = (obj * nativeint * int) 
type ILReaderTryGetMetadataSnapshot = (* path: *) string * (* snapshotTimeStamp: *) System.DateTime -> ILReaderMetadataSnapshot option

[<RequireQualifiedAccess>]
type MetadataOnlyFlag = Yes | No

[<RequireQualifiedAccess>]
type ReduceMemoryFlag = Yes | No

type ILReaderOptions =
   { pdbDirPath: string option

     // fsc.exe does not use reduceMemoryUsage (hence keeps MORE caches in AbstractIL and MORE memory mapping and MORE memory hogging but FASTER and SIMPLER file access)
     // fsi.exe does uses reduceMemoryUsage (hence keeps FEWER caches in AbstractIL and LESS memory mapping and LESS memory hogging but slightly SLOWER file access), because its long running
     // FCS uses reduceMemoryUsage (hence keeps FEWER caches in AbstractIL and LESS memory mapping and LESS memory hogging), because it is typically long running
     reduceMemoryUsage: ReduceMemoryFlag

     /// Only open a metadata reader for the metadata portion of the .NET binary without keeping alive any data associated with the PE reader
     /// - IL code will not be available (mdBody in ILMethodDef will return NotAvailable)
     /// - Managed resources will be reported back as ILResourceLocation.LocalIn (as always)
     /// - Native resources will not be available (none will be returned)
     /// - Static data associated with fields will not be available
     metadataOnly: MetadataOnlyFlag

     /// A function to call to try to get an object that acts as a snapshot of the metadata section of a .NET binary,
     /// and from which we can read the metadata. Only used when metadataOnly=true.
     tryGetMetadataSnapshot: ILReaderTryGetMetadataSnapshot }


/// Represents a reader of the metadata of a .NET binary.  May also give some values (e.g. IL code) from the PE file
/// if it was provided.
type ILModuleReader =
    abstract ILModuleDef: ILModuleDef
    abstract ILAssemblyRefs: ILAssemblyRef list
    
    /// ILModuleReader objects only need to be explicitly disposed if memory mapping is used, i.e. reduceMemoryUsage = false
    inherit System.IDisposable


/// Open a binary reader, except first copy the entire contents of the binary into 
/// memory, close the file and ensure any subsequent reads happen from the in-memory store. 
/// PDB files may not be read with this option. 
val internal OpenILModuleReader: string -> ILReaderOptions -> ILModuleReader

val internal ClearAllILModuleReaderCache : unit -> unit

/// Open a binary reader based on the given bytes. 
val internal OpenILModuleReaderFromBytes: fileNameForDebugOutput:string -> assemblyContents: byte[] -> options: ILReaderOptions -> ILModuleReader

type Statistics = 
    { mutable rawMemoryFileCount : int
      mutable memoryMapFileOpenedCount : int
      mutable memoryMapFileClosedCount : int
      mutable weakByteFileCount : int
      mutable byteFileCount : int }

val GetStatistics : unit -> Statistics

[<AutoOpen>]
module Shim =

    type IAssemblyReader =
        abstract GetILModuleReader: filename: string * readerOptions: ILReaderOptions -> ILModuleReader

    [<Sealed>]
    type DefaultAssemblyReader =
        interface IAssemblyReader

    val mutable AssemblyReader: IAssemblyReader