// #NoMT #CompilerOptions #RequiresENU 
//<Expects status="success">section='- INPUT FILES -          ' ! option=use                            kind=OptionString</Expects>
//<Expects status="success">section='- INPUT FILES -          ' ! option=load                           kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=fsi-server                     kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=fsi-server-input-codepage      kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=fsi-server-output-codepage     kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=fsi-server-no-unicode          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=fsi-server-lcid                kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=script.fsx arg1 arg2 ...       kind=OptionGeneral</Expects>
//<Expects status="success">section='NoSection                ' ! option=probeconsole                   kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=peekahead                      kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=noninteractive                 kind=OptionUnit</Expects>
//<Expects status="success">section='- INPUT FILES -          ' ! option=reference                      kind=OptionString</Expects>
//<Expects status="success">section='- CODE GENERATION -      ' ! option=debug                          kind=OptionSwitch</Expects>
//<Expects status="success">section='- CODE GENERATION -      ' ! option=debug                          kind=OptionString</Expects>
//<Expects status="success">section='- CODE GENERATION -      ' ! option=optimize                       kind=OptionSwitch</Expects>
//<Expects status="success">section='- CODE GENERATION -      ' ! option=tailcalls                      kind=OptionSwitch</Expects>
//<Expects status="success">section='- CODE GENERATION -      ' ! option=crossoptimize                  kind=OptionSwitch</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=warnaserror                    kind=OptionSwitch</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=warnaserror                    kind=OptionStringListSwitch</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=warn                           kind=OptionInt</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=nowarn                         kind=OptionStringList</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=warnon                         kind=OptionStringList</Expects>
//<Expects status="success">section='- ERRORS AND WARNINGS -  ' ! option=consolecolors                  kind=OptionSwitch</Expects>
//<Expects status="success">section='- LANGUAGE -             ' ! option=checked                        kind=OptionSwitch</Expects>
//<Expects status="success">section='- LANGUAGE -             ' ! option=define                         kind=OptionString</Expects>
//<Expects status="success">section='- LANGUAGE -             ' ! option=mlcompatibility                kind=OptionUnit</Expects>
//<Expects status="success">section='- MISCELLANEOUS -        ' ! option=nologo                         kind=OptionUnit</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=codepage                       kind=OptionInt</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=utf8output                     kind=OptionUnit</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=fullpaths                      kind=OptionUnit</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=lib                            kind=OptionStringList</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=noframework                    kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=ranges                         kind=OptionSet</Expects>
//<Expects status="success">section='NoSection                ' ! option=terms                          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=termsfile                      kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=stamps                         kind=OptionSet</Expects>
//<Expects status="success">section='NoSection                ' ! option=pause                          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=detuple                        kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=simulateException              kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=stackReserveSize               kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=tlr                            kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=finalSimplify                  kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=parseonly                      kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=typecheckonly                  kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=ast                            kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=tokenize                       kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=testInteractionParser          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=testparsererrorrecovery        kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=inlinethreshold                kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=extraoptimizationloops         kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=maxerrors                      kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=abortonerror                   kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=implicitresolution             kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=resolutions                    kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=resolutionframeworkregistrybase kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=resolutionassemblyfoldersuffix kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=resolutionassemblyfoldersconditions kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=msbuildresolution              kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=alwayscallvirt                 kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=nodebugdata                    kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=test                           kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=validate-type-providers        kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=vserrors                       kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=LCID                           kind=OptionInt</Expects>
//<Expects status="success">section='NoSection                ' ! option=flaterrors                     kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=sqmsessionguid                 kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=gccerrors                      kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=exename                        kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=jit                            kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=localoptimize                  kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=splitting                      kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=versionfile                    kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=times                          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=showextensionresolution        kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=metadataversion                kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=d                              kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=O                              kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=g                              kind=OptionSwitch</Expects>
//<Expects status="success">section='NoSection                ' ! option=i                              kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=r                              kind=OptionString</Expects>
//<Expects status="success">section='NoSection                ' ! option=I                              kind=OptionStringList</Expects>
//<Expects status="success">section='NoSection                ' ! option=light                          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=indentation-syntax             kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=no-indentation-syntax          kind=OptionUnit</Expects>
//<Expects status="success">section='NoSection                ' ! option=dumpAllCommandLineOptions      kind=OptionHelp</Expects>
//<Expects status="success">section='- INPUT FILES -          ' ! option=--                             kind=OptionRest</Expects>
//<Expects status="success">section='- MISCELLANEOUS -        ' ! option=help                           kind=OptionHelp</Expects>
//<Expects status="success">section='NoSection                ' ! option=\?                              kind=OptionHelp</Expects>
//<Expects status="success">section='NoSection                ' ! option=help                           kind=OptionHelp</Expects>
//<Expects status="success">section='NoSection                ' ! option=full-help                      kind=OptionHelp</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=exec                           kind=OptionUnit</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=gui                            kind=OptionSwitch</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=quiet                          kind=OptionUnit</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=readline                       kind=OptionSwitch</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=quotations-debug               kind=OptionSwitch</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=shadowcopyreferences           kind=OptionSwitch</Expects>
//<Expects status="success">section='- ADVANCED -             ' ! option=simpleresolution               kind=OptionUnit</Expects>

