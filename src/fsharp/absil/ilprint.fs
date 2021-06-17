// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.ILAsciiWriter

open System.IO
open System.Reflection

open FSharp.Compiler.IO
open Internal.Utilities
open Internal.Utilities.Library

open FSharp.Compiler.AbstractIL.AsciiConstants
open FSharp.Compiler.AbstractIL.ILX.Types
open FSharp.Compiler.AbstractIL.IL

#if DEBUG
let pretty () = true

// --------------------------------------------------------------------
// Pretty printing
// --------------------------------------------------------------------

let tyvar_generator =
  let i = ref 0
  fun n ->
    incr i; n + string !i

// Carry an environment because the way we print method variables
// depends on the gparams of the current scope.
type ppenv =
    { ilGlobals: ILGlobals
      ppenvClassFormals: int
      ppenvMethodFormals: int }

let ppenv_enter_method  mgparams env =
    {env with ppenvMethodFormals=mgparams}

let ppenv_enter_tdef gparams env =
    {env with ppenvClassFormals=List.length gparams; ppenvMethodFormals=0}

let mk_ppenv ilg = { ilGlobals = ilg; ppenvClassFormals = 0; ppenvMethodFormals = 0 }

let debug_ppenv = mk_ppenv

let ppenv_enter_modul env = { env with  ppenvClassFormals=0; ppenvMethodFormals=0 }

// --------------------------------------------------------------------
// Pretty printing - output streams
// --------------------------------------------------------------------

let output_string (os: TextWriter) (s:string) = os.Write s

let output_char (os: TextWriter) (c:char) = os.Write c

let output_int os (i:int) = output_string os (string i)

let output_hex_digit os i =
  assert (i >= 0 && i < 16)
  if i > 9 then output_char os (char (int32 'A' + (i-10)))
  else output_char os (char (int32 '0' + i))

