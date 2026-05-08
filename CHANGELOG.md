# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial project scaffolding (Phase 0)
- Solution structure: `EmojiPick/` (WPF app) + `EmojiPick.Installer/` (WiX MSI)
- Data models: `Config`, `EmojiEntry`, `TextContext`, `OllamaModels`
- Configuration service with `%APPDATA%\EmojiPick\config.json` support
- Logging service via Serilog (file rotation, 7-day retention)
- NativeMethods P/Invoke declarations (RegisterHotKey, SendInput, window management)
- OverlayWindow.xaml skeleton (400×300px, transparent, topmost)
- Embedded resources: emoji DB placeholder, default config template
- ILlmMatcher interface for unified LLM provider abstraction
- ResourceLoader for embedded resource access

## [1.0.0] - TBD

*Initial release — not yet tagged.*

### Planned Features
- Global hotkey (`Ctrl+Alt+E`, configurable)
- Emoji overlay picker with keyboard + mouse navigation
- Fuzzy matching (Levenshtein distance on emoji tags)
- LLM integration (Ollama HTTP + llama.cpp embedded)
- Smart context detection (selected text OR cursor context via UI Automation)
- Clipboard-based emoji injection (SendInput Ctrl+V)
- System tray icon with context menu
- Auto-start on Windows login
- MSI installer (WiX Toolset)
