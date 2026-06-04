# Visual Assets Plan

This document outlines the visual media strategy for the repository. Having high-quality, animated, and clear visual assets is the single most important factor for non-technical user understanding and overall repository visual appeal.

---

## 🎨 Asset Summary Table

| Asset Name | Type | Size / Aspect Ratio | Placement in README | Purpose |
| ---------- | ---- | ------------------- | ------------------- | ------- |
| `repo_banner.png` | Image | 1200 x 400 (3:1) | Top of README (Hero) | Visual branding & premium feel |
| `physics_fling_crawl.gif` | GIF | 800 x 450 (16:9) | "Killer Features" -> Physics | Demonstrate border crawling & gravity |
| `meeting_mode.gif` | GIF | 800 x 450 (16:9) | "Killer Features" -> Meetings | Demonstrate autonomous circle discussions |
| `drag_drop_context.gif` | GIF | 600 x 400 (3:2) | "Killer Features" -> Context | Demonstrate file parsing & attachments |
| `agent_control_center.png` | Image | 800 x 600 (4:3) | Screenshot Tour -> Config | Showcase customization and model tuning |
| `theme_comparison.png` | Image | 1000 x 500 (2:1) | Screenshot Tour -> Theme | Showcase Light vs Dark UI elegance |

---

## 📸 Capture Instructions & Guidelines

### 1. Repository Banner (`repo_banner.png`)
* **Design**: Gradient background matching the app's default color scheme (dark background `#212121` with a sleek violet-to-blue gradient wave). Add the logo text "Lil' Agents Windows" using a modern font (e.g. *Outfit* or *Inter*) and place 3-4 default character sprites (Nova, Sage, Bolt, Lumi) standing at the bottom.
* **Tools**: Figma or Photoshop.
* **Placement**: Top of `README.md` right after the main header.

### 2. Physics & Crawling demo (`physics_fling_crawl.gif`)
* **Setup**: Open a text editor (e.g. Visual Studio Code or Notepad) and size it to a portion of the screen.
* **Recording Steps**:
  1. Use a screen recorder (like ScreenToGif or OBS) configured for a high-DPI capture.
  2. Left-click and drag an agent (e.g. Nova).
  3. Fling them sideways and let them bounce off the screen boundaries.
  4. Place them near the side of your Notepad window and record them climbing up the border and walking on the title bar.
* **Format**: Save as a highly optimized 30fps GIF (restrict color palette to keep the file size under 5MB).
* **Placement**: README "Key Features" -> "Advanced Window-Border Physics" section.

### 3. Meeting Mode Circle Demo (`meeting_mode.gif`)
* **Setup**: Launch all 4 default agents. Make sure they are visible. Open the `AgentChatForm`.
* **Recording Steps**:
  1. Set the Chat context to a group with all 4 agents.
  2. Press the **Play (Autonomous Mode)** toggle.
  3. Record the screen as all 4 agents walk towards the center, form a rotating circle, and stream thoughts into speech bubbles (e.g., *"How do we design this parser?"*).
* **Format**: Keep duration under 8-10 seconds, export as GIF.
* **Placement**: README "Key Features" -> "Multi-Agent Chats & Autonomous Meetings".

### 4. Drag & Drop Context Demo (`drag_drop_context.gif`)
* **Setup**: Open the Chat interface side-by-side with a local explorer folder.
* **Recording Steps**:
  1. Hold a sample document (e.g., `research.pdf` or an image).
  2. Drag it into the Chat window.
  3. Record the overlay trigger showing the dotted border and the file chip popping into the message box.
  4. Type: *"Summarize this"* and hit send to show the agent processing.
* **Placement**: README "Key Features" -> "Universal Context Attachment".

### 5. Settings & Theme Screenshots (`agent_control_center.png` & `theme_comparison.png`)
* **Setup**: Capture the `AgentManagerForm` with clean desktop settings. Make sure no personal file names or developer logs are visible in the background.
* **For Theme**: Capture the Chat interface side-by-side—one configured in Light Mode and one in Dark Mode.
* **Format**: Clean PNG files (compress using TinyPNG to minimize repo size).
* **Placement**: README "Guided Screenshots Tour" section.
