# 🎯 EmojiPick

**Emoji Overlay Picker for Windows** — Select any emoji from anywhere, powered by fuzzy matching + local LLM intelligence.

---

## Overview

EmojiPick is a lightweight Windows desktop application that lets you instantly find and inject the perfect emoji into any application — Slack, Discord, Gmail, Notepad, you name it.

Press a global hotkey (`Ctrl+Alt+E` by default), and a picker appears next to your text cursor with emoji suggestions for the selected text, ranked by:

- ⚡ **Fuzzy matching** — Instant local search on English + French keywords (Unicode CLDR), accents optional
- 🧠 **LLM re-ranking** — Optional local AI (Ollama) for contextual understanding, applied in the background
- 🔄 **Smart merging** — AI picks first, then the remaining fuzzy matches

## Features

| Feature | Description |
|---------|-------------|
| **Global Hotkey** | Works anywhere, even when app is minimized to tray |
| **Smart Context** | Detects selected text OR reads cursor context from active window |
| **Fuzzy Matching** | Instant local matching against ~1,900 emoji with English + French keywords |
| **LLM Powered** | Optional Ollama re-ranking for contextual suggestions |
| **Offline Ready** | Fully functional without any LLM (fuzzy matching always works) |
| **Overlay UI** | Color emoji, dark/light/system theme, keyboard + mouse navigation, recent emoji |
| **Skin Tones** | Pick a default skin tone, or cycle it with `Ctrl+T` |
| **Injection** | Paste (clipboard restored), type as keystrokes, or copy only |
| **Auto-start** | Optional *Start with Windows* (tray menu or settings) |
| **Configurable** | Settings window (English/French UI), or `config.json` |

## Architecture

```
┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  HotKey      │───▶│  Selection       │───▶│  Emoji Matcher   │
│  (Ctrl+Alt+E)│    │  Handler         │    │  Fuzzy + LLM     │
└──────────────┘    └──────────────────┘    └──────────┬───────┘
                                                       │
                        ┌──────────────────┐    ┌──────▼───────┐
                        │  TrayIcon / UI   │◀───│  Overlay     │
                        │  (System Tray)   │    │  Window      │
                        └────────┬─────────┘    └──────┬───────┘
                                 │                     │
                                 ▼                     ▼
                        ┌──────────────────┐    ┌──────────────┐
                        │  ConfigService   │    │  Output       │
                        │  (%APPDATA%)     │    │  (Ctrl+V)     │
                        └──────────────────┘    └──────────────┘
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language | C# 12 |
| Runtime | .NET 8.0 LTS |
| UI | WPF (transparent, always-on-top overlay) + Emoji.Wpf for color emoji |
| Hotkeys | Windows API `RegisterHotKey` (P/Invoke) |
| Input | `SendInput` API — reliable across applications |
| LLM — Ollama | HTTP REST `localhost:11434` |
| LLM — llama.cpp | *Planned:* LLamaSharp C# bindings + GGUF models (CUDA / ROCm / CPU) |
| Logging | Serilog (file rotation, 7-day retention) |
| Installer | WiX Toolset v4 (MSI, per-machine) |
| Tests | xUnit |
| CI | Build.ps1 (PowerShell) + WiX v4 |

## Quick Start

### Requirements

| To… | You need |
|-----|----------|
| **Use** EmojiPick | Windows 10/11 (x64) and the [.NET 8 **Desktop** Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64) |
| **Build** it | Windows 10/11, the [.NET 8 **SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), Git, PowerShell (7+ recommended) |
| AI suggestions *(optional)* | [Ollama](https://ollama.com) running on `localhost:11434` with a model, e.g. `ollama pull mistral` |

WPF only runs on Windows, so EmojiPick can only be built and run on Windows.
WiX does **not** need to be installed: the WiX v4 SDK is downloaded from NuGet when the installer is built.

### Install

**With the MSI** (from the [releases](https://github.com/MrLouix/emoji-picker/releases), or built with `.\Build.ps1 -Installer`):

1. Run `EmojiPickerSetup.msi`, accept the license and confirm the administrator prompt
   (the MSI installs for all users in `C:\Program Files\EmojiPick\`).
2. Start **EmojiPick** from the Start menu or the desktop shortcut — the icon appears in the notification area.
3. To launch it with Windows, enable **Start with Windows** in the tray menu or in **Settings…**.
4. In any application, select text (or just place the cursor) and press `Ctrl+Alt+E`.

**Without installer**: copy the single-file `EmojiPick.exe` (from a release or from `publish\` after
`.\Build.ps1 -Publish`) anywhere and run it. It still needs the .NET 8 Desktop Runtime.

### Build from Source

```powershell
git clone https://github.com/MrLouix/emoji-picker.git
cd emoji-picker

# Allow the unsigned build script for this PowerShell session only
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

.\Build.ps1                  # Restore + build (Release)
.\Build.ps1 -Publish         # + single-file exe in .\publish\EmojiPick.exe
.\Build.ps1 -Installer       # + publish + MSI (EmojiPickerSetup.msi, path printed at the end)
.\Build.ps1 -Clean -Publish  # Clean first
.\Build.ps1 -Configuration Debug
```

The script logs everything to `build.log`. Each build gets a new version `major.minor.N`: `major.minor` comes
from `VERSION`, `N` goes up by one on every successful build (kept locally in `.build-number`, not committed),
and every build is listed in `build-history.log` with its version and commit. Installing a newer MSI over an
existing EmojiPick updates it. The same steps with `dotnet` directly (version `major.minor.0`):

```powershell
dotnet build EmojiPick.sln -c Release                      # app + tests (the installer is not part of the solution build)
dotnet run --project src\EmojiPick\EmojiPick.csproj        # run from source
dotnet test EmojiPick.Tests\EmojiPick.Tests.csproj         # unit tests

