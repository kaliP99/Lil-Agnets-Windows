@echo off
cd /d "%~dp0"
set APP=%~dp0bin\Release\net8.0-windows\LilAgentsWindows.exe

if not exist "%APP%" (
  echo App is not built yet. Building now...
  call "%~dp0build.bat"
)

if exist "%APP%" (
  start "" "%APP%"
) else (
  echo App could not be started because the executable was not found.
  pause
)
