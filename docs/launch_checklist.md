# First Public Release Checklist

Use this checklist to verify that all code, assets, hygiene, and configuration checks are complete before changing the repository visibility to **Public** on GitHub.

---

## 🧹 1. Repository Clean-up
- [ ] **Loose Scripts**: Verify that all Python refactoring scripts (e.g., `refactor.py`, `wire_dropdown.py`) are moved inside `scripts/refactoring/` and are not cluttering the root folder.
- [ ] **Temporary Outputs**: Delete diagnostic files from the project directory (e.g., delete local `app_output.txt`, `output.txt`, `stdout.txt`, `stderr.txt`).
- [ ] **Build Folders**: Remove any local `bin/` and `obj/` directories to test a completely clean compile:
  ```powershell
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue bin, obj
  ```

---

## 🛡️ 2. Credentials & Security Review
- [ ] **Hardcoded API Keys**: Scan all `.cs` source files for any developer API keys or bearer tokens (specifically checking `LocalAgentChatService.cs` and `AgentConfig.cs`).
- [ ] **Hardcoded File Paths**: Ensure there are no absolute paths referencing developer directories (e.g. searching for `"C:\Users\user\..."` in C# classes).
- [ ] **User Environment Variable Fallbacks**: Verify that API keys and endpoints resolve correctly from Process and User targets rather than expecting hardcoded local credentials.

---

## ⚖️ 3. Licensing & Open Source Compliance
- [ ] **MIT License**: Confirm `LICENSE` exists in the root directory and contains the correct copyright year (2026) and company/contributors tag.
- [ ] **Gitignore Validation**: Verify that `.gitignore` is in the root directory and correctly ignores MSBuild targets, `.vs/` hidden folders, and local configuration `agents.json`.
- [ ] **Code of Conduct & Security policies**: Ensure `CODE_OF_CONDUCT.md` and `SECURITY.md` exist and contain active moderation/reporting email addresses.

---

## 📚 4. Documentation & GitHub Polish
- [ ] **README Hyperlinks**: Test that all local directory reference links in `README.md` (like build script paths) render correctly in GitHub Markdown.
- [ ] **Contribution Flow**: Verify that `CONTRIBUTING.md` accurately describes the build tools, .NET 8 dependency requirements, and PR review process.
- [ ] **Templates Configuration**: Check that issue templates are located in `.github/ISSUE_TEMPLATE/` (`bug_report.md`, `feature_request.md`) and the pull request template is in `.github/` (`PULL_REQUEST_TEMPLATE.md`).
- [ ] **Tags & Topics**: Draft repository metadata:
  * **Description**: *"Interactive physics-driven desktop companions for Windows. Drag, fling, and crawl agents on windows, communicate with multi-agent queue-based group chats, or gather them for autonomous meetings."*
  * **Topics**: `desktop-pet`, `ai-agents`, `physics-simulation`, `dotnet-8`, `winforms`, `multi-agent-system`, `ollama`, `openai-api`, `anthropic`, `custom-sprites`

---

## 📦 5. Final Compilation & Verification
- [ ] **Clean Build Check**: Run a compile in Release configuration to ensure no compiler warnings are treated as breaking errors:
  ```powershell
  dotnet build -c Release
  ```
- [ ] **Run Test**: Execute the generated binary (`bin\Release\net8.0-windows\LilAgentsWindows.exe`), verify it launches without crash prompts, creates a system tray icon, and drops default characters (Nova, Sage, Bolt, Lumi) on the desktop.
- [ ] **Ollama Connectivity**: Run a quick chat test to verify that the app connects to the local Ollama backend.
