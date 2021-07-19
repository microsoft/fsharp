REM == %1 --> assembly

ildasm /TEXT /LINENUM /NOBAR "%~nx1" >"%~n1.il"
IF %ERRORLEVEL% NEQ 0 exit /b 1

echo %~dp0..\..\testenv\bin\ILComparer.exe "%~n1.il.bsl" "%~n1.il"
     %~dp0..\..\testenv\bin\ILComparer.exe "%~n1.il.bsl" "%~n1.il"

IF %ERRORLEVEL% EQU 0 exit /b 0

if /i "%TEST_UPDATE_BSL%" == "1" (
  echo copy /y "%~n1.il" "%~n1.il.bsl"
  copy /y "%~n1.il" "%~n1.il.bsl"
)

exit /b 1

