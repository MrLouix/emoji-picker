# 🎯 EmojiPick

**Emoji Overlay Picker for Windows** — Select any emoji from anywhere, powered by fuzzy matching + local LLM intelligence.

---

## Overview

EmojiPick is a lightweight Windows desktop application that lets you instantly find and inject the perfect emoji into any application — Slack, Discord, Gmail, Notepad, you name it.

Press a global hotkey (`Ctrl+Alt+E` by default), and a transparent overlay appears at your cursor with contextually relevant emoji suggestions, ranked by:

- ⚡ **Fuzzy matching** — Instant local Levenshtein distance on emoji tags (<500ms)
- 🧠 **LLM matching** — Optional local AI (Ollama / llama.cpp) for contextual understanding (1-3s async)
- 🔄 **Smart merging** — Both results deduplicated and scored for the best suggestions

## Features

| Feature | Description |
|---------|-------------|
| **Global Hotkey** | Works anywhere, even when app is minimized to tray |
| **Smart Context** | Detects selected text OR reads cursor context from active window |
| **Fuzzy Matching** | Instant local matching against 1500+ emoji database |
| **LLM Powered** | Ollama or llama.cpp for contextual emoji suggestions |
| **Offline Ready** | Fully functional without any LLM (fuzzy fallback always works) |
| **Overlay UI** | 400×300px transparent grid with keyboard + mouse navigation |
| **Clipboard Injection** | Paste emoji directly into any application |
| **Auto-start** | Optional Windows startup registration |
| **Configurable** | Hotkey, LLM providers, injection mode, theme, grid size — all via JSON |

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
| Runtime | .NET 7.0 LTS |
| UI | WPF (transparent, always-on-top overlay) |
| Hotkeys | Windows API `RegisterHotKey` (P/Invoke) |
| Input | `SendInput` API — reliable across applications |
| LLM — Ollama | HTTP REST `localhost:11434` |
| LLM — llama.cpp | LLamaSharp C# bindings + GGUF models |
| GPU Support | CUDA (NVIDIA) / ROCm (AMD) / CPU-only |
| Logging | Serilog (file rotation, 7-day retention) |
| Installer | WiX Toolset v4 (MSI, per-user) |
| Tests | xUnit |
| CI | Build.ps1 (PowerShell) + WiX v4 |

## Quick Start

### Prerequisites

- **Windows 10/11**
- **.NET 7.0 Runtime** ([download](https://dotnet.microsoft.com/en-us/download/dotnet/7.0))
- *(Optional)* **Ollama** running on `localhost:11434` with a model (e.g. `mistral`)
- *(Optional)* **WiX Toolset v4** for building the MSI — `dotnet tool install --global wix`

### Install

1. Download the latest [MSI installer](https://github.com/MrLouix/emoji-picker/releases) or standalone `.exe`
2. Run the installer — follow the wizard
3. EmojiPick starts automatically — icon appears in system tray
4. Go to any application, select text (or place cursor), press `Ctrl+Alt+E`

### Build from Source

```powershell
# Clone the repository
git clone https://github.com/MrLouix/emoji-picker.git
cd emoji-picker

# Build (recommended — uses Build.ps1)
.\Build.ps1                  # Build in Release mode
.\Build.ps1 -Publish         # Build + publish to ./publish/
.\Build.ps1 -Clean           # Clean before building
.\Build.ps1 -Clean -Publish  # Clean + rebuild + publish

# Or with dotnet directly
dotnet restore EmojiPick.sln
dotnet build EmojiPick.sln -c Release

# Run (Windows only)
dotnet run --project src/EmojiPick/EmojiPick.csproj

# Publish standalone executable
dotnet publish src/EmojiPick/EmojiPick.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

### Build MSI Installer

```powershell
# Requires WiX Toolset v4 SDK (included as NuGet package)
dotnet build EmojiPick.Installer/EmojiPick.wixproj -c Release
```

This produces `EmojiPickerSetup.msi` in the `EmojiPick.Installer/bin/Release/` folder.

## Configuration

User configuration is stored at `%APPDATA%\EmojiPick\config.json`:

```json
{
  "hotkey": {
    "modifiers": ["Ctrl", "Alt"],
    "key": "E"
  },
  "llm": {
    "enabled": true,
    "provider": "auto",
    "fallback_chain": ["ollama", "llamacpp", "fuzzy"],
    "providers": {
      "ollama": {
        "endpoint": "http://localhost:11434",
        "model": "mistral",
        "timeout_ms": 3000
      },
      "llamacpp": {
        "model_path": "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
        "use_gpu": true,
        "gpu_layers": 20
      }
    }
  }
}
```

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `↑` `↓` `←` `→` | Navigate emoji grid |
| `Enter` | Select & inject emoji |
| `Escape` | Close overlay |
| `Ctrl+C` | Copy selected emoji to clipboard |
| `Backspace` | Clear search input |

## Project Structure

```
emoji-overlay/
├── EmojiPick.sln              # Solution file (root)
├── EmojiPick.Tests/           # xUnit test project
├── EmojiPick.Installer/       # WiX v4 MSI installer
│   ├── EmojiPick.wixproj
│   ├── Product.wxs
│   ├── icon.ico
│   └── license.rtf
├── Build.ps1                  # Build & publish script (PowerShell)
├── build.log                  # Build log (not committed)
├── src/
│   └── EmojiPick/             # Main WPF application
│       ├── Models/            # Data models (Config, EmojiEntry, OllamaModels)
│       ├── Services/          # Business logic (HotKey, Clipboard, Matching, LLM)
│       ├── Windows/           # WPF UI (OverlayWindow, TrayIcon)
│       ├── Helpers/           # Utilities (P/Invoke, FuzzyMatcher, Resources)
│       └── Data/              # Embedded resources (emoji DB, default config)
└── docs/                      # Documentation (spec, plan)
```

## LLM Providers

EmojiPick supports a **fallback chain** — it tries providers in order until one succeeds:

1. **Ollama** — External service, great if already running. Fast with models like `mistral` (7B).
2. **llama.cpp** — Embedded inference via LLamaSharp. Zero external dependencies. Downloads GGUF model on first use.
3. **Fuzzy** — Always available. Instant local matching. No AI, no downloads.

> 💡 **No LLM installed?** No problem. EmojiPick works perfectly with fuzzy matching alone.

## Testing

```powershell
dotnet test EmojiPick.Tests/EmojiPick.Tests.csproj
```

## Roadmap

- [ ] Skin tone support (👍🏻👍🏼👍🏽👍🏾👍🏿)
- [ ] Usage history & frequently used emoji
- [ ] Configuration UI panel (instead of editing JSON)
- [ ] Multi-language support (FR, DE, ES, JP)
- [ ] Themes (dark/light)
- [ ] Custom emoji collections
- [ ] Statistics dashboard

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

---

**Document Version**: 1.1  
**Status**: 🚧 Initial development — Phase 0 complete
