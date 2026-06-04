# Non-Technical Setup Guide

This app creates little desktop characters that walk around your Windows screen. Each character represents a different AI agent.

## First: Install .NET

The app needs Microsoft's .NET 8 SDK.

1. Open this link:
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. Download the Windows `.NET 8 SDK`.
3. Install it.
4. Restart your computer.

## Build The App

1. Open this folder:
   `C:\Users\user\Brain\lil-agents-windows`
2. Double-click:
   `build.bat`
3. Wait until it says `Build complete`.

If it says `.NET SDK is not installed`, .NET was not installed correctly or Windows needs a restart.

## Connect It To Ollama Gemma4

Direct chat now uses your local Ollama `gemma4:latest` model by default.

1. Make sure Ollama is running.
2. Make sure `gemma4:latest` is installed in Ollama.
3. Close Lil Agents if it is already running.
4. Open `run.bat` again.

This points Lil Agents to:

`http://127.0.0.1:11434/api/generate`

Using model:

`gemma4:latest`

## Run The App

1. Double-click:
   `run.bat`
2. Small characters should appear on your desktop.
3. They will slowly walk around.

## Ask A Character Something

1. Double-click a character.
2. Type your question.
3. Click `Send`.
4. The character will pause walking and show a thinking animation.
5. The answer will stream into the same chat window.

## Customize Agents

1. Right-click the Lil Agents tray icon near the Windows clock.
2. Click `Agent Control Center`.
3. Select an agent on the left.
4. Change its name, context, color, and animation images.
5. Click `Save + Reload Agents`.

For custom animation:

- Walking images: choose 2-3 images for walking frames.
- Thinking images: choose 2-3 images for thinking frames.
- PNG files with transparent backgrounds work best.

To control where agents walk:

1. Open `Agent Control Center`.
2. Click `Select Area`.
3. Drag a rectangle on your screen.
4. Click `Save + Reload Agents`.

## Current Characters

- Nova: coding and technical work.
- Sage: planning, writing, and analysis.
- Bolt: quick coding help.
- Lumi: creative thinking and broad help.

## If A Character Opens But Does Not Answer

That usually means Ollama is not running, or `gemma4:latest` is not installed.

Start Ollama, then close Lil Agents and open `run.bat` again.

## Make It Start With Windows

1. Build the app first.
2. Press `Windows + R`.
3. Type:
   `shell:startup`
4. Press Enter.
5. Copy a shortcut to this file into that Startup folder:
   `C:\Users\user\Brain\lil-agents-windows\bin\Release\net8.0-windows\LilAgentsWindows.exe`

## Customize A Character

Edit this file:
`C:\Users\user\Brain\lil-agents-windows\TrayApplicationContext.cs`

Look for the `agents` section.

You can change:

- Character name
- AI tool name
- Command it runs
- Its personality/context
- Its color

After editing, run `build.bat` again.
