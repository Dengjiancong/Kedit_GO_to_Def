@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build-Kedit.ps1"
set "build_status=%errorlevel%"
if not "%build_status%"=="0" (
    echo.
    echo Build failed. See the error above.
    if /I "%~1"=="--no-pause" exit /b %build_status%
    pause
    exit /b %build_status%
)
echo.
echo Build complete.
if /I "%~1"=="--no-pause" exit /b 0
pause
