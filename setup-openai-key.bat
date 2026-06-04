@echo off
echo Lil Agents Windows - OpenAI API Key Setup
echo.
echo You need an OpenAI API key for direct in-window chat.
echo Get one from:
echo https://platform.openai.com/api-keys
echo.
set /p OPENAI_KEY=Paste your OpenAI API key here, then press Enter: 

if "%OPENAI_KEY%"=="" (
  echo.
  echo No key entered. Nothing was saved.
  pause
  exit /b 1
)

setx OPENAI_API_KEY "%OPENAI_KEY%"
echo.
echo Saved OPENAI_API_KEY for your Windows user.
echo.
echo IMPORTANT:
echo Close Lil Agents if it is running, then open run.bat again.
echo If it still says the key is missing, restart Windows once.
echo.
pause
