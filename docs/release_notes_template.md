# Release Notes Template

This template is a guide for publishing official releases on GitHub. Copy this structure when writing release notes for a new tagged release (e.g., `v1.0.0`, `v1.1.0`).

---

## 🚀 Lil' Agents Windows v[X.Y.Z]

[A catchy one-sentence summary of the focus of this release, e.g., "This release brings high-fidelity window crawling improvements, vision model base64 attachments, and the new physics engine updates."]

### 🌟 Release Highlights
* **[Highlight A]**: [E.g., Added meeting mode dynamics allowing agents to assemble in a circle.]
* **[Highlight B]**: [E.g., Complete UI overhaul featuring dark and light theme templates.]
* **[Highlight C]**: [E.g., Integrated UglyToad PdfPig parser for native PDF reading.]

---

### 📝 What's Changed

#### 🧍 Desktop Animation & Physics Engine
* [Detail 1] (e.g., Fixed boundary issues on multi-monitor configurations)
* [Detail 2] (e.g., Added thud and roll velocity check recoveries)

#### 🗣️ Multi-Agent Dialogues & Services
* [Detail 1] (e.g., Added @mentions listbox auto-complete)
* [Detail 2] (e.g., Refactored OpenAI / Anthropic proxy streaming loops)

#### 🐞 Bug Fixes
* **Fix**: Resolve memory leak caused by WebView2 background threads (#12)
* **Fix**: Correct typo in settings tray menu (#23)

---

### 💾 Installation & Upgrading

#### Quick Setup
1. Download the source zip file below (`Source code.zip`).
2. Extract the folder to a local directory.
3. Double-click `build.bat` (requires .NET 8.0 SDK).
4. Make sure Ollama or cloud credentials are set, and double-click `run.bat`.

#### Developer CLI Upgrade
```powershell
# Pull latest tag
git checkout tags/v[X.Y.Z] -b release-v[X.Y.Z]

# Rebuild
dotnet build -c Release
```

---

### ❤️ Contributor Shoutouts
A huge thank you to everyone who contributed code, assets, or feedback to this release!
* @contributor-username - Added the feature X (#10)
* @contributor-username - Fixed bug Y (#14)
