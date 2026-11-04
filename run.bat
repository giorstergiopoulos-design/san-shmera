@echo off
rem Builds and starts Chronikologio (stand-alone app). Needs the .NET 10 SDK.
setlocal
cd /d "%~dp0"
dotnet build src\Chronikologio.Windows\Chronikologio.Windows.csproj -c Release -nologo -v q
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
start "" "src\Chronikologio.Windows\bin\Release\net10.0-windows\Chronikologio.exe"
endlocal
