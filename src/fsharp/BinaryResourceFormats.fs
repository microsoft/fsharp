// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.BinaryResourceFormats

open FSharp.Compiler.IO
open Internal.Utilities
open FSharp.Compiler.AbstractIL.IL

// Helpers for generating binary blobs
module BinaryGenerationUtilities =
    // Little-endian encoding of int32
    let b0 n =  byte (n &&& 0xFF)
    let b1 n =  byte ((n >>> 8) &&& 0xFF)
    let b2 n =  byte ((n >>> 16) &&& 0xFF)
    let b3 n =  byte ((n >>> 24) &&& 0xFF)

    let i16 (i: int32) = [| b0 i; b1 i |]
    let i32 (i: int32) = [| b0 i; b1 i; b2 i; b3 i |]

    // Emit the bytes and pad to a 32-bit alignment
    let Padded initialAlignment (v: byte[]) =
        [| yield! v
           for _ in 1..(4 - (initialAlignment + v.Length) % 4) % 4 do
               yield 0x0uy |]

// Generate nodes in a .res file format. These are then linked by Abstract IL using linkNativeResources
module ResFileFormat =
    open BinaryGenerationUtilities

    let ResFileNode(dwTypeID, dwNameID, wMemFlags, wLangID, data: byte[]) =
        [| yield! i32 data.Length  // DWORD ResHdr.dwDataSize
           yield! i32 0x00000020  // dwHeaderSize
           yield! i32 ((dwTypeID <<< 16) ||| 0x0000FFFF)  // dwTypeID, sizeof(DWORD)
           yield! i32 ((dwNameID <<< 16) ||| 0x0000FFFF)   // dwNameID, sizeof(DWORD)
           yield! i32 0x00000000 // DWORD       dwDataVersion
           yield! i16 wMemFlags // WORD        wMemFlags
           yield! i16 wLangID   // WORD        wLangID
           yield! i32 0x00000000 // DWORD       dwVersion
           yield! i32 0x00000000 // DWORD       dwCharacteristics
           yield! Padded 0 data |]

    let ResFileHeader() = ResFileNode(0x0, 0x0, 0x0, 0x0, [| |])