let output_qstring os s =
  output_char os '"'
  for i = 0 to String.length s - 1 do
    let c = String.get s i
    if (c >= '\000' && c <= '\031') || (c >= '\127' && c <= '\255') then
      let c' = int32 c
      output_char os '\\'
      output_int os (c'/64)
      output_int os ((c' % 64) / 8)
      output_int os (c' % 8)
    else if (c = '"')  then
      output_char os '\\'; output_char os '"'
    else if (c = '\\')  then
      output_char os '\\'; output_char os '\\'
    else
      output_char os c
  done
  output_char os '"'
let output_sqstring os s =
  output_char os '\''
  for i = 0 to String.length s - 1 do
    let c = s.[i]
    if (c >= '\000' && c <= '\031') || (c >= '\127' && c <= '\255') then
      let c' = int32 c
      output_char os '\\'
      output_int os (c'/64)
      output_int os ((c' % 64) / 8)
      output_int os (c' % 8)
    else if (c = '\\')  then
      output_char os '\\'; output_char os '\\'
    else if (c = '\'')  then
      output_char os '\\'; output_char os '\''
    else
      output_char os c
  done
  output_char os '\''

let output_seq sep f os (a:seq<_>) =
  use e = a.GetEnumerator()
  if e.MoveNext() then
      f os e.Current
      while e.MoveNext() do
          output_string os sep
          f os e.Current

let output_array sep f os (a:_ []) =
  if not (Array.isEmpty a) then
      for i in 0..a.Length-2 do
        f os a.[i]
        output_string os sep
      f os (a.[a.Length - 1])

let output_parens f os a = output_string os "("; f os a; output_string os ")"

let output_angled f os a = output_string os "<"; f os a; output_string os ">"

let output_bracks f os a = output_string os "["; f os a; output_string os "]"

let output_id os n = output_sqstring os n

let output_label os n = output_string os n

let output_lid os lid = output_seq "." output_string os lid

let string_of_type_name (_, n) = n

let output_byte os i =
  output_hex_digit os (i / 16)
  output_hex_digit os (i % 16)

let output_bytes os (bytes:byte[]) =
  for i = 0 to bytes.Length - 1 do
    output_byte os (Bytes.get bytes i)
    output_string os " "


let bits_of_float32 (x:float32) = System.BitConverter.ToInt32(System.BitConverter.GetBytes(x), 0)

let bits_of_float (x:float) = System.BitConverter.DoubleToInt64Bits(x)

let output_u8 os (x:byte) = output_string os (string (int x))

let output_i8 os (x:sbyte) = output_string os (string (int x))

let output_u16 os (x:uint16) = output_string os (string (int x))

let output_i16 os (x:int16) = output_string os (string (int x))

let output_u32 os (x:uint32) = output_string os (string (int64 x))

let output_i32 os (x:int32) = output_string os (string x)

let output_u64 os (x:uint64) = output_string os (string (int64 x))

let output_i64 os (x:int64) = output_string os (string x)

let output_ieee32 os (x:float32) = output_string os "float32 ("; output_string os (string (bits_of_float32 x)); output_string os ")"

let output_ieee64 os (x:float) = output_string os "float64 ("; output_string os (string (bits_of_float x)); output_string os ")"

let rec goutput_scoref env os = function
  | ILScopeRef.Local -> ()
  | ILScopeRef.Assembly aref ->
      output_string os "["; output_sqstring os aref.Name; output_string os "]"
  | ILScopeRef.Module mref ->
      output_string os "[.module "; output_sqstring os mref.Name; output_string os "]"
  | ILScopeRef.PrimaryAssembly ->
      output_string os "["; output_sqstring os env.ilGlobals.primaryAssemblyName; output_string os "]"

and goutput_type_name_ref env os (scoref, enc, n) =
  goutput_scoref env os scoref
  output_seq "/" output_sqstring os (enc@[n])
and goutput_tref env os (x:ILTypeRef) =
  goutput_type_name_ref env os (x.Scope, x.Enclosing, x.Name)

and goutput_typ env os ty =
  match ty with
  | ILType.Boxed tr -> goutput_tspec env os tr
  | ILType.TypeVar tv ->
      // Special rule to print method type variables in Generic EE preferred form
      // when an environment is available to help us do this.
      let cgparams = env.ppenvClassFormals
      let mgparams = env.ppenvMethodFormals
      if int tv < cgparams then
        output_string os "!"
        output_tyvar os tv
      elif int tv - cgparams < mgparams then
        output_string os "!!"
        output_int os (int tv - cgparams)
      else
        output_string os "!"
        output_tyvar os tv
        output_int os (int tv)

  | ILType.Byref typ -> goutput_typ env os typ; output_string os "&"
  | ILType.Ptr typ -> goutput_typ env os typ; output_string   os "*"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_SByte.TypeSpec.Name -> output_string os "int8"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Int16.TypeSpec.Name -> output_string os "int16"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Int32.TypeSpec.Name -> output_string os "int32"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Int64.TypeSpec.Name -> output_string os "int64"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_IntPtr.TypeSpec.Name -> output_string os "native int"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Byte.TypeSpec.Name -> output_string os "unsigned int8"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_UInt16.TypeSpec.Name -> output_string os "unsigned int16"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_UInt32.TypeSpec.Name -> output_string os "unsigned int32"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_UInt64.TypeSpec.Name -> output_string os "unsigned int64"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_UIntPtr.TypeSpec.Name -> output_string os "native unsigned int"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Double.TypeSpec.Name -> output_string os "float64"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Single.TypeSpec.Name -> output_string os "float32"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Bool.TypeSpec.Name -> output_string os "bool"
  | ILType.Value tspec when tspec.Name = PrimaryAssemblyILGlobals.typ_Char.TypeSpec.Name -> output_string os "char"
  | ILType.Value tspec ->
      output_string os "value class "
      goutput_tref env os tspec.TypeRef
      output_string os " "
      goutput_gactuals env os tspec.GenericArgs
  | ILType.Void -> output_string os "void"
  | ILType.Array (bounds, ty) ->
      goutput_typ env os ty
      output_string os "["
      output_arr_bounds os bounds
      output_string os "]"
  | ILType.FunctionPointer csig ->
      output_string os "method "
      goutput_typ env os csig.ReturnType
      output_string os " *("
      output_seq ", " (goutput_typ env) os csig.ArgTypes
      output_string os ")"
  | _ -> output_string os "NaT"

and output_tyvar os d =
  output_u16 os d; ()

and goutput_ldtoken_info env os = function
  | ILToken.ILType x -> goutput_typ env os x
  | ILToken.ILMethod x -> output_string os "method "; goutput_mspec env os x
  | ILToken.ILField x -> output_string os "field "; goutput_fspec env os x

and goutput_typ_with_shortened_class_syntax env os = function
    ILType.Boxed tspec when tspec.GenericArgs = [] ->
      goutput_tref env os tspec.TypeRef
  | typ2 -> goutput_typ env os typ2

and goutput_gactuals env os inst =
  if not (List.isEmpty inst) then
    output_string os "<"
    output_seq ", " (goutput_gactual env)  os inst
    output_string os ">"

and goutput_gactual env os ty = goutput_typ env os ty

and goutput_tspec env os tspec =
      output_string os "class "
      goutput_tref env os tspec.TypeRef
      output_string os " "
      goutput_gactuals env os tspec.GenericArgs

and output_arr_bounds os = function
  | bounds when bounds = ILArrayShape.SingleDimensional -> ()
  | ILArrayShape l ->
      output_seq ", "
          (fun os -> function
            | (None, None)  -> output_string os ""
            | (None, Some sz) ->
                output_int os sz
            | (Some lower, None) ->
                output_int os lower
                output_string os " ... "
            | (Some lower, Some d) ->
                output_int os lower
                output_string os " ... "
                output_int os d)
          os
          l

and goutput_permission _env os p =
  let output_security_action os x =
    output_string os
      (match x with
        | ILSecurityAction.Request -> "request"
        | ILSecurityAction.Demand -> "demand"
        | ILSecurityAction.Assert-> "assert"
        | ILSecurityAction.Deny-> "deny"
        | ILSecurityAction.PermitOnly-> "permitonly"
        | ILSecurityAction.LinkCheck-> "linkcheck"
        | ILSecurityAction.InheritCheck-> "inheritcheck"
        | ILSecurityAction.ReqMin-> "reqmin"
        | ILSecurityAction.ReqOpt-> "reqopt"
        | ILSecurityAction.ReqRefuse-> "reqrefuse"
        | ILSecurityAction.PreJitGrant-> "prejitgrant"
        | ILSecurityAction.PreJitDeny-> "prejitdeny"
        | ILSecurityAction.NonCasDemand-> "noncasdemand"
        | ILSecurityAction.NonCasLinkDemand-> "noncaslinkdemand"
        | ILSecurityAction.NonCasInheritance-> "noncasinheritance"
        | ILSecurityAction.LinkDemandChoice -> "linkdemandchoice"
        | ILSecurityAction.InheritanceDemandChoice -> "inheritancedemandchoice"
        | ILSecurityAction.DemandChoice -> "demandchoice")

  match p with
  | ILSecurityDecl (sa, b) ->
      output_string os " .permissionset "
      output_security_action os sa
      output_string os " = ("
      output_bytes os b
      output_string os ")"

and goutput_security_decls env os (ps: ILSecurityDecls) =  output_seq " " (goutput_permission env)  os ps.AsList

and goutput_gparam env os (gf: ILGenericParameterDef) =
  output_string os (tyvar_generator gf.Name)
  output_parens (output_seq ", " (goutput_typ env)) os gf.Constraints

and goutput_gparams env os b =
  if not (isNil b) then
     output_string os "<"; output_seq ", " (goutput_gparam env) os b;  output_string os ">"; ()

and output_bcc os bcc =
  output_string os
    (match bcc with
    | ILArgConvention.FastCall -> "fastcall "
    | ILArgConvention.StdCall -> "stdcall "
    | ILArgConvention.ThisCall -> "thiscall "
    | ILArgConvention.CDecl -> "cdecl "
    | ILArgConvention.Default -> " "
    | ILArgConvention.VarArg -> "vararg ")

and output_callconv os (Callconv (hasthis, cc)) =
  output_string os
    (match hasthis with
      ILThisConvention.Instance -> "instance "
    | ILThisConvention.InstanceExplicit -> "explicit "
    | ILThisConvention.Static -> "")
  output_bcc os cc

and goutput_dlocref env os (dref:ILType) =
  match dref with
  | dref when
       dref.IsNominal &&
       isTypeNameForGlobalFunctions dref.TypeRef.Name &&
       dref.TypeRef.Scope = ILScopeRef.Local ->
   ()
  | dref when
       dref.IsNominal &&
       isTypeNameForGlobalFunctions dref.TypeRef.Name ->
   goutput_scoref env os dref.TypeRef.Scope
   output_string os "::"
  | ty ->goutput_typ_with_shortened_class_syntax env os ty;  output_string os "::"

and goutput_callsig env os (csig:ILCallingSignature) =
  output_callconv os csig.CallingConv
  output_string os " "
  goutput_typ env os csig.ReturnType
  output_parens (output_seq ", " (goutput_typ env)) os csig.ArgTypes

and goutput_mref env os (mref:ILMethodRef) =
  output_callconv os mref.CallingConv
  output_string os " "
  goutput_typ_with_shortened_class_syntax env os mref.ReturnType
  output_string os " "
  // no quotes for ".ctor"
  let name = mref.Name
  if name = ".ctor" || name = ".cctor" then output_string os name else output_id os name
  output_parens (output_seq ", " (goutput_typ env)) os mref.ArgTypes

and goutput_mspec env os (mspec:ILMethodSpec) =
  let fenv =
    ppenv_enter_method mspec.GenericArity
      (ppenv_enter_tdef (mkILFormalTypars mspec.DeclaringType.GenericArgs) env)
  output_callconv os mspec.CallingConv
  output_string os " "
  goutput_typ fenv os mspec.FormalReturnType
  output_string os " "
  goutput_dlocref env os mspec.DeclaringType
  output_string os " "
  let name = mspec.Name
  if name = ".ctor" || name = ".cctor" then output_string os name else output_id os name
  goutput_gactuals env os mspec.GenericArgs
  output_parens (output_seq ", " (goutput_typ fenv)) os mspec.FormalArgTypes

and goutput_vararg_mspec env os (mspec, varargs) =
   match varargs with
   | None -> goutput_mspec env os mspec
   | Some varargs' ->
       let fenv =
         ppenv_enter_method mspec.GenericArity
           (ppenv_enter_tdef (mkILFormalTypars mspec.DeclaringType.GenericArgs) env)
       output_callconv os mspec.CallingConv
       output_string os " "
       goutput_typ fenv os mspec.FormalReturnType
       output_string os " "
       goutput_dlocref env os mspec.DeclaringType
       let name = mspec.Name
       if name = ".ctor" || name = ".cctor" then output_string os name else output_id os name
       goutput_gactuals env os mspec.GenericArgs
       output_string os "("
       output_seq ", " (goutput_typ fenv) os mspec.FormalArgTypes
       output_string os ", ..., "
       output_seq ", " (goutput_typ fenv) os varargs'
       output_string os ")"

and goutput_vararg_sig env os (csig:ILCallingSignature, varargs:ILVarArgs) =
   match varargs with
   | None -> goutput_callsig env os csig; ()
   | Some varargs' ->
       goutput_typ env os csig.ReturnType
       output_string os " ("
       let argtys = csig.ArgTypes
       if argtys.Length <> 0 then
           output_seq ", " (goutput_typ env)  os argtys
       output_string os ", ..., "
       output_seq ", " (goutput_typ env) os varargs'
       output_string os ")"

and goutput_fspec env os (x:ILFieldSpec) =
  let fenv = ppenv_enter_tdef (mkILFormalTypars x.DeclaringType.GenericArgs) env
  goutput_typ fenv os x.FormalType
  output_string os " "
  goutput_dlocref env os x.DeclaringType
  output_id os x.Name

let output_member_access os access =
  output_string os
    (match access with
    | ILMemberAccess.Public -> "public"
    | ILMemberAccess.Private  -> "private"
    | ILMemberAccess.Family  -> "family"
    | ILMemberAccess.CompilerControlled -> "privatescope"
    | ILMemberAccess.FamilyAndAssembly -> "famandassem"
    | ILMemberAccess.FamilyOrAssembly -> "famorassem"
    | ILMemberAccess.Assembly -> "assembly")

let output_type_access os access =
  match access with
  | ILTypeDefAccess.Public -> output_string os "public"
  | ILTypeDefAccess.Private  -> output_string os "private"
  | ILTypeDefAccess.Nested  ilMemberAccess -> output_string os "nested "; output_member_access os ilMemberAccess

let output_encoding os e =
  match e with
  | ILDefaultPInvokeEncoding.Ansi -> output_string os " ansi "
  | ILDefaultPInvokeEncoding.Auto  -> output_string os " autochar "
  | ILDefaultPInvokeEncoding.Unicode -> output_string os " unicode "
let output_field_init os = function
  | ILFieldInit.String s -> output_string os "= "; output_string os s
  | ILFieldInit.Bool x-> output_string os "= bool"; output_parens output_string os (if x then "true" else "false")
  | ILFieldInit.Char x-> output_string os "= char"; output_parens output_u16 os x
  | ILFieldInit.Int8 x-> output_string os "= int8"; output_parens output_i8 os x
  | ILFieldInit.Int16 x-> output_string os "= int16"; output_parens output_i16 os x
  | ILFieldInit.Int32 x-> output_string os "= int32"; output_parens output_i32 os x
  | ILFieldInit.Int64 x-> output_string os "= int64"; output_parens output_i64 os x
  | ILFieldInit.UInt8 x-> output_string os "= uint8"; output_parens output_u8 os x
  | ILFieldInit.UInt16 x-> output_string os "= uint16"; output_parens output_u16 os x
  | ILFieldInit.UInt32 x-> output_string os "= uint32"; output_parens output_u32 os x
  | ILFieldInit.UInt64 x-> output_string os "= uint64"; output_parens output_u64 os x
  | ILFieldInit.Single x-> output_string os "= float32"; output_parens output_ieee32 os x
  | ILFieldInit.Double x-> output_string os "= float64"; output_parens output_ieee64 os x
  | ILFieldInit.Null-> output_string os "= nullref"

let output_at os b =
   Printf.fprintf os " at (* no labels for data available, data = %a *)" (output_parens output_bytes) b

let output_option f os = function None -> () | Some x -> f os x

let goutput_alternative_ref env os (alt: IlxUnionCase) =
  output_id os alt.Name
  alt.FieldDefs |> output_parens (output_array ", " (fun os fdef -> goutput_typ env os fdef.Type)) os

let goutput_curef env os (IlxUnionRef(_, tref, alts, _, _)) =
  output_string os " .classunion import "
  goutput_tref env os tref
  output_parens (output_array ", " (goutput_alternative_ref env)) os alts

let goutput_cuspec env os (IlxUnionSpec(IlxUnionRef(_, tref, _, _, _), i)) =
  output_string os "class /* classunion */ "
  goutput_tref env os  tref
  goutput_gactuals env os i

let output_basic_type os x =
  output_string os
    (match x with
    | DT_I1 -> "i1"
    | DT_U1 -> "u1"
    | DT_I2 -> "i2"
    | DT_U2 -> "u2"
    | DT_I4 -> "i4"
    | DT_U4 -> "u4"
    | DT_I8 -> "i8"
    | DT_U8 -> "u8"
    | DT_R4 -> "r4"
    | DT_R8 -> "r8"
    | DT_R  -> "r"
    | DT_I  -> "i"
    | DT_U  -> "u"
    | DT_REF  -> "ref")

let output_custom_attr_data os data =
  output_string os " = "; output_parens output_bytes os data

let goutput_custom_attr env os (attr: ILAttribute) =
  output_string os " .custom "
  goutput_mspec env os attr.Method
  let data = getCustomAttrData attr
  output_custom_attr_data os data

let goutput_custom_attrs env os (attrs : ILAttributes) =
  Array.iter (fun attr -> goutput_custom_attr env os attr;  output_string os "\n" ) attrs.AsArray

let goutput_fdef _tref env os (fd: ILFieldDef) =
  output_string os " .field "
  match fd.Offset with Some i -> output_string os "["; output_i32 os i; output_string os "] " | None -> ()
  match fd.Marshal with Some _i -> output_string os "// marshal attribute not printed\n"; | None -> ()
  output_member_access os fd.Access
  output_string os " "
  if fd.IsStatic then output_string os " static "
  if fd.IsLiteral then output_string os " literal "
  if fd.IsSpecialName then output_string os " specialname rtspecialname "
  if fd.IsInitOnly then output_string os " initonly "
  if fd.NotSerialized then output_string os " notserialized "
  goutput_typ env os fd.FieldType
  output_string os " "
  output_id os fd.Name
  output_option output_at os  fd.Data
  output_option output_field_init os fd.LiteralValue
  output_string os "\n"
  goutput_custom_attrs env os fd.CustomAttrs

let output_alignment os =  function
    Aligned -> ()
  | Unaligned1 -> output_string os "unaligned. 1 "
  | Unaligned2 -> output_string os "unaligned. 2 "
  | Unaligned4 -> output_string os "unaligned. 4 "

let output_volatility os =  function
    Nonvolatile -> ()
  | Volatile -> output_string os "volatile. "
let output_tailness os =  function
  | Tailcall -> output_string os "tail. "
  | _ -> ()
let output_after_tailcall os =  function
  | Tailcall  -> output_string os " ret "
  | _ -> ()
let rec goutput_apps env os =  function
  | Apps_tyapp (actual, cs) ->
      output_angled (goutput_gactual env) os actual
      output_string os " "
      output_angled (goutput_gparam env) os (mkILSimpleTypar "T")
      output_string os " "
      goutput_apps env os cs
  | Apps_app(ty, cs) ->
      output_parens (goutput_typ env) os ty
      output_string os " "
      goutput_apps env os cs
  | Apps_done ty ->
      output_string os "--> "
      goutput_typ env os ty

/// Print the short form of instructions
let output_short_u16 os (x:uint16) =
     if int x < 256 then (output_string os ".s "; output_u16 os x)
     else output_string os " "; output_u16 os x

let output_short_i32 os i32 =
     if  i32 < 256 && 0 >= i32 then (output_string os ".s "; output_i32 os i32)
     else output_string os " "; output_i32 os i32

let output_code_label os lab =
  output_string os (formatCodeLabel lab)

let goutput_local env os (l: ILLocal) =
  goutput_typ env os l.Type
  if l.IsPinned then output_string os " pinned"

let goutput_param env os (l: ILParameter) =
  match l.Name with
      None -> goutput_typ env os l.Type
    | Some n -> goutput_typ env os l.Type; output_string os " "; output_sqstring os n

let goutput_params env os ps =
  output_parens (output_seq ", " (goutput_param env)) os ps

let goutput_freevar env os l =
  goutput_typ env os l.fvType; output_string os " "; output_sqstring os l.fvName

let goutput_freevars env os ps =
  output_parens (output_seq ", " (goutput_freevar env)) os ps

let output_source os (s:ILSourceMarker) =
  if s.Document.File <> "" then
    output_string os " .line "
    output_int os s.Line
    if s.Column <> -1 then
      output_string os " : "
      output_int os s.Column
    output_string os " /* - "
    output_int os s.EndLine
    if s.Column <> -1 then
      output_string os " : "
      output_int os s.EndColumn
    output_string os "*/ "
    output_sqstring os s.Document.File


let rec goutput_instr env os inst =
  match inst with
  | si when isNoArgInstr si ->
       output_lid os (wordsOfNoArgInstr si)
  | I_brcmp (cmp, tg1)  ->
      output_string os
          (match cmp with
          | BI_beq -> "beq"
          | BI_bgt -> "bgt"
          | BI_bgt_un -> "bgt.un"
          | BI_bge -> "bge"
          | BI_bge_un -> "bge.un"
          | BI_ble -> "ble"
          | BI_ble_un -> "ble.un"
          | BI_blt -> "blt"
          | BI_blt_un -> "blt.un"
          | BI_bne_un -> "bne.un"
          | BI_brfalse -> "brfalse"
          | BI_brtrue -> "brtrue")
      output_string os " "
      output_code_label os tg1
  | I_br  tg -> output_string os "/* br "; output_code_label os tg;  output_string os "*/"
  | I_leave tg  -> output_string os "leave "; output_code_label os tg
  | I_call  (tl, mspec, varargs)  ->
      output_tailness os tl
      output_string os "call "
      goutput_vararg_mspec env os (mspec, varargs)
      output_after_tailcall os tl
  | I_calli (tl, mref, varargs) ->
      output_tailness os tl
      output_string os "calli "
      goutput_vararg_sig env os (mref, varargs)
      output_after_tailcall os tl
  | I_ldarg u16 -> output_string os "ldarg"; output_short_u16 os u16
  | I_ldarga  u16 -> output_string os "ldarga "; output_u16 os u16
  | (AI_ldc (dt, ILConst.I4 x)) ->
      output_string os "ldc."; output_basic_type os dt; output_short_i32 os x
  | (AI_ldc (dt, ILConst.I8 x)) ->
      output_string os "ldc."; output_basic_type os dt; output_string os " "; output_i64 os x
  | (AI_ldc (dt, ILConst.R4 x)) ->
      output_string os "ldc."; output_basic_type os dt; output_string os " "; output_ieee32 os x
  | (AI_ldc (dt, ILConst.R8 x)) ->
      output_string os "ldc."; output_basic_type os dt; output_string os " "; output_ieee64 os x
  | I_ldftn mspec -> output_string os "ldftn "; goutput_mspec env os mspec
  | I_ldvirtftn mspec -> output_string os "ldvirtftn "; goutput_mspec env os mspec
  | I_ldind (al, vol, dt) ->
      output_alignment os al
      output_volatility os vol
      output_string os "ldind."
      output_basic_type os dt
  | I_cpblk (al, vol)  ->
      output_alignment os al
      output_volatility os vol
      output_string os "cpblk"
  | I_initblk (al, vol)  ->
      output_alignment os al
      output_volatility os vol
      output_string os "initblk"
  | I_ldloc u16 -> output_string os "ldloc"; output_short_u16 os u16
  | I_ldloca  u16 -> output_string os "ldloca "; output_u16 os u16
  | I_starg u16 -> output_string os "starg "; output_u16 os u16
  | I_stind (al, vol, dt) ->
      output_alignment os al
      output_volatility os vol
      output_string os "stind."
      output_basic_type os dt
  | I_stloc u16 -> output_string os "stloc"; output_short_u16 os u16
  | I_switch l -> output_string os "switch "; output_parens (output_seq ", " output_code_label) os l
  | I_callvirt  (tl, mspec, varargs) ->
      output_tailness os tl
      output_string os "callvirt "
      goutput_vararg_mspec env os (mspec, varargs)
      output_after_tailcall os tl
  | I_callconstraint  (tl, ty, mspec, varargs) ->
      output_tailness os tl
      output_string os "constraint. "
      goutput_typ env os ty
      output_string os " callvirt "
      goutput_vararg_mspec env os (mspec, varargs)
      output_after_tailcall os tl
  | I_castclass ty  -> output_string os "castclass "; goutput_typ env os ty
  | I_isinst  ty  -> output_string os "isinst "; goutput_typ env os ty
  | I_ldfld (al, vol, fspec)  ->
      output_alignment os al
      output_volatility os vol
      output_string os "ldfld "
      goutput_fspec env os fspec
  | I_ldflda  fspec ->
      output_string os "ldflda "
      goutput_fspec env os fspec
  | I_ldsfld  (vol, fspec) ->
      output_volatility os vol
      output_string os "ldsfld "
      goutput_fspec env os fspec
  | I_ldsflda fspec ->
      output_string os "ldsflda "
      goutput_fspec env os fspec
  | I_stfld (al, vol, fspec)  ->
      output_alignment os al
      output_volatility os vol
      output_string os "stfld "
      goutput_fspec env os fspec
  | I_stsfld  (vol, fspec) ->
      output_volatility os vol
      output_string os "stsfld "
      goutput_fspec env os fspec
  | I_ldtoken  tok  -> output_string os "ldtoken ";  goutput_ldtoken_info env os tok
  | I_refanyval ty  -> output_string os "refanyval "; goutput_typ env os ty
  | I_refanytype  -> output_string os "refanytype"
  | I_mkrefany  typ -> output_string os "mkrefany "; goutput_typ env os typ
  | I_ldstr s ->
      output_string os "ldstr "
      output_string os s
  | I_newobj  (mspec, varargs) ->
      // newobj: IL has a special rule that the CC is always implicitly "instance" and need
      // not be mentioned explicitly
      output_string os "newobj "
      goutput_vararg_mspec env os (mspec, varargs)
  | I_stelem    dt      -> output_string os "stelem."; output_basic_type os dt
  | I_ldelem    dt      -> output_string os "ldelem."; output_basic_type os dt

  | I_newarr    (shape, typ) ->
      if shape = ILArrayShape.SingleDimensional then
        output_string os "newarr "
        goutput_typ_with_shortened_class_syntax env os typ
      else
        output_string os "newobj void "
        goutput_dlocref env os (mkILArrTy(typ, shape))
        output_string os ".ctor"
        let rank = shape.Rank
        output_parens (output_array ", " (goutput_typ env)) os (Array.create ( rank) PrimaryAssemblyILGlobals.typ_Int32)
  | I_stelem_any (shape, dt)     ->
      if shape = ILArrayShape.SingleDimensional then
        output_string os "stelem.any "; goutput_typ env os dt
      else
        output_string os "call instance void "
        goutput_dlocref env os (mkILArrTy(dt, shape))
        output_string os "Set"
        let rank = shape.Rank
        let arr = Array.create (rank + 1) PrimaryAssemblyILGlobals.typ_Int32
        arr.[rank] <- dt
        output_parens (output_array ", " (goutput_typ env)) os arr
  | I_ldelem_any (shape, tok) ->
      if shape = ILArrayShape.SingleDimensional then
        output_string os "ldelem.any "; goutput_typ env os tok
      else
        output_string os "call instance "
        goutput_typ env os tok
        output_string os " "
        goutput_dlocref env os (mkILArrTy(tok, shape))
        output_string os "Get"
        let rank = shape.Rank
        output_parens (output_array ", " (goutput_typ env)) os (Array.create ( rank) PrimaryAssemblyILGlobals.typ_Int32)
  | I_ldelema   (ro, _, shape, tok)  ->
      if ro = ReadonlyAddress then output_string os "readonly. "
      if shape = ILArrayShape.SingleDimensional then
        output_string os "ldelema "; goutput_typ env os tok
      else
        output_string os "call instance "
        goutput_typ env os (ILType.Byref tok)
        output_string os " "
        goutput_dlocref env os (mkILArrTy(tok, shape))
        output_string os "Address"
        let rank = shape.Rank
        output_parens (output_array ", " (goutput_typ env)) os (Array.create ( rank) PrimaryAssemblyILGlobals.typ_Int32)

  | I_box       tok     -> output_string os "box "; goutput_typ env os tok
  | I_unbox     tok     -> output_string os "unbox "; goutput_typ env os tok
  | I_unbox_any tok     -> output_string os "unbox.any "; goutput_typ env os tok
  | I_initobj   tok     -> output_string os "initobj "; goutput_typ env os tok
  | I_ldobj (al, vol, tok)        ->
      output_alignment os al
      output_volatility os vol
      output_string os "ldobj "
      goutput_typ env os tok
  | I_stobj  (al, vol, tok) ->
      output_alignment os al
      output_volatility os vol
      output_string os "stobj "
      goutput_typ env os tok
  | I_cpobj tok -> output_string os "cpobj "; goutput_typ env os tok
  | I_sizeof  tok -> output_string os "sizeof "; goutput_typ env os tok
  | I_seqpoint  s -> output_source os s
  | EI_ilzero ty -> output_string os "ilzero "; goutput_typ env os ty
  | _ ->
      output_string os "<printing for this instruction is not implemented>"


let goutput_ilmbody env os (il: ILMethodBody) =
  if il.IsZeroInit then output_string os " .zeroinit\n"
  output_string os " .maxstack "
  output_i32 os il.MaxStack
  output_string os "\n"
  if il.Locals.Length  <> 0 then
    output_string os " .locals("
    output_seq ", \n " (goutput_local env)  os il.Locals
    output_string os ")\n"

let goutput_mbody is_entrypoint env os (md: ILMethodDef) =
  if md.ImplAttributes &&& MethodImplAttributes.Native <> enum 0 then output_string os "native "
  elif md.ImplAttributes &&&  MethodImplAttributes.IL <> enum 0 then output_string os "cil "
  else output_string os "runtime "

  output_string os (if md.IsInternalCall then "internalcall " else " ")
  output_string os (if md.IsManaged then "managed " else " ")
  output_string os (if md.IsForwardRef then "forwardref " else " ")
  output_string os " \n{ \n"
  goutput_security_decls env os md.SecurityDecls
  goutput_custom_attrs env os md.CustomAttrs
  match md.Body with
    | MethodBody.IL il -> goutput_ilmbody env os il.Value
    | _ -> ()
  if is_entrypoint then output_string os " .entrypoint"
  output_string os "\n"
  output_string os "}\n"

let goutput_mdef env os (md:ILMethodDef) =
  let attrs =
      if md.IsVirtual then
            "virtual " +
            (if md.IsFinal then "final " else "") +
            (if md.IsNewSlot then "newslot " else "") +
            (if md.IsCheckAccessOnOverride then " strict " else "") +
            (if md.IsAbstract then " abstract " else "") +
              "  "
      elif md.IsNonVirtualInstance then     ""
      elif md.IsConstructor then "rtspecialname"
      elif md.IsStatic then
            "static " +
            (match md.Body with
              MethodBody.PInvoke (attrLazy) ->
                let attr = attrLazy.Value
                "pinvokeimpl(\"" + attr.Where.Name + "\" as \"" + attr.Name + "\"" +
                (match attr.CallingConv with
                | PInvokeCallingConvention.None -> ""
                | PInvokeCallingConvention.Cdecl -> " cdecl"
                | PInvokeCallingConvention.Stdcall -> " stdcall"
                | PInvokeCallingConvention.Thiscall -> " thiscall"
                | PInvokeCallingConvention.Fastcall -> " fastcall"
                | PInvokeCallingConvention.WinApi -> " winapi" ) +

                (match attr.CharEncoding with
                | PInvokeCharEncoding.None -> ""
                | PInvokeCharEncoding.Ansi -> " ansi"
                | PInvokeCharEncoding.Unicode -> " unicode"
                | PInvokeCharEncoding.Auto -> " autochar") +

                (if attr.NoMangle then " nomangle" else "") +
                (if attr.LastError then " lasterr" else "") +
                ")"
              | _ ->
                  "")
      elif md.IsClassInitializer then "specialname rtspecialname static"
      else ""
  let is_entrypoint = md.IsEntryPoint
  let menv = ppenv_enter_method (List.length md.GenericParams) env
  output_string os " .method "
  if md.IsHideBySig then output_string os "hidebysig "
  if md.IsReqSecObj then output_string os "reqsecobj "
  if md.IsSpecialName then output_string os "specialname "
  if md.IsUnmanagedExport then output_string os "unmanagedexp "
  output_member_access os md.Access
  output_string os " "
  output_string os attrs
  output_string os " "
  output_callconv os md.CallingConv
  output_string os " "
  (goutput_typ menv) os md.Return.Type
  output_string os " "
  output_id os md.Name
  output_string os " "
  (goutput_gparams env) os md.GenericParams
  output_string os " "
  (goutput_params menv) os md.Parameters
  output_string os " "
  if md.IsSynchronized then output_string os "synchronized "
  if md.IsMustRun then output_string os "/* mustrun */ "
  if md.IsPreserveSig then output_string os "preservesig "
  if md.IsNoInline then output_string os "noinlining "
  if md.IsAggressiveInline then output_string os "aggressiveinlining "
  (goutput_mbody is_entrypoint menv) os md
  output_string os "\n"

let goutput_pdef env os (pd: ILPropertyDef) =
    output_string os  "property\n\tgetter: "
    (match pd.GetMethod with None -> () | Some mref -> goutput_mref env os mref)
    output_string os  "\n\tsetter: "
    (match pd.SetMethod with None -> () | Some mref -> goutput_mref env os mref)

let goutput_superclass env os = function
    None -> ()
  | Some typ -> output_string os "extends "; (goutput_typ_with_shortened_class_syntax env) os typ

let goutput_superinterfaces env os imp =
  if not (List.isEmpty imp) then
      output_string os "implements "
      output_seq ", " (goutput_typ_with_shortened_class_syntax env) os imp

let goutput_implements env os (imp:ILTypes) =
  if not (List.isEmpty imp) then
      output_string os "implements "
      output_seq ", " (goutput_typ_with_shortened_class_syntax env) os imp

let the = function Some x -> x  | None -> failwith "the"

let output_type_layout_info os info =
  if info.Size <> None then (output_string os " .size "; output_i32 os (the info.Size))
  if info.Pack <> None then (output_string os " .pack "; output_u16 os (the info.Pack))

let splitTypeLayout = function
  | ILTypeDefLayout.Auto -> "auto", (fun _os () -> ())
  | ILTypeDefLayout.Sequential info -> "sequential", (fun os () -> output_type_layout_info os info)
  | ILTypeDefLayout.Explicit info -> "explicit", (fun os () -> output_type_layout_info os info)

let goutput_fdefs tref env os (fdefs: ILFieldDefs) =
  List.iter (fun f -> (goutput_fdef tref env) os f; output_string os "\n" ) fdefs.AsList

let goutput_mdefs env os (mdefs: ILMethodDefs) =
  Array.iter (fun f -> (goutput_mdef env) os f; output_string os "\n" ) mdefs.AsArray

let goutput_pdefs env os (pdefs: ILPropertyDefs) =
  List.iter (fun f -> (goutput_pdef env) os f; output_string os "\n" ) pdefs.AsList

let rec goutput_tdef enc env contents os (cd: ILTypeDef) =
  let env = ppenv_enter_tdef cd.GenericParams env
  let layout_attr, pp_layout_decls = splitTypeLayout cd.Layout
  if isTypeNameForGlobalFunctions cd.Name then
      if contents then
          let tref = (mkILNestedTyRef (ILScopeRef.Local, enc, cd.Name))
          goutput_mdefs env os cd.Methods
          goutput_fdefs tref env os cd.Fields
          goutput_pdefs env os cd.Properties
  else
      output_string os "\n"
      if cd.IsInterface then output_string os ".class  interface "
      else output_string os ".class "
      output_init_semantics os cd.Attributes
      output_string os " "
      output_type_access os cd.Access
      output_string os " "
      output_encoding os cd.Encoding
      output_string os " "
      output_string os layout_attr
      output_string os " "
      if cd.IsSealed then  output_string os "sealed "
      if cd.IsAbstract then  output_string os "abstract "
      if cd.IsSerializable then  output_string os "serializable "
      if cd.IsComInterop then  output_string os "import "
      output_sqstring os cd.Name
      goutput_gparams env os cd.GenericParams
      output_string os "\n\t"
      goutput_superclass env os cd.Extends
      output_string os "\n\t"
      goutput_implements env os cd.Implements
      output_string os "\n{\n "
      if contents then
        let tref = (mkILNestedTyRef (ILScopeRef.Local, enc, cd.Name))
        goutput_custom_attrs env os cd.CustomAttrs
        goutput_security_decls env os cd.SecurityDecls
        pp_layout_decls os ()
        goutput_fdefs tref env os cd.Fields
        goutput_mdefs env os cd.Methods
      goutput_tdefs contents  (enc@[cd.Name]) env os cd.NestedTypes
      output_string os "\n}"

and output_init_semantics os f =
  if f &&& TypeAttributes.BeforeFieldInit <> enum 0 then output_string os "beforefieldinit"

and goutput_lambdas env os lambdas =
  match lambdas with
   | Lambdas_forall (gf, l) ->
       output_angled (goutput_gparam env) os gf
       output_string os " "
       (goutput_lambdas env) os l
   | Lambdas_lambda (ps, l) ->
       output_parens (goutput_param env) os ps
       output_string os " "
       (goutput_lambdas env) os l
   | Lambdas_return typ -> output_string os "--> "; (goutput_typ env) os typ

and goutput_tdefs contents enc env os (td: ILTypeDefs) =
  List.iter (goutput_tdef enc env contents os) td.AsList

let output_ver os (version: ILVersionInfo) =
    output_string os " .ver "
    output_u16 os version.Major
    output_string os " : "
    output_u16 os version.Minor
    output_string os " : "
    output_u16 os version.Build
    output_string os " : "
    output_u16 os version.Revision

let output_locale os s = output_string os " .Locale "; output_qstring os s

let output_hash os x =
    output_string os " .hash = "; output_parens output_bytes os x
let output_publickeytoken os x =
  output_string os " .publickeytoken = "; output_parens output_bytes os x
let output_publickey os x =
  output_string os " .publickey = "; output_parens output_bytes os x

let output_publickeyinfo os = function
  | PublicKey k -> output_publickey os k
  | PublicKeyToken k -> output_publickeytoken os k

let output_assemblyRef os (aref:ILAssemblyRef) =
  output_string os " .assembly extern "
  output_sqstring os aref.Name
  if aref.Retargetable then output_string os " retargetable "
  output_string os " { "
  output_option output_hash os aref.Hash
  output_option output_publickeyinfo os aref.PublicKey
  output_option output_ver os aref.Version
  output_option output_locale os aref.Locale
  output_string os " } "

let output_modref os (modref:ILModuleRef) =
  output_string os (if modref.HasMetadata then " .module extern " else " .file nometadata " )
  output_sqstring os modref.Name
  output_option output_hash os modref.Hash

let goutput_resource env os r =
  output_string os " .mresource "
  output_string os (match r.Access with ILResourceAccess.Public -> " public " | ILResourceAccess.Private -> " private ")
  output_sqstring os r.Name
  output_string os " { "
  goutput_custom_attrs env os r.CustomAttrs
  match r.Location with
  | ILResourceLocation.Local _ ->
      output_string os " /* loc nyi */ "
  | ILResourceLocation.File (mref, off) ->
      output_string os " .file "
      output_sqstring os mref.Name
      output_string os "  at "
      output_i32 os off
  | ILResourceLocation.Assembly aref ->
      output_string os " .assembly extern "
      output_sqstring os aref.Name
  output_string os " }\n "

let goutput_manifest env os m =
  output_string os " .assembly "
  match m.AssemblyLongevity with
            | ILAssemblyLongevity.Unspecified -> ()
            | ILAssemblyLongevity.Library -> output_string os "library "
            | ILAssemblyLongevity.PlatformAppDomain -> output_string os "platformappdomain "
            | ILAssemblyLongevity.PlatformProcess -> output_string os "platformprocess "
            | ILAssemblyLongevity.PlatformSystem  -> output_string os "platformmachine "
  output_sqstring os m.Name
  output_string os " { \n"
  output_string os ".hash algorithm "; output_i32 os m.AuxModuleHashAlgorithm; output_string os "\n"
  goutput_custom_attrs env os m.CustomAttrs
  output_option output_publickey os m.PublicKey
  output_option output_ver os m.Version
  output_option output_locale os m.Locale
  output_string os " } \n"


let output_module_fragment_aux _refs os (ilg: ILGlobals) modul =
  try
    let env = mk_ppenv ilg
    let env = ppenv_enter_modul env
    goutput_tdefs false ([]) env os modul.TypeDefs
    goutput_tdefs true ([]) env os modul.TypeDefs
  with e ->
    output_string os "*** Error during printing : "; output_string os (e.ToString()); os.Flush()
    reraise()

let output_module_fragment os (ilg: ILGlobals) modul =
  let refs = computeILRefs ilg modul
  output_module_fragment_aux refs os ilg modul
  refs

let output_module_refs os refs =
  List.iter (fun  x -> output_assemblyRef os x; output_string os "\n") refs.AssemblyReferences
  List.iter (fun x -> output_modref os x; output_string os "\n") refs.ModuleReferences

let goutput_module_manifest env os modul =
  output_string os " .module "; output_sqstring os modul.Name
  goutput_custom_attrs env os modul.CustomAttrs
  output_string os " .imagebase "; output_i32 os modul.ImageBase
  output_string os " .file alignment "; output_i32 os modul.PhysicalAlignment
  output_string os " .subsystem "; output_i32 os modul.SubSystemFlags
  output_string os " .corflags "; output_i32 os ((if modul.IsILOnly then 0x0001 else 0) ||| (if modul.Is32Bit then 0x0002 else 0) ||| (if modul.Is32BitPreferred then 0x00020003 else 0))
  List.iter (fun r -> goutput_resource env os r) modul.Resources.AsList
  output_string os "\n"
  output_option (goutput_manifest env) os modul.Manifest

let output_module os (ilg: ILGlobals) modul =
  try
    let refs = computeILRefs ilg modul
    let env = mk_ppenv ilg
    let env = ppenv_enter_modul env
    output_module_refs  os refs
    goutput_module_manifest env os modul
    output_module_fragment_aux refs os ilg modul
  with e ->
    output_string os "*** Error during printing : "; output_string os (e.ToString()); os.Flush()
    raise e


#endif




