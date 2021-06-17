Microsoft (R) F# Compiler version 10.6.0.0 for F# 4.7
Copyright (c) Microsoft Corporation. All Rights Reserved.


		- OUTPUT FILES -
--out:<file>                             Name of the output file (Short form:
                                         -o)
--target:exe                             Build a console executable
--target:winexe                          Build a Windows executable
--target:library                         Build a library (Short form: -a)
--target:module                          Build a module that can be added to
                                         another assembly
--delaysign[+|-]                         Delay-sign the assembly using only
                                         the public portion of the strong
                                         name key
--publicsign[+|-]                        Public-sign the assembly using only
                                         the public portion of the strong
                                         name key, and mark the assembly as
                                         signed
--doc:<file>                             Write the xmldoc of the assembly to
                                         the given file
--keyfile:<file>                         Specify a strong name key file
--platform:<string>                      Limit which platforms this code can
                                         run on: x86, Itanium, x64,
                                         anycpu32bitpreferred, or anycpu. The
                                         default is anycpu.
--nooptimizationdata                     Only include optimization
                                         information essential for
                                         implementing inlined constructs.
                                         Inhibits cross-module inlining but
                                         improves binary compatibility.
--nointerfacedata                        Don't add a resource to the
                                         generated assembly containing
                                         F#-specific metadata
--sig:<file>                             Print the inferred interface of the
                                         assembly to a file
--allsigs                                Print the inferred interfaces of all
                                         compilation files to associated
                                         signature files
--nocopyfsharpcore                       Don't copy FSharp.Core.dll along the
                                         produced binaries


		- INPUT FILES -
--reference:<file>                       Reference an assembly (Short form:
                                         -r)
--compilertool:<file>                    Reference an assembly or directory
                                         containing a design time tool (Short
                                         form: -t)


		- RESOURCES -
--win32icon:<file>                       Specify a Win32 icon file (.ico)
--win32res:<file>                        Specify a Win32 resource file (.res)
--win32manifest:<file>                   Specify a Win32 manifest file
--nowin32manifest                        Do not include the default Win32
                                         manifest
--resource:<resinfo>                     Embed the specified managed resource
--linkresource:<resinfo>                 Link the specified resource to this
                                         assembly where the resinfo format is
                                         <file>[,<string
                                         name>[,public|private]]


		- CODE GENERATION -
--debug[+|-]                             Emit debug information (Short form:
                                         -g)
--debug:{full|pdbonly|portable|embedded} Specify debugging type: full,
                                         portable, embedded, pdbonly. ('full'
                                         is the default if no debuggging type
                                         specified and enables attaching a
                                         debugger to a running program,
                                         'portable' is a cross-platform
                                         format, 'embedded' is a
                                         cross-platform format embedded into
                                         the output file).
--embed[+|-]                             Embed all source files in the
                                         portable PDB file
--embed:<file;...>                       Embed specific source files in the
                                         portable PDB file
--sourcelink:<file>                      Source link information file to
                                         embed in the portable PDB file
--optimize[+|-]                          Enable optimizations (Short form:
                                         -O)
--tailcalls[+|-]                         Enable or disable tailcalls
--deterministic[+|-]                     Produce a deterministic assembly
                                         (including module version GUID and
                                         timestamp)
--pathmap:<path=sourcePath;...>          Maps physical paths to source path
                                         names output by the compiler
--crossoptimize[+|-]                     Enable or disable cross-module
                                         optimizations


		- ERRORS AND WARNINGS -
--warnaserror[+|-]                       Report all warnings as errors
--warnaserror[+|-]:<warn;...>            Report specific warnings as errors
--warn:<n>                               Set a warning level (0-5)
--nowarn:<warn;...>                      Disable specific warning messages
--warnon:<warn;...>                      Enable specific warnings that may be
                                         off by default
--consolecolors[+|-]                     Output warning and error messages in
                                         color


		- LANGUAGE -
--langversion:{?|version|latest|preview} Display the allowed values for
                                         language version, specify language
                                         version such as 'latest' or
                                         'preview'
--checked[+|-]                           Generate overflow checks
--define:<string>                        Define conditional compilation
                                         symbols (Short form: -d)
--mlcompatibility                        Ignore ML compatibility warnings


		- MISCELLANEOUS -
--nologo                                 Suppress compiler copyright message
--version                                Display compiler version banner and
                                         exit
--help                                   Display this usage message (Short
                                         form: -?)
--@<file>                                Read response file for more options


		- ADVANCED -
--codepage:<n>                           Specify the codepage used to read
                                         source files
--utf8output                             Output messages in UTF-8 encoding
--preferreduilang:<string>               Specify the preferred output
                                         language culture name (e.g. es-ES,
                                         ja-JP)
--fullpaths                              Output messages with fully qualified
                                         paths
--lib:<dir;...>                          Specify a directory for the include
                                         path which is used to resolve source
                                         files and assemblies (Short form:
                                         -I)
--simpleresolution                       Resolve assembly references using
                                         directory-based rules rather than
                                         MSBuild resolution
--targetprofile:<string>                 Specify target framework profile of
                                         this assembly. Valid values are
                                         mscorlib, netcore or netstandard.
                                         Default - mscorlib
--baseaddress:<address>                  Base address for the library to be
                                         built
--checksumalgorithm:{SHA1|SHA256}        Specify algorithm for calculating
                                         source file checksum stored in PDB.
                                         Supported values are: SHA1 or SHA256
                                         (default)
--noframework                            Do not reference the default CLI
                                         assemblies by default
--standalone                             Statically link the F# library and
                                         all referenced DLLs that depend on
                                         it into the assembly being generated
--staticlink:<file>                      Statically link the given assembly
                                         and all referenced DLLs that depend
                                         on this assembly. Use an assembly
                                         name e.g. mylib, not a DLL name.
--pdb:<string>                           Name the output debug file
--highentropyva[+|-]                     Enable high-entropy ASLR
--subsystemversion:<string>              Specify subsystem version of this
                                         assembly
--quotations-debug[+|-]                  Emit debug information in quotations
