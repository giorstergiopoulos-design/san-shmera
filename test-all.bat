@echo off
rem Runs the tests of Chronikologio and then the test suites of the Opsis apps it plugs into
rem (News Crawler: plugin contract and the "Σαν σήμερα" feed slot; Appearance: Opsis.UI; MotionDesk Studio: the widget host).
rem The Opsis repositories are looked for next to this folder; set OPSIS_ROOT to use another folder.
setlocal
chcp 65001 >nul
set "HERE=%~dp0"
if "%OPSIS_ROOT%"=="" set "OPSIS_ROOT=%HERE%..\"
set FAILED=0

echo === Chronikologio ===
dotnet test "%HERE%Chronikologio.slnx" --nologo -v q || set FAILED=1

call :suite "Opsis News Crawler" "%OPSIS_ROOT%OpsisNewsCrawler\tests\OpsisNewsCrawler.Core.Tests"
call :suite "Opsis Appearance"   "%OPSIS_ROOT%OpsisAppearance\tests\OpsisAppearance.Tests"
call :suite "MotionDesk Studio"  "%OPSIS_ROOT%MotionDeskStudio\tests\Opsis.Tests"

if "%FAILED%"=="0" (echo. & echo ALL SUITES PASSED) else (echo. & echo SOME SUITES FAILED)
exit /b %FAILED%

:suite
if not exist "%~2" ( echo === %~1: not found at %~2, skipped === & exit /b 0 )
echo === %~1 ===
dotnet test "%~2" --nologo -v q || set FAILED=1
exit /b 0
