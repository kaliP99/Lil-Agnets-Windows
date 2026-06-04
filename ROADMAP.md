# Project Roadmap

This roadmap outlines the technical vision and milestones for Lil' Agents Windows. It details completed achievements, items in active development, and future modules open for community collaboration.

---

## ✅ Completed (Milestone v1.0.0 - Open Source Launch)

### 🧍 Animation & Physics Core
* Custom frame loop lists for complex sprite states (Walking, Thinking, Sleeping, Falling, Dragged, Climbing, Stretching, Excited, Handshaking, Sitting, Yawning).
* High-fidelity physics engine with gravity, bouncing, thud recovery, window crawling, title-bar walks, border climbing, and custom velocity flings.
* Desktop agent-to-agent collision handshakes.

### 🗣️ Multi-Agent Dialogues
* Custom group chat management.
* `@mentions` parsing with sequential response queue.
* Physical meeting mode circles (agents dynamically gather in a circle at the center of the screen, facing each other and talking autonomously).

### 📎 Context & Tools
* Vision-aware image encoder for drag-and-drop pictures.
* Full-text extraction for text, Word (.docx), Excel (.xlsx), and PDF (.pdf) documents.
* Audio voice recording via microphone with real-time waveform canvas rendering.
* API service configurations for Ollama, OpenAI, Anthropic, NVIDIA NIM, and Fallback Routing.

---

## 🏗️ In Progress (Short-Term: v1.1.0)

### 📦 Packaging & Installation
* **Native Installer**: Build a standard Wix-based or MSI installer that handles prerequisites, registry setups, and desktop shortcut configuration.
* **Auto-Updater**: Add a simple updater that prompts users when a new release is published on GitHub.

### 💻 Workspace & Multi-Monitor Configuration
* **Monitor Management**: Create a visual "Monitor Mapping Layout" in the Control Center to restrict agents to specific monitors or coordinate bounds.
* **Smart App Dragging**: Refine crawling algorithms so agents can jump between screens when dragging them.

---

## 🔮 Planned (Medium-Term & Long-Term)

### 🔊 Audio Synthesis & Voice Input
* **Local TTS (Text-to-Speech)**: Integrate lightweight local TTS engines (like Piper, Tortoise, or System Speech synthesis) so agents can audibly talk to users or each other.
* **Offline STT (Speech-to-Text)**: Upgrade from standard Windows media recorders to local Whisper models for offline speech input.

### 🧠 SQLite Vector Memory (Long-Term Memory RAG)
* **Local SQLite Store**: Persist chat histories into an SQLite database with semantic memory search.
* **Automatic RAG**: Integrate local embedding models (via Ollama or ONNX) to automatically retrieve past discussions when queried.

### 🎮 Desktop Interactions & Games
* **Agent Tag**: Let agents chase each other or play simple desktop games (like Rock-Paper-Scissors) when idle.
* **Windows Tool Actions**: Allow agents to execute lightweight local powershell tasks or open directories (with user approval).
