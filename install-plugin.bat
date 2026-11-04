@echo off
rem Copies the freshly built Opsis plugin (dist\plugin, made by build-installer.bat) into %AppData%\Opsis\plugins\Chronikologio.
rem Opsis locks the plugin DLL while it runs: close Opsis first. The Opsis you use must be built with the plugin contract of 2026-11 (see docs\OPSIS-INTEGRATION.md).
setlocal
set "SRC=%~dp0dist\plugin"
set "DEST=%APPDATA%\Opsis\plugins\Chronikologio"
if not exist "%SRC%\Chronikologio.OpsisPlugin.dll" ( echo dist\plugin not found: run build-installer.bat first. & exit /b 1 )
tasklist /FI "IMAGENAME eq Opsis.exe" 2>nul | find /I "Opsis.exe" >nul && ( echo Opsis is running and locks the plugin. Close Opsis and run this again. & exit /b 2 )
robocopy "%SRC%" "%DEST%" /E /R:2 /W:2 /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Copy failed. & exit /b 3 )
echo Plugin copied to %DEST%. Start Opsis and approve the plugin when it asks.
exit /b 0