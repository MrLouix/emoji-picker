# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-10-15

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

### Changed (UI review)
- Picker layout from the UI review: context line ("From selection / Near the cursor “…”"),
  uppercase section titles, pulsing "AI refining…" indicator, dot on emoji added by the AI
  (the highlighted emoji stays highlighted), "No emoji for …" message with a hint, 52px tiles,
  10px corners, tile tooltips, window centered under the mouse pointer in cursor mode
- Keys: Ctrl+C copies the highlighted emoji and keeps the picker open; ←/→ wrap around the grid,
  ↑/↓ wrap within the column; Esc closes directly
- Default grid 6 × 3 with 18 results (existing config.json files keep their values)

### Changed (picker behavior)
- The picker keeps the width of the emoji grid: long emoji names and context are trimmed instead of widening it
- The picker can be moved by dragging any empty area (background, labels, footer)
- With text selected, the emoji is inserted after the selection instead of replacing it
- Browsers: Ctrl+C is tried whenever UI Automation reports no selection, since web editors such as
  Google Docs often report only the caret; the selection was picked up only some of the time
- "Visible rows" now shows more emoji: there are always enough results to fill the visible grid
  (columns × rows, or `behavior.maxResults` if higher). Before, extra rows only spread the same rows
  apart; a short result list now stays at the top
- The emoji grid no longer scrolls by 2px (padding of the default list template)

### Changed (versioning and updates)
- Every `Build.ps1` build gets a new version `major.minor.N`: `major.minor` from `VERSION`, `N` + 1 per
  successful build (local `.build-number`, not committed); builds are listed in `build-history.log`
- The MSI takes its version from `EmojiPick.exe` and installing it over an existing EmojiPick updates it,
  even at the same version; before, every MSI was 1.0.0 and reinstalling added a second EmojiPick

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
