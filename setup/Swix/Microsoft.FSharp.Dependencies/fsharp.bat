if "%VSCMD_TEST%" NEQ "" goto :test
if "%VSCMD_ARG_CLEAN_ENV%" NEQ "" goto :clean_env

set FSHARPINSTALLDIR=%VSINSTALLDIR%Common7\IDE\CommonExtensions\Microsoft\FSharp\Tools
set "PATH=%FSHARPINSTALLDIR%;%PATH%"

goto :end

:test

set __VSCMD_TEST_FSHARP_STATUS=pass

@REM ******************************************************************
@REM basic environment validation cases go here
@REM ******************************************************************

where fsc.exe
if "%ERRORLEVEL%" NEQ "0" (
    @echo [ERROR:%~nx0] Unable to find F# compiler via 'where fsc.exe'.
    set __VSCMD_TEST_FSHARP_STATUS=fail
)

where fsi.exe
if "%ERRORLEVEL%" NEQ "0" (
    @echo [ERROR:%~nx0] Unable to find F# Interactive via 'where fsi.exe'.
    set __VSCMD_TEST_FSHARP_STATUS=fail
)

@REM return value other than 0 if tests failed.
if "%__VSCMD_TEST_FSHARP_STATUS%" NEQ "pass" (
    set __VSCMD_TEST_FSHARP_STATUS=
    exit /B 1
)

set __VSCMD_TEST_FSHARP_STATUS=
exit /B 0

:clean_env

@REM Script only adds to PATH, no custom action required for -clean_env
@REM vsdevcmd.bat will clean-up this variable.

goto :end
:end

exit /B 0
