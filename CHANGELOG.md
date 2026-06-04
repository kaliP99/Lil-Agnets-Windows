# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-06-04

This release represents the initial public open-source launch of Lil' Agents Windows. It compiles the completed desktop companion features, physics systems, multi-agent frameworks, document parsing engines, and UI/UX modernization overhauls.

### Added
* **Multi-Agent Mentions**: Chat window now supports `@AgentName` suggestions and sequential multi-agent answer queues.
* **Meeting Mode (Formation Dynamics)**: Toggling autonomous conversations now commands active desktop companions to walk, gather, and form a rotating discussion circle in the center of the primary screen.
* **Custom Character Sprite Support**: Load custom image frame loops for different states (walking, thinking, sleeping, handshaking, falling, sitting, excited, stretching).
* **Crawling & Border Physics**: Desktop agents scan active application borders and can climb, walk on title bars, slide off edges, experience gravity, bounce, and recover from hard thuds.
* **Document Text Parsers**: Built-in parsers for `.pdf` (using UglyToad PdfPig), `.docx` (zip XML parsing), and `.xlsx` worksheets.
* **Vision Base64 Encoder**: High-fidelity base64 data encoding for dragging-and-dropping image attachments.
* **Audio Voice Recorder**: Integrated microphone voice input with a real-time amplitude audio wave animation overlay.
* **Fallback Routes**: Per-agent backup API endpoints and models that take over automatically if primary queries fail, utilizing exponential backoff retry routines.
* **Theme Configuration**: Integrated dark/light modes and hex color pickers inside the Agent Control Center.
* **Standard Repo Hygeine**: Created issue templates, pull request checklists, code of conduct, security policies, and `.gitignore`.

### Changed
* **Repository Clean-up**: Restructured directory layout by moving loose Python utility scripts to `scripts/refactoring/`.
* **Dimension Migrations**: Upgraded default configuration forms to support half-tripled layouts (480x630 pixels) for better widescreen UI reading.