// Generate the VS_VERSION_INFO structure held in a Win32 Version Resource in a PE file
//
// Web reference: http://www.piclist.com/tecHREF/os/win/api/win32/struc/src/str24_5.htm
module VersionResourceFormat =
    open BinaryGenerationUtilities

    let VersionInfoNode(data: byte[]) =
        [| yield! i16 (data.Length + 2) // wLength : int16 // Specifies the length, in bytes, of the VS_VERSION_INFO structure.
           yield! data |]

    let VersionInfoElement(wType, szKey, valueOpt: byte[] option, children: byte[][], isString) =
        // for String structs, wValueLength represents the word count, not the byte count
        let wValueLength = (match valueOpt with None -> 0 | Some value -> (if isString then value.Length / 2 else value.Length))
        VersionInfoNode
            [| yield! i16 wValueLength // wValueLength: int16. Specifies the length, in words, of the Value member.
               yield! i16 wType        // wType : int16 Specifies the type of data in the version resource.
               yield! Padded 2 szKey
               match valueOpt with
               | None -> yield! []
               | Some value -> yield! Padded 0 value
               for child in children do
                   yield! child  |]

    let Version(version: ILVersionInfo) =
        [| // DWORD dwFileVersionMS
           // Specifies the most significant 32 bits of the file's binary
           // version number. This member is used with dwFileVersionLS to form a 64-bit value used
           // for numeric comparisons.
           yield! i32 (int32 version.Major <<< 16 ||| int32 version.Minor)

           // DWORD dwFileVersionLS
           // Specifies the least significant 32 bits of the file's binary
           // version number. This member is used with dwFileVersionMS to form a 64-bit value used
           // for numeric comparisons.
           yield! i32 (int32 version.Build <<< 16 ||| int32 version.Revision)
        |]

    let String(string, value) =
        let wType = 0x1 // Specifies the type of data in the version resource.
        let szKey = Bytes.stringAsUnicodeNullTerminated string
        VersionInfoElement(wType, szKey, Some (Bytes.stringAsUnicodeNullTerminated value), [| |], true)

    let StringTable(language, strings) =
        let wType = 0x1 // Specifies the type of data in the version resource.
        let szKey = Bytes.stringAsUnicodeNullTerminated language
             // Specifies an 8-digit hexadecimal number stored as a Unicode string.

        let children =
            [| for string in strings do
                   yield String string |]
        VersionInfoElement(wType, szKey, None, children, false)

    let StringFileInfo(stringTables: #seq<string * #seq<string * string> >) =
        let wType = 0x1 // Specifies the type of data in the version resource.
        let szKey = Bytes.stringAsUnicodeNullTerminated "StringFileInfo" // Contains the Unicode string StringFileInfo
        // Contains an array of one or more StringTable structures.
        let children =
            [| for stringTable in stringTables do
                   yield StringTable stringTable |]
        VersionInfoElement(wType, szKey, None, children, false)

    let VarFileInfo(vars: #seq<int32 * int32>) =
        let wType = 0x1 // Specifies the type of data in the version resource.
        let szKey = Bytes.stringAsUnicodeNullTerminated "VarFileInfo" // Contains the Unicode string StringFileInfo
        // Contains an array of one or more StringTable structures.
        let children =
            [| for (lang, codePage) in vars do
                   let szKey = Bytes.stringAsUnicodeNullTerminated "Translation"
                   yield VersionInfoElement(0x0, szKey, Some([| yield! i16 lang
                                                                yield! i16 codePage |]), [| |], false) |]
        VersionInfoElement(wType, szKey, None, children, false)

    let VS_FIXEDFILEINFO(fileVersion: ILVersionInfo,
                         productVersion: ILVersionInfo,
                         dwFileFlagsMask,
                         dwFileFlags, dwFileOS,
                         dwFileType, dwFileSubtype,
                         lwFileDate: int64) =
        let dwStrucVersion = 0x00010000
        [| // DWORD dwSignature // Contains the value 0xFEEFO4BD.
           yield! i32  0xFEEF04BD

           // DWORD dwStrucVersion // Specifies the binary version number of this structure.
           yield! i32 dwStrucVersion

           // DWORD dwFileVersionMS, dwFileVersionLS // Specifies the most/least significant 32 bits of the file's binary version number.
           yield! Version fileVersion

           // DWORD dwProductVersionMS, dwProductVersionLS // Specifies the most/least significant 32 bits of the file's binary version number.
           yield! Version productVersion

           // DWORD dwFileFlagsMask // Contains a bitmask that specifies the valid bits in dwFileFlags.
           yield! i32 dwFileFlagsMask

           // DWORD dwFileFlags // Contains a bitmask that specifies the Boolean attributes of the file.
           yield! i32 dwFileFlags
                  // VS_FF_DEBUG 0x1L     The file contains debugging information or is compiled with debugging features enabled.
                  // VS_FF_INFOINFERRED   The file's version structure was created dynamically; therefore, some of the members
                  //                      in this structure may be empty or incorrect. This flag should never be set in a file's
                  //                      VS_VERSION_INFO data.
                  // VS_FF_PATCHED        The file has been modified and is not identical to the original shipping file of
                  //                      the same version number.
                  // VS_FF_PRERELEASE     The file is a development version, not a commercially released product.
                  // VS_FF_PRIVATEBUILD   The file was not built using standard release procedures. If this flag is
                  //                      set, the StringFileInfo structure should contain a PrivateBuild entry.
                  // VS_FF_SPECIALBUILD   The file was built by the original company using standard release procedures
                  //                      but is a variation of the normal file of the same version number. If this
                  //                      flag is set, the StringFileInfo structure should contain a SpecialBuild entry.

           //Specifies the operating system for which this file was designed. This member can be one of the following values: Flag
           yield! i32 dwFileOS
                  //VOS_DOS 0x0001L  The file was designed for MS-DOS.
                  //VOS_NT  0x0004L  The file was designed for Windows NT.
                  //VOS__WINDOWS16  The file was designed for 16-bit Windows.
                  //VOS__WINDOWS32  The file was designed for the Win32 API.
                  //VOS_OS216 0x00020000L  The file was designed for 16-bit OS/2.
                  //VOS_OS232  0x00030000L  The file was designed for 32-bit OS/2.
                  //VOS__PM16  The file was designed for 16-bit Presentation Manager.
                  //VOS__PM32  The file was designed for 32-bit Presentation Manager.
                  //VOS_UNKNOWN  The operating system for which the file was designed is unknown to Windows.

           // Specifies the general type of file. This member can be one of the following values:
           yield! i32 dwFileType

                //VFT_UNKNOWN The file type is unknown to Windows.
                //VFT_APP  The file contains an application.
                //VFT_DLL  The file contains a dynamic-link library (DLL).
                //VFT_DRV  The file contains a device driver. If dwFileType is VFT_DRV, dwFileSubtype contains a more specific description of the driver.
                //VFT_FONT  The file contains a font. If dwFileType is VFT_FONT, dwFileSubtype contains a more specific description of the font file.
                //VFT_VXD  The file contains a virtual device.
                //VFT_STATIC_LIB  The file contains a static-link library.

           // Specifies the function of the file. The possible values depend on the value of
           // dwFileType. For all values of dwFileType not described in the following list,
           // dwFileSubtype is zero. If dwFileType is VFT_DRV, dwFileSubtype can be one of the following values:
           yield! i32 dwFileSubtype
                //VFT2_UNKNOWN  The driver type is unknown by Windows.
                //VFT2_DRV_COMM  The file contains a communications driver.
                //VFT2_DRV_PRINTER  The file contains a printer driver.
                //VFT2_DRV_KEYBOARD  The file contains a keyboard driver.
                //VFT2_DRV_LANGUAGE  The file contains a language driver.
                //VFT2_DRV_DISPLAY  The file contains a display driver.
                //VFT2_DRV_MOUSE  The file contains a mouse driver.
                //VFT2_DRV_NETWORK  The file contains a network driver.
                //VFT2_DRV_SYSTEM  The file contains a system driver.
                //VFT2_DRV_INSTALLABLE  The file contains an installable driver.
                //VFT2_DRV_SOUND  The file contains a sound driver.
                //
                //If dwFileType is VFT_FONT, dwFileSubtype can be one of the following values:
                //
                //VFT2_UNKNOWN  The font type is unknown by Windows.
                //VFT2_FONT_RASTER  The file contains a raster font.
                //VFT2_FONT_VECTOR  The file contains a vector font.
                //VFT2_FONT_TRUETYPE  The file contains a TrueType font.
                //
                //If dwFileType is VFT_VXD, dwFileSubtype contains the virtual device identifier included in the virtual device control block.

           // Specifies the most significant 32 bits of the file's 64-bit binary creation date and time stamp.
           yield! i32 (int32 (lwFileDate >>> 32))

           //Specifies the least significant 32 bits of the file's 64-bit binary creation date and time stamp.
           yield! i32 (int32 lwFileDate)
         |]

    let VS_VERSION_INFO(fixedFileInfo, stringFileInfo, varFileInfo)  =
        let wType = 0x0
        let szKey = Bytes.stringAsUnicodeNullTerminated "VS_VERSION_INFO" // Contains the Unicode string VS_VERSION_INFO
        let value = VS_FIXEDFILEINFO (fixedFileInfo)
        let children =
            [| yield StringFileInfo stringFileInfo
               yield VarFileInfo varFileInfo
            |]
        VersionInfoElement(wType, szKey, Some value, children, false)

    let VS_VERSION_INFO_RESOURCE data =
        let dwTypeID = 0x0010
        let dwNameID = 0x0001
        let wMemFlags = 0x0030 // REVIEW: HARDWIRED TO ENGLISH
        let wLangID = 0x0
        ResFileFormat.ResFileNode(dwTypeID, dwNameID, wMemFlags, wLangID, VS_VERSION_INFO data)

module ManifestResourceFormat =

    let VS_MANIFEST_RESOURCE(data, isLibrary) =
        let dwTypeID = 0x0018
        let dwNameID = if isLibrary then 0x2 else 0x1
        let wMemFlags = 0x0
        let wLangID = 0x0
        ResFileFormat.ResFileNode(dwTypeID, dwNameID, wMemFlags, wLangID, data)
