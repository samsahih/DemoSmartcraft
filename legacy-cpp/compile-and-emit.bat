@echo off
setlocal
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /EHsc /std:c++17 quote_price.cpp emit_fixtures.cpp /Fe:emit_fixtures.exe
if errorlevel 1 exit /b 1
emit_fixtures.exe > ..\fixtures\quote-cases.json
echo Wrote ..\fixtures\quote-cases.json
