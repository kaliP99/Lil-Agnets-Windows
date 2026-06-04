@echo off
echo Lil Agents Windows - Local Claude Code / NVIDIA NIM Route Setup
echo.
echo This configures Lil Agents to talk to your local Free Claude Code proxy.
echo Default proxy:
echo http://127.0.0.1:8082/v1/messages
echo.
echo Default model:
echo nvidia_nim/qwen/qwen3-coder-480b-a35b-instruct
echo.

set BASE_URL=http://127.0.0.1:8082/v1/messages
set MODEL=nvidia_nim/qwen/qwen3-coder-480b-a35b-instruct

setx LIL_AGENTS_BASE_URL "%BASE_URL%"
setx LIL_AGENTS_MODEL "%MODEL%"
setx LIL_AGENTS_MAX_TOKENS "700"
set /p AUTH_TOKEN=Enter your Free Claude Code auth token, or press Enter to use freecc: 
if "%AUTH_TOKEN%"=="" set AUTH_TOKEN=freecc
setx LIL_AGENTS_API_KEY "%AUTH_TOKEN%"

echo.
echo Saved:
echo LIL_AGENTS_BASE_URL=%BASE_URL%
echo LIL_AGENTS_MODEL=%MODEL%
echo.
echo IMPORTANT:
echo 1. Make sure Free Claude Code / fcc-claude is running.
echo 2. Close Lil Agents from the tray icon.
echo 3. Open run.bat again.
echo.
pause
