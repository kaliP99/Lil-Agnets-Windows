# Contributing to Lil' Agents Windows

We are thrilled that you are interested in contributing to Lil' Agents Windows! Whether you are fixing a bug, designing a new character animation state, adding support for a new LLM provider, or optimizing the window physics, your help is welcome.

Please take a moment to review this document before starting.

---

## 🛠️ Getting Started

### Prerequisites
To build and debug this project, you will need:
1. **Windows 10 or 11**.
2. **.NET 8.0 SDK** (v8.0.x).
3. A C# IDE:
   * **Visual Studio 2022** (Community Edition works perfectly, ensure the ".NET Desktop Development" workload is selected).
   * **VS Code** (with the C# Dev Kit extension).
   * **JetBrains Rider**.
4. **Ollama** running locally if you plan to test offline capabilities.

### Development Setup Flow
1. **Fork the Repository**: Fork the repository on GitHub and clone your fork locally:
   ```bash
   git clone https://github.com/your-username/lil-agents-windows.git
   cd lil-agents-windows
   ```
2. **Create a Branch**: Create a descriptive feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Build the Application**: Double-click [build.bat](file:///C:/Users/user/Brain/lil-agents-windows/build.bat) or run:
   ```powershell
   dotnet build
   ```
4. **Run in Debug/Release**: Use Visual Studio's Start button or double-click [run.bat](file:///C:/Users/user/Brain/lil-agents-windows/run.bat) to verify everything launches correctly.

---

## 📂 Repository Structure

The project currently uses a flat C# layout in the root folder, making it straightforward to navigate:

* **C# Sources (Root)**:
  * `Program.cs`: Application entry point.
  * `TrayApplicationContext.cs`: Handles system tray icon, context menus, and synchronization of desktop agents.
  * `DesktopAgentForm.cs`: Core form drawing the 2D characters, running the physics loop, gravity calculations, and collision/meeting logic.
  * `AgentChatForm.cs`: Sliding panel containing WebView2, RichTextBox, mention suggestors, file drag-and-drop, and speech audio waveforms.
  * `LocalAgentChatService.cs`: Manages API integrations, prompt building, and fallback retries.
  * `AgentConfig.cs`: Configuration schemas (`AgentDefinition`, `AppConfig`, `AgentGroup`).
  * `ThemeManager.cs`: Color tokens for light and dark themes.
* `scripts/refactoring/`: Collection of internal Python utilities for cleaning duplicates and formatting menus.
* `docs/`: Asset plans, checklists, and template guidelines.
* `LilAgents.csproj`: Project dependencies and metadata.

---

## 🎨 Coding Standards & Guidelines

To maintain code quality and consistency, we ask that you follow these conventions:

### C# Style & Design
* **Block-Scoped Namespaces**: The codebase uses block-scoped namespaces (e.g. `namespace LilAgentsWindows { ... }`). Keep this structure for compatibility.
* **Nullable Reference Types**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Ensure your changes resolve any compiler warnings regarding null safety.
* **Naming Conventions**:
  * **Private Fields**: Prefix private fields with an underscore (e.g., `_agent`, `_isThinking`).
  * **Properties & Methods**: Use `PascalCase` (e.g., `UpdateSize()`, `CharacterName`).
  * **Local Variables**: Use `camelCase` (e.g., `scale`, `targetW`).
* **WinForms Layout**: Do not hardcode size values when designing form elements; base sizes on scaled factors or handle high-DPI scaling gracefully.

### Commits & Git
* Write clear, descriptive commit messages:
  * `feat: add support for local LLaVA vision model image parsing`
  * `fix: prevent character clipping on secondary monitor edges`
  * `docs: update troubleshooting guide in README`
* Keep pull requests focused on a single logical change. If you have multiple unrelated changes, split them into separate pull requests.

---

## 🚀 Submitting a Pull Request (PR)

1. **Verify Your Build**: Ensure the project compiles clean in Release configuration without warning messages:
   ```powershell
   dotnet build -c Release
   ```
2. **Push to Your Fork**:
   ```bash
   git push origin feature/your-feature-name
   ```
3. **Open a PR**: Go to the original repository on GitHub and click "New Pull Request".
4. **Fill Out the Template**: Use the provided Pull Request template to describe the problem, changes, and verification steps.
5. **Review Phase**: A maintainer will review your code. Address any requested changes or review comments.

Thank you for contributing!
