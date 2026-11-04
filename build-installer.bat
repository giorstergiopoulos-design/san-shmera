@echo off
rem Runs the tests, publishes the app and the Opsis plugin, and compiles the installer with Inno Setup.
rem Result: Output\ChronikologioSetup.exe (+ .sha256)
rem Usage:  build-installer.bat [skip-tests]
rem Needs:  .NET 10 SDK and Inno Setup 6 or 7 (ISCC.exe).
setlocal
cd /d "%~dp0"

if /i not "%~1"=="skip-tests" (
  echo [1/4] Running tests...
  dotnet test Chronikologio.slnx -c Release -nologo -v q
  if errorlevel 1 goto :fail
) else (
  echo [1/4] Tests skipped.
)

echo [2/4] Publishing the app...
if exist dist rmdir /s /q dist
dotnet publish src\Chronikologio.Windows\Chronikologio.Windows.csproj -c Release -o dist\app -nologo -v q
if errorlevel 1 goto :fail

echo [3/4] Building the Opsis plugin...
dotnet build src\Chronikologio.OpsisPlugin\Chronikologio.OpsisPlugin.csproj -c Release -nologo -v q
if errorlevel 1 goto :fail
xcopy /e /i /y /q "src\Chronikologio.OpsisPlugin\bin\Release\net10.0-windows\*" "dist\plugin\" >nul
if errorlevel 1 goto :fail
rem The contract DLL belongs to Opsis (it is loaded from Opsis, never from the plugin folder).
if exist "dist\plugin\OpsisNewsCrawler.Plugin.Abstractions.dll" del "dist\plugin\OpsisNewsCrawler.Plugin.Abstractions.dll"

echo [4/4] Compiling the installer...
set "ISCC="
for %%P in (
  "%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
  "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
  "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
  "%ProgramFiles%\Inno Setup 7\ISCC.exe"
  "%ProgramFiles%\Inno Setup 6\ISCC.exe"
) do if not defined ISCC if exist %%P set "ISCC=%%~P"
if not defined ISCC (
  echo Inno Setup was not found. Install it from https://jrsoftware.org/isdl.php and run this file again.
  goto :fail
)
if not exist Output mkdir Output
"%ISCC%" /Q installer\Chronikologio.iss
if errorlevel 1 goto :fail

powershell -NoProfile -Command "(Get-FileHash 'Output\ChronikologioSetup.exe' -Algorithm SHA256).Hash.ToLower() + ' *ChronikologioSetup.exe' | Set-Content -Encoding ascii 'Output\ChronikologioSetup.exe.sha256'"

echo.
echo Done: Output\ChronikologioSetup.exe
endlocal
exit /b 0

:fail
echo.
echo FAILED. See the messages above.
pause
endlocal
exit /b 1
