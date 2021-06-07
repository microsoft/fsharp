// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Defines the global environment for all type checking.
///
/// The environment (TcGlobals) are well-known types and values are hard-wired
/// into the compiler.  This lets the compiler perform particular optimizations
/// for these types and values, for example emitting optimized calls for
/// comparison and hashing functions.
module internal FSharp.Compiler.TcGlobals

open System.Collections.Concurrent
open System.Diagnostics

open Internal.Utilities.Library
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILX
open FSharp.Compiler.CompilerGlobalState
open FSharp.Compiler.Features
open FSharp.Compiler.IO
open FSharp.Compiler.Syntax.PrettyNaming
open FSharp.Compiler.Text.FileIndex
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics

open Internal.Utilities

let internal DummyFileNameForRangesWithoutASpecificLocation = startupFileName
let private envRange = rangeN DummyFileNameForRangesWithoutASpecificLocation 0

/// Represents an intrinsic value from FSharp.Core known to the compiler
[<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
type IntrinsicValRef =
    | IntrinsicValRef of NonLocalEntityRef * string * bool * TType * ValLinkageFullKey

    member x.Name = (let (IntrinsicValRef(_, nm, _,  _, _)) = x in nm)

    /// For debugging
    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    /// For debugging
    override x.ToString() = x.Name

let ValRefForIntrinsic (IntrinsicValRef(mvr, _, _, _, key))  = mkNonLocalValRef mvr key

//-------------------------------------------------------------------------
// Access the initial environment: names
//-------------------------------------------------------------------------

[<AutoOpen>]
module FSharpLib =

    let CoreOperatorsCheckedName = FSharpLib.Root + ".Core.Operators.Checked"
    let ControlName              = FSharpLib.Root + ".Control"
    let LinqName                 = FSharpLib.Root + ".Linq"
    let CollectionsName          = FSharpLib.Root + ".Collections"
    let LanguagePrimitivesName   = FSharpLib.Root + ".Core.LanguagePrimitives"
    let CompilerServicesName     = FSharpLib.Root + ".Core.CompilerServices"
    let LinqRuntimeHelpersName   = FSharpLib.Root + ".Linq.RuntimeHelpers"
    let RuntimeHelpersName       = FSharpLib.Root + ".Core.CompilerServices.RuntimeHelpers"
    let ExtraTopLevelOperatorsName = FSharpLib.Root + ".Core.ExtraTopLevelOperators"
    let NativeInteropName                 = FSharpLib.Root + ".NativeInterop"

    let QuotationsName             = FSharpLib.Root + ".Quotations"

    let ControlPath                 = IL.splitNamespace ControlName
    let LinqPath                    = IL.splitNamespace LinqName
    let CollectionsPath             = IL.splitNamespace CollectionsName
    let NativeInteropPath           = IL.splitNamespace NativeInteropName |> Array.ofList
    let CompilerServicesPath        = IL.splitNamespace CompilerServicesName |> Array.ofList
    let LinqRuntimeHelpersPath      = IL.splitNamespace LinqRuntimeHelpersName |> Array.ofList
    let RuntimeHelpersPath          = IL.splitNamespace RuntimeHelpersName |> Array.ofList
    let QuotationsPath              = IL.splitNamespace QuotationsName |> Array.ofList

    let RootPathArray                    = FSharpLib.RootPath |> Array.ofList
    let CorePathArray                    = FSharpLib.CorePath |> Array.ofList
    let LinqPathArray                    = LinqPath |> Array.ofList
    let ControlPathArray                 = ControlPath |> Array.ofList
    let CollectionsPathArray             = CollectionsPath |> Array.ofList

//-------------------------------------------------------------------------
// Access the initial environment: helpers to build references
//-------------------------------------------------------------------------

let private mkNonGenericTy tcref = TType_app(tcref, [])

let mkNonLocalTyconRef2 ccu path n = mkNonLocalTyconRef (mkNonLocalEntityRef ccu path) n

let mk_MFCore_tcref             ccu n = mkNonLocalTyconRef2 ccu FSharpLib.CorePathArray n
let mk_MFQuotations_tcref       ccu n = mkNonLocalTyconRef2 ccu FSharpLib.QuotationsPath n
let mk_MFLinq_tcref             ccu n = mkNonLocalTyconRef2 ccu LinqPathArray n
let mk_MFCollections_tcref      ccu n = mkNonLocalTyconRef2 ccu FSharpLib.CollectionsPathArray n
let mk_MFCompilerServices_tcref ccu n = mkNonLocalTyconRef2 ccu FSharpLib.CompilerServicesPath n
let mk_MFRuntimeHelpers_tcref   ccu n = mkNonLocalTyconRef2 ccu FSharpLib.RuntimeHelpersPath n
let mk_MFControl_tcref          ccu n = mkNonLocalTyconRef2 ccu FSharpLib.ControlPathArray n


type
    [<NoEquality; NoComparison; StructuredFormatDisplay("{DebugText}")>]
    BuiltinAttribInfo =
    | AttribInfo of ILTypeRef * TyconRef

    member this.TyconRef = let (AttribInfo(_, tcref)) = this in tcref

    member this.TypeRef  = let (AttribInfo(tref, _)) = this in tref

    /// For debugging
    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.DebugText = x.ToString()

    /// For debugging
    override x.ToString() = x.TyconRef.ToString()


[<Literal>]
let tname_DebuggerNonUserCodeAttribute = "System.Diagnostics.DebuggerNonUserCodeAttribute"
[<Literal>]
let tname_DebuggableAttribute_DebuggingModes = "DebuggingModes"
[<Literal>]
let tname_DebuggerHiddenAttribute = "System.Diagnostics.DebuggerHiddenAttribute"
[<Literal>]
let tname_DebuggerDisplayAttribute = "System.Diagnostics.DebuggerDisplayAttribute"
[<Literal>]
let tname_DebuggerTypeProxyAttribute = "System.Diagnostics.DebuggerTypeProxyAttribute"
[<Literal>]
let tname_DebuggerStepThroughAttribute = "System.Diagnostics.DebuggerStepThroughAttribute"
[<Literal>]
let tname_DebuggerBrowsableAttribute = "System.Diagnostics.DebuggerBrowsableAttribute"
[<Literal>]
let tname_DebuggerBrowsableState = "System.Diagnostics.DebuggerBrowsableState"

[<Literal>]
let tname_StringBuilder = "System.Text.StringBuilder"
[<Literal>]
let tname_IComparable = "System.IComparable"
[<Literal>]
let tname_Exception = "System.Exception"
[<Literal>]
let tname_Missing = "System.Reflection.Missing"
[<Literal>]
let tname_FormattableString = "System.FormattableString"
[<Literal>]
let tname_SerializationInfo = "System.Runtime.Serialization.SerializationInfo"
[<Literal>]
let tname_StreamingContext = "System.Runtime.Serialization.StreamingContext"
[<Literal>]
let tname_SecurityPermissionAttribute = "System.Security.Permissions.SecurityPermissionAttribute"
[<Literal>]
let tname_Delegate = "System.Delegate"
[<Literal>]
let tname_ValueType = "System.ValueType"
[<Literal>]
let tname_Enum = "System.Enum"
[<Literal>]
let tname_Array = "System.Array"
[<Literal>]
let tname_RuntimeArgumentHandle = "System.RuntimeArgumentHandle"
[<Literal>]
let tname_RuntimeTypeHandle = "System.RuntimeTypeHandle"
[<Literal>]
let tname_RuntimeMethodHandle = "System.RuntimeMethodHandle"
[<Literal>]
let tname_RuntimeFieldHandle = "System.RuntimeFieldHandle"
[<Literal>]
let tname_CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
[<Literal>]
let tname_ReferenceAssemblyAttribute = "System.Runtime.CompilerServices.ReferenceAssemblyAttribute"
[<Literal>]
let tname_DebuggableAttribute = "System.Diagnostics.DebuggableAttribute"
[<Literal>]
let tname_AsyncCallback = "System.AsyncCallback"
[<Literal>]
let tname_IAsyncResult = "System.IAsyncResult"

//-------------------------------------------------------------------------
// Table of all these "globals"
//-------------------------------------------------------------------------

type public TcGlobals(compilingFslib: bool, ilg:ILGlobals, fslibCcu: CcuThunk, directoryToResolveRelativePaths,
                      mlCompatibility: bool, isInteractive:bool,
                      // The helper to find system types amongst referenced DLLs
                      tryFindSysTypeCcu,
                      emitDebugInfoInQuotations: bool, noDebugData: bool,
                      pathMap: PathMap, langVersion: LanguageVersion) =

  let vara = Construct.NewRigidTypar "a" envRange
  let varb = Construct.NewRigidTypar "b" envRange
  let varc = Construct.NewRigidTypar "c" envRange
  let vard = Construct.NewRigidTypar "d" envRange
  let vare = Construct.NewRigidTypar "e" envRange

  let varaTy = mkTyparTy vara
  let varbTy = mkTyparTy varb
  let varcTy = mkTyparTy varc
  let vardTy = mkTyparTy vard
  let vareTy = mkTyparTy vare

  let v_int_tcr        = mk_MFCore_tcref fslibCcu "int"
  let v_nativeint_tcr  = mk_MFCore_tcref fslibCcu "nativeint"
  let v_unativeint_tcr = mk_MFCore_tcref fslibCcu "unativeint"
  let v_int32_tcr      = mk_MFCore_tcref fslibCcu "int32"
  let v_int16_tcr      = mk_MFCore_tcref fslibCcu "int16"
  let v_int64_tcr      = mk_MFCore_tcref fslibCcu "int64"
  let v_uint16_tcr     = mk_MFCore_tcref fslibCcu "uint16"
  let v_uint32_tcr     = mk_MFCore_tcref fslibCcu "uint32"
  let v_uint64_tcr     = mk_MFCore_tcref fslibCcu "uint64"
  let v_sbyte_tcr      = mk_MFCore_tcref fslibCcu "sbyte"
  let v_decimal_tcr    = mk_MFCore_tcref fslibCcu "decimal"
  let v_pdecimal_tcr   = mk_MFCore_tcref fslibCcu "decimal`1"
  let v_byte_tcr       = mk_MFCore_tcref fslibCcu "byte"
  let v_bool_tcr       = mk_MFCore_tcref fslibCcu "bool"
  let v_string_tcr     = mk_MFCore_tcref fslibCcu "string"
  let v_obj_tcr        = mk_MFCore_tcref fslibCcu "obj"
  let v_unit_tcr_canon = mk_MFCore_tcref fslibCcu "Unit"
  let v_unit_tcr_nice  = mk_MFCore_tcref fslibCcu "unit"
  let v_exn_tcr        = mk_MFCore_tcref fslibCcu "exn"
  let v_char_tcr       = mk_MFCore_tcref fslibCcu "char"
  let v_float_tcr      = mk_MFCore_tcref fslibCcu "float"
  let v_float32_tcr    = mk_MFCore_tcref fslibCcu "float32"
  let v_pfloat_tcr      = mk_MFCore_tcref fslibCcu "float`1"
  let v_pfloat32_tcr    = mk_MFCore_tcref fslibCcu "float32`1"
  let v_pint_tcr        = mk_MFCore_tcref fslibCcu "int`1"
  let v_pint8_tcr       = mk_MFCore_tcref fslibCcu "sbyte`1"
  let v_pint16_tcr      = mk_MFCore_tcref fslibCcu "int16`1"
  let v_pint64_tcr      = mk_MFCore_tcref fslibCcu "int64`1"
  let v_pnativeint_tcr  = mk_MFCore_tcref fslibCcu "nativeint`1"
  let v_puint_tcr       = mk_MFCore_tcref fslibCcu "uint`1"
  let v_puint8_tcr      = mk_MFCore_tcref fslibCcu "byte`1"
  let v_puint16_tcr     = mk_MFCore_tcref fslibCcu "uint16`1"
  let v_puint64_tcr     = mk_MFCore_tcref fslibCcu "uint64`1"
  let v_punativeint_tcr = mk_MFCore_tcref fslibCcu "unativeint`1"
  let v_byref_tcr      = mk_MFCore_tcref fslibCcu "byref`1"
  let v_byref2_tcr      = mk_MFCore_tcref fslibCcu "byref`2"
  let v_outref_tcr      = mk_MFCore_tcref fslibCcu "outref`1"
  let v_inref_tcr      = mk_MFCore_tcref fslibCcu "inref`1"
  let v_nativeptr_tcr  = mk_MFCore_tcref fslibCcu "nativeptr`1"
  let v_voidptr_tcr      = mk_MFCore_tcref fslibCcu "voidptr"
  let v_ilsigptr_tcr   = mk_MFCore_tcref fslibCcu "ilsigptr`1"
  let v_fastFunc_tcr   = mk_MFCore_tcref fslibCcu "FSharpFunc`2"
  let v_refcell_tcr_canon = mk_MFCore_tcref fslibCcu "Ref`1"
  let v_refcell_tcr_nice  = mk_MFCore_tcref fslibCcu "ref`1"

  let dummyAssemblyNameCarryingUsefulErrorInformation path typeName =
      FSComp.SR.tcGlobalsSystemTypeNotFound (String.concat "." path + "." + typeName)

  // Search for a type. If it is not found, leave a dangling CCU reference with some useful diagnostic information should
  // the type actually be dereferenced
  let findSysTypeCcu path typeName =
      match tryFindSysTypeCcu path typeName with
      | None -> CcuThunk.CreateDelayed(dummyAssemblyNameCarryingUsefulErrorInformation path typeName)
      | Some ccu -> ccu

  let tryFindSysTyconRef path nm =
      match tryFindSysTypeCcu path nm with
      | Some ccu -> Some (mkNonLocalTyconRef2 ccu (Array.ofList path) nm)
      | None -> None

  let findSysTyconRef path nm =
      let ccu = findSysTypeCcu path nm
      mkNonLocalTyconRef2 ccu (Array.ofList path) nm

  let findSysILTypeRef (nm:string) =
      let path, typeName = splitILTypeName nm
      let scoref =
          match tryFindSysTypeCcu path typeName with
          | None -> ILScopeRef.Assembly (mkSimpleAssemblyRef (dummyAssemblyNameCarryingUsefulErrorInformation path typeName))
          | Some ccu -> ccu.ILScopeRef
      mkILTyRef (scoref, nm)

  let tryFindSysILTypeRef (nm:string) =
      let path, typeName = splitILTypeName nm
      tryFindSysTypeCcu path typeName |> Option.map (fun ccu -> mkILTyRef (ccu.ILScopeRef, nm))

  let findSysAttrib (nm:string) =
      let tref = findSysILTypeRef nm
      let path, typeName = splitILTypeName nm
      AttribInfo(tref, findSysTyconRef path typeName)

  let tryFindSysAttrib nm =
      let path, typeName = splitILTypeName nm
      match tryFindSysTypeCcu path typeName with
      | Some _ -> Some (findSysAttrib nm)
      | None -> None

  let mkSysNonGenericTy path n = mkNonGenericTy(findSysTyconRef path n)
  let tryMkSysNonGenericTy path n = tryFindSysTyconRef path n |> Option.map mkNonGenericTy

  let sys = ["System"]
  let sysLinq = ["System";"Linq"]
  let sysCollections = ["System";"Collections"]
  let sysGenerics = ["System";"Collections";"Generic"]
  let sysCompilerServices = ["System";"Runtime";"CompilerServices"]

  let lazy_tcr = findSysTyconRef sys "Lazy`1"
  let v_fslib_IEvent2_tcr        = mk_MFControl_tcref fslibCcu "IEvent`2"
  let v_tcref_IQueryable      =  findSysTyconRef sysLinq "IQueryable`1"
  let v_tcref_IObservable      =  findSysTyconRef sys "IObservable`1"
  let v_tcref_IObserver        =  findSysTyconRef sys "IObserver`1"
  let v_fslib_IDelegateEvent_tcr = mk_MFControl_tcref fslibCcu "IDelegateEvent`1"

  let v_option_tcr_nice     = mk_MFCore_tcref fslibCcu "option`1"
  let v_valueoption_tcr_nice = mk_MFCore_tcref fslibCcu "voption`1"
  let v_list_tcr_canon        = mk_MFCollections_tcref fslibCcu "List`1"
  let v_list_tcr_nice            = mk_MFCollections_tcref fslibCcu "list`1"
  let v_lazy_tcr_nice            = mk_MFControl_tcref fslibCcu "Lazy`1"
  let v_seq_tcr                  = mk_MFCollections_tcref fslibCcu "seq`1"
  let v_format_tcr               = mk_MFCore_tcref     fslibCcu "PrintfFormat`5"
  let v_format4_tcr              = mk_MFCore_tcref     fslibCcu "PrintfFormat`4"
  let v_date_tcr                 = findSysTyconRef sys "DateTime"
  let v_IEnumerable_tcr          = findSysTyconRef sysGenerics "IEnumerable`1"
  let v_IEnumerator_tcr          = findSysTyconRef sysGenerics "IEnumerator`1"
  let v_System_Attribute_tcr     = findSysTyconRef sys "Attribute"
  let v_expr_tcr                 = mk_MFQuotations_tcref fslibCcu "Expr`1"
  let v_raw_expr_tcr             = mk_MFQuotations_tcref fslibCcu "Expr"
  let v_query_builder_tcref         = mk_MFLinq_tcref fslibCcu "QueryBuilder"
  let v_querySource_tcr         = mk_MFLinq_tcref fslibCcu "QuerySource`2"
  let v_linqExpression_tcr     = findSysTyconRef ["System";"Linq";"Expressions"] "Expression`1"

  let v_il_arr_tcr_map =
      Array.init 32 (fun idx ->
          let type_sig =
              let rank = idx + 1
              if rank = 1 then "[]`1"
              else "[" + (String.replicate (rank - 1) ",") + "]`1"
          mk_MFCore_tcref fslibCcu type_sig)

  let v_byte_ty         = mkNonGenericTy v_byte_tcr
  let v_sbyte_ty        = mkNonGenericTy v_sbyte_tcr
  let v_int16_ty        = mkNonGenericTy v_int16_tcr
  let v_uint16_ty       = mkNonGenericTy v_uint16_tcr
  let v_int_ty          = mkNonGenericTy v_int_tcr
  let v_int32_ty        = mkNonGenericTy v_int32_tcr
  let v_uint32_ty       = mkNonGenericTy v_uint32_tcr
  let v_int64_ty        = mkNonGenericTy v_int64_tcr
  let v_uint64_ty       = mkNonGenericTy v_uint64_tcr
  let v_float32_ty      = mkNonGenericTy v_float32_tcr
  let v_float_ty        = mkNonGenericTy v_float_tcr
  let v_nativeint_ty    = mkNonGenericTy v_nativeint_tcr
  let v_unativeint_ty   = mkNonGenericTy v_unativeint_tcr

  let v_enum_ty         = mkNonGenericTy v_int_tcr
  let v_bool_ty         = mkNonGenericTy v_bool_tcr
  let v_char_ty         = mkNonGenericTy v_char_tcr
  let v_obj_ty          = mkNonGenericTy v_obj_tcr
  let v_IFormattable_tcref = findSysTyconRef sys "IFormattable"
  let v_FormattableString_tcref = findSysTyconRef sys "FormattableString"
  let v_IFormattable_ty = mkNonGenericTy v_IFormattable_tcref
  let v_FormattableString_ty = mkNonGenericTy v_FormattableString_tcref
  let v_FormattableStringFactory_tcref = findSysTyconRef sysCompilerServices "FormattableStringFactory"
  let v_FormattableStringFactory_ty = mkNonGenericTy v_FormattableStringFactory_tcref
  let v_string_ty       = mkNonGenericTy v_string_tcr
  let v_decimal_ty      = mkSysNonGenericTy sys "Decimal"
  let v_unit_ty         = mkNonGenericTy v_unit_tcr_nice
  let v_system_Type_ty = mkSysNonGenericTy sys "Type"
  let v_Array_tcref = findSysTyconRef sys "Array"


  let v_system_Reflection_MethodInfo_ty = mkSysNonGenericTy ["System";"Reflection"] "MethodInfo"
  let v_nullable_tcr = findSysTyconRef sys "Nullable`1"

  (* local helpers to build value infos *)
  let mkNullableTy ty = TType_app(v_nullable_tcr, [ty])
  let mkByrefTy ty = TType_app(v_byref_tcr, [ty])
  let mkNativePtrTy ty = TType_app(v_nativeptr_tcr, [ty])
  let mkFunTy d r = TType_fun (d, r)
  let (-->) d r = mkFunTy d r
  let mkIteratedFunTy dl r = List.foldBack mkFunTy dl r
  let mkSmallRefTupledTy l = match l with [] -> v_unit_ty | [h] -> h | tys -> mkRawRefTupleTy tys
  let mkForallTyIfNeeded d r = match d with [] -> r | tps -> TType_forall(tps, r)

      // A table of all intrinsics that the compiler cares about
  let v_knownIntrinsics = ConcurrentDictionary<(string * string option * string * int), ValRef>(HashIdentity.Structural)

  let makeIntrinsicValRefGeneral isKnown (enclosingEntity, logicalName, memberParentName, compiledNameOpt, typars, (argtys, rty))  =
      let ty = mkForallTyIfNeeded typars (mkIteratedFunTy (List.map mkSmallRefTupledTy argtys) rty)
      let isMember = Option.isSome memberParentName
      let argCount = if isMember then List.sum (List.map List.length argtys) else 0
      let linkageType = if isMember then Some ty else None
      let key = ValLinkageFullKey({ MemberParentMangledName=memberParentName; MemberIsOverride=false; LogicalName=logicalName; TotalArgCount= argCount }, linkageType)
      let vref = IntrinsicValRef(enclosingEntity, logicalName, isMember, ty, key)
      let compiledName = defaultArg compiledNameOpt logicalName

      let key = (enclosingEntity.LastItemMangledName, memberParentName, compiledName, argCount)
      assert not (v_knownIntrinsics.ContainsKey(key))
      if isKnown && not (v_knownIntrinsics.ContainsKey(key)) then
          v_knownIntrinsics.[key] <- ValRefForIntrinsic vref
      vref

  let makeIntrinsicValRef info = makeIntrinsicValRefGeneral true info
  let makeOtherIntrinsicValRef info = makeIntrinsicValRefGeneral false info

  let v_IComparer_ty = mkSysNonGenericTy sysCollections "IComparer"
  let v_IEqualityComparer_ty = mkSysNonGenericTy sysCollections "IEqualityComparer"

  let v_system_RuntimeMethodHandle_ty = mkSysNonGenericTy sys "RuntimeMethodHandle"

  let mk_unop_ty ty             = [[ty]], ty
  let mk_binop_ty ty            = [[ty]; [ty]], ty
  let mk_shiftop_ty ty          = [[ty]; [v_int_ty]], ty
  let mk_binop_ty3 ty1 ty2 ty3  = [[ty1]; [ty2]], ty3
  let mk_rel_sig ty             = [[ty];[ty]], v_bool_ty
  let mk_compare_sig ty         = [[ty];[ty]], v_int_ty
  let mk_hash_sig ty            = [[ty]], v_int_ty
  let mk_compare_withc_sig  ty = [[v_IComparer_ty];[ty]; [ty]], v_int_ty
  let mk_equality_withc_sig ty = [[v_IEqualityComparer_ty];[ty];[ty]], v_bool_ty
  let mk_hash_withc_sig     ty = [[v_IEqualityComparer_ty]; [ty]], v_int_ty
  let mkListTy ty         = TType_app(v_list_tcr_nice, [ty])
  let mkSeqTy ty1         = TType_app(v_seq_tcr, [ty1])
  let mkRefCellTy ty      = TType_app(v_refcell_tcr_canon, [ty])
  let mkOptionTy ty      = TType_app(v_option_tcr_nice, [ty])
  let mkQuerySourceTy ty1 ty2         = TType_app(v_querySource_tcr, [ty1; ty2])
  let v_tcref_System_Collections_IEnumerable         = findSysTyconRef sysCollections "IEnumerable";
  let mkArrayType rank (ty : TType) : TType =
      assert (rank >= 1 && rank <= 32)
      TType_app(v_il_arr_tcr_map.[rank - 1], [ty])
  let mkLazyTy ty         = TType_app(lazy_tcr, [ty])

  let mkPrintfFormatTy aty bty cty dty ety = TType_app(v_format_tcr, [aty;bty;cty;dty; ety])
  let mk_format4_ty aty bty cty dty = TType_app(v_format4_tcr, [aty;bty;cty;dty])
  let mkQuotedExprTy aty = TType_app(v_expr_tcr, [aty])
  let mkRawQuotedExprTy = TType_app(v_raw_expr_tcr, [])
  let mkQueryBuilderTy = TType_app(v_query_builder_tcref, [])
  let mkLinqExpressionTy aty = TType_app(v_linqExpression_tcr, [aty])
  let v_cons_ucref = mkUnionCaseRef v_list_tcr_canon "op_ColonColon"
  let v_nil_ucref  = mkUnionCaseRef v_list_tcr_canon "op_Nil"


  let fslib_MF_nleref                   = mkNonLocalEntityRef fslibCcu FSharpLib.RootPathArray
  let fslib_MFCore_nleref               = mkNonLocalEntityRef fslibCcu FSharpLib.CorePathArray
  let fslib_MFLinq_nleref               = mkNonLocalEntityRef fslibCcu FSharpLib.LinqPathArray
  let fslib_MFCollections_nleref        = mkNonLocalEntityRef fslibCcu FSharpLib.CollectionsPathArray
  let fslib_MFCompilerServices_nleref   = mkNonLocalEntityRef fslibCcu FSharpLib.CompilerServicesPath
  let fslib_MFLinqRuntimeHelpers_nleref = mkNonLocalEntityRef fslibCcu FSharpLib.LinqRuntimeHelpersPath
  let fslib_MFControl_nleref            = mkNonLocalEntityRef fslibCcu FSharpLib.ControlPathArray
  let fslib_MFNativeInterop_nleref      = mkNonLocalEntityRef fslibCcu FSharpLib.NativeInteropPath

  let fslib_MFLanguagePrimitives_nleref        = mkNestedNonLocalEntityRef fslib_MFCore_nleref "LanguagePrimitives"
  let fslib_MFIntrinsicOperators_nleref        = mkNestedNonLocalEntityRef fslib_MFLanguagePrimitives_nleref "IntrinsicOperators"
  let fslib_MFIntrinsicFunctions_nleref        = mkNestedNonLocalEntityRef fslib_MFLanguagePrimitives_nleref "IntrinsicFunctions"
  let fslib_MFHashCompare_nleref               = mkNestedNonLocalEntityRef fslib_MFLanguagePrimitives_nleref "HashCompare"
  let fslib_MFOperators_nleref                 = mkNestedNonLocalEntityRef fslib_MFCore_nleref "Operators"
  let fslib_MFByRefKinds_nleref                 = mkNestedNonLocalEntityRef fslib_MFCore_nleref "ByRefKinds"
  let fslib_MFOperatorIntrinsics_nleref        = mkNestedNonLocalEntityRef fslib_MFOperators_nleref "OperatorIntrinsics"
  let fslib_MFOperatorsUnchecked_nleref        = mkNestedNonLocalEntityRef fslib_MFOperators_nleref "Unchecked"
  let fslib_MFOperatorsChecked_nleref        = mkNestedNonLocalEntityRef fslib_MFOperators_nleref "Checked"
  let fslib_MFExtraTopLevelOperators_nleref    = mkNestedNonLocalEntityRef fslib_MFCore_nleref "ExtraTopLevelOperators"
  let fslib_MFNullableOperators_nleref         = mkNestedNonLocalEntityRef fslib_MFLinq_nleref "NullableOperators"
  let fslib_MFQueryRunExtensions_nleref              = mkNestedNonLocalEntityRef fslib_MFLinq_nleref "QueryRunExtensions"
  let fslib_MFQueryRunExtensionsLowPriority_nleref   = mkNestedNonLocalEntityRef fslib_MFQueryRunExtensions_nleref "LowPriority"
  let fslib_MFQueryRunExtensionsHighPriority_nleref  = mkNestedNonLocalEntityRef fslib_MFQueryRunExtensions_nleref "HighPriority"

  let fslib_MFPrintfModule_nleref                 = mkNestedNonLocalEntityRef fslib_MFCore_nleref "PrintfModule"
  let fslib_MFSeqModule_nleref                 = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "SeqModule"
  let fslib_MFListModule_nleref                = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "ListModule"
  let fslib_MFArrayModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "ArrayModule"
  let fslib_MFArray2DModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "Array2DModule"
  let fslib_MFArray3DModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "Array3DModule"
  let fslib_MFArray4DModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "Array4DModule"
  let fslib_MFSetModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "SetModule"
  let fslib_MFMapModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "MapModule"
  let fslib_MFStringModule_nleref               = mkNestedNonLocalEntityRef fslib_MFCollections_nleref "StringModule"
  let fslib_MFNativePtrModule_nleref               = mkNestedNonLocalEntityRef fslib_MFNativeInterop_nleref "NativePtrModule"
  let fslib_MFOptionModule_nleref              = mkNestedNonLocalEntityRef fslib_MFCore_nleref "OptionModule"
  let fslib_MFRuntimeHelpers_nleref            = mkNestedNonLocalEntityRef fslib_MFCompilerServices_nleref "RuntimeHelpers"
  let fslib_MFQuotations_nleref                = mkNestedNonLocalEntityRef fslib_MF_nleref "Quotations"

  let fslib_MFLinqRuntimeHelpersQuotationConverter_nleref        = mkNestedNonLocalEntityRef fslib_MFLinqRuntimeHelpers_nleref "LeafExpressionConverter"
  let fslib_MFLazyExtensions_nleref            = mkNestedNonLocalEntityRef fslib_MFControl_nleref "LazyExtensions"

  let v_ref_tuple1_tcr      = findSysTyconRef sys "Tuple`1"
  let v_ref_tuple2_tcr      = findSysTyconRef sys "Tuple`2"
  let v_ref_tuple3_tcr      = findSysTyconRef sys "Tuple`3"
  let v_ref_tuple4_tcr      = findSysTyconRef sys "Tuple`4"
  let v_ref_tuple5_tcr      = findSysTyconRef sys "Tuple`5"
  let v_ref_tuple6_tcr      = findSysTyconRef sys "Tuple`6"
  let v_ref_tuple7_tcr      = findSysTyconRef sys "Tuple`7"
  let v_ref_tuple8_tcr      = findSysTyconRef sys "Tuple`8"
  let v_struct_tuple1_tcr      = findSysTyconRef sys "ValueTuple`1"
  let v_struct_tuple2_tcr      = findSysTyconRef sys "ValueTuple`2"
  let v_struct_tuple3_tcr      = findSysTyconRef sys "ValueTuple`3"
  let v_struct_tuple4_tcr      = findSysTyconRef sys "ValueTuple`4"
  let v_struct_tuple5_tcr      = findSysTyconRef sys "ValueTuple`5"
  let v_struct_tuple6_tcr      = findSysTyconRef sys "ValueTuple`6"
  let v_struct_tuple7_tcr      = findSysTyconRef sys "ValueTuple`7"
  let v_struct_tuple8_tcr      = findSysTyconRef sys "ValueTuple`8"

  let v_choice2_tcr     = mk_MFCore_tcref fslibCcu "Choice`2"
  let v_choice3_tcr     = mk_MFCore_tcref fslibCcu "Choice`3"
  let v_choice4_tcr     = mk_MFCore_tcref fslibCcu "Choice`4"
  let v_choice5_tcr     = mk_MFCore_tcref fslibCcu "Choice`5"
  let v_choice6_tcr     = mk_MFCore_tcref fslibCcu "Choice`6"
  let v_choice7_tcr     = mk_MFCore_tcref fslibCcu "Choice`7"
  let tyconRefEq x y = primEntityRefEq compilingFslib fslibCcu  x y

  let v_suppressed_types =
    [ mk_MFCore_tcref fslibCcu "Option`1";
      mk_MFCore_tcref fslibCcu "Ref`1";
      mk_MFCore_tcref fslibCcu "FSharpTypeFunc";
      mk_MFCore_tcref fslibCcu "FSharpFunc`2";
      mk_MFCore_tcref fslibCcu "Unit" ]

  let v_knownFSharpCoreModules =
     dict [ for nleref in [ fslib_MFLanguagePrimitives_nleref
                            fslib_MFIntrinsicOperators_nleref
                            fslib_MFIntrinsicFunctions_nleref
                            fslib_MFHashCompare_nleref
                            fslib_MFOperators_nleref
                            fslib_MFOperatorIntrinsics_nleref
                            fslib_MFOperatorsUnchecked_nleref
                            fslib_MFOperatorsChecked_nleref
                            fslib_MFExtraTopLevelOperators_nleref
                            fslib_MFNullableOperators_nleref
                            fslib_MFQueryRunExtensions_nleref
                            fslib_MFQueryRunExtensionsLowPriority_nleref
                            fslib_MFQueryRunExtensionsHighPriority_nleref

                            fslib_MFPrintfModule_nleref
                            fslib_MFSeqModule_nleref
                            fslib_MFListModule_nleref
                            fslib_MFArrayModule_nleref
                            fslib_MFArray2DModule_nleref
                            fslib_MFArray3DModule_nleref
                            fslib_MFArray4DModule_nleref
                            fslib_MFSetModule_nleref
                            fslib_MFMapModule_nleref
                            fslib_MFStringModule_nleref
                            fslib_MFNativePtrModule_nleref
                            fslib_MFOptionModule_nleref
                            fslib_MFRuntimeHelpers_nleref ] do

                    yield nleref.LastItemMangledName, ERefNonLocal nleref  ]

  let tryDecodeTupleTy tupInfo l =
      match l with
      | [t1;t2;t3;t4;t5;t6;t7;marker] ->
          match marker with
          | TType_app(tcref, [t8]) when tyconRefEq tcref v_ref_tuple1_tcr -> mkRawRefTupleTy [t1;t2;t3;t4;t5;t6;t7;t8] |> Some
          | TType_app(tcref, [t8]) when tyconRefEq tcref v_struct_tuple1_tcr -> mkRawStructTupleTy [t1;t2;t3;t4;t5;t6;t7;t8] |> Some
          | TType_tuple (_structness2, t8plus) -> TType_tuple (tupInfo, [t1;t2;t3;t4;t5;t6;t7] @ t8plus) |> Some
          | _ -> None
      | [] -> None
      | [_] -> None
      | _ -> TType_tuple (tupInfo, l)  |> Some


  let decodeTupleTy tupInfo l =
      match tryDecodeTupleTy tupInfo l with
      | Some ty -> ty
      | None -> failwith "couldn't decode tuple ty"

  let decodeTupleTyIfPossible tcref tupInfo l =
      match tryDecodeTupleTy tupInfo l with
      | Some ty -> ty
      | None -> TType_app(tcref, l)

  let mk_MFCore_attrib nm : BuiltinAttribInfo =
      AttribInfo(mkILTyRef(ilg.fsharpCoreAssemblyScopeRef, FSharpLib.Core + "." + nm), mk_MFCore_tcref fslibCcu nm)

  let mk_doc filename = ILSourceDocument.Create(language=None, vendor=None, documentType=None, file=filename)
  // Build the memoization table for files
  let v_memoize_file = new MemoizationTable<int, ILSourceDocument> ((fileOfFileIndex >> FileSystem.GetFullFilePathInDirectoryShim directoryToResolveRelativePaths >> mk_doc), keyComparer=HashIdentity.Structural)

  let v_and_info =                   makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    CompileOpName "&"                      , None                 , None          , [],         mk_rel_sig v_bool_ty)
  let v_addrof_info =                makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    CompileOpName "~&"                     , None                 , None          , [vara],     ([[varaTy]], mkByrefTy varaTy))
  let v_addrof2_info =               makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    CompileOpName "~&&"                    , None                 , None          , [vara],     ([[varaTy]], mkNativePtrTy varaTy))
  let v_and2_info =                  makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    CompileOpName "&&"                     , None                 , None          , [],         mk_rel_sig v_bool_ty)
  let v_or_info =                    makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    "or"                                   , None                 , Some "Or"     , [],         mk_rel_sig v_bool_ty)
  let v_or2_info =                   makeIntrinsicValRef(fslib_MFIntrinsicOperators_nleref,                    CompileOpName "||"                     , None                 , None          , [],         mk_rel_sig v_bool_ty)
  let v_compare_operator_info                = makeIntrinsicValRef(fslib_MFOperators_nleref,                   "compare"                              , None                 , Some "Compare", [vara],     mk_compare_sig varaTy)
  let v_equals_operator_info                 = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName "="                      , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_equals_nullable_operator_info        = makeIntrinsicValRef(fslib_MFNullableOperators_nleref,           CompileOpName "=?"                     , None                 , None          , [vara],     ([[varaTy];[mkNullableTy varaTy]], v_bool_ty))
  let v_nullable_equals_operator_info        = makeIntrinsicValRef(fslib_MFNullableOperators_nleref,           CompileOpName "?="                     , None                 , None          , [vara],     ([[mkNullableTy varaTy];[varaTy]], v_bool_ty))
  let v_nullable_equals_nullable_operator_info  = makeIntrinsicValRef(fslib_MFNullableOperators_nleref,        CompileOpName "?=?"                    , None                 , None          , [vara],     ([[mkNullableTy varaTy];[mkNullableTy varaTy]], v_bool_ty))
  let v_not_equals_operator_info             = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName "<>"                     , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_less_than_operator_info              = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName "<"                      , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_less_than_or_equals_operator_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName "<="                     , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_greater_than_operator_info           = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName ">"                      , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_greater_than_or_equals_operator_info = makeIntrinsicValRef(fslib_MFOperators_nleref,                   CompileOpName ">="                     , None                 , None          , [vara],     mk_rel_sig varaTy)

  let v_enumOfValue_info                     = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,          "EnumOfValue"                          , None                 , None          , [vara; varb],     ([[varaTy]], varbTy))

  let v_generic_comparison_withc_outer_info = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,           "GenericComparisonWithComparer"        , None                 , None          , [vara],     mk_compare_withc_sig  varaTy)
  let v_generic_hash_withc_tuple2_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastHashTuple2"                                   , None                 , None          , [vara;varb],               mk_hash_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy]))
  let v_generic_hash_withc_tuple3_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastHashTuple3"                                   , None                 , None          , [vara;varb;varc],          mk_hash_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy]))
  let v_generic_hash_withc_tuple4_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastHashTuple4"                                   , None                 , None          , [vara;varb;varc;vard],     mk_hash_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy]))
  let v_generic_hash_withc_tuple5_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastHashTuple5"                                   , None                 , None          , [vara;varb;varc;vard;vare], mk_hash_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy; vareTy]))
  let v_generic_equals_withc_tuple2_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastEqualsTuple2"                               , None                 , None          , [vara;varb],               mk_equality_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy]))
  let v_generic_equals_withc_tuple3_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastEqualsTuple3"                               , None                 , None          , [vara;varb;varc],          mk_equality_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy]))
  let v_generic_equals_withc_tuple4_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastEqualsTuple4"                               , None                 , None          , [vara;varb;varc;vard],     mk_equality_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy]))
  let v_generic_equals_withc_tuple5_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastEqualsTuple5"                               , None                 , None          , [vara;varb;varc;vard;vare], mk_equality_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy; vareTy]))

  let v_generic_compare_withc_tuple2_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastCompareTuple2"                             , None                 , None          , [vara;varb],               mk_compare_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy]))
  let v_generic_compare_withc_tuple3_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastCompareTuple3"                             , None                 , None          , [vara;varb;varc],          mk_compare_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy]))
  let v_generic_compare_withc_tuple4_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastCompareTuple4"                             , None                 , None          , [vara;varb;varc;vard],     mk_compare_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy]))
  let v_generic_compare_withc_tuple5_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,           "FastCompareTuple5"                             , None                 , None          , [vara;varb;varc;vard;vare], mk_compare_withc_sig (decodeTupleTy tupInfoRef [varaTy; varbTy; varcTy; vardTy; vareTy]))


  let v_generic_equality_er_outer_info             = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,    "GenericEqualityER"                    , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_get_generic_comparer_info               = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,       "GenericComparer"                      , None                 , None          , [],         ([], v_IComparer_ty))
  let v_get_generic_er_equality_comparer_info      = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,    "GenericEqualityERComparer"            , None                 , None          , [],         ([], v_IEqualityComparer_ty))
  let v_get_generic_per_equality_comparer_info  = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,       "GenericEqualityComparer"              , None                 , None          , [],         ([], v_IEqualityComparer_ty))
  let v_generic_equality_withc_outer_info       = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,       "GenericEqualityWithComparer"          , None                 , None          , [vara],     mk_equality_withc_sig varaTy)
  let v_generic_hash_withc_outer_info           = makeIntrinsicValRef(fslib_MFLanguagePrimitives_nleref,       "GenericHashWithComparer"              , None                 , None          , [vara],     mk_hash_withc_sig varaTy)

  let v_generic_equality_er_inner_info         = makeIntrinsicValRef(fslib_MFHashCompare_nleref,               "GenericEqualityERIntrinsic"           , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_generic_equality_per_inner_info     = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                  "GenericEqualityIntrinsic"             , None                 , None          , [vara],     mk_rel_sig varaTy)
  let v_generic_equality_withc_inner_info   = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                  "GenericEqualityWithComparerIntrinsic" , None                 , None          , [vara],     mk_equality_withc_sig varaTy)
  let v_generic_comparison_inner_info       = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                  "GenericComparisonIntrinsic"           , None                 , None          , [vara],     mk_compare_sig varaTy)
  let v_generic_comparison_withc_inner_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                  "GenericComparisonWithComparerIntrinsic", None                , None          , [vara],     mk_compare_withc_sig varaTy)

  let v_generic_hash_inner_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                              "GenericHashIntrinsic"                 , None                 , None          , [vara],     mk_hash_sig varaTy)
  let v_generic_hash_withc_inner_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                        "GenericHashWithComparerIntrinsic"     , None                 , None          , [vara],     mk_hash_withc_sig  varaTy)

  let v_create_instance_info       = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "CreateInstance"                       , None                 , None          , [vara],     ([[v_unit_ty]], varaTy))
  let v_unbox_info                 = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "UnboxGeneric"                         , None                 , None          , [vara],     ([[v_obj_ty]], varaTy))

  let v_unbox_fast_info            = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "UnboxFast"                            , None                 , None          , [vara],     ([[v_obj_ty]], varaTy))
  let v_istype_info                = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "TypeTestGeneric"                      , None                 , None          , [vara],     ([[v_obj_ty]], v_bool_ty))
  let v_istype_fast_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "TypeTestFast"                         , None                 , None          , [vara],     ([[v_obj_ty]], v_bool_ty))

  let v_dispose_info               = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "Dispose"                              , None                 , None          , [vara],     ([[varaTy]], v_unit_ty))

  let v_getstring_info             = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "GetString"                            , None                 , None          , [],         ([[v_string_ty];[v_int_ty]], v_char_ty))

  let v_reference_equality_inner_info = makeIntrinsicValRef(fslib_MFHashCompare_nleref,                        "PhysicalEqualityIntrinsic"            , None                 , None          , [vara],     mk_rel_sig varaTy)

  let v_bitwise_or_info            = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_BitwiseOr"                         , None                 , None          , [vara],     mk_binop_ty varaTy)
  let v_bitwise_and_info           = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_BitwiseAnd"                        , None                 , None          , [vara],     mk_binop_ty varaTy)
  let v_bitwise_xor_info           = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_ExclusiveOr"                       , None                 , None          , [vara],     mk_binop_ty varaTy)
  let v_bitwise_unary_not_info     = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_LogicalNot"                        , None                 , None          , [vara],     mk_unop_ty varaTy)
  let v_bitwise_shift_left_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_LeftShift"                         , None                 , None          , [vara],     mk_shiftop_ty varaTy)
  let v_bitwise_shift_right_info   = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_RightShift"                        , None                 , None          , [vara],     mk_shiftop_ty varaTy)
  let v_unchecked_addition_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Addition"                          , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_unchecked_subtraction_info = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Subtraction"                       , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_unchecked_multiply_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Multiply"                          , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_unchecked_division_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Division"                          , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_unchecked_modulus_info     = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Modulus"                           , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_unchecked_unary_plus_info  = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_UnaryPlus"                         , None                 , None          , [vara],     mk_unop_ty varaTy)
  let v_unchecked_unary_minus_info = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_UnaryNegation"                     , None                 , None          , [vara],     mk_unop_ty varaTy)
  let v_unchecked_unary_not_info   = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "not"                                  , None                 , Some "Not"    , [],     mk_unop_ty v_bool_ty)

  let v_checked_addition_info      = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "op_Addition"                          , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_checked_subtraction_info   = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "op_Subtraction"                       , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_checked_multiply_info      = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "op_Multiply"                          , None                 , None          , [vara;varb;varc],     mk_binop_ty3 varaTy varbTy  varcTy)
  let v_checked_unary_minus_info   = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "op_UnaryNegation"                     , None                 , None          , [vara],     mk_unop_ty varaTy)

  let v_byte_checked_info          = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "byte"                                 , None                 , Some "ToByte",    [vara],   ([[varaTy]], v_byte_ty))
  let v_sbyte_checked_info         = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "sbyte"                                , None                 , Some "ToSByte",   [vara],   ([[varaTy]], v_sbyte_ty))
  let v_int16_checked_info         = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "int16"                                , None                 , Some "ToInt16",   [vara],   ([[varaTy]], v_int16_ty))
  let v_uint16_checked_info        = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "uint16"                               , None                 , Some "ToUInt16",  [vara],   ([[varaTy]], v_uint16_ty))
  let v_int_checked_info           = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "int"                                  , None                 , Some "ToInt",     [vara],   ([[varaTy]], v_int_ty))
  let v_int32_checked_info         = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "int32"                                , None                 , Some "ToInt32",   [vara],   ([[varaTy]], v_int32_ty))
  let v_uint32_checked_info        = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "uint32"                               , None                 , Some "ToUInt32",  [vara],   ([[varaTy]], v_uint32_ty))
  let v_int64_checked_info         = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "int64"                                , None                 , Some "ToInt64",   [vara],   ([[varaTy]], v_int64_ty))
  let v_uint64_checked_info        = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "uint64"                               , None                 , Some "ToUInt64",  [vara],   ([[varaTy]], v_uint64_ty))
  let v_nativeint_checked_info     = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "nativeint"                            , None                 , Some "ToIntPtr",  [vara],   ([[varaTy]], v_nativeint_ty))
  let v_unativeint_checked_info    = makeIntrinsicValRef(fslib_MFOperatorsChecked_nleref,                      "unativeint"                           , None                 , Some "ToUIntPtr", [vara],   ([[varaTy]], v_unativeint_ty))

  let v_byte_operator_info         = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "byte"                                 , None                 , Some "ToByte",    [vara],   ([[varaTy]], v_byte_ty))
  let v_sbyte_operator_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "sbyte"                                , None                 , Some "ToSByte",   [vara],   ([[varaTy]], v_sbyte_ty))
  let v_int16_operator_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "int16"                                , None                 , Some "ToInt16",   [vara],   ([[varaTy]], v_int16_ty))
  let v_uint16_operator_info       = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "uint16"                               , None                 , Some "ToUInt16",  [vara],   ([[varaTy]], v_uint16_ty))
  let v_int_operator_info          = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "int"                                  , None                 , Some "ToInt",     [vara],   ([[varaTy]], v_int_ty))
  let v_int32_operator_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "int32"                                , None                 , Some "ToInt32",   [vara],   ([[varaTy]], v_int32_ty))
  let v_uint32_operator_info       = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "uint32"                               , None                 , Some "ToUInt32",  [vara],   ([[varaTy]], v_uint32_ty))
  let v_int64_operator_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "int64"                                , None                 , Some "ToInt64",   [vara],   ([[varaTy]], v_int64_ty))
  let v_uint64_operator_info       = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "uint64"                               , None                 , Some "ToUInt64",  [vara],   ([[varaTy]], v_uint64_ty))
  let v_float32_operator_info      = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "float32"                              , None                 , Some "ToSingle",  [vara],   ([[varaTy]], v_float32_ty))
  let v_float_operator_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "float"                                , None                 , Some "ToDouble",  [vara],   ([[varaTy]], v_float_ty))
  let v_nativeint_operator_info    = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "nativeint"                            , None                 , Some "ToIntPtr",  [vara],   ([[varaTy]], v_nativeint_ty))
  let v_unativeint_operator_info   = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "unativeint"                           , None                 , Some "ToUIntPtr", [vara],   ([[varaTy]], v_unativeint_ty))

  let v_char_operator_info         = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "char"                                 , None                 , Some "ToChar",    [vara],   ([[varaTy]], v_char_ty))
  let v_enum_operator_info         = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "enum"                                 , None                 , Some "ToEnum",    [vara],   ([[varaTy]], v_enum_ty))

  let v_hash_info                  = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "hash"                                 , None                 , Some "Hash"   , [vara],     ([[varaTy]], v_int_ty))
  let v_box_info                   = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "box"                                  , None                 , Some "Box"    , [vara],     ([[varaTy]], v_obj_ty))
  let v_isnull_info                = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "isNull"                               , None                 , Some "IsNull" , [vara],     ([[varaTy]], v_bool_ty))
  let v_isnotnull_info             = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "isNotNull"                            , None                 , Some "IsNotNull" , [vara],  ([[varaTy]], v_bool_ty))
  let v_raise_info                 = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "raise"                                , None                 , Some "Raise"  , [vara],     ([[mkSysNonGenericTy sys "Exception"]], varaTy))
  let v_failwith_info              = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "failwith"                             , None                 , Some "FailWith" , [vara],   ([[v_string_ty]], varaTy))
  let v_invalid_arg_info           = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "invalidArg"                           , None                 , Some "InvalidArg" , [vara], ([[v_string_ty]; [v_string_ty]], varaTy))
  let v_null_arg_info              = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "nullArg"                              , None                 , Some "NullArg" , [vara],    ([[v_string_ty]], varaTy))
  let v_invalid_op_info            = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "invalidOp"                            , None                 , Some "InvalidOp" , [vara],  ([[v_string_ty]], varaTy))
  let v_failwithf_info             = makeIntrinsicValRef(fslib_MFExtraTopLevelOperators_nleref,                "failwithf"                            , None                 , Some "PrintFormatToStringThenFail" , [vara;varb], ([[mk_format4_ty varaTy v_unit_ty v_string_ty v_string_ty]], varaTy))

  let v_reraise_info               = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "reraise"                              , None                 , Some "Reraise", [vara],     ([[v_unit_ty]], varaTy))
  let v_typeof_info                = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "typeof"                               , None                 , Some "TypeOf" , [vara],     ([], v_system_Type_ty))
  let v_methodhandleof_info        = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "methodhandleof"                       , None                 , Some "MethodHandleOf", [vara;varb], ([[varaTy --> varbTy]], v_system_RuntimeMethodHandle_ty))
  let v_sizeof_info                = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "sizeof"                               , None                 , Some "SizeOf" , [vara],     ([], v_int_ty))
  let v_nameof_info                = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "nameof"                               , None                 , Some "NameOf" , [vara],     ([[varaTy]], v_string_ty))

  let v_unchecked_defaultof_info   = makeIntrinsicValRef(fslib_MFOperatorsUnchecked_nleref,                    "defaultof"                            , None                 , Some "DefaultOf", [vara],     ([], varaTy))
  let v_typedefof_info             = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "typedefof"                            , None                 , Some "TypeDefOf", [vara],     ([], v_system_Type_ty))
  let v_range_op_info              = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_Range"                             , None                 , None          , [vara],     ([[varaTy];[varaTy]], mkSeqTy varaTy))
  let v_range_step_op_info         = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "op_RangeStep"                         , None                 , None          , [vara;varb], ([[varaTy];[varbTy];[varaTy]], mkSeqTy varaTy))
  let v_range_int32_op_info        = makeIntrinsicValRef(fslib_MFOperatorIntrinsics_nleref,                    "RangeInt32"                           , None                 , None          , [],     ([[v_int_ty];[v_int_ty];[v_int_ty]], mkSeqTy v_int_ty))

  let v_array_length_info          = makeIntrinsicValRef(fslib_MFArrayModule_nleref,                           "length"                               , None                 , Some "Length" , [vara],     ([[mkArrayType 1 varaTy]], v_int_ty))
  let v_array_get_info             = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "GetArray"                             , None                 , None          , [vara],     ([[mkArrayType 1 varaTy]; [v_int_ty]], varaTy))
  let v_array2D_get_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "GetArray2D"                           , None                 , None          , [vara],     ([[mkArrayType 2 varaTy];[v_int_ty]; [v_int_ty]], varaTy))
  let v_array3D_get_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "GetArray3D"                           , None                 , None          , [vara],     ([[mkArrayType 3 varaTy];[v_int_ty]; [v_int_ty]; [v_int_ty]], varaTy))
  let v_array4D_get_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "GetArray4D"                           , None                 , None          , [vara],     ([[mkArrayType 4 varaTy];[v_int_ty]; [v_int_ty]; [v_int_ty]; [v_int_ty]], varaTy))
  let v_array_set_info             = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "SetArray"                             , None                 , None          , [vara],     ([[mkArrayType 1 varaTy]; [v_int_ty]; [varaTy]], v_unit_ty))
  let v_array2D_set_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "SetArray2D"                           , None                 , None          , [vara],     ([[mkArrayType 2 varaTy];[v_int_ty]; [v_int_ty]; [varaTy]], v_unit_ty))
  let v_array3D_set_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "SetArray3D"                           , None                 , None          , [vara],     ([[mkArrayType 3 varaTy];[v_int_ty]; [v_int_ty]; [v_int_ty]; [varaTy]], v_unit_ty))
  let v_array4D_set_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "SetArray4D"                           , None                 , None          , [vara],     ([[mkArrayType 4 varaTy];[v_int_ty]; [v_int_ty]; [v_int_ty]; [v_int_ty]; [varaTy]], v_unit_ty))

  let v_option_toNullable_info     = makeIntrinsicValRef(fslib_MFOptionModule_nleref,                          "toNullable"                           , None                 , Some "ToNullable" , [vara],     ([[mkOptionTy varaTy]], mkNullableTy varaTy))
  let v_option_defaultValue_info   = makeIntrinsicValRef(fslib_MFOptionModule_nleref,                          "defaultValue"                         , None                 , Some "DefaultValue" , [vara],     ([[varaTy]; [mkOptionTy varaTy]], varaTy))

  let v_nativeptr_tobyref_info     = makeIntrinsicValRef(fslib_MFNativePtrModule_nleref,                       "toByRef"                              , None                 , Some "ToByRefInlined", [vara], ([[mkNativePtrTy varaTy]], mkByrefTy varaTy))

  let v_seq_collect_info           = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "collect"                              , None                 , Some "Collect", [vara;varb;varc], ([[varaTy --> varbTy]; [mkSeqTy varaTy]], mkSeqTy varcTy))
  let v_seq_delay_info             = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "delay"                                , None                 , Some "Delay"  , [varb],     ([[v_unit_ty --> mkSeqTy varbTy]], mkSeqTy varbTy))
  let v_seq_append_info            = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "append"                               , None                 , Some "Append" , [varb],     ([[mkSeqTy varbTy]; [mkSeqTy varbTy]], mkSeqTy varbTy))
  let v_seq_using_info             = makeIntrinsicValRef(fslib_MFRuntimeHelpers_nleref,                        "EnumerateUsing"                       , None                 , None          , [vara;varb;varc], ([[varaTy];[(varaTy --> varbTy)]], mkSeqTy varcTy))
  let v_seq_generated_info         = makeIntrinsicValRef(fslib_MFRuntimeHelpers_nleref,                        "EnumerateWhile"                       , None                 , None          , [varb],     ([[v_unit_ty --> v_bool_ty]; [mkSeqTy varbTy]], mkSeqTy varbTy))
  let v_seq_finally_info           = makeIntrinsicValRef(fslib_MFRuntimeHelpers_nleref,                        "EnumerateThenFinally"                 , None                 , None          , [varb],     ([[mkSeqTy varbTy]; [v_unit_ty --> v_unit_ty]], mkSeqTy varbTy))
  let v_seq_of_functions_info      = makeIntrinsicValRef(fslib_MFRuntimeHelpers_nleref,                        "EnumerateFromFunctions"               , None                 , None          , [vara;varb], ([[v_unit_ty --> varaTy]; [varaTy --> v_bool_ty]; [varaTy --> varbTy]], mkSeqTy varbTy))
  let v_create_event_info          = makeIntrinsicValRef(fslib_MFRuntimeHelpers_nleref,                        "CreateEvent"                          , None                 , None          , [vara;varb], ([[varaTy --> v_unit_ty]; [varaTy --> v_unit_ty]; [(v_obj_ty --> (varbTy --> v_unit_ty)) --> varaTy]], TType_app (v_fslib_IEvent2_tcr, [varaTy;varbTy])))
  let v_seq_to_array_info          = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "toArray"                              , None                 , Some "ToArray", [varb],     ([[mkSeqTy varbTy]], mkArrayType 1 varbTy))
  let v_seq_to_list_info           = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "toList"                               , None                 , Some "ToList" , [varb],     ([[mkSeqTy varbTy]], mkListTy varbTy))
  let v_seq_map_info               = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "map"                                  , None                 , Some "Map"    , [vara;varb], ([[varaTy --> varbTy]; [mkSeqTy varaTy]], mkSeqTy varbTy))
  let v_seq_singleton_info         = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "singleton"                            , None                 , Some "Singleton"              , [vara],     ([[varaTy]], mkSeqTy varaTy))
  let v_seq_empty_info             = makeIntrinsicValRef(fslib_MFSeqModule_nleref,                             "empty"                                , None                 , Some "Empty"                  , [vara],     ([], mkSeqTy varaTy))
  let v_new_format_info            = makeIntrinsicValRef(fslib_MFCore_nleref,                                  ".ctor"                                , Some "PrintfFormat`5", None                          , [vara;varb;varc;vard;vare], ([[v_string_ty]], mkPrintfFormatTy varaTy varbTy varcTy vardTy vareTy))
  let v_sprintf_info               = makeIntrinsicValRef(fslib_MFExtraTopLevelOperators_nleref,                "sprintf"                              , None                 , Some "PrintFormatToStringThen", [vara],     ([[mk_format4_ty varaTy v_unit_ty v_string_ty v_string_ty]], varaTy))
  let v_lazy_force_info            = makeIntrinsicValRef(fslib_MFLazyExtensions_nleref,                        "Force"                                , Some "Lazy`1"        , None                          , [vara],     ([[mkLazyTy varaTy]; []], varaTy))
  let v_lazy_create_info           = makeIntrinsicValRef(fslib_MFLazyExtensions_nleref,                        "Create"                               , Some "Lazy`1"        , None                          , [vara],     ([[v_unit_ty --> varaTy]], mkLazyTy varaTy))

  let v_seq_info                   = makeIntrinsicValRef(fslib_MFOperators_nleref,                             "seq"                                  , None                 , Some "CreateSequence"         , [vara],     ([[mkSeqTy varaTy]], mkSeqTy varaTy))
  let v_refcell_info               = makeIntrinsicValRef(fslib_MFCore_nleref,                                  "ref"                                  , Some "FSharpRef`1"   , None                          , [vara],     ([[mkRefCellTy varaTy]; []], varaTy))
  let v_splice_expr_info           = makeIntrinsicValRef(fslib_MFExtraTopLevelOperators_nleref,                "op_Splice"                            , None                 , None                          , [vara],     ([[mkQuotedExprTy varaTy]], varaTy))
  let v_splice_raw_expr_info       = makeIntrinsicValRef(fslib_MFExtraTopLevelOperators_nleref,                "op_SpliceUntyped"                     , None                 , None                          , [vara],     ([[mkRawQuotedExprTy]], varaTy))
  let v_new_decimal_info           = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "MakeDecimal"                          , None                 , None                          , [],         ([[v_int_ty]; [v_int_ty]; [v_int_ty]; [v_bool_ty]; [v_byte_ty]], v_decimal_ty))
  let v_deserialize_quoted_FSharp_20_plus_info    = makeIntrinsicValRef(fslib_MFQuotations_nleref,             "Deserialize"                          , Some "Expr"          , None                          , [],          ([[v_system_Type_ty ;mkListTy v_system_Type_ty ;mkListTy mkRawQuotedExprTy ; mkArrayType 1 v_byte_ty]], mkRawQuotedExprTy ))
  let v_deserialize_quoted_FSharp_40_plus_info    = makeIntrinsicValRef(fslib_MFQuotations_nleref,             "Deserialize40"                        , Some "Expr"          , None                          , [],          ([[v_system_Type_ty ;mkArrayType 1 v_system_Type_ty; mkArrayType 1 v_system_Type_ty; mkArrayType 1 mkRawQuotedExprTy; mkArrayType 1 v_byte_ty]], mkRawQuotedExprTy ))
  let v_call_with_witnesses_info   = makeIntrinsicValRef(fslib_MFQuotations_nleref,                            "CallWithWitnesses"                    , Some "Expr"          , None                          , [],         ([[v_system_Reflection_MethodInfo_ty; v_system_Reflection_MethodInfo_ty; mkListTy mkRawQuotedExprTy; mkListTy mkRawQuotedExprTy]], mkRawQuotedExprTy))
  let v_cast_quotation_info        = makeIntrinsicValRef(fslib_MFQuotations_nleref,                            "Cast"                                 , Some "Expr"          , None                          , [vara],      ([[mkRawQuotedExprTy]], mkQuotedExprTy varaTy))
  let v_lift_value_info            = makeIntrinsicValRef(fslib_MFQuotations_nleref,                            "Value"                                , Some "Expr"          , None                          , [vara],      ([[varaTy]], mkRawQuotedExprTy))
  let v_lift_value_with_name_info  = makeIntrinsicValRef(fslib_MFQuotations_nleref,                            "ValueWithName"                        , Some "Expr"          , None                          , [vara],      ([[varaTy; v_string_ty]], mkRawQuotedExprTy))
  let v_lift_value_with_defn_info  = makeIntrinsicValRef(fslib_MFQuotations_nleref,                            "WithValue"                            , Some "Expr"          , None                          , [vara],      ([[varaTy; mkQuotedExprTy varaTy]], mkQuotedExprTy varaTy))
  let v_query_value_info           = makeIntrinsicValRef(fslib_MFExtraTopLevelOperators_nleref,                "query"                                , None                 , None                          , [],      ([], mkQueryBuilderTy) )
  let v_query_run_value_info       = makeIntrinsicValRef(fslib_MFQueryRunExtensionsLowPriority_nleref,         "Run"                                  , Some "QueryBuilder"  , None                          , [vara],      ([[mkQueryBuilderTy];[mkQuotedExprTy varaTy]], varaTy) )
  let v_query_run_enumerable_info  = makeIntrinsicValRef(fslib_MFQueryRunExtensionsHighPriority_nleref,        "Run"                                  , Some "QueryBuilder"  , None                          , [vara],      ([[mkQueryBuilderTy];[mkQuotedExprTy (mkQuerySourceTy varaTy (mkNonGenericTy v_tcref_System_Collections_IEnumerable)) ]], mkSeqTy varaTy) )
  let v_query_for_value_info       = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "For"                                  , Some "QueryBuilder"  , None                          , [vara; vard; varb; vare], ([[mkQueryBuilderTy];[mkQuerySourceTy varaTy vardTy;varaTy --> mkQuerySourceTy varbTy vareTy]], mkQuerySourceTy varbTy vardTy) )
  let v_query_select_value_info    = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "Select"                               , Some "QueryBuilder"  , None                          , [vara; vare; varb], ([[mkQueryBuilderTy];[mkQuerySourceTy varaTy vareTy;varaTy --> varbTy]], mkQuerySourceTy varbTy vareTy) )
  let v_query_yield_value_info     = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "Yield"                                , Some "QueryBuilder"  , None                          , [vara; vare],      ([[mkQueryBuilderTy];[varaTy]], mkQuerySourceTy varaTy vareTy) )
  let v_query_yield_from_value_info = makeIntrinsicValRef(fslib_MFLinq_nleref,                                 "YieldFrom"                            , Some "QueryBuilder"  , None                          , [vara; vare],      ([[mkQueryBuilderTy];[mkQuerySourceTy varaTy vareTy]], mkQuerySourceTy varaTy vareTy) )
  let v_query_source_info          = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "Source"                               , Some "QueryBuilder"  , None                          , [vara],      ([[mkQueryBuilderTy];[mkSeqTy varaTy ]], mkQuerySourceTy varaTy (mkNonGenericTy v_tcref_System_Collections_IEnumerable)) )
  let v_query_source_as_enum_info  = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "get_Source"                           , Some "QuerySource`2" , None                          , [vara; vare],      ([[mkQuerySourceTy varaTy vareTy];[]], mkSeqTy varaTy) )
  let v_new_query_source_info     = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  ".ctor"                                 , Some "QuerySource`2" , None                          , [vara; vare],      ([[mkSeqTy varaTy]], mkQuerySourceTy varaTy vareTy) )
  let v_query_where_value_info     = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "Where"                                , Some "QueryBuilder"  , None                          , [vara; vare],      ([[mkQueryBuilderTy];[mkQuerySourceTy varaTy vareTy;varaTy --> v_bool_ty]], mkQuerySourceTy varaTy vareTy) )
  let v_query_zero_value_info      = makeIntrinsicValRef(fslib_MFLinq_nleref,                                  "Zero"                                 , Some "QueryBuilder"  , None                          , [vara; vare],      ([[mkQueryBuilderTy];[]], mkQuerySourceTy varaTy vareTy) )
  let v_fail_init_info             = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "FailInit"                             , None                 , None                          , [],      ([[v_unit_ty]], v_unit_ty))
  let v_fail_static_init_info      = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "FailStaticInit"                       , None                 , None                          , [],      ([[v_unit_ty]], v_unit_ty))
  let v_check_this_info            = makeIntrinsicValRef(fslib_MFIntrinsicFunctions_nleref,                    "CheckThis"                            , None                 , None                          , [vara],      ([[varaTy]], varaTy))
  let v_quote_to_linq_lambda_info  = makeIntrinsicValRef(fslib_MFLinqRuntimeHelpersQuotationConverter_nleref,  "QuotationToLambdaExpression"          , None                 , None                          , [vara],      ([[mkQuotedExprTy varaTy]], mkLinqExpressionTy varaTy))

  let tref_DebuggableAttribute = findSysILTypeRef tname_DebuggableAttribute
  let tref_CompilerGeneratedAttribute  = findSysILTypeRef tname_CompilerGeneratedAttribute

  let mutable generatedAttribsCache = []
  let mutable debuggerBrowsableNeverAttributeCache = None
  let mkDebuggerNonUserCodeAttribute() = mkILCustomAttribute (findSysILTypeRef tname_DebuggerNonUserCodeAttribute, [], [], [])
  let mkCompilerGeneratedAttribute () = mkILCustomAttribute (tref_CompilerGeneratedAttribute, [], [], [])
  let compilerGlobalState = CompilerGlobalState()

  // Requests attributes to be added to compiler generated methods.
  let addGeneratedAttrs (attrs: ILAttributes) =
    let attribs =
       match generatedAttribsCache with
       | [] ->
           let res = [ if not noDebugData then
                        yield mkCompilerGeneratedAttribute()
                        yield mkDebuggerNonUserCodeAttribute()]
           generatedAttribsCache <- res
           res
       | res -> res
    mkILCustomAttrs (attrs.AsList @ attribs)

  let addMethodGeneratedAttrs (mdef:ILMethodDef)   = mdef.With(customAttrs   = addGeneratedAttrs mdef.CustomAttrs)
  let addPropertyGeneratedAttrs (pdef:ILPropertyDef) = pdef.With(customAttrs = addGeneratedAttrs pdef.CustomAttrs)
  let addFieldGeneratedAttrs (fdef:ILFieldDef) = fdef.With(customAttrs = addGeneratedAttrs fdef.CustomAttrs)

  let tref_DebuggerBrowsableAttribute n =
        let typ_DebuggerBrowsableState =
            let tref = findSysILTypeRef tname_DebuggerBrowsableState
            ILType.Value (mkILNonGenericTySpec tref)
        mkILCustomAttribute (findSysILTypeRef tname_DebuggerBrowsableAttribute, [typ_DebuggerBrowsableState], [ILAttribElem.Int32 n], [])

  let mkDebuggerBrowsableNeverAttribute() =
      match debuggerBrowsableNeverAttributeCache with
      | None ->
          let res = tref_DebuggerBrowsableAttribute 0
          debuggerBrowsableNeverAttributeCache <- Some res
          res
      | Some res -> res

  let addNeverAttrs (attrs: ILAttributes) = mkILCustomAttrs (attrs.AsList @ [mkDebuggerBrowsableNeverAttribute()])
  let addPropertyNeverAttrs (pdef:ILPropertyDef) = pdef.With(customAttrs = addNeverAttrs pdef.CustomAttrs)
  let addFieldNeverAttrs (fdef:ILFieldDef) = fdef.With(customAttrs = addNeverAttrs fdef.CustomAttrs)
  let mkDebuggerTypeProxyAttribute (ty : ILType) = mkILCustomAttribute (findSysILTypeRef tname_DebuggerTypeProxyAttribute,  [ilg.typ_Type], [ILAttribElem.TypeRef (Some ty.TypeRef)], [])

  let betterTyconEntries =
     [| "Int32"    , v_int_tcr
        "IntPtr"   , v_nativeint_tcr
        "UIntPtr"  , v_unativeint_tcr
        "Int16"    , v_int16_tcr
        "Int64"    , v_int64_tcr
        "UInt16"   , v_uint16_tcr
        "UInt32"   , v_uint32_tcr
        "UInt64"   , v_uint64_tcr
        "SByte"    , v_sbyte_tcr
        "Decimal"  , v_decimal_tcr
        "Byte"     , v_byte_tcr
        "Boolean"  , v_bool_tcr
        "String"   , v_string_tcr
        "Object"   , v_obj_tcr
        "Exception", v_exn_tcr
        "Char"     , v_char_tcr
        "Double"   , v_float_tcr
        "Single"   , v_float32_tcr |]
            |> Array.map (fun (nm, tcr) ->
                let ty = mkNonGenericTy tcr
                nm, findSysTyconRef sys nm, (fun _ -> ty))

  let decompileTyconEntries =
        [|
            "FSharpFunc`2" ,       v_fastFunc_tcr      , (fun tinst -> mkFunTy (List.item 0 tinst) (List.item 1 tinst))
            "Tuple`2"      ,       v_ref_tuple2_tcr    , decodeTupleTy tupInfoRef
            "Tuple`3"      ,       v_ref_tuple3_tcr    , decodeTupleTy tupInfoRef
            "Tuple`4"      ,       v_ref_tuple4_tcr    , decodeTupleTy tupInfoRef
            "Tuple`5"      ,       v_ref_tuple5_tcr    , decodeTupleTy tupInfoRef
            "Tuple`6"      ,       v_ref_tuple6_tcr    , decodeTupleTy tupInfoRef
            "Tuple`7"      ,       v_ref_tuple7_tcr    , decodeTupleTy tupInfoRef
            "Tuple`8"      ,       v_ref_tuple8_tcr    , decodeTupleTyIfPossible v_ref_tuple8_tcr tupInfoRef
            "ValueTuple`2" ,       v_struct_tuple2_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`3" ,       v_struct_tuple3_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`4" ,       v_struct_tuple4_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`5" ,       v_struct_tuple5_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`6" ,       v_struct_tuple6_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`7" ,       v_struct_tuple7_tcr , decodeTupleTy tupInfoStruct
            "ValueTuple`8" ,       v_struct_tuple8_tcr , decodeTupleTyIfPossible v_struct_tuple8_tcr tupInfoStruct |]

  let betterEntries = Array.append betterTyconEntries decompileTyconEntries

  let mutable decompileTypeDict = null
  let mutable betterTypeDict1 = null
  let mutable betterTypeDict2 = null

  /// This map is indexed by stamps and lazy to avoid dereferencing while setting up the base imports.
  let getDecompileTypeDict () =
      match decompileTypeDict with
      | null ->
          let entries = decompileTyconEntries
          let t = Dictionary.newWithSize entries.Length
          for _, tcref, builder in entries do
              if tcref.CanDeref then
                  t.Add(tcref.Stamp, builder)
          decompileTypeDict <- t
          t
      | t -> t

  /// This map is for use when building FSharp.Core.dll. The backing Tycon's may not yet exist for
  /// the TyconRef's we have in our hands, hence we can't dereference them to find their stamps.
  /// So this dictionary is indexed by names. Make it lazy to avoid dereferencing while setting up the base imports.
  let getBetterTypeDict1 () =
      match betterTypeDict1 with
      | null ->
          let entries = betterEntries
          let t = Dictionary.newWithSize entries.Length
          for nm, tcref, builder in entries do
              t.Add(nm, fun tcref2 tinst2 -> if tyconRefEq tcref tcref2 then builder tinst2 else TType_app (tcref2, tinst2))
          betterTypeDict1 <- t
          t
      | t -> t

  /// This map is for use in normal times (not building FSharp.Core.dll). It is indexed by stamps
  /// and lazy to avoid dereferencing while setting up the base imports.
  let getBetterTypeDict2 () =
      match betterTypeDict2 with
      | null ->
          let entries = betterEntries
          let t = Dictionary.newWithSize entries.Length
          for _, tcref, builder in entries do
              if tcref.CanDeref then
                  t.Add(tcref.Stamp, builder)
          betterTypeDict2 <- t
          t
      | t -> t

  /// For logical purposes equate some F# types with .NET types, e.g. TType_tuple == System.Tuple/ValueTuple.
  /// Doing this normalization is a fairly performance critical piece of code as it is frequently invoked
  /// in the process of converting .NET metadata to F# internal compiler data structures (see import.fs).
  let decompileTy (tcref: EntityRef) tinst =
      if compilingFslib then
          // No need to decompile when compiling FSharp.Core.dll
          TType_app (tcref, tinst)
      else
          let dict = getDecompileTypeDict()
          match dict.TryGetValue tcref.Stamp with
          | true, builder -> builder tinst
          | _ -> TType_app (tcref, tinst)

  /// For cosmetic purposes "improve" some .NET types, e.g. Int32 --> int32.
  /// Doing this normalization is a fairly performance critical piece of code as it is frequently invoked
  /// in the process of converting .NET metadata to F# internal compiler data structures (see import.fs).
  let improveTy (tcref: EntityRef) tinst =
        if compilingFslib then
            let dict = getBetterTypeDict1()
            match dict.TryGetValue tcref.LogicalName with
            | true, builder -> builder tcref tinst
            | _ -> TType_app (tcref, tinst)
        else
            let dict = getBetterTypeDict2()
            match dict.TryGetValue tcref.Stamp with
            | true, builder -> builder tinst
            | _ -> TType_app (tcref, tinst)


  override x.ToString() = "<TcGlobals>"
  member _.ilg=ilg
      // A table of all intrinsics that the compiler cares about
  member _.knownIntrinsics                = v_knownIntrinsics
      // A table of known modules in FSharp.Core. Not all modules are necessarily listed, but the more we list the
      // better the job we do of mapping from provided expressions back to FSharp.Core F# functions and values.
  member _.knownFSharpCoreModules         = v_knownFSharpCoreModules
  member _.compilingFslib                 = compilingFslib
  member _.mlCompatibility                = mlCompatibility
  member _.emitDebugInfoInQuotations      = emitDebugInfoInQuotations
  member _.directoryToResolveRelativePaths= directoryToResolveRelativePaths
  member _.pathMap = pathMap
  member _.langVersion = langVersion
  member _.unionCaseRefEq x y = primUnionCaseRefEq compilingFslib fslibCcu x y
  member _.valRefEq x y = primValRefEq compilingFslib fslibCcu x y
  member _.fslibCcu                 = fslibCcu
  member val refcell_tcr_canon    = v_refcell_tcr_canon
  member val option_tcr_canon     = mk_MFCore_tcref     fslibCcu "Option`1"
  member val valueoption_tcr_canon    = mk_MFCore_tcref     fslibCcu "ValueOption`1"
  member _.list_tcr_canon       = v_list_tcr_canon
  member val set_tcr_canon        = mk_MFCollections_tcref   fslibCcu "Set`1"
  member val map_tcr_canon        = mk_MFCollections_tcref   fslibCcu "Map`2"
  member _.lazy_tcr_canon       = lazy_tcr
  member val refcell_tcr_nice     = v_refcell_tcr_nice
  member val array_tcr_nice       = v_il_arr_tcr_map.[0]
  member _.option_tcr_nice   = v_option_tcr_nice
  member _.valueoption_tcr_nice  = v_valueoption_tcr_nice
  member _.list_tcr_nice     = v_list_tcr_nice
  member _.lazy_tcr_nice     = v_lazy_tcr_nice
  member _.format_tcr       = v_format_tcr
  member _.expr_tcr       = v_expr_tcr
  member _.raw_expr_tcr       = v_raw_expr_tcr
  member _.nativeint_tcr  = v_nativeint_tcr
  member _.unativeint_tcr = v_unativeint_tcr
  member _.int_tcr        = v_int_tcr
  member _.int32_tcr      = v_int32_tcr
  member _.int16_tcr      = v_int16_tcr
  member _.int64_tcr      = v_int64_tcr
  member _.uint16_tcr     = v_uint16_tcr
  member _.uint32_tcr     = v_uint32_tcr
  member _.uint64_tcr     = v_uint64_tcr
  member _.sbyte_tcr      = v_sbyte_tcr
  member _.decimal_tcr    = v_decimal_tcr
  member _.date_tcr    = v_date_tcr
  member _.pdecimal_tcr   = v_pdecimal_tcr
  member _.byte_tcr       = v_byte_tcr
  member _.bool_tcr       = v_bool_tcr
  member _.unit_tcr_canon = v_unit_tcr_canon
  member _.unit_tcr_nice  = v_unit_tcr_nice
  member _.exn_tcr        = v_exn_tcr
  member _.char_tcr       = v_char_tcr
  member _.float_tcr      = v_float_tcr
  member _.float32_tcr    = v_float32_tcr
  member _.pfloat_tcr      = v_pfloat_tcr
  member _.pfloat32_tcr    = v_pfloat32_tcr
  member _.pint_tcr        = v_pint_tcr
  member _.pint8_tcr       = v_pint8_tcr
  member _.pint16_tcr      = v_pint16_tcr
  member _.pint64_tcr      = v_pint64_tcr
  member _.pnativeint_tcr  = v_pnativeint_tcr
  member _.puint_tcr       = v_puint_tcr
  member _.puint8_tcr      = v_puint8_tcr
  member _.puint16_tcr     = v_puint16_tcr
  member _.puint64_tcr     = v_puint64_tcr
  member _.punativeint_tcr = v_punativeint_tcr
  member _.byref_tcr      = v_byref_tcr
  member _.byref2_tcr     = v_byref2_tcr
  member _.outref_tcr     = v_outref_tcr
  member _.inref_tcr      = v_inref_tcr
  member _.nativeptr_tcr  = v_nativeptr_tcr
  member _.voidptr_tcr    = v_voidptr_tcr
  member _.ilsigptr_tcr   = v_ilsigptr_tcr
  member _.fastFunc_tcr = v_fastFunc_tcr
  member _.tcref_IQueryable = v_tcref_IQueryable
  member _.tcref_IObservable      = v_tcref_IObservable
  member _.tcref_IObserver      = v_tcref_IObserver
  member _.fslib_IEvent2_tcr      = v_fslib_IEvent2_tcr
  member _.fslib_IDelegateEvent_tcr      = v_fslib_IDelegateEvent_tcr
  member _.seq_tcr        = v_seq_tcr
  member val seq_base_tcr = mk_MFCompilerServices_tcref fslibCcu "GeneratedSequenceBase`1"
  member val ListCollector_tcr = mk_MFCompilerServices_tcref fslibCcu "ListCollector`1"
  member val ArrayCollector_tcr = mk_MFCompilerServices_tcref fslibCcu "ArrayCollector`1"
  member g.mk_GeneratedSequenceBase_ty seqElemTy = TType_app(g.seq_base_tcr,[seqElemTy])
  member g.mk_ListCollector_ty seqElemTy = TType_app(g.ListCollector_tcr,[seqElemTy])
  member g.mk_ArrayCollector_ty seqElemTy = TType_app(g.ArrayCollector_tcr,[seqElemTy])
  member val byrefkind_In_tcr =  mkNonLocalTyconRef fslib_MFByRefKinds_nleref "In"
  member val byrefkind_Out_tcr =  mkNonLocalTyconRef fslib_MFByRefKinds_nleref "Out"
  member val byrefkind_InOut_tcr =  mkNonLocalTyconRef fslib_MFByRefKinds_nleref "InOut"
  member val measureproduct_tcr = mk_MFCompilerServices_tcref fslibCcu "MeasureProduct`2"
  member val measureinverse_tcr = mk_MFCompilerServices_tcref fslibCcu "MeasureInverse`1"
  member val measureone_tcr = mk_MFCompilerServices_tcref fslibCcu "MeasureOne"
  member _.il_arr_tcr_map = v_il_arr_tcr_map
  member _.ref_tuple1_tcr     = v_ref_tuple1_tcr
  member _.ref_tuple2_tcr     = v_ref_tuple2_tcr
  member _.ref_tuple3_tcr     = v_ref_tuple3_tcr
  member _.ref_tuple4_tcr     = v_ref_tuple4_tcr
  member _.ref_tuple5_tcr     = v_ref_tuple5_tcr
  member _.ref_tuple6_tcr     = v_ref_tuple6_tcr
  member _.ref_tuple7_tcr     = v_ref_tuple7_tcr
  member _.ref_tuple8_tcr     = v_ref_tuple8_tcr
  member _.struct_tuple1_tcr     = v_struct_tuple1_tcr
  member _.struct_tuple2_tcr     = v_struct_tuple2_tcr
  member _.struct_tuple3_tcr     = v_struct_tuple3_tcr
  member _.struct_tuple4_tcr     = v_struct_tuple4_tcr
  member _.struct_tuple5_tcr     = v_struct_tuple5_tcr
  member _.struct_tuple6_tcr     = v_struct_tuple6_tcr
  member _.struct_tuple7_tcr     = v_struct_tuple7_tcr
  member _.struct_tuple8_tcr     = v_struct_tuple8_tcr
  member _.choice2_tcr    = v_choice2_tcr
  member _.choice3_tcr    = v_choice3_tcr
  member _.choice4_tcr    = v_choice4_tcr
  member _.choice5_tcr    = v_choice5_tcr
  member _.choice6_tcr    = v_choice6_tcr
  member _.choice7_tcr    = v_choice7_tcr
  member val nativeint_ty  = v_nativeint_ty
  member val unativeint_ty = v_unativeint_ty
  member val int32_ty      = v_int32_ty
  member val int16_ty      = v_int16_ty
  member val int64_ty      = v_int64_ty
  member val uint16_ty     = v_uint16_ty
  member val uint32_ty     = v_uint32_ty
  member val uint64_ty     = v_uint64_ty
  member val sbyte_ty      = v_sbyte_ty
  member _.byte_ty       = v_byte_ty
  member _.bool_ty       = v_bool_ty
  member _.int_ty       = v_int_ty
  member _.string_ty     = v_string_ty
  member _.system_IFormattable_tcref = v_IFormattable_tcref
  member _.system_FormattableString_tcref = v_FormattableString_tcref
  member _.system_FormattableStringFactory_tcref = v_FormattableStringFactory_tcref
  member _.system_IFormattable_ty = v_IFormattable_ty
  member _.system_FormattableString_ty = v_FormattableString_ty
  member _.system_FormattableStringFactory_ty = v_FormattableStringFactory_ty
  member _.unit_ty       = v_unit_ty
  member _.obj_ty        = v_obj_ty
  member _.char_ty       = v_char_ty
  member _.decimal_ty    = v_decimal_ty

  member val exn_ty        = mkNonGenericTy v_exn_tcr
  member val float_ty      = v_float_ty
  member val float32_ty    = v_float32_ty
      /// Memoization table to help minimize the number of ILSourceDocument objects we create
  member _.memoize_file x = v_memoize_file.Apply x

  member val system_Array_ty     = mkSysNonGenericTy sys "Array"
  member val system_Object_ty    = mkSysNonGenericTy sys "Object"
  member val system_IDisposable_ty    = mkSysNonGenericTy sys "IDisposable"
  member val system_RuntimeHelpers_ty    = mkSysNonGenericTy sysCompilerServices "RuntimeHelpers"
  member val system_Value_ty     = mkSysNonGenericTy sys "ValueType"
  member val system_Delegate_ty     = mkSysNonGenericTy sys "Delegate"
  member val system_MulticastDelegate_ty     = mkSysNonGenericTy sys "MulticastDelegate"
  member val system_Enum_ty      = mkSysNonGenericTy sys "Enum"
  member val system_Exception_ty = mkSysNonGenericTy sys "Exception"
  member val system_String_typ    = mkSysNonGenericTy sys "String"
  member val system_String_tcref  = findSysTyconRef sys "String"
  member val system_Int32_ty     = mkSysNonGenericTy sys "Int32"
  member _.system_Type_ty                  = v_system_Type_ty
  member val system_TypedReference_tcref        = tryFindSysTyconRef sys "TypedReference"
  member val system_ArgIterator_tcref           = tryFindSysTyconRef sys "ArgIterator"
  member val system_RuntimeArgumentHandle_tcref =  tryFindSysTyconRef sys "RuntimeArgumentHandle"
  member val system_SByte_tcref =  findSysTyconRef sys "SByte"
  member val system_Decimal_tcref =  findSysTyconRef sys "Decimal"
  member val system_Int16_tcref =  findSysTyconRef sys "Int16"
  member val system_Int32_tcref =  findSysTyconRef sys "Int32"
  member val system_Int64_tcref =  findSysTyconRef sys "Int64"
  member val system_IntPtr_tcref =  findSysTyconRef sys "IntPtr"
  member val system_Bool_tcref =  findSysTyconRef sys "Boolean"
  member val system_Byte_tcref =  findSysTyconRef sys "Byte"
  member val system_UInt16_tcref =  findSysTyconRef sys "UInt16"
  member val system_Char_tcref            =  findSysTyconRef sys "Char"
  member val system_UInt32_tcref          =  findSysTyconRef sys "UInt32"
  member val system_UInt64_tcref          =  findSysTyconRef sys "UInt64"
  member val system_UIntPtr_tcref         =  findSysTyconRef sys "UIntPtr"
  member val system_Single_tcref          =  findSysTyconRef sys "Single"
  member val system_Double_tcref          =  findSysTyconRef sys "Double"
  member val system_RuntimeTypeHandle_ty = mkSysNonGenericTy sys "RuntimeTypeHandle"
  member _.system_RuntimeMethodHandle_ty = v_system_RuntimeMethodHandle_ty

  member val system_MarshalByRefObject_tcref =  tryFindSysTyconRef sys "MarshalByRefObject"
  member val system_MarshalByRefObject_ty = tryMkSysNonGenericTy sys "MarshalByRefObject"

  member val system_ExceptionDispatchInfo_ty =
      tryMkSysNonGenericTy ["System"; "Runtime"; "ExceptionServices"] "ExceptionDispatchInfo"

  member _.system_Reflection_MethodInfo_ty = v_system_Reflection_MethodInfo_ty

  member val system_Array_tcref = v_Array_tcref
  member val system_Object_tcref  = findSysTyconRef sys "Object"
  member val system_Value_tcref = findSysTyconRef sys "ValueType"
  member val system_Void_tcref    = findSysTyconRef sys "Void"
  member val system_IndexOutOfRangeException_tcref    = findSysTyconRef sys "IndexOutOfRangeException"
  member val system_Nullable_tcref = v_nullable_tcr
  member val system_GenericIComparable_tcref = findSysTyconRef sys "IComparable`1"
  member val system_GenericIEquatable_tcref = findSysTyconRef sys "IEquatable`1"
  member val mk_IComparable_ty    = mkSysNonGenericTy sys "IComparable"
  member val system_LinqExpression_tcref = v_linqExpression_tcr

  member val mk_IStructuralComparable_ty = mkSysNonGenericTy sysCollections "IStructuralComparable"

  member val mk_IStructuralEquatable_ty = mkSysNonGenericTy sysCollections "IStructuralEquatable"

  member _.IComparer_ty = v_IComparer_ty
  member _.IEqualityComparer_ty = v_IEqualityComparer_ty
  member val tcref_System_Collections_IComparer = findSysTyconRef sysCollections "IComparer"
  member val tcref_System_Collections_IEqualityComparer = findSysTyconRef sysCollections "IEqualityComparer"
  member val tcref_System_Collections_Generic_IEqualityComparer = findSysTyconRef sysGenerics "IEqualityComparer`1"
  member val tcref_System_Collections_Generic_Dictionary = findSysTyconRef sysGenerics "Dictionary`2"
  member val tcref_System_Collections_Generic_IDictionary = findSysTyconRef sysGenerics "IDictionary`2"

  member val tcref_System_IComparable = findSysTyconRef sys "IComparable"
  member val tcref_System_IStructuralComparable = findSysTyconRef sysCollections "IStructuralComparable"
  member val tcref_System_IStructuralEquatable  = findSysTyconRef sysCollections "IStructuralEquatable"
  member val tcref_System_IDisposable = findSysTyconRef sys "IDisposable"

  member val tcref_LanguagePrimitives = mk_MFCore_tcref fslibCcu "LanguagePrimitives"

  member val tcref_System_Collections_Generic_List       = findSysTyconRef sysGenerics "List`1"
  member val tcref_System_Collections_Generic_IList       = findSysTyconRef sysGenerics "IList`1"
  member val tcref_System_Collections_Generic_IReadOnlyList       = findSysTyconRef sysGenerics "IReadOnlyList`1"
  member val tcref_System_Collections_Generic_ICollection = findSysTyconRef sysGenerics "ICollection`1"
  member val tcref_System_Collections_Generic_IReadOnlyCollection = findSysTyconRef sysGenerics "IReadOnlyCollection`1"
  member _.tcref_System_Collections_IEnumerable         = v_tcref_System_Collections_IEnumerable

  member _.tcref_System_Collections_Generic_IEnumerable = v_IEnumerable_tcr
  member _.tcref_System_Collections_Generic_IEnumerator = v_IEnumerator_tcr

  member _.tcref_System_Attribute = v_System_Attribute_tcr

  // Review: Does this need to be an option type?
  member val System_Runtime_CompilerServices_RuntimeFeature_ty = tryFindSysTyconRef sysCompilerServices "RuntimeFeature" |> Option.map mkNonGenericTy

  member val iltyp_TypedReference      = tryFindSysILTypeRef "System.TypedReference" |> Option.map mkILNonGenericValueTy
  member val iltyp_StreamingContext    = tryFindSysILTypeRef tname_StreamingContext  |> Option.map mkILNonGenericValueTy
  member val iltyp_SerializationInfo   = tryFindSysILTypeRef tname_SerializationInfo  |> Option.map mkILNonGenericBoxedTy
  member val iltyp_Missing             = findSysILTypeRef tname_Missing |> mkILNonGenericBoxedTy
  member val iltyp_AsyncCallback       = findSysILTypeRef tname_AsyncCallback |> mkILNonGenericBoxedTy
  member val iltyp_IAsyncResult        = findSysILTypeRef tname_IAsyncResult |> mkILNonGenericBoxedTy
  member val iltyp_IComparable         = findSysILTypeRef tname_IComparable |> mkILNonGenericBoxedTy
  member val iltyp_Exception           = findSysILTypeRef tname_Exception |> mkILNonGenericBoxedTy
  member val iltyp_ValueType           = findSysILTypeRef tname_ValueType |> mkILNonGenericBoxedTy
  member val iltyp_RuntimeFieldHandle  = findSysILTypeRef tname_RuntimeFieldHandle |> mkILNonGenericValueTy
  member val iltyp_RuntimeMethodHandle = findSysILTypeRef tname_RuntimeMethodHandle |> mkILNonGenericValueTy
  member val iltyp_RuntimeTypeHandle   = findSysILTypeRef tname_RuntimeTypeHandle |> mkILNonGenericValueTy
  member val iltyp_ReferenceAssemblyAttributeOpt = tryFindSysILTypeRef tname_ReferenceAssemblyAttribute |> Option.map mkILNonGenericBoxedTy


  member val attrib_AttributeUsageAttribute = findSysAttrib "System.AttributeUsageAttribute"
  member val attrib_ParamArrayAttribute     = findSysAttrib "System.ParamArrayAttribute"
  member val attrib_IDispatchConstantAttribute  = tryFindSysAttrib "System.Runtime.CompilerServices.IDispatchConstantAttribute"
  member val attrib_IUnknownConstantAttribute  = tryFindSysAttrib "System.Runtime.CompilerServices.IUnknownConstantAttribute"

  // We use 'findSysAttrib' here because lookup on attribute is done by name comparison, and can proceed
  // even if the type is not found in a system assembly.
  member val attrib_IsByRefLikeAttribute  = findSysAttrib "System.Runtime.CompilerServices.IsByRefLikeAttribute"
  member val attrib_IsReadOnlyAttribute  = findSysAttrib "System.Runtime.CompilerServices.IsReadOnlyAttribute"

  member val attrib_SystemObsolete          = findSysAttrib "System.ObsoleteAttribute"
  member val attrib_DllImportAttribute      = tryFindSysAttrib "System.Runtime.InteropServices.DllImportAttribute"
  member val attrib_StructLayoutAttribute   = findSysAttrib "System.Runtime.InteropServices.StructLayoutAttribute"
  member val attrib_TypeForwardedToAttribute   = findSysAttrib "System.Runtime.CompilerServices.TypeForwardedToAttribute"
  member val attrib_ComVisibleAttribute     = findSysAttrib "System.Runtime.InteropServices.ComVisibleAttribute"
  member val attrib_ComImportAttribute      = tryFindSysAttrib "System.Runtime.InteropServices.ComImportAttribute"
  member val attrib_FieldOffsetAttribute    = findSysAttrib "System.Runtime.InteropServices.FieldOffsetAttribute"
  member val attrib_MarshalAsAttribute      = tryFindSysAttrib "System.Runtime.InteropServices.MarshalAsAttribute"
  member val attrib_InAttribute             = findSysAttrib "System.Runtime.InteropServices.InAttribute"
  member val attrib_OutAttribute            = findSysAttrib "System.Runtime.InteropServices.OutAttribute"
  member val attrib_OptionalAttribute       = tryFindSysAttrib "System.Runtime.InteropServices.OptionalAttribute"
  member val attrib_DefaultParameterValueAttribute = tryFindSysAttrib "System.Runtime.InteropServices.DefaultParameterValueAttribute"
  member val attrib_ThreadStaticAttribute   = tryFindSysAttrib "System.ThreadStaticAttribute"
  member val attrib_SpecialNameAttribute   = tryFindSysAttrib "System.Runtime.CompilerServices.SpecialNameAttribute"
  member val attrib_VolatileFieldAttribute   = mk_MFCore_attrib "VolatileFieldAttribute"
  member val attrib_ContextStaticAttribute  = tryFindSysAttrib "System.ContextStaticAttribute"
  member val attrib_FlagsAttribute          = findSysAttrib "System.FlagsAttribute"
  member val attrib_DefaultMemberAttribute  = findSysAttrib "System.Reflection.DefaultMemberAttribute"
  member val attrib_DebuggerDisplayAttribute  = findSysAttrib "System.Diagnostics.DebuggerDisplayAttribute"
  member val attrib_DebuggerTypeProxyAttribute  = findSysAttrib "System.Diagnostics.DebuggerTypeProxyAttribute"
  member val attrib_PreserveSigAttribute    = tryFindSysAttrib "System.Runtime.InteropServices.PreserveSigAttribute"
  member val attrib_MethodImplAttribute     = findSysAttrib "System.Runtime.CompilerServices.MethodImplAttribute"
  member val attrib_ExtensionAttribute     = findSysAttrib "System.Runtime.CompilerServices.ExtensionAttribute"
  member val attrib_CallerLineNumberAttribute = findSysAttrib "System.Runtime.CompilerServices.CallerLineNumberAttribute"
  member val attrib_CallerFilePathAttribute = findSysAttrib "System.Runtime.CompilerServices.CallerFilePathAttribute"
  member val attrib_CallerMemberNameAttribute = findSysAttrib "System.Runtime.CompilerServices.CallerMemberNameAttribute"
  member val attrib_ReferenceAssemblyAttribute = findSysAttrib "System.Runtime.CompilerServices.ReferenceAssemblyAttribute"
  member val attrib_SkipLocalsInitAttribute  = findSysAttrib "System.Runtime.CompilerServices.SkipLocalsInitAttribute"

  member val attrib_ProjectionParameterAttribute           = mk_MFCore_attrib "ProjectionParameterAttribute"
  member val attrib_CustomOperationAttribute               = mk_MFCore_attrib "CustomOperationAttribute"
  member val attrib_NonSerializedAttribute                 = tryFindSysAttrib "System.NonSerializedAttribute"

  member val attrib_AutoSerializableAttribute              = mk_MFCore_attrib "AutoSerializableAttribute"
  member val attrib_RequireQualifiedAccessAttribute        = mk_MFCore_attrib "RequireQualifiedAccessAttribute"
  member val attrib_EntryPointAttribute                    = mk_MFCore_attrib "EntryPointAttribute"
  member val attrib_DefaultAugmentationAttribute           = mk_MFCore_attrib "DefaultAugmentationAttribute"
  member val attrib_CompilerMessageAttribute               = mk_MFCore_attrib "CompilerMessageAttribute"
  member val attrib_ExperimentalAttribute                  = mk_MFCore_attrib "ExperimentalAttribute"
  member val attrib_UnverifiableAttribute                  = mk_MFCore_attrib "UnverifiableAttribute"
  member val attrib_LiteralAttribute                       = mk_MFCore_attrib "LiteralAttribute"
  member val attrib_ConditionalAttribute                   = findSysAttrib "System.Diagnostics.ConditionalAttribute"
  member val attrib_OptionalArgumentAttribute              = mk_MFCore_attrib "OptionalArgumentAttribute"
  member val attrib_RequiresExplicitTypeArgumentsAttribute = mk_MFCore_attrib "RequiresExplicitTypeArgumentsAttribute"
  member val attrib_DefaultValueAttribute                  = mk_MFCore_attrib "DefaultValueAttribute"
  member val attrib_ClassAttribute                         = mk_MFCore_attrib "ClassAttribute"
  member val attrib_InterfaceAttribute                     = mk_MFCore_attrib "InterfaceAttribute"
  member val attrib_StructAttribute                        = mk_MFCore_attrib "StructAttribute"
  member val attrib_ReflectedDefinitionAttribute           = mk_MFCore_attrib "ReflectedDefinitionAttribute"
  member val attrib_CompiledNameAttribute                  = mk_MFCore_attrib "CompiledNameAttribute"
  member val attrib_AutoOpenAttribute                      = mk_MFCore_attrib "AutoOpenAttribute"
  member val attrib_InternalsVisibleToAttribute            = findSysAttrib "System.Runtime.CompilerServices.InternalsVisibleToAttribute"
  member val attrib_CompilationRepresentationAttribute     = mk_MFCore_attrib "CompilationRepresentationAttribute"
  member val attrib_CompilationArgumentCountsAttribute     = mk_MFCore_attrib "CompilationArgumentCountsAttribute"
  member val attrib_CompilationMappingAttribute            = mk_MFCore_attrib "CompilationMappingAttribute"
  member val attrib_CLIEventAttribute                      = mk_MFCore_attrib "CLIEventAttribute"
  member val attrib_CLIMutableAttribute                    = mk_MFCore_attrib "CLIMutableAttribute"
  member val attrib_AllowNullLiteralAttribute              = mk_MFCore_attrib "AllowNullLiteralAttribute"
  member val attrib_NoEqualityAttribute                    = mk_MFCore_attrib "NoEqualityAttribute"
  member val attrib_NoComparisonAttribute                  = mk_MFCore_attrib "NoComparisonAttribute"
  member val attrib_CustomEqualityAttribute                = mk_MFCore_attrib "CustomEqualityAttribute"
  member val attrib_CustomComparisonAttribute              = mk_MFCore_attrib "CustomComparisonAttribute"
  member val attrib_EqualityConditionalOnAttribute         = mk_MFCore_attrib "EqualityConditionalOnAttribute"
  member val attrib_ComparisonConditionalOnAttribute       = mk_MFCore_attrib "ComparisonConditionalOnAttribute"
  member val attrib_ReferenceEqualityAttribute             = mk_MFCore_attrib "ReferenceEqualityAttribute"
  member val attrib_StructuralEqualityAttribute            = mk_MFCore_attrib "StructuralEqualityAttribute"
  member val attrib_StructuralComparisonAttribute          = mk_MFCore_attrib "StructuralComparisonAttribute"
  member val attrib_SealedAttribute                        = mk_MFCore_attrib "SealedAttribute"
  member val attrib_AbstractClassAttribute                 = mk_MFCore_attrib "AbstractClassAttribute"
  member val attrib_GeneralizableValueAttribute            = mk_MFCore_attrib "GeneralizableValueAttribute"
  member val attrib_MeasureAttribute                       = mk_MFCore_attrib "MeasureAttribute"
  member val attrib_MeasureableAttribute                   = mk_MFCore_attrib "MeasureAnnotatedAbbreviationAttribute"
  member val attrib_NoDynamicInvocationAttribute           = mk_MFCore_attrib "NoDynamicInvocationAttribute"
  member val attrib_SecurityAttribute                      = tryFindSysAttrib "System.Security.Permissions.SecurityAttribute"
  member val attrib_SecurityCriticalAttribute              = findSysAttrib "System.Security.SecurityCriticalAttribute"
  member val attrib_SecuritySafeCriticalAttribute          = findSysAttrib "System.Security.SecuritySafeCriticalAttribute"
  member val attrib_ComponentModelEditorBrowsableAttribute = findSysAttrib "System.ComponentModel.EditorBrowsableAttribute"

  member g.improveType tcref tinst = improveTy tcref tinst

  member g.decompileType tcref tinst = decompileTy tcref tinst

  member _.new_decimal_info = v_new_decimal_info
  member _.seq_info    = v_seq_info
  member val seq_vref    = (ValRefForIntrinsic v_seq_info)
  member val fsharpref_vref = (ValRefForIntrinsic v_refcell_info)
  member val and_vref    = (ValRefForIntrinsic v_and_info)
  member val and2_vref   = (ValRefForIntrinsic v_and2_info)
  member val addrof_vref = (ValRefForIntrinsic v_addrof_info)
  member val addrof2_vref = (ValRefForIntrinsic v_addrof2_info)
  member val or_vref     = (ValRefForIntrinsic v_or_info)
  member val splice_expr_vref     = (ValRefForIntrinsic v_splice_expr_info)
  member val splice_raw_expr_vref     = (ValRefForIntrinsic v_splice_raw_expr_info)
  member val or2_vref    = (ValRefForIntrinsic v_or2_info)
  member val generic_equality_er_inner_vref     = ValRefForIntrinsic v_generic_equality_er_inner_info
  member val generic_equality_per_inner_vref = ValRefForIntrinsic v_generic_equality_per_inner_info
  member val generic_equality_withc_inner_vref  = ValRefForIntrinsic v_generic_equality_withc_inner_info
  member val generic_comparison_inner_vref    = ValRefForIntrinsic v_generic_comparison_inner_info
  member val generic_comparison_withc_inner_vref    = ValRefForIntrinsic v_generic_comparison_withc_inner_info
  member _.generic_comparison_withc_outer_info    = v_generic_comparison_withc_outer_info
  member _.generic_equality_er_outer_info     = v_generic_equality_er_outer_info
  member _.generic_equality_withc_outer_info  = v_generic_equality_withc_outer_info
  member _.generic_hash_withc_outer_info = v_generic_hash_withc_outer_info
  member val generic_hash_inner_vref = ValRefForIntrinsic v_generic_hash_inner_info
  member val generic_hash_withc_inner_vref = ValRefForIntrinsic v_generic_hash_withc_inner_info

  member val reference_equality_inner_vref         = ValRefForIntrinsic v_reference_equality_inner_info

  member val bitwise_or_vref            = ValRefForIntrinsic v_bitwise_or_info
  member val bitwise_and_vref           = ValRefForIntrinsic v_bitwise_and_info
  member val bitwise_xor_vref           = ValRefForIntrinsic v_bitwise_xor_info
  member val bitwise_unary_not_vref     = ValRefForIntrinsic v_bitwise_unary_not_info
  member val bitwise_shift_left_vref    = ValRefForIntrinsic v_bitwise_shift_left_info
  member val bitwise_shift_right_vref   = ValRefForIntrinsic v_bitwise_shift_right_info
  member val unchecked_addition_vref    = ValRefForIntrinsic v_unchecked_addition_info
  member val unchecked_unary_plus_vref  = ValRefForIntrinsic v_unchecked_unary_plus_info
  member val unchecked_unary_minus_vref = ValRefForIntrinsic v_unchecked_unary_minus_info
  member val unchecked_unary_not_vref = ValRefForIntrinsic v_unchecked_unary_not_info
  member val unchecked_subtraction_vref = ValRefForIntrinsic v_unchecked_subtraction_info
  member val unchecked_multiply_vref    = ValRefForIntrinsic v_unchecked_multiply_info
  member val unchecked_defaultof_vref    = ValRefForIntrinsic v_unchecked_defaultof_info

  member _.bitwise_or_info            = v_bitwise_or_info
  member _.bitwise_and_info           = v_bitwise_and_info
  member _.bitwise_xor_info           = v_bitwise_xor_info
  member _.bitwise_unary_not_info     = v_bitwise_unary_not_info
  member _.bitwise_shift_left_info    = v_bitwise_shift_left_info
  member _.bitwise_shift_right_info   = v_bitwise_shift_right_info
  member _.unchecked_addition_info    = v_unchecked_addition_info
  member _.unchecked_subtraction_info = v_unchecked_subtraction_info
  member _.unchecked_multiply_info    = v_unchecked_multiply_info
  member _.unchecked_division_info    = v_unchecked_division_info
  member _.unchecked_modulus_info     = v_unchecked_modulus_info
  member _.unchecked_unary_plus_info  = v_unchecked_unary_plus_info
  member _.unchecked_unary_minus_info = v_unchecked_unary_minus_info
  member _.unchecked_unary_not_info   = v_unchecked_unary_not_info
  member _.unchecked_defaultof_info   = v_unchecked_defaultof_info

  member _.checked_addition_info      = v_checked_addition_info
  member _.checked_subtraction_info   = v_checked_subtraction_info
  member _.checked_multiply_info      = v_checked_multiply_info
  member _.checked_unary_minus_info   = v_checked_unary_minus_info

  member _.byte_checked_info          = v_byte_checked_info
  member _.sbyte_checked_info         = v_sbyte_checked_info
  member _.int16_checked_info         = v_int16_checked_info
  member _.uint16_checked_info        = v_uint16_checked_info
  member _.int_checked_info           = v_int_checked_info
  member _.int32_checked_info         = v_int32_checked_info
  member _.uint32_checked_info        = v_uint32_checked_info
  member _.int64_checked_info         = v_int64_checked_info
  member _.uint64_checked_info        = v_uint64_checked_info
  member _.nativeint_checked_info     = v_nativeint_checked_info
  member _.unativeint_checked_info    = v_unativeint_checked_info

  member _.byte_operator_info       = v_byte_operator_info
  member _.sbyte_operator_info      = v_sbyte_operator_info
  member _.int16_operator_info      = v_int16_operator_info
  member _.uint16_operator_info     = v_uint16_operator_info
  member _.int_operator_info        = v_int_operator_info
  member _.int32_operator_info      = v_int32_operator_info
  member _.uint32_operator_info     = v_uint32_operator_info
  member _.int64_operator_info      = v_int64_operator_info
  member _.uint64_operator_info     = v_uint64_operator_info
  member _.float32_operator_info    = v_float32_operator_info
  member _.float_operator_info      = v_float_operator_info
  member _.nativeint_operator_info  = v_nativeint_operator_info
  member _.unativeint_operator_info = v_unativeint_operator_info

  member _.char_operator_info       = v_char_operator_info
  member _.enum_operator_info       = v_enum_operator_info

  member val compare_operator_vref    = ValRefForIntrinsic v_compare_operator_info
  member val equals_operator_vref    = ValRefForIntrinsic v_equals_operator_info
  member val equals_nullable_operator_vref    = ValRefForIntrinsic v_equals_nullable_operator_info
  member val nullable_equals_nullable_operator_vref    = ValRefForIntrinsic v_nullable_equals_nullable_operator_info
  member val nullable_equals_operator_vref    = ValRefForIntrinsic v_nullable_equals_operator_info
  member val not_equals_operator_vref    = ValRefForIntrinsic v_not_equals_operator_info
  member val less_than_operator_vref    = ValRefForIntrinsic v_less_than_operator_info
  member val less_than_or_equals_operator_vref    = ValRefForIntrinsic v_less_than_or_equals_operator_info
  member val greater_than_operator_vref    = ValRefForIntrinsic v_greater_than_operator_info
  member val greater_than_or_equals_operator_vref    = ValRefForIntrinsic v_greater_than_or_equals_operator_info

  member val raise_vref                 = ValRefForIntrinsic v_raise_info
  member val failwith_vref              = ValRefForIntrinsic v_failwith_info
  member val invalid_arg_vref           = ValRefForIntrinsic v_invalid_arg_info
  member val null_arg_vref              = ValRefForIntrinsic v_null_arg_info
  member val invalid_op_vref            = ValRefForIntrinsic v_invalid_op_info
  member val failwithf_vref             = ValRefForIntrinsic v_failwithf_info

  member _.equals_operator_info        = v_equals_operator_info
  member _.not_equals_operator         = v_not_equals_operator_info
  member _.less_than_operator          = v_less_than_operator_info
  member _.less_than_or_equals_operator = v_less_than_or_equals_operator_info
  member _.greater_than_operator       = v_greater_than_operator_info
  member _.greater_than_or_equals_operator = v_greater_than_or_equals_operator_info

  member _.hash_info                  = v_hash_info
  member _.box_info                   = v_box_info
  member _.isnull_info                = v_isnull_info
  member _.isnotnull_info             = v_isnotnull_info
  member _.raise_info                 = v_raise_info
  member _.failwith_info              = v_failwith_info
  member _.invalid_arg_info           = v_invalid_arg_info
  member _.null_arg_info              = v_null_arg_info
  member _.invalid_op_info            = v_invalid_op_info
  member _.failwithf_info             = v_failwithf_info
  member _.reraise_info               = v_reraise_info
  member _.methodhandleof_info        = v_methodhandleof_info
  member _.typeof_info                = v_typeof_info
  member _.typedefof_info             = v_typedefof_info

  member val reraise_vref               = ValRefForIntrinsic v_reraise_info
  member val methodhandleof_vref        = ValRefForIntrinsic v_methodhandleof_info
  member val typeof_vref                = ValRefForIntrinsic v_typeof_info
  member val sizeof_vref                = ValRefForIntrinsic v_sizeof_info
  member val nameof_vref                = ValRefForIntrinsic v_nameof_info
  member val typedefof_vref             = ValRefForIntrinsic v_typedefof_info
  member val enum_vref                  = ValRefForIntrinsic v_enum_operator_info
  member val enumOfValue_vref           = ValRefForIntrinsic v_enumOfValue_info
  member val range_op_vref              = ValRefForIntrinsic v_range_op_info
  member val range_step_op_vref         = ValRefForIntrinsic v_range_step_op_info
  member val range_int32_op_vref        = ValRefForIntrinsic v_range_int32_op_info
  member val array_get_vref             = ValRefForIntrinsic v_array_get_info
  member val array2D_get_vref           = ValRefForIntrinsic v_array2D_get_info
  member val array3D_get_vref           = ValRefForIntrinsic v_array3D_get_info
  member val array4D_get_vref           = ValRefForIntrinsic v_array4D_get_info
  member val seq_singleton_vref         = ValRefForIntrinsic v_seq_singleton_info
  member val seq_collect_vref           = ValRefForIntrinsic v_seq_collect_info
  member val nativeptr_tobyref_vref     = ValRefForIntrinsic v_nativeptr_tobyref_info
  member val seq_using_vref             = ValRefForIntrinsic v_seq_using_info
  member val seq_delay_vref             = ValRefForIntrinsic  v_seq_delay_info
  member val seq_append_vref            = ValRefForIntrinsic  v_seq_append_info
  member val seq_generated_vref         = ValRefForIntrinsic  v_seq_generated_info
  member val seq_finally_vref           = ValRefForIntrinsic  v_seq_finally_info
  member val seq_of_functions_vref      = ValRefForIntrinsic  v_seq_of_functions_info
  member val seq_map_vref               = ValRefForIntrinsic  v_seq_map_info
  member val seq_empty_vref             = ValRefForIntrinsic  v_seq_empty_info
  member val new_format_vref            = ValRefForIntrinsic v_new_format_info
  member val sprintf_vref               = ValRefForIntrinsic v_sprintf_info
  member val unbox_vref                 = ValRefForIntrinsic v_unbox_info
  member val unbox_fast_vref            = ValRefForIntrinsic v_unbox_fast_info
  member val istype_vref                = ValRefForIntrinsic v_istype_info
  member val istype_fast_vref           = ValRefForIntrinsic v_istype_fast_info
  member val query_source_vref          = ValRefForIntrinsic v_query_source_info
  member val query_value_vref           = ValRefForIntrinsic v_query_value_info
  member val query_run_value_vref       = ValRefForIntrinsic v_query_run_value_info
  member val query_run_enumerable_vref  = ValRefForIntrinsic v_query_run_enumerable_info
  member val query_for_vref             = ValRefForIntrinsic v_query_for_value_info
  member val query_yield_vref           = ValRefForIntrinsic v_query_yield_value_info
  member val query_yield_from_vref      = ValRefForIntrinsic v_query_yield_from_value_info
  member val query_select_vref          = ValRefForIntrinsic v_query_select_value_info
  member val query_where_vref           = ValRefForIntrinsic v_query_where_value_info
  member val query_zero_vref            = ValRefForIntrinsic v_query_zero_value_info
  member val seq_to_list_vref            = ValRefForIntrinsic v_seq_to_list_info
  member val seq_to_array_vref            = ValRefForIntrinsic v_seq_to_array_info

  member _.seq_collect_info           = v_seq_collect_info
  member _.seq_using_info             = v_seq_using_info
  member _.seq_delay_info             = v_seq_delay_info
  member _.seq_append_info            = v_seq_append_info
  member _.seq_generated_info         = v_seq_generated_info
  member _.seq_finally_info           = v_seq_finally_info
  member _.seq_of_functions_info      = v_seq_of_functions_info
  member _.seq_map_info               = v_seq_map_info
  member _.seq_singleton_info         = v_seq_singleton_info
  member _.seq_empty_info             = v_seq_empty_info
  member _.sprintf_info               = v_sprintf_info
  member _.new_format_info            = v_new_format_info
  member _.unbox_info                 = v_unbox_info
  member _.get_generic_comparer_info  = v_get_generic_comparer_info
  member _.get_generic_er_equality_comparer_info = v_get_generic_er_equality_comparer_info
  member _.get_generic_per_equality_comparer_info = v_get_generic_per_equality_comparer_info
  member _.dispose_info               = v_dispose_info
  member _.getstring_info             = v_getstring_info
  member _.unbox_fast_info            = v_unbox_fast_info
  member _.istype_info                = v_istype_info
  member _.istype_fast_info           = v_istype_fast_info
  member _.lazy_force_info            = v_lazy_force_info
  member _.lazy_create_info           = v_lazy_create_info
  member _.create_instance_info       = v_create_instance_info
  member _.create_event_info          = v_create_event_info
  member _.seq_to_list_info           = v_seq_to_list_info
  member _.seq_to_array_info          = v_seq_to_array_info

  member _.array_length_info          = v_array_length_info
  member _.array_get_info             = v_array_get_info
  member _.array2D_get_info           = v_array2D_get_info
  member _.array3D_get_info           = v_array3D_get_info
  member _.array4D_get_info           = v_array4D_get_info
  member _.array_set_info             = v_array_set_info
  member _.array2D_set_info           = v_array2D_set_info
  member _.array3D_set_info           = v_array3D_set_info
  member _.array4D_set_info           = v_array4D_set_info

  member val option_toNullable_info     = v_option_toNullable_info
  member val option_defaultValue_info     = v_option_defaultValue_info

  member _.deserialize_quoted_FSharp_20_plus_info       = v_deserialize_quoted_FSharp_20_plus_info
  member _.deserialize_quoted_FSharp_40_plus_info    = v_deserialize_quoted_FSharp_40_plus_info
  member _.call_with_witnesses_info = v_call_with_witnesses_info
  member _.cast_quotation_info        = v_cast_quotation_info
  member _.lift_value_info            = v_lift_value_info
  member _.lift_value_with_name_info            = v_lift_value_with_name_info
  member _.lift_value_with_defn_info            = v_lift_value_with_defn_info
  member _.query_source_as_enum_info            = v_query_source_as_enum_info
  member _.new_query_source_info            = v_new_query_source_info
  member _.query_builder_tcref            = v_query_builder_tcref
  member _.fail_init_info             = v_fail_init_info
  member _.fail_static_init_info           = v_fail_static_init_info
  member _.check_this_info            = v_check_this_info
  member _.quote_to_linq_lambda_info        = v_quote_to_linq_lambda_info


  member val generic_hash_withc_tuple2_vref = ValRefForIntrinsic v_generic_hash_withc_tuple2_info
  member val generic_hash_withc_tuple3_vref = ValRefForIntrinsic v_generic_hash_withc_tuple3_info
  member val generic_hash_withc_tuple4_vref = ValRefForIntrinsic v_generic_hash_withc_tuple4_info
  member val generic_hash_withc_tuple5_vref = ValRefForIntrinsic v_generic_hash_withc_tuple5_info
  member val generic_equals_withc_tuple2_vref = ValRefForIntrinsic v_generic_equals_withc_tuple2_info
  member val generic_equals_withc_tuple3_vref = ValRefForIntrinsic v_generic_equals_withc_tuple3_info
  member val generic_equals_withc_tuple4_vref = ValRefForIntrinsic v_generic_equals_withc_tuple4_info
  member val generic_equals_withc_tuple5_vref = ValRefForIntrinsic v_generic_equals_withc_tuple5_info
  member val generic_compare_withc_tuple2_vref = ValRefForIntrinsic v_generic_compare_withc_tuple2_info
  member val generic_compare_withc_tuple3_vref = ValRefForIntrinsic v_generic_compare_withc_tuple3_info
  member val generic_compare_withc_tuple4_vref = ValRefForIntrinsic v_generic_compare_withc_tuple4_info
  member val generic_compare_withc_tuple5_vref = ValRefForIntrinsic v_generic_compare_withc_tuple5_info
  member val generic_equality_withc_outer_vref = ValRefForIntrinsic v_generic_equality_withc_outer_info


  member _.cons_ucref = v_cons_ucref
  member _.nil_ucref = v_nil_ucref

    // A list of types that are explicitly suppressed from the F# intellisense
    // Note that the suppression checks for the precise name of the type
    // so the lowercase versions are visible
  member _.suppressed_types = v_suppressed_types

  /// Are we assuming all code gen is for F# interactive, with no static linking
  member _.isInteractive=isInteractive

  /// Indicates if we are generating witness arguments for SRTP constraints. Only done if the FSharp.Core
  /// supports witness arguments.
  member g.generateWitnesses =
      compilingFslib ||
      ((ValRefForIntrinsic g.call_with_witnesses_info).TryDeref.IsSome && langVersion.SupportsFeature LanguageFeature.WitnessPassing)

  /// Indicates if we can use System.Array.Empty when emitting IL for empty array literals
  member val isArrayEmptyAvailable = v_Array_tcref.ILTyconRawMetadata.Methods.FindByName "Empty" |> List.isEmpty |> not

  member _.FindSysTyconRef path nm = findSysTyconRef path nm

  member _.TryFindSysTyconRef path nm = tryFindSysTyconRef path nm

  member _.FindSysILTypeRef nm = findSysILTypeRef nm

  member _.TryFindSysILTypeRef nm = tryFindSysILTypeRef nm

  member _.FindSysAttrib nm = findSysAttrib nm

  member _.TryFindSysAttrib nm = tryFindSysAttrib nm

  member val ilxPubCloEnv =
      EraseClosures.newIlxPubCloEnv(ilg, addMethodGeneratedAttrs, addFieldGeneratedAttrs, addFieldNeverAttrs)

  member _.AddMethodGeneratedAttributes mdef = addMethodGeneratedAttrs mdef

  member _.AddFieldGeneratedAttrs mdef = addFieldGeneratedAttrs mdef

  member _.AddFieldNeverAttrs mdef = addFieldNeverAttrs mdef

  member _.mkDebuggerHiddenAttribute() = mkILCustomAttribute (findSysILTypeRef tname_DebuggerHiddenAttribute, [], [], [])

  member _.mkDebuggerDisplayAttribute s = mkILCustomAttribute (findSysILTypeRef tname_DebuggerDisplayAttribute, [ilg.typ_String], [ILAttribElem.String (Some s)], [])

  member _.DebuggerBrowsableNeverAttribute = mkDebuggerBrowsableNeverAttribute()

  member _.mkDebuggerStepThroughAttribute() =
      mkILCustomAttribute (findSysILTypeRef tname_DebuggerStepThroughAttribute, [], [], [])

  member _.mkDebuggableAttribute (jitOptimizerDisabled) =
      mkILCustomAttribute (tref_DebuggableAttribute, [ilg.typ_Bool; ilg.typ_Bool], [ILAttribElem.Bool false; ILAttribElem.Bool jitOptimizerDisabled], [])

  member _.mkDebuggableAttributeV2(jitTracking, ignoreSymbolStoreSequencePoints, jitOptimizerDisabled, enableEnC) =
        let debuggingMode =
            (if jitTracking then 1 else 0) |||
            (if jitOptimizerDisabled then 256 else 0) |||
            (if ignoreSymbolStoreSequencePoints then 2 else 0) |||
            (if enableEnC then 4 else 0)
        let tref_DebuggableAttribute_DebuggingModes = mkILTyRefInTyRef (tref_DebuggableAttribute, tname_DebuggableAttribute_DebuggingModes)
        mkILCustomAttribute
          (tref_DebuggableAttribute, [mkILNonGenericValueTy tref_DebuggableAttribute_DebuggingModes],
           (* See System.Diagnostics.DebuggableAttribute.DebuggingModes *)
           [ILAttribElem.Int32( debuggingMode )], [])

  member internal _.CompilerGlobalState = Some compilerGlobalState

  member _.CompilerGeneratedAttribute = mkCompilerGeneratedAttribute ()

  /// Find an FSharp.Core LaguagePrimitives dynamic function that corresponds to a trait witness, e.g.
  /// AdditionDynamic for op_Addition.  Also work out the type instantiation of the dynamic function.
  member _.MakeBuiltInWitnessInfo (t: TraitConstraintInfo) =
      let memberName =
          let nm = t.MemberName
          let coreName =
              if nm.StartsWith "op_" then nm.[3..]
              elif nm = "get_Zero" then "GenericZero"
              elif nm = "get_One" then "GenericOne"
              else nm
          coreName + "Dynamic"
      let gtps, argTys, retTy, tinst =
          match memberName, t.ArgumentTypes, t.ReturnType with
          | ("AdditionDynamic" | "MultiplyDynamic" | "SubtractionDynamic"| "DivisionDynamic" | "ModulusDynamic" | "CheckedAdditionDynamic" | "CheckedMultiplyDynamic" | "CheckedSubtractionDynamic" | "LeftShiftDynamic" | "RightShiftDynamic" | "BitwiseAndDynamic" | "BitwiseOrDynamic" | "ExclusiveOrDynamic" | "LessThanDynamic" | "GreaterThanDynamic" | "LessThanOrEqualDynamic" | "GreaterThanOrEqualDynamic" | "EqualityDynamic" | "InequalityDynamic"),
            [ arg0Ty; arg1Ty ],
            Some retTy ->
               [vara; varb; varc], [ varaTy; varbTy ], varcTy, [ arg0Ty; arg1Ty; retTy ]
          | ("UnaryNegationDynamic" | "CheckedUnaryNegationDynamic" | "LogicalNotDynamic" | "ExplicitDynamic"),
            [ arg0Ty ],
            Some retTy ->
               [vara; varb ], [ varaTy ], varbTy, [ arg0Ty; retTy ]
          | "DivideByIntDynamic", [arg0Ty; _], _ ->
               [vara], [ varaTy; v_int32_ty ], varaTy, [ arg0Ty ]
          | ("GenericZeroDynamic" | "GenericOneDynamic"), [], Some retTy ->
               [vara], [ ], varaTy, [ retTy ]
          | _ -> failwithf "unknown builtin witness '%s'" memberName
      let vref = makeOtherIntrinsicValRef (fslib_MFLanguagePrimitives_nleref, memberName, None, None, gtps, (List.map List.singleton argTys, retTy))
      vref, tinst

  /// Find an FSharp.Core operator that corresponds to a trait witness
  member g.TryMakeOperatorAsBuiltInWitnessInfo isStringTy isArrayTy (t: TraitConstraintInfo) argExprs =

    match t.MemberName, t.ArgumentTypes, t.ReturnType, argExprs with
    | "get_Sign", [aty], _, (objExpr :: _) ->
        // Call Operators.sign
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, "sign", None, Some "Sign", [vara], ([[varaTy]], v_int32_ty))
        let tyargs = [aty]
        Some (info, tyargs, [objExpr])
    | "Sqrt", [aty], Some bty, [_] ->
        // Call Operators.sqrt
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, "sqrt", None, Some "Sqrt", [vara; varb], ([[varaTy]], varbTy))
        let tyargs = [aty; bty]
        Some (info, tyargs, argExprs)
    | "Pow", [aty;bty], _, [_;_] ->
        // Call Operators.(**)
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, "op_Exponentiation", None, None, [vara; varb], ([[varaTy]; [varbTy]], varaTy))
        let tyargs = [aty;bty]
        Some (info, tyargs, argExprs)
    | "Atan2", [aty;_], Some bty, [_;_] ->
        // Call Operators.atan2
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, "atan2", None, Some "Atan2", [vara; varb], ([[varaTy]; [varaTy]], varbTy))
        let tyargs = [aty;bty]
        Some (info, tyargs, argExprs)
    | "get_Zero", _, Some aty, [_] ->
        // Call LanguagePrimitives.GenericZero
        let info = makeOtherIntrinsicValRef (fslib_MFLanguagePrimitives_nleref, "GenericZero", None, None, [vara], ([], varaTy))
        let tyargs = [aty]
        Some (info, tyargs, [])
    | "get_One", _, Some aty, [_] ->
        // Call LanguagePrimitives.GenericOne
        let info = makeOtherIntrinsicValRef (fslib_MFLanguagePrimitives_nleref, "GenericOne", None, None, [vara], ([], varaTy))
        let tyargs = [aty]
        Some (info, tyargs, [])
    | ("Abs" | "Sin" | "Cos" | "Tan" | "Sinh" | "Cosh" | "Tanh" | "Atan" | "Acos" | "Asin" | "Exp" | "Ceiling" | "Floor" | "Round" | "Truncate" | "Log10"| "Log"), [aty], _, [_] ->
        // Call corresponding Operators.*
        let nm = t.MemberName
        let lower = if nm = "Ceiling" then "ceil" else nm.ToLowerInvariant()
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, lower, None, Some nm, [vara], ([[varaTy]], varaTy))
        let tyargs = [aty]
        Some (info, tyargs, argExprs)
    | "get_Item", [arrTy; _], Some rty, [_; _] when isArrayTy g arrTy ->
        Some (g.array_get_info, [rty], argExprs)
    | "set_Item", [arrTy; _; ety], _, [_; _; _] when isArrayTy g arrTy ->
        Some (g.array_set_info, [ety], argExprs)
    | "get_Item", [sty; _; _], _, [_; _] when isStringTy g sty ->
        Some (g.getstring_info, [], argExprs)
    | "op_UnaryPlus", [aty], _, [_] ->
        // Call Operators.id
        let info = makeOtherIntrinsicValRef (fslib_MFOperators_nleref, "id", None, None, [vara], ([[varaTy]], varaTy))
        let tyargs = [aty]
        Some (info, tyargs, argExprs)
    | _ ->
        None

  member _.EraseClassUnionDef cud =
     EraseUnions.mkClassUnionDef (addMethodGeneratedAttrs, addPropertyGeneratedAttrs, addPropertyNeverAttrs, addFieldGeneratedAttrs, addFieldNeverAttrs, mkDebuggerTypeProxyAttribute) ilg cud

#if DEBUG
// This global is only used during debug output
let mutable global_g = None : TcGlobals option
#endif
