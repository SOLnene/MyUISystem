@echo off
cd /d "%~dp0"
if not exist "out\CloudSaveServer.exe" (
    echo Publish the server first. See README.md.
    pause
    exit /b 1
)
"out\CloudSaveServer.exe"
pause
