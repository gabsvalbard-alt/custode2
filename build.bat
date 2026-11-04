@echo off
setlocal
echo ========================================================
echo Compilazione Custode delle chiavi 2.0 (.NET Framework 4.8)
echo ========================================================

set MSBUILD="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if not exist %MSBUILD% (
    echo [ERRORE] MSBuild non trovato in %MSBUILD%
    pause
    exit /b 1
)

%MSBUILD% src\Custode2\Custode2.csproj /p:Configuration=Release /v:m

if %ERRORLEVEL% equ 0 (
    echo.
    echo [OK] Compilazione completata con successo!
    echo Eseguibile pronto in: src\Custode2\bin\Release\Custode2.exe
    if not exist "bin" mkdir "bin"
    copy /Y "src\Custode2\bin\Release\Custode2.exe" "bin\Custode2.exe" > nul
    echo Copiato anche in: bin\Custode2.exe
) else (
    echo.
    echo [ERRORE] Compilazione fallita con codice %ERRORLEVEL%
)

echo ========================================================
echo %cmdcmdline% | find /i "%~0" >nul
if not errorlevel 1 pause