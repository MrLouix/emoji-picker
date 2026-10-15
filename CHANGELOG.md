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

### Added (follow-ups)
- Settings window (tray menu or ⚙ in the picker): shortcut capture, theme, language, skin tone,
  color emoji, grid size, position, insertion mode, start with Windows, Ollama endpoint/model;
  changes apply without restarting (hotkey re-registered live)
- Color emoji rendering with Emoji.Wpf (`ui.colorEmoji`, on by default)
- Skin tones: `tones` variants in the database, ✋ button / Ctrl+T in the picker, `ui.skinTone`
- French user interface (picker, tray menu, settings), following `language` / Windows language

### Changed
- Target .NET 8 (LTS); .NET 7 is out of support
- MSI installer now packages the single-file published exe (it previously shipped `EmojiPick.exe` without its DLLs)

### Fixed
- Emoji database field `sequence` was never mapped to `EmojiEntry.Char`: every result was blank and nothing was pasted
- Backspace cleared the whole search instead of deleting one character
- Simulated Ctrl+C was sent while the hotkey modifiers were still held (became Ctrl+Alt+C)
- Stale clipboard content was used as context when nothing was selected; the user's clipboard was overwritten
- Paste could happen before focus returned to the target window
- Overlay positioned in physical pixels instead of DIPs (wrong place on high-DPI screens), could open off-screen
- Duplicate, unqualified and skin-tone variants flooded the results; one-letter tags matched every word
- `ui.gridRows`, `behavior.maxResults`, `behavior.fuzzyThreshold`, `logging.level` and `ui.positionMode` were ignored
- Holding the hotkey re-triggered it (MOD_NOREPEAT); digit hotkeys ("1") mapped to the mouse button
- Running a second instance silently failed to register the hotkey; config was rewritten on every exit
- Ollama answers without U+FE0F (`❤` vs `❤️`) or duplicate candidates were dropped

### Added
- Redesigned overlay: rounded card with shadow, search field with placeholder and clear button,
  hover/selection states, footer preview with emoji name and keyboard hints, fade-in animation
- Dark / light / system themes
- English + French CLDR keywords and names, accent-insensitive search, sentence-aware matching
- Recently used emoji when there is no query
- Context from UI Automation (selection or text before the caret) before falling back to Ctrl+C;
  Ctrl+C is never sent to console windows
- Picker opens next to the text caret when available
- Ollama suggestions re-rank results in the background (with backoff when Ollama is not running)
- Injection modes `paste`, `type` (Unicode keystrokes, clipboard untouched) and `clipboard`; Shift+Enter copies only
- Tray menu: open picker (also left-click), start with Windows, open settings file / logs folder
- Single-instance guard, global exception logging

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