# Installer: publish the single-file exe first, the MSI packages publish\EmojiPick.exe
dotnet publish src\EmojiPick\EmojiPick.csproj -c Release -r win-x64 -o publish
dotnet build EmojiPick.Installer\EmojiPick.wixproj -c Release
```

> Running from source while an installed EmojiPick is running fails with "already running":
> quit the installed one from its tray icon first.

## Configuration

Open **Settings…** from the tray menu (or the ⚙ button in the picker) to change the shortcut, theme,
language, skin tone, color emoji, grid size, insertion mode and Ollama options — changes apply immediately.

Everything is stored in `%APPDATA%\EmojiPick\config.json` (tray menu → *Open settings file*).
Property names are case-insensitive; invalid values are clamped and an unreadable file is backed up to `config.json.bak`.
Restart EmojiPick after editing the file by hand.

```json
{
  "hotkey": { "modifiers": ["Ctrl", "Alt"], "key": "E" },
  "ui": {
    "theme": "system",          // "dark" | "light" | "system"
    "fontSize": 24,
    "windowOpacity": 0.97,
    "gridColumns": 6,
    "gridRows": 3,              // visible rows, more results scroll
    "positionMode": "caret",    // "caret" | "cursor" | "center"
    "colorEmoji": true,         // color rendering via Emoji.Wpf, false = monochrome
    "skinTone": 0               // 0 = default, 1..5 = light → dark
  },
  "behavior": {
    "injectMode": "paste",      // "paste" | "type" (Unicode keystrokes) | "clipboard"
    "restoreClipboard": true,   // put the previous clipboard back after pasting
    "fuzzyThreshold": 0.6,
    "maxResults": 18            // never fewer than gridColumns × gridRows, so the visible grid is full
  },
  "language": "auto",           // UI and emoji-name language ("en", "fr", or "auto" = Windows language)
  "llm": {
    "enabled": true,
    "providers": {
      "ollama": { "enabled": true, "endpoint": "http://localhost:11434", "model": "mistral", "timeoutMs": 3000 }
    }
  },
  "logging": { "level": "info" }
}
```

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| *type* | Search (English or French keywords, accents optional) |
| `↑` `↓` `←` `→` / `Tab` | Navigate emoji grid (wraps around) |
| `PageUp` / `PageDown` | Jump one page of results |
| `Enter` / click | Insert emoji at the cursor |
| `Ctrl+C` | Copy the highlighted emoji, picker stays open |
| `Shift+Enter` / right-click | Copy emoji to clipboard and close |
| `Ctrl+T` / ✋ button | Cycle the skin tone (remembered) |
| `Escape` | Close the picker |
| Hotkey again | Close the picker |

When no text is selected, the picker shows your recently used emoji.

## Project Structure

```
emoji-picker/
├── EmojiPick.sln              # Solution (app + tests; installer excluded from solution builds)
├── Build.ps1                  # Build / publish / MSI script (PowerShell)
├── EmojiPick.Tests/           # xUnit tests
├── EmojiPick.Installer/       # WiX v4 MSI (packages publish\EmojiPick.exe)
│   ├── EmojiPick.wixproj
│   ├── Product.wxs
│   ├── icon.ico
│   └── license.rtf
├── tools/
│   ├── build_emoji_db.py      # Rebuilds the emoji database from Unicode CLDR
│   └── data/                  # Source emoji list used by the script
└── src/EmojiPick/             # WPF application
    ├── Models/                # Config, EmojiEntry, TextContext, Ollama DTOs
    ├── Services/              # Hotkey, clipboard, input, selection, matching, Ollama, tray, settings storage
    ├── Windows/               # OverlayWindow (picker), SettingsWindow
    ├── Helpers/               # P/Invoke, FuzzyMatcher, CaretLocator, Localization, resources
    └── Data/                  # Embedded emoji database and default config
```

## LLM Providers

1. **Fuzzy** — Always available. Instant local matching, no AI, no download.
2. **Ollama** — Optional. When it answers, it re-ranks the suggestions for the selected text
   (✨ badge in the picker). Configure endpoint and model in **Settings…**.
3. **llama.cpp** (LLamaSharp, embedded GGUF models) — *planned, not implemented yet.*

> 💡 **No LLM installed?** No problem. EmojiPick works perfectly with fuzzy matching alone.

## Testing

```powershell
dotnet test EmojiPick.Tests/EmojiPick.Tests.csproj
```

## Emoji Database

`src/EmojiPick/Data/emojis.json.gzip` holds one fully-qualified entry per emoji, enriched with
[Unicode CLDR](https://github.com/unicode-org/cldr-json) keywords and names (English + French).
To regenerate it (e.g. to add a language), download the CLDR annotation files and run:

```bash
python3 tools/build_emoji_db.py <cldr-dir> en fr
```

## Roadmap

- [x] Skin tone support (👍🏻👍🏼👍🏽👍🏾👍🏿)
- [x] Usage history (recently used emoji)
- [x] Configuration UI panel (instead of editing JSON)
- [x] French UI, keywords & names (CLDR) — [ ] DE, ES, JP
- [x] Themes (dark/light/system)
- [x] Color emoji rendering (Emoji.Wpf)
- [ ] Custom emoji collections
- [ ] Statistics dashboard

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

---

**Document Version**: 1.1  
**Status**: 🚧 Pre-release — fuzzy matching, Ollama, settings, skin tones and color emoji implemented; llama.cpp planned
