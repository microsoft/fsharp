// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.AbstractIL.StrongNameSign

#nowarn "9"

    open System
    open System.IO
    open System.Collections.Immutable
    open System.Reflection.PortableExecutable
    open System.Security.Cryptography
    open System.Reflection
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices

    open Internal.Utilities
    open Internal.Utilities.Library
    open FSharp.Compiler.IO

    type KeyType =
    | Public
    | KeyPair

    let ALG_TYPE_RSA = int (2 <<< 9)
    let ALG_CLASS_KEY_EXCHANGE = int (5 <<< 13)
    let ALG_CLASS_SIGNATURE = int (1 <<< 13)
    let CALG_RSA_KEYX = int (ALG_CLASS_KEY_EXCHANGE ||| ALG_TYPE_RSA)
    let CALG_RSA_SIGN = int (ALG_CLASS_SIGNATURE ||| ALG_TYPE_RSA)

    let ALG_CLASS_HASH = int (4 <<< 13)
    let ALG_TYPE_ANY = int 0
    let CALG_SHA1 = int (ALG_CLASS_HASH ||| ALG_TYPE_ANY ||| 4)
    let CALG_SHA_256 = int (ALG_CLASS_HASH ||| ALG_TYPE_ANY ||| 12)
    let CALG_SHA_384 = int (ALG_CLASS_HASH ||| ALG_TYPE_ANY ||| 13)
    let CALG_SHA_512 = int (ALG_CLASS_HASH ||| ALG_TYPE_ANY ||| 14)

    let PUBLICKEYBLOB = int 0x6
    let PRIVATEKEYBLOB = int 0x7
    let BLOBHEADER_CURRENT_BVERSION = int 0x2
    let BLOBHEADER_LENGTH = int 20
    let RSA_PUB_MAGIC = int 0x31415352
    let RSA_PRIV_MAGIC = int 0x32415352

    let getResourceString (_, str) = str
    let check _action (hresult) =
      if uint32 hresult >= 0x80000000ul then
        System.Runtime.InteropServices.Marshal.ThrowExceptionForHR hresult

    [<Struct; StructLayout(LayoutKind.Explicit)>]
    type ByteArrayUnion =
        [<FieldOffset(0)>]
        val UnderlyingArray: byte[]

        [<FieldOffset(0)>]
        val ImmutableArray: ImmutableArray<byte>

        new (immutableArray: ImmutableArray<byte>) = { UnderlyingArray = Array.empty<byte>; ImmutableArray = immutableArray}

    let getUnderlyingArray (array: ImmutableArray<byte>) =ByteArrayUnion(array).UnderlyingArray

    // Compute a hash over the elements of an assembly manifest file that should
    // remain static (skip checksum, Authenticode signatures and strong name signature blob)
    let hashAssembly (peReader:PEReader) (hashAlgorithm:IncrementalHash ) =
        // Hash content of all headers
        let peHeaders = peReader.PEHeaders
        let peHeaderOffset = peHeaders.PEHeaderStartOffset

        // Even though some data in OptionalHeader is different for 32 and 64,  this field is the same
        let checkSumOffset = peHeaderOffset + 0x40;                         // offsetof(IMAGE_OPTIONAL_HEADER, CheckSum)
        let securityDirectoryEntryOffset, peHeaderSize =
            match peHeaders.PEHeader.Magic with
            | PEMagic.PE32 ->       peHeaderOffset + 0x80, 0xE0             // offsetof(IMAGE_OPTIONAL_HEADER32, DataDirectory[IMAGE_DIRECTORY_ENTRY_SECURITY]), sizeof(IMAGE_OPTIONAL_HEADER32)
            | PEMagic.PE32Plus ->   peHeaderOffset + 0x90,0xF0              // offsetof(IMAGE_OPTIONAL_HEADER64, DataDirectory[IMAGE_DIRECTORY_ENTRY_SECURITY]), sizeof(IMAGE_OPTIONAL_HEADER64)
            | _ -> raise (BadImageFormatException(getResourceString(FSComp.SR.ilSignInvalidMagicValue())))

        let allHeadersSize = peHeaderOffset + peHeaderSize + int (peHeaders.CoffHeader.NumberOfSections) * 0x28;      // sizeof(IMAGE_SECTION_HEADER)
        let allHeaders =
            let array:byte[] = Array.zeroCreate<byte> allHeadersSize
            peReader.GetEntireImage().GetContent().CopyTo(0, array, 0, allHeadersSize)
            array

        // Clear checksum and security data directory
        for i in 0 .. 3 do allHeaders.[checkSumOffset + i] <- 0uy
        for i in 0 .. 7 do allHeaders.[securityDirectoryEntryOffset + i] <- 0uy
        hashAlgorithm.AppendData(allHeaders, 0, allHeadersSize)

        // Hash content of all sections
        let signatureDirectory = peHeaders.CorHeader.StrongNameSignatureDirectory
        let signatureStart =
            match peHeaders.TryGetDirectoryOffset signatureDirectory with
            | true, value -> value
            | _           -> raise (BadImageFormatException(getResourceString(FSComp.SR.ilSignBadImageFormat())))
        let signatureEnd = signatureStart + signatureDirectory.Size
        let buffer = getUnderlyingArray (peReader.GetEntireImage().GetContent())
        let sectionHeaders = peHeaders.SectionHeaders

        for i in 0 .. (sectionHeaders.Length - 1) do
            let section = sectionHeaders.[i]
            let mutable st = section.PointerToRawData
            let en = st + section.SizeOfRawData

            if st <= signatureStart && signatureStart < en then do
                // The signature should better end within this section as well
                if not ( (st < signatureEnd) && (signatureEnd <= en)) then raise (BadImageFormatException())

                // Signature starts within this section - hash everything up to the signature start
                hashAlgorithm.AppendData(buffer, st, signatureStart - st)

                // Trim what we have written
                st <- signatureEnd

            hashAlgorithm.AppendData(buffer, st, en - st)
            ()
        hashAlgorithm.GetHashAndReset()

    type BlobReader =
        val mutable _blob:byte[]
        val mutable _offset:int
        new (blob:byte[]) = { _blob = blob; _offset = 0; }

        member x.ReadInt32:int =
            let offset = x._offset
            x._offset <- offset + 4
            int (x._blob.[offset]) ||| (int (x._blob.[offset + 1]) <<< 8) ||| (int (x._blob.[offset + 2]) <<< 16) ||| (int (x._blob.[offset + 3]) <<< 24)

        member x.ReadBigInteger (length:int):byte[] =
            let arr:byte[] = Array.zeroCreate<byte> length
            Array.Copy(x._blob, x._offset, arr, 0, length) |> ignore
            x._offset <- x._offset  + length
            arr |> Array.rev

    let RSAParamatersFromBlob (blob:byte[]) keyType =
        let mutable reader = BlobReader blob
        if reader.ReadInt32 <> 0x00000207 && keyType = KeyType.KeyPair then raise (CryptographicException(getResourceString(FSComp.SR.ilSignPrivateKeyExpected())))
        reader.ReadInt32 |>ignore                                                                                                       // ALG_ID
        if reader.ReadInt32 <> RSA_PRIV_MAGIC then raise (CryptographicException(getResourceString(FSComp.SR.ilSignRsaKeyExpected())))  // 'RSA2'
        let byteLen, halfLen =
            let bitLen = reader.ReadInt32
            match bitLen % 16 with
            | 0 -> (bitLen / 8, bitLen / 16)
            | _ -> raise (CryptographicException(getResourceString(FSComp.SR.ilSignInvalidBitLen())))
        let mutable key = RSAParameters()
        key.Exponent <- reader.ReadBigInteger 4
        key.Modulus <- reader.ReadBigInteger byteLen
        key.P <- reader.ReadBigInteger halfLen
        key.Q <- reader.ReadBigInteger halfLen
        key.DP <- reader.ReadBigInteger halfLen
        key.DQ <- reader.ReadBigInteger halfLen
        key.InverseQ <- reader.ReadBigInteger halfLen
        key.D <- reader.ReadBigInteger byteLen
        key

    let toCLRKeyBlob (rsaParameters:RSAParameters) (algId:int) : byte[] =
        let validateRSAField (field:byte[]) expected (name:string) =
            if field <> null && field.Length <> expected then
                raise (CryptographicException(String.Format(getResourceString(FSComp.SR.ilSignInvalidRSAParams()), name)))

        // The original FCall this helper emulates supports other algId's - however, the only algid we need to support is CALG_RSA_KEYX. We will not port the codepaths dealing with other algid's.
        if algId <> CALG_RSA_KEYX then raise (CryptographicException(getResourceString(FSComp.SR.ilSignInvalidAlgId())))

        // Validate the RSA structure first.
        if rsaParameters.Modulus = null then raise (CryptographicException(String.Format(getResourceString(FSComp.SR.ilSignInvalidRSAParams()), "Modulus")))
        if rsaParameters.Exponent = null || rsaParameters.Exponent.Length > 4 then raise (CryptographicException(String.Format(getResourceString(FSComp.SR.ilSignInvalidRSAParams()), "Exponent")))

        let modulusLength = rsaParameters.Modulus.Length
        let halfModulusLength = (modulusLength + 1) / 2

        // We assume that if P != null, then so are Q, DP, DQ, InverseQ and D and indicate KeyPair RSA Parameters
        let isPrivate =
            if rsaParameters.P <> null then
                validateRSAField rsaParameters.P halfModulusLength "P"
                validateRSAField rsaParameters.Q halfModulusLength "Q"
                validateRSAField rsaParameters.DP halfModulusLength "DP"
                validateRSAField rsaParameters.InverseQ halfModulusLength "InverseQ"
                validateRSAField rsaParameters.D halfModulusLength "D"
                true
            else false

        let key =
            use ms = new MemoryStream()
            use bw = new BinaryWriter(ms)

            bw.Write(int CALG_RSA_SIGN)                                                // CLRHeader.aiKeyAlg
            bw.Write(int CALG_SHA1)                                                    // CLRHeader.aiHashAlg
            bw.Write(int (modulusLength + BLOBHEADER_LENGTH))                          // CLRHeader.KeyLength

            // Write out the BLOBHEADER
            bw.Write(byte (if isPrivate = true then PRIVATEKEYBLOB else PUBLICKEYBLOB))// BLOBHEADER.bType
            bw.Write(byte BLOBHEADER_CURRENT_BVERSION)                                 // BLOBHEADER.bVersion
            bw.Write(int16 0)                                                          // BLOBHEADER.wReserved
            bw.Write(int CALG_RSA_SIGN)                                                // BLOBHEADER.aiKeyAlg

            // Write the RSAPubKey header
            bw.Write(int (if isPrivate then RSA_PRIV_MAGIC else RSA_PUB_MAGIC))        // RSAPubKey.magic
            bw.Write(int (modulusLength * 8))                                          // RSAPubKey.bitLen

            let expAsDword =
                let mutable buffer = int 0
                for i in 0 .. rsaParameters.Exponent.Length - 1 do
                   buffer <- (buffer <<< 8) ||| int (rsaParameters.Exponent.[i])
                buffer

            bw.Write expAsDword                                                        // RSAPubKey.pubExp
            bw.Write(rsaParameters.Modulus |> Array.rev)                               // Copy over the modulus for both public and private
            if isPrivate = true then do
                bw.Write(rsaParameters.P  |> Array.rev)
                bw.Write(rsaParameters.Q  |> Array.rev)
                bw.Write(rsaParameters.DP |> Array.rev)
                bw.Write(rsaParameters.DQ |> Array.rev)
                bw.Write(rsaParameters.InverseQ |> Array.rev)
                bw.Write(rsaParameters.D |> Array.rev)

            bw.Flush()
            ms.ToArray()
        key

    let createSignature (hash:byte[]) (keyBlob:byte[]) keyType =
        use rsa = RSA.Create()
        rsa.ImportParameters(RSAParamatersFromBlob keyBlob keyType)
        let signature = rsa.SignHash(hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1)
        signature |>Array.rev

    let patchSignature (stream:Stream) (peReader:PEReader) (signature:byte[]) =
        let peHeaders = peReader.PEHeaders
        let signatureDirectory = peHeaders.CorHeader.StrongNameSignatureDirectory
        let signatureOffset =
            if signatureDirectory.Size > signature.Length then raise (BadImageFormatException(getResourceString(FSComp.SR.ilSignInvalidSignatureSize())))
            match peHeaders.TryGetDirectoryOffset signatureDirectory with
            | false, _ -> raise (BadImageFormatException(getResourceString(FSComp.SR.ilSignNoSignatureDirectory())))
            | true, signatureOffset -> int64 signatureOffset
        stream.Seek(signatureOffset, SeekOrigin.Begin) |>ignore
        stream.Write(signature, 0, signature.Length)

        let corHeaderFlagsOffset = int64(peHeaders.CorHeaderStartOffset + 16)           // offsetof(IMAGE_COR20_HEADER, Flags)
        stream.Seek(corHeaderFlagsOffset, SeekOrigin.Begin) |>ignore
        stream.WriteByte (byte (peHeaders.CorHeader.Flags ||| CorFlags.StrongNameSigned))
        ()

    let signStream stream keyBlob =
        use peReader = new PEReader(stream, PEStreamOptions.PrefetchEntireImage ||| PEStreamOptions.LeaveOpen)
        let hash =
            use hashAlgorithm = IncrementalHash.CreateHash(HashAlgorithmName.SHA1)
            hashAssembly peReader hashAlgorithm
        let signature = createSignature hash keyBlob KeyType.KeyPair
        patchSignature stream peReader signature

    let signFile filename keyBlob =
        use fs = FileSystem.OpenFileForWriteShim(filename, FileMode.Open, FileAccess.ReadWrite)
        signStream fs keyBlob

    let signatureSize (pk:byte[]) =
        if pk.Length < 25 then raise (CryptographicException(getResourceString(FSComp.SR.ilSignInvalidPKBlob())))
        let mutable reader = BlobReader pk
        reader.ReadBigInteger 12 |> ignore                                                     // Skip CLRHeader
        reader.ReadBigInteger 8  |> ignore                                                     // Skip BlobHeader
        let magic = reader.ReadInt32                                                           // Read magic
        if not (magic = RSA_PRIV_MAGIC || magic = RSA_PUB_MAGIC) then                          // RSAPubKey.magic
            raise (CryptographicException(getResourceString(FSComp.SR.ilSignInvalidPKBlob())))
        let x = reader.ReadInt32 / 8
        x

    // Returns a CLR Format Blob public key
    let getPublicKeyForKeyPair keyBlob =
        use rsa = RSA.Create()
        rsa.ImportParameters(RSAParamatersFromBlob keyBlob KeyType.KeyPair)
        let rsaParameters = rsa.ExportParameters false
        toCLRKeyBlob rsaParameters CALG_RSA_KEYX

    // Key signing
    type keyContainerName = string
    type keyPair = byte[]
    type pubkey = byte[]
    type pubkeyOptions = byte[] * bool

    let signerOpenPublicKeyFile filePath = FileSystem.OpenFileForReadShim(filePath).ReadAllBytes()

    let signerOpenKeyPairFile filePath = FileSystem.OpenFileForReadShim(filePath).ReadAllBytes()

    let signerGetPublicKeyForKeyPair (kp: keyPair) : pubkey = getPublicKeyForKeyPair kp

    let signerGetPublicKeyForKeyContainer (_kcName: keyContainerName) : pubkey =
        raise (NotImplementedException("signerGetPublicKeyForKeyContainer is not yet implemented"))

    let signerCloseKeyContainer (_kc: keyContainerName) : unit =
        raise (NotImplementedException("signerCloseKeyContainer is not yet implemented"))

    let signerSignatureSize (pk: pubkey) : int = signatureSize pk

    let signerSignFileWithKeyPair (fileName: string) (kp: keyPair) : unit = signFile fileName kp

    let signerSignFileWithKeyContainer (_fileName: string) (_kcName: keyContainerName) : unit =
        raise (NotImplementedException("signerSignFileWithKeyContainer is not yet implemented"))