// These are for FSC.EXE ONLY:

//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=out                            kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=target                         kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=target                         kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=target                         kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=target                         kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=delaysign                      kind=OptionSwitch</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=doc                            kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=keyfile                        kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=platform                       kind=OptionString</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=nooptimizationdata             kind=OptionUnit</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=nointerfacedata                kind=OptionUnit</Expects>
//<Expects status="notin">section='- OUTPUT FILES -         ' ! option=sig                            kind=OptionString</Expects>
//<Expects status="notin">section='- RESOURCES -            ' ! option=win32res                       kind=OptionString</Expects>
//<Expects status="notin">section='- RESOURCES -            ' ! option=win32manifest                  kind=OptionString</Expects>
//<Expects status="notin">section='- RESOURCES -            ' ! option=nowin32manifest                kind=OptionUnit</Expects>
//<Expects status="notin">section='- RESOURCES -            ' ! option=resource                       kind=OptionString</Expects>
//<Expects status="notin">section='- RESOURCES -            ' ! option=linkresource                   kind=OptionString</Expects>
//<Expects status="notin">section='- ADVANCED -             ' ! option=baseaddress                    kind=OptionString</Expects>
//<Expects status="notin">section='- ADVANCED -             ' ! option=noframework                    kind=OptionUnit</Expects>
//<Expects status="notin">section='- ADVANCED -             ' ! option=standalone                     kind=OptionUnit</Expects>
//<Expects status="notin">section='- ADVANCED -             ' ! option=staticlink                     kind=OptionString</Expects>
//<Expects status="notin">section='- ADVANCED -             ' ! option=pdb                            kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=generatehtml                   kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=htmloutputdir                  kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=htmlcss                        kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=htmlnamespacefile              kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=htmlnamespacefileappend        kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=o                              kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=a                              kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=cliroot                        kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=jit-optimize                   kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-jit-optimize                kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=jit-tracking                   kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-jit-tracking                kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=progress                       kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=compiling-fslib                kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=compiling-fslib-20     kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=version                        kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=local-optimize                 kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-local-optimize              kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=cross-optimize                 kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-cross-optimize              kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-string-interning            kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=statistics                     kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=generate-filter-blocks         kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=gccerrors                      kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=exename                        kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=max-errors                     kind=OptionInt</Expects>
//<Expects status="notin">section='NoSection                ' ! option=debug-file                     kind=OptionString</Expects>
//<Expects status="notin">section='NoSection                ' ! option=no-debug-file                  kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=Ooff                           kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=ml-keywords                    kind=OptionUnit</Expects>
//<Expects status="notin">section='NoSection                ' ! option=gnu-style-errors               kind=OptionUnit</Expects>


exit 0;;
