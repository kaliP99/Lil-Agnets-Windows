# 🧍 Lil' Agents Windows 🧍

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue.svg)]()
[![.NET Core](https://img.shields.io/badge/.NET-8.0-violet.svg)](https://dotnet.microsoft.com/download)

Lil' Agents is an interactive, physics-driven desktop companion framework for Windows. It puts small, animated AI agents directly onto your screen. Each agent has its own personality, custom movement, physics thresholds, and LLM routes. They can walk on your windows, talk to you via sleek streaming chat bubbles, or gather together in a circular formation for autonomous discussions.

---

## 🚀 Killer Features

### 🧍 Interactive Desktop Companions
* **State-Driven Behavior**: Agents walk, run, jump, sleep, sit, stretch, yawn, look around, and fly.
* **Sprite Customization**: Import custom image frames (PNG, JPG, BMP, GIF) to design your own characters. Transparent backgrounds work best!
* **Speech Bubbles**: Watch agents think out loud. Standard notifications stream in real-time above their heads.

### 🧗 Advanced Window-Border Physics
* **Border Crawling**: Agents scan open applications on your screen. They can climb up window borders (left/right) or walk across the top title bar.
* **Fling & Drop**: Drag agents and fling them across the screen. They bounce, slide off window edges, and recover from "Thuds" and "Rolls" using physical velocity calculations.
* **Window Pushing**: Let agents push your active windows when running or climbing (configurable per agent).

### 🗣️ Multi-Agent Chats & Autonomous Meetings
* **@Mentions System**: Chat with multiple agents in a single thread. Mention them by typing `@Nova` or `@Sage`, or cue everyone with `@all`.
* **Autonomous Meetings**: Toggle **Meeting Mode** in a group chat, and watch active agents physically gather in the center of your screen to form a slowly rotating circle, discussing topics and reacting to each other in real-time.
* **Social Collisions**: When agents bump into each other on the desktop, they stop to execute a "handshake" animation and exchange pleasantries.

### 📎 Universal Context Attachment
* **Text & Code Files**: Full text parsing using raw stream scanning.
* **Rich Document Parsers**: Automated, high-fidelity text extraction for PDFs (powered by `PdfPig`), Word Documents (`.docx`), and Excel Sheets (`.xlsx`).
* **Visual Vision Input**: Drag and drop images (`.png`, `.jpg`, `.jpeg`, `.gif`) to convert them to base64 data URIs for vision-enabled LLMs (like GPT-4o, Claude 3.5 Sonnet, or Ollama LLaVA/Gemma).

### 🎙️ Voice Input & Waveforms
* **One-Click Recording**: Click the microphone icon to input text via speech-to-text.
* **Interactive Waveform**: High-fidelity, smooth waveform animations show audio amplitudes in real-time while you speak.

### 🔌 Multi-Model Routing & Fallbacks
* **Provider Agnostic**: Connects directly to local Ollama endpoints, OpenAI, Anthropic, NVIDIA NIM, OpenAI-Compatible APIs, or Custom endpoints.
* **Automatic Fallbacks**: Configure a backup route (e.g. OpenAI GPT-3.5) that immediately takes over if your primary route (e.g. local Ollama) fails, with exponential backoff retries.
* **DPI-Aware Native UI**: Fully customizable light/dark themes, accent color HEX code pickers, and screen boundary restrictions (territories).

---

## 📸 Guided Screenshots Tour

Here are the key interfaces you will interact with. *(Placeholders for final screenshots)*

### 1. The Desktop Playroom
`[Placeholder: Desktop View of agents walking on code windows, climbing borders, and colliding for a handshake]`
* **What to capture**: Sage and Nova walking on top of a Visual Studio window, while Bolt is crawling up the side of a browser window, and Lumi is mid-air being flung.

### 2. Streamlined Group Chat
`[Placeholder: Elegant Chat Panel with code blocks, file attachments, and sequential responses]`
* **What to capture**: The `AgentChatForm` showing a group chat with `@Nova` and `@Sage` replying. Include a PDF attachment pill and a rendered markdown table.

### 3. Agent Meeting Mode
`[Placeholder: Physical meeting circle formation in the center of the screen]`
* **What to capture**: Active characters holding a circular formation in the center of the screen, with active speech bubbles streaming their discussion.

### 4. Agent Control Center
`[Placeholder: Setting UI showing provider selection, physics thresholds, and sprite frame selectors]`
* **What to capture**: The `AgentManagerForm` editing Nova. Highlight custom physics properties and the walking/thinking animation frames.

---

## 🛠️ Installation & Setup

### Prerequisites
* **Operating System**: Windows 10 or Windows 11.
* **Framework**: [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
* **AI Backend**: 
  * *Local*: [Ollama](https://ollama.com/) running with `gemma4:latest` (default model).
  * *Cloud*: API keys for OpenAI, Anthropic, or NVIDIA NIM (optional).

### ⚡ Quick Start (No Terminal Required)
1. Clone or download this repository.
2. Double-click [build.bat](file:///C:/Users/user/Brain/lil-agents-windows/build.bat) to build the project.
3. Start your local Ollama instance (or prepare API keys).
4. Double-click [run.bat](file:///C:/Users/user/Brain/lil-agents-windows/run.bat) to launch the app.
5. Look for the little characters walking on your screen and the System Tray Icon (near the Windows clock).

### 💻 Build From Source (Developer Path)
If you prefer building and running through PowerShell:
```powershell
# Navigate to project root
cd C:\[Your Path]\lil-agents-windows

# Build the project in Release configuration
dotnet build -c Release

# Run the executable
.\bin\Release\net8.0-windows\LilAgentsWindows.exe
```

---

## 🎓 Creating Your First Custom Agent

Get up and running with a customized companion in under 3 minutes:

1. **Open the Control Center**: Right-click the tray icon and select **Agent Control Center**.
2. **Add a New Agent**: Click the **+ Create New Agent** button on the bottom left.
3. **Choose an Identity**: Give your agent a name (e.g., `Pixel`), pick a body accent color (e.g., `#FF4488`), and set their size.
4. **Define their Mind**: Provide a system context in the **Personality/Context** box (e.g., *"You are a sarcastic retro game developer. Answer with video game puns."*).
5. **Configure the AI route**:
   * Set **AI Provider** to `OLLAMA`
   * Set **Model** to `gemma4:latest` (or any local model you have installed)
   * Set **Endpoint** to `http://127.0.0.1:11434/api/generate`
6. **Assign custom animations** (Optional): Under "Walking Frames" and "Thinking Frames", click to add local PNG image frames.
7. **Deploy**: Click **Save + Reload Agents**. Your new companion will drop onto your screen!

---

## 📝 Feature Walkthrough

### 💬 Conversing with an Agent
Double-click any character on your desktop to slide in their chat interface. Type a query and press `Enter`. The character will pause walking, transition into their "Thinking" animation, and stream the response directly into the window.

### 👥 Organizing Group Discussions
1. Open the Chat Interface.
2. Click the Agent Selector dropdown in the header and choose **+ Create New Group...**.
3. Group your agents (e.g., *Dev Team* with `Nova` and `Sage`).
4. Type a prompt like: `"@Nova how do we optimize this SQL query? @Sage review the architecture."`
5. Watch them reply sequentially in a structured timeline.

### 🔄 Gathering for a Circular Meeting
1. Select your Group in the Chat window.
2. Click the **Play (Autonomous Mode)** button next to the Send button.
3. The agents will immediately stop roaming, walk to the center of your screen, align into a circle, and start chatting autonomously.
4. Click **Pause** to release them back to roaming.

### 📎 Drag & Drop Context
Drag any text file, image, PDF, DOCX, or Excel spreadsheet from your Windows Explorer directly onto the Chat Window. The app will parse it instantly, show an attachment chip in your input box, and feed the file's content as context to the active agent.

---

## ⚙️ Configuration & Architecture

### File System Paths
Configurations are stored in standard Windows AppData:
* **Agent Settings**: `%APPDATA%\LilAgentsWindows\agents.json`
* **Conversation History**: `%APPDATA%\LilAgentsWindows\chat_history/`

### AI Configuration Providers
* **OLLAMA**: Native `/api/generate` structure. Default endpoint: `http://127.0.0.1:11434/api/generate`.
* **OPENAI**: Standard `/v1/chat/completions` API structure. Supports environment variables (e.g. `%OPENAI_API_KEY%`).
* **ANTHROPIC / NVIDIA NIM**: Native format integrations.
* **CUSTOM**: Custom proxy routing.

---

## ❓ FAQ

#### Q: Does it work fully offline?
**A**: Yes! If you use local providers like Ollama or Local LLaMA proxies, the application does not make any external network requests. All physics, text parsing, and graphics are processed locally.

#### Q: Can agents run on different models?
**A**: Yes! Each agent is fully sandboxed. You can have `Nova` running locally via Ollama, while `Sage` is running cloud-based Claude 3.5 Sonnet, and `Bolt` is routing through an OpenAI compatible server.

#### Q: Can I lock them to a specific monitor?
**A**: Yes. Open the right-click menu on any agent, choose **Movement Mode** -> **Set Territory...**, and drag a bounding box on the target monitor. The agent will never cross outside this box unless you tell them to.

#### Q: How do I quit the application?
**A**: Right-click the system tray icon (near the Windows clock) and click **Exit**.

---

## 🗺️ Roadmap

### Completed (v1.0.0)
* Modernized Flat UI & Theme Manager (Light/Dark profiles, Hex accent colors).
* Multi-agent conversation queues and `@mentions`.
* Fully interactive physics, border climbing, and gravity flings.
* DOCX, XLSX, PDF, and Vision (Base64) file parsers.
* Autonomous meeting mode circle dynamics.

### In Progress
* Native Windows MSI Installer.
* Multi-Monitor configuration page.
* Performance optimizations for WebView2 background handles.

### Planned
* voice synthesis (TTS) so companions can read replies aloud.
* Rich spritesheet support for smoother 2D animations (Aseprite integrations).
* SQLite-backed vector memory database for long-term companion history.

---

## 🤝 Contributing

Contributions are what make the open-source community an amazing place to learn, inspire, and create.
1. Review our [CONTRIBUTING.md](file:///C:/Users/user/Brain/lil-agents-windows/CONTRIBUTING.md) guide.
2. Fork the Project.
3. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
4. Commit your Changes (`git commit -m 'Add some AmazingFeature'`).
5. Push to the Branch (`git push origin feature/AmazingFeature`).
6. Open a Pull Request.

---

## 📄 License

Distributed under the MIT License. See [LICENSE](file:///C:/Users/user/Brain/lil-agents-windows/LICENSE) for more information.