#if !FX_NO_CORHOST_SIGNER
    // New mscoree functionality
    // This type represents methods that we don't currently need, so I'm leaving unimplemented
    type UnusedCOMMethod = unit -> unit
    [<System.Security.SecurityCritical; Interface>]
    [<ComImport; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); Guid("D332DB9E-B9B3-4125-8207-A14884F53216")>]
    type ICLRMetaHost =
        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract GetRuntime:
            [<In; MarshalAs(UnmanagedType.LPWStr)>] version: string *
            [<In; MarshalAs(UnmanagedType.LPStruct)>] interfaceId: System.Guid -> [<MarshalAs(UnmanagedType.Interface)>] System.Object

        // Methods that we don't need are stubbed out for now...
        abstract GetVersionFromFile: UnusedCOMMethod
        abstract EnumerateInstalledRuntimes: UnusedCOMMethod
        abstract EnumerateLoadedRuntimes: UnusedCOMMethod
        abstract Reserved01: UnusedCOMMethod

    // We don't currently support ComConversionLoss
    [<System.Security.SecurityCritical; Interface>]
    [<ComImport; ComConversionLoss; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); Guid("9FD93CCF-3280-4391-B3A9-96E1CDE77C8D")>]
    type ICLRStrongName =
        // Methods that we don't need are stubbed out for now...
        abstract GetHashFromAssemblyFile: UnusedCOMMethod
        abstract GetHashFromAssemblyFileW: UnusedCOMMethod
        abstract GetHashFromBlob: UnusedCOMMethod
        abstract GetHashFromFile: UnusedCOMMethod
        abstract GetHashFromFileW: UnusedCOMMethod
        abstract GetHashFromHandle: UnusedCOMMethod
        abstract StrongNameCompareAssemblies: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameFreeBuffer: [<In>] pbMemory: nativeint -> unit

        abstract StrongNameGetBlob: UnusedCOMMethod
        abstract StrongNameGetBlobFromImage: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameGetPublicKey :
                [<In; MarshalAs(UnmanagedType.LPWStr)>] pwzKeyContainer: string *
                [<In; MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2s)>] pbKeyBlob: byte[] *
                [<In; MarshalAs(UnmanagedType.U4)>] cbKeyBlob: uint32 *
                [<Out>] ppbPublicKeyBlob: nativeint byref *
                [<Out; MarshalAs(UnmanagedType.U4)>] pcbPublicKeyBlob: uint32 byref -> unit

        abstract StrongNameHashSize: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameKeyDelete: [<In; MarshalAs(UnmanagedType.LPWStr)>] pwzKeyContainer: string -> unit

        abstract StrongNameKeyGen: UnusedCOMMethod
        abstract StrongNameKeyGenEx: UnusedCOMMethod
        abstract StrongNameKeyInstall: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameSignatureGeneration :
                [<In; MarshalAs(UnmanagedType.LPWStr)>] pwzFilePath: string *
                [<In; MarshalAs(UnmanagedType.LPWStr)>] pwzKeyContainer: string *
                [<In; MarshalAs(UnmanagedType.LPArray, SizeParamIndex=3s)>] pbKeyBlob: byte [] *
                [<In; MarshalAs(UnmanagedType.U4)>] cbKeyBlob: uint32 *
                [<Out>] ppbSignatureBlob: nativeint *
                [<MarshalAs(UnmanagedType.U4)>] pcbSignatureBlob: uint32 byref -> unit

        abstract StrongNameSignatureGenerationEx: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameSignatureSize :
                [<In; MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1s)>] pbPublicKeyBlob: byte[] *
                [<In; MarshalAs(UnmanagedType.U4)>] cbPublicKeyBlob: uint32 *
                [<Out; MarshalAs(UnmanagedType.U4)>] pcbSize: uint32 byref -> unit

        abstract StrongNameSignatureVerification: UnusedCOMMethod

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract StrongNameSignatureVerificationEx :
                [<In; MarshalAs(UnmanagedType.LPWStr)>] pwzFilePath: string *
                [<In; MarshalAs(UnmanagedType.I1)>] fForceVerification: bool *
                [<In; MarshalAs(UnmanagedType.I1)>] pfWasVerified: bool byref -> [<MarshalAs(UnmanagedType.I1)>] bool

        abstract StrongNameSignatureVerificationFromImage: UnusedCOMMethod
        abstract StrongNameTokenFromAssembly: UnusedCOMMethod
        abstract StrongNameTokenFromAssemblyEx: UnusedCOMMethod
        abstract StrongNameTokenFromPublicKey: UnusedCOMMethod


    [<System.Security.SecurityCritical; Interface>]
    [<ComImport; InterfaceType(ComInterfaceType.InterfaceIsIUnknown); Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")>]
    type ICLRRuntimeInfo =
        // REVIEW: Methods that we don't need will be stubbed out for now...
        abstract GetVersionString: unit -> unit
        abstract GetRuntimeDirectory: unit -> unit
        abstract IsLoaded: unit -> unit
        abstract LoadErrorString: unit -> unit
        abstract LoadLibrary: unit -> unit
        abstract GetProcAddress: unit -> unit

        [<MethodImpl(MethodImplOptions.InternalCall, MethodCodeType=MethodCodeType.Runtime)>]
        abstract GetInterface :
            [<In; MarshalAs(UnmanagedType.LPStruct)>] coClassId: System.Guid *
            [<In; MarshalAs(UnmanagedType.LPStruct)>] interfaceId: System.Guid -> [<MarshalAs(UnmanagedType.Interface)>]System.Object

    [<System.Security.SecurityCritical>]
    [<DllImport("mscoree.dll", SetLastError = true, PreserveSig=false, EntryPoint="CreateInterface")>]
    let CreateInterface (
                        ([<MarshalAs(UnmanagedType.LPStruct)>] _clsidguid: System.Guid),
                        ([<MarshalAs(UnmanagedType.LPStruct)>] _guid: System.Guid),
                        ([<MarshalAs(UnmanagedType.Interface)>] _metaHost :
                            ICLRMetaHost byref)) : unit = failwith "CreateInterface"

    let legacySignerOpenPublicKeyFile filePath = FileSystem.OpenFileForReadShim(filePath).ReadAllBytes()

    let legacySignerOpenKeyPairFile filePath = FileSystem.OpenFileForReadShim(filePath).ReadAllBytes()

    let mutable iclrsn: ICLRStrongName option = None
    let getICLRStrongName () =
        match iclrsn with
        | None ->
            let CLSID_CLRStrongName = System.Guid(0xB79B0ACDu, 0xF5CDus, 0x409bus, 0xB5uy, 0xA5uy, 0xA1uy, 0x62uy, 0x44uy, 0x61uy, 0x0Buy, 0x92uy)
            let IID_ICLRStrongName = System.Guid(0x9FD93CCFu, 0x3280us, 0x4391us, 0xB3uy, 0xA9uy, 0x96uy, 0xE1uy, 0xCDuy, 0xE7uy, 0x7Cuy, 0x8Duy)
            let CLSID_CLRMetaHost =  System.Guid(0x9280188Du, 0x0E8Eus, 0x4867us, 0xB3uy, 0x0Cuy, 0x7Fuy, 0xA8uy, 0x38uy, 0x84uy, 0xE8uy, 0xDEuy)
            let IID_ICLRMetaHost = System.Guid(0xD332DB9Eu, 0xB9B3us, 0x4125us, 0x82uy, 0x07uy, 0xA1uy, 0x48uy, 0x84uy, 0xF5uy, 0x32uy, 0x16uy)
            let clrRuntimeInfoGuid = System.Guid(0xBD39D1D2u, 0xBA2Fus, 0x486aus, 0x89uy, 0xB0uy, 0xB4uy, 0xB0uy, 0xCBuy, 0x46uy, 0x68uy, 0x91uy)

            let runtimeVer = System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()
            let mutable metaHost = Unchecked.defaultof<ICLRMetaHost>
            CreateInterface(CLSID_CLRMetaHost, IID_ICLRMetaHost, &metaHost)
            if Unchecked.defaultof<ICLRMetaHost> = metaHost then
                failwith "Unable to obtain ICLRMetaHost object - check freshness of mscoree.dll"
            let runtimeInfo = metaHost.GetRuntime(runtimeVer, clrRuntimeInfoGuid) :?> ICLRRuntimeInfo
            let sn = runtimeInfo.GetInterface(CLSID_CLRStrongName, IID_ICLRStrongName) :?> ICLRStrongName
            if Unchecked.defaultof<ICLRStrongName> = sn then
                failwith "Unable to obtain ICLRStrongName object"
            iclrsn <- Some sn
            sn
        | Some sn -> sn

    let legacySignerGetPublicKeyForKeyPair kp =
     if runningOnMono then
        let snt = System.Type.GetType("Mono.Security.StrongName")
        let sn = System.Activator.CreateInstance(snt, [| box kp |])
        snt.InvokeMember("PublicKey", (BindingFlags.GetProperty ||| BindingFlags.Instance ||| BindingFlags.Public), null, sn, [| |], Globalization.CultureInfo.InvariantCulture) :?> byte[]
     else
        let mutable pSize = 0u
        let mutable pBuffer: nativeint = (nativeint)0
        let iclrSN = getICLRStrongName()

        iclrSN.StrongNameGetPublicKey(Unchecked.defaultof<string>, kp, (uint32) kp.Length, &pBuffer, &pSize) |> ignore
        let mutable keybuffer: byte [] = Bytes.zeroCreate (int pSize)
        // Copy the marshalled data over - we'll have to free this ourselves
        Marshal.Copy(pBuffer, keybuffer, 0, int pSize)
        iclrSN.StrongNameFreeBuffer pBuffer |> ignore
        keybuffer

    let legacySignerGetPublicKeyForKeyContainer kc =
        let mutable pSize = 0u
        let mutable pBuffer: nativeint = (nativeint)0
        let iclrSN = getICLRStrongName()
        iclrSN.StrongNameGetPublicKey(kc, Unchecked.defaultof<byte[]>, 0u, &pBuffer, &pSize) |> ignore
        let mutable keybuffer: byte [] = Bytes.zeroCreate (int pSize)
        // Copy the marshalled data over - we'll have to free this ourselves later
        Marshal.Copy(pBuffer, keybuffer, 0, int pSize)
        iclrSN.StrongNameFreeBuffer pBuffer |> ignore
        keybuffer

    let legacySignerCloseKeyContainer kc =
        let iclrSN = getICLRStrongName()
        iclrSN.StrongNameKeyDelete kc |> ignore

    let legacySignerSignatureSize (pk: byte[]) =
     if runningOnMono then
       if pk.Length > 32 then pk.Length - 32 else 128
     else
        let mutable pSize =  0u
        let iclrSN = getICLRStrongName()
        iclrSN.StrongNameSignatureSize(pk, uint32 pk.Length, &pSize) |> ignore
        int pSize

    let legacySignerSignFileWithKeyPair fileName kp =
     if runningOnMono then
        let snt = System.Type.GetType("Mono.Security.StrongName")
        let sn = System.Activator.CreateInstance(snt, [| box kp |])
        let conv (x: obj) = if (unbox x: bool) then 0 else -1
        snt.InvokeMember("Sign", (BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public), null, sn, [| box fileName |], Globalization.CultureInfo.InvariantCulture) |> conv |> check "Sign"
        snt.InvokeMember("Verify", (BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public), null, sn, [| box fileName |], Globalization.CultureInfo.InvariantCulture) |> conv |> check "Verify"
     else
        let mutable pcb = 0u
        let mutable ppb = (nativeint)0
        let mutable ok = false
        let iclrSN = getICLRStrongName()
        iclrSN.StrongNameSignatureGeneration(fileName, Unchecked.defaultof<string>, kp, uint32 kp.Length, ppb, &pcb) |> ignore
        iclrSN.StrongNameSignatureVerificationEx(fileName, true, &ok) |> ignore

    let legacySignerSignFileWithKeyContainer fileName kcName =
        let mutable pcb = 0u
        let mutable ppb = (nativeint)0
        let mutable ok = false
        let iclrSN = getICLRStrongName()
        iclrSN.StrongNameSignatureGeneration(fileName, kcName, Unchecked.defaultof<byte[]>, 0u, ppb, &pcb) |> ignore
        iclrSN.StrongNameSignatureVerificationEx(fileName, true, &ok) |> ignore
#endif

    let failWithContainerSigningUnsupportedOnThisPlatform() = failwith (FSComp.SR.containerSigningUnsupportedOnThisPlatform() |> snd)

    //---------------------------------------------------------------------
    // Strong name signing
    //---------------------------------------------------------------------
    type ILStrongNameSigner =
        | PublicKeySigner of pubkey
        | PublicKeyOptionsSigner of pubkeyOptions
        | KeyPair of keyPair
        | KeyContainer of keyContainerName

        static member OpenPublicKeyOptions s p = PublicKeyOptionsSigner((signerOpenPublicKeyFile s), p)
        static member OpenPublicKey pubkey = PublicKeySigner pubkey
        static member OpenKeyPairFile s = KeyPair(signerOpenKeyPairFile s)
        static member OpenKeyContainer s = KeyContainer s

        member s.Close () =
            match s with
            | PublicKeySigner _
            | PublicKeyOptionsSigner _
            | KeyPair _ -> ()
            | KeyContainer containerName ->
#if !FX_NO_CORHOST_SIGNER
                legacySignerCloseKeyContainer containerName
#else
                ignore containerName
                failWithContainerSigningUnsupportedOnThisPlatform()
#endif
        member s.IsFullySigned =
            match s with
            | PublicKeySigner _ -> false
            | PublicKeyOptionsSigner pko -> let _, usePublicSign = pko
                                            usePublicSign
            | KeyPair _ -> true
            | KeyContainer _ ->
#if !FX_NO_CORHOST_SIGNER
                true
#else
                failWithContainerSigningUnsupportedOnThisPlatform()
#endif

        member s.PublicKey =
            match s with
            | PublicKeySigner pk -> pk
            | PublicKeyOptionsSigner pko -> let pk, _ = pko
                                            pk
            | KeyPair kp -> signerGetPublicKeyForKeyPair kp
            | KeyContainer containerName ->
#if !FX_NO_CORHOST_SIGNER
                legacySignerGetPublicKeyForKeyContainer containerName
#else
                ignore containerName
                failWithContainerSigningUnsupportedOnThisPlatform()
#endif

        member s.SignatureSize =
            let pkSignatureSize pk =
                try
                    signerSignatureSize pk
                with e ->
                  failwith ("A call to StrongNameSignatureSize failed ("+e.Message+")")
                  0x80
            match s with
            | PublicKeySigner pk -> pkSignatureSize pk
            | PublicKeyOptionsSigner pko -> let pk, _ = pko
                                            pkSignatureSize pk
            | KeyPair kp -> pkSignatureSize (signerGetPublicKeyForKeyPair kp)
            | KeyContainer containerName ->
#if !FX_NO_CORHOST_SIGNER
                pkSignatureSize (legacySignerGetPublicKeyForKeyContainer containerName)
#else
                ignore containerName
                failWithContainerSigningUnsupportedOnThisPlatform()
#endif

        member s.SignFile file =
            match s with
            | PublicKeySigner _ -> ()
            | PublicKeyOptionsSigner _ -> ()
            | KeyPair kp -> signerSignFileWithKeyPair file kp
            | KeyContainer containerName ->
#if !FX_NO_CORHOST_SIGNER
                legacySignerSignFileWithKeyContainer file containerName
#else
                ignore containerName
                failWithContainerSigningUnsupportedOnThisPlatform()
#endif
