@echo off
cd /d "%~dp0"
taskkill /IM LilAgentsWindows.exe /F >nul 2>nul

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK is not installed.
  echo.
  echo Install it from:
  echo https://dotnet.microsoft.com/en-us/download/dotnet/8.0
  echo.
  pause
  exit /b 1
)

dotnet build -c Release
if errorlevel 1 (
  echo.
  echo Build failed. Read the error above.
  pause
  exit /b 1
)

echo.
echo Build complete.
echo App location:
echo %~dp0bin\Release\net8.0-windows\LilAgentsWindows.exe
pause
